using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Infrastructure.Persistence;

namespace RPGdeMesa.Infrastructure.Repositories;

public class MonstroRepository : IMonstroRepository
{
    private readonly AppDbContext _contexto;

    public MonstroRepository(AppDbContext contexto) => _contexto = contexto;

    public async Task<Monstro?> ObterPorIdAsync(int idMonstro)
    {
        _contexto.ChangeTracker.Clear();
        return await _contexto.Monstros.FirstOrDefaultAsync(m => m.IdMonstro == idMonstro);
    }

    public async Task<List<Monstro>> ListarPorUsuarioAsync(int idAccount)
    {
        _contexto.ChangeTracker.Clear();
        return await _contexto.Monstros
            .Where(m => m.IdAccount == idAccount)
            .OrderByDescending(m => m.IdMonstro)
            .ToListAsync();
    }

    public async Task AdicionarAsync(Monstro monstro)
    {
        _contexto.Monstros.Add(monstro);
        await _contexto.SaveChangesAsync();
    }

    public async Task AtualizarAsync(Monstro monstro)
    {
        _contexto.Monstros.Update(monstro);
        await _contexto.SaveChangesAsync();
    }

    public async Task RemoverAsync(Monstro monstro)
    {
        _contexto.Monstros.Remove(monstro);
        await _contexto.SaveChangesAsync();
    }
}