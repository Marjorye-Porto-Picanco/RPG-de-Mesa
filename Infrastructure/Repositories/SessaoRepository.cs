using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Sessoes;
using RPGdeMesa.Infrastructure.Persistence;

namespace RPGdeMesa.Infrastructure.Repositories;

public class SessaoRepository : ISessaoRepository
{
    private readonly AppDbContext _contexto;

    public SessaoRepository(AppDbContext contexto) => _contexto = contexto;

    public async Task<Campanha?> ObterCampanhaParaSessaoAsync(int idCampanha)
    {
        return await _contexto.Campanhas
            .AsNoTracking()
            .AsSplitQuery()
            .Include(c => c.UsuarioMestre).ThenInclude(u => u.Perfil)
            .Include(c => c.UsuarioCampanhas).ThenInclude(uc => uc.Usuario).ThenInclude(u => u.Perfil)
            .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem).ThenInclude(p => p!.Usuario).ThenInclude(u => u.Perfil)
            .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem).ThenInclude(p => p!.PersonagemClasses).ThenInclude(pcl => pcl.Classe)
            .Include(c => c.MapaCampanhas).ThenInclude(mc => mc.Mapa)
            .FirstOrDefaultAsync(c => c.IdCampanha == idCampanha);
    }

    public async Task<SessaoMesa> ObterOuCriarSessaoPorCampanhaAsync(int idCampanha, int? idMapaInicial = null)
    {
        _contexto.ChangeTracker.Clear();

        var existente = await _contexto.SessoesMesa
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IdCampanha == idCampanha);

        if (existente != null)
            return existente;

        try
        {
            var novaSessao = new SessaoMesa(idCampanha, idMapaInicial);
            await _contexto.SessoesMesa.AddAsync(novaSessao);
            await _contexto.SaveChangesAsync();
            return novaSessao;
        }
        catch (DbUpdateException)
        {
            _contexto.ChangeTracker.Clear();
            return await _contexto.SessoesMesa
                .AsNoTracking()
                .FirstAsync(s => s.IdCampanha == idCampanha);
        }
    }

    public async Task<SessaoMesa?> ObterSessaoCompletaAsync(int idSessao)
    {
        return await _contexto.SessoesMesa
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.MapaAtivo)
            .Include(s => s.Participantes).ThenInclude(p => p.Usuario).ThenInclude(u => u.Perfil)
            .Include(s => s.Participantes).ThenInclude(p => p.Personagem).ThenInclude(pe => pe!.PersonagemClasses).ThenInclude(pc => pc.Classe)
            .FirstOrDefaultAsync(s => s.IdSessao == idSessao);
    }

    public async Task<bool> ConectarParticipanteAsync(int idSessao, int idAccount, int? idPersonagem)
    {
        _contexto.ChangeTracker.Clear();

        var participante = await _contexto.SessaoParticipantes
            .FirstOrDefaultAsync(sp => sp.IdSessao == idSessao && sp.IdAccount == idAccount);

        bool estavaOffline;

        if (participante == null)
        {
            participante = new SessaoParticipante(idSessao, idAccount, idPersonagem);
            await _contexto.SessaoParticipantes.AddAsync(participante);
            estavaOffline = true;
        }
        else
        {
            estavaOffline = !participante.EstaConectadoAgora();
            participante.Conectar(idPersonagem);
        }

        await _contexto.SaveChangesAsync();
        return estavaOffline;
    }

    public async Task AtualizarHeartbeatAsync(int idSessao, int idAccount)
    {
        _contexto.ChangeTracker.Clear();

        var participante = await _contexto.SessaoParticipantes
            .FirstOrDefaultAsync(sp => sp.IdSessao == idSessao && sp.IdAccount == idAccount);

        if (participante != null)
        {
            participante.AtualizarHeartbeat();
            await _contexto.SaveChangesAsync();
        }
    }

    public async Task<bool> DesconectarParticipanteAsync(int idSessao, int idAccount)
    {
        _contexto.ChangeTracker.Clear();

        var participante = await _contexto.SessaoParticipantes
            .FirstOrDefaultAsync(sp => sp.IdSessao == idSessao && sp.IdAccount == idAccount);

        if (participante == null || !participante.Online)
            return false;

        participante.Desconectar();
        await _contexto.SaveChangesAsync();
        return true;
    }

    public async Task AtualizarMapaAtivoAsync(int idSessao, int? idMapa)
    {
        _contexto.ChangeTracker.Clear();

        var sessao = await _contexto.SessoesMesa
            .FirstOrDefaultAsync(s => s.IdSessao == idSessao);

        if (sessao == null) return;

        sessao.AlterarMapaAtivo(idMapa);
        await _contexto.SaveChangesAsync();
    }

    public async Task AdicionarMensagemAsync(SessaoMensagem mensagem)
    {
        _contexto.ChangeTracker.Clear();
        await _contexto.SessaoMensagens.AddAsync(mensagem);
        await _contexto.SaveChangesAsync();
    }

    public async Task<List<SessaoMensagem>> ListarMensagensAsync(int idSessao, int limitePorCanal = 80)
    {
        var recentes = await _contexto.SessaoMensagens
            .AsNoTracking()
            .Include(m => m.Usuario)
            .Where(m => m.IdSessao == idSessao)
            .OrderByDescending(m => m.DataEnvio)
            .Take(limitePorCanal * 4)
            .ToListAsync();

        recentes.Reverse();
        return recentes;
    }

    public async Task<Personagem?> ObterFichaCompletaAsync(int idPersonagem)
    {
        var personagem = await _contexto.Personagens
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Raca)
            .Include(p => p.Antecedente)
            .Include(p => p.PersonagemClasses).ThenInclude(pc => pc.Classe)
            .Include(p => p.Salvaguardas)
            .Include(p => p.Pericias)
            .Include(p => p.Inventario).ThenInclude(inv => inv!.Itens).ThenInclude(ii => ii.Item)
            .Include(p => p.PersonagemMagias).ThenInclude(pm => pm.Magia)
            .FirstOrDefaultAsync(p => p.IdPersonagem == idPersonagem);

        if (personagem != null)
        {
            if (personagem.Raca != null)
                personagem.RacaTexto = personagem.Raca.Nome;
            if (personagem.Antecedente != null)
                personagem.AntecedenteTexto = personagem.Antecedente.Nome;
        }

        return personagem;
    }
}