using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StoreManager.Interfaces;
using Microsoft.AspNetCore.Mvc;
using StoreManager.Models.SyncfusionViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using StoreManager.Models;
using WebMathTraining.Models;

namespace StoreManager.Controllers.Api
{
  [Authorize]
  [Produces("application/json")]
  [Route("api/TestResult")]
  public class TestResultController : Controller
  {
    private readonly ICatalogRepository<TestResult> _context;

    public TestResultController(ICatalogRepository<TestResult> context)
    {
      _context = context;
    }

    // GET: api/TestGroup
    [HttpGet]
    public async Task<IActionResult> GetTestResult()
    {
      var items = await _context.ListAllAsync();
      var Items = items.ToList();
      int Count = Items.Count();
      return Ok(new { Items, Count });
    }

    [HttpGet("[action]/{id}")]
    public async Task<IActionResult> GetById(int id)
    {
      var result = await _context.GetByIdAsync(id);

      return Ok(result);
    }

    [HttpPost("[action]")]
    public IActionResult Insert([FromBody]CrudViewModel<TestResult> payload)
    {
      var testGroup = payload.value;
      _context.AddAsync(testGroup).Wait();
      return Ok(testGroup);
    }

    [HttpPost("[action]")]
    public IActionResult Update([FromBody]CrudViewModel<TestResult> payload)
    {
      var testGroup = payload.value;
      //testGroup.LastUpdated = DateTime.UtcNow;
      _context.UpdateAsync(testGroup).Wait();
      return Ok(testGroup);
    }

    [HttpPost("[action]")]
    public IActionResult Remove([FromBody]CrudViewModel<TestResult> payload)
    {
      var result = payload.value;
      _context.DeleteAsync(result).Wait();
      return Ok(result);
    }

  }
}