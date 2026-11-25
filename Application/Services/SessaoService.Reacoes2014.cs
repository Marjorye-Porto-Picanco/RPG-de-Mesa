using RPGdeMesa.Domain.Combate;
using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Application.Services;

public partial class SessaoService
{
    public Task<(bool Sucesso, string Mensagem)> MoverComReacoes2014Async(SessaoEstadoDto estado, int conta,
        int idPino, int coluna, int linha, bool desengajando, bool disparada = false) =>
        RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
            ResolverMovimento2014(estado, conta, encontro, pinos, idPino, coluna, linha, desengajando, disparada),
            incluirJogador: true);

    private static ResultadoComandoCombate2014 ResolverMovimento2014(SessaoEstadoDto estado, int conta, Encontro2014 encontro,
        IReadOnlyList<SessaoPino> pinos, int idPino, int coluna, int linha, bool desengajando, bool disparada)
    {
            var pino = ExigirPino(estado, pinos, idPino);
            if (encontro.Rodada == 0 || encontro.IdPinoAtual != idPino || encontro.MovimentoPendente != null || encontro.JanelaLendaria ||
                (!estado.EhMestre && (estado.IdAccountTurno != conta || pino.IdEntidade != estado.IdPersonagemLogado ||
                    pino.TipoEntidade != TiposPinoSessao.Personagem)) || coluna is < 0 or > 19 || linha is < 0 or > 19)
                throw new DomainException("Este movimento não está disponível no turno atual.");

            var ficha = encontro.Ficha(idPino);
            if (PvAtual(pino) <= 0 || ficha.SurpresoNesteTurno || RegrasCombate2014.Incapacitado(ficha) ||
                ficha.Tem(Condicao2014.Agarrado) || ficha.Tem(Condicao2014.Impedido))
                throw new DomainException("Este combatente não pode se deslocar.");

            decimal distancia = RegrasCombate2014.DistanciaMetros(pino.Coluna, pino.Linha, coluna, linha);
            if (ficha.Tem(Condicao2014.Caido)) distancia *= 2;
            bool iraDisparar = ficha.Disparada || disparada;
            if (ficha.DeslocamentoGasto + distancia > pino.DeslocamentoMetros * (iraDisparar ? 2 : 1))
                throw new DomainException("O destino excede o deslocamento restante neste turno.");

            if (disparada || desengajando)
            {
                ConsumirAcao(encontro, pino, TipoAcao2014.Acao);
                ficha.Disparada |= disparada;
                ficha.Desengajando |= desengajando;
            }

            ficha.DeslocamentoGasto += distancia;

            var reacoes = new List<int>();
            if (!ficha.Desengajando && !pino.Escondido && !ficha.Tem(Condicao2014.Invisivel))
            {
                foreach (var candidato in estado.Pinos.Where(p => p.IdMapa == estado.IdMapaAtivo && p.IdPino != idPino &&
                    p.TipoEntidade is TiposPinoSessao.Monstro or TiposPinoSessao.Npc && PvAtual(p) > 0 && pinos.Any(x => x.IdPino == p.IdPino)))
                {
                    var outro = encontro.Ficha(candidato.IdPino);
                    if (outro.ReacaoUsada || RegrasCombate2014.Incapacitado(outro) || outro.Tem(Condicao2014.Cego))
                        continue;

                    if (ObterAtaquesEntidade(estado, candidato).Any(a => a.CorpoACorpo && a.TemFormulaDano &&
                        ProvocouOportunidade2014(candidato, pino, coluna, linha, a)))
                        reacoes.Add(candidato.IdPino);
                }
            }

            var movimento = new MovimentoPendente2014(idPino, pino.Coluna, pino.Linha, coluna, linha, reacoes);
            if (reacoes.Count == 0)
                return new($"{pino.Nome} se moveu.", MovimentoConfirmado: movimento);

            encontro.MovimentoPendente = movimento;
            return new($"Movimento de {pino.Nome} aguardando decisão de reação.");
    }

    public Task<(bool Sucesso, string Mensagem)> DispensarOportunidade2014Async(SessaoEstadoDto estado, int conta, int idAtor) =>
        RegistrarComandoCombateAsync(estado, conta, (_, encontro, _) =>
        {
            var movimento = encontro.MovimentoPendente;
            if (movimento is null || !movimento.ReacoesPendentes.Contains(idAtor))
                throw new DomainException("A reação já foi resolvida.");

            var confirmado = FinalizarReacao2014(encontro, idAtor, true);
            return new(confirmado is null ? "Reação dispensada." : "Reação dispensada e movimento concluído.", MovimentoConfirmado: confirmado);
        });

    private static MovimentoPendente2014? FinalizarReacao2014(Encontro2014 encontro, int atacante, bool podeMover)
    {
        var movimento = encontro.MovimentoPendente!;
        movimento.ReacoesPendentes.Remove(atacante);
        if (podeMover && movimento.ReacoesPendentes.Count > 0)
            return null;

        encontro.MovimentoPendente = null;
        return podeMover ? movimento : null;
    }

    private static bool ProvocouOportunidade2014(PinoMapaDto atacante, PinoMapaDto alvo, int coluna, int linha, AtaqueEntidadeDto ataque)
    {
        decimal alcance = RegrasMagiaCombate.InterpretarAlcance(ataque.Alcance).MetrosMaximos ?? 1.5m;
        return DistanciaPinos(atacante, alvo) <= alcance &&
            RegrasCombate2014.DistanciaMetros(atacante.Coluna, atacante.Linha, coluna, linha) > alcance;
    }
}
