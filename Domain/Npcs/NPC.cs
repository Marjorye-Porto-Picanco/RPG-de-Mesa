using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Usuarios;
using RPGdeMesa.Domain.ValueObjects;

namespace RPGdeMesa.Domain.Npcs;

public class NPC : IAggregateRoot
{
    public int IdNpc { get; private set; }
    public int? IdCampanha { get; private set; }
    public int? IdAccount { get; private set; }

    public string Nome { get; private set; } = string.Empty;
    public string? RacaTexto { get; set; }
    public string? ClasseTexto { get; set; }
    public int Nivel { get; set; } = 1;
    public string? AntecedenteTexto { get; set; }
    public string? Tendencia { get; set; } = "Neutra";

    public int Forca { get; set; } = 10;
    public int Destreza { get; set; } = 10;
    public int Constituicao { get; set; } = 10;
    public int Inteligencia { get; set; } = 10;
    public int Sabedoria { get; set; } = 10;
    public int Carisma { get; set; } = 10;

    public string? Historia { get; set; }
    public string? Imagem { get; set; } = "img/capacampanha3.png";
    public string? Ocupacao { get; private set; }
    public string? Alinhamento { get; private set; }
    public string? Localizacao { get; private set; }
    public string? Descricao { get; private set; }

    public virtual Campanha? Campanha { get; private set; }
    public virtual Usuario? Usuario { get; private set; }

    public NPC() { }

    // Construtor usado quando criado rápido dentro de uma campanha
    public NPC(int idCampanha, string nome, string? ocupacao, string? alinhamento, string? localizacao, string? descricao)
    {
        ValidarNome(nome);
        IdCampanha = idCampanha;
        Nome = nome.Trim();
        Ocupacao = ocupacao;
        Alinhamento = alinhamento;
        Tendencia = alinhamento ?? "Neutra";
        Localizacao = localizacao;
        Descricao = descricao;
        Historia = descricao;
    }

    // Construtor completo para criação de NPC via Ficha (Biblioteca do Usuário ou Campanha)
    public NPC(
        int idAccount,
        string nome,
        string? racaTexto,
        string? classeTexto,
        int nivel,
        string? antecedenteTexto,
        string? tendencia,
        int forca, int destreza, int constituicao, int inteligencia, int sabedoria, int carisma,
        string? historia,
        string? imagem = null,
        int? idCampanha = null)
    {
        PrepararNovoNpc(idAccount, nome, idCampanha);
        RacaTexto = racaTexto;
        ClasseTexto = classeTexto;
        Nivel = nivel > 0 ? nivel : 1;
        AntecedenteTexto = antecedenteTexto;
        Tendencia = string.IsNullOrWhiteSpace(tendencia) ? "Neutra" : tendencia;

        Forca = forca;
        Destreza = destreza;
        Constituicao = constituicao;
        Inteligencia = inteligencia;
        Sabedoria = sabedoria;
        Carisma = carisma;

        Historia = historia;
        Descricao = historia;
        Ocupacao = !string.IsNullOrWhiteSpace(classeTexto) ? classeTexto : antecedenteTexto;
        Alinhamento = Tendencia;

        if (!string.IsNullOrWhiteSpace(imagem))
            Imagem = imagem;
    }

    public void PrepararNovoNpc(int idAccount, string nome, int? idCampanha = null)
    {
        if (idAccount <= 0)
            throw new DomainException("O NPC deve pertencer a uma conta de usuário válida.");

        ValidarNome(nome);

        IdAccount = idAccount;
        IdCampanha = idCampanha;
        Nome = nome.Trim();
    }

    public void VincularACampanha(int idCampanha)
    {
        if (idCampanha <= 0)
            throw new DomainException("ID de campanha inválido para vínculo.");
        IdCampanha = idCampanha;
    }

    public void DesvincularDeCampanha()
    {
        IdCampanha = null;
    }

    private void ValidarNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome do NPC é obrigatório.");
        if (nome.Length > 100)
            throw new DomainException("O nome do NPC deve ter no máximo 100 caracteres.");
    }

    public void Atualizar(string nome, string? ocupacao, string? alinhamento, string? localizacao, string? descricao)
    {
        ValidarNome(nome);
        Nome = nome.Trim();
        Ocupacao = ocupacao;
        Alinhamento = alinhamento;
        Localizacao = localizacao;
        Descricao = descricao;
    }
}