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
    public string TipoEntidade { get; private set; } = string.Empty;
    public int IdEntidade { get; private set; }
    public int Coluna { get; private set; }
    public int Linha { get; private set; }
    public bool Visivel { get; private set; } = true;

    public virtual SessaoMesa Sessao { get; private set; } = null!;

    protected SessaoPino() { }

    public SessaoPino(int idSessao, string tipoEntidade, int idEntidade, int coluna, int linha, bool visivel = true)
    {
        if (idSessao <= 0)
            throw new DomainException("Sessão inválida.");
        if (string.IsNullOrWhiteSpace(tipoEntidade))
            throw new DomainException("Tipo de pino inválido.");
        if (idEntidade <= 0)
            throw new DomainException("Entidade inválida para o pino.");

        IdSessao = idSessao;
        TipoEntidade = tipoEntidade.Trim();
        IdEntidade = idEntidade;
        Coluna = Math.Clamp(coluna, 0, 19);
        Linha = Math.Clamp(linha, 0, 19);
        Visivel = visivel;
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
}