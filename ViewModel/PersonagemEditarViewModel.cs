using Microsoft.AspNetCore.Components;
using RPGdeMesa.Models;
using RPGdeMesa.Services;
using System;
using System.Threading.Tasks;

namespace RPGdeMesa.ViewModel
{
    public class PersonagemEditarViewModel
    {
        private readonly PersonagemService _personagemService;
        private readonly NavigationManager _navigation;

        public PersonagemEditarViewModel(PersonagemService personagemService, NavigationManager navigation)
        {
            _personagemService = personagemService;
            _navigation = navigation;
        }

        public Personagem PersonagemAtual { get; set; } = new();
        public string AbaAtiva { get; set; } = "atributos";
        public string MensagemFeedback { get; set; } = "";
        public bool Sucesso { get; set; } = false;
        public bool IsProcessing { get; set; } = false;

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();

        public async Task CarregarPersonagemAsync(int id)
        {
            var personagemDb = await _personagemService.ObterPorIdAsync(id);
            if (personagemDb != null)
            {
                PersonagemAtual = personagemDb;
                NotifyStateChanged();
            }
            else
            {
                _navigation.NavigateTo("/personagem-lib");
            }
        }

        public void MudarAba(string novaAba)
        {
            AbaAtiva = novaAba;
            NotifyStateChanged();
        }

        public async Task SalvarAlteracoesAsync()
        {
            if (IsProcessing) return;

            try
            {
                IsProcessing = true;
                MensagemFeedback = "Salvando alterações...";
                NotifyStateChanged();

                var resultado = await _personagemService.AtualizarPersonagemAsync(PersonagemAtual);
                Sucesso = resultado.Sucesso;
                MensagemFeedback = resultado.Mensagem;

                if (Sucesso)
                {
                    await Task.Delay(800);
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