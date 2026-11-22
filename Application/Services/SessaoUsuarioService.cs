using Microsoft.Maui.Storage;

namespace RPGdeMesa.Application.Services;

public class SessaoUsuarioService
{
    private const string ChaveToken = "rpgdemesa_auth_token";
    private const string ChaveUsuarioId = "rpgdemesa_auth_uid";
    private const string ChaveUsuarioNome = "rpgdemesa_auth_uname";
    private const string ChaveUsuarioEmail = "rpgdemesa_auth_uemail";
    private const string ChaveUltimoAcessoUtc = "rpgdemesa_auth_last_utc";
    private static readonly TimeSpan ValidadeToken = TimeSpan.FromDays(30);

    public int IdUsuarioLogado { get; private set; } = 0;
    public string NomeUsuario { get; private set; } = string.Empty;
    public string EmailUsuario { get; private set; } = string.Empty;
    public bool EstaLogado => IdUsuarioLogado > 0;

    public string? AvatarUrl { get; private set; }
    public bool AvatarCarregado { get; private set; }

    public event Action? OnChange;

    public void FazerLogin(int id, string nome, string email, bool persistirSessao = true)
    {
        IdUsuarioLogado = id;
        NomeUsuario = nome;
        EmailUsuario = email;
        AvatarUrl = null;
        AvatarCarregado = false;

        if (persistirSessao)
            SalvarTokenSessao(id, nome, email);

        OnChange?.Invoke();
    }

    public void FazerLogout()
    {
        IdUsuarioLogado = 0;
        NomeUsuario = string.Empty;
        EmailUsuario = string.Empty;
        AvatarUrl = null;
        AvatarCarregado = false;

        LimparTokenSessao();
        OnChange?.Invoke();
    }

    public void AtualizarNomeUsuario(string novoNome)
    {
        NomeUsuario = novoNome;
        try
        {
            if (IdUsuarioLogado > 0)
                Preferences.Default.Set(ChaveUsuarioNome, novoNome);
        }
        catch { }

        OnChange?.Invoke();
    }

    public void AtualizarAvatar(string? avatarUrl)
    {
        AvatarUrl = avatarUrl;
        AvatarCarregado = true;
        OnChange?.Invoke();
    }

    public void SalvarTokenSessao(int idAccount, string nome, string email)
    {
        try
        {
            Preferences.Default.Set(ChaveToken, Guid.NewGuid().ToString("N"));
            Preferences.Default.Set(ChaveUsuarioId, idAccount);
            Preferences.Default.Set(ChaveUsuarioNome, nome);
            Preferences.Default.Set(ChaveUsuarioEmail, email);
            Preferences.Default.Set(ChaveUltimoAcessoUtc, DateTime.UtcNow.Ticks);
        }
        catch { }
    }

    /// <summary>
    /// Executa de forma síncrona na abertura do app: se houver token válido dentro de 30 dias,
    /// restaura a sessão imediatamente e reseta o timer para mais 30 dias.
    /// </summary>
    public bool TentarRestaurarSessaoLocal()
    {
        if (EstaLogado)
        {
            RenovarTokenSessao();
            return true;
        }

        try
        {
            string token = Preferences.Default.Get(ChaveToken, string.Empty);
            int idAccount = Preferences.Default.Get(ChaveUsuarioId, 0);
            string nome = Preferences.Default.Get(ChaveUsuarioNome, string.Empty);
            string email = Preferences.Default.Get(ChaveUsuarioEmail, string.Empty);
            long ultimoAcessoTicks = Preferences.Default.Get(ChaveUltimoAcessoUtc, 0L);

            if (string.IsNullOrWhiteSpace(token) || idAccount <= 0 || ultimoAcessoTicks <= 0)
                return false;

            var ultimoAcessoUtc = new DateTime(ultimoAcessoTicks, DateTimeKind.Utc);
            if ((DateTime.UtcNow - ultimoAcessoUtc) > ValidadeToken)
            {
                LimparTokenSessao();
                return false;
            }

            IdUsuarioLogado = idAccount;
            NomeUsuario = nome;
            EmailUsuario = email;
            AvatarUrl = null;
            AvatarCarregado = false;

            RenovarTokenSessao();
            OnChange?.Invoke();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void RenovarTokenSessao()
    {
        try
        {
            if (IdUsuarioLogado > 0)
                Preferences.Default.Set(ChaveUltimoAcessoUtc, DateTime.UtcNow.Ticks);
        }
        catch { }
    }

    public void LimparTokenSessao()
    {
        try
        {
            Preferences.Default.Remove(ChaveToken);
            Preferences.Default.Remove(ChaveUsuarioId);
            Preferences.Default.Remove(ChaveUsuarioNome);
            Preferences.Default.Remove(ChaveUsuarioEmail);
            Preferences.Default.Remove(ChaveUltimoAcessoUtc);
        }
        catch { }
    }
}