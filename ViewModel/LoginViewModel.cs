using Microsoft.AspNetCore.Components;
using RPGdeMesa.Services;
using System;
using System.Threading.Tasks;

namespace RPGdeMesa.ViewModel
{
    public class LoginViewModel
    {
        private readonly UsuarioService _usuarioService;
        private readonly AuthService _authService;
        private readonly NavigationManager _navigation;
        private readonly SessaoUsuarioService _sessaoUsuario;

        public LoginViewModel(
            UsuarioService usuarioService,
            AuthService authService,
            NavigationManager navigation,
            SessaoUsuarioService sessaoUsuario)
        {
            _usuarioService = usuarioService;
            _authService = authService;
            _navigation = navigation;
            _sessaoUsuario = sessaoUsuario;
        }

        public string LoginOuEmail { get; set; } = "";
        public string Senha { get; set; } = "";
        public string MensagemFeedback { get; set; } = "";
        public bool Sucesso { get; set; } = false;
        public bool IsProcessing { get; set; } = false;

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();

        public async Task EntrarAsync()
        {
            if (IsProcessing) return;

            try
            {
                IsProcessing = true;
                MensagemFeedback = "";
                NotifyStateChanged();

                var resultado = await _usuarioService.AutenticarAsync(LoginOuEmail, Senha);

                Sucesso = resultado.Sucesso;
                MensagemFeedback = resultado.Mensagem;

                if (Sucesso && resultado.UsuarioLogado != null)
                {
                    _sessaoUsuario.FazerLogin(
                        resultado.UsuarioLogado.IdAccount,
                        resultado.UsuarioLogado.UsarName,
                        resultado.UsuarioLogado.EMail
                    );

                    await Task.Delay(800);
                    _navigation.NavigateTo("/campanhas");
                }
            }
            finally
            {
                IsProcessing = false;
                NotifyStateChanged();
            }
        }

        public async Task LoginComGoogleAsync()
        {
            if (IsProcessing) return;

            try
            {
                IsProcessing = true;
                MensagemFeedback = "Aguardando autenticação do Google...";
                NotifyStateChanged();

                var (email, nome) = await _authService.ObterDadosUsuarioGoogleAsync();

                if (string.IsNullOrEmpty(email))
                {
                    MensagemFeedback = "Não foi possível obter o e-mail da conta Google ou a operação foi cancelada.";
                    Sucesso = false;
                    return;
                }

                var resultado = await _usuarioService.AutenticarOuCadastrarSocialAsync(email, nome ?? "aventureiro");

                Sucesso = resultado.Sucesso;
                MensagemFeedback = resultado.Mensagem;

                if (Sucesso && resultado.UsuarioLogado != null)
                {
                    _sessaoUsuario.FazerLogin(
                        resultado.UsuarioLogado.IdAccount,
                        resultado.UsuarioLogado.UsarName,
                        resultado.UsuarioLogado.EMail
                    );

                    await Task.Delay(1000);
                    _navigation.NavigateTo("/campanhas");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO LOGIN SOCIAL GOOGLE]: {ex.Message}");
                MensagemFeedback = "Ocorreu um erro ao processar o login com o Google.";
                Sucesso = false;
            }
            finally
            {
                IsProcessing = false;
                NotifyStateChanged();
            }
        }

        public async Task LoginComFacebookAsync() { }
        public async Task LoginComGitHubAsync() { }
    }
}