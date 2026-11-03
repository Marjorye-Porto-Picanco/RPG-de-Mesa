using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Common;

namespace RPGdeMesa.Domain.Npcs;

public class NPC : IAggregateRoot
{
    public int IdNpc { get; private set; }
    public int? IdCampanha { get; private set; }
    public int? IdAccount { get; private set; } // Opcional: caso pertença diretamente à conta do usuário
    public string Nome { get; private set; } = string.Empty;
    public string? Ocupacao { get; private set; }
    public string? Alinhamento { get; private set; }
    public string? Localizacao { get; private set; }
    public string? Descricao { get; private set; }
    public string? Imagem { get; private set; } = "img/capacampanha3.png";

    public virtual Campanha? Campanha { get; private set; }

    public NPC() { }

    // Construtor para NPC vinculado a uma campanha
    public NPC(int idCampanha, string nome, string? ocupacao, string? alinhamento, string? localizacao, string? descricao)
    {
        ValidarNome(nome);
        IdCampanha = idCampanha;
        AtribuirCampos(nome, ocupacao, alinhamento, localizacao, descricao);
    }

    // Novo Construtor para NPC avulso criado na biblioteca do usuário
    public NPC(int idAccount, string nome, string? ocupacao, string? alinhamento, string? localizacao, string? descricao, string? imagem = null)
    {
        ValidarNome(nome);
        IdAccount = idAccount;
        AtribuirCampos(nome, ocupacao, alinhamento, localizacao, descricao);
        if (!string.IsNullOrWhiteSpace(imagem))
            Imagem = imagem;
    }

    private void ValidarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do NPC é obrigatório.");
        if (nome.Length > 100)
            throw new DomainException("O nome do NPC deve ter no máximo 100 caracteres.");
    }

    private void AtribuirCampos(string nome, string? ocupacao, string? alinhamento, string? localizacao, string? descricao)
    {
        Nome = nome.Trim();
        Ocupacao = ocupacao;
        Alinhamento = alinhamento;
        Localizacao = localizacao;
        Descricao = descricao;
    }

    public void Atualizar(string nome, string? ocupacao, string? alinhamento, string? localizacao, string? descricao)
    {
        ValidarNome(nome);
        AtribuirCampos(nome, ocupacao, alinhamento, localizacao, descricao);
    }
}