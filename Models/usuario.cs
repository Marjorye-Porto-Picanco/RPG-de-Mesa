using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPGdeMesa.Models
{
    [Table("Usuario")]
    public class Usuario
    {
        [Key]
        [Column("id_account")]
        public int IdAccount { get; set; }

        [Column("usar_name")]
        public string UsarName { get; set; } = string.Empty;

        [Column("e_mail")]
        public string EMail { get; set; } = string.Empty;

        [Column("password")]
        public string Password { get; set; } = string.Empty;

        [Column("data_cadastro")]
        public DateTime DataCadastro { get; set; } = DateTime.Now;

        [Column("status_conta")]
        public string StatusConta { get; set; } = "ativo";

        // Relacionamento (1 : 1)
        public virtual Perfil? Perfil { get; set; }

        // Relacionamentos (1 : N)
        // public virtual ICollection<Campanha> CampanhasCriadas { get; set; } = new List<Campanha>();
        public virtual ICollection<Personagem> Personagens { get; set; } = new List<Personagem>();
    }
}