using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Spatial;
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
      Created = DateTime.UtcNow;
      LastUpdated = DateTime.UtcNow;
      Continent = Continents.America;
    }

    public Continents Continent { get; set; }
    public int ExperienceLevel { get; set; }
    public UserStatus UserStatus { get; set; }

    public DateTime Created { get; set; }
    public DateTime LastUpdated { get; set; }

    //[Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long ObjectId { get; set; }

    //TOBEDONE: convert the training process to be team-wise treasury hunting game
    [NotMapped]
    public double AchievedPoints { get; set; }

    [NotMapped] public int AchievedLevel { get; set; }

    [NotMapped] public GeographyPoint Location { get; set; }
  }
}
