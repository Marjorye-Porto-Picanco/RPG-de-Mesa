namespace RPGdeMesa.Domain.Usuarios;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorIdAsync(int idAccount);
    Task<Usuario?> ObterPorEmailAsync(string email);
    Task<Usuario?> ObterPorLoginOuEmailAsync(string loginOuEmail);
    Task<bool> ExisteUsernameAsync(string username);
    Task<bool> ExisteEmailAsync(string email);
    Task AdicionarAsync(Usuario usuario);
    Task AtualizarAsync(Usuario usuario);
}