using Microsoft.AspNetCore.Components;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Personagens;
using System;
using System.Threading.Tasks;

namespace RPGdeMesa.Presentation.ViewModel
{
    public class PersonagemCriarViewModel
    {
        private readonly PersonagemService _personagemService;
        private readonly NpcService _npcService;
        private readonly SessaoUsuarioService _sessaoUsuario;
        private readonly NavigationManager _navigation;

        public PersonagemCriarViewModel(
            PersonagemService personagemService,
            NpcService npcService,
            SessaoUsuarioService sessaoUsuario,
            NavigationManager navigation)
        {
            _personagemService = personagemService;
            _npcService = npcService;
            _sessaoUsuario = sessaoUsuario;
            _navigation = navigation;
            LimparFormulario();
        }

        public Personagem PersonagemAtual { get; set; } = new();
        public string TipoSelecionado { get; set; } = "Personagem"; // Controla se é Personagem ou NPC
        public int? IdCampanhaOpcional { get; set; } // Caso a tela seja aberta a partir de uma campanha

        public string MensagemFeedback { get; set; } = "";
        public bool Sucesso { get; set; } = false;
        public bool IsProcessing { get; set; } = false;

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();

        public void LimparFormulario()
        {
            TipoSelecionado = "Personagem";
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
                MensagemFeedback = $"O nome do {(TipoSelecionado == "NPC" ? "NPC" : "personagem")} é obrigatório!";
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
                MensagemFeedback = TipoSelecionado == "NPC" ? "Forjando NPC..." : "Criando aventureiro...";
                NotifyStateChanged();

                if (TipoSelecionado == "NPC")
                {
                    var novoNpc = new NPC(
                        idAccount: idUsuario,
                        nome: PersonagemAtual.Nome,
                        racaTexto: PersonagemAtual.RacaTexto,
                        classeTexto: PersonagemAtual.ClasseTexto,
                        nivel: PersonagemAtual.Nivel,
                        antecedenteTexto: PersonagemAtual.AntecedenteTexto,
                        tendencia: PersonagemAtual.Tendencia,
                        forca: PersonagemAtual.Forca,
                        destreza: PersonagemAtual.Destreza,
                        constituicao: PersonagemAtual.Constituicao,
                        inteligencia: PersonagemAtual.Inteligencia,
                        sabedoria: PersonagemAtual.Sabedoria,
                        carisma: PersonagemAtual.Carisma,
                        historia: PersonagemAtual.Historia,
                        imagem: PersonagemAtual.Imagem,
                        idCampanha: IdCampanhaOpcional
                    );

                    var resultadoNpc = await _npcService.CriarNpcAsync(idUsuario, novoNpc, IdCampanhaOpcional);
                    Sucesso = resultadoNpc.Sucesso;
                    MensagemFeedback = resultadoNpc.Mensagem;
                }
                else
                {
                    var resultadoPj = await _personagemService.CriarPersonagemAsync(idUsuario, PersonagemAtual);
                    Sucesso = resultadoPj.Sucesso;
                    MensagemFeedback = resultadoPj.Mensagem;
                }

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