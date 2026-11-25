namespace RPGdeMesa.Domain.Bestiario;

public class MonstroAtaque
{
    public int IdMonstroAtaque { get; set; }
    public int IdMonstro { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int? BonusAtaque { get; set; }
    public string? Dano { get; set; }
    public string? TipoDano { get; set; }
    public string? Alcance { get; set; }
    public string? Descricao { get; set; }
    public virtual Monstro? Monstro { get; set; }
}
