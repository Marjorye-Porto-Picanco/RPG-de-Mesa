using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RPGdeMesa.Domain.Itens;

namespace RPGdeMesa.Application.Services;

public class ItemService
{
    private readonly IItemRepository _itemRepository;

    public ItemService(IItemRepository itemRepository)
    {
        _itemRepository = itemRepository;
    }

    public async Task<IEnumerable<Item>> ObterItensPorUsuarioAsync(int idAccount)
    {
        return await _itemRepository.ObterPorUsuarioAsync(idAccount);
    }

    public async Task<ItemResult> CriarItemAsync(int idAccount, Item item)
    {
        try
        {
            int idGerado = await _itemRepository.AdicionarAsync(item);
            return new ItemResult(true, "Item cadastrado com sucesso!", idGerado);
        }
        catch (Exception ex)
        {
            return new ItemResult(false, $"Erro ao cadastrar item: {ex.Message}", 0);
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> RemoverItemAsync(int idAccount, int idItem)
    {
        try
        {
            var item = await _itemRepository.ObterPorIdAsync(idItem);
            if (item is null || item.IdAccount != idAccount)
                return (false, "Item não encontrado ou sem permissão para removê-lo.");

            await _itemRepository.RemoverAsync(item);
            return (true, "Item removido com sucesso!");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao remover item: {ex.Message}");
        }
    }
}

public class ItemResult
{
    public bool Sucesso { get; set; }
    public string Mensagem { get; set; } = string.Empty;
    public int IdItemCriado { get; set; }

    public ItemResult(bool sucesso, string mensagem, int idItemCriado)
    {
        Sucesso = sucesso;
        Mensagem = mensagem;
        IdItemCriado = idItemCriado;
    }
}