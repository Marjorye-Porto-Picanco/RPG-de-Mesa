using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Personagens;

namespace RPGdeMesa.Domain.Campanhas;

public class PersonagemCampanha
{
    public int IdPersonagem { get; private set; }
    public int IdCampanha { get; private set; }
    public string StatusAprovacao { get; private set; } = "Pendente";

    public virtual Personagem? Personagem { get; private set; }
    public virtual Campanha Campanha { get; private set; } = null!;

    protected PersonagemCampanha() { }

    public PersonagemCampanha(int idPersonagem, int idCampanha, string statusAprovacao = "Pendente")
    {
        if (idPersonagem <= 0)
            throw new DomainException("Personagem inválido para vínculo.");
        if (idCampanha <= 0)
            throw new DomainException("Campanha inválida para vínculo.");

        IdPersonagem = idPersonagem;
        IdCampanha = idCampanha;
        StatusAprovacao = string.IsNullOrWhiteSpace(statusAprovacao) ? "Pendente" : statusAprovacao.Trim();
    }

    public void AprovarFicha() => StatusAprovacao = "Aceita";

    public void RecusarFicha() => StatusAprovacao = "Recusada";

    public void ReenviarFicha() => StatusAprovacao = "Pendente";
}