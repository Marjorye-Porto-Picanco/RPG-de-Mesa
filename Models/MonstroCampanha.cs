using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPGdeMesa.Models
{
    [Table("Monstro_Campanha")]
    public class MonstroCampanha
    {
        [Column("id_campanha")]
        public int IdCampanha { get; set; }

        [ForeignKey("IdCampanha")]
        public virtual Campanha? Campanha { get; set; }

        [Column("id_monstro")]
        public int IdMonstro { get; set; }

        [ForeignKey("IdMonstro")]
        public virtual Monstro? Monstro { get; set; }
    }
}