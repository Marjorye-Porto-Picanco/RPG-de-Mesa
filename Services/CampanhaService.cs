using RPGdeMesa.Components.Pages;
using RPGdeMesa.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RPGdeMesa.Data;


namespace RPGdeMesa.Services
{
    public class CampanhaService
    {
        // Substitui "AppDbContext" pelo nome exato do teu contexto de base de dados
        private readonly AppDbContext _contexto;

        public CampanhaService(AppDbContext contexto)
        {
            _contexto = contexto;
        }

        public async Task<(bool Sucesso, string Mensagem)> CriarCampanhaAsync(int idAccount, string nome, string descricao)
        {
            try
            {
                var novaCampanha = new Campanha
                {
                    IdAccount = idAccount,
                    Nome = nome,
                    Descricao = descricao
                    // A propriedade DataCriacao não precisa ser enviada, pois já assume DateTime.Now no teu modelo
                };

                _contexto.Campanhas.Add(novaCampanha);

                // O SaveChangesAsync executa o INSERT automaticamente na base de dados
                int linhasAfetadas = await _contexto.SaveChangesAsync();

                if (linhasAfetadas > 0)
                    return (true, "Campanha forjada com sucesso!");
                else
                    return (false, "Não foi possível guardar a campanha.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO EF CORE]: {ex.Message}");

                // Se der erro de Foreign Key (ex: IdAccount não existir na tabela de utilizadores), cai aqui
                return (false, "Erro ao comunicar com a base de dados.");
            }
        }
    }
}