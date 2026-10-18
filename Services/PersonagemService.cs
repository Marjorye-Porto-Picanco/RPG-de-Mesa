using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Data;
using RPGdeMesa.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RPGdeMesa.Services
{
    public class PersonagemService
    {
        private readonly AppDbContext _contexto;

        public PersonagemService(AppDbContext contexto)
        {
            _contexto = contexto;
        }

        public async Task<(bool Sucesso, string Mensagem)> CriarPersonagemAsync(int idAccount, Personagem personagem)
        {
            try
            {
                personagem.IdAccount = idAccount;

                if (personagem.PvMaximo <= 0) personagem.PvMaximo = 10;
                personagem.PvAtual = personagem.PvMaximo;

                _contexto.Personagens.Add(personagem);
                int linhasAfetadas = await _contexto.SaveChangesAsync();

                if (linhasAfetadas > 0)
                    return (true, "Personagem forjado com sucesso!");
                else
                    return (false, "Não foi possível guardar o personagem.");
            }
            catch (Exception ex)
            {
                string erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return (false, $"Erro ao comunicar com a base de dados: {erroDetalhado}");
            }
        }

        public async Task<List<Personagem>> ObterPersonagensPorUsuarioAsync(int idAccount)
        {
            _contexto.ChangeTracker.Clear();
            return await _contexto.Personagens
                .Where(p => p.IdAccount == idAccount)
                .ToListAsync();
        }

        public async Task<Personagem?> ObterPorIdAsync(int idPersonagem)
        {
            _contexto.ChangeTracker.Clear();
            return await _contexto.Personagens
                .FirstOrDefaultAsync(p => p.IdPersonagem == idPersonagem);
        }

        public async Task<(bool Sucesso, string Mensagem)> AtualizarPersonagemAsync(Personagem personagem)
        {
            try
            {
                _contexto.Personagens.Update(personagem);
                int linhas = await _contexto.SaveChangesAsync();

                if (linhas > 0)
                    return (true, "Personagem atualizado com sucesso!");
                else
                    return (false, "Nenhuma alteração foi realizada.");
            }
            catch (Exception ex)
            {
                string erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return (false, $"Erro ao atualizar: {erroDetalhado}");
            }
        }
    }
}