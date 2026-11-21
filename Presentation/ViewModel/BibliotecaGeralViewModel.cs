using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Magias;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Presentation.Services;

namespace RPGdeMesa.Presentation.ViewModel;

public sealed record CategoriaItem(string Chave, string Nome);
public sealed record ExclusaoPendente(string Tipo, int Id, string Nome);

public class BibliotecaGeralViewModel
{
    private static readonly TimeSpan ValidadeCache = TimeSpan.FromSeconds(45);

    private readonly ItemService _itemService;
    private readonly MagiaService _magiaService;
    private readonly MonstroService _monstroService;
    private readonly NpcService _npcService;
    private readonly SessaoUsuarioService _sessaoUsuario;
    private readonly AppCacheService _cache;
    private readonly HomeCacheService _homeCache;

    public BibliotecaGeralViewModel(
        ItemService itemService,
        MagiaService magiaService,
        MonstroService monstroService,
        NpcService npcService,
        SessaoUsuarioService sessaoUsuario,
        AppCacheService cache,
        HomeCacheService homeCache)
    {
        _itemService = itemService;
        _magiaService = magiaService;
        _monstroService = monstroService;
        _npcService = npcService;
        _sessaoUsuario = sessaoUsuario;
        _cache = cache;
        _homeCache = homeCache;
    }

    public int IdUsuarioLogado => _sessaoUsuario.IdUsuarioLogado;
    public bool EstaLogado => _sessaoUsuario.EstaLogado;

    public IReadOnlyList<CategoriaItem> Categorias { get; } =
    [
        new("itens", "Itens"),
        new("magias", "Magias"),
        new("monstros", "Monstros"),
        new("npcs", "NPCs")
    ];

    public IReadOnlyList<CategoriaItem> OpcoesTipoItem { get; } =
    [
        new("", "Todos os tipos"),
        new("armas", "Armas"),
        new("armaduras", "Armaduras"),
        new("equipamentos", "Equipamentos de Aventura"),
        new("ferramentas", "Ferramentas"),
        new("montarias", "Montarias e Veículos"),
        new("pocoes", "Poções"),
        new("aneis", "Anéis"),
        new("cajados", "Cajados"),
        new("bastoes", "Bastões"),
        new("varinhas", "Varinhas"),
        new("pergaminhos", "Pergaminhos"),
        new("maravilhosos", "Itens Maravilhosos")
    ];

    public IReadOnlyList<CategoriaItem> OpcoesCirculoMagia { get; } =
    [
        new("", "Todos os círculos"),
        new("0", "Truque"),
        new("1", "1º Círculo"),
        new("2", "2º Círculo"),
        new("3", "3º Círculo"),
        new("4", "4º Círculo"),
        new("5", "5º Círculo"),
        new("6", "6º Círculo"),
        new("7", "7º Círculo"),
        new("8", "8º Círculo"),
        new("9", "9º Círculo")
    ];

    public IEnumerable<string> OpcoesEscolaMagia => EntradasMagias
        .Select(m => m.Escola?.Trim())
        .Where(e => !string.IsNullOrWhiteSpace(e))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(e => e)
        .Cast<string>();

    public string CategoriaAtual { get; private set; } = "itens";
    public string TermoBusca { get; set; } = string.Empty;
    public string FiltroTipoItem { get; set; } = string.Empty;
    public string FiltroCirculoMagia { get; set; } = string.Empty;
    public string FiltroEscolaMagia { get; set; } = string.Empty;
    public int LimiteVisivel { get; private set; } = 30;
    public bool Carregando { get; private set; }
    public bool TemErro { get; private set; }
    public string StatusMensagem { get; private set; } = "Carregando biblioteca...";

    // Coleções carregadas
    public List<Item> EntradasItens { get; private set; } = new();
    public List<Magia> EntradasMagias { get; private set; } = new();
    public List<Monstro> EntradasMonstros { get; private set; } = new();
    public List<NPC> EntradasNpcs { get; private set; } = new();

    // Estado dos Modais
    public bool ExibirModalCriacao { get; private set; }
    public Item? ItemSelecionado { get; private set; }
    public bool ExibirModalItem { get; private set; }
    public Magia? MagiaSelecionada { get; private set; }
    public bool ExibirModalMagia { get; private set; }
    public Monstro? MonstroSelecionado { get; private set; }
    public bool ExibirModalMonstro { get; private set; }
    public NPC? NpcSelecionado { get; private set; }
    public bool ExibirModalNpc { get; private set; }

    // Estado de Exclusão
    public ExclusaoPendente? ExclusaoAtual { get; private set; }
    public bool Excluindo { get; private set; }
    public string? ErroExclusao { get; private set; }

    // Filtros Calculados
    public IEnumerable<Item> ItensFiltrados => EntradasItens
        .Where(item => CorrespondeAoTipoSelecionado(item, FiltroTipoItem))
        .Where(item => string.IsNullOrWhiteSpace(TermoBusca) ||
            item.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
            item.Tipo?.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) == true ||
            ObterRotuloMeta(item).Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
            item.Descricao?.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) == true);

    public IEnumerable<Magia> MagiasFiltradas => EntradasMagias
        .Where(m => string.IsNullOrWhiteSpace(FiltroCirculoMagia) ||
            (int.TryParse(FiltroCirculoMagia, out int circulo) && m.CirculoNivel == circulo))
        .Where(m => string.IsNullOrWhiteSpace(FiltroEscolaMagia) ||
            string.Equals(m.Escola?.Trim(), FiltroEscolaMagia.Trim(), StringComparison.OrdinalIgnoreCase))
        .Where(m => string.IsNullOrWhiteSpace(TermoBusca) ||
            m.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
            m.Escola?.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) == true ||
            m.DanoEfeito?.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) == true ||
            m.Alcance?.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) == true ||
            ObterRotuloCirculo(m.CirculoNivel).Contains(TermoBusca, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Monstro> MonstrosFiltrados => EntradasMonstros
        .Where(m => string.IsNullOrWhiteSpace(TermoBusca) ||
            m.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
            m.Tipo?.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) == true ||
            m.Tendencia?.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) == true);

    public IEnumerable<NPC> NpcsFiltrados => EntradasNpcs
        .Where(n => string.IsNullOrWhiteSpace(TermoBusca) ||
            n.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
            n.ClasseTexto?.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) == true ||
            n.RacaTexto?.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) == true ||
            n.Historia?.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) == true);

    public bool PodeMostrarMais => CategoriaAtual switch
    {
        "magias" => MagiasFiltradas.Count() > LimiteVisivel,
        "monstros" => MonstrosFiltrados.Count() > LimiteVisivel,
        "npcs" => NpcsFiltrados.Count() > LimiteVisivel,
        _ => ItensFiltrados.Count() > LimiteVisivel
    };

    public Task CarregarDadosAsync() => CarregarDadosAsync(false);

    public async Task CarregarDadosAsync(bool forcarAtualizacao)
    {
        TemErro = false;
        LimiteVisivel = 30;

        if (!EstaLogado)
        {
            EntradasItens.Clear();
            EntradasMagias.Clear();
            EntradasMonstros.Clear();
            EntradasNpcs.Clear();
            StatusMensagem = "Faça login para visualizar a biblioteca.";
            return;
        }

        var snapshot = _cache.ObterBiblioteca(IdUsuarioLogado);
        if (snapshot is not null)
        {
            EntradasItens = snapshot.Itens;
            EntradasMagias = snapshot.Magias;
            EntradasMonstros = snapshot.Monstros;
            EntradasNpcs = snapshot.Npcs;
            AtualizarMensagemStatus();

            if (!forcarAtualizacao && !_cache.BibliotecaExpirada(IdUsuarioLogado, ValidadeCache))
                return;
        }

        Carregando = snapshot is null;
        if (Carregando)
            StatusMensagem = "Carregando registros do banco de dados...";

        try
        {
            var itens = await _itemService.ObterItensPorUsuarioAsync(IdUsuarioLogado);
            var magias = await _magiaService.ObterTodasMagiasAsync();
            var monstros = await _monstroService.ObterMonstrosPorUsuarioAsync(IdUsuarioLogado);
            var npcs = await _npcService.ObterNpcsPorUsuarioAsync(IdUsuarioLogado);

            EntradasItens = itens.OrderBy(i => i.Nome).ToList();
            EntradasMagias = magias;
            EntradasMonstros = monstros.OrderBy(m => m.Nome).ToList();
            EntradasNpcs = npcs.OrderBy(n => n.Nome).ToList();

            _cache.SalvarBiblioteca(new BibliotecaCacheSnapshot
            {
                IdUsuario = IdUsuarioLogado,
                AtualizadoEmUtc = DateTime.UtcNow,
                Itens = EntradasItens,
                Magias = EntradasMagias,
                Monstros = EntradasMonstros,
                Npcs = EntradasNpcs
            });

            AtualizarMensagemStatus();
        }
        catch (Exception ex)
        {
            TemErro = true;
            StatusMensagem = "Não foi possível carregar os registros da biblioteca.";
            System.Diagnostics.Debug.WriteLine($"[Biblioteca] Erro: {ex.Message}");
        }
        finally
        {
            Carregando = false;
        }
    }

    public void TrocarCategoria(string categoria)
    {
        if (string.Equals(categoria, "armas", StringComparison.OrdinalIgnoreCase))
        {
            CategoriaAtual = "itens";
            FiltroTipoItem = "armas";
            FiltroCirculoMagia = string.Empty;
            FiltroEscolaMagia = string.Empty;
        }
        else
        {
            CategoriaAtual = categoria;
            if (categoria != "itens")
                FiltroTipoItem = string.Empty;

            if (categoria != "magias")
            {
                FiltroCirculoMagia = string.Empty;
                FiltroEscolaMagia = string.Empty;
            }
        }

        ResetarPaginacao();
        FecharTodosModaisDetalhes();
        AtualizarMensagemStatus();
    }

    public void AtualizarMensagemStatus()
    {
        StatusMensagem = CategoriaAtual switch
        {
            "magias" => $"{MagiasFiltradas.Count()} magia(s) exibida(s).",
            "monstros" => $"{MonstrosFiltrados.Count()} monstro(s) exibido(s).",
            "npcs" => $"{NpcsFiltrados.Count()} NPC(s) exibido(s).",
            _ => $"{ItensFiltrados.Count()} item(ns) exibido(s)."
        };
    }

    public void ResetarPaginacao()
    {
        LimiteVisivel = 30;
        AtualizarMensagemStatus();
    }

    public void MostrarMais() => LimiteVisivel += 30;

    // Ações de Modais
    public void AbrirModalCriacao() => ExibirModalCriacao = true;
    public void FecharModalCriacao() => ExibirModalCriacao = false;

    public void AbrirModalItem(Item item) { ItemSelecionado = item; ExibirModalItem = true; }
    public void FecharModalItem() { ExibirModalItem = false; ItemSelecionado = null; }

    public void AbrirModalMagia(Magia magia) { MagiaSelecionada = magia; ExibirModalMagia = true; }
    public void FecharModalMagia() { ExibirModalMagia = false; MagiaSelecionada = null; }

    public void AbrirModalMonstro(Monstro monstro) { MonstroSelecionado = monstro; ExibirModalMonstro = true; }
    public void FecharModalMonstro() { ExibirModalMonstro = false; MonstroSelecionado = null; }

    public void AbrirModalNpc(NPC npc) { NpcSelecionado = npc; ExibirModalNpc = true; }
    public void FecharModalNpc() { ExibirModalNpc = false; NpcSelecionado = null; }

    private void FecharTodosModaisDetalhes()
    {
        FecharModalItem();
        FecharModalMagia();
        FecharModalMonstro();
        FecharModalNpc();
    }

    // Ações de Exclusão
    public void PedirExclusao(string tipo, int id, string nome)
    {
        ErroExclusao = null;
        ExclusaoAtual = new ExclusaoPendente(tipo, id, nome);
    }

    public void CancelarExclusao()
    {
        if (Excluindo) return;
        ExclusaoAtual = null;
        ErroExclusao = null;
    }

    public async Task ConfirmarExclusaoAsync()
    {
        if (ExclusaoAtual is null || Excluindo) return;

        Excluindo = true;
        ErroExclusao = null;
        try
        {
            var tipoExcluido = ExclusaoAtual.Tipo;
            var resultado = tipoExcluido switch
            {
                "monstro" => await _monstroService.RemoverMonstroAsync(IdUsuarioLogado, ExclusaoAtual.Id),
                "npc" => await _npcService.RemoverNpcAsync(ExclusaoAtual.Id),
                _ => await _itemService.RemoverItemAsync(IdUsuarioLogado, ExclusaoAtual.Id)
            };

            if (!resultado.Sucesso)
            {
                ErroExclusao = resultado.Mensagem;
                return;
            }

            ExclusaoAtual = null;
            FecharTodosModaisDetalhes();
            _cache.InvalidarBiblioteca();
            if (tipoExcluido == "npc")
                _cache.InvalidarPersonagensLib();
            _homeCache.Limpar();

            await CarregarDadosAsync(forcarAtualizacao: true);
        }
        catch (Exception ex)
        {
            ErroExclusao = ex.InnerException?.Message ?? ex.Message;
        }
        finally
        {
            Excluindo = false;
        }
    }

    // Regras Auxiliares de Filtro e Rótulos
    private static bool CorrespondeAoTipoSelecionado(Item item, string filtroTipo)
    {
        if (string.IsNullOrWhiteSpace(filtroTipo))
            return true;

        var filtro = filtroTipo.Trim().ToLowerInvariant();
        var tipoItem = item.Tipo?.Trim() ?? string.Empty;
        var ehArmadura = ContemTermo(tipoItem, "armadura", "armor", "escudo", "shield");

        return filtro switch
        {
            "armas" => !ehArmadura && (
                !string.IsNullOrWhiteSpace(item.Dano) ||
                ContemTermo(tipoItem, "arma", "weapon", "armamento", "ammunition", "munição", "municao")),

            "armaduras" => ehArmadura,

            "equipamentos" => ContemTermo(tipoItem, "equipamento", "adventuring gear", "gear"),

            "ferramentas" => ContemTermo(tipoItem, "ferramenta", "tool"),

            "montarias" => ContemTermo(tipoItem, "montaria", "veículo", "veiculo", "mount", "vehicle"),

            "pocoes" => ContemTermo(tipoItem, "poção", "pocao", "poções", "pocoes", "potion"),

            "aneis" => ContemTermo(tipoItem, "anel", "anéis", "aneis", "ring"),

            "cajados" => ContemTermo(tipoItem, "cajado", "staff"),

            "bastoes" => ContemTermo(tipoItem, "bastão", "bastao", "bastões", "bastoes", "rod"),

            "varinhas" => ContemTermo(tipoItem, "varinha", "wand"),

            "pergaminhos" => ContemTermo(tipoItem, "pergaminho", "scroll"),

            "maravilhosos" => ContemTermo(tipoItem, "maravilhoso", "wondrous"),

            _ => tipoItem.Contains(filtro, StringComparison.OrdinalIgnoreCase)
        };
    }

    public static string ObterRotuloMeta(Item item)
    {
        var tipo = item.Tipo?.Trim();
        if (string.IsNullOrWhiteSpace(tipo))
            return "Item";

        return tipo.ToLowerInvariant() switch
        {
            "weapon" or "arma" or "armas" => "Arma",
            "armor" or "armadura" or "armaduras" => "Armadura",
            "adventuring gear" => "Equipamento de Aventura",
            "ammunition" => "Munição",
            "tools" or "tool" => "Ferramenta",
            "mounts and vehicles" => "Montaria e Veículo",
            "potion" or "poções" => "Poção",
            "ring" or "anéis" => "Anel",
            "staff" or "cajados" => "Cajado",
            "rod" or "bastões" => "Bastão",
            "wand" or "varinhas" => "Varinha",
            "scroll" or "pergaminhos" => "Pergaminho",
            "wondrous items" or "wondrous item" or "itens maravilhosos" => "Item Maravilhoso",
            _ => char.ToUpper(tipo[0]) + tipo[1..]
        };
    }

    public static string ObterRotuloCirculo(int circuloNivel) =>
        circuloNivel <= 0 ? "Truque" : $"{circuloNivel}º Círculo";

    public static string ObterRotuloMetaMagia(Magia magia)
    {
        var circulo = ObterRotuloCirculo(magia.CirculoNivel);
        return string.IsNullOrWhiteSpace(magia.Escola)
            ? circulo
            : $"{circulo} · {magia.Escola}";
    }

    private static bool ContemTermo(string texto, params string[] termos) =>
        termos.Any(t => texto.Contains(t, StringComparison.OrdinalIgnoreCase));
}