using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseEntity.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StoreManager.Models.SyncfusionViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.eShopWeb.Infrastructure.Data;

namespace StoreManager.Controllers.Api
{
  [Authorize]
  [Produces("application/json")]
  [Route("api/PluginAssembly")]
  public class PluginAssemblyController : Controller
  {
    private readonly CatalogContext _context;

    public PluginAssemblyController(CatalogContext context)
    {
      _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetPluginAssembly()
    {
      var Items = await _context.PluginAssembly.ToListAsync();
      int Count = Items.Count();
      return Ok(new {Items, Count});
    }

    [HttpPost("[action]")]
    public IActionResult Insert([FromBody] CrudViewModel<PluginAssembly> payload)
    {
      var entity = payload.value;
      _context.PluginAssembly.Add(entity);
      _context.SaveChanges();
      return Ok(entity);
    }

    [HttpPost("[action]")]
    public IActionResult Update([FromBody] CrudViewModel<PluginAssembly> payload)
    {
      var entity = payload.value;
      _context.PluginAssembly.Update(entity);
      _context.SaveChanges();
      return Ok(entity);
    }

    [HttpPost("[action]")]
    public IActionResult Remove([FromBody] CrudViewModel<PluginAssembly> payload)
    {
      var entity = _context.PluginAssembly
        .FirstOrDefault(x => x.Id.Equals(payload.key));
      _context.PluginAssembly.Remove(entity);
      _context.SaveChanges();
      return Ok(entity);

    }
  }
}