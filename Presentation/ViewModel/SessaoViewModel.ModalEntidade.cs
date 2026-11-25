using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Combate;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Sessoes;
using RPGdeMesa.Domain.ValueObjects;

namespace RPGdeMesa.Presentation.ViewModel;

public partial class SessaoViewModel
{
    public int IdAtaqueMonstroSelecionado { get; set; }
    public string AlvoAtaqueMonstroSelecionado { get; set; } = string.Empty;
    public string? FeedbackAtaqueEntidade { get; private set; }
    public bool ExecutandoAtaqueEntidade { get; private set; }
    public int IdItemNpcAtaqueSelecionado { get; set; }
    public string AlvoAtaqueNpcSelecionado { get; set; } = string.Empty;
    public int IdMagiaNpcSelecionada { get; set; }
    public string AlvoMagiaNpcSelecionado { get; set; } = string.Empty;
    public IEnumerable<PinoMapaDto> AlvosAtaqueMonstro => ObterAlvosDaEntidade(PinoMonstroEmVisualizacao?.IdPino ?? 0);
    public IEnumerable<PinoMapaDto> AlvosNpc => ObterAlvosDaEntidade(PinoNpcEmVisualizacao?.IdPino ?? 0);

    public Task AtacarComNpcSelecionadoAsync() =>
        AtacarPelaFichaAsync(PinoNpcEmVisualizacao, IdItemNpcAtaqueSelecionado, AlvoAtaqueNpcSelecionado);

    public Task ConjurarMagiaNpcSelecionadaAsync()
    {
        if (!Estado.EhMestre || NpcEmVisualizacao is not { } npc || PinoNpcEmVisualizacao is not { } origem)
            return Task.CompletedTask;
        var magia = npc.Magias.FirstOrDefault(x => x.IdMagia == IdMagiaNpcSelecionada)?.Magia;
        if (magia is null || !int.TryParse(AlvoMagiaNpcSelecionado, out int idAlvo))
        {
            FeedbackAtaqueEntidade = "Selecione uma magia cadastrada no NPC e um alvo.";
            Notificar();
            return Task.CompletedTask;
        }

        string expressao = RegrasMagiaCombate.ExtrairPrimeiraExpressaoDado(magia.DanoEfeito);
        var duracao = RegrasMagiaCombate.InterpretarDuracao(magia.Duracao);
        int bonus = AtributosBase.CalcularModificador(npc.Carisma) + RegrasMagiaCombate.CalcularBonusProficiencia(npc.Nivel);
        var recursos = Estado.Encontro.Fichas.GetValueOrDefault(origem.IdPino)?.Recursos;
        string circulo = $"Círculo {magia.CirculoNivel}";
        string? recurso = recursos?.ContainsKey(magia.Nome) == true ? magia.Nome :
            recursos?.ContainsKey(circulo) == true ? circulo : null;
        var comando = new HabilidadeMestre2014(magia.Nome, origem.IdPino, [idAlvo], new(Recurso: recurso),
            string.IsNullOrWhiteSpace(expressao) ? ResolucaoHabilidade2014.Efeito : ResolucaoHabilidade2014.AtaqueMagico,
            expressao, null, Atributo2014.Destreza, 10, false, Concentracao: duracao.SugereConcentracao,
            RodadasConcentracao: duracao.Tipo == TipoDuracaoMagia.PorRodadas ? duracao.Rodadas : null,
            BonusAtaque: bonus, Alcance: RegrasMagiaCombate.InterpretarAlcance(magia.Alcance).MetrosMaximos);
        return UsarControle2014Async((servico, estado, conta) => servico.UsarHabilidade2014Async(estado, conta, comando));
    }

    public Task AtacarComMonstroSelecionadoAsync() =>
        AtacarPelaFichaAsync(PinoMonstroEmVisualizacao, IdAtaqueMonstroSelecionado, AlvoAtaqueMonstroSelecionado);

    public Monstro? MonstroEmVisualizacao { get; private set; }
    public PinoMapaDto? PinoMonstroEmVisualizacao { get; private set; }
    public int PvAtualMonstroEdicao { get; set; }
    public NPC? NpcEmVisualizacao { get; private set; }
    public PinoMapaDto? PinoNpcEmVisualizacao { get; private set; }
    public int PvAtualNpcEdicao { get; set; }
    public bool SalvandoPvEntidade { get; private set; }

    public async Task SalvarPvAtualMonstroAsync()
    {
        if (!Estado.EhMestre || MonstroEmVisualizacao == null || SalvandoPvEntidade) return;

        int novoPv = Math.Clamp(PvAtualMonstroEdicao, 0, Math.Max(1, MonstroEmVisualizacao.PvMaximo));
        PvAtualMonstroEdicao = novoPv;

        await AtualizarPvEntidadeInternoAsync(TiposPinoSessao.Monstro, MonstroEmVisualizacao.IdMonstro, novoPv, PinoMonstroEmVisualizacao?.IdPino);
    }

    public async Task RestaurarPvMaximoMonstroAsync()
    {
        if (!Estado.EhMestre || MonstroEmVisualizacao == null || SalvandoPvEntidade) return;

        PvAtualMonstroEdicao = MonstroEmVisualizacao.PvMaximo;
        await AtualizarPvEntidadeInternoAsync(TiposPinoSessao.Monstro, MonstroEmVisualizacao.IdMonstro, MonstroEmVisualizacao.PvMaximo, PinoMonstroEmVisualizacao?.IdPino);
    }

    public async Task RestaurarPvMonstroPeloPainelAsync(Monstro monstro)
    {
        if (!Estado.EhMestre || SalvandoPvEntidade) return;
        await AtualizarPvEntidadeInternoAsync(TiposPinoSessao.Monstro, monstro.IdMonstro, monstro.PvMaximo);
    }

    public async Task SalvarPvAtualNpcAsync()
    {
        if (!Estado.EhMestre || NpcEmVisualizacao == null || SalvandoPvEntidade) return;

        int pvMax = SessaoService.CalcularPvMaximoNpc(NpcEmVisualizacao);
        int novoPv = Math.Clamp(PvAtualNpcEdicao, 0, pvMax);
        PvAtualNpcEdicao = novoPv;

        await AtualizarPvEntidadeInternoAsync(TiposPinoSessao.Npc, NpcEmVisualizacao.IdNpc, novoPv, PinoNpcEmVisualizacao?.IdPino);
    }

    public async Task RestaurarPvMaximoNpcAsync()
    {
        if (!Estado.EhMestre || NpcEmVisualizacao == null || SalvandoPvEntidade) return;

        int pvMax = SessaoService.CalcularPvMaximoNpc(NpcEmVisualizacao);
        PvAtualNpcEdicao = pvMax;
        await AtualizarPvEntidadeInternoAsync(TiposPinoSessao.Npc, NpcEmVisualizacao.IdNpc, pvMax, PinoNpcEmVisualizacao?.IdPino);
    }

    public async Task RestaurarPvNpcPeloPainelAsync(NPC npc)
    {
        if (!Estado.EhMestre || SalvandoPvEntidade) return;
        int pvMax = SessaoService.CalcularPvMaximoNpc(npc);
        await AtualizarPvEntidadeInternoAsync(TiposPinoSessao.Npc, npc.IdNpc, pvMax);
    }

    private async Task AtualizarPvEntidadeInternoAsync(string tipoEntidade, int idEntidade, int novoPv, int? idPino = null)
    {
        SalvandoPvEntidade = true;
        Notificar();

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, msg) = await _sessaoService.AtualizarPvPinoNoMapaAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                Estado.IdMapaAtivo,
                _sessaoUsuario.IdUsuarioLogado,
                tipoEntidade,
                idEntidade,
                novoPv,
                idPino);

            FeedbackAcao = msg;

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
            {
                Estado = atualizado;

                if (tipoEntidade == TiposPinoSessao.Monstro && MonstroEmVisualizacao?.IdMonstro == idEntidade)
                {
                    PinoMonstroEmVisualizacao = Estado.Pinos.FirstOrDefault(p =>
                        p.IdMapa == Estado.IdMapaAtivo &&
                        p.TipoEntidade == TiposPinoSessao.Monstro &&
                        p.IdPino == PinoMonstroEmVisualizacao?.IdPino);
                }
                else if (tipoEntidade == TiposPinoSessao.Npc && NpcEmVisualizacao?.IdNpc == idEntidade)
                {
                    PinoNpcEmVisualizacao = Estado.Pinos.FirstOrDefault(p =>
                        p.IdMapa == Estado.IdMapaAtivo &&
                        p.TipoEntidade == TiposPinoSessao.Npc &&
                        p.IdPino == PinoNpcEmVisualizacao?.IdPino);
                }
            }
        }
        finally
        {
            SalvandoPvEntidade = false;
            _dbLock.Release();
            Notificar();
        }
    }

    public void FecharModalEntidade()
    {
        MonstroEmVisualizacao = null;
        PinoMonstroEmVisualizacao = null;
        NpcEmVisualizacao = null;
        PinoNpcEmVisualizacao = null;
        Notificar();
    }
}
