using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WebMathTraining.Models
{
  public class TestQuestionViewModel
  {
    public Guid Id { get; set; }

    public TestCategory Category { get; set; }

    [Required]
    public int Level { get; set; }

    [DataType(DataType.MultilineText)]
    public string QuestionText { get; set; }

    public TestAnswerType AnswerChoice { get; set; }

    public string TextAnswer { get; set; }

    public double NumericAnswer { get; set; }

    public string StatusMessage { get; set; }

  }

  public class QuestionDetailViewModel
  {
    public Guid Id { get; set; }

    public TestCategory Category { get; set; }

    [Required]
    public int Level { get; set; }

    public TestImage Image { get; set; }

    [Required]
    public string ImageId { get { return Image?.Id.ToString() ?? ""; } }

    public TestAnswerType AnswerChoice { get; set; }

    public string TextAnswer { get; set; }

    public double NumericAnswer { get; set; }

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

    public bool IsTextBased => (Image != null &&
                                string.Compare(Image.ContentType, "Text", StringComparison.InvariantCultureIgnoreCase) == 0);
  }
}
