using System;
using System.Collections.Generic;
using WebMathTraining.Models;
using StoreManager.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreManager.Controllers
{
    [Authorize]
    public class TestResultController : Controller
    {
    private readonly ICatalogRepository<TestResult> _context;

    public TestResultController(ICatalogRepository<TestResult> context)
    {
      _context = context;
    }

    public IActionResult Index()
    {
      return View();
    }

    public IActionResult Detail(int id)
    {
      var obj = _context.GetByIdAsync(id).Result;

      if (obj == null)
      {
        return NotFound();
      }

      return View(obj);
    }
  }
}