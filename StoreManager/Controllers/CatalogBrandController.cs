using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreManager.Controllers
{
    [Authorize(Roles = Pages.MainMenu.CatalogBrand.RoleName)]
    public class CatalogBrandController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}