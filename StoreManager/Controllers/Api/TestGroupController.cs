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
  [Route("api/TestGroup")]
  public class TestGroupController : Controller
  {
    private readonly ICatalogRepository<TestGroup> _context;

    public TestGroupController(ICatalogRepository<TestGroup> context)
    {
      _context = context;
    }

    // GET: api/TestGroup
    [HttpGet]
    public async Task<IActionResult> GetTestGroup()
    {
      var items = await _context.ListAllAsync();
      List<TestGroup> Items = items.ToList();
      int Count = Items.Count();
      return Ok(new { Items, Count });
    }

    [HttpGet("[action]/{id}")]
    public async Task<IActionResult> GetById(int id)
    {
      var result = await _context.GetByIdAsync(id);

      return Ok(result);
    }

    private void UpdateTestGroup(int id)
    {
      try
      {
        var salesOrder = _context.GetByIdAsync(id).Result;

        if (salesOrder != null)
        {
          salesOrder.LastUpdated = DateTime.UtcNow;
          _context.UpdateAsync(salesOrder).Wait();
        }
      }
      catch (Exception)
      {

        throw;
      }
    }

    [HttpPost("[action]")]
    public IActionResult Insert([FromBody]CrudViewModel<TestGroup> payload)
    {
      var testGroup = payload.value;
      _context.AddAsync(testGroup).Wait();
      UpdateTestGroup(testGroup.Id);
      return Ok(testGroup);
    }

    [HttpPost("[action]")]
    public IActionResult Update([FromBody]CrudViewModel<TestGroup> payload)
    {
      var testGroup = payload.value;
      testGroup.LastUpdated = DateTime.UtcNow;
      _context.UpdateAsync(testGroup).Wait();
      return Ok(testGroup);
    }

    [HttpPost("[action]")]
    public async Task<IActionResult> Remove([FromBody]CrudViewModel<TestGroup> payload)
    {
      var result = await _context.GetByIdAsync(Convert.ToInt32(payload.key));
      await _context.DeleteAsync(result);
      return Ok(result);
    }

  }
}