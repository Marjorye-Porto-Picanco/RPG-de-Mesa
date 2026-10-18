using System.ComponentModel.DataAnnotations.Schema;

namespace RPGdeMesa.Models
{
    [Table("Usuario_Campanha")]
    public class UsuarioCampanha
    {
        [Column("id_account")]
        public int IdAccount { get; set; }

        [Column("id_campanha")]
        public int IdCampanha { get; set; }

        [ForeignKey("IdAccount")]
        public virtual Usuario Usuario { get; set; } = null!;

        [ForeignKey("IdCampanha")]
        public virtual Campanha Campanha { get; set; } = null!;
    }
}