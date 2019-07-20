using System.Security.Principal;

namespace StoreManager.Interfaces
{
    public interface IIdentityParser<T>
    {
        T Parse(IPrincipal principal);
    }
}
