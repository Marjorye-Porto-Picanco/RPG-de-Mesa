using RPGdeMesa.Domain.Bestiario;

namespace RPGdeMesa.Domain.Campanhas;

public class MonstroCampanha
{
    public int IdCampanha { get; private set; }
    public int IdMonstro { get; private set; }

    public virtual Campanha? Campanha { get; private set; }
    public virtual Monstro? Monstro { get; private set; }

    protected MonstroCampanha() { }

    internal MonstroCampanha(int idCampanha, int idMonstro)
    {
        IdCampanha = idCampanha;
        IdMonstro = idMonstro;
    }
}