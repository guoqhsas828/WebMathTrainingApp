using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
}
