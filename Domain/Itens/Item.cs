using RPGdeMesa.Domain.Common;

namespace RPGdeMesa.Domain.Itens;

public class Item : IAggregateRoot
{
    public int IdItem { get; private set; }
    public int? IdAccount { get; private set; } 
    public string Nome { get; private set; } = string.Empty;
    public string? Tipo { get; private set; }
    public decimal? Peso { get; private set; }
    public decimal? Preco { get; private set; }
    public string? Dano { get; private set; }
    public string? TipoDano { get; private set; }
    public string? Propriedades { get; private set; }
    public int CaBonus { get; private set; } = 0;
    public string? Descricao { get; private set; }
    public string? Raridade { get; private set; }
    public string? Sintonizacao { get; private set; }

    public Item() { }

    public Item(int? idAccount, string nome, string? tipo, decimal? peso, decimal? preco, string? dano, string? tipoDano, string? propriedades, int caBonus, string? descricao, string? raridade, string? sintonizacao)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do item é obrigatório.");
        if (peso is < 0)
            throw new DomainException("O peso do item não pode ser negativo.");
        if (preco is < 0)
            throw new DomainException("O preço do item não pode ser negativo.");

        IdAccount = idAccount;
        Nome = nome.Trim();
        Tipo = tipo;
        Peso = peso;
        Preco = preco;
        Dano = dano;
        TipoDano = tipoDano;
        Propriedades = propriedades;
        CaBonus = caBonus;
        Descricao = descricao;
        Raridade = raridade;
        Sintonizacao = sintonizacao;
    }
}