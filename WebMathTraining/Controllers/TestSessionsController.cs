using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using StoreManager.Models;
using WebMathTraining.Models;
using WebMathTraining.Services;
using StoreManager.Interfaces;
using StoreManager.Specifications;

namespace WebMathTraining.Controllers
{
  [Route("[controller]/[action]")]
  public class TestSessionsController : Controller
  {
    private readonly IAppUserManageService _userContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITestSessionService<int> _testSessionService;
    private readonly ICatalogRepository<TestQuestion> _questionRepository;
    private readonly ICatalogRepository<TestImage> _testImageContext;
    private readonly ICatalogRepository<TestSession> _testSessions;
    private readonly ICatalogRepository<TestResult> _testResults;
    private readonly IBlobFileService _blobFileService;


    public TestSessionsController(ICatalogRepository<TestImage> testImageContext, IAppUserManageService userContext,
      UserManager<ApplicationUser> userManager,
      ITestSessionService<int> service,
      ICatalogRepository<TestSession> testSessions,
      ICatalogRepository<TestQuestion> testQuestions,
      ICatalogRepository<TestResult> testResults, IBlobFileService blobFileService)
    {
      _userContext = userContext;
      _userManager = userManager;
      _testSessionService = service;
      _testImageContext = testImageContext;
      _testSessions = testSessions;
      _questionRepository = testQuestions;
      _testResults = testResults;
      _blobFileService = blobFileService;
    }

    // GET: TestSessions
    public async Task<IActionResult> Index(int levelFilter)
    {
      var user = await _userManager.GetUserAsync(User);

      if (user == null)
        return RedirectToAction("Login", "Account");

      var userProfile = await _userContext.FindByUserEmail(user.Email);
      var testSessions = new List<TestSession>();

      var isAdmin = await _userManager.IsInRoleAsync(user, Constants.AdministratorRole);

      if (userProfile.UserStatus != UserStatus.InActive)
      {
        var list = await _testSessionService.FindAllAsync();
        testSessions = list.ToList();
        var privateSessionsNotForUser = isAdmin ? new HashSet<int>() : testSessions.Where(s => s.Description != null && s.Description.StartsWith("Private Test (", StringComparison.InvariantCultureIgnoreCase) && !s.Description.StartsWith($"Private Test ({user.UserName})", StringComparison.InvariantCultureIgnoreCase)).Select(s => s.ObjectId).ToHashSet();
        if (levelFilter > 0)
          testSessions = testSessions.Where(t =>  t.Category == TestCategory.Math && t.TargetGrade == levelFilter && !privateSessionsNotForUser.Contains(t.ObjectId)).ToList();
        else
          testSessions = testSessions.Where(t => t.Category == TestCategory.Math && !privateSessionsNotForUser.Contains(t.ObjectId)).ToList();
      }

      if (userProfile.UserStatus == UserStatus.Trial)
      {
        testSessions = testSessions.Where(s => s.Name.StartsWith("Trial")).ToList();
      }

      return View(testSessions);
    }

    public async Task<IActionResult> HistoryTest()
    {
      var user = await _userManager.GetUserAsync(User);

      if (user == null)
        return RedirectToAction("Login", "Account");

      var userProfile = await _userContext.FindByUserEmail(user.Email);
      var testSessions = new List<TestSession>();

      var isAdmin = await _userManager.IsInRoleAsync(user, Constants.AdministratorRole);

      if (userProfile.UserStatus != UserStatus.InActive)
      {
        var list = await _testSessionService.FindAllAsync();
        testSessions = list.ToList();
        var privateSessionsNotForUser = isAdmin ? new HashSet<int>() : testSessions.Where(s => s.Description != null && s.Description.StartsWith("Private Test (", StringComparison.InvariantCultureIgnoreCase) && !s.Description.StartsWith($"Private Test ({user.UserName})", StringComparison.InvariantCultureIgnoreCase)).Select(s => s.ObjectId).ToHashSet();
          testSessions = testSessions.Where(t => t.Category == TestCategory.History && !privateSessionsNotForUser.Contains(t.ObjectId)).ToList();
      }

      if (userProfile.UserStatus == UserStatus.Trial)
      {
        testSessions = testSessions.Where(s => s.Name.StartsWith("Trial")).ToList();
      }

      return View(testSessions);
    }

    // GET: TestSessions/Details/5
    public async Task<IActionResult> Details(int? id)
    {
      if (id == null)
      {
        return NotFound();
      }

      var testSession = await _testSessionService.GetTestSessionAsync(id.Value);
      if (testSession == null)
      {
        return NotFound();
      }

      return View(new TestSessionsViewModel(testSession));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Details(int? id, TestSessionsViewModel viewModel)
    {
      if (id == null)
      {
        return NotFound();
      }

      var testSession = await _testSessionService.GetTestSessionAsync(id.Value);
      if (testSession == null)
      {
        return NotFound();
      }

      if (viewModel.DistinctQuestionIds.Count != viewModel.TestScores.Count)
        return BadRequest("Question and scores not matching");

      testSession.Testers.Items.Clear();
      foreach (var tester in viewModel.DistinctTesters)
      {
        var user = await _userContext.FindById(tester);
        if (user == null) continue;
        testSession.Testers.Add(new TesterItem() { TesterId = tester, Grade = user.ExperienceLevel, Group = user.Continent.ToString() });
      }

      testSession.TestQuestions.Clear();
      for (var idx = 0; idx < viewModel.DistinctQuestionIds.Count; ++idx)
      {
        var questionId = viewModel.DistinctQuestionIds[idx];
        var q = await _questionRepository.GetByIdAsync(questionId);
        if (q == null) continue;
        testSession.TestQuestions.Add(new TestQuestionItem()
        {
          Idx = idx,
          PenaltyPoint = -1,
          QuestionId = questionId,
          ScorePoint = viewModel.TestScores[idx]
        });
      }

      testSession.TargetGrade = viewModel.TargetGrade;
      testSession.LastUpdatedLocal = DateTime.Now;
      testSession.Testers = testSession.Testers;
      testSession.TestQuestions = testSession.TestQuestions;
      await _testSessions.UpdateAsync(testSession);

      return RedirectToAction(nameof(Index));
    }

    // GET: TestSessions/Create
    public IActionResult Create()
    {
      //var currentUser = await _userManager.GetUserAsync(User);
      //var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);
      //if (isAdmin)
      return View();
      //else
      //  return RedirectToAction(nameof(Index));
    }

    // GET: TestSessions/Create
    public IActionResult CreateHistory()
    {
      //var currentUser = await _userManager.GetUserAsync(User);
      //var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);
      //if (isAdmin)
      return View();
      //else
      //  return RedirectToAction(nameof(Index));
    }

    // POST: TestSessions/Create
    // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
    // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Name,Description,PlannedStart,PlannedEnd,QuestionRequest,TargetGrade, Category")] TestSession testSession)
    {
      var user = await _userManager.GetUserAsync(User);
      var isAdmin = user != null && await _userManager.IsInRoleAsync(user, Constants.AdministratorRole);
      if (!isAdmin)
      {
        testSession.Description = $"Private Test ({user?.UserName ?? "?"}) Created {DateTime.Now}";
      }

      testSession.Category = TestCategory.Math;
      var currentUser = await _userContext.FindByUserEmail(user?.Email);
      if (currentUser == null || currentUser.ExperienceLevel <= 0)
        return BadRequest(
          "Only valid user with properly configured Grade information can create test sessions, please update your profile");

      if (ModelState.IsValid)
      {
        //testSession.Id = Guid.NewGuid();
        testSession.LastUpdated = DateTime.UtcNow;
        if (testSession.TargetGrade <= 0) testSession.TargetGrade = currentUser.ExperienceLevel;
        var targetTestGrade = testSession.TargetGrade;
        var testQuestions = _questionRepository.ListAsync(new TestQuestionFilterSpecification(targetTestGrade - 1, TestCategory.Math)).Result.Select(q => new Tuple<int, int>(q.ObjectId, q.Level)).ToList();

        var questionList = new List<Tuple<int, int>>();
        if (testQuestions.Count <= testSession.QuestionRequest)
        {
          questionList.AddRange(testQuestions);
        }
        else if (testSession.QuestionRequest > 0)
        {
          var random = new Random(DateTime.Now.Second);
          HashSet<int> picked = new HashSet<int>();
          while (picked.Count < testSession.QuestionRequest)
            picked.Add(random.Next(testQuestions.Count));

          foreach (var idx in picked)
            questionList.Add(testQuestions[idx]);
        }

        foreach (var q in questionList)
        {
          testSession.TestQuestions.Add(new TestQuestionItem()
          {
            Idx = testSession.TestQuestions.Count,
            PenaltyPoint = -1,
            QuestionId = q.Item1,
            ScorePoint = q.Item2 > currentUser.ExperienceLevel ? 5.0 : (q.Item2 < currentUser.ExperienceLevel ? 3.0 : 4.0)
          });
        }

        testSession.TestQuestions = testSession.TestQuestions;
        await _testSessions.AddAsync(testSession);
        return RedirectToAction(nameof(Index));
      }
      return View(testSession);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateHistory([Bind("Id,Name,Description,PlannedStart,PlannedEnd,QuestionRequest,TargetGrade, Category")] TestSession testSession)
    {
      var user = await _userManager.GetUserAsync(User);
      var isAdmin = user != null && await _userManager.IsInRoleAsync(user, Constants.AdministratorRole);
      if (!isAdmin)
      {
        testSession.Description = $"Private Test ({user?.UserName ?? "?"}) Created {DateTime.Now}";
      }

      testSession.Category = TestCategory.History;
      var currentUser = await _userContext.FindByUserEmail(user?.Email);
      if (currentUser == null || currentUser.ExperienceLevel <= 0)
        return BadRequest(
          "Only valid user with properly configured Grade information can create test sessions, please update your profile");

      if (ModelState.IsValid)
      {
        //testSession.Id = Guid.NewGuid();
        testSession.LastUpdated = DateTime.UtcNow;
        if (testSession.TargetGrade <= 0) testSession.TargetGrade = currentUser.ExperienceLevel;
        var targetTestGrade = testSession.TargetGrade;
        var testQuestions = _questionRepository.ListAsync(new TestQuestionFilterSpecification(targetTestGrade - 1, TestCategory.History)).Result.Select(q => new Tuple<int, int>(q.ObjectId, q.Level)).ToList();

        var questionList = new List<Tuple<int, int>>();
        if (testQuestions.Count <= testSession.QuestionRequest)
        {
          questionList.AddRange(testQuestions);
        }
        else if (testSession.QuestionRequest > 0)
        {
          var random = new Random(DateTime.Now.Second);
          HashSet<int> picked = new HashSet<int>();
          while (picked.Count < testSession.QuestionRequest)
            picked.Add(random.Next(testQuestions.Count));

          foreach (var idx in picked)
            questionList.Add(testQuestions[idx]);
        }

        foreach (var q in questionList)
        {
          testSession.TestQuestions.Add(new TestQuestionItem()
          {
            Idx = testSession.TestQuestions.Count,
            PenaltyPoint = -1,
            QuestionId = q.Item1,
            ScorePoint = q.Item2 > currentUser.ExperienceLevel ? 5.0 : (q.Item2 < currentUser.ExperienceLevel ? 3.0 : 4.0)
          });
        }

        testSession.TestQuestions = testSession.TestQuestions;
        await _testSessions.AddAsync(testSession);
        return RedirectToAction(nameof(HistoryTest));
      }
      return View(testSession);
    }

    // GET: TestSessions/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
      if (id == null)
      {
        return NotFound();
      }

      var currentUser = await _userManager.GetUserAsync(User);
      var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);
      if (!isAdmin)
        return BadRequest("Only user with admin permission can edit a test session");

      var testSession = await _testSessionService.GetTestSessionAsync(id.Value);
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
    public async Task<IActionResult> Edit(int id, [Bind("Id,ObjectId,Name,Description,PlannedStart,PlannedEnd,TargetGrade,TesterData,TestQuestionData,LastUpdated,PlannedStartLocal,PlannedEndLocal")] TestSession testSession)
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
          await _testSessions.UpdateAsync(testSession);
        }
        catch (DbUpdateConcurrencyException)
        {
          if (_testSessions.GetByIdAsync(testSession.Id).Result == null)
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
    public async Task<IActionResult> Delete(int? id)
    {
      if (id == null)
      {
        return NotFound();
      }

      var currentUser = await _userManager.GetUserAsync(User);
      var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);
      if (!isAdmin)
        return Challenge();

      var testSession = await _testSessionService.GetTestSessionAsync(id.Value);
      if (testSession == null)
      {
        return NotFound();
      }

      return View(testSession);
    }

    // POST: TestSessions/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
      //Need to remove all test results related to such session
      await _testSessionService.DeleteTestSessionAsync(id);
      return RedirectToAction(nameof(Index));
    }

    // GET: TestSessions/AddQuestion
    public async Task<IActionResult> AddQuestion(int id)
    {
      var currentUser = await _userManager.GetUserAsync(User);
      var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);
      if (!isAdmin)
        return Challenge();

      return View(new AddQuestionViewModel { TestSessionId = id });
    }

    // POST: TestSessions/AddQuestion
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddQuestion(int id, AddQuestionViewModel testQuestionItem)
    {
      _testSessionService.AddQuestion(id, testQuestionItem.QuestionId, testQuestionItem.ScorePoint, testQuestionItem.PenaltyPoint);
      return RedirectToAction(nameof(Index));
    }

    // GET: TestSessions/Register/5
    public async Task<IActionResult> Register(int id)
    {
      var testSession = await _testSessionService.GetTestSessionAsync(id);
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

        var userProfile = await _userContext.FindByUserEmail(user.Email);
        _testSessionService.RegisterUser(id, userProfile);
      }
      catch (DbUpdateConcurrencyException)
      {
        if (_testSessions.GetByIdAsync(testSession.Id).Result == null)
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
    public async Task<IActionResult> NextQuestion(int id, int questionIdx)
    {
      if (questionIdx < 0) return await Register(id);

      var testSession = await _testSessionService.GetTestSessionAsync(id);
      if (testSession == null)
      {
        return NotFound();
      }

      if (DateTime.UtcNow < testSession.PlannedStart) //Test time has not arrived yet
        return BadRequest($"Test start time has not arrived yet, please visit back after {testSession.PlannedStart}");

      var user = await _userManager.GetUserAsync(User);
      var testUser = await _userContext.FindByUserEmail(user.Email);
      _testSessionService.CreateNewTestResult(testSession.ObjectId, testUser.ObjectId);

      var testResult = await _testSessionService.GetTestResultAsync(testSession.ObjectId, testUser.ObjectId);

      if (questionIdx >= testSession.TestQuestions.Count) //The user may reach this state by keep moving to next question without submitting answers
      {
        return RedirectToAction(nameof(FinalSubmit), new { id = id });
      }

      var testQuestionItem = testSession.TestQuestions.Items[questionIdx];
      var testQuestion = await _questionRepository.GetByIdAsync(testQuestionItem.QuestionId);
      if (testQuestion == null)
        return BadRequest($"This test data of session {testSession.Name} has been corrupted, please contact with the administrator");

      if (testResult == null)
      {
        return NotFound();
      }

      var testImage = await _testImageContext.GetByIdAsync(testQuestion.QuestionImageId);
      var vm = new NextQuestionDetailViewModel(testQuestion, testImage, id, testSession.Name, questionIdx)
      { ScorePoint = testQuestionItem.ScorePoint, PenaltyPoint = testQuestionItem.PenaltyPoint };
      var testResultItem = testResult.TestResults.Items.FirstOrDefault(ttr => ttr.QuestionId == testQuestion.ObjectId);
      if (testResultItem != null)
      {
        vm.TextAnswer = testResultItem.Answer;
      }

      return View(vm);
    }

    public async Task<IActionResult> TestSessionResult(int? sessionId, int userId, string userName)
    {
      //If group id provided, show the points for the whole team, otherwise, only the user points
      if (sessionId == null)
      {
        return NotFound();
      }

      var user = await _userManager.GetUserAsync(User);
      var userProfile = await _userContext.FindByUserEmail(user.Email);
      if (userProfile.ObjectId != userId)
      {
        var isAdmin = user != null && await _userManager.IsInRoleAsync(user, Constants.AdministratorRole);
        if (!isAdmin)
          return BadRequest($"Only allowed to view the details of your own or pre-authorized test results");
      }

      var latestSession = await _testSessionService.GetTestSessionAsync(sessionId.Value);

      if (latestSession == null)
      {
        return NotFound();
      }

      var testResult = await _testSessionService.GetTestResultAsync(latestSession.ObjectId, userId);

      if (testResult == null)
        return NotFound();

      var viewModel = new TestResultDetailViewModel(latestSession.Id, userName, testResult);
      return View(viewModel);
    }

    public async Task<IActionResult> ReviewQuestion(int? id, int userId, int questionId, int? questionIdx)
    {
      //If group id provided, show the points for the whole team, otherwise, only the user points
      if (id == null)
      {
        return NotFound();
      }

      var testSession = await _testSessionService.GetTestSessionAsync(id.Value);
      if (testSession == null)
      {
        return NotFound();
      }

      var testUser = await _userManager.GetUserAsync(User);
      var userProfile = await _userContext.FindByUserEmail(testUser?.Email);
      var testResult = await _testSessionService.GetTestResultAsync(testSession.ObjectId, userId);
      var requestUser = await _userContext.FindById(userId);
      if (testUser == null || userProfile.UserStatus != UserStatus.Active || testResult == null)
        return NotFound();

      for (int idx = 0; idx < testSession.TestQuestions.Count; ++idx)
      {
        var testQuestionItem = testSession.TestQuestions.Items[idx];
        if ((questionIdx.HasValue && idx == questionIdx.Value) || testQuestionItem.QuestionId == questionId)
        {
          var testQuestion = await _questionRepository.GetByIdAsync(testQuestionItem.QuestionId);
          var testImage = await _testImageContext.GetByIdAsync(testQuestion.QuestionImageId);
          var testResultItem = testResult.TestResults.Items.FirstOrDefault(ttr => ttr.QuestionId == testQuestion.ObjectId);
          var vm = new ReviewQuestionViewModel(testQuestion, testImage, id.Value, testSession.Name, idx)
          {
            ScorePoint = testQuestionItem.ScorePoint,
            PenaltyPoint = testQuestionItem.PenaltyPoint,
            UserId = testResult.UserId,
            TextAnswer = testResultItem?.Answer ?? "",
            ActualScore = testResultItem?.Score ?? 0,
            CorrectAnswer = testQuestion.TestAnswer?.TextAnswer,
            ShowAnswer = false,
            TestUserName = requestUser?.FirstName
          };
          return View(vm);
        }
      }

      return RedirectToAction(nameof(TestInstruction), new { id = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ReviewQuestion([Bind(@"UserId,SessionId,TestSessionName,
                                                           QuestionText,ImageId,CorrectAnswer,
                                                           QuestionIdx,TextAnswer,ShowAnswer,
                                                           ScorePoint,PenaltyPoint,ActualScore,TheTip, TestUserName")]ReviewQuestionViewModel Model)
    {
      Model.ShowAnswer = !Model.ShowAnswer;
      return View(Model);
    }

    // POST: TestSessions/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitAnswer(int id, int questionIdx, NextQuestionDetailViewModel viewModel)
    {
      var testSession = await _testSessionService.GetTestSessionAsync(id);
      if (testSession == null)
      {
        return NotFound();
      }

      var testQuestionItem = testSession.TestQuestions.Items[questionIdx];
      var testQuestion = await _questionRepository.GetByIdAsync(testQuestionItem.QuestionId);
      if (testQuestion == null)
        return NotFound();

      var user = await _userManager.GetUserAsync(User);
      var testUser = await _userContext.FindByUserEmail(user.Email);
      var testResult = await _testSessionService.GetTestResultAsync(testSession.ObjectId, testUser.ObjectId);
      if (testResult == null)
        return NotFound();

      testResult.TestEnded = testResult.TestEnded > DateTime.UtcNow ? testResult.TestEnded : DateTime.UtcNow;
      bool runOvertime = (testResult.TestStarted >= testSession.PlannedStart && (testResult.TestEnded - testResult.TestStarted) >= testSession.SessionTimeSpan);
      var cleanedTestAnswer = viewModel.TextAnswer.TrimEnd();

      var testResultNew = await _testSessionService.JudgeAnswerAsync(testSession, testQuestion, testUser.ObjectId, cleanedTestAnswer);

      await _testResults.UpdateAsync(testResultNew);

      if (runOvertime)
        return RedirectToAction(nameof(TestSessionResult), new { sessionId = id, userId = testUser.ObjectId, userName = testUser.UserName });
      else if (questionIdx >= testSession.TestQuestions.Count - 1)
        return RedirectToAction(nameof(FinalSubmit), new { id = id });

      return RedirectToAction(nameof(NextQuestion), new { id = id, questionIdx = questionIdx + 1 });
    }

    // GET: TestSessions/FinalSubmit
    public async Task<IActionResult> FinalSubmit(int? id)
    {
      if (id == null)
      {
        return NotFound();
      }

      var testSession = await _testSessionService.GetTestSessionAsync(id.Value);
      if (testSession == null)
      {
        return NotFound();
      }

      var user = await _userManager.GetUserAsync(User);
      var testUser = await _userContext.FindByUserEmail(user.Email);
      var testResult = await _testSessionService.GetTestResultAsync(testSession.ObjectId, testUser.ObjectId);
      if (testResult == null)
        return NotFound();

      return View(new FinalSubmitViewModel()
      {
        TestSessionId = id.Value,
        SessionName = testSession.Name,
        AllowedTimeSpan = testSession.SessionTimeSpan,
        TestStart = testResult.TestStarted.ToLocalTime(),
        SessionObjectId = testSession.ObjectId,
        UserObjectId = testUser.ObjectId,
        UserName = testUser.UserName
      });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FinalSubmit(int id, [Bind("SessionObjectId,UserName,UserObjectId,AllowedTimeSpan,SessionName")]FinalSubmitViewModel viewModel)
    {
      //Seal the test result by moving the test end time to the session end time
      var testResult = await _testSessionService.GetTestResultAsync(viewModel.SessionObjectId, viewModel.UserObjectId);
      if (testResult != null)
      {
        var user = await _userManager.GetUserAsync(User);
        testResult.TestEnded = testResult.TestStarted.Add(viewModel.AllowedTimeSpan.Add(TimeSpan.FromSeconds(1)));
        await _testResults.UpdateAsync(testResult);

        await _userContext.IncreaseUserPoints(user.Email, Math.Max(testResult.FinalScore - testResult.MaximumScore * 0.8, 0.0));
      }
      else
      {
        _testSessionService.CreateNewTestResult(viewModel.SessionObjectId, viewModel.UserObjectId);
      }

      return RedirectToAction(nameof(TestSessionResult), new { sessionId = id, userId = viewModel.UserObjectId, userName = viewModel.UserName });
    }

    // GET: TestSessions/TestInstruction
    public async Task<IActionResult> TestInstruction(int? id)
    {
      if (id == null)
      {
        return NotFound();
      }

      var testSession = await _testSessionService.GetTestSessionAsync(id.Value);
      var user = await _userManager.GetUserAsync(User);
      var testUser = await _userContext.FindByUserEmail(user.Email);

      if (testSession == null || testUser == null)
      {
        return NotFound();
      }

      if (!testSession.IsRegisteredUser(testUser.ObjectId))
      {
        return RedirectToAction(nameof(Register), new { id = id.Value });
      }

      var existingTestResult = await _testSessionService.GetTestResultAsync(testSession.ObjectId, testUser.ObjectId);

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
    public async Task<IActionResult> TestInstruction(int id, [Bind("SessionObjectId,UserName,UserObjectId,AllowedTimeSpan,SessionName, TestStart")]TestInstructionViewModel viewModel)
    {
      await _testSessionService.CreateNewTestResult(viewModel.SessionObjectId, viewModel.UserObjectId);

      return RedirectToAction(nameof(NextQuestion), new { id = id, questionIdx = 0 });
    }

    // GET: TestSessions
    public async Task<IActionResult> ResetTimer(int id, int userId)
    {
      var currentUser = await _userManager.GetUserAsync(User);
      var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);

      if (!isAdmin)
        return BadRequest("Only user with admin role has the permission to restart the test");

      var testSession = await _testSessionService.GetTestSessionAsync(id);
      if (testSession == null)
        return NotFound();

      var testResult = await _testSessionService.GetTestResultAsync(testSession.ObjectId, userId);
      if (testResult == null)
        return RedirectToAction(nameof(Index));

      testResult.TestStarted = testSession.PlannedStart.AddMinutes(-1);
      await _testResults.UpdateAsync(testResult);
      return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> GetTestImageFile(int id)
    {
      var image = await _testImageContext.GetByIdAsync(id);
      if (image == null || String.Compare(image.ContentType, "Text", StringComparison.InvariantCultureIgnoreCase) == 0)
      {
        return null;
      }

      byte[] imageBytes;
      string contentType;
      if (image.Width != CloudContainer.None)
      {
        //string base64Str = Convert.ToBase64String(image.Data);
        //imageBytes = Convert.FromBase64String(base64Str);
        var fileName = image.Name;
        var containerName = image.Width.ToString();

        if (fileName.IndexOf('.') < 0) fileName += ".PNG";
        var cloudData = await _blobFileService.DownloadBlobToByteArrayAsync(fileName, containerName);
        imageBytes = cloudData.Item1;
        contentType = cloudData.Item2;
      }
      else
      {
        imageBytes = image.Data;
        contentType = image.ContentType;
      }

      FileResult imageUserFile = File(imageBytes, contentType);
      return imageUserFile;
    }
  }
}
