using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Personagens;

namespace RPGdeMesa.Presentation.ViewModel
{
    public class PersonagemEditarViewModel
    {
        private const long LimiteImagemBytes = 2 * 1024 * 1024;
        private readonly PersonagemService _personagemService;
        private readonly NavigationManager _navigation;

        public PersonagemEditarViewModel(
            PersonagemService personagemService,
            NavigationManager navigation)
        {
            _personagemService = personagemService;
            _navigation = navigation;
        }

        // Listas Estáticas de Apoio
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

        public Personagem PersonagemAtual { get; set; } = new();
        public string AbaAtiva { get; set; } = "atributos";
        public string SecaoDiarioAtiva { get; set; } = "ideais";

        public List<ClassePersonagemEntry> ClassesSelecionadas { get; set; } = new();

        public int IdadeNumero { get; set; } = 25;
        public decimal PesoKg { get; set; } = 70m;

        public HashSet<string> SalvaguardasSelecionadas { get; set; } = new();
        public HashSet<string> PericiasSelecionadas { get; set; } = new();
        public List<MagiaGrimorioEntry> Magias { get; set; } = new();

        // Controle de Estado de Saída e Navegação
        public bool ExibirAvisoSaida { get; private set; }
        public bool LiberarProximaNavegacao { get; private set; }
        public string? DestinoPendente { get; private set; }

        public string MensagemFeedback { get; set; } = "";
        public bool Sucesso { get; set; } = false;
        public bool IsProcessing { get; set; } = false;

        public event Action? OnChange;
        public void NotifyStateChanged() => OnChange?.Invoke();

        public void MudarAba(string novaAba)
        {
            AbaAtiva = novaAba;
            NotifyStateChanged();
        }

        public void MudarSecaoDiario(string secao)
        {
            SecaoDiarioAtiva = secao;
            NotifyStateChanged();
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

        public void AlterarNomeClasse(ClassePersonagemEntry cl, string? novoNome)
        {
            cl.NomeClasse = string.IsNullOrWhiteSpace(novoNome) ? "Guerreiro" : novoNome;
            SincronizarClassesENivel();
            NotifyStateChanged();
        }

        public void AlterarNivelClasse(ClassePersonagemEntry cl, string? valorTexto)
        {
            if (int.TryParse(valorTexto, out int nv))
            {
                cl.Nivel = Math.Clamp(nv, 1, 20);
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

        public static string ObterModificador(int valor)
        {
            var modificador = (int)Math.Floor((valor - 10) / 2.0);
            return modificador >= 0 ? $"+{modificador}" : modificador.ToString();
        }

        public async Task CarregarFotoAsync(InputFileChangeEventArgs evento)
        {
            var arquivo = evento.File;
            if (arquivo.Size > LimiteImagemBytes) return;
            if (string.IsNullOrWhiteSpace(arquivo.ContentType) || !arquivo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                await using var origem = arquivo.OpenReadStream(LimiteImagemBytes);
                using var memoria = new MemoryStream();
                await origem.CopyToAsync(memoria);

                PersonagemAtual.Imagem = $"data:{arquivo.ContentType};base64,{Convert.ToBase64String(memoria.ToArray())}";
                NotifyStateChanged();
            }
            catch { }
        }

        public async Task CarregarPersonagemAsync(int idPersonagem)
        {
            try
            {
                IsProcessing = true;
                MensagemFeedback = "";

                ExibirAvisoSaida = false;
                LiberarProximaNavegacao = false;
                DestinoPendente = null;

                NotifyStateChanged();

                var encontrado = await _personagemService.ObterPorIdAsync(idPersonagem);
                if (encontrado != null)
                {
                    PersonagemAtual = encontrado;
                    ExtrairMedidasFisicas();

                    ClassesSelecionadas.Clear();
                    if (encontrado.PersonagemClasses != null && encontrado.PersonagemClasses.Any())
                    {
                        foreach (var pc in encontrado.PersonagemClasses)
                        {
                            if (pc.Classe != null)
                            {
                                ClassesSelecionadas.Add(new ClassePersonagemEntry
                                {
                                    NomeClasse = pc.Classe.Nome,
                                    Nivel = Math.Max(1, pc.NivelNaClasse)
                                });
                            }
                        }
                    }

                    if (!ClassesSelecionadas.Any())
                    {
                        ClassesSelecionadas.Add(new ClassePersonagemEntry
                        {
                            NomeClasse = string.IsNullOrWhiteSpace(encontrado.ClasseTexto) ? "Guerreiro" : encontrado.ClasseTexto,
                            Nivel = Math.Max(1, encontrado.Nivel)
                        });
                    }

                    SincronizarClassesENivel();

                    SalvaguardasSelecionadas = encontrado.Salvaguardas.Select(s => s.Atributo).ToHashSet();
                    PericiasSelecionadas = encontrado.Pericias.Select(p => p.NomePericia).ToHashSet();

                    Magias.Clear();
                    foreach (var pm in encontrado.PersonagemMagias)
                    {
                        if (pm.Magia != null)
                        {
                            Magias.Add(new MagiaGrimorioEntry
                            {
                                Nome = pm.Magia.Nome,
                                Circulo = MagiaGrimorioEntry.ConverterNivelParaTexto(pm.Magia.CirculoNivel),
                                Efeito = pm.Magia.DanoEfeito ?? string.Empty,
                                AlcanceNotas = pm.Magia.Alcance ?? string.Empty
                            });
                        }
                    }
                }
                else
                {
                    Sucesso = false;
                    MensagemFeedback = "Personagem não encontrado.";
                }
            }
            catch (Exception ex)
            {
                Sucesso = false;
                MensagemFeedback = $"Erro ao carregar ficha: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
                NotifyStateChanged();
            }
        }

        private void ExtrairMedidasFisicas()
        {
            if (!string.IsNullOrWhiteSpace(PersonagemAtual.Idade))
            {
                var mIdade = Regex.Match(PersonagemAtual.Idade, @"\d+");
                if (mIdade.Success && int.TryParse(mIdade.Value, out int idNum))
                    IdadeNumero = idNum;
            }

            if (!string.IsNullOrWhiteSpace(PersonagemAtual.Peso))
            {
                var textoPeso = PersonagemAtual.Peso.Replace(',', '.');
                var mPeso = Regex.Match(textoPeso, @"\d+(\.\d+)?");
                if (mPeso.Success && decimal.TryParse(mPeso.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal valorPeso))
                {
                    PesoKg = valorPeso;
                }
            }
        }

        public void SincronizarMedidasFisicas()
        {
            SincronizarClassesENivel();
            PersonagemAtual.Idade = $"{IdadeNumero} anos";
            PersonagemAtual.Peso = $"{PesoKg.ToString("0.#", new CultureInfo("pt-BR"))} kg";
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

        // Guarda de Navegação
        public void AbrirAvisoSaida()
        {
            ExibirAvisoSaida = true;
            NotifyStateChanged();
        }

        public void ConfirmarNavegacao(LocationChangingContext contexto)
        {
            if (Sucesso || LiberarProximaNavegacao)
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

        public void SairDaEdicao()
        {
            ExibirAvisoSaida = false;       
            LiberarProximaNavegacao = true; 
            _navigation.NavigateTo(DestinoPendente ?? "/personagem-lib");
        }

        public async Task SalvarAlteracoesAsync()
        {
            if (IsProcessing) return;

            int somaNiveis = ClassesSelecionadas.Sum(c => c.Nivel);
            if (somaNiveis < 1 || somaNiveis > 20)
            {
                Sucesso = false;
                MensagemFeedback = "A soma dos níveis das classes deve estar entre 1 e 20.";
                NotifyStateChanged();
                return;
            }

            try
            {
                IsProcessing = true;
                MensagemFeedback = "Salvando alterações...";
                NotifyStateChanged();

                SincronizarMedidasFisicas();

                var resultadoPj = await _personagemService.AtualizarPersonagemAsync(PersonagemAtual);
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

                var resultado3FN = await _personagemService.SalvarComplementos3FNAsync(
                    PersonagemAtual.IdPersonagem,
                    listaClasses,
                    SalvaguardasSelecionadas,
                    PericiasSelecionadas,
                    dadosInventario: null,
                    itens: null,
                    listaMagias);

                Sucesso = resultado3FN.Sucesso;
                MensagemFeedback = Sucesso ? "Ficha atualizada com sucesso!" : resultado3FN.Mensagem;
                if (Sucesso) LiberarProximaNavegacao = true;
                NotifyStateChanged();
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