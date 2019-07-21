using StoreManager.Models;
using System.Threading.Tasks;

namespace StoreManager.Interfaces
{

  public interface IOrderRepository : IAsyncRepository<SalesOrder>
  {
    Task<SalesOrder> GetByIdWithItemsAsync(object id);
  }

  public interface ICatalogRepository<T> : IAsyncRepository<T> where T : CatalogEntityModel
  { }
}
