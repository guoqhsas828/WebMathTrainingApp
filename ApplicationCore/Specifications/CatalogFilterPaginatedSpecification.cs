using Microsoft.eShopWeb.ApplicationCore.Entities;
using StoreManager.Models;

namespace StoreManager.Specifications
{
    public class CatalogFilterPaginatedSpecification : BaseSpecification<Product>
    {
        public CatalogFilterPaginatedSpecification(int skip, int take, int? brandId, int? typeId)
            : base(i => (!brandId.HasValue || i.CatalogBrandId == brandId) &&
                (!typeId.HasValue || i.ProductTypeId == typeId))
        {
            ApplyPaging(skip, take);
        }
    }
}
