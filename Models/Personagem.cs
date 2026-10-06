using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPGdeMesa.Models
{
    [Table("Personagem")]
    public class Personagem
    {
        [Key]
        [Column("id_personagem")]
        public int IdPersonagem { get; set; }

        [Column("id_account")]
        public int IdAccount { get; set; }

        [Column("id_raca")]
        public int IdRaca { get; set; }

        [Column("id_antecedente")]
        public int IdAntecedente { get; set; }

        [Column("nome")]
        public string Nome { get; set; } = string.Empty;

        [Column("nivel")]
        public int Nivel { get; set; } = 1;

        [Column("experiencia_xp")]
        public int ExperienciaXp { get; set; } = 0;

        [Column("pv_atual")]
        public int PvAtual { get; set; }

        [Column("pv_maximo")]
        public int PvMaximo { get; set; }

        [Column("iniciativa")]
        public int? Iniciativa { get; set; }

        [Column("deslocamento")]
        public int? Deslocamento { get; set; }

        // Relacionamento 1:N (Usuario -> Personagens)
        [ForeignKey("IdAccount")]
        public virtual Usuario Usuario { get; set; } = null!;

        // Relacionamentos Simples (na-comment pay)
        // public virtual Raca Raca { get; set; } = null!;
        // public virtual Antecedentes Antecedentes { get; set; } = null!;
        // public virtual Inventario? Inventario { get; set; }

        // Relacionamentos N:N (na-comment pay)
        // public virtual ICollection<PersonagemCampanha> PersonagemCampanhas { get; set; } = new List<PersonagemCampanha>();
        // public virtual ICollection<PersonagemClasse> PersonagemClasses { get; set; } = new List<PersonagemClasse>();
        // public virtual ICollection<PersonagemAtributos> PersonagemAtributos { get; set; } = new List<PersonagemAtributos>();
        // public virtual ICollection<PersonagemMagia> PersonagemMagias { get; set; } = new List<PersonagemMagia>();
        // public virtual ICollection<PersonagemHabilidades> PersonagemHabilidades { get; set; } = new List<PersonagemHabilidades>();
        // public virtual ICollection<PersonagemAcao> PersonagemAcoes { get; set; } = new List<PersonagemAcao>();
        // public virtual ICollection<PersonagemCondicao> PersonagemCondicoes { get; set; } = new List<PersonagemCondicao>();
    }
}