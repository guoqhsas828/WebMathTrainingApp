using System;
using System.ComponentModel.DataAnnotations;

namespace StoreManager.Models.ManageViewModels
{
  public class IndexViewModel
  {
    public IndexViewModel()
    {
    }

    public IndexViewModel(ApplicationUser user, UserProfile userProfile)
    {
      Username = user.UserName;
      Email = user.Email;
      PhoneNumber = user.PhoneNumber;
      IsEmailConfirmed = user.EmailConfirmed;
      ExperienceLevel = userProfile.ExperienceLevel;
      UserStatus = userProfile.UserStatus;
      AchievedPoints = userProfile.AchievedPoints;
      AchievedLevel = userProfile.AchievedLevel;
    }

    public string Username { get; set; }

    public bool IsEmailConfirmed { get; set; }

    [Required] [EmailAddress] public string Email { get; set; }

    [Phone]
    [Display(Name = "Phone number")]
    public string PhoneNumber { get; set; }

    public string StatusMessage { get; set; }

    public int ExperienceLevel { get; set; }

    public UserStatus UserStatus { get; set; }

    public double AchievedPoints { get; set; }

    public int AchievedLevel { get; set; }
  }
}
