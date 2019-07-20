using System;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using StoreManager.Models;

namespace WebMathTraining.Services
{
  public interface IAppUserManageService
  {
    Task<IdentityResult> DeleteAppUserAsync(string id);

    Task<bool> MarkStatusAsync(string id, UserStatus userStatus);

    Task<bool> UpdateUserLoginTime(string userEmail);

    Task<UserProfile> FindByUserEmail(string userEmail);
    Task<UserProfile> FindById(int id);

    Task<bool> IncreaseUserPoints(string userEmail, double p);
  }
}
