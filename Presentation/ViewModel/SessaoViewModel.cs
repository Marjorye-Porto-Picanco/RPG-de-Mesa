using Microsoft.AspNetCore.Components;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Presentation.ViewModel;

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
    public string AbaFichaModalInicial { get; private set; } = "atributos";

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

    private async Task SincronizarAgoraAsync(CancellationToken ct = default)
    {
        if (Estado.IdSessao <= 0 || _desconectado) return;

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

        string nomeExibicao = _sessaoUsuario.NomeUsuario;
        var meuJogador = Estado.Jogadores.FirstOrDefault(j => j.IdAccount == _sessaoUsuario.IdUsuarioLogado);
        if (meuJogador != null && !string.IsNullOrWhiteSpace(meuJogador.NomePersonagem))
        {
            nomeExibicao = $"{meuJogador.Username} ({meuJogador.NomePersonagem})";
        }

        await _dbLock.WaitAsync();
        try
        {
            await _sessaoService.RegistrarRolagemDadosAsync(Estado.IdSessao, nomeExibicao, rolagem);

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;

            AbaChatAtiva = CanaisSessao.Registro;
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

    public async Task AbrirFichaPropriaAsync(string abaInicial = "atributos")
    {
        if (!Estado.IdPersonagemLogado.HasValue)
        {
            FeedbackAcao = Estado.EhMestre
                ? "Você está conectado como Mestre. Clique em um jogador na lista à direita para ver a ficha dele."
                : "Você ainda não possui uma ficha aceita nesta campanha.";
            Notificar();
            return;
        }

        await AbrirFichaPorIdAsync(Estado.IdPersonagemLogado.Value, abaInicial);
    }

    public async Task AbrirFichaParticipanteAsync(ParticipanteSessaoDto participante)
    {
        if (!participante.IdPersonagem.HasValue) return;

        if (!Estado.EhMestre && participante.IdAccount != _sessaoUsuario.IdUsuarioLogado)
            return;

        await AbrirFichaPorIdAsync(participante.IdPersonagem.Value, "atributos");
    }

    private async Task AbrirFichaPorIdAsync(int idPersonagem, string abaInicial)
    {
        FeedbackAcao = null;
        ExibirModalDados = false;
        AbaFichaModalInicial = abaInicial;

        await _dbLock.WaitAsync();
        try
        {
            FichaEmVisualizacao = await _sessaoService.ObterFichaCompletaAsync(idPersonagem);
            if (FichaEmVisualizacao == null)
            {
                FeedbackAcao = "Não foi possível carregar os dados da ficha.";
                return;
            }

            ExibirModalFicha = true;
        }
        finally
        {
            _dbLock.Release();
            Notificar();
        }
    }

    public void FecharModalFicha()
    {
        ExibirModalFicha = false;
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

    public async Task SairDaSessaoENavegarAsync()
    {
        await DesconectarInternoAsync();
        _nav.NavigateTo("/campanhas");
    }

    private async Task DesconectarInternoAsync()
    {
        if (_desconectado) return;
        _desconectado = true;

        _syncCts?.Cancel();
        if (_syncTask != null)
        {
            try { await _syncTask; } catch { }
        }

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
        _syncCts?.Dispose();
        _dbLock.Dispose();
    }

    private void Notificar() => OnChange?.Invoke();
}