using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text;
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
    private readonly IEmailSender _emailSender;

    public TestImageController(TestDbContext context, 
                               ITestQuestionService service, 
                               ITestSessionService sessionService, 
                               UserManager<ApplicationUser> userMgr,
                               IEmailSender emailSender)
    {
      _context = context;
      _testQuestionService = service;
      _testSessionService = sessionService;
      _userManager = userMgr;
      _emailSender = emailSender;
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
      var uploadProcessLogger = new StringBuilder();

      try
      {
        var currentUser = await _userManager.GetUserAsync(User);
        uploadProcessLogger.AppendLine($"Files: {String.Join(',', files.Select(f => f.FileName))}");

        var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);
        uploadProcessLogger.AppendLine($"isAdmin: {isAdmin}");
        if (!isAdmin)
          return BadRequest("Only user with admin role can upload questions");

        //Pre-processing the files
        var txtFiles = files.Where(f => f.ContentType.ToLower().StartsWith("text/")).ToList();
        var imageFiles = files.Where(f => f.ContentType.ToLower().StartsWith("image/")).ToList();

        if (txtFiles.Count != 1)
        {
          throw new ArgumentException(
            "Cannot simply upload image files any more, need ONE formatted text file to provide extra information");
        }

        var uploadedFile = txtFiles[0];
        var fileDirNameInfos = uploadedFile.FileName.Split("\\");
        if (fileDirNameInfos.Length >= 1)
        {
          var realFileName = fileDirNameInfos[fileDirNameInfos.Length - 1]; //this shall be the test group name
          var testGroupName = realFileName.Replace(".txt", "");
          var dateTimeNow = DateTime.Now;
          var testSessionName = fileDirNameInfos.Length >= 2
            ? fileDirNameInfos[fileDirNameInfos.Length - 2]
            : $"{testGroupName}_{dateTimeNow.Day:D2}-{$"{(Constants.Month)dateTimeNow.Month}".Substring(0, 3)}-{dateTimeNow.Year:D4}";

          if (string.IsNullOrEmpty(testSessionName) || string.IsNullOrEmpty(testGroupName))
            return BadRequest("Invalid file name or folder name");

          var testGroupId = await _testSessionService.CreateNewTestGroup(testGroupName);
          var testGroup = await _testSessionService.FindTestGroupAsyncById(testGroupId);
          uploadProcessLogger.AppendLine($"TestGoup: {testGroupId} is null ? {testGroup == null}");
          var sessionId = _testSessionService.CreateNewSession(testSessionName);
          var testSession = await _context.TestSessions.FindAsync(sessionId);
          uploadProcessLogger.AppendLine($"TestSession: {sessionId} is null ? {testSession == null}");
          string result;
          using (var reader = new StreamReader(uploadedFile.OpenReadStream()))
          {
            result = reader.ReadToEnd();
          }

          var questionStrList = string.IsNullOrEmpty(result) ? new List<string>() : result.Split('\n').ToList();
          foreach (var questionStr in questionStrList)
          {
            var questionDetails = questionStr.Split(Constants.TxtUploadColumnBreaker);
            if (questionDetails.Length < 6) continue;
            if (!Int32.TryParse(questionDetails[0], out var gradeLevel)) gradeLevel = 3;
            var questionType = questionDetails[4]; //Text or PNG
            var imageName = questionDetails[1].Replace("\r", "");
            var questionContent = questionDetails[5].Replace("\r", "").TrimEnd();
            var testAnswer = questionDetails[2].Replace("\r", "");
            if (!Double.TryParse(questionDetails[3].Replace("\r", ""), out var scorePoint)) scorePoint = 3.0;

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
                throw new Exception($"Cannot locate the expected image file in the upload list named {questionContent} with line {imageName}");
              }
            }
            else
            {
              imageId = _testQuestionService.CreateTestImage(EncodingUtil.StrToByteArray(questionContent), imageName,
                "Text");
            }

            uploadProcessLogger.AppendLine($"ImageId: {imageId}");
            var questionId = _testQuestionService.CreateOrUpdate(Guid.NewGuid(), imageId, gradeLevel, testAnswer);
            uploadProcessLogger.AppendLine($"QuestionId: {questionId}");
            if (testSession != null)
            {
              _testSessionService.AddQuestion(testSession.Id, questionId, scorePoint, -1.0);
              uploadProcessLogger.AppendLine($"Test question {questionId} added to session {testSession.Id}");
            }
          }


          if (testSession != null && testGroup != null)
          {
            var addSession = _testSessionService.AddSessionIntoTestGroup(testSession.ObjectId, testGroupName);
            uploadProcessLogger.AppendLine($"Test session {testSession.Name} added into group {testGroupName}");
          }
        }
      }
      catch (Exception e)
      {
        uploadProcessLogger.AppendLine("Exception:");
        uploadProcessLogger.AppendLine(e.Message);
        uploadProcessLogger.AppendLine(e.InnerException?.Message ?? "");
      }
      finally
      {
        await _emailSender.SendEmailAsync(Constants.AdminEmail, $"File upload log {DateTime.Now}",
          uploadProcessLogger.ToString());
      }

      return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ViewImage(Guid? id)
    {
      string contentType = "image/png";
      MemoryStream ms;
      if (id == null)
      {
        var randomizer = new Random(DateTime.Now.Second);
        var cloudUtil = new CloudBlobUtility();
        var data = await cloudUtil.DownloadBlobToByteArrayAsync(Constants.FunTips[randomizer.Next(0, Constants.FunTips.Length-1)]);
        ms = new MemoryStream(data.Item1);
        contentType = data.Item2;
        return new FileStreamResult(ms, contentType);
      }
      else
      {
        var image = _context.TestImages.FirstOrDefault(m => m.Id == id.Value);
        if (image == null)
          return NotFound();

        ms = new MemoryStream(image.Data);
        contentType = image.ContentType;
      }
      return new FileStreamResult(ms, contentType);
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
