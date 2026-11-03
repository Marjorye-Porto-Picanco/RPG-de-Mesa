using RPGdeMesa.Domain.Personagens;

namespace RPGdeMesa.Domain.Campanhas;

public class PersonagemCampanha
{
    public int IdPersonagem { get; private set; }
    public int IdCampanha { get; private set; }

    public virtual Personagem? Personagem { get; private set; }
    public virtual Campanha Campanha { get; private set; } = null!;

    protected PersonagemCampanha() { }

    internal PersonagemCampanha(int idPersonagem, int idCampanha)
    {
        IdPersonagem = idPersonagem;
        IdCampanha = idCampanha;
    }
}