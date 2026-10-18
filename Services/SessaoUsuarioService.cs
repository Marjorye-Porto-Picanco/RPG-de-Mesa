namespace RPGdeMesa.Services
{
    public class SessaoUsuarioService
    {
        public int IdUsuarioLogado { get; private set; } = 0;
        public string NomeUsuario { get; private set; } = string.Empty;
        public string EmailUsuario { get; private set; } = string.Empty;
        public bool EstaLogado => IdUsuarioLogado > 0;

        public void FazerLogin(int id, string nome, string email)
        {
            IdUsuarioLogado = id;
            NomeUsuario = nome;
            EmailUsuario = email;
        }

        public void FazerLogout()
        {
            IdUsuarioLogado = 0;
            NomeUsuario = string.Empty;
            EmailUsuario = string.Empty;
        }
    }
}