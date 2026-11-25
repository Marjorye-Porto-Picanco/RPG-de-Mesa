using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Sessoes;
using RPGdeMesa.Infrastructure.Persistence;

namespace RPGdeMesa.Infrastructure.Repositories;

public partial class SessaoRepository
{
    public Task AlterarCombate2014Async(int idSessao, int idMapa, int idConta, bool participantePermitido, Guid operacao,
        Func<ControleCombate2014, IReadOnlyList<SessaoPino>, ResultadoComandoCombate2014> alterar) =>
        ExecutarOperacaoAsync(idSessao, operacao, $"RPGdeMesa:combate:{idSessao}", async contexto =>
        {
            var sessao = await contexto.SessoesMesa.Include(s => s.Campanha)
                .FirstOrDefaultAsync(s => s.IdSessao == idSessao && s.Ativa && s.IdMapaAtivo == idMapa);
            if (sessao is null)
                throw new DomainException("A sessão ou o mapa ativo mudou.");

            if (sessao.Campanha.IdAccount != idConta && (!participantePermitido ||
                !await contexto.SessaoParticipantes.AnyAsync(p => p.IdSessao == idSessao && p.IdAccount == idConta)))
            {
                throw new DomainException("Esta operação de combate exige autorização do Mestre.");
            }

            var pinos = await contexto.SessaoPinos.AsNoTracking()
                .Where(p => p.IdSessao == idSessao && p.IdMapa == idMapa).ToListAsync();
            var controle = LerControleCombate2014(sessao);
            var resultado = alterar(controle, pinos);
            foreach (var pv in resultado.AlteracoesPv ?? [])
            {
                var alvo = pinos.Find(p => p.IdPino == pv.IdPino);
                if (alvo is null) throw new DomainException("O alvo saiu do mapa.");
                await AplicarPvCombateAsync(contexto, alvo, pv);
            }

            if (resultado.MovimentoConfirmado is { } move)
            {
                int movidos = await contexto.SessaoPinos.Where(p => p.IdPino == move.IdMovendo && p.IdSessao == idSessao &&
                    p.IdMapa == idMapa && p.Coluna == move.ColunaOrigem && p.Linha == move.LinhaOrigem)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.Coluna, move.Coluna).SetProperty(x => x.Linha, move.Linha));
                if (movidos == 0) throw new DomainException("A posição da criatura mudou durante a reação.");
            }

            if (resultado.SincronizarTurno)
            {
                var atual = pinos.Find(p => p.IdPino == controle.ObterMapa(idMapa).IdPinoAtual);
                int? dono = atual?.TipoEntidade == TiposPinoSessao.Personagem && !controle.ObterMapa(idMapa).JanelaLendaria &&
                    !controle.ObterMapa(idMapa).Ficha(atual.IdPino).SurpresoNesteTurno
                    ? await contexto.Personagens.Where(p => p.IdPersonagem == atual.IdEntidade)
                        .Select(p => (int?)p.IdAccount).FirstOrDefaultAsync()
                    : null;
                sessao.DefinirTurnoJogador(dono);
                if (dono.HasValue)
                {
                    var participante = await contexto.SessaoParticipantes.FirstOrDefaultAsync(p => p.IdSessao == idSessao && p.IdAccount == dono);
                    participante?.DefinirEsquiva(false);
                }
            }

            controle.ObterMapa(idMapa).RemoverEfeitosSemConcentracao();
            sessao.SalvarCombate2014(JsonSerializer.Serialize(controle));
            if (!string.IsNullOrWhiteSpace(resultado.Mensagem))
                AdicionarRegistroCombate(contexto, idSessao, resultado.Mensagem);
        });

    private static ControleCombate2014 LerControleCombate2014(SessaoMesa sessao) =>
        sessao.Combate2014Json is { Length: > 0 } json
            ? JsonSerializer.Deserialize<ControleCombate2014>(json) ?? new()
            : new();

    private static async Task AplicarPvCombateAsync(AppDbContext contexto, SessaoPino alvo, AlteracaoPvCombate2014 pv)
    {
        int alterados = alvo.TipoEntidade == TiposPinoSessao.Personagem
            ? await contexto.Personagens.Where(p => p.IdPersonagem == alvo.IdEntidade && p.PvAtual == pv.PvAnterior)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.PvAtual, pv.PvNovo))
            : await contexto.SessaoPinos.Where(p => p.IdPino == alvo.IdPino && (p.PvAtual ?? pv.PvMaximo) == pv.PvAnterior)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.PvAtual, (int?)pv.PvNovo));
        if (alterados == 0)
            throw new DomainException("Os PV do alvo mudaram. Confira o alvo e tente novamente.");
    }

    private static void AdicionarRegistroCombate(AppDbContext contexto, int idSessao, string mensagem) =>
        contexto.SessaoMensagens.AddRange(new SessaoMensagem(idSessao, null, CanaisSessao.Chat, mensagem),
            new SessaoMensagem(idSessao, null, CanaisSessao.Combate, mensagem));
}
