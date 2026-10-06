using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPGdeMesa.Models
{
    [Table("Perfil")]
    public class Perfil
    {
        [Key]
        [Column("id_perfil")]
        public int IdPerfil { get; set; }

        [Column("id_account")]
        public int IdAccount { get; set; }

        [Column("biografia")]
        public string? Biografia { get; set; }

        [Column("avatar_url")]
        public string? AvatarUrl { get; set; }

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; } = DateTime.Now;

        // Propriedade de navegação
        public virtual Usuario Usuario { get; set; } = null!;
    }
}