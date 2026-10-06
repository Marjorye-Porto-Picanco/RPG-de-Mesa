using Microsoft.AspNetCore.Components;
using RPGdeMesa.Services;

namespace RPGdeMesa.ViewModel
{
    public class LoginViewModel
    {
        private readonly UsuarioService _usuarioService;
        private readonly NavigationManager _navigation;

        public LoginViewModel(UsuarioService usuarioService, NavigationManager navigation)
        {
            _usuarioService = usuarioService;
            _navigation = navigation;
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
                    await Task.Delay(800);
                    _navigation.NavigateTo("/sessao");
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
            string clientId = "73720001215-0ts3vufh4h4vslttbue67nf9p85auf9e.apps.googleusercontent.com";
            string redirectUri = DeviceInfo.Platform == DevicePlatform.WinUI ? "http://localhost:5000/" : "rpgdemesa://";
            string urlAutenticacao = $"https://accounts.google.com/o/oauth2/v2/auth?client_id={clientId}&redirect_uri={redirectUri}&response_type=code&scope=email%20profile";

            await ProcessarLoginSocialAsync("Google", urlAutenticacao, redirectUri);
        }

        public async Task LoginComFacebookAsync() { }
        public async Task LoginComGitHubAsync() { }

        private async Task ProcessarLoginSocialAsync(string provedor, string urlAutenticacao, string redirectUri)
        {
            if (IsProcessing) return;

            try
            {
                IsProcessing = true;
                MensagemFeedback = "Aguardando autenticação...";
                NotifyStateChanged();

                string emailObtido = "";
                string nomeObtido = "";

                if (DeviceInfo.Platform == DevicePlatform.WinUI)
                {
                    var listener = new System.Net.HttpListener();
                    listener.Prefixes.Add("http://localhost:5000/");
                    listener.Start();

                    if (provedor.Equals("Google", StringComparison.OrdinalIgnoreCase) && !urlAutenticacao.Contains("scope="))
                    {
                        urlAutenticacao += "&scope=openid%20email%20profile";
                    }

                    await Launcher.Default.OpenAsync(new Uri(urlAutenticacao));

                    var context = await listener.GetContextAsync();
                    var request = context.Request;
                    string codigoAutorizacao = request.QueryString["code"];

                    var response = context.Response;
                    string responseString = "";
                    try
                    {
                        using var stream = await FileSystem.OpenAppPackageFileAsync("sucesso-auth.html");
                        using var reader = new StreamReader(stream);
                        responseString = await reader.ReadToEndAsync();
                    }
                    catch
                    {
                        responseString = "<html><body><h3>Autenticação realizada com sucesso! Pode fechar esta janela.</h3></body></html>";
                    }

                    var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.OutputStream.Close();
                    listener.Stop();

                    if (!string.IsNullOrEmpty(codigoAutorizacao))
                    {
                        string emailRealDoGoogle = "";
                        string nomeRealDoGoogle = "";

                        try
                        {
                            using var httpClient = new HttpClient();
                            string cId = "73720001215-0ts3vufh4h4vslttbue67nf9p85auf9e.apps.googleusercontent.com";
                            string clientSecret = "";
                            string callbackUri = "http://localhost:5000/";

                            var tokenRequestData = new Dictionary<string, string>
                            {
                                { "code", codigoAutorizacao },
                                { "client_id", cId },
                                { "client_secret", clientSecret },
                                { "redirect_uri", callbackUri },
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
                            IsProcessing = false;
                            NotifyStateChanged();
                            return;
                        }

                        var resultado = await _usuarioService.AutenticarOuCadastrarSocialAsync(emailRealDoGoogle, nomeRealDoGoogle);
                        Sucesso = resultado.Sucesso;
                        MensagemFeedback = resultado.Mensagem;

                        if (Sucesso)
                        {
                            await Task.Delay(1000);
                            _navigation.NavigateTo("/sessao");
                        }
                    }
                    else
                    {
                        MensagemFeedback = "Código de autorização não encontrado.";
                        Sucesso = false;
                    }
                }
                else
                {
                    var authResult = await WebAuthenticator.AuthenticateAsync(
                        new Uri(urlAutenticacao),
                        new Uri(redirectUri)
                    );

                    emailObtido = authResult?.Properties?["email"] ?? "usuario_social@exemplo.com";
                    nomeObtido = authResult?.Properties?["name"] ?? $"aventureiro_{provedor.ToLower()}";

                    var resultado = await _usuarioService.AutenticarOuCadastrarSocialAsync(emailObtido, nomeObtido);

                    Sucesso = resultado.Sucesso;
                    MensagemFeedback = resultado.Mensagem;

                    if (Sucesso)
                    {
                        await Task.Delay(1000);
                        _navigation.NavigateTo("/sessao");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                MensagemFeedback = "Autenticação social cancelada.";
                Sucesso = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO LOGIN SOCIAL {provedor}]: {ex.Message}");
                MensagemFeedback = $"Erro ao autenticar com o {provedor}. Tente novamente.";
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