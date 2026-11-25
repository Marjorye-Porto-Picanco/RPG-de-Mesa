using RPGdeMesa.Domain.Combate;
using RPGdeMesa.Domain.Sessoes;
using RPGdeMesa.Domain.ValueObjects;

namespace RPGdeMesa.Application.Services;

public partial class SessaoService
{
    public static IEnumerable<AtaqueEntidadeDto> ObterAtaquesEntidade(SessaoEstadoDto estado, PinoMapaDto atacante)
    {
        if (atacante.TipoEntidade == TiposPinoSessao.Monstro)
        {
            var monstro = estado.MonstrosCampanha.Find(m => m.IdMonstro == atacante.IdEntidade);
            return monstro?.Ataques.Select(a => new AtaqueEntidadeDto(
                a.IdMonstroAtaque, a.Nome, a.Dano, a.TipoDano, a.BonusAtaque ?? 0, 0, a.Alcance,
                EhCorpoACorpo(a.Descricao, a.Alcance))) ?? [];
        }

        if (atacante.TipoEntidade != TiposPinoSessao.Npc ||
            estado.NpcsCampanha.Find(n => n.IdNpc == atacante.IdEntidade) is not { } npc)
            return [];

        int forca = AtributosBase.CalcularModificador(npc.Forca);
        int destreza = AtributosBase.CalcularModificador(npc.Destreza);
        int proficiencia = RegrasMagiaCombate.CalcularBonusProficiencia(npc.Nivel);

        var ataquesDeItens = npc.Itens.Where(x => x.Item is not null && !string.IsNullOrWhiteSpace(x.Item.Dano)).Select(x =>
        {
            var item = x.Item!;
            bool usaDestreza = item.Propriedades?.Contains("acuidade", StringComparison.OrdinalIgnoreCase) == true ||
                item.Propriedades?.Contains("distância", StringComparison.OrdinalIgnoreCase) == true;
            int atributo = usaDestreza ? Math.Max(forca, destreza) : forca;
            string propriedades = $"{item.Tipo} {item.Propriedades}";
            bool contato = EhCorpoACorpo(propriedades, null);
            bool alcanceEstendido = propriedades.Contains("alcance", StringComparison.OrdinalIgnoreCase) ||
                propriedades.Contains("reach", StringComparison.OrdinalIgnoreCase);
            string? alcance = contato ? (alcanceEstendido ? "3 m" : "1,5 m") : null;
            return new AtaqueEntidadeDto(x.IdItem, item.Nome, item.Dano, item.TipoDano, atributo + proficiencia, atributo, alcance, contato);
        });

        // Ataques personalizados de NPC aguardam suporte de persistência.
        // return ataquesDeItens.Concat(npc.Ataques.Where(a => a.IdNpcAtaque > 0).Select(a => new AtaqueEntidadeDto(
        //     -a.IdNpcAtaque, a.Nome, a.Dano, a.TipoDano, a.BonusAtaque ?? 0, 0, a.Alcance,
        //     EhCorpoACorpo(a.Descricao, a.Alcance))));
        return ataquesDeItens;
    }

    public Task<(bool Sucesso, string Mensagem)> AtacarComEntidadeAsync(SessaoEstadoDto estado, int conta,
        int atacante, int ataque, int alvo) =>
        ExecutarGolpes2014Async(estado, conta, atacante, [new(ataque, alvo)], new());

    private static bool EhCorpoACorpo(string? descricao, string? alcance)
    {
        string texto = descricao?.ToLowerInvariant() ?? "";
        if (texto.Contains("melee") || texto.Contains("corpo a corpo")) return true;
        return !texto.Contains("ranged") && !texto.Contains("distância") && !texto.Contains("distancia") &&
            alcance?.Contains('/') != true;
    }
}
