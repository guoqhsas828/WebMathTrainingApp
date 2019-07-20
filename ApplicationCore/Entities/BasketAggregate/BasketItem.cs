namespace StoreManager.Models
{
  public class BasketItem : CatalogEntityModel
  {
    public int BasketItemId { get { return Id; } set { Id = value; } }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public int CatalogItemId { get; set; } //Product Id
  }
}
