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
    public const string TxtUploadColumnBreaker = "<brk_/>";
    public const string ClientSupportEmail = "TestAdmG1888@hotmail.com";

    public static readonly int[] Levels = new[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12};

    public static readonly string[] TrialQuestions = new[] 
    {
      "It takes 6 minutes to cut a lumber into 3 pieces, how long does it take to cut the lumber into 6 pieces?",
      "125 * 111 * 5 * 8 * 4 = ?",
      "993 + 994 + 995 + 996 + 997 + 998 + 999 = ?",
      "At the summer camp, 7 pupils eat ice cream every day, 9 pupils eat ice cream every second day and the rest of the pupils don't eat ice cream at all. Yesterday, 13 pupils had ice cream. How many pupils will eat ice cream today?",
      "There are 2018 persons in a row. Each of them is either a liar (who always lies) or a knight (who always tells the truth). Each person says 'There are more liars to my left than knights to my right'. How many liars are there in the row?",
      "If the sum of the positive integer a and 5 is less than 12, what is the sum of all possible values of a?",
      " John is 33 years old. His three sons are 5, 6 and 10 years old. In how many years will the three sons together be as old as their father? ",
      //"Calculate 2 + 2 - 2 + 2 - 2 + 2 - 2 + 2 - 2 + 2",
      //"The human heart beats approximately 70 times per minute. How many beats approximately will it make in half an hour?",
    };

  public static readonly string[] TrialQuestionAnswers = new string[]
    {
      "15",
      "2220000",
      "6972",
      "10",
      "1009",
      "21",
      "4",
      //"4",
      //"2100",
    };

    public static readonly int[] TrialQuestionLevels = new[]
    {
      2,
      3,
      4,
      5,
      5,
      4,
      1,
    };

    public static readonly string[] AvailableChoices = new[] { "A", "B", "C", "D", "E" };

    public enum Month
    {
      January = 1,
      February = 2,
      March = 3,
      April = 4,
      May = 5,
      June = 6,
      July = 7,
      August = 8,
      September = 9,
      October = 10,
      November = 11,
      December = 12
    }
  }
}
