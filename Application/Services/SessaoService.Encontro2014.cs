using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Sessoes;
using System.Globalization;
using System.Text;

namespace RPGdeMesa.Application.Services;

public partial class SessaoService
{
    public Task<(bool Sucesso, string Mensagem)> ConfigurarIniciativaAsync(SessaoEstadoDto estado, int conta,
        IReadOnlyList<EntradaIniciativa2014> entradas, bool covil) =>
        RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
        {
            if (entradas.Count == 0 || entradas.DistinctBy(x => x.IdPino).Count() != entradas.Count)
                throw new DomainException("Selecione combatentes distintos para a iniciativa.");
            foreach (var entrada in entradas) ExigirPino(estado, pinos, entrada.IdPino);
            encontro.Covil = covil;
            var aptos = CombatentesNoEncontro(estado, encontro, pinos);
            if (encontro.Rodada == 0) encontro.Iniciar(entradas, aptos.Contains);
            else
            {
                encontro.Ordem = encontro.OrdenarIniciativas(entradas);
                if (!encontro.Ordem.Any(e => e.IdPino == encontro.IdPinoAtual)) encontro.Avancar(aptos.Contains);
            }
            return new(DescreverTurno(estado, encontro), SincronizarTurno: true);
        });

    public Task<(bool Sucesso, string Mensagem)> AvancarIniciativaAsync(SessaoEstadoDto estado, int conta) =>
        RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
        {
            if (encontro.Rodada == 0 || encontro.IdPinoAtual != estado.Encontro.IdPinoAtual || encontro.Rodada != estado.Encontro.Rodada ||
                (!estado.EhMestre && estado.IdAccountTurno != conta))
                throw new DomainException("O turno atual mudou.");
            if (encontro.MovimentoPendente is not null)
                throw new DomainException("Resolva ou dispense as reações antes de avançar.");
            var aptos = CombatentesNoEncontro(estado, encontro, pinos);
            bool janelaLendaria = encontro.Ordem.Any(e => e.IdPino > 0 && e.IdPino != encontro.IdPinoAtual &&
                encontro.Ficha(e.IdPino) is { UsosLendariosRestantes: > 0, SurpresaPendente: false } ficha &&
                !RegrasCombate2014.Incapacitado(ficha) && estado.Pinos.Any(p => p.IdPino == e.IdPino && PvAtual(p) > 0));
            encontro.ConcluirTurno(janelaLendaria, aptos.Contains);
            return new(DescreverTurno(estado, encontro), SincronizarTurno: true);
        }, incluirJogador: true);

    public Task<(bool Sucesso, string Mensagem)> EncerrarIniciativaAsync(SessaoEstadoDto estado, int conta) =>
        RegistrarComandoCombateAsync(estado, conta, (_, encontro, _) =>
        {
            encontro.Encerrar();
            return new("A iniciativa foi encerrada.", SincronizarTurno: true);
        });

    public Task<(bool Sucesso, string Mensagem)> AlterarCondicaoAsync(SessaoEstadoDto estado, int conta, int id,
        EfeitoCombate2014 efeito, bool remover) => RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
    {
        var pino = ExigirPino(estado, pinos, id);
        var ficha = encontro.Ficha(id);
        ficha.Efeitos.RemoveAll(e => e.Condicao == efeito.Condicao && (remover || e.IdOrigem == efeito.IdOrigem));
        if (!remover) AplicarCondicao(ficha, efeito);
        return new($"{pino.Nome} {(remover ? "perdeu" : "recebeu")} a condição {efeito.Condicao}.");
    });

    public Task<(bool Sucesso, string Mensagem)> ConfigurarAfinidadeAsync(SessaoEstadoDto estado, int conta, int id,
        string tipo, AfinidadeDano2014 afinidade) => RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
    {
        var pino = ExigirPino(estado, pinos, id);
        var chave = NormalizarTipoDano(tipo);
        if (chave.Length is < 1 or > 30) throw new DomainException("Informe um tipo de dano.");
        var afinidades = encontro.Ficha(id).Afinidades;
        if (afinidade == AfinidadeDano2014.Normal) afinidades.Remove(chave);
        else afinidades[chave] = afinidade;
        return new($"Afinidade de {pino.Nome} atualizada para {tipo.Trim()}.");
    });

    public Task<(bool Sucesso, string Mensagem)> UsarAcaoBasica2014Async(SessaoEstadoDto estado, int conta, int ator,
        AcaoBasica2014 acao, int alvo, string? gatilho, TipoAcao2014 tipo) =>
        RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
        {
            var pino = ExigirPino(estado, pinos, ator);
            var ficha = ConsumirAcao(encontro, pino, tipo);
            switch (acao)
            {
                case AcaoBasica2014.Esquivar: ficha.Esquivando = true; break;
                case AcaoBasica2014.Desengajar: ficha.Desengajando = true; break;
                case AcaoBasica2014.Disparada: ficha.Disparada = true; break;
                case AcaoBasica2014.Ajudar:
                    var inimigo = ExigirPino(estado, pinos, alvo);
                    if (ator == alvo || DistanciaPinos(pino, inimigo) > 1.5m) throw new DomainException("Escolha um alvo adjacente para Ajudar.");
                    ficha.AlvoAjuda = alvo;
                    break;
                case AcaoBasica2014.Preparar:
                    if (string.IsNullOrWhiteSpace(gatilho) || gatilho.Length > 180) throw new DomainException("Informe o gatilho da ação preparada.");
                    ficha.GatilhoPreparado = gatilho.Trim();
                    break;
                default: throw new DomainException("Ação inválida.");
            }
            return new($"{pino.Nome} usou {acao}{(acao == AcaoBasica2014.Preparar ? $" ({ficha.GatilhoPreparado})" : "")}.");
        });

    public Task<(bool Sucesso, string Mensagem)> EsconderPino2014Async(SessaoEstadoDto estado, int conta,
        int ator, TipoAcao2014 tipo, int cd, int modificador) =>
        RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
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
            var ficha = encontro.Ficha(ator);
            if (!string.IsNullOrWhiteSpace(nome))
            {
                if (nome.Length > 120 || maximo is < 1 or > 99 || recarga is 1 or < 0 or > 6)
                    throw new DomainException("Confira os usos e a recarga.");
                ficha.Recursos[nome.Trim()] = new() { Maximo = maximo, Restantes = maximo, RecargaD6 = recarga == 0 ? null : recarga, DescansoCurto = curto };
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
                foreach (var recurso in ficha.Recursos.Values.Where(r => longo || r.DescansoCurto || r.RecargaD6.HasValue))
                    recurso.Restantes = recurso.Maximo;
                if (longo) ficha.ResistenciasLendariasRestantes = ficha.ResistenciasLendariasMaximas;
            }
            return new();
        });

    public Task<(bool Sucesso, string Mensagem)> ResolverMorte2014Async(SessaoEstadoDto estado, int conta, int id,
        string acao, int cura = 0) => RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
    {
        var pino = ExigirPino(estado, pinos, id);
        if (!estado.EhMestre && (pino.TipoEntidade != TiposPinoSessao.Personagem || pino.IdEntidade != estado.IdPersonagemLogado || acao != "rolar"))
            throw new DomainException("Faça o teste com seu personagem.");
        var ficha = encontro.Ficha(id);
        int pv = PvAtual(pino);
        int maximo = pino.PvMaximo ?? pv;
        if (ficha.Morto) throw new DomainException("A criatura está morta.");
        switch (acao)
        {
            case "acompanhar": ficha.UsaTestesMorte = true; return new();
            case "curar" when cura > 0:
                LimparInconsciencia(ficha);
                return new($"{pino.Nome} recuperou PV.", [new(id, pv, maximo, Math.Min(maximo, pv + cura))]);
            case "estabilizar" when pv == 0:
                ficha.RestaurarMorte(); ficha.Estavel = true;
                return new($"{pino.Nome} foi estabilizado.");
            case "rolar" when pv == 0 && !ficha.Estavel && (pino.TipoEntidade == TiposPinoSessao.Personagem || ficha.UsaTestesMorte):
                if (encontro.Rodada > 0 && (encontro.IdPinoAtual != id || ficha.RodadaUltimoTesteMorte == encontro.Rodada))
                    throw new DomainException("O teste está disponível uma vez por rodada no turno do combatente.");
                ficha.RodadaUltimoTesteMorte = encontro.Rodada;
                int dado = Random.Shared.Next(1, 21);
                int novoPv = RegrasCombate2014.RegistrarTesteMorte(dado, ficha);
                if (novoPv > 0) LimparInconsciencia(ficha);
                return new($"Teste contra a morte de {pino.Nome} ({dado}). " +
                    (novoPv > 0 ? "Recuperou 1 PV." : ficha.Morto ? "Morreu." : ficha.Estavel ? "Estabilizou." :
                        $"{ficha.SucessosMorte} sucesso(s), {ficha.FalhasMorte} falha(s)."), novoPv > 0 ? [new(id, pv, maximo, novoPv)] : null);
            default: throw new DomainException("Selecione uma operação válida para os PV atuais.");
        }
    }, incluirJogador: acao == "rolar");

    public Task<(bool Sucesso, string Mensagem)> SolicitarTeste2014Async(SessaoEstadoDto estado, int conta,
        int contaAlvo, string nome, int cd, int modificador, Rolagem2014 modo, bool ocultarCd) =>
        RegistrarComandoCombateAsync(estado, conta, (controle, _, _) =>
        {
            if (string.IsNullOrWhiteSpace(nome) || nome.Length > 100 || cd is < 1 or > 40 || modificador is < -20 or > 30)
                throw new DomainException("Confira o nome, a CD e o modificador.");
            var contas = estado.Jogadores.Where(j => contaAlvo == 0 || j.IdAccount == contaAlvo).Select(j => j.IdAccount).Distinct().ToList();
            if (contas.Count == 0) throw new DomainException("Nenhum jogador selecionado.");
            if (controle.Pedidos.Count > 80) controle.Pedidos.RemoveAll(p => p.Total.HasValue);
            if (controle.Pedidos.Count + contas.Count > 120) throw new DomainException("Há muitos pedidos de teste pendentes.");
            foreach (int destinatario in contas)
                controle.Pedidos.Add(new(Guid.NewGuid(), destinatario, nome.Trim(), cd, modificador, modo, ocultarCd));
            return new();
        });

    public Task<(bool Sucesso, string Mensagem)> ResponderTeste2014Async(SessaoEstadoDto estado, int conta, Guid id) =>
        RegistrarComandoCombateAsync(estado, conta, (controle, _, _) =>
        {
            int indice = controle.Pedidos.FindIndex(p => p.Id == id && p.IdConta == conta && !p.Total.HasValue);
            if (indice < 0) throw new DomainException("Este pedido já foi respondido ou pertence a outro jogador.");
            var pedido = controle.Pedidos[indice];
            int dado = RegrasCombate2014.RolarD20(pedido.Modo, out _, out _);
            controle.Pedidos[indice] = pedido with { Total = dado + pedido.Modificador };
            return new();
        }, incluirJogador: true);

    private static HashSet<int> CombatentesNoEncontro(SessaoEstadoDto estado, Encontro2014 encontro, IReadOnlyList<SessaoPino> pinos)
    {
        var existentes = pinos.Select(p => p.IdPino).ToHashSet();
        return estado.Pinos.Where(p => existentes.Contains(p.IdPino) && (PvAtual(p) > 0 || p.TipoEntidade == TiposPinoSessao.Personagem ||
            encontro.Fichas.GetValueOrDefault(p.IdPino)?.UsaTestesMorte == true)).Select(p => p.IdPino).ToHashSet();
    }

    private async Task<(bool Sucesso, string Mensagem)> RegistrarComandoCombateAsync(SessaoEstadoDto estado, int conta,
        Func<ControleCombate2014, Encontro2014, IReadOnlyList<SessaoPino>, ResultadoComandoCombate2014> executar, bool incluirJogador = false)
    {
        if (!estado.SessaoAtiva || estado.IdMapaAtivo is not int mapa || (!estado.EhMestre && !incluirJogador))
            return (false, "Abra um mapa ativo para usar os controles de combate.");
        string? mensagem = null;
        bool mudouJogador = false;
        try
        {
            await _sessaoRepo.AlterarCombate2014Async(estado.IdSessao, mapa, conta, incluirJogador, Guid.NewGuid(), (controle, pinos) =>
            {
                var resultado = executar(controle, controle.ObterMapa(mapa), pinos);
                mensagem = resultado.Mensagem;
                mudouJogador = resultado.AlteracoesPv?.Any(a => pinos.Any(p => p.IdPino == a.IdPino && p.TipoEntidade == TiposPinoSessao.Personagem)) == true;
                return resultado;
            });
            if (mudouJogador) InvalidarCacheCampanha(estado.IdCampanha);
            return (true, mensagem ?? "Alteração registrada.");
        }
        catch (DomainException erro) { return (false, erro.Message); }
    }

    private static PinoMapaDto ExigirPino(SessaoEstadoDto estado, IReadOnlyList<SessaoPino> pinos, int id)
    {
        var pino = pinos.FirstOrDefault(p => p.IdPino == id);
        var ficha = estado.Pinos.Find(p => p.IdPino == id && p.IdMapa == estado.IdMapaAtivo);
        if (pino is null || ficha is null) throw new DomainException("Selecione um pino do mapa ativo.");
        if (pino.Coluna != ficha.Coluna || pino.Linha != ficha.Linha)
            throw new DomainException("A posição do combatente mudou. Confira o mapa e tente novamente.");
        if (pino.TipoEntidade != TiposPinoSessao.Personagem && pino.PvAtual != ficha.PvAtual && pino.PvAtual.HasValue)
            throw new DomainException("Os PV do combatente mudaram. Confira o mapa e tente novamente.");
        return ficha;
    }

    private static int PvAtual(PinoMapaDto pino) => pino.PvAtual ?? pino.PvMaximo ?? 0;
    public static decimal DistanciaPinos(PinoMapaDto a, PinoMapaDto b) => RegrasCombate2014.DistanciaMetros(a.Coluna, a.Linha, b.Coluna, b.Linha);
    private static string DescreverTurno(SessaoEstadoDto estado, Encontro2014 encontro) => encontro.Rodada == 0
        ? "A iniciativa foi encerrada."
        : $"Rodada {encontro.Rodada} · {(encontro.JanelaLendaria ? "Ações lendárias" : encontro.IdPinoAtual == Encontro2014.TurnoCovil ? "Ação de covil" : estado.Pinos.Find(p => p.IdPino == encontro.IdPinoAtual)?.Nome)}.";

    private static EstadoCombatente2014 ConsumirAcao(Encontro2014 encontro, PinoMapaDto pino, TipoAcao2014 tipo, int custo = 1)
    {
        var ficha = encontro.Ficha(pino.IdPino);
        if (PvAtual(pino) <= 0 || RegrasCombate2014.Incapacitado(ficha)) throw new DomainException("O combatente está incapacitado.");
        if (encontro.Rodada == 0 && tipo == TipoAcao2014.Acao) return ficha;

        bool turno = encontro.IdPinoAtual == pino.IdPino && !encontro.JanelaLendaria;
        switch (tipo)
        {
            case TipoAcao2014.Acao when turno && !ficha.AcaoUsada: ficha.AcaoUsada = true; break;
            case TipoAcao2014.Bonus when turno && !ficha.BonusUsado: ficha.BonusUsado = true; break;
            case TipoAcao2014.Reacao when encontro.Rodada > 0 && !ficha.ReacaoUsada:
                ficha.ReacaoUsada = true; ficha.GatilhoPreparado = null; break;
            case TipoAcao2014.Lendaria when encontro.JanelaLendaria && encontro.IdPinoAtual != pino.IdPino &&
                    !ficha.UsouAcaoLendariaNesteIntervalo && !ficha.SurpresaPendente && ficha.UsosLendariosRestantes >= custo:
                ficha.UsosLendariosRestantes -= custo; ficha.UsouAcaoLendariaNesteIntervalo = true; break;
            case TipoAcao2014.Covil when encontro.CovilDisponivel: encontro.CovilDisponivel = false; break;
            default: throw new DomainException("Esta ação não está disponível neste turno.");
        }
        return ficha;
    }

    private static void AplicarCondicao(EstadoCombatente2014 ficha, EfeitoCombate2014 efeito)
    {
        if (efeito.Rodadas is < 1 or > 100 || efeito.Cd is < 1 or > 40 || (efeito.Cd.HasValue && !efeito.Atributo.HasValue))
            throw new DomainException("Confira a duração e a salvaguarda da condição.");
        ficha.Efeitos.Add(efeito);
        if (RegrasCombate2014.Incapacitado(ficha)) ficha.EncerrarConcentracao();
    }

    private static void LimparInconsciencia(EstadoCombatente2014 ficha)
    {
        ficha.RestaurarMorte();
        ficha.Efeitos.RemoveAll(e => e.Condicao == Condicao2014.Inconsciente);
    }

    private static string NormalizarTipoDano(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo)) return "";
        string chave = string.Concat(tipo.Trim().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToUpperInvariant();
        return chave switch
        {
            "ACID" => "ACIDO", "BLUDGEONING" or "CONTUNDENTE" => "CONCUSSAO", "COLD" => "FRIO",
            "FIRE" => "FOGO", "FORCE" => "ENERGIA", "LIGHTNING" => "ELETRICO", "NECROTIC" => "NECROTICO",
            "PIERCING" => "PERFURANTE", "POISON" => "VENENO", "PSYCHIC" => "PSIQUICO", "RADIANT" => "RADIANTE",
            "SLASHING" => "CORTANTE", "THUNDER" => "TROVAO", _ => chave
        };
    }
}
