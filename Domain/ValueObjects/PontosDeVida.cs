using RPGdeMesa.Domain.Common;

namespace RPGdeMesa.Domain.ValueObjects;

public record PontosDeVida
{
    public int Atual { get; init; }
    public int Maximo { get; init; }

    protected PontosDeVida() { }

    public PontosDeVida(int atual, int maximo)
    {
        if (maximo <= 0)
            throw new DomainException("O PV máximo deve ser maior que zero.");

        Maximo = maximo;
        Atual = Math.Clamp(atual, 0, maximo);
    }

    public PontosDeVida ReceberDano(int dano)
    {
        if (dano < 0) throw new DomainException("O dano não pode ser negativo.");
        return new PontosDeVida(Math.Max(0, Atual - dano), Maximo);
    }

    public PontosDeVida Curar(int cura)
    {
        if (cura < 0) throw new DomainException("A cura não pode ser negativa.");
        return new PontosDeVida(Math.Min(Maximo, Atual + cura), Maximo);
    }
}