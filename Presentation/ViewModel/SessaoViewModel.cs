using Microsoft.AspNetCore.Components;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Presentation.ViewModel;

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

    public event Action? OnChange;

    public bool Carregando { get; private set; } = true;
    public string? ErroCarregamento { get; private set; }
    public string? FeedbackAcao { get; private set; }

    public SessaoEstadoDto Estado { get; private set; } = new();
    public Personagem? FichaEmVisualizacao { get; private set; }

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
        if (Estado.IdSessao <= 0 || _desconectado || ModoRevisaoPosSessao) return;

        await _dbLock.WaitAsync(ct);
        try
        {
            var novoEstado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (novoEstado != null)
            {
                Estado = novoEstado;

                if (!Estado.SessaoAtiva && !Estado.EhMestre)
                {
                    SessaoEncerradaPeloMestre = true;
                    ExibirModalDados = false;
                    ExibirModalFicha = false;
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

    public async Task SelecionarMapaAsync(ChangeEventArgs e)
    {
        if (!Estado.EhMestre || Estado.IdSessao <= 0) return;

        int? idMapaSelecionado = int.TryParse(e.Value?.ToString(), out int parsed) && parsed > 0
            ? parsed
            : null;

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