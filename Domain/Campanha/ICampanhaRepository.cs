using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Usuarios;

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
    Task<List<Monstro>> ListarTodosMonstrosAsync();
    Task AdicionarAsync(Campanha campanha);
    Task AtualizarAsync(Campanha campanha);
    Task RemoverCampanhaAsync(int idCampanha);
    Task RemoverUsuarioDaCampanhaAsync(int idAccount, int idCampanha);
    Task EnviarFichaCampanhaAsync(int idCampanha, int idPersonagem, string statusInicial = "Pendente");
    Task AtualizarStatusFichaCampanhaAsync(int idCampanha, int idPersonagem, string novoStatus);
    Task RemoverPersonagemCampanhaAsync(int idCampanha, int idPersonagem);
    Task AdicionarMonstroCampanhaAsync(int idCampanha, int idMonstro);
    Task AdicionarNpcCampanhaAsync(int idCampanha, int idNpc);
    Task AdicionarItemCampanhaAsync(int idCampanha, int idItem);
    Task RemoverMonstroCampanhaAsync(int idCampanha, int idMonstro);
    Task RemoverNpcCampanhaAsync(int idCampanha, int idNpc);
    Task RemoverItemCampanhaAsync(int idCampanha, int idItem);
    Task RemoverNpcAsync(int idNpc);
}