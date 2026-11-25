using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Magias;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Usuarios;
using RPGdeMesa.Infrastructure.Persistence;

namespace RPGdeMesa.Infrastructure.Repositories;

public class CampanhaRepository : ICampanhaRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public CampanhaRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<Campanha?> ObterPorIdComDetalhesAsync(int idCampanha)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Campanhas
            .AsNoTracking()
            .AsSplitQuery()
            .Include(c => c.UsuarioMestre).ThenInclude(u => u.Perfil)
            .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem).ThenInclude(p => p!.Usuario).ThenInclude(u => u.Perfil)
            .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem).ThenInclude(p => p!.PersonagemClasses).ThenInclude(pcl => pcl.Classe)
            .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem).ThenInclude(p => p!.Salvaguardas)
            .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem).ThenInclude(p => p!.Pericias)
            .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem).ThenInclude(p => p!.Inventario).ThenInclude(inv => inv!.Itens).ThenInclude(ii => ii.Item)
            .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem).ThenInclude(p => p!.PersonagemMagias).ThenInclude(pm => pm.Magia)
            .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem).ThenInclude(p => p!.CirculosMagicos)
            .Include(c => c.MonstroCampanhas).ThenInclude(mc => mc.Monstro).ThenInclude(m => m!.Ataques)
            .Include(c => c.NpcCampanhas).ThenInclude(nc => nc.Npc)
            .Include(c => c.NpcCampanhas).ThenInclude(nc => nc.Npc).ThenInclude(n => n!.Itens).ThenInclude(i => i.Item)
            .Include(c => c.NpcCampanhas).ThenInclude(nc => nc.Npc).ThenInclude(n => n!.Magias).ThenInclude(m => m.Magia)
            // .Include(c => c.NpcCampanhas).ThenInclude(nc => nc.Npc).ThenInclude(n => n!.Ataques)
            .Include(c => c.ItemCampanhas).ThenInclude(ic => ic.Item)
            .Include(c => c.MapaCampanhas).ThenInclude(mc => mc.Mapa)
            .FirstOrDefaultAsync(c => c.IdCampanha == idCampanha);
    }

    public async Task<Campanha?> ObterPorCodigoConviteAsync(string codigoConvite)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        string codigo = codigoConvite.Trim().ToUpper();
        return await contexto.Campanhas
            .AsNoTracking()
            .Include(c => c.UsuarioMestre).ThenInclude(u => u.Perfil)
            .FirstOrDefaultAsync(c => c.CodigoConvite != null && c.CodigoConvite.ToUpper() == codigo);
    }

    public async Task<List<Campanha>> ListarPorMestreAsync(int idAccount)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Campanhas
            .AsNoTracking()
            .Where(c => c.IdAccount == idAccount)
            .OrderByDescending(c => c.DataCriacao)
            .ToListAsync();
    }

    public async Task<List<Campanha>> ListarPorJogadorAsync(int idAccount)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var idsCampanhasUsuario = await contexto.UsuariosCampanha
            .AsNoTracking()
            .Where(uc => uc.IdAccount == idAccount)
            .Select(uc => uc.IdCampanha)
            .ToListAsync();

        var porUsuario = await contexto.Campanhas
            .AsNoTracking()
            .Include(c => c.UsuarioMestre).ThenInclude(u => u.Perfil)
            .Where(c => idsCampanhasUsuario.Contains(c.IdCampanha))
            .ToListAsync();

        var porPersonagem = await contexto.PersonagensCampanha
            .AsNoTracking()
            .Where(pc => pc.Personagem != null && pc.Personagem.IdAccount == idAccount)
            .Select(pc => pc.Campanha)
            .ToListAsync();

        return porUsuario.Union(porPersonagem).DistinctBy(c => c.IdCampanha).ToList();
    }

    public async Task<bool> CodigoConviteExisteAsync(string codigoConvite)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Campanhas.AsNoTracking().AnyAsync(c => c.CodigoConvite == codigoConvite);
    }

    public async Task<bool> UsuarioJaParticipaAsync(int idAccount, int idCampanha)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.UsuariosCampanha
            .AsNoTracking()
            .AnyAsync(uc => uc.IdAccount == idAccount && uc.IdCampanha == idCampanha);
    }

    public async Task AdicionarUsuarioNaCampanhaAsync(UsuarioCampanha vinculo)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        bool jaExisteNoBanco = await contexto.UsuariosCampanha
            .AsNoTracking()
            .AnyAsync(uc => uc.IdAccount == vinculo.IdAccount && uc.IdCampanha == vinculo.IdCampanha);

        if (!jaExisteNoBanco)
        {
            await contexto.UsuariosCampanha.AddAsync(vinculo);
            await contexto.SaveChangesAsync();
        }
    }

    public async Task<List<NPC>> ListarTodosNpcsAsync()
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Npcs.AsNoTracking().ToListAsync();
    }

    public async Task<List<Item>> ListarTodosItensAsync()
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Itens.AsNoTracking().ToListAsync();
    }

    public async Task<List<Monstro>> ListarTodosMonstrosAsync()
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Monstros.AsNoTracking().Include(m => m.Ataques).ToListAsync();
    }

    public async Task<List<Mapa>> ListarTodosMapasAsync()
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Mapas.AsNoTracking().ToListAsync();
    }

    public async Task<List<Magia>> ListarTodasMagiasAsync()
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Magias.AsNoTracking().OrderBy(m => m.CirculoNivel).ThenBy(m => m.Nome).ToListAsync();
    }

    public async Task AdicionarAsync(Campanha campanha)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        contexto.Campanhas.Add(campanha);
        await contexto.SaveChangesAsync();
    }

    public async Task AtualizarAsync(Campanha campanha)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        contexto.Campanhas.Update(campanha);
        await contexto.SaveChangesAsync();
    }

    public async Task RemoverCampanhaAsync(int idCampanha)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        var campanha = await contexto.Campanhas.FirstOrDefaultAsync(c => c.IdCampanha == idCampanha);
        if (campanha is null) return;

        var usuarios = await contexto.UsuariosCampanha.Where(uc => uc.IdCampanha == idCampanha).ToListAsync();
        var personagens = await contexto.PersonagensCampanha.Where(pc => pc.IdCampanha == idCampanha).ToListAsync();
        var monstros = await contexto.MonstroCampanhas.Where(mc => mc.IdCampanha == idCampanha).ToListAsync();
        var npcs = await contexto.NpcCampanhas.Where(nc => nc.IdCampanha == idCampanha).ToListAsync();
        var itens = await contexto.ItemCampanhas.Where(ic => ic.IdCampanha == idCampanha).ToListAsync();
        var mapas = await contexto.MapaCampanhas.Where(mc => mc.IdCampanha == idCampanha).ToListAsync();

        contexto.UsuariosCampanha.RemoveRange(usuarios);
        contexto.PersonagensCampanha.RemoveRange(personagens);
        contexto.MonstroCampanhas.RemoveRange(monstros);
        contexto.NpcCampanhas.RemoveRange(npcs);
        contexto.ItemCampanhas.RemoveRange(itens);
        contexto.MapaCampanhas.RemoveRange(mapas);
        contexto.Campanhas.Remove(campanha);
        await contexto.SaveChangesAsync();
    }

    public async Task RemoverUsuarioDaCampanhaAsync(int idAccount, int idCampanha)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        var vinculoUsuario = await contexto.UsuariosCampanha
            .Where(uc => uc.IdAccount == idAccount && uc.IdCampanha == idCampanha)
            .ToListAsync();
        var idsPersonagens = contexto.Personagens
            .Where(p => p.IdAccount == idAccount)
            .Select(p => p.IdPersonagem);
        var vinculosPersonagens = await contexto.PersonagensCampanha
            .Where(pc => pc.IdCampanha == idCampanha && idsPersonagens.Contains(pc.IdPersonagem))
            .ToListAsync();

        contexto.UsuariosCampanha.RemoveRange(vinculoUsuario);
        contexto.PersonagensCampanha.RemoveRange(vinculosPersonagens);
        await contexto.SaveChangesAsync();
    }

    public async Task EnviarFichaCampanhaAsync(int idCampanha, int idPersonagem, string statusInicial = "Pendente")
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var personagemEscolhido = await contexto.Personagens
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdPersonagem == idPersonagem);

        if (personagemEscolhido == null) return;

        var vinculoExistente = await contexto.PersonagensCampanha
            .FirstOrDefaultAsync(pc => pc.IdCampanha == idCampanha && pc.IdPersonagem == idPersonagem);

        if (vinculoExistente == null)
        {
            await contexto.PersonagensCampanha.AddAsync(new PersonagemCampanha(idPersonagem, idCampanha, statusInicial));
        }
        else
        {
            if (statusInicial.Equals("Aceita", StringComparison.OrdinalIgnoreCase))
                vinculoExistente.AprovarFicha();
            else if (statusInicial.Equals("Recusada", StringComparison.OrdinalIgnoreCase))
                vinculoExistente.RecusarFicha();
            else if (!string.Equals(vinculoExistente.StatusAprovacao, "Aceita", StringComparison.OrdinalIgnoreCase))
                vinculoExistente.ReenviarFicha();
        }

        await contexto.SaveChangesAsync();
    }

    public async Task AtualizarStatusFichaCampanhaAsync(int idCampanha, int idPersonagem, string novoStatus, string? motivoRecusa = null)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        var vinculo = await contexto.PersonagensCampanha
            .FirstOrDefaultAsync(pc => pc.IdCampanha == idCampanha && pc.IdPersonagem == idPersonagem);

        if (vinculo != null)
        {
            if (novoStatus.Equals("Aceita", StringComparison.OrdinalIgnoreCase))
                vinculo.AprovarFicha();
            else if (novoStatus.Equals("Recusada", StringComparison.OrdinalIgnoreCase))
                vinculo.RecusarFicha(motivoRecusa);
            else
                vinculo.ReenviarFicha();

            await contexto.SaveChangesAsync();
        }
    }

    public async Task RemoverPersonagemCampanhaAsync(int idCampanha, int idPersonagem)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        var vinculo = await contexto.PersonagensCampanha
            .FirstOrDefaultAsync(pc => pc.IdCampanha == idCampanha && pc.IdPersonagem == idPersonagem);

        if (vinculo != null)
        {
            contexto.PersonagensCampanha.Remove(vinculo);
            await contexto.SaveChangesAsync();
        }
    }

    public async Task AdicionarMonstroCampanhaAsync(int idCampanha, int idMonstro)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        bool jaExiste = await contexto.MonstroCampanhas
            .AsNoTracking()
            .AnyAsync(mc => mc.IdCampanha == idCampanha && mc.IdMonstro == idMonstro);

        if (!jaExiste)
        {
            await contexto.MonstroCampanhas.AddAsync(new MonstroCampanha(idCampanha, idMonstro));
            await contexto.SaveChangesAsync();
        }
    }

    public async Task AdicionarNpcCampanhaAsync(int idCampanha, int idNpc)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        bool jaExiste = await contexto.NpcCampanhas
            .AsNoTracking()
            .AnyAsync(nc => nc.IdCampanha == idCampanha && nc.IdNpc == idNpc);

        if (!jaExiste)
        {
            await contexto.NpcCampanhas.AddAsync(new NpcCampanha(idCampanha, idNpc));
            await contexto.SaveChangesAsync();
        }
    }

    public async Task AdicionarItemCampanhaAsync(int idCampanha, int idItem)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        bool jaExiste = await contexto.ItemCampanhas
            .AsNoTracking()
            .AnyAsync(ic => ic.IdCampanha == idCampanha && ic.IdItem == idItem);

        if (!jaExiste)
        {
            await contexto.ItemCampanhas.AddAsync(new ItemCampanha(idCampanha, idItem));
            await contexto.SaveChangesAsync();
        }
    }

    public async Task AdicionarMapaAsync(Mapa mapa)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        contexto.Mapas.Add(mapa);
        await contexto.SaveChangesAsync();
    }

    public async Task AdicionarMapaCampanhaAsync(int idCampanha, int idMapa)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        bool jaExiste = await contexto.MapaCampanhas
            .AsNoTracking()
            .AnyAsync(mc => mc.IdCampanha == idCampanha && mc.IdMapa == idMapa);

        if (!jaExiste)
        {
            await contexto.MapaCampanhas.AddAsync(new MapaCampanha
            {
                IdCampanha = idCampanha,
                IdMapa = idMapa
            });
            await contexto.SaveChangesAsync();
        }
    }

    public async Task RemoverMonstroCampanhaAsync(int idCampanha, int idMonstro)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        var vinculo = await contexto.MonstroCampanhas
            .FirstOrDefaultAsync(mc => mc.IdCampanha == idCampanha && mc.IdMonstro == idMonstro);

        if (vinculo != null)
        {
            contexto.MonstroCampanhas.Remove(vinculo);
            await contexto.SaveChangesAsync();
        }
    }

    public async Task RemoverNpcCampanhaAsync(int idCampanha, int idNpc)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        var vinculo = await contexto.NpcCampanhas
            .FirstOrDefaultAsync(nc => nc.IdCampanha == idCampanha && nc.IdNpc == idNpc);

        if (vinculo != null)
        {
            contexto.NpcCampanhas.Remove(vinculo);
            await contexto.SaveChangesAsync();
        }
    }

    public async Task RemoverItemCampanhaAsync(int idCampanha, int idItem)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        var vinculo = await contexto.ItemCampanhas
            .FirstOrDefaultAsync(ic => ic.IdCampanha == idCampanha && ic.IdItem == idItem);

        if (vinculo != null)
        {
            contexto.ItemCampanhas.Remove(vinculo);
            await contexto.SaveChangesAsync();
        }
    }

    public async Task RemoverMapaCampanhaAsync(int idCampanha, int idMapa)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        var vinculo = await contexto.MapaCampanhas
            .FirstOrDefaultAsync(mc => mc.IdCampanha == idCampanha && mc.IdMapa == idMapa);

        if (vinculo != null)
        {
            contexto.MapaCampanhas.Remove(vinculo);
            await contexto.SaveChangesAsync();
        }
    }

    public async Task RemoverNpcAsync(int idNpc)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        var npc = await contexto.Npcs.FirstOrDefaultAsync(n => n.IdNpc == idNpc);
        if (npc != null)
        {
            contexto.Npcs.Remove(npc);
            await contexto.SaveChangesAsync();
        }
    }

    public async Task RemoverMapaAsync(int idMapa)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        var mapa = await contexto.Mapas.FirstOrDefaultAsync(m => m.IdMapa == idMapa);
        if (mapa != null)
        {
            contexto.Mapas.Remove(mapa);
            await contexto.SaveChangesAsync();
        }
    }
}
