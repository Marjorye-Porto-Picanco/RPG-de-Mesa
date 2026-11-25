using System.Text.RegularExpressions;

namespace RPGdeMesa.Domain.Sessoes;

public enum Rolagem2014 { Normal, Vantagem, Desvantagem }
public enum Cobertura2014 { Nenhuma, Meia, TresQuartos, Total }
public enum AfinidadeDano2014 { Normal, Resistencia, Imunidade, Vulnerabilidade }
public enum Momento2014 { InicioTurno, FimTurno, Manual }
public enum Atributo2014 { Forca, Destreza, Constituicao, Inteligencia, Sabedoria, Carisma }
public enum TipoAcao2014 { Acao, Bonus, Reacao, Lendaria, Covil }
public enum AcaoBasica2014 { Esquivar, Desengajar, Disparada, Ajudar, Preparar, Esconder }
public enum SistemaArea2014 { AlvosMarcados, Raio, Linha, Cone }
public enum Condicao2014
{
    Agarrado, Amedrontado, Atordoado, Caido, Cego, Enfeiticado, Envenenado,
    Exausto, Impedido, Incapacitado, Inconsciente, Invisivel, Paralisado, Petrificado, Surdo
}

public sealed record ParcelaDano2014(string Expressao, string? Tipo);

public static class RegrasCombate2014
{
    private static readonly Regex TermoDano = new(
        @"\G(?<sinal>[+-]?)(?:(?<quantidade>[0-9]{0,2})d(?<faces>[0-9]{1,3})|(?<fixo>[0-9]{1,5}))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static int RolarD20(Rolagem2014 modo, out int primeiro, out int? segundo)
    {
        primeiro = Random.Shared.Next(1, 21);
        segundo = modo == Rolagem2014.Normal ? null : Random.Shared.Next(1, 21);
        return modo switch
        {
            Rolagem2014.Vantagem => Math.Max(primeiro, segundo ?? primeiro),
            Rolagem2014.Desvantagem => Math.Min(primeiro, segundo ?? primeiro),
            _ => primeiro
        };
    }

    public static Rolagem2014 CombinarRolagem(bool vantagem, bool desvantagem) => (vantagem, desvantagem) switch
    {
        (true, false) => Rolagem2014.Vantagem,
        (false, true) => Rolagem2014.Desvantagem,
        _ => Rolagem2014.Normal
    };

    public static int BonusCobertura(Cobertura2014 cobertura) => cobertura switch
    {
        Cobertura2014.Meia => 2,
        Cobertura2014.TresQuartos => 5,
        _ => 0
    };

    public static int AjustarDano(int dano, AfinidadeDano2014 afinidade) => afinidade switch
    {
        AfinidadeDano2014.Resistencia => dano / 2,
        AfinidadeDano2014.Imunidade => 0,
        AfinidadeDano2014.Vulnerabilidade => dano * 2,
        _ => dano
    };

    public static bool DanoValido(string? formula) => InterpretarDano(formula, out _, out _);

    public static decimal DistanciaMetros(int coluna, int linha, int outraColuna, int outraLinha) =>
        Math.Max(Math.Abs(coluna - outraColuna), Math.Abs(linha - outraLinha)) * 1.5m;

    public static bool TentarRolarDano(string? formula, bool critico, int bonus, out int dano)
    {
        dano = 0;
        if (!InterpretarDano(formula, out var grupos, out int fixo)) return false;

        int multiplicador = critico ? 2 : 1;
        dano = fixo + bonus;
        foreach (var (quantidade, faces) in grupos)
        {
            for (int i = 0; i < quantidade * multiplicador; i++)
                dano += Random.Shared.Next(1, faces + 1);
        }
        dano = Math.Max(0, dano);
        return true;
    }

    public static int? RegistrarDanoMorte(int dano, int pvMaximo, int pvAnterior, bool critico, EstadoCombatente2014 ficha)
    {
        if (dano <= 0) return null;
        if (dano - pvAnterior >= pvMaximo)
        {
            ficha.Estavel = false;
            ficha.FalhasMorte = 3;
            return 0;
        }

        if (pvAnterior > 0) return null;
        ficha.Estavel = false;
        ficha.FalhasMorte = Math.Min(3, ficha.FalhasMorte + (critico ? 2 : 1));
        return 0;
    }

    public static int RegistrarTesteMorte(int d20, EstadoCombatente2014 combatente)
    {
        if (combatente.Morto || combatente.Estavel) return 0;
        if (d20 == 20) { combatente.RestaurarMorte(); return 1; }
        if (d20 >= 10)
        {
            combatente.SucessosMorte++;
            if (combatente.SucessosMorte == 3)
            {
                combatente.RestaurarMorte();
                combatente.Estavel = true;
            }
        }
        else
        {
            combatente.FalhasMorte = Math.Min(3, combatente.FalhasMorte + (d20 == 1 ? 2 : 1));
        }
        return 0;
    }

    public static bool Incapacitado(EstadoCombatente2014? combatente) => combatente is not null &&
        (combatente.Morto || combatente.Efeitos.Any(e => e.Condicao is Condicao2014.Incapacitado or Condicao2014.Atordoado or
            Condicao2014.Paralisado or Condicao2014.Petrificado or Condicao2014.Inconsciente));

    private static bool InterpretarDano(string? dano, out List<(int Quantidade, int Faces)> grupos, out int fixo)
    {
        grupos = [];
        fixo = 0;
        if (string.IsNullOrWhiteSpace(dano) || dano.Length > 192) return false;

        string formula = string.Concat(dano.Where(c => !char.IsWhiteSpace(c))).Replace('−', '-');
        int posicao = 0;
        int termos = 0;
        foreach (Match termo in TermoDano.Matches(formula))
        {
            string sinal = termo.Groups["sinal"].Value;
            if (++termos > 20 || (posicao > 0 && sinal.Length == 0)) return false;
            posicao += termo.Length;

            if (termo.Groups["fixo"].Success)
            {
                int valor = int.Parse(termo.Groups["fixo"].ValueSpan);
                fixo += sinal == "-" ? -valor : valor;
                continue;
            }

            int faces = int.Parse(termo.Groups["faces"].ValueSpan);
            if (faces < 2 || sinal == "-") return false;
            int quantidade = int.TryParse(termo.Groups["quantidade"].ValueSpan, out int dados) ? Math.Clamp(dados, 1, 20) : 1;
            grupos.Add((quantidade, Math.Min(faces, 100)));
        }

        return termos > 0 && posicao == formula.Length;
    }
}
