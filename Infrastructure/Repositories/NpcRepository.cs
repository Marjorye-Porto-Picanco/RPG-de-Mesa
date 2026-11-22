using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Infrastructure.Persistence;

namespace RPGdeMesa.Infrastructure.Repositories;

public class NpcRepository : INpcRepository
{
    private readonly AppDbContext _contexto;
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    public NpcRepository(AppDbContext contexto) => _contexto = contexto;

    private async Task<T> ExecutarComLockAsync<T>(Func<Task<T>> operacao)
    {
        await _dbLock.WaitAsync();
        try
        {
            return await operacao();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private async Task ExecutarComLockAsync(Func<Task> operacao)
    {
        await _dbLock.WaitAsync();
        try
        {
            await operacao();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public Task<NPC?> ObterPorIdAsync(int idNpc) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            return await _contexto.Npcs
                .Include(n => n.NpcCampanhas)
                .FirstOrDefaultAsync(n => n.IdNpc == idNpc);
        });

    public Task<List<NPC>> ListarPorUsuarioAsync(int idAccount) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            return await _contexto.Npcs
                .Include(n => n.NpcCampanhas)
                .Where(n => n.IdAccount == idAccount)
                .ToListAsync();
        });

    public Task<List<NPC>> ListarPorCampanhaAsync(int idCampanha) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            return await _contexto.Npcs
                .Include(n => n.NpcCampanhas)
                .Where(n => n.NpcCampanhas.Any(nc => nc.IdCampanha == idCampanha))
                .ToListAsync();
        });

    public Task AdicionarAsync(NPC npc) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.Npcs.Add(npc);
            await _contexto.SaveChangesAsync();
        });

    public Task AtualizarAsync(NPC npc) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.Npcs.Update(npc);
            await _contexto.SaveChangesAsync();
        });

    public Task RemoverAsync(NPC npc) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.Npcs.Remove(npc);
            await _contexto.SaveChangesAsync();
        });
}