namespace RPGdeMesa.Domain.Sessoes;

public sealed record SessaoSinal(
    bool Ativa,
    string? Titulo,
    int? IdMapa,
    int? IdTurno,
    DateTime DataAtualizacao,
    int UltimaMensagem,
    List<ParticipanteSinal> Participantes,
    List<PinoSinal> Pinos)
{
    public bool TemMesmoTabuleiro(SessaoSinal anterior) =>
        Ativa == anterior.Ativa && Titulo == anterior.Titulo &&
        IdMapa == anterior.IdMapa && IdTurno == anterior.IdTurno && DataAtualizacao == anterior.DataAtualizacao &&
        Participantes.SequenceEqual(anterior.Participantes) && Pinos.SequenceEqual(anterior.Pinos);
}

public sealed record ParticipanteSinal(
    int IdAccount, int? IdPersonagem, bool Online, bool Esquivando, bool Escondido,
    int? PvAtual, int? PvMaximo, int? Destreza);

public sealed record PinoSinal(int Id, int? Mapa, string Tipo, int Entidade, int Coluna, int Linha, bool Visivel, int? Pv);
