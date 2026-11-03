using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Usuarios;
using RPGdeMesa.Domain.Npcs;

namespace RPGdeMesa.Domain.Campanhas;

public class Campanha : IAggregateRoot
{
    public int IdCampanha { get; private set; }
    public int IdAccount { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public DateTime DataCriacao { get; private set; }
    public string? Descricao { get; private set; }
    public string CodigoConvite { get; private set; } = string.Empty;

    public virtual Usuario UsuarioMestre { get; private set; } = null!;

    private readonly List<PersonagemCampanha> _personagemCampanhas = new();
    public virtual IReadOnlyCollection<PersonagemCampanha> PersonagemCampanhas => _personagemCampanhas.AsReadOnly();

    private readonly List<MonstroCampanha> _monstroCampanhas = new();
    public virtual IReadOnlyCollection<MonstroCampanha> MonstroCampanhas => _monstroCampanhas.AsReadOnly();

    private readonly List<NPC> _npcs = new();
    public virtual IReadOnlyCollection<NPC> Npcs => _npcs.AsReadOnly();

    public virtual ICollection<UsuarioCampanha> UsuarioCampanhas { get; private set; } = new List<UsuarioCampanha>();

    public IEnumerable<Personagem> Personagens => _personagemCampanhas
        .Where(pc => pc.Personagem != null)
        .Select(pc => pc.Personagem!);



    public Campanha() { }

    public Campanha(int idAccountMestre, string nome, string? descricao, string? codigoConvite = null)
    {
        if (idAccountMestre <= 0)
            throw new DomainException("A campanha precisa de um mestre válido.");
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome da campanha é obrigatório.");

        IdAccount = idAccountMestre;
        Nome = nome.Trim();
        Descricao = descricao;
        DataCriacao = DateTime.Now;
        CodigoConvite = string.IsNullOrWhiteSpace(codigoConvite)
            ? GerarCodigoConvitePadrao()
            : codigoConvite.Trim().ToUpperInvariant();
    }

    public void AtualizarDetalhes(string nome, string? descricao)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome da campanha não pode ficar vazio.");

        Nome = nome.Trim();
        Descricao = descricao;
    }

    public void VincularPersonagem(int idPersonagem)
    {
        if (_personagemCampanhas.Any(pc => pc.IdPersonagem == idPersonagem))
            throw new DomainException("Este personagem já faz parte da campanha.");

        _personagemCampanhas.Add(new PersonagemCampanha(idPersonagem, IdCampanha));
    }

    public void RemoverPersonagem(int idPersonagem)
    {
        var vinculo = _personagemCampanhas.FirstOrDefault(pc => pc.IdPersonagem == idPersonagem);
        if (vinculo != null)
            _personagemCampanhas.Remove(vinculo);
    }

    public void VincularMonstro(int idMonstro)
    {
        if (_monstroCampanhas.Any(mc => mc.IdMonstro == idMonstro))
            throw new DomainException("Este monstro já está vinculado à campanha.");

        _monstroCampanhas.Add(new MonstroCampanha(IdCampanha, idMonstro));
    }

    public NPC AdicionarNpc(string nome, string? ocupacao, string? alinhamento, string? localizacao, string? descricao)
    {
        var npc = new NPC(IdCampanha, nome, ocupacao, alinhamento, localizacao, descricao);
        _npcs.Add(npc);
        return npc;
    }

    private static string GerarCodigoConvitePadrao() =>
        Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}