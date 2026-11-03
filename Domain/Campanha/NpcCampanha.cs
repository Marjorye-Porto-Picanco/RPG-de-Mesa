using RPGdeMesa.Domain.Npcs;

namespace RPGdeMesa.Domain.Campanhas;

public class NpcCampanha
{
    public int IdNpc { get; private set; }
    public int IdCampanha { get; private set; }

    public virtual NPC? Npc { get; private set; }
    public virtual Campanha? Campanha { get; private set; }

    public NpcCampanha() { }

    public NpcCampanha(int idCampanha, int idNpc)
    {
        IdCampanha = idCampanha;
        IdNpc = idNpc;
    }
}