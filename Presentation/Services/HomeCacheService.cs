using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Presentation.ViewModel;

namespace RPGdeMesa.Presentation.Services
{
    public sealed class HomeCampanhaResumo
    {
        public int IdCampanha { get; set; }
        public int IdAccount { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Descricao { get; set; }
        public DateTime DataCriacao { get; set; }
        public string? Capa { get; set; }
    }

    public sealed class HomeDiscoSnapshot
    {
        public int IdUsuario { get; set; }
        public DateTime AtualizadoEmUtc { get; set; } = DateTime.UtcNow;
        public int TotalCampanhasMestre { get; set; }
        public int TotalCampanhasJogador { get; set; }
        public HomeCampanhaResumo? CampanhaDestaque { get; set; }
        public int TotalPersonagensDestaque { get; set; }
        public int TotalElementosMesaDestaque { get; set; }
        public List<HomeCampanhaResumo> CampanhasResumo { get; set; } = new();
        public List<AtividadeRecenteItem> AtividadesRecentes { get; set; } = new();
    }

    public class HomeCacheSnapshot
    {
        public int IdUsuario { get; init; }
        public DateTime AtualizadoEmUtc { get; init; } = DateTime.UtcNow;
        public List<Campanha> Campanhas { get; init; } = new();
        public List<Campanha> CampanhasComoMestre { get; init; } = new();
        public List<Campanha> CampanhasComoJogador { get; init; } = new();
        public List<Personagem> PersonagensUsuario { get; init; } = new();
        public List<NPC> NpcsUsuario { get; init; } = new();
        public List<Item> ItensUsuario { get; init; } = new();
        public Campanha? CampanhaEmDestaque { get; init; }
        public List<AtividadeRecenteItem> AtividadesRecentes { get; init; } = new();
    }

    public class HomeCacheService
    {
        private const int LimiteBase64Disco = 350_000;
        private readonly object _lock = new();
        private HomeCacheSnapshot? _snapshot;

        public event Action? OnCacheAlterado;
        private void NotificarAlteracao() => OnCacheAlterado?.Invoke();

        public HomeCacheSnapshot? Obter(int idUsuario)
        {
            lock (_lock)
            {
                if (_snapshot is null || _snapshot.IdUsuario != idUsuario)
                    return null;

                return _snapshot;
            }
        }

        public bool EstaExpirado(int idUsuario, TimeSpan tempoValidade)
        {
            lock (_lock)
            {
                if (_snapshot is null || _snapshot.IdUsuario != idUsuario)
                    return true;

                return (DateTime.UtcNow - _snapshot.AtualizadoEmUtc) > tempoValidade;
            }
        }

        public void Salvar(HomeCacheSnapshot snapshot)
        {
            lock (_lock)
            {
                _snapshot = snapshot;
            }

            SalvarNoDiscoEmSegundoPlano(CriarDiscoSnapshot(snapshot));
            NotificarAlteracao();
        }

        public HomeDiscoSnapshot? ObterDoDisco(int idUsuario)
        {
            if (idUsuario <= 0) return null;

            try
            {
                var caminho = ObterCaminhoArquivoDisco(idUsuario);
                if (!File.Exists(caminho)) return null;

                var json = File.ReadAllText(caminho);
                var dto = JsonSerializer.Deserialize<HomeDiscoSnapshot>(json);
                return dto is not null && dto.IdUsuario == idUsuario ? dto : null;
            }
            catch
            {
                return null;
            }
        }

        public void Limpar()
        {
            lock (_lock)
            {
                _snapshot = null;
            }
        }

        public void LimparComDisco(int idUsuario)
        {
            lock (_lock)
            {
                _snapshot = null;
            }

            try
            {
                var caminho = ObterCaminhoArquivoDisco(idUsuario);
                if (File.Exists(caminho))
                    File.Delete(caminho);
            }
            catch
            {
            }
        }

        // --- WRITE-THROUGH (ATUALIZAÇÃO INSTANTÂNEA NA RAM E NO DISCO) ---

        public void UpsertCampanha(int idUsuario, Campanha campanha, bool comoMestre)
        {
            if (idUsuario <= 0 || campanha is null) return;

            HomeCacheSnapshot? atualizado = null;
            lock (_lock)
            {
                if (_snapshot is null || _snapshot.IdUsuario != idUsuario) return;

                var mestre = comoMestre
                    ? _snapshot.CampanhasComoMestre
                        .Where(c => c.IdCampanha != campanha.IdCampanha)
                        .Append(campanha)
                        .OrderByDescending(c => c.DataCriacao)
                        .ThenByDescending(c => c.IdCampanha)
                        .ToList()
                    : _snapshot.CampanhasComoMestre;

                var jogador = !comoMestre
                    ? _snapshot.CampanhasComoJogador
                        .Where(c => c.IdCampanha != campanha.IdCampanha)
                        .Append(campanha)
                        .OrderByDescending(c => c.DataCriacao)
                        .ThenByDescending(c => c.IdCampanha)
                        .ToList()
                    : _snapshot.CampanhasComoJogador;

                var todas = mestre.Concat(jogador)
                    .DistinctBy(c => c.IdCampanha)
                    .OrderByDescending(c => c.DataCriacao)
                    .ThenByDescending(c => c.IdCampanha)
                    .ToList();

                var destaque = todas.FirstOrDefault();
                if (destaque is not null && _snapshot.CampanhaEmDestaque?.IdCampanha == destaque.IdCampanha && destaque.IdCampanha != campanha.IdCampanha)
                    destaque = _snapshot.CampanhaEmDestaque;

                atualizado = ReconstruirSnapshot(
                    idUsuario,
                    todas,
                    mestre,
                    jogador,
                    _snapshot.PersonagensUsuario,
                    _snapshot.NpcsUsuario,
                    _snapshot.ItensUsuario,
                    destaque);

                _snapshot = atualizado;
            }

            if (atualizado is not null)
            {
                SalvarNoDiscoEmSegundoPlano(CriarDiscoSnapshot(atualizado));
                NotificarAlteracao();
            }
        }

        public void RemoverCampanha(int idUsuario, int idCampanha)
        {
            if (idUsuario <= 0 || idCampanha <= 0) return;

            HomeCacheSnapshot? atualizado = null;
            lock (_lock)
            {
                if (_snapshot is null || _snapshot.IdUsuario != idUsuario) return;

                var mestre = _snapshot.CampanhasComoMestre.Where(c => c.IdCampanha != idCampanha).ToList();
                var jogador = _snapshot.CampanhasComoJogador.Where(c => c.IdCampanha != idCampanha).ToList();
                var todas = mestre.Concat(jogador)
                    .DistinctBy(c => c.IdCampanha)
                    .OrderByDescending(c => c.DataCriacao)
                    .ThenByDescending(c => c.IdCampanha)
                    .ToList();

                var destaque = _snapshot.CampanhaEmDestaque?.IdCampanha == idCampanha
                    ? todas.FirstOrDefault()
                    : _snapshot.CampanhaEmDestaque;

                atualizado = ReconstruirSnapshot(
                    idUsuario,
                    todas,
                    mestre,
                    jogador,
                    _snapshot.PersonagensUsuario,
                    _snapshot.NpcsUsuario,
                    _snapshot.ItensUsuario,
                    destaque);

                _snapshot = atualizado;
            }

            if (atualizado is not null)
            {
                SalvarNoDiscoEmSegundoPlano(CriarDiscoSnapshot(atualizado));
                NotificarAlteracao();
            }
        }

        public void UpsertPersonagem(int idUsuario, Personagem personagem)
        {
            if (idUsuario <= 0 || personagem is null) return;

            HomeCacheSnapshot? atualizado = null;
            lock (_lock)
            {
                if (_snapshot is null || _snapshot.IdUsuario != idUsuario) return;

                var personagens = _snapshot.PersonagensUsuario
                    .Where(p => p.IdPersonagem != personagem.IdPersonagem)
                    .Append(personagem)
                    .ToList();

                atualizado = ReconstruirSnapshot(
                    idUsuario,
                    _snapshot.Campanhas,
                    _snapshot.CampanhasComoMestre,
                    _snapshot.CampanhasComoJogador,
                    personagens,
                    _snapshot.NpcsUsuario,
                    _snapshot.ItensUsuario,
                    _snapshot.CampanhaEmDestaque);

                _snapshot = atualizado;
            }

            if (atualizado is not null)
            {
                SalvarNoDiscoEmSegundoPlano(CriarDiscoSnapshot(atualizado));
                NotificarAlteracao();
            }
        }

        public void RemoverPersonagem(int idUsuario, int idPersonagem)
        {
            if (idUsuario <= 0 || idPersonagem <= 0) return;

            HomeCacheSnapshot? atualizado = null;
            lock (_lock)
            {
                if (_snapshot is null || _snapshot.IdUsuario != idUsuario) return;

                var personagens = _snapshot.PersonagensUsuario
                    .Where(p => p.IdPersonagem != idPersonagem)
                    .ToList();

                atualizado = ReconstruirSnapshot(
                    idUsuario,
                    _snapshot.Campanhas,
                    _snapshot.CampanhasComoMestre,
                    _snapshot.CampanhasComoJogador,
                    personagens,
                    _snapshot.NpcsUsuario,
                    _snapshot.ItensUsuario,
                    _snapshot.CampanhaEmDestaque);

                _snapshot = atualizado;
            }

            if (atualizado is not null)
            {
                SalvarNoDiscoEmSegundoPlano(CriarDiscoSnapshot(atualizado));
                NotificarAlteracao();
            }
        }

        public void UpsertNpc(int idUsuario, NPC npc)
        {
            if (idUsuario <= 0 || npc is null) return;

            HomeCacheSnapshot? atualizado = null;
            lock (_lock)
            {
                if (_snapshot is null || _snapshot.IdUsuario != idUsuario) return;

                var npcs = _snapshot.NpcsUsuario
                    .Where(n => n.IdNpc != npc.IdNpc)
                    .Append(npc)
                    .ToList();

                atualizado = ReconstruirSnapshot(
                    idUsuario,
                    _snapshot.Campanhas,
                    _snapshot.CampanhasComoMestre,
                    _snapshot.CampanhasComoJogador,
                    _snapshot.PersonagensUsuario,
                    npcs,
                    _snapshot.ItensUsuario,
                    _snapshot.CampanhaEmDestaque);

                _snapshot = atualizado;
            }

            if (atualizado is not null)
            {
                SalvarNoDiscoEmSegundoPlano(CriarDiscoSnapshot(atualizado));
                NotificarAlteracao();
            }
        }

        public void RemoverNpc(int idUsuario, int idNpc)
        {
            if (idUsuario <= 0 || idNpc <= 0) return;

            HomeCacheSnapshot? atualizado = null;
            lock (_lock)
            {
                if (_snapshot is null || _snapshot.IdUsuario != idUsuario) return;

                var npcs = _snapshot.NpcsUsuario
                    .Where(n => n.IdNpc != idNpc)
                    .ToList();

                atualizado = ReconstruirSnapshot(
                    idUsuario,
                    _snapshot.Campanhas,
                    _snapshot.CampanhasComoMestre,
                    _snapshot.CampanhasComoJogador,
                    _snapshot.PersonagensUsuario,
                    npcs,
                    _snapshot.ItensUsuario,
                    _snapshot.CampanhaEmDestaque);

                _snapshot = atualizado;
            }

            if (atualizado is not null)
            {
                SalvarNoDiscoEmSegundoPlano(CriarDiscoSnapshot(atualizado));
                NotificarAlteracao();
            }
        }

        public void UpsertItem(int idUsuario, Item item)
        {
            if (idUsuario <= 0 || item is null) return;

            HomeCacheSnapshot? atualizado = null;
            lock (_lock)
            {
                if (_snapshot is null || _snapshot.IdUsuario != idUsuario) return;

                var itens = _snapshot.ItensUsuario
                    .Where(i => i.IdItem != item.IdItem)
                    .Append(item)
                    .ToList();

                atualizado = ReconstruirSnapshot(
                    idUsuario,
                    _snapshot.Campanhas,
                    _snapshot.CampanhasComoMestre,
                    _snapshot.CampanhasComoJogador,
                    _snapshot.PersonagensUsuario,
                    _snapshot.NpcsUsuario,
                    itens,
                    _snapshot.CampanhaEmDestaque);

                _snapshot = atualizado;
            }

            if (atualizado is not null)
            {
                SalvarNoDiscoEmSegundoPlano(CriarDiscoSnapshot(atualizado));
                NotificarAlteracao();
            }
        }

        public void RemoverItem(int idUsuario, int idItem)
        {
            if (idUsuario <= 0 || idItem <= 0) return;

            HomeCacheSnapshot? atualizado = null;
            lock (_lock)
            {
                if (_snapshot is null || _snapshot.IdUsuario != idUsuario) return;

                var itens = _snapshot.ItensUsuario
                    .Where(i => i.IdItem != idItem)
                    .ToList();

                atualizado = ReconstruirSnapshot(
                    idUsuario,
                    _snapshot.Campanhas,
                    _snapshot.CampanhasComoMestre,
                    _snapshot.CampanhasComoJogador,
                    _snapshot.PersonagensUsuario,
                    _snapshot.NpcsUsuario,
                    itens,
                    _snapshot.CampanhaEmDestaque);

                _snapshot = atualizado;
            }

            if (atualizado is not null)
            {
                SalvarNoDiscoEmSegundoPlano(CriarDiscoSnapshot(atualizado));
                NotificarAlteracao();
            }
        }

        private static HomeCacheSnapshot ReconstruirSnapshot(
            int idUsuario,
            List<Campanha> campanhas,
            List<Campanha> mestre,
            List<Campanha> jogador,
            List<Personagem> personagens,
            List<NPC> npcs,
            List<Item> itens,
            Campanha? destaque)
        {
            return new HomeCacheSnapshot
            {
                IdUsuario = idUsuario,
                AtualizadoEmUtc = DateTime.UtcNow,
                Campanhas = campanhas,
                CampanhasComoMestre = mestre,
                CampanhasComoJogador = jogador,
                PersonagensUsuario = personagens,
                NpcsUsuario = npcs,
                ItensUsuario = itens,
                CampanhaEmDestaque = destaque,
                AtividadesRecentes = HomeViewModel.GerarAtividadesRecentes(idUsuario, campanhas, personagens, npcs, itens)
            };
        }

        private static HomeDiscoSnapshot CriarDiscoSnapshot(HomeCacheSnapshot s)
        {
            return new HomeDiscoSnapshot
            {
                IdUsuario = s.IdUsuario,
                AtualizadoEmUtc = s.AtualizadoEmUtc,
                TotalCampanhasMestre = s.CampanhasComoMestre.Count,
                TotalCampanhasJogador = s.CampanhasComoJogador.Count,
                CampanhaDestaque = s.CampanhaEmDestaque is null ? null : ParaResumo(s.CampanhaEmDestaque),
                TotalPersonagensDestaque = s.CampanhaEmDestaque?.Personagens.Count() ?? 0,
                TotalElementosMesaDestaque =
                    (s.CampanhaEmDestaque?.MonstroCampanhas.Count ?? 0) +
                    (s.CampanhaEmDestaque?.NpcCampanhas.Count ?? 0),
                CampanhasResumo = s.Campanhas.Take(3).Select(ParaResumo).ToList(),
                AtividadesRecentes = s.AtividadesRecentes
            };
        }

        private static HomeCampanhaResumo ParaResumo(Campanha c) => new()
        {
            IdCampanha = c.IdCampanha,
            IdAccount = c.IdAccount,
            Nome = c.Nome,
            Descricao = c.Descricao,
            DataCriacao = c.DataCriacao,
            Capa = !string.IsNullOrWhiteSpace(c.Capa) && c.Capa.Length <= LimiteBase64Disco ? c.Capa : null
        };

        private static string ObterCaminhoArquivoDisco(int idUsuario) =>
            Path.Combine(FileSystem.CacheDirectory, $"home_cache_u{idUsuario}.json");

        private static void SalvarNoDiscoEmSegundoPlano(HomeDiscoSnapshot dto)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    var caminho = ObterCaminhoArquivoDisco(dto.IdUsuario);
                    var json = JsonSerializer.Serialize(dto);
                    File.WriteAllText(caminho, json);
                }
                catch
                {
                }
            });
        }
    }
}