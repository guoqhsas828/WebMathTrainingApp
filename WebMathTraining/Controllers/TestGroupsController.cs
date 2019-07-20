using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.Infrastructure.Data;
using StoreManager.Models;
using WebMathTraining.Models;
using WebMathTraining.Services;

namespace WebMathTraining.Views
{
  [Route("[controller]/[action]")]
  public class TestGroupsController : Controller
  {
    private readonly IAppUserManageService _userContext;
    private readonly ITestSessionService<int> _testSessionService;
    private readonly UserManager<ApplicationUser> _userManager;

    public TestGroupsController(IAppUserManageService userContext, 
      ITestSessionService<int> service, UserManager<ApplicationUser> userManager)
    {
      _userContext = userContext;
      _testSessionService = service;
      _userManager = userManager;
    }

    // GET: TestGroups
    public async Task<IActionResult> Index()
    {
      var user = await _userManager.GetUserAsync(User);
      if (user == null)
        return RedirectToAction("Login", "Account");
      var userProfile = await _userContext.FindByUserEmail(user.Email);
      return View(await _testSessionService.FindAllTestGroupAsync(userProfile));
    }

    // GET: TestGroups/Details/5
    public async Task<IActionResult> Details(int? id)
    {
      //Show the results for all test sessions linked to this group
      if (id == null)
      {
        return NotFound();
      }

      var testGroup = await _testSessionService.FindTestGroupAsyncById(id.Value);
      if (testGroup == null)
      {
        return NotFound();
      }

      var allTestSessions = await _testSessionService.FindAllAsync();
      var groupTestSessions =  allTestSessions.Where(s => testGroup.EnrolledSessionIds.Contains(s.ObjectId));

      var viewModel = new TestGroupSummaryViewModel( testGroup.Name);

      foreach (var session in groupTestSessions.OrderByDescending(s => s.LastUpdated))
      {
        var testResults = await _testSessionService.GetTestResultsAsync(session.ObjectId);
        foreach (var tr in testResults.ToList().OrderByDescending(s => s.FinalScore))
        {
          if (!testGroup.MemberObjectIds.Contains(tr.UserId)) continue; //Only shows score of members in the team

          var testResultV = new TestResultViewModel()
          {
            SessionId = session.Id,
            SessionName = session.Name,
            Tester = await _userContext.FindById(tr.UserId),
            TestResult = tr
          };

          viewModel.TestResults.Add(testResultV);
        }
      }

      return View(viewModel);
    }

  }
}
