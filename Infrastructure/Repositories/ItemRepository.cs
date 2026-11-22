using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Infrastructure.Persistence;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RPGdeMesa.Infrastructure.Repositories;

public class ItemRepository : IItemRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ItemRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IEnumerable<Item>> ObterPorUsuarioAsync(int idAccount)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Itens
            .AsNoTracking()
            .Where(i => i.IdAccount == null || i.IdAccount == idAccount)
            .ToListAsync();
    }

    public async Task<Item?> ObterPorIdAsync(int idItem)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Itens
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.IdItem == idItem);
    }

    public async Task<int> AdicionarAsync(Item item)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        await contexto.Itens.AddAsync(item);
        await contexto.SaveChangesAsync();
        return item.IdItem;
    }

    public async Task RemoverAsync(Item item)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        contexto.Itens.Remove(item);
        await contexto.SaveChangesAsync();
    }
}