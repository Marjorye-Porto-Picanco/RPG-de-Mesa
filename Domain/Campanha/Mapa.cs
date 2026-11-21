using RPGdeMesa.Domain.Usuarios;

namespace RPGdeMesa.Domain.Campanhas;

public class Mapa
{
    public int IdMapa { get; set; }
    public int? IdAccount { get; set; }
    public string NomeRegiao { get; set; } = string.Empty;
    public string? Escala { get; set; }
    public string? ImagemUrl { get; set; }

    public Usuario? Usuario { get; set; }
    public ICollection<MapaCampanha> MapaCampanhas { get; set; } = new List<MapaCampanha>();
}