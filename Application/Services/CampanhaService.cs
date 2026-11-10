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

            var todosItens = await _campanhaRepo.ListarTodosItensAsync();
            foreach (var itemPadrao in todosItens.Where(i => i.IdAccount == null))
            {
                novaCampanha.VincularItem(itemPadrao.IdItem);
            }

            var todosMonstros = await _campanhaRepo.ListarTodosMonstrosAsync();
            foreach (var monstroPadrao in todosMonstros.Where(m => m.IdAccount == null))
            {
                novaCampanha.VincularMonstro(monstroPadrao.IdMonstro);
            }

            await _campanhaRepo.AdicionarAsync(novaCampanha);

            return (true, "Campanha forjada com sucesso com a base padrão de monstros e itens!");
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

            var todosItens = await _campanhaRepo.ListarTodosItensAsync();
            foreach (var itemPadrao in todosItens.Where(i => i.IdAccount == null))
            {
                novaCampanha.VincularItem(itemPadrao.IdItem);
            }

            var todosMonstros = await _campanhaRepo.ListarTodosMonstrosAsync();
            foreach (var monstroPadrao in todosMonstros.Where(m => m.IdAccount == null))
            {
                novaCampanha.VincularMonstro(monstroPadrao.IdMonstro);
            }

            if (itens != null)
            {
                foreach (var it in itens)
                {
                    if (!novaCampanha.ItemCampanhas.Any(ic => ic.IdItem == it.IdItem))
                        novaCampanha.VincularItem(it.IdItem);
                }
            }

            if (monstros != null)
            {
                foreach (var m in monstros)
                {
                    if (!novaCampanha.MonstroCampanhas.Any(mc => mc.IdMonstro == m.IdMonstro))
                        novaCampanha.VincularMonstro(m.IdMonstro);
                }
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

    public async Task<List<Monstro>> ObterMonstrosDoUsuarioAsync(int idAccount)
    {
        var todos = await _campanhaRepo.ListarTodosMonstrosAsync();
        return todos.Where(m => m.IdAccount == idAccount).ToList();
    }

    public async Task<List<Monstro>> ObterMonstrosParaSessaoAsync(int idAccount)
    {
        var todos = await _campanhaRepo.ListarTodosMonstrosAsync();
        return todos.Where(m => m.IdAccount == null || m.IdAccount == idAccount).ToList();
    }

    public Task<List<NPC>> ObterNPCsDoUsuarioAsync(int idAccount) =>
        _npcRepo.ListarPorUsuarioAsync(idAccount);

    public async Task<List<Item>> ObterItensDoUsuarioAsync(int idAccount)
    {
        var todos = await _campanhaRepo.ListarTodosItensAsync();
        return todos.Where(i => i.IdAccount == idAccount).ToList();
    }

    public async Task<List<Item>> ObterItensParaSessaoAsync(int idAccount)
    {
        var todos = await _campanhaRepo.ListarTodosItensAsync();
        return todos.Where(i => i.IdAccount == null || i.IdAccount == idAccount).ToList();
    }

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
            await _campanhaRepo.AdicionarMonstroCampanhaAsync(idCampanha, idMonstro);
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
            await _campanhaRepo.AdicionarNpcCampanhaAsync(idCampanha, idNpc);
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
            await _campanhaRepo.AdicionarItemCampanhaAsync(idCampanha, idItem);
            return (true, "Item vinculado com sucesso!");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao vincular item: {ex.Message}");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> AdicionarPersonagemNaCampanhaAsync(int idCampanha, int idPersonagem)
    {
        try
        {
            var campanha = await _campanhaRepo.ObterPorIdComDetalhesAsync(idCampanha);
            if (campanha == null) return (false, "Campanha não encontrada.");

            campanha.VincularPersonagem(idPersonagem);
            await _campanhaRepo.AtualizarAsync(campanha);

            return (true, "Personagem vinculado à campanha com sucesso!");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao vincular personagem: {ex.Message}");
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

            await _campanhaRepo.AdicionarMonstroCampanhaAsync(idCampanha, monstro.IdMonstro);

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