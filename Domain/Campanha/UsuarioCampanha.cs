using RPGdeMesa.Domain.Usuarios;

namespace RPGdeMesa.Domain.Campanhas;

public class UsuarioCampanha
{
    public int IdAccount { get; private set; }
    public int IdCampanha { get; private set; }

    public virtual Usuario Usuario { get; private set; } = null!;
    public virtual Campanha Campanha { get; private set; } = null!;

    protected UsuarioCampanha() { }

    public UsuarioCampanha(int idAccount, int idCampanha)
    {
        IdAccount = idAccount;
        IdCampanha = idCampanha;
    }
}