using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StoreManager.Interfaces;
using Microsoft.AspNetCore.Mvc;
using StoreManager.Models.SyncfusionViewModels;
using Microsoft.AspNetCore.Authorization;
using WebMathTraining.Models;

namespace StoreManager.Controllers.Api
{
  [Authorize]
  [Produces("application/json")]
  [Route("api/TestSession")]
  public class TestSessionController : Controller
  {
    private readonly ICatalogRepository<TestSession> _context;

    public TestSessionController(ICatalogRepository<TestSession> context)
    {
      _context = context;
    }

    // GET: api/TestSession
    [HttpGet]
    public async Task<IActionResult> GetTestSession()
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
    public IActionResult Insert([FromBody]CrudViewModel<TestSession> payload)
    {
      var testGroup = payload.value;
      _context.AddAsync(testGroup).Wait();
      return Ok(testGroup);
    }

    [HttpPost("[action]")]
    public IActionResult Update([FromBody]CrudViewModel<TestSession> payload)
    {
      var testGroup = payload.value;
      //testGroup.LastUpdated = DateTime.UtcNow;
      _context.UpdateAsync(testGroup).Wait();
      return Ok(testGroup);
    }

    [HttpPost("[action]")]
    public IActionResult Remove([FromBody]CrudViewModel<TestSession> payload)
    {
      var result = payload.value;
      _context.DeleteAsync(result).Wait();
      return Ok(result);
    }

  }
}