using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Infrastructure.Persistence;

namespace RPGdeMesa.Infrastructure.Repositories;

public class MonstroRepository : IMonstroRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public MonstroRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<Monstro?> ObterPorIdAsync(int idMonstro)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Monstros
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.IdMonstro == idMonstro);
    }

    public async Task<List<Monstro>> ListarPorUsuarioAsync(int idAccount)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Monstros
            .AsNoTracking()
            // Lista monstros públicos e monstros pertencentes à conta logada.
            .Where(m => m.IdAccount == null || m.IdAccount == idAccount)
            .OrderByDescending(m => m.IdMonstro)
            .ToListAsync();
    }

    public async Task AdicionarAsync(Monstro monstro)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        contexto.Monstros.Add(monstro);
        await contexto.SaveChangesAsync();
    }

    public async Task AtualizarAsync(Monstro monstro)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        contexto.Monstros.Update(monstro);
        await contexto.SaveChangesAsync();
    }

    public async Task RemoverAsync(Monstro monstro)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        contexto.Monstros.Remove(monstro);
        await contexto.SaveChangesAsync();
    }
}