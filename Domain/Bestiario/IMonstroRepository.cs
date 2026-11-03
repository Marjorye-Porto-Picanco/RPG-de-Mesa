namespace RPGdeMesa.Domain.Bestiario;

public interface IMonstroRepository
{
    Task<Monstro?> ObterPorIdAsync(int idMonstro);
    Task<List<Monstro>> ListarPorUsuarioAsync(int idAccount);
    Task AdicionarAsync(Monstro monstro);
    Task AtualizarAsync(Monstro monstro);
    Task RemoverAsync(Monstro monstro);
}