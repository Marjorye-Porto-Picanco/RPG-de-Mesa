using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Infrastructure.Persistence;

namespace RPGdeMesa.Infrastructure.Repositories;

public class NpcRepository : INpcRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public NpcRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<NPC?> ObterPorIdAsync(int idNpc)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Npcs
            .AsNoTracking()
            .Include(n => n.NpcCampanhas)
            .Include(n => n.Magias).ThenInclude(m => m.Magia)
            .Include(n => n.Itens).ThenInclude(i => i.Item)
            // .Include(n => n.Ataques)
            .FirstOrDefaultAsync(n => n.IdNpc == idNpc);
    }

    public async Task<List<NPC>> ListarPorUsuarioAsync(int idAccount)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Npcs
            .AsNoTracking()
            .Include(n => n.NpcCampanhas)
            .Include(n => n.Magias).ThenInclude(m => m.Magia)
            .Include(n => n.Itens).ThenInclude(i => i.Item)
            // .Include(n => n.Ataques)
            .Where(n => n.IdAccount == idAccount)
            .ToListAsync();
    }

    public async Task<List<NPC>> ListarPorCampanhaAsync(int idCampanha)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Npcs
            .AsNoTracking()
            .Include(n => n.NpcCampanhas)
            .Include(n => n.Magias).ThenInclude(m => m.Magia)
            .Include(n => n.Itens).ThenInclude(i => i.Item)
            // .Include(n => n.Ataques)
            .Where(n => n.NpcCampanhas.Any(nc => nc.IdCampanha == idCampanha))
            .ToListAsync();
    }

    public async Task AdicionarAsync(NPC npc)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        contexto.Npcs.Add(npc);
        await contexto.SaveChangesAsync();
    }

    public async Task AtualizarAsync(NPC npc)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        contexto.Npcs.Update(npc);
        await contexto.SaveChangesAsync();
    }

    public async Task RemoverAsync(NPC npc)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        contexto.Npcs.Remove(npc);
        await contexto.SaveChangesAsync();
    }
}
