using System;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using WebMathTraining.Models;

namespace WebMathTraining.Services
{
    public interface IAppUserManageService
    {
        Task<IdentityResult> DeleteAppUserAsync(string id);

        Task<bool> MarkStatusAsync(string id, UserStatus userStatus);
    }
}
