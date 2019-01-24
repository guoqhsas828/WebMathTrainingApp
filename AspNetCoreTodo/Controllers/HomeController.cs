using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Http;
using WebMathTraining.Models;

namespace WebMathTraining.Controllers
{
  public class HomeController : Controller
  {
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStringLocalizer<HomeController> _localizer;

    public HomeController(IStringLocalizer<HomeController> localizer, UserManager<ApplicationUser> userManager)
    {
      _userManager = userManager;
      _localizer = localizer;
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
      ViewData["Message"] = _localizer["About"];

      return View();
    }

    public IActionResult Contact()
    {
      ViewData["Message"] = _localizer["Contact"];

      return View();
    }

    [HttpPost]
    public IActionResult SetLanguage(string culture, string returnUrl)
    {
      Response.Cookies.Append(
          CookieRequestCultureProvider.DefaultCookieName,
          CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
          new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
      );

      return LocalRedirect(returnUrl);
    }

    public IActionResult Error()
    {
      return View(new ErrorViewModel {RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier});
    }
  }
}
