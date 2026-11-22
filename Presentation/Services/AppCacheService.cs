using System;
using System.Collections.Generic;
using System.Linq;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Magias;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Personagens;

namespace RPGdeMesa.Presentation.Services;

public sealed class CampanhasCacheSnapshot
{
    public int IdUsuario { get; init; }
    public DateTime AtualizadoEmUtc { get; init; } = DateTime.UtcNow;
    public List<Campanha> CampanhasMestre { get; init; } = new();
    public List<Campanha> CampanhasJogador { get; init; } = new();
}

public sealed class PersonagensLibCacheSnapshot
{
    public int IdUsuario { get; init; }
    public DateTime AtualizadoEmUtc { get; init; } = DateTime.UtcNow;
    public List<Personagem> Personagens { get; init; } = new();
    public List<NPC> Npcs { get; init; } = new();
}

public sealed class BibliotecaCacheSnapshot
{
    public int IdUsuario { get; init; }
    public DateTime AtualizadoEmUtc { get; init; } = DateTime.UtcNow;
    public List<Item> Itens { get; init; } = new();
    public List<Magia> Magias { get; init; } = new();
    public List<Monstro> Monstros { get; init; } = new();
    public List<NPC> Npcs { get; init; } = new();
}

public sealed class PerfilCacheSnapshot
{
    public int IdUsuario { get; init; }
    public DateTime AtualizadoEmUtc { get; init; } = DateTime.UtcNow;
    public string NomeUsuario { get; init; } = string.Empty;
    public string NomeExibicao { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string StatusConta { get; init; } = "ativo";
    public DateTime? DataCadastro { get; init; }
    public string Biografia { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public int TotalPersonagens { get; init; }
    public int TotalMonstros { get; init; }
}

public class AppCacheService
{
    private readonly object _lock = new();
    private CampanhasCacheSnapshot? _campanhas;
    private PersonagensLibCacheSnapshot? _personagensLib;
    private BibliotecaCacheSnapshot? _biblioteca;
    private PerfilCacheSnapshot? _perfil;

    public event Action? OnCacheAlterado;
    private void NotificarAlteracao() => OnCacheAlterado?.Invoke();

    // --- CAMPANHAS ---
    public CampanhasCacheSnapshot? ObterCampanhas(int idUsuario)
    {
        lock (_lock)
            return _campanhas is not null && _campanhas.IdUsuario == idUsuario ? _campanhas : null;
    }

    public bool CampanhasExpiradas(int idUsuario, TimeSpan validade)
    {
        lock (_lock)
            return _campanhas is null || _campanhas.IdUsuario != idUsuario || (DateTime.UtcNow - _campanhas.AtualizadoEmUtc) > validade;
    }

    public void SalvarCampanhas(CampanhasCacheSnapshot snapshot)
    {
        lock (_lock) _campanhas = snapshot;
    }

    public void InvalidarCampanhas()
    {
        lock (_lock) _campanhas = null;
    }

    public void UpsertCampanhaMestre(int idUsuario, Campanha campanha)
    {
        if (campanha is null || idUsuario <= 0) return;

        lock (_lock)
        {
            if (_campanhas is null || _campanhas.IdUsuario != idUsuario) return;

            var mestre = _campanhas.CampanhasMestre
                .Where(c => c.IdCampanha != campanha.IdCampanha)
                .Append(campanha)
                .OrderByDescending(c => c.DataCriacao)
                .ThenByDescending(c => c.IdCampanha)
                .ToList();

            _campanhas = new CampanhasCacheSnapshot
            {
                IdUsuario = idUsuario,
                AtualizadoEmUtc = DateTime.UtcNow,
                CampanhasMestre = mestre,
                CampanhasJogador = _campanhas.CampanhasJogador
            };
        }

        NotificarAlteracao();
    }

    public void UpsertCampanhaJogador(int idUsuario, Campanha campanha)
    {
        if (campanha is null || idUsuario <= 0) return;

        lock (_lock)
        {
            if (_campanhas is null || _campanhas.IdUsuario != idUsuario) return;

            var jogador = _campanhas.CampanhasJogador
                .Where(c => c.IdCampanha != campanha.IdCampanha)
                .Append(campanha)
                .OrderByDescending(c => c.DataCriacao)
                .ThenByDescending(c => c.IdCampanha)
                .ToList();

            _campanhas = new CampanhasCacheSnapshot
            {
                IdUsuario = idUsuario,
                AtualizadoEmUtc = DateTime.UtcNow,
                CampanhasMestre = _campanhas.CampanhasMestre,
                CampanhasJogador = jogador
            };
        }

        NotificarAlteracao();
    }

    public void RemoverCampanha(int idUsuario, int idCampanha)
    {
        if (idUsuario <= 0 || idCampanha <= 0) return;

        lock (_lock)
        {
            if (_campanhas is null || _campanhas.IdUsuario != idUsuario) return;

            _campanhas = new CampanhasCacheSnapshot
            {
                IdUsuario = idUsuario,
                AtualizadoEmUtc = DateTime.UtcNow,
                CampanhasMestre = _campanhas.CampanhasMestre.Where(c => c.IdCampanha != idCampanha).ToList(),
                CampanhasJogador = _campanhas.CampanhasJogador.Where(c => c.IdCampanha != idCampanha).ToList()
            };
        }

        NotificarAlteracao();
    }

    // --- PERSONAGENS / NPCS ---
    public PersonagensLibCacheSnapshot? ObterPersonagensLib(int idUsuario)
    {
        lock (_lock)
            return _personagensLib is not null && _personagensLib.IdUsuario == idUsuario ? _personagensLib : null;
    }

    public bool PersonagensLibExpirados(int idUsuario, TimeSpan validade)
    {
        lock (_lock)
            return _personagensLib is null || _personagensLib.IdUsuario != idUsuario || (DateTime.UtcNow - _personagensLib.AtualizadoEmUtc) > validade;
    }

    public void SalvarPersonagensLib(PersonagensLibCacheSnapshot snapshot)
    {
        lock (_lock) _personagensLib = snapshot;
    }

    public void InvalidarPersonagensLib()
    {
        lock (_lock) _personagensLib = null;
    }

    public void UpsertPersonagem(int idUsuario, Personagem personagem)
    {
        if (personagem is null || idUsuario <= 0) return;

        lock (_lock)
        {
            if (_personagensLib is not null && _personagensLib.IdUsuario == idUsuario)
            {
                var lista = _personagensLib.Personagens
                    .Where(p => p.IdPersonagem != personagem.IdPersonagem)
                    .Append(personagem)
                    .OrderBy(p => p.Nome)
                    .ToList();

                _personagensLib = new PersonagensLibCacheSnapshot
                {
                    IdUsuario = idUsuario,
                    AtualizadoEmUtc = DateTime.UtcNow,
                    Personagens = lista,
                    Npcs = _personagensLib.Npcs
                };

                AtualizarContadorPerfilLocked(idUsuario, totalPersonagens: lista.Count);
            }
        }

        NotificarAlteracao();
    }

    public void RemoverPersonagem(int idUsuario, int idPersonagem)
    {
        if (idUsuario <= 0 || idPersonagem <= 0) return;

        lock (_lock)
        {
            if (_personagensLib is not null && _personagensLib.IdUsuario == idUsuario)
            {
                var lista = _personagensLib.Personagens
                    .Where(p => p.IdPersonagem != idPersonagem)
                    .ToList();

                _personagensLib = new PersonagensLibCacheSnapshot
                {
                    IdUsuario = idUsuario,
                    AtualizadoEmUtc = DateTime.UtcNow,
                    Personagens = lista,
                    Npcs = _personagensLib.Npcs
                };

                AtualizarContadorPerfilLocked(idUsuario, totalPersonagens: lista.Count);
            }
        }

        NotificarAlteracao();
    }

    // --- BIBLIOTECA GERAL ---
    public BibliotecaCacheSnapshot? ObterBiblioteca(int idUsuario)
    {
        lock (_lock)
            return _biblioteca is not null && _biblioteca.IdUsuario == idUsuario ? _biblioteca : null;
    }

    public bool BibliotecaExpirada(int idUsuario, TimeSpan validade)
    {
        lock (_lock)
            return _biblioteca is null || _biblioteca.IdUsuario != idUsuario || (DateTime.UtcNow - _biblioteca.AtualizadoEmUtc) > validade;
    }

    public void SalvarBiblioteca(BibliotecaCacheSnapshot snapshot)
    {
        lock (_lock) _biblioteca = snapshot;
    }

    public void InvalidarBiblioteca()
    {
        lock (_lock) _biblioteca = null;
    }

    public void UpsertNpc(int idUsuario, NPC npc)
    {
        if (npc is null || idUsuario <= 0) return;

        lock (_lock)
        {
            if (_biblioteca is not null && _biblioteca.IdUsuario == idUsuario)
            {
                var npcs = _biblioteca.Npcs
                    .Where(n => n.IdNpc != npc.IdNpc)
                    .Append(npc)
                    .OrderBy(n => n.Nome)
                    .ToList();

                _biblioteca = new BibliotecaCacheSnapshot
                {
                    IdUsuario = idUsuario,
                    AtualizadoEmUtc = DateTime.UtcNow,
                    Itens = _biblioteca.Itens,
                    Magias = _biblioteca.Magias,
                    Monstros = _biblioteca.Monstros,
                    Npcs = npcs
                };
            }

            if (_personagensLib is not null && _personagensLib.IdUsuario == idUsuario)
            {
                var npcs = _personagensLib.Npcs
                    .Where(n => n.IdNpc != npc.IdNpc)
                    .Append(npc)
                    .OrderBy(n => n.Nome)
                    .ToList();

                _personagensLib = new PersonagensLibCacheSnapshot
                {
                    IdUsuario = idUsuario,
                    AtualizadoEmUtc = DateTime.UtcNow,
                    Personagens = _personagensLib.Personagens,
                    Npcs = npcs
                };
            }
        }

        NotificarAlteracao();
    }

    public void RemoverNpc(int idUsuario, int idNpc)
    {
        if (idUsuario <= 0 || idNpc <= 0) return;

        lock (_lock)
        {
            if (_biblioteca is not null && _biblioteca.IdUsuario == idUsuario)
            {
                _biblioteca = new BibliotecaCacheSnapshot
                {
                    IdUsuario = idUsuario,
                    AtualizadoEmUtc = DateTime.UtcNow,
                    Itens = _biblioteca.Itens,
                    Magias = _biblioteca.Magias,
                    Monstros = _biblioteca.Monstros,
                    Npcs = _biblioteca.Npcs.Where(n => n.IdNpc != idNpc).ToList()
                };
            }

            if (_personagensLib is not null && _personagensLib.IdUsuario == idUsuario)
            {
                _personagensLib = new PersonagensLibCacheSnapshot
                {
                    IdUsuario = idUsuario,
                    AtualizadoEmUtc = DateTime.UtcNow,
                    Personagens = _personagensLib.Personagens,
                    Npcs = _personagensLib.Npcs.Where(n => n.IdNpc != idNpc).ToList()
                };
            }
        }

        NotificarAlteracao();
    }

    public void UpsertMonstro(int idUsuario, Monstro monstro)
    {
        if (monstro is null || idUsuario <= 0) return;

        lock (_lock)
        {
            if (_biblioteca is not null && _biblioteca.IdUsuario == idUsuario)
            {
                var monstros = _biblioteca.Monstros
                    .Where(m => m.IdMonstro != monstro.IdMonstro)
                    .Append(monstro)
                    .OrderBy(m => m.Nome)
                    .ToList();

                _biblioteca = new BibliotecaCacheSnapshot
                {
                    IdUsuario = idUsuario,
                    AtualizadoEmUtc = DateTime.UtcNow,
                    Itens = _biblioteca.Itens,
                    Magias = _biblioteca.Magias,
                    Monstros = monstros,
                    Npcs = _biblioteca.Npcs
                };

                AtualizarContadorPerfilLocked(idUsuario, totalMonstros: monstros.Count(m => m.IdAccount == idUsuario));
            }
        }

        NotificarAlteracao();
    }

    public void RemoverMonstro(int idUsuario, int idMonstro)
    {
        if (idUsuario <= 0 || idMonstro <= 0) return;

        lock (_lock)
        {
            if (_biblioteca is not null && _biblioteca.IdUsuario == idUsuario)
            {
                var monstros = _biblioteca.Monstros
                    .Where(m => m.IdMonstro != idMonstro)
                    .ToList();

                _biblioteca = new BibliotecaCacheSnapshot
                {
                    IdUsuario = idUsuario,
                    AtualizadoEmUtc = DateTime.UtcNow,
                    Itens = _biblioteca.Itens,
                    Magias = _biblioteca.Magias,
                    Monstros = monstros,
                    Npcs = _biblioteca.Npcs
                };

                AtualizarContadorPerfilLocked(idUsuario, totalMonstros: monstros.Count(m => m.IdAccount == idUsuario));
            }
        }

        NotificarAlteracao();
    }

    public void UpsertItem(int idUsuario, Item item)
    {
        if (item is null || idUsuario <= 0) return;

        lock (_lock)
        {
            if (_biblioteca is not null && _biblioteca.IdUsuario == idUsuario)
            {
                var itens = _biblioteca.Itens
                    .Where(i => i.IdItem != item.IdItem)
                    .Append(item)
                    .OrderBy(i => i.Nome)
                    .ToList();

                _biblioteca = new BibliotecaCacheSnapshot
                {
                    IdUsuario = idUsuario,
                    AtualizadoEmUtc = DateTime.UtcNow,
                    Itens = itens,
                    Magias = _biblioteca.Magias,
                    Monstros = _biblioteca.Monstros,
                    Npcs = _biblioteca.Npcs
                };
            }
        }

        NotificarAlteracao();
    }

    public void RemoverItem(int idUsuario, int idItem)
    {
        if (idUsuario <= 0 || idItem <= 0) return;

        lock (_lock)
        {
            if (_biblioteca is not null && _biblioteca.IdUsuario == idUsuario)
            {
                _biblioteca = new BibliotecaCacheSnapshot
                {
                    IdUsuario = idUsuario,
                    AtualizadoEmUtc = DateTime.UtcNow,
                    Itens = _biblioteca.Itens.Where(i => i.IdItem != idItem).ToList(),
                    Magias = _biblioteca.Magias,
                    Monstros = _biblioteca.Monstros,
                    Npcs = _biblioteca.Npcs
                };
            }
        }

        NotificarAlteracao();
    }

    // --- PERFIL ---
    public PerfilCacheSnapshot? ObterPerfil(int idUsuario)
    {
        lock (_lock)
            return _perfil is not null && _perfil.IdUsuario == idUsuario ? _perfil : null;
    }

    public bool PerfilExpirado(int idUsuario, TimeSpan validade)
    {
        lock (_lock)
            return _perfil is null || _perfil.IdUsuario != idUsuario || (DateTime.UtcNow - _perfil.AtualizadoEmUtc) > validade;
    }

    public void SalvarPerfil(PerfilCacheSnapshot snapshot)
    {
        lock (_lock) _perfil = snapshot;
    }

    public void InvalidarPerfil()
    {
        lock (_lock) _perfil = null;
    }

    public void AtualizarPerfilBasico(int idUsuario, string nomeExibicao, string biografia, string? avatarUrl)
    {
        if (idUsuario <= 0) return;

        lock (_lock)
        {
            if (_perfil is null || _perfil.IdUsuario != idUsuario) return;

            _perfil = new PerfilCacheSnapshot
            {
                IdUsuario = idUsuario,
                AtualizadoEmUtc = DateTime.UtcNow,
                NomeUsuario = _perfil.NomeUsuario,
                NomeExibicao = nomeExibicao,
                Email = _perfil.Email,
                StatusConta = _perfil.StatusConta,
                DataCadastro = _perfil.DataCadastro,
                Biografia = biografia,
                AvatarUrl = avatarUrl,
                TotalPersonagens = _perfil.TotalPersonagens,
                TotalMonstros = _perfil.TotalMonstros
            };
        }

        NotificarAlteracao();
    }

    private void AtualizarContadorPerfilLocked(int idUsuario, int? totalPersonagens = null, int? totalMonstros = null)
    {
        if (_perfil is null || _perfil.IdUsuario != idUsuario) return;

        _perfil = new PerfilCacheSnapshot
        {
            IdUsuario = idUsuario,
            AtualizadoEmUtc = DateTime.UtcNow,
            NomeUsuario = _perfil.NomeUsuario,
            NomeExibicao = _perfil.NomeExibicao,
            Email = _perfil.Email,
            StatusConta = _perfil.StatusConta,
            DataCadastro = _perfil.DataCadastro,
            Biografia = _perfil.Biografia,
            AvatarUrl = _perfil.AvatarUrl,
            TotalPersonagens = totalPersonagens ?? _perfil.TotalPersonagens,
            TotalMonstros = totalMonstros ?? _perfil.TotalMonstros
        };
    }

    // --- LIMPEZA GERAL (LOGOUT / TROCA DE CONTA) ---
    public void LimparTudo()
    {
        lock (_lock)
        {
            _campanhas = null;
            _personagensLib = null;
            _biblioteca = null;
            _perfil = null;
        }

        NotificarAlteracao();
    }
}