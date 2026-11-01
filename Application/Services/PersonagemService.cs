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
}