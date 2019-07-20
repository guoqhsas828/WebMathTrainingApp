using StoreManager.Models;
using StoreManager.Pages;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StoreManager.Services
{
  public class Roles : IRoles
  {
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public Roles(RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager)
    {
      _roleManager = roleManager;
      _userManager = userManager;
    }

    public async Task GenerateRolesFromPagesAsync()
    {
      Type t = typeof(MainMenu);
      foreach (Type item in t.GetNestedTypes())
      {
        foreach (var itm in item.GetFields())
        {
          if (itm.Name.Contains("RoleName"))
          {
            string roleName = (string)itm.GetValue(item);
            if (!await _roleManager.RoleExistsAsync(roleName))
              await _roleManager.CreateAsync(new IdentityRole(roleName));
          }
        }
      }
    }

    public async Task AddToRoles(string applicationUserId)
    {
      var user = await _userManager.FindByIdAsync(applicationUserId);
      if (user != null)
      {
        var roles = _roleManager.Roles;
        List<string> listRoles = roles.Select(r => r.Name).ToList();

        //await _userManager.AddToRolesAsync(user, listRoles);

        foreach (var roleName in listRoles)
        {
          if (!(await _userManager.IsInRoleAsync(user, roleName)))
          {
            await _userManager.AddToRoleAsync(user, roleName);
          }
        }
      }
    }

  }
}
