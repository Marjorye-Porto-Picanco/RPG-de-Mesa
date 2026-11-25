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
        if (peso is > 999.99m)
            throw new DomainException("O peso do item deve ser de até 999,99 kg.");
        if (preco is > 99_999_999.99m)
            throw new DomainException("O preço do item deve ser de até 99.999.999,99 PO.");

        nome = nome.Trim();
        ValidarTamanho(nome, 100, "Nome");
        ValidarTamanho(tipo, 50, "Tipo");
        ValidarTamanho(dano, 20, "Dano");
        ValidarTamanho(tipoDano, 30, "Tipo de dano");
        ValidarTamanho(raridade, 50, "Raridade");
        ValidarTamanho(sintonizacao, 50, "Sintonização");

        IdAccount = idAccount;
        Nome = nome;
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

    private static void ValidarTamanho(string? valor, int limite, string campo)
    {
        if (valor?.Length > limite)
            throw new DomainException($"O campo {campo} deve ter até {limite} caracteres.");
    }
}
