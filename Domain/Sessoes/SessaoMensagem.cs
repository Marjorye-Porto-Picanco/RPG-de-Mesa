using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Usuarios;

namespace RPGdeMesa.Domain.Sessoes;

public static class CanaisSessao
{
    public const string Chat = "Chat";
    public const string Registro = "Registro";
    public const string Npc = "NPC";
    public const string Combate = "Combate";
}

public class SessaoMensagem
{
    public int IdMensagem { get; private set; }
    public int IdSessao { get; private set; }
    public int? IdAccount { get; private set; }
    public string Canal { get; private set; } = CanaisSessao.Chat;
    public string Conteudo { get; private set; } = string.Empty;
    public DateTime DataEnvio { get; private set; }

    public virtual SessaoMesa Sessao { get; private set; } = null!;
    public virtual Usuario? Usuario { get; private set; }

    protected SessaoMensagem() { }

    public SessaoMensagem(int idSessao, int? idAccount, string canal, string conteudo)
    {
        if (idSessao <= 0)
            throw new DomainException("Sessão inválida para enviar mensagem.");
        if (string.IsNullOrWhiteSpace(canal))
            throw new DomainException("Canal de mensagem inválido.");
        if (string.IsNullOrWhiteSpace(conteudo))
            throw new DomainException("A mensagem não pode estar vazia.");

        IdSessao = idSessao;
        IdAccount = idAccount;
        Canal = canal.Trim();
        Conteudo = conteudo.Trim();
        DataEnvio = DateTime.UtcNow;
    }
}