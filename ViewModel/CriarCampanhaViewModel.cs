using Microsoft.AspNetCore.Components;
using RPGdeMesa.Services;
using System;
using System.Threading.Tasks;

namespace RPGdeMesa.ViewModel
{
    public class CriarCampanhaViewModel
    {
        private readonly CampanhaService _campanhaService;
        private readonly NavigationManager _navigation;

        public CriarCampanhaViewModel(CampanhaService campanhaService, NavigationManager navigation)
        {
            _campanhaService = campanhaService;
            _navigation = navigation;
        }

        // ---> ADICIONE AS PROPRIEDADES AQUI <---
        public string Nome { get; set; } = "";
        public string Notas { get; set; } = "";

        // Propriedades de estado para a tela
        public string MensagemFeedback { get; set; } = "";
        public bool Sucesso { get; set; } = false;
        public bool IsProcessing { get; set; } = false;

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();

        public async Task SalvarCampanhaAsync()
        {
            if (IsProcessing) return;

            if (string.IsNullOrWhiteSpace(Nome))
            {
                Sucesso = false;
                MensagemFeedback = "O nome da campanha é obrigatório!";
                NotifyStateChanged();
                return;
            }

            try
            {
                IsProcessing = true;
                MensagemFeedback = "Forjando a campanha...";
                NotifyStateChanged();

                // ID do mestre fixo em 1 para testes. Depois puxaremos da sessão.
                int idUsuarioLogado = 1;

                var resultado = await _campanhaService.CriarCampanhaAsync(idUsuarioLogado, Nome, Notas);

                Sucesso = resultado.Sucesso;
                MensagemFeedback = resultado.Mensagem;

                if (Sucesso)
                {
                    await Task.Delay(1000);
                    _navigation.NavigateTo("/campanhas");
                }
            }
            finally
            {
                IsProcessing = false;
                NotifyStateChanged();
            }
        }
    }
}