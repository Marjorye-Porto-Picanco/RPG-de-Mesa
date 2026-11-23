using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Common;

namespace RPGdeMesa.Domain.Sessoes;

public class SessaoMesa : IAggregateRoot
{
    public int IdSessao { get; private set; }
    public int IdCampanha { get; private set; }
    public int NumeroSessao { get; private set; } = 1;
    public string? Titulo { get; private set; }
    public int? IdMapaAtivo { get; private set; }
    public int? IdAccountTurno { get; private set; }
    public bool Ativa { get; private set; }
    public DateTime DataInicio { get; private set; }
    public DateTime? DataFim { get; private set; }
    public DateTime DataAtualizacao { get; private set; }
    public string? Resumo { get; private set; }
    public int XpTotalDistribuido { get; private set; }

    public virtual Campanha Campanha { get; private set; } = null!;
    public virtual Mapa? MapaAtivo { get; private set; }

    private readonly List<SessaoParticipante> _participantes = new();
    public virtual IReadOnlyCollection<SessaoParticipante> Participantes => _participantes.AsReadOnly();

    private readonly List<SessaoMensagem> _mensagens = new();
    public virtual IReadOnlyCollection<SessaoMensagem> Mensagens => _mensagens.AsReadOnly();

    protected SessaoMesa() { }

    public SessaoMesa(int idCampanha, int numeroSessao = 1, int? idMapaInicial = null, string? titulo = null)
    {
        if (idCampanha <= 0)
            throw new DomainException("Campanha inválida para iniciar a sessão.");

        IdCampanha = idCampanha;
        NumeroSessao = Math.Max(1, numeroSessao);
        Titulo = string.IsNullOrWhiteSpace(titulo) ? $"Sessão #{NumeroSessao}" : titulo.Trim();
        IdMapaAtivo = idMapaInicial;
        IdAccountTurno = null;
        Ativa = true;
        DataInicio = DateTime.UtcNow;
        DataAtualizacao = DataInicio;
        XpTotalDistribuido = 0;
    }

    public void AbrirSessao(int? idMapaPadrao = null)
    {
        Ativa = true;
        if (!IdMapaAtivo.HasValue && idMapaPadrao.HasValue)
            IdMapaAtivo = idMapaPadrao;
        DataAtualizacao = DateTime.UtcNow;
    }

    public void FinalizarSessao()
    {
        Ativa = false;
        IdAccountTurno = null;
        DataFim ??= DateTime.UtcNow;
        DataAtualizacao = DateTime.UtcNow;
    }

    public void AtualizarResumo(string? titulo, string? resumo, int? xpTotalDistribuido = null)
    {
        if (!string.IsNullOrWhiteSpace(titulo))
            Titulo = titulo.Trim();
        else if (string.IsNullOrWhiteSpace(Titulo))
            Titulo = $"Sessão #{NumeroSessao}";

        Resumo = string.IsNullOrWhiteSpace(resumo) ? null : resumo.Trim();

        if (xpTotalDistribuido.HasValue)
            XpTotalDistribuido = Math.Max(0, xpTotalDistribuido.Value);

        DataAtualizacao = DateTime.UtcNow;
    }

    public void AlterarMapaAtivo(int? idMapa)
    {
        IdMapaAtivo = idMapa;
        DataAtualizacao = DateTime.UtcNow;
    }

    public void DefinirTurnoJogador(int? idAccount)
    {
        IdAccountTurno = idAccount;
        DataAtualizacao = DateTime.UtcNow;
    }

    public void RegistrarAtualizacao()
    {
        DataAtualizacao = DateTime.UtcNow;
    }
}