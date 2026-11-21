using System;
using System.Collections.Generic;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Presentation.ViewModel;

namespace RPGdeMesa.Presentation.Services
{
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
        private readonly object _lock = new();
        private HomeCacheSnapshot? _snapshot;

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
        }

        public void Limpar()
        {
            lock (_lock)
            {
                _snapshot = null;
            }
        }
    }
}