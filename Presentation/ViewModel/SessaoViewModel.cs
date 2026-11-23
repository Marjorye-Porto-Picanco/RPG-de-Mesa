using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Sessoes;
using RPGdeMesa.Domain.ValueObjects;

namespace RPGdeMesa.Presentation.ViewModel;

public sealed record PinoSelecionadoParaColocar(string TipoEntidade, int IdEntidade, string Nome);

public class ItemSlotSessaoDto
{
    public int IdItem { get; set; }
    public int Quantidade { get; set; } = 1;
}

public class SalvarFichaSessaoArgs
{
    public Personagem Personagem { get; set; } = new();
    public Inventario DadosInventario { get; set; } = new();
    public List<(int IdItem, int Quantidade)> Itens { get; set; } = new();
}

public class FichaRevisaoItemVm
{
    public Personagem Personagem { get; set; } = new();
    public int XpGanhoSessao { get; set; }
    public int MoedasPC { get; set; }
    public int MoedasPP { get; set; }
    public int MoedasPE { get; set; }
    public int MoedasPO { get; set; }
    public int MoedasPL { get; set; }
    public string NotasInventario { get; set; } = string.Empty;
    public List<ItemSlotSessaoDto> Itens { get; set; } = new();
}

public class SessaoViewModel : IAsyncDisposable
{
    private readonly SessaoService _sessaoService;
    private readonly SessaoUsuarioService _sessaoUsuario;
    private readonly NavigationManager _nav;
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    private CancellationTokenSource? _syncCts;
    private Task? _syncTask;
    private bool _desconectado;
    private bool _moveuDuranteArraste;

    public event Action? OnChange;

    public bool Carregando { get; private set; } = true;
    public string? ErroCarregamento { get; private set; }
    public string? FeedbackAcao { get; private set; }

    public SessaoEstadoDto Estado { get; private set; } = new();
    public Personagem? FichaEmVisualizacao { get; private set; }

    // Controle de Turno
    public bool AlterandoTurno { get; private set; }
    public bool EhMeuTurno =>
        !Estado.EhMestre &&
        Estado.IdAccountTurno.HasValue &&
        Estado.IdAccountTurno.Value == _sessaoUsuario.IdUsuarioLogado;

    // Encerramento pelo Mestre (para jogadores)
    public bool SessaoEncerradaPeloMestre { get; private set; }

    // Revisão Pós-Sessão (para o Mestre)
    public bool ModoRevisaoPosSessao { get; private set; }
    public bool FinalizandoSessao { get; private set; }
    public bool SalvandoRevisao { get; private set; }
    public string? FeedbackRevisao { get; private set; }
    public List<FichaRevisaoItemVm> FichasRevisao { get; private set; } = new();
    public FichaRevisaoItemVm? FichaRevisaoSelecionada { get; private set; }
    public List<Item> ItensCampanhaRevisao { get; private set; } = new();
    public int XpEmLote { get; set; } = 0;
    public string TituloSessaoRevisao { get; set; } = string.Empty;
    public string ResumoSessaoRevisao { get; set; } = string.Empty;

    // Chat
    public string AbaChatAtiva { get; private set; } = CanaisSessao.Chat;
    public string TextoMensagem { get; set; } = string.Empty;
    public bool EnviandoMensagem { get; private set; }

    // Mapa & Zoom (Grade D&D 20x20 -> 1,5m/quadrado = 30m x 30m)
    public double NivelZoom { get; private set; } = 1.0;
    public const double ZoomMinimo = 0.6;
    public const double ZoomMaximo = 2.6;
    public const int GradeColunas = 20;
    public const int GradeLinhas = 20;
    public const int TamanhoBaseMapaPx = 720;

    // Painel de Pinos e Arraste por Pointer Events (Exclusivo do Mestre)
    public bool ExibirPainelPinos { get; private set; }
    public string AbaPinosAtiva { get; private set; } = "jogadores";
    public PinoSelecionadoParaColocar? PinoParaPosicionar { get; private set; }
    public PinoMapaDto? PinoArrastando { get; private set; }
    public (int Coluna, int Linha)? CelulaDestinoArraste { get; private set; }
    public bool NovoPinoVisivel { get; set; } = true;

    // Modais de Ficha de Monstro / NPC (ao clicar no pino)
    public Monstro? MonstroEmVisualizacao { get; private set; }
    public PinoMapaDto? PinoMonstroEmVisualizacao { get; private set; }
    public int PvAtualMonstroEdicao { get; set; }
    public bool SalvandoPvMonstro { get; private set; }
    public NPC? NpcEmVisualizacao { get; private set; }

    // Modal de Ataque (Combate)
    public bool ExibirModalAtaque { get; private set; }
    public bool CarregandoAtaque { get; private set; }
    public bool ExecutandoAtaque { get; private set; }
    public string? ErroModalAtaque { get; private set; }
    public List<Item> ArmasDisponiveisAtaque { get; private set; } = new();
    public Personagem? PersonagemAtacante { get; private set; }
    public int IdArmaSelecionada { get; private set; }
    public int IdMonstroAlvoSelecionado { get; private set; }
    public int ModificadorAtaque { get; set; }
    public int ModificadorDano { get; set; }
    public int EtapaAtaque { get; private set; } = 1; // 1=Escolher arma/alvo, 2=Acertou (rolar dano), 3=Finalizado
    public RegistroAtaqueDto? ResultadoAtaqueAtual { get; private set; }

    public Item? ArmaSelecionada =>
        ArmasDisponiveisAtaque.FirstOrDefault(a => a.IdItem == IdArmaSelecionada);

    public List<PinoMapaDto> MonstrosAlvoNoMapa =>
        PinosVisiveisNoMapa
            .Where(p => p.TipoEntidade == TiposPinoSessao.Monstro)
            .OrderBy(p => p.Nome)
            .ToList();

    public PinoMapaDto? MonstroAlvoSelecionado =>
        MonstrosAlvoNoMapa.FirstOrDefault(m => m.IdEntidade == IdMonstroAlvoSelecionado);

    public bool AlvoSelecionadoVivo =>
        MonstroAlvoSelecionado != null &&
        (MonstroAlvoSelecionado.PvAtual ?? MonstroAlvoSelecionado.PvMaximo ?? 0) > 0;

    // Modais (Mini janelas)
    public bool ExibirModalDados { get; private set; }
    public bool ExibirModalFicha { get; private set; }
    public string AbaFichaModalAtiva { get; private set; } = "atributos";
    public bool CarregandoFichaModal { get; private set; }
    public bool SalvandoFichaModal { get; private set; }
    public string? FeedbackFichaModal { get; private set; }

    // Edição de inventário dentro do modal da ficha
    public List<ItemSlotSessaoDto> ItensModalEdicao { get; private set; } = new();
    public int ModalMoedasPC { get; set; }
    public int ModalMoedasPP { get; set; }
    public int ModalMoedasPE { get; set; }
    public int ModalMoedasPO { get; set; }
    public int ModalMoedasPL { get; set; }
    public string ModalNotasInventario { get; set; } = string.Empty;

    public bool PodeEditarFichaModal =>
        FichaEmVisualizacao != null &&
        (Estado.EhMestre || FichaEmVisualizacao.IdAccount == _sessaoUsuario.IdUsuarioLogado);

    public IEnumerable<MensagemSessaoDto> MensagensAbaAtiva =>
        Estado.Mensagens.Where(m => string.Equals(m.Canal, AbaChatAtiva, StringComparison.OrdinalIgnoreCase));

    // Exibe apenas os pinos pertencentes ao mapa ativo atual
    public IEnumerable<PinoMapaDto> PinosVisiveisNoMapa
    {
        get
        {
            if (!Estado.IdMapaAtivo.HasValue)
                return Enumerable.Empty<PinoMapaDto>();

            var pinosDoMapa = Estado.Pinos.Where(p => p.IdMapa == Estado.IdMapaAtivo);
            return Estado.EhMestre
                ? pinosDoMapa
                : pinosDoMapa.Where(p => p.Visivel);
        }
    }

    public SessaoViewModel(
        SessaoService sessaoService,
        SessaoUsuarioService sessaoUsuario,
        NavigationManager nav)
    {
        _sessaoService = sessaoService;
        _sessaoUsuario = sessaoUsuario;
        _nav = nav;
    }

    public async Task InicializarAsync(int idCampanha, int? idPersonagem = null)
    {
        if (Estado.IdSessao > 0)
        {
            await DesconectarInternoAsync();
            Estado = new();
        }

        Carregando = true;
        ErroCarregamento = null;
        FeedbackAcao = null;
        FeedbackFichaModal = null;
        FeedbackRevisao = null;
        SessaoEncerradaPeloMestre = false;
        ModoRevisaoPosSessao = false;
        ExibirModalDados = false;
        ExibirModalFicha = false;
        ExibirModalAtaque = false;
        ExibirPainelPinos = false;
        PinoParaPosicionar = null;
        PinoArrastando = null;
        CelulaDestinoArraste = null;
        _moveuDuranteArraste = false;
        MonstroEmVisualizacao = null;
        PinoMonstroEmVisualizacao = null;
        NpcEmVisualizacao = null;
        FichaEmVisualizacao = null;
        FichasRevisao.Clear();
        FichaRevisaoSelecionada = null;
        TituloSessaoRevisao = string.Empty;
        ResumoSessaoRevisao = string.Empty;
        _desconectado = false;
        Notificar();

        if (!_sessaoUsuario.EstaLogado)
        {
            _nav.NavigateTo("/login");
            return;
        }

        if (idCampanha <= 0)
        {
            ErroCarregamento = "Campanha inválida. Entre pela tela de campanhas escolhendo sua ficha.";
            Carregando = false;
            Notificar();
            return;
        }

        await _dbLock.WaitAsync();
        try
        {
            var resultado = await _sessaoService.EntrarNaSessaoAsync(
                idCampanha,
                _sessaoUsuario.IdUsuarioLogado,
                _sessaoUsuario.NomeUsuario,
                idPersonagem);

            if (!resultado.Sucesso || resultado.Estado == null)
            {
                ErroCarregamento = resultado.Mensagem;
                Carregando = false;
                Notificar();
                return;
            }

            Estado = resultado.Estado;
            Carregando = false;
            Notificar();
        }
        finally
        {
            _dbLock.Release();
        }

        IniciarSincronizacaoPeriodica();
    }

    private void IniciarSincronizacaoPeriodica()
    {
        _syncCts?.Cancel();
        _syncCts?.Dispose();
        _syncCts = new CancellationTokenSource();
        var token = _syncCts.Token;

        _syncTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2.5));
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    await SincronizarAgoraAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private async Task PararSincronizacaoAsync()
    {
        var cts = _syncCts;
        _syncCts = null;

        if (cts != null)
        {
            try { cts.Cancel(); } catch { }
        }

        var task = _syncTask;
        _syncTask = null;

        if (task != null)
        {
            try { await task; } catch { }
        }

        cts?.Dispose();
    }

    private async Task SincronizarAgoraAsync(CancellationToken ct = default)
    {
        if (Estado.IdSessao <= 0 || _desconectado || ModoRevisaoPosSessao || PinoArrastando != null)
            return;

        await _dbLock.WaitAsync(ct);
        try
        {
            if (PinoArrastando != null) return;

            var novoEstado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (novoEstado != null)
            {
                Estado = novoEstado;

                if (PinoMonstroEmVisualizacao != null)
                {
                    var pinoAtualizado = Estado.Pinos.FirstOrDefault(p =>
                        p.IdMapa == Estado.IdMapaAtivo &&
                        p.TipoEntidade == TiposPinoSessao.Monstro &&
                        p.IdEntidade == PinoMonstroEmVisualizacao.IdEntidade);
                    if (pinoAtualizado != null)
                    {
                        PinoMonstroEmVisualizacao = pinoAtualizado;
                    }
                }

                if (ExibirModalAtaque && EtapaAtaque == 1)
                {
                    if (!Estado.EhMestre && !EhMeuTurno)
                    {
                        ExibirModalAtaque = false;
                        FeedbackAcao = "O Mestre encerrou o seu turno.";
                    }
                    else if (!AlvoSelecionadoVivo)
                    {
                        var proximoVivo = MonstrosAlvoNoMapa.FirstOrDefault(m => (m.PvAtual ?? m.PvMaximo ?? 0) > 0);
                        IdMonstroAlvoSelecionado = proximoVivo?.IdEntidade ?? 0;
                    }
                }

                if (!Estado.SessaoAtiva && !Estado.EhMestre)
                {
                    SessaoEncerradaPeloMestre = true;
                    ExibirModalDados = false;
                    ExibirModalFicha = false;
                    ExibirModalAtaque = false;
                    MonstroEmVisualizacao = null;
                    PinoMonstroEmVisualizacao = null;
                    NpcEmVisualizacao = null;
                    _desconectado = true;
                    _syncCts?.Cancel();
                }

                Notificar();
            }
        }
        catch
        {
        }
        finally
        {
            _dbLock.Release();
        }
    }

    // =========================================================================
    // CONTROLE DE TURNO PELO MESTRE
    // =========================================================================
    public async Task AlternarTurnoJogadorAsync(ParticipanteSessaoDto jogador)
    {
        if (!Estado.EhMestre || Estado.IdSessao <= 0 || AlterandoTurno) return;

        bool jaEstaNoTurno = Estado.IdAccountTurno == jogador.IdAccount;
        int? novoId = jaEstaNoTurno ? null : jogador.IdAccount;
        string nome = jogador.NomePersonagem ?? jogador.Username;

        await DefinirTurnoInternoAsync(novoId, nome);
    }

    public async Task EncerrarTurnoAtualAsync()
    {
        if (!Estado.EhMestre || Estado.IdSessao <= 0 || AlterandoTurno) return;
        await DefinirTurnoInternoAsync(null, null);
    }

    private async Task DefinirTurnoInternoAsync(int? idAccountJogador, string? nomeJogador)
    {
        AlterandoTurno = true;
        Notificar();

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, erro) = await _sessaoService.DefinirTurnoJogadorAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado,
                idAccountJogador,
                nomeJogador);

            if (!sucesso)
                FeedbackAcao = erro;

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;
        }
        finally
        {
            AlterandoTurno = false;
            _dbLock.Release();
            Notificar();
        }
    }

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
        MonstroEmVisualizacao = null;
        PinoMonstroEmVisualizacao = null;
        NpcEmVisualizacao = null;
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

            var primeiroAlvoVivo = MonstrosAlvoNoMapa.FirstOrDefault(m => (m.PvAtual ?? m.PvMaximo ?? 0) > 0);
            IdMonstroAlvoSelecionado = primeiroAlvoVivo?.IdEntidade ?? 0;
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

    public void SelecionarArmaAtaque(int idItem)
    {
        IdArmaSelecionada = idItem;
        AplicarModificadoresPadraoArma(ArmaSelecionada);
        Notificar();
    }

    public void SelecionarAlvoAtaque(int idMonstro)
    {
        var alvo = MonstrosAlvoNoMapa.FirstOrDefault(m => m.IdEntidade == idMonstro);
        if (alvo == null || (alvo.PvAtual ?? alvo.PvMaximo ?? 0) <= 0)
            return;

        IdMonstroAlvoSelecionado = idMonstro;
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
        var alvo = MonstroAlvoSelecionado;

        if (arma == null)
        {
            ErroModalAtaque = "Selecione uma arma do seu inventário.";
            Notificar();
            return;
        }

        if (alvo == null)
        {
            ErroModalAtaque = "Selecione um monstro alvo no mapa.";
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
            IdMonstroAlvo = alvo.IdEntidade,
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
            EtapaAtaque = 2; // Aguarda o jogador clicar para rolar o dano da arma
            Notificar();
            return;
        }

        // Falhou: registra imediatamente no chat e na aba Combate
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
        {
            var proximoVivo = MonstrosAlvoNoMapa.FirstOrDefault(m => (m.PvAtual ?? m.PvMaximo ?? 0) > 0);
            IdMonstroAlvoSelecionado = proximoVivo?.IdEntidade ?? 0;
        }

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

    // =========================================================================
    // AJUSTE E RESTAURAÇÃO DE PV DO MONSTRO (EXCLUSIVO DO MESTRE)
    // =========================================================================
    public async Task SalvarPvAtualMonstroAsync()
    {
        if (!Estado.EhMestre || MonstroEmVisualizacao == null || SalvandoPvMonstro) return;

        int novoPv = Math.Clamp(PvAtualMonstroEdicao, 0, Math.Max(1, MonstroEmVisualizacao.PvMaximo));
        PvAtualMonstroEdicao = novoPv;

        await AtualizarPvMonstroInternoAsync(MonstroEmVisualizacao.IdMonstro, novoPv);
    }

    public async Task RestaurarPvMaximoMonstroAsync()
    {
        if (!Estado.EhMestre || MonstroEmVisualizacao == null || SalvandoPvMonstro) return;

        PvAtualMonstroEdicao = MonstroEmVisualizacao.PvMaximo;
        await AtualizarPvMonstroInternoAsync(MonstroEmVisualizacao.IdMonstro, MonstroEmVisualizacao.PvMaximo);
    }

    public async Task RestaurarPvMonstroPeloPainelAsync(Monstro monstro)
    {
        if (!Estado.EhMestre || SalvandoPvMonstro) return;
        await AtualizarPvMonstroInternoAsync(monstro.IdMonstro, monstro.PvMaximo);
    }

    private async Task AtualizarPvMonstroInternoAsync(int idMonstro, int novoPv)
    {
        SalvandoPvMonstro = true;
        Notificar();

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, msg) = await _sessaoService.AtualizarPvMonstroNoMapaAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                Estado.IdMapaAtivo,
                _sessaoUsuario.IdUsuarioLogado,
                idMonstro,
                novoPv);

            FeedbackAcao = msg;

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
            {
                Estado = atualizado;
                if (MonstroEmVisualizacao != null && MonstroEmVisualizacao.IdMonstro == idMonstro)
                {
                    PinoMonstroEmVisualizacao = Estado.Pinos.FirstOrDefault(p =>
                        p.IdMapa == Estado.IdMapaAtivo &&
                        p.TipoEntidade == TiposPinoSessao.Monstro &&
                        p.IdEntidade == idMonstro);
                }
            }
        }
        finally
        {
            SalvandoPvMonstro = false;
            _dbLock.Release();
            Notificar();
        }
    }

    // =========================================================================
    // PAINEL DE PINOS POR MAPA, ARRASTE (POINTER EVENTS) E FICHAS DE MONSTRO / NPC
    // =========================================================================
    public void AlternarPainelPinos()
    {
        if (!Estado.EhMestre) return;
        ExibirPainelPinos = !ExibirPainelPinos;
        if (!ExibirPainelPinos)
            PinoParaPosicionar = null;
        Notificar();
    }

    public void MudarAbaPinos(string aba)
    {
        AbaPinosAtiva = aba;
        Notificar();
    }

    public PinoMapaDto? ObterPinoNoMapa(string tipoEntidade, int idEntidade) =>
        Estado.IdMapaAtivo.HasValue
            ? Estado.Pinos.FirstOrDefault(p =>
                p.IdMapa == Estado.IdMapaAtivo &&
                p.TipoEntidade == tipoEntidade &&
                p.IdEntidade == idEntidade)
            : null;

    public string? ObterResumoOutroMapaDoPino(string tipoEntidade, int idEntidade)
    {
        var outros = Estado.Pinos
            .Where(p => p.IdMapa.HasValue
                     && p.IdMapa != Estado.IdMapaAtivo
                     && p.TipoEntidade == tipoEntidade
                     && p.IdEntidade == idEntidade)
            .ToList();

        if (!outros.Any()) return null;

        var nomes = outros.Select(p =>
        {
            var mapa = Estado.MapasDisponiveis.FirstOrDefault(m => m.IdMapa == p.IdMapa);
            string nomeMapa = mapa?.NomeRegiao ?? $"Mapa #{p.IdMapa}";
            return $"{nomeMapa} ({p.CoordenadaTexto})";
        });

        return $"Salvo em: {string.Join(", ", nomes)}";
    }

    public void SelecionarPinoParaPosicionar(string tipoEntidade, int idEntidade, string nome)
    {
        if (!Estado.EhMestre) return;

        if (!Estado.IdMapaAtivo.HasValue)
        {
            FeedbackAcao = "Selecione um mapa na barra superior antes de posicionar pinos.";
            Notificar();
            return;
        }

        if (PinoParaPosicionar is not null &&
            PinoParaPosicionar.TipoEntidade == tipoEntidade &&
            PinoParaPosicionar.IdEntidade == idEntidade)
        {
            PinoParaPosicionar = null;
            FeedbackAcao = null;
        }
        else
        {
            PinoParaPosicionar = new PinoSelecionadoParaColocar(tipoEntidade, idEntidade, nome);
            FeedbackAcao = $"Clique em uma casa de \"{Estado.MapaAtivo?.NomeRegiao}\" para posicionar \"{nome}\".";
        }
        Notificar();
    }

    public void CancelarSelecaoPino()
    {
        PinoParaPosicionar = null;
        FeedbackAcao = null;
        Notificar();
    }

    public void IniciarArrastePino(PinoMapaDto pino)
    {
        if (!Estado.EhMestre) return;
        PinoParaPosicionar = null;
        PinoArrastando = pino;
        CelulaDestinoArraste = (pino.Coluna, pino.Linha);
        _moveuDuranteArraste = false;
        Notificar();
    }

    public void AtualizarCelulaDestinoArraste(int coluna, int linha)
    {
        if (!Estado.EhMestre || PinoArrastando is null) return;
        if (CelulaDestinoArraste == (coluna, linha)) return;

        CelulaDestinoArraste = (coluna, linha);
        if (coluna != PinoArrastando.Coluna || linha != PinoArrastando.Linha)
            _moveuDuranteArraste = true;

        Notificar();
    }

    public void FinalizarArrastePino()
    {
        if (PinoArrastando is null && CelulaDestinoArraste is null) return;
        PinoArrastando = null;
        CelulaDestinoArraste = null;
        _moveuDuranteArraste = false;
        Notificar();
    }

    public async Task SoltarPinoNaCelulaAsync(int coluna, int linha)
    {
        if (!Estado.EhMestre || PinoArrastando is null || Estado.IdSessao <= 0 || !Estado.IdMapaAtivo.HasValue)
        {
            FinalizarArrastePino();
            return;
        }

        var pino = PinoArrastando;
        int? idMapaAlvo = Estado.IdMapaAtivo;
        int colClamped = Math.Clamp(coluna, 0, GradeColunas - 1);
        int linClamped = Math.Clamp(linha, 0, GradeLinhas - 1);

        bool realmenteMoveu = _moveuDuranteArraste && (pino.Coluna != colClamped || pino.Linha != linClamped);

        PinoArrastando = null;
        CelulaDestinoArraste = null;
        _moveuDuranteArraste = false;

        if (!realmenteMoveu)
        {
            Notificar();
            await AoClicarPinoMapaAsync(pino);
            return;
        }

        pino.Coluna = colClamped;
        pino.Linha = linClamped;
        FeedbackAcao = null;
        Notificar();

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, erro) = await _sessaoService.PosicionarPinoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                idMapaAlvo,
                _sessaoUsuario.IdUsuarioLogado,
                pino.TipoEntidade,
                pino.IdEntidade,
                colClamped,
                linClamped,
                pino.Visivel);

            if (!sucesso)
                FeedbackAcao = erro;

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;

            Notificar();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task AoClicarCelulaMapaAsync(int coluna, int linha)
    {
        if (!Estado.EhMestre || PinoParaPosicionar is null || Estado.IdSessao <= 0)
            return;

        if (!Estado.IdMapaAtivo.HasValue)
        {
            FeedbackAcao = "Selecione um mapa antes de posicionar pinos.";
            Notificar();
            return;
        }

        var alvo = PinoParaPosicionar;
        var existente = ObterPinoNoMapa(alvo.TipoEntidade, alvo.IdEntidade);
        bool visivel = existente?.Visivel ?? NovoPinoVisivel;

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, erro) = await _sessaoService.PosicionarPinoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                Estado.IdMapaAtivo,
                _sessaoUsuario.IdUsuarioLogado,
                alvo.TipoEntidade,
                alvo.IdEntidade,
                coluna,
                linha,
                visivel);

            if (!sucesso)
            {
                FeedbackAcao = erro;
            }
            else
            {
                PinoParaPosicionar = null;
                FeedbackAcao = null;
            }

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;

            Notificar();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task AlternarVisibilidadePinoAsync(string tipoEntidade, int idEntidade)
    {
        if (!Estado.EhMestre || Estado.IdSessao <= 0 || !Estado.IdMapaAtivo.HasValue) return;

        var pino = ObterPinoNoMapa(tipoEntidade, idEntidade);
        if (pino == null) return;

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, erro) = await _sessaoService.AlterarVisibilidadePinoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                Estado.IdMapaAtivo,
                _sessaoUsuario.IdUsuarioLogado,
                tipoEntidade,
                idEntidade,
                !pino.Visivel);

            if (!sucesso)
                FeedbackAcao = erro;

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;

            Notificar();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task RemoverPinoDoMapaAsync(string tipoEntidade, int idEntidade)
    {
        if (!Estado.EhMestre || Estado.IdSessao <= 0 || !Estado.IdMapaAtivo.HasValue) return;

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, erro) = await _sessaoService.RemoverPinoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                Estado.IdMapaAtivo,
                _sessaoUsuario.IdUsuarioLogado,
                tipoEntidade,
                idEntidade);

            if (!sucesso)
                FeedbackAcao = erro;

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;

            Notificar();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task AoClicarPinoMapaAsync(PinoMapaDto pino)
    {
        if (pino.TipoEntidade == TiposPinoSessao.Monstro)
        {
            MonstroEmVisualizacao = Estado.MonstrosCampanha.FirstOrDefault(m => m.IdMonstro == pino.IdEntidade);
            PinoMonstroEmVisualizacao = pino;
            PvAtualMonstroEdicao = pino.PvAtual ?? MonstroEmVisualizacao?.PvMaximo ?? 0;
            NpcEmVisualizacao = null;
            ExibirModalDados = false;
            ExibirModalFicha = false;
            ExibirModalAtaque = false;
            Notificar();
            return;
        }

        if (pino.TipoEntidade == TiposPinoSessao.Npc)
        {
            NpcEmVisualizacao = Estado.NpcsCampanha.FirstOrDefault(n => n.IdNpc == pino.IdEntidade);
            MonstroEmVisualizacao = null;
            PinoMonstroEmVisualizacao = null;
            ExibirModalDados = false;
            ExibirModalFicha = false;
            ExibirModalAtaque = false;
            Notificar();
            return;
        }

        if (pino.TipoEntidade == TiposPinoSessao.Personagem)
        {
            var jogador = Estado.Jogadores.FirstOrDefault(j => j.IdPersonagem == pino.IdEntidade);
            if (jogador != null)
            {
                await AbrirFichaParticipanteAsync(jogador);
            }
        }
    }

    public void FecharModalEntidade()
    {
        MonstroEmVisualizacao = null;
        PinoMonstroEmVisualizacao = null;
        NpcEmVisualizacao = null;
        Notificar();
    }

    public async Task SelecionarMapaAsync(ChangeEventArgs e)
    {
        if (!Estado.EhMestre || Estado.IdSessao <= 0) return;

        int? idMapaSelecionado = int.TryParse(e.Value?.ToString(), out int parsed) && parsed > 0
            ? parsed
            : null;

        PinoParaPosicionar = null;
        PinoArrastando = null;
        CelulaDestinoArraste = null;
        _moveuDuranteArraste = false;

        Estado.IdMapaAtivo = idMapaSelecionado;
        Estado.MapaAtivo = Estado.MapasDisponiveis.FirstOrDefault(m => m.IdMapa == idMapaSelecionado);
        Notificar();

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, mensagem) = await _sessaoService.AlterarMapaAtivoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado,
                idMapaSelecionado);

            FeedbackAcao = sucesso ? null : mensagem;

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;

            Notificar();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public void MudarAbaChat(string canal)
    {
        AbaChatAtiva = canal;
        Notificar();
    }

    public async Task EnviarMensagemChatAsync()
    {
        if (string.IsNullOrWhiteSpace(TextoMensagem) || Estado.IdSessao <= 0 || EnviandoMensagem)
            return;

        string mensagemParaEnviar = TextoMensagem.Trim();
        TextoMensagem = string.Empty;
        EnviandoMensagem = true;
        Notificar();

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, erro) = await _sessaoService.EnviarMensagemChatAsync(
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado,
                mensagemParaEnviar);

            if (!sucesso)
                FeedbackAcao = erro;

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;
        }
        finally
        {
            EnviandoMensagem = false;
            _dbLock.Release();
            Notificar();
        }
    }

    public async Task RegistrarRolagemAsync(ResultadoRolagemDto rolagem)
    {
        if (Estado.IdSessao <= 0) return;

        string nomeExibicao = string.IsNullOrWhiteSpace(_sessaoUsuario.NomeUsuario)
            ? $"Usuário #{_sessaoUsuario.IdUsuarioLogado}"
            : _sessaoUsuario.NomeUsuario;

        if (Estado.EhMestre)
        {
            nomeExibicao = $"{nomeExibicao} (Mestre)";
        }
        else
        {
            var meuJogador = Estado.Jogadores.FirstOrDefault(j => j.IdAccount == _sessaoUsuario.IdUsuarioLogado);
            if (meuJogador != null && !string.IsNullOrWhiteSpace(meuJogador.NomePersonagem))
            {
                nomeExibicao = $"{meuJogador.Username} ({meuJogador.NomePersonagem})";
            }
        }

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, erro) = await _sessaoService.RegistrarRolagemDadosAsync(Estado.IdSessao, nomeExibicao, rolagem);
            if (!sucesso)
                FeedbackAcao = erro;

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;

            AbaChatAtiva = CanaisSessao.Chat;
            Notificar();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public void AbrirModalDados()
    {
        ExibirModalFicha = false;
        ExibirModalAtaque = false;
        MonstroEmVisualizacao = null;
        PinoMonstroEmVisualizacao = null;
        NpcEmVisualizacao = null;
        ExibirModalDados = true;
        Notificar();
    }

    public void FecharModalDados()
    {
        ExibirModalDados = false;
        Notificar();
    }

    public void MudarAbaFichaModal(string aba)
    {
        AbaFichaModalAtiva = aba;
        Notificar();
    }

    public async Task AbrirFichaPropriaAsync(string abaInicial = "atributos")
    {
        if (Estado.IdPersonagemLogado.HasValue)
        {
            await AbrirFichaPorIdAsync(Estado.IdPersonagemLogado.Value, abaInicial);
            return;
        }

        if (Estado.EhMestre)
        {
            var primeiroJogadorComFicha = Estado.Jogadores.FirstOrDefault(j => j.IdPersonagem.HasValue);
            if (primeiroJogadorComFicha?.IdPersonagem != null)
            {
                await AbrirFichaPorIdAsync(primeiroJogadorComFicha.IdPersonagem.Value, abaInicial);
                return;
            }

            FeedbackAcao = "Nenhum jogador possui ficha aceita nesta campanha ainda.";
            Notificar();
            return;
        }

        FeedbackAcao = "Você ainda não possui uma ficha aceita nesta campanha.";
        Notificar();
    }

    public async Task AbrirFichaParticipanteAsync(ParticipanteSessaoDto participante, string abaInicial = "atributos")
    {
        if (!participante.IdPersonagem.HasValue)
        {
            if (Estado.EhMestre)
            {
                FeedbackAcao = $"{participante.Username} ainda não possui ficha aceita nesta campanha.";
                Notificar();
            }
            return;
        }

        if (!Estado.EhMestre && participante.IdAccount != _sessaoUsuario.IdUsuarioLogado)
            return;

        await AbrirFichaPorIdAsync(participante.IdPersonagem.Value, abaInicial);
    }

    private async Task AbrirFichaPorIdAsync(int idPersonagem, string abaInicial)
    {
        FeedbackAcao = null;
        FeedbackFichaModal = null;
        ExibirModalDados = false;
        ExibirModalAtaque = false;
        MonstroEmVisualizacao = null;
        PinoMonstroEmVisualizacao = null;
        NpcEmVisualizacao = null;
        AbaFichaModalAtiva = abaInicial;
        CarregandoFichaModal = true;
        Notificar();

        await _dbLock.WaitAsync();
        try
        {
            FichaEmVisualizacao = await _sessaoService.ObterFichaCompletaAsync(idPersonagem);
            if (FichaEmVisualizacao == null)
            {
                FeedbackAcao = "Não foi possível carregar os dados da ficha.";
                ExibirModalFicha = false;
                return;
            }

            CarregarInventarioModal(FichaEmVisualizacao);
            ExibirModalFicha = true;
        }
        catch (Exception ex)
        {
            FeedbackAcao = $"Erro ao abrir ficha: {ex.Message}";
            ExibirModalFicha = false;
        }
        finally
        {
            CarregandoFichaModal = false;
            _dbLock.Release();
            Notificar();
        }
    }

    private void CarregarInventarioModal(Personagem p)
    {
        var inv = p.Inventario;
        ModalMoedasPC = inv?.MoedasPC ?? 0;
        ModalMoedasPP = inv?.MoedasPP ?? 0;
        ModalMoedasPE = inv?.MoedasPE ?? 0;
        ModalMoedasPO = inv?.MoedasPO ?? 0;
        ModalMoedasPL = inv?.MoedasPL ?? 0;
        ModalNotasInventario = inv?.Notas ?? string.Empty;

        ItensModalEdicao = inv?.Itens?
            .Select(ii => new ItemSlotSessaoDto
            {
                IdItem = ii.IdItem,
                Quantidade = Math.Max(1, ii.Quantidade)
            })
            .ToList() ?? new List<ItemSlotSessaoDto>();
    }

    public void AdicionarItemModal()
    {
        int idPadrao = Estado.ItensCampanha.FirstOrDefault()?.IdItem ?? 0;
        ItensModalEdicao.Add(new ItemSlotSessaoDto { IdItem = idPadrao, Quantidade = 1 });
        Notificar();
    }

    public void RemoverItemModal(ItemSlotSessaoDto slot)
    {
        ItensModalEdicao.Remove(slot);
        Notificar();
    }

    public Item? ObterDetalhesItem(int idItem)
    {
        return Estado.ItensCampanha.FirstOrDefault(i => i.IdItem == idItem)
               ?? FichaEmVisualizacao?.Inventario?.Itens?.FirstOrDefault(ii => ii.IdItem == idItem)?.Item;
    }

    public decimal CalcularPesoModal()
    {
        return ItensModalEdicao
            .Where(s => s.IdItem > 0 && s.Quantidade > 0)
            .Sum(s => (ObterDetalhesItem(s.IdItem)?.Peso ?? 0m) * s.Quantidade);
    }

    public async Task SalvarFichaEmVisualizacaoAsync()
    {
        if (SalvandoFichaModal || FichaEmVisualizacao == null || Estado.IdCampanha <= 0) return;

        SalvandoFichaModal = true;
        FeedbackFichaModal = null;
        Notificar();

        var itensAgrupados = ItensModalEdicao
            .Where(s => s.IdItem > 0 && s.Quantidade > 0)
            .GroupBy(s => s.IdItem)
            .Select(g => (IdItem: g.Key, Quantidade: g.Sum(x => x.Quantidade)))
            .ToList();

        decimal capacidade = Math.Round(Math.Max(1, FichaEmVisualizacao.Forca) * 7.5m, 1);

        var dadosInv = new Inventario
        {
            IdPersonagem = FichaEmVisualizacao.IdPersonagem,
            CapacidadeCarga = capacidade,
            PesoAtual = CalcularPesoModal(),
            Dinheiro = Math.Max(0, ModalMoedasPO),
            MoedasPC = Math.Max(0, ModalMoedasPC),
            MoedasPP = Math.Max(0, ModalMoedasPP),
            MoedasPE = Math.Max(0, ModalMoedasPE),
            MoedasPO = Math.Max(0, ModalMoedasPO),
            MoedasPL = Math.Max(0, ModalMoedasPL),
            Notas = ModalNotasInventario
        };

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, mensagem) = await _sessaoService.SalvarFichaNaSessaoAsync(
                Estado.IdCampanha,
                _sessaoUsuario.IdUsuarioLogado,
                FichaEmVisualizacao,
                dadosInv,
                itensAgrupados);

            FeedbackFichaModal = mensagem;

            if (sucesso)
            {
                FichaEmVisualizacao = await _sessaoService.ObterFichaCompletaAsync(FichaEmVisualizacao.IdPersonagem)
                                      ?? FichaEmVisualizacao;
                CarregarInventarioModal(FichaEmVisualizacao);
            }
        }
        catch (Exception ex)
        {
            FeedbackFichaModal = $"Erro ao salvar ficha: {ex.Message}";
        }
        finally
        {
            SalvandoFichaModal = false;
            _dbLock.Release();
            Notificar();
        }
    }

    public void FecharModalFicha()
    {
        ExibirModalFicha = false;
        FeedbackFichaModal = null;
        Notificar();
    }

    public void FocarMapa()
    {
        ExibirModalDados = false;
        ExibirModalFicha = false;
        ExibirModalAtaque = false;
        MonstroEmVisualizacao = null;
        PinoMonstroEmVisualizacao = null;
        NpcEmVisualizacao = null;
        FeedbackAcao = null;
        Notificar();
    }

    public void AumentarZoom()
    {
        NivelZoom = Math.Min(ZoomMaximo, Math.Round(NivelZoom + 0.2, 2));
        Notificar();
    }

    public void DiminuirZoom()
    {
        NivelZoom = Math.Max(ZoomMinimo, Math.Round(NivelZoom - 0.2, 2));
        Notificar();
    }

    public void ResetarZoom()
    {
        NivelZoom = 1.0;
        Notificar();
    }

    public void LimparFeedback()
    {
        FeedbackAcao = null;
        PinoParaPosicionar = null;
        Notificar();
    }

    // =========================================================================
    // FINALIZAR SESSÃO E REVISÃO PÓS-SESSÃO (EXCLUSIVO DO MESTRE)
    // =========================================================================
    public async Task FinalizarSessaoAsync()
    {
        if (!Estado.EhMestre || Estado.IdSessao <= 0 || FinalizandoSessao) return;

        FinalizandoSessao = true;
        FeedbackAcao = null;
        ExibirModalDados = false;
        ExibirModalFicha = false;
        ExibirModalAtaque = false;
        ExibirPainelPinos = false;
        PinoParaPosicionar = null;
        PinoArrastando = null;
        CelulaDestinoArraste = null;
        _moveuDuranteArraste = false;
        MonstroEmVisualizacao = null;
        PinoMonstroEmVisualizacao = null;
        NpcEmVisualizacao = null;
        Notificar();

        _desconectado = true;
        await PararSincronizacaoAsync();

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, mensagem) = await _sessaoService.FinalizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado,
                _sessaoUsuario.NomeUsuario);

            if (!sucesso)
            {
                FeedbackAcao = mensagem;
                _desconectado = false;
                FinalizandoSessao = false;
                Notificar();
                IniciarSincronizacaoPeriodica();
                return;
            }

            var revisao = await _sessaoService.ObterDadosRevisaoPosSessaoAsync(
                Estado.IdCampanha,
                _sessaoUsuario.IdUsuarioLogado);

            if (!revisao.Sucesso || revisao.Dados == null)
            {
                FeedbackAcao = revisao.Mensagem;
                _nav.NavigateTo("/campanhas");
                return;
            }

            ItensCampanhaRevisao = revisao.Dados.ItensCampanha;
            FichasRevisao = revisao.Dados.Fichas.Select(CriarVmRevisao).ToList();
            FichaRevisaoSelecionada = FichasRevisao.FirstOrDefault();
            XpEmLote = 0;
            TituloSessaoRevisao = string.IsNullOrWhiteSpace(Estado.TituloSessao)
                ? $"Sessão #{Estado.NumeroSessao}"
                : Estado.TituloSessao;
            ResumoSessaoRevisao = string.Empty;
            FeedbackRevisao = "Sessão encerrada e todos os jogadores desconectados. Revise as fichas, distribua o XP, registre o resumo da sessão e conclua.";
            ModoRevisaoPosSessao = true;
        }
        finally
        {
            FinalizandoSessao = false;
            _dbLock.Release();
            Notificar();
        }
    }

    private static FichaRevisaoItemVm CriarVmRevisao(Personagem p)
    {
        var inv = p.Inventario;
        var itens = inv?.Itens?
            .Select(ii => new ItemSlotSessaoDto
            {
                IdItem = ii.IdItem,
                Quantidade = Math.Max(1, ii.Quantidade)
            })
            .ToList() ?? new List<ItemSlotSessaoDto>();

        return new FichaRevisaoItemVm
        {
            Personagem = p,
            XpGanhoSessao = 0,
            MoedasPC = inv?.MoedasPC ?? 0,
            MoedasPP = inv?.MoedasPP ?? 0,
            MoedasPE = inv?.MoedasPE ?? 0,
            MoedasPO = inv?.MoedasPO ?? 0,
            MoedasPL = inv?.MoedasPL ?? 0,
            NotasInventario = inv?.Notas ?? string.Empty,
            Itens = itens
        };
    }

    public void SelecionarFichaRevisao(FichaRevisaoItemVm ficha)
    {
        FichaRevisaoSelecionada = ficha;
        Notificar();
    }

    public void AplicarXpEmLote()
    {
        int valor = Math.Max(0, XpEmLote);
        foreach (var ficha in FichasRevisao)
        {
            ficha.XpGanhoSessao = valor;
        }
        FeedbackRevisao = $"XP +{valor} aplicado para todas as {FichasRevisao.Count} fichas.";
        Notificar();
    }

    public void AdicionarItemRevisao(FichaRevisaoItemVm ficha)
    {
        int idPadrao = ItensCampanhaRevisao.FirstOrDefault()?.IdItem ?? 0;
        ficha.Itens.Add(new ItemSlotSessaoDto { IdItem = idPadrao, Quantidade = 1 });
        Notificar();
    }

    public void RemoverItemRevisao(FichaRevisaoItemVm ficha, ItemSlotSessaoDto slot)
    {
        ficha.Itens.Remove(slot);
        Notificar();
    }

    public async Task ConcluirRevisaoPosSessaoAsync()
    {
        if (SalvandoRevisao || Estado.IdCampanha <= 0) return;

        SalvandoRevisao = true;
        FeedbackRevisao = null;
        Notificar();

        await _dbLock.WaitAsync();
        try
        {
            var payload = FichasRevisao.Select(f =>
            {
                var itensAgrupados = f.Itens
                    .Where(i => i.IdItem > 0 && i.Quantidade > 0)
                    .GroupBy(i => i.IdItem)
                    .Select(g => (IdItem: g.Key, Quantidade: g.Sum(x => x.Quantidade)))
                    .ToList();

                decimal pesoTotal = itensAgrupados.Sum(x =>
                {
                    var itemInfo = ItensCampanhaRevisao.FirstOrDefault(ic => ic.IdItem == x.IdItem);
                    return (itemInfo?.Peso ?? 0m) * x.Quantidade;
                });

                decimal capacidade = Math.Round(Math.Max(1, f.Personagem.Forca) * 7.5m, 1);

                var dadosInv = new Inventario
                {
                    IdPersonagem = f.Personagem.IdPersonagem,
                    CapacidadeCarga = capacidade,
                    PesoAtual = pesoTotal,
                    Dinheiro = f.MoedasPO,
                    MoedasPC = Math.Max(0, f.MoedasPC),
                    MoedasPP = Math.Max(0, f.MoedasPP),
                    MoedasPE = Math.Max(0, f.MoedasPE),
                    MoedasPO = Math.Max(0, f.MoedasPO),
                    MoedasPL = Math.Max(0, f.MoedasPL),
                    Notas = f.NotasInventario
                };

                return new RevisaoFichaSalvarDto
                {
                    Personagem = f.Personagem,
                    XpGanhoSessao = Math.Max(0, f.XpGanhoSessao),
                    DadosInventario = dadosInv,
                    Itens = itensAgrupados
                };
            }).ToList();

            var (sucesso, mensagem) = await _sessaoService.ConcluirRevisaoPosSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado,
                payload,
                TituloSessaoRevisao,
                ResumoSessaoRevisao);

            if (!sucesso)
            {
                FeedbackRevisao = mensagem;
                return;
            }

            ModoRevisaoPosSessao = false;
            _nav.NavigateTo($"/sessao-iniciar/{Estado.IdCampanha}");
        }
        finally
        {
            SalvandoRevisao = false;
            _dbLock.Release();
            Notificar();
        }
    }

    public void VoltarParaPreparacaoCampanha()
    {
        if (Estado.IdCampanha > 0)
            _nav.NavigateTo($"/sessao-entrar/{Estado.IdCampanha}");
        else
            _nav.NavigateTo("/campanhas");
    }

    public async Task SairDaSessaoENavegarAsync()
    {
        await DesconectarInternoAsync();
        _nav.NavigateTo("/campanhas");
    }

    private async Task DesconectarInternoAsync()
    {
        if (_desconectado)
        {
            await PararSincronizacaoAsync();
            return;
        }

        _desconectado = true;
        await PararSincronizacaoAsync();

        if (Estado.IdSessao > 0 && _sessaoUsuario.EstaLogado)
        {
            await _dbLock.WaitAsync();
            try
            {
                await _sessaoService.SairDaSessaoAsync(
                    Estado.IdSessao,
                    _sessaoUsuario.IdUsuarioLogado,
                    _sessaoUsuario.NomeUsuario);
            }
            finally
            {
                _dbLock.Release();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DesconectarInternoAsync();
    }

    private void Notificar() => OnChange?.Invoke();
}