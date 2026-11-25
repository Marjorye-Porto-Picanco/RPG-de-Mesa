using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Infrastructure.Persistence;

namespace RPGdeMesa.Infrastructure.Repositories;

public class MonstroRepository : IMonstroRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public MonstroRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<Monstro?> ObterPorIdAsync(int idMonstro)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Monstros
            .AsNoTracking()
            .Include(m => m.Ataques)
            .FirstOrDefaultAsync(m => m.IdMonstro == idMonstro);
    }

    public async Task<List<Monstro>> ListarPorUsuarioAsync(int idAccount)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Monstros
            .AsNoTracking()
            .Include(m => m.Ataques)
            // Lista monstros públicos e monstros pertencentes à conta logada.
            .Where(m => m.IdAccount == null || m.IdAccount == idAccount)
            .OrderByDescending(m => m.IdMonstro)
            .ToListAsync();
    }

    public async Task AdicionarAsync(Monstro monstro)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        contexto.Monstros.Add(monstro);
        await contexto.SaveChangesAsync();
    }

    public async Task AtualizarAsync(Monstro monstro)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        var existente = await contexto.Monstros.Include(m => m.Ataques)
            .FirstOrDefaultAsync(m => m.IdMonstro == monstro.IdMonstro);
        if (existente is null) return;
        existente.Nome = monstro.Nome;
        existente.TipoMonstro = monstro.TipoMonstro;
        existente.NivelDesafio = monstro.NivelDesafio;
        existente.PvMaximo = monstro.PvMaximo;
        existente.ClasseArmadura = monstro.ClasseArmadura;
        existente.Tamanho = monstro.Tamanho;
        existente.Tendencia = monstro.Tendencia;
        existente.Deslocamento = monstro.Deslocamento;
        existente.Sentidos = monstro.Sentidos;
        existente.Resistencias = monstro.Resistencias;
        existente.Habilidades = monstro.Habilidades;
        existente.Descricao = monstro.Descricao;
        existente.Imagem = monstro.Imagem;
        contexto.MonstroAtaques.RemoveRange(existente.Ataques);
        existente.Ataques = monstro.Ataques.Select(a => new MonstroAtaque
        {
            IdMonstro = existente.IdMonstro,
            Nome = a.Nome,
            BonusAtaque = a.BonusAtaque,
            Dano = a.Dano,
            TipoDano = a.TipoDano,
            Alcance = a.Alcance,
            Descricao = a.Descricao
        }).ToList();
        await contexto.SaveChangesAsync();
    }

    public async Task RemoverAsync(Monstro monstro)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        contexto.Monstros.Remove(monstro);
        await contexto.SaveChangesAsync();
    }
}
