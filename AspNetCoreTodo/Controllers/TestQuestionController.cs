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
using WebMathTraining.Utilities;

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

    public async Task<IActionResult> Index(string nameStr, int levelFilter)
    {
      var currentUser = await _userManager.GetUserAsync(User);
      if (currentUser == null)
      {
        return Challenge();
      }

      var questions = await _context.TestQuestions.Include(tq => tq.QuestionImage)
                            .OrderBy(q => q.ObjectId)
                            .Select(tq => new TestQuestionViewModel { Category = tq.Category, Id = tq.Id, Level = tq.Level, ObjectId = tq.ObjectId,
                              TextAnswer = tq.TestAnswer == null ? null : tq.TestAnswer.TextAnswer, Name = (tq.QuestionImage == null ? "" : tq.QuestionImage.Name)})
                            .Where(q => (levelFilter > 0 ? q.Level == levelFilter : q.Level > 0)).Take(300).ToListAsync();

      if (!string.IsNullOrWhiteSpace(nameStr))
        questions = questions.Where(q => q.Name.ToLower().StartsWith(nameStr.ToLower())).ToList();
     return View(questions); 
    }

    [HttpPost]
    public IActionResult SaveQuestion(Guid modelId, string imageId, QuestionDetailViewModel viewModel)
    {
      var image = _context.TestImages.FirstOrDefault(tim => tim.Id.ToString() == imageId);
      if (image == null)
        return BadRequest($"Test image of id {imageId} not found");

      var entity = _testQuestionService.FindTestQuestion(modelId);
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

      return RedirectToAction(nameof(Index));
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
        return RedirectToAction(nameof(Index));
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
          if (image.DataText != viewModel.QuestionText)
          {
            image.DataText = viewModel.QuestionText;
            _context.Update(image);
            _context.SaveChanges();
          }
        }

        _context.SaveChanges();
      }

      return RedirectToAction("Index");
    }

    public string GetTestQuestionString(string id)
    {
      var image = _context.TestImages.FirstOrDefault(tim => tim.Id.ToString() == id);
      if (image == null || String.Compare(image.ContentType, "Text", StringComparison.InvariantCultureIgnoreCase) != 0)
      {
        return null;
      }

      return EncodingUtil.ByteArrayToStr(image.Data);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new TestQuestionViewModel { Category = TestCategory.Math, ImageContainer = CloudContainer.None, AnswerChoice = TestAnswerType.Text, Level = 4});
    }

    [HttpPost]
    public IActionResult CreateNew(TestQuestionViewModel viewModel)
    {
      var id = Guid.NewGuid();
      var imageId = _testQuestionService.CreateTestImage(string.IsNullOrEmpty(viewModel.QuestionText) ? null : EncodingUtil.StrToByteArray( viewModel.QuestionText), string.IsNullOrEmpty(viewModel.Name) ? id.ToString() : viewModel.Name, viewModel.ImageContainer == CloudContainer.None ? "Text" : "PNG", viewModel.ImageContainer.ToString());
      var retVal = _testQuestionService.CreateOrUpdate(id, imageId, viewModel.Level, viewModel.TextAnswer,
        viewModel.Category, viewModel.AnswerChoice);
      if (viewModel.SessionId > 0)
      {
        //TODO need to get a test session service and add this question to a test session
      }

      return RedirectToAction(nameof(Index), new { levelFilter=viewModel.Level, nameStr = viewModel.Name});
    }

    public IActionResult Delete(Guid id)
    {
      _testQuestionService.DeleteQuestion(id);
      return RedirectToAction(nameof(Index));
    }
  }
}
