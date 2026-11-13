using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Personagens;

namespace RPGdeMesa.Presentation.ViewModel
{
    public class SessaoIniciarViewModel
    {
        private readonly CampanhaService _campanhaService;
        private readonly PersonagemService _personagemService;
        private readonly NpcService _npcService;
        private readonly ItemService _itemService;
        private readonly NavigationManager _navigation;

        public SessaoIniciarViewModel(
            CampanhaService campanhaService,
            PersonagemService personagemService,
            NpcService npcService,
            ItemService itemService,
            NavigationManager navigation)
        {
            _campanhaService = campanhaService;
            _personagemService = personagemService;
            _npcService = npcService;
            _itemService = itemService;
            _navigation = navigation;
        }

        public int IdCampanha { get; set; }
        public Campanha? CampanhaAtual { get; private set; }
        public List<Monstro> MonstrosDisponiveis { get; private set; } = new();
        public List<NPC> NpcsDisponiveis { get; private set; } = new();
        public List<Item> ItensDisponiveis { get; private set; } = new();
        public string TermoBusca { get; set; } = string.Empty;

        public string AbaMenu { get; set; } = "iniciar";
        public string CategoriaAtiva { get; set; } = "todos";
        public string ModoVisualizacao { get; set; } = "vinculados";
        public string CategoriaSelecionada { get; set; } = "npc";

        // Fichas do Mestre
        public string FiltroFichasMestre { get; set; } = "pendentes";
        public Personagem? FichaInspecionada { get; private set; }
        public string AbaInspecao { get; set; } = "atributos";
        public string SecaoDiarioInspecao { get; set; } = "ideais";
        public string MensagemFeedbackFichas { get; private set; } = string.Empty;

        public int IdPersonagemParaRecusar { get; set; } = 0;
        public string TextoMotivoRecusa { get; set; } = string.Empty;

        // Form Fields - NPC
        public string NpcNome { get; set; } = string.Empty;
        public string NpcRaca { get; set; } = "Humano";
        public string NpcClasse { get; set; } = "Guerreiro";
        public string NpcTendencia { get; set; } = "Neutra";
        public string NpcAntecedente { get; set; } = string.Empty;
        public string NpcHistoria { get; set; } = string.Empty;
        public int NpcNivel { get; set; } = 1;
        public int NpcForca { get; set; } = 10;
        public int NpcDestreza { get; set; } = 10;
        public int NpcConstituicao { get; set; } = 10;
        public int NpcInteligencia { get; set; } = 10;
        public int NpcSabedoria { get; set; } = 10;
        public int NpcCarisma { get; set; } = 10;

        // Form Fields - Monstro
        public string MonstroNome { get; set; } = string.Empty;
        public string MonstroTipo { get; set; } = "Outro";
        public string MonstroTamanho { get; set; } = "Médio";
        public string MonstroAlinhamento { get; set; } = string.Empty;
        public string MonstroDeslocamento { get; set; } = "9 m";
        public string MonstroSentidos { get; set; } = string.Empty;
        public string MonstroResistencias { get; set; } = string.Empty;
        public string MonstroHabilidades { get; set; } = string.Empty;
        public string MonstroDescricao { get; set; } = string.Empty;
        public decimal MonstroNd { get; set; } = 1;
        public int MonstroDefesa { get; set; } = 12;
        public int MonstroVida { get; set; } = 15;

        // Form Fields - Item
        public string ItemNome { get; set; } = string.Empty;
        public string ItemTipo { get; set; } = string.Empty;
        public string ItemDescricao { get; set; } = string.Empty;
        public decimal? ItemPeso { get; set; } = null;
        public decimal? ItemPreco { get; set; } = null;
        public string? ItemDano { get; set; } = string.Empty;
        public string? ItemTipoDano { get; set; } = string.Empty;
        public string? ItemPropriedades { get; set; } = string.Empty;
        public int ItemCaBonus { get; set; } = 0;
        public string ItemRaridade { get; set; } = string.Empty;
        public string ItemSintonizacao { get; set; } = "Não";

        public string MensagemFeedback { get; private set; } = string.Empty;

        public event Action? OnChange;
        public void NotifyStateChanged() => OnChange?.Invoke();

        // Propriedades Calculadas (LINQ)
        public IEnumerable<PersonagemCampanha> FichasPendentes =>
            CampanhaAtual?.FichasPendentes ?? Enumerable.Empty<PersonagemCampanha>();

        public IEnumerable<PersonagemCampanha> FichasAceitas =>
            CampanhaAtual?.FichasAceitas ?? Enumerable.Empty<PersonagemCampanha>();

        public string StatusDaFichaInspecionada =>
            CampanhaAtual?.PersonagemCampanhas
                .FirstOrDefault(pc => pc.IdPersonagem == FichaInspecionada?.IdPersonagem)?.StatusAprovacao ?? "Pendente";

        public IEnumerable<NPC> NpcsVinculadosFiltrados =>
            (CampanhaAtual?.Npcs ?? Array.Empty<NPC>()).Where(n =>
                string.IsNullOrWhiteSpace(TermoBusca) ||
                n.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
                (n.ClasseTexto != null && n.ClasseTexto.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase)) ||
                (n.Ocupacao != null && n.Ocupacao.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase)));

        public IEnumerable<MonstroCampanha> MonstrosVinculadosFiltrados =>
            (CampanhaAtual?.MonstroCampanhas ?? Enumerable.Empty<MonstroCampanha>()).Where(mc =>
                string.IsNullOrWhiteSpace(TermoBusca) ||
                (mc.Monstro != null && (
                    mc.Monstro.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
                    (mc.Monstro.TipoMonstro != null && mc.Monstro.TipoMonstro.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase))
                )));

        public IEnumerable<ItemCampanha> ItensVinculadosFiltrados =>
            (CampanhaAtual?.ItemCampanhas ?? Enumerable.Empty<ItemCampanha>()).Where(ic =>
                string.IsNullOrWhiteSpace(TermoBusca) ||
                (ic.Item != null && (
                    ic.Item.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
                    (ic.Item.Tipo != null && ic.Item.Tipo.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase))
                )));

        public IEnumerable<NPC> NpcsDisponiveisFiltrados =>
            NpcsDisponiveis.Where(n =>
                string.IsNullOrWhiteSpace(TermoBusca) ||
                n.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
                (n.ClasseTexto != null && n.ClasseTexto.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase)) ||
                (n.Ocupacao != null && n.Ocupacao.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase)));

        public IEnumerable<Monstro> MonstrosDisponiveisFiltrados =>
            MonstrosDisponiveis.Where(m =>
                string.IsNullOrWhiteSpace(TermoBusca) ||
                m.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
                (m.TipoMonstro != null && m.TipoMonstro.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase)));

        public IEnumerable<Item> ItensDisponiveisFiltrados =>
            ItensDisponiveis.Where(i =>
                string.IsNullOrWhiteSpace(TermoBusca) ||
                i.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
                (i.Tipo != null && i.Tipo.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase)));

        public async Task CarregarDadosCampanhaAsync(int idCampanha)
        {
            IdCampanha = idCampanha;
            if (IdCampanha > 0)
            {
                CampanhaAtual = await _campanhaService.ObterDetalhesCampanhaAsync(IdCampanha);
                if (CampanhaAtual != null)
                {
                    var todosMonstros = await _campanhaService.ObterMonstrosParaSessaoAsync(CampanhaAtual.IdAccount);
                    var idsMonstrosVinculados = CampanhaAtual.MonstroCampanhas.Select(mc => mc.IdMonstro).ToList();
                    MonstrosDisponiveis = todosMonstros.Where(m => !idsMonstrosVinculados.Contains(m.IdMonstro)).ToList();

                    var todosNpcsUsuario = await _npcService.ObterNpcsPorUsuarioAsync(CampanhaAtual.IdAccount);
                    var idsNpcsVinculados = CampanhaAtual.NpcCampanhas.Select(nc => nc.IdNpc).ToList();
                    NpcsDisponiveis = todosNpcsUsuario.Where(n => !idsNpcsVinculados.Contains(n.IdNpc)).ToList();

                    var todosItens = await _campanhaService.ObterItensParaSessaoAsync(CampanhaAtual.IdAccount);
                    var idsItensVinculados = CampanhaAtual.ItemCampanhas.Select(ic => ic.IdItem).ToList();
                    ItensDisponiveis = todosItens.Where(i => !idsItensVinculados.Contains(i.IdItem)).ToList();
                }
            }
            NotifyStateChanged();
        }

        public async Task InspecionarFichaNaAbaAsync(int idPersonagem)
        {
            AbaMenu = "fichas";
            FiltroFichasMestre = "aceitas";
            await CarregarFichaParaInspecaoAsync(idPersonagem);
        }

        public async Task CarregarFichaParaInspecaoAsync(int idPersonagem)
        {
            FichaInspecionada = await _personagemService.ObterPorIdAsync(idPersonagem);
            AbaInspecao = "atributos";
            SecaoDiarioInspecao = "ideais";
            NotifyStateChanged();
        }

        public async Task AprovarFichaJogadorAsync(int idPersonagem)
        {
            var resultado = await _campanhaService.AprovarFichaCampanhaAsync(IdCampanha, idPersonagem);
            MensagemFeedbackFichas = resultado.Mensagem;
            await CarregarDadosCampanhaAsync(IdCampanha);
            if (FichaInspecionada?.IdPersonagem == idPersonagem)
            {
                FichaInspecionada = await _personagemService.ObterPorIdAsync(idPersonagem);
            }
            NotifyStateChanged();
        }

        public void AbrirModalRecusa(int idPersonagem)
        {
            IdPersonagemParaRecusar = idPersonagem;
            TextoMotivoRecusa = string.Empty;
            NotifyStateChanged();
        }

        public async Task ConfirmarRecusaComMotivoAsync()
        {
            if (IdPersonagemParaRecusar <= 0) return;

            var resultado = await _campanhaService.RecusarFichaCampanhaAsync(IdCampanha, IdPersonagemParaRecusar, TextoMotivoRecusa);
            MensagemFeedbackFichas = resultado.Mensagem;
            if (FichaInspecionada?.IdPersonagem == IdPersonagemParaRecusar)
            {
                FichaInspecionada = null;
            }
            IdPersonagemParaRecusar = 0;
            TextoMotivoRecusa = string.Empty;
            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        public async Task SalvarRegistroAsync()
        {
            if (CampanhaAtual == null) return;

            try
            {
                if (CategoriaSelecionada == "npc")
                {
                    var novoNpc = new NPC(
                        idAccount: CampanhaAtual.IdAccount,
                        nome: NpcNome,
                        racaTexto: NpcRaca,
                        classeTexto: NpcClasse,
                        nivel: NpcNivel,
                        antecedenteTexto: NpcAntecedente,
                        tendencia: NpcTendencia,
                        forca: NpcForca,
                        destreza: NpcDestreza,
                        constituicao: NpcConstituicao,
                        inteligencia: NpcInteligencia,
                        sabedoria: NpcSabedoria,
                        carisma: NpcCarisma,
                        historia: NpcHistoria,
                        idCampanha: IdCampanha
                    );

                    var resultado = await _npcService.CriarNpcAsync(CampanhaAtual.IdAccount, novoNpc, IdCampanha);
                    MensagemFeedback = resultado.Sucesso ? "NPC cadastrado e vinculado à campanha!" : resultado.Mensagem;
                    if (resultado.Sucesso) { NpcNome = NpcAntecedente = NpcHistoria = string.Empty; }
                }
                else if (CategoriaSelecionada == "monstro")
                {
                    var novoMonstro = new Monstro
                    {
                        Nome = MonstroNome,
                        TipoMonstro = MonstroTipo,
                        NivelDesafio = MonstroNd,
                        PvMaximo = MonstroVida,
                        ClasseArmadura = MonstroDefesa,
                        Tamanho = MonstroTamanho,
                        Tendencia = MonstroAlinhamento,
                        Deslocamento = MonstroDeslocamento,
                        Sentidos = MonstroSentidos,
                        Resistencias = MonstroResistencias,
                        Habilidades = MonstroHabilidades,
                        Descricao = MonstroDescricao
                    };

                    var resultado = await _campanhaService.CriarE_VincularMonstroNaCampanhaAsync(IdCampanha, novoMonstro);
                    MensagemFeedback = resultado.Mensagem;
                    if (resultado.Sucesso) { MonstroNome = MonstroDescricao = string.Empty; }
                }
                else if (CategoriaSelecionada == "item")
                {
                    var novoItem = new Item(
                        idAccount: CampanhaAtual.IdAccount,
                        nome: ItemNome,
                        tipo: ItemTipo,
                        peso: ItemPeso,
                        preco: ItemPreco,
                        dano: string.IsNullOrWhiteSpace(ItemDano) ? null : ItemDano,
                        tipoDano: string.IsNullOrWhiteSpace(ItemTipoDano) ? null : ItemTipoDano,
                        propriedades: string.IsNullOrWhiteSpace(ItemPropriedades) ? null : ItemPropriedades,
                        caBonus: ItemCaBonus,
                        descricao: ItemDescricao,
                        raridade: string.IsNullOrEmpty(ItemRaridade) ? null : ItemRaridade,
                        sintonizacao: ItemSintonizacao
                    );

                    var resultadoCriacao = await _itemService.CriarItemAsync(CampanhaAtual.IdAccount, novoItem);
                    if (resultadoCriacao.Sucesso)
                    {
                        if (resultadoCriacao.IdItemCriado > 0)
                        {
                            await _campanhaService.AdicionarItemNaCampanhaAsync(IdCampanha, resultadoCriacao.IdItemCriado);
                        }

                        MensagemFeedback = "Item cadastrado e vinculado com sucesso!";
                        ItemNome = ItemTipo = ItemDescricao = string.Empty;
                        ItemDano = ItemTipoDano = ItemPropriedades = string.Empty;
                        ItemPeso = null;
                        ItemPreco = null;
                        ItemCaBonus = 0;
                        ItemRaridade = string.Empty;
                        ItemSintonizacao = "Não";
                    }
                    else
                    {
                        MensagemFeedback = resultadoCriacao.Mensagem;
                    }
                }

                await CarregarDadosCampanhaAsync(IdCampanha);
            }
            catch (Exception ex)
            {
                MensagemFeedback = $"Erro: {ex.Message}";
                NotifyStateChanged();
            }
        }

        public async Task VincularMonstro(int idMonstro)
        {
            var resultado = await _campanhaService.AdicionarMonstroNaCampanhaAsync(IdCampanha, idMonstro);
            MensagemFeedback = resultado.Mensagem;
            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        public async Task VincularNpcExistente(int idNpc)
        {
            var resultado = await _campanhaService.AdicionarNpcNaCampanhaAsync(IdCampanha, idNpc);
            MensagemFeedback = resultado.Mensagem;
            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        public async Task VincularItem(int idItem)
        {
            var resultado = await _campanhaService.AdicionarItemNaCampanhaAsync(IdCampanha, idItem);
            MensagemFeedback = resultado.Mensagem;
            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        public async Task DesvincularMonstro(int idMonstro)
        {
            var resultado = await _campanhaService.RemoverMonstroDaCampanhaAsync(IdCampanha, idMonstro);
            MensagemFeedback = resultado.Mensagem;
            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        public async Task DesvincularNpc(int idNpc)
        {
            var resultado = await _campanhaService.RemoverNpcDaCampanhaAsync(IdCampanha, idNpc);
            MensagemFeedback = resultado.Mensagem;
            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        public async Task DesvincularItem(int idItem)
        {
            var resultado = await _campanhaService.RemoverItemDaCampanhaAsync(IdCampanha, idItem);
            MensagemFeedback = resultado.Mensagem;
            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        public static string ObterModificador(int valor)
        {
            var modificador = (int)Math.Floor((valor - 10) / 2.0);
            return modificador >= 0 ? $"+{modificador}" : modificador.ToString();
        }

        public void AbrirIniciar() { AbaMenu = "iniciar"; NotifyStateChanged(); }
        public void AbrirConfig() { AbaMenu = "config"; NotifyStateChanged(); }
        public void AbrirFichas()
        {
            AbaMenu = "fichas";
            FiltroFichasMestre = FichasPendentes.Any() ? "pendentes" : "aceitas";
            NotifyStateChanged();
        }
        public void AbrirHist() { AbaMenu = "hist"; NotifyStateChanged(); }
    }
}