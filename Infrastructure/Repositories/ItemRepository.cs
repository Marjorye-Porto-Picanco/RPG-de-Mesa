using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Infrastructure.Persistence;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RPGdeMesa.Infrastructure.Repositories;

public class ItemRepository : IItemRepository
{
    private readonly AppDbContext _contexto;

    public ItemRepository(AppDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<IEnumerable<Item>> ObterPorUsuarioAsync(int idAccount)
    {
        _contexto.ChangeTracker.Clear();
        return await _contexto.Itens
            .Where(i => i.IdAccount == null || i.IdAccount == idAccount)
            .ToListAsync();
    }

    public async Task<Item?> ObterPorIdAsync(int idItem)
    {
        _contexto.ChangeTracker.Clear();
        return await _contexto.Itens.FirstOrDefaultAsync(i => i.IdItem == idItem);
    }

    public async Task<int> AdicionarAsync(Item item)
    {
        await _contexto.Itens.AddAsync(item);
        await _contexto.SaveChangesAsync();
        return item.IdItem; 
    }

    public async Task RemoverAsync(Item item)
    {
        _contexto.Itens.Remove(item);
        await _contexto.SaveChangesAsync();
    }
}