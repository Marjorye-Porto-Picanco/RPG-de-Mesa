using Microsoft.EntityFrameworkCore;
using RPGdeMesa.Data;
using RPGdeMesa.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RPGdeMesa.Services
{
    public class MonstroService
    {
        private readonly AppDbContext _contexto;

        public MonstroService(AppDbContext contexto)
        {
            _contexto = contexto;
        }

        public async Task<List<Monstro>> ObterMonstrosPorUsuarioAsync(int idAccount)
        {
            _contexto.ChangeTracker.Clear();
            return await _contexto.Monstros
                .Where(m => m.IdAccount == idAccount)
                .OrderByDescending(m => m.IdMonstro)
                .ToListAsync();
        }

        public async Task<(bool Sucesso, string Mensagem)> CriarMonstroAsync(int idAccount, Monstro monstro)
        {
            try
            {
                monstro.IdAccount = idAccount;

                if (string.IsNullOrWhiteSpace(monstro.Imagem))
                {
                    monstro.Imagem = "img/capacampanha3.png"; 
                }

                _contexto.Monstros.Add(monstro);
                int linhas = await _contexto.SaveChangesAsync();

                if (linhas > 0)
                    return (true, "Monstro forjado e adicionado ao bestiário com sucesso!");
                else
                    return (false, "Não foi possível guardar o monstro.");
            }
            catch (Exception ex)
            {
                string erroDetalhado = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return (false, $"Erro ao salvar monstro: {erroDetalhado}");
            }
        }
        public async Task<Monstro?> ObterPorIdAsync(int idMonstro)
        {
            _contexto.ChangeTracker.Clear();
            return await _contexto.Monstros
                .FirstOrDefaultAsync(m => m.IdMonstro == idMonstro);
        }

    }
}