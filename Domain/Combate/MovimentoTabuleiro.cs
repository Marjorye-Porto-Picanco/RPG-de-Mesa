namespace RPGdeMesa.Domain.Combate;

public enum FaseMovimentoTabuleiro
{
    AprovacaoMestre,
    ConfirmacaoDisparada,
    Reacoes,
    Retorno
}

public readonly record struct CasaTabuleiro(int Coluna, int Linha)
{
    public bool Valida => Coluna is >= 0 and < 20 && Linha is >= 0 and < 20;
    public override string ToString() => $"{(char)('A' + Coluna)}{Linha + 1}";
}

public sealed record MovimentoTabuleiro(
    Guid Id, int IdMapa, int IdPino, int IdPersonagem, string Jogador,
    CasaTabuleiro Origem, CasaTabuleiro Destino, int Rodada, FaseMovimentoTabuleiro Fase,
    bool Disparada = false, string? Motivo = null);
