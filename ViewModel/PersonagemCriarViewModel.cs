using Microsoft.AspNetCore.Components;
using RPGdeMesa.Models;
using RPGdeMesa.Services;
using System;
using System.Threading.Tasks;

namespace RPGdeMesa.ViewModel
{
    public class PersonagemCriarViewModel
    {
        private readonly PersonagemService _personagemService;
        private readonly SessaoUsuarioService _sessaoUsuario;
        private readonly NavigationManager _navigation;

        public PersonagemCriarViewModel(
            PersonagemService personagemService,
            SessaoUsuarioService sessaoUsuario,
            NavigationManager navigation)
        {
            _personagemService = personagemService;
            _sessaoUsuario = sessaoUsuario;
            _navigation = navigation;
            LimparFormulario();
        }

        public Personagem PersonagemAtual { get; set; } = new();
        public string MensagemFeedback { get; set; } = "";
        public bool Sucesso { get; set; } = false;
        public bool IsProcessing { get; set; } = false;

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();

        public void LimparFormulario()
        {
            PersonagemAtual = new Personagem
            {
                Nivel = 1,
                Forca = 10,
                Destreza = 10,
                Constituicao = 10,
                Inteligencia = 10,
                Sabedoria = 10,
                Carisma = 10,
                Tendencia = "Neutra",
                Imagem = "img/armaduraprata.png"
            };
            MensagemFeedback = "";
            Sucesso = false;
        }

        public async Task SalvarPersonagemAsync()
        {
            if (IsProcessing) return;

            if (string.IsNullOrWhiteSpace(PersonagemAtual.Nome))
            {
                Sucesso = false;
                MensagemFeedback = "O nome do personagem é obrigatório!";
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
                MensagemFeedback = "Criando aventureiro...";
                NotifyStateChanged();

                var resultado = await _personagemService.CriarPersonagemAsync(idUsuario, PersonagemAtual);
                Sucesso = resultado.Sucesso;
                MensagemFeedback = resultado.Mensagem;
                NotifyStateChanged();

                if (Sucesso)
                {
                    await Task.Delay(1000);
                    _navigation.NavigateTo("/personagem-lib");
                }
            }
            catch (Exception ex)
            {
                Sucesso = false;
                MensagemFeedback = $"Erro ao salvar: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
                NotifyStateChanged();
            }
        }
    }
}