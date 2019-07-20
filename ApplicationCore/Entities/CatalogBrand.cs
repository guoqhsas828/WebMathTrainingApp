using System.ComponentModel.DataAnnotations;

namespace StoreManager.Models
{
  public class CatalogBrand : BaseEntityModel
  {
    public int CatalogBrandId
    {
      get => Id;
      set => Id = value;
    }

    [MaxLength(128)]
    public string Brand { get; set; }

    [MaxLength(1024)]
    public string Description { get; set; }
  }
}
