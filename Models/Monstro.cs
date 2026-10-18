using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPGdeMesa.Models
{
    [Table("Monstro")]
    public class Monstro
    {
        [Key]
        [Column("id_monstro")]
        public int IdMonstro { get; set; }

        [Column("id_account")]
        public int IdAccount { get; set; }

        [Column("nome")]
        public string Nome { get; set; } = string.Empty;

        [Column("tipo_monstro")]
        public string? TipoMonstro { get; set; }

        [Column("nivel_desafio")]
        public decimal? NivelDesafio { get; set; } = 1;

        [Column("pv_maximo")]
        public int PvMaximo { get; set; } = 24;

        [Column("classe_armadura")]
        public int ClasseArmadura { get; set; } = 13;

        [Column("tamanho")]
        public string? Tamanho { get; set; } = "Médio";

        [Column("tendencia")]
        public string? Tendencia { get; set; }

        [Column("deslocamento")]
        public string? Deslocamento { get; set; }

        [Column("sentidos")]
        public string? Sentidos { get; set; }

        [Column("resistencias")]
        public string? Resistencias { get; set; }

        [Column("habilidades")]
        public string? Habilidades { get; set; }

        [Column("descricao")]
        public string? Descricao { get; set; }

        [Column("imagem")]
        public string? Imagem { get; set; }

        [ForeignKey("IdAccount")]
        public virtual Usuario? Usuario { get; set; }

        [NotMapped]
        public int Vida
        {
            get => PvMaximo;
            set => PvMaximo = value;
        }

        [NotMapped]
        public int Defesa
        {
            get => ClasseArmadura;
            set => ClasseArmadura = value;
        }

        [NotMapped]
        public string? Tipo
        {
            get => TipoMonstro;
            set => TipoMonstro = value;
        }

        [NotMapped]
        public double Nd
        {
            get => NivelDesafio.HasValue ? (double)NivelDesafio.Value : 1;
            set => NivelDesafio = (decimal)value;
        }

        public virtual ICollection<MonstroCampanha> MonstroCampanhas { get; set; } = new List<MonstroCampanha>();
        //public virtual ICollection<MonstroRegras> MonstroRegras { get; set; } = new List<MonstroRegras>();
        //public virtual ICollection<MonstroCondicao> MonstroCondicoes { get; set; } = new List<MonstroCondicao>();
    }
}