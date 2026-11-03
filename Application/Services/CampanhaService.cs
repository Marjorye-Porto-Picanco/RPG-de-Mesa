using RPGdeMesa.Domain.Bestiario;
using RPGdeMesa.Domain.Campanhas;
using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Itens;
using RPGdeMesa.Domain.Npcs;
using RPGdeMesa.Domain.Personagens;
using RPGdeMesa.Domain.Usuarios;

namespace RPGdeMesa.Application.Services;

public class CampanhaService
{
    private readonly ICampanhaRepository _campanhaRepo;
    private readonly IMonstroRepository _monstroRepo;
    private readonly INpcRepository _npcRepo;

    public CampanhaService(
        ICampanhaRepository campanhaRepo,
        IMonstroRepository monstroRepo,
        INpcRepository npcRepo)
    {
        _campanhaRepo = campanhaRepo;
        _monstroRepo = monstroRepo;
        _npcRepo = npcRepo;
    }

    public async Task<(bool Sucesso, string Mensagem)> CriarCampanhaAsync(int idAccount, string nome, string descricao, string codigoConvite)
    {
        try
        {
            if (await _campanhaRepo.CodigoConviteExisteAsync(codigoConvite))
                return (false, "Código de convite gerado já existe. Tente salvar novamente.");

            var novaCampanha = new Campanha(idAccount, nome, descricao, codigoConvite);

            // Vincula automaticamente todos os itens padrão já existentes na base
            var todosItensPadrao = await _campanhaRepo.ListarTodosItensAsync();
            foreach (var item in todosItensPadrao)
            {
                novaCampanha.VincularItem(item.IdItem);
            }

            await _campanhaRepo.AdicionarAsync(novaCampanha);

            return (true, "Campanha forjada com sucesso com a base de itens padrão!");
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

            // Vincula todos os itens padrão da base por padrão
            var todosItensPadrao = await _campanhaRepo.ListarTodosItensAsync();
            foreach (var itemPadrao in todosItensPadrao)
            {
                novaCampanha.VincularItem(itemPadrao.IdItem);
            }

            // Se o usuário passou itens específicos na lista que não sejam os padrões, garante o vínculo
            if (itens != null)
            {
                foreach (var it in itens)
                {
                    try { novaCampanha.VincularItem(it.IdItem); } catch { /* Evita duplicar se já veio dos padrões */ }
                }
            }

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
                foreach (var npc in npcs.Where(n => n.IdNpc > 0))
                    novaCampanha.VincularNpc(npc.IdNpc);
            }

            await _campanhaRepo.AdicionarAsync(novaCampanha);

            if (npcs != null)
            {
                foreach (var npc in npcs.Where(n => n.IdNpc <= 0))
                {
                    npc.PrepararNovoNpc(idAccount, npc.Nome, novaCampanha.IdCampanha);
                    await _npcRepo.AdicionarAsync(npc);
                }
            }

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
        _npcRepo.ListarPorUsuarioAsync(idAccount);

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
            return (false, $"Erro ao vincular monstro: {ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> AdicionarNpcNaCampanhaAsync(int idCampanha, int idNpc)
    {
        try
        {
            var campanha = await _campanhaRepo.ObterPorIdComDetalhesAsync(idCampanha);
            if (campanha == null) return (false, "Campanha não encontrada.");

            campanha.VincularNpc(idNpc);
            await _campanhaRepo.AtualizarAsync(campanha);

            return (true, "NPC vinculado à campanha com sucesso!");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao vincular NPC: {ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> AdicionarItemNaCampanhaAsync(int idCampanha, int idItem)
    {
        try
        {
            var campanha = await _campanhaRepo.ObterPorIdComDetalhesAsync(idCampanha);
            if (campanha == null) return (false, "Campanha não encontrada.");

            campanha.VincularItem(idItem);
            await _campanhaRepo.AtualizarAsync(campanha);

            return (true, "Item vinculado com sucesso!");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao vincular item: {ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> CriarE_VincularMonstroNaCampanhaAsync(int idCampanha, Monstro monstro)
    {
        try
        {
            var campanha = await _campanhaRepo.ObterPorIdComDetalhesAsync(idCampanha);
            if (campanha == null) return (false, "Campanha não encontrada.");

            monstro.PrepararNovoMonstro(campanha.IdAccount);
            await _monstroRepo.AdicionarAsync(monstro);

            campanha.VincularMonstro(monstro.IdMonstro);
            await _campanhaRepo.AtualizarAsync(campanha);

            return (true, "Monstro criado e vinculado com sucesso!");
        }
        catch (Exception ex)
        {
            return (false, $"Erro: {ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> RemoverMonstroDaCampanhaAsync(int idCampanha, int idMonstro)
    {
        try
        {
            await _campanhaRepo.RemoverMonstroCampanhaAsync(idCampanha, idMonstro);
            return (true, "Monstro desvinculado com sucesso!");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao desvincular: {ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> RemoverNpcDaCampanhaAsync(int idCampanha, int idNpc)
    {
        try
        {
            await _campanhaRepo.RemoverNpcCampanhaAsync(idCampanha, idNpc);
            return (true, "NPC desvinculado da campanha com sucesso!");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao desvincular NPC: {ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> RemoverItemDaCampanhaAsync(int idCampanha, int idItem)
    {
        try
        {
            await _campanhaRepo.RemoverItemCampanhaAsync(idCampanha, idItem);
            return (true, "Item desvinculado com sucesso!");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao desvincular item: {ex.Message}");
        }
    }
}