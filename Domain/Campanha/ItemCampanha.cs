using RPGdeMesa.Domain.Itens;

namespace RPGdeMesa.Domain.Campanhas;

public class ItemCampanha
{
    public int IdItem { get; private set; }
    public int IdCampanha { get; private set; }

    public virtual Item? Item { get; private set; }
    public virtual Campanha? Campanha { get; private set; }

    public ItemCampanha() { }

    public ItemCampanha(int idCampanha, int idItem)
    {
        IdCampanha = idCampanha;
        IdItem = idItem;
    }
}