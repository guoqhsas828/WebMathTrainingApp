using System;
using System.Collections.Generic;
using System.Linq;
using WebMathTraining.Utilities;

namespace WebMathTraining.Models
{
  public class AddQuestionViewModel
  {
    public int Idx { get; set; }

    public long QuestionId { get; set; }

    public double ScorePoint { get; set; }

    public double PenaltyPoint { get; set; }

    public Guid TestSessionId { get; set; }

  }

  public class FinalSubmitViewModel
  {
    public string SessionName { get; set; }

    public Guid TestSessionId { get; set; }

    public long SessionObjectId { get; set; }

    public TimeSpan AllowedTimeSpan { get; set; }

    public string AllowedTestTime { get { return AllowedTimeSpan.Display(); } set { return; } }

    public string TimeUsed { get { return (DateTime.Now - TestStart).Display(); } }

    public DateTime TestStart { get; set; }

    public long UserObjectId { get; set; }

    public string UserName { get; set; }
  }

  public class TestInstructionViewModel
  {
    public string SessionName { get; set; }

    public string SessionDescription { get; set; }

    public Guid TestSessionId { get; set; }

    public long SessionObjectId { get; set; }

    public TimeSpan AllowedTimeSpan { get; set; }

    public DateTime TestStart { get; set; }

    public long UserObjectId { get; set; }

    public string UserName { get; set; }

    public int TotalQuestions { get; set; }

    public double TotalScorePoints { get; set; }

    public string AllowedTestTime { get { return AllowedTimeSpan.Display(); } set { return; } }

    public string UsedTestTime { get { return (DateTime.UtcNow - TestStart).Display(); } }
  }
}
