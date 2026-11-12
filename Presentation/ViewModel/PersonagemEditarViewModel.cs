using Microsoft.AspNetCore.Components;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Personagens;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RPGdeMesa.Presentation.ViewModel
{
    public class PersonagemEditarViewModel
    {
        private readonly PersonagemService _personagemService;

        public PersonagemEditarViewModel(PersonagemService personagemService)
        {
            _personagemService = personagemService;
        }

        public Personagem PersonagemAtual { get; set; } = new();
        public string AbaAtiva { get; set; } = "atributos";
        public string SecaoDiarioAtiva { get; set; } = "ideais";

        // Multiclasse (3FN)
        public List<ClassePersonagemEntry> ClassesSelecionadas { get; set; } = new();

        // Controles numéricos editáveis (Idade e Peso; Altura fica bloqueada)
        public int IdadeNumero { get; set; } = 25;
        public decimal PesoKg { get; set; } = 70m;

        // 3FN: Salvaguardas e Perícias
        public HashSet<string> SalvaguardasSelecionadas { get; set; } = new();
        public HashSet<string> PericiasSelecionadas { get; set; } = new();

        // 3FN: Grimório
        public List<MagiaGrimorioEntry> Magias { get; set; } = new();

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

        public async Task CarregarPersonagemAsync(int idPersonagem)
        {
            try
            {
                IsProcessing = true;
                MensagemFeedback = "";
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

                // Passa null para dadosInventario e itens para preservar o inventário da campanha intacto
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