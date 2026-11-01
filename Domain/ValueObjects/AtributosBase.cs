using RPGdeMesa.Domain.Common;

namespace RPGdeMesa.Domain.ValueObjects;

public record AtributosBase
{
    public int Forca { get; init; } = 10;
    public int Destreza { get; init; } = 10;
    public int Constituicao { get; init; } = 10;
    public int Inteligencia { get; init; } = 10;
    public int Sabedoria { get; init; } = 10;
    public int Carisma { get; init; } = 10;

    protected AtributosBase() { }

    public AtributosBase(int forca, int destreza, int constituicao, int inteligencia, int sabedoria, int carisma)
    {
        if (forca < 1 || destreza < 1 || constituicao < 1 || inteligencia < 1 || sabedoria < 1 || carisma < 1)
            throw new DomainException("Os atributos do personagem não podem ser menores que 1.");

        Forca = forca;
        Destreza = destreza;
        Constituicao = constituicao;
        Inteligencia = inteligencia;
        Sabedoria = sabedoria;
        Carisma = carisma;
    }

    // Regra de D&D 5e direto no Value Object!
    public static int CalcularModificador(int valorAtributo) => (int)Math.Floor((valorAtributo - 10) / 2.0);

    public int ModForca => CalcularModificador(Forca);
    public int ModDestreza => CalcularModificador(Destreza);
    public int ModConstituicao => CalcularModificador(Constituicao);
    public int ModInteligencia => CalcularModificador(Inteligencia);
    public int ModSabedoria => CalcularModificador(Sabedoria);
    public int ModCarisma => CalcularModificador(Carisma);
}