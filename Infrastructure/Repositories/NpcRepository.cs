using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Infrastructure.Persistence;

namespace RPGdeMesa.Infrastructure.Repositories;

public class NpcRepository : INpcRepository
{
    private readonly AppDbContext _contexto;

    public NpcRepository(AppDbContext contexto) => _contexto = contexto;

    public async Task<NPC?> ObterPorIdAsync(int idNpc)
    {
        _contexto.ChangeTracker.Clear();
        return await _contexto.Npcs.FirstOrDefaultAsync(n => n.IdNpc == idNpc);
    }

    public async Task<List<NPC>> ListarPorUsuarioAsync(int idAccount)
    {
        _contexto.ChangeTracker.Clear();
        return await _contexto.Npcs
            .Where(n => n.IdAccount == idAccount || (n.Campanha != null && n.Campanha.IdAccount == idAccount))
            .ToListAsync();
    }

    public async Task<List<NPC>> ListarPorCampanhaAsync(int idCampanha)
    {
        _contexto.ChangeTracker.Clear();
        return await _contexto.Npcs
            .Where(n => n.IdCampanha == idCampanha)
            .ToListAsync();
    }

    public async Task AdicionarAsync(NPC npc)
    {
        _contexto.Npcs.Add(npc);
        await _contexto.SaveChangesAsync();
    }

    public async Task AtualizarAsync(NPC npc)
    {
        _contexto.Npcs.Update(npc);
        await _contexto.SaveChangesAsync();
    }

    public async Task RemoverAsync(NPC npc)
    {
        _contexto.Npcs.Remove(npc);
        await _contexto.SaveChangesAsync();
    }
}