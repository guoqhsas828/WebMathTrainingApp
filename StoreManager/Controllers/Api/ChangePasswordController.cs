using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StoreManager.Data;
using StoreManager.Models;
using StoreManager.Models.ManageViewModels;
using StoreManager.Models.SyncfusionViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace StoreManager.Controllers.Api
{
  [Authorize]
  [Produces("application/json")]
  [Route("api/ChangePassword")]
  public class ChangePasswordController : Controller
  {
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public ChangePasswordController(ApplicationDbContext context,
      UserManager<ApplicationUser> userManager,
      RoleManager<IdentityRole> roleManager)
    {
      _context = context;
      _userManager = userManager;
      _roleManager = roleManager;
    }

    // GET: api/ChangePassword
    [HttpGet]
    public IActionResult GetChangePassword()
    {
      List<ApplicationUser> Items = new List<ApplicationUser>();
      Items = _context.Users.ToList();
      int Count = Items.Count();
      return Ok(new {Items, Count});
    }

    [HttpPost("[action]")]
    public async Task<IActionResult> Update([FromBody] CrudViewModel<UserProfile> payload)
    {
      UserProfile profile = payload.value;
      if (profile.Password.Equals(profile.ConfirmPassword))
      {
        var user = await _userManager.FindByIdAsync(profile.ApplicationUserId);
        var result = await _userManager.ChangePasswordAsync(user, profile.OldPassword, profile.Password);
      }

      profile = _context.UserProfile.SingleOrDefault(x => x.ApplicationUserId.Equals(profile.ApplicationUserId));
      return Ok(profile);
    }
  }
}