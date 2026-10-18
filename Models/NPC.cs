using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPGdeMesa.Models
{
    [Table("NPC")]
    public class NPC
    {
        [Key]
        [Column("id_npc")]
        public int IdNpc { get; set; }

        [Column("id_campanha")]
        public int IdCampanha { get; set; }

        [Required]
        [StringLength(100)]
        [Column("nome")]
        public string Nome { get; set; } = string.Empty;

        [StringLength(50)]
        [Column("ocupacao")]
        public string? Ocupacao { get; set; }

        [StringLength(50)]
        [Column("alinhamento")]
        public string? Alinhamento { get; set; }

        [StringLength(100)]
        [Column("localizacao")]
        public string? Localizacao { get; set; }

        [Column("descricao")]
        public string? Descricao { get; set; }

        [ForeignKey("IdCampanha")]
        public virtual Campanha? Campanha { get; set; }
    }
}