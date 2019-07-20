using StoreManager.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StoreManager.Interfaces
{
    public interface IAsyncRepository<T> where T : BaseEntityModel
    {
        Task<T> GetByIdAsync(int id);
        Task<IReadOnlyList<T>> ListAllAsync();
        Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec);
        Task<T> AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
        Task<int> CountAsync(ISpecification<T> spec);
    }
}
