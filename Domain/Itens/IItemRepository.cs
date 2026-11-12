using System.Collections.Generic;
using System.Threading.Tasks;

namespace RPGdeMesa.Domain.Itens;

public interface IItemRepository
{
    Task<IEnumerable<Item>> ObterPorUsuarioAsync(int idAccount);
    Task<Item?> ObterPorIdAsync(int idItem);
    Task<int> AdicionarAsync(Item item);
    Task RemoverAsync(Item item);
}