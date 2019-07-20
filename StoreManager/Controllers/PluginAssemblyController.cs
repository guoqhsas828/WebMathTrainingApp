using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreManager.Controllers
{
    [Authorize(Roles = Constants.AdministratorRole)]
    public class PluginAssemblyController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}