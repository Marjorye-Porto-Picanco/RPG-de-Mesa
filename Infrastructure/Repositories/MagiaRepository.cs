using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Magias;
using RPGdeMesa.Infrastructure.Persistence;
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
        return await _contexto.Magias
            .AsNoTracking()
            .OrderBy(m => m.CirculoNivel)
            .ThenBy(m => m.Nome)
            .ToListAsync();
    }
}