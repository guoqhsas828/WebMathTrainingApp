using Microsoft.eShopWeb.Infrastructure.Identity;
using StoreManager.Models;

namespace Microsoft.eShopWeb.ApplicationCore.Entities
{
    public class CatalogItem : CatalogEntityModel
    {
      public int CatalogItemId
      {
        get { return Id; }
        set { Id = value; }
      }
    public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string PictureUri { get; set; }
        public int CatalogTypeId { get; set; }
        public CatalogType CatalogType { get; set; }
        public int CatalogBrandId { get; set; }
        public CatalogBrand CatalogBrand { get; set; }
    }
}