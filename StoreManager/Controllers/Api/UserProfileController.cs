using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StoreManager.Data;
using StoreManager.Models;
using StoreManager.Models.SyncfusionViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.EntityFrameworkCore;
using StoreManager.Interfaces;
using WebMathTraining.Models;

namespace StoreManager.Controllers.Api
{
  [Authorize]
  [Produces("application/json")]
  [Route("api/UserProfile")]
  public class UserProfileController : Controller
  {
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICatalogRepository<TestGroup> _testGroupManager;

    public UserProfileController(ApplicationDbContext context,
      UserManager<ApplicationUser> userManager,
      ICatalogRepository<TestGroup> testGroups)
    {
      _context = context;
      _userManager = userManager;
      _testGroupManager = testGroups;
    }

    [HttpGet("[action]/{email}")]
    public IActionResult GetByUserEmail([FromRoute] string email)
    {
      UserProfile userProfile = _context.UserProfile.SingleOrDefault(x => x.Email.Equals(email));
      List<UserProfile> Items = new List<UserProfile>();
      if (userProfile != null)
      {
        Items.Add(userProfile);
      }

      int Count = Items.Count();
      return Ok(new { Items, Count });
    }

    [HttpGet("[action]/{id}")]
    public IActionResult GetByApplicationUserId([FromRoute] string id)
    {
      UserProfile userProfile = _context.UserProfile.SingleOrDefault(x => x.ApplicationUserId.Equals(id));
      List<UserProfile> Items = new List<UserProfile>();
      if (userProfile != null)
      {
        Items.Add(userProfile);
      }

      int Count = Items.Count();
      return Ok(new { Items, Count });
    }

    [HttpPost("[action]")]
    public async Task<IActionResult> Insert([FromBody] CrudViewModel<UserProfile> payload)
    {
      UserProfile register = payload?.value;
      if (register != null && Int32.TryParse(register.ApplicationUserId, out int testGroupId))
      {
        var testGroup = await _testGroupManager.GetByIdAsync(testGroupId);
        var tester = await _context.UserProfile.FirstOrDefaultAsync(u => u.Id == register.Id);
        testGroup.MemberObjectIds.Add(tester.Id);
        testGroup.MemberObjectIds = testGroup.MemberObjectIds;
        testGroup.LastUpdated = DateTime.UtcNow;
        await _testGroupManager.UpdateAsync(testGroup);
      }

      return Ok(register);
    }

    [HttpPost("[action]")]
    public async Task<IActionResult> Update([FromBody] CrudViewModel<UserProfile> payload)
    {
      var register = payload?.value;
      if (register != null && Int32.TryParse(register.ApplicationUserId, out int testGroupId))
      {
        var testGroup = await _testGroupManager.GetByIdAsync(testGroupId);
        if (testGroup != null)
        {
          testGroup.MemberObjectIds.Clear();
          testGroup.MemberObjectIds = testGroup.MemberObjectIds;
          testGroup.LastUpdated = DateTime.UtcNow;
          await _testGroupManager.UpdateAsync(testGroup);
        }
      }

      return Ok(register);
    }

    [HttpPost("[action]")]
    public async Task<IActionResult> ChangePassword([FromBody] CrudViewModel<UserProfile> payload)
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

    [HttpPost("[action]")]
    public async Task<IActionResult> Remove([FromBody] CrudViewModel<UserProfile> payload)
    {
      var register = payload?.value;
      if (register != null && Int32.TryParse(register.ApplicationUserId, out int testGroupId))
      {
        var testGroup = await _testGroupManager.GetByIdAsync(testGroupId);
        var tester = await _context.UserProfile.FirstOrDefaultAsync(u => u.Id == register.Id);
        testGroup.MemberObjectIds.Remove(tester.Id);
        testGroup.MemberObjectIds = testGroup.MemberObjectIds;
        testGroup.LastUpdated = DateTime.UtcNow;
        await _testGroupManager.UpdateAsync(testGroup);
      }

      return Ok();
    }
  }
}