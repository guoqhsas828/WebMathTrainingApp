using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebMathTraining.Data;
using WebMathTraining.Models;
using WebMathTraining.Services;
using WebMathTraining.Views;

namespace WebMathTraining.Controllers
{
  public class TestSessionsController : Controller
  {
    private readonly TestDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITestSessionService _testSessionService;

    public TestSessionsController(TestDbContext context, UserManager<ApplicationUser> userManager, ITestSessionService service)
    {
      _context = context;
      _userManager = userManager;
      _testSessionService = service;
    }

    // GET: TestSessions
    public async Task<IActionResult> Index()
    {
      var user = await _userManager.GetUserAsync(User);
      var testSessions = new List<TestSession>();
      
      if (user.UserStatus != UserStatus.InActive)
      {
        testSessions = await _context.TestSessions.ToListAsync();
      }

      if (user.UserStatus == UserStatus.Trial)
      {
        testSessions = testSessions.Where(s => s.Name.StartsWith("Trial")).ToList();
      }

      return View(testSessions);
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
    public async Task<IActionResult> Create()
    {
      var currentUser = await _userManager.GetUserAsync(User);
      var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);
      if (isAdmin)
        return View();
      else
        return RedirectToAction(nameof(Index));
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
      _testSessionService.AddQuestion(id, testQuestionItem.QuestionId, testQuestionItem.ScorePoint, testQuestionItem.PenaltyPoint);
      return RedirectToAction(nameof(Index));
    }

    // GET: TestSessions/Register/5
    public async Task<IActionResult> Register(Guid id)
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

        _testSessionService.RegisterUser(id, user);
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

    // GET: TestSessions/NextQuestion
    public async Task<IActionResult> NextQuestion(Guid id, int questionIdx)
    {
      if (questionIdx < 0) return await Register(id);

      var testSession = await _context.TestSessions.FindAsync(id);
      if (testSession == null)
      {
        return NotFound();
      }

      if (questionIdx >= testSession.TestQuestions.Count)
      {
        var user = await _userManager.GetUserAsync(User);
        //Find a test group that contains this session
        var testGroup = _context.TestGroups.FirstOrDefault(g =>
          g.EnrolledSessionIds.Contains(testSession.ObjectId) && g.MemberObjectIds.Contains(user.ObjectId));

        if (testGroup == null)
          return NotFound();

        return RedirectToAction("Details", "TestGroups", new {id = testGroup.Id});
      }

      var testQuestionItem = testSession.TestQuestions.Items[questionIdx];
      var testQuestion = _context.TestQuestions.Where(q => q.ObjectId == testQuestionItem.QuestionId)
        .Include(q => q.QuestionImage).FirstOrDefault();
      if (testQuestion == null)
        return NotFound();

      var testUser = await _userManager.GetUserAsync(User);
      _testSessionService.CreateNewTestResult(testSession.ObjectId, testUser.ObjectId);

      var testResult = _testSessionService.GetTestResults(testSession.ObjectId)
        .FirstOrDefault(tr => tr.UserId == testUser.ObjectId);
      if (testResult == null)
      {
        return NotFound();
      }

      var vm = new NextQuestionDetailViewModel(testQuestion, id, testSession.Name, questionIdx)
        {TotalScore = testResult.FinalScore};
      var testResultItem = testResult.TestResults.Items.FirstOrDefault(ttr => ttr.QuestionId == testQuestion.ObjectId);
      if (testResultItem != null)
      {
        vm.TextAnswer = testResultItem.Answer;
      }

      return View(vm);
    }

    // POST: TestSessions/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitAnswer(Guid id, int questionIdx, NextQuestionDetailViewModel viewModel)
    {
      var testSession = await _context.TestSessions.FindAsync(id);
      if (testSession == null)
      {
        return NotFound();
      }

      var testQuestionItem = testSession.TestQuestions.Items[questionIdx];
      var testQuestion = _context.TestQuestions.Where(q => q.ObjectId == testQuestionItem.QuestionId)
        .Include(q => q.QuestionImage).FirstOrDefault();
      if (testQuestion == null)
        return NotFound();

      var testUser = await _userManager.GetUserAsync(User);
      var testResult = _testSessionService.GetTestResults(testSession.ObjectId)
        .FirstOrDefault(tr => tr.UserId == testUser.ObjectId);
      if (testResult == null)
        return NotFound();

      testResult.TestEnded = DateTime.UtcNow;
      bool runOvertime = ((testResult.TestEnded - testResult.TestStarted) > testSession.SessionTimeSpan);
      var testResultItem =
        testResult.TestResults.Items.FirstOrDefault(ttr => ttr.QuestionId == testQuestion.ObjectId);
      if (testResultItem == null)
      {
        testResultItem = new TestResultItem() {Answer = viewModel.TextAnswer, QuestionId = testQuestion.ObjectId,};
        testResult.TestResults.Add(testResultItem);
      }
      else if (!runOvertime)
      {
        testResultItem.Answer = viewModel.TextAnswer;
      }

      double score = _testSessionService.JudgeAnswer(testSession, testQuestion, ref testResultItem);
      testResult.FinalScore = testResult.TestResults.Items.Sum(t => t.Score);
      testResult.TestResults = testResult.TestResults;
      testResult.TestEnded =
        runOvertime ? testResult.TestStarted.Add(testSession.SessionTimeSpan) : testResult.TestEnded;
      _context.Update(testResult);
      await _context.SaveChangesAsync();

      return RedirectToAction("NextQuestion",
        new {id = id, questionIdx = runOvertime ? Int32.MaxValue : questionIdx + 1});
    }

    private bool TestSessionExists(Guid id)
    {
      return _context.TestSessions.Any(e => e.Id == id);
    }
  }
}
