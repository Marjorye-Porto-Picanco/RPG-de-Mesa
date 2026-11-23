using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Magias;
using RPGdeMesa.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RPGdeMesa.Infrastructure.Repositories;

public class MagiaRepository : IMagiaRepository
{
    private readonly AppDbContext _contexto;

    public MagiaRepository(AppDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<List<Magia>> ListarTodasAsync()
    {
        var todas = await _contexto.Magias
            .AsNoTracking()
            .OrderBy(m => m.CirculoNivel)
            .ThenBy(m => m.Nome)
            .ThenBy(m => m.IdMagia)
            .ToListAsync();

        return todas
            .GroupBy(m => m.Nome.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(m => m.CirculoNivel)
            .ThenBy(m => m.Nome)
            .ToList();
    }
}