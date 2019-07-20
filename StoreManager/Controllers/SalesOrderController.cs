using System;
using System.Collections.Generic;
using System.Linq;
using StoreManager.Interfaces;
using StoreManager.Data;
using StoreManager.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace StoreManager.Controllers
{
    [Authorize(Roles = Pages.MainMenu.SalesOrder.RoleName)]
    public class SalesOrderController : Controller
    {
        private readonly IAsyncRepository<SalesOrder> _context;

        public SalesOrderController(IAsyncRepository<SalesOrder> context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Detail(int id)
        {
            var salesOrder = _context.GetByIdAsync(id).Result;

            if (salesOrder == null)
            {
                return NotFound();
            }

            return View(salesOrder);
        }
    }
}