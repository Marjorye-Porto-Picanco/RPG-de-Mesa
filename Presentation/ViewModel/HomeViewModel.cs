using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using RPGdeMesa.Application.Services;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Presentation.Services;

namespace RPGdeMesa.Presentation.ViewModel
{
    public class AtividadeRecenteItem
    {
        public string Iniciais { get; set; } = "✦";
        public string Titulo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public string Meta { get; set; } = string.Empty;
        public string Rota { get; set; } = "/campanhas";
        public DateTime DataReferencia { get; set; }
        public int OrdemId { get; set; }
    }

    public class HomeViewModel
    {
        internal const string CapaPadrao = "img/home-campanhas.webp";
        private static readonly TimeSpan ValidadeCache = TimeSpan.FromMinutes(2);

        private readonly SemaphoreSlim _semaforoCarga = new(1, 1);
        private readonly CampanhaService _campanhaService;
        private readonly PersonagemService _personagemService;
        private readonly NpcService _npcService;
        private readonly ItemService _itemService;
        private readonly SessaoUsuarioService _sessaoUsuario;
        private readonly HomeCacheService _cache;
        private readonly AppCacheService _appCache;
        private readonly NavigationManager _navigation;

        private int _totalMestrePreview;
        private int _totalJogadorPreview;
        private int _totalPersonagensDestaque;
        private int _totalElementosMesaDestaque;

        public HomeViewModel(
            CampanhaService campanhaService,
            PersonagemService personagemService,
            NpcService npcService,
            ItemService itemService,
            SessaoUsuarioService sessaoUsuario,
            HomeCacheService cache,
            AppCacheService appCache,
            NavigationManager navigation)
        {
            _campanhaService = campanhaService;
            _personagemService = personagemService;
            _npcService = npcService;
            _itemService = itemService;
            _sessaoUsuario = sessaoUsuario;
            _cache = cache;
            _appCache = appCache;
            _navigation = navigation;
        }

        public bool Carregando { get; private set; }
        public List<Campanha> Campanhas { get; private set; } = new();
        public List<Campanha> CampanhasComoMestre { get; private set; } = new();
        public List<Campanha> CampanhasComoJogador { get; private set; } = new();
        public List<Personagem> PersonagensUsuario { get; private set; } = new();
        public List<NPC> NpcsUsuario { get; private set; } = new();
        public List<Item> ItensUsuario { get; private set; } = new();
        public Campanha? CampanhaEmDestaque { get; private set; }
        public HomeCampanhaResumo? DestaqueResumo { get; private set; }
        public List<HomeCampanhaResumo> CampanhasResumo { get; private set; } = new();
        public int IndiceCampanhaCarrossel { get; private set; }
        public IReadOnlyList<HomeCampanhaResumo?> CampanhasCarrossel =>
            CampanhasResumo.Count == 0 ? new[] { DestaqueResumo } : CampanhasResumo;
        public HomeCampanhaResumo? CampanhaCarrosselAtual =>
            CampanhasResumo.Count == 0 ? DestaqueResumo : CampanhasResumo[IndiceCampanhaCarrossel];
        public List<AtividadeRecenteItem> AtividadesRecentes { get; private set; } = new();

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();

        public string PrimeiroNomeUsuario
        {
            get
            {
                var nome = string.IsNullOrWhiteSpace(_sessaoUsuario.NomeUsuario)
                    ? "Mestre da mesa"
                    : _sessaoUsuario.NomeUsuario;

                return nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Aventureiro";
            }
        }

        public string DataFormatada =>
            DateTime.Now.ToString("D", new CultureInfo("pt-BR"));

        public bool EhApenasJogador =>
            _totalJogadorPreview > 0 && _totalMestrePreview == 0;

        public bool ExibirResumoMesa => DestaqueResumo is not null;

        public int TotalPersonagensDestaque => _totalPersonagensDestaque;

        public int TotalElementosMesaDestaque => _totalElementosMesaDestaque;

        public bool EhMestreDaCampanha(Campanha campanha) =>
            campanha.IdAccount == _sessaoUsuario.IdUsuarioLogado;

        public bool EhMestreDaCampanha(HomeCampanhaResumo campanha) =>
            campanha.IdAccount == _sessaoUsuario.IdUsuarioLogado;

        public string ObterCapaCampanha(Campanha? campanha, string fallback = CapaPadrao) =>
            string.IsNullOrWhiteSpace(campanha?.Capa) ? fallback : campanha.Capa;

        public string ObterCapaCampanha(HomeCampanhaResumo? campanha, string fallback = CapaPadrao) =>
            string.IsNullOrWhiteSpace(campanha?.Capa) ? fallback : campanha.Capa;

        public string ObterRotaCampanha(Campanha? campanha)
        {
            if (campanha is null) return "/criar-campanha";

            return EhMestreDaCampanha(campanha)
                ? $"/sessao-iniciar/{campanha.IdCampanha}"
                : $"/sessao-entrar/{campanha.IdCampanha}";
        }

        public string ObterRotaCampanha(HomeCampanhaResumo? campanha)
        {
            if (campanha is null) return "/criar-campanha";

            return EhMestreDaCampanha(campanha)
                ? $"/sessao-iniciar/{campanha.IdCampanha}"
                : $"/sessao-entrar/{campanha.IdCampanha}";
        }

        public string ObterTextoBotaoHero(HomeCampanhaResumo? campanha)
        {
            if (campanha is null) return "Criar campanha";

            return EhMestreDaCampanha(campanha) ? "Iniciar sessão" : "Entrar na sessão";
        }

        public string RotaBotaoSecundarioHero =>
            EhApenasJogador ? "/campanhas" : "/saladojogo";

        public string TextoBotaoSecundarioHero =>
            EhApenasJogador ? "Ver campanhas" : "Entrar na sala";

        public void CampanhaAnterior() => MoverCampanhaCarrossel(-1);

        public void ProximaCampanha() => MoverCampanhaCarrossel(1);

        public void SelecionarCampanhaCarrossel(int indice)
        {
            if (indice < 0 || indice >= CampanhasResumo.Count || indice == IndiceCampanhaCarrossel)
                return;

            IndiceCampanhaCarrossel = indice;
            NotifyStateChanged();
        }

        private void MoverCampanhaCarrossel(int direcao)
        {
            int total = CampanhasResumo.Count;
            if (total > 1)
                SelecionarCampanhaCarrossel((IndiceCampanhaCarrossel + direcao + total) % total);
        }

        /// <summary>
        /// Hidrata a Home de forma síncrona (RAM primeiro, depois JSON em disco)
        /// para renderizar imediatamente sem tela vazia.
        /// </summary>
        public bool HidratarDoCacheSincrono()
        {
            if (!_sessaoUsuario.EstaLogado)
                return false;

            int idUsuario = _sessaoUsuario.IdUsuarioLogado;

            var snapshotRam = _cache.Obter(idUsuario);
            if (snapshotRam is not null)
            {
                AplicarSnapshot(snapshotRam);
                return true;
            }

            var snapshotDisco = _cache.ObterDoDisco(idUsuario);
            if (snapshotDisco is not null)
            {
                AplicarDiscoSnapshot(snapshotDisco);
                return true;
            }

            return false;
        }

        public async Task CarregarDadosAsync(bool forcarAtualizacao = false)
        {
            if (!_sessaoUsuario.EstaLogado)
            {
                _navigation.NavigateTo("/login");
                return;
            }

            int idUsuario = _sessaoUsuario.IdUsuarioLogado;

            // 1. Se o cache em RAM existe e não expirou, aplica em 0ms sem ir ao banco
            var snapshot = _cache.Obter(idUsuario);
            if (snapshot is not null)
            {
                AplicarSnapshot(snapshot);
                NotifyStateChanged();

                if (!forcarAtualizacao && !_cache.EstaExpirado(idUsuario, ValidadeCache))
                    return;
            }
            else if (DestaqueResumo is null && CampanhasResumo.Count == 0)
            {
                // Se ainda não hidratou do disco, hidrata agora antes de consultar o banco
                var disco = _cache.ObterDoDisco(idUsuario);
                if (disco is not null)
                {
                    AplicarDiscoSnapshot(disco);
                    NotifyStateChanged();
                }
            }

            // 2. Atualiza em segundo plano (ou faz a primeira carga) com proteção de concorrência
            if (!await _semaforoCarga.WaitAsync(0))
                return;

            bool jaTinhaPreview = snapshot is not null || DestaqueResumo is not null || CampanhasResumo.Count > 0;
            Carregando = !jaTinhaPreview;
            if (Carregando) NotifyStateChanged();

            try
            {
                var campanhasSnap = !forcarAtualizacao && !_appCache.CampanhasExpiradas(idUsuario, ValidadeCache)
                    ? _appCache.ObterCampanhas(idUsuario)
                    : null;

                var personagensSnap = !forcarAtualizacao && !_appCache.PersonagensLibExpirados(idUsuario, ValidadeCache)
                    ? _appCache.ObterPersonagensLib(idUsuario)
                    : null;

                var bibliotecaSnap = !forcarAtualizacao && !_appCache.BibliotecaExpirada(idUsuario, ValidadeCache)
                    ? _appCache.ObterBiblioteca(idUsuario)
                    : null;

                var tarefaMestre = campanhasSnap is not null
                    ? Task.FromResult(campanhasSnap.CampanhasMestre)
                    : _campanhaService.ObterCampanhasPorUsuarioAsync(idUsuario);

                var tarefaJogador = campanhasSnap is not null
                    ? Task.FromResult(campanhasSnap.CampanhasJogador)
                    : _campanhaService.ObterCampanhasComoJogadorAsync(idUsuario);

                var tarefaPersonagens = personagensSnap is not null
                    ? Task.FromResult(personagensSnap.Personagens)
                    : _personagemService.ObterPersonagensPorUsuarioAsync(idUsuario);

                var tarefaNpcs = personagensSnap is not null
                    ? Task.FromResult(personagensSnap.Npcs)
                    : bibliotecaSnap is not null
                        ? Task.FromResult(bibliotecaSnap.Npcs)
                        : _npcService.ObterNpcsPorUsuarioAsync(idUsuario);

                var tarefaItens = bibliotecaSnap is not null
                    ? Task.FromResult(bibliotecaSnap.Itens)
                    : ObterItensListaAsync(idUsuario);

                await Task.WhenAll(tarefaMestre, tarefaJogador, tarefaPersonagens, tarefaNpcs, tarefaItens);

                CampanhasComoMestre = tarefaMestre.Result;
                CampanhasComoJogador = tarefaJogador.Result;
                PersonagensUsuario = tarefaPersonagens.Result;
                NpcsUsuario = tarefaNpcs.Result;
                ItensUsuario = tarefaItens.Result;

                if (campanhasSnap is null)
                {
                    _appCache.SalvarCampanhas(new CampanhasCacheSnapshot
                    {
                        IdUsuario = idUsuario,
                        AtualizadoEmUtc = DateTime.UtcNow,
                        CampanhasMestre = CampanhasComoMestre,
                        CampanhasJogador = CampanhasComoJogador
                    });
                }

                if (personagensSnap is null)
                {
                    _appCache.SalvarPersonagensLib(new PersonagensLibCacheSnapshot
                    {
                        IdUsuario = idUsuario,
                        AtualizadoEmUtc = DateTime.UtcNow,
                        Personagens = PersonagensUsuario,
                        Npcs = NpcsUsuario
                    });
                }

                Campanhas = CampanhasComoMestre
                    .Concat(CampanhasComoJogador)
                    .DistinctBy(c => c.IdCampanha)
                    .OrderByDescending(c => c.DataCriacao)
                    .ThenByDescending(c => c.IdCampanha)
                    .ToList();

                // Pinta a tela imediatamente antes de buscar os detalhes pesados da campanha em destaque
                var primeira = Campanhas.FirstOrDefault();
                CampanhaEmDestaque = primeira;
                AtividadesRecentes = GerarAtividadesRecentes(idUsuario, Campanhas, PersonagensUsuario, NpcsUsuario, ItensUsuario);
                SincronizarCamposResumo();
                Carregando = false;
                NotifyStateChanged();

                if (primeira is not null)
                {
                    var detalhada = await _campanhaService.ObterDetalhesCampanhaAsync(primeira.IdCampanha);
                    if (detalhada is not null)
                    {
                        CampanhaEmDestaque = detalhada;
                        SincronizarCamposResumo();
                    }
                }

                _cache.Salvar(new HomeCacheSnapshot
                {
                    IdUsuario = idUsuario,
                    AtualizadoEmUtc = DateTime.UtcNow,
                    Campanhas = Campanhas,
                    CampanhasComoMestre = CampanhasComoMestre,
                    CampanhasComoJogador = CampanhasComoJogador,
                    PersonagensUsuario = PersonagensUsuario,
                    NpcsUsuario = NpcsUsuario,
                    ItensUsuario = ItensUsuario,
                    CampanhaEmDestaque = CampanhaEmDestaque,
                    AtividadesRecentes = AtividadesRecentes
                });
            }
            finally
            {
                Carregando = false;
                _semaforoCarga.Release();
                NotifyStateChanged();
            }
        }

        private async Task<List<Item>> ObterItensListaAsync(int idUsuario)
        {
            var itens = await _itemService.ObterItensPorUsuarioAsync(idUsuario);
            return itens.ToList();
        }

        private void AplicarSnapshot(HomeCacheSnapshot s)
        {
            Campanhas = s.Campanhas;
            CampanhasComoMestre = s.CampanhasComoMestre;
            CampanhasComoJogador = s.CampanhasComoJogador;
            PersonagensUsuario = s.PersonagensUsuario;
            NpcsUsuario = s.NpcsUsuario;
            ItensUsuario = s.ItensUsuario;
            CampanhaEmDestaque = s.CampanhaEmDestaque;
            AtividadesRecentes = s.AtividadesRecentes;
            SincronizarCamposResumo();
        }

        private void AplicarDiscoSnapshot(HomeDiscoSnapshot d)
        {
            _totalMestrePreview = d.TotalCampanhasMestre;
            _totalJogadorPreview = d.TotalCampanhasJogador;
            DestaqueResumo = d.CampanhaDestaque;
            _totalPersonagensDestaque = d.TotalPersonagensDestaque;
            _totalElementosMesaDestaque = d.TotalElementosMesaDestaque;
            AtualizarCampanhasResumo(d.CampanhasResumo);
            AtividadesRecentes = d.AtividadesRecentes;
        }

        private void SincronizarCamposResumo()
        {
            _totalMestrePreview = CampanhasComoMestre.Count;
            _totalJogadorPreview = CampanhasComoJogador.Count;
            DestaqueResumo = CampanhaEmDestaque is null ? null : ParaResumo(CampanhaEmDestaque);
            _totalPersonagensDestaque = CampanhaEmDestaque?.Personagens.Count() ?? 0;
            _totalElementosMesaDestaque =
                (CampanhaEmDestaque?.MonstroCampanhas.Count ?? 0) +
                (CampanhaEmDestaque?.NpcCampanhas.Count ?? 0);
            AtualizarCampanhasResumo(Campanhas.Take(3).Select(ParaResumo).ToList());
        }

        private void AtualizarCampanhasResumo(List<HomeCampanhaResumo> campanhas)
        {
            var idSelecionado = CampanhaCarrosselAtual?.IdCampanha;
            CampanhasResumo = campanhas;
            IndiceCampanhaCarrossel = Math.Max(0, campanhas.FindIndex(c => c.IdCampanha == idSelecionado));
        }

        private static HomeCampanhaResumo ParaResumo(Campanha c) => new()
        {
            IdCampanha = c.IdCampanha,
            IdAccount = c.IdAccount,
            Nome = c.Nome,
            Descricao = c.Descricao,
            DataCriacao = c.DataCriacao,
            Capa = c.Capa
        };

        public static List<AtividadeRecenteItem> GerarAtividadesRecentes(
            int idUsuario,
            List<Campanha> campanhas,
            List<Personagem> personagens,
            List<NPC> npcs,
            List<Item> itens)
        {
            var eventos = new List<AtividadeRecenteItem>();

            foreach (var c in campanhas)
            {
                bool ehMestre = c.IdAccount == idUsuario;
                eventos.Add(new AtividadeRecenteItem
                {
                    Iniciais = ObterIniciais(c.Nome),
                    Titulo = c.Nome,
                    Descricao = ehMestre ? "Campanha criada como Mestre" : "Campanha vinculada como Jogador",
                    Meta = c.DataCriacao.ToString("dd/MM"),
                    Rota = ehMestre ? $"/sessao-iniciar/{c.IdCampanha}" : $"/sessao-entrar/{c.IdCampanha}",
                    DataReferencia = c.DataCriacao,
                    OrdemId = c.IdCampanha
                });
            }

            foreach (var p in personagens.OrderByDescending(x => x.IdPersonagem).Take(3))
            {
                eventos.Add(new AtividadeRecenteItem
                {
                    Iniciais = ObterIniciais(p.Nome),
                    Titulo = p.Nome,
                    Descricao = $"Personagem · {p.RacaTexto ?? "Aventureiro"} {p.ClasseTexto ?? "Geral"} (Nv. {p.Nivel})",
                    Meta = "Ficha",
                    Rota = "/personagem-lib",
                    DataReferencia = DateTime.MinValue,
                    OrdemId = p.IdPersonagem
                });
            }

            foreach (var npc in npcs.OrderByDescending(x => x.IdNpc).Take(3))
            {
                eventos.Add(new AtividadeRecenteItem
                {
                    Iniciais = ObterIniciais(npc.Nome),
                    Titulo = npc.Nome,
                    Descricao = $"NPC · {npc.ClasseTexto ?? npc.Ocupacao ?? "Personagem do Mestre"}",
                    Meta = "NPC",
                    Rota = "/BibliotecaGeral?categoria=npcs",
                    DataReferencia = DateTime.MinValue,
                    OrdemId = npc.IdNpc
                });
            }

            foreach (var item in itens.OrderByDescending(x => x.IdItem).Take(3))
            {
                eventos.Add(new AtividadeRecenteItem
                {
                    Iniciais = ObterIniciais(item.Nome),
                    Titulo = item.Nome,
                    Descricao = $"Item · {item.Tipo ?? "Equipamento"}",
                    Meta = "Item",
                    Rota = "/BibliotecaGeral?categoria=itens",
                    DataReferencia = DateTime.MinValue,
                    OrdemId = item.IdItem
                });
            }

            return eventos
                .OrderByDescending(e => e.DataReferencia)
                .ThenByDescending(e => e.OrdemId)
                .Take(3)
                .ToList();
        }

        private static string ObterIniciais(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "✦";
            var partes = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return partes.Length == 1
                ? partes[0][..Math.Min(2, partes[0].Length)].ToUpperInvariant()
                : $"{char.ToUpperInvariant(partes[0][0])}{char.ToUpperInvariant(partes[1][0])}";
        }
    }
}
