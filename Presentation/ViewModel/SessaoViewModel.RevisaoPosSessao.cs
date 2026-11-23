using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Magias;
using RPGdeMesa.Domain.Personagens;

namespace RPGdeMesa.Presentation.ViewModel;

public partial class SessaoViewModel
{
    public bool ModoRevisaoPosSessao { get; private set; }
    public bool FinalizandoSessao { get; private set; }
    public bool SalvandoRevisao { get; private set; }
    public string? FeedbackRevisao { get; private set; }
    public List<FichaRevisaoItemVm> FichasRevisao { get; private set; } = new();
    public FichaRevisaoItemVm? FichaRevisaoSelecionada { get; private set; }
    public List<Item> ItensCampanhaRevisao { get; private set; } = new();
    public List<Magia> MagiasCatalogoRevisao { get; private set; } = new();
    public string TermoBuscaMagiaRevisao { get; set; } = string.Empty;
    public int XpEmLote { get; set; } = 0;
    public string TituloSessaoRevisao { get; set; } = string.Empty;
    public string ResumoSessaoRevisao { get; set; } = string.Empty;

    public async Task FinalizarSessaoAsync()
    {
        if (!Estado.EhMestre || Estado.IdSessao <= 0 || FinalizandoSessao) return;

        FinalizandoSessao = true;
        FeedbackAcao = null;
        ExibirModalDados = false;
        ExibirModalFicha = false;
        ExibirModalAtaque = false;
        ExibirModalMagia = false;
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
            MagiasCatalogoRevisao = revisao.Dados.MagiasCatalogo;
            TermoBuscaMagiaRevisao = string.Empty;
            FichasRevisao = revisao.Dados.Fichas.Select(CriarVmRevisao).ToList();
            FichaRevisaoSelecionada = FichasRevisao.FirstOrDefault();
            XpEmLote = 0;
            TituloSessaoRevisao = string.IsNullOrWhiteSpace(Estado.TituloSessao)
                ? $"Sessão #{Estado.NumeroSessao}"
                : Estado.TituloSessao;
            ResumoSessaoRevisao = string.Empty;
            FeedbackRevisao = "Sessão encerrada e todos os jogadores desconectados. Revise as fichas, distribua o XP, ajuste itens/magias, registre o resumo da sessão e conclua.";
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

        var magias = p.PersonagemMagias?
            .Where(pm => pm.IdMagia > 0)
            .Select(pm => pm.IdMagia)
            .Distinct()
            .Select(id => new MagiaSlotSessaoDto { IdMagia = id })
            .ToList() ?? new List<MagiaSlotSessaoDto>();

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
            Itens = itens,
            Magias = magias
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

    public IEnumerable<Magia> ObterMagiasDisponiveisParaSlotRevisao(int idMagiaAtual)
    {
        return MagiasCatalogoRevisao.Where(m =>
            m.IdMagia == idMagiaAtual ||
            string.IsNullOrWhiteSpace(TermoBuscaMagiaRevisao) ||
            m.Nome.Contains(TermoBuscaMagiaRevisao, StringComparison.OrdinalIgnoreCase) ||
            (m.Escola != null && m.Escola.Contains(TermoBuscaMagiaRevisao, StringComparison.OrdinalIgnoreCase)));
    }

    public void AdicionarMagiaRevisao(FichaRevisaoItemVm ficha)
    {
        var jaEquipadas = ficha.Magias.Select(m => m.IdMagia).ToHashSet();
        var proxima = ObterMagiasDisponiveisParaSlotRevisao(0)
            .FirstOrDefault(m => !jaEquipadas.Contains(m.IdMagia))
            ?? MagiasCatalogoRevisao.FirstOrDefault(m => !jaEquipadas.Contains(m.IdMagia));

        int idPadrao = proxima?.IdMagia ?? 0;
        ficha.Magias.Add(new MagiaSlotSessaoDto { IdMagia = idPadrao });
        Notificar();
    }

    public void RemoverMagiaRevisao(FichaRevisaoItemVm ficha, MagiaSlotSessaoDto slot)
    {
        ficha.Magias.Remove(slot);
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

                var idsMagias = f.Magias
                    .Where(m => m.IdMagia > 0)
                    .Select(m => m.IdMagia)
                    .Distinct()
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
                    Itens = itensAgrupados,
                    IdsMagias = idsMagias
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
}