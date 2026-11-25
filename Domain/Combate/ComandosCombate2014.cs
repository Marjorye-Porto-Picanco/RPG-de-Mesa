namespace RPGdeMesa.Domain.Combate;

public sealed record GolpeMestre2014(
    int IdAtaque,
    int IdAlvo,
    Rolagem2014 Rolagem = Rolagem2014.Normal,
    Cobertura2014 Cobertura = Cobertura2014.Nenhuma,
    decimal? Alcance = null,
    IReadOnlyList<ParcelaDano2014>? DanoAdicional = null,
    EfeitoCombate2014? Efeito = null,
    bool UsarResistenciaLendaria = false);

public sealed record UsoAcao2014(
    TipoAcao2014 Acao = TipoAcao2014.Acao,
    string? Recurso = null,
    int CustoLendario = 1,
    bool Oportunidade = false);

public sealed record HabilidadeMestre2014(
    string Nome,
    int IdOrigem,
    IReadOnlyList<int> Alvos,
    UsoAcao2014 Acao,
    ResolucaoHabilidade2014 Resolucao,
    string? Dano,
    string? TipoDano,
    Atributo2014 Atributo,
    int Cd,
    bool MeioDano,
    SistemaArea2014 Area = SistemaArea2014.AlvosMarcados,
    decimal Tamanho = 0,
    int Coluna = 0,
    int Linha = 0,
    EfeitoCombate2014? Efeito = null,
    bool Concentracao = false,
    int? RodadasConcentracao = null,
    bool UsarResistenciaLendaria = false,
    int BonusAtaque = 0,
    int ModificadorSalvaguarda = 0,
    Rolagem2014 Rolagem = Rolagem2014.Normal,
    Cobertura2014 Cobertura = Cobertura2014.Nenhuma,
    decimal? Alcance = null);

public sealed record AtaqueEntidadeDto(
    int Id,
    string Nome,
    string? Dano,
    string? TipoDano,
    int BonusAtaque,
    int BonusDano,
    string? Alcance = null,
    bool CorpoACorpo = true)
{
    public bool TemFormulaDano => RegrasCombate2014.DanoValido(Dano);
}