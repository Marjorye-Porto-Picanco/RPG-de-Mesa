using Microsoft.Extensions.Logging;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Combate;
using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Presentation.ViewModel;

public partial class SessaoViewModel
{
    private GolpeMestre2014[] _golpesMestre = [];

    public bool ExibirCombateMestre { get; private set; }
    public bool AtaqueMestreComAtacanteFixo { get; private set; }
    public int IdPinoAtacanteMestre { get; private set; }
    public int IdAtaqueMestre { get; set; }
    public int IdPinoAlvoMestre { get; private set; }
    public IReadOnlyList<GolpeMestre2014> GolpesMestre => _golpesMestre;
    public Rolagem2014 RolagemAtaqueMestre { get; set; }
    public Cobertura2014 CoberturaAtaqueMestre { get; set; }
    public TipoAcao2014 TipoAcaoMestre { get; set; }
    public int CustoLendarioMestre { get; set; } = 1;
    public string RecursoAtaqueMestre { get; set; } = "";
    public decimal? AlcanceAtaqueMestre { get; set; }
    public string DanoAdicionalMestre { get; set; } = "";
    public string TipoDanoAdicionalMestre { get; set; } = "";
    public Condicao2014? CondicaoNoAcerto { get; set; }
    public int? RodadasCondicaoNoAcerto { get; set; }
    public int? CdCondicaoNoAcerto { get; set; }
    public Atributo2014 AtributoCondicaoNoAcerto { get; set; }
    public bool TesteParaEscaparNoAcerto { get; set; }
    public bool UsarResistenciaLendariaNoAcerto { get; set; }

    public IEnumerable<PinoMapaDto> AtacantesMestre => PinosVisiveisNoMapa
        .Where(p => (p.TipoEntidade is TiposPinoSessao.Monstro or TiposPinoSessao.Npc) && PinoVivo(p))
        .Where(p => !(AtaqueMestreComAtacanteFixo || AtaqueOportunidade2014) || p.IdPino == IdPinoAtacanteMestre)
        .OrderBy(p => p.Nome);

    public IEnumerable<AtaqueEntidadeDto> AtaquesMestre =>
        AtacantesMestre.FirstOrDefault(p => p.IdPino == IdPinoAtacanteMestre) is { } atacante
            ? SessaoService.ObterAtaquesEntidade(Estado, atacante).Where(ataque => !AtaqueOportunidade2014 || ataque.CorpoACorpo)
            : [];

    public IEnumerable<PinoMapaDto> AlvosMestre => ObterAlvosDaEntidade(IdPinoAtacanteMestre)
        .Where(p => !AtaqueOportunidade2014 || p.IdPino == Estado.Encontro.MovimentoPendente?.IdMovendo);

    public bool PodeAtacarComoMestre => Estado.EhMestre && Estado.SessaoAtiva && !ExecutandoAtaqueEntidade &&
        (_golpesMestre.Length > 0 || PodeAdicionarGolpeMestre);

    public bool PodeAdicionarGolpeMestre => !ExecutandoAtaqueEntidade &&
        AtaquesMestre.Any(a => a.Id == IdAtaqueMestre && a.TemFormulaDano) && !DanoAdicionalMestreInvalido &&
        AlvosMestre.Any(p => p.IdPino == IdPinoAlvoMestre);

    public bool DanoAdicionalMestreInvalido => !string.IsNullOrWhiteSpace(DanoAdicionalMestre) &&
        !RegrasCombate2014.DanoValido(DanoAdicionalMestre);

    public decimal? DistanciaAtaqueMestre => AtacantesMestre.FirstOrDefault(p => p.IdPino == IdPinoAtacanteMestre) is { } atacante &&
        AlvosMestre.FirstOrDefault(p => p.IdPino == IdPinoAlvoMestre) is { } alvo
        ? SessaoService.DistanciaPinos(atacante, alvo)
        : null;

    public void AbrirCombateMestre() => AbrirCombateMestre(null);

    public void AbrirAtaquePinoAcoesMestre()
    {
        if (PinoAcoesMestre is { TipoEntidade: TiposPinoSessao.Monstro or TiposPinoSessao.Npc } pino && PinoVivo(pino))
            AbrirCombateMestre(pino.IdPino);
    }

    private void AbrirCombateMestre(int? idPino)
    {
        if (!Estado.EhMestre || !Estado.SessaoAtiva || ExibirCombateMestre) return;

        ExibirModalDados = false;
        ExibirModalFicha = false;
        ExibirPainel2014 = false;
        AtaqueMestreComAtacanteFixo = idPino.HasValue;
        _ataqueOportunidade2014 = false;
        TipoAcaoMestre = TipoAcao2014.Acao;
        SelecionarAtacanteMestreInterno(idPino ?? 0);
        ExibirCombateMestre = true;
        AbaChatAtiva = CanaisSessao.Combate;
        Notificar();
    }

    public void FecharCombateMestre()
    {
        if (ExecutandoAtaqueEntidade) return;

        ExibirCombateMestre = false;
        _ataqueOportunidade2014 = false;
        Notificar();
    }

    public void SelecionarAtacanteMestre(int idPino)
    {
        SelecionarAtacanteMestreInterno(idPino);
        Notificar();
    }

    private void SelecionarAtacanteMestreInterno(int idPino)
    {
        IdPinoAtacanteMestre = idPino;
        IdPinoAlvoMestre = 0;
        _golpesMestre = [];
        RecursoAtaqueMestre = "";
        RolagemAtaqueMestre = Rolagem2014.Normal;
        CoberturaAtaqueMestre = Cobertura2014.Nenhuma;
        AlcanceAtaqueMestre = null;
        DanoAdicionalMestre = "";
        TipoDanoAdicionalMestre = "";
        CondicaoNoAcerto = null;
        RodadasCondicaoNoAcerto = null;
        CdCondicaoNoAcerto = null;
        TesteParaEscaparNoAcerto = false;
        UsarResistenciaLendariaNoAcerto = false;
        FeedbackAtaqueEntidade = null;

        IdAtaqueMestre = idPino > 0
            ? AtaquesMestre.FirstOrDefault(a => a.TemFormulaDano)?.Id ?? 0
            : 0;
    }

    public void SelecionarAlvoMestre(int idPino)
    {
        IdPinoAlvoMestre = idPino;
        FeedbackAtaqueEntidade = null;
        Notificar();
    }

    public void LimparFeedbackCombateMestre()
    {
        FeedbackAtaqueEntidade = null;
        Notificar();
    }

    public void AtualizarDanoAdicionalMestre()
    {
        FeedbackAtaqueEntidade = null;
        Notificar();
    }

    public void AdicionarGolpeMestre()
    {
        if (!PodeAdicionarGolpeMestre || _golpesMestre.Length >= 12) return;

        _golpesMestre = [.. _golpesMestre, MontarGolpeMestre()];
        FeedbackAtaqueEntidade = null;
        Notificar();
    }

    public void RemoverGolpeMestre(int indice)
    {
        if (ExecutandoAtaqueEntidade || indice < 0 || indice >= _golpesMestre.Length) return;

        _golpesMestre = _golpesMestre.Where((_, i) => i != indice).ToArray();
        Notificar();
    }

    public void LimparGolpesMestre()
    {
        if (ExecutandoAtaqueEntidade || _golpesMestre.Length == 0) return;

        _golpesMestre = [];
        FeedbackAtaqueEntidade = null;
        Notificar();
    }

    public async Task ConfirmarAtaqueMestreAsync()
    {
        if (!PodeAtacarComoMestre) return;

        GolpeMestre2014[] golpes = _golpesMestre.Length > 0
            ? _golpesMestre
            : [MontarGolpeMestre()];

        var uso = new UsoAcao2014(
            TipoAcaoMestre,
            string.IsNullOrWhiteSpace(RecursoAtaqueMestre) ? null : RecursoAtaqueMestre,
            CustoLendarioMestre,
            _ataqueOportunidade2014);

        bool sucesso = await ExecutarComandoCombateUiAsync((estado, conta) =>
            _sessaoService.ExecutarGolpes2014Async(estado, conta, IdPinoAtacanteMestre, golpes, uso));

        if (sucesso)
        {
            _golpesMestre = [];
            if (_ataqueOportunidade2014)
            {
                ExibirCombateMestre = false;
                _ataqueOportunidade2014 = false;
            }
            Notificar();
        }
    }

    private GolpeMestre2014 MontarGolpeMestre() => new(
        IdAtaqueMestre,
        IdPinoAlvoMestre,
        RolagemAtaqueMestre,
        CoberturaAtaqueMestre,
        AlcanceAtaqueMestre,
        string.IsNullOrWhiteSpace(DanoAdicionalMestre)
            ? null
            : [new ParcelaDano2014(DanoAdicionalMestre.Trim(), TipoDanoAdicionalMestre.Trim())],
        CondicaoNoAcerto is { } condicao
            ? new EfeitoCombate2014
            {
                Condicao = condicao,
                Momento = Momento2014.FimTurno,
                Rodadas = RodadasCondicaoNoAcerto,
                Cd = CdCondicaoNoAcerto,
                Atributo = CdCondicaoNoAcerto.HasValue ? AtributoCondicaoNoAcerto : null,
                TesteParaEscapar = TesteParaEscaparNoAcerto
            }
            : null,
        UsarResistenciaLendariaNoAcerto);

    private IEnumerable<PinoMapaDto> ObterAlvosDaEntidade(int idPinoAtacante) => PinosVisiveisNoMapa
        .Where(p => p.IdPino != idPinoAtacante &&
            (PinoVivo(p) ||
             p.TipoEntidade == TiposPinoSessao.Personagem ||
             Estado.Encontro.Fichas.GetValueOrDefault(p.IdPino)?.UsaTestesMorte == true) &&
            Estado.Encontro.Fichas.GetValueOrDefault(p.IdPino)?.Morto != true)
        .OrderBy(p => p.Nome);

    private static bool PinoVivo(PinoMapaDto pino) => (pino.PvAtual ?? pino.PvMaximo ?? 0) > 0;

    private Task AtacarPelaFichaAsync(PinoMapaDto? atacante, int idAtaque, string chaveAlvo)
    {
        if (atacante is null) return Task.CompletedTask;

        int.TryParse(chaveAlvo, out int idPinoAlvo);
        return ExecutarAtaqueEntidadeAsync(atacante.IdPino, idAtaque, idPinoAlvo);
    }

    private Task ExecutarAtaqueEntidadeAsync(int idPinoAtacante, int idAtaque, int idPinoAlvo) =>
        ExecutarComandoCombateUiAsync((estado, conta) =>
            _sessaoService.AtacarComEntidadeAsync(estado, conta, idPinoAtacante, idAtaque, idPinoAlvo));

    private async Task<bool> ExecutarComandoCombateUiAsync(Func<SessaoEstadoDto, int, Task<(bool Sucesso, string Mensagem)>> executar)
    {
        if (ExecutandoAtaqueEntidade || Estado.IdSessao <= 0 || !Estado.SessaoAtiva)
            return false;

        ExecutandoAtaqueEntidade = true;
        FeedbackAtaqueEntidade = null;
        Notificar();

        await _dbLock.WaitAsync();
        bool sucesso = false;
        try
        {
            int idSessao = Estado.IdSessao;
            int idCampanha = Estado.IdCampanha;
            int idAccount = _sessaoUsuario.IdUsuarioLogado;

            var resultado = await executar(Estado, idAccount);
            sucesso = resultado.Sucesso;
            FeedbackAtaqueEntidade = resultado.Mensagem;

            var atualizado = await _sessaoService.SincronizarSessaoAsync(idCampanha, idSessao, idAccount);
            if (atualizado is not null && Estado.IdSessao == idSessao)
                Estado = atualizado;
        }
        catch (Exception erro)
        {
            _logger.LogWarning(erro, "Falha ao resolver ou sincronizar uma ação de combate");
            FeedbackAtaqueEntidade ??= "A mesa não confirmou a ação. Confira o registro de Combate.";
        }
        finally
        {
            _dbLock.Release();
            ExecutandoAtaqueEntidade = false;
            Notificar();
        }

        return sucesso;
    }
}