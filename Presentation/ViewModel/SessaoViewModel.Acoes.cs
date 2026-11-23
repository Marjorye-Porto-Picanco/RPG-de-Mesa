using System.Globalization;
using System.Text.RegularExpressions;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Sessoes;
using RPGdeMesa.Domain.ValueObjects;

namespace RPGdeMesa.Presentation.ViewModel;

public partial class SessaoViewModel
{
    // Modal de Ataque (Combate)
    public bool ExibirModalAtaque { get; private set; }
    public bool CarregandoAtaque { get; private set; }
    public bool ExecutandoAtaque { get; private set; }
    public string? ErroModalAtaque { get; private set; }
    public List<Item> ArmasDisponiveisAtaque { get; private set; } = new();
    public Personagem? PersonagemAtacante { get; private set; }
    public int IdArmaSelecionada { get; private set; }
    public string? TipoAlvoSelecionado { get; private set; }
    public int IdAlvoSelecionado { get; private set; }
    public int ModificadorAtaque { get; set; }
    public int ModificadorDano { get; set; }
    public int EtapaAtaque { get; private set; } = 1; // 1=Escolher arma/alvo, 2=Acertou (rolar dano), 3=Finalizado
    public RegistroAtaqueDto? ResultadoAtaqueAtual { get; private set; }

    // Modal de Esquivar (Defesa: desvantagem 2d20 em ataques recebidos até próximo turno)
    public bool ExibirModalEsquivar { get; private set; }
    public bool ExecutandoEsquivar { get; private set; }
    public string? ErroEsquivar { get; private set; }

    // Modal de Esconder (Teste de Destreza/Furtividade)
    public bool ExibirModalEsconder { get; private set; }
    public bool CarregandoEsconder { get; private set; }
    public bool ExecutandoEsconder { get; private set; }
    public string? ErroEsconder { get; private set; }
    public int EtapaEsconder { get; private set; } = 1; // 1=Configurar teste, 2=Resultado
    public string NomePersonagemEsconder { get; private set; } = string.Empty;
    public int ModDestrezaEsconder { get; private set; }
    public bool TemProficienciaFurtividade { get; private set; }
    public int BonusProficienciaEsconder { get; private set; }
    public int BonusExtraEsconder { get; set; }
    public int CdEsconder { get; set; } = 12;
    public ResultadoEsconderDto? ResultadoEsconderAtual { get; private set; }

    // Movimento no Turno: Disparada (2x deslocamento, sujeito a Ataque de Oportunidade)
    // e Desengajar (1x deslocamento, imune a Ataque de Oportunidade)
    public bool ModoDisparadaAtivo { get; private set; }
    public bool ModoDesengajarAtivo { get; private set; }
    public bool ModoMovimentoAtivo => ModoDisparadaAtivo || ModoDesengajarAtivo;
    public bool ExibirModalConfirmarDisparada { get; private set; }
    public bool ExecutandoDisparada { get; private set; }
    public string? ErroDisparada { get; private set; }

    public PinoMapaDto? PinoJogadorDisparada { get; private set; }
    public Personagem? PersonagemMovimentoAtual { get; private set; }
    public string NomePersonagemDisparada { get; private set; } = string.Empty;
    public decimal DeslocamentoNormalMetros { get; private set; } = 9m;
    public decimal DeslocamentoDisparadaMetros => DeslocamentoNormalMetros * 2m;
    public decimal AlcanceMovimentoAtualMetros => ModoDesengajarAtivo ? DeslocamentoNormalMetros : DeslocamentoDisparadaMetros;

    public (int Coluna, int Linha)? DestinoDisparada { get; private set; }
    public decimal DistanciaDestinoMetros { get; private set; }

    // Monstros vivos no raio 1x1 da casa de origem do jogador
    public List<PinoMapaDto> MonstrosAmeacandoOrigem { get; private set; } = new();

    public string DeslocamentoNormalTexto => FormatarMetros(DeslocamentoNormalMetros);
    public string DeslocamentoDisparadaTexto => FormatarMetros(DeslocamentoDisparadaMetros);
    public string AlcanceMovimentoAtualTexto => FormatarMetros(AlcanceMovimentoAtualMetros);
    public string DistanciaDestinoTexto => FormatarMetros(DistanciaDestinoMetros);

    public string DestinoDisparadaTexto =>
        DestinoDisparada is { } d
            ? $"{(char)('A' + Math.Clamp(d.Coluna, 0, GradeColunas - 1))}{d.Linha + 1}"
            : string.Empty;

    public string? NomeProximoJogadorTurno
    {
        get
        {
            var proximo = ObterProximoJogadorTurno();
            return proximo is null ? null : (proximo.NomePersonagem ?? proximo.Username);
        }
    }

    public Item? ArmaSelecionada =>
        ArmasDisponiveisAtaque.FirstOrDefault(a => a.IdItem == IdArmaSelecionada);

    // Todos os alvos visíveis no mapa atual: Monstros, NPCs e outros Jogadores (exclui o próprio atacante)
    public List<PinoMapaDto> AlvosNoMapa =>
        PinosVisiveisNoMapa
            .Where(p => !(p.TipoEntidade == TiposPinoSessao.Personagem &&
                          Estado.IdPersonagemLogado.HasValue &&
                          p.IdEntidade == Estado.IdPersonagemLogado.Value))
            .OrderBy(p => p.TipoEntidade == TiposPinoSessao.Monstro ? 0 : p.TipoEntidade == TiposPinoSessao.Npc ? 1 : 2)
            .ThenBy(p => p.Nome)
            .ToList();

    public PinoMapaDto? AlvoSelecionado =>
        AlvosNoMapa.FirstOrDefault(a =>
            a.TipoEntidade == TipoAlvoSelecionado &&
            a.IdEntidade == IdAlvoSelecionado);

    public bool AlvoSelecionadoVivo =>
        AlvoSelecionado != null &&
        (AlvoSelecionado.PvAtual ?? AlvoSelecionado.PvMaximo ?? 0) > 0;

    private void FecharOutrosModaisCombate()
    {
        ExibirModalDados = false;
        ExibirModalFicha = false;
        ExibirModalAtaque = false;
        ExibirModalEsquivar = false;
        ExibirModalEsconder = false;
        LimparEstadoDisparadaInterno();
        MonstroEmVisualizacao = null;
        PinoMonstroEmVisualizacao = null;
        NpcEmVisualizacao = null;
        PinoNpcEmVisualizacao = null;
        FeedbackAcao = null;
    }

    // =========================================================================
    // SISTEMA DE ESQUIVAR
    // =========================================================================
    public void AbrirModalEsquivar()
    {
        if (Estado.EhMestre)
        {
            FeedbackAcao = "A ação de Esquivar é exclusiva dos jogadores no turno.";
            Notificar();
            return;
        }

        if (!EhMeuTurno)
        {
            FeedbackAcao = "Aguarde o Mestre liberar o seu turno para esquivar.";
            Notificar();
            return;
        }

        FecharOutrosModaisCombate();
        ErroEsquivar = null;
        ExibirModalEsquivar = true;
        Notificar();
    }

    public void FecharModalEsquivar()
    {
        if (ExecutandoEsquivar) return;
        ExibirModalEsquivar = false;
        ErroEsquivar = null;
        Notificar();
    }

    public async Task ConfirmarEsquivarAsync()
    {
        if (ExecutandoEsquivar || !EhMeuTurno || Estado.IdSessao <= 0) return;

        ExecutandoEsquivar = true;
        ErroEsquivar = null;
        Notificar();

        var eu = Estado.Jogadores.FirstOrDefault(j => j.IdAccount == _sessaoUsuario.IdUsuarioLogado);
        string meuNome = eu?.NomePersonagem ?? _sessaoUsuario.NomeUsuario;
        var proximo = ObterProximoJogadorTurno();
        string? nomeProximo = proximo is null ? null : (proximo.NomePersonagem ?? proximo.Username);

        await _dbLock.WaitAsync();
        try
        {
            var (ok, erro) = await _sessaoService.RegistrarEsquivaAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado,
                meuNome,
                proximo?.IdAccount,
                nomeProximo);

            if (!ok)
            {
                ErroEsquivar = erro;
                return;
            }

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;

            ExibirModalEsquivar = false;
            FeedbackAcao = null;
            AbaChatAtiva = CanaisSessao.Chat;
        }
        catch (Exception ex)
        {
            ErroEsquivar = $"Erro ao esquivar: {ex.Message}";
        }
        finally
        {
            ExecutandoEsquivar = false;
            _dbLock.Release();
            Notificar();
        }
    }

    // =========================================================================
    // SISTEMA DE ESCONDER (TESTE DE DESTREZA - FURTIVIDADE)
    // =========================================================================
    public async Task AbrirModalEsconderAsync()
    {
        if (Estado.EhMestre)
        {
            FeedbackAcao = "A ação de Esconder é exclusiva dos jogadores no turno.";
            Notificar();
            return;
        }

        if (!EhMeuTurno)
        {
            FeedbackAcao = "Aguarde o Mestre liberar o seu turno para se esconder.";
            Notificar();
            return;
        }

        if (!Estado.IdPersonagemLogado.HasValue)
        {
            FeedbackAcao = "Você precisa de uma ficha aprovada na sessão para se esconder.";
            Notificar();
            return;
        }

        FecharOutrosModaisCombate();
        ExibirModalEsconder = true;
        CarregandoEsconder = true;
        ErroEsconder = null;
        EtapaEsconder = 1;
        ResultadoEsconderAtual = null;
        BonusExtraEsconder = 0;
        CdEsconder = 12;
        Notificar();

        await _dbLock.WaitAsync();
        try
        {
            var personagem = await _sessaoService.ObterFichaCompletaAsync(Estado.IdPersonagemLogado.Value);
            if (personagem == null)
            {
                ErroEsconder = "Não foi possível carregar sua ficha.";
                return;
            }

            NomePersonagemEsconder = personagem.Nome;
            ModDestrezaEsconder = AtributosBase.CalcularModificador(personagem.Destreza);

            TemProficienciaFurtividade = personagem.Pericias != null &&
                personagem.Pericias.Any(p =>
                    !string.IsNullOrWhiteSpace(p.NomePericia) &&
                    p.NomePericia.Contains("Furtividade", StringComparison.OrdinalIgnoreCase));

            int bonusProfNivel = 2 + (Math.Clamp(personagem.Nivel, 1, 20) - 1) / 4;
            BonusProficienciaEsconder = TemProficienciaFurtividade ? bonusProfNivel : 0;
        }
        catch (Exception ex)
        {
            ErroEsconder = $"Erro ao carregar ficha: {ex.Message}";
        }
        finally
        {
            CarregandoEsconder = false;
            _dbLock.Release();
            Notificar();
        }
    }

    public async Task RolarTesteEsconderAsync()
    {
        if (ExecutandoEsconder || !EhMeuTurno || !Estado.IdPersonagemLogado.HasValue) return;

        ExecutandoEsconder = true;
        ErroEsconder = null;
        Notificar();

        int dado = Random.Shared.Next(1, 21);
        int bonusExtra = Math.Clamp(BonusExtraEsconder, -50, 50);
        int cd = Math.Clamp(CdEsconder, 1, 40);
        int total = dado + ModDestrezaEsconder + BonusProficienciaEsconder + bonusExtra;
        bool passou = dado == 20 || (dado != 1 && total >= cd);

        ResultadoEsconderAtual = new ResultadoEsconderDto
        {
            NomePersonagem = NomePersonagemEsconder,
            DadoD20 = dado,
            ModificadorDestreza = ModDestrezaEsconder,
            TemProficiencia = TemProficienciaFurtividade,
            BonusProficiencia = BonusProficienciaEsconder,
            BonusExtra = bonusExtra,
            Total = total,
            CdAlvo = cd,
            Passou = passou
        };

        var proximo = ObterProximoJogadorTurno();
        string? nomeProximo = proximo is null ? null : (proximo.NomePersonagem ?? proximo.Username);

        await _dbLock.WaitAsync();
        try
        {
            var (ok, erro) = await _sessaoService.RegistrarEsconderAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                Estado.IdMapaAtivo,
                _sessaoUsuario.IdUsuarioLogado,
                Estado.IdPersonagemLogado.Value,
                ResultadoEsconderAtual,
                proximo?.IdAccount,
                nomeProximo);

            if (!ok)
                ErroEsconder = erro;

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;

            EtapaEsconder = 2;
            AbaChatAtiva = CanaisSessao.Chat;
        }
        catch (Exception ex)
        {
            ErroEsconder = $"Erro ao executar teste de Furtividade: {ex.Message}";
        }
        finally
        {
            ExecutandoEsconder = false;
            _dbLock.Release();
            Notificar();
        }
    }

    public void FecharModalEsconder()
    {
        if (ExecutandoEsconder) return;
        ExibirModalEsconder = false;
        ErroEsconder = null;
        EtapaEsconder = 1;
        ResultadoEsconderAtual = null;
        Notificar();
    }

    // =========================================================================
    // SISTEMA DE DISPARADA E DESENGAJAR
    // =========================================================================
    public Task AlternarModoDisparadaAsync() =>
        IniciarModoMovimentoAsync(desengajar: false);

    public Task AlternarModoDesengajarAsync() =>
        IniciarModoMovimentoAsync(desengajar: true);

    private async Task IniciarModoMovimentoAsync(bool desengajar)
    {
        if (ExecutandoDisparada) return;

        if ((desengajar && ModoDesengajarAtivo) || (!desengajar && ModoDisparadaAtivo))
        {
            CancelarDisparada();
            return;
        }

        string nomeAcao = desengajar ? "Desengajar" : "Disparada";

        if (Estado.EhMestre)
        {
            FeedbackAcao = $"A ação de {nomeAcao} é usada pelo jogador no turno dele.";
            Notificar();
            return;
        }

        if (!EhMeuTurno)
        {
            FeedbackAcao = $"Aguarde o Mestre liberar o seu turno para usar {nomeAcao}.";
            Notificar();
            return;
        }

        if (!Estado.IdPersonagemLogado.HasValue || !Estado.IdMapaAtivo.HasValue)
        {
            FeedbackAcao = $"É preciso ter uma ficha aprovada e um mapa ativo para usar {nomeAcao}.";
            Notificar();
            return;
        }

        var meuPino = Estado.Pinos.FirstOrDefault(p =>
            p.IdMapa == Estado.IdMapaAtivo &&
            p.TipoEntidade == TiposPinoSessao.Personagem &&
            p.IdEntidade == Estado.IdPersonagemLogado.Value);

        if (meuPino is null)
        {
            FeedbackAcao = "Seu pino ainda não está posicionado no mapa atual.";
            Notificar();
            return;
        }

        FecharOutrosModaisCombate();

        await _dbLock.WaitAsync();
        try
        {
            PersonagemMovimentoAtual = await _sessaoService.ObterFichaCompletaAsync(Estado.IdPersonagemLogado.Value);
            NomePersonagemDisparada = PersonagemMovimentoAtual?.Nome ?? meuPino.Nome;
            DeslocamentoNormalMetros = ExtrairDeslocamentoEmMetros(PersonagemMovimentoAtual?.Deslocamento);
        }
        finally
        {
            _dbLock.Release();
        }

        PinoJogadorDisparada = meuPino;
        DestinoDisparada = null;
        DistanciaDestinoMetros = 0m;
        ErroDisparada = null;
        ExibirModalConfirmarDisparada = false;
        ModoDisparadaAtivo = !desengajar;
        ModoDesengajarAtivo = desengajar;
        AtualizarMonstrosAmeacandoOrigem();

        if (desengajar)
        {
            FeedbackAcao = $"↺ Desengajar ativo (até {DeslocamentoNormalTexto}, imune a Ataques de Oportunidade): clique em uma casa verde no mapa para recuar.";
        }
        else if (MonstrosAmeacandoOrigem.Any())
        {
            string nomes = string.Join(", ", MonstrosAmeacandoOrigem.Select(m => m.Nome));
            FeedbackAcao = $"◷ Disparada ativa ({DeslocamentoNormalTexto} → {DeslocamentoDisparadaTexto}). ⚠️ Sair do raio 1×1 de {nomes} sem Desengajar causará Ataque de Oportunidade automático.";
        }
        else
        {
            FeedbackAcao = $"◷ Disparada ativa ({DeslocamentoNormalTexto} → {DeslocamentoDisparadaTexto}): clique em uma casa destacada no mapa para se mover.";
        }

        Notificar();
    }

    public void CancelarDisparada()
    {
        if (ExecutandoDisparada) return;
        LimparEstadoDisparadaInterno();
        FeedbackAcao = null;
        Notificar();
    }

    private void LimparEstadoDisparadaInterno()
    {
        ModoDisparadaAtivo = false;
        ModoDesengajarAtivo = false;
        ExibirModalConfirmarDisparada = false;
        PinoJogadorDisparada = null;
        PersonagemMovimentoAtual = null;
        DestinoDisparada = null;
        DistanciaDestinoMetros = 0m;
        ErroDisparada = null;
        MonstrosAmeacandoOrigem.Clear();
    }

    private void AtualizarMonstrosAmeacandoOrigem()
    {
        if (PinoJogadorDisparada is null)
        {
            MonstrosAmeacandoOrigem = new List<PinoMapaDto>();
            return;
        }

        MonstrosAmeacandoOrigem = PinosVisiveisNoMapa
            .Where(p => p.TipoEntidade == TiposPinoSessao.Monstro
                        && (p.PvAtual ?? p.PvMaximo ?? 0) > 0
                        && CalcularDistanciaQuadrados(PinoJogadorDisparada.Coluna, PinoJogadorDisparada.Linha, p.Coluna, p.Linha) <= 1)
            .ToList();
    }

    private List<PinoMapaDto> ObterMonstrosQueAtacamAoMover(int colDestino, int linDestino)
    {
        if (ModoDesengajarAtivo || MonstrosAmeacandoOrigem.Count == 0)
            return new List<PinoMapaDto>();

        return MonstrosAmeacandoOrigem
            .Where(m => CalcularDistanciaQuadrados(colDestino, linDestino, m.Coluna, m.Linha) > 1)
            .ToList();
    }

    public bool EhCasaOrigemDisparada(int coluna, int linha) =>
        ModoMovimentoAtivo &&
        PinoJogadorDisparada is not null &&
        PinoJogadorDisparada.Coluna == coluna &&
        PinoJogadorDisparada.Linha == linha;

    public bool CasaDentroDoAlcanceDisparada(int coluna, int linha)
    {
        if (!ModoMovimentoAtivo || PinoJogadorDisparada is null)
            return false;

        decimal dist = CalcularDistanciaMetros(PinoJogadorDisparada.Coluna, PinoJogadorDisparada.Linha, coluna, linha);
        return dist > 0m && dist <= AlcanceMovimentoAtualMetros;
    }

    public bool CelulaProvocaAtaqueOportunidade(int coluna, int linha)
    {
        if (!ModoDisparadaAtivo || ModoDesengajarAtivo || MonstrosAmeacandoOrigem.Count == 0)
            return false;

        if (!CasaDentroDoAlcanceDisparada(coluna, linha))
            return false;

        return ObterMonstrosQueAtacamAoMover(coluna, linha).Count > 0;
    }

    public string ObterTooltipCelulaDisparada(int coluna, int linha, string coordTexto)
    {
        if (!ModoMovimentoAtivo || PinoJogadorDisparada is null)
            return $"{coordTexto} (1,5m)";

        if (PinoJogadorDisparada.Coluna == coluna && PinoJogadorDisparada.Linha == linha)
            return $"{coordTexto} · Sua posição atual";

        decimal dist = CalcularDistanciaMetros(PinoJogadorDisparada.Coluna, PinoJogadorDisparada.Linha, coluna, linha);
        if (dist > AlcanceMovimentoAtualMetros)
            return $"{coordTexto} · {FormatarMetros(dist)} (fora do alcance de {AlcanceMovimentoAtualTexto})";

        if (ModoDesengajarAtivo)
            return $"{coordTexto} · {FormatarMetros(dist)} (Desengajar seguro — sem Ataque de Oportunidade)";

        var ameacadores = ObterMonstrosQueAtacamAoMover(coluna, linha);
        if (ameacadores.Count > 0)
        {
            string nomes = string.Join(", ", ameacadores.Select(m => m.Nome));
            return $"{coordTexto} · {FormatarMetros(dist)} · ⚠️ Provoca Ataque de Oportunidade de {nomes}";
        }

        return $"{coordTexto} · {FormatarMetros(dist)} (clique para Disparada)";
    }

    private async Task ProcessarCliqueCelulaDisparadaAsync(int coluna, int linha)
    {
        if (ExecutandoDisparada)
            return;

        if (PinoJogadorDisparada is null)
        {
            CancelarDisparada();
            return;
        }

        int colClamped = Math.Clamp(coluna, 0, GradeColunas - 1);
        int linClamped = Math.Clamp(linha, 0, GradeLinhas - 1);

        decimal distancia = CalcularDistanciaMetros(
            PinoJogadorDisparada.Coluna,
            PinoJogadorDisparada.Linha,
            colClamped,
            linClamped);

        if (distancia <= 0m)
        {
            FeedbackAcao = "Escolha uma casa diferente da sua posição atual.";
            Notificar();
            return;
        }

        if (distancia > AlcanceMovimentoAtualMetros)
        {
            string acao = ModoDesengajarAtivo ? "Desengajar" : "Disparada";
            FeedbackAcao = $"Casa fora do alcance de {acao}! Distância: {FormatarMetros(distancia)} (máximo: {AlcanceMovimentoAtualTexto}).";
            Notificar();
            return;
        }

        AtualizarMonstrosAmeacandoOrigem();
        DestinoDisparada = (colClamped, linClamped);
        DistanciaDestinoMetros = distancia;
        ErroDisparada = null;

        await ExecutarMovimentoDiretoAsync();
    }

    public void VoltarEscolhaCasaDisparada()
    {
        if (ExecutandoDisparada) return;
        ExibirModalConfirmarDisparada = false;
        DestinoDisparada = null;
        DistanciaDestinoMetros = 0m;
        ErroDisparada = null;
        Notificar();
    }

    public async Task ConfirmarDisparadaAsync()
    {
        await ExecutarMovimentoDiretoAsync();
    }

    private async Task ExecutarMovimentoDiretoAsync()
    {
        if (ExecutandoDisparada || !EhMeuTurno || PinoJogadorDisparada is null || DestinoDisparada is null || !Estado.IdMapaAtivo.HasValue)
            return;

        ExecutandoDisparada = true;
        ErroDisparada = null;
        Notificar();

        var destino = DestinoDisparada.Value;
        bool ehDesengajar = ModoDesengajarAtivo;
        string origemTexto = PinoJogadorDisparada.CoordenadaTexto;
        string destinoTexto = DestinoDisparadaTexto;
        var proximoJogador = ObterProximoJogadorTurno();
        string nomeProximo = proximoJogador is not null
            ? (proximoJogador.NomePersonagem ?? proximoJogador.Username)
            : string.Empty;

        var monstrosAtacantes = ehDesengajar
            ? new List<PinoMapaDto>()
            : ObterMonstrosQueAtacamAoMover(destino.Coluna, destino.Linha);

        await _dbLock.WaitAsync();
        try
        {
            bool caiuInconsciente = false;
            int danoTotalOportunidade = 0;

            if (!ehDesengajar && monstrosAtacantes.Count > 0)
            {
                var fichaAtual = await _sessaoService.ObterFichaCompletaAsync(PinoJogadorDisparada.IdEntidade)
                                 ?? PersonagemMovimentoAtual;

                int pvMaximo = Math.Max(1, fichaAtual?.PvMaximo ?? PinoJogadorDisparada.PvMaximo ?? 10);
                int pvCorrente = Math.Clamp(fichaAtual?.PvAtual ?? PinoJogadorDisparada.PvAtual ?? pvMaximo, 0, pvMaximo);

                var listaAtaques = new List<AtaqueOportunidadeDto>();
                foreach (var pinoMonstro in monstrosAtacantes)
                {
                    var monstro = Estado.MonstrosCampanha.FirstOrDefault(m => m.IdMonstro == pinoMonstro.IdEntidade);
                    string expressao = ExtrairExpressaoDanoMonstro(monstro);
                    var (qtd, faces, bonus) = InterpretarExpressaoDado(expressao);

                    var rolagens = new List<int>(qtd);
                    for (int i = 0; i < qtd; i++)
                        rolagens.Add(Random.Shared.Next(1, faces + 1));

                    int dano = Math.Max(1, rolagens.Sum() + bonus);
                    danoTotalOportunidade += dano;
                    pvCorrente = Math.Max(0, pvCorrente - dano);

                    listaAtaques.Add(new AtaqueOportunidadeDto
                    {
                        IdMonstro = pinoMonstro.IdEntidade,
                        NomeMonstro = pinoMonstro.Nome,
                        ExpressaoDano = expressao,
                        ResultadosDano = rolagens,
                        ModificadorDano = bonus,
                        DanoTotal = dano,
                        PvAposAtaque = pvCorrente
                    });
                }

                var (okAo, erroAo) = await _sessaoService.RegistrarAtaquesOportunidadeAsync(
                    Estado.IdSessao,
                    _sessaoUsuario.IdUsuarioLogado,
                    PinoJogadorDisparada.IdEntidade,
                    NomePersonagemDisparada,
                    pvCorrente,
                    pvMaximo,
                    listaAtaques);

                if (!okAo)
                {
                    DestinoDisparada = null;
                    FeedbackAcao = erroAo ?? "Erro ao registrar Ataque de Oportunidade.";
                    return;
                }

                caiuInconsciente = pvCorrente <= 0;
            }

            if (!caiuInconsciente)
            {
                var (okMove, erroMove) = await _sessaoService.PosicionarPinoAsync(
                    Estado.IdCampanha,
                    Estado.IdSessao,
                    Estado.IdMapaAtivo,
                    _sessaoUsuario.IdUsuarioLogado,
                    TiposPinoSessao.Personagem,
                    PinoJogadorDisparada.IdEntidade,
                    destino.Coluna,
                    destino.Linha,
                    PinoJogadorDisparada.Visivel);

                if (!okMove)
                {
                    DestinoDisparada = null;
                    FeedbackAcao = erroMove ?? "Não foi possível mover seu pino.";
                    return;
                }

                string textoTurno = proximoJogador is not null
                    ? $" Turno passado para {nomeProximo}."
                    : " Turno encerrado.";

                string mensagem = ehDesengajar
                    ? $"↺ {NomePersonagemDisparada} usou Desengajar e recuou com segurança de {origemTexto} para {destinoTexto} ({DistanciaDestinoTexto}), evitando Ataques de Oportunidade.{textoTurno}"
                    : $"◷ {NomePersonagemDisparada} usou Disparada ({DeslocamentoNormalTexto} → {DeslocamentoDisparadaTexto}) e moveu-se de {origemTexto} para {destinoTexto} ({DistanciaDestinoTexto}).{textoTurno}";

                await _sessaoService.EnviarMensagemChatAsync(
                    Estado.IdSessao,
                    _sessaoUsuario.IdUsuarioLogado,
                    mensagem);
            }

            var (okTurno, erroTurno) = await _sessaoService.DefinirTurnoJogadorAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado,
                proximoJogador?.IdAccount,
                proximoJogador is not null ? nomeProximo : null);

            if (!okTurno)
            {
                DestinoDisparada = null;
                FeedbackAcao = erroTurno ?? "Ação concluída, mas não foi possível passar o turno.";
                return;
            }

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;

            LimparEstadoDisparadaInterno();
            if (caiuInconsciente)
            {
                FeedbackAcao = $"☠️ Ataque de Oportunidade (-{danoTotalOportunidade} PV)! Você caiu inconsciente antes de completar o movimento.";
                AbaChatAtiva = CanaisSessao.Combate;
            }
            else if (danoTotalOportunidade > 0)
            {
                FeedbackAcao = $"⚡ Ataque de Oportunidade! Você sofreu -{danoTotalOportunidade} PV ao sair do raio 1×1 sem Desengajar.";
                AbaChatAtiva = CanaisSessao.Combate;
            }
            else
            {
                FeedbackAcao = null;
                AbaChatAtiva = CanaisSessao.Chat;
            }
        }
        catch (Exception ex)
        {
            DestinoDisparada = null;
            FeedbackAcao = $"Erro ao concluir movimento: {ex.Message}";
        }
        finally
        {
            ExecutandoDisparada = false;
            _dbLock.Release();
            Notificar();
        }
    }

    private ParticipanteSessaoDto? ObterProximoJogadorTurno()
    {
        var jogadores = Estado.Jogadores
            .Where(j => j.Online && j.IdPersonagem.HasValue)
            .ToList();

        if (jogadores.Count == 0)
        {
            jogadores = Estado.Jogadores
                .Where(j => j.IdPersonagem.HasValue)
                .ToList();
        }

        if (jogadores.Count <= 1)
            return null;

        int indiceAtual = jogadores.FindIndex(j => j.IdAccount == _sessaoUsuario.IdUsuarioLogado);
        if (indiceAtual < 0)
            return jogadores.FirstOrDefault();

        int proximoIndice = (indiceAtual + 1) % jogadores.Count;
        var proximo = jogadores[proximoIndice];
        return proximo.IdAccount == _sessaoUsuario.IdUsuarioLogado ? null : proximo;
    }

    private static int CalcularDistanciaQuadrados(int colOrigem, int linOrigem, int colDestino, int linDestino) =>
        Math.Max(Math.Abs(colDestino - colOrigem), Math.Abs(linDestino - linOrigem));

    private static decimal CalcularDistanciaMetros(int colOrigem, int linOrigem, int colDestino, int linDestino) =>
        CalcularDistanciaQuadrados(colOrigem, linOrigem, colDestino, linDestino) * 1.5m;

    private static string ExtrairExpressaoDanoMonstro(Monstro? monstro)
    {
        if (monstro is not null)
        {
            string?[] campos = [monstro.Habilidades, monstro.Descricao];
            foreach (var texto in campos)
            {
                if (string.IsNullOrWhiteSpace(texto)) continue;

                var match = Regex.Match(texto, @"\b(\d+d\d+(?:\s*[+-]\s*\d+)?)\b", RegexOptions.IgnoreCase);
                if (match.Success)
                    return match.Groups[1].Value.Replace(" ", "");
            }

            int bonusNd = Math.Max(0, (int)Math.Floor(monstro.NivelDesafio ?? 1m));
            return bonusNd > 0 ? $"1d6+{bonusNd}" : "1d4";
        }

        return "1d6";
    }

    private static decimal ExtrairDeslocamentoEmMetros(object? valorDeslocamento)
    {
        if (valorDeslocamento is null) return 9m;
        if (valorDeslocamento is decimal d && d > 0) return d;
        if (valorDeslocamento is int i && i > 0) return i;
        if (valorDeslocamento is double db && db > 0) return (decimal)db;

        var texto = valorDeslocamento.ToString()?.Replace(',', '.');
        if (string.IsNullOrWhiteSpace(texto)) return 9m;

        var match = Regex.Match(texto, @"\d+(\.\d+)?");
        if (match.Success &&
            decimal.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var metros) &&
            metros > 0)
        {
            return metros;
        }

        return 9m;
    }

    private static string FormatarMetros(decimal metros) =>
        $"{metros.ToString("0.#", new CultureInfo("pt-BR"))} m";

    // =========================================================================
    // SISTEMA DE ATAQUE (ARMA DO INVENTÁRIO -> ALVO -> D20 VS CA -> DANO)
    // =========================================================================
    public async Task AbrirModalAtaqueAsync()
    {
        if (Estado.EhMestre)
        {
            FeedbackAcao = "A ação de ataque usa as armas do inventário do jogador.";
            Notificar();
            return;
        }

        if (!Estado.IdPersonagemLogado.HasValue)
        {
            FeedbackAcao = "Você precisa ter uma ficha aprovada na sessão para atacar.";
            Notificar();
            return;
        }

        if (!EhMeuTurno)
        {
            FeedbackAcao = "Aguarde o Mestre liberar o seu turno para realizar ações.";
            Notificar();
            return;
        }

        FecharOutrosModaisCombate();

        ExibirModalAtaque = true;
        CarregandoAtaque = true;
        ErroModalAtaque = null;
        EtapaAtaque = 1;
        ResultadoAtaqueAtual = null;
        ArmasDisponiveisAtaque = new List<Item>();
        Notificar();

        await _dbLock.WaitAsync();
        try
        {
            PersonagemAtacante = await _sessaoService.ObterFichaCompletaAsync(Estado.IdPersonagemLogado.Value);
            if (PersonagemAtacante == null)
            {
                ErroModalAtaque = "Não foi possível carregar sua ficha.";
                return;
            }

            var itensInventario = PersonagemAtacante.Inventario?.Itens?
                .Where(ii => ii.Quantidade > 0)
                .Select(ii => ii.Item ?? Estado.ItensCampanha.FirstOrDefault(ic => ic.IdItem == ii.IdItem))
                .Where(i => i != null)
                .Select(i => i!)
                .ToList() ?? new List<Item>();

            ArmasDisponiveisAtaque = itensInventario
                .Where(i => !string.IsNullOrWhiteSpace(i.Dano) ||
                            (!string.IsNullOrWhiteSpace(i.Tipo) &&
                             i.Tipo.Contains("arma", StringComparison.OrdinalIgnoreCase)))
                .GroupBy(i => i.IdItem)
                .Select(g => g.First())
                .OrderBy(i => i.Nome)
                .ToList();

            IdArmaSelecionada = ArmasDisponiveisAtaque.FirstOrDefault()?.IdItem ?? 0;
            AplicarModificadoresPadraoArma(ArmaSelecionada);
            SelecionarPrimeiroAlvoVivo();
        }
        catch (Exception ex)
        {
            ErroModalAtaque = $"Erro ao carregar armas do inventário: {ex.Message}";
        }
        finally
        {
            CarregandoAtaque = false;
            _dbLock.Release();
            Notificar();
        }
    }

    private void SelecionarPrimeiroAlvoVivo()
    {
        var primeiroVivo = AlvosNoMapa.FirstOrDefault(a => (a.PvAtual ?? a.PvMaximo ?? 0) > 0);
        TipoAlvoSelecionado = primeiroVivo?.TipoEntidade;
        IdAlvoSelecionado = primeiroVivo?.IdEntidade ?? 0;
    }

    public void SelecionarArmaAtaque(int idItem)
    {
        IdArmaSelecionada = idItem;
        AplicarModificadoresPadraoArma(ArmaSelecionada);
        Notificar();
    }

    public void SelecionarAlvoAtaque(string tipoEntidade, int idEntidade)
    {
        var alvo = AlvosNoMapa.FirstOrDefault(a =>
            a.TipoEntidade == tipoEntidade &&
            a.IdEntidade == idEntidade);

        if (alvo == null || (alvo.PvAtual ?? alvo.PvMaximo ?? 0) <= 0)
            return;

        TipoAlvoSelecionado = tipoEntidade;
        IdAlvoSelecionado = idEntidade;
        ErroModalAtaque = null;
        Notificar();
    }

    private void AplicarModificadoresPadraoArma(Item? arma)
    {
        if (PersonagemAtacante == null)
        {
            ModificadorAtaque = 0;
            ModificadorDano = 0;
            return;
        }

        int modFor = AtributosBase.CalcularModificador(PersonagemAtacante.Forca);
        int modDes = AtributosBase.CalcularModificador(PersonagemAtacante.Destreza);

        bool usaDestreza = arma != null && (
            (!string.IsNullOrWhiteSpace(arma.Propriedades) &&
             (arma.Propriedades.Contains("Acuidade", StringComparison.OrdinalIgnoreCase) ||
              arma.Propriedades.Contains("Distância", StringComparison.OrdinalIgnoreCase) ||
              arma.Propriedades.Contains("Munição", StringComparison.OrdinalIgnoreCase))) ||
            (!string.IsNullOrWhiteSpace(arma.Tipo) &&
             arma.Tipo.Contains("Distância", StringComparison.OrdinalIgnoreCase)));

        int mod = usaDestreza ? Math.Max(modFor, modDes) : modFor;
        ModificadorAtaque = mod;
        ModificadorDano = mod;
    }

    public async Task RolarDadoAtaqueAsync()
    {
        if (ExecutandoAtaque || Estado.IdSessao <= 0) return;

        if (!EhMeuTurno)
        {
            ErroModalAtaque = "Aguarde o Mestre liberar o seu turno para atacar.";
            Notificar();
            return;
        }

        var arma = ArmaSelecionada;
        var alvo = AlvoSelecionado;

        if (arma == null)
        {
            ErroModalAtaque = "Selecione uma arma do seu inventário.";
            Notificar();
            return;
        }

        if (alvo == null)
        {
            ErroModalAtaque = "Selecione um alvo no mapa.";
            Notificar();
            return;
        }

        int pvMax = alvo.PvMaximo ?? 1;
        int pvAtual = alvo.PvAtual ?? pvMax;

        if (pvAtual <= 0)
        {
            ErroModalAtaque = $"{alvo.Nome} já está com 0 PV e não pode ser atacado novamente.";
            Notificar();
            return;
        }

        ErroModalAtaque = null;
        bool alvoEsquivando = alvo.Esquivando;
        int dado1 = Random.Shared.Next(1, 21);
        int dado2 = alvoEsquivando ? Random.Shared.Next(1, 21) : dado1;
        int dadoD20 = alvoEsquivando ? Math.Min(dado1, dado2) : dado1;

        int modAtk = Math.Clamp(ModificadorAtaque, -50, 50);
        int totalAtk = dadoD20 + modAtk;
        int caAlvo = alvo.ClasseArmadura ?? 10;

        bool ehCritico = dadoD20 == 20;
        bool acertou = ehCritico || totalAtk >= caAlvo;

        ResultadoAtaqueAtual = new RegistroAtaqueDto
        {
            NomeAtacante = PersonagemAtacante?.Nome ?? _sessaoUsuario.NomeUsuario,
            NomeArma = arma.Nome,
            ExpressaoDano = string.IsNullOrWhiteSpace(arma.Dano) ? "1d4" : arma.Dano.Trim(),
            TipoDano = arma.TipoDano,
            TipoAlvo = alvo.TipoEntidade,
            IdEntidadeAlvo = alvo.IdEntidade,
            NomeAlvo = alvo.Nome,
            CaAlvo = caAlvo,
            AlvoEsquivando = alvoEsquivando,
            DadoAtaque1 = dado1,
            DadoAtaque2 = dado2,
            DadoAtaque = dadoD20,
            ModificadorAtaque = modAtk,
            TotalAtaque = totalAtk,
            EhCritico = ehCritico,
            Acertou = acertou,
            PvAnterior = pvAtual,
            PvNovo = pvAtual,
            PvMaximo = pvMax
        };

        if (acertou)
        {
            EtapaAtaque = 2;
            Notificar();
            return;
        }

        ExecutandoAtaque = true;
        Notificar();

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, erro) = await _sessaoService.RegistrarAtaqueAsync(
                Estado.IdSessao,
                Estado.IdMapaAtivo,
                _sessaoUsuario.IdUsuarioLogado,
                ResultadoAtaqueAtual);

            if (!sucesso)
                ErroModalAtaque = erro;

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;

            EtapaAtaque = 3;
        }
        finally
        {
            ExecutandoAtaque = false;
            _dbLock.Release();
            Notificar();
        }
    }

    public async Task RolarDanoEAplicarAsync()
    {
        if (ExecutandoAtaque || ResultadoAtaqueAtual == null || !ResultadoAtaqueAtual.Acertou)
            return;

        ExecutandoAtaque = true;
        ErroModalAtaque = null;
        Notificar();

        var (qtd, faces, bonusFormula) = InterpretarExpressaoDado(ResultadoAtaqueAtual.ExpressaoDano);
        int qtdDadosRolados = ResultadoAtaqueAtual.EhCritico ? qtd * 2 : qtd;

        var rolagensDano = new List<int>(qtdDadosRolados);
        for (int i = 0; i < qtdDadosRolados; i++)
        {
            rolagensDano.Add(Random.Shared.Next(1, faces + 1));
        }

        int modTotal = Math.Clamp(ModificadorDano, -50, 50) + bonusFormula;
        int danoTotal = Math.Max(0, rolagensDano.Sum() + modTotal);
        int pvNovo = Math.Max(0, ResultadoAtaqueAtual.PvAnterior - danoTotal);

        ResultadoAtaqueAtual.ResultadosDano = rolagensDano;
        ResultadoAtaqueAtual.ModificadorDano = modTotal;
        ResultadoAtaqueAtual.DanoTotal = danoTotal;
        ResultadoAtaqueAtual.PvNovo = pvNovo;

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, erro) = await _sessaoService.RegistrarAtaqueAsync(
                Estado.IdSessao,
                Estado.IdMapaAtivo,
                _sessaoUsuario.IdUsuarioLogado,
                ResultadoAtaqueAtual);

            if (!sucesso)
                ErroModalAtaque = erro;

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;

            EtapaAtaque = 3;
        }
        finally
        {
            ExecutandoAtaque = false;
            _dbLock.Release();
            Notificar();
        }
    }

    public void NovoAtaque()
    {
        EtapaAtaque = 1;
        ResultadoAtaqueAtual = null;
        ErroModalAtaque = null;

        if (!AlvoSelecionadoVivo)
            SelecionarPrimeiroAlvoVivo();

        Notificar();
    }

    public void FecharModalAtaque()
    {
        ExibirModalAtaque = false;
        ErroModalAtaque = null;
        EtapaAtaque = 1;
        ResultadoAtaqueAtual = null;
        Notificar();
    }

    private static (int Quantidade, int Faces, int Bonus) InterpretarExpressaoDado(string? expressao)
    {
        if (string.IsNullOrWhiteSpace(expressao))
            return (1, 4, 0);

        var match = Regex.Match(expressao.Replace(" ", ""), @"^(\d*)d(\d+)([+-]\d+)?$", RegexOptions.IgnoreCase);
        if (!match.Success)
            return (1, 4, 0);

        int qtd = string.IsNullOrEmpty(match.Groups[1].Value)
            ? 1
            : Math.Clamp(int.Parse(match.Groups[1].Value), 1, 20);
        int faces = Math.Clamp(int.Parse(match.Groups[2].Value), 2, 100);
        int bonus = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

        return (qtd, faces, bonus);
    }
}