using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Data;
using RPGdeMesa.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RPGdeMesa.Services
{
    public class CampanhaService
    {
        private readonly AppDbContext _contexto;

        public CampanhaService(AppDbContext contexto)
        {
            _contexto = contexto;
        }

        public async Task<(bool Sucesso, string Mensagem)> CriarCampanhaAsync(int idAccount, string nome, string descricao, string codigoConvite)
        {
            try
            {
                bool codigoExiste = await _contexto.Campanhas.AnyAsync(c => c.CodigoConvite == codigoConvite);

                if (codigoExiste)
                {
                    return (false, "Código de convite gerado já existe. Tente salvar novamente.");
                }

                var novaCampanha = new Campanha
                {
                    IdAccount = idAccount,
                    Nome = nome,
                    Descricao = descricao,
                    CodigoConvite = codigoConvite,
                    DataCriacao = DateTime.Now
                };

                _contexto.Campanhas.Add(novaCampanha);
                int linhasAfetadas = await _contexto.SaveChangesAsync();

                if (linhasAfetadas > 0)
                    return (true, "Campanha forjada com sucesso!");
                else
                    return (false, "Não foi possível guardar a campanha.");
            }
            catch (Exception ex)
            {
                string erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                System.Diagnostics.Debug.WriteLine($"[ERRO EF CORE]: {erroDetalhado}");
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
            List<Personagem> personagens = null)
        {
            try
            {
                bool codigoExiste = await _contexto.Campanhas.AnyAsync(c => c.CodigoConvite == codigoConvite);

                if (codigoExiste)
                {
                    return (false, "Código de convite gerado já existe. Tente salvar novamente.");
                }

                var novaCampanha = new Campanha
                {
                    IdAccount = idAccount,
                    Nome = nome,
                    Descricao = descricao,
                    CodigoConvite = codigoConvite,
                    DataCriacao = DateTime.Now
                };

                if (monstros != null && monstros.Any())
                {
                    foreach (var monstro in monstros)
                    {
                        novaCampanha.MonstroCampanhas.Add(new MonstroCampanha
                        {
                            IdMonstro = monstro.IdMonstro
                        });
                    }
                }

                if (personagens != null && personagens.Any())
                {
                    foreach (var pers in personagens)
                    {
                        novaCampanha.PersonagemCampanhas.Add(new PersonagemCampanha
                        {
                            IdPersonagem = pers.IdPersonagem
                        });
                    }
                }

                _contexto.Campanhas.Add(novaCampanha);
                await _contexto.SaveChangesAsync();

                if (npcs != null && npcs.Any())
                {
                    foreach (var npc in npcs)
                    {
                        npc.IdCampanha = novaCampanha.IdCampanha;
                        _contexto.Entry(npc).State = EntityState.Modified;
                    }
                    await _contexto.SaveChangesAsync();
                }

                return (true, "Campanha e recursos forjados com sucesso!");
            }
            catch (Exception ex)
            {
                string erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                System.Diagnostics.Debug.WriteLine($"[ERRO EF CORE]: {erroDetalhado}");
                return (false, $"Erro ao comunicar com a base de dados: {erroDetalhado}");
            }
        }

        public async Task<Campanha?> ObterDetalhesCampanhaAsync(int idCampanha)
        {
            _contexto.ChangeTracker.Clear();
            return await _contexto.Campanhas
                .Include(c => c.UsuarioMestre)
                .Include(c => c.PersonagemCampanhas)
                    .ThenInclude(pc => pc.Personagem)
                .Include(c => c.MonstroCampanhas)
                    .ThenInclude(mc => mc.Monstro)
                .FirstOrDefaultAsync(c => c.IdCampanha == idCampanha);
        }

        public async Task<List<Campanha>> ObterCampanhasPorUsuarioAsync(int idAccount)
        {
            _contexto.ChangeTracker.Clear();
            return await _contexto.Campanhas
                .Where(c => c.IdAccount == idAccount)
                .OrderByDescending(c => c.DataCriacao)
                .ToListAsync();
        }

        public async Task<List<Campanha>> ObterCampanhasComoJogadorAsync(int idAccount)
        {
            _contexto.ChangeTracker.Clear();

            var campanhasPorUsuario = await _contexto.UsuariosCampanha
                .Where(uc => uc.IdAccount == idAccount)
                .Select(uc => uc.Campanha)
                .ToListAsync();

            var campanhasPorPersonagem = await _contexto.PersonagensCampanha
                .Where(pc => pc.Personagem != null && pc.Personagem.IdAccount == idAccount)
                .Select(pc => pc.Campanha)
                .ToListAsync();

            return campanhasPorUsuario.Union(campanhasPorPersonagem).Distinct().ToList();
        }

        public async Task<List<Monstro>> ObterMonstrosDoUsuarioAsync(int idAccount)
        {
            return await _contexto.Monstros.ToListAsync();
        }

        public async Task<List<NPC>> ObterNPCsDoUsuarioAsync(int idAccount)
        {
            return await _contexto.Npcs.ToListAsync();
        }

        public async Task<List<Item>> ObterItensDoUsuarioAsync(int idAccount)
        {
            return await _contexto.Itens.ToListAsync();
        }

        public async Task<Campanha?> BuscarPorCodigoAsync(string codigoConvite)
        {
            if (string.IsNullOrWhiteSpace(codigoConvite))
                return null;

            return await _contexto.Campanhas
                .Include(c => c.UsuarioMestre)
                .FirstOrDefaultAsync(c => c.CodigoConvite != null && c.CodigoConvite.ToUpper() == codigoConvite.Trim().ToUpper());
        }

        public async Task<(bool Sucesso, string Mensagem)> EntrarNaCampanhaPorCodigoAsync(int idAccount, string codigoConvite)
        {
            try
            {
                var campanha = await BuscarPorCodigoAsync(codigoConvite);
                if (campanha == null)
                {
                    return (false, "Campanha não encontrada com este código de convite.");
                }

                if (campanha.IdAccount == idAccount)
                {
                    return (false, "Você é o Mestre desta campanha!");
                }

                _contexto.ChangeTracker.Clear();

                bool jaParticipa = await _contexto.UsuariosCampanha
                    .AnyAsync(uc => uc.IdAccount == idAccount && uc.IdCampanha == campanha.IdCampanha);

                if (jaParticipa)
                {
                    return (false, "Você já está participando desta campanha.");
                }
                var novoVinculo = new UsuarioCampanha
                {
                    IdAccount = idAccount,
                    IdCampanha = campanha.IdCampanha
                };

                _contexto.UsuariosCampanha.Add(novoVinculo);
                await _contexto.SaveChangesAsync();

                return (true, $"Você entrou na campanha '{campanha.Nome}' com sucesso! Agora você pode criar ou vincular seu personagem dentro dela.");
            }
            catch (Exception ex)
            {
                string erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return (false, $"Erro ao entrar na campanha: {erroDetalhado}");
            }
        }
    }
}