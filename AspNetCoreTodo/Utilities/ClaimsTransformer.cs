using System;
using Microsoft.AspNetCore.Authentication;
using System.Threading.Tasks;
using System.Security.Claims;

namespace WebMathTraining.Utilities
{
  public class ClaimsTransformer : IClaimsTransformation
  {
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
      string userName = "Joe";
      // new claim for userName
      string issuer = "Simulator";
      string valueType = ClaimValueTypes.String;
      string type1 = ClaimTypes.Country;
      string value1 = "New Zealand";
      string type2 = ClaimTypes.StateOrProvince;
      string value2 = "Wellington";

      // new claim for non-userName
      string value = "Null";

      if (principal != null && !principal.HasClaim(c => c.Type == ClaimTypes.Country))
      {
        if (principal.Identity is ClaimsIdentity identity && identity.IsAuthenticated && identity.Name != null)
        {
          if (identity.Name.ToLower() == userName.ToLower())
          {
            identity.AddClaims(new Claim[]
            {
              new Claim(type1, value1, valueType, issuer),
              new Claim(type2, value2, valueType, issuer)
            });
          }
          else // Not Joe
          {
            identity.AddClaims(new Claim[]
            {
              new Claim(type1, value, valueType, issuer),
              new Claim(type2, value, valueType, issuer),
            });
          }
        }
      }

      return Task.FromResult(principal);
    }
  }
}
