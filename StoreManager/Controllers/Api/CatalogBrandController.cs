using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StoreManager.Data;
using StoreManager.Models;
using StoreManager.Models.SyncfusionViewModels;
using Microsoft.AspNetCore.Authorization;

namespace StoreManager.Controllers.Api
{
    [Authorize]
    [Produces("application/json")]
    [Route("api/CatalogBrand")]
    public class CatalogBrandController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CatalogBrandController(ApplicationDbContext context)
        {
            _context = context;
        }

    // GET: api/CatalogBrand
    [HttpGet]
        public async Task<IActionResult> GetCatalogBrand()
        {
            List<CatalogBrand> Items = await _context.CatalogBrand.ToListAsync();
            int Count = Items.Count();
            return Ok(new { Items, Count });
        }



        [HttpPost("[action]")]
        public IActionResult Insert([FromBody]CrudViewModel<CatalogBrand> payload)
        {
            var val = payload.value;
            _context.CatalogBrand.Add(val);
            _context.SaveChanges();
            return Ok(val);
        }

        [HttpPost("[action]")]
        public IActionResult Update([FromBody]CrudViewModel<CatalogBrand> payload)
        {
            var val = payload.value;
            _context.CatalogBrand.Update(val);
            _context.SaveChanges();
            return Ok(val);
        }

        [HttpPost("[action]")]
        public IActionResult Remove([FromBody]CrudViewModel<CatalogBrand> payload)
        {
            var val = _context.CatalogBrand
                .Where(x => x.Id == (int)payload.key)
                .FirstOrDefault();
            _context.CatalogBrand.Remove(val);
            _context.SaveChanges();
            return Ok(val);

        }
    }
}