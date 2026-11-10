using System.Text.Json;
using Microsoft.JSInterop;

namespace RPGdeMesa.Presentation.Services;

public sealed class SessaoCampanhaStorageService(IJSRuntime jsRuntime)
{
    private const string ChaveBase = "rpgdemesa-campanha-sessao";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SessaoCampanhaData> ObterAsync(int idCampanha)
    {
        var json = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", ObterChave(idCampanha));
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<SessaoCampanhaData>(json, JsonOptions) ?? new(); }
        catch (JsonException) { return new(); }
    }

    public async Task SalvarAsync(int idCampanha, SessaoCampanhaData dados)
    {
        dados.AtualizadaEm = DateTime.UtcNow;
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", ObterChave(idCampanha), JsonSerializer.Serialize(dados, JsonOptions));
    }

    private static string ObterChave(int idCampanha) => $"{ChaveBase}-{idCampanha}";
}

public sealed class SessaoCampanhaData
{
    public int MaxArmas { get; set; } = 3;
    public int MaxItens { get; set; } = 4;
    public List<int> JogadoresSelecionados { get; set; } = new();
    public DateTime? SessaoCriadaEm { get; set; }
    public bool CodigoGerado { get; set; }
    public DateTime AtualizadaEm { get; set; }
    public List<RecursoSessaoData> Recursos { get; set; } = new();
    public List<HistoricoSessaoData> Historico { get; set; } = new();
}

public sealed class RecursoSessaoData
{
    public string Id { get; set; } = string.Empty;
    public string Categoria { get; set; } = "monstros";
    public string Nome { get; set; } = string.Empty;
    public string? Tipo { get; set; }
    public int Quantidade { get; set; } = 1;
    public string? Detalhes { get; set; }
}

public sealed class HistoricoSessaoData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Nome { get; set; } = string.Empty;
    public DateTime Data { get; set; } = DateTime.Today;
    public string Status { get; set; } = "planejada";
    public string Resumo { get; set; } = string.Empty;
    public string? ProximosPassos { get; set; }
}
