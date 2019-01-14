using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebMathTraining.Data;
using WebMathTraining.Models;
using WebMathTraining.Services;
using WebMathTraining.Utilities;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebMathTraining.Controllers
{
  public class TestImageController : Controller
  {
    private readonly TestDbContext _context;
    private readonly ITestQuestionService _testQuestionService;
    private readonly ITestSessionService _testSessionService;
    private readonly UserManager<ApplicationUser> _userManager;


    public TestImageController(TestDbContext context, 
                               ITestQuestionService service, 
                               ITestSessionService sessionService, 
                               UserManager<ApplicationUser> userMgr)
    {
      _context = context;
      _testQuestionService = service;
      _testSessionService = sessionService;
      _userManager = userMgr;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
      var processedImages = _context.TestQuestions.Select(q => q.QuestionImage.Id.ToString()).ToHashSet<string>();
      var images = await _context.TestImages.Where(img => !processedImages.Contains(img.Id.ToString())).ToListAsync();
      return View(new TestImageViewModel { TestImages = images });
    }

    [HttpPost]
    public async Task<IActionResult> UploadImage(IList<IFormFile> files)
    {
      var currentUser = await _userManager.GetUserAsync(User);

      var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);
      if (!isAdmin)
        return BadRequest("Only user with admin role can upload questions");

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
        if (string.IsNullOrEmpty(directFolderName) || string.IsNullOrEmpty(testGroupName))
          return BadRequest("Invalid file name or folder name");

        var testGroupId =  await _testSessionService.CreateNewTestGroup(testGroupName);
        var testGroup = await _testSessionService.FindTestGroupAsyncById(testGroupId);

        var sessionId = _testSessionService.CreateNewSession(directFolderName);
        var testSession = await _context.TestSessions.FindAsync(sessionId);

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
          var questionType = questionDetails[4]; //Text or PNG
          var imageName = questionDetails[1].Replace("\r", "");
          var questionContent = questionDetails[5].Replace("\r", "");
          var testAnswer = questionDetails[2].Replace("\r", "");
          double scorePoint;
          if (!Double.TryParse(questionDetails[3].Replace("\r", ""), out scorePoint)) scorePoint = 3.0;

          Guid imageId;
          if (questionContent.ToUpper().EndsWith(".PNG") || questionType.ToUpper() == "PNG")
          {
            //we need to locate the image file
            var imageUploadedFile = imageFiles.FirstOrDefault(f =>
              f.FileName.EndsWith(questionContent, StringComparison.InvariantCultureIgnoreCase));
            if (imageUploadedFile != null && imageUploadedFile.ContentType.ToLower().StartsWith("image/"))
            {
              var ms = new MemoryStream();
              imageUploadedFile.OpenReadStream().CopyTo(ms);

              imageId = _testQuestionService.CreateTestImage(ms.ToArray(), imageName, imageUploadedFile.ContentType);
            }
            else
            {
              throw new Exception(
                $"Cannot locate the expected image file in the upload list named {questionContent} with line {imageName}");
            }
          }
          else
          {
            imageId = _testQuestionService.CreateTestImage(EncodingUtil.StrToByteArray(questionContent), imageName,
              "Text");
          }

          var questionId = _testQuestionService.CreateOrUpdate(Guid.NewGuid(), imageId, gradeLevel, testAnswer);

          if (testSession != null)
            _testSessionService.AddQuestion(testSession.Id, questionId, scorePoint, -1.0);
        }


        if (testSession != null && testGroup != null)
        {
          var addSession = _testSessionService.AddSessionIntoTestGroup(testSession.ObjectId, testGroupName);
        }
      }

      return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public FileStreamResult ViewImage(Guid id)
    {

      var image = _context.TestImages.FirstOrDefault(m => m.Id == id);

      MemoryStream ms = new MemoryStream(image.Data);

      return new FileStreamResult(ms, image.ContentType);

    }

    public IActionResult DeleteConfirmed(Guid id)
    {
      try
      {
        var image = _context.TestImages.Find(id);
        _context.TestImages.Remove(image);
        _context.SaveChanges();
        return RedirectToAction(nameof(Index));
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


    [HttpPost]
    public async Task<IActionResult> SwapTextColumn(IList<IFormFile> files)
    {
      //Pre-processing the files
      var txtFiles = files.Where(f => f.ContentType.ToLower().StartsWith("text/")).ToList();

      if (txtFiles.Count != 1) throw new ArgumentException("Cannot simply upload image files any more, need ONE formatted text file to provide extra information");

      var uploadedFile = txtFiles[0];
      string inputStr = String.Empty;
      using (var reader = new StreamReader(uploadedFile.OpenReadStream()))
      {
        inputStr = reader.ReadToEnd();
      }

      var processedStrList = EncodingUtil.SwapTextColumns(inputStr, new[] {0, 2, 4, 5, 1, 3});
      using (var fileWriter = new StreamWriter(uploadedFile.FileName, false))
      {
        fileWriter.Write(processedStrList);
      }

      return RedirectToAction(nameof(Index));
    }

  }
}
