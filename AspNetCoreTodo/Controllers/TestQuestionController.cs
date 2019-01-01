using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
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
      //var images = _context.TestImages.ToList();
      //return View(new TestQuestionViewModel { });
      return View();
    }

    public IActionResult SaveQuestion(Guid questionId)
    {
      return RedirectToAction("Index");
    }

    //[HttpGet]
    //public FileStreamResult ViewImage(Guid id)
    //{

    //  var image = _context.TestImages.FirstOrDefault(m => m.Id == id);

    //  MemoryStream ms = new MemoryStream(image.Data);

    //  return new FileStreamResult(ms, image.ContentType);

    //}

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
          return View(new TestQuestionViewModel { Category = TestCategory.Math, Id = Guid.NewGuid(), Level = 1, Image = testQuestion, QuestionId = id.ToString(), QuestionName = testQuestion.Name });
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
