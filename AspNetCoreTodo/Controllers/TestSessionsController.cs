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

      if (user == null)
        return RedirectToAction("Login", "Account");

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Details(Guid? id, TestSessionsViewModel viewModel)
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

      if (viewModel.DistinctQuestionIds.Count != viewModel.TestScores.Count)
        return BadRequest("Question and scores not matching");

      testSession.Testers.Items.Clear();
      foreach (var tester in viewModel.DistinctTesters)
      {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.ObjectId == tester);
        if (user == null) continue;
        testSession.Testers.Add(new TesterItem(){TesterId = tester, Grade = user.ExperienceLevel, Group = user.Continent.ToString()});
      }

      testSession.TestQuestions.Clear();
      for (var idx =0; idx < viewModel.DistinctQuestionIds.Count; ++idx)
      {
        var questionId = viewModel.DistinctQuestionIds[idx];
        var q = await _context.TestQuestions.FirstOrDefaultAsync(qt => qt.ObjectId == questionId);
        if (q == null) continue;
        testSession.TestQuestions.Add(new TestQuestionItem()
        {
          Idx = idx, PenaltyPoint = -1, QuestionId = questionId, ScorePoint = viewModel.TestScores[idx]
        });
      }

      testSession.LastUpdatedLocal = DateTime.Now;
      testSession.Testers = testSession.Testers;
      testSession.TestQuestions = testSession.TestQuestions;
      _context.Update(testSession);
      await _context.SaveChangesAsync();

      return RedirectToAction(nameof(Index));
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
      var currentUser = await _userManager.GetUserAsync(User);
      var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);
      if (!isAdmin)
        return BadRequest("Only user with admin permission can create a test session");

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

      var currentUser = await _userManager.GetUserAsync(User);
      var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);
      if (!isAdmin)
        return BadRequest("Only user with admin permission can edit a test session");

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
    public async Task<IActionResult> Edit(Guid id, [Bind("Id,ObjectId,Name,Description,PlannedStart,PlannedEnd,TesterData,TestQuestionData,LastUpdated,PlannedStartLocal,PlannedEndLocal")] TestSession testSession)
    {
      if (id != testSession.Id)
      {
        return NotFound();
      }

      var currentUser = await _userManager.GetUserAsync(User);
      var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);
      if (!isAdmin)
        return Challenge();

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

      var currentUser = await _userManager.GetUserAsync(User);
      var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);
      if (!isAdmin)
        return Challenge();

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
    public async Task<IActionResult> AddQuestion(Guid id)
    {
      var currentUser = await _userManager.GetUserAsync(User);
      var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);
      if (!isAdmin)
        return Challenge();

      return View(new AddQuestionViewModel { TestSessionId = id});
    }

    // POST: TestSessions/AddQuestion
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddQuestion(Guid id, AddQuestionViewModel testQuestionItem)
    {
      _testSessionService.AddQuestion(id, testQuestionItem.QuestionId, testQuestionItem.ScorePoint, testQuestionItem.PenaltyPoint);
      return RedirectToAction(nameof(Index));
    }

    // GET: TestSessions/Register/5
    public async Task<IActionResult> Register(Guid id)
    {
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

      if (DateTime.UtcNow < testSession.PlannedStart) //Test time has not arrived yet
        return BadRequest($"Test start time has not arrived yet, please visit back after {testSession.PlannedStart}");

      var testUser = await _userManager.GetUserAsync(User);
      _testSessionService.CreateNewTestResult(testSession.ObjectId, testUser.ObjectId);

      var testResult = _testSessionService.GetTestResults(testSession.ObjectId).FirstOrDefault(tr => tr.UserId == testUser.ObjectId);

      if (questionIdx >= testSession.TestQuestions.Count) //The user may reach this state by keep moving to next question without submitting answers
      {
        return RedirectToAction(nameof(FinalSubmit), new { id = id });
      }

      var testQuestionItem = testSession.TestQuestions.Items[questionIdx];
      var testQuestion = _context.TestQuestions.Where(q => q.ObjectId == testQuestionItem.QuestionId)
        .Include(q => q.QuestionImage).FirstOrDefault();
      if (testQuestion == null)
        return BadRequest($"This test data of session {testSession.Name} has been corrupted, please contact with the administrator");

      if (testResult == null)
      {
        return NotFound();
      }

      var vm = new NextQuestionDetailViewModel(testQuestion, id, testSession.Name, questionIdx)
        {ScorePoint = testQuestionItem.ScorePoint, PenaltyPoint = testQuestionItem.PenaltyPoint};
      var testResultItem = testResult.TestResults.Items.FirstOrDefault(ttr => ttr.QuestionId == testQuestion.ObjectId);
      if (testResultItem != null)
      {
        vm.TextAnswer = testResultItem.Answer;
      }

      return View(vm);
    }

    public async Task<IActionResult> TestSessionResult(Guid? sessionId, long userId, string userName)
    {
      //If group id provided, show the points for the whole team, otherwise, only the user points
      if (sessionId == null)
      {
        return NotFound();
      }

      var user = await _userManager.GetUserAsync(User);
      if (user.ObjectId != userId)
      {
        var isAdmin = user != null && await _userManager.IsInRoleAsync(user, Constants.AdministratorRole);
        if (!isAdmin)
          return BadRequest($"Only allowed to view the details of your own or pre-authorized test results");
      }

      var latestSession = await _context.TestSessions.FindAsync(sessionId.Value);

      if (latestSession == null)
      {
        return NotFound();
      }

      var testResult = _testSessionService.GetTestResults(latestSession.ObjectId)
        .FirstOrDefault(tr => tr.UserId == userId);

      if (testResult == null)
        return NotFound();

      var viewModel = new TestResultDetailViewModel(latestSession.Id, userName, testResult);
      return View(viewModel);
    }

    public async Task<IActionResult> ReviewQuestion(Guid? id,  long userId, long questionId, int? questionIdx)
    {
      //If group id provided, show the points for the whole team, otherwise, only the user points
      if (id == null)
      {
        return NotFound();
      }

      var testSession = await _context.TestSessions.FindAsync(id);
      if (testSession == null)
      {
        return NotFound();
      }

      var testUser = await _userManager.GetUserAsync(User);
      var testResult = _testSessionService.GetTestResults(testSession.ObjectId).FirstOrDefault(tr => tr.UserId == userId);

      if (testUser == null || testUser.UserStatus != UserStatus.Active || testResult == null)
        return NotFound();

      for (int idx = 0; idx < testSession.TestQuestions.Count; ++idx)
      {
        var testQuestionItem = testSession.TestQuestions.Items[idx];
        if ((questionIdx.HasValue && idx == questionIdx.Value) || testQuestionItem.QuestionId == questionId)
        {
          var testQuestion = await _context.TestQuestions.Include(q => q.QuestionImage).FirstOrDefaultAsync(q => q.ObjectId == testQuestionItem.QuestionId);
          var testResultItem = testResult.TestResults.Items.FirstOrDefault(ttr => ttr.QuestionId == testQuestion.ObjectId);
          var vm = new ReviewQuestionViewModel(testQuestion, id.Value, testSession.Name, idx)
            {
              ScorePoint = testQuestionItem.ScorePoint,
              PenaltyPoint = testQuestionItem.PenaltyPoint,
              UserId = testResult.UserId,
              TextAnswer = testResultItem?.Answer ?? "",
              ActualScore = testResultItem?.Score ?? 0,
              CorrectAnswer = testQuestion.TestAnswer?.TextAnswer,
              ShowAnswer = false
        };
          return View(vm);
        }
      }

      return RedirectToAction(nameof(TestInstruction), new {id = id});
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ReviewQuestion([Bind(@"UserId,SessionId,TestSessionName,
                                                           QuestionText,ImageId,CorrectAnswer,
                                                           QuestionIdx,TextAnswer,ShowAnswer,
                                                           ScorePoint,PenaltyPoint,ActualScore,TheTip")]ReviewQuestionViewModel Model)
    {
      //Model.ShowAnswer = !Model.ShowAnswer;
      return View(Model);
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

      testResult.TestEnded = testResult.TestEnded > DateTime.UtcNow ? testResult.TestEnded : DateTime.UtcNow;
      bool runOvertime = (testResult.TestStarted >= testSession.PlannedStart && (testResult.TestEnded - testResult.TestStarted) >= testSession.SessionTimeSpan);
      var testResultItem =
        testResult.TestResults.Items.FirstOrDefault(ttr => ttr.QuestionId == testQuestion.ObjectId);
      if (testResultItem == null)
      {
        var cleanedTestAnswer = viewModel.TextAnswer.TrimEnd();
        testResultItem = new TestResultItem() {Answer = cleanedTestAnswer, QuestionId = testQuestion.ObjectId,};
        testResult.TestResults.Add(testResultItem);
      }
      else if (!runOvertime)
      {
        testResultItem.Answer = viewModel.TextAnswer;
      }

      double score = _testSessionService.JudgeAnswer(testSession, testQuestion, ref testResultItem);
      testResult.FinalScore = testResult.TestResults.Items.Sum(t => t.Score);
      testResult.MaximumScore = testSession.TestQuestions.Items.Sum(q => q.ScorePoint);
      testResult.TestResults = testResult.TestResults;
      if (testResult.TestStarted < testSession.PlannedStart)
      {
        testResult.TestStarted = DateTime.UtcNow;
      }

      testResult.TestEnded =
        runOvertime ? testResult.TestStarted.Add(testSession.SessionTimeSpan) : testResult.TestEnded;
      _context.Update(testResult);
      await _context.SaveChangesAsync();

      if (runOvertime)
        return RedirectToAction(nameof(TestSessionResult), new { sessionId = id, userId = testUser.ObjectId, userName = testUser.UserName });
      else if (questionIdx >= testSession.TestQuestions.Count - 1)
        return RedirectToAction(nameof(FinalSubmit), new { id = id });

      return RedirectToAction(nameof(NextQuestion), new {id = id, questionIdx = questionIdx + 1});
    }

    // GET: TestSessions/FinalSubmit
    public async Task<IActionResult> FinalSubmit(Guid? id)
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

      var testUser = await _userManager.GetUserAsync(User);
      var testResult = _testSessionService.GetTestResults(testSession.ObjectId)
        .FirstOrDefault(tr => tr.UserId == testUser.ObjectId);
      if (testResult == null)
        return NotFound();

      return View(new FinalSubmitViewModel() { TestSessionId = id.Value, SessionName = testSession.Name, AllowedTimeSpan = testSession.SessionTimeSpan,
        TestStart = testResult.TestStarted.ToLocalTime(), SessionObjectId = testSession.ObjectId, UserObjectId = testUser.ObjectId, UserName = testUser.UserName });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FinalSubmit(Guid id, [Bind("SessionObjectId,UserName,UserObjectId,AllowedTimeSpan,SessionName")]FinalSubmitViewModel viewModel)
    {
      //Seal the test result by moving the test end time to the session end time
      var testResult = _testSessionService.GetTestResults(viewModel.SessionObjectId).FirstOrDefault(tr => tr.UserId == viewModel.UserObjectId);
      if (testResult != null)
      {
        testResult.TestEnded = testResult.TestStarted.Add(viewModel.AllowedTimeSpan.Add(TimeSpan.FromSeconds(1)));
        _context.Update(testResult);
        await _context.SaveChangesAsync();
      }
      else
      {
        _testSessionService.CreateNewTestResult(viewModel.SessionObjectId, viewModel.UserObjectId);
      }

      return RedirectToAction(nameof(TestSessionResult), new { sessionId = id, userId = viewModel.UserObjectId, userName = viewModel.UserName });
    }

    // GET: TestSessions/TestInstruction
    public async Task<IActionResult> TestInstruction(Guid? id)
    {
      if (id == null)
      {
        return NotFound();
      }

      var testSession = await _context.TestSessions.FindAsync(id);
      var testUser = await _userManager.GetUserAsync(User);

      if (testSession == null || testUser == null)
      {
        return NotFound();
      }

      if (!testSession.IsRegisteredUser(testUser.ObjectId))
      {
        return RedirectToAction(nameof(Register), new { id = id.Value});
      }

      var existingTestResult = _testSessionService.GetTestResults(testSession.ObjectId).FirstOrDefault(tr => tr.UserId == testUser.ObjectId);

      return View(new TestInstructionViewModel()
      {
        TestSessionId = id.Value,
        SessionName = testSession.Name,
        SessionDescription = testSession.Description,
        AllowedTimeSpan = testSession.SessionTimeSpan,
        SessionObjectId = testSession.ObjectId,
        UserObjectId = testUser.ObjectId,
        UserName = testUser.UserName,
        TestStart = existingTestResult?.TestStarted ?? DateTime.UtcNow,
        TotalQuestions = testSession.TestQuestions.Count,
        TotalScorePoints = testSession.TestQuestions.Sum(q => q.ScorePoint)
      });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestInstruction(Guid id, [Bind("SessionObjectId,UserName,UserObjectId,AllowedTimeSpan,SessionName, TestStart")]TestInstructionViewModel viewModel)
    {
       _testSessionService.CreateNewTestResult(viewModel.SessionObjectId, viewModel.UserObjectId);

      return RedirectToAction(nameof(NextQuestion), new { id = id, questionIdx = 0 });
    }

    // GET: TestSessions
    public async Task<IActionResult> ResetTimer(Guid id, long userId)
    {
      var currentUser = await _userManager.GetUserAsync(User);
      var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);

      if (!isAdmin)
        return BadRequest("Only user with admin role has the permission to restart the test");

      var testSession = await _context.TestSessions.FindAsync(id);
      if (testSession == null)
        return NotFound();

      var testResult = _testSessionService.GetTestResults(testSession.ObjectId).FirstOrDefault(tr => tr.UserId == userId);
      if (testResult == null)
        return RedirectToAction(nameof(Index));

      testResult.TestStarted = testSession.PlannedStart.AddMinutes(-1);
      _context.TestResults.Update(testResult);
      await _context.SaveChangesAsync();
      return RedirectToAction(nameof(Index));
    }


    private bool TestSessionExists(Guid id)
    {
      return _context.TestSessions.Any(e => e.Id == id);
    }
  }
}
