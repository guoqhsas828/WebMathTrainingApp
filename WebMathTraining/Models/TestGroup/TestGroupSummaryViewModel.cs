using System;
using System.Collections.Generic;
using System.Linq;
using StoreManager.Models;
using WebMathTraining.Utilities;

namespace WebMathTraining.Models
{
  public class TestGroupSummaryViewModel
  {
    public TestGroupSummaryViewModel(string team)
    {
      TeamName = team;
      TestResults = new List<TestResultViewModel>();
    }

    public string TeamName { get; set; }

    public List<TestResultViewModel> TestResults { get; set; }
  }

  public class TestResultViewModel
  {
    public int SessionId { get; set; } //Latest session?
    public string SessionName { get; set; }

    public int TotalQuestionAnswered
    {
      get { return TestResult?.TestResults.Count ?? 0; }
    }

    public string CorrectRatio { get { return TestResult.CorrectRatio(); } }

    public UserProfile Tester { get; set; }

    public TestResult TestResult { get; set; }
  }

  public class TestResultDetailViewModel
  {
    public TestResultDetailViewModel()
    {
    }

    public TestResultDetailViewModel(int sessionId, string userName, TestResult result)
    {
      SessionId = sessionId;
      Tester = userName;
      TestResult = result;
    }

    public int SessionId { get; set; }

    public string Tester { get; set; }

    public double MaximumScore
    {
      get { return TestResult?.MaximumScore ?? 0.0; }
    }

    public double FinalScore { get { return TestResult?.FinalScore ?? 0.0; } }

    public DateTime StartTime { get { return TestResult?.TestStarted ?? DateTime.MinValue; } }

    public DateTime EndTime { get { return TestResult?.TestEnded ?? DateTime.MaxValue; } }

    public TestResult TestResult { get; set; }
  }
}
