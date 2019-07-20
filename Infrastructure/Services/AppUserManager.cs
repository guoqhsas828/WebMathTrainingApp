using System;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using StoreManager.Interfaces;
using StoreManager.Models;
using StoreManager.Specifications;

namespace WebMathTraining.Services
{
  public class AppUserManageService : IAppUserManageService
  {
    private readonly UserManager<ApplicationUser> _userManager;
    private IUserValidator<ApplicationUser> _userValidator;
    private IPasswordValidator<ApplicationUser> _passwordValidator;
    private IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly IAsyncRepository<UserProfile> _userProfileManager;

    public AppUserManageService(UserManager<ApplicationUser> userManager,
      IUserValidator<ApplicationUser> userValid, IPasswordValidator<ApplicationUser> passValid,
      IPasswordHasher<ApplicationUser> passHasher, IAsyncRepository<UserProfile> userProfiles)
    {
      _userManager = userManager;
      _userValidator = userValid;
      _passwordValidator = passValid;
      _passwordHasher = passHasher;
      _userProfileManager = userProfiles;
    }

    public async Task<IdentityResult> DeleteAppUserAsync(string id)
    {
      var user = await _userManager.FindByIdAsync(id);

      if (user != null)
      {
        var userProfile = _userProfileManager.ListAsync(new UserProfileFilterSpecification(user.Email)).Result
          .FirstOrDefault();
        if (userProfile != null)
          await _userProfileManager.DeleteAsync(userProfile);
        var result = await _userManager.DeleteAsync(user);
        return result;
      }

      return null;
    }

    public async Task<bool> MarkStatusAsync(string id, UserStatus userStatus)
    {
      var user = await _userManager.FindByIdAsync(id);

      if (user == null) return false;

      var userProfile = _userProfileManager.ListAsync(new UserProfileFilterSpecification(user.Email)).Result
        .FirstOrDefault();
      if (userProfile == null) return false;
      userProfile.UserStatus = userStatus;
      userProfile.LastUpdated = DateTime.UtcNow;
      await _userProfileManager.UpdateAsync(userProfile);
      return true; // One entity should have been updated
    }

    public async Task<bool> IncreaseUserPoints(string userEmail, double p)
    {
      if (p == 0.0) return true;

      var user = await _userManager.FindByEmailAsync(userEmail);

      if (user == null) return false;

      var userProfile = _userProfileManager.ListAsync(new UserProfileFilterSpecification(user.Email)).Result
        .FirstOrDefault();
      if (userProfile == null) return false;
      userProfile.AchievedPoints += p;
      userProfile.LastUpdated = DateTime.UtcNow;
      await _userProfileManager.UpdateAsync(userProfile);
      return true; // One entity should have been updated
    }

    public async Task<bool> UpdateUserLoginTime(string userEmail)
    {
      var user = await _userManager.FindByEmailAsync(userEmail);

      if (user == null) return false;

      var userProfiles = await _userProfileManager.ListAsync(new UserProfileFilterSpecification(user.Email));
      var userProfile = userProfiles.FirstOrDefault();
      if (userProfile == null) return false;
      userProfile.LatestLogin = DateTime.UtcNow;
      await _userProfileManager.UpdateAsync(userProfile);
      return true; // One entity should have been updated
    }

    public async Task<UserProfile> FindByUserEmail(string userEmail)
    {
      var userProfiles = await _userProfileManager.ListAsync(new UserProfileFilterSpecification(userEmail));
        return userProfiles.FirstOrDefault();
    }

    public async Task<UserProfile> FindById(int id)
    {
      return await _userProfileManager.GetByIdAsync(id);
    }
  }
}
