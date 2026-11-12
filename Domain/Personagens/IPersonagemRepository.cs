namespace RPGdeMesa.Domain.Personagens;

public interface IPersonagemRepository
{
    Task<Personagem?> ObterPorIdAsync(int idPersonagem);
    Task<List<Personagem>> ListarPorUsuarioAsync(int idAccount);
    Task AdicionarAsync(Personagem personagem);
    Task AtualizarAsync(Personagem personagem);
    Task SalvarFichaCompleta3FNAsync(
        int idPersonagem,
        IEnumerable<(string NomeClasse, int Nivel)> classes,
        IEnumerable<string> salvaguardas,
        IEnumerable<string> pericias,
        Inventario? dadosInventario,
        IEnumerable<(int IdItem, int Quantidade)>? itens,
        IEnumerable<Magia> magias);
    Task SalvarInventarioPersonagem3FNAsync(
        int idPersonagem,
        Inventario dadosInventario,
        IEnumerable<(int IdItem, int Quantidade)> itens);
    Task RemoverAsync(Personagem personagem);
}