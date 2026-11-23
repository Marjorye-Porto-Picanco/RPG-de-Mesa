using System.Globalization;
using System.Text.RegularExpressions;
using RPGdeMesa.Application.Services;
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

    // Modo Disparada (Movimento: Botão -> Clique na casa <= 2x deslocamento -> Confirmação -> Próximo turno)
    public bool ModoDisparadaAtivo { get; private set; }
    public bool ExibirModalConfirmarDisparada { get; private set; }
    public bool ExecutandoDisparada { get; private set; }
    public string? ErroDisparada { get; private set; }
    public PinoMapaDto? PinoJogadorDisparada { get; private set; }
    public string NomePersonagemDisparada { get; private set; } = string.Empty;
    public decimal DeslocamentoNormalMetros { get; private set; } = 9m;
    public decimal DeslocamentoDisparadaMetros => DeslocamentoNormalMetros * 2m;
    public (int Coluna, int Linha)? DestinoDisparada { get; private set; }
    public decimal DistanciaDestinoMetros { get; private set; }

    public string DeslocamentoNormalTexto => FormatarMetros(DeslocamentoNormalMetros);
    public string DeslocamentoDisparadaTexto => FormatarMetros(DeslocamentoDisparadaMetros);
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

    // =========================================================================
    // SISTEMA DE DISPARADA (DOBRA DESLOCAMENTO -> CLIQUE NA CASA -> PRÓXIMO TURNO)
    // =========================================================================
    public async Task AlternarModoDisparadaAsync()
    {
        if (ModoDisparadaAtivo)
        {
            CancelarDisparada();
            return;
        }

        if (Estado.EhMestre)
        {
            FeedbackAcao = "A ação de Disparada é usada pelo jogador no turno dele.";
            Notificar();
            return;
        }

        if (!EhMeuTurno)
        {
            FeedbackAcao = "Aguarde o Mestre liberar o seu turno para usar Disparada.";
            Notificar();
            return;
        }

        if (!Estado.IdPersonagemLogado.HasValue || !Estado.IdMapaAtivo.HasValue)
        {
            FeedbackAcao = "É preciso ter uma ficha aprovada e um mapa ativo para usar Disparada.";
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

        ExibirModalDados = false;
        ExibirModalFicha = false;
        ExibirModalAtaque = false;
        MonstroEmVisualizacao = null;
        PinoMonstroEmVisualizacao = null;
        NpcEmVisualizacao = null;
        PinoNpcEmVisualizacao = null;

        await _dbLock.WaitAsync();
        try
        {
            var personagem = await _sessaoService.ObterFichaCompletaAsync(Estado.IdPersonagemLogado.Value);
            NomePersonagemDisparada = personagem?.Nome ?? meuPino.Nome;
            DeslocamentoNormalMetros = ExtrairDeslocamentoEmMetros(personagem?.Deslocamento);
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
        ModoDisparadaAtivo = true;
        FeedbackAcao = $"◷ Disparada ativa ({DeslocamentoNormalTexto} → {DeslocamentoDisparadaTexto}): clique em uma casa destacada no mapa para se mover.";
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
        ExibirModalConfirmarDisparada = false;
        PinoJogadorDisparada = null;
        DestinoDisparada = null;
        DistanciaDestinoMetros = 0m;
        ErroDisparada = null;
    }

    public bool EhCasaOrigemDisparada(int coluna, int linha) =>
        ModoDisparadaAtivo &&
        PinoJogadorDisparada is not null &&
        PinoJogadorDisparada.Coluna == coluna &&
        PinoJogadorDisparada.Linha == linha;

    public bool CasaDentroDoAlcanceDisparada(int coluna, int linha)
    {
        if (!ModoDisparadaAtivo || PinoJogadorDisparada is null)
            return false;

        decimal dist = CalcularDistanciaMetros(PinoJogadorDisparada.Coluna, PinoJogadorDisparada.Linha, coluna, linha);
        return dist > 0m && dist <= DeslocamentoDisparadaMetros;
    }

    public string ObterTooltipCelulaDisparada(int coluna, int linha, string coordTexto)
    {
        if (!ModoDisparadaAtivo || PinoJogadorDisparada is null)
            return $"{coordTexto} (1,5m)";

        if (PinoJogadorDisparada.Coluna == coluna && PinoJogadorDisparada.Linha == linha)
            return $"{coordTexto} · Sua posição atual";

        decimal dist = CalcularDistanciaMetros(PinoJogadorDisparada.Coluna, PinoJogadorDisparada.Linha, coluna, linha);
        return dist <= DeslocamentoDisparadaMetros
            ? $"{coordTexto} · {FormatarMetros(dist)} (clique para Disparada)"
            : $"{coordTexto} · {FormatarMetros(dist)} (fora do alcance de {DeslocamentoDisparadaTexto})";
    }

    private void ProcessarCliqueCelulaDisparada(int coluna, int linha)
    {
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

        if (distancia > DeslocamentoDisparadaMetros)
        {
            FeedbackAcao = $"Casa fora do alcance da Disparada! Distância: {FormatarMetros(distancia)} (máximo: {DeslocamentoDisparadaTexto}).";
            Notificar();
            return;
        }

        DestinoDisparada = (colClamped, linClamped);
        DistanciaDestinoMetros = distancia;
        ErroDisparada = null;
        ExibirModalConfirmarDisparada = true;
        Notificar();
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
        if (ExecutandoDisparada || !EhMeuTurno || PinoJogadorDisparada is null || DestinoDisparada is null || !Estado.IdMapaAtivo.HasValue)
            return;

        ExecutandoDisparada = true;
        ErroDisparada = null;
        Notificar();

        var destino = DestinoDisparada.Value;
        string origemTexto = PinoJogadorDisparada.CoordenadaTexto;
        string destinoTexto = DestinoDisparadaTexto;
        var proximoJogador = ObterProximoJogadorTurno();

        await _dbLock.WaitAsync();
        try
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
                ErroDisparada = erroMove ?? "Não foi possível mover seu pino.";
                return;
            }

            string nomeProximo = proximoJogador is not null
                ? (proximoJogador.NomePersonagem ?? proximoJogador.Username)
                : string.Empty;

            string textoTurno = proximoJogador is not null
                ? $" Turno passado para {nomeProximo}."
                : " Turno encerrado.";

            string mensagem = $"◷ {NomePersonagemDisparada} usou Disparada ({DeslocamentoNormalTexto} → {DeslocamentoDisparadaTexto}) e moveu-se de {origemTexto} para {destinoTexto} ({DistanciaDestinoTexto}).{textoTurno}";

            await _sessaoService.EnviarMensagemChatAsync(
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado,
                mensagem);

            var (okTurno, erroTurno) = await _sessaoService.DefinirTurnoJogadorAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado,
                proximoJogador?.IdAccount,
                proximoJogador is not null ? nomeProximo : null);

            if (!okTurno)
            {
                ErroDisparada = erroTurno ?? "Pino movido, mas não foi possível passar o turno.";
                return;
            }

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;

            LimparEstadoDisparadaInterno();
            FeedbackAcao = null;
            AbaChatAtiva = CanaisSessao.Chat;
        }
        catch (Exception ex)
        {
            ErroDisparada = $"Erro ao concluir Disparada: {ex.Message}";
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

    private static decimal CalcularDistanciaMetros(int colOrigem, int linOrigem, int colDestino, int linDestino)
    {
        int quadrados = Math.Max(Math.Abs(colDestino - colOrigem), Math.Abs(linDestino - linOrigem));
        return quadrados * 1.5m;
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

        ExibirModalDados = false;
        ExibirModalFicha = false;
        LimparEstadoDisparadaInterno();
        MonstroEmVisualizacao = null;
        PinoMonstroEmVisualizacao = null;
        NpcEmVisualizacao = null;
        PinoNpcEmVisualizacao = null;
        FeedbackAcao = null;

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
        int dadoD20 = Random.Shared.Next(1, 21);
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