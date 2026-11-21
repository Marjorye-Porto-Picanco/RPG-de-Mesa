namespace RPGdeMesa.Domain.Campanhas;

public class MapaCampanha
{
    public int IdMapa { get; set; }
    public int IdCampanha { get; set; }

    public Mapa Mapa { get; set; } = null!;
    public Campanha Campanha { get; set; } = null!;
}