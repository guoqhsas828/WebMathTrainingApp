using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebMathTraining.Data;
using WebMathTraining.Models;

namespace WebMathTraining.Controllers
{
  public class TestSessionsController : Controller
  {
    private readonly TestDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public TestSessionsController(TestDbContext context, UserManager<ApplicationUser> userManager)
    {
      _context = context;
      _userManager = userManager;
    }

    // GET: TestSessions
    public async Task<IActionResult> Index()
    {
      return View(await _context.TestSessions.ToListAsync());
    }

    // GET: TestSessions/Details/5
    public async Task<IActionResult> Details(Guid? id)
    {
      if (id == null)
      {
        return NotFound();
      }

      var testSession = await _context.TestSessions
          .FirstOrDefaultAsync(m => m.Id == id);
      if (testSession == null)
      {
        return NotFound();
      }

      return View(new TestSessionsViewModel(testSession));
    }

    // GET: TestSessions/Create
    public IActionResult Create()
    {
      return View();
    }

    // POST: TestSessions/Create
    // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
    // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Name,Description,PlannedStart,PlannedEnd")] TestSession testSession)
    {
      if (ModelState.IsValid)
      {
        testSession.Id = Guid.NewGuid();
        testSession.LastUpdated = DateTime.UtcNow;
        _context.Add(testSession);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
      }
      return View(testSession);
    }

    // GET: TestSessions/Edit/5
    public async Task<IActionResult> Edit(Guid? id)
    {
      if (id == null)
      {
        return NotFound();
      }

      var testSession = await _context.TestSessions.FindAsync(id);
      if (testSession == null)
      {
        return NotFound();
      }
      return View(testSession);
    }

    // POST: TestSessions/Edit/5
    // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
    // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [Bind("Id,ObjectId,Name,Description,PlannedStart,PlannedEnd")] TestSession testSession)
    {
      if (id != testSession.Id)
      {
        return NotFound();
      }

      if (ModelState.IsValid)
      {
        try
        {
          testSession.LastUpdated = DateTime.UtcNow;
          _context.Update(testSession);
          await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
          if (!TestSessionExists(testSession.Id))
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
      return View(testSession);
    }

    // GET: TestSessions/Delete/5
    public async Task<IActionResult> Delete(Guid? id)
    {
      if (id == null)
      {
        return NotFound();
      }

      var testSession = await _context.TestSessions
          .FirstOrDefaultAsync(m => m.Id == id);
      if (testSession == null)
      {
        return NotFound();
      }

      return View(testSession);
    }

    // POST: TestSessions/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
      var testSession = await _context.TestSessions.FindAsync(id);
      _context.TestSessions.Remove(testSession);
      await _context.SaveChangesAsync();
      return RedirectToAction(nameof(Index));
    }

    // GET: TestSessions/Register/5
    public async Task<IActionResult> Register(Guid? id)
    {
      if (id == null)
      {
        return NotFound();
      }

      var testSession = await _context.TestSessions.FindAsync(id);
      if (testSession == null)
      {
        return NotFound();
      }

      try
      {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
          throw new ApplicationException($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
        }

        var registeredIds = testSession.Testers.Items.Select(t => t.TesterId).ToHashSet<long>();
        if (registeredIds.Contains(user.ObjectId))
        {
          throw new ApplicationException($"User with ID '{_userManager.GetUserId(User)}' already registered.");
        }

        testSession.Testers.Add(new TesterItem {  TesterId = user.ObjectId, Grade = user.ExperienceLevel, Group = user.Continent.ToString()});
        testSession.LastUpdated = DateTime.UtcNow;
        testSession.Testers = testSession.Testers; //Just give the ProtoBuff mechanism a kick
        _context.Update(testSession);
        await _context.SaveChangesAsync();
      }
      catch (DbUpdateConcurrencyException)
      {
        if (!TestSessionExists(testSession.Id))
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

    // GET: TestSessions/AddQuestion
    public IActionResult AddQuestion(Guid id)
    {
      return View(new AddQuestionViewModel { TestSessionId = id});
    }

    // POST: TestSessions/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuestion(Guid id, AddQuestionViewModel testQuestionItem)
    {
      var testSession = await _context.TestSessions.FindAsync(id);
      if (testSession == null)
      {
        return NotFound();
      }

      var addedQuestionIds = testSession.TestQuestions.Select(q => q.QuestionId).ToHashSet<long>();
      if (addedQuestionIds.Contains(testQuestionItem.QuestionId))
        throw new ApplicationException($"Question with ID '{testQuestionItem.QuestionId}' already added.");

      testSession.TestQuestions.Add(new TestQuestionItem { Idx = testSession.TestQuestions.Count, QuestionId = testQuestionItem.QuestionId, PenaltyPoint = testQuestionItem.PenaltyPoint, ScorePoint = testQuestionItem.ScorePoint });
      testSession.TestQuestions = testSession.TestQuestions;
      testSession.LastUpdated = DateTime.UtcNow;
      _context.Update(testSession);
      await _context.SaveChangesAsync();
      return RedirectToAction(nameof(Index));
    }

    private bool TestSessionExists(Guid id)
    {
      return _context.TestSessions.Any(e => e.Id == id);
    }
  }
}
