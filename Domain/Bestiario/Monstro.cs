using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Usuarios;

namespace RPGdeMesa.Domain.Bestiario;

public class Monstro : IAggregateRoot
{
    public int IdMonstro { get; private set; }
    public int IdAccount { get; private set; }
    public string Nome { get; set; } = string.Empty;
    public string? TipoMonstro { get; set; }
    public decimal? NivelDesafio { get; set; } = 1;
    public int PvMaximo { get; set; } = 24;
    public int ClasseArmadura { get; set; } = 13;
    public string? Tamanho { get; set; } = "Médio";
    public string? Tendencia { get; set; }
    public string? Deslocamento { get; set; }
    public string? Sentidos { get; set; }
    public string? Resistencias { get; set; }
    public string? Habilidades { get; set; }
    public string? Descricao { get; set; }
    public string? Imagem { get; set; }

    public virtual Usuario? Usuario { get; private set; }

    private readonly List<MonstroCampanha> _monstroCampanhas = new();
    public virtual IReadOnlyCollection<MonstroCampanha> MonstroCampanhas => _monstroCampanhas.AsReadOnly();

    public int Vida
    {
        get => PvMaximo;
        set => PvMaximo = value;
    }

    public int Defesa
    {
        get => ClasseArmadura;
        set => ClasseArmadura = value;
    }

    public string? Tipo
    {
        get => TipoMonstro;
        set => TipoMonstro = value;
    }

    public double Nd
    {
        get => NivelDesafio.HasValue ? (double)NivelDesafio.Value : 1;
        set => NivelDesafio = (decimal)value;
    }

    public Monstro() { }

    // Regra de Domínio chamada pelo MonstroService antes de salvar
    public void PrepararNovoMonstro(int idAccount)
    {
        if (idAccount <= 0)
            throw new DomainException("Conta de usuário inválida para criar o monstro.");
        if (string.IsNullOrWhiteSpace(Nome))
            throw new DomainException("O nome do monstro é obrigatório.");

        IdAccount = idAccount;
        if (string.IsNullOrWhiteSpace(Imagem))
            Imagem = "img/capacampanha3.png";
    }
}