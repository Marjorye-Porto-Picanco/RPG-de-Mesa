using System.Collections.Generic;
using RPGdeMesa.Domain.Common;
using RPGdeMesa.Domain.Personagens;

namespace RPGdeMesa.Domain.Magias;

public class Magia : IAggregateRoot
{
    public int IdMagia { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int CirculoNivel { get; set; }
    public string? Escola { get; set; }
    public string? TempoConjuracao { get; set; }
    public string? Alcance { get; set; }
    public string? Componentes { get; set; }
    public string? Duracao { get; set; }
    public string? DanoEfeito { get; set; }

    public virtual ICollection<PersonagemMagia> PersonagemMagias { get; set; } = new List<PersonagemMagia>();
}