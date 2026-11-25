using RPGdeMesa.Domain.Magias;

namespace RPGdeMesa.Domain.Npcs;

public class NpcMagia
{
    public int IdNpc { get; set; }
    public int IdMagia { get; set; }
    public virtual NPC Npc { get; set; } = null!;
    public virtual Magia Magia { get; set; } = null!;
}
