using RPGdeMesa.Domain.Itens;

namespace RPGdeMesa.Domain.Personagens;

public class Raca
{
    public int IdRaca { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Tamanho { get; set; } = "Médio";
    public int DeslocamentoBase { get; set; } = 9;
    public bool? VisaoNoEscuro { get; set; } = false;
}

public class Antecedente
{
    public int IdAntecedente { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? ProficienciasPericias { get; set; }
    public string? EquipamentoInicial { get; set; }
}

public class Classe
{
    public int IdClasse { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string DadoVida { get; set; } = "d8";
    public string? AtributoPrimario { get; set; }
}

public class PersonagemClasse
{
    public int IdPersonagem { get; set; }
    public int IdClasse { get; set; }
    public int NivelNaClasse { get; set; } = 1;

    public virtual Personagem? Personagem { get; set; }
    public virtual Classe? Classe { get; set; }
}

public class PersonagemSalvaguarda
{
    public int IdPersonagem { get; set; }
    public string Atributo { get; set; } = string.Empty;
    public virtual Personagem? Personagem { get; set; }

    public PersonagemSalvaguarda() { }
    public PersonagemSalvaguarda(int idPersonagem, string atributo)
    {
        IdPersonagem = idPersonagem;
        Atributo = atributo;
    }
}

public class PersonagemPericia
{
    public int IdPersonagem { get; set; }
    public string NomePericia { get; set; } = string.Empty;
    public virtual Personagem? Personagem { get; set; }

    public PersonagemPericia() { }
    public PersonagemPericia(int idPersonagem, string nomePericia)
    {
        IdPersonagem = idPersonagem;
        NomePericia = nomePericia;
    }
}

public class Inventario
{
    public int IdInventario { get; set; }
    public int IdPersonagem { get; set; }
    public decimal? CapacidadeCarga { get; set; }
    public decimal? PesoAtual { get; set; }
    public decimal? Dinheiro { get; set; }
    public int MoedasPC { get; set; }
    public int MoedasPP { get; set; }
    public int MoedasPE { get; set; }
    public int MoedasPO { get; set; }
    public int MoedasPL { get; set; }
    public string? Notas { get; set; }

    public virtual Personagem? Personagem { get; set; }
    public virtual ICollection<InventarioItem> Itens { get; set; } = new List<InventarioItem>();
}

public class InventarioItem
{
    public int IdInventario { get; set; }
    public int IdItem { get; set; }
    public int Quantidade { get; set; } = 1;

    public virtual Inventario? Inventario { get; set; }
    public virtual Item? Item { get; set; }
}

public class Magia
{
    public int IdMagia { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int CirculoNivel { get; set; }
    public string? Escola { get; set; }
    public string? TempoConjuracao { get; set; }
    public string? Alcance { get; set; }
    public string? Componentes { get; set; }
    public string? Duracao { get; set; }
    public string? DanoEfeito { get; set; }
}

public class PersonagemMagia
{
    public int IdPersonagem { get; set; }
    public int IdMagia { get; set; }

    public virtual Personagem? Personagem { get; set; }
    public virtual Magia? Magia { get; set; }
}