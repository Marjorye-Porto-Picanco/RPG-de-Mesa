namespace RPGdeMesa.Domain.Personagens;

public interface IPersonagemRepository
{
    Task<Personagem?> ObterPorIdAsync(int idPersonagem);
    Task<List<Personagem>> ListarPorUsuarioAsync(int idAccount);
    Task AdicionarAsync(Personagem personagem);
    Task AtualizarAsync(Personagem personagem);
    Task RemoverAsync(Personagem personagem);
}