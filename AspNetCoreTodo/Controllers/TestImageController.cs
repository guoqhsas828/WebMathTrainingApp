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
      //Pre-processing the files
      var txtFiles = files.Where(f => f.ContentType.ToLower().StartsWith("text/")).ToList();
      var imageFiles = files.Where(f => f.ContentType.ToLower().StartsWith("image/")).ToList();

      if (txtFiles.Count != 1) throw new ArgumentException("Cannot simply upload image files any more, need ONE formatted text file to provide extra information");

      var uploadedFile = txtFiles[0];
      var fileDirNameInfos = uploadedFile.FileName.Split("\\");
      if (fileDirNameInfos.Length >= 2)
      {
        var realFileName = fileDirNameInfos[fileDirNameInfos.Length - 1]; //this shall be the test group name
        var testGroupName = realFileName.Replace(".txt", "");
        var directFolderName = fileDirNameInfos[fileDirNameInfos.Length - 2]; //This shall be the test session name
        var testGroup = _context.TestGroups.FirstOrDefault(g => String.Compare(g.Name, testGroupName, StringComparison.InvariantCultureIgnoreCase) == 0);
        var testSession = _context.TestSessions.FirstOrDefault(s => String.Compare(s.Name, directFolderName, StringComparison.InvariantCultureIgnoreCase) == 0);
        if (testSession == null)
        {
          _testSessionService.CreateNewSession(directFolderName);
          testSession = _context.TestSessions.FirstOrDefault(s => String.Compare(s.Name, directFolderName, StringComparison.InvariantCultureIgnoreCase) == 0);
        }

        if (testGroup == null)
        {
          //_testSessionService.create Create new test group?
        }

        var questionStrList = new List<string>();
        var result = string.Empty;
        using (var reader = new StreamReader(uploadedFile.OpenReadStream()))
        {
          result = reader.ReadToEnd();
        }

        questionStrList = string.IsNullOrEmpty(result) ? new List<string>() : result.Split('\n').ToList();
        foreach (var questionStr in questionStrList)
        {
          var questionDetails = questionStr.Split(Constants.TxtUploadColumnBreaker);
          if (questionDetails.Length < 6) continue;
          Int32 gradeLevel;
          if (!Int32.TryParse(questionDetails[0], out gradeLevel)) gradeLevel = 3;
          var questionType = questionDetails[1]; //Text or PNG
          var imageName = questionDetails[2].Replace("\r", "");
          var imageContent = questionDetails[3].Replace("\r", "");
          var testAnswer = questionDetails[4].Replace("\r", "");
          double scorePoint;
          if (!Double.TryParse(questionDetails[5].Replace("\r", ""), out scorePoint)) scorePoint = 3.0;

          Guid imageId;
          if (questionType.ToLower() == "text")
          {
            imageId = _testQuestionService.CreateTestImage(TestQuestionService.StrToByteArray(imageContent), imageName, "Text");
          }
          else
          {
            //we need to locate the image file
            var imageUploadedFile = imageFiles.FirstOrDefault(f => f.FileName.EndsWith(imageContent, StringComparison.InvariantCultureIgnoreCase));
            if (imageUploadedFile != null && imageUploadedFile.ContentType.ToLower().StartsWith("image/"))
            {
              var ms = new MemoryStream();
              imageUploadedFile.OpenReadStream().CopyTo(ms);

              imageId = _testQuestionService.CreateTestImage(ms.ToArray(), imageName, imageUploadedFile.ContentType);
            }
            else
            {
              throw new Exception($"Cannot locate the expected image file in the upload list named {imageContent} with line {imageName}");
            }
          }

          var questionId = _testQuestionService.CreateOrUpdate(Guid.NewGuid(), imageId, gradeLevel, testAnswer);

          if (testSession != null)
            _testSessionService.AddQuestion(testSession.Id, questionId, scorePoint, -1.0);
        }


        if (testSession != null)
        {
          var addSession = _testSessionService.AddSessionIntoTestGroup(testSession.ObjectId, testGroupName);
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
