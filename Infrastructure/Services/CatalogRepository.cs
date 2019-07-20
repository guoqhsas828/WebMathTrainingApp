using StoreManager.Interfaces;
using Microsoft.EntityFrameworkCore;
using StoreManager.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StoreManager.Data;
using Microsoft.eShopWeb.Infrastructure.Data;

namespace Microsoft.eShopWeb.Infrastructure.Services
{
  public class CatalogRepository<T> :  ICatalogRepository<T> where T : CatalogEntityModel
  {
    private readonly CatalogContext _dbContext;

    public CatalogRepository(CatalogContext catalogContext) 
    {
      _dbContext = catalogContext;
    }

    public virtual async Task<T> GetByIdAsync(int id)
    {
      return await _dbContext.Set<T>().FindAsync(id);
    }

    public async Task<IReadOnlyList<T>> ListAllAsync()
    {
      return await _dbContext.Set<T>().ToListAsync();
    }

    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec)
    {
      return await ApplySpecification(spec).ToListAsync();
    }

    public async Task<int> CountAsync(ISpecification<T> spec)
    {
      return await ApplySpecification(spec).CountAsync();
    }

    public async Task<T> AddAsync(T entity)
    {
      _dbContext.Set<T>().Add(entity);
      await _dbContext.SaveChangesAsync();

      return entity;
    }

    public async Task UpdateAsync(T entity)
    {
      _dbContext.Entry(entity).State = EntityState.Modified;
      await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(T entity)
    {
      _dbContext.Set<T>().Remove(entity);
      await _dbContext.SaveChangesAsync();
    }

    private IQueryable<T> ApplySpecification(ISpecification<T> spec)
    {
      return SpecificationEvaluator<T>.GetQuery(_dbContext.Set<T>().AsQueryable(), spec);
    }

  }
}
