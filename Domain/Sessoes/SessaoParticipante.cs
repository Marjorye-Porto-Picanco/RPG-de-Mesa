using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Usuarios;

namespace RPGdeMesa.Domain.Sessoes;

public class SessaoParticipante
{
    public int IdSessao { get; private set; }
    public int IdAccount { get; private set; }
    public int? IdPersonagem { get; private set; }
    public bool Online { get; private set; }
    public DateTime UltimoHeartbeat { get; private set; }

    public virtual SessaoMesa Sessao { get; private set; } = null!;
    public virtual Usuario Usuario { get; private set; } = null!;
    public virtual Personagem? Personagem { get; private set; }
    public bool Esquivando { get; private set; }
    public bool Escondido { get; private set; }

    public void DefinirEsquiva(bool esquivando) => Esquivando = esquivando;
    public void DefinirEscondido(bool escondido) => Escondido = escondido;

    protected SessaoParticipante() { }

    public SessaoParticipante(int idSessao, int idAccount, int? idPersonagem = null)
    {
        if (idSessao <= 0)
            throw new DomainException("Sessão inválida.");
        if (idAccount <= 0)
            throw new DomainException("Usuário inválido para participar da sessão.");

        IdSessao = idSessao;
        IdAccount = idAccount;
        IdPersonagem = idPersonagem;
        Online = true;
        UltimoHeartbeat = DateTime.UtcNow;
    }

    public void Conectar(int? idPersonagem = null)
    {
        if (idPersonagem.HasValue)
            IdPersonagem = idPersonagem;

        Online = true;
        UltimoHeartbeat = DateTime.UtcNow;
    }

    public void AtualizarHeartbeat()
    {
        Online = true;
        UltimoHeartbeat = DateTime.UtcNow;
    }

    public void Desconectar()
    {
        Online = false;
        UltimoHeartbeat = DateTime.UtcNow;
    }

    public bool EstaConectadoAgora(int toleranciaSegundos = 15) =>
        Online && (DateTime.UtcNow - UltimoHeartbeat).TotalSeconds <= toleranciaSegundos;
}