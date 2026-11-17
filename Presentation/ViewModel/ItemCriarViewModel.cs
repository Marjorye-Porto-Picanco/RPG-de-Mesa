using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Itens;
using System;
using System.Threading.Tasks;

namespace RPGdeMesa.Presentation.ViewModel
{
    public class ItemCriarViewModel
    {
        private readonly ItemService _itemService;
        private readonly SessaoUsuarioService _sessaoUsuario;
        private readonly NavigationManager _navigation;

        public ItemCriarViewModel(
            ItemService itemService,
            SessaoUsuarioService sessaoUsuario,
            NavigationManager navigation)
        {
            _itemService = itemService;
            _sessaoUsuario = sessaoUsuario;
            _navigation = navigation;
            LimparFormulario();
        }

        // Campos do Formulário validados
        public string ItemNome { get; set; } = string.Empty;
        public string ItemTipo { get; set; } = "Equipamento de Aventura";
        public decimal? ItemPeso { get; set; }
        public decimal? ItemPreco { get; set; }
        public string ItemDano { get; set; } = string.Empty;
        public string ItemTipoDano { get; set; } = string.Empty;
        public string ItemPropriedades { get; set; } = string.Empty;
        public int ItemCaBonus { get; set; } = 0;
        public string ItemRaridade { get; set; } = "Sem raridade";
        public string ItemSintonizacao { get; set; } = "Não";
        public string ItemDescricao { get; set; } = string.Empty;

        public string MensagemFeedback { get; set; } = "";
        public bool Sucesso { get; set; } = false;
        public bool IsProcessing { get; set; } = false;

        // Controle de saída (NavigationLock)
        public bool ExibirAvisoSaida { get; private set; }
        public bool LiberarProximaNavegacao { get; private set; }
        public string? DestinoPendente { get; private set; }

        // Propriedades calculadas para o Preview Lateral em Tempo Real
        public string PreviewNome => string.IsNullOrWhiteSpace(ItemNome) ? "Novo Item" : ItemNome.Trim();
        public string PreviewTipo => string.IsNullOrWhiteSpace(ItemTipo) ? "Não definido" : ItemTipo;
        public string PreviewRaridade => string.IsNullOrWhiteSpace(ItemRaridade) ? "Sem raridade" : ItemRaridade;
        public string PreviewPeso => $"{(ItemPeso ?? 0m):0.##} kg";
        public string PreviewPreco => $"{(ItemPreco ?? 0m):0.##} PO";
        public string PreviewCa => $"+{Math.Max(0, ItemCaBonus)}";
        public string PreviewPropriedades => string.IsNullOrWhiteSpace(ItemPropriedades) ? "—" : ItemPropriedades.Trim();
        public string PreviewSintonizacao => string.IsNullOrWhiteSpace(ItemSintonizacao) ? "Não" : ItemSintonizacao;

        public string PreviewDano
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ItemDano)) return "—";
                return string.IsNullOrWhiteSpace(ItemTipoDano)
                    ? ItemDano.Trim()
                    : $"{ItemDano.Trim()} ({ItemTipoDano.Trim()})";
            }
        }

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();

        public void LimparFormulario()
        {
            ItemNome = string.Empty;
            ItemTipo = "Equipamento de Aventura";
            ItemPeso = null;
            ItemPreco = null;
            ItemDano = string.Empty;
            ItemTipoDano = string.Empty;
            ItemPropriedades = string.Empty;
            ItemCaBonus = 0;
            ItemRaridade = "Sem raridade";
            ItemSintonizacao = "Não";
            ItemDescricao = string.Empty;

            MensagemFeedback = "";
            Sucesso = false;
            IsProcessing = false;
            ExibirAvisoSaida = false;
            LiberarProximaNavegacao = false;
            DestinoPendente = null;
        }

        public bool FormularioIntocado =>
            string.IsNullOrWhiteSpace(ItemNome) &&
            string.IsNullOrWhiteSpace(ItemDescricao) &&
            string.IsNullOrWhiteSpace(ItemDano) &&
            ItemPeso == null &&
            ItemPreco == null;

        public async Task SalvarItemAsync()
        {
            if (IsProcessing) return;

            if (string.IsNullOrWhiteSpace(ItemNome))
            {
                Sucesso = false;
                MensagemFeedback = "O nome do item é obrigatório!";
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
                MensagemFeedback = "Forjando item...";
                NotifyStateChanged();

                // Tratamento rigoroso para evitar nulos ou strings vazias no banco de dados
                string? tipoFinal = string.IsNullOrWhiteSpace(ItemTipo) ? null : ItemTipo.Trim();
                string? danoFinal = string.IsNullOrWhiteSpace(ItemDano) ? null : ItemDano.Trim();
                string? tipoDanoFinal = string.IsNullOrWhiteSpace(ItemTipoDano) ? null : ItemTipoDano.Trim();
                string? propriedadesFinal = string.IsNullOrWhiteSpace(ItemPropriedades) ? null : ItemPropriedades.Trim();
                string? descricaoFinal = string.IsNullOrWhiteSpace(ItemDescricao) ? null : ItemDescricao.Trim();
                string? raridadeFinal = string.IsNullOrWhiteSpace(ItemRaridade) || ItemRaridade == "Sem raridade" ? null : ItemRaridade.Trim();
                string? sintonizacaoFinal = string.IsNullOrWhiteSpace(ItemSintonizacao) ? "Não" : ItemSintonizacao.Trim();

                decimal? pesoFinal = ItemPeso.HasValue ? Math.Max(0m, ItemPeso.Value) : null;
                decimal? precoFinal = ItemPreco.HasValue ? Math.Max(0m, ItemPreco.Value) : null;
                int caFinal = Math.Max(0, ItemCaBonus);

                var novoItem = new Item(
                    idAccount: idUsuario,
                    nome: ItemNome.Trim(),
                    tipo: tipoFinal,
                    peso: pesoFinal,
                    preco: precoFinal,
                    dano: danoFinal,
                    tipoDano: tipoDanoFinal,
                    propriedades: propriedadesFinal,
                    caBonus: caFinal,
                    descricao: descricaoFinal,
                    raridade: raridadeFinal,
                    sintonizacao: sintonizacaoFinal
                );

                var resultado = await _itemService.CriarItemAsync(idUsuario, novoItem);
                Sucesso = resultado.Sucesso;
                MensagemFeedback = resultado.Mensagem;

                if (Sucesso)
                {
                    LiberarProximaNavegacao = true;
                    NotifyStateChanged();

                    await Task.Delay(1000);
                    _navigation.NavigateTo("/BibliotecaGeral?categoria=itens");
                }
                else
                {
                    NotifyStateChanged();
                }
            }
            catch (Exception ex)
            {
                var erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                Sucesso = false;
                MensagemFeedback = $"Erro ao salvar: {erroDetalhado}";
                NotifyStateChanged();
            }
            finally
            {
                IsProcessing = false;
                NotifyStateChanged();
            }
        }

        public void CancelarCriacao()
        {
            AbrirAvisoSaida();
        }

        public void AbrirAvisoSaida()
        {
            if (FormularioIntocado || Sucesso)
            {
                LiberarProximaNavegacao = true;
                _navigation.NavigateTo("/BibliotecaGeral?categoria=itens");
                return;
            }
            ExibirAvisoSaida = true;
            NotifyStateChanged();
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
            NotifyStateChanged();
        }

        public void SairDaCriacao()
        {
            ExibirAvisoSaida = false;
            LiberarProximaNavegacao = true;
            _navigation.NavigateTo(DestinoPendente ?? "/BibliotecaGeral?categoria=itens");
        }
    }
}