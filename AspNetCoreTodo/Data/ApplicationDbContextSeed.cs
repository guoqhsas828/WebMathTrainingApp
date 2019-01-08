using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebMathTraining.Models;
using WebMathTraining.Services;
using User = WebMathTraining.Models.ApplicationUser;

namespace WebMathTraining.Data
{
  public static class ApplicationDbContextSeed
  {
    public static async void Seed(IServiceProvider serviceProvider)
    {
      try
      {
        //var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        //var user = await userManager.FindByEmailAsync(Constants.AdminEmail);
        var testSessionService = serviceProvider.GetRequiredService<ITestSessionService>();
        var sessionId = testSessionService.CreateNewSession("Trial Test");
        //testSessionService.RegisterUser(sessionId, user);

        var testQuestionService = serviceProvider.GetRequiredService<ITestQuestionService>();

        for (int idx = 0; idx < Constants.TrialQuestions.Length; ++idx)
        {
          var testImageId = testQuestionService.CreateTestImage(Constants.TrialQuestions[idx], "Trial " + (idx + 1));
          var questionId = Guid.NewGuid();
          var errMsg =
            testQuestionService.CreateOrUpdate(questionId, testImageId, 1, Constants.TrialQuestionAnswers[idx]);
           
          testSessionService.AddQuestion(sessionId, idx+1, 3.0, -1.0);
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(e);
        throw;
      }

      return;
    }

    //public static async Task Seed(IServiceProvider serviceProvider)
      //{
      //  UserManager<ApplicationUser> userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
      //  RoleManager<IdentityRole> roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

      //  // User Info
      //  string userName = Constants.AdminEmail;
      //  string email = Constants.AdminEmail;
      //  string password = Constants.AdminPswd;
      //  string role = Constants.AdministratorRole;

      //  if (await userManager.FindByNameAsync(userName) == null)
      //  {
      //    // Create SuperAdmins role if it doesn't exist
      //    if (await roleManager.FindByNameAsync(role) == null)
      //    {
      //      await roleManager.CreateAsync(new IdentityRole(role));
      //    }

      //    // Create user account if it doesn't exist
      //    ApplicationUser user = new ApplicationUser
      //    {
      //      UserName = userName,
      //      Email = email
      //    };

      //    IdentityResult result = await userManager.CreateAsync(user, password);

      //    // Assign role to the user
      //    if (result.Succeeded)
      //    {
      //      await userManager.AddToRoleAsync(user, role);
      //    }
      //  }
      //}
    }
}
