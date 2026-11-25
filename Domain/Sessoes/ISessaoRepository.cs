using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Combate;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Magias;
using RPGdeMesa.Domain.Personagens;

namespace RPGdeMesa.Domain.Sessoes;

public interface ISessaoRepository
{
    Task<Campanha?> ObterCampanhaParaSessaoAsync(int idCampanha, CancellationToken cancellationToken = default);
    Task<bool> SessaoEstaAtivaAsync(int idCampanha);
    Task<SessaoMesa?> ObterSessaoPorCampanhaAsync(int idCampanha);
    Task<SessaoMesa?> ObterSessaoPorIdAsync(int idSessao, CancellationToken cancellationToken = default);
    Task<SessaoMesa> AbrirOuReabrirSessaoAsync(int idCampanha, int? idMapaInicial = null);
    Task<SessaoMesa?> ObterSessaoCompletaAsync(int idSessao, CancellationToken cancellationToken = default);
    Task<SessaoSinal?> LerSinalSessaoAsync(int idSessao, CancellationToken cancellationToken);
    Task<bool> ConectarParticipanteAsync(int idSessao, int idAccount, int? idPersonagem);
    Task AtualizarHeartbeatAsync(int idSessao, int idAccount, CancellationToken cancellationToken = default);
    Task<bool> DesconectarParticipanteAsync(int idSessao, int idAccount);
    Task FinalizarSessaoAsync(int idSessao);
    Task AtualizarResumoSessaoAsync(int idSessao, string? titulo, string? resumo, int? xpTotalDistribuido = null);
    Task AtualizarMapaAtivoAsync(int idSessao, int? idMapa);
    Task AtualizarTurnoJogadorAsync(int idCampanha, int idSessao, int? idAccountTurno,
        int? idAccountQueDeveEstarNoTurno, Guid operacao, string aviso);
    Task AdicionarMensagemAsync(SessaoMensagem mensagem);
    Task AdicionarMensagensDaOperacaoAsync(int idSessao, Guid operacao, IReadOnlyList<SessaoMensagem> mensagens);
    Task AlterarCombate2014Async(int idSessao, int idMapa, int idConta, bool participantePermitido, Guid operacao,
        Func<ControleCombate2014, IReadOnlyList<SessaoPino>, ResultadoComandoCombate2014> alterar);
    Task<List<SessaoMensagem>> ListarMensagensAsync(int idSessao, int limitePorCanal = 80,
        int? desdeId = null, CancellationToken cancellationToken = default);
    Task<List<SessaoMesa>> ListarHistoricoSessoesAsync(int idCampanha);
    Task<Dictionary<int, (int TotalMensagensChat, int TotalRolagens)>> ContarAtividadePorSessoesAsync(IEnumerable<int> idsSessao);
    Task<Personagem?> ObterFichaCompletaAsync(int idPersonagem);
    Task<List<Personagem>> ListarFichasAceitasCompletasAsync(int idCampanha);
    Task<List<Item>> ListarItensCampanhaAsync(int idCampanha);
    Task<List<Magia>> ListarMagiasCatalogoAsync(CancellationToken cancellationToken = default);
    Task SalvarMagiasPersonagemAsync(int idPersonagem, IEnumerable<int> idsMagia);
    Task SalvarCirculosMagicosAsync(int idPersonagem, IEnumerable<PersonagemCirculoMagico> circulos);
    Task AplicarDescansoCirculosMagicosAsync(int idPersonagem, bool longo, IEnumerable<int> circulosRecuperados);

    Task<List<SessaoPino>> ListarPinosSessaoAsync(int idSessao, CancellationToken cancellationToken = default);
    Task SalvarOuMoverPinoAsync(int idSessao, int? idMapa, string tipoEntidade, int idEntidade, int coluna, int linha, bool visivel, int? idPino = null);
    Task CriarInstanciaPinoAsync(int idSessao, int idMapa, int idMestre, string tipoEntidade, int idEntidade, int coluna, int linha, bool visivel, Guid operacao);
    Task AlterarVisibilidadePinoAsync(int idSessao, int? idMapa, string tipoEntidade, int idEntidade, bool visivel, int? idPino = null);
    Task AtualizarPvPinoAsync(int idSessao, int? idMapa, string tipoEntidade, int idEntidade, int pvAtual, int? idPino = null);
    Task AtualizarPvPersonagemAsync(int idPersonagem, int pvAtual);
    Task RemoverPinoAsync(int idSessao, int? idMapa, string tipoEntidade, int idEntidade, int? idPino = null);
    Task AtualizarEstadoCombateParticipanteAsync(int idSessao, int idAccount, bool? esquivando = null, bool? escondido = null);
}
