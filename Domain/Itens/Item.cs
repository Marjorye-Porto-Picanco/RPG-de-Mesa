using RPGdeMesa.Domain.Common;

namespace RPGdeMesa.Domain.Itens;

public class Item : IAggregateRoot
{
    public int IdItem { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? Tipo { get; private set; }
    public decimal? Peso { get; private set; }
    public decimal? Preco { get; private set; }
    public string? Dano { get; private set; }
    public string? TipoDano { get; private set; }
    public string? Propriedades { get; private set; }
    public int CaBonus { get; private set; } = 0;
    public string? Descricao { get; private set; }

    public Item() { }

    public Item(string nome, string? tipo, decimal? peso, decimal? preco, string? dano, string? tipoDano, string? propriedades, int caBonus, string? descricao)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do item é obrigatório.");
        if (peso is < 0)
            throw new DomainException("O peso do item não pode ser negativo.");
        if (preco is < 0)
            throw new DomainException("O preço do item não pode ser negativo.");

        Nome = nome.Trim();
        Tipo = tipo;
        Peso = peso;
        Preco = preco;
        Dano = dano;
        TipoDano = tipoDano;
        Propriedades = propriedades;
        CaBonus = caBonus;
        Descricao = descricao;
    }
}