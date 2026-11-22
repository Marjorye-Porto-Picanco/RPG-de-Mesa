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
        private const string BannerPadrao = "img/maprpg.png";
        private const string CapaPadrao = "img/capacampanha1.png";
        private static readonly TimeSpan ValidadeCache = TimeSpan.FromSeconds(45);

        private readonly SemaphoreSlim _semaforoCarga = new(1, 1);
        private readonly CampanhaService _campanhaService;
        private readonly PersonagemService _personagemService;
        private readonly NpcService _npcService;
        private readonly ItemService _itemService;
        private readonly SessaoUsuarioService _sessaoUsuario;
        private readonly HomeCacheService _cache;
        private readonly AppCacheService _appCache;
        private readonly NavigationManager _navigation;

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
            CampanhasComoJogador.Count > 0 && CampanhasComoMestre.Count == 0;

        public bool ExibirResumoMesa => CampanhaEmDestaque is not null;

        public string CapaCampanhaDestaque =>
            ObterCapaCampanha(CampanhaEmDestaque, BannerPadrao);

        public int TotalPersonagensDestaque =>
            CampanhaEmDestaque?.Personagens.Count() ?? 0;

        public int TotalElementosMesaDestaque =>
            (CampanhaEmDestaque?.MonstroCampanhas.Count ?? 0) +
            (CampanhaEmDestaque?.NpcCampanhas.Count ?? 0);

        public bool EhMestreDaCampanha(Campanha campanha) =>
            campanha.IdAccount == _sessaoUsuario.IdUsuarioLogado;

        public string ObterCapaCampanha(Campanha? campanha, string fallback = CapaPadrao)
        {
            return string.IsNullOrWhiteSpace(campanha?.Capa) ? fallback : campanha.Capa;
        }

        public string ObterRotaCampanha(Campanha? campanha)
        {
            if (campanha is null) return "/criar-campanha";

            return EhMestreDaCampanha(campanha)
                ? $"/sessao-iniciar/{campanha.IdCampanha}"
                : $"/sessao-entrar/{campanha.IdCampanha}";
        }

        public string TextoBotaoPrincipalHero
        {
            get
            {
                if (CampanhaEmDestaque is null) return "Criar campanha";
                return EhMestreDaCampanha(CampanhaEmDestaque) ? "Iniciar sessão" : "Entrar como jogador";
            }
        }

        public string RotaBotaoSecundarioHero =>
            EhApenasJogador ? "/campanhas" : "/saladojogo";

        public string TextoBotaoSecundarioHero =>
            EhApenasJogador ? "Ver campanhas" : "Entrar na sala";

        public async Task CarregarDadosAsync(bool forcarAtualizacao = false)
        {
            if (!_sessaoUsuario.EstaLogado)
            {
                _navigation.NavigateTo("/login");
                return;
            }

            int idUsuario = _sessaoUsuario.IdUsuarioLogado;

            // 1. Hidrata imediatamente do cache da Home se existir para este usuário
            var snapshot = _cache.Obter(idUsuario);
            if (snapshot is not null)
            {
                AplicarSnapshot(snapshot);
                NotifyStateChanged();

                if (!forcarAtualizacao && !_cache.EstaExpirado(idUsuario, ValidadeCache))
                    return;
            }

            // 2. Atualiza em segundo plano (ou faz a primeira carga) com proteção de concorrência
            if (!await _semaforoCarga.WaitAsync(0))
                return;

            Carregando = snapshot is null;
            if (Carregando) NotifyStateChanged();

            try
            {
                // Reaproveita snapshots válidos do AppCacheService (evita ir ao banco pelo que já foi baixado)
                var campanhasSnap = !forcarAtualizacao && !_appCache.CampanhasExpiradas(idUsuario, ValidadeCache)
                    ? _appCache.ObterCampanhas(idUsuario)
                    : null;

                var personagensSnap = !forcarAtualizacao && !_appCache.PersonagensLibExpirados(idUsuario, ValidadeCache)
                    ? _appCache.ObterPersonagensLib(idUsuario)
                    : null;

                var bibliotecaSnap = !forcarAtualizacao && !_appCache.BibliotecaExpirada(idUsuario, ValidadeCache)
                    ? _appCache.ObterBiblioteca(idUsuario)
                    : null;

                // Dispara em paralelo apenas o que não estava em cache
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

                // Alimenta o AppCacheService para que outras telas também abram rápido
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

                var primeira = Campanhas.FirstOrDefault();
                if (primeira is not null)
                {
                    CampanhaEmDestaque = await _campanhaService.ObterDetalhesCampanhaAsync(primeira.IdCampanha) ?? primeira;
                }
                else
                {
                    CampanhaEmDestaque = null;
                }

                MontarAtividadesRecentes();

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
        }

        private void MontarAtividadesRecentes()
        {
            var eventos = new List<AtividadeRecenteItem>();

            foreach (var c in Campanhas)
            {
                eventos.Add(new AtividadeRecenteItem
                {
                    Iniciais = ObterIniciais(c.Nome),
                    Titulo = c.Nome,
                    Descricao = EhMestreDaCampanha(c) ? "Campanha criada como Mestre" : "Campanha vinculada como Jogador",
                    Meta = c.DataCriacao.ToString("dd/MM"),
                    Rota = ObterRotaCampanha(c),
                    DataReferencia = c.DataCriacao,
                    OrdemId = c.IdCampanha
                });
            }

            foreach (var p in PersonagensUsuario.OrderByDescending(x => x.IdPersonagem).Take(3))
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

            foreach (var npc in NpcsUsuario.OrderByDescending(x => x.IdNpc).Take(3))
            {
                eventos.Add(new AtividadeRecenteItem
                {
                    Iniciais = ObterIniciais(npc.Nome),
                    Titulo = npc.Nome,
                    Descricao = $"NPC · {npc.ClasseTexto ?? npc.Ocupacao ?? "Personagem do Mestre"}",
                    Meta = "NPC",
                    Rota = "/biblioteca",
                    DataReferencia = DateTime.MinValue,
                    OrdemId = npc.IdNpc
                });
            }

            foreach (var item in ItensUsuario.OrderByDescending(x => x.IdItem).Take(3))
            {
                eventos.Add(new AtividadeRecenteItem
                {
                    Iniciais = ObterIniciais(item.Nome),
                    Titulo = item.Nome,
                    Descricao = $"Item · {item.Tipo ?? "Equipamento"}",
                    Meta = "Item",
                    Rota = "/biblioteca",
                    DataReferencia = DateTime.MinValue,
                    OrdemId = item.IdItem
                });
            }

            AtividadesRecentes = eventos
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