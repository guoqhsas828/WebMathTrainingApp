using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using WebMathTraining.Models;

namespace WebMathTraining.Utilities
{
  public class EmailUserValidator : IUserValidator<ApplicationUser>
  {
    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
      if (user.Email.ToLower().IndexOf('@') <=0) //EndsWith("@example.com")
      {
        return Task.FromResult(IdentityResult.Failed(new IdentityError
        {
          Code = "EmailDomainError",
          Description = "Invalid email" //example.com email addresses are NOT allowed.
        }));
      }
      else
      {
        return Task.FromResult(IdentityResult.Success);
      }
    }
  }
}
