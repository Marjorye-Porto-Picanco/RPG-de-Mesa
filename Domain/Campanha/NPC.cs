using RPGdeMesa.Domain.Common;

namespace RPGdeMesa.Domain.Campanhas;

public class NPC
{
    public int IdNpc { get; private set; }
    public int IdCampanha { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? Ocupacao { get; private set; }
    public string? Alinhamento { get; private set; }
    public string? Localizacao { get; private set; }
    public string? Descricao { get; private set; }

    public virtual Campanha? Campanha { get; private set; }

    public NPC() { }

    public NPC(int idCampanha, string nome, string? ocupacao, string? alinhamento, string? localizacao, string? descricao)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do NPC é obrigatório.");
        if (nome.Length > 100)
            throw new DomainException("O nome do NPC deve ter no máximo 100 caracteres.");

        IdCampanha = idCampanha;
        Nome = nome.Trim();
        Ocupacao = ocupacao;
        Alinhamento = alinhamento;
        Localizacao = localizacao;
        Descricao = descricao;
    }

    public void Atualizar(string nome, string? ocupacao, string? alinhamento, string? localizacao, string? descricao)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do NPC é obrigatório.");

        Nome = nome.Trim();
        Ocupacao = ocupacao;
        Alinhamento = alinhamento;
        Localizacao = localizacao;
        Descricao = descricao;
    }
}