using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Common;

namespace RPGdeMesa.Domain.Sessoes;

public static class TiposPinoSessao
{
    public const string Personagem = "Personagem";
    public const string Npc = "NPC";
    public const string Monstro = "Monstro";
}

public class SessaoPino
{
    public int IdPino { get; private set; }
    public int IdSessao { get; private set; }
    public int? IdMapa { get; private set; }
    public string TipoEntidade { get; private set; } = string.Empty;
    public int IdEntidade { get; private set; }
    public int Coluna { get; private set; }
    public int Linha { get; private set; }
    public bool Visivel { get; private set; } = true;
    public int? PvAtual { get; private set; }

    public virtual SessaoMesa Sessao { get; private set; } = null!;
    public virtual Mapa? Mapa { get; private set; }

    protected SessaoPino() { }

    public SessaoPino(
        int idSessao,
        int? idMapa,
        string tipoEntidade,
        int idEntidade,
        int coluna,
        int linha,
        bool visivel = true,
        int? pvAtual = null)
    {
        if (idSessao <= 0)
            throw new DomainException("Sessão inválida.");
        if (string.IsNullOrWhiteSpace(tipoEntidade))
            throw new DomainException("Tipo de pino inválido.");
        if (idEntidade <= 0)
            throw new DomainException("Entidade inválida para o pino.");

        IdSessao = idSessao;
        IdMapa = idMapa;
        TipoEntidade = tipoEntidade.Trim();
        IdEntidade = idEntidade;
        Coluna = Math.Clamp(coluna, 0, 19);
        Linha = Math.Clamp(linha, 0, 19);
        Visivel = visivel;
        PvAtual = pvAtual.HasValue ? Math.Max(0, pvAtual.Value) : null;
    }

    public void MoverPara(int coluna, int linha)
    {
        Coluna = Math.Clamp(coluna, 0, 19);
        Linha = Math.Clamp(linha, 0, 19);
    }

    public void DefinirVisibilidade(bool visivel)
    {
        Visivel = visivel;
    }

    public void DefinirPvAtual(int pvAtual)
    {
        PvAtual = Math.Max(0, pvAtual);
    }

    public int AplicarDano(int dano, int pvMaximo)
    {
        int atual = PvAtual ?? Math.Max(0, pvMaximo);
        PvAtual = Math.Max(0, atual - Math.Max(0, dano));
        return PvAtual.Value;
    }
}