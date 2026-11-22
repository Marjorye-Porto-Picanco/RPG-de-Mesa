using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Presentation.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RPGdeMesa.Presentation.ViewModel
{
    public class CriarCampanhaViewModel
    {
        private readonly CampanhaService _campanhaService;
        private readonly NavigationManager _navigation;
        private readonly IJSRuntime _jsRuntime;
        private readonly SessaoUsuarioService _sessaoUsuario;
        private readonly ImagemPickerService _imagemPicker;

        public CriarCampanhaViewModel(
            CampanhaService campanhaService,
            NavigationManager navigation,
            IJSRuntime jsRuntime,
            SessaoUsuarioService sessaoUsuario,
            ImagemPickerService imagemPicker)
        {
            _campanhaService = campanhaService;
            _navigation = navigation;
            _jsRuntime = jsRuntime;
            _sessaoUsuario = sessaoUsuario;
            _imagemPicker = imagemPicker;

            CodigoConvite = GerarCodigoConvite();
        }

        public string Nome { get; set; } = "";
        public string Notas { get; set; } = "";
        public string CodigoConvite { get; set; } = "";

        public List<Monstro> ListaMonstros { get; set; } = new();
        public List<NPC> ListaNPCs { get; set; } = new();
        public List<Item> ListaItensCampanha { get; set; } = new();

        public List<Monstro> MonstrosDisponiblesBiblioteca { get; set; } = new();
        public List<NPC> NPCsDisponiveisBiblioteca { get; set; } = new();
        public List<Item> ItensDisponiveisBiblioteca { get; set; } = new();

        // Termos de pesquisa por digitação
        public string BuscaMonstro { get; set; } = "";
        public string BuscaNPC { get; set; } = "";
        public string BuscaItem { get; set; } = "";

        public List<Mapa> ListaMapas { get; set; } = new();
        public string? CapaBase64 { get; set; }
        public string? NomeArquivoCapa { get; set; }
        public bool CarregandoImagem { get; private set; }

        public string MensagemFeedback { get; set; } = "";
        public bool Sucesso { get; set; } = false;
        public bool IsProcessing { get; set; } = false;
        public bool CarregandoImagem { get; private set; }

        public int TotalRecursosVinculados =>
            ListaMonstros.Count + ListaNPCs.Count + ListaItensCampanha.Count;

        public IEnumerable<Monstro> MonstrosFiltrados =>
            MonstrosDisponiblesBiblioteca
                .Where(m => !ListaMonstros.Any(v => v.IdMonstro == m.IdMonstro))
                .Where(m => string.IsNullOrWhiteSpace(BuscaMonstro) ||
                            m.Nome.Contains(BuscaMonstro.Trim(), StringComparison.OrdinalIgnoreCase) ||
                            (m.Tipo?.Contains(BuscaMonstro.Trim(), StringComparison.OrdinalIgnoreCase) ?? false))
                .OrderBy(m => m.Nome)
                .Take(8);

        public IEnumerable<NPC> NPCsFiltrados =>
            NPCsDisponiveisBiblioteca
                .Where(n => !ListaNPCs.Any(v => v.IdNpc == n.IdNpc))
                .Where(n => string.IsNullOrWhiteSpace(BuscaNPC) ||
                            n.Nome.Contains(BuscaNPC.Trim(), StringComparison.OrdinalIgnoreCase) ||
                            (n.ClasseTexto?.Contains(BuscaNPC.Trim(), StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (n.RacaTexto?.Contains(BuscaNPC.Trim(), StringComparison.OrdinalIgnoreCase) ?? false))
                .OrderBy(n => n.Nome)
                .Take(8);

        public IEnumerable<Item> ItensFiltrados =>
            ItensDisponiveisBiblioteca
                .Where(i => !ListaItensCampanha.Any(v => v.IdItem == i.IdItem))
                .Where(i => string.IsNullOrWhiteSpace(BuscaItem) ||
                            i.Nome.Contains(BuscaItem.Trim(), StringComparison.OrdinalIgnoreCase) ||
                            (i.Tipo?.Contains(BuscaItem.Trim(), StringComparison.OrdinalIgnoreCase) ?? false))
                .OrderBy(i => i.Nome)
                .Take(8);

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();

        public async Task CarregarBibliotecasAsync()
        {
            int idUsuario = _sessaoUsuario.IdUsuarioLogado;
            if (idUsuario <= 0) return;

            var tarefaMonstros = _campanhaService.ObterMonstrosDoUsuarioAsync(idUsuario);
            var tarefaNpcs = _campanhaService.ObterNPCsDoUsuarioAsync(idUsuario);
            var tarefaItens = _campanhaService.ObterItensDoUsuarioAsync(idUsuario);

            await Task.WhenAll(tarefaMonstros, tarefaNpcs, tarefaItens);

            MonstrosDisponiblesBiblioteca = tarefaMonstros.Result.Where(m => m.IdAccount == idUsuario).ToList();
            NPCsDisponiveisBiblioteca = tarefaNpcs.Result.Where(n => n.IdAccount == idUsuario).ToList();
            ItensDisponiveisBiblioteca = tarefaItens.Result.Where(i => i.IdAccount == idUsuario).ToList();

            NotifyStateChanged();
        }

        // --- MONSTROS ---
        public void AdicionarMonstroDaBiblioteca(Monstro monstro)
        {
            if (monstro != null && !ListaMonstros.Any(m => m.IdMonstro == monstro.IdMonstro))
            {
                ListaMonstros.Add(monstro);
                BuscaMonstro = "";
                NotifyStateChanged();
            }
        }

        public void RemoverMonstro(Monstro monstro)
        {
            ListaMonstros.Remove(monstro);
            NotifyStateChanged();
        }

        // --- NPCs ---
        public void AdicionarNPCDaBiblioteca(NPC npc)
        {
            if (npc != null && !ListaNPCs.Any(n => n.IdNpc == npc.IdNpc))
            {
                ListaNPCs.Add(npc);
                BuscaNPC = "";
                NotifyStateChanged();
            }
        }

        public void RemoverNPC(NPC npc)
        {
            ListaNPCs.Remove(npc);
            NotifyStateChanged();
        }

        // --- ITENS ---
        public void AdicionarItemDaBiblioteca(Item item)
        {
            if (item != null && !ListaItensCampanha.Any(i => i.IdItem == item.IdItem))
            {
                ListaItensCampanha.Add(item);
                BuscaItem = "";
                NotifyStateChanged();
            }
        }

        public void RemoverItem(Item item)
        {
            ListaItensCampanha.Remove(item);
            NotifyStateChanged();
        }

        // --- MAPAS E CAPA ---
        public async Task SelecionarCapaAsync()
        {
            if (CarregandoImagem) return;
            CarregandoImagem = true;
            NotifyStateChanged();

            try
            {
                var res = await _imagemPicker.SelecionarUmaAsync(ladoMaximo: 1280);
                if (res.Erro is not null)
                {
                    Sucesso = false;
                    MensagemFeedback = res.Erro;
                }
                else if (res.Imagem is not null)
                {
                    CapaBase64 = res.Imagem.DataUrl;
                    NomeArquivoCapa = res.Imagem.NomeArquivo;
                    MensagemFeedback = "";
                }
            }
            finally
            {
                CarregandoImagem = false;
                NotifyStateChanged();
            }
        }

        public void RemoverCapa()
        {
            CapaBase64 = null;
            NomeArquivoCapa = null;
            NotifyStateChanged();
        }

        public async Task AdicionarMapasAsync()
        {
            if (CarregandoImagem) return;
            CarregandoImagem = true;
            NotifyStateChanged();

            try
            {
                var res = await _imagemPicker.SelecionarVariasAsync(maxArquivos: 10, ladoMaximo: 1600);
                foreach (var img in res.Imagens)
                {
                    ListaMapas.Add(new Mapa
                    {
                        IdAccount = _sessaoUsuario.IdUsuarioLogado,
                        NomeRegiao = Path.GetFileNameWithoutExtension(img.NomeArquivo),
                        ImagemUrl = img.DataUrl
                    });
                }

                if (res.Erro is not null)
                {
                    Sucesso = false;
                    MensagemFeedback = res.Erro;
                }
                else if (res.Imagens.Count > 0)
                {
                    MensagemFeedback = "";
                }
            }
            finally
            {
                CarregandoImagem = false;
                NotifyStateChanged();
            }
        }

        public void RemoverMapa(Mapa mapa)
        {
            ListaMapas.Remove(mapa);
            NotifyStateChanged();
        }

        // --- CÓDIGO DE CONVITE ---
        public string GerarCodigoConvite()
        {
            const string caracteres = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            char[] codigo = new char[6];
            for (int i = 0; i < codigo.Length; i++) codigo[i] = caracteres[random.Next(caracteres.Length)];
            return $"{new string(codigo, 0, 3)}-{new string(codigo, 3, 3)}";
        }

        public async Task CopiarCodigoAsync()
        {
            if (string.IsNullOrEmpty(CodigoConvite)) return;
            try
            {
                await _jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", CodigoConvite);
                MensagemFeedback = "Código copiado!";
                Sucesso = true;
            }
            catch { MensagemFeedback = "Erro ao copiar."; Sucesso = false; }
            finally { NotifyStateChanged(); }
        }

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

                int idUsuarioLogado = _sessaoUsuario.IdUsuarioLogado;
                if (idUsuarioLogado <= 0)
                {
                    Sucesso = false;
                    MensagemFeedback = "Sessão expirada. Faça login novamente.";
                    NotifyStateChanged();
                    return;
                }

                var resultado = await _campanhaService.CriarCampanhaCompletaAsync(
                    idUsuarioLogado,
                    Nome,
                    Notas,
                    CodigoConvite,
                    CapaBase64,
                    ListaMonstros,
                    ListaNPCs,
                    ListaItensCampanha,
                    ListaMapas
                );

                Sucesso = resultado.Sucesso;
                MensagemFeedback = resultado.Mensagem;

                if (Sucesso)
                {
                    await Task.Delay(1000);
                    _navigation.NavigateTo("/campanhas");
                }
            }
            catch (Exception ex)
            {
                Sucesso = false;
                MensagemFeedback = $"Erro de comunicação com o banco: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
                NotifyStateChanged();
            }
        }
    }
}