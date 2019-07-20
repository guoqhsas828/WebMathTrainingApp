using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StoreManager.Data;
using StoreManager.Models;
using StoreManager.Models.SyncfusionViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.eShopWeb.Infrastructure.Data;
using StoreManager.Interfaces;
using WebMathTraining.Models;

namespace StoreManager.Controllers.Api
{
  [Authorize]
  [Produces("application/json")]
  [Route("api/TestResultItem")]
  public class TestResultItemController : Controller
  {
    private readonly ICatalogRepository<TestResult> _context;

    public TestResultItemController(ICatalogRepository<TestResult> context)
    {
      _context = context;
    }

    // GET: api/TestResultItem
    [HttpGet]
    public async Task<IActionResult> GetTestResultItem()
    {
      var headers = Request.Headers["TestResultId"];
      var Items = new List<TestResultItem>();
      int Count = 0;

      if (Int32.TryParse(headers, out int testResultId))
      {
        var testResult = await _context.GetByIdAsync(testResultId);
        Items = testResult.TestResults.Items;
        Count = Items.Count();
      }

      return Ok(new {Items, Count});
    }

    private void UpdateTestResult(TestResult testResult)
    {
      testResult.FinalScore = testResult.TestResults.Items.Sum(t => t.Score);
    }

    [HttpPost("[action]")]
    public IActionResult Insert([FromBody] CrudViewModel<TestResultItem> payload)
    {
      var val = payload.value;
      var headers = Request.Headers["TestResultId"];

      if (Int32.TryParse(headers, out int testResultId))
      {
        var parentObj = _context.GetByIdAsync(testResultId).Result;
        if (parentObj != null)
        {
          parentObj.TestResults.Add(val);
          UpdateTestResult(parentObj);
          _context.UpdateAsync(parentObj).Wait();
        }
      }

      return Ok(val);
    }

    [HttpPost("[action]")]
    public IActionResult Update([FromBody] CrudViewModel<TestResultItem> payload)
    {
      var val = payload.value;
      var headers = Request.Headers["TestResultId"];

      if (Int32.TryParse(headers, out int testResultId))
      {
        var parentObj = _context.GetByIdAsync(testResultId).Result;
        if (parentObj != null)
        {
          parentObj.TestResults.Add(val);
          UpdateTestResult(parentObj);
          _context.UpdateAsync(parentObj).Wait();
        }
      }

      return Ok(val);
    }

    [HttpPost("[action]")]
    public IActionResult Remove([FromBody] CrudViewModel<TestResultItem> payload)
    {
      var val = payload.value;
      var headers = Request.Headers["TestResultId"];

      if (Int32.TryParse(headers, out int testResultId))
      {
        var parentObj = _context.GetByIdAsync(testResultId).Result;
        if (parentObj != null)
        {
          parentObj.TestResults.Add(val);
          UpdateTestResult(parentObj);
          _context.UpdateAsync(parentObj).Wait();
        }
      }

      return Ok(val);

    }
  }
}