using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WebMathTraining.Models.ManageViewModels
{
  public class IndexViewModel
  {
    public IndexViewModel()
    { }

    public IndexViewModel(ApplicationUser user)
    {
      Username = user.UserName;
      Email = user.Email;
      PhoneNumber = user.PhoneNumber;
      IsEmailConfirmed = user.EmailConfirmed;
      ExperienceLevel = user.ExperienceLevel;
      UserStatus = user.UserStatus;
      AchievedPoints = user.AchievedPoints;
      AchievedLevel = user.AchievedLevel;
    }

    public string Username { get; set; }

    public bool IsEmailConfirmed { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Phone]
    [Display(Name = "Phone number")]
    public string PhoneNumber { get; set; }

    public int ExperienceLevel { get; set; }

    public UserStatus UserStatus { get; set; }

    public string StatusMessage { get; set; }

    public double AchievedPoints { get; set; }

    public int AchievedLevel { get; set; }
  }
}
