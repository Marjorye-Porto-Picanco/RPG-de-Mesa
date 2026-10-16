using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using RPGdeMesa.Services;

namespace RPGdeMesa.ViewModel
{
    public class RegisterViewModel
    {
        private readonly UsuarioService _usuarioService;
        private readonly AuthService _authService;
        private readonly NavigationManager _navigation;

        public RegisterViewModel(UsuarioService usuarioService, AuthService authService, NavigationManager navigation)
        {
            _usuarioService = usuarioService;
            _authService = authService;
            _navigation = navigation;
        }

        public bool ExibirModal { get; set; } = false;
        public string Usuario { get; set; } = "";
        public string Email { get; set; } = "";
        public string Senha { get; set; } = "";
        public string ConfirmarSenha { get; set; } = "";
        public bool AceitouTermos { get; set; } = false;

        public string MensagemFeedback { get; set; } = "";
        public bool Sucesso { get; set; } = false;
        public bool IsProcessing { get; set; } = false;

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();

        public void FecharModal()
        {
            ExibirModal = false;
            NotifyStateChanged();
        }

        public void AbrirModal()
        {
            ExibirModal = true;
            NotifyStateChanged();
        }

        public void AceitarTermosEFechar()
        {
            AceitouTermos = true;
            ExibirModal = false;
            NotifyStateChanged();
        }

        public async Task RealizarCadastroAsync()
        {
            if (IsProcessing) return;
            try
            {
                IsProcessing = true;
                MensagemFeedback = "";
                NotifyStateChanged();

                var resultado = await _usuarioService.CadastrarAsync(Usuario, Email, Senha, ConfirmarSenha, AceitouTermos);
                Sucesso = resultado.Sucesso;
                MensagemFeedback = resultado.Mensagem;

                if (Sucesso)
                {
                    await Task.Delay(1200);
                    _navigation.NavigateTo("/login");
                }
            }
            finally
            {
                IsProcessing = false;
                NotifyStateChanged();
            }
        }

        public async Task RegistrarComGoogleAsync()
        {
            if (IsProcessing) return;

            try
            {
                IsProcessing = true;
                MensagemFeedback = "Aguardando autenticação do Google...";
                NotifyStateChanged();

                (bool Sucesso, string Mensagem) resultadoCadastro = (false, "");

                var (email, nome) = await _authService.ObterDadosUsuarioGoogleAsync(async (emailGoogle) =>
                {
                    resultadoCadastro = await _usuarioService.CadastrarSocialAsync(emailGoogle, "aventureiro");
                    return !resultadoCadastro.Sucesso; 
                });

                if (string.IsNullOrEmpty(email))
                {
                    MensagemFeedback = "Não foi possível obter o e-mail da conta Google ou a operação foi cancelada.";
                    Sucesso = false;
                    return;
                }
         
                Sucesso = resultadoCadastro.Sucesso;
                MensagemFeedback = resultadoCadastro.Mensagem;

                if (Sucesso)
                {
                    await Task.Delay(1500);
                    _navigation.NavigateTo("/login");
                }
                else
                {
                    if (MensagemFeedback.Contains("já cadastrado", StringComparison.OrdinalIgnoreCase) ||
                        MensagemFeedback.Contains("já possui", StringComparison.OrdinalIgnoreCase))
                    {
                        MensagemFeedback += " Redirecionando para a página de login...";
                        NotifyStateChanged();

                        await Task.Delay(2500);
                        _navigation.NavigateTo("/login");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO REGISTRO SOCIAL GOOGLE]: {ex.Message}");
                MensagemFeedback = "Ocorreu um erro ao processar o registro com o Google.";
                Sucesso = false;
            }
            finally
            {
                IsProcessing = false;
                NotifyStateChanged();
            }
        }

        public async Task RegistrarComFacebookAsync() { }
        public async Task RegistrarComGitHubAsync() { }
    }
}