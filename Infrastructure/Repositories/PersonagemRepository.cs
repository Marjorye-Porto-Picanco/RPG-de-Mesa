using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Infrastructure.Persistence;

namespace RPGdeMesa.Infrastructure.Repositories;

public class PersonagemRepository : IPersonagemRepository
{
    private readonly AppDbContext _contexto;

    public PersonagemRepository(AppDbContext contexto) => _contexto = contexto;

    public async Task<Personagem?> ObterPorIdAsync(int idPersonagem)
    {
        _contexto.ChangeTracker.Clear();
        return await _contexto.Personagens.FirstOrDefaultAsync(p => p.IdPersonagem == idPersonagem);
    }

    public async Task<List<Personagem>> ListarPorUsuarioAsync(int idAccount)
    {
        _contexto.ChangeTracker.Clear();
        return await _contexto.Personagens.Where(p => p.IdAccount == idAccount).ToListAsync();
    }

    public async Task AdicionarAsync(Personagem personagem)
    {
        _contexto.Personagens.Add(personagem);
        await _contexto.SaveChangesAsync();
    }

    public async Task AtualizarAsync(Personagem personagem)
    {
        _contexto.Personagens.Update(personagem);
        await _contexto.SaveChangesAsync();
    }

    public async Task RemoverAsync(Personagem personagem)
    {
        _contexto.Personagens.Remove(personagem);
        await _contexto.SaveChangesAsync();
    }
}