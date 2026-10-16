
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Authentication;

namespace RPGdeMesa.Services
{
    public class AuthService
    {
        public async Task<(string? Email, string? Nome)> ObterDadosUsuarioGoogleAsync(Func<string, Task<bool>>? verificarEmailExiste = null)
        {
            try
            {
                string clientId = DeviceInfo.Platform == DevicePlatform.WinUI
                    ? "73720001215-0ts3vufh4h4vslttbue67nf9p85auf9e.apps.googleusercontent.com"
                    : "73720001215-ea13i226utbacp783a1fq71of4bhk4c4.apps.googleusercontent.com";

                string redirectUri = DeviceInfo.Platform == DevicePlatform.WinUI ? "http://localhost:5000/" : "rpgdemesa://";
                //tirar o &prompt=select_account caso n queira ficar selecionando qual conta entrar
                string authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope=email%20profile&prompt=select_account";
                string codigoAutorizacao = null;

#if WINDOWS
                using var listener = new HttpListener();
                listener.Prefixes.Add(redirectUri);
                listener.Start();

                await Launcher.Default.OpenAsync(new Uri(authUrl));

                var context = await listener.GetContextAsync();
                codigoAutorizacao = context.Request.QueryString["code"];

                (string? Email, string? Nome) dadosUsuario = (null, null);

                // 1. Se obteve o código, já busca os dados do usuário do Google primeiro!
                if (!string.IsNullOrEmpty(codigoAutorizacao))
                {
                    dadosUsuario = await TrocarCodigoPorDadosUsuarioAsync(codigoAutorizacao, clientId, redirectUri);
                }

                // 2. Verifica se o e-mail já existe no banco (usando o callback)
                bool jaExisteConta = false;
                if (!string.IsNullOrEmpty(dadosUsuario.Email) && verificarEmailExiste != null)
                {
                    jaExisteConta = await verificarEmailExiste(dadosUsuario.Email);
                }

                // 3. Escolhe o arquivo HTML e a mensagem correta para a ÚNICA aba aberta no navegador
                string nomeArquivoHtml = jaExisteConta ? "retorno-auth.html" : "sucesso-auth.html";
                string mensagemCustomizada = jaExisteConta 
                    ? "Este e-mail já possui uma conta cadastrada. Retorne ao aplicativo para fazer login!" 
                    : "Autenticação realizada com sucesso! Pode fechar esta janela.";

                var response = context.Response;
                response.ContentType = "text/html; charset=utf-8";

                string responseString = "";
                try
                {
                    using var stream = await FileSystem.OpenAppPackageFileAsync(nomeArquivoHtml);
                    using var reader = new StreamReader(stream);
                    responseString = await reader.ReadToEndAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AVISO HTML]: Não foi possível ler {nomeArquivoHtml}: {ex.Message}");
                    responseString = "<html><body><h3>[[MENSAGEM_PLACEHOLDER]]</h3></body></html>";
                }

                string tituloHtml = jaExisteConta ? "Conta Já Cadastrada" : "Aventura Iniciada!";
                string classeCss = jaExisteConta ? "aviso" : "sucesso";

                responseString = responseString
                    .Replace("Aventura Iniciada!", tituloHtml)
                    .Replace("class=\"mensagem-status\"", $"class=\"mensagem-status {classeCss}\"")
                    .Replace("[[MENSAGEM_PLACEHOLDER]]", mensagemCustomizada);

                var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.OutputStream.Close();
                listener.Stop();

                return dadosUsuario;

#elif ANDROID
                var authResult = await WebAuthenticator.Default.AuthenticateAsync(
                    new Uri(authUrl),
                    new Uri(redirectUri)
                );

                if (authResult?.Properties != null && authResult.Properties.TryGetValue("code", out var codeVal))
                {
                    codigoAutorizacao = codeVal;
                }

                if (!string.IsNullOrEmpty(codigoAutorizacao))
                {
                    return await TrocarCodigoPorDadosUsuarioAsync(codigoAutorizacao, clientId, redirectUri);
                }
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO GOOGLE AUTH]: {ex.Message}");
            }

            return (null, null);
        }

        private async Task<(string? Email, string? Nome)> TrocarCodigoPorDadosUsuarioAsync(string codigoAutorizacao, string clientId, string redirectUri)
        {
            try
            {
                using var httpClient = new HttpClient();
                var tokenRequestData = new Dictionary<string, string>
                {
                    { "code", codigoAutorizacao },
                    { "client_id", clientId },
                    { "redirect_uri", redirectUri },
                    { "grant_type", "authorization_code" }
                };

                if (DeviceInfo.Platform == DevicePlatform.WinUI)
                {
                    string clientSecret = "";
                    tokenRequestData.Add("client_secret", clientSecret);
                }

                var tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(tokenRequestData));
                string tokenJsonResponse = await tokenResponse.Content.ReadAsStringAsync();

                if (tokenResponse.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(tokenJsonResponse);
                    if (doc.RootElement.TryGetProperty("access_token", out var accessTokenProp))
                    {
                        string accessToken = accessTokenProp.GetString();
                        return await ObterInformacoesDaAPI_GoogleAsync(accessToken);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO TROCA TOKEN]: {ex.Message}");
            }

            return (null, null);
        }

        private async Task<(string? Email, string? Nome)> ObterInformacoesDaAPI_GoogleAsync(string accessToken)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var resposta = await client.GetStringAsync("https://www.googleapis.com/oauth2/v3/userinfo");

                using var doc = JsonDocument.Parse(resposta);
                string? email = doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null;
                string? nome = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : null;

                return (email, nome);
            }
            catch
            {
                return (null, null);
            }
        }
    }
}
