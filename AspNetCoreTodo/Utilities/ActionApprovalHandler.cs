using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using WebMathTraining.Models;

namespace WebMathTraining.Utilities
{
  public class ActionApprovalRequirement : IAuthorizationRequirement
  {
    public bool AllowManager { get; set; }

    public bool AllowAssistant { get; set; }
  }

  public class ActionApprovalHandler : AuthorizationHandler<ActionApprovalRequirement>
  {
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
      ActionApprovalRequirement requirement)
    {
      var schedule = context.Resource as ActionPolicy;

      string user = context.User.Identity.Name;

      if (schedule != null &&
          ((requirement.AllowManager && schedule.Manager.Equals(user, StringComparison.OrdinalIgnoreCase))
           || requirement.AllowAssistant && schedule.Assistant.Equals(user, StringComparison.OrdinalIgnoreCase)
          ))
      {
        context.Succeed(requirement);
      }
      else
      {
        context.Fail();
      }
      return Task.CompletedTask;
    }
  }
}
