using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebMathTraining.Models
{
  public class TestGroupSummaryViewModel
  {
    public Guid Id { get; set; } //Test Group Id
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
}
