using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMathTraining.Data;
using WebMathTraining.Models;

namespace WebMathTraining.Services
{
  public interface ITestSessionService
  {
    Guid CreateNewSession(string name);
    void RegisterUser(Guid sessionId, ApplicationUser user);
    void AddQuestion(Guid sessionId, long questionId, double scorePoint, double penaltyPoint=0.0);
    double JudgeAnswer(TestSession test, TestQuestion question, ref TestResultItem answer); //Judge the answer and return the score point
    IList<TestResult> GetTestResults(long sessionId);
    void CreateNewTestResult(long sessionId, long userId);
    Task<IList<TestGroup>> FindAllTestGroupAsync();
    Task<TestGroup> FindTestGroupAsyncById(Guid groupId);
  }

  public class TestSessionService : ITestSessionService
  {
    private readonly TestDbContext _context;
    private readonly AppUserManageService _userManager;

    public TestSessionService(TestDbContext context, AppUserManageService userManager)
    {
      _context = context;
      _userManager = userManager;
    }

    public async Task<IList<TestGroup>> FindAllTestGroupAsync()
    {
      return await _context.TestGroups.ToArrayAsync();
    }

    public async Task<TestGroup> FindTestGroupAsyncById(Guid groupId)
    {
      return await _context.TestGroups.FindAsync(groupId);
    }


    public Guid CreateNewSession(string name)
    {
      var existingSession = _context.TestSessions.FirstOrDefault(s => String.Compare(s.Name, name, StringComparison.InvariantCultureIgnoreCase) ==0);
      if (existingSession != null)
        return existingSession.Id;

      var testSession = new TestSession()
      {
        Id = Guid.NewGuid(),
        LastUpdated = DateTime.UtcNow,
        Name = name,
        Description = name,
        PlannedStart = DateTime.UtcNow,
        PlannedEnd = DateTime.UtcNow.Add(TimeSpan.FromMinutes(5.0))
      };
      _context.Add(testSession);
      _context.SaveChanges();
      return testSession.Id;
    }

    public void RegisterUser(Guid sessionId, ApplicationUser user)
    {
      var testSession = _context.TestSessions.Find(sessionId);
      if (testSession == null)
        throw new ArgumentException("sessionId");

      var registeredIds = testSession.Testers.Items.Select(t => t.TesterId).ToHashSet<long>();
      if (registeredIds.Contains(user.ObjectId))
      {
        return;
      }

      testSession.Testers.Add(new TesterItem { TesterId = user.ObjectId, Grade = user.ExperienceLevel, Group = user.Continent.ToString() });
      testSession.LastUpdated = DateTime.UtcNow;
      testSession.Testers = testSession.Testers; //Just give the ProtoBuff mechanism a kick
      _context.Update(testSession);
      _context.SaveChanges();
    }

    public void AddQuestion(Guid sessionId, long questionId, double scorePoint, double penaltyPoint = 0.0)
    {
      var testSession =  _context.TestSessions.Find(sessionId);
      if (testSession == null)
      {
        throw new ArgumentException("sessionId");
      }

      var addedQuestionIds = testSession.TestQuestions.Select(q => q.QuestionId).ToHashSet<long>();
      if (addedQuestionIds.Contains(questionId))
        return;

      testSession.TestQuestions.Add(new TestQuestionItem { Idx = testSession.TestQuestions.Count, QuestionId = questionId, PenaltyPoint = penaltyPoint, ScorePoint = scorePoint });
      testSession.TestQuestions = testSession.TestQuestions;
      testSession.LastUpdated = DateTime.UtcNow;
      _context.Update(testSession);
      _context.SaveChanges();
    }

    public IList<TestResult> GetTestResults(long sessionId)
    {
      return _context.TestResults.Where(tr => tr.TestSessionId == sessionId).ToList();
    }

    public void CreateNewTestResult(long sessionId, long userId)
    {
      var testResult = GetTestResults(sessionId).FirstOrDefault(tr => tr.UserId == userId);

      if (testResult != null) return; //Result already exists

      var testSession = _context.TestSessions.FirstOrDefault(s => s.ObjectId == sessionId);

      testResult = new TestResult
      {
        TestStarted = DateTime.UtcNow,
        TestSessionId = sessionId,
        UserId = userId,
        FinalScore = 0.0,
        MaximumScore = testSession?.TestQuestions.Items.Sum(t => t.ScorePoint) ?? 0.0
      };
      _context.TestResults.Add(testResult);
      _context.SaveChanges();
    }

    public double JudgeAnswer(TestSession test, TestQuestion question, ref TestResultItem answer)
    {
      if (test == null || question == null) 
        throw new ArgumentException("Invalid test or question");

      if (answer == null)
        throw new ArgumentException("Invalid answer");

      if (question.ObjectId != answer.QuestionId)
        throw new ArgumentException("The answer does not match the question");

      if (question.TestAnswer == null)
        throw new NotImplementedException();

      var testItem = test.TestQuestions.Items.FirstOrDefault(q => q.QuestionId == question.ObjectId);
      if (testItem == null)
        throw new ArgumentException("question");

      switch (question.TestAnswer.AnswerType)
      {
        case TestAnswerType.SingleChoice:
        case TestAnswerType.Text:
        case TestAnswerType.Integer:
          answer.CorrectAnswer = question.TestAnswer.TextAnswer;
          answer.Score = string.IsNullOrEmpty(answer.Answer) ? 0.0 : String.Compare(question.TestAnswer.TextAnswer, answer.Answer,
                   StringComparison.InvariantCultureIgnoreCase) == 0
            ? testItem.ScorePoint
            : testItem.PenaltyPoint;
          break;
        case TestAnswerType.Number:
          answer.CorrectAnswer = question.TestAnswer.NumericAnswer.ToString(CultureInfo.InvariantCulture);
          if (string.IsNullOrEmpty(answer.Answer))
            answer.Score = 0.0;
          else if (!double.TryParse(answer.Answer, out var numAnswer))
          {
            answer.Score = testItem.PenaltyPoint;
          }
          else
          {
            answer.Score =
              Math.Abs(question.TestAnswer.NumericAnswer - numAnswer) <= question.TestAnswer.NumericAccuracy
                ? testItem.ScorePoint
                : testItem.PenaltyPoint;
          }
          break;
        default:
          throw new ArgumentException($"Test answer type [{question.TestAnswer.AnswerType}] not supported");
      }

      return answer.Score;
    }
  }

}
