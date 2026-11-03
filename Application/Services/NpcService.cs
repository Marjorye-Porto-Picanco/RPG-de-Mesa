using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Npcs;

namespace RPGdeMesa.Application.Services;

public class NpcService
{
    private readonly INpcRepository _npcRepo;

    public NpcService(INpcRepository npcRepo) => _npcRepo = npcRepo;

    public Task<List<NPC>> ObterNpcsPorUsuarioAsync(int idAccount) =>
        _npcRepo.ListarPorUsuarioAsync(idAccount);

    public Task<List<NPC>> ObterNpcsPorCampanhaAsync(int idCampanha) =>
        _npcRepo.ListarPorCampanhaAsync(idCampanha);

    public Task<NPC?> ObterPorIdAsync(int idNpc) =>
        _npcRepo.ObterPorIdAsync(idNpc);

    public async Task<(bool Sucesso, string Mensagem)> CriarNpcAsync(int idAccount, NPC npc, int? idCampanha = null)
    {
        try
        {
            npc.PrepararNovoNpc(idAccount, npc.Nome, idCampanha);
            await _npcRepo.AdicionarAsync(npc);
            return (true, "NPC forjado e salvo com sucesso!");
        }
        catch (DomainException dex)
        {
            return (false, dex.Message);
        }
        catch (Exception ex)
        {
            string erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            return (false, $"Erro ao comunicar com a base de dados: {erroDetalhado}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> VincularNpcACampanhaAsync(int idNpc, int idCampanha)
    {
        try
        {
            var npc = await _npcRepo.ObterPorIdAsync(idNpc);
            if (npc == null) return (false, "NPC não encontrado.");

            npc.VincularACampanha(idCampanha);
            await _npcRepo.AtualizarAsync(npc);
            return (true, "NPC vinculado à campanha com sucesso!");
        }
        catch (DomainException dex)
        {
            return (false, dex.Message);
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao vincular NPC: {ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> AtualizarNpcAsync(NPC npc)
    {
        try
        {
            await _npcRepo.AtualizarAsync(npc);
            return (true, "NPC atualizado com sucesso!");
        }
        catch (DomainException dex)
        {
            return (false, dex.Message);
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao atualizar: {ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> RemoverNpcAsync(int idNpc)
    {
        try
        {
            var npc = await _npcRepo.ObterPorIdAsync(idNpc);
            if (npc == null) return (false, "NPC não encontrado.");

            await _npcRepo.RemoverAsync(npc);
            return (true, "NPC removido com sucesso!");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao remover: {ex.Message}");
        }
    }
}