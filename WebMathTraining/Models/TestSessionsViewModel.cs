using System;
using System.Collections.Generic;
using System.Linq;
using StoreManager.Models;
using Microsoft.Rest.TransientFaultHandling;

namespace WebMathTraining.Models
{
  public class TestSessionsViewModel 
  {
    public TestSessionsViewModel()
    { }

    public TestSessionsViewModel(TestSession entity)
    {
      Id = entity.Id;
      ObjectId = entity.ObjectId;
      Name = entity.Name;
      Description = entity.Description;
      PlannedStart = entity.PlannedStart.ToLocalTime();
      PlannedEnd = entity.PlannedEnd.ToLocalTime();
      RegisteredUsers = String.Join('+', entity.Testers.Items.Select(t => t.TesterId).OrderBy(id => id));

      TestQuestions = String.Join('+', entity.TestQuestions.OrderBy(id => id.QuestionId).Select(q => q.QuestionId));
      LastUpdated = entity.LastUpdated.ToLocalTime();
      ScorePoints = String.Join('+', entity.TestQuestions.OrderBy(q => q.QuestionId).Select(q => q.ScorePoint));
      TargetGrade = entity.TargetGrade;
    }

    public int Id { get; set; }
    public int ObjectId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public DateTime PlannedStart { get; set; }

    public DateTime PlannedEnd { get; set; }

    public DateTime LastUpdated { get; set; }

    public string RegisteredUsers { get; set; }

    public string TestQuestions { get; set; }

    public string ScorePoints { get; set; }

    public HashSet<int> DistinctTesters
    {
      get
      {
        var retVal = new HashSet<int>();
        if (string.IsNullOrEmpty(RegisteredUsers)) return retVal;
        foreach (var str in RegisteredUsers.Split('+'))
        {
          if (Int32.TryParse(str, out var tester))
            retVal.Add(tester);
        }

        return retVal;
      }
    }

    public IList<int> DistinctQuestionIds
    {
      get
      {
        var retVal = new List<int>();
        if (string.IsNullOrEmpty(TestQuestions)) return retVal;
        foreach (var str in TestQuestions.Split('+'))
        {
          if (Int32.TryParse(str, out var questionId))
            retVal.Add(questionId);
        }

        return retVal;
      }
    }

    public IList<double> TestScores
    {
      get
      {
        var retVal = new List<double>();
        if (string.IsNullOrEmpty(ScorePoints)) return retVal;
        foreach (var str in ScorePoints.Split('+'))
        {
          if (Double.TryParse(str, out var score))
            retVal.Add(score);
        }

        return retVal;
      }
    }

    public int TargetGrade { get; set; }
  }
}
