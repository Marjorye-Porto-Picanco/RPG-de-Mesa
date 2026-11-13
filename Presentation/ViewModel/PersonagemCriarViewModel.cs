using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Presentation.Services;

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
        public const int TotalEtapas = 4;
        public const long LimiteImagemBytes = 2 * 1024 * 1024;

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

        // Listas de Opções para a View
        public IReadOnlyList<string> Etapas { get; } =
        [
            "Identidade & Físico",
            "Atributos & Perícias",
            "História & Diário",
            "Grimório & Magias"
        ];

        public IReadOnlyList<string> ListaOpcoesClasses { get; } =
        [
            "Bárbaro", "Bardo", "Bruxo", "Clérigo", "Druida", "Feiticeiro",
            "Guerreiro", "Ladino", "Mago", "Monge", "Paladino", "Patrulheiro", "Outra"
        ];

        public IReadOnlyList<string> ListaSalvaguardas { get; } =
        [
            "Força", "Destreza", "Constituição", "Inteligência", "Sabedoria", "Carisma"
        ];

        public IReadOnlyList<string> ListaPericias { get; } =
        [
            "Acrobacia", "Adestrar Animais", "Arcanismo", "Atletismo", "Atuação", "Enganação",
            "Furtividade", "História", "Intimidação", "Intuição", "Investigação", "Medicina",
            "Natureza", "Percepção", "Persuasão", "Prestidigitação", "Religião", "Sobrevivência"
        ];

        // Estado do Personagem e Formulário
        public Personagem PersonagemAtual { get; set; } = new();
        public int? IdCampanhaOpcional { get; set; }

        public List<ClassePersonagemEntry> ClassesSelecionadas { get; set; } = new();

        public int IdadeNumero { get; set; } = 25;
        public int AlturaCm { get; set; } = 170;
        public decimal PesoKg { get; set; } = 70m;

        public HashSet<string> SalvaguardasSelecionadas { get; set; } = new();
        public HashSet<string> PericiasSelecionadas { get; set; } = new();
        public List<MagiaGrimorioEntry> Magias { get; set; } = new();

        // Estado do Wizard, Imagem e Navegação
        public int EtapaAtual { get; private set; } = 1;
        public string? ErroEtapa { get; private set; }
        public string? ErroImagem { get; private set; }
        public bool ExibirAvisoSaida { get; private set; }
        public bool LiberarProximaNavegacao { get; private set; }
        public string? DestinoPendente { get; private set; }

        public string MensagemFeedback { get; set; } = "";
        public bool Sucesso { get; set; } = false;
        public bool IsProcessing { get; set; } = false;

        public bool FormularioIntocado =>
            EtapaAtual == 1 && string.IsNullOrWhiteSpace(PersonagemAtual.Nome);

        public string DestinoPadrao =>
            IdCampanhaOpcional.HasValue && IdCampanhaOpcional.Value > 0 ? "/campanhas" : "/personagem-lib";

        public event Action? OnChange;
        public void NotifyStateChanged() => OnChange?.Invoke();

        public void LimparFormulario()
        {
            EtapaAtual = 1;
            ErroEtapa = null;
            ErroImagem = null;
            ExibirAvisoSaida = false;
            LiberarProximaNavegacao = false;
            DestinoPendente = null;

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

        // Navegação entre Etapas (Wizard)
        public void AvancarEtapa()
        {
            ErroEtapa = null;

            if (EtapaAtual == 1)
            {
                SincronizarClassesENivel();

                if (string.IsNullOrWhiteSpace(PersonagemAtual.Nome))
                {
                    ErroEtapa = "Informe o nome do personagem para continuar.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(PersonagemAtual.RacaTexto))
                {
                    ErroEtapa = "Selecione a raça do personagem para continuar.";
                    return;
                }

                if (!ClassesSelecionadas.Any() || ClassesSelecionadas.Any(c => string.IsNullOrWhiteSpace(c.NomeClasse)))
                {
                    ErroEtapa = "Selecione pelo menos uma classe válida.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(PersonagemAtual.AntecedenteTexto))
                {
                    ErroEtapa = "Selecione o antecedente do personagem para continuar.";
                    return;
                }

                int somaNiveis = ClassesSelecionadas.Sum(c => c.Nivel);
                if (somaNiveis < 1 || somaNiveis > 20)
                {
                    ErroEtapa = "A soma dos níveis das classes deve estar entre 1 e 20.";
                    return;
                }

                if (IdadeNumero <= 0 || AlturaCm <= 0 || PesoKg <= 0)
                {
                    ErroEtapa = "Preencha idade, altura e peso com valores numéricos válidos.";
                    return;
                }

                SincronizarMedidasFisicas();
            }

            if (EtapaAtual < TotalEtapas)
            {
                EtapaAtual++;
            }
        }

        public void VoltarEtapa()
        {
            ErroEtapa = null;
            if (EtapaAtual > 1) EtapaAtual--;
        }

        public void IrParaEtapa(int etapa)
        {
            if (etapa >= 1 && etapa <= EtapaAtual)
            {
                ErroEtapa = null;
                EtapaAtual = etapa;
            }
        }

        // Gerenciamento de Classes e Níveis
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

        public void AlterarNomeClasse(ClassePersonagemEntry cl, string? novoNome)
        {
            cl.NomeClasse = string.IsNullOrWhiteSpace(novoNome) ? "Guerreiro" : novoNome;
            SincronizarClassesENivel();
        }

        public void AlterarNivelClasse(ClassePersonagemEntry cl, string? valorTexto)
        {
            if (int.TryParse(valorTexto, out int nv))
            {
                cl.Nivel = Math.Clamp(nv, 1, 20);
                SincronizarClassesENivel();
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

        // Cálculos e Utilitários
        public static string ObterModificador(int valor)
        {
            var modificador = (int)Math.Floor((valor - 10) / 2.0);
            return modificador >= 0 ? $"+{modificador}" : modificador.ToString();
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

        // Upload de Imagem
        public async Task CarregarFotoAsync(InputFileChangeEventArgs evento)
        {
            ErroImagem = null;
            var arquivo = evento.File;

            if (arquivo.Size > LimiteImagemBytes)
            {
                ErroImagem = "A imagem deve ter no máximo 2 MB.";
                return;
            }

            if (string.IsNullOrWhiteSpace(arquivo.ContentType) || !arquivo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                ErroImagem = "Selecione um arquivo de imagem válido.";
                return;
            }

            try
            {
                await using var origem = arquivo.OpenReadStream(LimiteImagemBytes);
                using var memoria = new MemoryStream();
                await origem.CopyToAsync(memoria);

                PersonagemAtual.Imagem = $"data:{arquivo.ContentType};base64,{Convert.ToBase64String(memoria.ToArray())}";
            }
            catch (Exception)
            {
                ErroImagem = "Não foi possível carregar essa imagem. Tente outro arquivo.";
            }
        }

        // Guarda de Navegação e Saída
        public void AbrirAvisoSaida()
        {
            if (FormularioIntocado)
            {
                LiberarProximaNavegacao = true;
                _navigation.NavigateTo(DestinoPadrao);
                return;
            }

            ExibirAvisoSaida = true;
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
        }

        public void SairDaCriacao()
        {
            ExibirAvisoSaida = false;
            LiberarProximaNavegacao = true;
            _navigation.NavigateTo(DestinoPendente ?? DestinoPadrao);
        }

        // Persistência Final
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
                LiberarProximaNavegacao = true;
                MensagemFeedback = "Personagem forjado com sucesso!";
                NotifyStateChanged();

                await Task.Delay(1000);
                _navigation.NavigateTo(DestinoPadrao);
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