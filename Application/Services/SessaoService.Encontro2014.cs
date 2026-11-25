using Microsoft.Extensions.Logging;
using RPGdeMesa.Domain.Combate;
using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Application.Services;

public partial class SessaoService
{
    public Task<(bool Sucesso, string Mensagem)> ConfigurarIniciativaAsync(SessaoEstadoDto estado, int conta,
        IReadOnlyList<EntradaIniciativa2014> entradas, bool covil) =>
        RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
        {
            if (entradas is null || entradas.Count == 0 || entradas.DistinctBy(x => x.IdPino).Count() != entradas.Count)
                return new() { Erro = "Selecione pelo menos um combatente, sem repetir pinos na iniciativa." };

            if (entradas.Any(entrada => entrada.Desempate < 0))
                return new() { Erro = "O desempate da iniciativa precisa ser zero ou maior." };

            foreach (var entrada in entradas)
                ExigirPino(estado, pinos, entrada.IdPino);

            var aptos = CombatentesNoEncontro(estado, encontro, pinos);
            if (encontro.Rodada == 0 && !entradas.Any(entrada => aptos.Contains(entrada.IdPino)))
                return new() { Erro = "Marque pelo menos um combatente que possa participar do encontro." };

            encontro.Covil = covil;

            if (encontro.Rodada == 0)
            {
                encontro.Iniciar(entradas, aptos.Contains);
            }
            else
            {
                encontro.Ordem = encontro.OrdenarIniciativas(entradas);
                if (!encontro.Ordem.Any(e => e.IdPino == encontro.IdPinoAtual))
                    encontro.Avancar(aptos.Contains);
            }

            return new(DescreverTurno(estado, encontro), SincronizarTurno: true);
        });

    public Task<(bool Sucesso, string Mensagem)> AvancarIniciativaAsync(SessaoEstadoDto estado, int conta) =>
        RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
        {
            if (encontro.Rodada == 0 || encontro.IdPinoAtual != estado.Encontro.IdPinoAtual ||
                encontro.Rodada != estado.Encontro.Rodada ||
                (!estado.EhMestre && estado.IdAccountTurno != conta))
                return new() { Erro = "O turno atual mudou. Confira a rodada antes de avançar." };

            if (encontro.MovimentoPendente is not null)
                return new() { Erro = "Resolva ou dispense as reações antes de avançar." };

            return ConcluirTurno2014(estado, encontro, pinos);
        }, incluirJogador: true);

    private static ResultadoComandoCombate2014 ConcluirTurno2014(SessaoEstadoDto estado, Encontro2014 encontro, IReadOnlyList<SessaoPino> pinos)
    {
        var aptos = CombatentesNoEncontro(estado, encontro, pinos);
        bool janelaLendaria = encontro.Ordem.Any(e =>
            e.IdPino > 0 &&
            e.IdPino != encontro.IdPinoAtual &&
            encontro.Ficha(e.IdPino) is { UsosLendariosRestantes: > 0, SurpresaPendente: false } ficha &&
            !RegrasCombate2014.Incapacitado(ficha) &&
            estado.Pinos.Any(p => p.IdPino == e.IdPino && PvAtual(p) > 0));

        encontro.ConcluirTurno(janelaLendaria, aptos.Contains);
        return new(DescreverTurno(estado, encontro), SincronizarTurno: true);
    }

    public Task<(bool Sucesso, string Mensagem)> EncerrarIniciativaAsync(SessaoEstadoDto estado, int conta) =>
        RegistrarComandoCombateAsync(estado, conta, (_, encontro, _) =>
        {
            if (encontro.Rodada == 0)
                return new() { Erro = "O combate já foi encerrado." };

            encontro.Encerrar();
            return new("A iniciativa foi encerrada.", SincronizarTurno: true);
        });

    public Task<(bool Sucesso, string Mensagem)> AlterarCondicaoAsync(SessaoEstadoDto estado, int conta, int id,
        EfeitoCombate2014 efeito, bool remover) =>
        RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
        {
            if (!remover && CondicaoInvalida(efeito))
                return new() { Erro = "Confira a duração e a salvaguarda da condição." };

            var pino = ExigirPino(estado, pinos, id);
            var ficha = encontro.Ficha(id);
            ficha.Efeitos.RemoveAll(e => e.Condicao == efeito.Condicao && (remover || e.IdOrigem == efeito.IdOrigem));
            if (!remover)
                AplicarCondicao(ficha, efeito);
            return new($"{pino.Nome} {(remover ? "perdeu" : "recebeu")} a condição {efeito.Condicao}.");
        });

    public Task<(bool Sucesso, string Mensagem)> ConfigurarAfinidadeAsync(SessaoEstadoDto estado, int conta, int id,
        string tipo, AfinidadeDano2014 afinidade) =>
        RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
        {
            var pino = ExigirPino(estado, pinos, id);
            var chave = RegrasCombate2014.NormalizarTipoDano(tipo);
            if (chave.Length is < 1 or > 30)
                return new() { Erro = "Informe um tipo de dano com até 30 caracteres." };

            var afinidades = encontro.Ficha(id).Afinidades;
            if (afinidade == AfinidadeDano2014.Normal)
                afinidades.Remove(chave);
            else
                afinidades[chave] = afinidade;

            return new($"Afinidade de {pino.Nome} atualizada para {tipo.Trim()}.");
        });

    public Task<(bool Sucesso, string Mensagem)> UsarAcaoBasica2014Async(SessaoEstadoDto estado, int conta, int ator,
        AcaoBasica2014 acao, int alvo, string? gatilho, TipoAcao2014 tipo) =>
        RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
        {
            if (tipo is not (TipoAcao2014.Acao or TipoAcao2014.Bonus))
                throw new DomainException("Escolha Ação ou Ação bônus para esta operação.");

            var pino = ExigirPino(estado, pinos, ator);
            PinoMapaDto? inimigo = null;
            string? gatilhoLimpo = null;

            switch (acao)
            {
                case AcaoBasica2014.Esquivar:
                case AcaoBasica2014.Desengajar:
                case AcaoBasica2014.Disparada:
                    break;

                case AcaoBasica2014.Ajudar:
                    if (alvo <= 0)
                        throw new DomainException("Selecione um alvo adjacente para Ajudar.");
                    if (ator == alvo)
                        throw new DomainException("Escolha um alvo distinto do combatente para Ajudar.");
                    inimigo = ExigirPino(estado, pinos, alvo);
                    if (DistanciaPinos(pino, inimigo) > 1.5m)
                        throw new DomainException("Escolha um alvo adjacente (até 1,5 m) para Ajudar.");
                    break;

                case AcaoBasica2014.Preparar:
                    gatilhoLimpo = gatilho?.Trim();
                    if (string.IsNullOrWhiteSpace(gatilhoLimpo) || gatilhoLimpo.Length > 180)
                        throw new DomainException("Informe o gatilho da ação preparada (até 180 caracteres).");
                    break;

                default:
                    throw new DomainException("Ação inválida.");
            }

            var ficha = ConsumirAcao(encontro, pino, tipo);

            switch (acao)
            {
                case AcaoBasica2014.Esquivar:
                    ficha.Esquivando = true;
                    return new($"{pino.Nome} assumiu postura de Esquiva.");

                case AcaoBasica2014.Desengajar:
                    ficha.Desengajando = true;
                    return new($"{pino.Nome} usou Desengajar.");

                case AcaoBasica2014.Disparada:
                    ficha.Disparada = true;
                    return new($"{pino.Nome} usou Disparada.");

                case AcaoBasica2014.Ajudar:
                    ficha.AlvoAjuda = inimigo!.IdPino;
                    return new($"{pino.Nome} usou Ajudar contra {inimigo.Nome} (próximo ataque aliado tem vantagem).");

                case AcaoBasica2014.Preparar:
                    ficha.GatilhoPreparado = gatilhoLimpo;
                    return new($"{pino.Nome} preparou uma ação ({gatilhoLimpo}).");

                default:
                    throw new DomainException("Ação inválida.");
            }
        });

    public Task<(bool Sucesso, string Mensagem)> EsconderPino2014Async(SessaoEstadoDto estado, int conta,
            int ator, TipoAcao2014 tipo, int cd, int modificador) =>
            RegistrarComandoCombateAsync(estado, conta, (controle, encontro, pinos) =>
            {
                if (tipo is not (TipoAcao2014.Acao or TipoAcao2014.Bonus) || cd is < 1 or > 40 || modificador is < -50 or > 50)
                    throw new DomainException("Confira o tipo de ação, a CD e o modificador de Furtividade.");

                var pino = ExigirPino(estado, pinos, ator);
                if (pinos.Any(p => p.IdPino == ator && !p.Visivel))
                    throw new DomainException("O pino selecionado já está oculto.");

                ConsumirAcao(encontro, pino, tipo);
                int dado = RegrasCombate2014.RolarD20(Rolagem2014.Normal, out int _, out int? _);
                int total = dado + modificador;
                bool passou = total >= cd;
                string resultado = passou ? "Ficou escondido no mapa." : "O teste falhou.";
                return new($"{pino.Nome} rolou Furtividade: {total} (d20 {dado}, modificador {modificador:+0;-0;0}) contra CD {cd}. {resultado}",
                    IdPinoParaOcultar: passou ? ator : null);
            });

    public Task<(bool Sucesso, string Mensagem)> ConfigurarRecurso2014Async(SessaoEstadoDto estado, int conta, int ator,
        string nome, int maximo, int recarga, bool curto, int lendarias, int resistencias) =>
        RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
        {
            var pino = ExigirPino(estado, pinos, ator);
            bool temNome = !string.IsNullOrWhiteSpace(nome);
            if (temNome && (nome.Length > 120 || maximo is < 1 or > 99 || recarga is 1 or < 0 or > 6))
                return new() { Erro = "Informe um nome com até 120 caracteres, de 1 a 99 usos e uma recarga de 0 ou de 2 a 6." };

            var ficha = encontro.Ficha(ator);
            if (temNome)
            {
                ficha.Recursos[nome.Trim()] = new RecursoCombate2014
                {
                    Maximo = maximo,
                    Restantes = maximo,
                    RecargaD6 = recarga == 0 ? null : recarga,
                    DescansoCurto = curto
                };
            }

            ficha.UsosLendariosRestantes = ficha.UsosLendariosMaximos = Math.Clamp(lendarias, 0, 10);
            ficha.ResistenciasLendariasRestantes = ficha.ResistenciasLendariasMaximas = Math.Clamp(resistencias, 0, 10);
            return new($"Recursos de {pino.Nome} configurados.");
        });

    public Task<(bool Sucesso, string Mensagem)> RecuperarRecursos2014Async(SessaoEstadoDto estado, int conta, bool longo) =>
        RegistrarComandoCombateAsync(estado, conta, (controle, _, _) =>
        {
            foreach (var ficha in controle.Mapas.Values.SelectMany(m => m.Fichas.Values))
            {
                ficha.GarantirIntegridade();
                foreach (var recurso in ficha.Recursos.Values.Where(r => r is not null && (longo || r.DescansoCurto || r.RecargaD6.HasValue)))
                    recurso.Restantes = recurso.Maximo;

                if (longo)
                    ficha.ResistenciasLendariasRestantes = ficha.ResistenciasLendariasMaximas;
            }
            return new();
        });

    public Task<(bool Sucesso, string Mensagem)> ResolverMorte2014Async(SessaoEstadoDto estado, int conta, int id,
        string acao, int cura = 0) =>
        RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
        {
            var pino = ExigirPino(estado, pinos, id);
            if (!estado.EhMestre && (pino.TipoEntidade != TiposPinoSessao.Personagem ||
                pino.IdEntidade != estado.IdPersonagemLogado || acao != "rolar"))
                throw new DomainException("Faça o teste com seu personagem.");

            var ficha = encontro.Ficha(id);
            int pv = PvAtual(pino);
            int maximo = pino.PvMaximo ?? pv;
            if (ficha.Morto)
                return new() { Erro = "A criatura está morta." };

            switch (acao)
            {
                case "acompanhar":
                    ficha.UsaTestesMorte = true;
                    return new($"{pino.Nome} agora acompanha testes contra a morte.");

                case "curar" when cura > 0:
                    LimparInconsciencia(ficha);
                    int novoPvCura = (int)Math.Min(Math.Max(1, maximo), (long)pv + cura);
                    return new($"{pino.Nome} recuperou {novoPvCura - pv} PV ({novoPvCura}/{Math.Max(1, maximo)}).",
                        [new(id, pv, Math.Max(1, maximo), novoPvCura)]);

                case "estabilizar" when pv == 0:
                    ficha.RestaurarMorte();
                    ficha.Estavel = true;
                    return new($"{pino.Nome} foi estabilizado.");

                case "rolar" when pv == 0 && !ficha.Estavel &&
                    (pino.TipoEntidade == TiposPinoSessao.Personagem || ficha.UsaTestesMorte):
                    if (encontro.Rodada > 0 && (encontro.IdPinoAtual != id || ficha.RodadaUltimoTesteMorte == encontro.Rodada))
                        return new() { Erro = "O teste está disponível uma vez por rodada no turno do combatente." };

                    ficha.RodadaUltimoTesteMorte = encontro.Rodada;
                    int dado = Random.Shared.Next(1, 21);
                    int novoPv = RegrasCombate2014.RegistrarTesteMorte(dado, ficha);
                    if (novoPv > 0)
                        LimparInconsciencia(ficha);

                    return new($"Teste contra a morte de {pino.Nome} ({dado}). " +
                        (novoPv > 0
                            ? "Recuperou 1 PV."
                            : ficha.Morto
                                ? "Morreu."
                                : ficha.Estavel
                                    ? "Estabilizou."
                                    : $"{ficha.SucessosMorte} sucesso(s), {ficha.FalhasMorte} falha(s)."),
                        novoPv > 0 ? [new(id, pv, Math.Max(1, maximo), novoPv)] : null);

                default:
                    return new() { Erro = "Esta ação não está disponível para os PV atuais do combatente." };
            }
        }, incluirJogador: acao == "rolar");

    public Task<(bool Sucesso, string Mensagem)> SolicitarTeste2014Async(SessaoEstadoDto estado, int conta,
        int contaAlvo, string nome, int cd, int modificador, Rolagem2014 modo, bool ocultarCd) =>
        RegistrarComandoCombateAsync(estado, conta, (controle, _, _) =>
        {
            if (string.IsNullOrWhiteSpace(nome) || nome.Length > 100 || cd is < 1 or > 40 || modificador is < -20 or > 30)
                return new() { Erro = "Informe o nome do teste, uma CD de 1 a 40 e um modificador de −20 a 30." };

            var contas = estado.Jogadores
                .Where(j => contaAlvo == 0 || j.IdAccount == contaAlvo)
                .Select(j => j.IdAccount)
                .Distinct()
                .ToList();

            if (contas.Count == 0)
                return new() { Erro = "Não há jogador disponível para receber este teste." };

            if (controle.Pedidos.Count > 80)
                controle.Pedidos.RemoveAll(p => p.Total.HasValue);

            if (controle.Pedidos.Count + contas.Count > 120)
                return new() { Erro = "Há muitos pedidos pendentes. Aguarde as respostas antes de enviar outro teste." };

            foreach (int destinatario in contas)
                controle.Pedidos.Add(new(Guid.NewGuid(), destinatario, nome.Trim(), cd, modificador, modo, ocultarCd));

            return new($"Pedido de teste «{nome.Trim()}» enviado.");
        });

    public Task<(bool Sucesso, string Mensagem)> ResponderTeste2014Async(SessaoEstadoDto estado, int conta, Guid id) =>
        RegistrarComandoCombateAsync(estado, conta, (controle, _, _) =>
        {
            int indice = controle.Pedidos.FindIndex(p => p.Id == id && p.IdConta == conta && !p.Total.HasValue);
            if (indice < 0)
                throw new DomainException("Este pedido já foi respondido ou pertence a outro jogador.");

            var pedido = controle.Pedidos[indice];
            int dado = RegrasCombate2014.RolarD20(pedido.Modo, out _, out _);
            int total = dado + pedido.Modificador;
            controle.Pedidos[indice] = pedido with { Total = total };

            var jogador = estado.Jogadores.FirstOrDefault(j => j.IdAccount == conta);
            string quem = jogador?.NomePersonagem ?? jogador?.Username ?? $"Jogador #{conta}";
            string detalheCd = pedido.OcultarCd || !pedido.Cd.HasValue ? "" : $" contra CD {pedido.Cd}";
            return new($"🎲 {quem} rolou {pedido.Nome}: {total} (d20 {dado}, mod {pedido.Modificador:+0;-0;0}){detalheCd}.");
        }, incluirJogador: true);

    private static HashSet<int> CombatentesNoEncontro(SessaoEstadoDto estado, Encontro2014 encontro, IReadOnlyList<SessaoPino> pinos)
    {
        var existentes = pinos.Select(p => p.IdPino).ToHashSet();
        return estado.Pinos
            .Where(p => existentes.Contains(p.IdPino) &&
                (PvAtual(p) > 0 ||
                 p.TipoEntidade == TiposPinoSessao.Personagem ||
                 encontro.Fichas.GetValueOrDefault(p.IdPino)?.UsaTestesMorte == true))
            .Select(p => p.IdPino)
            .ToHashSet();
    }

    private async Task<(bool Sucesso, string Mensagem)> RegistrarComandoCombateAsync(
        SessaoEstadoDto estado,
        int conta,
        Func<ControleCombate2014, Encontro2014, IReadOnlyList<SessaoPino>, ResultadoComandoCombate2014> executar,
        bool incluirJogador = false,
        bool comandoTabuleiro = false)
    {
        if (!estado.SessaoAtiva || estado.IdMapaAtivo is not int mapa || (!estado.EhMestre && !incluirJogador))
            return (false, "Abra um mapa ativo para usar os controles de combate.");

        string? mensagem = null;
        string? erroComando = null;
        bool mudouJogador = false;
        try
        {
            await _sessaoRepo.AlterarCombate2014Async(estado.IdSessao, mapa, conta, incluirJogador, Guid.NewGuid(), (controle, pinos) =>
            {
                var encontro = controle.ObterMapa(mapa);
                var tabuleiro = controle.Tabuleiro;
                var resultado = !comandoTabuleiro && tabuleiro?.Fase is FaseMovimentoTabuleiro.Retorno or FaseMovimentoTabuleiro.AprovacaoMestre or FaseMovimentoTabuleiro.ConfirmacaoDisparada
                    ? new ResultadoComandoCombate2014 { Erro = "Aguarde a resolução do movimento do tabuleiro físico." }
                    : executar(controle, encontro, pinos);
                erroComando = resultado.Erro;
                if (erroComando is not null)
                    return resultado;

                if (!comandoTabuleiro && tabuleiro is { Fase: FaseMovimentoTabuleiro.Reacoes } && encontro.MovimentoPendente is null)
                    resultado = ConcluirReacoesTabuleiro(estado, controle, encontro, pinos, tabuleiro, resultado);

                encontro.RemoverEfeitosSemConcentracao();
                mensagem = resultado.Mensagem;
                mudouJogador = resultado.AlteracoesPv?.Any(a =>
                    pinos.Any(p => p.IdPino == a.IdPino && p.TipoEntidade == TiposPinoSessao.Personagem)) == true;
                return resultado;
            });

            if (erroComando is not null)
                return (false, erroComando);

            if (mudouJogador)
                InvalidarCacheCampanha(estado.IdCampanha);

            return (true, mensagem ?? "Alteração registrada.");
        }
        catch (DomainException erro)
        {
            return (false, erro.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Falha ao registrar ação de combate na sessão {SessaoId}, mapa {MapaId}", estado.IdSessao, mapa);
            return (false, "A mesa não confirmou a ação. Confira o registro de Combate antes de tentar novamente.");
        }
    }

    private static PinoMapaDto ExigirPino(SessaoEstadoDto estado, IReadOnlyList<SessaoPino> pinos, int id)
    {
        var pino = pinos.FirstOrDefault(p => p.IdPino == id);
        var ficha = estado.Pinos.Find(p => p.IdPino == id && p.IdMapa == estado.IdMapaAtivo);
        if (pino is null || ficha is null)
            throw new DomainException("Selecione um pino do mapa ativo.");
        if (pino.Coluna != ficha.Coluna || pino.Linha != ficha.Linha)
            throw new DomainException("A posição do combatente mudou. Confira o mapa e tente novamente.");
        if (pino.TipoEntidade != TiposPinoSessao.Personagem && pino.PvAtual != ficha.PvAtual && pino.PvAtual.HasValue)
            throw new DomainException("Os PV do combatente mudaram. Confira o mapa e tente novamente.");
        return ficha;
    }

    private static int PvAtual(PinoMapaDto pino) => pino.PvAtual ?? pino.PvMaximo ?? 0;

    public static decimal DistanciaPinos(PinoMapaDto a, PinoMapaDto b) =>
        RegrasCombate2014.DistanciaMetros(a.Coluna, a.Linha, b.Coluna, b.Linha);

    private static string DescreverTurno(SessaoEstadoDto estado, Encontro2014 encontro) => encontro.Rodada == 0
        ? "A iniciativa foi encerrada."
        : $"Rodada {encontro.Rodada} · {(encontro.JanelaLendaria ? "Ações lendárias" : encontro.IdPinoAtual == Encontro2014.TurnoCovil ? "Ação de covil" : estado.Pinos.Find(p => p.IdPino == encontro.IdPinoAtual)?.Nome ?? "Combatente")}.";

    private static EstadoCombatente2014 ConsumirAcao(Encontro2014 encontro, PinoMapaDto pino, TipoAcao2014 tipo, int custo = 1)
    {
        var ficha = encontro.Ficha(pino.IdPino);
        if (PvAtual(pino) <= 0 || RegrasCombate2014.Incapacitado(ficha))
            throw new DomainException("O combatente está incapacitado.");

        if (encontro.Rodada == 0 && tipo is TipoAcao2014.Acao or TipoAcao2014.Bonus)
            return ficha;

        bool turno = encontro.IdPinoAtual == pino.IdPino && !encontro.JanelaLendaria;
        switch (tipo)
        {
            case TipoAcao2014.Acao when turno && !ficha.AcaoUsada:
                ficha.AcaoUsada = true;
                break;

            case TipoAcao2014.Bonus when turno && !ficha.BonusUsado:
                ficha.BonusUsado = true;
                break;

            case TipoAcao2014.Reacao when encontro.Rodada > 0 && !ficha.ReacaoUsada:
                ficha.ReacaoUsada = true;
                ficha.GatilhoPreparado = null;
                break;

            case TipoAcao2014.Lendaria when encontro.JanelaLendaria && encontro.IdPinoAtual != pino.IdPino &&
                    !ficha.UsouAcaoLendariaNesteIntervalo && !ficha.SurpresaPendente && ficha.UsosLendariosRestantes >= custo:
                ficha.UsosLendariosRestantes -= custo;
                ficha.UsouAcaoLendariaNesteIntervalo = true;
                break;

            case TipoAcao2014.Covil when encontro.CovilDisponivel:
                encontro.CovilDisponivel = false;
                break;

            default:
                throw new DomainException("Esta ação não está disponível neste turno.");
        }

        return ficha;
    }

    private static void AplicarCondicao(EstadoCombatente2014 ficha, EfeitoCombate2014 efeito)
    {
        if (CondicaoInvalida(efeito))
            throw new DomainException("Confira a duração e a salvaguarda da condição.");

        ficha.Efeitos.Add(efeito);
        if (RegrasCombate2014.Incapacitado(ficha))
            ficha.EncerrarConcentracao();
    }

    private static bool CondicaoInvalida(EfeitoCombate2014 efeito) =>
        efeito.Rodadas is < 1 or > 100 || efeito.Cd is < 1 or > 40 ||
        (efeito.Cd.HasValue && !efeito.Atributo.HasValue);

    private static void LimparInconsciencia(EstadoCombatente2014 ficha)
    {
        ficha.RestaurarMorte();
        ficha.Efeitos.RemoveAll(e => e.Condicao == Condicao2014.Inconsciente);
    }
}
