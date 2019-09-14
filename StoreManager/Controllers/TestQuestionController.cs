using System;
using System.Collections.Generic;
using WebMathTraining.Models;
using StoreManager.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using StoreManager.Models;
using WebMathTraining.Services;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WebMathTraining.Utilities;

namespace StoreManager.Controllers
{
  [Authorize(Roles = Constants.AdministratorRole)]
  public class TestQuestionController : Controller
  {
    private readonly ICatalogRepository<TestQuestion> _context;
    private readonly ICatalogRepository<TestImage> _imageContext;
    private readonly ITestQuestionService<int> _testQuestionService;

    public TestQuestionController(ICatalogRepository<TestQuestion> context, 
      ICatalogRepository<TestImage> imageContext, ITestQuestionService<int> testQuestionService)
      : base()
    {
      _context = context;
      _imageContext = imageContext;
      _testQuestionService = testQuestionService;
    }

    public IActionResult Index()
    {
      return View();
    }

    [HttpPost]
    public IActionResult Insert()
    {
      return View(new TestQuestionViewModel { AnswerChoice = TestAnswerType.SingleChoice, Category = TestCategory.Math, Level = 4});
    }

    public async Task<IActionResult> Details(int id)
    {
      var entity = await _context.GetByIdAsync(id);
      if (entity == null)
      {
        return RedirectToAction(nameof(Index));
      }
      else
      {
        var image = await _imageContext.GetByIdAsync(entity.QuestionImageId);

        return View(new QuestionDetailViewModel(entity) {Image = image});
      }
    }

    public IActionResult CreateQuestion(int id) //Note, the parameter here need to match asp-route-***
    {
      try
      {
        var testImage = _imageContext.GetByIdAsync(id).Result;
        if (testImage == null)
        {
          return RedirectToAction("Index");
        }
        else
        {
          return View(new QuestionDetailViewModel(new TestQuestion
              {Category = TestCategory.Math, Level = 1, QuestionImageId = testImage.Id, TestAnswer = new TestAnswer()})
            {Image = testImage});
        }
      }
      catch (Exception ex)
      {
        ModelState.AddModelError("Create Question Error", ex.Message);
      }

      return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> SaveDetail(int modelId, int imageId, QuestionDetailViewModel viewModel)
    {
      var image = await _imageContext.GetByIdAsync(imageId);
      if (image != null)
      {
        var entity = await _context.GetByIdAsync(modelId);
        if (entity == null)
        {
          entity = new TestQuestion()
          {
            //Id = modelId,
            Category = viewModel.Category,
            Level = viewModel.Level,
            TestAnswer = viewModel.CreateTestAnswer(),
            QuestionImageId = imageId
          };
          await _context.AddAsync(entity);
        }
        else
        {
          entity.Category = viewModel.Category;
          entity.Level = viewModel.Level;
          entity.QuestionImageId = imageId;
          entity.TestAnswer = viewModel.CreateTestAnswer();
          if (viewModel.QuestionText != null && image.DataText != viewModel.QuestionText)
          {
            image.DataText = viewModel.QuestionText;
            await _imageContext.UpdateAsync(image);
          }

          await _context.UpdateAsync(entity);
        }
      }

      return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> SaveQuestion(int modelId, int imageId, QuestionDetailViewModel viewModel)
    {
      var image = await _imageContext.GetByIdAsync(imageId);
      if (image == null)
        return BadRequest($"Test image of id {imageId} not found");

      var entity = await _context.GetByIdAsync(modelId);
      if (entity == null)
      {
        entity = new TestQuestion()
        {
          //Id = modelId,
          Category = viewModel.Category,
          Level = viewModel.Level,
          TestAnswer = viewModel.CreateTestAnswer(),
          //Height = image.Height,
          QuestionImageId = imageId
        };
        await _context.AddAsync(entity);
      }
      else
      {
        entity.Category = viewModel.Category;
        entity.Level = viewModel.Level;
        entity.QuestionImageId = imageId;
        entity.TestAnswer = viewModel.CreateTestAnswer();

        await _context.UpdateAsync(entity);
      }

      return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult CreateNew(TestQuestionViewModel viewModel)
    {
      var id = 0;
      var imageId = _testQuestionService.CreateTestImage(string.IsNullOrEmpty(viewModel.QuestionText) ? null : EncodingUtil.StrToByteArray(viewModel.QuestionText), string.IsNullOrEmpty(viewModel.Name) ? id.ToString() : viewModel.Name, viewModel.ImageContainer == CloudContainer.None ? "Text" : "PNG", viewModel.ImageContainer.ToString());
      var retVal = _testQuestionService.CreateOrUpdate(id, imageId, viewModel.Level, viewModel.TextAnswer,        viewModel.Category, viewModel.AnswerChoice, viewModel.AnswerTip);
      if (viewModel.SessionId > 0)
      {
        //TODO need to get a test session service and add this question to a test session
      }

      return RedirectToAction(nameof(Index));
    }

    public string GetTestQuestionString(int id)
    {
      var image = _imageContext.GetByIdAsync(id).Result;
      if (image == null || String.Compare(image.ContentType, "Text", StringComparison.InvariantCultureIgnoreCase) != 0)
      {
        return null;
      }

      return EncodingUtil.ByteArrayToStr(image.Data);
    }

    [HttpPost]
    public async Task<IActionResult> ExportQuestions(IList<IFormFile> files)
    {
      ////Pre-processing the files
      //var txtFile = files.Where(f => f.ContentType.ToLower().StartsWith("text/")).FirstOrDefault();

      //if (txtFile == null) throw new ArgumentException("Cannot export questions, need ONE formatted .xml file to provide extra information");
      //Configurator.Init();
      //var args = new string[] { "-o", txtFile.FileName, "-u" };

      //var total = await ExportTestQuestions(txtFile.FileName);

      return RedirectToAction(nameof(Index));
    }

    private async Task<int> ExportTestQuestions(string selectedFile)
    {
      throw new NotImplementedException();
      //var args = new string[] { "-e", "-f", selectedFile, "-q", "from TestQuestion" };
      //var de = new BaseEntity.Metadata.DataExporter(false, "%Y%m%d", false);
      //var testQuestions = await _context.TestQuestions.Include(tq => tq.QuestionImage).ToListAsync();
      //var total = de.Export(selectedFile, testQuestions);

      //return total;
    }
  }
}