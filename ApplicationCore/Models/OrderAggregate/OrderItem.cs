using StoreManager.Models;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate
{

    public class OrderItem : BaseEntityModel
    {
      public int OrderItemId
      {
        get { return Id; }
        set { Id = value; }
      }
      public CatalogItemOrdered ItemOrdered { get; private set; }
        public decimal UnitPrice { get; private set; }
        public int Units { get; private set; }

        private OrderItem()
        {
            // required by EF
        }

        public OrderItem(CatalogItemOrdered itemOrdered, decimal unitPrice, int units)
        {
            ItemOrdered = itemOrdered;
            UnitPrice = unitPrice;
            Units = units;
        }
    }
}
