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

    // Retorna APENAS as fichas base da biblioteca do usuário (IdPersonagemOrigem == null)
    public async Task<List<Personagem>> ListarPorUsuarioAsync(int idAccount)
    {
        _contexto.ChangeTracker.Clear();
        return await _contexto.Personagens
            .AsNoTracking()
            .Include(p => p.Raca)
            .Include(p => p.Antecedente)
            .Where(p => p.IdAccount == idAccount && p.IdPersonagemOrigem == null)
            .ToListAsync();
    }

    public async Task AdicionarAsync(Personagem personagem)
    {
        await VincularRacaEAntecedente3FNAsync(personagem);
        _contexto.Personagens.Add(personagem);
        await _contexto.SaveChangesAsync();
    }

    public async Task AtualizarAsync(Personagem personagem)
    {
        _contexto.ChangeTracker.Clear();
        var existente = await _contexto.Personagens.FirstOrDefaultAsync(p => p.IdPersonagem == personagem.IdPersonagem);
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

            _contexto.Entry(existente).CurrentValues.SetValues(personagem);

            existente.Nome = nomeOriginal;
            existente.IdPersonagemOrigem = idOrigemOriginal;
            existente.IdRaca = idRacaOriginal;
            existente.RacaTexto = racaOriginal;
            existente.IdAntecedente = idAntecedenteOriginal;
            existente.AntecedenteTexto = antecedenteOriginal;
            existente.Altura = alturaOriginal;
            existente.Genero = generoOriginal;

            await _contexto.SaveChangesAsync();
        }
    }

    public async Task<int> ClonarPersonagemParaCampanhaAsync(
        int idPersonagemBase,
        int idCampanha,
        Inventario dadosInventario,
        IEnumerable<(int IdItem, int Quantidade)> itens)
    {
        _contexto.ChangeTracker.Clear();

        // Verifica se já existe uma instância clonada deste personagem base nesta mesma campanha
        var vinculoExistenteNaCampanha = await _contexto.PersonagensCampanha
            .Include(pc => pc.Personagem)
            .FirstOrDefaultAsync(pc =>
                pc.IdCampanha == idCampanha &&
                pc.Personagem != null &&
                (pc.IdPersonagem == idPersonagemBase || pc.Personagem.IdPersonagemOrigem == idPersonagemBase));

        if (vinculoExistenteNaCampanha != null && vinculoExistenteNaCampanha.Personagem?.IdPersonagemOrigem != null)
        {
            // Se já era uma instância exclusiva desta campanha (ex: foi recusada e está reenviando), apenas atualiza o inventário dela
            int idInstanciaExistente = vinculoExistenteNaCampanha.IdPersonagem;
            await SalvarInventarioInternoAsync(idInstanciaExistente, dadosInventario, itens);
            await _contexto.SaveChangesAsync();
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

        await _contexto.Personagens.AddAsync(cloneCampanha);
        await _contexto.SaveChangesAsync();

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

    private async Task VincularRacaEAntecedente3FNAsync(Personagem personagem)
    {
        if (!string.IsNullOrWhiteSpace(personagem.RacaTexto))
        {
            string nomeRaca = personagem.RacaTexto.Trim();
            var racaBanco = await _contexto.Racas
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
                await _contexto.Racas.AddAsync(racaBanco);
                await _contexto.SaveChangesAsync();
            }

            personagem.IdRaca = racaBanco.IdRaca;
            personagem.RacaTexto = racaBanco.Nome;
            personagem.Deslocamento = racaBanco.DeslocamentoBase;
        }

        if (!string.IsNullOrWhiteSpace(personagem.AntecedenteTexto))
        {
            string nomeAntecedente = personagem.AntecedenteTexto.Trim();
            var antecedenteBanco = await _contexto.Antecedentes
                .FirstOrDefaultAsync(a => a.Nome.ToLower() == nomeAntecedente.ToLower());

            if (antecedenteBanco == null)
            {
                antecedenteBanco = new Antecedente
                {
                    Nome = nomeAntecedente
                };
                await _contexto.Antecedentes.AddAsync(antecedenteBanco);
                await _contexto.SaveChangesAsync();
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
        _contexto.ChangeTracker.Clear();

        var classesAntigas = await _contexto.PersonagemClasses.Where(pc => pc.IdPersonagem == idPersonagem).ToListAsync();
        _contexto.PersonagemClasses.RemoveRange(classesAntigas);
        await _contexto.SaveChangesAsync();

        var classesAgrupadas = classes
            .Where(c => !string.IsNullOrWhiteSpace(c.NomeClasse))
            .GroupBy(c => c.NomeClasse.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => (NomeClasse: g.Key, Nivel: g.Sum(x => Math.Max(1, x.Nivel))))
            .ToList();

        foreach (var c in classesAgrupadas)
        {
            var classeBanco = await _contexto.Classes
                .FirstOrDefaultAsync(cl => cl.Nome.ToLower() == c.NomeClasse.ToLower());

            if (classeBanco == null)
            {
                classeBanco = new Classe
                {
                    Nome = c.NomeClasse,
                    DadoVida = "d8",
                    AtributoPrimario = "Variável"
                };
                await _contexto.Classes.AddAsync(classeBanco);
                await _contexto.SaveChangesAsync();
            }

            await _contexto.PersonagemClasses.AddAsync(new PersonagemClasse
            {
                IdPersonagem = idPersonagem,
                IdClasse = classeBanco.IdClasse,
                NivelNaClasse = c.Nivel
            });
        }

        var salvAntigas = await _contexto.PersonagemSalvaguardas.Where(s => s.IdPersonagem == idPersonagem).ToListAsync();
        _contexto.PersonagemSalvaguardas.RemoveRange(salvAntigas);
        foreach (var s in salvaguardas.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            await _contexto.PersonagemSalvaguardas.AddAsync(new PersonagemSalvaguarda(idPersonagem, s.Trim()));
        }

        var perAntigas = await _contexto.PersonagemPericias.Where(p => p.IdPersonagem == idPersonagem).ToListAsync();
        _contexto.PersonagemPericias.RemoveRange(perAntigas);
        foreach (var p in pericias.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            await _contexto.PersonagemPericias.AddAsync(new PersonagemPericia(idPersonagem, p.Trim()));
        }

        if (dadosInventario != null && itens != null)
        {
            await SalvarInventarioInternoAsync(idPersonagem, dadosInventario, itens);
        }

        var magiasAntigas = await _contexto.PersonagemMagias.Where(pm => pm.IdPersonagem == idPersonagem).ToListAsync();
        _contexto.PersonagemMagias.RemoveRange(magiasAntigas);
        await _contexto.SaveChangesAsync();

        foreach (var m in magias.Where(x => !string.IsNullOrWhiteSpace(x.Nome)))
        {
            var novaMagia = new Magia
            {
                Nome = m.Nome.Trim(),
                CirculoNivel = m.CirculoNivel,
                DanoEfeito = m.DanoEfeito,
                Alcance = m.Alcance
            };
            await _contexto.Magias.AddAsync(novaMagia);
            await _contexto.SaveChangesAsync();

            await _contexto.PersonagemMagias.AddAsync(new PersonagemMagia
            {
                IdPersonagem = idPersonagem,
                IdMagia = novaMagia.IdMagia
            });
        }

        await _contexto.SaveChangesAsync();
    }

    public async Task SalvarInventarioPersonagem3FNAsync(
        int idPersonagem,
        Inventario dadosInventario,
        IEnumerable<(int IdItem, int Quantidade)> itens)
    {
        _contexto.ChangeTracker.Clear();
        await SalvarInventarioInternoAsync(idPersonagem, dadosInventario, itens);
        await _contexto.SaveChangesAsync();
    }

    private async Task SalvarInventarioInternoAsync(
        int idPersonagem,
        Inventario dadosInventario,
        IEnumerable<(int IdItem, int Quantidade)> itens)
    {
        var inv = await _contexto.Inventarios
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
            await _contexto.Inventarios.AddAsync(inv);
            await _contexto.SaveChangesAsync();
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

            _contexto.InventarioItens.RemoveRange(inv.Itens);
            await _contexto.SaveChangesAsync();
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

        await _contexto.InventarioItens.AddRangeAsync(itensAgrupados);
    }

    public async Task RemoverAsync(Personagem personagem)
    {
        _contexto.ChangeTracker.Clear();

        // Se o usuário excluir a ficha base da biblioteca, desvincula o ponteiro das instâncias de campanhas para não dar erro de FK
        var copiasEmCampanhas = await _contexto.Personagens
            .Where(p => p.IdPersonagemOrigem == personagem.IdPersonagem)
            .ToListAsync();

        foreach (var copia in copiasEmCampanhas)
        {
            copia.IdPersonagemOrigem = null;
        }
        await _contexto.SaveChangesAsync();

        var alvo = await _contexto.Personagens.FirstOrDefaultAsync(p => p.IdPersonagem == personagem.IdPersonagem);
        if (alvo != null)
        {
            _contexto.Personagens.Remove(alvo);
            await _contexto.SaveChangesAsync();
        }
    }
}