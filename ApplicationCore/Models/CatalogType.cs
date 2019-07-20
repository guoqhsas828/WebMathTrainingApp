using StoreManager.Models;

namespace Microsoft.eShopWeb.Infrastructure.Identity
{
    public class CatalogType : CatalogEntityModel
    {
      public int CatalogTypeId
      {
        get { return Id; }
        set { Id = value; }
      }
      public string Type { get; set; }
    }
}
