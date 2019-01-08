using System.Collections;
using System.Collections.Generic;

namespace WebMathTraining
{
  public static class Constants
  {
    public const string AdministratorRole = "Administrator";
    public const string AdminUserName = "SuperAdmin";
    public const string AdminEmail = "TestAdmG1888@hotmail.com";
    public const string AdminPswd = "Test123$";
    public const string TrialUserRole = "Trial";

    public const string ClientSupportEmail = "TestAdmG1888@hotmail.com";

    public static readonly int[] Levels = new[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};

    public static readonly string[] TrialQuestions = new[] 
    {
      "It takes 6 minutes to cut a lumber into 3 pieces, how long does it take to cut the lumber into 6 pieces?",
      "125 * 111 * 5 * 8 * 4 = ?",
      "993 + 994 + 995 + 996 + 997 + 998 + 999 = ?"
    };

  public static readonly string[] TrialQuestionAnswers = new[]
    {
      "15",
      "2220000",
      "6972"
    };
  }
}
