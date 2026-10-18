using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using RPGdeMesa.Models;
using RPGdeMesa.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RPGdeMesa.ViewModel
{
    public class CriarCampanhaViewModel
    {
        private readonly CampanhaService _campanhaService;
        private readonly NavigationManager _navigation;
        private readonly IJSRuntime _jsRuntime;
        private readonly SessaoUsuarioService _sessaoUsuario;

        public CriarCampanhaViewModel(
            CampanhaService campanhaService,
            NavigationManager navigation,
            IJSRuntime jsRuntime,
            SessaoUsuarioService sessaoUsuario)
        {
            _campanhaService = campanhaService;
            _navigation = navigation;
            _jsRuntime = jsRuntime;
            _sessaoUsuario = sessaoUsuario;

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

        public int IdMonstroSelecionadoNaLista { get; set; }
        public int IdNPCSelecionadoNaLista { get; set; }
        public int IdItemSelecionadoNaLista { get; set; }

        public List<IBrowserFile> ListaMapas { get; set; } = new();
        public IBrowserFile? ArquivoCapa { get; set; }

        public bool ExibirModalSelecionarMonstro { get; set; } = false;
        public bool ExibirModalSelecionarNPC { get; set; } = false;
        public bool ExibirModalSelecionarItem { get; set; } = false;

        public Monstro? MonstroSelecionadoParaVer { get; set; }
        public NPC? NPCSelecionadoParaVer { get; set; }
        public Item? ItemSelecionadoParaVer { get; set; }

        public string MensagemFeedback { get; set; } = "";
        public bool Sucesso { get; set; } = false;
        public bool IsProcessing { get; set; } = false;

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();

        public async Task CarregarBibliotecasAsync()
        {
            int idUsuario = _sessaoUsuario.IdUsuarioLogado;
            if (idUsuario > 0)
            {
                MonstrosDisponiblesBiblioteca = await _campanhaService.ObterMonstrosDoUsuarioAsync(idUsuario);
                NPCsDisponiveisBiblioteca = await _campanhaService.ObterNPCsDoUsuarioAsync(idUsuario);
                ItensDisponiveisBiblioteca = await _campanhaService.ObterItensDoUsuarioAsync(idUsuario);
                NotifyStateChanged();
            }
        }

        // --- MÉTODOS DE MONSTROS ---
        public void AbrirModalSelecionarMonstro()
        {
            ExibirModalSelecionarMonstro = true;
            NotifyStateChanged();
        }

        public void FecharModalSelecionarMonstro()
        {
            ExibirModalSelecionarMonstro = false;
            IdMonstroSelecionadoNaLista = 0;
            NotifyStateChanged();
        }

        public void AdicionarMonstroDaBiblioteca(Monstro monstro)
        {
            if (monstro != null && !ListaMonstros.Contains(monstro))
            {
                ListaMonstros.Add(monstro);
                FecharModalSelecionarMonstro();
            }
        }

        public void RemoverMonstro(Monstro monstro)
        {
            ListaMonstros.Remove(monstro);
            NotifyStateChanged();
        }

        public void AbrirDetalhesMonstro(Monstro monstro)
        {
            MonstroSelecionadoParaVer = monstro;
            NotifyStateChanged();
        }

        public void FecharDetalhesMonstro()
        {
            MonstroSelecionadoParaVer = null;
            NotifyStateChanged();
        }

        // --- MÉTODOS DE NPCs ---
        public void AbrirModalSelecionarNPC()
        {
            ExibirModalSelecionarNPC = true;
            NotifyStateChanged();
        }

        public void FecharModalSelecionarNPC()
        {
            ExibirModalSelecionarNPC = false;
            IdNPCSelecionadoNaLista = 0;
            NotifyStateChanged();
        }

        public void AdicionarNPCDaBiblioteca(NPC npc)
        {
            if (npc != null && !ListaNPCs.Contains(npc))
            {
                ListaNPCs.Add(npc);
                FecharModalSelecionarNPC();
            }
        }

        public void RemoverNPC(NPC npc)
        {
            ListaNPCs.Remove(npc);
            NotifyStateChanged();
        }

        public void AbrirDetalhesNPC(NPC npc)
        {
            NPCSelecionadoParaVer = npc;
            NotifyStateChanged();
        }

        public void FecharDetalhesNPC()
        {
            NPCSelecionadoParaVer = null;
            NotifyStateChanged();
        }

        // --- MÉTODOS DE ITENS ---
        public void AbrirModalSelecionarItem()
        {
            ExibirModalSelecionarItem = true;
            NotifyStateChanged();
        }

        public void FecharModalSelecionarItem()
        {
            ExibirModalSelecionarItem = false;
            IdItemSelecionadoNaLista = 0;
            NotifyStateChanged();
        }

        public void AdicionarItemDaBiblioteca(Item item)
        {
            if (item != null && !ListaItensCampanha.Contains(item))
            {
                ListaItensCampanha.Add(item);
                FecharModalSelecionarItem();
            }
        }

        public void RemoverItem(Item item)
        {
            ListaItensCampanha.Remove(item);
            NotifyStateChanged();
        }

        public void AbrirDetalhesItem(Item item)
        {
            ItemSelecionadoParaVer = item;
            NotifyStateChanged();
        }

        public void FecharDetalhesItem()
        {
            ItemSelecionadoParaVer = null;
            NotifyStateChanged();
        }

        // --- MAPAS E CAPA ---
        public void AdicionarMapas(InputFileChangeEventArgs e)
        {
            foreach (var arquivo in e.GetMultipleFiles(10))
            {
                ListaMapas.Add(arquivo);
            }
            NotifyStateChanged();
        }

        public void RemoverMapa(IBrowserFile mapa)
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
                    ListaMonstros,
                    ListaNPCs,
                    ListaItensCampanha
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