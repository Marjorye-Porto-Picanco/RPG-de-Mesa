namespace RPGdeMesa.Domain.Usuarios;

public class Perfil
{
    public int IdPerfil { get; private set; }
    public int IdAccount { get; private set; }
    public string? NomeExibicao { get; private set; }
    public string? Biografia { get; private set; }
    public string? AvatarUrl { get; private set; }
    public DateTime DataCriacao { get; private set; }

    public virtual Usuario Usuario { get; private set; } = null!;

    protected Perfil() { }

    internal Perfil(Usuario usuario, string? biografia = null, string? avatarUrl = null, string? nomeExibicao = null)
    {
        Usuario = usuario;
        IdAccount = usuario.IdAccount;
        NomeExibicao = string.IsNullOrWhiteSpace(nomeExibicao) ? null : nomeExibicao.Trim();
        Biografia = string.IsNullOrWhiteSpace(biografia) ? null : biografia.Trim();
        AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl;
        DataCriacao = DateTime.Now;
    }

    internal void Atualizar(string? nomeExibicao, string? biografia, string? avatarUrl)
    {
        NomeExibicao = string.IsNullOrWhiteSpace(nomeExibicao) ? null : nomeExibicao.Trim();
        Biografia = string.IsNullOrWhiteSpace(biografia) ? null : biografia.Trim();
        AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl;
    }
}