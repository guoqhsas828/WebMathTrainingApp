﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using StoreManager.Models;

namespace WebMathTraining.Models
{
  public class NextQuestionDetailViewModel
  {
    public NextQuestionDetailViewModel()
    {
    }

    public NextQuestionDetailViewModel(TestQuestion entity, TestImage image, int id, string sessionName, int idx)
    {
      TestQuestion = entity;
      TestSessionName = sessionName;
      QuestionIdx = idx;
      SessionId = id;
      ScorePoint = 0.0;
      Image = image;
    }

    public int SessionId { get; set; }

    public TestCategory Category { get { return TestQuestion?.Category ?? TestCategory.Math; } }

    public TestImage Image { get; set; } //return TestQuestion?.QuestionImage;

    [Required]
    public int ImageId { get { return Image?.Id ?? 0; } }

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

    [DisplayName("The Answer")]
    [Required(ErrorMessage = "Integer Value Only")]
    [Range(-int.MaxValue, int.MaxValue, ErrorMessage = "Must be a interger number")]
    public int NumericAnswer
    {
      get
      {
        if (int.TryParse(TextAnswer, out int retInt))
        {
          return retInt;
        }
        else
          return default(int);
      }
      set
      {
        TextAnswer = value.ToString();
      }
    }

    public string TextAnswer { get; set; }

    public double ScorePoint { get; set; }

    public double PenaltyPoint { get; set; }

  }

  public class ReviewQuestionViewModel
  {
    public ReviewQuestionViewModel()
    {
    }

    public ReviewQuestionViewModel(TestQuestion entity, TestImage image, int id, string sessionName, int idx)
    {
      AnswerChoice = entity.TestAnswer?.AnswerType ?? TestAnswerType.None;
      Image = image; //entity.QuestionImage;
      ImageId = image?.Id ?? 0;
      TestSessionName = sessionName;
      QuestionIdx = idx;
      SessionId = id;
      TheTip = entity.TestAnswer?.AnswerChoice6;//"Sing the Nyan Cat song until it drives everyone crazy";
    }

    public int UserId { get; set; }

    public string TestUserName { get; set; }

    public int SessionId { get; set; }

    public TestImage Image { get; set; }

    [Required]
    public int ImageId
    {
      //get { return Image?.Id ?? 0; }
      get;set;
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
    //private int _imageId;

    #endregion
  }
}
