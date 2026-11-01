using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Usuarios;

namespace RPGdeMesa.Application.Services;

public class UsuarioService
{
    private readonly IUsuarioRepository _usuarioRepo;

    public UsuarioService(IUsuarioRepository usuarioRepo)
    {
        _usuarioRepo = usuarioRepo;
    }

    public async Task<(bool Sucesso, string Mensagem)> CadastrarAsync(string usuario, string email, string senha, string confirmarSenha, bool aceitouTermos)
    {
        if (!aceitouTermos)
            return (false, "Você precisa concordar com os termos de uso.");

        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
            return (false, "Por favor, preencha todos os campos obrigatórios.");

        if (!ValidarEmail(email))
            return (false, "Insira um e-mail válido.");

        if (senha != confirmarSenha)
            return (false, "A confirmação de senha não confere com a senha digitada.");

        if (!ValidarComplexidadeSenha(senha))
            return (false, "A senha deve ter pelo menos 8 caracteres e conter pelo menos uma letra maiúscula, uma minúscula, um número e um caractere especial.");

        if (!ValidarNomeUsuario(usuario))
            return (false, "O nome de utilizador deve ter entre 3 e 20 caracteres e conter apenas letras, números e sublinhado (_).");

        try
        {
            string usuarioLimpo = usuario.Trim();
            string emailLimpo = email.Trim().ToLower();

            if (await _usuarioRepo.ExisteUsernameAsync(usuarioLimpo))
                return (false, "Este nome de utilizador já está em uso.");

            if (await _usuarioRepo.ExisteEmailAsync(emailLimpo))
                return (false, "Este e-mail já está cadastrado.");

            var novoUsuario = new Usuario(usuarioLimpo, emailLimpo, GerarHashSHA256(senha));
            await _usuarioRepo.AdicionarAsync(novoUsuario);

            return (true, "Cadastro realizado com sucesso!");
        }
        catch (DomainException dex)
        {
            return (false, dex.Message);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERRO CADASTRO]: {ex.Message}");
            return (false, "Ocorreu um erro ao salvar o cadastro. Tente novamente.");
        }
    }

    public async Task<(bool Sucesso, string Mensagem, Usuario? UsuarioLogado)> AutenticarAsync(string loginOuEmail, string senha)
    {
        if (string.IsNullOrWhiteSpace(loginOuEmail) && string.IsNullOrWhiteSpace(senha))
            return (false, "Por favor, preencha o usuário/e-mail e a senha.", null);

        if (string.IsNullOrWhiteSpace(loginOuEmail))
            return (false, "Por favor, informe o seu usuário ou e-mail.", null);

        if (string.IsNullOrWhiteSpace(senha))
            return (false, "Por favor, informe a sua senha.", null);

        try
        {
            var usuario = await _usuarioRepo.ObterPorLoginOuEmailAsync(loginOuEmail);

            if (usuario == null)
                return (false, "Utilizador ou e-mail não encontrado.", null);

            string senhaHash = GerarHashSHA256(senha);
            if (usuario.Password != senhaHash)
                return (false, "Senha incorreta. Tente novamente.", null);

            if (usuario.StatusConta != "ativo")
                return (false, "Sua conta está inativa ou bloqueada.", null);

            return (true, "Login realizado com sucesso!", usuario);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERRO BANCO]: {ex.Message}");
            return (false, "Erro ao conectar ao banco de dados. Verifique o SQL Server.", null);
        }
    }

    public async Task<(bool Sucesso, string Mensagem, Usuario? UsuarioLogado)> AutenticarOuCadastrarSocialAsync(string email, string nomeSocial)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (false, "E-mail não fornecido pelo provedor social.", null);

        try
        {
            string emailLimpo = email.Trim().ToLower();
            var usuario = await _usuarioRepo.ObterPorEmailAsync(emailLimpo);

            if (usuario != null)
            {
                if (usuario.StatusConta != "ativo")
                    return (false, "Sua conta está inativa ou bloqueada.", null);

                return (true, "Login social realizado com sucesso!", usuario);
            }

            string usernameFinal = await GerarUsernameSocialUnicoAsync(nomeSocial);
            var novoUsuario = new Usuario(usernameFinal, emailLimpo, GerarHashSHA256(Guid.NewGuid().ToString()));

            await _usuarioRepo.AdicionarAsync(novoUsuario);
            return (true, "Conta criada via rede social com sucesso!", novoUsuario);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERRO SOCIAL]: {ex.Message}");
            return (false, "Ocorreu um erro ao processar o login social.", null);
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> CadastrarSocialAsync(string email, string nomeSocial)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (false, "E-mail não fornecido pelo provedor social.");

        try
        {
            string emailLimpo = email.Trim().ToLower();
            if (await _usuarioRepo.ExisteEmailAsync(emailLimpo))
                return (false, "Este e-mail do Google já possui uma conta cadastrada.");

            string usernameFinal = await GerarUsernameSocialUnicoAsync(nomeSocial);
            var novoUsuario = new Usuario(usernameFinal, emailLimpo, GerarHashSHA256(Guid.NewGuid().ToString()));

            await _usuarioRepo.AdicionarAsync(novoUsuario);
            return (true, "Conta criada via rede social com sucesso!");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERRO SOCIAL]: {ex.Message}");
            return (false, "Ocorreu um erro ao processar o registro social.");
        }
    }

    private async Task<string> GerarUsernameSocialUnicoAsync(string? nomeSocial)
    {
        string usernameBase = Regex.Replace(nomeSocial ?? "aventureiro", @"[^a-zA-Z0-9_]", "").ToLower();
        if (usernameBase.Length < 3) usernameBase = "user_social";
        if (usernameBase.Length > 15) usernameBase = usernameBase.Substring(0, 15);
        string usernameFinal = usernameBase;
        int contador = 1;

        while (await _usuarioRepo.ExisteUsernameAsync(usernameFinal))
        {
            usernameFinal = $"{usernameBase}{contador}";
            contador++;
        }
        return usernameFinal;
    }

    public string GerarHashSHA256(string texto)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(texto));
        return Convert.ToHexString(bytes);
    }

    private bool ValidarEmail(string email) =>
        !string.IsNullOrWhiteSpace(email) && Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);

    private bool ValidarComplexidadeSenha(string senha) =>
        Regex.IsMatch(senha, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$");

    private bool ValidarNomeUsuario(string usuario) =>
        !string.IsNullOrWhiteSpace(usuario) && Regex.IsMatch(usuario, @"^[a-zA-Z0-9_]{3,20}$");
}