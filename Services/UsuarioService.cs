using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Data;
using RPGdeMesa.Models;

namespace RPGdeMesa.Services
{
    public class UsuarioService
    {
        private readonly AppDbContext _context;

        public UsuarioService(AppDbContext context)
        {
            _context = context;
        }

        // METODO PARA A TELA DE CADASTRO
        public async Task<(bool Sucesso, string Mensagem)> CadastrarAsync( string usuario, string email, string senha, string confirmarSenha, bool aceitouTermos)
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

            //Formato do Nome de Usuário (Apenas letras, números e _ sem espaços)
            if (!ValidarNomeUsuario(usuario))
                return (false, "O nome de utilizador deve ter entre 3 e 20 caracteres e conter apenas letras, números e sublinhado (_).");
            try
            {
                string usuarioLimpo = usuario.Trim();
                string emailLimpo = email.Trim().ToLower();

                bool usuarioExiste = await _context.Usuarios
                    .AsNoTracking()
                    .AnyAsync(u => u.UsarName.ToLower() == usuarioLimpo.ToLower());

                if (usuarioExiste)
                    return (false, "Este nome de utilizador já está em uso.");

                bool emailExiste = await _context.Usuarios
                    .AsNoTracking()
                    .AnyAsync(u => u.EMail.ToLower() == emailLimpo);

                if (emailExiste)
                    return (false, "Este e-mail já está cadastrado.");

                var novoUsuario = new Usuario
                {
                    UsarName = usuarioLimpo,
                    EMail = emailLimpo,
                    Password = GerarHashSHA256(senha),
                    DataCadastro = DateTime.Now,
                    StatusConta = "ativo",
                    Perfil = new Perfil
                    {
                        DataCriacao = DateTime.Now
                    }
                };

                _context.Usuarios.Add(novoUsuario);
                await _context.SaveChangesAsync();
                return (true, "Cadastro realizado com sucesso!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO CADASTRO]: {ex.Message}");
                return (false, "Ocorreu um erro ao salvar o cadastro. Tente novamente.");
            }
        }



        // METODO PARA A TELA DE LOGIN
        public async Task<(bool Sucesso, string Mensagem, Usuario? UsuarioLogado)> AutenticarAsync(string loginOuEmail, string senha)
        {
            // 1. Falta de preenchimento dos campos
            if (string.IsNullOrWhiteSpace(loginOuEmail) && string.IsNullOrWhiteSpace(senha))
                return (false, "Por favor, preencha o usuário/e-mail e a senha.", null);

            if (string.IsNullOrWhiteSpace(loginOuEmail))
                return (false, "Por favor, informe o seu usuário ou e-mail.", null);

            if (string.IsNullOrWhiteSpace(senha))
                return (false, "Por favor, informe a sua senha.", null);

            try
            {
                string loginLimpo = loginOuEmail.Trim().ToLower();

                // 2. Busca o usuário apenas pelo Login ou E-mail primeiro
                var usuario = await _context.Usuarios
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UsarName.ToLower() == loginLimpo || u.EMail.ToLower() == loginLimpo);

                // Se não encontrar o usuário/e-mail cadastrado
                if (usuario == null)
                    return (false, "Utilizador ou e-mail não encontrado.", null);

                // 3. Compara o Hash da senha informada com a senha salva no banco
                string senhaHash = GerarHashSHA256(senha);
                if (usuario.Password != senhaHash)
                    return (false, "Senha incorreta. Tente novamente.", null);

                // 4. Verificação de status da conta
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



        // METODO PARA AUTENTICAÇÃO VIA REDES SOCIAIS (GOOGLE, FACEBOOK, ETC)
        public async Task<(bool Sucesso, string Mensagem, Usuario? UsuarioLogado)> AutenticarOuCadastrarSocialAsync(string email, string nomeSocial)
        {
            if (string.IsNullOrWhiteSpace(email))
                return (false, "E-mail não fornecido pelo provedor social.", null);

            try
            {
                string emailLimpo = email.Trim().ToLower();

                var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.EMail.ToLower() == emailLimpo);

                if (usuario != null)
                {
                    if (usuario.StatusConta != "ativo")
                        return (false, "Sua conta está inativa ou bloqueada.", null);

                    return (true, "Login social realizado com sucesso!", usuario);
                }

                string usernameBase = Regex.Replace(nomeSocial ?? "aventureiro", @"[^a-zA-Z0-9_]", "").ToLower();
                if (usernameBase.Length < 3) usernameBase = "user_social";
                if (usernameBase.Length > 15) usernameBase = usernameBase.Substring(0, 15);
                string usernameFinal = usernameBase;
                int contador = 1;

                while (await _context.Usuarios.AnyAsync(u => u.UsarName.ToLower() == usernameFinal))
                {
                    usernameFinal = $"{usernameBase}{contador}";
                    contador++;
                }

                var novoUsuario = new Usuario
                {
                    UsarName = usernameFinal,
                    EMail = emailLimpo,
                    Password = GerarHashSHA256(Guid.NewGuid().ToString()),
                    DataCadastro = DateTime.Now,
                    StatusConta = "ativo",
                    Perfil = new Perfil
                    {
                        DataCriacao = DateTime.Now
                    }
                };

                _context.Usuarios.Add(novoUsuario);
                await _context.SaveChangesAsync();

                return (true, "Conta criada via rede social com sucesso!", novoUsuario);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO SOCIAL]: {ex.Message}");
                return (false, "Ocorreu um erro ao processar o login social.", null);
            }
        }

        // METODO EXCLUSIVO PARA O CADASTRO SOCIAL
        public async Task<(bool Sucesso, string Mensagem, Usuario? UsuarioLogado)> CadastrarSocialAsync(string email, string nomeSocial)
        {
            if (string.IsNullOrWhiteSpace(email))
                return (false, "E-mail não fornecido pelo provedor social.", null);

            try
            {
                string emailLimpo = email.Trim().ToLower();
                bool emailExiste = await _context.Usuarios.AnyAsync(u => u.EMail.ToLower() == emailLimpo);

                if (emailExiste)
                    return (false, "Este e-mail já está cadastrado. Faça login na sua conta.", null);

                // Se não existe, cria a nova conta
                string usernameBase = Regex.Replace(nomeSocial ?? "aventureiro", @"[^a-zA-Z0-9_]", "").ToLower();
                if (usernameBase.Length < 3) usernameBase = "user_social";
                if (usernameBase.Length > 15) usernameBase = usernameBase.Substring(0, 15);
                string usernameFinal = usernameBase;
                int contador = 1;

                while (await _context.Usuarios.AnyAsync(u => u.UsarName.ToLower() == usernameFinal))
                {
                    usernameFinal = $"{usernameBase}{contador}";
                    contador++;
                }

                var novoUsuario = new Usuario
                {
                    UsarName = usernameFinal,
                    EMail = emailLimpo,
                    Password = GerarHashSHA256(Guid.NewGuid().ToString()),
                    DataCadastro = DateTime.Now,
                    StatusConta = "ativo",
                    Perfil = new Perfil
                    {
                        DataCriacao = DateTime.Now
                    }
                };

                _context.Usuarios.Add(novoUsuario);
                await _context.SaveChangesAsync();

                return (true, "Conta cadastrada com sucesso!", novoUsuario);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO CADASTRO SOCIAL]: {ex.Message}");
                return (false, "Ocorreu um erro ao processar o cadastro social.", null);
            }
        }




        // METODOS AUXILIARES
        public string GerarHashSHA256(string texto)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(texto));
            return Convert.ToHexString(bytes);
        }

        private bool ValidarEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase);
        }
        private bool ValidarComplexidadeSenha(string senha)
        {
            // Padrão: Pelo menos 1 letra maiúscula, 1 minúscula, 1 número e 1 caractere especial
            string pattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$";
            return Regex.IsMatch(senha, pattern);
        }
        private bool ValidarNomeUsuario(string usuario)
        {
            if (string.IsNullOrWhiteSpace(usuario))
                return false;

            // Entre 3 e 20 caracteres, permitindo apenas letras, números e sublinhado (_)
            string pattern = @"^[a-zA-Z0-9_]{3,20}$";
            return Regex.IsMatch(usuario, pattern);
        }
    }
}