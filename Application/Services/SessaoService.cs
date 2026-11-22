using System.Collections.Concurrent;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Application.Services;

public class SessaoService
{
    private static readonly TimeSpan ValidadeCacheCampanha = TimeSpan.FromSeconds(60);

    private readonly ISessaoRepository _sessaoRepo;
    private readonly IPersonagemRepository _personagemRepo;
    private readonly ConcurrentDictionary<int, (Campanha Campanha, DateTime AtualizadoEmUtc)> _cacheCampanhas = new();

    public SessaoService(
        ISessaoRepository sessaoRepo,
        IPersonagemRepository personagemRepo)
    {
        _sessaoRepo = sessaoRepo;
        _personagemRepo = personagemRepo;
    }

    public void InvalidarCacheCampanha(int idCampanha) =>
        _cacheCampanhas.TryRemove(idCampanha, out _);

    private async Task<Campanha?> ObterCampanhaComCacheAsync(int idCampanha, bool forcarAtualizacao = false)
    {
        if (!forcarAtualizacao &&
            _cacheCampanhas.TryGetValue(idCampanha, out var entrada) &&
            (DateTime.UtcNow - entrada.AtualizadoEmUtc) < ValidadeCacheCampanha)
        {
            return entrada.Campanha;
        }

        var campanha = await _sessaoRepo.ObterCampanhaParaSessaoAsync(idCampanha);
        if (campanha != null)
        {
            _cacheCampanhas[idCampanha] = (campanha, DateTime.UtcNow);
        }

        return campanha;
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

    public async Task<SessaoEstadoDto?> SincronizarSessaoAsync(int idCampanha, int idSessao, int idAccount)
    {
        var tarefaSessao = _sessaoRepo.ObterSessaoPorIdAsync(idSessao);
        var tarefaCampanha = ObterCampanhaComCacheAsync(idCampanha);

        await Task.WhenAll(tarefaSessao, tarefaCampanha);

        var sessaoAtual = tarefaSessao.Result;
        var campanha = tarefaCampanha.Result;

        if (sessaoAtual != null && sessaoAtual.Ativa)
        {
            await _sessaoRepo.AtualizarHeartbeatAsync(idSessao, idAccount);
        }

        if (campanha == null) return null;

        return await MontarEstadoSessaoAsync(campanha, idSessao, idAccount);
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
    // GERENCIAMENTO DE PINOS POR MAPA (EXCLUSIVO DO MESTRE)
    // =========================================================================
    public async Task<(bool Sucesso, string Mensagem)> PosicionarPinoAsync(
        int idCampanha,
        int idSessao,
        int? idMapa,
        int idAccountMestre,
        string tipoEntidade,
        int idEntidade,
        int coluna,
        int linha,
        bool visivel)
    {
        try
        {
            var campanha = await ObterCampanhaComCacheAsync(idCampanha);
            if (campanha == null)
                return (false, "Campanha não encontrada.");

            if (campanha.IdAccount != idAccountMestre)
                return (false, "Apenas o Mestre pode posicionar pinos no mapa.");

            if (!idMapa.HasValue)
                return (false, "Selecione um mapa antes de posicionar pinos.");

            await _sessaoRepo.SalvarOuMoverPinoAsync(idSessao, idMapa, tipoEntidade, idEntidade, coluna, linha, visivel);
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
        bool visivel)
    {
        try
        {
            var campanha = await ObterCampanhaComCacheAsync(idCampanha);
            if (campanha == null)
                return (false, "Campanha não encontrada.");

            if (campanha.IdAccount != idAccountMestre)
                return (false, "Apenas o Mestre pode alterar a visibilidade dos pinos.");

            await _sessaoRepo.AlterarVisibilidadePinoAsync(idSessao, idMapa, tipoEntidade, idEntidade, visivel);
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
        int idEntidade)
    {
        try
        {
            var campanha = await ObterCampanhaComCacheAsync(idCampanha);
            if (campanha == null)
                return (false, "Campanha não encontrada.");

            if (campanha.IdAccount != idAccountMestre)
                return (false, "Apenas o Mestre pode remover pinos do mapa.");

            await _sessaoRepo.RemoverPinoAsync(idSessao, idMapa, tipoEntidade, idEntidade);
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao remover pino: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> EnviarMensagemChatAsync(
        int idSessao,
        int idAccount,
        string conteudo)
    {
        try
        {
            var msg = new SessaoMensagem(idSessao, idAccount, CanaisSessao.Chat, conteudo);
            await _sessaoRepo.AdicionarMensagemAsync(msg);
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

            await Task.WhenAll(
                _sessaoRepo.AdicionarMensagemAsync(msgChat),
                _sessaoRepo.AdicionarMensagemAsync(msgRegistro));

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao registrar rolagem: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public Task<Personagem?> ObterFichaCompletaAsync(int idPersonagem) =>
        _sessaoRepo.ObterFichaCompletaAsync(idPersonagem);

    public async Task<(bool Sucesso, string Mensagem)> SalvarFichaNaSessaoAsync(
        int idCampanha,
        int idAccount,
        Personagem ficha,
        Inventario dadosInventario,
        IEnumerable<(int IdItem, int Quantidade)> itens)
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

            await Task.WhenAll(tarefaFichas, tarefaItens);

            return (true, string.Empty, new RevisaoPosSessaoDto
            {
                IdCampanha = campanha.IdCampanha,
                NomeCampanha = campanha.Nome,
                Fichas = tarefaFichas.Result,
                ItensCampanha = tarefaItens.Result
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
            return (true, "Revisão concluída! Fichas, XPs e resumo da sessão foram salvos.");
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

    private async Task<SessaoEstadoDto> MontarEstadoSessaoAsync(Campanha campanha, int idSessao, int idAccountLogado)
    {
        var tarefaSessao = _sessaoRepo.ObterSessaoCompletaAsync(idSessao);
        var tarefaMensagens = _sessaoRepo.ListarMensagensAsync(idSessao);
        var tarefaPinos = _sessaoRepo.ListarPinosSessaoAsync(idSessao);

        await Task.WhenAll(tarefaSessao, tarefaMensagens, tarefaPinos);

        var sessaoCompleta = tarefaSessao.Result;
        var mensagens = tarefaMensagens.Result;
        var pinosDb = tarefaPinos.Result;

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
        foreach (var (idConta, infoUsuario) in contasJogadores.OrderBy(x => x.Value.Username))
        {
            presencaPorConta.TryGetValue(idConta, out var presenca);
            fichasAceitasPorConta.TryGetValue(idConta, out var ficha);

            var fichaAtiva = presenca?.Personagem ?? ficha;
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
                Online = presenca?.EstaConectadoAgora() ?? false
            });
        }

        int? idPersonagemAtual = jogadoresDto
            .FirstOrDefault(j => j.IdAccount == idAccountLogado)?.IdPersonagem;

        var pinosDto = new List<PinoMapaDto>();
        foreach (var pino in pinosDb)
        {
            string nome = string.Empty;
            string? imagem = null;

            if (pino.TipoEntidade == TiposPinoSessao.Personagem)
            {
                var jog = jogadoresDto.FirstOrDefault(j => j.IdPersonagem == pino.IdEntidade);
                nome = jog?.NomePersonagem ?? jog?.Username ?? $"Herói #{pino.IdEntidade}";
                imagem = jog?.AvatarUrl;
            }
            else if (pino.TipoEntidade == TiposPinoSessao.Npc)
            {
                var npc = npcsCampanha.FirstOrDefault(n => n.IdNpc == pino.IdEntidade);
                nome = npc?.Nome ?? $"NPC #{pino.IdEntidade}";
                imagem = npc?.Imagem;
            }
            else if (pino.TipoEntidade == TiposPinoSessao.Monstro)
            {
                var monstro = monstrosCampanha.FirstOrDefault(m => m.IdMonstro == pino.IdEntidade);
                nome = monstro?.Nome ?? $"Monstro #{pino.IdEntidade}";
                imagem = monstro?.Imagem;
            }

            pinosDto.Add(new PinoMapaDto
            {
                IdPino = pino.IdPino,
                IdMapa = pino.IdMapa,
                TipoEntidade = pino.TipoEntidade,
                IdEntidade = pino.IdEntidade,
                Nome = nome,
                ImagemUrl = imagem,
                Coluna = pino.Coluna,
                Linha = pino.Linha,
                Visivel = pino.Visivel
            });
        }

        var mensagensDto = mensagens.Select(m => new MensagemSessaoDto
        {
            IdMensagem = m.IdMensagem,
            Canal = m.Canal,
            EhSistema = m.IdAccount == null,
            Autor = m.IdAccount == null
                ? "Sistema"
                : (m.Usuario?.NomeParaExibicao ?? $"Usuário #{m.IdAccount}"),
            Conteudo = m.Conteudo,
            DataEnvioLocal = DateTime.SpecifyKind(m.DataEnvio, DateTimeKind.Utc).ToLocalTime()
        }).ToList();

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
            IdMapaAtivo = idMapaAtivo,
            MapaAtivo = mapaAtivo,
            MapasDisponiveis = mapasDisponiveis,
            ItensCampanha = itensCampanha,
            NpcsCampanha = npcsCampanha,
            MonstrosCampanha = monstrosCampanha,
            Pinos = pinosDto,
            Mestre = mestreDto,
            Jogadores = jogadoresDto,
            Mensagens = mensagensDto
        };
    }

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
    public int? IdMapaAtivo { get; set; }
    public MapaSessaoDto? MapaAtivo { get; set; }
    public List<MapaSessaoDto> MapasDisponiveis { get; set; } = new();
    public List<Item> ItensCampanha { get; set; } = new();
    public List<NPC> NpcsCampanha { get; set; } = new();
    public List<Monstro> MonstrosCampanha { get; set; } = new();
    public List<PinoMapaDto> Pinos { get; set; } = new();
    public ParticipanteSessaoDto Mestre { get; set; } = new();
    public List<ParticipanteSessaoDto> Jogadores { get; set; } = new();
    public List<MensagemSessaoDto> Mensagens { get; set; } = new();
}

public class PinoMapaDto
{
    public int IdPino { get; set; }
    public int? IdMapa { get; set; }
    public string TipoEntidade { get; set; } = string.Empty;
    public int IdEntidade { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? ImagemUrl { get; set; }
    public int Coluna { get; set; }
    public int Linha { get; set; }
    public bool Visivel { get; set; } = true;
    public string CoordenadaTexto => $"{(char)('A' + Coluna)}{Linha + 1}";
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
}

public class RevisaoFichaSalvarDto
{
    public Personagem Personagem { get; set; } = new();
    public int XpGanhoSessao { get; set; }
    public Inventario DadosInventario { get; set; } = new();
    public List<(int IdItem, int Quantidade)> Itens { get; set; } = new();
}