using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Http;
using WebMathTraining.Models;
using WebMathTraining.Resources;

namespace WebMathTraining.Controllers
{
  public class HomeController : Controller
  {
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly LocService _localizer;

    public HomeController(LocService localizer, UserManager<ApplicationUser> userManager)
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
      var messages = new[]
      {
        "This  online  math  training  site  provides  all  levels  of  contest  questions  to  enhance  the mathematics  skills  for  interested  students",
        "We  also  seek to  build  a  social  platform  for kids  to  train  and  work  within  a  team  ,  motivating  the members  to  dig out  their  full  potentials",
        "The  site  is still  in  the trial run  status  ,  there are  over 600  questions  in  the  competition  library  covering  various  grades  ,  more  questions  will  be added  and  functions  enhanced",
        "The  primary  focus  of  this  stage  is  to  provide  test  questions  for  the  students  in  the  elementary school  (1-5  grade  ),  we  will  group students into the  1-2  grade  team  and  3-5  grade  team",
        "The usage of this site  will  be limited  to  a  small  group  of  connected  kids  so that  they  can  train  ,  compete  and  interact with each other",
        "We  are  planning  to  introduce  more  multi-media  features  ,  may  transform  the  test  process  to  a  team  based  treasury-hunting  game",
        "Privacy  will  be protected at the best effort  ,  please  report  any  functionality  you  suspect  will  compromise  the  privacy  information  you  provide  to  the  site",
        "Please  leave  notes  in  the  bug report  section",
        "Newly registered  user  will  only  have access to  the  trial  test  questions  ,  till  getting upgraded into  active  status",
        "Please  go to  your  account  to  verify  the  email  and  update  your  school  grade  information",

      };
      var sbr = new StringBuilder();
      ViewData["Title"] = _localizer.GetLocalizedHtmlString("About");
      foreach (var str in messages)
      {
        foreach (var subStr in str.Split("  "))
        {
          sbr.Append(_localizer.GetLocalizedHtmlString(subStr));
        }

        sbr.AppendLine(".");
      }

      ViewData["Message"] = sbr.ToString();
      return View();
    }

    public IActionResult Contact()
    {
      ViewData["Message"] = _localizer.GetLocalizedHtmlString("Contact");

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
