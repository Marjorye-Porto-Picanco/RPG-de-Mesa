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

        [Column("nome")]
        public string Nome { get; set; } = string.Empty;

        [Column("nivel")]
        public int Nivel { get; set; } = 1;

        [Column("experiencia_xp")]
        public int ExperienciaXp { get; set; } = 0;

        [Column("pv_atual")]
        public int PvAtual { get; set; } = 10;

        [Column("pv_maximo")]
        public int PvMaximo { get; set; } = 10;

        [Column("iniciativa")]
        public int? Iniciativa { get; set; } = 0;

        [Column("deslocamento")]
        public int? Deslocamento { get; set; } = 9;

        [Column("raca_texto")]
        public string? RacaTexto { get; set; }

        [Column("classe")]
        public string? ClasseTexto { get; set; }

        [Column("antecedente_texto")]
        public string? AntecedenteTexto { get; set; }

        [Column("tendencia")]
        public string? Tendencia { get; set; }

        [Column("historia")]
        public string? Historia { get; set; }

        [Column("imagem")]
        public string? Imagem { get; set; } = "img/armaduraprata.png";

        [Column("forca")]
        public int Forca { get; set; } = 10;

        [Column("destreza")]
        public int Destreza { get; set; } = 10;

        [Column("constituicao")]
        public int Constituicao { get; set; } = 10;

        [Column("inteligencia")]
        public int Inteligencia { get; set; } = 10;

        [Column("sabedoria")]
        public int Sabedoria { get; set; } = 10;

        [Column("carisma")]
        public int Carisma { get; set; } = 10;

        [ForeignKey("IdAccount")]
        public virtual Usuario Usuario { get; set; } = null!;

        [Column("idade")]
        public string? Idade { get; set; }

        [Column("genero")]
        public string? Genero { get; set; }

        [Column("altura")]
        public string? Altura { get; set; }

        [Column("peso")]
        public string? Peso { get; set; }

        [Column("aparencia")]
        public string? Aparencia { get; set; }

        [Column("aliados")]
        public string? Aliados { get; set; }

        [Column("caracteristicas")]
        public string? Caracteristicas { get; set; }

        [Column("tesouro")]
        public string? Tesouro { get; set; }

        // Relacionamentos N:N 
        public virtual ICollection<PersonagemCampanha> PersonagemCampanhas { get; set; } = new List<PersonagemCampanha>();

        //outros que vamos tirando do comentario ao ir criando as classes
        // public virtual Raca Raca { get; set; } = null!;
        // public virtual Antecedentes Antecedentes { get; set; } = null!;
        // public virtual Inventario? Inventario { get; set; }
        // public virtual ICollection<PersonagemClasse> PersonagemClasses { get; set; } = new List<PersonagemClasse>();
        // public virtual ICollection<PersonagemAtributos> PersonagemAtributos { get; set; } = new List<PersonagemAtributos>();
        // public virtual ICollection<PersonagemMagia> PersonagemMagias { get; set; } = new List<PersonagemMagia>();
        // public virtual ICollection<PersonagemHabilidades> PersonagemHabilidades { get; set; } = new List<PersonagemHabilidades>();
        // public virtual ICollection<PersonagemAcao> PersonagemAcoes { get; set; } = new List<PersonagemAcao>();
        // public virtual ICollection<PersonagemCondicao> PersonagemCondicoes { get; set; } = new List<PersonagemCondicao>();
    }
}