using Microsoft.AspNetCore.Components;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Presentation.ViewModel;

public partial class SessaoViewModel
{
    private bool _moveuDuranteArraste;

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
        if (ModoMovimentoAtivo && EhMeuTurno)
        {
            await ProcessarCliqueCelulaDisparadaAsync(coluna, linha);
            return;
        }

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
        if (ModoMovimentoAtivo && EhMeuTurno)
        {
            await ProcessarCliqueCelulaDisparadaAsync(pino.Coluna, pino.Linha);
            return;
        }

        if (pino.TipoEntidade == TiposPinoSessao.Monstro)
        {
            MonstroEmVisualizacao = Estado.MonstrosCampanha.FirstOrDefault(m => m.IdMonstro == pino.IdEntidade);
            PinoMonstroEmVisualizacao = pino;
            PvAtualMonstroEdicao = pino.PvAtual ?? MonstroEmVisualizacao?.PvMaximo ?? 0;
            NpcEmVisualizacao = null;
            PinoNpcEmVisualizacao = null;
            ExibirModalDados = false;
            ExibirModalFicha = false;
            ExibirModalAtaque = false;
            ExibirModalEsquivar = false;
            ExibirModalEsconder = false;
            Notificar();
            return;
        }

        if (pino.TipoEntidade == TiposPinoSessao.Npc)
        {
            NpcEmVisualizacao = Estado.NpcsCampanha.FirstOrDefault(n => n.IdNpc == pino.IdEntidade);
            PinoNpcEmVisualizacao = pino;
            PvAtualNpcEdicao = pino.PvAtual ?? (NpcEmVisualizacao != null ? SessaoService.CalcularPvMaximoNpc(NpcEmVisualizacao) : 0);
            MonstroEmVisualizacao = null;
            PinoMonstroEmVisualizacao = null;
            ExibirModalDados = false;
            ExibirModalFicha = false;
            ExibirModalAtaque = false;
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