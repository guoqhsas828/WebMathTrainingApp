using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebMathTraining.Models;

namespace WebMathTraining.Controllers
{
  public class HomeController : Controller
  {
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(UserManager<ApplicationUser> userManager)
    {
      _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
      if (User != null)
      {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
          user.LatestLogin = DateTime.UtcNow;
          var updateResult = await _userManager.UpdateAsync(user);
          if (!updateResult.Succeeded)
            throw new ApplicationException($"Unexpected error occurred updating user information");
        }
      }

      return View();
    }

    public IActionResult About()
    {
      ViewData["Message"] = "";

      return View();
    }

    public IActionResult Contact()
    {
      ViewData["Message"] = "";

      return View();
    }

    public IActionResult Error()
    {
      return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
    }
  }
}
