using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Presentation.ViewModel;

public sealed record PinoSelecionadoParaColocar(string TipoEntidade, int IdEntidade, string Nome, bool NovaInstancia = false);

public class ItemSlotSessaoDto
{
    public int IdItem { get; set; }
    public int Quantidade { get; set; } = 1;
}

public class MagiaSlotSessaoDto
{
    public int IdMagia { get; set; }
}

public class SalvarFichaSessaoArgs
{
    public Personagem Personagem { get; set; } = new();
    public Inventario DadosInventario { get; set; } = new();
    public List<(int IdItem, int Quantidade)> Itens { get; set; } = new();
    public List<int> IdsMagias { get; set; } = new();
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
    public List<MagiaSlotSessaoDto> Magias { get; set; } = new();
}

public partial class SessaoViewModel : IAsyncDisposable
{
    private readonly SessaoService _sessaoService;
    private readonly SessaoUsuarioService _sessaoUsuario;
    private readonly NavigationManager _nav;
    private readonly ILogger<SessaoViewModel> _logger;
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    private CancellationTokenSource? _syncCts;
    private Task? _syncTask;
    private bool _desconectado;
    private (int? Jogador, Guid Id)? _turnoPendente;

    public event Action? OnChange;

    public bool Carregando { get; private set; } = true;
    public string? ErroCarregamento { get; private set; }
    public string? FeedbackAcao { get; private set; }
    public bool Reconectando { get; private set; }

    public SessaoEstadoDto Estado { get; private set; } = new();

    public bool AlterandoTurno { get; private set; }
    private int _acoesUsadasNoTurno;
    private int _acoesExtrasNoTurno;
    private int? _turnoObservado;
    private decimal _distanciaMovidaNoTurno;
    private bool _disparadaUsadaNoTurno;
    public int AcoesDisponiveisNoTurno => Math.Max(0, 1 + _acoesExtrasNoTurno - _acoesUsadasNoTurno);
    public bool EhMeuTurno =>
        !Estado.EhMestre &&
        Estado.IdAccountTurno.HasValue &&
        Estado.IdAccountTurno.Value == _sessaoUsuario.IdUsuarioLogado;

    public bool SessaoEncerradaPeloMestre { get; private set; }

    public bool ExibirModalDados { get; private set; }

    public SessaoViewModel(
        SessaoService sessaoService,
        SessaoUsuarioService sessaoUsuario,
        NavigationManager nav,
        ILogger<SessaoViewModel> logger)
    {
        _sessaoService = sessaoService;
        _sessaoUsuario = sessaoUsuario;
        _nav = nav;
        _logger = logger;
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
        ExibirModalMagia = false;
        ConjuracaoEmCarregamento = false;
        TurnosRestantesConjuracao = 0;
        _idMagiaEmCarregamento = 0;
        _circuloEmCarregamento = 0;
        ExibirModalEsquivar = false;
        ExibirModalEsconder = false;
        LimparEstadoDisparadaInterno();
        ExibirPainelPinos = false;
        PinoParaPosicionar = null;
        PinoArrastando = null;
        CelulaDestinoArraste = null;
        _moveuDuranteArraste = false;
        _idPinoAcoesMestre = null;
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
        _turnoPendente = null;
        _mensagemPendente = null;
        FeedbackChat = null;
        Reconectando = false;
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
            _turnoObservado = Estado.IdAccountTurno;
            _acoesUsadasNoTurno = 0;
            _distanciaMovidaNoTurno = 0m;
            _disparadaUsadaNoTurno = false;
            _acoesExtrasNoTurno = 0;
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
        if (Estado.IdSessao <= 0 || _desconectado || ModoRevisaoPosSessao || PinoArrastando != null ||
            (ct.CanBeCanceled && _dbLock.CurrentCount == 0))
            return;

        var estadoAnterior = Estado;
        bool bloqueioObtido = false;
        try
        {
            var novoEstado = await _sessaoService.SincronizarSessaoAsync(
                estadoAnterior.IdCampanha, estadoAnterior.IdSessao, _sessaoUsuario.IdUsuarioLogado, ct);
            bloqueioObtido = await _dbLock.WaitAsync(ct.CanBeCanceled ? 0 : Timeout.Infinite, ct);
            if (!bloqueioObtido || !ReferenceEquals(Estado, estadoAnterior) ||
                PinoArrastando != null || _gravandoMovimentoPino || _desconectado || ModoRevisaoPosSessao)
                return;

            if (novoEstado != null)
            {
                var mensagensAnterior = Estado.Mensagens.Select(m => m.IdMensagem).ToHashSet();
                foreach (var mensagem in novoEstado.Mensagens.Where(m => !mensagensAnterior.Contains(m.IdMensagem)))
                {
                    if (mensagem.Conteudo.Contains("iniciou um descanso longo da mesa", StringComparison.OrdinalIgnoreCase))
                        await AplicarDescansoRecebido(longo: true);
                    else if (mensagem.Conteudo.Contains("iniciou um descanso curto da mesa", StringComparison.OrdinalIgnoreCase))
                        await AplicarDescansoRecebido(longo: false);
                }

                bool turnoMudou = _turnoObservado != novoEstado.IdAccountTurno;
                if (ConjuracaoEmCarregamento && Estado.IdPersonagemLogado.HasValue)
                {
                    var anterior = Estado.Pinos.FirstOrDefault(p => p.TipoEntidade == TiposPinoSessao.Personagem && p.IdEntidade == Estado.IdPersonagemLogado.Value);
                    var atualizado = novoEstado.Pinos.FirstOrDefault(p => p.TipoEntidade == TiposPinoSessao.Personagem && p.IdEntidade == Estado.IdPersonagemLogado.Value);
                    if (anterior?.PvAtual.HasValue == true && atualizado?.PvAtual.HasValue == true && atualizado.PvAtual.Value < anterior.PvAtual.Value)
                        CancelarConjuracaoAtual();
                }
                Estado = novoEstado;
                ConcluirDeslocamento2014();
                if (turnoMudou)
                {
                    _turnoObservado = Estado.IdAccountTurno;
                    _acoesUsadasNoTurno = 0;
                    _distanciaMovidaNoTurno = 0m;
                    _disparadaUsadaNoTurno = false;
                    _acoesExtrasNoTurno = 0;
                    if (Estado.IdAccountTurno.HasValue)
                        AvancarTurnoConjuracao();
                }

                if (PinoMonstroEmVisualizacao != null)
                {
                    var pinoAtualizado = Estado.Pinos.FirstOrDefault(p =>
                        p.IdMapa == Estado.IdMapaAtivo &&
                        p.TipoEntidade == TiposPinoSessao.Monstro &&
                        p.IdPino == PinoMonstroEmVisualizacao.IdPino);
                    if (pinoAtualizado != null)
                        PinoMonstroEmVisualizacao = pinoAtualizado;
                }

                if (PinoNpcEmVisualizacao != null)
                {
                    var pinoAtualizado = Estado.Pinos.FirstOrDefault(p =>
                        p.IdMapa == Estado.IdMapaAtivo &&
                        p.TipoEntidade == TiposPinoSessao.Npc &&
                        p.IdPino == PinoNpcEmVisualizacao.IdPino);
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
                    else if (!AlvoSelecionadoVivo || !AlvoSelecionadoNoAlcance)
                    {
                        SelecionarPrimeiroAlvoVivo();
                    }
                }

                if (ExibirModalMagia && EtapaMagia == 1 && !Estado.EhMestre && !EhMeuTurno)
                {
                    ExibirModalMagia = false;
                    FeedbackAcao = "O Mestre encerrou o seu turno.";
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
                    ExibirModalMagia = false;
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

            }
            bool recuperou = Reconectando;
            Reconectando = false;
            if (novoEstado is not null || recuperou)
                Notificar();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (!ReferenceEquals(Estado, estadoAnterior) || _desconectado || Reconectando)
                return;

            _logger.LogWarning(ex, "Falha ao sincronizar a sessão {IdSessao}", estadoAnterior.IdSessao);
            Reconectando = true;
            Notificar();
        }
        finally
        {
            if (bloqueioObtido)
                _dbLock.Release();
        }
    }

    public async Task AlternarTurnoJogadorAsync(ParticipanteSessaoDto jogador)
    {
        if (!Estado.EhMestre || Estado.IdSessao <= 0 || AlterandoTurno) return;
        if (Estado.Encontro.Rodada > 0) { AbrirPainel2014(); return; }

        bool jaEstaNoTurno = Estado.IdAccountTurno == jogador.IdAccount;
        int? novoId = jaEstaNoTurno ? null : jogador.IdAccount;
        string nome = jogador.NomePersonagem ?? jogador.Username;

        await DefinirTurnoInternoAsync(novoId, nome);
    }

    public async Task EncerrarTurnoAtualAsync()
    {
        if (!Estado.EhMestre || Estado.IdSessao <= 0 || AlterandoTurno) return;
        if (Estado.Encontro.Rodada > 0) { await AvancarEncontro2014Async(); return; }
        await DefinirTurnoInternoAsync(null, null);
    }

    public async Task PassarMeuTurnoAsync()
    {
        if (!EhMeuTurno || AlterandoTurno) return;
        if (Estado.Encontro.Rodada > 0) { await AvancarEncontro2014Async(); return; }
        await DefinirTurnoInternoAsync(null, null);
    }

    public bool ConsumirAcaoDoTurno()
    {
        if (!EhMeuTurno || AcoesDisponiveisNoTurno <= 0)
        {
            FeedbackAcao = "Sua ação deste turno já foi usada.";
            Notificar();
            return false;
        }

        _acoesUsadasNoTurno++;
        Notificar();
        return true;
    }

    public void ConcederAcaoExtraPorHabilidade(int quantidade = 1)
    {
        _acoesExtrasNoTurno = Math.Max(0, _acoesExtrasNoTurno + Math.Max(0, quantidade));
        Notificar();
    }

    private async Task DefinirTurnoInternoAsync(int? idAccountJogador, string? nomeJogador)
    {
        if (_turnoPendente is not { } pendente || pendente.Jogador != idAccountJogador)
            _turnoPendente = (idAccountJogador, Guid.NewGuid());
        var idOperacao = _turnoPendente.Value.Id;
        AlterandoTurno = true;
        Notificar();

        bool confirmado = false;
        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, erro) = await _sessaoService.DefinirTurnoJogadorAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado,
                idAccountJogador,
                nomeJogador,
                idOperacao);

            confirmado = sucesso;
            if (!sucesso)
                FeedbackAcao = erro;
            else
            {
                _turnoPendente = null;
                FeedbackAcao = null;
            }
        }
        finally
        {
            _dbLock.Release();
            try
            {
                if (confirmado)
                    await SincronizarAgoraAsync();
            }
            finally
            {
                AlterandoTurno = false;
                Notificar();
            }
        }
    }

    public void AbrirModalDados()
    {
        ExibirModalFicha = false;
        ExibirModalAtaque = false;
        ExibirModalMagia = false;
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
        ExibirModalMagia = false;
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
