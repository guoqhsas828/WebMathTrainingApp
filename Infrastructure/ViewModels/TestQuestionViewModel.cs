﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using StoreManager.Models;
using WebMathTraining.Services;

namespace WebMathTraining.Models
{
  public class TestQuestionViewModel
  {
    public int Id { get; set; }

    public TestCategory Category { get; set; }

    [Required]
    public int Level { get; set; }

    [DataType(DataType.MultilineText)]
    public string QuestionText { get; set; }

    public TestAnswerType AnswerChoice { get; set; }

    public string TextAnswer { get; set; }

    public double NumericAnswer { get; set; }

    public CloudContainer ImageContainer { get; set; }

    public string Name { get; set; }

    public int SessionId { get; set; }

    public int ObjectId { get; set; }

    public string AnswerTip { get; set; }
  }

  public class QuestionDetailViewModel
  {
    public QuestionDetailViewModel()
    {
    }

    public QuestionDetailViewModel(TestQuestion entity)
    {
      Category = entity.Category;
      Id = entity.Id;
      Level = entity.Level;
      //Image = entity.QuestionImage;
      AnswerChoice = entity.TestAnswer?.AnswerType ?? TestAnswerType.None;
      TextAnswer = entity.TestAnswer?.TextAnswer ?? default(string);
      //QuestionText = Image?.DataText;
      AnswerChoice6 = entity.TestAnswer?.AnswerChoice6;
    }

    public int Id { get; set; }

    public TestCategory Category { get; set; }

    [Required]
    public int Level { get; set; }

    public TestImage Image { get; set; }

    [Required]
    public int ImageId { get { return Image?.Id ?? 0; } }

    public string ImageName { get { return Image?.Name; } }

    public TestAnswerType AnswerChoice { get; set; }

    public string TextAnswer { get; set; }

    public double NumericAnswer { get; set; }

    public string QuestionText
    {
      get { return Image?.DataText; }
    }

    public string AnswerChoice1 { get; set; }

    public string AnswerChoice2 { get; set; }

    public string AnswerChoice3 { get; set; }

    public string AnswerChoice4 { get; set; }

    public string AnswerChoice5 { get; set; }

    [Display(Name="Answer Tip")]
    public string AnswerChoice6 { get; set; }

    public bool IsTextBased => (Image != null &&
                                string.Compare(Image.ContentType, "Text", StringComparison.InvariantCultureIgnoreCase) == 0);

    public bool IsChoiceBased => (AnswerChoice == TestAnswerType.MultipleChoice || AnswerChoice == TestAnswerType.SingleChoice);

    public TestAnswer CreateTestAnswer()
    {
      return new TestAnswer { AnswerType = AnswerChoice, AnswerChoice1 = AnswerChoice1, AnswerChoice2 = AnswerChoice2,
      AnswerChoice3 = AnswerChoice3, AnswerChoice4 = AnswerChoice4, AnswerChoice5 = AnswerChoice5, AnswerChoice6 = AnswerChoice6, TextAnswer = TextAnswer};
    }
  }
}
