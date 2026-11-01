using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Common;

namespace RPGdeMesa.Application.Services;

public class MonstroService
{
    private readonly IMonstroRepository _monstroRepo;

    public MonstroService(IMonstroRepository monstroRepo) => _monstroRepo = monstroRepo;

    public Task<List<Monstro>> ObterMonstrosPorUsuarioAsync(int idAccount) =>
        _monstroRepo.ListarPorUsuarioAsync(idAccount);

    public async Task<(bool Sucesso, string Mensagem)> CriarMonstroAsync(int idAccount, Monstro monstro)
    {
        try
        {
            monstro.PrepararNovoMonstro(idAccount);
            await _monstroRepo.AdicionarAsync(monstro);
            return (true, "Monstro forjado e adicionado ao bestiário com sucesso!");
        }
        catch (DomainException dex)
        {
            return (false, dex.Message);
        }
        catch (Exception ex)
        {
            string erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            return (false, $"Erro ao salvar monstro: {erroDetalhado}");
        }
    }

    public Task<Monstro?> ObterPorIdAsync(int idMonstro) =>
        _monstroRepo.ObterPorIdAsync(idMonstro);
}