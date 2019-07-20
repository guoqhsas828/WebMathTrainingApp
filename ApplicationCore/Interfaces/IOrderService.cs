using StoreManager.Models;
using System.Threading.Tasks;

namespace StoreManager.Interfaces
{
    public interface IOrderService
    {
        Task CreateOrderAsync(int basketId, Address shippingAddress);
    }
}
