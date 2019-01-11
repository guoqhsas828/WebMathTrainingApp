using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebMathTraining.Models
{
  public class TestGroupSummaryViewModel
  {
    public Guid SessionId { get; set; } //Latest session?
    public string SessionName { get; set; }
    public string TeamName { get; set; }

    public IList<TestResultViewModel> TestResults { get; set; }
  }

  public class TestResultViewModel
  {
    public ApplicationUser Tester { get; set; }

    public TestResult TestResult { get; set; }
  }

  public class TestResultDetailViewModel
  {
    public TestResultDetailViewModel()
    {
    }

    public TestResultDetailViewModel(string userName, TestResult result)
    {
      Tester = userName;
      TestResult = result;
    }

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
