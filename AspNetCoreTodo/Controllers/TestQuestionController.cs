using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.IO;
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
      var questions = _context.TestQuestions.Select(tq => new TestQuestionViewModel { Category = tq.Category, Id = tq.Id, Level = tq.Level});
      return View(questions);
    }

    [HttpPost]
    public IActionResult SaveQuestion(Guid modelId, string imageId, QuestionDetailViewModel viewModel)
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
            Category = viewModel.Category,
            Level = viewModel.Level,
            TestAnswer = new TestAnswer { AnswerType = viewModel.AnswerChoice},
            //Height = image.Height,
            QuestionImage = image
          };
          _context.TestQuestions.Add(entity);
        }
        else
        {
          entity.Category = viewModel.Category;
          entity.Level = viewModel.Level;
          entity.QuestionImage = image;
          entity.TestAnswer = new TestAnswer{ AnswerType = viewModel.AnswerChoice};
        }

        _context.SaveChanges();
      }

      return RedirectToAction("Index");
    }


    [HttpGet]
    public IActionResult CreateQuestion(Guid id) //Note, the parameter here need to match asp-route-***
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
          return View(new QuestionDetailViewModel { Category = TestCategory.Math, Id = Guid.NewGuid(), Level = 1, Image = testQuestion, AnswerChoice = TestAnswerType.Text});
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
        return View(new QuestionDetailViewModel
        {
          Category = entity.Category, Id = entity.Id, Level = entity.Level, Image = entity.QuestionImage,
          AnswerChoice = entity.TestAnswer?.AnswerType ?? TestAnswerType.None,
          TextAnswer = entity.TestAnswer?.TextAnswer ?? default(string)
        });
      }
    }

    [HttpPost]
    public IActionResult SaveDetail(Guid modelId, string imageId, QuestionDetailViewModel viewModel)
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
            Category = viewModel.Category,
            Level = viewModel.Level,
            TestAnswer = new TestAnswer() {AnswerType = viewModel.AnswerChoice, TextAnswer = viewModel.TextAnswer},
            QuestionImage = image
          };
          _context.TestQuestions.Add(entity);
        }
        else
        {
          entity.Category = viewModel.Category;
          entity.Level = viewModel.Level;
          entity.QuestionImage = image;
          entity.TestAnswer = new TestAnswer(){AnswerType = viewModel.AnswerChoice, TextAnswer = viewModel.TextAnswer};
        }

        _context.SaveChanges();
      }

      return RedirectToAction("Index");
    }

    public IActionResult GetTestImageFile(string id) 
    {
      var image = _context.TestImages.FirstOrDefault(tim => tim.Id.ToString() == id);
      if (image == null || String.Compare(image.ContentType, "Text", StringComparison.InvariantCultureIgnoreCase) == 0)
      {
        return null;
      }

      byte[] imageBytes;
      if (String.Compare(image.ContentType, "Text", StringComparison.InvariantCultureIgnoreCase) == 0)
      {
        string base64Str = Convert.ToBase64String(image.Data);
        imageBytes = Convert.FromBase64String(base64Str);
        //using (var ms = new MemoryStream(imageBytes, 0, imageBytes.Length))
        //{
        //  ms.Write(imageBytes, 0, imageBytes.Length);
        //  System.Drawing.Image = image.FromStream(ms, true);
        //}
        //retVal = string.Format("data:image/gif;base64,{0}", base64Str);
      }
      else
      {
        imageBytes = image.Data;
      }

      FileResult imageUserFile = File(imageBytes, "image/jpeg");
      return imageUserFile;
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new TestQuestionViewModel { Category = TestCategory.Math, Id = new Guid()});
    }

    [HttpPost]
    public IActionResult CreateNew(Guid id, TestQuestionViewModel viewModel)
    {
      var testStr = viewModel.QuestionText;
      if (!string.IsNullOrEmpty(testStr))
      {
        var image = new TestImage()
        {
          ContentType = "Text", Data = StrToByteArray(testStr), Id = new Guid(), Length = testStr.Length,
          Name = id.ToString()
        };

        _context.TestImages.Add(image);
        _context.SaveChanges();

        var entity = _context.TestQuestions.Find(id);
        if (entity == null)
        {
          entity = new TestQuestion()
          {
            Id = id,
            Category = viewModel.Category,
            Level = viewModel.Level,
            TestAnswer = new TestAnswer() { AnswerType = viewModel.AnswerChoice, TextAnswer = viewModel.TextAnswer },
            QuestionImage = image
          };
          _context.TestQuestions.Add(entity);
        }
        else
        {
          entity.Category = viewModel.Category;
          entity.Level = viewModel.Level;
          entity.QuestionImage = image;
          entity.TestAnswer = new TestAnswer() { AnswerType = viewModel.AnswerChoice, TextAnswer = viewModel.TextAnswer };
        }

        _context.SaveChanges();
      }

      return RedirectToAction("Index");
    }

    private static byte[] StrToByteArray(string str)
    {
      var encoding = new System.Text.UTF8Encoding();
      return encoding.GetBytes(str);
    }
  }
}
