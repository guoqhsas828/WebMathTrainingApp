using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebMathTraining.Data;
using WebMathTraining.Models;
using WebMathTraining.Services;

namespace WebMathTraining.Views
{
  public class TestGroupsController : Controller
  {
    private readonly TestDbContext _context;
    private readonly ApplicationDbContext _userContext;
    private readonly ITestSessionService _testSessionService;

    public TestGroupsController(TestDbContext context, ApplicationDbContext userContext, ITestSessionService service)
    {
      _context = context;
      _userContext = userContext;
      _testSessionService = service;
    }

    // GET: TestGroups
    public async Task<IActionResult> Index()
    {
      return View(await _context.TestGroups.ToListAsync());
    }

    // GET: TestGroups/Details/5
    public async Task<IActionResult> Details(Guid? id)
    {
      //Show the results for all test sessions linked to this group
      if (id == null)
      {
        return NotFound();
      }

      var testGroup = await _context.TestGroups.FindAsync(id.Value);
      if (testGroup == null)
      {
        return NotFound();
      }

      var groupTestSessions = _context.TestSessions.Where(s => testGroup.EnrolledSessionIds.Contains(s.ObjectId));

      var viewModel = new TestGroupSummaryViewModel( testGroup.Name);

      foreach (var session in groupTestSessions.OrderByDescending(s => s.LastUpdated))
      {
        var testResults = _testSessionService.GetTestResults(session.ObjectId).Select(tr =>
          new TestResultViewModel()
          {
            SessionId = session.Id,
            SessionName = session.Name,
            Tester = _userContext.Users.FirstOrDefault(u => u.ObjectId == tr.UserId),
            TestResult = tr
          }).OrderByDescending(tr => tr.TestResult.FinalScore).ToList();
        viewModel.TestResults.AddRange(testResults);
      }

      return View(viewModel);
    }

    // GET: TestGroups/Create
    public IActionResult Create()
    {
      return View();
    }

    // POST: TestGroups/Create
    // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
    // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Name,Description,TeamHeadId,MembersInfo,EnrolledSessionsInfo")]
      TestGroup testGroup)
    {
      if (ModelState.IsValid)
      {
        testGroup.Id = Guid.NewGuid();
        testGroup.LastUpdated = DateTime.UtcNow;
        _context.Add(testGroup);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
      }

      return View(testGroup);
    }

    // GET: TestGroups/Edit/5
    public async Task<IActionResult> Edit(Guid? id)
    {
      if (id == null)
      {
        return NotFound();
      }

      var testGroup = await _context.TestGroups.FindAsync(id);
      if (testGroup == null)
      {
        return NotFound();
      }

      return View(testGroup);
    }

    // POST: TestGroups/Edit/5
    // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
    // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id,
      [Bind("Id,Name,Description,TeamHeadId,MembersInfo,EnrolledSessionsInfo")]
      TestGroup testGroup)
    {
      if (id != testGroup.Id)
      {
        return NotFound();
      }

      if (ModelState.IsValid)
      {
        try
        {
          testGroup.LastUpdated = DateTime.UtcNow;
          _context.Update(testGroup);
          await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
          if (!TestGroupExists(testGroup.Id))
          {
            return NotFound();
          }
          else
          {
            throw;
          }
        }

        return RedirectToAction(nameof(Index));
      }

      return View(testGroup);
    }

    // GET: TestGroups/Delete/5
    public async Task<IActionResult> Delete(Guid? id)
    {
      if (id == null)
      {
        return NotFound();
      }

      var testGroup = await _context.TestGroups
        .FirstOrDefaultAsync(m => m.Id == id);
      if (testGroup == null)
      {
        return NotFound();
      }

      return View(testGroup);
    }

    // POST: TestGroups/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
      var testGroup = await _context.TestGroups.FindAsync(id);
      _context.TestGroups.Remove(testGroup);
      await _context.SaveChangesAsync();
      return RedirectToAction(nameof(Index));
    }

    private bool TestGroupExists(Guid id)
    {
      return _context.TestGroups.Any(e => e.Id == id);
    }
  }
}
