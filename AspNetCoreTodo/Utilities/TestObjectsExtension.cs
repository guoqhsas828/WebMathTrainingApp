using System;
using System.Collections.Generic;
using System.Linq;
using WebMathTraining.Models;


namespace WebMathTraining.Utilities
{
  public static class TestObjectsExtension
  {
    public static string CorrectRatio(this TestResult testResult)
    {
      return String.Format("{0}%", testResult == null || testResult.MaximumScore == 0.0 ? 0.0 : Math.Round(testResult.FinalScore * 100.0 / testResult.MaximumScore, 2));
    }
  }
}
