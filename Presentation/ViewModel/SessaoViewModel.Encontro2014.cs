using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Combate;
using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Presentation.ViewModel;

public partial class SessaoViewModel
{
    public bool ExibirPainel2014 { get; private set; }
    private bool _ataqueOportunidade2014;
    public bool AtaqueOportunidade2014 => _ataqueOportunidade2014;
    private (int IdPino, int Coluna, int Linha, decimal Distancia, bool Disparada)? _deslocamentoPendente2014;

    public void AbrirPainel2014()
    {
        if (!Estado.EhMestre || !Estado.SessaoAtiva) return;

        ExibirCombateMestre = false;
        ExibirModalDados = false;
        ExibirModalFicha = false;
        FeedbackAtaqueEntidade = null;
        ExibirPainel2014 = true;
        AbaChatAtiva = CanaisSessao.Combate;
        Notificar();
    }

    public void FecharPainel2014()
    {
        if (ExecutandoAtaqueEntidade) return;

        ExibirPainel2014 = false;
        Notificar();
    }

    public Task<bool> UsarControle2014Async(Func<SessaoService, SessaoEstadoDto, int, Task<(bool Sucesso, string Mensagem)>> comando) =>
        ExecutarComandoCombateUiAsync((estado, conta) => comando(_sessaoService, estado, conta));

    public Task AvancarEncontro2014Async() =>
        UsarControle2014Async((servico, estado, conta) => servico.AvancarIniciativaAsync(estado, conta));

    public Task ResponderTeste2014Async(Guid id) =>
        UsarControle2014Async((servico, estado, conta) => servico.ResponderTeste2014Async(estado, conta, id));

    public Task RolarMorteJogador2014Async(int id) =>
        UsarControle2014Async((servico, estado, conta) => servico.ResolverMorte2014Async(estado, conta, id, "rolar"));

    public void AbrirAtaqueOportunidade2014(int id)
    {
        if (!Estado.EhMestre ||
            Estado.Encontro.MovimentoPendente is not { } movimento ||
            !movimento.ReacoesPendentes.Contains(id))
            return;

        ExibirPainel2014 = false;
        ExibirModalDados = false;
        ExibirModalFicha = false;
        AtaqueMestreComAtacanteFixo = true;
        _ataqueOportunidade2014 = true;
        TipoAcaoMestre = TipoAcao2014.Reacao;
        SelecionarAtacanteMestreInterno(id);
        IdPinoAlvoMestre = movimento.IdMovendo;
        ExibirCombateMestre = true;
        AbaChatAtiva = CanaisSessao.Combate;
        Notificar();
    }

    public Task DispensarOportunidade2014Async(int id) =>
        UsarControle2014Async((s, e, conta) => s.DispensarOportunidade2014Async(e, conta, id));

    private void ConcluirDeslocamento2014()
    {
        if (_deslocamentoPendente2014 is not { } movimento ||
            Estado.Encontro.MovimentoPendente?.IdMovendo == movimento.IdPino)
            return;

        _deslocamentoPendente2014 = null;
        if (Estado.Pinos.Any(p => p.IdPino == movimento.IdPino && p.Coluna == movimento.Coluna && p.Linha == movimento.Linha))
        {
            _distanciaMovidaNoTurno += movimento.Distancia;
            if (movimento.Disparada) _disparadaUsadaNoTurno = true;
            FeedbackAcao = null;
        }
        else
        {
            FeedbackAcao = "O movimento foi interrompido pela reação.";
        }
    }
}