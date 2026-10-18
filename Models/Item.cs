using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPGdeMesa.Models
{
    [Table("Item")]
    public class Item
    {
        [Key]
        [Column("id_item")]
        public int IdItem { get; set; }

        [Required]
        [StringLength(100)]
        [Column("nome")]
        public string Nome { get; set; } = string.Empty;

        [StringLength(50)]
        [Column("tipo")]
        public string? Tipo { get; set; }

        [Column("peso", TypeName = "decimal(5, 2)")]
        public decimal? Peso { get; set; }

        [Column("preco", TypeName = "decimal(10, 2)")]
        public decimal? Preco { get; set; }

        [StringLength(20)]
        [Column("dano")]
        public string? Dano { get; set; }

        [StringLength(30)]
        [Column("tipo_dano")]
        public string? TipoDano { get; set; }

        [Column("propriedades")]
        public string? Propriedades { get; set; }

        [Column("ca_bonus")]
        public int CaBonus { get; set; } = 0;

        [Column("descricao")]
        public string? Descricao { get; set; }
    }
}