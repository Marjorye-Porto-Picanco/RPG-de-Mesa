using Microsoft.AspNetCore.Components;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Sessoes;

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

public partial class SessaoViewModel : IAsyncDisposable
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

    // Controle de Turno
    public bool AlterandoTurno { get; private set; }
    public bool EhMeuTurno =>
        !Estado.EhMestre &&
        Estado.IdAccountTurno.HasValue &&
        Estado.IdAccountTurno.Value == _sessaoUsuario.IdUsuarioLogado;

    // Encerramento pelo Mestre (para jogadores)
    public bool SessaoEncerradaPeloMestre { get; private set; }

    // Modal de Dados
    public bool ExibirModalDados { get; private set; }

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
        ExibirModalEsquivar = false;
        ExibirModalEsconder = false;
        LimparEstadoDisparadaInterno();
        ExibirPainelPinos = false;
        PinoParaPosicionar = null;
        PinoArrastando = null;
        CelulaDestinoArraste = null;
        _moveuDuranteArraste = false;
        MonstroEmVisualizacao = null;
        PinoMonstroEmVisualizacao = null;
        NpcEmVisualizacao = null;
        PinoNpcEmVisualizacao = null;
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
                        PinoMonstroEmVisualizacao = pinoAtualizado;
                }

                if (PinoNpcEmVisualizacao != null)
                {
                    var pinoAtualizado = Estado.Pinos.FirstOrDefault(p =>
                        p.IdMapa == Estado.IdMapaAtivo &&
                        p.TipoEntidade == TiposPinoSessao.Npc &&
                        p.IdEntidade == PinoNpcEmVisualizacao.IdEntidade);
                    if (pinoAtualizado != null)
                        PinoNpcEmVisualizacao = pinoAtualizado;
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
                        SelecionarPrimeiroAlvoVivo();
                    }
                }

                if (ExibirModalEsquivar && !Estado.EhMestre && !EhMeuTurno)
                {
                    ExibirModalEsquivar = false;
                    FeedbackAcao = "O Mestre encerrou o seu turno.";
                }

                if (ExibirModalEsconder && EtapaEsconder == 1 && !Estado.EhMestre && !EhMeuTurno)
                {
                    ExibirModalEsconder = false;
                    FeedbackAcao = "O Mestre encerrou o seu turno.";
                }

                if (ModoMovimentoAtivo)
                {
                    if (!Estado.EhMestre && !EhMeuTurno)
                    {
                        LimparEstadoDisparadaInterno();
                        FeedbackAcao = "O Mestre encerrou o seu turno.";
                    }
                    else if (Estado.IdPersonagemLogado.HasValue && Estado.IdMapaAtivo.HasValue)
                    {
                        var pinoAtual = Estado.Pinos.FirstOrDefault(p =>
                            p.IdMapa == Estado.IdMapaAtivo &&
                            p.TipoEntidade == TiposPinoSessao.Personagem &&
                            p.IdEntidade == Estado.IdPersonagemLogado.Value);

                        if (pinoAtual is null)
                            LimparEstadoDisparadaInterno();
                        else
                            PinoJogadorDisparada = pinoAtual;
                    }
                }

                if (!Estado.SessaoAtiva && !Estado.EhMestre)
                {
                    SessaoEncerradaPeloMestre = true;
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
    // MODAL DE DADOS, FOCO E NAVEGAÇÃO / DESCONEXÃO
    // =========================================================================
    public void AbrirModalDados()
    {
        ExibirModalFicha = false;
        ExibirModalAtaque = false;
        ExibirModalEsquivar = false;
        ExibirModalEsconder = false;
        LimparEstadoDisparadaInterno();
        MonstroEmVisualizacao = null;
        PinoMonstroEmVisualizacao = null;
        NpcEmVisualizacao = null;
        PinoNpcEmVisualizacao = null;
        ExibirModalDados = true;
        Notificar();
    }

    public void FecharModalDados()
    {
        ExibirModalDados = false;
        Notificar();
    }

    public void FocarMapa()
    {
        ExibirModalDados = false;
        ExibirModalFicha = false;
        ExibirModalAtaque = false;
        ExibirModalEsquivar = false;
        ExibirModalEsconder = false;
        MonstroEmVisualizacao = null;
        PinoMonstroEmVisualizacao = null;
        NpcEmVisualizacao = null;
        PinoNpcEmVisualizacao = null;
        if (!ModoMovimentoAtivo)
            FeedbackAcao = null;
        Notificar();
    }

    public void LimparFeedback()
    {
        FeedbackAcao = null;
        PinoParaPosicionar = null;
        if (ModoMovimentoAtivo)
            LimparEstadoDisparadaInterno();
        Notificar();
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