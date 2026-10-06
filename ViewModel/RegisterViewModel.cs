using Microsoft.AspNetCore.Components;
using RPGdeMesa.Services;

namespace RPGdeMesa.ViewModel
{
    public class RegisterViewModel
    {
        private readonly UsuarioService _usuarioService;
        private readonly NavigationManager _navigation;

        public RegisterViewModel(UsuarioService usuarioService, NavigationManager navigation)
        {
            _usuarioService = usuarioService;
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
            string clientId = "73720001215-0ts3vufh4h4vslttbue67nf9p85auf9e.apps.googleusercontent.com";
            string redirectUri = DeviceInfo.Platform == DevicePlatform.WinUI ? "http://localhost:5000/" : "rpgdemesa://";
            string urlAutenticacao = $"https://accounts.google.com/o/oauth2/v2/auth?client_id={clientId}&redirect_uri={redirectUri}&response_type=code&scope=email%20profile";

            await ProcessarRegistroSocialAsync("Google", urlAutenticacao, redirectUri);
        }

        public async Task RegistrarComFacebookAsync() { }
        public async Task RegistrarComGitHubAsync() { }

        private async Task ProcessarRegistroSocialAsync(string provedor, string urlAutenticacao, string redirectUri)
        {
            if (IsProcessing) return;

            try
            {
                IsProcessing = true;
                MensagemFeedback = "Aguardando autenticação...";
                NotifyStateChanged();

                string codigoAutorizacao = null;
                System.Net.HttpListener listener = null;
                System.Net.HttpListenerContext context = null;

#if WINDOWS
                listener = new System.Net.HttpListener();
                listener.Prefixes.Add(redirectUri);
                listener.Start();

                await Launcher.Default.OpenAsync(new Uri(urlAutenticacao));

                context = await listener.GetContextAsync();
                codigoAutorizacao = context.Request.QueryString["code"];
#elif ANDROID
                var authResult = await WebAuthenticator.Default.AuthenticateAsync(
                    new Uri(urlAutenticacao),
                    new Uri(redirectUri)
                );

                if (authResult?.Properties != null && authResult.Properties.TryGetValue("code", out var codeVal))
                {
                    codigoAutorizacao = codeVal;
                }
#else
                await Launcher.Default.OpenAsync(new Uri(urlAutenticacao));
                MensagemFeedback = "Plataforma não suportada para captura automática.";
                IsProcessing = false;
                NotifyStateChanged();
                return;
#endif

                if (!string.IsNullOrEmpty(codigoAutorizacao))
                {
                    string emailRealDoGoogle = "";
                    string nomeRealDoGoogle = "";

                    try
                    {
                        using var httpClient = new HttpClient();
                        string clientId = "73720001215-0ts3vufh4h4vslttbue67nf9p85auf9e.apps.googleusercontent.com";
                        string clientSecret = "";

                        var tokenRequestData = new Dictionary<string, string>
                        {
                            { "code", codigoAutorizacao },
                            { "client_id", clientId },
                            { "client_secret", clientSecret },
                            { "redirect_uri", redirectUri },
                            { "grant_type", "authorization_code" }
                        };

                        var tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(tokenRequestData));
                        string tokenJsonResponse = await tokenResponse.Content.ReadAsStringAsync();

                        if (tokenResponse.IsSuccessStatusCode)
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(tokenJsonResponse);
                            if (doc.RootElement.TryGetProperty("access_token", out var accessTokenProp))
                            {
                                string accessToken = accessTokenProp.GetString();

                                var requestUser = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v3/userinfo");
                                requestUser.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                                var userInfoResponse = await httpClient.SendAsync(requestUser);
                                string userInfoJson = await userInfoResponse.Content.ReadAsStringAsync();

                                if (userInfoResponse.IsSuccessStatusCode)
                                {
                                    using var userDoc = System.Text.Json.JsonDocument.Parse(userInfoJson);

                                    if (userDoc.RootElement.TryGetProperty("email", out var emailProp))
                                        emailRealDoGoogle = emailProp.GetString();

                                    if (userDoc.RootElement.TryGetProperty("name", out var nameProp))
                                        nomeRealDoGoogle = nameProp.GetString();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[EXCEÇÃO NO FLUXO GOOGLE]: {ex.Message}");
                    }

                    if (string.IsNullOrEmpty(emailRealDoGoogle))
                    {
                        MensagemFeedback = "Erro: O Google não retornou o e-mail do usuário.";
                        Sucesso = false;
                        listener?.Stop();
                        IsProcessing = false;
                        NotifyStateChanged();
                        return;
                    }

                    var resultado = await _usuarioService.CadastrarSocialAsync(emailRealDoGoogle, nomeRealDoGoogle);
                    Sucesso = resultado.Sucesso;
                    MensagemFeedback = resultado.Mensagem;

#if WINDOWS
                    if (context != null)
                    {
                        var response = context.Response;
                        response.ContentType = "text/html; charset=utf-8";

                        string responseString = "";
                        try
                        {
                            using var stream = await FileSystem.OpenAppPackageFileAsync("retorno-auth.html");
                            using var reader = new StreamReader(stream);
                            responseString = await reader.ReadToEndAsync();
                        }
                        catch
                        {
                            responseString = "<html><body><h3>Autenticação realizada. Pode fechar esta janela.</h3></body></html>";
                        }

                        string tituloHtml = Sucesso ? "Aventura Iniciada!" : "Aviso da Aventura";
                        string classeCss = Sucesso ? "sucesso" : "aviso";

                        responseString = responseString
                            .Replace("Aventura Iniciada!", tituloHtml)
                            .Replace("class=\"mensagem-status\"", $"class=\"mensagem-status {classeCss}\"")
                            .Replace("[[MENSAGEM_PLACEHOLDER]]", MensagemFeedback);

                        var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                        response.ContentLength64 = buffer.Length;
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        response.OutputStream.Close();
                    }
                    listener?.Stop();
#endif

                    if (Sucesso)
                    {
                        await Task.Delay(1500);
                        _navigation.NavigateTo("/login");
                    }
                }
                else
                {
                    MensagemFeedback = "Código de autorização não encontrado.";
                    Sucesso = false;
                    listener?.Stop();
                }
            }
            catch (TaskCanceledException)
            {
                MensagemFeedback = "Autenticação social cancelada.";
                Sucesso = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO REGISTRO SOCIAL {provedor}]: {ex.Message}");
                MensagemFeedback = $"Erro ao registrar com o {provedor}. Tente novamente.";
                Sucesso = false;
            }
            finally
            {
                IsProcessing = false;
                NotifyStateChanged();
            }
        }
    }
}