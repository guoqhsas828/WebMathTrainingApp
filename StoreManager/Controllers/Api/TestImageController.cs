using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using WebMathTraining.Models;
using StoreManager.Specifications;
using StoreManager.Interfaces;
using WebMathTraining.Services;
using StoreManager.Models;
using StoreManager.Models.SyncfusionViewModels;
using StoreManager.Services;
using WebMathTraining.Utilities;

namespace StoreManager.Controllers.Api
{
  [Authorize(Roles = Constants.AdministratorRole)]
  [Produces("application/json")]
[Route("api/TestImage")]
public class TestImageController : Controller
  {
    private readonly CatalogContext _context;
    private readonly ITestQuestionService<int> _testQuestionService;
    private readonly ICatalogRepository<TestImage> _testImageService;
    private readonly ITestSessionService<int> _testSessionService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;

    public TestImageController(CatalogContext context, ITestQuestionService<int> testQuestions, 
                               ICatalogRepository<TestImage> testImages, 
                               ITestSessionService<int> testSessionService,
                               UserManager<ApplicationUser> userMgr,
                               IEmailSender emailSender)
    {
      _context = context;
      _testQuestionService = testQuestions;
      _testImageService = testImages;
      _userManager = userMgr;
      _emailSender = emailSender;
      _testSessionService = testSessionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTestImage()
    {
      var imageIds = _context.TestQuestion.Select(q => q.QuestionImageId).ToHashSet<int>();
      var images = await _testImageService.ListAsync(new TestImageFilterSpecification("text"));
      List<TestImage> Items = images.ToList().Where(img => !imageIds.Contains(img.Id)).ToList();
      int Count = Items.Count();
      return Ok(new {Items, Count});
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
          var testSession = await _context.TestSession.FindAsync(sessionId);
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

            int imageId;
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
              else //the image file must be in the cloud
              {
                imageId = _testQuestionService.CreateTestImage(null, imageName, "image/PNG", questionContent);
              }
            }
            else
            {
              imageId = _testQuestionService.CreateTestImage(EncodingUtil.StrToByteArray(questionContent), imageName,
                "Text");
            }

            uploadProcessLogger.AppendLine($"ImageId: {imageId}");
            if (gradeLevel > 0)
            {
              var questionId = _testQuestionService.CreateOrUpdate(0, imageId, gradeLevel, testAnswer);
              uploadProcessLogger.AppendLine($"Question with Id: {questionId} Added");
              if (testSession != null)
              {
                _testSessionService.AddQuestion(testSession.Id, questionId, scorePoint, -1.0);
                uploadProcessLogger.AppendLine($"Test question {questionId} added to session {testSession.Id}");
              }
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

    [HttpPost("[action]")]
    public IActionResult Insert([FromBody]CrudViewModel<TestImage> payload)
    {
      var result = payload?.value;
      _testImageService.AddAsync(result).Wait();
      return Ok(result);
    }

    [HttpPost("[action]")]
    public IActionResult Remove([FromBody]CrudViewModel<TestImage> payload)
    {
      var result = _context.TestImage.FirstOrDefault(ti => ti.ObjectId == (int) payload.key);
      _testImageService.DeleteAsync(result).Wait();
      return Ok(result);
    }

    [HttpPost("[action]")]
    public IActionResult Update([FromBody]CrudViewModel<TestImage> payload) //Note, the parameter here need to match asp-route-***
    {
        var testImage = payload?.value;
        if (testImage != null)
        {
          _testImageService.UpdateAsync(testImage).Wait();
        }
        return Ok(testImage);
    }

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

      //var processedStrList = EncodingUtil.SwapTextColumns(inputStr, new[] {0, 2, 4, 5, 1, 3});
      //using (var fileWriter = new StreamWriter(uploadedFile.FileName, false))
      //{
      //  fileWriter.Write(processedStrList);
      //}

      return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> GenSchema(IList<IFormFile> files)
    {
      ////Pre-processing the files
      //var txtFile = files.Where(f => f.ContentType.ToLower().StartsWith("text/")).FirstOrDefault();

      //if (txtFile == null) throw new ArgumentException("Cannot generate schema, need ONE formatted text file to provide extra information");
      //Configurator.Init();
      //var args = new string[] { "-o", txtFile.FileName ,"-u"};
      //if (txtFile.FileName.StartsWith("InitDatabase", StringComparison.CurrentCultureIgnoreCase))
      //{
      //  Utilities.GenSchema.InitDatabase();
      //}

      //Utilities.GenSchema.GenerateSchema(args);

      return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ExportImages(IList<IFormFile> files)
    {
      ////Pre-processing the files
      //var txtFile = files.Where(f => f.ContentType.ToLower().StartsWith("text/")).FirstOrDefault();

      //if (txtFile == null) throw new ArgumentException("Cannot simply upload image files any more, need ONE formatted text file to provide extra information");
      //Configurator.Init();
      //var args = new string[] { "-o", txtFile.FileName, "-u" };
      //using (new SessionBinder())
      //{
      //  var de = new BaseEntity.Metadata.DataImporter();
      //  de.Import(txtFile.FileName);
      //  //var total = await ExportTestImages(txtFile.FileName);
      //}
      return RedirectToAction(nameof(Index));
    }

    private async Task<int> ExportTestImages(string selectedFile)
    {
      //var args = new string[] { "-e", "-f", selectedFile, "-q", "from TestImage" };
      //var de = new BaseEntity.Metadata.DataExporter(false, "%Y%m%d", false);
      //var images = _context.TestImages.ToList();
      //foreach (var image in images)
      //{
      //  try
      //  {
      //    if (image.Width != CloudContainer.None && image.Data == null)
      //    {
      //      var fileName = image.Name;
      //      var containerName = image.Width.ToString();

      //      if (fileName.IndexOf('.') < 0) fileName += ".PNG";
      //      var cloudData = await _blobFileService.DownloadBlobToByteArrayAsync(fileName, containerName);
      //      image.Data = cloudData.Item1;
      //      image.ContentType = cloudData.Item2;
      //    }
      //  }
      //  catch (Exception ex)
      //  {
      //    var msg = ex.Message;
      //  }
      //}

      var total = 0;
      //var total = de.Export(selectedFile, images);

      return total;
    }

  }
}
