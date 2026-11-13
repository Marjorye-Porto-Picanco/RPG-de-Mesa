using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Presentation.Services;

namespace RPGdeMesa.Presentation.ViewModel;

public sealed record CategoriaItem(string Chave, string Nome);
public sealed record ExclusaoPendente(string Tipo, int Id, string Nome);

public class BibliotecaGeralViewModel
{
    private readonly ItemService _itemService;
    private readonly MonstroService _monstroService;
    private readonly NpcService _npcService;
    private readonly SessaoUsuarioService _sessaoUsuario;

    public BibliotecaGeralViewModel(
        ItemService itemService,
        MonstroService monstroService,
        NpcService npcService,
        SessaoUsuarioService sessaoUsuario)
    {
        _itemService = itemService;
        _monstroService = monstroService;
        _npcService = npcService;
        _sessaoUsuario = sessaoUsuario;
    }

    public int IdUsuarioLogado => _sessaoUsuario.IdUsuarioLogado;
    public bool EstaLogado => _sessaoUsuario.EstaLogado;

    public IReadOnlyList<CategoriaItem> Categorias { get; } =
    [
        new("itens", "Itens"),
        new("armas", "Armas"),
        new("magias", "Magias"),
        new("monstros", "Monstros"),
        new("npcs", "NPCs")
    ];

    public string CategoriaAtual { get; private set; } = "itens";
    public string TermoBusca { get; set; } = string.Empty;
    public int LimiteVisivel { get; private set; } = 30;
    public bool Carregando { get; private set; }
    public bool TemErro { get; private set; }
    public string StatusMensagem { get; private set; } = "Carregando biblioteca...";

    // Coleções carregadas
    public List<Item> EntradasItens { get; private set; } = new();
    public List<Monstro> EntradasMonstros { get; private set; } = new();
    public List<NPC> EntradasNpcs { get; private set; } = new();

    // Estado dos Modais
    public bool ExibirModalCriacao { get; private set; }
    public Item? ItemSelecionado { get; private set; }
    public bool ExibirModalItem { get; private set; }
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
        .Where(item => PertenceACategoria(item, CategoriaAtual))
        .Where(item => string.IsNullOrWhiteSpace(TermoBusca) ||
            item.Nome.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) ||
            item.Tipo?.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) == true ||
            item.Descricao?.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) == true);

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
            n.Ocupacao?.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) == true ||
            n.Historia?.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) == true);

    public bool PodeMostrarMais => CategoriaAtual switch
    {
        "monstros" => MonstrosFiltrados.Count() > LimiteVisivel,
        "npcs" => NpcsFiltrados.Count() > LimiteVisivel,
        _ => ItensFiltrados.Count() > LimiteVisivel
    };

    public async Task CarregarDadosAsync()
    {
        Carregando = true;
        TemErro = false;
        StatusMensagem = "Carregando registros do banco de dados...";
        LimiteVisivel = 30;

        try
        {
            if (!EstaLogado)
            {
                EntradasItens.Clear();
                EntradasMonstros.Clear();
                EntradasNpcs.Clear();
                StatusMensagem = "Faça login para visualizar a biblioteca.";
                return;
            }

            var itens = await _itemService.ObterItensPorUsuarioAsync(IdUsuarioLogado);
            var monstros = await _monstroService.ObterMonstrosPorUsuarioAsync(IdUsuarioLogado);
            var npcs = await _npcService.ObterNpcsPorUsuarioAsync(IdUsuarioLogado);

            EntradasItens = itens.OrderBy(i => i.Nome).ToList();
            EntradasMonstros = monstros.OrderBy(m => m.Nome).ToList();
            EntradasNpcs = npcs.OrderBy(n => n.Nome).ToList();

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
        CategoriaAtual = categoria;
        ResetarPaginacao();
        FecharTodosModaisDetalhes();
        AtualizarMensagemStatus();
    }

    public void AtualizarMensagemStatus()
    {
        StatusMensagem = CategoriaAtual switch
        {
            "monstros" => $"{EntradasMonstros.Count} monstro(s) cadastrado(s).",
            "npcs" => $"{EntradasNpcs.Count} NPC(s) cadastrado(s).",
            _ => $"{EntradasItens.Count} item(ns) cadastrado(s)."
        };
    }

    public void ResetarPaginacao() => LimiteVisivel = 30;
    public void MostrarMais() => LimiteVisivel += 30;

    // Ações de Modais
    public void AbrirModalCriacao() => ExibirModalCriacao = true;
    public void FecharModalCriacao() => ExibirModalCriacao = false;

    public void AbrirModalItem(Item item) { ItemSelecionado = item; ExibirModalItem = true; }
    public void FecharModalItem() { ExibirModalItem = false; ItemSelecionado = null; }

    public void AbrirModalMonstro(Monstro monstro) { MonstroSelecionado = monstro; ExibirModalMonstro = true; }
    public void FecharModalMonstro() { ExibirModalMonstro = false; MonstroSelecionado = null; }

    public void AbrirModalNpc(NPC npc) { NpcSelecionado = npc; ExibirModalNpc = true; }
    public void FecharModalNpc() { ExibirModalNpc = false; NpcSelecionado = null; }

    private void FecharTodosModaisDetalhes()
    {
        FecharModalItem();
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
            var resultado = ExclusaoAtual.Tipo switch
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
            await CarregarDadosAsync();
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

    // Regras Auxiliares de Exibição/Categoria
    public static bool PertenceACategoria(Item item, string categoria)
    {
        var tipo = item.Tipo ?? string.Empty;
        var descricao = item.Descricao ?? string.Empty;
        var ehArma = !string.IsNullOrWhiteSpace(item.Dano) || ContemTermo(tipo, "arma", "weapon", "armamento");
        var ehMagia = ContemTermo(tipo, "magia", "mágica", "spell") || ContemTermo(descricao, "magia", "mágica", "spell");

        return categoria switch
        {
            "armas" => ehArma,
            "magias" => ehMagia && !ehArma,
            _ => !ehArma && !ehMagia
        };
    }

    public static string ObterRotuloMeta(Item item) =>
        !string.IsNullOrWhiteSpace(item.Tipo) ? item.Tipo : "Item";

    private static bool ContemTermo(string texto, params string[] termos) =>
        termos.Any(t => texto.Contains(t, StringComparison.OrdinalIgnoreCase));
}