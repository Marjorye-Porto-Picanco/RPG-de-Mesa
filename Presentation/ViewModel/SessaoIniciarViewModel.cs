using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Magias;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Presentation.Services;

namespace RPGdeMesa.Presentation.ViewModel
{
    public class SessaoIniciarViewModel
    {
        private static readonly string? ImagemPadraoNpc = new NPC().Imagem;
        private static readonly string? ImagemPadraoMonstro = new Monstro().Imagem;

        private readonly CampanhaService _campanhaService;
        private readonly PersonagemService _personagemService;
        private readonly NpcService _npcService;
        private readonly ItemService _itemService;
        private readonly SessaoService _sessaoService;
        private readonly ImagemPickerService _imagemPicker;
        private readonly AppCacheService _appCache;
        private readonly HomeCacheService _homeCache;
        private readonly NavigationManager _navigation;
        private readonly SessaoUsuarioService _sessaoUsuario;
        // Ataques personalizados de NPC desativados até a implementação da funcionalidade.
        // private readonly EditorAtaques _editorAtaquesNpc = new();
        private readonly EditorAtaques _editorAtaquesMonstro = new();
        private List<AtaqueCatalogoCadastro> _catalogoAtaques = new();

        public SessaoIniciarViewModel(
            CampanhaService campanhaService,
            PersonagemService personagemService,
            NpcService npcService,
            ItemService itemService,
            SessaoService sessaoService,
            ImagemPickerService imagemPicker,
            AppCacheService appCache,
            HomeCacheService homeCache,
            NavigationManager navigation,
            SessaoUsuarioService sessaoUsuario)
        {
            _campanhaService = campanhaService;
            _personagemService = personagemService;
            _npcService = npcService;
            _itemService = itemService;
            _sessaoService = sessaoService;
            _imagemPicker = imagemPicker;
            _appCache = appCache;
            _homeCache = homeCache;
            _navigation = navigation;
            _sessaoUsuario = sessaoUsuario;
        }

        public int IdCampanha { get; set; }
        public Campanha? CampanhaAtual { get; private set; }
        public bool SessaoEmAndamento => HistoricoSessoes.Any(s => s.Ativa);
        public bool CarregandoCampanha { get; private set; }
        public bool CarregandoImagem { get; private set; }
        public bool SalvandoRegistro { get; private set; }
        public List<Monstro> MonstrosDisponiveis { get; private set; } = new();
        public List<NPC> NpcsDisponiveis { get; private set; } = new();
        public List<Item> ItensDisponiveis { get; private set; } = new();
        public List<Magia> MagiasCatalogo { get; private set; } = new();
        public HashSet<int> NpcItensSelecionados { get; } = new();
        public HashSet<int> NpcMagiasSelecionadas { get; } = new();
        public string BuscaItemNpc { get; set; } = string.Empty;
        public string BuscaMagiaNpc { get; set; } = string.Empty;
        public IEnumerable<Item> ItensNpcDisponiveis => (CampanhaAtual?.Itens.AsEnumerable() ?? Enumerable.Empty<Item>())
            .Concat(ItensDisponiveis).GroupBy(i => i.IdItem).Select(g => g.First()).OrderBy(i => i.Nome);
        public IEnumerable<Item> ItensNpcCatalogoFiltrados => ItensNpcDisponiveis
            .Where(i => !NpcItensSelecionados.Contains(i.IdItem))
            .Where(i => string.IsNullOrWhiteSpace(BuscaItemNpc) || i.Nome.Contains(BuscaItemNpc, StringComparison.OrdinalIgnoreCase) || (i.Tipo?.Contains(BuscaItemNpc, StringComparison.OrdinalIgnoreCase) ?? false));
        public IEnumerable<Magia> MagiasNpcCatalogoFiltradas => MagiasCatalogo
            .Where(m => !NpcMagiasSelecionadas.Contains(m.IdMagia))
            .Where(m => string.IsNullOrWhiteSpace(BuscaMagiaNpc) || m.Nome.Contains(BuscaMagiaNpc, StringComparison.OrdinalIgnoreCase) || (m.Escola?.Contains(BuscaMagiaNpc, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(m => m.CirculoNivel).ThenBy(m => m.Nome);
        public List<Mapa> MapasDisponiveis { get; private set; } = new();
        public List<string> OpcoesRacaNpc { get; private set; } = new();
        public List<string> OpcoesClasseNpc { get; private set; } = new();
        public string TermoBusca { get; set; } = string.Empty;

        public string AbaMenu { get; set; } = "iniciar";
        public string CategoriaAtiva { get; set; } = "todos";
        public string ModoVisualizacao { get; set; } = "vinculados";
        public string CategoriaSelecionada { get; set; } = "npc";

        // Histórico de Sessões
        public List<HistoricoSessaoDto> HistoricoSessoes { get; private set; } = new();
        public HistoricoSessaoDto? SessaoHistoricoSelecionada { get; private set; }
        public string TituloHistoricoEdicao { get; set; } = string.Empty;
        public string ResumoHistoricoEdicao { get; set; } = string.Empty;
        public bool SalvandoHistorico { get; private set; }
        public string MensagemFeedbackHistorico { get; private set; } = string.Empty;

        // Chat da Sessão no Histórico
        public bool ExibirChatHistorico { get; private set; }
        public bool CarregandoChatHistorico { get; private set; }
        public List<MensagemSessaoDto> ChatHistoricoMensagens { get; private set; } = new();
        private int _idSessaoChatCarregado;

        // Mapas da Campanha
        public string AbaMapas { get; set; } = "vinculados";
        public string TermoBuscaMapa { get; set; } = string.Empty;
        public Mapa? MapaSelecionadoParaVer { get; private set; }
        public string NovoMapaNome { get; set; } = string.Empty;
        public string NovoMapaEscala { get; set; } = string.Empty;
        public string? NovoMapaImagemBase64 { get; private set; }
        public string? NovoMapaNomeArquivo { get; private set; }
        public string MensagemFeedbackMapas { get; private set; } = string.Empty;

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
        public string? NpcImagem { get; set; } = ImagemPadraoNpc;
        public string? ErroImagemNpc { get; private set; }
        public bool TemFotoNpc => NpcImagem?.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) == true;

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
        public string? MonstroImagem { get; set; } = ImagemPadraoMonstro;
        public string? ErroImagemMonstro { get; private set; }
        public bool TemFotoMonstro => MonstroImagem?.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) == true;

        public IReadOnlyList<AtaqueCadastro> AtaquesMonstro => _editorAtaquesMonstro.Ataques;
        public string BuscaAtaqueMonstro
        {
            get => _editorAtaquesMonstro.Busca;
            set => _editorAtaquesMonstro.Busca = value;
        }

        public IEnumerable<AtaqueCatalogoCadastro> AtaquesMonstroCatalogoFiltrados
        {
            get
            {
                var selecionados = AtaquesMonstro.Select(a => a.IdCatalogo).ToHashSet();
                var busca = BuscaAtaqueMonstro.Trim();
                return _catalogoAtaques.Where(a => !selecionados.Contains(a.Id) &&
                    (busca.Length == 0 || a.Nome.Contains(busca, StringComparison.OrdinalIgnoreCase) ||
                     a.Origem.Contains(busca, StringComparison.OrdinalIgnoreCase) ||
                     a.Dano?.Contains(busca, StringComparison.OrdinalIgnoreCase) == true));
            }
        }

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

        public IEnumerable<Mapa> MapasVinculadosFiltrados =>
            (CampanhaAtual?.MapaCampanhas ?? Enumerable.Empty<MapaCampanha>())
                .Where(mc => mc.Mapa != null)
                .Select(mc => mc.Mapa!)
                .Where(m =>
                    string.IsNullOrWhiteSpace(TermoBuscaMapa) ||
                    m.NomeRegiao.Contains(TermoBuscaMapa, StringComparison.OrdinalIgnoreCase) ||
                    (m.Escala != null && m.Escala.Contains(TermoBuscaMapa, StringComparison.OrdinalIgnoreCase)));

        public IEnumerable<Mapa> MapasDisponiveisFiltrados =>
            MapasDisponiveis.Where(m =>
                string.IsNullOrWhiteSpace(TermoBuscaMapa) ||
                m.NomeRegiao.Contains(TermoBuscaMapa, StringComparison.OrdinalIgnoreCase) ||
                (m.Escala != null && m.Escala.Contains(TermoBuscaMapa, StringComparison.OrdinalIgnoreCase)));

        public IEnumerable<NPC> NpcsVinculadosFiltrados =>
            (CampanhaAtual?.Npcs ?? Array.Empty<NPC>()).Where(n =>
                string.IsNullOrWhiteSpace(TermoBusca) ||
                n.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
                (n.ClasseTexto != null && n.ClasseTexto.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase)) ||
                (n.AntecedenteTexto != null && n.AntecedenteTexto.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase)) ||
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
                (n.AntecedenteTexto != null && n.AntecedenteTexto.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase)) ||
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
            bool trocouCampanha = CampanhaAtual?.IdCampanha != idCampanha;
            if (trocouCampanha)
            {
                CarregandoCampanha = true;
                NotifyStateChanged();
            }

            try
            {
                if (!OpcoesRacaNpc.Any() || !OpcoesClasseNpc.Any())
                {
                    var racasNpc = await _personagemService.ListarRacasAsync();
                    var classesNpc = await _personagemService.ListarClassesAsync();
                    OpcoesRacaNpc = racasNpc.Select(r => r.Nome).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    OpcoesClasseNpc = classesNpc.Select(c => c.Nome).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                }
                IdCampanha = idCampanha;
                if (IdCampanha > 0)
                {
                    var campanha = await _campanhaService.ObterDetalhesCampanhaAsync(IdCampanha);
                    if (campanha is not null && campanha.IdAccount != _sessaoUsuario.IdUsuarioLogado)
                    {
                        CampanhaAtual = null;
                        _catalogoAtaques.Clear();
                        _navigation.NavigateTo("/campanhas", replace: true);
                        return;
                    }

                    CampanhaAtual = campanha;
                    var tarefaHistorico = CarregarHistoricoInternoAsync();
                    if (CampanhaAtual != null)
                    {
                        var tarefaMonstros = _campanhaService.ObterMonstrosParaSessaoAsync(CampanhaAtual.IdAccount);
                        var tarefaNpcs = _npcService.ObterNpcsPorUsuarioAsync(CampanhaAtual.IdAccount);
                        var tarefaItens = _campanhaService.ObterItensParaSessaoAsync(CampanhaAtual.IdAccount);
                        var tarefaMapas = _campanhaService.ObterMapasDoUsuarioAsync(CampanhaAtual.IdAccount);
                        var tarefaMagias = _campanhaService.ObterTodasMagiasAsync();

                        await Task.WhenAll(tarefaMonstros, tarefaNpcs, tarefaItens, tarefaMapas, tarefaMagias, tarefaHistorico);
                        MagiasCatalogo = tarefaMagias.Result;
                        var catalogoAtaques = tarefaMonstros.Result.SelectMany(m => m.Ataques.Select(a =>
                            new AtaqueCatalogoCadastro(a.IdMonstroAtaque, m.Nome, a.Nome,
                                a.BonusAtaque, a.Dano, a.TipoDano, a.Alcance, a.Descricao)));
                        // Ao reativar a origem NPC, diferenciar seus IDs dos IDs de monstros.
                        // catalogoAtaques = catalogoAtaques.Concat(tarefaNpcs.Result.SelectMany(n => n.Ataques.Select(a =>
                        //     new AtaqueCatalogoCadastro(a.IdNpcAtaque, n.Nome, a.Nome,
                        //         a.BonusAtaque, a.Dano, a.TipoDano, a.Alcance, a.Descricao))));
                        _catalogoAtaques = catalogoAtaques.OrderBy(a => a.Nome).ThenBy(a => a.Origem).ToList();

                        var idsMonstrosVinculados = CampanhaAtual.MonstroCampanhas.Select(mc => mc.IdMonstro).ToList();
                        MonstrosDisponiveis = tarefaMonstros.Result.Where(m => !idsMonstrosVinculados.Contains(m.IdMonstro)).ToList();

                        var idsNpcsVinculados = CampanhaAtual.NpcCampanhas.Select(nc => nc.IdNpc).ToList();
                        NpcsDisponiveis = tarefaNpcs.Result.Where(n => !idsNpcsVinculados.Contains(n.IdNpc)).ToList();

                        var idsItensVinculados = CampanhaAtual.ItemCampanhas.Select(ic => ic.IdItem).ToList();
                        ItensDisponiveis = tarefaItens.Result.Where(i => !idsItensVinculados.Contains(i.IdItem)).ToList();

                        var idsMapasVinculados = CampanhaAtual.MapaCampanhas.Select(mc => mc.IdMapa).ToList();
                        MapasDisponiveis = tarefaMapas.Result.Where(m => !idsMapasVinculados.Contains(m.IdMapa)).ToList();

                        if (MapaSelecionadoParaVer != null && !idsMapasVinculados.Contains(MapaSelecionadoParaVer.IdMapa))
                        {
                            MapaSelecionadoParaVer = null;
                        }
                    }
                    else
                    {
                        _catalogoAtaques.Clear();
                        await tarefaHistorico;
                    }
                }
            }
            finally
            {
                CarregandoCampanha = false;
                NotifyStateChanged();
            }
        }

        private async Task CarregarHistoricoInternoAsync()
        {
            int? idSelecionadoAntes = SessaoHistoricoSelecionada?.IdSessao;
            HistoricoSessoes = await _sessaoService.ObterHistoricoSessoesAsync(IdCampanha);

            if (idSelecionadoAntes.HasValue)
            {
                SessaoHistoricoSelecionada = HistoricoSessoes.FirstOrDefault(s => s.IdSessao == idSelecionadoAntes.Value)
                    ?? HistoricoSessoes.FirstOrDefault();
            }
            else
            {
                SessaoHistoricoSelecionada = HistoricoSessoes.FirstOrDefault();
            }

            if (SessaoHistoricoSelecionada != null)
            {
                TituloHistoricoEdicao = SessaoHistoricoSelecionada.Titulo;
                ResumoHistoricoEdicao = SessaoHistoricoSelecionada.Resumo ?? string.Empty;
            }
            else
            {
                TituloHistoricoEdicao = string.Empty;
                ResumoHistoricoEdicao = string.Empty;
            }

            if (SessaoHistoricoSelecionada?.IdSessao != _idSessaoChatCarregado)
            {
                ExibirChatHistorico = false;
                ChatHistoricoMensagens.Clear();
                _idSessaoChatCarregado = 0;
            }
        }

        public void SelecionarSessaoHistorico(HistoricoSessaoDto sessao)
        {
            SessaoHistoricoSelecionada = sessao;
            TituloHistoricoEdicao = sessao.Titulo;
            ResumoHistoricoEdicao = sessao.Resumo ?? string.Empty;
            MensagemFeedbackHistorico = string.Empty;
            ExibirChatHistorico = false;
            ChatHistoricoMensagens.Clear();
            _idSessaoChatCarregado = 0;
            NotifyStateChanged();
        }

        public async Task AlternarChatHistoricoAsync()
        {
            if (SessaoHistoricoSelecionada == null)
                return;

            if (ExibirChatHistorico)
            {
                ExibirChatHistorico = false;
                NotifyStateChanged();
                return;
            }

            ExibirChatHistorico = true;

            if (_idSessaoChatCarregado != SessaoHistoricoSelecionada.IdSessao)
            {
                CarregandoChatHistorico = true;
                NotifyStateChanged();

                try
                {
                    ChatHistoricoMensagens = await _sessaoService.ObterChatHistoricoSessaoAsync(SessaoHistoricoSelecionada.IdSessao);
                    _idSessaoChatCarregado = SessaoHistoricoSelecionada.IdSessao;
                }
                finally
                {
                    CarregandoChatHistorico = false;
                }
            }

            NotifyStateChanged();
        }

        public async Task SalvarResumoHistoricoAsync()
        {
            if (CampanhaAtual == null || SessaoHistoricoSelecionada == null || SalvandoHistorico)
                return;

            SalvandoHistorico = true;
            MensagemFeedbackHistorico = string.Empty;
            NotifyStateChanged();

            try
            {
                var (sucesso, mensagem) = await _sessaoService.SalvarResumoHistoricoAsync(
                    IdCampanha,
                    SessaoHistoricoSelecionada.IdSessao,
                    CampanhaAtual.IdAccount,
                    TituloHistoricoEdicao,
                    ResumoHistoricoEdicao);

                MensagemFeedbackHistorico = mensagem;
                if (sucesso)
                {
                    await CarregarHistoricoInternoAsync();
                }
            }
            finally
            {
                SalvandoHistorico = false;
                NotifyStateChanged();
            }
        }

        // --- FOTO DO NPC ---
        public async Task SelecionarFotoNpcAsync()
        {
            if (CarregandoImagem) return;
            CarregandoImagem = true;
            ErroImagemNpc = null;
            NotifyStateChanged();

            try
            {
                var res = await _imagemPicker.SelecionarUmaAsync(ladoMaximo: 512);
                if (res.Erro is not null)
                    ErroImagemNpc = res.Erro;
                else if (res.Imagem is not null)
                    NpcImagem = res.Imagem.DataUrl;
            }
            finally
            {
                CarregandoImagem = false;
                NotifyStateChanged();
            }
        }

        public void RemoverFotoNpc()
        {
            NpcImagem = ImagemPadraoNpc;
            ErroImagemNpc = null;
            NotifyStateChanged();
        }

        private void LimparFormularioNpc()
        {
            // _editorAtaquesNpc.Limpar();
            NpcItensSelecionados.Clear();
            NpcMagiasSelecionadas.Clear();
            BuscaItemNpc = string.Empty;
            BuscaMagiaNpc = string.Empty;
            NpcNome = string.Empty;
            NpcRaca = "Humano";
            NpcClasse = "Guerreiro";
            NpcNivel = 1;
            NpcTendencia = "Neutra";
            NpcAntecedente = string.Empty;
            NpcForca = 10;
            NpcDestreza = 10;
            NpcConstituicao = 10;
            NpcInteligencia = 10;
            NpcSabedoria = 10;
            NpcCarisma = 10;
            NpcHistoria = string.Empty;
            NpcImagem = ImagemPadraoNpc;
            ErroImagemNpc = null;
        }

        public void AlterarItemNpc(int idItem, bool selecionado)
        {
            if (selecionado) NpcItensSelecionados.Add(idItem); else NpcItensSelecionados.Remove(idItem);
            NotifyStateChanged();
        }

        public void AlterarMagiaNpc(int idMagia, bool selecionado)
        {
            if (selecionado) NpcMagiasSelecionadas.Add(idMagia); else NpcMagiasSelecionadas.Remove(idMagia);
            NotifyStateChanged();
        }

        // --- FOTO DO MONSTRO ---
        public async Task SelecionarFotoMonstroAsync()
        {
            if (CarregandoImagem) return;
            CarregandoImagem = true;
            ErroImagemMonstro = null;
            NotifyStateChanged();

            try
            {
                var res = await _imagemPicker.SelecionarUmaAsync(ladoMaximo: 512);
                if (res.Erro is not null)
                    ErroImagemMonstro = res.Erro;
                else if (res.Imagem is not null)
                    MonstroImagem = res.Imagem.DataUrl;
            }
            finally
            {
                CarregandoImagem = false;
                NotifyStateChanged();
            }
        }

        public void RemoverFotoMonstro()
        {
            MonstroImagem = ImagemPadraoMonstro;
            ErroImagemMonstro = null;
            NotifyStateChanged();
        }

        private void LimparFormularioMonstro()
        {
            _editorAtaquesMonstro.Limpar();
            MonstroNome = string.Empty;
            MonstroTipo = "Outro";
            MonstroTamanho = "Médio";
            MonstroAlinhamento = string.Empty;
            MonstroNd = 1;
            MonstroDefesa = 12;
            MonstroVida = 15;
            MonstroDeslocamento = "9 m";
            MonstroSentidos = string.Empty;
            MonstroResistencias = string.Empty;
            MonstroHabilidades = string.Empty;
            MonstroDescricao = string.Empty;
            MonstroImagem = ImagemPadraoMonstro;
            ErroImagemMonstro = null;
        }

        public AtaqueCadastro AdicionarAtaqueMonstro()
        {
            var ataque = new AtaqueCadastro();
            _editorAtaquesMonstro.Ataques.Add(ataque);
            NotifyStateChanged();
            return ataque;
        }

        public void RemoverAtaqueMonstro(AtaqueCadastro ataque)
        {
            if (_editorAtaquesMonstro.Ataques.Remove(ataque))
                NotifyStateChanged();
        }

        public void VincularAtaqueCatalogoMonstro(AtaqueCatalogoCadastro ataque)
        {
            var ataques = _editorAtaquesMonstro.Ataques;
            if (ataques.Any(a => a.IdCatalogo == ataque.Id))
                return;

            ataques.Add(new AtaqueCadastro
            {
                IdCatalogo = ataque.Id,
                Nome = ataque.Nome,
                BonusAtaque = ataque.BonusAtaque,
                Dano = ataque.Dano,
                TipoDano = ataque.TipoDano,
                Alcance = ataque.Alcance,
                Descricao = ataque.Descricao
            });
            NotifyStateChanged();
        }

        private sealed class EditorAtaques
        {
            public List<AtaqueCadastro> Ataques { get; } = new();
            public string Busca { get; set; } = string.Empty;

            public IEnumerable<AtaqueCadastro> AtaquesPreenchidos =>
                Ataques.Where(a => !string.IsNullOrWhiteSpace(a.Nome));

            public void Limpar()
            {
                Ataques.Clear();
                Busca = string.Empty;
            }
        }

        // --- MÉTODOS DE MAPAS ---
        public void SelecionarMapaParaVisualizar(Mapa? mapa)
        {
            if (mapa != null && MapaSelecionadoParaVer?.IdMapa == mapa.IdMapa)
                MapaSelecionadoParaVer = null;
            else
                MapaSelecionadoParaVer = mapa;

            NotifyStateChanged();
        }

        public async Task SelecionarImagemNovoMapaAsync()
        {
            if (CarregandoImagem) return;
            CarregandoImagem = true;
            MensagemFeedbackMapas = string.Empty;
            NotifyStateChanged();

            try
            {
                var res = await _imagemPicker.SelecionarUmaAsync(ladoMaximo: 1600);
                if (res.Erro is not null)
                {
                    MensagemFeedbackMapas = res.Erro;
                }
                else if (res.Imagem is not null)
                {
                    NovoMapaImagemBase64 = res.Imagem.DataUrl;
                    NovoMapaNomeArquivo = res.Imagem.NomeArquivo;

                    if (string.IsNullOrWhiteSpace(NovoMapaNome))
                        NovoMapaNome = Path.GetFileNameWithoutExtension(res.Imagem.NomeArquivo);
                }
            }
            finally
            {
                CarregandoImagem = false;
                NotifyStateChanged();
            }
        }

        public async Task CriarEVincularNovoMapaAsync()
        {
            if (CampanhaAtual == null) return;

            if (string.IsNullOrWhiteSpace(NovoMapaNome))
            {
                MensagemFeedbackMapas = "Informe o nome da região/mapa.";
                NotifyStateChanged();
                return;
            }

            if (string.IsNullOrWhiteSpace(NovoMapaImagemBase64))
            {
                MensagemFeedbackMapas = "Selecione uma imagem para o mapa.";
                NotifyStateChanged();
                return;
            }

            var novoMapa = new Mapa
            {
                IdAccount = CampanhaAtual.IdAccount,
                NomeRegiao = NovoMapaNome.Trim(),
                Escala = string.IsNullOrWhiteSpace(NovoMapaEscala) ? null : NovoMapaEscala.Trim(),
                ImagemUrl = NovoMapaImagemBase64
            };

            var resultado = await _campanhaService.CriarEVincularMapaNaCampanhaAsync(IdCampanha, novoMapa);
            MensagemFeedbackMapas = resultado.Mensagem;

            if (resultado.Sucesso)
            {
                NovoMapaNome = string.Empty;
                NovoMapaEscala = string.Empty;
                NovoMapaImagemBase64 = null;
                NovoMapaNomeArquivo = null;
                AbaMapas = "vinculados";
                await CarregarDadosCampanhaAsync(IdCampanha);
                MapaSelecionadoParaVer = CampanhaAtual?.MapaCampanhas
                    .Select(mc => mc.Mapa)
                    .FirstOrDefault(m => m != null && m.IdMapa == novoMapa.IdMapa);
                NotifyStateChanged();
            }
            else
            {
                NotifyStateChanged();
            }
        }

        public async Task VincularMapaExistenteAsync(int idMapa)
        {
            var resultado = await _campanhaService.AdicionarMapaNaCampanhaAsync(IdCampanha, idMapa);
            MensagemFeedbackMapas = resultado.Mensagem;
            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        public async Task DesvincularMapaAsync(int idMapa)
        {
            var resultado = await _campanhaService.RemoverMapaDaCampanhaAsync(IdCampanha, idMapa);
            MensagemFeedbackMapas = resultado.Mensagem;
            if (MapaSelecionadoParaVer?.IdMapa == idMapa)
                MapaSelecionadoParaVer = null;

            await CarregarDadosCampanhaAsync(IdCampanha);
        }

        // --- FICHAS ---
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

        // --- CONFIGURAÇÕES (NPC, MONSTRO, ITEM) ---
        public async Task SalvarRegistroAsync()
        {
            if (CampanhaAtual == null || CampanhaAtual.IdAccount != _sessaoUsuario.IdUsuarioLogado || SalvandoRegistro)
                return;

            SalvandoRegistro = true;
            NotifyStateChanged();

            try
            {
                int idUsuario = CampanhaAtual.IdAccount;

                if (CategoriaSelecionada == "npc")
                {
                    var novoNpc = new NPC(
                        idAccount: idUsuario,
                        nome: NpcNome,
                        racaTexto: NpcRaca,
                        classeTexto: NpcClasse,
                        nivel: Math.Clamp(NpcNivel, 1, 20),
                        antecedenteTexto: NpcAntecedente,
                        tendencia: NpcTendencia,
                        forca: Math.Clamp(NpcForca, 1, 30),
                        destreza: Math.Clamp(NpcDestreza, 1, 30),
                        constituicao: Math.Clamp(NpcConstituicao, 1, 30),
                        inteligencia: Math.Clamp(NpcInteligencia, 1, 30),
                        sabedoria: Math.Clamp(NpcSabedoria, 1, 30),
                        carisma: Math.Clamp(NpcCarisma, 1, 30),
                        historia: NpcHistoria,
                        imagem: NpcImagem,
                        idCampanha: IdCampanha
                    );
                    novoNpc.Itens = NpcItensSelecionados.Select(id => new NpcItem { IdItem = id, Quantidade = 1, Npc = novoNpc }).ToList();
                    novoNpc.Magias = NpcMagiasSelecionadas.Select(id => new NpcMagia { IdMagia = id, Npc = novoNpc }).ToList();
                    /*
                    novoNpc.Ataques = _editorAtaquesNpc.AtaquesPreenchidos.Select(a => new NpcAtaque
                    {
                        Nome = a.Nome.Trim(),
                        BonusAtaque = a.BonusAtaque,
                        Dano = a.Dano,
                        TipoDano = a.TipoDano,
                        Alcance = a.Alcance,
                        Descricao = a.Descricao
                    }).ToList();
                    */

                    var resultado = await _npcService.CriarNpcAsync(idUsuario, novoNpc, IdCampanha);
                    MensagemFeedback = resultado.Sucesso ? "NPC cadastrado e vinculado à campanha!" : resultado.Mensagem;
                    if (resultado.Sucesso)
                    {
                        if (novoNpc.IdNpc > 0)
                        {
                            _appCache.UpsertNpc(idUsuario, novoNpc);
                            _homeCache.UpsertNpc(idUsuario, novoNpc);
                        }
                        else
                        {
                            _appCache.InvalidarBiblioteca();
                            _appCache.InvalidarPersonagensLib();
                            _homeCache.Limpar();
                        }

                        LimparFormularioNpc();
                    }
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
                        Descricao = MonstroDescricao,
                        Imagem = MonstroImagem,
                        Ataques = _editorAtaquesMonstro.AtaquesPreenchidos.Select(a => new MonstroAtaque
                        {
                            Nome = a.Nome.Trim(),
                            BonusAtaque = a.BonusAtaque,
                            Dano = a.Dano,
                            TipoDano = a.TipoDano,
                            Alcance = a.Alcance,
                            Descricao = a.Descricao
                        }).ToList()
                    };

                    var resultado = await _campanhaService.CriarE_VincularMonstroNaCampanhaAsync(IdCampanha, novoMonstro);
                    MensagemFeedback = resultado.Mensagem;
                    if (resultado.Sucesso)
                    {
                        if (novoMonstro.IdMonstro > 0)
                            _appCache.UpsertMonstro(idUsuario, novoMonstro);
                        else
                            _appCache.InvalidarBiblioteca();

                        LimparFormularioMonstro();
                    }
                }
                else if (CategoriaSelecionada == "item")
                {
                    var novoItem = new Item(
                        idAccount: idUsuario,
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

                    var resultadoCriacao = await _itemService.CriarItemAsync(idUsuario, novoItem);
                    if (resultadoCriacao.Sucesso)
                    {
                        if (resultadoCriacao.IdItemCriado > 0)
                        {
                            await _campanhaService.AdicionarItemNaCampanhaAsync(IdCampanha, resultadoCriacao.IdItemCriado);
                        }

                        _appCache.InvalidarBiblioteca();
                        _homeCache.Limpar();

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
            }
            finally
            {
                SalvandoRegistro = false;
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
        public void AbrirMapas() { AbaMenu = "mapas"; NotifyStateChanged(); }
        public void AbrirNotas() { AbaMenu = "notas"; NotifyStateChanged(); }
        public void AbrirFichas()
        {
            AbaMenu = "fichas";
            FiltroFichasMestre = FichasPendentes.Any() ? "pendentes" : "aceitas";
            NotifyStateChanged();
        }
        public async Task AbrirHistAsync()
        {
            AbaMenu = "hist";
            MensagemFeedbackHistorico = string.Empty;
            if (IdCampanha > 0)
            {
                await CarregarHistoricoInternoAsync();
            }
            NotifyStateChanged();
        }
    }
}
