namespace RPGdeMesa.Domain.Npcs;

public interface INpcRepository
{
    Task<NPC?> ObterPorIdAsync(int idNpc);
    Task<List<NPC>> ListarPorUsuarioAsync(int idAccount);
    Task<List<NPC>> ListarPorCampanhaAsync(int idCampanha);
    Task AdicionarAsync(NPC npc);
    Task AtualizarAsync(NPC npc);
    Task RemoverAsync(NPC npc);
}