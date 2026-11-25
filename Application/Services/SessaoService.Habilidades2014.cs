using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Application.Services;

public partial class SessaoService
{
    public Task<(bool Sucesso, string Mensagem)> UsarHabilidade2014Async(SessaoEstadoDto estado, int conta, HabilidadeMestre2014 uso) =>
        RegistrarComandoCombateAsync(estado, conta, (controle, encontro, pinos) =>
        {
            if (string.IsNullOrWhiteSpace(uso.Nome) || uso.Nome.Length > 120 || uso.Cd is < 1 or > 40 || uso.RodadasConcentracao is < 1 or > 1000)
                throw new DomainException("Confira o nome, a CD e a duração da habilidade.");
            PinoMapaDto? origem = null;
            if (uso.IdOrigem > 0)
            {
                origem = ExigirPino(estado, pinos, uso.IdOrigem);
                var ficha = ConsumirAcao(encontro, origem, uso.Acao.Acao, Math.Clamp(uso.Acao.CustoLendario, 1, 10));
                ConsumirRecurso(ficha, uso.Acao.Recurso);
                if (uso.Concentracao)
                {
                    ficha.Concentracao = uso.Nome.Trim();
                    ficha.RodadasConcentracao = uso.RodadasConcentracao;
                }
            }
            else if (uso.Concentracao || uso.Acao.Acao != TipoAcao2014.Acao || !string.IsNullOrWhiteSpace(uso.Acao.Recurso))
                throw new DomainException("Selecione quem está usando a habilidade.");

            var alvos = SelecionarAlvosHabilidade(estado, pinos, origem, uso);
            if (uso.Cobertura == Cobertura2014.Total && uso.Area == SistemaArea2014.AlvosMarcados)
                throw new DomainException("O alvo tem cobertura total.");
            if (origem is not null && uso.Alcance is decimal alcance &&
                (uso.Area == SistemaArea2014.Raio ? RegrasCombate2014.DistanciaMetros(origem.Coluna, origem.Linha, uso.Coluna, uso.Linha) > alcance :
                    uso.Area == SistemaArea2014.AlvosMarcados && alvos.Any(alvo => DistanciaPinos(origem, alvo) > alcance)))
                throw new DomainException("A habilidade está fora do alcance.");
            if (alvos.Count == 0) throw new DomainException("Selecione ao menos um alvo na área.");
            var parcelas = new Dictionary<string, int>();
            if (uso.Resolucao is ResolucaoHabilidade2014.Dano or ResolucaoHabilidade2014.Salvaguarda or ResolucaoHabilidade2014.Cura or ResolucaoHabilidade2014.AtaqueMagico)
            {
                if (!RegrasCombate2014.TentarRolarDano(uso.Dano, false, 0, out int valor) &&
                    !(uso.Resolucao == ResolucaoHabilidade2014.Salvaguarda && uso.Efeito is not null && string.IsNullOrWhiteSpace(uso.Dano)))
                    throw new DomainException("Confira a fórmula da habilidade.");
                parcelas[NormalizarTipoDano(uso.TipoDano)] = valor;
            }

            var mensagens = new List<string>();
            var alteracoes = new List<AlteracaoPvCombate2014>();
            foreach (var alvo in alvos)
            {
                var defesa = encontro.Ficha(alvo.IdPino);
                int anterior = PvAtual(alvo);
                int maximo = alvo.PvMaximo ?? anterior;
                if (defesa.Morto || (anterior <= 0 && alvo.TipoEntidade != TiposPinoSessao.Personagem && !defesa.UsaTestesMorte &&
                    uso.Resolucao != ResolucaoHabilidade2014.Cura)) continue;

                bool passou = false;
                bool critico = false;
                string texto = alvo.Nome;
                if (uso.Resolucao == ResolucaoHabilidade2014.Salvaguarda)
                {
                    int bonus = alvo.Modificador(uso.Atributo) + Math.Clamp(uso.ModificadorSalvaguarda, -20, 30);
                    if (uso.Atributo == Atributo2014.Destreza) bonus += RegrasCombate2014.BonusCobertura(uso.Cobertura);
                    bool falhaAutomatica = uso.Atributo is Atributo2014.Forca or Atributo2014.Destreza &&
                        (defesa.Tem(Condicao2014.Atordoado) || defesa.Tem(Condicao2014.Paralisado) || defesa.Tem(Condicao2014.Inconsciente) ||
                            defesa.Tem(Condicao2014.Petrificado) || (anterior == 0 && alvo.TipoEntidade == TiposPinoSessao.Personagem));
                    var modoTeste = RegrasCombate2014.CombinarRolagem(uso.Atributo == Atributo2014.Destreza && (defesa.Esquivando || alvo.Esquivando),
                        uso.Atributo == Atributo2014.Destreza && defesa.Tem(Condicao2014.Impedido));
                    int total = falhaAutomatica ? int.MinValue : RegrasCombate2014.RolarD20(modoTeste, out _, out _) + bonus;
                    passou = PassouSalvaguarda2014(total, uso.Cd, defesa, uso.UsarResistenciaLendaria);
                    texto += $" {(passou ? "passou" : "falhou")} na salvaguarda ({(falhaAutomatica ? "falha automática" : total.ToString())}, CD {uso.Cd}).";
                }
                if (uso.Resolucao == ResolucaoHabilidade2014.AtaqueMagico)
                {
                    var fonte = origem is null ? new EstadoCombatente2014() : encontro.Ficha(origem.IdPino);
                    bool adjacente = origem is not null && DistanciaPinos(origem, alvo) <= 1.5m;
                    bool inconsciente = defesa.Tem(Condicao2014.Inconsciente) || anterior == 0;
                    var modo = CalcularModoAtaque(uso.Rolagem, fonte, defesa, alvo.Esquivando, adjacente, inconsciente);
                    int dado = RegrasCombate2014.RolarD20(modo, out _, out _);
                    int ca = (alvo.ClasseArmadura ?? 10) + RegrasCombate2014.BonusCobertura(uso.Cobertura);
                    int total = dado + Math.Clamp(uso.BonusAtaque, -20, 30);
                    if (dado == 1 || (dado != 20 && total < ca)) { mensagens.Add($"{texto}: errou ({total} contra CA {ca})."); continue; }
                    critico = dado == 20 || (adjacente && (inconsciente || defesa.Tem(Condicao2014.Paralisado)));
                    texto += $" sofreu {(critico ? "acerto crítico" : "acerto")} ({total} contra CA {ca}).";
                }

                if (uso.Resolucao == ResolucaoHabilidade2014.Cura)
                {
                    int cura = parcelas.Values.Sum();
                    int novo = Math.Min(maximo, anterior + cura);
                    if (novo > anterior)
                    {
                        LimparInconsciencia(defesa);
                        alteracoes.Add(new(alvo.IdPino, anterior, maximo, novo));
                    }
                    texto += $" recuperou {novo - anterior} PV ({novo}/{maximo}).";
                }
                else if (parcelas.Count > 0 && (!passou || uso.MeioDano))
                {
                    var (dano, detalhes) = critico
                        ? RolarParcelas2014([new(uso.Dano!, uso.TipoDano)], defesa, true)
                        : AjustarParcelas2014(parcelas, defesa, passou);
                    int novo = Math.Max(0, anterior - dano);
                    alteracoes.Add(new(alvo.IdPino, anterior, maximo, novo));
                    texto += $" {detalhes}. PV {novo}/{maximo}." + AplicarConsequencias2014(alvo, defesa, anterior, novo, dano, critico);
                }
                if (!passou && !defesa.Morto && uso.Efeito is { } efeito)
                    texto += ResolverEfeito2014(alvo, defesa, efeito with { IdOrigem = origem?.IdPino,
                        ConcentracaoOrigem = uso.Concentracao ? uso.Nome.Trim() : null }, uso.UsarResistenciaLendaria);
                mensagens.Add(texto);
            }
            if (mensagens.Count == 0) throw new DomainException("Nenhum alvo válido para a habilidade.");
            return new($"{origem?.Nome ?? "Ambiente"} usou {uso.Nome.Trim()}.\n{string.Join("\n", mensagens)}", alteracoes);
        });

    public Task<(bool Sucesso, string Mensagem)> TestarCondicao2014Async(SessaoEstadoDto estado, int conta, int id, Condicao2014 condicao, int bonus) =>
        RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
        {
            var pino = ExigirPino(estado, pinos, id);
            var ficha = encontro.Ficha(id);
            var efeito = ficha.Efeitos.FirstOrDefault(e => e.Condicao == condicao && e.Cd.HasValue && e.Atributo.HasValue);
            if (efeito?.Cd is not int cd || efeito.Atributo is not { } atributo) throw new DomainException("A condição não tem teste configurado.");
            int total = Random.Shared.Next(1, 21) + pino.Modificador(atributo) + Math.Clamp(bonus, -20, 30);
            if (total >= cd) ficha.Efeitos.Remove(efeito);
            return new($"{pino.Nome} {(total >= cd ? "encerrou" : "manteve")} {condicao} no teste ({total} contra CD {cd}).");
        });

    public Task<(bool Sucesso, string Mensagem)> EncerrarConcentracao2014Async(SessaoEstadoDto estado, int conta, int id) =>
        RegistrarComandoCombateAsync(estado, conta, (_, encontro, pinos) =>
        {
            var pino = ExigirPino(estado, pinos, id);
            encontro.Ficha(id).EncerrarConcentracao();
            return new($"{pino.Nome} encerrou a concentração.");
        });

    private static List<PinoMapaDto> SelecionarAlvosHabilidade(SessaoEstadoDto estado, IReadOnlyList<SessaoPino> pinos,
        PinoMapaDto? origem, HabilidadeMestre2014 uso)
    {
        var ativos = uso.Alvos.Distinct().Select(id => ExigirPino(estado, pinos, id)).ToList();
        if (uso.Area == SistemaArea2014.AlvosMarcados) return ativos;
        if (uso.Tamanho is <= 0 or > 90 || uso.Coluna is < 0 or > 19 || uso.Linha is < 0 or > 19)
            throw new DomainException("Confira o tamanho e as coordenadas da área.");
        if (ativos.Count == 0) ativos = pinos.Select(p => ExigirPino(estado, pinos, p.IdPino)).ToList();
        if (uso.Area == SistemaArea2014.Raio)
            return ativos.Where(p => RegrasCombate2014.DistanciaMetros(uso.Coluna, uso.Linha, p.Coluna, p.Linha) <= uso.Tamanho).ToList();
        if (origem is null) throw new DomainException("Selecione a origem e uma direção para linha ou cone.");

        int dx = uso.Coluna - origem.Coluna;
        int dy = uso.Linha - origem.Linha;
        double tamanhoDirecao = Math.Sqrt(dx * dx + dy * dy);
        if (tamanhoDirecao <= 0) throw new DomainException("Escolha uma direção diferente da casa da origem.");
        return ativos.Where(p =>
        {
            int x = p.Coluna - origem.Coluna;
            int y = p.Linha - origem.Linha;
            double frente = (x * dx + y * dy) / tamanhoDirecao * 1.5;
            double lado = Math.Abs(x * dy - y * dx) / tamanhoDirecao * 1.5;
            return frente > 0 && frente <= (double)uso.Tamanho && lado <= (uso.Area == SistemaArea2014.Linha ? 0.75 : frente / 2);
        }).ToList();
    }
}

public enum ResolucaoHabilidade2014 { Salvaguarda, AtaqueMagico, Dano, Cura, Efeito }

public sealed record HabilidadeMestre2014(string Nome, int IdOrigem, IReadOnlyList<int> Alvos, UsoAcao2014 Acao,
    ResolucaoHabilidade2014 Resolucao, string? Dano, string? TipoDano, Atributo2014 Atributo, int Cd, bool MeioDano,
    SistemaArea2014 Area = SistemaArea2014.AlvosMarcados, decimal Tamanho = 0, int Coluna = 0, int Linha = 0,
    EfeitoCombate2014? Efeito = null, bool Concentracao = false, int? RodadasConcentracao = null,
    bool UsarResistenciaLendaria = false, int BonusAtaque = 0, int ModificadorSalvaguarda = 0,
    Rolagem2014 Rolagem = Rolagem2014.Normal, Cobertura2014 Cobertura = Cobertura2014.Nenhuma, decimal? Alcance = null);
