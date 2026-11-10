using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Usuarios;
using RPGdeMesa.Domain.ValueObjects;

namespace RPGdeMesa.Domain.Personagens;

public class Personagem : IAggregateRoot
{
    public int IdPersonagem { get; private set; }
    public int IdAccount { get; private set; }
    public int? IdRaca { get; set; }
    public int? IdAntecedente { get; set; }

    public string Nome { get; set; } = string.Empty;
    public int Nivel { get; set; } = 1;
    public int ExperienciaXp { get; set; } = 0;
    public int PvAtual { get; set; } = 10;
    public int PvMaximo { get; set; } = 10;
    public int? Iniciativa { get; set; } = 0;
    public int? Deslocamento { get; set; } = 9;
    public string? RacaTexto { get; set; }
    public string? ClasseTexto { get; set; }
    public string? AntecedenteTexto { get; set; }
    public string? Tendencia { get; set; }
    public string? Historia { get; set; }
    public string? Imagem { get; set; } = "img/armaduraprata.png";

    public int Forca { get; set; } = 10;
    public int Destreza { get; set; } = 10;
    public int Constituicao { get; set; } = 10;
    public int Inteligencia { get; set; } = 10;
    public int Sabedoria { get; set; } = 10;
    public int Carisma { get; set; } = 10;

    public string? Idade { get; set; }
    public string? Genero { get; set; }
    public string? Altura { get; set; }
    public string? Peso { get; set; }
    public string? Aparencia { get; set; }
    public string? Aliados { get; set; }
    public string? Caracteristicas { get; set; }
    public string? Tesouro { get; set; }

    public string? TracosPersonalidade { get; set; }
    public string? PontoDeVista { get; set; }
    public string? Ideais { get; set; }
    public string? Ligacoes { get; set; }
    public string? Defeitos { get; set; }
    public string? Idiomas { get; set; }

    public virtual Usuario Usuario { get; private set; } = null!;
    public virtual Raca? Raca { get; set; }
    public virtual Antecedente? Antecedente { get; set; }

    private readonly List<PersonagemCampanha> _personagemCampanhas = new();
    public virtual IReadOnlyCollection<PersonagemCampanha> PersonagemCampanhas => _personagemCampanhas.AsReadOnly();

    // Relações 3FN
    public virtual Inventario? Inventario { get; set; }
    public virtual ICollection<PersonagemClasse> PersonagemClasses { get; set; } = new List<PersonagemClasse>();
    public virtual ICollection<PersonagemSalvaguarda> Salvaguardas { get; set; } = new List<PersonagemSalvaguarda>();
    public virtual ICollection<PersonagemPericia> Pericias { get; set; } = new List<PersonagemPericia>();
    public virtual ICollection<PersonagemMagia> PersonagemMagias { get; set; } = new List<PersonagemMagia>();

    public Personagem() { }

    public Personagem(int idAccount, string nome, string? racaTexto, string? classeTexto, int pvMaximo = 10)
    {
        VincularContaEValidar(idAccount);
        Nome = nome.Trim();
        RacaTexto = racaTexto;
        ClasseTexto = classeTexto;
        PvMaximo = pvMaximo > 0 ? pvMaximo : 10;
        PvAtual = PvMaximo;
    }

    public void PrepararNovoPersonagem(int idAccount)
    {
        VincularContaEValidar(idAccount);
        if (string.IsNullOrWhiteSpace(Nome))
            throw new DomainException("O nome do personagem é obrigatório.");

        if (PvMaximo <= 0) PvMaximo = 10;
        if (PvAtual <= 0) PvAtual = PvMaximo;
    }

    public void VincularContaEValidar(int idAccount)
    {
        if (idAccount <= 0)
            throw new DomainException("O personagem deve pertencer a uma conta válida.");
        IdAccount = idAccount;
    }

    public void DefinirAtributos(AtributosBase atributos)
    {
        Forca = atributos.Forca;
        Destreza = atributos.Destreza;
        Constituicao = atributos.Constituicao;
        Inteligencia = atributos.Inteligencia;
        Sabedoria = atributos.Sabedoria;
        Carisma = atributos.Carisma;
    }

    public void ReceberDano(int dano)
    {
        var vida = new PontosDeVida(PvAtual, PvMaximo).ReceberDano(dano);
        PvAtual = vida.Atual;
    }

    public void Curar(int cura)
    {
        var vida = new PontosDeVida(PvAtual, PvMaximo).Curar(cura);
        PvAtual = vida.Atual;
    }

    public void SubirDeNivel(int ganhoPvMaximo)
    {
        if (Nivel >= 20)
            throw new DomainException("O personagem já atingiu o nível máximo (20).");
        if (ganhoPvMaximo <= 0)
            throw new DomainException("O ganho de vida ao subir de nível deve ser positivo.");

        Nivel++;
        PvMaximo += ganhoPvMaximo;
        PvAtual += ganhoPvMaximo;
    }
}