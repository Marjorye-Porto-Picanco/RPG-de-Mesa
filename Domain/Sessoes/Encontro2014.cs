namespace RPGdeMesa.Domain.Sessoes;

public sealed class ControleCombate2014
{
    public Dictionary<int, Encontro2014> Mapas { get; set; } = [];
    public List<PedidoTeste2014> Pedidos { get; set; } = [];

    public Encontro2014 ObterMapa(int idMapa)
    {
        if (!Mapas.TryGetValue(idMapa, out var encontro))
        {
            encontro = new();
            Mapas.Add(idMapa, encontro);
        }
        return encontro;
    }
}

public sealed record PedidoTeste2014(Guid Id, int IdConta, string Nome, int? Cd, int Modificador,
    Rolagem2014 Modo = Rolagem2014.Normal, bool OcultarCd = false, int? Total = null);

public sealed record AlteracaoPvCombate2014(int IdPino, int PvAnterior, int PvMaximo, int PvNovo);

public sealed record ResultadoComandoCombate2014(string? Mensagem = null,
    IReadOnlyList<AlteracaoPvCombate2014>? AlteracoesPv = null, bool SincronizarTurno = false,
    MovimentoPendente2014? MovimentoConfirmado = null);

public sealed record MovimentoPendente2014(int IdMovendo, int ColunaOrigem, int LinhaOrigem, int Coluna, int Linha,
    List<int> ReacoesPendentes);

public sealed class Encontro2014
{
    public const int TurnoCovil = -1;
    public int Rodada { get; set; }
    public int IdPinoAtual { get; set; }
    public bool Covil { get; set; }
    public bool CovilDisponivel { get; set; }
    public bool JanelaLendaria { get; set; }
    public MovimentoPendente2014? MovimentoPendente { get; set; }
    public List<EntradaIniciativa2014> Ordem { get; set; } = [];
    public Dictionary<int, EstadoCombatente2014> Fichas { get; set; } = [];

    public EstadoCombatente2014 Ficha(int idPino)
    {
        if (!Fichas.TryGetValue(idPino, out var ficha))
        {
            ficha = new();
            Fichas.Add(idPino, ficha);
        }
        return ficha;
    }

    public void Iniciar(IEnumerable<EntradaIniciativa2014> entradas, Func<int, bool>? disponivel = null)
    {
        Ordem = OrdenarIniciativas(entradas);
        foreach (var entrada in Ordem.Where(e => e.IdPino > 0))
        {
            var ficha = Ficha(entrada.IdPino);
            ficha.ReacaoUsada = ficha.SurpresaPendente = entrada.Surpreso;
            ficha.UsosLendariosRestantes = ficha.UsosLendariosMaximos;
            ficha.RodadaUltimoTesteMorte = 0;
        }
        Rodada = 1;
        IdPinoAtual = 0;
        CovilDisponivel = false;
        Avancar(disponivel);
    }

    public void Avancar(Func<int, bool>? disponivel = null)
    {
        if (Rodada == 0) return;

        int indice = Ordem.FindIndex(e => e.IdPino == IdPinoAtual);
        if (!JanelaLendaria && indice >= 0 && IdPinoAtual > 0) Ficha(IdPinoAtual).TerminarTurno();
        JanelaLendaria = false;
        int proximo = indice + 1;
        for (int tentativas = 0; tentativas < Ordem.Count; tentativas++, proximo++)
        {
            if (proximo >= Ordem.Count)
            {
                Rodada++;
                proximo = 0;
            }

            var entrada = Ordem[proximo];
            if (entrada.IdPino == TurnoCovil)
            {
                if (Ordem.All(e => e.IdPino <= 0 || Ficha(e.IdPino).Morto || disponivel?.Invoke(e.IdPino) is false)) break;
                IdPinoAtual = TurnoCovil;
                CovilDisponivel = true;
                return;
            }
            if (Ficha(entrada.IdPino).Morto || disponivel?.Invoke(entrada.IdPino) is false) continue;

            CovilDisponivel = false;
            foreach (var ficha in Fichas.Values) ficha.UsouAcaoLendariaNesteIntervalo = false;
            IdPinoAtual = entrada.IdPino;
            Ficha(IdPinoAtual).IniciarTurno(Rodada == 1 && entrada.Surpreso);
            return;
        }

        Encerrar();
    }

    public List<EntradaIniciativa2014> OrdenarIniciativas(IEnumerable<EntradaIniciativa2014> entradas)
    {
        var pinos = entradas.Where(e => e.IdPino > 0);
        if (Covil) pinos = pinos.Append(new(TurnoCovil, 20, int.MaxValue));
        return pinos.OrderByDescending(x => x.Iniciativa).ThenBy(x => x.Desempate).ToList();
    }

    public void Encerrar()
    {
        Rodada = 0;
        IdPinoAtual = 0;
        CovilDisponivel = false;
        JanelaLendaria = false;
        Ordem.Clear();
        MovimentoPendente = null;
    }

    public void ConcluirTurno(bool abrirJanelaLendaria, Func<int, bool>? disponivel = null)
    {
        if (abrirJanelaLendaria && !JanelaLendaria && IdPinoAtual > 0)
        {
            Ficha(IdPinoAtual).TerminarTurno();
            JanelaLendaria = true;
            return;
        }
        Avancar(disponivel);
    }

    public void RemoverEfeitosSemConcentracao()
    {
        foreach (var ficha in Fichas.Values)
            ficha.Efeitos.RemoveAll(e => e.ConcentracaoOrigem is not null && e.IdOrigem is int origem &&
                Fichas.GetValueOrDefault(origem)?.Concentracao != e.ConcentracaoOrigem);
    }
}

public sealed record EntradaIniciativa2014(int IdPino, int Iniciativa, int Desempate, bool Surpreso = false);

public sealed class EstadoCombatente2014
{
    public List<EfeitoCombate2014> Efeitos { get; set; } = [];
    public Dictionary<string, AfinidadeDano2014> Afinidades { get; set; } = [];
    public Dictionary<string, RecursoCombate2014> Recursos { get; set; } = [];
    public string? Concentracao { get; set; }
    public int? RodadasConcentracao { get; set; }
    public string? GatilhoPreparado { get; set; }
    public int? AlvoAjuda { get; set; }
    public bool Esquivando { get; set; }
    public bool Disparada { get; set; }
    public bool Desengajando { get; set; }
    public bool AcaoUsada { get; set; }
    public bool BonusUsado { get; set; }
    public bool ReacaoUsada { get; set; }
    public bool SurpresoNesteTurno { get; set; }
    public bool SurpresaPendente { get; set; }
    public decimal DeslocamentoGasto { get; set; }
    public int UsosLendariosMaximos { get; set; }
    public int UsosLendariosRestantes { get; set; }
    public int ResistenciasLendariasMaximas { get; set; }
    public int ResistenciasLendariasRestantes { get; set; }
    public bool UsouAcaoLendariaNesteIntervalo { get; set; }
    public bool UsaTestesMorte { get; set; }
    public int SucessosMorte { get; set; }
    public int FalhasMorte { get; set; }
    public bool Estavel { get; set; }
    public int RodadaUltimoTesteMorte { get; set; }
    public bool Morto => FalhasMorte >= 3;

    public bool Tem(Condicao2014 condicao) => Efeitos.Any(e => e.Condicao == condicao);

    public void IniciarTurno(bool surpreso)
    {
        AcaoUsada = BonusUsado = ReacaoUsada = surpreso;
        SurpresoNesteTurno = surpreso;
        DeslocamentoGasto = 0;
        AlvoAjuda = null;
        Esquivando = Disparada = Desengajando = false;
        GatilhoPreparado = null;
        UsosLendariosRestantes = UsosLendariosMaximos;
        UsouAcaoLendariaNesteIntervalo = false;

        AvancarEfeitos(Momento2014.InicioTurno);
        if (RodadasConcentracao is int duracao)
        {
            RodadasConcentracao = duracao - 1;
            if (RodadasConcentracao <= 0) EncerrarConcentracao();
        }

        foreach (var recurso in Recursos.Values.Where(r => r.RecargaD6 is >= 2 and <= 6 && r.Restantes == 0))
        {
            if (Random.Shared.Next(1, 7) >= recurso.RecargaD6)
                recurso.Restantes = recurso.Maximo;
        }
    }

    public void TerminarTurno()
    {
        AvancarEfeitos(Momento2014.FimTurno);
        if (SurpresoNesteTurno) ReacaoUsada = SurpresoNesteTurno = SurpresaPendente = false;
    }

    private void AvancarEfeitos(Momento2014 momento)
    {
        foreach (var efeito in Efeitos.Where(x => x.Momento == momento && x.Rodadas.HasValue))
            efeito.Rodadas--;

        Efeitos.RemoveAll(x => x.Rodadas <= 0);
    }

    public void EncerrarConcentracao()
    {
        Concentracao = null;
        RodadasConcentracao = null;
    }

    public void RestaurarMorte()
    {
        SucessosMorte = FalhasMorte = 0;
        Estavel = false;
    }
}

public sealed record EfeitoCombate2014
{
    public Condicao2014 Condicao { get; set; }
    public int? Rodadas { get; set; }
    public Momento2014 Momento { get; set; }
    public int? Cd { get; set; }
    public Atributo2014? Atributo { get; set; }
    public int? IdOrigem { get; set; }
    public bool TesteParaEscapar { get; set; }
    public string? ConcentracaoOrigem { get; set; }
}

public sealed class RecursoCombate2014
{
    public int Maximo { get; set; }
    public int Restantes { get; set; }
    public int? RecargaD6 { get; set; }
    public bool DescansoCurto { get; set; }
}
