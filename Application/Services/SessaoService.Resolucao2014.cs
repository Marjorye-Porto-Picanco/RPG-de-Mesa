using RPGdeMesa.Domain.Combate;
using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Application.Services;

public partial class SessaoService
{
    private Task<(bool Sucesso, string Mensagem)> RegistrarResultadoJogador2014Async(SessaoEstadoDto estado, int conta,
        RegistroAtaqueDto ataque, int idAlvo, string dados)
    {
        int danoBruto = ataque.DanoTotal;
        return RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
        {
            if (encontro.Rodada > 0 && (encontro.JanelaLendaria || !pinos.Any(p => p.IdPino == encontro.IdPinoAtual &&
                p.TipoEntidade == TiposPinoSessao.Personagem && p.IdEntidade == estado.IdPersonagemLogado)))
                throw new DomainException("O turno do personagem acabou.");

            var alvo = ExigirPino(estado, pinos, idAlvo);
            string descricao = $"⚔ {ataque.NomeAtacante} usou {ataque.NomeArma} contra {alvo.Nome} ({dados}, total {ataque.TotalAtaque} contra CA {ataque.CaAlvo}).";
            if (!ataque.Acertou)
                return new($"{descricao} Errou.");

            var defesa = encontro.Ficha(idAlvo);
            if (defesa.Morto)
                throw new DomainException("O alvo está morto.");

            var (dano, detalhes) = AjustarParcelas2014(
                new() { [RegrasCombate2014.NormalizarTipoDano(ataque.TipoDano)] = danoBruto },
                defesa,
                false);

            int atual = PvAtual(alvo);
            int maximo = Math.Max(1, alvo.PvMaximo ?? atual);
            int novo = Math.Max(0, atual - dano);
            ataque.PvNovo = novo;
            ataque.DanoTotal = dano;
            string consequencias = AplicarConsequencias2014(alvo, defesa, atual, novo, dano, ataque.EhCritico);
            return new($"{descricao} {detalhes}. {alvo.Nome} ficou com {novo}/{maximo} PV.{consequencias}", [new(idAlvo, atual, maximo, novo)]);
        }, incluirJogador: true);
    }

    public Task<(bool Sucesso, string Mensagem)> ExecutarGolpes2014Async(SessaoEstadoDto estado, int conta, int idAtacante,
        IReadOnlyList<GolpeMestre2014> golpes, UsoAcao2014 uso)
    {
        if (!estado.EhMestre || !estado.SessaoAtiva || estado.IdMapaAtivo is not int idMapa)
            return Task.FromResult((false, "Abra um mapa ativo para usar os controles de combate."));

        var atacanteSelecionado = estado.Pinos.Find(p => p.IdPino == idAtacante && p.IdMapa == idMapa);
        if (golpes is null || golpes.Count is < 1 or > 12 || atacanteSelecionado is null ||
            atacanteSelecionado.TipoEntidade is not (TiposPinoSessao.Monstro or TiposPinoSessao.Npc))
            return Task.FromResult((false, "Selecione um monstro ou NPC e pelo menos um ataque."));

        var ataques = ObterAtaquesEntidade(estado, atacanteSelecionado)
            .GroupBy(a => a.Id)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var golpe in golpes)
        {
            if (golpe is null)
                return Task.FromResult((false, "Há um golpe inválido na sequência de multiataque."));
            if (golpe.IdAlvo <= 0 || golpe.IdAlvo == idAtacante)
                return Task.FromResult((false, "Escolha um alvo distinto do atacante para cada golpe."));
            if (!ataques.TryGetValue(golpe.IdAtaque, out var ataque))
                return Task.FromResult((false, "Escolha um ataque vinculado ao combatente."));
            if (string.IsNullOrWhiteSpace(ataque.Dano))
                return Task.FromResult((false, $"{ataque.Nome} está sem fórmula de dano na ficha. Monte a sequência no multiataque ou use Ações rápidas → Habilidades para resolver um efeito."));
            if (!ataque.TemFormulaDano)
                return Task.FromResult((false, $"Corrija o campo Dano de {ataque.Nome}. O valor «{ataque.Dano}» é inválido. Use uma fórmula como 2d6+5+1d12 ou um número fixo."));
            if (golpe.DanoAdicional is { Count: > 5 })
                return Task.FromResult((false, "O golpe aceita até cinco parcelas de dano adicional."));

            var adicionalInvalido = golpe.DanoAdicional?.FirstOrDefault(p => p is null || !RegrasCombate2014.DanoValido(p.Expressao));
            if (adicionalInvalido is not null)
                return Task.FromResult((false, $"Corrija o campo Dano adicional de {ataque.Nome}. O valor «{adicionalInvalido.Expressao}» é inválido. Use uma fórmula como 1d6 ou um número fixo."));

            if (golpe.Efeito is { } efeito &&
                (efeito.Rodadas is < 1 or > 100 || efeito.Cd is < 1 or > 40 || (efeito.Cd.HasValue && !efeito.Atributo.HasValue)))
                return Task.FromResult((false, "Confira a duração e a salvaguarda da condição no acerto."));
        }

        return RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
        {
            var atacante = ExigirPino(estado, pinos, idAtacante);
            var movimento = encontro.MovimentoPendente;

            if (uso.Oportunidade && (uso.Acao != TipoAcao2014.Reacao || golpes.Count != 1 || movimento is null ||
                !movimento.ReacoesPendentes.Contains(idAtacante) || golpes[0].IdAlvo != movimento.IdMovendo))
                throw new DomainException("A oportunidade já foi resolvida ou o alvo se moveu.");

            var alvosResolvidos = new List<PinoMapaDto>(golpes.Count);
            foreach (var golpe in golpes)
            {
                var alvo = ExigirPino(estado, pinos, golpe.IdAlvo);
                if (alvo.IdPino == atacante.IdPino)
                    throw new DomainException("Escolha um alvo distinto do atacante.");

                var ataque = ataques[golpe.IdAtaque];
                if (uso.Oportunidade && (!ataque.CorpoACorpo || !ProvocouOportunidade2014(atacante, alvo, movimento!.Coluna, movimento.Linha, ataque)))
                    throw new DomainException("Escolha um ataque corpo a corpo cujo alcance o alvo está deixando.");

                decimal distancia = DistanciaPinos(atacante, alvo);
                decimal? alcance = golpe.Alcance ?? RegrasMagiaCombate.InterpretarAlcance(ataque.Alcance).MetrosMaximos;
                if (alcance is decimal max && distancia > max)
                    return new() { Erro = $"{alvo.Nome} está a {distancia:0.#} m, fora do alcance de {max:0.#} m." };
                if (golpe.Cobertura == Cobertura2014.Total)
                    throw new DomainException($"{alvo.Nome} tem cobertura total.");

                alvosResolvidos.Add(alvo);
            }

            var ficha = ConsumirAcao(encontro, atacante, uso.Acao, Math.Clamp(uso.CustoLendario, 1, 10));
            ConsumirRecurso(ficha, uso.Recurso);

            var resultados = new List<string>(golpes.Count);
            var alteracoes = new Dictionary<int, AlteracaoPvCombate2014>();

            for (int i = 0; i < golpes.Count; i++)
            {
                var golpe = golpes[i];
                var alvo = alvosResolvidos[i];
                var ataque = ataques[golpe.IdAtaque];
                var defesa = encontro.Ficha(alvo.IdPino);

                int anterior = PvAtual(alvo);
                int atual = alteracoes.TryGetValue(alvo.IdPino, out var alteracao) ? alteracao.PvNovo : anterior;
                bool acompanha = alvo.TipoEntidade == TiposPinoSessao.Personagem || defesa.UsaTestesMorte;

                if (defesa.Morto || (atual <= 0 && !acompanha))
                {
                    if (golpes.Count == 1)
                        throw new DomainException("O alvo está derrotado.");
                    resultados.Add($"{alvo.Nome} já está derrotado.");
                    continue;
                }

                decimal distancia = DistanciaPinos(atacante, alvo);
                var parcelas = new List<ParcelaDano2014> { new(ataque.Dano!, ataque.TipoDano) };
                if (golpe.DanoAdicional is { Count: > 0 })
                    parcelas.AddRange(golpe.DanoAdicional);

                bool adjacente = distancia <= 1.5m;
                bool inconsciente = defesa.Tem(Condicao2014.Inconsciente) || (acompanha && atual == 0);
                var modo = CalcularModoAtaque(golpe.Rolagem, ficha, defesa, alvo.Esquivando, adjacente, inconsciente);

                var ajuda = encontro.Fichas
                    .FirstOrDefault(x => x.Key != atacante.IdPino && x.Value?.AlvoAjuda == alvo.IdPino)
                    .Value;
                if (ajuda is not null)
                {
                    ajuda.AlvoAjuda = null;
                    modo = RegrasCombate2014.CombinarRolagem(true, modo == Rolagem2014.Desvantagem);
                }

                int d20 = RegrasCombate2014.RolarD20(modo, out int primeiro, out int? segundo);
                int ca = (alvo.ClasseArmadura ?? 10) + RegrasCombate2014.BonusCobertura(golpe.Cobertura);
                int total = d20 + ataque.BonusAtaque;
                string dados = segundo.HasValue ? $"d20 {primeiro}/{segundo}, escolhido {d20}" : $"d20 {d20}";
                string prefixo = golpes.Count > 1 ? $"[{i + 1}/{golpes.Count}] " : "";
                string descricao = $"{prefixo}⚔ {atacante.Nome} usou {ataque.Nome} contra {alvo.Nome} ({dados}, total {total} contra CA {ca}).";

                if (d20 == 1 || (d20 != 20 && total < ca))
                {
                    resultados.Add($"{descricao} Errou.");
                    continue;
                }

                bool critico = d20 == 20 || (adjacente && (inconsciente || defesa.Tem(Condicao2014.Paralisado)));
                var (dano, detalhes) = RolarParcelas2014(parcelas, defesa, critico, ataque.BonusDano);
                int maximo = Math.Max(1, alvo.PvMaximo ?? anterior);
                int novo = Math.Max(0, atual - dano);
                alteracoes[alvo.IdPino] = new(alvo.IdPino, anterior, maximo, novo);

                string consequencias = AplicarConsequencias2014(alvo, defesa, atual, novo, dano, critico);
                if (golpe.Efeito is { } efeito && !defesa.Morto)
                    consequencias += ResolverEfeito2014(alvo, defesa, efeito with { IdOrigem = atacante.IdPino }, golpe.UsarResistenciaLendaria);

                resultados.Add($"{descricao} {(critico ? "Crítico" : "Acerto")}. {detalhes}. {alvo.Nome} ficou com {novo}/{maximo} PV.{consequencias}");
            }

            MovimentoPendente2014? confirmado = null;
            if (uso.Oportunidade && movimento is not null)
            {
                bool podeMover = !alteracoes.TryGetValue(movimento.IdMovendo, out var pv) || pv.PvNovo > 0;
                confirmado = FinalizarReacao2014(encontro, idAtacante, podeMover);
            }

            return new(string.Join("\n", resultados), alteracoes.Values.ToList(), MovimentoConfirmado: confirmado);
        });
    }

    private static Rolagem2014 CalcularModoAtaque(Rolagem2014 pedido, EstadoCombatente2014 ficha,
        EstadoCombatente2014 defesa, bool esquiva, bool adjacente, bool inconsciente)
    {
        bool vantagem = pedido == Rolagem2014.Vantagem || ficha.Tem(Condicao2014.Invisivel) || defesa.Tem(Condicao2014.Cego) ||
            defesa.Tem(Condicao2014.Impedido) || defesa.Tem(Condicao2014.Atordoado) || defesa.Tem(Condicao2014.Paralisado) ||
            defesa.Tem(Condicao2014.Petrificado) || inconsciente || (adjacente && defesa.Tem(Condicao2014.Caido));
        bool desvantagem = pedido == Rolagem2014.Desvantagem || ficha.Tem(Condicao2014.Cego) || ficha.Tem(Condicao2014.Envenenado) ||
            ficha.Tem(Condicao2014.Impedido) || ficha.Tem(Condicao2014.Caido) || defesa.Tem(Condicao2014.Invisivel) ||
            (!adjacente && defesa.Tem(Condicao2014.Caido)) || ((defesa.Esquivando || esquiva) &&
            !RegrasCombate2014.Incapacitado(defesa) && !defesa.Tem(Condicao2014.Agarrado));
        return RegrasCombate2014.CombinarRolagem(vantagem, desvantagem);
    }

    private static (int Total, string Detalhes) RolarParcelas2014(IEnumerable<ParcelaDano2014> parcelas,
        EstadoCombatente2014 defesa, bool critico, int bonus = 0, bool metade = false)
    {
        var tipos = new Dictionary<string, int>();
        foreach (var parcela in parcelas)
        {
            if (!RegrasCombate2014.TentarRolarDano(parcela.Expressao, critico, bonus, out int valor))
                throw new DomainException("A fórmula de dano é inválida.");

            string chave = RegrasCombate2014.NormalizarTipoDano(parcela.Tipo);
            tipos[chave] = tipos.GetValueOrDefault(chave) + valor;
            bonus = 0;
        }
        return AjustarParcelas2014(tipos, defesa, metade);
    }

    private static (int Total, string Detalhes) AjustarParcelas2014(Dictionary<string, int> tipos, EstadoCombatente2014 defesa, bool metade)
    {
        int total = 0;
        var detalhes = new List<string>();
        foreach (var (tipo, bruto) in tipos)
        {
            var afinidade = defesa.Afinidades.GetValueOrDefault(tipo);
            int liquido = RegrasCombate2014.AjustarDano(metade ? bruto / 2 : bruto, afinidade);
            total += liquido;
            detalhes.Add($"{liquido}{(tipo.Length == 0 ? "" : " de " + tipo.ToLowerInvariant())}" +
                (afinidade == AfinidadeDano2014.Normal ? "" : $" ({afinidade.ToString().ToLowerInvariant()})"));
        }
        return (total, "Dano " + string.Join(" + ", detalhes));
    }

    private static string AplicarConsequencias2014(PinoMapaDto alvo, EstadoCombatente2014 ficha, int anterior, int novo, int dano, bool critico)
    {
        if (dano <= 0) return "";
        bool acompanha = alvo.TipoEntidade == TiposPinoSessao.Personagem || ficha.UsaTestesMorte;
        if (novo == 0)
        {
            ficha.EncerrarConcentracao();
            if (!acompanha) return " Derrotado.";
            RegrasCombate2014.RegistrarDanoMorte(dano, alvo.PvMaximo ?? 1, anterior, critico, ficha);
            if (ficha.Morto) return " Morreu.";
            if (!ficha.Tem(Condicao2014.Inconsciente))
                ficha.Efeitos.Add(new EfeitoCombate2014 { Condicao = Condicao2014.Inconsciente });
            return anterior == 0 ? $" {ficha.FalhasMorte} falha(s) contra a morte." : " Ficou inconsciente.";
        }

        if (ficha.Concentracao is not { } magia) return "";

        int cd = Math.Max(10, dano / 2);
        int teste = Random.Shared.Next(1, 21) + alvo.Modificador(Atributo2014.Constituicao);
        if (teste >= cd) return $" Manteve concentração ({teste} contra CD {cd}).";

        ficha.EncerrarConcentracao();
        return $" Perdeu concentração em {magia} ({teste} contra CD {cd}).";
    }

    private static string ResolverEfeito2014(PinoMapaDto alvo, EstadoCombatente2014 defesa, EfeitoCombate2014 efeito, bool resistenciaLendaria)
    {
        if (efeito.Cd is int cd && efeito.Atributo is { } atributo && !efeito.TesteParaEscapar)
        {
            int rolado = Random.Shared.Next(1, 21) + alvo.Modificador(atributo);
            if (PassouSalvaguarda2014(rolado, cd, defesa, resistenciaLendaria))
                return $" Passou na salvaguarda de {atributo} ({rolado} contra CD {cd}).";
        }

        defesa.Efeitos.RemoveAll(e => e.Condicao == efeito.Condicao && e.IdOrigem == efeito.IdOrigem);
        AplicarCondicao(defesa, efeito);
        return $" Recebeu {efeito.Condicao}.";
    }

    private static bool PassouSalvaguarda2014(int total, int cd, EstadoCombatente2014 ficha, bool resistencia)
    {
        if (total >= cd) return true;
        if (!resistencia || ficha.ResistenciasLendariasRestantes <= 0) return false;
        ficha.ResistenciasLendariasRestantes--;
        return true;
    }

    private static void ConsumirRecurso(EstadoCombatente2014 ficha, string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return;
        if (!ficha.Recursos.TryGetValue(nome, out var recurso) || recurso is null || recurso.Restantes <= 0)
            throw new DomainException("O recurso selecionado está sem usos disponíveis.");
        recurso.Restantes--;
    }
}
