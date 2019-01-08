using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using WebMathTraining.Data;
using WebMathTraining.Models;
using WebMathTraining.Services;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebMathTraining.Controllers
{
  public class TestQuestionController : Controller
  {
    private readonly TestDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITestQuestionService _testQuestionService;

    public TestQuestionController(TestDbContext context, UserManager<ApplicationUser> userManager, ITestQuestionService service)
    {
      _context = context;
      _userManager = userManager;
      _testQuestionService = service;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
      var currentUser = await _userManager.GetUserAsync(User);
      if (currentUser == null)
      {
        return Challenge();
      }
      var questions = _context.TestQuestions.Where(q => Math.Abs(q.Level - currentUser.ExperienceLevel) <=1).OrderBy(q => q.ObjectId).Select(tq => new TestQuestionViewModel { Category = tq.Category, Id = tq.Id, Level = tq.Level});
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
            TestAnswer = viewModel.CreateTestAnswer(),
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
          entity.TestAnswer = viewModel.CreateTestAnswer();
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
        var testImage = _context.TestImages.Find(id);
        if (testImage == null)
        {
          return RedirectToAction("Index");
        }
        else
        {
          return View(new QuestionDetailViewModel(new TestQuestion { Category = TestCategory.Math, Id = Guid.NewGuid(), Level = 1, QuestionImage = testImage, TestAnswer = new TestAnswer()}));
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
        return View(new QuestionDetailViewModel(entity));
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
            TestAnswer = viewModel.CreateTestAnswer(),
            QuestionImage = image
          };
          _context.TestQuestions.Add(entity);
        }
        else
        {
          entity.Category = viewModel.Category;
          entity.Level = viewModel.Level;
          entity.QuestionImage = image;
          entity.TestAnswer = viewModel.CreateTestAnswer();
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

    public string GetTestQuestionString(string id)
    {
      var image = _context.TestImages.FirstOrDefault(tim => tim.Id.ToString() == id);
      if (image == null || String.Compare(image.ContentType, "Text", StringComparison.InvariantCultureIgnoreCase) != 0)
      {
        return null;
      }

      return TestQuestionService.ByteArrayToStr(image.Data);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new TestQuestionViewModel { Category = TestCategory.Math});
    }

    [HttpPost]
    public IActionResult CreateNew(TestQuestionViewModel viewModel)
    {
      var id = Guid.NewGuid();
      var imageId = _testQuestionService.CreateTestImage( viewModel.QuestionText, id.ToString());
      var retVal = _testQuestionService.CreateOrUpdate(id, imageId, viewModel.Level, viewModel.TextAnswer,
        viewModel.Category, viewModel.AnswerChoice);
      return RedirectToAction("Index");
    }


  }
}
