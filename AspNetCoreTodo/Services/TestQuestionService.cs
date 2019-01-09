using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using WebMathTraining.Data;
using WebMathTraining.Models;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;

namespace WebMathTraining.Services
{
  public interface ITestQuestionService
  {
    Guid CreateTestImage(string testStr, string imageName);
    long CreateOrUpdate(Guid id, Guid imageId, int level, string textAnswer, TestCategory category = TestCategory.Math,
    TestAnswerType answerChoice = TestAnswerType.Text);
    int CountQuestions();
    void DeleteQuestion(Guid id);
  }

  public class TestQuestionService : ITestQuestionService
  {
    private readonly TestDbContext _context;

    public TestQuestionService(TestDbContext context)
    {
      _context = context;
    }

    public int CountQuestions()
    {
      return _context.TestQuestions.Count();
    }

    public Guid CreateTestImage(string imageStr, string imageName)
    {
      if (string.IsNullOrEmpty(imageStr))
        throw new ArgumentException("imageStr");

      var image = _context.TestImages.FirstOrDefault(g => String.Compare(g.Name, imageName, StringComparison.InvariantCultureIgnoreCase) ==0);
      if (image == null)
      {
        image = new TestImage()
        {
          ContentType = "Text",
          Data = StrToByteArray(imageStr),
          Id = Guid.NewGuid(),
          Length = imageStr.Length,
          Name = imageName
        };

        _context.TestImages.Add(image);
        _context.SaveChanges();
      }
      else
      {
        //Image exists, don't update
      }

      return image.Id;
    }

    public long CreateOrUpdate(Guid id, Guid imageId, int level, string textAnswer, TestCategory category = TestCategory.Math,
      TestAnswerType answerChoice = TestAnswerType.Text)
    {
      TestQuestion entity = null;
      try
      {
        var image = _context.TestImages.Find(imageId);
        if (image != null)
        {
          entity = _context.TestQuestions.Find(id);
          if (entity == null)
          {
            entity = new TestQuestion()
            {
              Id = id,
              Category = category,
              Level = level,
              TestAnswer = new TestAnswer() {AnswerType = answerChoice, TextAnswer = textAnswer},
              QuestionImage = image
            };
            _context.TestQuestions.Add(entity);
          }
          else
          {
            entity.Category = category;
            entity.Level = level;
            entity.QuestionImage = image;
            entity.TestAnswer = new TestAnswer()
              {AnswerType = answerChoice, TextAnswer = textAnswer};
          }

          _context.SaveChanges();
        }
      }
      catch (Exception ex)
      {
        return -1;
      }

      return entity?.ObjectId ?? 0;
    }

    public void DeleteQuestion(Guid id)
    {
      var q = _context.TestQuestions.Find(id);
      if (q != null)
      {
        _context.TestQuestions.Remove(q);
        _context.SaveChanges();
      }
    }

    public static byte[] StrToByteArray(string str)
    {
      var encoding = new System.Text.UTF8Encoding();
      return encoding.GetBytes(str);
    }

    public static string ByteArrayToStr(byte[] bytes)
    {
      var encoding = new System.Text.UTF8Encoding();
      return encoding.GetString(bytes, 0, bytes.Length);
    }
  }
}
