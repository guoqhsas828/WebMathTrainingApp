using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using WebMathTraining.Data;
using WebMathTraining.Models;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebMathTraining.Controllers
{
  public class TestQuestionController : Controller
  {
    private readonly TestDbContext _context;

    public TestQuestionController(TestDbContext context)
    {
      _context = context;
    }

    [HttpGet]
    public IActionResult Index()
    {
      var questions = _context.TestQuestions.Select(tq => new TestQuestionViewModel { Category = tq.Category, Id = tq.Id, Image = tq.QuestionImage, Level = tq.Level});
      return View(questions);
    }

    public IActionResult SaveQuestion(Guid modelId, TestCategory category, int level, string imageId)
    {
      var image = _context.TestImages.FirstOrDefault(tim => tim.Id.ToString() == imageId);
      if (image != null)
      {
        var entity = _context.TestQuestions.Find(modelId);
        if (entity == null)
        {
          entity = new TestQuestion()
          {
            Id = modelId,
            Category = category,
            Level = level,
            //Width = image.Width,
            //Height = image.Height,
            QuestionImage = image
          };
          _context.TestQuestions.Add(entity);
        }
        else
        {
          entity.Category = category;
          entity.Level = level;
          entity.QuestionImage = image;
        }

        _context.SaveChanges();
      }

      return RedirectToAction("Index");
    }

    [HttpGet]
    public IActionResult Edit(Guid id)
    {
      var entity = _context.TestQuestions.Where(q => q.Id == id).Include(q => q.QuestionImage).FirstOrDefault();
      if (entity == null)
      {
        return RedirectToAction("Index");
      }
      else
      {
        return View(new TestQuestionViewModel { Category = entity.Category, Id = entity.Id, Level = entity.Level, Image = entity.QuestionImage});
      }
    }

    [HttpGet]
    public IActionResult CreateQuestion(Guid id) //Note, the parameter here 
    {
      try
      {
        var testQuestion = _context.TestImages.Find(id);
        if (testQuestion == null)
        {
          return RedirectToAction("Index");
        }
        else
        {
          return View(new TestQuestionViewModel { Category = TestCategory.Math, Id = Guid.NewGuid(), Level = 1, Image = testQuestion });
        }
      }
      catch (Exception ex)
      {
        ModelState.AddModelError("Create Question Error", ex.Message);
      }
      return RedirectToAction("Index");
    }

    [HttpGet]
    public IActionResult Details(Guid id)
    {
      var entity = _context.TestQuestions.Where(q => q.Id == id).Include(q => q.QuestionImage).FirstOrDefault();
      if (entity == null)
      {
        return RedirectToAction("Index");
      }
      else
      {
        return View(new QuestionDetailViewModel { Category = entity.Category, Id = entity.Id, Level = entity.Level, Image = entity.QuestionImage, AnswerChoice = TestAnswerType.SingleChoice});
      }
    }


    public IActionResult SaveDetail(Guid modelId, TestCategory category, int level, string imageId, TestAnswerType aType)
    {
      var image = _context.TestImages.FirstOrDefault(tim => tim.Id.ToString() == imageId);
      if (image != null)
      {
        var entity = _context.TestQuestions.Find(modelId);
        if (entity == null)
        {
          entity = new TestQuestion()
          {
            Id = modelId,
            Category = category,
            Level = level,
            TestAnswer = new TestAnswer() {AnswerType = aType},
            QuestionImage = image
          };
          _context.TestQuestions.Add(entity);
        }
        else
        {
          entity.Category = category;
          entity.Level = level;
          entity.QuestionImage = image;
          entity.TestAnswer = new TestAnswer(){AnswerType = aType};
        }

        _context.SaveChanges();
      }

      return RedirectToAction("Index");
    }

    public IActionResult GetTestImageFile(string id)
    {
      var image = _context.TestImages.FirstOrDefault(tim => tim.Id.ToString() == id);
      if (image == null)
      {
        return null;
      }

      FileResult imageUserFile = File(image.Data, "image/jpeg");
      return imageUserFile;
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new QuestionDetailViewModel { Category = "Math", Id = new Guid(), Level = entity.Level, Image = entity.QuestionImage });
    }

    private static byte[] StrToByteArray(string str)
    {
      var encoding = new System.Text.UTF8Encoding();
      return encoding.GetBytes(str);
    }
  }
}
