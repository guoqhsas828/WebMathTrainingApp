using WebMathTraining.Models;
using StoreManager.Models;

namespace StoreManager.Specifications
{

    public class CatalogFilterSpecification : BaseSpecification<Product>
    {
        public CatalogFilterSpecification(int? brandId, int? typeId)
            : base(i => (!brandId.HasValue || i.CatalogBrandId == brandId) &&
                (!typeId.HasValue || i.ProductTypeId == typeId))
        {
        }
    }

  public class UserProfileFilterSpecification : BaseSpecification<UserProfile>
  {
    public UserProfileFilterSpecification(string email)
      : base(i => string.Compare(i.Email, email, true) == 0)
    { }
  }

}
