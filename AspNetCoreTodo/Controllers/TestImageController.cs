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

    public TestImageController(TestDbContext context, ITestQuestionService service)
    {
      _context = context;
      _testQuestionService = service;
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
      foreach (var uploadedImage in files)
      {
        if (uploadedImage != null && uploadedImage.ContentType.ToLower().StartsWith("image/"))
        {
          var ms = new MemoryStream();
          uploadedImage.OpenReadStream().CopyTo(ms);

          var imageId = _testQuestionService.CreateTestImage(ms.ToArray(), uploadedImage.Name, uploadedImage.ContentType);
          _testQuestionService.CreateOrUpdate(Guid.NewGuid(), imageId, 1, "");

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
