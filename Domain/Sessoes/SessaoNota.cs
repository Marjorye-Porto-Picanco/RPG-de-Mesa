using RPGdeMesa.Domain.Common;

namespace RPGdeMesa.Domain.Sessoes;

public class SessaoNota
{
    public const int LimiteNome = 150;
    public const int LimiteConteudo = 10000;

    public Guid IdNota { get; private set; }
    public int IdCampanha { get; private set; }
    public int NumeroSessao { get; private set; }
    public int IdAccount { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string Conteudo { get; private set; } = string.Empty;
    public DateTime CriadaEmUtc { get; private set; }

    private SessaoNota() { }

    public SessaoNota(Guid idNota, int idCampanha, int numeroSessao, int idAccount, string nome, string conteudo)
    {
        if (idNota == Guid.Empty || idCampanha <= 0 || numeroSessao <= 0 || idAccount <= 0)
            throw new DomainException("Sessão inválida para registrar a nota.");

        Nome = nome.Trim();
        Conteudo = conteudo.Trim();

        if (Nome.Length == 0 || Nome.Length > LimiteNome)
            throw new DomainException($"Informe um nome com até {LimiteNome} caracteres.");

        if (Conteudo.Length == 0 || Conteudo.Length > LimiteConteudo)
            throw new DomainException($"Informe um conteúdo com até {LimiteConteudo} caracteres.");

        IdNota = idNota;
        IdCampanha = idCampanha;
        NumeroSessao = numeroSessao;
        IdAccount = idAccount;
        CriadaEmUtc = DateTime.UtcNow;
    }
}
