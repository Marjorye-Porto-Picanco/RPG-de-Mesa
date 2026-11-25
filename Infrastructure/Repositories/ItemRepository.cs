using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using RPGdeMesa.Domain.Common;
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
        try
        {
            await contexto.SaveChangesAsync();
        }
        catch (DbUpdateException erro) when (erro.InnerException is SqlException sql)
        {
            string? mensagem = sql.Number switch
            {
                2601 or 2627 => "O cadastro entraria em conflito com um item existente. Confira sua biblioteca.",
                2628 or 8152 => "Um campo do item excede o tamanho aceito. Encurte os textos e tente novamente.",
                8115 => "Um valor numérico excede o limite aceito. Confira o peso, o preço e o bônus de CA.",
                515 or 547 => "Os dados do item não atendem às regras de cadastro. Confira os campos preenchidos.",
                _ => null
            };

            if (mensagem is null)
                throw;

            throw new DomainException(mensagem, erro);
        }

        return item.IdItem;
    }

    public async Task RemoverAsync(Item item)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        contexto.Itens.Remove(item);
        await contexto.SaveChangesAsync();
    }
}
