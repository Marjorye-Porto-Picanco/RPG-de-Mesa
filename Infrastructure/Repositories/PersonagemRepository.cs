using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Magias;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Infrastructure.Persistence;

namespace RPGdeMesa.Infrastructure.Repositories;

public class PersonagemRepository : IPersonagemRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public PersonagemRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<Personagem?> ObterPorIdAsync(int idPersonagem)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        var personagem = await contexto.Personagens
            .AsNoTracking()
            .AsSplitQuery()
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

    // Retorna APENAS as fichas base da biblioteca do usuário (IdPersonagemOrigem == null)
    public async Task<List<Personagem>> ListarPorUsuarioAsync(int idAccount)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Personagens
            .AsNoTracking()
            .Include(p => p.Raca)
            .Include(p => p.Antecedente)
            .Where(p => p.IdAccount == idAccount && p.IdPersonagemOrigem == null)
            .ToListAsync();
    }

    public async Task<List<Raca>> ListarRacasAsync()
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Racas.AsNoTracking().OrderBy(r => r.Nome).ToListAsync();
    }

    public async Task<List<Classe>> ListarClassesAsync()
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        return await contexto.Classes.AsNoTracking().OrderBy(c => c.Nome).ToListAsync();
    }

    public async Task AdicionarAsync(Personagem personagem)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        await VincularRacaEAntecedente3FNAsync(contexto, personagem);
        contexto.Personagens.Add(personagem);
        await contexto.SaveChangesAsync();
    }

    public async Task AtualizarAsync(Personagem personagem)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        var existente = await contexto.Personagens.FirstOrDefaultAsync(p => p.IdPersonagem == personagem.IdPersonagem);
        if (existente != null)
        {
            string nomeOriginal = existente.Nome;
            int? idOrigemOriginal = existente.IdPersonagemOrigem;
            int? idRacaOriginal = existente.IdRaca;
            string? racaOriginal = existente.RacaTexto;
            int? idAntecedenteOriginal = existente.IdAntecedente;
            string? antecedenteOriginal = existente.AntecedenteTexto;
            string? alturaOriginal = existente.Altura;
            string? generoOriginal = existente.Genero;

            contexto.Entry(existente).CurrentValues.SetValues(personagem);

            existente.Nome = nomeOriginal;
            existente.IdPersonagemOrigem = idOrigemOriginal;
            existente.IdRaca = idRacaOriginal;
            existente.RacaTexto = racaOriginal;
            existente.IdAntecedente = idAntecedenteOriginal;
            existente.AntecedenteTexto = antecedenteOriginal;
            existente.Altura = alturaOriginal;
            existente.Genero = generoOriginal;

            await contexto.SaveChangesAsync();
        }
    }

    public async Task<int> ClonarPersonagemParaCampanhaAsync(
        int idPersonagemBase,
        int idCampanha,
        Inventario dadosInventario,
        IEnumerable<(int IdItem, int Quantidade)> itens)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        // Verifica se já existe uma instância clonada deste personagem base nesta mesma campanha
        var vinculoExistenteNaCampanha = await contexto.PersonagensCampanha
            .Include(pc => pc.Personagem)
            .FirstOrDefaultAsync(pc =>
                pc.IdCampanha == idCampanha &&
                pc.Personagem != null &&
                (pc.IdPersonagem == idPersonagemBase || pc.Personagem.IdPersonagemOrigem == idPersonagemBase));

        if (vinculoExistenteNaCampanha != null && vinculoExistenteNaCampanha.Personagem?.IdPersonagemOrigem != null)
        {
            // Se já era uma instância exclusiva desta campanha (ex: foi recusada e está reenviando), apenas atualiza o inventário dela
            int idInstanciaExistente = vinculoExistenteNaCampanha.IdPersonagem;
            await SalvarInventarioInternoAsync(contexto, idInstanciaExistente, dadosInventario, itens);
            await contexto.SaveChangesAsync();
            return idInstanciaExistente;
        }

        // Busca a ficha base completa da biblioteca
        var fichaBase = await ObterPorIdAsync(idPersonagemBase);
        if (fichaBase == null)
            throw new InvalidOperationException("Ficha base não encontrada na biblioteca.");

        int idOrigemReal = fichaBase.IdPersonagemOrigem ?? fichaBase.IdPersonagem;

        // Cria uma nova instância isolada de Personagem para a campanha
        var cloneCampanha = new Personagem(
            fichaBase.IdAccount,
            fichaBase.Nome,
            fichaBase.RacaTexto,
            fichaBase.ClasseTexto,
            fichaBase.PvMaximo)
        {
            IdPersonagemOrigem = idOrigemReal,
            IdRaca = fichaBase.IdRaca,
            IdAntecedente = fichaBase.IdAntecedente,
            Nivel = fichaBase.Nivel,
            ExperienciaXp = fichaBase.ExperienciaXp,
            PvAtual = fichaBase.PvAtual,
            PvMaximo = fichaBase.PvMaximo,
            Iniciativa = fichaBase.Iniciativa,
            Deslocamento = fichaBase.Deslocamento,
            AntecedenteTexto = fichaBase.AntecedenteTexto,
            Tendencia = fichaBase.Tendencia,
            Historia = fichaBase.Historia,
            Imagem = fichaBase.Imagem,
            Forca = fichaBase.Forca,
            Destreza = fichaBase.Destreza,
            Constituicao = fichaBase.Constituicao,
            Inteligencia = fichaBase.Inteligencia,
            Sabedoria = fichaBase.Sabedoria,
            Carisma = fichaBase.Carisma,
            Idade = fichaBase.Idade,
            Genero = fichaBase.Genero,
            Altura = fichaBase.Altura,
            Peso = fichaBase.Peso,
            Aparencia = fichaBase.Aparencia,
            Aliados = fichaBase.Aliados,
            Caracteristicas = fichaBase.Caracteristicas,
            Tesouro = fichaBase.Tesouro,
            TracosPersonalidade = fichaBase.TracosPersonalidade,
            PontoDeVista = fichaBase.PontoDeVista,
            Ideais = fichaBase.Ideais,
            Ligacoes = fichaBase.Ligacoes,
            Defeitos = fichaBase.Defeitos,
            Idiomas = fichaBase.Idiomas
        };

        await contexto.Personagens.AddAsync(cloneCampanha);
        await contexto.SaveChangesAsync();

        // Copia Classes (Multiclasse), Salvaguardas, Perícias e Magias da Ficha Base + Salva o Inventário da Campanha
        var classesBase = fichaBase.PersonagemClasses
            .Where(pc => pc.Classe != null)
            .Select(pc => (pc.Classe!.Nome, pc.NivelNaClasse))
            .ToList();

        var salvaguardasBase = fichaBase.Salvaguardas.Select(s => s.Atributo).ToList();
        var periciasBase = fichaBase.Pericias.Select(p => p.NomePericia).ToList();
        var magiasBase = fichaBase.PersonagemMagias
            .Where(pm => pm.Magia != null)
            .Select(pm => pm.Magia!)
            .ToList();

        dadosInventario.IdPersonagem = cloneCampanha.IdPersonagem;

        await SalvarFichaCompleta3FNAsync(
            cloneCampanha.IdPersonagem,
            classesBase,
            salvaguardasBase,
            periciasBase,
            dadosInventario,
            itens,
            magiasBase);

        return cloneCampanha.IdPersonagem;
    }

    private static async Task VincularRacaEAntecedente3FNAsync(AppDbContext contexto, Personagem personagem)
    {
        if (!string.IsNullOrWhiteSpace(personagem.RacaTexto))
        {
            string nomeRaca = personagem.RacaTexto.Trim();
            var racaBanco = await contexto.Racas
                .FirstOrDefaultAsync(r => r.Nome.ToLower() == nomeRaca.ToLower());

            if (racaBanco == null)
            {
                racaBanco = new Raca
                {
                    Nome = nomeRaca,
                    Tamanho = "Médio",
                    DeslocamentoBase = 9,
                    VisaoNoEscuro = false
                };
                await contexto.Racas.AddAsync(racaBanco);
                await contexto.SaveChangesAsync();
            }

            personagem.IdRaca = racaBanco.IdRaca;
            personagem.RacaTexto = racaBanco.Nome;
            personagem.Deslocamento = racaBanco.DeslocamentoBase;
        }

        if (!string.IsNullOrWhiteSpace(personagem.AntecedenteTexto))
        {
            string nomeAntecedente = personagem.AntecedenteTexto.Trim();
            var antecedenteBanco = await contexto.Antecedentes
                .FirstOrDefaultAsync(a => a.Nome.ToLower() == nomeAntecedente.ToLower());

            if (antecedenteBanco == null)
            {
                antecedenteBanco = new Antecedente
                {
                    Nome = nomeAntecedente
                };
                await contexto.Antecedentes.AddAsync(antecedenteBanco);
                await contexto.SaveChangesAsync();
            }

            personagem.IdAntecedente = antecedenteBanco.IdAntecedente;
            personagem.AntecedenteTexto = antecedenteBanco.Nome;
        }
    }

    public async Task SalvarFichaCompleta3FNAsync(
        int idPersonagem,
        IEnumerable<(string NomeClasse, int Nivel)> classes,
        IEnumerable<string> salvaguardas,
        IEnumerable<string> pericias,
        Inventario? dadosInventario,
        IEnumerable<(int IdItem, int Quantidade)>? itens,
        IEnumerable<Magia> magias)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        var classesAntigas = await contexto.PersonagemClasses.Where(pc => pc.IdPersonagem == idPersonagem).ToListAsync();
        contexto.PersonagemClasses.RemoveRange(classesAntigas);
        await contexto.SaveChangesAsync();

        var classesAgrupadas = classes
            .Where(c => !string.IsNullOrWhiteSpace(c.NomeClasse))
            .GroupBy(c => c.NomeClasse.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => (NomeClasse: g.Key, Nivel: g.Sum(x => Math.Max(1, x.Nivel))))
            .ToList();

        foreach (var c in classesAgrupadas)
        {
            var classeBanco = await contexto.Classes
                .FirstOrDefaultAsync(cl => cl.Nome.ToLower() == c.NomeClasse.ToLower());

            if (classeBanco == null)
            {
                classeBanco = new Classe
                {
                    Nome = c.NomeClasse,
                    DadoVida = "d8",
                    AtributoPrimario = "Variável"
                };
                await contexto.Classes.AddAsync(classeBanco);
                await contexto.SaveChangesAsync();
            }

            await contexto.PersonagemClasses.AddAsync(new PersonagemClasse
            {
                IdPersonagem = idPersonagem,
                IdClasse = classeBanco.IdClasse,
                NivelNaClasse = c.Nivel
            });
        }

        var salvAntigas = await contexto.PersonagemSalvaguardas.Where(s => s.IdPersonagem == idPersonagem).ToListAsync();
        contexto.PersonagemSalvaguardas.RemoveRange(salvAntigas);
        foreach (var s in salvaguardas.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            await contexto.PersonagemSalvaguardas.AddAsync(new PersonagemSalvaguarda(idPersonagem, s.Trim()));
        }

        var perAntigas = await contexto.PersonagemPericias.Where(p => p.IdPersonagem == idPersonagem).ToListAsync();
        contexto.PersonagemPericias.RemoveRange(perAntigas);
        foreach (var p in pericias.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            await contexto.PersonagemPericias.AddAsync(new PersonagemPericia(idPersonagem, p.Trim()));
        }

        if (dadosInventario != null && itens != null)
        {
            await SalvarInventarioInternoAsync(contexto, idPersonagem, dadosInventario, itens);
        }

        var magiasAntigas = await contexto.PersonagemMagias.Where(pm => pm.IdPersonagem == idPersonagem).ToListAsync();
        contexto.PersonagemMagias.RemoveRange(magiasAntigas);
        await contexto.SaveChangesAsync();

        foreach (var m in magias.Where(x => !string.IsNullOrWhiteSpace(x.Nome)))
        {
            var novaMagia = new Magia
            {
                Nome = m.Nome.Trim(),
                CirculoNivel = m.CirculoNivel,
                DanoEfeito = m.DanoEfeito,
                Alcance = m.Alcance
            };
            await contexto.Magias.AddAsync(novaMagia);
            await contexto.SaveChangesAsync();

            await contexto.PersonagemMagias.AddAsync(new PersonagemMagia
            {
                IdPersonagem = idPersonagem,
                IdMagia = novaMagia.IdMagia
            });
        }

        await contexto.SaveChangesAsync();
    }

    public async Task SalvarInventarioPersonagem3FNAsync(
        int idPersonagem,
        Inventario dadosInventario,
        IEnumerable<(int IdItem, int Quantidade)> itens)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        await SalvarInventarioInternoAsync(contexto, idPersonagem, dadosInventario, itens);
        await contexto.SaveChangesAsync();
    }

    private static async Task SalvarInventarioInternoAsync(
        AppDbContext contexto,
        int idPersonagem,
        Inventario dadosInventario,
        IEnumerable<(int IdItem, int Quantidade)> itens)
    {
        var inv = await contexto.Inventarios
            .Include(i => i.Itens)
            .FirstOrDefaultAsync(i => i.IdPersonagem == idPersonagem);

        if (inv == null)
        {
            inv = new Inventario
            {
                IdPersonagem = idPersonagem,
                CapacidadeCarga = dadosInventario.CapacidadeCarga,
                PesoAtual = dadosInventario.PesoAtual,
                Dinheiro = dadosInventario.Dinheiro,
                MoedasPC = dadosInventario.MoedasPC,
                MoedasPP = dadosInventario.MoedasPP,
                MoedasPE = dadosInventario.MoedasPE,
                MoedasPO = dadosInventario.MoedasPO,
                MoedasPL = dadosInventario.MoedasPL,
                Notas = dadosInventario.Notas
            };
            await contexto.Inventarios.AddAsync(inv);
            await contexto.SaveChangesAsync();
        }
        else
        {
            inv.CapacidadeCarga = dadosInventario.CapacidadeCarga;
            inv.PesoAtual = dadosInventario.PesoAtual;
            inv.Dinheiro = dadosInventario.Dinheiro;
            inv.MoedasPC = dadosInventario.MoedasPC;
            inv.MoedasPP = dadosInventario.MoedasPP;
            inv.MoedasPE = dadosInventario.MoedasPE;
            inv.MoedasPO = dadosInventario.MoedasPO;
            inv.MoedasPL = dadosInventario.MoedasPL;
            inv.Notas = dadosInventario.Notas;

            contexto.InventarioItens.RemoveRange(inv.Itens);
            await contexto.SaveChangesAsync();
        }

        var itensAgrupados = itens
            .Where(x => x.IdItem > 0)
            .GroupBy(x => x.IdItem)
            .Select(g => new InventarioItem
            {
                IdInventario = inv.IdInventario,
                IdItem = g.Key,
                Quantidade = g.Sum(x => Math.Max(1, x.Quantidade))
            });

        await contexto.InventarioItens.AddRangeAsync(itensAgrupados);
    }

    public async Task RemoverAsync(Personagem personagem)
    {
        await using var contexto = await _factory.CreateDbContextAsync();

        // Se o usuário excluir a ficha base da biblioteca, desvincula o ponteiro das instâncias de campanhas para não dar erro de FK
        var copiasEmCampanhas = await contexto.Personagens
            .Where(p => p.IdPersonagemOrigem == personagem.IdPersonagem)
            .ToListAsync();

        foreach (var copia in copiasEmCampanhas)
        {
            copia.IdPersonagemOrigem = null;
        }
        await contexto.SaveChangesAsync();

        var alvo = await contexto.Personagens.FirstOrDefaultAsync(p => p.IdPersonagem == personagem.IdPersonagem);
        if (alvo != null)
        {
            contexto.Personagens.Remove(alvo);
            await contexto.SaveChangesAsync();
        }
    }
}
