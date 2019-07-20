using StoreManager.Models;

namespace StoreManager.Specifications
{
    public class CustomerOrdersWithItemsSpecification : BaseSpecification<SalesOrder>
    {
        public CustomerOrdersWithItemsSpecification(string buyerId)
            : base(o => o.SalesOrderName == buyerId)
        {
            AddInclude(o => o.SalesOrderLines);
            AddInclude($"{nameof(SalesOrder.SalesOrderLines)}.{nameof(SalesOrderLine.Product)}");
        }
    }

  public class CustomerSpecification : BaseSpecification<Customer>
  {
    public CustomerSpecification(string buyerId)
      : base(o => o.CustomerName == buyerId)
    {
    }
  }
}
