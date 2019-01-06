using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
      PlannedStart = entity.PlannedStart;
      PlannedEnd = entity.PlannedEnd;
      RegisteredUsers = "";
      foreach (var tester in entity.Testers.Items)
      {
        RegisteredUsers += tester.TesterId + "/";
      }
    }

    public Guid Id { get; set; }

    public long ObjectId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public DateTime PlannedStart { get; set; }

    public DateTime PlannedEnd { get; set; }

    public DateTime LastUpdated { get; set; }

    public string RegisteredUsers { get; set; }
  }
}
