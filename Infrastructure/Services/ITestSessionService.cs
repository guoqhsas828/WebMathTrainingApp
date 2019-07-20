using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using StoreManager.Models;
using StoreManager.Specifications;
using StoreManager.Interfaces;
using WebMathTraining.Models;

namespace WebMathTraining.Services
{
  public interface ITestSessionService<T> where T : struct
  {
    T CreateNewSession(string name);
    void RegisterUser(T sessionId, UserProfile user);
    Task<TestSession> GetTestSessionAsync(T id);
    Task DeleteTestSessionAsync(T id);
    void AddQuestion(T sessionId, T questionId, double scorePoint, double penaltyPoint=0.0);
    Task<TestResult> JudgeAnswerAsync(TestSession test, TestQuestion question, T testerId, string answer); //Judge the answer and return the score point
    Task<IReadOnlyList<TestResult>> GetTestResultsAsync(T sessionId);
    Task<TestResult> GetTestResultAsync(T sessionId, T userId);
    Task CreateNewTestResult(T sessionId, T userId);
    Task<T> CreateNewTestGroup(string groupName);
    Task<IList<TestGroup>> FindAllTestGroupAsync(UserProfile user);
    Task<TestGroup> FindTestGroupAsyncById(T groupId);
    Task<TestGroup> FindTestGroupAsyncByName(string groupName);
    bool AddSessionIntoTestGroup(T sessionId, string groupName);
    Task<IReadOnlyList<TestSession>> FindAllAsync();
  }

  public class TestSessionService : ITestSessionService<int>
  {
    private readonly ICatalogRepository<TestQuestion> _testQuestionRepository;
    private readonly ICatalogRepository<TestResult> _testResultRepository;
    private readonly ICatalogRepository<TestGroup> _testGroupRepository;
    private readonly ICatalogRepository<TestSession> _testSessionRepository;
    
    public TestSessionService(ICatalogRepository<TestQuestion> testQuestionRepo, ICatalogRepository<TestResult> testResultRepository, ICatalogRepository<TestGroup> testGroupRepo, ICatalogRepository<TestSession> testSessionRepo)
    {
      _testQuestionRepository = testQuestionRepo;
      _testResultRepository = testResultRepository;
      _testGroupRepository = testGroupRepo;
      _testSessionRepository = testSessionRepo;
    }

    public async Task<TestResult> GetTestResultAsync(int sessionId, int userId)
    {
      var list = await GetTestResultsAsync(sessionId);
      return list.FirstOrDefault(tr => tr.UserId == userId);
    }

    public async Task<TestSession> GetTestSessionAsync(int id)
    {
      return await _testSessionRepository.GetByIdAsync(id);
    }

    public async Task DeleteTestSessionAsync(int id)
    {
      var testSession = await GetTestSessionAsync(id);
      if (testSession == null)
        return;
      var testResults = await GetTestResultsAsync(testSession.ObjectId);
      foreach (var result in testResults)
      {
        await _testResultRepository.DeleteAsync(result);
      }

      await _testSessionRepository.DeleteAsync(testSession);
    }

    public async Task<int> CreateNewTestGroup(string groupName)
    {
      var existingTestGroup = await FindTestGroupAsyncByName(groupName);
      if (existingTestGroup != null) return existingTestGroup.Id;

      var testGroup = new TestGroup()
      {
        Name = groupName,
        Description = groupName,
        LastUpdated = DateTime.UtcNow,

      };
      await _testGroupRepository.AddAsync(testGroup);

      return testGroup.Id;
    }

    public async Task<IList<TestGroup>> FindAllTestGroupAsync(UserProfile user)
    {
      var list = await _testGroupRepository.ListAllAsync();

      if (user == null || user.UserStatus != UserStatus.Active)
      {
        return list.Where(g => g.Name.StartsWith("Trial")).ToArray();
      }

      return list.Where(g => g.Name.StartsWith("Trial") || g.MemberObjectIds.Contains(user.ObjectId)).ToArray();
    }

    public async Task<TestGroup> FindTestGroupAsyncByName(string groupName)
    {
      var list = await _testGroupRepository.ListAsync(new TestGroupFilterSpecification( groupName));
      return list.FirstOrDefault();
    }

    public async Task<TestGroup> FindTestGroupAsyncById(int groupId)
    {
      var list = await _testGroupRepository.ListAsync(new TestGroupFilterSpecification(groupId));
      return list.FirstOrDefault();
    }

    public bool AddSessionIntoTestGroup(int sessionId, string groupName)
    {
      var testGroup = FindTestGroupAsyncByName(groupName).Result;
      if (testGroup == null) return false;

        testGroup.EnrolledSessionIds.Add(sessionId);
        testGroup.EnrolledSessionIds = testGroup.EnrolledSessionIds;
      _testGroupRepository.UpdateAsync(testGroup).Wait();

      return true;
    }

    public int CreateNewSession(string name)
    {
      var existingSession = _testSessionRepository.ListAsync(new TestSessionFilterSpecification(name)).Result.FirstOrDefault();
      if (existingSession != null)
        return existingSession.Id;

      var testSession = new TestSession()
      {
        LastUpdated = DateTime.UtcNow,
        Name = name,
        Description = name,
        PlannedStart = DateTime.UtcNow,
        PlannedEnd = DateTime.UtcNow.Add(TimeSpan.FromMinutes(30.0))
      };
      _testSessionRepository.AddAsync(testSession).Wait();
      return testSession.Id;
    }

    public void RegisterUser(int sessionId, UserProfile userProfile)
    {
      var testSession = _testSessionRepository.GetByIdAsync(sessionId).Result;
      if (testSession == null)
        throw new ArgumentException("sessionId");

      var registeredIds = new HashSet<int>(testSession.Testers.Items.Select(t => t.TesterId));
      if (registeredIds.Contains(userProfile.Id))
      {
        return;
      }

      testSession.Testers.Add(new TesterItem { TesterId = userProfile.Id, Grade = userProfile.ExperienceLevel, Group = userProfile.Continent.ToString() });
      testSession.LastUpdated = DateTime.UtcNow;
      testSession.Testers = testSession.Testers; //Just give the ProtoBuff mechanism a kick
      _testSessionRepository.UpdateAsync(testSession);
    }

    public void AddQuestion(int sessionId, int questionId, double scorePoint, double penaltyPoint = 0.0)
    {
      var testSession =  _testSessionRepository.GetByIdAsync(sessionId).Result;
      if (testSession == null)
      {
        throw new ArgumentException("sessionId");
      }

      var addedQuestionIds = new HashSet<int>(testSession.TestQuestions.Select(q => q.QuestionId));
      if (addedQuestionIds.Contains(questionId))
        return;

      var question = _testQuestionRepository.GetByIdAsync(questionId).Result;
      if (question == null) return; //Avoid adding deleted questions
      testSession.TestQuestions.Add(new TestQuestionItem { Idx = testSession.TestQuestions.Count, QuestionId = questionId, PenaltyPoint = penaltyPoint, ScorePoint = scorePoint });
      testSession.TestQuestions = testSession.TestQuestions;
      testSession.LastUpdated = DateTime.UtcNow;
      _testSessionRepository.UpdateAsync(testSession).Wait();
    }

    public async Task<IReadOnlyList<TestResult>> GetTestResultsAsync(int sessionId)
    {
      return await _testResultRepository.ListAsync(new TestSessionResultFilterSpecification( sessionId));
    }

    public async Task CreateNewTestResult(int sessionId, int userId)
    {
      var testResult = await GetTestResultAsync(sessionId, userId);

      if (testResult != null) return; //Result already exists

      var testSession = await _testSessionRepository.GetByIdAsync(sessionId);

      testResult = new TestResult
      {
        TestStarted = DateTime.UtcNow,
        TestSessionId = sessionId,
        UserId = userId,
        FinalScore = 0.0,
        MaximumScore = testSession?.TestQuestions.Items.Sum(t => t.ScorePoint) ?? 0.0
      };

      await _testResultRepository.AddAsync(testResult);
    }

    public async Task<IReadOnlyList<TestSession>> FindAllAsync()
    {
      return await _testSessionRepository.ListAllAsync();
    }

    public async Task<TestResult> JudgeAnswerAsync(TestSession test, TestQuestion question, int testerId, string answer)
    {
      if (test == null || question == null) 
        throw new ArgumentException("Invalid test or question");

      if (answer == null)
        throw new ArgumentException("Invalid answer");

      //if (question.ObjectId != answer.QuestionId)
      //  throw new ArgumentException("The answer does not match the question");

      if (question.TestAnswer == null)
        throw new NotImplementedException();

      var testItem = test.TestQuestions.Items.FirstOrDefault(q => q.QuestionId == question.ObjectId);
      if (testItem == null)
        throw new ArgumentException("question");

      await CreateNewTestResult(test.ObjectId, testerId);
      var testResult = await GetTestResultAsync(test.ObjectId, testerId);
      var cleanedTestAnswer = answer.TrimEnd();
      bool runOvertime = (testResult.TestStarted >= test.PlannedStart && (testResult.TestEnded - testResult.TestStarted) >= test.SessionTimeSpan);
      var testResultItem = testResult.TestResults.Items.FirstOrDefault(ttr => ttr.QuestionId == question.ObjectId);
      if (testResultItem == null)
      {
        testResultItem = new TestResultItem() { Answer = cleanedTestAnswer, QuestionId = question.ObjectId, };
        testResult.TestResults.Add(testResultItem);
      }
      else if (!runOvertime)
      {
        testResultItem.Answer = cleanedTestAnswer;
      }
      else
      {
        return testResult;
      }

      switch (question.TestAnswer.AnswerType)
      {
        case TestAnswerType.SingleChoice:
        case TestAnswerType.Text:
        case TestAnswerType.Integer:
          testResultItem.CorrectAnswer = question.TestAnswer.TextAnswer;
          testResultItem.Score = string.IsNullOrEmpty(cleanedTestAnswer) ? 0.0 : String.Compare(question.TestAnswer.TextAnswer, cleanedTestAnswer,
                   StringComparison.InvariantCultureIgnoreCase) == 0
            ? testItem.ScorePoint
            : testItem.PenaltyPoint;
          break;
        case TestAnswerType.Number:
          testResultItem.CorrectAnswer = question.TestAnswer.NumericAnswer.ToString(CultureInfo.InvariantCulture);
          if (string.IsNullOrEmpty(cleanedTestAnswer))
            testResultItem.Score = 0.0;
          else if (!double.TryParse(cleanedTestAnswer, out var numAnswer))
          {
            testResultItem.Score = testItem.PenaltyPoint;
          }
          else
          {
            testResultItem.Score =
              Math.Abs(question.TestAnswer.NumericAnswer - numAnswer) <= question.TestAnswer.NumericAccuracy
                ? testItem.ScorePoint
                : testItem.PenaltyPoint;
          }
          break;
        default:
          throw new ArgumentException($"Test answer type [{question.TestAnswer.AnswerType}] not supported");
      }

      testResult.FinalScore = testResult.TestResults.Items.Sum(t => t.Score);
      testResult.MaximumScore = test.TestQuestions.Items.Sum(q => q.ScorePoint);
      testResult.TestResults = testResult.TestResults;
      if (testResult.TestStarted < test.PlannedStart)
      {
        testResult.TestStarted = DateTime.UtcNow;
      }

      testResult.TestEnded = runOvertime ? testResult.TestStarted.Add(test.SessionTimeSpan) : testResult.TestEnded;
      return testResult;
    }
  }

}
