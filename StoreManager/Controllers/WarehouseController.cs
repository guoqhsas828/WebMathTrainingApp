﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreManager.Controllers
{
    [Authorize(Roles = Pages.MainMenu.Warehouse.RoleName)]
    public class WarehouseController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}