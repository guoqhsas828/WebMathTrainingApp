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

namespace StoreManager.Controllers
{
    [Authorize(Roles = Constants.AdministratorRole)]
    public class TestImageController : Controller
    {

    private readonly ICatalogRepository<TestImage> _context;
    private readonly IBlobFileService _blobFileService;

    public TestImageController(ICatalogRepository<TestImage> context, IBlobFileService blobFileService) : base()
    {
      _context = context;
      _blobFileService = blobFileService;
    } 

    public IActionResult Index()
    {
      return View();
    }

    public async Task<IActionResult> ViewImage(int? id)
    {
      string contentType = "image/png";
      MemoryStream ms;
      if (id == null)
      {
        var randomizer = new Random(DateTime.Now.Second);
        var fileNames = await _blobFileService.ListBlobFileNamesAsync();
        var data = await _blobFileService.DownloadBlobToByteArrayAsync(fileNames[randomizer.Next(0, fileNames.Count - 1)]);
        ms = new MemoryStream(data.Item1);
        contentType = data.Item2;
        return new FileStreamResult(ms, contentType);
      }

      var image = await _context.GetByIdAsync(id.Value); // .TestImage.FirstOrDefault(ti => ti.ObjectId == id.Value);

      if (image == null)
      {
        return NotFound();
      }

      if (image.Width != CloudContainer.None)
      {
        var fileName = image.Name;
        var containerName = image.Width.ToString();

        if (fileName.IndexOf('.') < 0) fileName += ".PNG";
        var cloudData = await _blobFileService.DownloadBlobToByteArrayAsync(fileName, containerName);
        ms = new MemoryStream(cloudData.Item1);
        contentType = cloudData.Item2;
      }
      else
      {
        ms = new MemoryStream(image.Data);
        contentType = image.ContentType;
      }

      return new FileStreamResult(ms, contentType);
    }

    public async Task<IActionResult> GetTestImageFile(int id)
    {
      var image = await _context.GetByIdAsync(id);
      if (image == null || String.Compare(image.ContentType, "Text", StringComparison.InvariantCultureIgnoreCase) == 0)
      {
        return null;
      }

      byte[] imageBytes;
      string contentType;
      if (image.Width != CloudContainer.None)
      {
        //string base64Str = Convert.ToBase64String(image.Data);
        //imageBytes = Convert.FromBase64String(base64Str);
        var fileName = image.Name;
        var containerName = image.Width.ToString();

        if (fileName.IndexOf('.') < 0) fileName += ".PNG";
        var cloudData = await _blobFileService.DownloadBlobToByteArrayAsync(fileName, containerName);
        imageBytes = cloudData.Item1;
        contentType = cloudData.Item2;
      }
      else
      {
        imageBytes = image.Data;
        contentType = image.ContentType;
      }

      FileResult imageUserFile = File(imageBytes, contentType);
      return imageUserFile;
    }
  }
}