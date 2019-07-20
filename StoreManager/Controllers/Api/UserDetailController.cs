using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StoreManager.Interfaces;
using Microsoft.AspNetCore.Mvc;
using StoreManager.Models.SyncfusionViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using StoreManager.Models;
using WebMathTraining.Models;

namespace StoreManager.Controllers.Api
{
  [Authorize]
  [Produces("application/json")]
  [Route("api/UserDetail")]
  public class UserDetailController : Controller
  {
    private readonly ICatalogRepository<TestGroup> _context;
    private readonly IAsyncRepository<UserProfile> _userProfileManager;

    public UserDetailController(ICatalogRepository<TestGroup> context, IAsyncRepository<UserProfile> userProfiles)
    {
      _context = context;
      _userProfileManager = userProfiles;
    }

    // GET: api/UserDetail
    [HttpGet]
    public async Task<IActionResult> GetUserDetail()
    {
      var headers = Request.Headers["TestGroupId"];
      int testGroupId = Convert.ToInt32(headers);
      var testGroup = await _context.GetByIdAsync(testGroupId);
      var list = await _userProfileManager.ListAllAsync();
        
      var Items = list.Where(u => testGroup.MemberObjectIds.Contains(u.ObjectId)).Select(user => new UserDetail(user){ApplicationUserId = testGroupId}).ToList();
      int Count = Items.Count();
      return Ok(new { Items, Count });
    }

    [HttpPost("[action]")]
    public IActionResult Insert([FromBody]CrudViewModel<UserDetail> payload)
    {
      var userDetail = payload.value;
      var testGroup = _context.GetByIdAsync(userDetail.ApplicationUserId).Result;
      testGroup.MemberObjectIds.Add(userDetail.Id);
      testGroup.MemberObjectIds = testGroup.MemberObjectIds;
      _context.UpdateAsync(testGroup).Wait();
      return Ok(userDetail);
    }


    [HttpPost("[action]")]
    public IActionResult Remove([FromBody]CrudViewModel<UserDetail> payload)
    {
      var userDetail = payload.value;
      var testGroup = _context.GetByIdAsync(userDetail.ApplicationUserId).Result;
      testGroup.MemberObjectIds.Remove(userDetail.Id);
      testGroup.MemberObjectIds = testGroup.MemberObjectIds;
      _context.UpdateAsync(testGroup).Wait();
      return Ok(userDetail);
    }

    //[HttpPost("[action]")]
    //public async Task<IActionResult> Update([FromBody] CrudViewModel<UserDetail> payload)
    //{
    //  var profile = payload.value;
    //  _context.UserProfile.Update(profile);
    //  await _context.SaveChangesAsync();
    //  return Ok(profile);
    //}
  }
}