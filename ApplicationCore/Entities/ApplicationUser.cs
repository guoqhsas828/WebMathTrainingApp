using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace StoreManager.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
      public ApplicationUser() : base()
      {
      }

    public bool SupportsBlogFeature()
    {

      return !string.IsNullOrEmpty(PhoneNumber) && PhoneNumber.EndsWith("4456") && PhoneNumber.Contains("82");

    }
  }
}
