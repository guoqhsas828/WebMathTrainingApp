using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace StoreManager.Models
{
  public class Product : BaseEntityModel
  {
    public int ProductId
    {
      get { return Id; }
      set { Id = value; }
    }

    [Required] [MaxLength(128)] public string ProductName { get; set; }
    [MaxLength(128)] public string ProductCode { get; set; }
    [MaxLength(128)] public string Barcode { get; set; }
    [MaxLength(1024)] public string Description { get; set; }
    [MaxLength(1024)] public string ProductImageUrl { get; set; }
    [Display(Name = "UOM")] public int UnitOfMeasureId { get; set; }
    public double DefaultBuyingPrice { get; set; } = 0.0;
    public double DefaultSellingPrice { get; set; } = 0.0;
    [Display(Name = "Branch")] public int BranchId { get; set; }
    [Display(Name = "Currency")] public int CurrencyId { get; set; }

    public int ProductTypeId { get; set; }
    public ProductType ProductType { get; set; }
    public int CatalogBrandId { get; set; }
    public CatalogBrand CatalogBrand { get; set; }
  }
}
