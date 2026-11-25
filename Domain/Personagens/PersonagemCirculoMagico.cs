namespace RPGdeMesa.Domain.Personagens;

public class PersonagemCirculoMagico
{
    public int IdPersonagem { get; set; }
    public int CirculoNivel { get; set; }
    public int TotalSlots { get; set; }
    public int SlotsUsados { get; set; }
    public int? TotalSlotsPendente { get; set; }
    public virtual Personagem Personagem { get; set; } = null!;

    public int SlotsRestantes => Math.Max(0, TotalSlots - SlotsUsados);
    public void AplicarDescanso() { if (TotalSlotsPendente.HasValue) TotalSlots = Math.Max(0, TotalSlotsPendente.Value); TotalSlotsPendente = null; SlotsUsados = 0; }
}
