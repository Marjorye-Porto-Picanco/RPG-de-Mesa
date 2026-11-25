using System.Globalization;
using System.Text.RegularExpressions;

namespace RPGdeMesa.Domain.Combate;

public enum TipoConjuracaoClasse
{
    Nenhum = 0,
    Completo = 1,
    Meio = 2,
    Pacto = 3
}

public enum CategoriaAlcanceMagia
{
    Pessoal,
    Toque,
    DistanciaMetros,
    Ilimitado
}

public enum CategoriaTempoConjuracao
{
    Acao,
    AcaoBonus,
    Reacao,
    LongoForaDeCombate
}

public enum TipoDuracaoMagia
{
    Instantanea,
    PorRodadas,
    LongaNarrativa
}

public enum ModoResolucaoMagia
{
    AtaqueMagico,
    Salvaguarda,
    Cura,
    Efeito
}

public sealed record AlcanceMagiaInfo(
    CategoriaAlcanceMagia Categoria,
    decimal? MetrosMaximos,
    string RotuloPtBr);

public sealed record TempoConjuracaoInfo(
    CategoriaTempoConjuracao Categoria,
    bool PermitidaEmCombate,
    string RotuloPtBr,
    int TurnosCarregamento = 0);

public sealed record DuracaoMagiaInfo(
    TipoDuracaoMagia Tipo,
    int? Rodadas,
    bool SugereConcentracao,
    string RotuloPtBr);

public sealed class MagiaSessaoItemDto
{
    public int IdMagia { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int CirculoNivel { get; set; }
    public string? Escola { get; set; }
    public string? TempoConjuracao { get; set; }
    public string? Alcance { get; set; }
    public string? Componentes { get; set; }
    public string? Duracao { get; set; }
    public string? DanoEfeito { get; set; }
    public AlcanceMagiaInfo AlcanceInfo { get; set; } = new(CategoriaAlcanceMagia.Ilimitado, null, "Livre");
    public TempoConjuracaoInfo TempoInfo { get; set; } = new(CategoriaTempoConjuracao.Acao, true, "1 Ação");
    public DuracaoMagiaInfo DuracaoInfo { get; set; } = new(TipoDuracaoMagia.Instantanea, 0, false, "Instantânea");
}

public sealed class ResultadoConjuracaoDto
{
    public string NomeConjurador { get; set; } = string.Empty;
    public string NomeMagia { get; set; } = string.Empty;
    public int CirculoBase { get; set; }
    public int CirculoUsado { get; set; }
    public ModoResolucaoMagia Modo { get; set; }
    public string DuracaoTexto { get; set; } = string.Empty;
    public bool Concentracao { get; set; }
    public string? TipoAlvo { get; set; }
    public int IdEntidadeAlvo { get; set; }
    public int? IdPinoAlvo { get; set; }
    public string NomeAlvo { get; set; } = string.Empty;
    public int CaAlvo { get; set; }
    public bool AlvoEsquivando { get; set; }
    public int DadoD201 { get; set; }
    public int DadoD202 { get; set; }
    public int DadoD20 { get; set; }
    public int ModificadorRolagem { get; set; }
    public int TotalRolagem { get; set; }
    public int CdAlvo { get; set; }
    public string AtributoSalvaguarda { get; set; } = "Destreza";
    public bool EhCritico { get; set; }
    public bool EhFalhaCritica { get; set; }
    public bool Acertou { get; set; }
    public bool AlvoPassouSalvaguarda { get; set; }
    public bool MeioDanoSePassar { get; set; }
    public string ExpressaoDado { get; set; } = string.Empty;
    public List<int> ResultadosDado { get; set; } = new();
    public int BonusExtra { get; set; }
    public int ValorTotal { get; set; }
    public int PvAnterior { get; set; }
    public int PvNovo { get; set; }
    public int PvMaximo { get; set; }
}

public static class RegrasMagiaCombate
{
    private static readonly int[][] TabelaConjuradorCompleto =
    [
        [2],
        [3],
        [4, 2],
        [4, 3],
        [4, 3, 2],
        [4, 3, 3],
        [4, 3, 3, 1],
        [4, 3, 3, 2],
        [4, 3, 3, 3, 1],
        [4, 3, 3, 3, 2],
        [4, 3, 3, 3, 2, 1],
        [4, 3, 3, 3, 2, 1],
        [4, 3, 3, 3, 2, 1, 1],
        [4, 3, 3, 3, 2, 1, 1],
        [4, 3, 3, 3, 2, 1, 1, 1],
        [4, 3, 3, 3, 2, 1, 1, 1],
        [4, 3, 3, 3, 2, 1, 1, 1, 1],
        [4, 3, 3, 3, 3, 1, 1, 1, 1],
        [4, 3, 3, 3, 3, 2, 1, 1, 1],
        [4, 3, 3, 3, 3, 2, 2, 1, 1]
    ];

    public static int CalcularBonusProficiencia(int nivel) =>
        2 + (Math.Clamp(nivel, 1, 20) - 1) / 4;

    public static TipoConjuracaoClasse IdentificarTipoConjuracao(string? nomeClasse)
    {
        string classe = (nomeClasse ?? string.Empty).Trim().ToLowerInvariant();

        if (classe.Contains("bruxo") || classe.Contains("warlock"))
            return TipoConjuracaoClasse.Pacto;

        if (classe.Contains("paladino") || classe.Contains("paladin") ||
            classe.Contains("patrulheiro") || classe.Contains("ranger"))
            return TipoConjuracaoClasse.Meio;

        if (classe.Contains("bardo") || classe.Contains("bard") ||
            classe.Contains("clérigo") || classe.Contains("clerigo") || classe.Contains("cleric") ||
            classe.Contains("druida") || classe.Contains("druid") ||
            classe.Contains("feiticeiro") || classe.Contains("sorcerer") ||
            classe.Contains("mago") || classe.Contains("wizard") ||
            classe.Contains("outra"))
            return TipoConjuracaoClasse.Completo;

        return TipoConjuracaoClasse.Nenhum;
    }

    public static Dictionary<int, int> ObterEspacosPorCirculo(TipoConjuracaoClasse tipo, int nivel)
    {
        nivel = Math.Clamp(nivel, 1, 20);
        var resultado = new Dictionary<int, int>();

        if (tipo == TipoConjuracaoClasse.Nenhum)
            return resultado;

        if (tipo == TipoConjuracaoClasse.Pacto)
        {
            int circuloPacto = Math.Min(5, (nivel + 1) / 2);
            int qtd = nivel switch
            {
                1 => 1,
                <= 10 => 2,
                <= 16 => 3,
                _ => 4
            };
            resultado[circuloPacto] = qtd;
            return resultado;
        }

        int nivelEfetivo = tipo switch
        {
            TipoConjuracaoClasse.Completo => nivel,
            TipoConjuracaoClasse.Meio => nivel < 2 ? 0 : (nivel + 1) / 2,
            _ => 0
        };

        if (nivelEfetivo == 0)
            return resultado;

        int[] linha = TabelaConjuradorCompleto[nivelEfetivo - 1];
        for (int i = 0; i < linha.Length; i++)
            resultado[i + 1] = linha[i];

        return resultado;
    }

    public static (int BonusAtaqueMagico, int CdResistir, string AtributoUsado) CalcularParametrosConjuracao(
        string? nomeClasse,
        int nivel,
        int inteligencia,
        int sabedoria,
        int carisma)
    {
        int prof = CalcularBonusProficiencia(nivel);
        string classe = (nomeClasse ?? string.Empty).Trim().ToLowerInvariant();

        (string attr, int valor) = classe switch
        {
            var c when c.Contains("mago") || c.Contains("wizard") => ("INT", inteligencia),
            var c when c.Contains("clérigo") || c.Contains("clerigo") || c.Contains("cleric") ||
                       c.Contains("druida") || c.Contains("druid") ||
                       c.Contains("patrulheiro") || c.Contains("ranger") => ("SAB", sabedoria),
            var c when c.Contains("bardo") || c.Contains("bard") ||
                       c.Contains("bruxo") || c.Contains("warlock") ||
                       c.Contains("feiticeiro") || c.Contains("sorcerer") ||
                       c.Contains("paladino") || c.Contains("paladin") => ("CAR", carisma),
            _ => inteligencia >= sabedoria && inteligencia >= carisma
                ? ("INT", inteligencia)
                : sabedoria >= carisma
                    ? ("SAB", sabedoria)
                    : ("CAR", carisma)
        };

        int mod = (int)Math.Floor((valor - 10) / 2.0);
        return (prof + mod, 8 + prof + mod, attr);
    }

    public static AlcanceMagiaInfo InterpretarAlcance(string? alcanceRaw)
    {
        if (string.IsNullOrWhiteSpace(alcanceRaw))
            return new(CategoriaAlcanceMagia.Ilimitado, null, "Livre");

        string txt = alcanceRaw.Trim().ToLowerInvariant();

        if (txt.StartsWith("self") || txt.StartsWith("pessoal"))
            return new(CategoriaAlcanceMagia.Pessoal, 0m, "Pessoal");

        if (txt.StartsWith("touch") || txt.StartsWith("toque"))
            return new(CategoriaAlcanceMagia.Toque, 1.5m, "Toque (1,5 m)");

        if (txt.Contains("sight") || txt.Contains("unlimited") || txt.Contains("visão") || txt.Contains("ilimitado"))
            return new(CategoriaAlcanceMagia.Ilimitado, null, "Visão / Ilimitado");

        var match = Regex.Match(txt.Replace(',', '.'), @"(\d+(?:\.\d+)?)");
        if (!match.Success || !decimal.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal valor))
            return new(CategoriaAlcanceMagia.Ilimitado, null, alcanceRaw.Trim());

        decimal metros = txt.Contains("mile") || txt.Contains("milha")
            ? valor * 1609m
            : txt.Contains("feet") || txt.Contains("foot") || txt.Contains("ft") || txt.Contains("pés") || txt.Contains("pes")
                ? Math.Round(valor * 0.3m, 1)
                : valor;

        string rotulo = $"{metros.ToString("0.#", new CultureInfo("pt-BR"))} m";
        return new(CategoriaAlcanceMagia.DistanciaMetros, metros, rotulo);
    }

    public static TempoConjuracaoInfo InterpretarTempo(string? tempoRaw)
    {
        if (string.IsNullOrWhiteSpace(tempoRaw))
            return new(CategoriaTempoConjuracao.Acao, true, "1 Ação");

        string txt = tempoRaw.Trim().ToLowerInvariant();

        if (txt.Contains("bonus") || txt.Contains("bônus"))
            return new(CategoriaTempoConjuracao.AcaoBonus, true, "1 Ação Bônus");

        if (txt.Contains("reaction") || txt.Contains("reação"))
            return new(CategoriaTempoConjuracao.Reacao, true, "1 Reação");

        if (txt.Contains("action") || txt.Contains("ação"))
            return new(CategoriaTempoConjuracao.Acao, true, "1 Ação");

        if (txt.Contains("minute") || txt.Contains("minuto"))
        {
            var match = Regex.Match(txt, @"(\d+)");
            int minutos = match.Success ? Math.Max(1, int.Parse(match.Value)) : 1;
            return new(CategoriaTempoConjuracao.LongoForaDeCombate, false, tempoRaw.Trim(), minutos * 10);
        }

        if (txt.Contains("hour") || txt.Contains("hora"))
            return new(CategoriaTempoConjuracao.LongoForaDeCombate, false, tempoRaw.Trim(), 600);

        return new(CategoriaTempoConjuracao.LongoForaDeCombate, false, tempoRaw.Trim());
    }

    public static DuracaoMagiaInfo InterpretarDuracao(string? duracaoRaw)
    {
        if (string.IsNullOrWhiteSpace(duracaoRaw))
            return new(TipoDuracaoMagia.Instantanea, 0, false, "Instantânea");

        string txt = duracaoRaw.Trim().ToLowerInvariant();
        bool concentracao = txt.StartsWith("up to") || txt.StartsWith("até");

        if (txt.Contains("instant"))
            return new(TipoDuracaoMagia.Instantanea, 0, false, "Instantânea");

        var match = Regex.Match(txt, @"(\d+)");
        int valor = match.Success ? int.Parse(match.Groups[1].Value) : 1;

        if (txt.Contains("round") || txt.Contains("rodada"))
            return new(TipoDuracaoMagia.PorRodadas, valor, concentracao, $"{valor} rodada(s)");

        if (txt.Contains("minute") || txt.Contains("minuto"))
        {
            int rodadas = valor * 10;
            return new(TipoDuracaoMagia.PorRodadas, rodadas, concentracao, $"{valor} min ({rodadas} rodadas)");
        }

        if (txt.Contains("hour") || txt.Contains("hora"))
            return new(TipoDuracaoMagia.LongaNarrativa, null, concentracao, $"{valor} hora(s)");

        if (txt.Contains("day") || txt.Contains("dia"))
            return new(TipoDuracaoMagia.LongaNarrativa, null, concentracao, $"{valor} dia(s)");

        return new(TipoDuracaoMagia.LongaNarrativa, null, concentracao, duracaoRaw.Trim());
    }

    public static ModoResolucaoMagia InferirModoResolucao(MagiaSessaoItemDto magia)
    {
        string nome = magia.Nome.ToLowerInvariant();
        string dano = (magia.DanoEfeito ?? string.Empty).ToLowerInvariant();

        if (nome.Contains("cure") || nome.Contains("heal") || nome.Contains("cura") || nome.Contains("healing") || nome.Contains("restoration"))
            return ModoResolucaoMagia.Cura;

        if (magia.AlcanceInfo.Categoria == CategoriaAlcanceMagia.Pessoal)
            return ModoResolucaoMagia.Efeito;

        if (nome.Contains("splash") || nome.Contains("fireball") || nome.Contains("bola de fogo") ||
            nome.Contains("burning hands") || nome.Contains("lightning") || nome.Contains("sacred flame") ||
            dano.Contains("save") || dano.Contains("salvaguarda"))
            return ModoResolucaoMagia.Salvaguarda;

        if (!string.IsNullOrWhiteSpace(ExtrairPrimeiraExpressaoDado(magia.DanoEfeito)) ||
            nome.Contains("arrow") || nome.Contains("bolt") || nome.Contains("ray") || nome.Contains("missile"))
            return ModoResolucaoMagia.AtaqueMagico;

        return ModoResolucaoMagia.Efeito;
    }

    public static string ExtrairPrimeiraExpressaoDado(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        var match = Regex.Match(texto, @"\b(\d+d\d+(?:\s*[+-]\s*\d+)?)\b", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Replace(" ", "") : string.Empty;
    }
}