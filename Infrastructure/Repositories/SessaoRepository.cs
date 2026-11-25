using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Combate;
using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Magias;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Sessoes;
using RPGdeMesa.Infrastructure.Persistence;

namespace RPGdeMesa.Infrastructure.Repositories;

public partial class SessaoRepository : ISessaoRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SessaoRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<Campanha?> ObterCampanhaParaSessaoAsync(int idCampanha, CancellationToken cancellationToken = default)
    {
        await using var contexto = await _factory.CreateDbContextAsync(cancellationToken);
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
            .Include(c => c.NpcCampanhas).ThenInclude(nc => nc.Npc).ThenInclude(n => n!.Itens).ThenInclude(i => i.Item)
            .Include(c => c.NpcCampanhas).ThenInclude(nc => nc.Npc).ThenInclude(n => n!.Magias).ThenInclude(m => m.Magia)
            // .Include(c => c.NpcCampanhas).ThenInclude(nc => nc.Npc).ThenInclude(n => n!.Ataques)
            .Include(c => c.MonstroCampanhas).ThenInclude(mc => mc.Monstro).ThenInclude(m => m!.Ataques)
            .FirstOrDefaultAsync(c => c.IdCampanha == idCampanha, cancellationToken);
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

    public async Task<SessaoMesa?> ObterSessaoPorIdAsync(int idSessao, CancellationToken cancellationToken = default)
    {
        await using var contexto = await _factory.CreateDbContextAsync(cancellationToken);
        return await contexto.SessoesMesa
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IdSessao == idSessao, cancellationToken);
    }

    public async Task<SessaoMesa> AbrirOuReabrirSessaoAsync(int idCampanha, int? idMapaInicial = null)
    {
        await using var contextoEstrategia = await _factory.CreateDbContextAsync();
        return await contextoEstrategia.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var contexto = await _factory.CreateDbContextAsync();
            await using var transacao = await contexto.Database.BeginTransactionAsync();
            await BloquearOperacaoAsync(contexto, $"RPGdeMesa:abrir-sessao:{idCampanha}");

            var sessaoAtiva = await contexto.SessoesMesa
                .Where(s => s.IdCampanha == idCampanha && s.Ativa)
                .OrderByDescending(s => s.IdSessao)
                .FirstOrDefaultAsync();

            if (sessaoAtiva is null)
            {
                int ultimoNumero = await contexto.SessoesMesa
                    .Where(s => s.IdCampanha == idCampanha)
                    .MaxAsync(s => (int?)s.NumeroSessao) ?? 0;

                sessaoAtiva = new SessaoMesa(idCampanha, ultimoNumero + 1, idMapaInicial);
                contexto.SessoesMesa.Add(sessaoAtiva);
            }
            else
            {
                sessaoAtiva.AbrirSessao(idMapaInicial);
            }

            await contexto.SaveChangesAsync();
            await transacao.CommitAsync();
            return sessaoAtiva;
        });
    }

    public async Task<SessaoMesa?> ObterSessaoCompletaAsync(int idSessao, CancellationToken cancellationToken = default)
    {
        await using var contexto = await _factory.CreateDbContextAsync(cancellationToken);
        return await contexto.SessoesMesa
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.MapaAtivo)
            .Include(s => s.Participantes).ThenInclude(p => p.Usuario).ThenInclude(u => u.Perfil)
            .Include(s => s.Participantes).ThenInclude(p => p.Personagem).ThenInclude(pe => pe!.PersonagemClasses).ThenInclude(pc => pc.Classe)
            .FirstOrDefaultAsync(s => s.IdSessao == idSessao, cancellationToken);
    }

    public async Task<SessaoSinal?> LerSinalSessaoAsync(int idSessao, CancellationToken cancellationToken)
    {
        await using var contexto = await _factory.CreateDbContextAsync(cancellationToken);
        var limiteOnline = DateTime.UtcNow.AddSeconds(-SessaoParticipante.ToleranciaPresencaSegundos);
        return await ConsultaSinalSessao(contexto, idSessao, limiteOnline).FirstOrDefaultAsync(cancellationToken);
    }

    internal static IQueryable<SessaoSinal> ConsultaSinalSessao(AppDbContext contexto, int idSessao, DateTime limiteOnline) =>
        contexto.SessoesMesa
            .AsNoTracking()
            .AsSplitQuery()
            .Where(s => s.IdSessao == idSessao)
            .Select(s => new SessaoSinal(
                s.Ativa, s.Titulo, s.IdMapaAtivo, s.IdAccountTurno, s.DataAtualizacao,
                s.Mensagens.Max(m => (int?)m.IdMensagem) ?? 0,
                s.Participantes.OrderBy(p => p.IdAccount).Select(p => new ParticipanteSinal(
                    p.IdAccount, p.IdPersonagem, p.Online && p.UltimoHeartbeat >= limiteOnline,
                    p.Esquivando, p.Escondido, (int?)p.Personagem!.PvAtual,
                    (int?)p.Personagem!.PvMaximo, (int?)p.Personagem!.Destreza)).ToList(),
                contexto.SessaoPinos.Where(p => p.IdSessao == s.IdSessao).OrderBy(p => p.IdPino)
                    .Select(p => new PinoSinal(p.IdPino, p.IdMapa, p.TipoEntidade, p.IdEntidade,
                        p.Coluna, p.Linha, p.Visivel, p.PvAtual)).ToList()));

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

    public async Task AtualizarHeartbeatAsync(int idSessao, int idAccount, CancellationToken cancellationToken = default)
    {
        await using var contexto = await _factory.CreateDbContextAsync(cancellationToken);

        var agora = DateTime.UtcNow;
        var limite = agora.AddSeconds(-5);
        await contexto.SessaoParticipantes
            .Where(p => p.IdSessao == idSessao && p.IdAccount == idAccount &&
                (!p.Online || p.UltimoHeartbeat <= limite) && p.Sessao.Ativa)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.Online, true)
                .SetProperty(x => x.UltimoHeartbeat, agora), cancellationToken);
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

    public Task AtualizarMapaAtivoAsync(int idSessao, int? idMapa) =>
        ExecutarOperacaoAsync(idSessao, Guid.NewGuid(), $"RPGdeMesa:combate:{idSessao}", async contexto =>
        {
            var sessao = await contexto.SessoesMesa.FirstOrDefaultAsync(s => s.IdSessao == idSessao);
            if (sessao is null) return;
            ExigirTabuleiroLivre(sessao.Combate2014Json);
            sessao.AlterarMapaAtivo(idMapa);
        });

    public Task AtualizarTurnoJogadorAsync(int idCampanha, int idSessao, int? idAccountJogador,
        int? idAccountQueDeveEstarNoTurno, Guid operacao, string aviso) =>
        ExecutarOperacaoAsync(idSessao, operacao, $"RPGdeMesa:combate:{idSessao}", async contexto =>
        {
            var sessao = await contexto.SessoesMesa.FirstOrDefaultAsync(s =>
                s.IdSessao == idSessao && s.IdCampanha == idCampanha && s.Ativa);
            if (sessao is null)
                throw new DomainException("A sessão não está ativa.");
            ExigirTabuleiroLivre(sessao.Combate2014Json);
            if (idAccountQueDeveEstarNoTurno.HasValue && sessao.IdAccountTurno != idAccountQueDeveEstarNoTurno)
                throw new DomainException("Apenas o Mestre ou o jogador no turno atual pode passar ou encerrar o turno.");

            sessao.DefinirTurnoJogador(idAccountJogador);
            if (idAccountJogador.HasValue)
            {
                var participante = await contexto.SessaoParticipantes
                    .FirstOrDefaultAsync(p => p.IdSessao == idSessao && p.IdAccount == idAccountJogador.Value);
                participante?.DefinirEsquiva(false);
            }

            contexto.SessaoMensagens.AddRange(
                new SessaoMensagem(idSessao, null, CanaisSessao.Chat, aviso),
                new SessaoMensagem(idSessao, null, CanaisSessao.Combate, aviso));
        });

    public async Task AtualizarEstadoCombateParticipanteAsync(
        int idSessao,
        int idAccount,
        bool? esquivando = null,
        bool? escondido = null)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var participante = await contexto.SessaoParticipantes
            .FirstOrDefaultAsync(p => p.IdSessao == idSessao && p.IdAccount == idAccount);

        if (participante == null) return;

        if (esquivando.HasValue)
            participante.DefinirEsquiva(esquivando.Value);

        if (escondido.HasValue)
            participante.DefinirEscondido(escondido.Value);

        await contexto.SaveChangesAsync();
    }

    public async Task AdicionarMensagemAsync(SessaoMensagem mensagem)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        await contexto.SessaoMensagens.AddAsync(mensagem);
        await contexto.SaveChangesAsync();
    }

    public Task AdicionarMensagensDaOperacaoAsync(int idSessao, Guid operacao, IReadOnlyList<SessaoMensagem> mensagens) =>
        ExecutarOperacaoAsync(idSessao, operacao, $"RPGdeMesa:mensagem:{idSessao}:{operacao}", contexto =>
        {
            contexto.SessaoMensagens.AddRange(mensagens.Select(m =>
                new SessaoMensagem(m.IdSessao, m.IdAccount, m.Canal, m.Conteudo)));
            return Task.CompletedTask;
        });

    private Task ExecutarOperacaoAsync(int idSessao, Guid operacao, string recurso, Func<AppDbContext, Task> executar) =>
        ExecutarOperacaoCondicionalAsync(idSessao, operacao, recurso, async contexto =>
        {
            await executar(contexto);
            return true;
        });

    private async Task ExecutarOperacaoCondicionalAsync(int idSessao, Guid operacao, string recurso, Func<AppDbContext, Task<bool>> executar)
    {
        await using var contextoEstrategia = await _factory.CreateDbContextAsync();
        await contextoEstrategia.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var contexto = await _factory.CreateDbContextAsync();
            await using var transacao = await contexto.Database.BeginTransactionAsync();
            await BloquearOperacaoAsync(contexto, recurso);

            var operacoes = contexto.Set<SessaoOperacao>();
            if (!await operacoes.AnyAsync(o => o.IdSessao == idSessao && o.IdOperacao == operacao))
            {
                if (!await executar(contexto))
                    return;

                operacoes.Add(new SessaoOperacao { IdSessao = idSessao, IdOperacao = operacao });
                await contexto.SaveChangesAsync();
            }
            await transacao.CommitAsync();
        });
    }

    private static Task<int> BloquearOperacaoAsync(AppDbContext contexto, string recurso) =>
        contexto.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @resultado int;
            EXEC @resultado = sys.sp_getapplock
                @Resource = {recurso}, @LockMode = 'Exclusive',
                @LockOwner = 'Transaction', @LockTimeout = 5000;
            IF @resultado < 0
                THROW 51001, 'A mesa esta processando uma operacao. Tente novamente.', 1;
            """);

    public async Task<List<SessaoMensagem>> ListarMensagensAsync(int idSessao, int limitePorCanal = 80,
        int? desdeId = null, CancellationToken cancellationToken = default)
    {
        await using var contexto = await _factory.CreateDbContextAsync(cancellationToken);
        var recentes = await contexto.SessaoMensagens
            .AsNoTracking()
            .Include(m => m.Usuario).ThenInclude(u => u!.Perfil)
            .Where(m => m.IdSessao == idSessao && (!desdeId.HasValue || m.IdMensagem >= desdeId.Value))
            .OrderByDescending(m => m.DataEnvio)
            .ThenByDescending(m => m.IdMensagem)
            .Take(limitePorCanal * 4)
            .ToListAsync(cancellationToken);

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
            .Include(p => p.CirculosMagicos)
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

    public async Task SalvarCirculosMagicosAsync(int idPersonagem, IEnumerable<PersonagemCirculoMagico> circulos)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        foreach (var slot in circulos.Where(s => s.CirculoNivel is >= 1 and <= 9))
        {
            var existente = await contexto.PersonagemCirculosMagicos.FindAsync(idPersonagem, slot.CirculoNivel);
            if (existente is null)
            {
                contexto.PersonagemCirculosMagicos.Add(new PersonagemCirculoMagico
                {
                    IdPersonagem = idPersonagem, CirculoNivel = slot.CirculoNivel,
                    TotalSlots = Math.Max(0, slot.TotalSlots), SlotsUsados = Math.Max(0, slot.SlotsUsados),
                    TotalSlotsPendente = slot.TotalSlotsPendente
                });
            }
            else
            {
                existente.TotalSlots = Math.Max(0, slot.TotalSlots);
                existente.SlotsUsados = Math.Max(0, slot.SlotsUsados);
                existente.TotalSlotsPendente = slot.TotalSlotsPendente;
            }
        }
        await contexto.SaveChangesAsync();
    }

    public async Task AplicarDescansoCirculosMagicosAsync(int idPersonagem, bool longo, IEnumerable<int> circulosRecuperados)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        var selecionados = circulosRecuperados.ToHashSet();
        var slots = await contexto.PersonagemCirculosMagicos.Where(s => s.IdPersonagem == idPersonagem).ToListAsync();
        foreach (var slot in slots)
        {
            if (longo) slot.AplicarDescanso();
            else if (selecionados.Contains(slot.CirculoNivel)) slot.SlotsUsados = 0;
        }
        await contexto.SaveChangesAsync();
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
            .Include(p => p.CirculosMagicos)
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

    public async Task<List<Magia>> ListarMagiasCatalogoAsync(CancellationToken cancellationToken = default)
    {
        await using var contexto = await _factory.CreateDbContextAsync(cancellationToken);
        var todas = await contexto.Magias
            .AsNoTracking()
            .OrderBy(m => m.CirculoNivel)
            .ThenBy(m => m.Nome)
            .ThenBy(m => m.IdMagia)
            .ToListAsync(cancellationToken);

        return todas
            .GroupBy(m => m.Nome.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(m => m.CirculoNivel)
            .ThenBy(m => m.Nome)
            .ToList();
    }

    public async Task SalvarMagiasPersonagemAsync(int idPersonagem, IEnumerable<int> idsMagia)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var idsDesejados = idsMagia
            .Where(id => id > 0)
            .Distinct()
            .ToHashSet();

        var existentes = await contexto.PersonagemMagias
            .Where(pm => pm.IdPersonagem == idPersonagem)
            .ToListAsync();

        var paraRemover = existentes
            .Where(pm => !idsDesejados.Contains(pm.IdMagia))
            .ToList();

        if (paraRemover.Any())
            contexto.PersonagemMagias.RemoveRange(paraRemover);

        var idsExistentes = existentes.Select(pm => pm.IdMagia).ToHashSet();
        var paraAdicionar = idsDesejados
            .Where(id => !idsExistentes.Contains(id))
            .Select(id => new PersonagemMagia
            {
                IdPersonagem = idPersonagem,
                IdMagia = id
            })
            .ToList();

        if (paraAdicionar.Any())
            await contexto.PersonagemMagias.AddRangeAsync(paraAdicionar);

        await contexto.SaveChangesAsync();
    }

    // =========================================================================
    // PINOS (TOKENS) SALVOS POR MAPA
    // =========================================================================
    public async Task<List<SessaoPino>> ListarPinosSessaoAsync(int idSessao, CancellationToken cancellationToken = default)
    {
        await using var contexto = await _factory.CreateDbContextAsync(cancellationToken);
        return await contexto.SessaoPinos
            .AsNoTracking()
            .Where(p => p.IdSessao == idSessao)
            .OrderBy(p => p.IdPino)
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<SessaoPino> ConsultaPino(AppDbContext contexto, int idSessao, int? idMapa, string tipo, int idEntidade, int? idPino = null) =>
        contexto.SessaoPinos.Where(p => p.IdSessao == idSessao && p.IdMapa == idMapa &&
            p.TipoEntidade == tipo && p.IdEntidade == idEntidade && (!idPino.HasValue || p.IdPino == idPino))
            .OrderBy(p => p.Instancia);

    public Task SalvarOuMoverPinoAsync(int idSessao, int? idMapa, string tipoEntidade, int idEntidade, int coluna, int linha, bool visivel, int? idPino = null) =>
        ExecutarOperacaoAsync(idSessao, Guid.NewGuid(), $"RPGdeMesa:combate:{idSessao}", async contexto =>
        {
            string? combate = await contexto.SessoesMesa.Where(s => s.IdSessao == idSessao).Select(s => s.Combate2014Json).FirstOrDefaultAsync();
            ExigirTabuleiroLivre(combate);

            var existente = await ConsultaPino(contexto, idSessao, idMapa, tipoEntidade, idEntidade, idPino).FirstOrDefaultAsync();

            if (existente == null)
            {
                if (idPino.HasValue) throw new DomainException("Este pino não está mais no mapa.");
                var novo = new SessaoPino(idSessao, idMapa, tipoEntidade, idEntidade, coluna, linha, visivel);
                contexto.SessaoPinos.Add(novo);
            }
            else
            {
                existente.MoverPara(coluna, linha);
                existente.DefinirVisibilidade(visivel);
            }
        });

    private static void ExigirTabuleiroLivre(string? combate)
    {
        if (ControleCombate2014.DesserializarSeguro(combate).Tabuleiro is not null)
            throw new DomainException("Aguarde a resolução do movimento do tabuleiro físico.");
    }

    public Task CriarInstanciaPinoAsync(int idSessao, int idMapa, int idMestre, string tipoEntidade, int idEntidade, int coluna, int linha, bool visivel, Guid operacao) =>
        ExecutarOperacaoAsync(idSessao, operacao, $"RPGdeMesa:combate:{idSessao}", async contexto =>
        {
            string? combate = await contexto.SessoesMesa.Where(s => s.IdSessao == idSessao).Select(s => s.Combate2014Json).FirstOrDefaultAsync();
            ExigirTabuleiroLivre(combate);
            bool podeCriar = await contexto.SessoesMesa.AnyAsync(s => s.IdSessao == idSessao && s.Ativa &&
                s.IdMapaAtivo == idMapa && s.Campanha.IdAccount == idMestre);
            if (!podeCriar || tipoEntidade is not (TiposPinoSessao.Monstro or TiposPinoSessao.Npc))
                throw new DomainException("Apenas o Mestre pode adicionar cópias de criaturas no mapa ativo.");

            int instancia = (await ConsultaPino(contexto, idSessao, idMapa, tipoEntidade, idEntidade)
                .MaxAsync(p => (int?)p.Instancia) ?? 0) + 1;
            contexto.SessaoPinos.Add(new SessaoPino(idSessao, idMapa, tipoEntidade, idEntidade, coluna, linha, visivel, instancia: instancia));
        });

    public async Task AlterarVisibilidadePinoAsync(int idSessao, int? idMapa, string tipoEntidade, int idEntidade, bool visivel, int? idPino = null)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var existente = await ConsultaPino(contexto, idSessao, idMapa, tipoEntidade, idEntidade, idPino).FirstOrDefaultAsync();

        if (existente == null) return;

        existente.DefinirVisibilidade(visivel);
        await contexto.SaveChangesAsync();
    }

    public Task RemoverPinoAsync(int idSessao, int? idMapa, string tipoEntidade, int idEntidade, int? idPino = null) =>
        ExecutarOperacaoAsync(idSessao, Guid.NewGuid(), $"RPGdeMesa:combate:{idSessao}", async contexto =>
        {
            string? combate = await contexto.SessoesMesa.Where(s => s.IdSessao == idSessao).Select(s => s.Combate2014Json).FirstOrDefaultAsync();
            ExigirTabuleiroLivre(combate);
            var existente = await ConsultaPino(contexto, idSessao, idMapa, tipoEntidade, idEntidade, idPino).FirstOrDefaultAsync();
            if (existente is not null)
                contexto.SessaoPinos.Remove(existente);
        });

    public async Task AtualizarPvPinoAsync(
        int idSessao,
        int? idMapa,
        string tipoEntidade,
        int idEntidade,
        int pvAtual,
        int? idPino = null)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var pino = await ConsultaPino(contexto, idSessao, idMapa, tipoEntidade, idEntidade, idPino).FirstOrDefaultAsync();

        if (pino == null) return;

        pino.DefinirPvAtual(pvAtual);
        await contexto.SaveChangesAsync();
    }

    public async Task AtualizarPvPersonagemAsync(int idPersonagem, int pvAtual)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var personagem = await contexto.Personagens
            .FirstOrDefaultAsync(p => p.IdPersonagem == idPersonagem);

        if (personagem == null) return;

        personagem.PvAtual = Math.Clamp(pvAtual, 0, Math.Max(1, personagem.PvMaximo));
        await contexto.SaveChangesAsync();
    }
}
