using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Usuarios;

namespace RPGdeMesa.Application.Services;

public class CampanhaService
{
    private readonly ICampanhaRepository _campanhaRepo;
    private readonly IMonstroRepository _monstroRepo;

    public CampanhaService(ICampanhaRepository campanhaRepo, IMonstroRepository monstroRepo)
    {
        _campanhaRepo = campanhaRepo;
        _monstroRepo = monstroRepo;
    }

    public async Task<(bool Sucesso, string Mensagem)> CriarCampanhaAsync(int idAccount, string nome, string descricao, string codigoConvite)
    {
        try
        {
            if (await _campanhaRepo.CodigoConviteExisteAsync(codigoConvite))
                return (false, "Código de convite gerado já existe. Tente salvar novamente.");

            var novaCampanha = new Campanha(idAccount, nome, descricao, codigoConvite);
            await _campanhaRepo.AdicionarAsync(novaCampanha);

            return (true, "Campanha forjada com sucesso!");
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

    public async Task<(bool Sucesso, string Mensagem)> CriarCampanhaCompletaAsync(
        int idAccount,
        string nome,
        string descricao,
        string codigoConvite,
        List<Monstro> monstros,
        List<NPC> npcs,
        List<Item> itens,
        List<Personagem>? personagens = null)
    {
        try
        {
            if (await _campanhaRepo.CodigoConviteExisteAsync(codigoConvite))
                return (false, "Código de convite gerado já existe. Tente salvar novamente.");

            var novaCampanha = new Campanha(idAccount, nome, descricao, codigoConvite);

            if (monstros != null)
            {
                foreach (var m in monstros)
                    novaCampanha.VincularMonstro(m.IdMonstro);
            }

            if (personagens != null)
            {
                foreach (var p in personagens)
                    novaCampanha.VincularPersonagem(p.IdPersonagem);
            }

            if (npcs != null)
            {
                foreach (var npc in npcs)
                    novaCampanha.AdicionarNpc(npc.Nome, npc.Ocupacao, npc.Alinhamento, npc.Localizacao, npc.Descricao);
            }

            await _campanhaRepo.AdicionarAsync(novaCampanha);
            return (true, "Campanha e recursos forjados com sucesso!");
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

    public Task<Campanha?> ObterDetalhesCampanhaAsync(int idCampanha) =>
        _campanhaRepo.ObterPorIdComDetalhesAsync(idCampanha);

    public Task<List<Campanha>> ObterCampanhasPorUsuarioAsync(int idAccount) =>
        _campanhaRepo.ListarPorMestreAsync(idAccount);

    public Task<List<Campanha>> ObterCampanhasComoJogadorAsync(int idAccount) =>
        _campanhaRepo.ListarPorJogadorAsync(idAccount);

    public Task<List<Monstro>> ObterMonstrosDoUsuarioAsync(int idAccount) =>
        _monstroRepo.ListarPorUsuarioAsync(idAccount);

    public Task<List<NPC>> ObterNPCsDoUsuarioAsync(int idAccount) =>
        _campanhaRepo.ListarTodosNpcsAsync();

    public Task<List<Item>> ObterItensDoUsuarioAsync(int idAccount) =>
        _campanhaRepo.ListarTodosItensAsync();

    public async Task<Campanha?> BuscarPorCodigoAsync(string codigoConvite)
    {
        if (string.IsNullOrWhiteSpace(codigoConvite)) return null;
        return await _campanhaRepo.ObterPorCodigoConviteAsync(codigoConvite);
    }

    public async Task<(bool Sucesso, string Mensagem)> EntrarNaCampanhaPorCodigoAsync(int idAccount, string codigoConvite)
    {
        try
        {
            var campanha = await BuscarPorCodigoAsync(codigoConvite);
            if (campanha == null)
                return (false, "Campanha não encontrada com este código de convite.");

            if (campanha.IdAccount == idAccount)
                return (false, "Você é o Mestre desta campanha!");

            if (await _campanhaRepo.UsuarioJaParticipaAsync(idAccount, campanha.IdCampanha))
                return (false, "Você já está participando desta campanha.");

            var novoVinculo = new UsuarioCampanha(idAccount, campanha.IdCampanha);
            await _campanhaRepo.AdicionarUsuarioNaCampanhaAsync(novoVinculo);

            return (true, $"Você entrou na campanha '{campanha.Nome}' com sucesso! Agora você pode criar ou vincular seu personagem dentro dela.");
        }
        catch (Exception ex)
        {
            string erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            return (false, $"Erro ao entrar na campanha: {erroDetalhado}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> AtualizarCampanhaAsync(Campanha campanha)
    {
        try
        {
            await _campanhaRepo.AtualizarAsync(campanha);
            return (true, "Campanha atualizada com sucesso!");
        }
        catch (Exception ex)
        {
            string erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            return (false, $"Erro ao atualizar: {erroDetalhado}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> AdicionarMonstroNaCampanhaAsync(int idCampanha, int idMonstro)
    {
        try
        {
            var campanha = await _campanhaRepo.ObterPorIdComDetalhesAsync(idCampanha);
            if (campanha == null) return (false, "Campanha não encontrada.");

            campanha.VincularMonstro(idMonstro);
            await _campanhaRepo.AtualizarAsync(campanha);

            return (true, "Monstro vinculado com sucesso!");
        }
        catch (Exception ex)
        {
            string erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            return (false, $"Erro ao vincular monstro: {erroDetalhado}");
        }
    }
}