using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Bestiario;
using System;
using System.Threading.Tasks;

namespace RPGdeMesa.Presentation.ViewModel
{
    public class MonstroCriarViewModel
    {
        private readonly MonstroService _monstroService;
        private readonly SessaoUsuarioService _sessaoUsuario;
        private readonly NavigationManager _navigation;

        public MonstroCriarViewModel(MonstroService monstroService, SessaoUsuarioService sessaoUsuario, NavigationManager navigation)
        {
            _monstroService = monstroService;
            _sessaoUsuario = sessaoUsuario;
            _navigation = navigation;
            LimparFormulario();
        }

        public Monstro MonstroAtual { get; set; } = new();
        public string MensagemFeedback { get; set; } = "";
        public bool Sucesso { get; set; } = false;
        public bool IsProcessing { get; set; } = false;

        // Controle de saída (NavigationLock)
        public bool ExibirAvisoSaida { get; private set; }
        public bool LiberarProximaNavegacao { get; private set; }
        public string? DestinoPendente { get; private set; }

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();

        public void LimparFormulario()
        {
            MonstroAtual = new Monstro
            {
                Tamanho = "Médio",
                Nd = 1,
                ClasseArmadura = 13,
                PvMaximo = 24,
                TipoMonstro = "Outro",
            };
            MensagemFeedback = "";
            Sucesso = false;
            IsProcessing = false;
            ExibirAvisoSaida = false;
            LiberarProximaNavegacao = false;
            DestinoPendente = null;
        }

        public bool FormularioIntocado =>
            string.IsNullOrWhiteSpace(MonstroAtual.Nome) && MonstroAtual.PvMaximo == 24;

        public async Task SalvarMonstroAsync()
        {
            if (IsProcessing) return;

            if (string.IsNullOrWhiteSpace(MonstroAtual.Nome))
            {
                Sucesso = false;
                MensagemFeedback = "O nome da criatura é obrigatório!";
                NotifyStateChanged();
                return;
            }

            int idUsuario = _sessaoUsuario.IdUsuarioLogado;
            if (idUsuario <= 0)
            {
                Sucesso = false;
                MensagemFeedback = "Usuário não identificado. Faça login novamente.";
                NotifyStateChanged();
                return;
            }

            try
            {
                IsProcessing = true;
                MensagemFeedback = "Forjando monstro...";
                NotifyStateChanged();

                var resultado = await _monstroService.CriarMonstroAsync(idUsuario, MonstroAtual);
                Sucesso = resultado.Sucesso;
                MensagemFeedback = resultado.Mensagem;

                if (Sucesso)
                {
                    // Libera a navegação para que o NavigationLock não exiba o aviso de perda de dados
                    LiberarProximaNavegacao = true;
                    NotifyStateChanged();

                    await Task.Delay(1000);
                    // Redireciona para a biblioteca abrindo diretamente a aba de monstros
                    _navigation.NavigateTo("/BibliotecaGeral?categoria=monstros");
                }
                else
                {
                    NotifyStateChanged();
                }
            }
            catch (Exception ex)
            {
                Sucesso = false;
                MensagemFeedback = $"Erro ao salvar: {ex.Message}";
                NotifyStateChanged();
            }
            finally
            {
                IsProcessing = false;
                NotifyStateChanged();
            }
        }

        public void CancelarCriacao()
        {
            LiberarProximaNavegacao = true;
            _navigation.NavigateTo("/BibliotecaGeral?categoria=monstros");
        }

        public void AbrirAvisoSaida()
        {
            if (FormularioIntocado || Sucesso)
            {
                LiberarProximaNavegacao = true;
                _navigation.NavigateTo("/BibliotecaGeral?categoria=monstros");
                return;
            }
            ExibirAvisoSaida = true;
            NotifyStateChanged();
        }

        public void ConfirmarNavegacao(LocationChangingContext contexto)
        {
            if (Sucesso || LiberarProximaNavegacao || FormularioIntocado)
            {
                LiberarProximaNavegacao = false;
                return;
            }

            contexto.PreventNavigation();
            DestinoPendente = contexto.TargetLocation;
            ExibirAvisoSaida = true;
            NotifyStateChanged();
        }

        public void FecharAvisoSaida()
        {
            ExibirAvisoSaida = false;
            DestinoPendente = null;
            NotifyStateChanged();
        }

        public void SairDaCriacao()
        {
            ExibirAvisoSaida = false;
            LiberarProximaNavegacao = true;
            _navigation.NavigateTo(DestinoPendente ?? "/BibliotecaGeral?categoria=monstros");
        }
    }
}