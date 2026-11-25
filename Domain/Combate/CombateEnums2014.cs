namespace RPGdeMesa.Domain.Combate;

public enum Rolagem2014 { Normal, Vantagem, Desvantagem }
public enum Cobertura2014 { Nenhuma, Meia, TresQuartos, Total }
public enum AfinidadeDano2014 { Normal, Resistencia, Imunidade, Vulnerabilidade }
public enum Momento2014 { InicioTurno, FimTurno, Manual }
public enum Atributo2014 { Forca, Destreza, Constituicao, Inteligencia, Sabedoria, Carisma }
public enum TipoAcao2014 { Acao, Bonus, Reacao, Lendaria, Covil }
public enum AcaoBasica2014 { Esquivar, Desengajar, Disparada, Ajudar, Preparar, Esconder }
public enum SistemaArea2014 { AlvosMarcados, Raio, Linha, Cone }
public enum ResolucaoHabilidade2014 { Salvaguarda, AtaqueMagico, Dano, Cura, Efeito }

public enum Condicao2014
{
    Agarrado,
    Amedrontado,
    Atordoado,
    Caido,
    Cego,
    Enfeiticado,
    Envenenado,
    Exausto,
    Impedido,
    Incapacitado,
    Inconsciente,
    Invisivel,
    Paralisado,
    Petrificado,
    Surdo
}

public sealed record ParcelaDano2014(string Expressao, string? Tipo);