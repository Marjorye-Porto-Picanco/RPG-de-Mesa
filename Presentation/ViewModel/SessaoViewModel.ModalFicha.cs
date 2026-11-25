using System.Globalization;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Magias;
using RPGdeMesa.Domain.Personagens;

namespace RPGdeMesa.Presentation.ViewModel;

public partial class SessaoViewModel
{
    private static readonly string[] ClassesPadraoFallback =
    [
        "Bárbaro", "Bardo", "Bruxo", "Clérigo", "Druida", "Feiticeiro",
        "Guerreiro", "Ladino", "Mago", "Monge", "Paladino", "Patrulheiro"
    ];

    public Personagem? FichaEmVisualizacao { get; private set; }
    public bool ExibirModalFicha { get; private set; }
    public string AbaFichaModalAtiva { get; private set; } = "atributos";
    public bool CarregandoFichaModal { get; private set; }
    public bool SalvandoFichaModal { get; private set; }
    public string? FeedbackFichaModal { get; private set; }

    // Edição de classes (multiclasse) dentro do modal da ficha
    public List<ClasseSlotSessaoDto> ClassesModalEdicao { get; private set; } = new();

    // Edição de inventário dentro do modal da ficha
    public List<ItemSlotSessaoDto> ItensModalEdicao { get; private set; } = new();
    public int ModalMoedasPC { get; set; }
    public int ModalMoedasPP { get; set; }
    public int ModalMoedasPE { get; set; }
    public int ModalMoedasPO { get; set; }
    public int ModalMoedasPL { get; set; }
    public string ModalNotasInventario { get; set; } = string.Empty;

    // Edição de grimório (magias) dentro do modal da ficha
    public List<MagiaSlotSessaoDto> MagiasModalEdicao { get; private set; } = new();
    public string TermoBuscaMagiaModal { get; set; } = string.Empty;
    public string FiltroCirculoMagiaModal { get; set; } = "todos";
    public int IdMagiaSelecionadaModal { get; set; }

    public bool PodeEditarFichaModal =>
        FichaEmVisualizacao != null &&
        (Estado.EhMestre || FichaEmVisualizacao.IdAccount == _sessaoUsuario.IdUsuarioLogado);

    public IEnumerable<Magia> MagiasCatalogoDisponiveisModal
    {
        get
        {
            var idsEquipados = MagiasModalEdicao
                .Select(m => m.IdMagia)
                .ToHashSet();

            var nomesEquipados = MagiasModalEdicao
                .Select(m => ObterDetalhesMagia(m.IdMagia)?.Nome?.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return Estado.MagiasCatalogo
                .Where(m => !idsEquipados.Contains(m.IdMagia) && !nomesEquipados.Contains(m.Nome.Trim()))
                .Where(m =>
                    FiltroCirculoMagiaModal == "todos" ||
                    m.CirculoNivel.ToString(CultureInfo.InvariantCulture) == FiltroCirculoMagiaModal)
                .Where(m =>
                    string.IsNullOrWhiteSpace(TermoBuscaMagiaModal) ||
                    m.Nome.Contains(TermoBuscaMagiaModal, StringComparison.OrdinalIgnoreCase) ||
                    (m.Escola != null && m.Escola.Contains(TermoBuscaMagiaModal, StringComparison.OrdinalIgnoreCase)) ||
                    (m.DanoEfeito != null && m.DanoEfeito.Contains(TermoBuscaMagiaModal, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public List<string> ObterOpcoesClassesModal(string? classeAtual = null) =>
        ObterOpcoesClassesComFallback(Estado.ClassesCatalogo, ClassesModalEdicao, classeAtual);

    public string ObterResumoClassesModal()
    {
        var validas = ClassesModalEdicao
            .Where(c => !string.IsNullOrWhiteSpace(c.NomeClasse))
            .ToList();

        if (validas.Any())
            return string.Join(" / ", validas.Select(c => $"{c.NomeClasse} {Math.Clamp(c.Nivel, 1, 20)}"));

        if (FichaEmVisualizacao != null && !string.IsNullOrWhiteSpace(FichaEmVisualizacao.ClasseTexto))
            return FichaEmVisualizacao.ClasseTexto;

        return "Classe";
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

            CarregarClassesModal(FichaEmVisualizacao);
            CarregarInventarioModal(FichaEmVisualizacao);
            CarregarMagiasModal(FichaEmVisualizacao);
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

    private void CarregarClassesModal(Personagem p)
    {
        ClassesModalEdicao = ExtrairClassesDePersonagem(p);
        SincronizarClassesModal();
    }

    public void AdicionarClasseModal()
    {
        if (!PodeEditarFichaModal || FichaEmVisualizacao == null) return;

        if (ClassesModalEdicao.Sum(c => Math.Max(1, c.Nivel)) >= 20)
        {
            FeedbackFichaModal = "A soma dos níveis das classes já atingiu o limite máximo (20).";
            Notificar();
            return;
        }

        var opcoes = ObterOpcoesClassesModal();
        string novaClasse = opcoes.FirstOrDefault(op =>
            !ClassesModalEdicao.Any(c => c.NomeClasse.Equals(op, StringComparison.OrdinalIgnoreCase)))
            ?? opcoes.FirstOrDefault()
            ?? "Guerreiro";

        ClassesModalEdicao.Add(new ClasseSlotSessaoDto
        {
            NomeClasse = novaClasse,
            Nivel = 1
        });

        FeedbackFichaModal = null;
        SincronizarClassesModal();
        Notificar();
    }

    public void RemoverClasseModal(ClasseSlotSessaoDto classe)
    {
        if (!PodeEditarFichaModal || ClassesModalEdicao.Count <= 1) return;

        ClassesModalEdicao.Remove(classe);
        FeedbackFichaModal = null;
        SincronizarClassesModal();
        Notificar();
    }

    public void AlterarNomeClasseModal(ClasseSlotSessaoDto classe, string? novoNome)
    {
        if (!PodeEditarFichaModal) return;

        classe.NomeClasse = string.IsNullOrWhiteSpace(novoNome) ? "Guerreiro" : novoNome.Trim();
        SincronizarClassesModal();
        Notificar();
    }

    public void AlterarNivelClasseModal(ClasseSlotSessaoDto classe, string? valorTexto)
    {
        if (!PodeEditarFichaModal) return;

        if (int.TryParse(valorTexto, out int nivel))
        {
            classe.Nivel = Math.Clamp(nivel, 1, 20);
            SincronizarClassesModal();
            Notificar();
        }
    }

    private void SincronizarClassesModal()
    {
        if (FichaEmVisualizacao == null) return;
        AplicarResumoClassesNoModelo(FichaEmVisualizacao, ClassesModalEdicao);
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

    private void CarregarMagiasModal(Personagem p)
    {
        MagiasModalEdicao = p.PersonagemMagias?
            .Where(pm => pm.IdMagia > 0)
            .Select(pm => pm.IdMagia)
            .Distinct()
            .Select(id => new MagiaSlotSessaoDto { IdMagia = id })
            .ToList() ?? new List<MagiaSlotSessaoDto>();

        TermoBuscaMagiaModal = string.Empty;
        FiltroCirculoMagiaModal = "todos";
        IdMagiaSelecionadaModal = MagiasCatalogoDisponiveisModal.FirstOrDefault()?.IdMagia ?? 0;
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

    public void AdicionarMagiaModal()
    {
        var disponiveis = MagiasCatalogoDisponiveisModal.ToList();
        if (!disponiveis.Any())
            return;

        int idParaAdicionar = disponiveis.Any(m => m.IdMagia == IdMagiaSelecionadaModal)
            ? IdMagiaSelecionadaModal
            : disponiveis[0].IdMagia;

        if (MagiasModalEdicao.All(m => m.IdMagia != idParaAdicionar))
        {
            MagiasModalEdicao.Add(new MagiaSlotSessaoDto { IdMagia = idParaAdicionar });
        }

        IdMagiaSelecionadaModal = MagiasCatalogoDisponiveisModal.FirstOrDefault()?.IdMagia ?? 0;
        Notificar();
    }

    public void RemoverMagiaModal(MagiaSlotSessaoDto slot)
    {
        MagiasModalEdicao.Remove(slot);
        if (IdMagiaSelecionadaModal == 0)
            IdMagiaSelecionadaModal = MagiasCatalogoDisponiveisModal.FirstOrDefault()?.IdMagia ?? 0;
        Notificar();
    }

    public Item? ObterDetalhesItem(int idItem)
    {
        return Estado.ItensCampanha.FirstOrDefault(i => i.IdItem == idItem)
               ?? FichaEmVisualizacao?.Inventario?.Itens?.FirstOrDefault(ii => ii.IdItem == idItem)?.Item;
    }

    public Magia? ObterDetalhesMagia(int idMagia)
    {
        return Estado.MagiasCatalogo.FirstOrDefault(m => m.IdMagia == idMagia)
               ?? FichaEmVisualizacao?.PersonagemMagias?.FirstOrDefault(pm => pm.IdMagia == idMagia)?.Magia;
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

        var classesParaSalvar = ClassesModalEdicao
            .Where(c => !string.IsNullOrWhiteSpace(c.NomeClasse))
            .Select(c => (NomeClasse: c.NomeClasse.Trim(), Nivel: Math.Max(1, c.Nivel)))
            .ToList();

        int somaNiveis = classesParaSalvar.Sum(c => c.Nivel);
        if (classesParaSalvar.Count == 0 || somaNiveis < 1 || somaNiveis > 20)
        {
            FeedbackFichaModal = "Informe ao menos uma classe e mantenha a soma dos níveis entre 1 e 20.";
            Notificar();
            return;
        }

        SincronizarClassesModal();

        SalvandoFichaModal = true;
        FeedbackFichaModal = null;
        Notificar();

        var itensAgrupados = ItensModalEdicao
            .Where(s => s.IdItem > 0 && s.Quantidade > 0)
            .GroupBy(s => s.IdItem)
            .Select(g => (IdItem: g.Key, Quantidade: g.Sum(x => x.Quantidade)))
            .ToList();

        var idsMagias = MagiasModalEdicao
            .Where(m => m.IdMagia > 0)
            .Select(m => m.IdMagia)
            .Distinct()
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

        bool salvouComSucesso = false;

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, mensagem) = await _sessaoService.SalvarFichaNaSessaoAsync(
                Estado.IdCampanha,
                _sessaoUsuario.IdUsuarioLogado,
                FichaEmVisualizacao,
                dadosInv,
                itensAgrupados,
                idsMagias,
                classesParaSalvar);

            FeedbackFichaModal = mensagem;
            salvouComSucesso = sucesso;

            if (sucesso)
            {
                FichaEmVisualizacao = await _sessaoService.ObterFichaCompletaAsync(FichaEmVisualizacao.IdPersonagem)
                                      ?? FichaEmVisualizacao;
                CarregarClassesModal(FichaEmVisualizacao);
                CarregarInventarioModal(FichaEmVisualizacao);
                CarregarMagiasModal(FichaEmVisualizacao);
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

        if (salvouComSucesso)
        {
            await SincronizarAgoraAsync();
        }
    }

    public void FecharModalFicha()
    {
        ExibirModalFicha = false;
        FeedbackFichaModal = null;
        Notificar();
    }

    private static List<ClasseSlotSessaoDto> ExtrairClassesDePersonagem(Personagem p)
    {
        var lista = p.PersonagemClasses?
            .Where(pc => pc.Classe != null && !string.IsNullOrWhiteSpace(pc.Classe.Nome))
            .Select(pc => new ClasseSlotSessaoDto
            {
                NomeClasse = pc.Classe!.Nome.Trim(),
                Nivel = Math.Clamp(pc.NivelNaClasse, 1, 20)
            })
            .ToList() ?? new List<ClasseSlotSessaoDto>();

        if (!lista.Any())
        {
            string nomeFallback = string.IsNullOrWhiteSpace(p.ClasseTexto)
                ? "Guerreiro"
                : p.ClasseTexto.Split('/')[0].Trim();

            lista.Add(new ClasseSlotSessaoDto
            {
                NomeClasse = string.IsNullOrWhiteSpace(nomeFallback) ? "Guerreiro" : nomeFallback,
                Nivel = Math.Clamp(p.Nivel, 1, 20)
            });
        }

        return lista;
    }

    private static List<string> ObterOpcoesClassesComFallback(
        IEnumerable<Classe> catalogo,
        IEnumerable<ClasseSlotSessaoDto>? atuais,
        string? classeAtual = null)
    {
        var nomes = catalogo
            .Select(c => c.Nome)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .ToList();

        if (!nomes.Any())
            nomes.AddRange(ClassesPadraoFallback);

        if (atuais != null)
        {
            nomes.AddRange(atuais
                .Select(c => c.NomeClasse)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(classeAtual))
            nomes.Add(classeAtual.Trim());

        return nomes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static void AplicarResumoClassesNoModelo(Personagem personagem, List<ClasseSlotSessaoDto> classes)
    {
        var validas = classes.Where(c => !string.IsNullOrWhiteSpace(c.NomeClasse)).ToList();
        if (!validas.Any())
        {
            personagem.Nivel = 1;
            personagem.ClasseTexto = "Guerreiro";
            return;
        }

        int somaNiveis = validas.Sum(c => Math.Clamp(c.Nivel, 1, 20));
        personagem.Nivel = Math.Clamp(somaNiveis, 1, 20);

        string textoResumo = validas.Count == 1
            ? validas[0].NomeClasse.Trim()
            : string.Join(" / ", validas.Select(c => $"{c.NomeClasse.Trim()} {Math.Clamp(c.Nivel, 1, 20)}"));

        personagem.ClasseTexto = textoResumo.Length > 50 ? textoResumo[..50] : textoResumo;
    }
}