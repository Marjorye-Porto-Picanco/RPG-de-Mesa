using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Usuarios;
using RPGdeMesa.Infrastructure.Persistence;
using RPGdeMesa.Domain.Npcs;

namespace RPGdeMesa.Infrastructure.Repositories;

public class CampanhaRepository : ICampanhaRepository
{
    private readonly AppDbContext _contexto;

    public CampanhaRepository(AppDbContext contexto) => _contexto = contexto;

    public async Task<Campanha?> ObterPorIdComDetalhesAsync(int idCampanha)
    {
        _contexto.ChangeTracker.Clear();
        return await _contexto.Campanhas
            .Include(c => c.UsuarioMestre)
            .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem)
            .Include(c => c.MonstroCampanhas).ThenInclude(mc => mc.Monstro)
            .Include(c => c.Npcs) 
            .FirstOrDefaultAsync(c => c.IdCampanha == idCampanha);
    }

    public async Task<Campanha?> ObterPorCodigoConviteAsync(string codigoConvite) =>
        await _contexto.Campanhas
            .Include(c => c.UsuarioMestre)
            .FirstOrDefaultAsync(c => c.CodigoConvite != null && c.CodigoConvite.ToUpper() == codigoConvite.Trim().ToUpper());

    public async Task<List<Campanha>> ListarPorMestreAsync(int idAccount)
    {
        _contexto.ChangeTracker.Clear();
        return await _contexto.Campanhas
            .Where(c => c.IdAccount == idAccount)
            .OrderByDescending(c => c.DataCriacao)
            .ToListAsync();
    }

    public async Task<List<Campanha>> ListarPorJogadorAsync(int idAccount)
    {
        _contexto.ChangeTracker.Clear();

        var idsCampanhasUsuario = await _contexto.UsuariosCampanha
            .Where(uc => uc.IdAccount == idAccount)
            .Select(uc => uc.IdCampanha)
            .ToListAsync();

        var porUsuario = await _contexto.Campanhas
            .Include(c => c.UsuarioMestre)
            .Where(c => idsCampanhasUsuario.Contains(c.IdCampanha))
            .ToListAsync();

        var porPersonagem = await _contexto.PersonagensCampanha
            .Where(pc => pc.Personagem != null && pc.Personagem.IdAccount == idAccount)
            .Select(pc => pc.Campanha)
            .ToListAsync();

        return porUsuario.Union(porPersonagem).Distinct().ToList();
    }

    public async Task<bool> CodigoConviteExisteAsync(string codigoConvite) =>
        await _contexto.Campanhas.AnyAsync(c => c.CodigoConvite == codigoConvite);

    public async Task<bool> UsuarioJaParticipaAsync(int idAccount, int idCampanha) =>
        await _contexto.UsuariosCampanha
            .AsNoTracking()
            .AnyAsync(uc => uc.IdAccount == idAccount && uc.IdCampanha == idCampanha);

    public async Task AdicionarUsuarioNaCampanhaAsync(UsuarioCampanha vinculo)
    {
        var existenteLocal = _contexto.UsuariosCampanha.Local
            .FirstOrDefault(uc => uc.IdAccount == vinculo.IdAccount && uc.IdCampanha == vinculo.IdCampanha);

        if (existenteLocal == null)
        {
            var jaExisteNoBanco = await _contexto.UsuariosCampanha
                .AsNoTracking()
                .AnyAsync(uc => uc.IdAccount == vinculo.IdAccount && uc.IdCampanha == vinculo.IdCampanha);

            if (!jaExisteNoBanco)
            {
                await _contexto.UsuariosCampanha.AddAsync(vinculo);
                await _contexto.SaveChangesAsync();
            }
        }
    }

    public async Task<List<NPC>> ListarTodosNpcsAsync() => await _contexto.Npcs.ToListAsync();
    public async Task<List<Item>> ListarTodosItensAsync() => await _contexto.Itens.ToListAsync();

    public async Task AdicionarAsync(Campanha campanha)
    {
        _contexto.Campanhas.Add(campanha);
        await _contexto.SaveChangesAsync();
    }

    public async Task AtualizarAsync(Campanha campanha)
    {
        _contexto.Campanhas.Update(campanha);
        await _contexto.SaveChangesAsync();
    }

    public async Task RemoverMonstroCampanhaAsync(int idCampanha, int idMonstro)
    {
        var vinculo = await _contexto.MonstroCampanhas
            .FirstOrDefaultAsync(mc => mc.IdCampanha == idCampanha && mc.IdMonstro == idMonstro);

        if (vinculo != null)
        {
            _contexto.MonstroCampanhas.Remove(vinculo);
            await _contexto.SaveChangesAsync();
        }
    }

    public async Task RemoverNpcAsync(int idNpc)
    {
        var npc = await _contexto.Npcs.FirstOrDefaultAsync(n => n.IdNpc == idNpc);
        if (npc != null)
        {
            _contexto.Npcs.Remove(npc);
            await _contexto.SaveChangesAsync();
        }
    }
}