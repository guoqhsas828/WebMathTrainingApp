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
      AnswerChoice = entity.TestAnswer?.AnswerType ?? TestAnswerType.None;
      Image = entity.QuestionImage;
      ImageId = entity?.QuestionImage?.Id.ToString();
      TestSessionName = sessionName;
      QuestionIdx = idx;
      SessionId = id;
      TheTip = "Work harder, check-double-triple-check";
      ShowAnswer = false;
    }

    public long UserId { get; set; }

    public Guid SessionId { get; set; }

    public TestImage Image { get; set; }

    [Required]
    public string ImageId
    {
      get
      {
        if (_imageId == null)
        {
          _imageId = Image?.Id.ToString() ?? "";
        }

        return _imageId;
      }
      set { _imageId = value; }
    }

    public TestAnswerType AnswerChoice { get; set; }

    public string QuestionText
    {
      get
      {
        if (_questionText == null)
        {
          if (Image == null || String.Compare(Image.ContentType, "Text", StringComparison.InvariantCultureIgnoreCase) !=
              0)
            _questionText = null;
          else
          {
            var encoding = new System.Text.UTF8Encoding();
            _questionText = encoding.GetString(Image.Data, 0, Image.Data.Length);
          }
        }

        return _questionText;
      }
      set { _questionText = value; }
    }

    public string ShownCorrectAnswer
    {
      get => ShowAnswer ? CorrectAnswer : "";
    }

    public string CorrectAnswer { get; set; }

    public bool IsTextBased => QuestionText != null;

    public string TestSessionName { get; set; }

    public int QuestionIdx { get; set; }

    public double ActualScore { get; set; }

    public string TextAnswer { get; set; }

    public double ScorePoint { get; set; }

    public double PenaltyPoint { get; set; }

    public string TheTip { get; set; }

    public bool ShowAnswer { get; set; }

    #region Data

    private string _questionText;
    private string _imageId;

    #endregion
  }
}
