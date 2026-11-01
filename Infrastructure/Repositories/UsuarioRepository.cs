using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Usuarios;
using RPGdeMesa.Infrastructure.Persistence;

namespace RPGdeMesa.Infrastructure.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _context;

    public UsuarioRepository(AppDbContext context) => _context = context;

    public async Task<Usuario?> ObterPorIdAsync(int idAccount) =>
        await _context.Usuarios.Include(u => u.Perfil).FirstOrDefaultAsync(u => u.IdAccount == idAccount);

    public async Task<Usuario?> ObterPorEmailAsync(string email) =>
        await _context.Usuarios.FirstOrDefaultAsync(u => u.EMail.ToLower() == email.ToLower());

    public async Task<Usuario?> ObterPorLoginOuEmailAsync(string loginOuEmail)
    {
        string limpo = loginOuEmail.Trim().ToLower();
        return await _context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UsarName.ToLower() == limpo || u.EMail.ToLower() == limpo);
    }

    public async Task<bool> ExisteUsernameAsync(string username) =>
        await _context.Usuarios.AsNoTracking().AnyAsync(u => u.UsarName.ToLower() == username.ToLower());

    public async Task<bool> ExisteEmailAsync(string email) =>
        await _context.Usuarios.AsNoTracking().AnyAsync(u => u.EMail.ToLower() == email.ToLower());

    public async Task AdicionarAsync(Usuario usuario)
    {
        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();
    }

    public async Task AtualizarAsync(Usuario usuario)
    {
        _context.Usuarios.Update(usuario);
        await _context.SaveChangesAsync();
    }
}