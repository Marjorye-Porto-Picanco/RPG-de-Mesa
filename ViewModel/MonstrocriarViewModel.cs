using Microsoft.AspNetCore.Components;
using RPGdeMesa.Models;
using RPGdeMesa.Services;
using System;
using System.Threading.Tasks;

namespace RPGdeMesa.ViewModel
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

        public Monstro MonstroAtual { get; set; }
        public string MensagemFeedback { get; set; } = "";
        public bool Sucesso { get; set; } = false;

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
                Imagem = "img/capacampanha3.png"
            };
            MensagemFeedback = "";
            Sucesso = false;
        }

        public async Task SalvarMonstroAsync()
        {
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

            var resultado = await _monstroService.CriarMonstroAsync(idUsuario, MonstroAtual);
            Sucesso = resultado.Sucesso;
            MensagemFeedback = resultado.Mensagem;
            NotifyStateChanged();

            if (Sucesso)
            {
                await Task.Delay(1000);
                _navigation.NavigateTo("/monstros-lib");
            }
        }
    }
}