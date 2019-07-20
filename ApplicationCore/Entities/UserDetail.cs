using StoreManager.Models;
using System;

namespace Microsoft.eShopWeb.ApplicationCore.Entities
{
  public class UserDetail : BaseEntityModel
  {
    public UserDetail(UserProfile user)
    {
      UserName = user.UserName;
      Continent = user.Continent;
      ExperienceLevel = user.ExperienceLevel;
      UserStatus = user.UserStatus;
      AchievedPoints = user.AchievedPoints;
      Email = user.Email;
    }

    public string UserName { get; set; }
    public string Description { get; set; }
    public Continents Continent { get; set; }
    public int ExperienceLevel { get; set; }
    public UserStatus UserStatus { get; set; }
    public string Email { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastUpdated { get; set; }

    public DateTime LatestLogin { get; set; }

    //TOBEDONE: convert the training process to be team-wise treasury hunting game
    public double AchievedPoints { get; set; }

    public int AchievedLevel { get; set; }

    public int ApplicationUserId { get; set; }
  }
}