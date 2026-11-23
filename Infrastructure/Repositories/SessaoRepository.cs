using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Sessoes;
using RPGdeMesa.Infrastructure.Persistence;

namespace RPGdeMesa.Infrastructure.Repositories;

public class SessaoRepository : ISessaoRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SessaoRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<Campanha?> ObterCampanhaParaSessaoAsync(int idCampanha)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Campanhas
            .AsNoTracking()
            .AsSplitQuery()
            .Include(c => c.UsuarioMestre).ThenInclude(u => u.Perfil)
            .Include(c => c.UsuarioCampanhas).ThenInclude(uc => uc.Usuario).ThenInclude(u => u.Perfil)
            .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem).ThenInclude(p => p!.Usuario).ThenInclude(u => u.Perfil)
            .Include(c => c.PersonagemCampanhas).ThenInclude(pc => pc.Personagem).ThenInclude(p => p!.PersonagemClasses).ThenInclude(pcl => pcl.Classe)
            .Include(c => c.MapaCampanhas).ThenInclude(mc => mc.Mapa)
            .Include(c => c.ItemCampanhas).ThenInclude(ic => ic.Item)
            .Include(c => c.NpcCampanhas).ThenInclude(nc => nc.Npc)
            .Include(c => c.MonstroCampanhas).ThenInclude(mc => mc.Monstro)
            .FirstOrDefaultAsync(c => c.IdCampanha == idCampanha);
    }

    public async Task<bool> SessaoEstaAtivaAsync(int idCampanha)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.SessoesMesa
            .AsNoTracking()
            .AnyAsync(s => s.IdCampanha == idCampanha && s.Ativa);
    }

    public async Task<SessaoMesa?> ObterSessaoPorCampanhaAsync(int idCampanha)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.SessoesMesa
            .AsNoTracking()
            .Where(s => s.IdCampanha == idCampanha)
            .OrderByDescending(s => s.Ativa)
            .ThenByDescending(s => s.IdSessao)
            .FirstOrDefaultAsync();
    }

    public async Task<SessaoMesa?> ObterSessaoPorIdAsync(int idSessao)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.SessoesMesa
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IdSessao == idSessao);
    }

    public async Task<SessaoMesa> AbrirOuReabrirSessaoAsync(int idCampanha, int? idMapaInicial = null)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var sessaoAtiva = await contexto.SessoesMesa
            .Where(s => s.IdCampanha == idCampanha && s.Ativa)
            .OrderByDescending(s => s.IdSessao)
            .FirstOrDefaultAsync();

        if (sessaoAtiva != null)
        {
            sessaoAtiva.AbrirSessao(idMapaInicial);
            await contexto.SaveChangesAsync();
            return sessaoAtiva;
        }

        int ultimoNumero = await contexto.SessoesMesa
            .AsNoTracking()
            .Where(s => s.IdCampanha == idCampanha)
            .MaxAsync(s => (int?)s.NumeroSessao) ?? 0;

        var novaSessao = new SessaoMesa(idCampanha, ultimoNumero + 1, idMapaInicial);
        await contexto.SessoesMesa.AddAsync(novaSessao);
        await contexto.SaveChangesAsync();
        return novaSessao;
    }

    public async Task<SessaoMesa?> ObterSessaoCompletaAsync(int idSessao)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.SessoesMesa
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.MapaAtivo)
            .Include(s => s.Participantes).ThenInclude(p => p.Usuario).ThenInclude(u => u.Perfil)
            .Include(s => s.Participantes).ThenInclude(p => p.Personagem).ThenInclude(pe => pe!.PersonagemClasses).ThenInclude(pc => pc.Classe)
            .FirstOrDefaultAsync(s => s.IdSessao == idSessao);
    }

    public async Task<bool> ConectarParticipanteAsync(int idSessao, int idAccount, int? idPersonagem)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var participante = await contexto.SessaoParticipantes
            .FirstOrDefaultAsync(sp => sp.IdSessao == idSessao && sp.IdAccount == idAccount);

        bool estavaOffline;

        if (participante == null)
        {
            participante = new SessaoParticipante(idSessao, idAccount, idPersonagem);
            await contexto.SessaoParticipantes.AddAsync(participante);
            estavaOffline = true;
        }
        else
        {
            estavaOffline = !participante.EstaConectadoAgora();
            participante.Conectar(idPersonagem);
        }

        await contexto.SaveChangesAsync();
        return estavaOffline;
    }

    public async Task AtualizarHeartbeatAsync(int idSessao, int idAccount)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var participante = await contexto.SessaoParticipantes
            .FirstOrDefaultAsync(sp => sp.IdSessao == idSessao && sp.IdAccount == idAccount);

        if (participante != null)
        {
            participante.AtualizarHeartbeat();
            await contexto.SaveChangesAsync();
        }
    }

    public async Task<bool> DesconectarParticipanteAsync(int idSessao, int idAccount)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var participante = await contexto.SessaoParticipantes
            .FirstOrDefaultAsync(sp => sp.IdSessao == idSessao && sp.IdAccount == idAccount);

        if (participante == null || !participante.Online)
            return false;

        participante.Desconectar();
        await contexto.SaveChangesAsync();
        return true;
    }

    public async Task FinalizarSessaoAsync(int idSessao)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var sessao = await contexto.SessoesMesa
            .Include(s => s.Participantes)
            .FirstOrDefaultAsync(s => s.IdSessao == idSessao);

        if (sessao == null) return;

        sessao.FinalizarSessao();
        foreach (var participante in sessao.Participantes)
        {
            participante.Desconectar();
        }

        await contexto.SaveChangesAsync();
    }

    public async Task AtualizarResumoSessaoAsync(int idSessao, string? titulo, string? resumo, int? xpTotalDistribuido = null)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var sessao = await contexto.SessoesMesa
            .FirstOrDefaultAsync(s => s.IdSessao == idSessao);

        if (sessao == null) return;

        sessao.AtualizarResumo(titulo, resumo, xpTotalDistribuido);
        await contexto.SaveChangesAsync();
    }

    public async Task AtualizarMapaAtivoAsync(int idSessao, int? idMapa)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var sessao = await contexto.SessoesMesa
            .FirstOrDefaultAsync(s => s.IdSessao == idSessao);

        if (sessao == null) return;

        sessao.AlterarMapaAtivo(idMapa);
        await contexto.SaveChangesAsync();
    }

    public async Task AdicionarMensagemAsync(SessaoMensagem mensagem)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        await contexto.SessaoMensagens.AddAsync(mensagem);
        await contexto.SaveChangesAsync();
    }

    public async Task<List<SessaoMensagem>> ListarMensagensAsync(int idSessao, int limitePorCanal = 80)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        var recentes = await contexto.SessaoMensagens
            .AsNoTracking()
            .Include(m => m.Usuario).ThenInclude(u => u!.Perfil)
            .Where(m => m.IdSessao == idSessao)
            .OrderByDescending(m => m.DataEnvio)
            .Take(limitePorCanal * 4)
            .ToListAsync();

        recentes.Reverse();
        return recentes;
    }

    public async Task<List<SessaoMesa>> ListarHistoricoSessoesAsync(int idCampanha)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.SessoesMesa
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.MapaAtivo)
            .Include(s => s.Participantes).ThenInclude(p => p.Usuario).ThenInclude(u => u.Perfil)
            .Include(s => s.Participantes).ThenInclude(p => p.Personagem)
            .Where(s => s.IdCampanha == idCampanha)
            .OrderByDescending(s => s.NumeroSessao)
            .ThenByDescending(s => s.IdSessao)
            .ToListAsync();
    }

    public async Task<Dictionary<int, (int TotalMensagensChat, int TotalRolagens)>> ContarAtividadePorSessoesAsync(
        IEnumerable<int> idsSessao)
    {
        var ids = idsSessao.Distinct().ToList();
        if (!ids.Any())
            return new Dictionary<int, (int, int)>();

        await using var contexto = await _factory.CreateDbContextAsync();
        var agrupado = await contexto.SessaoMensagens
            .AsNoTracking()
            .Where(m => ids.Contains(m.IdSessao))
            .GroupBy(m => m.IdSessao)
            .Select(g => new
            {
                IdSessao = g.Key,
                MensagensChat = g.Count(m => m.Canal == CanaisSessao.Chat && m.IdAccount != null),
                Rolagens = g.Count(m => m.Canal == CanaisSessao.Registro)
            })
            .ToListAsync();

        return agrupado.ToDictionary(
            x => x.IdSessao,
            x => (x.MensagensChat, x.Rolagens));
    }

    public async Task<Personagem?> ObterFichaCompletaAsync(int idPersonagem)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        var personagem = await contexto.Personagens
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Usuario).ThenInclude(u => u.Perfil)
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

    public async Task<List<Personagem>> ListarFichasAceitasCompletasAsync(int idCampanha)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        var idsAceitos = await contexto.PersonagensCampanha
            .AsNoTracking()
            .Where(pc => pc.IdCampanha == idCampanha &&
                        (string.IsNullOrEmpty(pc.StatusAprovacao) || pc.StatusAprovacao == "Aceita"))
            .Select(pc => pc.IdPersonagem)
            .ToListAsync();

        if (!idsAceitos.Any())
            return new List<Personagem>();

        var personagens = await contexto.Personagens
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Usuario).ThenInclude(u => u.Perfil)
            .Include(p => p.Raca)
            .Include(p => p.Antecedente)
            .Include(p => p.PersonagemClasses).ThenInclude(pc => pc.Classe)
            .Include(p => p.Salvaguardas)
            .Include(p => p.Pericias)
            .Include(p => p.Inventario).ThenInclude(inv => inv!.Itens).ThenInclude(ii => ii.Item)
            .Include(p => p.PersonagemMagias).ThenInclude(pm => pm.Magia)
            .Where(p => idsAceitos.Contains(p.IdPersonagem))
            .OrderBy(p => p.Nome)
            .ToListAsync();

        foreach (var p in personagens)
        {
            if (p.Raca != null) p.RacaTexto = p.Raca.Nome;
            if (p.Antecedente != null) p.AntecedenteTexto = p.Antecedente.Nome;
        }

        return personagens;
    }

    public async Task<List<Item>> ListarItensCampanhaAsync(int idCampanha)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.ItemCampanhas
            .AsNoTracking()
            .Where(ic => ic.IdCampanha == idCampanha && ic.Item != null)
            .Select(ic => ic.Item!)
            .OrderBy(i => i.Nome)
            .ToListAsync();
    }

    // =========================================================================
    // PINOS (TOKENS) SALVOS POR MAPA
    // =========================================================================
    public async Task<List<SessaoPino>> ListarPinosSessaoAsync(int idSessao)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.SessaoPinos
            .AsNoTracking()
            .Where(p => p.IdSessao == idSessao)
            .OrderBy(p => p.IdPino)
            .ToListAsync();
    }

    public async Task SalvarOuMoverPinoAsync(int idSessao, int? idMapa, string tipoEntidade, int idEntidade, int coluna, int linha, bool visivel)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var existente = await contexto.SessaoPinos
            .FirstOrDefaultAsync(p => p.IdSessao == idSessao
                                   && p.IdMapa == idMapa
                                   && p.TipoEntidade == tipoEntidade
                                   && p.IdEntidade == idEntidade);

        if (existente == null)
        {
            var novo = new SessaoPino(idSessao, idMapa, tipoEntidade, idEntidade, coluna, linha, visivel);
            await contexto.SessaoPinos.AddAsync(novo);
        }
        else
        {
            existente.MoverPara(coluna, linha);
            existente.DefinirVisibilidade(visivel);
        }

        await contexto.SaveChangesAsync();
    }

    public async Task AlterarVisibilidadePinoAsync(int idSessao, int? idMapa, string tipoEntidade, int idEntidade, bool visivel)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var existente = await contexto.SessaoPinos
            .FirstOrDefaultAsync(p => p.IdSessao == idSessao
                                   && p.IdMapa == idMapa
                                   && p.TipoEntidade == tipoEntidade
                                   && p.IdEntidade == idEntidade);

        if (existente == null) return;

        existente.DefinirVisibilidade(visivel);
        await contexto.SaveChangesAsync();
    }

    public async Task RemoverPinoAsync(int idSessao, int? idMapa, string tipoEntidade, int idEntidade)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var existente = await contexto.SessaoPinos
            .FirstOrDefaultAsync(p => p.IdSessao == idSessao
                                   && p.IdMapa == idMapa
                                   && p.TipoEntidade == tipoEntidade
                                   && p.IdEntidade == idEntidade);

        if (existente == null) return;

        contexto.SessaoPinos.Remove(existente);
        await contexto.SaveChangesAsync();
    }

    public async Task AtualizarPvPinoAsync(
        int idSessao,
        int? idMapa,
        string tipoEntidade,
        int idEntidade,
        int pvAtual)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var pino = await contexto.SessaoPinos.FirstOrDefaultAsync(p =>
            p.IdSessao == idSessao &&
            p.IdMapa == idMapa &&
            p.TipoEntidade == tipoEntidade &&
            p.IdEntidade == idEntidade);

        if (pino == null) return;

        pino.DefinirPvAtual(pvAtual);
        await contexto.SaveChangesAsync();
    }
    public async Task AtualizarTurnoJogadorAsync(int idSessao, int? idAccountTurno)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var sessao = await contexto.SessoesMesa
            .FirstOrDefaultAsync(s => s.IdSessao == idSessao);

        if (sessao == null) return;

        sessao.DefinirTurnoJogador(idAccountTurno);
        await contexto.SaveChangesAsync();
    }
}