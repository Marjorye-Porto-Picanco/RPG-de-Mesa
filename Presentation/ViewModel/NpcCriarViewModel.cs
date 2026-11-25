using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Magias;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Presentation.Services;

namespace RPGdeMesa.Presentation.ViewModel
{
    public class NpcCriarViewModel
    {
        private const string RotaBibliotecaNpcs = "/BibliotecaGeral?categoria=npcs";
        private static readonly string? ImagemPadrao = new NPC().Imagem;

        private readonly NpcService _npcService;
        private readonly PersonagemService _personagemService;
        private readonly SessaoUsuarioService _sessaoUsuario;
        private readonly NavigationManager _navigation;
        private readonly AppCacheService _appCache;
        private readonly HomeCacheService _homeCache;
        private readonly ImagemPickerService _imagemPicker;
        private readonly ItemService _itemService;
        private readonly MagiaService _magiaService;

        private AtributoCampo[]? _atributos;

        public sealed record AtributoCampo(string Sigla, Func<int> Obter, Action<int> Definir);

        public NpcCriarViewModel(
            NpcService npcService,
            PersonagemService personagemService,
            SessaoUsuarioService sessaoUsuario,
            NavigationManager navigation,
            AppCacheService appCache,
            HomeCacheService homeCache,
            ImagemPickerService imagemPicker,
            ItemService itemService,
            MagiaService magiaService)
        {
            _npcService = npcService;
            _personagemService = personagemService;
            _sessaoUsuario = sessaoUsuario;
            _navigation = navigation;
            _appCache = appCache;
            _homeCache = homeCache;
            _imagemPicker = imagemPicker;
            _itemService = itemService;
            _magiaService = magiaService;
            LimparFormulario();
        }

        public NPC Npc { get; private set; } = CriarNpcPadrao();
        public int? IdCampanha { get; set; }
        public List<string> Racas { get; private set; } = new();
        public List<string> Classes { get; private set; } = new();
        public List<Item> ItensDisponiveis { get; private set; } = new();
        public List<Magia> MagiasDisponiveis { get; private set; } = new();
        public HashSet<int> ItensSelecionados { get; } = new();
        public HashSet<int> MagiasSelecionadas { get; } = new();

        public string BuscaItem { get; set; } = string.Empty;
        public string BuscaMagia { get; set; } = string.Empty;

        public Item? ItemEmVisualizacao { get; private set; }
        public Magia? MagiaEmVisualizacao { get; private set; }

        public bool CarregandoFoto { get; private set; }
        public string? ErroImagem { get; private set; }
        public bool Processando { get; private set; }
        public bool Sucesso { get; private set; }
        public string Mensagem { get; private set; } = string.Empty;

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();

        public AtributoCampo[] Atributos => _atributos ??= new[]
        {
            new AtributoCampo("FOR", () => Npc.Forca, v => Npc.Forca = v),
            new AtributoCampo("DES", () => Npc.Destreza, v => Npc.Destreza = v),
            new AtributoCampo("CON", () => Npc.Constituicao, v => Npc.Constituicao = v),
            new AtributoCampo("INT", () => Npc.Inteligencia, v => Npc.Inteligencia = v),
            new AtributoCampo("SAB", () => Npc.Sabedoria, v => Npc.Sabedoria = v),
            new AtributoCampo("CAR", () => Npc.Carisma, v => Npc.Carisma = v)
        };

        public IEnumerable<Item> ItensFiltrados => ItensDisponiveis
            .Where(i => !ItensSelecionados.Contains(i.IdItem))
            .Where(i =>
                string.IsNullOrWhiteSpace(BuscaItem) ||
                i.Nome.Contains(BuscaItem, StringComparison.OrdinalIgnoreCase) ||
                (i.Tipo?.Contains(BuscaItem, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.Dano?.Contains(BuscaItem, StringComparison.OrdinalIgnoreCase) ?? false));

        public IEnumerable<Magia> MagiasFiltradas => MagiasDisponiveis
            .Where(m => !MagiasSelecionadas.Contains(m.IdMagia))
            .Where(m =>
                string.IsNullOrWhiteSpace(BuscaMagia) ||
                m.Nome.Contains(BuscaMagia, StringComparison.OrdinalIgnoreCase) ||
                (m.Escola?.Contains(BuscaMagia, StringComparison.OrdinalIgnoreCase) ?? false));

        public IEnumerable<Item> ItensVinculados =>
            ItensDisponiveis.Where(i => ItensSelecionados.Contains(i.IdItem));

        public IEnumerable<Magia> MagiasVinculadas =>
            MagiasDisponiveis.Where(m => MagiasSelecionadas.Contains(m.IdMagia));

        public bool TemFoto =>
            Npc.Imagem?.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) == true;

        public string NomePreview =>
            string.IsNullOrWhiteSpace(Npc.Nome) ? "Aldeão" : Npc.Nome.Trim();

        public string Inicial =>
            StringInfo.GetNextTextElement(NomePreview).ToUpperInvariant();

        public async Task InicializarAsync(int? idCampanha)
        {
            LimparFormulario();
            IdCampanha = idCampanha;

            var racas = await _personagemService.ListarRacasAsync();
            var classes = await _personagemService.ListarClassesAsync();
            ItensDisponiveis = (await _itemService.ObterItensPorUsuarioAsync(_sessaoUsuario.IdUsuarioLogado)).ToList();
            MagiasDisponiveis = await _magiaService.ObterTodasMagiasAsync();

            Racas = racas.Select(r => r.Nome).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Classes = classes.Select(c => c.Nome).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            NotifyStateChanged();
        }

        public void LimparFormulario()
        {
            Npc = CriarNpcPadrao();
            ItensSelecionados.Clear();
            MagiasSelecionadas.Clear();
            BuscaItem = string.Empty;
            BuscaMagia = string.Empty;
            ItemEmVisualizacao = null;
            MagiaEmVisualizacao = null;
            _atributos = null;
            ErroImagem = null;
            CarregandoFoto = false;
            Processando = false;
            Sucesso = false;
            Mensagem = string.Empty;
        }

        public void AdicionarItem(int id)
        {
            ItensSelecionados.Add(id);
            ItemEmVisualizacao = ItensDisponiveis.FirstOrDefault(i => i.IdItem == id);
            MagiaEmVisualizacao = null;
            NotifyStateChanged();
        }

        public void RemoverItem(int id)
        {
            ItensSelecionados.Remove(id);
            if (ItemEmVisualizacao?.IdItem == id)
                ItemEmVisualizacao = ItensVinculados.FirstOrDefault();
            NotifyStateChanged();
        }

        public void SelecionarItem(Item item)
        {
            ItemEmVisualizacao = item;
            MagiaEmVisualizacao = null;
            NotifyStateChanged();
        }

        public void AdicionarMagia(int id)
        {
            MagiasSelecionadas.Add(id);
            MagiaEmVisualizacao = MagiasDisponiveis.FirstOrDefault(m => m.IdMagia == id);
            ItemEmVisualizacao = null;
            NotifyStateChanged();
        }

        public void RemoverMagia(int id)
        {
            MagiasSelecionadas.Remove(id);
            if (MagiaEmVisualizacao?.IdMagia == id)
                MagiaEmVisualizacao = MagiasVinculadas.FirstOrDefault();
            NotifyStateChanged();
        }

        public void SelecionarMagia(Magia magia)
        {
            MagiaEmVisualizacao = magia;
            ItemEmVisualizacao = null;
            NotifyStateChanged();
        }

        public void Voltar() => _navigation.NavigateTo(RotaBibliotecaNpcs);

        public async Task CarregarFotoAsync()
        {
            if (CarregandoFoto) return;
            CarregandoFoto = true;
            ErroImagem = null;
            NotifyStateChanged();

            try
            {
                var res = await _imagemPicker.SelecionarUmaAsync(ladoMaximo: 512);
                if (res.Erro is not null)
                    ErroImagem = res.Erro;
                else if (res.Imagem is not null)
                    Npc.Imagem = res.Imagem.DataUrl;
            }
            finally
            {
                CarregandoFoto = false;
                NotifyStateChanged();
            }
        }

        public void RemoverFoto()
        {
            Npc.Imagem = ImagemPadrao;
            ErroImagem = null;
            NotifyStateChanged();
        }

        public void AoDigitarNumero(ChangeEventArgs e, int min, int max, Action<int> definir)
        {
            if (int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor))
            {
                definir(Math.Clamp(valor, min, max));
                NotifyStateChanged();
            }
        }

        public async Task SalvarAsync()
        {
            if (Processando) return;

            var idUsuario = _sessaoUsuario.IdUsuarioLogado;
            if (idUsuario <= 0)
            {
                Sucesso = false;
                Mensagem = "Usuário não identificado. Faça login novamente.";
                NotifyStateChanged();
                return;
            }

            Processando = true;
            Mensagem = string.Empty;
            NotifyStateChanged();

            try
            {
                Npc.Itens = ItensSelecionados.Select(id => new NpcItem { IdItem = id, Quantidade = 1, Npc = Npc }).ToList();
                Npc.Magias = MagiasSelecionadas.Select(id => new NpcMagia { IdMagia = id, Npc = Npc }).ToList();

                var resultado = await _npcService.CriarNpcAsync(idUsuario, Npc);
                Sucesso = resultado.Sucesso;
                Mensagem = resultado.Mensagem;

                if (resultado.Sucesso)
                {
                    _appCache.InvalidarBiblioteca();
                    _appCache.InvalidarPersonagensLib();
                    _homeCache.Limpar();
                }

                if (Sucesso && IdCampanha is > 0)
                {
                    var vinculo = await _npcService.VincularNpcACampanhaAsync(Npc.IdNpc, IdCampanha.Value);
                    Sucesso = vinculo.Sucesso;
                    Mensagem = vinculo.Sucesso
                        ? "NPC cadastrado e vinculado à campanha com sucesso!"
                        : $"NPC salvo, mas não foi possível vincular à campanha: {vinculo.Mensagem}";
                }

                if (Sucesso)
                {
                    var mensagemFinal = Mensagem;
                    LimparFormulario();
                    Sucesso = true;
                    Mensagem = mensagemFinal;
                    NotifyStateChanged();

                    await Task.Delay(500);
                    _navigation.NavigateTo(RotaBibliotecaNpcs);
                }
            }
            catch (Exception ex)
            {
                Sucesso = false;
                Mensagem = $"Erro ao salvar: {ex.Message}";
            }
            finally
            {
                Processando = false;
                NotifyStateChanged();
            }
        }

        public static string Texto(string? valor, string padrao) =>
            string.IsNullOrWhiteSpace(valor) ? padrao : valor.Trim();

        public static string FormatarCirculo(int nivel) =>
            nivel <= 0 ? "Truque" : $"{nivel}º círculo";

        public static int Modificador(int valor) =>
            (int)Math.Floor((valor - 10) / 2.0);

        public static string FormatarModificador(int valor)
        {
            var mod = Modificador(valor);
            return mod >= 0 ? $"+{mod}" : mod.ToString(CultureInfo.InvariantCulture);
        }

        public static string ClasseModificador(int valor) => Modificador(valor) switch
        {
            > 0 => "mod-pos",
            < 0 => "mod-neg",
            _ => string.Empty
        };

        private static NPC CriarNpcPadrao() => new()
        {
            Nome = "",
            RacaTexto = "Humano",
            ClasseTexto = "Guerreiro",
            Nivel = 1,
            AntecedenteTexto = "",
            Tendencia = "Neutra",
            Forca = 10,
            Destreza = 10,
            Constituicao = 10,
            Inteligencia = 10,
            Sabedoria = 10,
            Carisma = 10,
            Historia = ""
        };
    }
}