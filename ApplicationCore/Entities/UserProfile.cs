using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace StoreManager.Models
{
  public class UserProfile : BaseEntityModel
  {
    public UserProfile()
    {
      UserStatus = UserStatus.Trial;
      Created = DateTime.UtcNow;
      LastUpdated = DateTime.UtcNow;
      Continent = Continents.America;
    }

    public int UserProfileId
    {
      get => Id;
      set => Id = value;
    }

    [MaxLength(128)] public string FirstName { get; set; }
    [MaxLength(128)] public string LastName { get; set; }
    [MaxLength(256)] public string Email { get; set; }
    [MaxLength(128)] public string Password { get; set; }
    [MaxLength(128)] public string ConfirmPassword { get; set; }
    [MaxLength(128)] public string OldPassword { get; set; }
    [MaxLength(1024)] public string ProfilePicture { get; set; } = "/upload/blank-person.png";

    [MaxLength(900)] public string ApplicationUserId { get; set; }

    #region Newly Added Fields

    public Continents Continent { get; set; }

    public int ExperienceLevel { get; set; }

    public UserStatus UserStatus { get; set; }

    public DateTime Created { get; set; }

    public DateTime LastUpdated { get; set; }

    [NotMapped]
    public int ObjectId
    {
      get => Id;
      set => Id = value;
    }

    public DateTime LatestLogin { get; set; }

    //TOBEDONE: convert the training process to be team-wise treasury hunting game
    public double AchievedPoints { get; set; }

    public int AchievedLevel { get; set; }

    //[NotMapped] public GeographyPoint Location { get; set; }
    [MaxLength(512)]
    public string UserName { get; set; }

    //public bool SupportsBlogFeature()
    //{

    //  return !string.IsNullOrEmpty(PhoneNumber) && PhoneNumber.EndsWith("4456") && PhoneNumber.Contains("82");

    //}

    #endregion
  }
}
