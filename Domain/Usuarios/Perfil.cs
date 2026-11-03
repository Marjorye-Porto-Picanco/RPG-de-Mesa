namespace RPGdeMesa.Domain.Usuarios;

public class Perfil
{
    public int IdPerfil { get; private set; }
    public int IdAccount { get; private set; }
    public string? Biografia { get; private set; }
    public string? AvatarUrl { get; private set; }
    public DateTime DataCriacao { get; private set; }

    public virtual Usuario Usuario { get; private set; } = null!;

    protected Perfil() { }

    internal Perfil(Usuario usuario, string? biografia = null, string? avatarUrl = null)
    {
        Usuario = usuario;
        Biografia = biografia;
        AvatarUrl = avatarUrl;
        DataCriacao = DateTime.Now;
    }

    internal void Atualizar(string? biografia, string? avatarUrl)
    {
        Biografia = biografia;
        AvatarUrl = avatarUrl;
    }
}