using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Magias;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Presentation.Services;

namespace RPGdeMesa.Presentation.ViewModel
{
    public class PersonagemEditarViewModel
    {
        private readonly PersonagemService _personagemService;
        private readonly CampanhaService _campanhaService;
        private readonly SessaoUsuarioService _sessaoUsuario;
        private readonly NavigationManager _navigation;
        private readonly ImagemPickerService _imagemPicker;
        private readonly AppCacheService _appCache;
        private readonly HomeCacheService _homeCache;

        public PersonagemEditarViewModel(
            PersonagemService personagemService,
            CampanhaService campanhaService,
            SessaoUsuarioService sessaoUsuario,
            NavigationManager navigation,
            ImagemPickerService imagemPicker,
            AppCacheService appCache,
            HomeCacheService homeCache)
        {
            _personagemService = personagemService;
            _campanhaService = campanhaService;
            _sessaoUsuario = sessaoUsuario;
            _navigation = navigation;
            _imagemPicker = imagemPicker;
            _appCache = appCache;
            _homeCache = homeCache;
        }

        // Listas Estáticas de Apoio
        public IReadOnlyList<string> ListaOpcoesClasses { get; private set; } = Array.Empty<string>();
        public IReadOnlyList<string> ListaOpcoesRacas { get; private set; } = Array.Empty<string>();

        public async Task CarregarOpcoesBancoAsync()
        {
            var classes = await _personagemService.ListarClassesAsync();
            var racas = await _personagemService.ListarRacasAsync();
            ListaOpcoesClasses = classes.Select(c => c.Nome).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            ListaOpcoesRacas = racas.Select(r => r.Nome).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

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

        public IReadOnlyList<string> ListaCirculos { get; } =
        [
            "Truque", "1º Círculo", "2º Círculo", "3º Círculo", "4º Círculo",
            "5º Círculo", "6º Círculo", "7º Círculo", "8º Círculo", "9º Círculo"
        ];

        public IReadOnlyList<string> ListaEscolasMagia { get; } =
        [
            "Abjuração", "Adivinhação", "Conjuração", "Encantamento",
            "Evocação", "Ilusão", "Necromancia", "Transmutação"
        ];

        public Personagem PersonagemAtual { get; set; } = new();
        public string AbaAtiva { get; set; } = "atributos";
        public string SecaoDiarioAtiva { get; set; } = "ideais";

        public List<ClassePersonagemEntry> ClassesSelecionadas { get; set; } = new();

        public int IdadeNumero { get; set; } = 25;
        public decimal PesoKg { get; set; } = 70m;

        public HashSet<string> SalvaguardasSelecionadas { get; set; } = new();
        public HashSet<string> PericiasSelecionadas { get; set; } = new();

        // Catálogo e Grimório de Magias
        public List<Magia> MagiasCatalogo { get; private set; } = new();
        public List<MagiaGrimorioEntry> Magias { get; set; } = new();
        public string TermoBuscaMagia { get; set; } = string.Empty;
        public string FiltroCirculoMagia { get; set; } = "todos";
        public MagiaGrimorioEntry? MagiaEmVisualizacao { get; private set; }
        public bool ExibirFormNovaMagia { get; set; } = false;
        public MagiaGrimorioEntry NovaMagiaManual { get; set; } = new();

        public IEnumerable<Magia> MagiasCatalogoFiltradas =>
            MagiasCatalogo
                .Where(m => !Magias.Any(eq =>
                    (eq.IdMagia > 0 && eq.IdMagia == m.IdMagia) ||
                    eq.Nome.Equals(m.Nome, StringComparison.OrdinalIgnoreCase)))
                .Where(m =>
                    FiltroCirculoMagia == "todos" ||
                    m.CirculoNivel.ToString(CultureInfo.InvariantCulture) == FiltroCirculoMagia)
                .Where(m =>
                    string.IsNullOrWhiteSpace(TermoBuscaMagia) ||
                    m.Nome.Contains(TermoBuscaMagia, StringComparison.OrdinalIgnoreCase) ||
                    (m.Escola != null && m.Escola.Contains(TermoBuscaMagia, StringComparison.OrdinalIgnoreCase)) ||
                    (m.DanoEfeito != null && m.DanoEfeito.Contains(TermoBuscaMagia, StringComparison.OrdinalIgnoreCase)));

        // Controle de Estado de Saída e Navegação
        public bool ExibirAvisoSaida { get; private set; }
        public bool LiberarProximaNavegacao { get; private set; }
        public string? DestinoPendente { get; private set; }

        public bool CarregandoFoto { get; private set; }
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

        public async Task CarregarFotoAsync()
        {
            if (CarregandoFoto) return;
            CarregandoFoto = true;
            NotifyStateChanged();

            try
            {
                var res = await _imagemPicker.SelecionarUmaAsync(ladoMaximo: 512);
                if (res.Imagem is not null)
                    PersonagemAtual.Imagem = res.Imagem.DataUrl;
                else if (res.Erro is not null)
                {
                    Sucesso = false;
                    MensagemFeedback = res.Erro;
                }
            }
            finally
            {
                CarregandoFoto = false;
                NotifyStateChanged();
            }
        }

        private async Task<List<Magia>> ObterMagiasSeguroAsync()
        {
            try
            {
                return await _campanhaService.ObterTodasMagiasAsync();
            }
            catch
            {
                return new List<Magia>();
            }
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
                TermoBuscaMagia = string.Empty;
                FiltroCirculoMagia = "todos";
                ExibirFormNovaMagia = false;
                NovaMagiaManual = new MagiaGrimorioEntry();
                MagiaEmVisualizacao = null;

                NotifyStateChanged();

                var idUsuario = _sessaoUsuario.IdUsuarioLogado;
                var bibSnap = idUsuario > 0 ? _appCache.ObterBiblioteca(idUsuario) : null;

                var tarefaMagias = bibSnap is not null && bibSnap.Magias.Count > 0
                    ? Task.FromResult(bibSnap.Magias)
                    : ObterMagiasSeguroAsync();

                var tarefaPersonagem = _personagemService.ObterPorIdAsync(idPersonagem);
                await Task.WhenAll(tarefaMagias, tarefaPersonagem);

                MagiasCatalogo = tarefaMagias.Result;
                var encontrado = tarefaPersonagem.Result;

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
                            Magias.Add(MagiaGrimorioEntry.DeEntidade(pm.Magia));
                        }
                    }

                    MagiaEmVisualizacao = Magias.FirstOrDefault();
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

        // --- MÉTODOS DE MAGIAS (CATÁLOGO + PERSONALIZADA + VISUALIZAÇÃO) ---
        public void VincularMagiaDoCatalogo(Magia magia)
        {
            if (magia == null) return;
            if (Magias.Any(m => (m.IdMagia > 0 && m.IdMagia == magia.IdMagia) ||
                                m.Nome.Equals(magia.Nome, StringComparison.OrdinalIgnoreCase)))
                return;

            var entry = MagiaGrimorioEntry.DeEntidade(magia);
            Magias.Add(entry);
            MagiaEmVisualizacao = entry;
            NotifyStateChanged();
        }

        public void AlternarFormNovaMagia()
        {
            ExibirFormNovaMagia = !ExibirFormNovaMagia;
            if (ExibirFormNovaMagia)
                NovaMagiaManual = new MagiaGrimorioEntry();
            NotifyStateChanged();
        }

        public void AdicionarMagiaManual()
        {
            if (string.IsNullOrWhiteSpace(NovaMagiaManual.Nome))
            {
                Sucesso = false;
                MensagemFeedback = "Informe o nome da magia para adicionar.";
                NotifyStateChanged();
                return;
            }

            MensagemFeedback = "";
            var nova = new MagiaGrimorioEntry
            {
                Nome = NovaMagiaManual.Nome.Trim(),
                Circulo = NovaMagiaManual.Circulo,
                Escola = NovaMagiaManual.Escola.Trim(),
                TempoConjuracao = NovaMagiaManual.TempoConjuracao.Trim(),
                Alcance = NovaMagiaManual.Alcance.Trim(),
                Componentes = NovaMagiaManual.Componentes.Trim(),
                Duracao = NovaMagiaManual.Duracao.Trim(),
                DanoEfeito = NovaMagiaManual.DanoEfeito.Trim()
            };

            Magias.Add(nova);
            MagiaEmVisualizacao = nova;
            NovaMagiaManual = new MagiaGrimorioEntry();
            ExibirFormNovaMagia = false;
            NotifyStateChanged();
        }

        public void VisualizarMagia(MagiaGrimorioEntry? magia)
        {
            MagiaEmVisualizacao = magia;
            NotifyStateChanged();
        }

        public void VisualizarMagiaCatalogo(Magia magia)
        {
            MagiaEmVisualizacao = MagiaGrimorioEntry.DeEntidade(magia);
            NotifyStateChanged();
        }

        public void AdicionarMagia()
        {
            Magias.Add(new MagiaGrimorioEntry());
            NotifyStateChanged();
        }

        public void RemoverMagia(MagiaGrimorioEntry magia)
        {
            Magias.Remove(magia);
            if (MagiaEmVisualizacao == magia)
                MagiaEmVisualizacao = Magias.FirstOrDefault();
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
                        Nome = m.Nome.Trim(),
                        CirculoNivel = m.ObterNivelCirculo(),
                        Escola = string.IsNullOrWhiteSpace(m.Escola) ? null : m.Escola.Trim(),
                        TempoConjuracao = string.IsNullOrWhiteSpace(m.TempoConjuracao) ? null : m.TempoConjuracao.Trim(),
                        Alcance = string.IsNullOrWhiteSpace(m.Alcance) ? null : m.Alcance.Trim(),
                        Componentes = string.IsNullOrWhiteSpace(m.Componentes) ? null : m.Componentes.Trim(),
                        Duracao = string.IsNullOrWhiteSpace(m.Duracao) ? null : m.Duracao.Trim(),
                        DanoEfeito = string.IsNullOrWhiteSpace(m.DanoEfeito) ? null : m.DanoEfeito.Trim()
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

                if (Sucesso)
                {
                    LiberarProximaNavegacao = true;

                    // Write-through: atualiza a ficha editada direto na RAM e na Home
                    var idUsuario = PersonagemAtual.IdAccount > 0
                        ? PersonagemAtual.IdAccount
                        : _sessaoUsuario.IdUsuarioLogado;

                    if (idUsuario > 0 && PersonagemAtual.IdPersonagem > 0)
                    {
                        _appCache.UpsertPersonagem(idUsuario, PersonagemAtual);
                        _homeCache.UpsertPersonagem(idUsuario, PersonagemAtual);
                    }
                }

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
