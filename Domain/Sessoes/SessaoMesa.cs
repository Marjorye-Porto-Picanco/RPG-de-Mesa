using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Common;

namespace RPGdeMesa.Domain.Sessoes;

public class SessaoMesa : IAggregateRoot
{
    public int IdSessao { get; private set; }
    public int IdCampanha { get; private set; }
    public int? IdMapaAtivo { get; private set; }
    public DateTime DataAtualizacao { get; private set; }

    public virtual Campanha Campanha { get; private set; } = null!;
    public virtual Mapa? MapaAtivo { get; private set; }

    private readonly List<SessaoParticipante> _participantes = new();
    public virtual IReadOnlyCollection<SessaoParticipante> Participantes => _participantes.AsReadOnly();

    private readonly List<SessaoMensagem> _mensagens = new();
    public virtual IReadOnlyCollection<SessaoMensagem> Mensagens => _mensagens.AsReadOnly();

    protected SessaoMesa() { }

    public SessaoMesa(int idCampanha, int? idMapaInicial = null)
    {
        if (idCampanha <= 0)
            throw new DomainException("Campanha inválida para iniciar a sessão.");

        IdCampanha = idCampanha;
        IdMapaAtivo = idMapaInicial;
        DataAtualizacao = DateTime.UtcNow;
    }

    public void AlterarMapaAtivo(int? idMapa)
    {
        IdMapaAtivo = idMapa;
        DataAtualizacao = DateTime.UtcNow;
    }

    public void RegistrarAtualizacao()
    {
        DataAtualizacao = DateTime.UtcNow;
    }
}