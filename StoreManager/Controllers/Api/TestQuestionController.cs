using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using WebMathTraining.Models;
using StoreManager.Interfaces;
using StoreManager.Models.SyncfusionViewModels;
using StoreManager.Specifications;

namespace StoreManager.Controllers.Api
{
  [Authorize(Roles = Constants.AdministratorRole)]
  [Produces("application/json")]
  [Route("api/TestQuestion")]
  public class TestQuestionController : Controller
  {
    private readonly ICatalogRepository<TestQuestion> _testQuestionService;
    private readonly ICatalogRepository<TestImage> _testImageService;

    public TestQuestionController(ICatalogRepository<TestQuestion> testQuestions,
                               ICatalogRepository<TestImage> testImageService)
    {
      _testQuestionService = testQuestions;
      _testImageService = testImageService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTestQuestion()
    {
      var list = await _testQuestionService.ListAllAsync();
      var Items = new List<QuestionDetailViewModel>();
      foreach (var q in list)
      {
        var testImage = await _testImageService.GetByIdAsync(q.QuestionImageId);
        Items.Add(new QuestionDetailViewModel(q) {Image = testImage});
      }

      int Count = Items.Count();
      return Ok(new {Items, Count});
    }


    [HttpPost("[action]")]
    public IActionResult Insert([FromBody]CrudViewModel<TestQuestion> payload)
    {
      var result = payload?.value;
      _testQuestionService.AddAsync(result).Wait();
      return Ok(result);
    }

    [HttpPost("[action]")]
    public IActionResult Remove([FromBody]CrudViewModel<TestQuestion> payload)
    {
      if (Int32.TryParse(payload.key.ToString(), out int objKey))
      {
        var result = _testQuestionService.GetByIdAsync(objKey).Result;
        _testQuestionService.DeleteAsync(result).Wait();
        return Ok(result);
      }

      return RedirectToPage("Index");
    }

    [HttpPost("[action]")]
    public IActionResult Update([FromBody]CrudViewModel<TestQuestion> payload) //TODO need to work out the processing
    {
      var result = payload.value;
      _testQuestionService.UpdateAsync(result).Wait();
      return Ok(result);
    }
  }
}
