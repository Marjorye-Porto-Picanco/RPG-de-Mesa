using RPGdeMesa.Domain.Itens;

namespace RPGdeMesa.Domain.Npcs;

public class NpcItem
{
    public int IdNpc { get; set; }
    public int IdItem { get; set; }
    public int Quantidade { get; set; } = 1;
    public virtual NPC Npc { get; set; } = null!;
    public virtual Item Item { get; set; } = null!;
}
