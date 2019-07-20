using System;
using System.Collections.Generic;
using WebMathTraining.Models;
using StoreManager.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreManager.Controllers
{
    [Authorize(Roles = Constants.AdministratorRole)]
    public class TestGroupController : Controller
    {
    private readonly ICatalogRepository<TestGroup> _context;

    public TestGroupController(ICatalogRepository<TestGroup> context)
    {
      _context = context;
    }

    public IActionResult Index()
    {
      return View();
    }

    public IActionResult Details(int id)
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