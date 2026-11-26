using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Combate;
using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Presentation.ViewModel;

public partial class SessaoViewModel
{
    private bool _moveuDuranteArraste;
    private bool _gravandoMovimentoPino;
    private int? _idPinoAcoesMestre;
    private int? _idPinoParaMover;

    public double NivelZoom { get; private set; } = 1.0;
    public const double ZoomMinimo = 0.6;
    public const double ZoomMaximo = 2.6;
    public const int GradeColunas = 20;
    public const int GradeLinhas = 20;
    public const int TamanhoBaseMapaPx = 720;

    public bool ExibirPainelPinos { get; private set; }
    public string AbaPinosAtiva { get; private set; } = "jogadores";
    public PinoSelecionadoParaColocar? PinoParaPosicionar { get; private set; }
    public PinoMapaDto? PinoArrastando { get; private set; }
    public (int Coluna, int Linha)? CelulaDestinoArraste { get; private set; }
    public bool NovoPinoVisivel { get; set; } = true;
    public bool GravandoMovimentoPino => _gravandoMovimentoPino;

    public PinoMapaDto? PinoAcoesMestre => Estado.EhMestre && Estado.SessaoAtiva &&
        Estado.IdMapaAtivo is int idMapa && _idPinoAcoesMestre is int idPino
        ? Estado.Pinos.Find(p => p.IdPino == idPino && p.IdMapa == idMapa)
        : null;

    public PinoMapaDto? PinoParaMover => Estado.EhMestre && Estado.SessaoAtiva &&
        Estado.IdMapaAtivo is int idMapa && _idPinoParaMover is int idPino
        ? Estado.Pinos.Find(p => p.IdPino == idPino && p.IdMapa == idMapa)
        : null;

    public bool ModoPosicionamentoOuMovimentoMestre =>
        Estado.EhMestre && (PinoParaPosicionar is not null || PinoParaMover is not null);

    public IEnumerable<PinoMapaDto> PinosVisiveisNoMapa
    {
        get
        {
            if (!Estado.IdMapaAtivo.HasValue)
                return Enumerable.Empty<PinoMapaDto>();

            var pinosDoMapa = Estado.Pinos.Where(p => p.IdMapa == Estado.IdMapaAtivo);
            if (Estado.EhMestre)
                return pinosDoMapa;

            return pinosDoMapa.Where(p =>
                p.Visivel ||
                (p.TipoEntidade == TiposPinoSessao.Personagem &&
                 Estado.IdPersonagemLogado.HasValue &&
                 p.IdEntidade == Estado.IdPersonagemLogado.Value));
        }
    }

    public void AlternarPainelPinos()
    {
        if (!Estado.EhMestre) return;
        ExibirPainelPinos = !ExibirPainelPinos;
        Notificar();
    }

    public void FecharPainelPinos()
    {
        if (!ExibirPainelPinos) return;
        ExibirPainelPinos = false;
        Notificar();
    }

    public void MudarAbaPinos(string aba)
    {
        AbaPinosAtiva = aba;
        Notificar();
    }

    public PinoMapaDto? ObterPinoNoMapa(string tipoEntidade, int idEntidade) =>
        Estado.IdMapaAtivo.HasValue
            ? Estado.Pinos.Where(p =>
                p.IdMapa == Estado.IdMapaAtivo &&
                p.TipoEntidade == tipoEntidade &&
                p.IdEntidade == idEntidade).OrderBy(p => p.Instancia).FirstOrDefault()
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

    public void SelecionarPinoParaPosicionar(
        string tipoEntidade,
        int idEntidade,
        string nome,
        bool novaInstancia = false,
        bool recolherPainel = false)
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
            PinoParaPosicionar.IdEntidade == idEntidade &&
            PinoParaPosicionar.NovaInstancia == novaInstancia)
        {
            PinoParaPosicionar = null;
            FeedbackAcao = null;
        }
        else
        {
            _idPinoParaMover = null;
            PinoParaPosicionar = new PinoSelecionadoParaColocar(tipoEntidade, idEntidade, nome, novaInstancia);
            FeedbackAcao = null;

            if (recolherPainel)
                ExibirPainelPinos = false;
        }

        Notificar();
    }

    public void AlternarMovimentoPinoPorToque(PinoMapaDto pino)
    {
        if (!Estado.EhMestre || _gravandoMovimentoPino || !Estado.IdMapaAtivo.HasValue || pino.IdMapa != Estado.IdMapaAtivo)
            return;

        if (_idPinoParaMover == pino.IdPino)
        {
            _idPinoParaMover = null;
            FeedbackAcao = null;
        }
        else
        {
            PinoParaPosicionar = null;
            PinoArrastando = null;
            CelulaDestinoArraste = null;
            _moveuDuranteArraste = false;
            _idPinoAcoesMestre = pino.IdPino;
            _idPinoParaMover = pino.IdPino;
            ExibirPainelPinos = false;
            FeedbackAcao = null;
        }

        Notificar();
    }

    public void CancelarMovimentoPinoPorToque()
    {
        if (_idPinoParaMover is null) return;
        _idPinoParaMover = null;
        Notificar();
    }

    public void CancelarSelecaoPino()
    {
        PinoParaPosicionar = null;
        _idPinoParaMover = null;
        FeedbackAcao = null;
        Notificar();
    }

    public void IniciarArrastePino(PinoMapaDto pino)
    {
        if (!Estado.EhMestre || _gravandoMovimentoPino || ModoPosicionamentoOuMovimentoMestre) return;
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
    }

    public void FinalizarArrastePino()
    {
        if (PinoArrastando is null && CelulaDestinoArraste is null) return;
        PinoArrastando = null;
        CelulaDestinoArraste = null;
        _moveuDuranteArraste = false;
        Notificar();
    }

    public async Task<bool> MoverPinoPorIdAsync(int idPino, int coluna, int linha)
    {
        if (!Estado.EhMestre || _gravandoMovimentoPino || Estado.IdSessao <= 0 || Estado.IdMapaAtivo is not int idMapaAlvo)
            return false;

        var pino = Estado.Pinos.Find(p => p.IdPino == idPino && p.IdMapa == idMapaAlvo);
        if (pino is null)
            return false;

        int colClamped = Math.Clamp(coluna, 0, GradeColunas - 1);
        int linClamped = Math.Clamp(linha, 0, GradeLinhas - 1);

        if (pino.Coluna == colClamped && pino.Linha == linClamped)
            return true;

        if (Estado.Encontro.Rodada > 0 && Estado.Encontro.IdPinoAtual == pino.IdPino)
        {
            _gravandoMovimentoPino = true;
            Notificar();
            try
            {
                bool sucessoCombate = await UsarControle2014Async(
                    (s, e, conta) => s.MoverComReacoes2014Async(e, conta, pino.IdPino, colClamped, linClamped, false));
                FeedbackAcao = FeedbackAtaqueEntidade;
                return sucessoCombate;
            }
            finally
            {
                _gravandoMovimentoPino = false;
                Notificar();
            }
        }

        var posicaoAnterior = (pino.Coluna, pino.Linha);
        bool gravadoComSucesso = false;
        _gravandoMovimentoPino = true;
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
                pino.Visivel,
                idPino: pino.IdPino);

            if (!sucesso)
            {
                FeedbackAcao = erro;
                return false;
            }

            gravadoComSucesso = true;
            return true;
        }
        finally
        {
            if (!gravadoComSucesso)
                (pino.Coluna, pino.Linha) = posicaoAnterior;

            _gravandoMovimentoPino = false;
            _dbLock.Release();
            Notificar();
        }
    }

    public async Task SoltarPinoNaCelulaAsync(int coluna, int linha)
    {
        if (!Estado.EhMestre || PinoArrastando is null || Estado.IdSessao <= 0 || !Estado.IdMapaAtivo.HasValue)
        {
            FinalizarArrastePino();
            return;
        }

        var pino = PinoArrastando;
        int colClamped = Math.Clamp(coluna, 0, GradeColunas - 1);
        int linClamped = Math.Clamp(linha, 0, GradeLinhas - 1);
        bool realmenteMoveu = _moveuDuranteArraste && (pino.Coluna != colClamped || pino.Linha != linClamped);

        PinoArrastando = null;
        CelulaDestinoArraste = null;
        _moveuDuranteArraste = false;

        if (!realmenteMoveu)
        {
            await AoClicarPinoMapaAsync(pino);
            return;
        }

        await MoverPinoPorIdAsync(pino.IdPino, colClamped, linClamped);
    }

    public async Task AoClicarCelulaMapaAsync(int coluna, int linha)
    {
        if (ModoMovimentoAtivo && EhMeuTurno)
        {
            await ProcessarCliqueCelulaDisparadaAsync(coluna, linha);
            return;
        }

        if (!Estado.EhMestre || Estado.IdSessao <= 0 || _gravandoMovimentoPino)
            return;

        if (PinoParaMover is { } pinoParaMover)
        {
            bool moveu = await MoverPinoPorIdAsync(pinoParaMover.IdPino, coluna, linha);
            if (moveu)
            {
                _idPinoParaMover = null;
                Notificar();
            }
            return;
        }

        if (PinoParaPosicionar is null)
            return;

        if (!Estado.IdMapaAtivo.HasValue)
        {
            FeedbackAcao = "Selecione um mapa antes de posicionar pinos.";
            Notificar();
            return;
        }

        var alvo = PinoParaPosicionar;
        var existente = ObterPinoNoMapa(alvo.TipoEntidade, alvo.IdEntidade);
        bool visivel = alvo.NovaInstancia ? NovoPinoVisivel : existente?.Visivel ?? NovoPinoVisivel;

        _gravandoMovimentoPino = true;
        Notificar();

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
                visivel,
                novaInstancia: alvo.NovaInstancia);

            if (!sucesso)
            {
                // Mantém PinoParaPosicionar ativo para o mestre tentar outra casa
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
        }
        finally
        {
            _gravandoMovimentoPino = false;
            _dbLock.Release();
            Notificar();
        }
    }

    public async Task<bool> AlternarVisibilidadePinoAsync(string tipoEntidade, int idEntidade, int? idPino = null, bool? visivel = null)
    {
        if (!Estado.EhMestre || Estado.IdSessao <= 0 || Estado.IdMapaAtivo is not int idMapa) return false;
        int idSessao = Estado.IdSessao;

        await _dbLock.WaitAsync();
        try
        {
            if (!Estado.EhMestre || Estado.IdSessao != idSessao || Estado.IdMapaAtivo != idMapa) return false;
            var pino = ObterPinoParaAcao(tipoEntidade, idEntidade, idPino, idMapa);
            if (pino is null) return false;

            bool novaVisibilidade = visivel ?? !pino.Visivel;
            var (sucesso, erro) = await _sessaoService.AlterarVisibilidadePinoAsync(
                Estado.IdCampanha,
                idSessao,
                idMapa,
                _sessaoUsuario.IdUsuarioLogado,
                tipoEntidade,
                idEntidade,
                novaVisibilidade,
                pino.IdPino);

            FeedbackAcao = sucesso ? null : erro;
            if (sucesso) pino.Visivel = novaVisibilidade;
            await SincronizarAposAcaoPinoAsync();
            return sucesso;
        }
        finally
        {
            _dbLock.Release();
            Notificar();
        }
    }

    public async Task<bool> RemoverPinoDoMapaAsync(string tipoEntidade, int idEntidade, int? idPino = null)
    {
        if (!Estado.EhMestre || Estado.IdSessao <= 0 || Estado.IdMapaAtivo is not int idMapa) return false;
        int idSessao = Estado.IdSessao;

        await _dbLock.WaitAsync();
        try
        {
            if (!Estado.EhMestre || Estado.IdSessao != idSessao || Estado.IdMapaAtivo != idMapa) return false;
            var pino = ObterPinoParaAcao(tipoEntidade, idEntidade, idPino, idMapa);
            if (idPino.HasValue && pino is null) return false;

            var (sucesso, erro) = await _sessaoService.RemoverPinoAsync(
                Estado.IdCampanha,
                idSessao,
                idMapa,
                _sessaoUsuario.IdUsuarioLogado,
                tipoEntidade,
                idEntidade,
                idPino);

            FeedbackAcao = sucesso ? null : erro;
            if (sucesso && pino is not null)
            {
                if (_idPinoParaMover == pino.IdPino)
                    _idPinoParaMover = null;
                Estado.Pinos.Remove(pino);
            }
            await SincronizarAposAcaoPinoAsync();
            return sucesso;
        }
        finally
        {
            _dbLock.Release();
            Notificar();
        }
    }

    private PinoMapaDto? ObterPinoParaAcao(string tipoEntidade, int idEntidade, int? idPino, int idMapa) =>
        idPino.HasValue
            ? Estado.Pinos.Find(p => p.IdPino == idPino && p.IdMapa == idMapa && p.TipoEntidade == tipoEntidade && p.IdEntidade == idEntidade)
            : ObterPinoNoMapa(tipoEntidade, idEntidade);

    private async Task SincronizarAposAcaoPinoAsync()
    {
        try
        {
            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha, Estado.IdSessao, _sessaoUsuario.IdUsuarioLogado);
            if (atualizado is not null)
                Estado = atualizado;
        }
        catch (Exception erro)
        {
            _logger.LogWarning(erro, "Falha ao sincronizar o mapa da sessão {IdSessao}", Estado.IdSessao);
        }
    }

    public async Task AoClicarPinoMapaAsync(PinoMapaDto pino)
    {
        if (ModoMovimentoAtivo && EhMeuTurno)
        {
            await ProcessarCliqueCelulaDisparadaAsync(pino.Coluna, pino.Linha);
            return;
        }

        if (Estado.EhMestre)
        {
            if (ModoPosicionamentoOuMovimentoMestre)
            {
                await AoClicarCelulaMapaAsync(pino.Coluna, pino.Linha);
                return;
            }

            if (Estado.SessaoAtiva && pino.IdMapa == Estado.IdMapaAtivo && _idPinoAcoesMestre != pino.IdPino)
            {
                _idPinoAcoesMestre = pino.IdPino;
                FeedbackAtaqueEntidade = null;
            }
            Notificar();
            return;
        }

        await AbrirFichaPinoMapaAsync(pino);
    }

    public Task AbrirFichaPinoAcoesMestreAsync(int idPino) => PinoAcoesMestre is { } pino && pino.IdPino == idPino
        ? AbrirFichaPinoMapaAsync(pino)
        : Task.CompletedTask;

    private async Task AbrirFichaPinoMapaAsync(PinoMapaDto pino)
    {
        if (pino.TipoEntidade == TiposPinoSessao.Monstro)
        {
            MonstroEmVisualizacao = Estado.MonstrosCampanha.FirstOrDefault(m => m.IdMonstro == pino.IdEntidade);
            PinoMonstroEmVisualizacao = pino;
            IdAtaqueMonstroSelecionado = MonstroEmVisualizacao?.Ataques.FirstOrDefault(a => RegrasCombate2014.DanoValido(a.Dano))?.IdMonstroAtaque ?? 0;
            AlvoAtaqueMonstroSelecionado = AlvosAtaqueMonstro.FirstOrDefault() is { } primeiroAlvo
                ? primeiroAlvo.IdPino.ToString() : string.Empty;
            FeedbackAtaqueEntidade = null;
            PvAtualMonstroEdicao = pino.PvAtual ?? MonstroEmVisualizacao?.PvMaximo ?? 0;
            NpcEmVisualizacao = null;
            PinoNpcEmVisualizacao = null;
            ExibirModalDados = false;
            ExibirModalFicha = false;
            ExibirModalAtaque = false;
            ExibirModalMagia = false;
            ExibirModalEsquivar = false;
            ExibirModalEsconder = false;
            Notificar();
            return;
        }

        if (pino.TipoEntidade == TiposPinoSessao.Npc)
        {
            NpcEmVisualizacao = Estado.NpcsCampanha.FirstOrDefault(n => n.IdNpc == pino.IdEntidade);
            PinoNpcEmVisualizacao = pino;
            IdItemNpcAtaqueSelecionado = NpcEmVisualizacao?.Itens.FirstOrDefault(x => RegrasCombate2014.DanoValido(x.Item?.Dano))?.IdItem ?? 0;
            IdMagiaNpcSelecionada = NpcEmVisualizacao?.Magias.FirstOrDefault()?.IdMagia ?? 0;
            AlvoAtaqueNpcSelecionado = ObterAlvosDaEntidade(pino.IdPino).FirstOrDefault()?.IdPino.ToString() ?? string.Empty;
            AlvoMagiaNpcSelecionado = AlvoAtaqueNpcSelecionado;
            FeedbackAtaqueEntidade = null;
            PvAtualNpcEdicao = pino.PvAtual ?? (NpcEmVisualizacao != null ? SessaoService.CalcularPvMaximoNpc(NpcEmVisualizacao) : 0);
            MonstroEmVisualizacao = null;
            PinoMonstroEmVisualizacao = null;
            ExibirModalDados = false;
            ExibirModalFicha = false;
            ExibirModalAtaque = false;
            ExibirModalMagia = false;
            ExibirModalEsquivar = false;
            ExibirModalEsconder = false;
            Notificar();
            return;
        }

        if (pino.TipoEntidade == TiposPinoSessao.Personagem)
        {
            var jogador = Estado.Jogadores.FirstOrDefault(j => j.IdPersonagem == pino.IdEntidade);
            if (jogador != null)
            {
                if (Estado.EhMestre)
                    await AbrirFichaPorIdAsync(pino.IdEntidade, "atributos", pino.IdPino);
                else
                    await AbrirFichaParticipanteAsync(jogador);
            }
        }
    }

    public async Task SelecionarMapaAsync(ChangeEventArgs e)
    {
        if (!Estado.EhMestre || Estado.IdSessao <= 0) return;

        int? idMapaSelecionado = int.TryParse(e.Value?.ToString(), out int parsed) && parsed > 0
            ? parsed
            : null;

        PinoParaPosicionar = null;
        _idPinoParaMover = null;
        PinoArrastando = null;
        CelulaDestinoArraste = null;
        _moveuDuranteArraste = false;
        _idPinoAcoesMestre = null;

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
}