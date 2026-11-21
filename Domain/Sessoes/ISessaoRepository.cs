using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Personagens;

namespace RPGdeMesa.Domain.Sessoes;

public interface ISessaoRepository
{
    Task<Campanha?> ObterCampanhaParaSessaoAsync(int idCampanha);
    Task<SessaoMesa> ObterOuCriarSessaoPorCampanhaAsync(int idCampanha, int? idMapaInicial = null);
    Task<SessaoMesa?> ObterSessaoCompletaAsync(int idSessao);
    Task<bool> ConectarParticipanteAsync(int idSessao, int idAccount, int? idPersonagem);
    Task AtualizarHeartbeatAsync(int idSessao, int idAccount);
    Task<bool> DesconectarParticipanteAsync(int idSessao, int idAccount);
    Task AtualizarMapaAtivoAsync(int idSessao, int? idMapa);
    Task AdicionarMensagemAsync(SessaoMensagem mensagem);
    Task<List<SessaoMensagem>> ListarMensagensAsync(int idSessao, int limitePorCanal = 80);
    Task<Personagem?> ObterFichaCompletaAsync(int idPersonagem);
}