using Microsoft.AspNetCore.Components;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Personagens;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace RPGdeMesa.Presentation.ViewModel
{
    public class ClassePersonagemEntry
    {
        public string NomeClasse { get; set; } = "Guerreiro";
        public int Nivel { get; set; } = 1;
    }

    public class ItemInventarioVinculado
    {
        public string Categoria { get; set; } = string.Empty;
        public int IdItem { get; set; }
        public string Nome { get; set; } = string.Empty;
        public int Quantidade { get; set; } = 1;
        public decimal PesoUnitario { get; set; }
        public decimal PrecoUnitario { get; set; }
    }

    public class MagiaGrimorioEntry
    {
        public string Nome { get; set; } = string.Empty;
        public string Circulo { get; set; } = "1º Círculo";
        public string Efeito { get; set; } = string.Empty;
        public string AlcanceNotas { get; set; } = string.Empty;

        public int ObterNivelCirculo() => Circulo switch
        {
            "Truque" => 0,
            "1º Círculo" => 1,
            "2º Círculo" => 2,
            "3º Círculo" => 3,
            "4º Círculo" => 4,
            "5º Círculo" => 5,
            _ => 1
        };

        public static string ConverterNivelParaTexto(int nivel) => nivel switch
        {
            0 => "Truque",
            1 => "1º Círculo",
            2 => "2º Círculo",
            3 => "3º Círculo",
            4 => "4º Círculo",
            5 => "5º Círculo",
            _ => "1º Círculo"
        };
    }

    public class PersonagemCriarViewModel
    {
        private readonly PersonagemService _personagemService;
        private readonly CampanhaService _campanhaService;
        private readonly SessaoUsuarioService _sessaoUsuario;
        private readonly NavigationManager _navigation;

        public PersonagemCriarViewModel(
            PersonagemService personagemService,
            CampanhaService campanhaService,
            SessaoUsuarioService sessaoUsuario,
            NavigationManager navigation)
        {
            _personagemService = personagemService;
            _campanhaService = campanhaService;
            _sessaoUsuario = sessaoUsuario;
            _navigation = navigation;
            LimparFormulario();
        }

        public Personagem PersonagemAtual { get; set; } = new();
        public int? IdCampanhaOpcional { get; set; }

        public List<ClassePersonagemEntry> ClassesSelecionadas { get; set; } = new();

        public int IdadeNumero { get; set; } = 25;
        public int AlturaCm { get; set; } = 170;
        public decimal PesoKg { get; set; } = 70m;

        public HashSet<string> SalvaguardasSelecionadas { get; set; } = new();
        public HashSet<string> PericiasSelecionadas { get; set; } = new();
        public List<MagiaGrimorioEntry> Magias { get; set; } = new();

        public string MensagemFeedback { get; set; } = "";
        public bool Sucesso { get; set; } = false;
        public bool IsProcessing { get; set; } = false;

        public event Action? OnChange;
        public void NotifyStateChanged() => OnChange?.Invoke();

        public void LimparFormulario()
        {
            IdadeNumero = 25;
            AlturaCm = 170;
            PesoKg = 70m;

            ClassesSelecionadas = new List<ClassePersonagemEntry>
            {
                new ClassePersonagemEntry { NomeClasse = "Guerreiro", Nivel = 1 }
            };

            PersonagemAtual = new Personagem
            {
                Nivel = 1,
                ExperienciaXp = 0,
                Forca = 10,
                Destreza = 10,
                Constituicao = 10,
                Inteligencia = 10,
                Sabedoria = 10,
                Carisma = 10,
                RacaTexto = "Humano",
                ClasseTexto = "Guerreiro",
                AntecedenteTexto = "Acólito",
                Tendencia = "Neutra",
                Genero = "Masculino",
                Idade = "25 anos",
                Altura = "1,70 m",
                Peso = "70 kg",
                Imagem = "img/armaduraprata.png"
            };

            SalvaguardasSelecionadas.Clear();
            PericiasSelecionadas.Clear();
            Magias.Clear();

            MensagemFeedback = "";
            Sucesso = false;
        }

        public void AdicionarClasse()
        {
            if (ClassesSelecionadas.Sum(c => c.Nivel) >= 20) return;
            ClassesSelecionadas.Add(new ClassePersonagemEntry { NomeClasse = "Mago", Nivel = 1 });
            SincronizarClassesENivel();
            NotifyStateChanged();
        }

        public void RemoverClasse(ClassePersonagemEntry item)
        {
            if (ClassesSelecionadas.Count > 1)
            {
                ClassesSelecionadas.Remove(item);
                SincronizarClassesENivel();
                NotifyStateChanged();
            }
        }

        public void SincronizarClassesENivel()
        {
            var validas = ClassesSelecionadas.Where(c => !string.IsNullOrWhiteSpace(c.NomeClasse)).ToList();
            if (!validas.Any())
            {
                PersonagemAtual.Nivel = 1;
                PersonagemAtual.ClasseTexto = "Guerreiro";
                return;
            }

            int somaNiveis = validas.Sum(c => Math.Clamp(c.Nivel, 1, 20));
            PersonagemAtual.Nivel = Math.Clamp(somaNiveis, 1, 20);

            string textoResumo = validas.Count == 1
                ? validas[0].NomeClasse
                : string.Join(" / ", validas.Select(c => $"{c.NomeClasse} {c.Nivel}"));

            PersonagemAtual.ClasseTexto = textoResumo.Length > 50 ? textoResumo[..50] : textoResumo;
        }

        public static string ClassificarCategoriaItem(Item i)
        {
            if ((i.Tipo != null && (i.Tipo.Contains("arma", StringComparison.OrdinalIgnoreCase) ||
                                    i.Tipo.Contains("bast", StringComparison.OrdinalIgnoreCase) ||
                                    i.Tipo.Contains("cajado", StringComparison.OrdinalIgnoreCase) ||
                                    i.Tipo.Contains("varinha", StringComparison.OrdinalIgnoreCase))) ||
                !string.IsNullOrWhiteSpace(i.Dano))
                return "Armas";

            if ((i.Tipo != null && (i.Tipo.Contains("armadura", StringComparison.OrdinalIgnoreCase) ||
                                    i.Tipo.Contains("escudo", StringComparison.OrdinalIgnoreCase))) ||
                i.CaBonus > 0)
                return "Armaduras";

            if (i.Tipo != null && (i.Tipo.Contains("poç", StringComparison.OrdinalIgnoreCase) ||
                                   i.Tipo.Contains("poc", StringComparison.OrdinalIgnoreCase) ||
                                   i.Tipo.Contains("pergaminho", StringComparison.OrdinalIgnoreCase) ||
                                   i.Tipo.Contains("consum", StringComparison.OrdinalIgnoreCase)))
                return "Consumiveis";

            return "Outros";
        }

        public void AlternarSalvaguarda(string nome, bool marcado)
        {
            if (marcado) SalvaguardasSelecionadas.Add(nome);
            else SalvaguardasSelecionadas.Remove(nome);
        }

        public void AlternarPericia(string nome, bool marcado)
        {
            if (marcado) PericiasSelecionadas.Add(nome);
            else PericiasSelecionadas.Remove(nome);
        }

        public void AdicionarMagia()
        {
            Magias.Add(new MagiaGrimorioEntry());
            NotifyStateChanged();
        }

        public void RemoverMagia(MagiaGrimorioEntry magia)
        {
            Magias.Remove(magia);
            NotifyStateChanged();
        }

        public void SincronizarMedidasFisicas()
        {
            SincronizarClassesENivel();
            PersonagemAtual.Idade = $"{IdadeNumero} anos";
            PersonagemAtual.Altura = $"{(AlturaCm / 100.0):F2} m".Replace('.', ',');
            PersonagemAtual.Peso = $"{PesoKg.ToString("0.#", new CultureInfo("pt-BR"))} kg";
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

                SincronizarMedidasFisicas();

                var resultadoPj = await _personagemService.CriarPersonagemAsync(idUsuario, PersonagemAtual);
                if (!resultadoPj.Sucesso)
                {
                    Sucesso = false;
                    MensagemFeedback = resultadoPj.Mensagem;
                    NotifyStateChanged();
                    return;
                }

                var listaClasses = ClassesSelecionadas
                    .Where(c => !string.IsNullOrWhiteSpace(c.NomeClasse))
                    .Select(c => (c.NomeClasse, Math.Clamp(c.Nivel, 1, 20)))
                    .ToList();

                var listaMagias = Magias
                    .Where(m => !string.IsNullOrWhiteSpace(m.Nome))
                    .Select(m => new Magia
                    {
                        Nome = m.Nome,
                        CirculoNivel = m.ObterNivelCirculo(),
                        DanoEfeito = m.Efeito,
                        Alcance = m.AlcanceNotas
                    })
                    .ToList();

                await _personagemService.SalvarComplementos3FNAsync(
                    PersonagemAtual.IdPersonagem,
                    listaClasses,
                    SalvaguardasSelecionadas,
                    PericiasSelecionadas,
                    dadosInventario: null,
                    itens: null,
                    listaMagias);

                if (IdCampanhaOpcional.HasValue && IdCampanhaOpcional.Value > 0)
                {
                    await _campanhaService.AdicionarPersonagemNaCampanhaAsync(IdCampanhaOpcional.Value, PersonagemAtual.IdPersonagem);
                }

                Sucesso = true;
                MensagemFeedback = "Personagem forjado com sucesso!";
                NotifyStateChanged();

                await Task.Delay(1000);
                _navigation.NavigateTo("/personagem-lib");
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