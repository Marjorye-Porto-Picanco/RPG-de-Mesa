using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Sessoes;

namespace RPGdeMesa.Presentation.ViewModel;

public partial class SessaoViewModel
{
    public string AbaChatAtiva { get; private set; } = CanaisSessao.Chat;
    public string TextoMensagem { get; set; } = string.Empty;
    public bool EnviandoMensagem { get; private set; }
    public string? FeedbackChat { get; private set; }
    private (string Texto, Guid Id)? _mensagemPendente;

    public IEnumerable<MensagemSessaoDto> MensagensAbaAtiva =>
        Estado.Mensagens.Where(m => string.Equals(m.Canal, AbaChatAtiva, StringComparison.OrdinalIgnoreCase)).Reverse();

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
        if (_mensagemPendente is not { } pendente || pendente.Texto != mensagemParaEnviar)
            _mensagemPendente = (mensagemParaEnviar, Guid.NewGuid());
        var idOperacao = _mensagemPendente.Value.Id;
        TextoMensagem = string.Empty;
        EnviandoMensagem = true;
        FeedbackChat = "Enviando…";
        Notificar();

        bool enviada = false;
        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, erro) = await _sessaoService.EnviarMensagemChatAsync(
                Estado.IdSessao,
                _sessaoUsuario.IdUsuarioLogado,
                mensagemParaEnviar,
                idOperacao);

            enviada = sucesso;
            FeedbackChat = sucesso ? "Mensagem enviada." : erro;
            if (sucesso)
                _mensagemPendente = null;
            else
                TextoMensagem = mensagemParaEnviar;
        }
        finally
        {
            EnviandoMensagem = false;
            _dbLock.Release();
            Notificar();
        }

        if (enviada)
            await SincronizarAgoraAsync();
    }

    public async Task<bool> RegistrarRolagemAsync(ResultadoRolagemDto rolagem)
    {
        if (Estado.IdSessao <= 0) return false;

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

        bool registrada;
        await _dbLock.WaitAsync();
        try
        {
            var (sucesso, erro) = await _sessaoService.RegistrarRolagemDadosAsync(Estado.IdSessao, nomeExibicao, rolagem);
            registrada = sucesso;
            if (!registrada)
                FeedbackAcao = erro;

            AbaChatAtiva = CanaisSessao.Chat;
            Notificar();
        }
        finally
        {
            _dbLock.Release();
        }
        if (registrada)
            await SincronizarAgoraAsync();
        return registrada;
    }
}
