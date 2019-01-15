using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WebMathTraining.Models
{
  public class NextQuestionDetailViewModel
  {
    public NextQuestionDetailViewModel()
    {
    }

    public NextQuestionDetailViewModel(TestQuestion entity, Guid id, string sessionName, int idx)
    {
      TestQuestion = entity;
      TestSessionName = sessionName;
      QuestionIdx = idx;
      SessionId = id;
      ScorePoint = 0.0;
    }

    public Guid SessionId { get; set; }

    public TestCategory Category { get { return TestQuestion?.Category ?? TestCategory.Math; } }

    public TestImage Image { get { return TestQuestion?.QuestionImage; } }

    [Required]
    public string ImageId { get { return Image?.Id.ToString() ?? ""; } }

    public TestAnswerType AnswerChoice { get { return TestQuestion?.TestAnswer?.AnswerType ?? TestAnswerType.None; } }

    public string QuestionText
    {
      get
      {
        if (Image == null ||
            String.Compare(Image.ContentType, "Text", StringComparison.InvariantCultureIgnoreCase) != 0)
          return null;

        var encoding = new System.Text.UTF8Encoding();
        return encoding.GetString(Image.Data, 0, Image.Data.Length);
      }
    }

    public string ChoiceAnswer { get { return TextAnswer; } set { TextAnswer = value; } }

    public bool IsTextBased => (Image != null &&
                                string.Compare(Image.ContentType, "Text", StringComparison.InvariantCultureIgnoreCase) == 0);

    public bool IsChoiceBased => (AnswerChoice == TestAnswerType.MultipleChoice || AnswerChoice == TestAnswerType.SingleChoice);

    public string TestSessionName { get; set; }

    public int QuestionIdx { get; set; }

    public readonly TestQuestion TestQuestion;

    public double NumericAnswer { get; set; }

    public string TextAnswer { get; set; }

    public double ScorePoint { get; set; }

    public double PenaltyPoint { get; set; }

  }

  public class ReviewQuestionViewModel
  {
    public ReviewQuestionViewModel()
    {
    }

    public ReviewQuestionViewModel(TestQuestion entity, Guid id, string sessionName, int idx)
    {
      TestQuestion = entity;
      TestSessionName = sessionName;
      QuestionIdx = idx;
      SessionId = id;
      TheTip = "Work harder, check-double-triple-check";
    }

    public long UserId { get; set; }

    public Guid SessionId { get; set; }

    public TestCategory Category { get { return TestQuestion?.Category ?? TestCategory.Math; } }

    public TestImage Image { get { return TestQuestion?.QuestionImage; } }

    [Required]
    public string ImageId { get { return Image?.Id.ToString() ?? ""; } }

    public TestAnswerType AnswerChoice { get { return TestQuestion?.TestAnswer?.AnswerType ?? TestAnswerType.None; } }

    public string QuestionText
    {
      get
      {
        if (Image == null ||
            String.Compare(Image.ContentType, "Text", StringComparison.InvariantCultureIgnoreCase) != 0)
          return null;

        var encoding = new System.Text.UTF8Encoding();
        return encoding.GetString(Image.Data, 0, Image.Data.Length);
      }
    }

    public string CorrectAnswer { get; set; }

    public bool IsTextBased => (Image != null &&
                                string.Compare(Image.ContentType, "Text", StringComparison.InvariantCultureIgnoreCase) == 0);

    public bool IsChoiceBased => (AnswerChoice == TestAnswerType.MultipleChoice || AnswerChoice == TestAnswerType.SingleChoice);

    public string TestSessionName { get; set; }

    public int QuestionIdx { get; set; }

    public readonly TestQuestion TestQuestion;

    public double ActualScore { get; set; }

    public string TextAnswer { get; set; }

    public double ScorePoint { get; set; }

    public double PenaltyPoint { get; set; }

    public string TheTip { get; set; }
  }
}
