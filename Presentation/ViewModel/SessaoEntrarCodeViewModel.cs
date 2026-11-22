using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Presentation.Services;

namespace RPGdeMesa.Presentation.ViewModel
{
    public class SessaoEntrarCodeViewModel
    {
        private readonly CampanhaService _campanhaService;
        private readonly PersonagemService _personagemService;
        private readonly SessaoUsuarioService _sessaoUsuario;
        private readonly AppCacheService _appCache;
        private readonly HomeCacheService _homeCache;
        private readonly NavigationManager _navigation;

        public SessaoEntrarCodeViewModel(
            CampanhaService campanhaService,
            PersonagemService personagemService,
            SessaoUsuarioService sessaoUsuario,
            AppCacheService appCache,
            HomeCacheService homeCache,
            NavigationManager navigation)
        {
            _campanhaService = campanhaService;
            _personagemService = personagemService;
            _sessaoUsuario = sessaoUsuario;
            _appCache = appCache;
            _homeCache = homeCache;
            _navigation = navigation;
        }

        // Controle de Fluxo e Feedback
        public int EtapaAtual { get; private set; } = 1;
        public string CodigoConvite { get; set; } = string.Empty;
        public string Mensagem { get; private set; } = string.Empty;
        public bool TemErro { get; private set; }
        public bool Carregando { get; private set; }

        // Dados da Campanha e Personagens
        public Campanha? CampanhaAtual { get; private set; }
        public List<Personagem> PersonagensDisponiveis { get; private set; } = new();
        public List<PersonagemCampanha> VinculosDoJogador { get; private set; } = new();
        public int PersonagemSelecionadoId { get; private set; }
        public Personagem? PersonagemEmPreparacao { get; private set; }

        // Inventário e Moedas em Preparação
        public List<ItemInventarioVinculado> ItensInventarioEdicao { get; private set; } = new();
        public int MoedasPC { get; set; } = 0;
        public int MoedasPP { get; set; } = 0;
        public int MoedasPE { get; set; } = 0;
        public int MoedasPO { get; set; } = 10;
        public int MoedasPL { get; set; } = 0;
        public string NotasInventario { get; set; } = string.Empty;

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();

        // Propriedades Calculadas
        public List<Item> ItensDaCampanha =>
            CampanhaAtual?.Itens.OrderBy(i => i.Nome).ToList() ?? new List<Item>();

        public List<Personagem> PersonagensNaoEnviados =>
            PersonagensDisponiveis
                .Where(p => !VinculosDoJogador.Any(v =>
                    v.IdPersonagem == p.IdPersonagem ||
                    (v.Personagem != null && v.Personagem.IdPersonagemOrigem == p.IdPersonagem)))
                .ToList();

        public decimal PesoTotalEdicao =>
            ItensInventarioEdicao
                .Where(i => i.IdItem > 0)
                .Sum(i => i.PesoUnitario * Math.Max(1, i.Quantidade));

        public decimal CapacidadeCargaMaximaEdicao =>
            Math.Round(Math.Max(1, PersonagemEmPreparacao?.Forca ?? 10) * 7.5m, 1);

        public void Limpar()
        {
            EtapaAtual = 1;
            CodigoConvite = string.Empty;
            Mensagem = string.Empty;
            TemErro = false;
            Carregando = false;

            CampanhaAtual = null;
            PersonagensDisponiveis.Clear();
            VinculosDoJogador.Clear();
            PersonagemSelecionadoId = 0;
            PersonagemEmPreparacao = null;

            ResetarInventarioEdicao();
        }

        private void ResetarInventarioEdicao()
        {
            ItensInventarioEdicao.Clear();
            MoedasPC = 0;
            MoedasPP = 0;
            MoedasPE = 0;
            MoedasPO = 10;
            MoedasPL = 0;
            NotasInventario = string.Empty;
        }

        public async Task ValidarCodigoAsync()
        {
            if (Carregando) return;

            Mensagem = string.Empty;
            TemErro = false;

            var codigo = CodigoConvite?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(codigo))
            {
                DefinirErro("Informe o código de convite antes de continuar.");
                return;
            }

            Carregando = true;
            NotifyStateChanged();

            try
            {
                if (!_sessaoUsuario.EstaLogado)
                {
                    DefinirErro("Faça login antes de entrar em uma campanha.");
                    return;
                }

                int idUsuario = _sessaoUsuario.IdUsuarioLogado;

                var campanhaEncontrada = await _campanhaService.BuscarPorCodigoAsync(codigo);
                if (campanhaEncontrada is null)
                {
                    DefinirErro("Nenhuma campanha foi encontrada com esse código.");
                    return;
                }

                var resultadoEntrada = await _campanhaService.EntrarNaCampanhaPorCodigoAsync(idUsuario, codigo);
                if (!resultadoEntrada.Sucesso &&
                    !resultadoEntrada.Mensagem.Contains("já está participando", StringComparison.OrdinalIgnoreCase))
                {
                    DefinirErro(resultadoEntrada.Mensagem);
                    return;
                }

                var persSnap = _appCache.ObterPersonagensLib(idUsuario);
                var tarefaDetalhes = _campanhaService.ObterDetalhesCampanhaAsync(campanhaEncontrada.IdCampanha);
                var tarefaPersonagens = persSnap is not null
                    ? Task.FromResult(persSnap.Personagens)
                    : _personagemService.ObterPersonagensPorUsuarioAsync(idUsuario);

                await Task.WhenAll(tarefaDetalhes, tarefaPersonagens);

                CampanhaAtual = tarefaDetalhes.Result ?? campanhaEncontrada;
                PersonagensDisponiveis = tarefaPersonagens.Result;

                // Write-through: campanha já entra instantaneamente na aba "Como Jogador" e na Home
                _appCache.UpsertCampanhaJogador(idUsuario, CampanhaAtual);
                _homeCache.UpsertCampanha(idUsuario, CampanhaAtual, comoMestre: false);

                VinculosDoJogador = CampanhaAtual.PersonagemCampanhas
                    .Where(pc => pc.Personagem != null && pc.Personagem.IdAccount == idUsuario)
                    .ToList();

                PersonagemSelecionadoId = 0;
                PersonagemEmPreparacao = null;
                ResetarInventarioEdicao();

                if (PersonagensNaoEnviados.Count == 1)
                {
                    await AoEscolherPersonagemAsync(PersonagensNaoEnviados[0].IdPersonagem);
                }

                EtapaAtual = 2;
            }
            catch (Exception ex)
            {
                DefinirErro($"Não foi possível validar o código: {ex.Message}");
            }
            finally
            {
                Carregando = false;
                NotifyStateChanged();
            }
        }

        public async Task AoEscolherPersonagemAsync(int idPersonagem)
        {
            PersonagemSelecionadoId = idPersonagem;
            PersonagemEmPreparacao = await _personagemService.ObterPorIdAsync(idPersonagem)
                                   ?? PersonagensDisponiveis.FirstOrDefault(p => p.IdPersonagem == idPersonagem);

            ResetarInventarioEdicao();

            if (PersonagemEmPreparacao?.Inventario != null)
            {
                MoedasPC = PersonagemEmPreparacao.Inventario.MoedasPC;
                MoedasPP = PersonagemEmPreparacao.Inventario.MoedasPP;
                MoedasPE = PersonagemEmPreparacao.Inventario.MoedasPE;
                MoedasPO = PersonagemEmPreparacao.Inventario.MoedasPO;
                MoedasPL = PersonagemEmPreparacao.Inventario.MoedasPL;
                NotasInventario = PersonagemEmPreparacao.Inventario.Notas ?? string.Empty;

                var idsPermitidos = ItensDaCampanha.Select(i => i.IdItem).ToHashSet();
                foreach (var ii in PersonagemEmPreparacao.Inventario.Itens)
                {
                    if (!idsPermitidos.Contains(ii.IdItem)) continue;

                    var itemRef = ItensDaCampanha.FirstOrDefault(x => x.IdItem == ii.IdItem) ?? ii.Item;
                    if (itemRef != null)
                    {
                        ItensInventarioEdicao.Add(new ItemInventarioVinculado
                        {
                            Categoria = PersonagemCriarViewModel.ClassificarCategoriaItem(itemRef),
                            IdItem = itemRef.IdItem,
                            Nome = itemRef.Nome,
                            Quantidade = ii.Quantidade,
                            PesoUnitario = itemRef.Peso ?? 0m,
                            PrecoUnitario = itemRef.Preco ?? 0m
                        });
                    }
                }
            }

            NotifyStateChanged();
        }

        public IEnumerable<Item> ObterItensCampanhaPorCategoria(string categoria)
        {
            if (!ItensDaCampanha.Any()) return Enumerable.Empty<Item>();
            var filtrados = ItensDaCampanha.Where(i => PersonagemCriarViewModel.ClassificarCategoriaItem(i) == categoria);
            return filtrados.Any() ? filtrados : ItensDaCampanha;
        }

        public void AdicionarSlotItem(string categoria)
        {
            ItensInventarioEdicao.Add(new ItemInventarioVinculado
            {
                Categoria = categoria,
                Quantidade = 1
            });
            NotifyStateChanged();
        }

        public void AlterarItemSlot(ItemInventarioVinculado slot, string? valorSelecionado)
        {
            if (int.TryParse(valorSelecionado, out int idItem))
            {
                slot.IdItem = idItem;
                var itemCampanha = ItensDaCampanha.FirstOrDefault(i => i.IdItem == idItem);
                if (itemCampanha != null)
                {
                    slot.Nome = itemCampanha.Nome;
                    slot.PesoUnitario = itemCampanha.Peso ?? 0m;
                    slot.PrecoUnitario = itemCampanha.Preco ?? 0m;
                }
                else
                {
                    slot.Nome = string.Empty;
                    slot.PesoUnitario = 0m;
                    slot.PrecoUnitario = 0m;
                }

                NotifyStateChanged();
            }
        }

        public void RemoverSlotItem(ItemInventarioVinculado slot)
        {
            ItensInventarioEdicao.Remove(slot);
            NotifyStateChanged();
        }

        public async Task EnviarFichaParaAnaliseAsync()
        {
            if (Carregando) return;

            if (CampanhaAtual is null || PersonagemEmPreparacao is null)
            {
                DefinirErro("Selecione um personagem para enviar ao Mestre.");
                return;
            }

            TemErro = false;
            Carregando = true;
            NotifyStateChanged();

            try
            {
                var dadosInventario = new Inventario
                {
                    CapacidadeCarga = CapacidadeCargaMaximaEdicao,
                    PesoAtual = PesoTotalEdicao,
                    Dinheiro = MoedasPO,
                    MoedasPC = MoedasPC,
                    MoedasPP = MoedasPP,
                    MoedasPE = MoedasPE,
                    MoedasPO = MoedasPO,
                    MoedasPL = MoedasPL,
                    Notas = NotasInventario
                };

                var listaItens = ItensInventarioEdicao
                    .Where(i => i.IdItem > 0)
                    .Select(i => (i.IdItem, Math.Max(1, i.Quantidade)))
                    .ToList();

                var resultadoInstancia = await _personagemService.InstanciarPersonagemParaCampanhaAsync(
                    PersonagemEmPreparacao.IdPersonagem,
                    CampanhaAtual.IdCampanha,
                    dadosInventario,
                    listaItens);

                if (!resultadoInstancia.Sucesso || resultadoInstancia.IdPersonagemInstancia <= 0)
                {
                    DefinirErro(resultadoInstancia.Mensagem);
                    return;
                }

                var resultadoAvaliacao = await _campanhaService.EnviarFichaParaAvaliacaoAsync(
                    CampanhaAtual.IdCampanha,
                    resultadoInstancia.IdPersonagemInstancia);

                if (!resultadoAvaliacao.Sucesso)
                {
                    DefinirErro(resultadoAvaliacao.Mensagem);
                    return;
                }

                int idUsuario = _sessaoUsuario.IdUsuarioLogado;
                _appCache.UpsertCampanhaJogador(idUsuario, CampanhaAtual);
                _homeCache.UpsertCampanha(idUsuario, CampanhaAtual, comoMestre: false);

                Mensagem = "Ficha e inventário enviados para análise do Mestre! Voltando para suas campanhas...";
                NotifyStateChanged();

                await Task.Delay(700);
                _navigation.NavigateTo("/campanhas");
            }
            catch (Exception ex)
            {
                DefinirErro($"Erro ao enviar ficha para análise: {ex.Message}");
            }
            finally
            {
                Carregando = false;
                NotifyStateChanged();
            }
        }

        public void VoltarParaCodigo()
        {
            EtapaAtual = 1;
            Mensagem = string.Empty;
            TemErro = false;
            NotifyStateChanged();
        }

        public bool PodeAcessarEtapa(int etapa) =>
            etapa == 1 || (etapa == 2 && CampanhaAtual is not null);

        public void IrParaEtapa(int etapa)
        {
            if (PodeAcessarEtapa(etapa))
            {
                EtapaAtual = etapa;
                Mensagem = string.Empty;
                TemErro = false;
                NotifyStateChanged();
            }
        }

        public void VoltarParaCampanhas() => _navigation.NavigateTo("/campanhas");

        private void DefinirErro(string mensagem)
        {
            TemErro = true;
            Mensagem = mensagem;
            NotifyStateChanged();
        }
    }
}