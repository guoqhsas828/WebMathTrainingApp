using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebMathTraining.Models;
using User = WebMathTraining.Models.ApplicationUser;

namespace WebMathTraining.Data
{
  public static class ApplicationUserDbSeed
  {
    public static async Task Seed(IServiceProvider serviceProvider)
    {
      UserManager<ApplicationUser> userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
      RoleManager<IdentityRole> roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

      // User Info
      string userName = Constants.AdminEmail;
      string email = Constants.AdminEmail;
      string password = Constants.AdminPswd;
      string role = Constants.AdministratorRole;

      if (await userManager.FindByNameAsync(userName) == null)
      {
        // Create SuperAdmins role if it doesn't exist
        if (await roleManager.FindByNameAsync(role) == null)
        {
          await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Create user account if it doesn't exist
        ApplicationUser user = new ApplicationUser
        {
          UserName = userName,
          Email = email
        };

        IdentityResult result = await userManager.CreateAsync(user, password);

        // Assign role to the user
        if (result.Succeeded)
        {
          await userManager.AddToRoleAsync(user, role);
        }
      }
    }
  }
}
