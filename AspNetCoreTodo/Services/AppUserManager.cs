using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using WebMathTraining.Data;
using WebMathTraining.Models;

namespace WebMathTraining.Services
{
    public class AppUserManageService : IAppUserManageService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private IUserValidator<ApplicationUser> _userValidator;
        private IPasswordValidator<ApplicationUser> _passwordValidator;
        private IPasswordHasher<ApplicationUser> _passwordHasher;

        public AppUserManageService(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
          IUserValidator<ApplicationUser> userValid, IPasswordValidator<ApplicationUser> passValid,
          IPasswordHasher<ApplicationUser> passHasher)
        {
            _context = context;
            _userManager = userManager;
            _userValidator = userValid;
            _passwordValidator = passValid;
            _passwordHasher = passHasher;
        }

        public async Task<IdentityResult> DeleteAppUserAsync(string id)
        {
            ApplicationUser user = await _userManager.FindByIdAsync(id);

            if (user != null)
            {
                IdentityResult result = await _userManager.DeleteAsync(user);
                return result;
            }

            return null;
        }

        public async Task<bool> MarkStatusAsync(string id, UserStatus userStatus)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null) return false;

            user.UserStatus = userStatus;
            user.LastUpdated = DateTime.UtcNow;
            var saveResult = await _context.SaveChangesAsync();
            return saveResult == 1; // One entity should have been updated
        }
    }
}
