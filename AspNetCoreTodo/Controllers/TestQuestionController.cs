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
        var entity = new TestQuestion()
        {
          Id = modelId,
          Category = category,
          Level = level,
          //Width = image.Width,
          //Height = image.Height,
          QuestionImage = image
        };

        _context.TestQuestions.Add(entity);

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
  }
}
