using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using WebMathTraining.Data;
using WebMathTraining.Models;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebMathTraining.Controllers
{
    public class TestImageController : Controller
    {
        private readonly TestDbContext _context;

        public TestImageController(TestDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var images = _context.TestImages.ToList();
            return View(new TestImageViewModel { TestImages = images });
        }

        [HttpPost]
        public IActionResult UploadImage(IList<IFormFile> files)
        {
            IFormFile uploadedImage = files.FirstOrDefault();
            if (uploadedImage == null || uploadedImage.ContentType.ToLower().StartsWith("image/"))
            {
                MemoryStream ms = new MemoryStream();
                uploadedImage.OpenReadStream().CopyTo(ms);

        //System.Drawing.Image image = System.Drawing.Image.FromStream(ms);

        var imageEntity = new TestImage()
                {
                    Id = Guid.NewGuid(),
                    Name = uploadedImage.FileName,
                    Data = ms.ToArray(),
                    //Width = image.Width,
                    //Height = image.Height,
                    ContentType = uploadedImage.ContentType
                };

                _context.TestImages.Add(imageEntity);

               _context.SaveChanges();

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
