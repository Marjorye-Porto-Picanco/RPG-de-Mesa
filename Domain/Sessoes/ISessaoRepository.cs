using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Personagens;

namespace RPGdeMesa.Domain.Sessoes;

public interface ISessaoRepository
{
    Task<Campanha?> ObterCampanhaParaSessaoAsync(int idCampanha);
    Task<bool> SessaoEstaAtivaAsync(int idCampanha);
    Task<SessaoMesa?> ObterSessaoPorCampanhaAsync(int idCampanha);
    Task<SessaoMesa?> ObterSessaoPorIdAsync(int idSessao);
    Task<SessaoMesa> AbrirOuReabrirSessaoAsync(int idCampanha, int? idMapaInicial = null);
    Task<SessaoMesa?> ObterSessaoCompletaAsync(int idSessao);
    Task<bool> ConectarParticipanteAsync(int idSessao, int idAccount, int? idPersonagem);
    Task AtualizarHeartbeatAsync(int idSessao, int idAccount);
    Task<bool> DesconectarParticipanteAsync(int idSessao, int idAccount);
    Task FinalizarSessaoAsync(int idSessao);
    Task AtualizarResumoSessaoAsync(int idSessao, string? titulo, string? resumo, int? xpTotalDistribuido = null);
    Task AtualizarMapaAtivoAsync(int idSessao, int? idMapa);
    Task AdicionarMensagemAsync(SessaoMensagem mensagem);
    Task<List<SessaoMensagem>> ListarMensagensAsync(int idSessao, int limitePorCanal = 80);
    Task<List<SessaoMesa>> ListarHistoricoSessoesAsync(int idCampanha);
    Task<Dictionary<int, (int TotalMensagensChat, int TotalRolagens)>> ContarAtividadePorSessoesAsync(IEnumerable<int> idsSessao);
    Task<Personagem?> ObterFichaCompletaAsync(int idPersonagem);
    Task<List<Personagem>> ListarFichasAceitasCompletasAsync(int idCampanha);
    Task<List<Item>> ListarItensCampanhaAsync(int idCampanha);

    Task<List<SessaoPino>> ListarPinosSessaoAsync(int idSessao);
    Task SalvarOuMoverPinoAsync(int idSessao, int? idMapa, string tipoEntidade, int idEntidade, int coluna, int linha, bool visivel);
    Task AlterarVisibilidadePinoAsync(int idSessao, int? idMapa, string tipoEntidade, int idEntidade, bool visivel);
    Task RemoverPinoAsync(int idSessao, int? idMapa, string tipoEntidade, int idEntidade);
}