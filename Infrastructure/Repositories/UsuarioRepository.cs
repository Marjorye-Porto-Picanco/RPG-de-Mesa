using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Usuarios;
using RPGdeMesa.Infrastructure.Persistence;

namespace RPGdeMesa.Infrastructure.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public UsuarioRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<Usuario?> ObterPorIdAsync(int idAccount)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Usuarios
            .AsNoTracking()
            .Include(u => u.Perfil)
            .FirstOrDefaultAsync(u => u.IdAccount == idAccount);
    }

    public async Task<Usuario?> ObterPorEmailAsync(string email)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Usuarios
            .AsNoTracking()
            .Include(u => u.Perfil)
            .FirstOrDefaultAsync(u => u.EMail.ToLower() == email.ToLower());
    }

    public async Task<Usuario?> ObterPorLoginOuEmailAsync(string loginOuEmail)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        string limpo = loginOuEmail.Trim().ToLower();
        return await contexto.Usuarios
            .AsNoTracking()
            .Include(u => u.Perfil)
            .FirstOrDefaultAsync(u => u.UsarName.ToLower() == limpo || u.EMail.ToLower() == limpo);
    }

    public async Task<bool> ExisteUsernameAsync(string username)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Usuarios.AsNoTracking().AnyAsync(u => u.UsarName.ToLower() == username.ToLower());
    }

    public async Task<bool> ExisteEmailAsync(string email)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Usuarios.AsNoTracking().AnyAsync(u => u.EMail.ToLower() == email.ToLower());
    }

    public async Task AdicionarAsync(Usuario usuario)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        contexto.Usuarios.Add(usuario);
        await contexto.SaveChangesAsync();
    }

    public async Task AtualizarAsync(Usuario usuario)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        contexto.Usuarios.Update(usuario);
        await contexto.SaveChangesAsync();
    }
}