using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Usuarios;

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

    private readonly List<NpcCampanha> _npcCampanhas = new();
    public virtual IReadOnlyCollection<NpcCampanha> NpcCampanhas => _npcCampanhas.AsReadOnly();

    private readonly List<ItemCampanha> _itemCampanhas = new();
    public virtual IReadOnlyCollection<ItemCampanha> ItemCampanhas => _itemCampanhas.AsReadOnly();

    public virtual ICollection<UsuarioCampanha> UsuarioCampanhas { get; private set; } = new List<UsuarioCampanha>();

    // Retorna apenas os personagens já aceitos pelo mestre para a mesa ativa
    public IEnumerable<Personagem> Personagens => _personagemCampanhas
        .Where(pc => pc.Personagem != null &&
                    (string.IsNullOrEmpty(pc.StatusAprovacao) ||
                     pc.StatusAprovacao.Equals("Aceita", StringComparison.OrdinalIgnoreCase)))
        .Select(pc => pc.Personagem!);

    public IEnumerable<PersonagemCampanha> FichasPendentes => _personagemCampanhas
        .Where(pc => pc.Personagem != null &&
                     string.Equals(pc.StatusAprovacao, "Pendente", StringComparison.OrdinalIgnoreCase));

    public IEnumerable<PersonagemCampanha> FichasAceitas => _personagemCampanhas
        .Where(pc => pc.Personagem != null &&
                    (string.IsNullOrEmpty(pc.StatusAprovacao) ||
                     string.Equals(pc.StatusAprovacao, "Aceita", StringComparison.OrdinalIgnoreCase)));

    public IEnumerable<NPC> Npcs => _npcCampanhas
        .Where(nc => nc.Npc != null)
        .Select(nc => nc.Npc!);

    public IEnumerable<Item> Itens => _itemCampanhas
        .Where(ic => ic.Item != null)
        .Select(ic => ic.Item!);

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

    public void VincularPersonagem(int idPersonagem, string statusAprovacao = "Aceita")
    {
        if (_personagemCampanhas.Any(pc => pc.IdPersonagem == idPersonagem))
            throw new DomainException("Este personagem já faz parte da campanha.");

        _personagemCampanhas.Add(new PersonagemCampanha(idPersonagem, IdCampanha, statusAprovacao));
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

    public void VincularNpc(int idNpc)
    {
        if (_npcCampanhas.Any(nc => nc.IdNpc == idNpc))
            throw new DomainException("Este NPC já está vinculado à campanha.");

        _npcCampanhas.Add(new NpcCampanha(IdCampanha, idNpc));
    }

    public void VincularItem(int idItem)
    {
        if (_itemCampanhas.Any(ic => ic.IdItem == idItem))
            throw new DomainException("Este item já está vinculado à campanha.");

        _itemCampanhas.Add(new ItemCampanha(IdCampanha, idItem));
    }

    public void RemoverItemCampanha(int idItem)
    {
        var vinculo = _itemCampanhas.FirstOrDefault(ic => ic.IdItem == idItem);
        if (vinculo != null)
            _itemCampanhas.Remove(vinculo);
    }

    private static string GerarCodigoConvitePadrao() =>
        Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}