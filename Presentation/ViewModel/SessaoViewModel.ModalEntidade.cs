using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Presentation.ViewModel;

public partial class SessaoViewModel
{
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

        await AtualizarPvEntidadeInternoAsync(TiposPinoSessao.Monstro, MonstroEmVisualizacao.IdMonstro, novoPv);
    }

    public async Task RestaurarPvMaximoMonstroAsync()
    {
        if (!Estado.EhMestre || MonstroEmVisualizacao == null || SalvandoPvEntidade) return;

        PvAtualMonstroEdicao = MonstroEmVisualizacao.PvMaximo;
        await AtualizarPvEntidadeInternoAsync(TiposPinoSessao.Monstro, MonstroEmVisualizacao.IdMonstro, MonstroEmVisualizacao.PvMaximo);
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

        await AtualizarPvEntidadeInternoAsync(TiposPinoSessao.Npc, NpcEmVisualizacao.IdNpc, novoPv);
    }

    public async Task RestaurarPvMaximoNpcAsync()
    {
        if (!Estado.EhMestre || NpcEmVisualizacao == null || SalvandoPvEntidade) return;

        int pvMax = SessaoService.CalcularPvMaximoNpc(NpcEmVisualizacao);
        PvAtualNpcEdicao = pvMax;
        await AtualizarPvEntidadeInternoAsync(TiposPinoSessao.Npc, NpcEmVisualizacao.IdNpc, pvMax);
    }

    public async Task RestaurarPvNpcPeloPainelAsync(NPC npc)
    {
        if (!Estado.EhMestre || SalvandoPvEntidade) return;
        int pvMax = SessaoService.CalcularPvMaximoNpc(npc);
        await AtualizarPvEntidadeInternoAsync(TiposPinoSessao.Npc, npc.IdNpc, pvMax);
    }

    private async Task AtualizarPvEntidadeInternoAsync(string tipoEntidade, int idEntidade, int novoPv)
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
                novoPv);

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
                        p.IdEntidade == idEntidade);
                }
                else if (tipoEntidade == TiposPinoSessao.Npc && NpcEmVisualizacao?.IdNpc == idEntidade)
                {
                    PinoNpcEmVisualizacao = Estado.Pinos.FirstOrDefault(p =>
                        p.IdMapa == Estado.IdMapaAtivo &&
                        p.TipoEntidade == TiposPinoSessao.Npc &&
                        p.IdEntidade == idEntidade);
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