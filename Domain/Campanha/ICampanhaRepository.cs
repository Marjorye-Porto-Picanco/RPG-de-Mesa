using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Campanhas;

namespace RPGdeMesa.Domain.Campanhas;

public interface ICampanhaRepository
{
    Task<Campanha?> ObterPorIdComDetalhesAsync(int idCampanha);
    Task<Campanha?> ObterPorCodigoConviteAsync(string codigoConvite);
    Task<List<Campanha>> ListarPorMestreAsync(int idAccount);
    Task<List<Campanha>> ListarPorJogadorAsync(int idAccount);
    Task<bool> CodigoConviteExisteAsync(string codigoConvite);
    Task<bool> UsuarioJaParticipaAsync(int idAccount, int idCampanha);
    Task AdicionarUsuarioNaCampanhaAsync(UsuarioCampanha vinculo);
    Task<List<NPC>> ListarTodosNpcsAsync();
    Task<List<Item>> ListarTodosItensAsync();
    Task AdicionarAsync(Campanha campanha);
    Task AtualizarAsync(Campanha campanha);

    // Novos métodos contratuais para remoção de vínculos na sessão
    Task RemoverMonstroCampanhaAsync(int idCampanha, int idMonstro);
    Task RemoverNpcAsync(int idNpc);
}