using RPGdeMesa.Domain.Combate;

namespace RPGdeMesa.Presentation.ViewModel;

public partial class SessaoViewModel
{
    public Task<bool> ProporMovimentoTabuleiroAsync(int idPino, CasaTabuleiro origem, CasaTabuleiro destino) =>
        UsarControle2014Async((servico, estado, conta) => servico.ProporMovimentoTabuleiroAsync(estado, conta, idPino, origem, destino));

    public Task<bool> DecidirMovimentoTabuleiroAsync(Guid id, bool aceitar, bool disparada) =>
        UsarControle2014Async((servico, estado, conta) => servico.DecidirMovimentoTabuleiroAsync(estado, conta, id, aceitar, disparada));

    public Task<bool> ConfirmarRetornoTabuleiroAsync(Guid id) =>
        UsarControle2014Async((servico, estado, conta) => servico.ConfirmarRetornoTabuleiroAsync(estado, conta, id));
}
