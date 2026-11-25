using RPGdeMesa.Domain.Combate;
using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Application.Services;

public partial class SessaoService
{
    public Task<(bool Sucesso, string Mensagem)> ProporMovimentoTabuleiroAsync(
        SessaoEstadoDto estado, int conta, int idPino, CasaTabuleiro origem, CasaTabuleiro destino) =>
        RegistrarComandoCombateAsync(estado, conta, (controle, encontro, pinos) =>
        {
            if (controle.Tabuleiro is not null || encontro.MovimentoPendente is not null)
                return new() { Erro = "Aguarde a resolução do movimento anterior." };
            if (!origem.Valida || !destino.Valida || origem == destino)
                return new() { Erro = "As casas informadas pelo tabuleiro são inválidas." };

            var pino = ExigirPino(estado, pinos, idPino);
            if (pino.TipoEntidade != TiposPinoSessao.Personagem || new CasaTabuleiro(pino.Coluna, pino.Linha) != origem)
                return new() { Erro = "O pino virtual saiu da casa de origem. Confira o vínculo da peça." };

            var movimento = new MovimentoTabuleiro(Guid.NewGuid(), estado.IdMapaAtivo!.Value, idPino, pino.IdEntidade,
                pino.Nome, origem, destino, encontro.Rodada, FaseMovimentoTabuleiro.AprovacaoMestre);
            if (DestinoOcupado(pinos, movimento))
                return PedirRetornoTabuleiro(controle, movimento, "O destino já está ocupado.");

            if (!TurnoDoPino(encontro, movimento))
            {
                controle.Tabuleiro = movimento;
                return new($"Aguardando aprovação do Mestre para o movimento de {pino.Nome}.");
            }

            var ficha = encontro.Ficha(idPino);
            decimal distancia = RegrasCombate2014.DistanciaMetros(origem.Coluna, origem.Linha, destino.Coluna, destino.Linha);
            if (ficha.Tem(Condicao2014.Caido)) distancia *= 2;
            decimal depois = ficha.DeslocamentoGasto + distancia;
            if (depois <= pino.DeslocamentoMetros * (ficha.Disparada ? 2 : 1))
                return AplicarMovimentoTabuleiro(estado, conta, controle, encontro, pinos, movimento);

            if (!ficha.Disparada && !ficha.AcaoUsada && depois <= pino.DeslocamentoMetros * 2 &&
                PvAtual(pino) > 0 && !ficha.SurpresoNesteTurno && !RegrasCombate2014.Incapacitado(ficha) &&
                !ficha.Tem(Condicao2014.Agarrado) && !ficha.Tem(Condicao2014.Impedido))
            {
                controle.Tabuleiro = movimento with { Fase = FaseMovimentoTabuleiro.ConfirmacaoDisparada };
                return new($"Aguardando {pino.Nome} decidir se usa Disparada.");
            }

            return PedirRetornoTabuleiro(controle, movimento, "O destino ultrapassa o deslocamento disponível.");
        }, comandoTabuleiro: true);

    public Task<(bool Sucesso, string Mensagem)> DecidirMovimentoTabuleiroAsync(
        SessaoEstadoDto estado, int conta, Guid id, bool aceitar, bool decidirDisparada) =>
        RegistrarComandoCombateAsync(estado, conta, (controle, encontro, pinos) =>
        {
            var fase = decidirDisparada ? FaseMovimentoTabuleiro.ConfirmacaoDisparada : FaseMovimentoTabuleiro.AprovacaoMestre;
            if (controle.Tabuleiro is not { } movimento || movimento.Id != id || movimento.Fase != fase ||
                (decidirDisparada && (estado.IdPersonagemLogado != movimento.IdPersonagem || !pinos.Any(p =>
                    p.IdPino == movimento.IdPino && p.IdEntidade == estado.IdPersonagemLogado &&
                    p.TipoEntidade == TiposPinoSessao.Personagem))))
                return new() { Erro = "Esta decisão não está mais disponível nesta tela." };

            if (!aceitar)
                return PedirRetornoTabuleiro(controle, movimento, decidirDisparada ? "O jogador recusou a Disparada." : "O Mestre recusou o movimento.");
            if (!pinos.Any(p => p.IdPino == movimento.IdPino && p.Coluna == movimento.Origem.Coluna && p.Linha == movimento.Origem.Linha) ||
                DestinoOcupado(pinos, movimento))
                return PedirRetornoTabuleiro(controle, movimento, "As posições no mapa mudaram enquanto a decisão estava pendente.");

            if (decidirDisparada)
            {
                if (!TurnoDoPino(encontro, movimento) || encontro.Rodada != movimento.Rodada ||
                    estado.IdAccountTurno != conta || encontro.Ficha(movimento.IdPino).Disparada ||
                    encontro.Ficha(movimento.IdPino).AcaoUsada)
                    return PedirRetornoTabuleiro(controle, movimento, "O turno ou a ação de Disparada já mudou.");

                return AplicarMovimentoTabuleiro(estado, conta, controle, encontro, pinos, movimento with { Disparada = true });
            }

            if (TurnoDoPino(encontro, movimento))
                return PedirRetornoTabuleiro(controle, movimento, "O turno do jogador começou. Volte à origem para mover no próprio turno.");

            controle.Tabuleiro = null;
            return new($"{movimento.Jogador} se moveu com autorização do Mestre.",
                MovimentoConfirmado: new(movimento.IdPino, movimento.Origem.Coluna, movimento.Origem.Linha,
                    movimento.Destino.Coluna, movimento.Destino.Linha, []));
        }, incluirJogador: decidirDisparada, comandoTabuleiro: true);

    public Task<(bool Sucesso, string Mensagem)> ConfirmarRetornoTabuleiroAsync(
        SessaoEstadoDto estado, int conta, Guid id) =>
        RegistrarComandoCombateAsync(estado, conta, (controle, encontro, pinos) =>
        {
            if (controle.Tabuleiro is not { Fase: FaseMovimentoTabuleiro.Retorno } movimento || movimento.Id != id)
                return new() { Erro = "Esta volta já foi confirmada." };

            controle.Tabuleiro = null;
            var resultado = new ResultadoComandoCombate2014($"A peça de {movimento.Jogador} voltou para {movimento.Origem}. Mesa liberada.");
            return EncerrarTurnoDepoisDaDisparada(estado, encontro, pinos, movimento, resultado);
        }, comandoTabuleiro: true);

    private static bool TurnoDoPino(Encontro2014 encontro, MovimentoTabuleiro movimento) =>
        encontro.Rodada > 0 && !encontro.JanelaLendaria && encontro.IdPinoAtual == movimento.IdPino;

    private static bool DestinoOcupado(IReadOnlyList<SessaoPino> pinos, MovimentoTabuleiro movimento) =>
        pinos.Any(p => p.IdPino != movimento.IdPino && p.Coluna == movimento.Destino.Coluna && p.Linha == movimento.Destino.Linha);

    private static ResultadoComandoCombate2014 PedirRetornoTabuleiro(
        ControleCombate2014 controle, MovimentoTabuleiro movimento, string motivo)
    {
        controle.Tabuleiro = movimento with { Fase = FaseMovimentoTabuleiro.Retorno, Motivo = motivo };
        return new($"{motivo} {movimento.Jogador} deve voltar para {movimento.Origem}.");
    }

    private static ResultadoComandoCombate2014 AplicarMovimentoTabuleiro(SessaoEstadoDto estado, int conta,
        ControleCombate2014 controle, Encontro2014 encontro, IReadOnlyList<SessaoPino> pinos, MovimentoTabuleiro movimento)
    {
        ResultadoComandoCombate2014 resultado;
        try
        {
            resultado = ResolverMovimento2014(estado, conta, encontro, pinos, movimento.IdPino,
                movimento.Destino.Coluna, movimento.Destino.Linha, desengajando: false, disparada: movimento.Disparada);
        }
        catch (DomainException erro)
        {
            return PedirRetornoTabuleiro(controle, movimento with { Disparada = false }, erro.Message);
        }

        if (encontro.MovimentoPendente is not null)
        {
            controle.Tabuleiro = movimento with { Fase = FaseMovimentoTabuleiro.Reacoes };
            return resultado;
        }

        controle.Tabuleiro = null;
        return EncerrarTurnoDepoisDaDisparada(estado, encontro, pinos, movimento, resultado);
    }

    private static ResultadoComandoCombate2014 ConcluirReacoesTabuleiro(SessaoEstadoDto estado, ControleCombate2014 controle,
        Encontro2014 encontro, IReadOnlyList<SessaoPino> pinos, MovimentoTabuleiro movimento, ResultadoComandoCombate2014 resultado)
    {
        if (resultado.MovimentoConfirmado?.IdMovendo != movimento.IdPino)
        {
            var retorno = PedirRetornoTabuleiro(controle, movimento, "O movimento foi interrompido pela reação.");
            return resultado with { Mensagem = $"{resultado.Mensagem}\n{retorno.Mensagem}" };
        }

        controle.Tabuleiro = null;
        return EncerrarTurnoDepoisDaDisparada(estado, encontro, pinos, movimento, resultado);
    }

    private static ResultadoComandoCombate2014 EncerrarTurnoDepoisDaDisparada(SessaoEstadoDto estado, Encontro2014 encontro,
        IReadOnlyList<SessaoPino> pinos, MovimentoTabuleiro movimento, ResultadoComandoCombate2014 resultado)
    {
        if (!movimento.Disparada || encontro.Rodada != movimento.Rodada || !TurnoDoPino(encontro, movimento))
            return resultado;

        var turno = ConcluirTurno2014(estado, encontro, pinos);
        return resultado with { SincronizarTurno = true, Mensagem = $"{resultado.Mensagem}\n{turno.Mensagem}" };
    }
}
