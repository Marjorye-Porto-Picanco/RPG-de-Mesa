using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPGdeMesa.Models
{
    [Table("Personagem_Campanha")]
    public class PersonagemCampanha
    {
        [Column("id_personagem")]
        public int IdPersonagem { get; set; }

        [Column("id_campanha")]
        public int IdCampanha { get; set; }

        [ForeignKey("IdPersonagem")]
        public virtual Personagem? Personagem { get; set; }

        [ForeignKey("IdCampanha")]
        public virtual Campanha Campanha { get; set; } = null!;
    }
}