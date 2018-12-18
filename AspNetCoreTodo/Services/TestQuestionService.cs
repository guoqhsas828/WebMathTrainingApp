using System;
using System.IO;
using System.Drawing;
using System.Threading.Tasks;
using WebMathTraining.Data;
using WebMathTraining.Models;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;

namespace WebMathTraining.Services
{
    public class TestQuestionService
    {
        //private readonly ApplicationDbContext _context;

        //public async Task<IActionResult> Create(IFormFile Image)
        //{
        //    var testQuestion = new TestQuestion();
        //    if (Image!= null)

        //    {
        //        if (Image.Length > 0)

        //        //Convert Image to byte and save to database

        //        {

        //            byte[] p1 = null;
        //            using (var fs1 = Image.OpenReadStream())
        //            using (var ms1 = new MemoryStream())
        //            {
        //                fs1.CopyTo(ms1);
        //                p1 = ms1.ToArray();
        //            }
        //            .Img= p1;

        //        }
        //    }

        //    _context.Add(client);
        //    await _context.SaveChangesAsync();

        //    return RedirectToAction("Index");
        //}
    }
}
