using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StoreManager.Models;

namespace WebMathTraining.ActionFilters
{
  public class UserInAdminRoleAttribute : TypeFilterAttribute
  {
    public UserInAdminRoleAttribute() : base(typeof(ValidUserInAdminRoleFilterImpl))
    {
    }

    private class ValidUserInAdminRoleFilterImpl : IAsyncActionFilter
    {
      public ValidUserInAdminRoleFilterImpl(UserManager<ApplicationUser> userMgr)
      {
        _userManager = userMgr;
      }

      //Runs after the OnAuthentication method  
      public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
      {
        if (context.ActionArguments.ContainsKey("id"))
        {
          var id = context.ActionArguments["id"] as string;
          if (!string.IsNullOrEmpty(id))
          {
            if ((await _userManager.GetUsersInRoleAsync(Constants.AdministratorRole)).All(u => u.Id != id))
            {
              context.Result = new NotFoundObjectResult(id);
              return;
            }
          }
        }
        await next();
      }

      private readonly UserManager<ApplicationUser> _userManager;
    }

  }

  public class ValidUserAttribute : TypeFilterAttribute
  {
    public ValidUserAttribute() : base(typeof(ValidUserFilterImpl))
    {
    }

    private class ValidUserFilterImpl : IAsyncActionFilter
    {
      public ValidUserFilterImpl(UserManager<ApplicationUser> userMgr)
      {
        _userManager = userMgr;
      }

      //Runs after the OnAuthentication method  
      public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
      {
        if (context.ActionArguments.ContainsKey("id"))
        {
          var id = context.ActionArguments["id"] as string;
          if (!string.IsNullOrEmpty(id))
          {
            if ((await _userManager.FindByIdAsync(id)) != null)
            {
              context.Result = new NotFoundObjectResult(id);
              return;
            }
          }
        }
        await next();
      }

      private readonly UserManager<ApplicationUser> _userManager;
    }

  }
}
