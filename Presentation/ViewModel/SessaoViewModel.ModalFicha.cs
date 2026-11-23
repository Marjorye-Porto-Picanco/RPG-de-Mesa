using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Personagens;

namespace RPGdeMesa.Presentation.ViewModel;

public partial class SessaoViewModel
{
    public Personagem? FichaEmVisualizacao { get; private set; }
    public bool ExibirModalFicha { get; private set; }
    public string AbaFichaModalAtiva { get; private set; } = "atributos";
    public bool CarregandoFichaModal { get; private set; }
    public bool SalvandoFichaModal { get; private set; }
    public string? FeedbackFichaModal { get; private set; }

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
        ExibirModalMagia = false;
        ExibirModalEsquivar = false;
        ExibirModalEsconder = false;
        LimparEstadoDisparadaInterno();
        MonstroEmVisualizacao = null;
        PinoMonstroEmVisualizacao = null;
        NpcEmVisualizacao = null;
        PinoNpcEmVisualizacao = null;
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
}