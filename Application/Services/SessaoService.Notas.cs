using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Application.Services;

public partial class SessaoService
{
    public Task<List<SessaoNota>> ListarNotasAsync(int idCampanha, int numeroSessao, int idUsuario) =>
        _sessaoRepo.ListarNotasAsync(idCampanha, numeroSessao, idUsuario);

    public Task<SessaoNota> AdicionarNotaAsync(Guid idNota, int idCampanha, int numeroSessao, int idUsuario,
        string nome, string conteudo) =>
        _sessaoRepo.AdicionarNotaAsync(new SessaoNota(idNota, idCampanha, numeroSessao, idUsuario, nome, conteudo), idUsuario);
}
