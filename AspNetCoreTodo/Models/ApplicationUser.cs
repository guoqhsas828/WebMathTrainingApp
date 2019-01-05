using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace WebMathTraining.Models
{
  public enum Continents
  {
    None = 0,
    Africa,
    Asia,
    Australia,
    Europe,
    America
  }

  public enum UserStatus
  {
    InActive = -1,
    Trial = 0,
    Active = 1
  }

  // Add profile data for application users by adding properties to the ApplicationUser class
  public class ApplicationUser : IdentityUser
  {
    public ApplicationUser() : base()
    {
      UserStatus = UserStatus.Trial;
      Created = DateTime.Now;
      LastUpdated = DateTime.Now;
    }

    public Continents Continent { get; set; }
    public int ExperienceLevel { get; set; }
    public UserStatus UserStatus { get; set; }

    public DateTime Created { get; set; }
    public DateTime LastUpdated { get; set; }

    //[Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long ObjectId { get; set; }
  }
}
