using System;
using System.Collections.Generic;
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
    }
}