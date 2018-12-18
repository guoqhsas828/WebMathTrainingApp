using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebMathTraining.Models;
using WebMathTraining.Services;

namespace WebMathTraining.Controllers
{
    [Authorize(Roles = Constants.AdministratorRole)]
    public class ManageUsersController : Controller
    {
        private readonly IAppUserManageService _appUserManagerService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ManageUsersController(UserManager<ApplicationUser> userManager, IAppUserManageService appUserService)
        {
            _userManager = userManager;
            _appUserManagerService = appUserService;
        }

        public async Task<IActionResult> Index()
        {
            var admins = (await _userManager
                .GetUsersInRoleAsync(Constants.AdministratorRole))
              .ToArray();

            var everyone = await _userManager.Users
              .ToArrayAsync();

            var model = new ManageUsersViewModel
            {
                Administrators = admins,
                Everyone = everyone
            };

            return View(model);
        }

        public async Task<IActionResult> Delete(string id)
        {
            IdentityResult result = await _appUserManagerService.DeleteAppUserAsync(id);

            if (result != null)
            {
                if (!result.Succeeded)
                {
                    AddErrors(result);
                }
                else
                {
                    return RedirectToAction("Index");
                }
            }
            else
            {
                ModelState.AddModelError("", "User Not Found");
            }

            return View("Index", _userManager.Users);
        }

        public async Task<IActionResult> DeActivate(string id)
        {
            var retVal = await _appUserManagerService.MarkStatusAsync(id, UserStatus.InActive);

            if (!retVal)
            {
                ModelState.AddModelError("", string.Format("Updating User {0} failed", id));
            }
            else
            {
                return RedirectToAction("Index");
            }

            return View("Index", _userManager.Users);
        }

        public async Task<IActionResult> Activate(string id)
        {
            var retVal = await _appUserManagerService.MarkStatusAsync(id, UserStatus.Active);

            if (!retVal)
            {
                ModelState.AddModelError("", string.Format("Updating User {0} failed", id));
            }
            else
            {
                return RedirectToAction("Index");
            }

            return View("Index", _userManager.Users);
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.TryAddModelError("", error.Description);
            }
        }
    }
}
