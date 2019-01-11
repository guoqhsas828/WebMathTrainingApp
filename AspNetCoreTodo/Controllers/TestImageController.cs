using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using WebMathTraining.Data;
using WebMathTraining.Models;
using WebMathTraining.Services;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebMathTraining.Controllers
{
  public class TestImageController : Controller
  {
    private readonly TestDbContext _context;
    private readonly ITestQuestionService _testQuestionService;
    private readonly ITestSessionService _testSessionService;

    public TestImageController(TestDbContext context, ITestQuestionService service, ITestSessionService sessionService)
    {
      _context = context;
      _testQuestionService = service;
      _testSessionService = sessionService;
    }

    [HttpGet]
    public IActionResult Index()
    {
      var processedImages = _context.TestQuestions.Select(q => q.QuestionImage.Id.ToString()).ToHashSet<string>();
      var images = _context.TestImages.Where(img => !processedImages.Contains(img.Id.ToString())).ToList();
      return View(new TestImageViewModel { TestImages = images });
    }

    [HttpPost]
    public IActionResult UploadImage(IList<IFormFile> files)
    {
      foreach (var uploadedFile in files)
      {
        if (uploadedFile != null && uploadedFile.ContentType.ToLower().StartsWith("image/"))
        {
          var ms = new MemoryStream();
          uploadedFile.OpenReadStream().CopyTo(ms);

          var imageId = _testQuestionService.CreateTestImage(ms.ToArray(), uploadedFile.Name, uploadedFile.ContentType);
          _testQuestionService.CreateOrUpdate(Guid.NewGuid(), imageId, 1, "");

        }
        else if (uploadedFile != null && uploadedFile.ContentType.ToLower().StartsWith("text/"))
        {
          var questionStrList = new List<string>();
          var result = string.Empty;
          using (var reader = new StreamReader(uploadedFile.OpenReadStream()))
          {
            result = reader.ReadToEnd();
          }

          var realFileName = uploadedFile.FileName.Split("\\").Last();
          questionStrList = string.IsNullOrEmpty(result) ? new List<string>() : result.Split('\n').ToList();
          foreach (var questionStr in questionStrList)
          {
            var questionDetails = questionStr.Split("<question_line>");
            if (questionDetails.Length < 4) continue;
            var imageId = _testQuestionService.CreateTestImage(TestQuestionService.StrToByteArray(questionDetails[2]), questionDetails[1], "Text");
            var questionId = _testQuestionService.CreateOrUpdate(Guid.NewGuid(), imageId, Convert.ToInt32(questionDetails[0]), questionDetails[3]);
            var testSession = _context.TestSessions.FirstOrDefault(s => String.Compare(s.Name, realFileName.Replace(".txt", ""), StringComparison.InvariantCultureIgnoreCase) == 0);
            if (testSession != null)
              _testSessionService.AddQuestion(testSession.Id, questionId, 3.0, -1);
          }
        }
      }

      return RedirectToAction("Index");
    }

    [HttpGet]
    public FileStreamResult ViewImage(Guid id)
    {

      var image = _context.TestImages.FirstOrDefault(m => m.Id == id);

      MemoryStream ms = new MemoryStream(image.Data);

      return new FileStreamResult(ms, image.ContentType);

    }

    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
      try
      {
        var movie = _context.TestImages.Find(id);
        _context.TestImages.Remove(movie);
        _context.SaveChanges();
        return RedirectToAction("Index");
      }
      catch (Exception ex)
      {
        ModelState.AddModelError("Delete Error", ex.Message);
      }

      return View(new TestImageViewModel { TestImages = _context.TestImages.ToList() });
    }

    //public async Task<IActionResult> Delete(Guid id)
    //{
    //    var image = _context.TestImages.FirstOrDefault(m => m.Id == id);
    //    if (image == null)
    //        return NotFound();

    //    return View(image);
    //}
  }
}
