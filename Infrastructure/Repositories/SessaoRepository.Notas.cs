using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Sessoes;
using RPGdeMesa.Infrastructure.Persistence;

namespace RPGdeMesa.Infrastructure.Repositories;

public partial class SessaoRepository
{
    public async Task<List<SessaoNota>> ListarNotasAsync(int idCampanha, int numeroSessao, int idUsuario)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        await VerificarParticipanteDasNotasAsync(contexto, idCampanha, idUsuario);

        return await contexto.SessaoNotas.AsNoTracking()
            .Where(nota => nota.IdCampanha == idCampanha &&
                           nota.NumeroSessao == numeroSessao &&
                           nota.IdAccount == idUsuario)
            .OrderByDescending(nota => nota.CriadaEmUtc)
            .ThenByDescending(nota => nota.IdNota)
            .ToListAsync();
    }

    public async Task<SessaoNota> AdicionarNotaAsync(SessaoNota nota, int idUsuario)
    {
        await using var contexto = await _factory.CreateDbContextAsync();
        await VerificarParticipanteDasNotasAsync(contexto, nota.IdCampanha, idUsuario);

        if (nota.IdAccount != idUsuario)
            throw new DomainException("Não é possível salvar uma nota em nome de outro usuário.");

        var notaSalva = await contexto.SessaoNotas.AsNoTracking().SingleOrDefaultAsync(existente =>
            existente.IdNota == nota.IdNota &&
            existente.IdCampanha == nota.IdCampanha &&
            existente.NumeroSessao == nota.NumeroSessao &&
            existente.IdAccount == idUsuario);

        if (notaSalva is not null)
            return notaSalva;

        contexto.SessaoNotas.Add(nota);
        await contexto.SaveChangesAsync();
        return nota;
    }

    private static async Task VerificarParticipanteDasNotasAsync(AppDbContext contexto, int idCampanha, int idUsuario)
    {
        if (idUsuario <= 0)
            throw new DomainException("Só participantes da campanha podem acessar as notas da sessão.");

        bool ehMestre = await contexto.Campanhas.AnyAsync(campanha =>
            campanha.IdCampanha == idCampanha && campanha.IdAccount == idUsuario);

        if (ehMestre)
            return;

        bool ehJogador = await contexto.UsuariosCampanha.AnyAsync(uc =>
            uc.IdCampanha == idCampanha && uc.IdAccount == idUsuario);

        if (!ehJogador)
        {
            ehJogador = await contexto.PersonagensCampanha.AnyAsync(pc =>
                pc.IdCampanha == idCampanha &&
                pc.Personagem != null &&
                pc.Personagem.IdAccount == idUsuario);
        }

        if (!ehJogador)
            throw new DomainException("Só participantes da campanha podem acessar as notas da sessão.");
    }
}
