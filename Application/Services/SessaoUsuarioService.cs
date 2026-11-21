namespace RPGdeMesa.Application.Services;

public class SessaoUsuarioService
{
    public int IdUsuarioLogado { get; private set; } = 0;
    public string NomeUsuario { get; private set; } = string.Empty;
    public string EmailUsuario { get; private set; } = string.Empty;
    public bool EstaLogado => IdUsuarioLogado > 0;

    public string? AvatarUrl { get; private set; }
    public bool AvatarCarregado { get; private set; }

    public event Action? OnChange;

    public void FazerLogin(int id, string nome, string email)
    {
        IdUsuarioLogado = id;
        NomeUsuario = nome;
        EmailUsuario = email;
        AvatarUrl = null;
        AvatarCarregado = false;
        OnChange?.Invoke();
    }

    public void FazerLogout()
    {
        IdUsuarioLogado = 0;
        NomeUsuario = string.Empty;
        EmailUsuario = string.Empty;
        AvatarUrl = null;
        AvatarCarregado = false;
        OnChange?.Invoke();
    }

    public void AtualizarNomeUsuario(string novoNome)
    {
        NomeUsuario = novoNome;
        OnChange?.Invoke();
    }

    public void AtualizarAvatar(string? avatarUrl)
    {
        AvatarUrl = avatarUrl;
        AvatarCarregado = true;
        OnChange?.Invoke();
    }
}