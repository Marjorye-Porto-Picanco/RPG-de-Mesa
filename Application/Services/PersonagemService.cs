using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Personagens;

namespace RPGdeMesa.Application.Services;

public class PersonagemService
{
    private readonly IPersonagemRepository _personagemRepo;

    public PersonagemService(IPersonagemRepository personagemRepo) => _personagemRepo = personagemRepo;

    public async Task<(bool Sucesso, string Mensagem)> CriarPersonagemAsync(int idAccount, Personagem personagem)
    {
        try
        {
            personagem.PrepararNovoPersonagem(idAccount);
            await _personagemRepo.AdicionarAsync(personagem);
            return (true, "Personagem forjado com sucesso!");
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

    public async Task<(bool Sucesso, string Mensagem)> SalvarComplementos3FNAsync(
        int idPersonagem,
        IEnumerable<(string NomeClasse, int Nivel)> classes,
        IEnumerable<string> salvaguardas,
        IEnumerable<string> pericias,
        Inventario? dadosInventario,
        IEnumerable<(int IdItem, int Quantidade)>? itens,
        IEnumerable<Magia> magias)
    {
        try
        {
            await _personagemRepo.SalvarFichaCompleta3FNAsync(
                idPersonagem, classes, salvaguardas, pericias, dadosInventario, itens, magias);
            return (true, "Dados da ficha salvos com sucesso!");
        }
        catch (Exception ex)
        {
            string erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            return (false, $"Erro ao salvar detalhes da ficha: {erroDetalhado}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> SalvarInventarioCampanhaAsync(
        int idPersonagem,
        Inventario dadosInventario,
        IEnumerable<(int IdItem, int Quantidade)> itens)
    {
        try
        {
            await _personagemRepo.SalvarInventarioPersonagem3FNAsync(idPersonagem, dadosInventario, itens);
            return (true, "Inventário da campanha vinculado ao personagem com sucesso!");
        }
        catch (Exception ex)
        {
            string erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            return (false, $"Erro ao salvar inventário: {erroDetalhado}");
        }
    }

    // Cria uma instância clonada independente do personagem da biblioteca para uma campanha específica
    public async Task<(bool Sucesso, string Mensagem, int IdPersonagemInstancia)> InstanciarPersonagemParaCampanhaAsync(
        int idPersonagemBase,
        int idCampanha,
        Inventario dadosInventario,
        IEnumerable<(int IdItem, int Quantidade)> itens)
    {
        try
        {
            int idGerado = await _personagemRepo.ClonarPersonagemParaCampanhaAsync(
                idPersonagemBase, idCampanha, dadosInventario, itens);
            return (true, "Ficha instanciada para a campanha com sucesso!", idGerado);
        }
        catch (Exception ex)
        {
            string erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            return (false, $"Erro ao preparar personagem para a campanha: {erroDetalhado}", 0);
        }
    }

    public Task<List<Personagem>> ObterPersonagensPorUsuarioAsync(int idAccount) =>
        _personagemRepo.ListarPorUsuarioAsync(idAccount);

    public Task<Personagem?> ObterPorIdAsync(int idPersonagem) =>
        _personagemRepo.ObterPorIdAsync(idPersonagem);

    public async Task<(bool Sucesso, string Mensagem)> AtualizarPersonagemAsync(Personagem personagem)
    {
        try
        {
            await _personagemRepo.AtualizarAsync(personagem);
            return (true, "Personagem atualizado com sucesso!");
        }
        catch (DomainException dex)
        {
            return (false, dex.Message);
        }
        catch (Exception ex)
        {
            string erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            return (false, $"Erro ao atualizar: {erroDetalhado}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> RemoverPersonagemAsync(int idAccount, int idPersonagem)
    {
        try
        {
            var personagem = await _personagemRepo.ObterPorIdAsync(idPersonagem);
            if (personagem is null || personagem.IdAccount != idAccount)
                return (false, "Personagem não encontrado ou sem permissão para removê-lo.");

            await _personagemRepo.RemoverAsync(personagem);
            return (true, "Personagem removido com sucesso!");
        }
        catch (Exception ex)
        {
            string erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            return (false, $"Erro ao remover personagem: {erroDetalhado}");
        }
    }
}