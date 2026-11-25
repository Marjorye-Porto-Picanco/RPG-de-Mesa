using System.Collections.Concurrent;
using System.Text.Json;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Magias;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Sessoes;
using RPGdeMesa.Domain.ValueObjects;

namespace RPGdeMesa.Application.Services;

public partial class SessaoService
{
    private static readonly TimeSpan ValidadeCacheCampanha = TimeSpan.FromSeconds(60);

    private readonly ISessaoRepository _sessaoRepo;
    private readonly IPersonagemRepository _personagemRepo;
    private readonly ConcurrentDictionary<int, (Campanha Campanha, DateTime AtualizadoEmUtc)> _cacheCampanhas = new();
    private List<Magia>? _cacheMagiasCatalogo;
    private DateTime _cacheMagiasAtualizadoEmUtc = DateTime.MinValue;

    public SessaoService(
        ISessaoRepository sessaoRepo,
        IPersonagemRepository personagemRepo)
    {
        _sessaoRepo = sessaoRepo;
        _personagemRepo = personagemRepo;
    }

    public void InvalidarCacheCampanha(int idCampanha) =>
        _cacheCampanhas.TryRemove(idCampanha, out _);

    private async Task<Campanha?> ObterCampanhaComCacheAsync(int idCampanha, bool forcarAtualizacao = false,
        CancellationToken cancellationToken = default)
    {
        if (!forcarAtualizacao &&
            _cacheCampanhas.TryGetValue(idCampanha, out var entrada) &&
            (DateTime.UtcNow - entrada.AtualizadoEmUtc) < ValidadeCacheCampanha)
        {
            return entrada.Campanha;
        }

        var campanha = await _sessaoRepo.ObterCampanhaParaSessaoAsync(idCampanha, cancellationToken);
        if (campanha != null)
        {
            _cacheCampanhas[idCampanha] = (campanha, DateTime.UtcNow);
        }

        return campanha;
    }

    private async Task<List<Magia>> ObterMagiasCatalogoComCacheAsync(bool forcarAtualizacao = false,
        CancellationToken cancellationToken = default)
    {
        if (!forcarAtualizacao &&
            _cacheMagiasCatalogo != null &&
            (DateTime.UtcNow - _cacheMagiasAtualizadoEmUtc) < ValidadeCacheCampanha)
        {
            return _cacheMagiasCatalogo;
        }

        var magias = await _sessaoRepo.ListarMagiasCatalogoAsync(cancellationToken);
        _cacheMagiasCatalogo = magias;
        _cacheMagiasAtualizadoEmUtc = DateTime.UtcNow;
        return magias;
    }

    public Task<bool> VerificarSessaoAtivaAsync(int idCampanha) =>
        _sessaoRepo.SessaoEstaAtivaAsync(idCampanha);

    public async Task<(bool Sucesso, string Mensagem, SessaoEstadoDto? Estado)> EntrarNaSessaoAsync(
        int idCampanha,
        int idAccount,
        string nomeUsuario,
        int? idPersonagemSelecionado = null)
    {
        try
        {
            var campanha = await ObterCampanhaComCacheAsync(idCampanha, forcarAtualizacao: true);
            if (campanha == null)
                return (false, "Campanha não encontrada.", null);

            bool ehMestre = campanha.IdAccount == idAccount;
            bool participa = ehMestre
                || campanha.UsuarioCampanhas.Any(uc => uc.IdAccount == idAccount)
                || campanha.PersonagemCampanhas.Any(pc => pc.Personagem != null && pc.Personagem.IdAccount == idAccount);

            if (!participa)
                return (false, "Você não tem permissão para entrar nesta sessão.", null);

            SessaoMesa? sessao;
            if (ehMestre)
            {
                int? idMapaInicial = campanha.MapaCampanhas
                    .Select(mc => (int?)mc.IdMapa)
                    .FirstOrDefault();

                sessao = await _sessaoRepo.AbrirOuReabrirSessaoAsync(idCampanha, idMapaInicial);
            }
            else
            {
                sessao = await _sessaoRepo.ObterSessaoPorCampanhaAsync(idCampanha);
                if (sessao == null || !sessao.Ativa)
                {
                    return (false, "Nenhuma sessão ativa no momento. Aguarde o Mestre abrir a mesa.", null);
                }
            }

            int? idPersonagemJogador = null;
            if (!ehMestre)
            {
                var fichasAceitasConta = campanha.FichasAceitas
                    .Where(pc => pc.Personagem != null && pc.Personagem.IdAccount == idAccount)
                    .ToList();

                if (idPersonagemSelecionado.HasValue &&
                    fichasAceitasConta.Any(pc => pc.IdPersonagem == idPersonagemSelecionado.Value))
                {
                    idPersonagemJogador = idPersonagemSelecionado.Value;
                }
                else
                {
                    idPersonagemJogador = fichasAceitasConta
                        .Select(pc => (int?)pc.IdPersonagem)
                        .FirstOrDefault();
                }

                if (!idPersonagemJogador.HasValue)
                    return (false, "Selecione uma ficha aprovada pelo Mestre antes de conectar na sessão.", null);
            }

            bool precisaAnunciarEntrada = await _sessaoRepo.ConectarParticipanteAsync(
                sessao.IdSessao, idAccount, idPersonagemJogador);

            if (precisaAnunciarEntrada)
            {
                string apelido = string.IsNullOrWhiteSpace(nomeUsuario) ? $"Usuário #{idAccount}" : nomeUsuario;
                string textoEntrada = ehMestre
                    ? $"{apelido} (Mestre) abriu a Sessão #{sessao.NumeroSessao}."
                    : $"{apelido} conectou-se à sessão.";

                await _sessaoRepo.AdicionarMensagemAsync(new SessaoMensagem(
                    sessao.IdSessao,
                    idAccount: null,
                    CanaisSessao.Chat,
                    textoEntrada));
            }

            var estado = await MontarEstadoSessaoAsync(campanha, sessao.IdSessao, idAccount);
            return (true, "Conectado à sessão.", estado);
        }
        catch (DomainException dex)
        {
            return (false, dex.Message, null);
        }
        catch (Exception ex)
        {
            string detalhe = ex.InnerException?.Message ?? ex.Message;
            return (false, $"Erro ao entrar na sessão: {detalhe}", null);
        }
    }

    public async Task<SessaoEstadoDto?> SincronizarSessaoAsync(int idCampanha, int idSessao, int idAccount,
        CancellationToken cancellationToken = default)
    {
        var tarefaSessao = _sessaoRepo.ObterSessaoPorIdAsync(idSessao, cancellationToken);
        var tarefaCampanha = ObterCampanhaComCacheAsync(idCampanha, cancellationToken: cancellationToken);

        await Task.WhenAll(tarefaSessao, tarefaCampanha);

        var sessaoAtual = tarefaSessao.Result;
        var campanha = tarefaCampanha.Result;

        if (sessaoAtual != null && sessaoAtual.Ativa)
        {
            await _sessaoRepo.AtualizarHeartbeatAsync(idSessao, idAccount, cancellationToken);
        }

        if (campanha == null) return null;

        return await MontarEstadoSessaoAsync(campanha, idSessao, idAccount, cancellationToken);
    }

    public async Task<(SessaoEstadoDto? Estado, SessaoSinal? Sinal)> SincronizarMudancasAsync(
        SessaoEstadoDto atual, SessaoSinal? anterior, int idAccount,
        bool registrarPresenca, bool reconciliar, CancellationToken cancellationToken)
    {
        if (registrarPresenca && atual.SessaoAtiva)
            await _sessaoRepo.AtualizarHeartbeatAsync(atual.IdSessao, idAccount, cancellationToken);

        var sinal = await _sessaoRepo.LerSinalSessaoAsync(atual.IdSessao, cancellationToken);
        if (sinal is null)
            return (atual.SessaoAtiva ? atual.ComSessaoEncerrada() : null, null);

        if (!reconciliar && anterior is not null && sinal.TemMesmoTabuleiro(anterior))
        {
            if (sinal.UltimaMensagem == anterior.UltimaMensagem)
                return (null, sinal);

            // Relê uma janela curta para incluir mensagens gravadas fora da ordem de IDs.
            int desdeId = atual.Mensagens.Select(m => m.IdMensagem).OrderDescending().Take(16)
                .DefaultIfEmpty(0).Min();
            var recentes = await _sessaoRepo.ListarMensagensAsync(atual.IdSessao,
                desdeId: desdeId, cancellationToken: cancellationToken);
            var idsAtuais = atual.Mensagens.Select(m => m.IdMensagem).ToHashSet();
            var novas = recentes.Where(m => !idsAtuais.Contains(m.IdMensagem)).Select(ParaMensagemDto).ToList();
            if (novas.Count == 0)
                return (null, sinal);

            var mensagens = atual.Mensagens.Concat(novas)
                .OrderByDescending(m => m.DataEnvioLocal.ToUniversalTime()).ThenByDescending(m => m.IdMensagem)
                .Take(320).Reverse().ToList();
            return (atual.ComMensagens(mensagens), sinal);
        }

        var campanha = await ObterCampanhaComCacheAsync(atual.IdCampanha, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (campanha is null)
            return (atual.SessaoAtiva ? atual.ComSessaoEncerrada() : null, null);

        var estado = await MontarEstadoSessaoAsync(campanha, atual.IdSessao, idAccount, cancellationToken);
        return (estado, sinal);
    }

    public async Task SairDaSessaoAsync(int idSessao, int idAccount, string nomeUsuario)
    {
        try
        {
            bool estavaConectado = await _sessaoRepo.DesconectarParticipanteAsync(idSessao, idAccount);
            if (estavaConectado)
            {
                string apelido = string.IsNullOrWhiteSpace(nomeUsuario) ? $"Usuário #{idAccount}" : nomeUsuario;
                await _sessaoRepo.AdicionarMensagemAsync(new SessaoMensagem(
                    idSessao,
                    idAccount: null,
                    CanaisSessao.Chat,
                    $"{apelido} desconectou-se da sessão."));
            }
        }
        catch
        {
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> FinalizarSessaoAsync(
        int idCampanha,
        int idSessao,
        int idAccountMestre,
        string nomeMestre)
    {
        try
        {
            var campanha = await ObterCampanhaComCacheAsync(idCampanha);
            if (campanha == null)
                return (false, "Campanha não encontrada.");

            if (campanha.IdAccount != idAccountMestre)
                return (false, "Apenas o Mestre pode finalizar a sessão.");

            await _sessaoRepo.FinalizarSessaoAsync(idSessao);
            InvalidarCacheCampanha(idCampanha);

            string apelido = string.IsNullOrWhiteSpace(nomeMestre) ? "O Mestre" : nomeMestre;
            await _sessaoRepo.AdicionarMensagemAsync(new SessaoMensagem(
                idSessao,
                idAccount: null,
                CanaisSessao.Chat,
                $"{apelido} finalizou a sessão. Todos os participantes foram desconectados."));

            return (true, "Sessão finalizada! Revisando fichas dos jogadores.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao finalizar sessão: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> AlterarMapaAtivoAsync(
        int idCampanha,
        int idSessao,
        int idAccount,
        int? idMapa)
    {
        try
        {
            var campanha = await ObterCampanhaComCacheAsync(idCampanha);
            if (campanha == null)
                return (false, "Campanha não encontrada.");

            if (campanha.IdAccount != idAccount)
                return (false, "Apenas o Mestre pode alterar o mapa da sessão.");

            if (idMapa.HasValue && !campanha.MapaCampanhas.Any(mc => mc.IdMapa == idMapa.Value))
                return (false, "O mapa selecionado não está vinculado a esta campanha.");

            await _sessaoRepo.AtualizarMapaAtivoAsync(idSessao, idMapa);
            return (true, "Mapa atualizado para toda a mesa.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao alterar mapa: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    // =========================================================================
    // CONTROLE DE TURNO (MESTRE OU JOGADOR DONO DO TURNO ATUAL)
    // =========================================================================
    public async Task<(bool Sucesso, string Mensagem)> DefinirTurnoJogadorAsync(
        int idCampanha,
        int idSessao,
        int idAccountSolicitante,
        int? idAccountJogador,
        string? nomeJogador = null,
        Guid? idOperacao = null)
    {
        try
        {
            var campanha = await ObterCampanhaComCacheAsync(idCampanha);
            if (campanha == null)
                return (false, "Campanha não encontrada.");

            bool ehMestre = campanha.IdAccount == idAccountSolicitante;
            if (!ehMestre && idAccountJogador.HasValue)
            {
                bool destinoValido = campanha.UsuarioCampanhas.Any(uc => uc.IdAccount == idAccountJogador.Value)
                    || campanha.FichasAceitas.Any(pc => pc.Personagem != null && pc.Personagem.IdAccount == idAccountJogador.Value);
                if (!destinoValido)
                    return (false, "O próximo jogador informado não participa desta campanha.");
            }

            string nomeExibicao = string.IsNullOrWhiteSpace(nomeJogador)
                ? $"Jogador #{idAccountJogador}"
                : nomeJogador;

            string textoSistema = ehMestre
                ? (idAccountJogador.HasValue
                    ? $"⏳ O Mestre liberou o turno para {nomeExibicao}."
                    : "🔒 O Mestre encerrou o turno atual. Ações dos jogadores bloqueadas.")
                : (idAccountJogador.HasValue
                    ? $"⏳ Turno passado automaticamente para {nomeExibicao}."
                    : "🔒 Turno encerrado.");

            await _sessaoRepo.AtualizarTurnoJogadorAsync(idCampanha, idSessao, idAccountJogador,
                ehMestre ? null : idAccountSolicitante, idOperacao ?? Guid.NewGuid(), textoSistema);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao atualizar turno: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    // =========================================================================
    // GERENCIAMENTO DE PINOS POR MAPA (MESTRE OU JOGADOR MOVENDO SEU PINO NO TURNO)
    // =========================================================================
    public async Task<(bool Sucesso, string Mensagem)> PosicionarPinoAsync(
        int idCampanha,
        int idSessao,
        int? idMapa,
        int idAccountSolicitante,
        string tipoEntidade,
        int idEntidade,
        int coluna,
        int linha,
        bool visivel,
        int? idPino = null,
        bool novaInstancia = false)
    {
        try
        {
            var campanha = await ObterCampanhaComCacheAsync(idCampanha);
            if (campanha == null)
                return (false, "Campanha não encontrada.");

            if (!idMapa.HasValue)
                return (false, "Selecione um mapa antes de posicionar pinos.");

            bool ehMestre = campanha.IdAccount == idAccountSolicitante;
            if (!ehMestre)
            {
                if (!string.Equals(tipoEntidade, TiposPinoSessao.Personagem, StringComparison.OrdinalIgnoreCase))
                    return (false, "Apenas o Mestre pode posicionar ou mover NPCs e monstros no mapa.");

                var sessao = await _sessaoRepo.ObterSessaoPorIdAsync(idSessao);
                if (sessao == null || !sessao.Ativa)
                    return (false, "A sessão não está ativa.");

                if (sessao.IdAccountTurno != idAccountSolicitante)
                    return (false, "Aguarde o seu turno para mover seu personagem no mapa.");

                if (sessao.IdMapaAtivo != idMapa)
                    return (false, "Você só pode mover seu personagem no mapa ativo da sessão.");

                bool fichaDoJogador = campanha.FichasAceitas.Any(pc =>
                    pc.IdPersonagem == idEntidade &&
                    pc.Personagem != null &&
                    pc.Personagem.IdAccount == idAccountSolicitante);

                if (!fichaDoJogador)
                {
                    var fichaDb = await _sessaoRepo.ObterFichaCompletaAsync(idEntidade);
                    fichaDoJogador = fichaDb != null && fichaDb.IdAccount == idAccountSolicitante;
                }

                if (!fichaDoJogador)
                    return (false, "Você só pode mover o pino do seu próprio personagem.");
            }

            if (novaInstancia)
            {
                bool fichaVinculada = tipoEntidade switch
                {
                    TiposPinoSessao.Monstro => campanha.MonstroCampanhas.Any(x => x.IdMonstro == idEntidade),
                    TiposPinoSessao.Npc => campanha.NpcCampanhas.Any(x => x.IdNpc == idEntidade),
                    _ => false
                };
                if (!ehMestre || !fichaVinculada)
                    return (false, "Selecione uma criatura vinculada à campanha.");

                await _sessaoRepo.CriarInstanciaPinoAsync(idSessao, idMapa.Value, idAccountSolicitante, tipoEntidade,
                    idEntidade, coluna, linha, visivel, Guid.NewGuid());
            }
            else
                await _sessaoRepo.SalvarOuMoverPinoAsync(idSessao, idMapa, tipoEntidade, idEntidade, coluna, linha, visivel, idPino);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao posicionar pino: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> AlterarVisibilidadePinoAsync(
        int idCampanha,
        int idSessao,
        int? idMapa,
        int idAccountMestre,
        string tipoEntidade,
        int idEntidade,
        bool visivel,
        int? idPino = null)
    {
        try
        {
            var campanha = await ObterCampanhaComCacheAsync(idCampanha);
            if (campanha == null)
                return (false, "Campanha não encontrada.");

            if (campanha.IdAccount != idAccountMestre)
                return (false, "Apenas o Mestre pode alterar a visibilidade dos pinos.");

            await _sessaoRepo.AlterarVisibilidadePinoAsync(idSessao, idMapa, tipoEntidade, idEntidade, visivel, idPino);

            if (visivel && tipoEntidade == TiposPinoSessao.Personagem)
            {
                var sessaoComp = await _sessaoRepo.ObterSessaoCompletaAsync(idSessao);
                var part = sessaoComp?.Participantes.FirstOrDefault(p => p.IdPersonagem == idEntidade);
                if (part != null && part.Escondido)
                {
                    await _sessaoRepo.AtualizarEstadoCombateParticipanteAsync(
                        idSessao,
                        part.IdAccount,
                        escondido: false);
                }
            }

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao alterar visibilidade: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> RemoverPinoAsync(
        int idCampanha,
        int idSessao,
        int? idMapa,
        int idAccountMestre,
        string tipoEntidade,
        int idEntidade,
        int? idPino = null)
    {
        try
        {
            var campanha = await ObterCampanhaComCacheAsync(idCampanha);
            if (campanha == null)
                return (false, "Campanha não encontrada.");

            if (campanha.IdAccount != idAccountMestre)
                return (false, "Apenas o Mestre pode remover pinos do mapa.");

            await _sessaoRepo.RemoverPinoAsync(idSessao, idMapa, tipoEntidade, idEntidade, idPino);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao remover pino: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    // =========================================================================
    // COMBATE: ESQUIVAR (DESVANTAGEM 2D20 ATÉ RETORNAR O TURNO + PASSA TURNO)
    // =========================================================================
    public async Task<(bool Sucesso, string Mensagem)> RegistrarEsquivaAsync(
        int idCampanha,
        int idSessao,
        int idAccountJogador,
        string nomePersonagem,
        int? idProximoAccountTurno,
        string? nomeProximoJogador,
        bool passarTurno = true)
    {
        try
        {
            var sessao = await _sessaoRepo.ObterSessaoPorIdAsync(idSessao);
            if (sessao == null || !sessao.Ativa)
                return (false, "A sessão não está ativa.");

            if (sessao.IdAccountTurno != idAccountJogador)
                return (false, "Aguarde o Mestre liberar o seu turno para esquivar.");

            await _sessaoRepo.AtualizarEstadoCombateParticipanteAsync(
                idSessao,
                idAccountJogador,
                esquivando: true);

            string textoSistema =
                $"◈ {nomePersonagem} assumiu postura de Esquiva! " +
                $"Até o turno voltar para {nomePersonagem}, ataques contra ele têm desvantagem (2d20, menor valor).";

            await Task.WhenAll(
                _sessaoRepo.AdicionarMensagemAsync(new SessaoMensagem(idSessao, null, CanaisSessao.Chat, textoSistema)),
                _sessaoRepo.AdicionarMensagemAsync(new SessaoMensagem(idSessao, null, CanaisSessao.Combate, textoSistema)));

            if (!passarTurno) return (true, string.Empty);
            return await DefinirTurnoJogadorAsync(
                idCampanha,
                idSessao,
                idAccountJogador,
                idProximoAccountTurno,
                nomeProximoJogador);
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao registrar esquiva: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    // =========================================================================
    // COMBATE: ESCONDER (TESTE DE FURTIVIDADE -> PINO INVISÍVEL + PASSA TURNO)
    // =========================================================================
    public async Task<(bool Sucesso, string Mensagem)> RegistrarEsconderAsync(
        int idCampanha,
        int idSessao,
        int? idMapa,
        int idAccountJogador,
        int idPersonagem,
        ResultadoEsconderDto resultado,
        int? idProximoAccountTurno,
        string? nomeProximoJogador,
        bool passarTurno = true)
    {
        try
        {
            var sessao = await _sessaoRepo.ObterSessaoPorIdAsync(idSessao);
            if (sessao == null || !sessao.Ativa)
                return (false, "A sessão não está ativa.");

            if (sessao.IdAccountTurno != idAccountJogador)
                return (false, "Aguarde o Mestre liberar o seu turno para se esconder.");

            await _sessaoRepo.AtualizarEstadoCombateParticipanteAsync(
                idSessao,
                idAccountJogador,
                escondido: resultado.Passou);

            if (resultado.Passou && idMapa.HasValue)
            {
                await _sessaoRepo.AlterarVisibilidadePinoAsync(
                    idSessao,
                    idMapa,
                    TiposPinoSessao.Personagem,
                    idPersonagem,
                    visivel: false);
            }

            string partesBonus = $"DES {(resultado.ModificadorDestreza >= 0 ? "+" : "")}{resultado.ModificadorDestreza}";
            if (resultado.BonusProficiencia > 0)
                partesBonus += $", Prof. +{resultado.BonusProficiencia}";
            if (resultado.BonusExtra != 0)
                partesBonus += $", Extra {(resultado.BonusExtra > 0 ? "+" : "")}{resultado.BonusExtra}";

            string textoSistema = resultado.Passou
                ? $"🥷 {resultado.NomePersonagem} realizou um teste de Furtividade: {resultado.Total} [d20: {resultado.DadoD20} | {partesBonus}] vs CD {resultado.CdAlvo} → SUCESSO! Ficou escondido no mapa."
                : $"⌕ {resultado.NomePersonagem} tentou se esconder (Furtividade): {resultado.Total} [d20: {resultado.DadoD20} | {partesBonus}] vs CD {resultado.CdAlvo} → FALHOU!";

            await Task.WhenAll(
                _sessaoRepo.AdicionarMensagemAsync(new SessaoMensagem(idSessao, null, CanaisSessao.Chat, textoSistema)),
                _sessaoRepo.AdicionarMensagemAsync(new SessaoMensagem(idSessao, null, CanaisSessao.Combate, textoSistema)));

            if (!passarTurno) return (true, string.Empty);
            return await DefinirTurnoJogadorAsync(
                idCampanha,
                idSessao,
                idAccountJogador,
                idProximoAccountTurno,
                nomeProximoJogador);
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao registrar tentativa de esconder: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    // =========================================================================
    // COMBATE: ATAQUE, ATAQUE DE OPORTUNIDADE E CONTROLE DE PV
    // =========================================================================
    public async Task<(bool Sucesso, string Mensagem)> RegistrarAtaquesOportunidadeAsync(
        int idSessao,
        int idAccountJogador,
        int idPersonagemJogador,
        string nomePersonagem,
        int pvNovo,
        int pvMaximo,
        List<AtaqueOportunidadeDto> ataques)
    {
        try
        {
            if (ataques == null || ataques.Count == 0)
                return (true, string.Empty);

            var sessao = await _sessaoRepo.ObterSessaoPorIdAsync(idSessao);
            if (sessao == null || !sessao.Ativa)
                return (false, "A sessão não está ativa.");

            if (sessao.IdAccountTurno != idAccountJogador)
                return (false, "Ação fora do turno atual.");

            int pvFinal = Math.Clamp(pvNovo, 0, Math.Max(1, pvMaximo));
            await _sessaoRepo.AtualizarPvPersonagemAsync(idPersonagemJogador, pvFinal);
            InvalidarCacheCampanha(sessao.IdCampanha);

            foreach (var atq in ataques)
            {
                string dados = string.Join(" + ", atq.ResultadosDano);
                string mod = atq.ModificadorDano > 0
                    ? $" + {atq.ModificadorDano}"
                    : atq.ModificadorDano < 0
                        ? $" - {Math.Abs(atq.ModificadorDano)}"
                        : string.Empty;

                string statusPv = atq.PvAposAtaque <= 0
                    ? $"☠️ {nomePersonagem} caiu inconsciente! (PV: 0/{pvMaximo})"
                    : $"PV de {nomePersonagem}: {atq.PvAposAtaque}/{pvMaximo}";

                string textoSistema =
                    $"⚡ ATAQUE DE OPORTUNIDADE! {atq.NomeMonstro} atingiu {nomePersonagem} ao sair do alcance corpo a corpo (1,5 m) sem usar Desengajar! " +
                    $"Dano ({atq.ExpressaoDano}): -{atq.DanoTotal} PV [{dados}]{mod}. {statusPv}";

                await Task.WhenAll(
                    _sessaoRepo.AdicionarMensagemAsync(new SessaoMensagem(idSessao, null, CanaisSessao.Chat, textoSistema)),
                    _sessaoRepo.AdicionarMensagemAsync(new SessaoMensagem(idSessao, null, CanaisSessao.Combate, textoSistema)));
            }

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao aplicar ataque de oportunidade: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> RegistrarAtaqueAsync(
        int idSessao,
        int? idMapa,
        int idAccountJogador,
        RegistroAtaqueDto ataque,
        SessaoEstadoDto? estado = null)
    {
        try
        {
            var sessao = await _sessaoRepo.ObterSessaoPorIdAsync(idSessao);
            if (sessao == null || !sessao.Ativa)
                return (false, "A sessão não está ativa.");

            if (sessao.IdAccountTurno != idAccountJogador)
                return (false, "Aguarde o Mestre liberar o seu turno para atacar.");

            var sessaoComp = await _sessaoRepo.ObterSessaoCompletaAsync(idSessao);
            var partAtacante = sessaoComp?.Participantes.FirstOrDefault(p => p.IdAccount == idAccountJogador);
            if (partAtacante != null && partAtacante.Escondido)
            {
                await _sessaoRepo.AtualizarEstadoCombateParticipanteAsync(idSessao, idAccountJogador, escondido: false);
                if (partAtacante.IdPersonagem.HasValue && idMapa.HasValue)
                {
                    await _sessaoRepo.AlterarVisibilidadePinoAsync(
                        idSessao,
                        idMapa,
                        TiposPinoSessao.Personagem,
                        partAtacante.IdPersonagem.Value,
                        visivel: true);
                }
            }

            string modAtk = ataque.ModificadorAtaque > 0
                ? $" + {ataque.ModificadorAtaque}"
                : ataque.ModificadorAtaque < 0
                    ? $" - {Math.Abs(ataque.ModificadorAtaque)}"
                    : string.Empty;

            string detalheDado = ataque.AlvoEsquivando
                ? $"2d20 Desvantagem/Esquiva: [{ataque.DadoAtaque1}, {ataque.DadoAtaque2}] → {ataque.DadoAtaque}{modAtk}"
                : $"d20: {ataque.DadoAtaque}{modAtk}";

            if (estado is not null && estado.IdSessao == idSessao && estado.IdMapaAtivo == idMapa &&
                ataque.IdPinoAlvo is int idAlvo && (estado.Encontro.Rodada > 0 || estado.Encontro.Fichas.ContainsKey(idAlvo)))
            {
                return await RegistrarResultadoJogador2014Async(estado, idAccountJogador, ataque, idAlvo, detalheDado);
            }

            string textoSistema;

            if (ataque.Acertou)
            {
                if (ataque.TipoAlvo == TiposPinoSessao.Personagem)
                {
                    await _sessaoRepo.AtualizarPvPersonagemAsync(ataque.IdEntidadeAlvo, ataque.PvNovo);
                    InvalidarCacheCampanha(sessao.IdCampanha);
                }
                else
                {
                    await _sessaoRepo.AtualizarPvPinoAsync(
                        idSessao,
                        idMapa,
                        ataque.TipoAlvo,
                        ataque.IdEntidadeAlvo,
                        ataque.PvNovo,
                        ataque.IdPinoAlvo);
                }

                string dadosDano = string.Join(" + ", ataque.ResultadosDano);
                string modDano = ataque.ModificadorDano > 0
                    ? $" + {ataque.ModificadorDano}"
                    : ataque.ModificadorDano < 0
                        ? $" - {Math.Abs(ataque.ModificadorDano)}"
                        : string.Empty;

                string tipoDanoTxt = string.IsNullOrWhiteSpace(ataque.TipoDano)
                    ? string.Empty
                    : $" ({ataque.TipoDano})";

                string statusAlvo = ataque.PvNovo <= 0
                    ? (ataque.TipoAlvo == TiposPinoSessao.Personagem
                        ? $"☠️ {ataque.NomeAlvo} caiu inconsciente! (PV: 0/{ataque.PvMaximo})"
                        : $"☠️ {ataque.NomeAlvo} foi derrotado! (PV: 0/{ataque.PvMaximo})")
                    : $"PV de {ataque.NomeAlvo}: {ataque.PvNovo}/{ataque.PvMaximo}";

                if (ataque.EhCritico)
                {
                    textoSistema =
                        $"💥 CRÍTICO! {ataque.NomeAtacante} acertou um golpe crítico em {ataque.NomeAlvo} com {ataque.NomeArma}! " +
                        $"Ataque: 20 natural [{detalheDado}] (Total {ataque.TotalAtaque}) vs CA {ataque.CaAlvo} → ACERTO CRÍTICO! " +
                        $"Dano dobrado (2× {ataque.ExpressaoDano}): {ataque.DanoTotal}{tipoDanoTxt} [{dadosDano}]{modDano}. {statusAlvo}";
                }
                else
                {
                    textoSistema =
                        $"⚔️ {ataque.NomeAtacante} atacou {ataque.NomeAlvo} com {ataque.NomeArma}! " +
                        $"Ataque: {ataque.TotalAtaque} [{detalheDado}] vs CA {ataque.CaAlvo} → ACERTOU! " +
                        $"Dano ({ataque.ExpressaoDano}): {ataque.DanoTotal}{tipoDanoTxt} [{dadosDano}]{modDano}. {statusAlvo}";
                }
            }
            else
            {
                textoSistema =
                    $"⚔️ {ataque.NomeAtacante} atacou {ataque.NomeAlvo} com {ataque.NomeArma}. " +
                    $"Ataque: {ataque.TotalAtaque} [{detalheDado}] vs CA {ataque.CaAlvo} → FALHOU!";
            }

            var msgChat = new SessaoMensagem(idSessao, idAccount: null, CanaisSessao.Chat, textoSistema);
            var msgCombate = new SessaoMensagem(idSessao, idAccount: null, CanaisSessao.Combate, textoSistema);

            await Task.WhenAll(
                _sessaoRepo.AdicionarMensagemAsync(msgChat),
                _sessaoRepo.AdicionarMensagemAsync(msgCombate));

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao registrar ataque: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> AtualizarPvPinoNoMapaAsync(
        int idCampanha,
        int idSessao,
        int? idMapa,
        int idAccountMestre,
        string tipoEntidade,
        int idEntidade,
        int pvAtual,
        int? idPino = null)
    {
        try
        {
            var campanha = await ObterCampanhaComCacheAsync(idCampanha);
            if (campanha == null)
                return (false, "Campanha não encontrada.");

            if (campanha.IdAccount != idAccountMestre)
                return (false, "Apenas o Mestre pode alterar o PV manualmente.");

            if (tipoEntidade == TiposPinoSessao.Personagem)
            {
                await _sessaoRepo.AtualizarPvPersonagemAsync(idEntidade, Math.Max(0, pvAtual));
                InvalidarCacheCampanha(idCampanha);
            }
            else
            {
                await _sessaoRepo.AtualizarPvPinoAsync(
                    idSessao,
                    idMapa,
                    tipoEntidade,
                    idEntidade,
                    Math.Max(0, pvAtual), idPino);
            }

            return (true, "PV atualizado.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao atualizar PV: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> EnviarMensagemChatAsync(
        int idSessao,
        int idAccount,
        string conteudo,
        Guid? idOperacao = null)
    {
        try
        {
            var msg = new SessaoMensagem(idSessao, idAccount, CanaisSessao.Chat, conteudo);
            await _sessaoRepo.AdicionarMensagensDaOperacaoAsync(idSessao, idOperacao ?? Guid.NewGuid(), [msg]);
            return (true, string.Empty);
        }
        catch (DomainException dex)
        {
            return (false, dex.Message);
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao enviar mensagem: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> RegistrarRolagemDadosAsync(
        int idSessao,
        string nomeExibicao,
        ResultadoRolagemDto rolagem)
    {
        try
        {
            string dadosIndividuais = string.Join(" + ", rolagem.ResultadosIndividuais);
            string modTexto = rolagem.Modificador > 0
                ? $" + {rolagem.Modificador}"
                : rolagem.Modificador < 0
                    ? $" - {Math.Abs(rolagem.Modificador)}"
                    : string.Empty;

            string contextoTexto = string.IsNullOrWhiteSpace(rolagem.Contexto)
                ? string.Empty
                : $" ({rolagem.Contexto.Trim()})";

            string textoSistema =
                $"🎲 {nomeExibicao} rolou {rolagem.Expressao}{contextoTexto}: Total {rolagem.Total} [{dadosIndividuais}]{modTexto}";

            var msgChat = new SessaoMensagem(idSessao, idAccount: null, CanaisSessao.Chat, textoSistema);
            var msgRegistro = new SessaoMensagem(idSessao, idAccount: null, CanaisSessao.Registro, textoSistema);

            await _sessaoRepo.AdicionarMensagensDaOperacaoAsync(idSessao, rolagem.IdOperacao, [msgChat, msgRegistro]);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao registrar rolagem: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public Task<Personagem?> ObterFichaCompletaAsync(int idPersonagem) =>
        _sessaoRepo.ObterFichaCompletaAsync(idPersonagem);

    public Task SalvarCirculosMagicosAsync(int idPersonagem, IEnumerable<PersonagemCirculoMagico> circulos) =>
        _sessaoRepo.SalvarCirculosMagicosAsync(idPersonagem, circulos);

    public Task AplicarDescansoCirculosMagicosAsync(int idPersonagem, bool longo, IEnumerable<int> circulosRecuperados) =>
        _sessaoRepo.AplicarDescansoCirculosMagicosAsync(idPersonagem, longo, circulosRecuperados);

    public async Task<(bool Sucesso, string Mensagem)> SalvarFichaNaSessaoAsync(
        int idCampanha,
        int idAccount,
        Personagem ficha,
        Inventario dadosInventario,
        IEnumerable<(int IdItem, int Quantidade)> itens,
        IEnumerable<int>? idsMagia = null)
    {
        try
        {
            var campanha = await ObterCampanhaComCacheAsync(idCampanha);
            if (campanha == null)
                return (false, "Campanha não encontrada.");

            bool ehMestre = campanha.IdAccount == idAccount;
            if (!ehMestre && ficha.IdAccount != idAccount)
                return (false, "Você não tem permissão para alterar esta ficha.");

            await _personagemRepo.AtualizarAsync(ficha);
            await _personagemRepo.SalvarInventarioPersonagem3FNAsync(ficha.IdPersonagem, dadosInventario, itens);

            if (idsMagia != null)
            {
                await _sessaoRepo.SalvarMagiasPersonagemAsync(ficha.IdPersonagem, idsMagia);
            }

            InvalidarCacheCampanha(idCampanha);

            return (true, "Alterações salvas na ficha da sessão!");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao salvar ficha: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem, RevisaoPosSessaoDto? Dados)> ObterDadosRevisaoPosSessaoAsync(
        int idCampanha,
        int idAccountMestre)
    {
        try
        {
            var campanha = await ObterCampanhaComCacheAsync(idCampanha);
            if (campanha == null)
                return (false, "Campanha não encontrada.", null);

            if (campanha.IdAccount != idAccountMestre)
                return (false, "Apenas o Mestre pode realizar a revisão pós-sessão.", null);

            var tarefaFichas = _sessaoRepo.ListarFichasAceitasCompletasAsync(idCampanha);
            var tarefaItens = _sessaoRepo.ListarItensCampanhaAsync(idCampanha);
            var tarefaMagias = ObterMagiasCatalogoComCacheAsync(forcarAtualizacao: true);

            await Task.WhenAll(tarefaFichas, tarefaItens, tarefaMagias);

            return (true, string.Empty, new RevisaoPosSessaoDto
            {
                IdCampanha = campanha.IdCampanha,
                NomeCampanha = campanha.Nome,
                Fichas = tarefaFichas.Result,
                ItensCampanha = tarefaItens.Result,
                MagiasCatalogo = tarefaMagias.Result
            });
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao carregar revisão pós-sessão: {ex.InnerException?.Message ?? ex.Message}", null);
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> ConcluirRevisaoPosSessaoAsync(
        int idCampanha,
        int idSessao,
        int idAccountMestre,
        IEnumerable<RevisaoFichaSalvarDto> fichasRevisadas,
        string? tituloSessao,
        string? resumoSessao)
    {
        try
        {
            var campanha = await ObterCampanhaComCacheAsync(idCampanha);
            if (campanha == null)
                return (false, "Campanha não encontrada.");

            if (campanha.IdAccount != idAccountMestre)
                return (false, "Apenas o Mestre pode concluir a revisão das fichas.");

            int xpTotalDistribuido = 0;

            foreach (var item in fichasRevisadas)
            {
                int xpGanho = Math.Max(0, item.XpGanhoSessao);
                xpTotalDistribuido += xpGanho;

                item.Personagem.ExperienciaXp = Math.Max(0, item.Personagem.ExperienciaXp + xpGanho);
                item.Personagem.PvMaximo = Math.Max(1, item.Personagem.PvMaximo);
                item.Personagem.PvAtual = Math.Clamp(item.Personagem.PvAtual, 0, item.Personagem.PvMaximo);
                item.Personagem.Nivel = Math.Clamp(item.Personagem.Nivel, 1, 20);

                await _personagemRepo.AtualizarAsync(item.Personagem);
                await _personagemRepo.SalvarInventarioPersonagem3FNAsync(
                    item.Personagem.IdPersonagem,
                    item.DadosInventario,
                    item.Itens);
                await _sessaoRepo.SalvarMagiasPersonagemAsync(
                    item.Personagem.IdPersonagem,
                    item.IdsMagias);
            }

            if (idSessao > 0)
            {
                await _sessaoRepo.AtualizarResumoSessaoAsync(
                    idSessao,
                    tituloSessao,
                    resumoSessao,
                    xpTotalDistribuido);
            }

            InvalidarCacheCampanha(idCampanha);
            return (true, "Revisão concluída! Fichas, XPs, magias e resumo da sessão foram salvos.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao concluir revisão: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public async Task<List<HistoricoSessaoDto>> ObterHistoricoSessoesAsync(int idCampanha)
    {
        var tarefaCampanha = ObterCampanhaComCacheAsync(idCampanha);
        var tarefaSessoes = _sessaoRepo.ListarHistoricoSessoesAsync(idCampanha);

        await Task.WhenAll(tarefaCampanha, tarefaSessoes);

        var campanha = tarefaCampanha.Result;
        var sessoes = tarefaSessoes.Result;

        if (campanha == null || !sessoes.Any())
            return new List<HistoricoSessaoDto>();

        var contagens = await _sessaoRepo.ContarAtividadePorSessoesAsync(sessoes.Select(s => s.IdSessao));

        var resultado = new List<HistoricoSessaoDto>();
        foreach (var s in sessoes)
        {
            var inicioLocal = DateTime.SpecifyKind(s.DataInicio, DateTimeKind.Utc).ToLocalTime();
            DateTime? fimLocal = s.DataFim.HasValue
                ? DateTime.SpecifyKind(s.DataFim.Value, DateTimeKind.Utc).ToLocalTime()
                : null;

            var fimParaDuracao = s.DataFim ?? (s.Ativa ? DateTime.UtcNow : s.DataAtualizacao);
            var duracao = fimParaDuracao - s.DataInicio;
            if (duracao < TimeSpan.Zero) duracao = TimeSpan.Zero;

            contagens.TryGetValue(s.IdSessao, out var atividade);

            var participantes = s.Participantes
                .OrderByDescending(p => p.IdAccount == campanha.IdAccount)
                .ThenBy(p => p.Usuario?.NomeParaExibicao)
                .Select(p => new ParticipanteHistoricoDto
                {
                    IdAccount = p.IdAccount,
                    NomeUsuario = p.Usuario?.NomeParaExibicao ?? $"Usuário #{p.IdAccount}",
                    EhMestre = p.IdAccount == campanha.IdAccount,
                    NomePersonagem = p.Personagem?.Nome,
                    ClasseNivel = p.Personagem != null
                        ? $"{p.Personagem.ClasseTexto ?? "Aventureiro"} (Nv. {p.Personagem.Nivel})"
                        : null
                })
                .ToList();

            resultado.Add(new HistoricoSessaoDto
            {
                IdSessao = s.IdSessao,
                NumeroSessao = s.NumeroSessao,
                Titulo = string.IsNullOrWhiteSpace(s.Titulo) ? $"Sessão #{s.NumeroSessao}" : s.Titulo,
                Ativa = s.Ativa,
                DataInicioLocal = inicioLocal,
                DataFimLocal = fimLocal,
                DuracaoFormatada = FormatarDuracao(duracao),
                MapaUtilizado = s.MapaAtivo?.NomeRegiao,
                Resumo = s.Resumo,
                XpTotalDistribuido = s.XpTotalDistribuido,
                TotalMensagensChat = atividade.TotalMensagensChat,
                TotalRolagens = atividade.TotalRolagens,
                Participantes = participantes
            });
        }

        return resultado;
    }

    public async Task<List<MensagemSessaoDto>> ObterChatHistoricoSessaoAsync(int idSessao)
    {
        if (idSessao <= 0)
            return new List<MensagemSessaoDto>();

        var mensagens = await _sessaoRepo.ListarMensagensAsync(idSessao, 500);

        return mensagens
            .Where(m => string.Equals(m.Canal, CanaisSessao.Chat, StringComparison.OrdinalIgnoreCase))
            .Select(m => new MensagemSessaoDto
            {
                IdMensagem = m.IdMensagem,
                Canal = m.Canal,
                EhSistema = m.IdAccount == null,
                Autor = m.IdAccount == null
                    ? "Sistema"
                    : (m.Usuario?.NomeParaExibicao ?? $"Usuário #{m.IdAccount}"),
                Conteudo = m.Conteudo,
                DataEnvioLocal = DateTime.SpecifyKind(m.DataEnvio, DateTimeKind.Utc).ToLocalTime()
            })
            .ToList();
    }

    public async Task<(bool Sucesso, string Mensagem)> SalvarResumoHistoricoAsync(
        int idCampanha,
        int idSessao,
        int idAccountMestre,
        string? titulo,
        string? resumo)
    {
        try
        {
            var campanha = await ObterCampanhaComCacheAsync(idCampanha);
            if (campanha == null)
                return (false, "Campanha não encontrada.");

            if (campanha.IdAccount != idAccountMestre)
                return (false, "Apenas o Mestre pode editar o histórico desta campanha.");

            await _sessaoRepo.AtualizarResumoSessaoAsync(idSessao, titulo, resumo);
            return (true, "Resumo da sessão atualizado com sucesso!");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao salvar resumo: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public static int CalcularPvMaximoNpc(NPC npc)
    {
        return Math.Max(1, npc.PvMaximo);
    }

    public static int CalcularCaBase(int destreza) =>
        Math.Max(1, 10 + AtributosBase.CalcularModificador(destreza));

    private static string FormatarDuracao(TimeSpan duracao)
    {
        if (duracao.TotalMinutes < 1)
            return "Menos de 1 min";

        int horas = (int)duracao.TotalHours;
        int minutos = duracao.Minutes;

        if (horas > 0 && minutos > 0)
            return $"{horas}h {minutos}min";
        if (horas > 0)
            return $"{horas}h";
        return $"{minutos} min";
    }

    private async Task<SessaoEstadoDto> MontarEstadoSessaoAsync(Campanha campanha, int idSessao, int idAccountLogado,
        CancellationToken cancellationToken = default)
    {
        var tarefaSessao = _sessaoRepo.ObterSessaoCompletaAsync(idSessao, cancellationToken);
        var tarefaMensagens = _sessaoRepo.ListarMensagensAsync(idSessao, cancellationToken: cancellationToken);
        var tarefaPinos = _sessaoRepo.ListarPinosSessaoAsync(idSessao, cancellationToken);
        var tarefaMagias = ObterMagiasCatalogoComCacheAsync(cancellationToken: cancellationToken);

        await Task.WhenAll(tarefaSessao, tarefaMensagens, tarefaPinos, tarefaMagias);

        var sessaoCompleta = tarefaSessao.Result;
        var mensagens = tarefaMensagens.Result;
        var pinosDb = tarefaPinos.Result;
        var magiasCatalogo = tarefaMagias.Result;

        var mapasDisponiveis = campanha.MapaCampanhas
            .Where(mc => mc.Mapa != null)
            .Select(mc => new MapaSessaoDto
            {
                IdMapa = mc.Mapa.IdMapa,
                NomeRegiao = mc.Mapa.NomeRegiao,
                Escala = mc.Mapa.Escala,
                ImagemUrl = mc.Mapa.ImagemUrl
            })
            .OrderBy(m => m.NomeRegiao)
            .ToList();

        var itensCampanha = campanha.ItemCampanhas
            .Where(ic => ic.Item != null)
            .Select(ic => ic.Item!)
            .OrderBy(i => i.Nome)
            .ToList();

        var npcsCampanha = campanha.NpcCampanhas
            .Where(nc => nc.Npc != null)
            .Select(nc => nc.Npc!)
            .OrderBy(n => n.Nome)
            .ToList();

        var monstrosCampanha = campanha.MonstroCampanhas
            .Where(mc => mc.Monstro != null)
            .Select(mc => mc.Monstro!)
            .OrderBy(m => m.Nome)
            .ToList();

        int? idMapaAtivo = sessaoCompleta?.IdMapaAtivo;
        var mapaAtivo = mapasDisponiveis.FirstOrDefault(m => m.IdMapa == idMapaAtivo);

        var presencaPorConta = (sessaoCompleta?.Participantes ?? Array.Empty<SessaoParticipante>())
            .ToDictionary(p => p.IdAccount, p => p);

        presencaPorConta.TryGetValue(campanha.IdAccount, out var presencaMestre);
        var mestreDto = new ParticipanteSessaoDto
        {
            IdAccount = campanha.IdAccount,
            Username = campanha.UsuarioMestre?.NomeParaExibicao ?? "Mestre",
            EhMestre = true,
            SubtituloLinha1 = "Mestre",
            SubtituloLinha2 = string.Empty,
            AvatarUrl = campanha.UsuarioMestre?.Perfil?.AvatarUrl,
            Online = presencaMestre?.EstaConectadoAgora() ?? false
        };

        var fichasAceitasPorConta = campanha.FichasAceitas
            .Where(pc => pc.Personagem != null)
            .GroupBy(pc => pc.Personagem!.IdAccount)
            .ToDictionary(g => g.Key, g => g.First().Personagem!);

        var contasJogadores = new Dictionary<int, (string Username, string? AvatarPerfil)>();

        foreach (var uc in campanha.UsuarioCampanhas.Where(uc => uc.IdAccount != campanha.IdAccount))
        {
            contasJogadores[uc.IdAccount] = (
                uc.Usuario?.NomeParaExibicao ?? $"Jogador #{uc.IdAccount}",
                uc.Usuario?.Perfil?.AvatarUrl);
        }

        foreach (var kv in fichasAceitasPorConta.Where(kv => kv.Key != campanha.IdAccount))
        {
            if (!contasJogadores.ContainsKey(kv.Key))
            {
                contasJogadores[kv.Key] = (
                    kv.Value.Usuario?.NomeParaExibicao ?? $"Jogador #{kv.Key}",
                    kv.Value.Usuario?.Perfil?.AvatarUrl);
            }
        }

        var jogadoresDto = new List<ParticipanteSessaoDto>();
        var fichasPorIdPersonagem = new Dictionary<int, Personagem>();

        foreach (var (idConta, infoUsuario) in contasJogadores.OrderBy(x => x.Value.Username))
        {
            presencaPorConta.TryGetValue(idConta, out var presenca);
            fichasAceitasPorConta.TryGetValue(idConta, out var ficha);

            var fichaAtiva = presenca?.Personagem ?? ficha;
            if (fichaAtiva != null)
            {
                fichasPorIdPersonagem[fichaAtiva.IdPersonagem] = fichaAtiva;
            }

            string nomeFicha = fichaAtiva?.Nome ?? "Sem ficha aceita";
            string classeNivel = fichaAtiva != null ? FormatarClasseENivel(fichaAtiva) : string.Empty;

            jogadoresDto.Add(new ParticipanteSessaoDto
            {
                IdAccount = idConta,
                IdPersonagem = fichaAtiva?.IdPersonagem,
                Username = infoUsuario.Username,
                EhMestre = false,
                NomePersonagem = fichaAtiva?.Nome,
                SubtituloLinha1 = nomeFicha,
                SubtituloLinha2 = classeNivel,
                AvatarUrl = !string.IsNullOrWhiteSpace(fichaAtiva?.Imagem)
                    ? fichaAtiva.Imagem
                    : infoUsuario.AvatarPerfil,
                Online = presenca?.EstaConectadoAgora() ?? false,
                Esquivando = presenca?.Esquivando ?? false,
                Escondido = presenca?.Escondido ?? false
            });
        }

        int? idPersonagemAtual = jogadoresDto
            .FirstOrDefault(j => j.IdAccount == idAccountLogado)?.IdPersonagem;

        int? idAccountTurno = sessaoCompleta?.IdAccountTurno;
        string? nomeJogadorTurno = idAccountTurno.HasValue
            ? jogadoresDto.FirstOrDefault(j => j.IdAccount == idAccountTurno.Value)?.NomePersonagem
              ?? jogadoresDto.FirstOrDefault(j => j.IdAccount == idAccountTurno.Value)?.Username
            : null;

        var pinosDto = new List<PinoMapaDto>();
        foreach (var pino in pinosDb)
        {
            string nome = string.Empty;
            string? imagem = null;
            int? pvAtual = pino.PvAtual;
            int? pvMaximo = null;
            int? classeArmadura = null;
            bool esquivando = false;
            bool escondido = false;
            int[]? atributos = null;
            decimal deslocamento = 9m;

            if (pino.TipoEntidade == TiposPinoSessao.Personagem)
            {
                var jog = jogadoresDto.FirstOrDefault(j => j.IdPersonagem == pino.IdEntidade);
                nome = jog?.NomePersonagem ?? jog?.Username ?? $"Herói #{pino.IdEntidade}";
                imagem = jog?.AvatarUrl;
                esquivando = jog?.Esquivando ?? false;
                escondido = jog?.Escondido ?? false;

                if (fichasPorIdPersonagem.TryGetValue(pino.IdEntidade, out var fichaJog))
                {
                    pvMaximo = fichaJog.PvMaximo;
                    pvAtual = fichaJog.PvAtual;
                    classeArmadura = CalcularCaBase(fichaJog.Destreza);
                    atributos = [fichaJog.Forca, fichaJog.Destreza, fichaJog.Constituicao, fichaJog.Inteligencia, fichaJog.Sabedoria, fichaJog.Carisma];
                    deslocamento = fichaJog.Deslocamento ?? 9;
                }
            }
            else if (pino.TipoEntidade == TiposPinoSessao.Npc)
            {
                var npc = npcsCampanha.FirstOrDefault(n => n.IdNpc == pino.IdEntidade);
                nome = npc?.Nome ?? $"NPC #{pino.IdEntidade}";
                imagem = npc?.Imagem;
                if (npc != null)
                {
                    pvMaximo = CalcularPvMaximoNpc(npc);
                    pvAtual = pino.PvAtual ?? pvMaximo;
                    classeArmadura = Math.Max(1, npc.ClasseArmadura);
                    atributos = [npc.Forca, npc.Destreza, npc.Constituicao, npc.Inteligencia, npc.Sabedoria, npc.Carisma];
                    deslocamento = npc.Deslocamento;
                }
            }
            else if (pino.TipoEntidade == TiposPinoSessao.Monstro)
            {
                var monstro = monstrosCampanha.FirstOrDefault(m => m.IdMonstro == pino.IdEntidade);
                nome = monstro?.Nome ?? $"Monstro #{pino.IdEntidade}";
                imagem = monstro?.Imagem;
                pvMaximo = monstro?.PvMaximo;
                pvAtual = pino.PvAtual ?? monstro?.PvMaximo;
                classeArmadura = monstro?.ClasseArmadura;
                deslocamento = RegrasMagiaCombate.InterpretarAlcance(monstro?.Deslocamento).MetrosMaximos ?? 9m;
            }

            pinosDto.Add(new PinoMapaDto
            {
                IdPino = pino.IdPino,
                IdMapa = pino.IdMapa,
                TipoEntidade = pino.TipoEntidade,
                IdEntidade = pino.IdEntidade,
                Instancia = pino.Instancia,
                Nome = pino.Instancia > 1 ? $"{nome} {pino.Instancia}" : nome,
                ImagemUrl = imagem,
                Coluna = pino.Coluna,
                Linha = pino.Linha,
                Visivel = pino.Visivel,
                PvAtual = pvAtual,
                PvMaximo = pvMaximo,
                ClasseArmadura = classeArmadura,
                Atributos = atributos,
                DeslocamentoMetros = deslocamento,
                Esquivando = esquivando,
                Escondido = escondido
            });
        }

        var mensagensDto = mensagens.Select(ParaMensagemDto).ToList();
        var controleCombate = sessaoCompleta?.Combate2014Json is { Length: > 0 } combateJson
            ? JsonSerializer.Deserialize<ControleCombate2014>(combateJson) ?? new()
            : new ControleCombate2014();

        return new SessaoEstadoDto
        {
            IdSessao = idSessao,
            NumeroSessao = sessaoCompleta?.NumeroSessao ?? 1,
            TituloSessao = sessaoCompleta?.Titulo ?? $"Sessão #{sessaoCompleta?.NumeroSessao ?? 1}",
            IdCampanha = campanha.IdCampanha,
            NomeCampanha = campanha.Nome,
            SessaoAtiva = sessaoCompleta?.Ativa ?? false,
            EhMestre = campanha.IdAccount == idAccountLogado,
            IdPersonagemLogado = idPersonagemAtual,
            IdAccountTurno = idAccountTurno,
            NomeJogadorTurno = nomeJogadorTurno,
            IdMapaAtivo = idMapaAtivo,
            MapaAtivo = mapaAtivo,
            MapasDisponiveis = mapasDisponiveis,
            ItensCampanha = itensCampanha,
            MagiasCatalogo = magiasCatalogo,
            NpcsCampanha = npcsCampanha,
            MonstrosCampanha = monstrosCampanha,
            Pinos = pinosDto,
            Encontro = idMapaAtivo.HasValue ? controleCombate.ObterMapa(idMapaAtivo.Value) : new(),
            PedidosTeste = controleCombate.Pedidos.Where(p => campanha.IdAccount == idAccountLogado || p.IdConta == idAccountLogado)
                .Select(p => campanha.IdAccount != idAccountLogado && p.OcultarCd ? p with { Cd = null } : p).ToList(),
            Mestre = mestreDto,
            Jogadores = jogadoresDto,
            Mensagens = mensagensDto
        };
    }

    private static MensagemSessaoDto ParaMensagemDto(SessaoMensagem mensagem) => new()
    {
        IdMensagem = mensagem.IdMensagem,
        Canal = mensagem.Canal,
        EhSistema = mensagem.IdAccount == null,
        Autor = mensagem.IdAccount == null
            ? "Sistema"
            : (mensagem.Usuario?.NomeParaExibicao ?? $"Usuário #{mensagem.IdAccount}"),
        Conteudo = mensagem.Conteudo,
        DataEnvioLocal = DateTime.SpecifyKind(mensagem.DataEnvio, DateTimeKind.Utc).ToLocalTime()
    };

    private static string FormatarClasseENivel(Personagem personagem)
    {
        if (personagem.PersonagemClasses != null && personagem.PersonagemClasses.Any(pc => pc.Classe != null))
        {
            var partes = personagem.PersonagemClasses
                .Where(pc => pc.Classe != null)
                .Select(pc => $"{pc.Classe!.Nome} {pc.NivelNaClasse}");

            int nivelTotal = Math.Max(personagem.Nivel, personagem.PersonagemClasses.Sum(pc => pc.NivelNaClasse));
            return $"{string.Join(" / ", partes)} • Nv. {nivelTotal}";
        }

        string classe = string.IsNullOrWhiteSpace(personagem.ClasseTexto) ? "Aventureiro" : personagem.ClasseTexto;
        return $"{classe} • Nv. {personagem.Nivel}";
    }
}

public class SessaoEstadoDto
{
    public int IdSessao { get; set; }
    public int NumeroSessao { get; set; } = 1;
    public string TituloSessao { get; set; } = string.Empty;
    public int IdCampanha { get; set; }
    public string NomeCampanha { get; set; } = string.Empty;
    public bool SessaoAtiva { get; set; }
    public bool EhMestre { get; set; }
    public int? IdPersonagemLogado { get; set; }
    public int? IdAccountTurno { get; set; }
    public string? NomeJogadorTurno { get; set; }
    public int? IdMapaAtivo { get; set; }
    public MapaSessaoDto? MapaAtivo { get; set; }
    public List<MapaSessaoDto> MapasDisponiveis { get; set; } = new();
    public List<Item> ItensCampanha { get; set; } = new();
    public List<Magia> MagiasCatalogo { get; set; } = new();
    public List<NPC> NpcsCampanha { get; set; } = new();
    public List<Monstro> MonstrosCampanha { get; set; } = new();
    public List<PinoMapaDto> Pinos { get; set; } = new();
    public ParticipanteSessaoDto Mestre { get; set; } = new();
    public List<ParticipanteSessaoDto> Jogadores { get; set; } = new();
    public List<MensagemSessaoDto> Mensagens { get; set; } = new();
    public Encontro2014 Encontro { get; set; } = new();
    public List<PedidoTeste2014> PedidosTeste { get; set; } = [];

    internal SessaoEstadoDto ComMensagens(List<MensagemSessaoDto> mensagens)
    {
        var estado = (SessaoEstadoDto)MemberwiseClone();
        estado.Mensagens = mensagens;
        return estado;
    }

    internal SessaoEstadoDto ComSessaoEncerrada()
    {
        var estado = (SessaoEstadoDto)MemberwiseClone();
        estado.SessaoAtiva = false;
        return estado;
    }
}

public class PinoMapaDto
{
    public int IdPino { get; set; }
    public int Instancia { get; set; } = 1;
    public int? IdMapa { get; set; }
    public string TipoEntidade { get; set; } = string.Empty;
    public int IdEntidade { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? ImagemUrl { get; set; }
    public int Coluna { get; set; }
    public int Linha { get; set; }
    public bool Visivel { get; set; } = true;
    public int? PvAtual { get; set; }
    public int? PvMaximo { get; set; }
    public int? ClasseArmadura { get; set; }
    public int[]? Atributos { get; set; }
    public decimal DeslocamentoMetros { get; set; } = 9m;
    public int Modificador(Atributo2014 atributo) => Atributos is { Length: 6 }
        ? AtributosBase.CalcularModificador(Atributos[(int)atributo]) : 0;
    public bool Esquivando { get; set; }
    public bool Escondido { get; set; }
    public string CoordenadaTexto => $"{(char)('A' + Coluna)}{Linha + 1}";
}

public class AtaqueOportunidadeDto
{
    public int IdMonstro { get; set; }
    public string NomeMonstro { get; set; } = string.Empty;
    public string ExpressaoDano { get; set; } = string.Empty;
    public List<int> ResultadosDano { get; set; } = new();
    public int ModificadorDano { get; set; }
    public int DanoTotal { get; set; }
    public int PvAposAtaque { get; set; }
}

public class RegistroAtaqueDto
{
    public string NomeAtacante { get; set; } = string.Empty;
    public string NomeArma { get; set; } = string.Empty;
    public string ExpressaoDano { get; set; } = string.Empty;
    public string? TipoDano { get; set; }
    public string TipoAlvo { get; set; } = TiposPinoSessao.Monstro;
    public int IdEntidadeAlvo { get; set; }
    public int? IdPinoAlvo { get; set; }
    public string NomeAlvo { get; set; } = string.Empty;
    public int CaAlvo { get; set; }
    public bool AlvoEsquivando { get; set; }
    public int DadoAtaque1 { get; set; }
    public int DadoAtaque2 { get; set; }
    public int DadoAtaque { get; set; }
    public int ModificadorAtaque { get; set; }
    public int TotalAtaque { get; set; }
    public bool EhCritico { get; set; }
    public bool Acertou { get; set; }
    public List<int> ResultadosDano { get; set; } = new();
    public int ModificadorDano { get; set; }
    public int DanoTotal { get; set; }
    public int PvAnterior { get; set; }
    public int PvNovo { get; set; }
    public int PvMaximo { get; set; }
}

public class ResultadoEsconderDto
{
    public string NomePersonagem { get; set; } = string.Empty;
    public int DadoD20 { get; set; }
    public int ModificadorDestreza { get; set; }
    public bool TemProficiencia { get; set; }
    public int BonusProficiencia { get; set; }
    public int BonusExtra { get; set; }
    public int Total { get; set; }
    public int CdAlvo { get; set; } = 12;
    public bool Passou { get; set; }
}

public class HistoricoSessaoDto
{
    public int IdSessao { get; set; }
    public int NumeroSessao { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public bool Ativa { get; set; }
    public DateTime DataInicioLocal { get; set; }
    public DateTime? DataFimLocal { get; set; }
    public string DuracaoFormatada { get; set; } = string.Empty;
    public string? MapaUtilizado { get; set; }
    public string? Resumo { get; set; }
    public int XpTotalDistribuido { get; set; }
    public int TotalMensagensChat { get; set; }
    public int TotalRolagens { get; set; }
    public List<ParticipanteHistoricoDto> Participantes { get; set; } = new();
}

public class ParticipanteHistoricoDto
{
    public int IdAccount { get; set; }
    public string NomeUsuario { get; set; } = string.Empty;
    public bool EhMestre { get; set; }
    public string? NomePersonagem { get; set; }
    public string? ClasseNivel { get; set; }
}

public class MapaSessaoDto
{
    public int IdMapa { get; set; }
    public string NomeRegiao { get; set; } = string.Empty;
    public string? Escala { get; set; }
    public string? ImagemUrl { get; set; }
}

public class ParticipanteSessaoDto
{
    public int IdAccount { get; set; }
    public int? IdPersonagem { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool EhMestre { get; set; }
    public string? NomePersonagem { get; set; }
    public string SubtituloLinha1 { get; set; } = string.Empty;
    public string SubtituloLinha2 { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool Online { get; set; }
    public bool Esquivando { get; set; }
    public bool Escondido { get; set; }
}

public class MensagemSessaoDto
{
    public int IdMensagem { get; set; }
    public string Canal { get; set; } = CanaisSessao.Chat;
    public bool EhSistema { get; set; }
    public string Autor { get; set; } = string.Empty;
    public string Conteudo { get; set; } = string.Empty;
    public DateTime DataEnvioLocal { get; set; }
}

public class ResultadoRolagemDto
{
    public Guid IdOperacao { get; } = Guid.NewGuid();
    public string Contexto { get; set; } = "Rolagem da mesa";
    public int Faces { get; set; } = 20;
    public int Quantidade { get; set; } = 1;
    public int Modificador { get; set; }
    public List<int> ResultadosIndividuais { get; set; } = new();
    public int Total { get; set; }
    public string Expressao { get; set; } = string.Empty;
}

public class RevisaoPosSessaoDto
{
    public int IdCampanha { get; set; }
    public string NomeCampanha { get; set; } = string.Empty;
    public List<Personagem> Fichas { get; set; } = new();
    public List<Item> ItensCampanha { get; set; } = new();
    public List<Magia> MagiasCatalogo { get; set; } = new();
}

public class RevisaoFichaSalvarDto
{
    public Personagem Personagem { get; set; } = new();
    public int XpGanhoSessao { get; set; }
    public Inventario DadosInventario { get; set; } = new();
    public List<(int IdItem, int Quantidade)> Itens { get; set; } = new();
    public List<int> IdsMagias { get; set; } = new();
}
