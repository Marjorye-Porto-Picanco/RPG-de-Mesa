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
    private readonly AppDbContext _contexto;
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    public CampanhaRepository(AppDbContext contexto) => _contexto = contexto;

    private async Task<T> ExecutarComLockAsync<T>(Func<Task<T>> operacao)
    {
        await _dbLock.WaitAsync();
        try
        {
            return await operacao();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    private async Task ExecutarComLockAsync(Func<Task> operacao)
    {
        await _dbLock.WaitAsync();
        try
        {
            await operacao();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public Task<Campanha?> ObterPorIdComDetalhesAsync(int idCampanha) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            return await _contexto.Campanhas
                .AsSplitQuery()
                .Include(c => c.UsuarioMestre)
                .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem).ThenInclude(p => p!.Usuario)
                .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem).ThenInclude(p => p!.PersonagemClasses).ThenInclude(pcl => pcl.Classe)
                .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem).ThenInclude(p => p!.Salvaguardas)
                .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem).ThenInclude(p => p!.Pericias)
                .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem).ThenInclude(p => p!.Inventario).ThenInclude(inv => inv!.Itens).ThenInclude(ii => ii.Item)
                .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem).ThenInclude(p => p!.PersonagemMagias).ThenInclude(pm => pm.Magia)
                .Include(c => c.MonstroCampanhas).ThenInclude(mc => mc.Monstro)
                .Include(c => c.NpcCampanhas).ThenInclude(nc => nc.Npc)
                .Include(c => c.ItemCampanhas).ThenInclude(ic => ic.Item)
                .Include(c => c.MapaCampanhas).ThenInclude(mc => mc.Mapa)
                .FirstOrDefaultAsync(c => c.IdCampanha == idCampanha);
        });

    public Task<Campanha?> ObterPorCodigoConviteAsync(string codigoConvite) =>
        ExecutarComLockAsync(() =>
            _contexto.Campanhas
                .Include(c => c.UsuarioMestre)
                .FirstOrDefaultAsync(c => c.CodigoConvite != null && c.CodigoConvite.ToUpper() == codigoConvite.Trim().ToUpper()));

    public Task<List<Campanha>> ListarPorMestreAsync(int idAccount) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            return await _contexto.Campanhas
                .Where(c => c.IdAccount == idAccount)
                .OrderByDescending(c => c.DataCriacao)
                .ToListAsync();
        });

    public Task<List<Campanha>> ListarPorJogadorAsync(int idAccount) =>
        ExecutarComLockAsync(async () =>
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
        });

    public Task<bool> CodigoConviteExisteAsync(string codigoConvite) =>
        ExecutarComLockAsync(() => _contexto.Campanhas.AnyAsync(c => c.CodigoConvite == codigoConvite));

    public Task<bool> UsuarioJaParticipaAsync(int idAccount, int idCampanha) =>
        ExecutarComLockAsync(() =>
            _contexto.UsuariosCampanha
                .AsNoTracking()
                .AnyAsync(uc => uc.IdAccount == idAccount && uc.IdCampanha == idCampanha));

    public Task AdicionarUsuarioNaCampanhaAsync(UsuarioCampanha vinculo) =>
        ExecutarComLockAsync(async () =>
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
        });

    public Task<List<NPC>> ListarTodosNpcsAsync() =>
        ExecutarComLockAsync(() => _contexto.Npcs.AsNoTracking().ToListAsync());

    public Task<List<Item>> ListarTodosItensAsync() =>
        ExecutarComLockAsync(() => _contexto.Itens.AsNoTracking().ToListAsync());

    public Task<List<Monstro>> ListarTodosMonstrosAsync() =>
        ExecutarComLockAsync(() => _contexto.Monstros.AsNoTracking().ToListAsync());

    public Task<List<Mapa>> ListarTodosMapasAsync() =>
        ExecutarComLockAsync(() => _contexto.Mapas.AsNoTracking().ToListAsync());

    public Task<List<Magia>> ListarTodasMagiasAsync() =>
        ExecutarComLockAsync(() =>
            _contexto.Magias.AsNoTracking().OrderBy(m => m.CirculoNivel).ThenBy(m => m.Nome).ToListAsync());

    public Task AdicionarAsync(Campanha campanha) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.Campanhas.Add(campanha);
            await _contexto.SaveChangesAsync();
        });

    public Task AtualizarAsync(Campanha campanha) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.Campanhas.Update(campanha);
            await _contexto.SaveChangesAsync();
        });

    public Task RemoverCampanhaAsync(int idCampanha) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            var campanha = await _contexto.Campanhas.FirstOrDefaultAsync(c => c.IdCampanha == idCampanha);
            if (campanha is null) return;

            var usuarios = await _contexto.UsuariosCampanha.Where(uc => uc.IdCampanha == idCampanha).ToListAsync();
            var personagens = await _contexto.PersonagensCampanha.Where(pc => pc.IdCampanha == idCampanha).ToListAsync();
            var monstros = await _contexto.MonstroCampanhas.Where(mc => mc.IdCampanha == idCampanha).ToListAsync();
            var npcs = await _contexto.NpcCampanhas.Where(nc => nc.IdCampanha == idCampanha).ToListAsync();
            var itens = await _contexto.ItemCampanhas.Where(ic => ic.IdCampanha == idCampanha).ToListAsync();
            var mapas = await _contexto.MapaCampanhas.Where(mc => mc.IdCampanha == idCampanha).ToListAsync();

            _contexto.UsuariosCampanha.RemoveRange(usuarios);
            _contexto.PersonagensCampanha.RemoveRange(personagens);
            _contexto.MonstroCampanhas.RemoveRange(monstros);
            _contexto.NpcCampanhas.RemoveRange(npcs);
            _contexto.ItemCampanhas.RemoveRange(itens);
            _contexto.MapaCampanhas.RemoveRange(mapas);
            _contexto.Campanhas.Remove(campanha);
            await _contexto.SaveChangesAsync();
        });

    public Task RemoverUsuarioDaCampanhaAsync(int idAccount, int idCampanha) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            var vinculoUsuario = await _contexto.UsuariosCampanha
                .Where(uc => uc.IdAccount == idAccount && uc.IdCampanha == idCampanha)
                .ToListAsync();
            var idsPersonagens = _contexto.Personagens
                .Where(p => p.IdAccount == idAccount)
                .Select(p => p.IdPersonagem);
            var vinculosPersonagens = await _contexto.PersonagensCampanha
                .Where(pc => pc.IdCampanha == idCampanha && idsPersonagens.Contains(pc.IdPersonagem))
                .ToListAsync();

            _contexto.UsuariosCampanha.RemoveRange(vinculoUsuario);
            _contexto.PersonagensCampanha.RemoveRange(vinculosPersonagens);
            await _contexto.SaveChangesAsync();
        });

    public Task EnviarFichaCampanhaAsync(int idCampanha, int idPersonagem, string statusInicial = "Pendente") =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();

            var personagemEscolhido = await _contexto.Personagens
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.IdPersonagem == idPersonagem);

            if (personagemEscolhido == null) return;

            var vinculoExistente = await _contexto.PersonagensCampanha
                .FirstOrDefaultAsync(pc => pc.IdCampanha == idCampanha && pc.IdPersonagem == idPersonagem);

            if (vinculoExistente == null)
            {
                await _contexto.PersonagensCampanha.AddAsync(new PersonagemCampanha(idPersonagem, idCampanha, statusInicial));
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

            await _contexto.SaveChangesAsync();
        });

    public Task AtualizarStatusFichaCampanhaAsync(int idCampanha, int idPersonagem, string novoStatus, string? motivoRecusa = null) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            var vinculo = await _contexto.PersonagensCampanha
                .FirstOrDefaultAsync(pc => pc.IdCampanha == idCampanha && pc.IdPersonagem == idPersonagem);

            if (vinculo != null)
            {
                if (novoStatus.Equals("Aceita", StringComparison.OrdinalIgnoreCase))
                    vinculo.AprovarFicha();
                else if (novoStatus.Equals("Recusada", StringComparison.OrdinalIgnoreCase))
                    vinculo.RecusarFicha(motivoRecusa);
                else
                    vinculo.ReenviarFicha();

                await _contexto.SaveChangesAsync();
            }
        });

    public Task RemoverPersonagemCampanhaAsync(int idCampanha, int idPersonagem) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            var vinculo = await _contexto.PersonagensCampanha
                .FirstOrDefaultAsync(pc => pc.IdCampanha == idCampanha && pc.IdPersonagem == idPersonagem);

            if (vinculo != null)
            {
                _contexto.PersonagensCampanha.Remove(vinculo);
                await _contexto.SaveChangesAsync();
            }
        });

    public Task AdicionarMonstroCampanhaAsync(int idCampanha, int idMonstro) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            bool jaExiste = await _contexto.MonstroCampanhas
                .AsNoTracking()
                .AnyAsync(mc => mc.IdCampanha == idCampanha && mc.IdMonstro == idMonstro);

            if (!jaExiste)
            {
                await _contexto.MonstroCampanhas.AddAsync(new MonstroCampanha(idCampanha, idMonstro));
                await _contexto.SaveChangesAsync();
            }
        });

    public Task AdicionarNpcCampanhaAsync(int idCampanha, int idNpc) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            bool jaExiste = await _contexto.NpcCampanhas
                .AsNoTracking()
                .AnyAsync(nc => nc.IdCampanha == idCampanha && nc.IdNpc == idNpc);

            if (!jaExiste)
            {
                await _contexto.NpcCampanhas.AddAsync(new NpcCampanha(idCampanha, idNpc));
                await _contexto.SaveChangesAsync();
            }
        });

    public Task AdicionarItemCampanhaAsync(int idCampanha, int idItem) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            bool jaExiste = await _contexto.ItemCampanhas
                .AsNoTracking()
                .AnyAsync(ic => ic.IdCampanha == idCampanha && ic.IdItem == idItem);

            if (!jaExiste)
            {
                await _contexto.ItemCampanhas.AddAsync(new ItemCampanha(idCampanha, idItem));
                await _contexto.SaveChangesAsync();
            }
        });

    public Task AdicionarMapaAsync(Mapa mapa) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.Mapas.Add(mapa);
            await _contexto.SaveChangesAsync();
        });

    public Task AdicionarMapaCampanhaAsync(int idCampanha, int idMapa) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            bool jaExiste = await _contexto.MapaCampanhas
                .AsNoTracking()
                .AnyAsync(mc => mc.IdCampanha == idCampanha && mc.IdMapa == idMapa);

            if (!jaExiste)
            {
                await _contexto.MapaCampanhas.AddAsync(new MapaCampanha
                {
                    IdCampanha = idCampanha,
                    IdMapa = idMapa
                });
                await _contexto.SaveChangesAsync();
            }
        });

    public Task RemoverMonstroCampanhaAsync(int idCampanha, int idMonstro) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            var vinculo = await _contexto.MonstroCampanhas
                .FirstOrDefaultAsync(mc => mc.IdCampanha == idCampanha && mc.IdMonstro == idMonstro);

            if (vinculo != null)
            {
                _contexto.MonstroCampanhas.Remove(vinculo);
                await _contexto.SaveChangesAsync();
            }
        });

    public Task RemoverNpcCampanhaAsync(int idCampanha, int idNpc) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            var vinculo = await _contexto.NpcCampanhas
                .FirstOrDefaultAsync(nc => nc.IdCampanha == idCampanha && nc.IdNpc == idNpc);

            if (vinculo != null)
            {
                _contexto.NpcCampanhas.Remove(vinculo);
                await _contexto.SaveChangesAsync();
            }
        });

    public Task RemoverItemCampanhaAsync(int idCampanha, int idItem) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            var vinculo = await _contexto.ItemCampanhas
                .FirstOrDefaultAsync(ic => ic.IdCampanha == idCampanha && ic.IdItem == idItem);

            if (vinculo != null)
            {
                _contexto.ItemCampanhas.Remove(vinculo);
                await _contexto.SaveChangesAsync();
            }
        });

    public Task RemoverMapaCampanhaAsync(int idCampanha, int idMapa) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            var vinculo = await _contexto.MapaCampanhas
                .FirstOrDefaultAsync(mc => mc.IdCampanha == idCampanha && mc.IdMapa == idMapa);

            if (vinculo != null)
            {
                _contexto.MapaCampanhas.Remove(vinculo);
                await _contexto.SaveChangesAsync();
            }
        });

    public Task RemoverNpcAsync(int idNpc) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            var npc = await _contexto.Npcs.FirstOrDefaultAsync(n => n.IdNpc == idNpc);
            if (npc != null)
            {
                _contexto.Npcs.Remove(npc);
                await _contexto.SaveChangesAsync();
            }
        });

    public Task RemoverMapaAsync(int idMapa) =>
        ExecutarComLockAsync(async () =>
        {
            _contexto.ChangeTracker.Clear();
            var mapa = await _contexto.Mapas.FirstOrDefaultAsync(m => m.IdMapa == idMapa);
            if (mapa != null)
            {
                _contexto.Mapas.Remove(mapa);
                await _contexto.SaveChangesAsync();
            }
        });
}