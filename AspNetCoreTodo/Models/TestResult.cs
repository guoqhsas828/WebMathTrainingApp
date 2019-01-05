using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebMathTraining.Models
{
  [NotMapped]
  public class TestResult
  {
    public string TestSessionId { get; set; }

    public string UserId { get; set; }

    public double[] FinalScores { get; set; }

    public DateTime TestStarted { get; set; }

    public DateTime TestEnded { get; set; }

  }

  public class TestResultItem
  {
    public string QuestionId { get; set; }

    public string Answer { get; set; }

    public string CorrectAnswer { get; set; }

    public double Score { get; set; }
  }
}
