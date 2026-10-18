using RPGdeMesa.Components.Pages;
using RPGdeMesa.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
namespace RPGdeMesa.Models
{
    [Table("Campanha")]
    public class Campanha
    {
        [Key]
        [Column("id_campanha")]
        public int IdCampanha { get; set; }

        [Column("id_account")]
        public int IdAccount { get; set; }

        [Column("nome")]
        public string Nome { get; set; } = string.Empty;

        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; } = DateTime.Now;

        [Column("descricao")]
        public string? Descricao { get; set; }

        [Column("codigo_convite")]
        [StringLength(10)]
        public string CodigoConvite { get; set; } = string.Empty;

        [ForeignKey("IdAccount")]
        public virtual Usuario UsuarioMestre { get; set; } = null!;

        // Descomente e ajuste conforme vamos criando o resto
        //public virtual ICollection<Mapa> Mapas { get; set; } = new List<Mapa>();
        //public virtual ICollection<Sessao> Sessoes { get; set; } = new List<Sessao>();
        //public virtual ICollection<NPC> NPCs { get; set; } = new List<NPC>();
        public virtual ICollection<PersonagemCampanha> PersonagemCampanhas { get; set; } = new List<PersonagemCampanha>();
        public virtual ICollection<MonstroCampanha> MonstroCampanhas { get; set; } = new List<MonstroCampanha>();

        [NotMapped]
        public IEnumerable<Personagem> Personagens => PersonagemCampanhas?
            .Where(pc => pc.Personagem != null)
            .Select(pc => pc.Personagem!) ?? Enumerable.Empty<Personagem>();
    }
}