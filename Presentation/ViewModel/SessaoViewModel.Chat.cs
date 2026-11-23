using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Presentation.ViewModel;

public partial class SessaoViewModel
{
    public string AbaChatAtiva { get; private set; } = CanaisSessao.Chat;
    public string TextoMensagem { get; set; } = string.Empty;
    public bool EnviandoMensagem { get; private set; }

    public IEnumerable<MensagemSessaoDto> MensagensAbaAtiva =>
        Estado.Mensagens.Where(m => string.Equals(m.Canal, AbaChatAtiva, StringComparison.OrdinalIgnoreCase));

    public void MudarAbaChat(string canal)
    {
        AbaChatAtiva = canal;
        Notificar();
    }

    public async Task EnviarMensagemChatAsync()
    {
        if (string.IsNullOrWhiteSpace(TextoMensagem) || Estado.IdSessao <= 0 || EnviandoMensagem)
            return;

        string mensagemParaEnviar = TextoMensagem.Trim();
        TextoMensagem = string.Empty;
        EnviandoMensagem = true;
        Notificar();

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, erro) = await _sessaoService.EnviarMensagemChatAsync(
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado,
                mensagemParaEnviar);

            if (!sucesso)
                FeedbackAcao = erro;

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;
        }
        finally
        {
            EnviandoMensagem = false;
            _dbLock.Release();
            Notificar();
        }
    }

    public async Task RegistrarRolagemAsync(ResultadoRolagemDto rolagem)
    {
        if (Estado.IdSessao <= 0) return;

        string nomeExibicao = string.IsNullOrWhiteSpace(_sessaoUsuario.NomeUsuario)
            ? $"Usuário #{_sessaoUsuario.IdUsuarioLogado}"
            : _sessaoUsuario.NomeUsuario;

        if (Estado.EhMestre)
        {
            nomeExibicao = $"{nomeExibicao} (Mestre)";
        }
        else
        {
            var meuJogador = Estado.Jogadores.FirstOrDefault(j => j.IdAccount == _sessaoUsuario.IdUsuarioLogado);
            if (meuJogador != null && !string.IsNullOrWhiteSpace(meuJogador.NomePersonagem))
            {
                nomeExibicao = $"{meuJogador.Username} ({meuJogador.NomePersonagem})";
            }
        }

        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, erro) = await _sessaoService.RegistrarRolagemDadosAsync(Estado.IdSessao, nomeExibicao, rolagem);
            if (!sucesso)
                FeedbackAcao = erro;

            var atualizado = await _sessaoService.SincronizarSessaoAsync(
                Estado.IdCampanha,
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado);

            if (atualizado != null)
                Estado = atualizado;

            AbaChatAtiva = CanaisSessao.Chat;
            Notificar();
        }
        finally
        {
            _dbLock.Release();
        }
    }
}