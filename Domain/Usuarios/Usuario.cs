using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Personagens;

namespace RPGdeMesa.Domain.Usuarios;

public class Usuario : IAggregateRoot
{
    public int IdAccount { get; private set; }
    public string UsarName { get; private set; } = string.Empty;
    public string EMail { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;
    public DateTime DataCadastro { get; private set; }
    public string StatusConta { get; private set; } = "ativo";

    public virtual Perfil? Perfil { get; private set; }

    private readonly List<Personagem> _personagens = new();
    public virtual IReadOnlyCollection<Personagem> Personagens => _personagens.AsReadOnly();

    protected Usuario() { }

    public Usuario(string usarName, string email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(usarName))
            throw new DomainException("O nome de usuário é obrigatório.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("E-mail inválido.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("A senha é obrigatória.");

        UsarName = usarName.Trim();
        EMail = email.Trim().ToLowerInvariant();
        Password = passwordHash;
        DataCadastro = DateTime.Now;
        StatusConta = "ativo";
        Perfil = new Perfil(this);
    }

    public void AtualizarPerfil(string? biografia, string? avatarUrl)
    {
        if (Perfil is null)
            Perfil = new Perfil(this, biografia, avatarUrl);
        else
            Perfil.Atualizar(biografia, avatarUrl);
    }

    public void AlterarStatusConta(string novoStatus)
    {
        if (string.IsNullOrWhiteSpace(novoStatus))
            throw new DomainException("O status da conta não pode ser vazio.");
        StatusConta = novoStatus;
    }
}