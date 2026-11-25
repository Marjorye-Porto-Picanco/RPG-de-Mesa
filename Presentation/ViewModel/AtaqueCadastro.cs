namespace RPGdeMesa.Presentation.ViewModel;

public sealed class AtaqueCadastro
{
    public string Nome { get; set; } = "Ataque";
    public int? BonusAtaque { get; set; }
    public string? Dano { get; set; }
    public string? TipoDano { get; set; }
    public string? Alcance { get; set; }
    public string? Descricao { get; set; }
    internal int? IdCatalogo { get; init; }
}

public sealed record AtaqueCatalogoCadastro(
    int Id, string Origem, string Nome, int? BonusAtaque, string? Dano,
    string? TipoDano, string? Alcance, string? Descricao);
