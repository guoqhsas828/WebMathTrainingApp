using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using WebMathTraining.Models;
using WebMathTraining.Services;

namespace WebMathTraining.Controllers
{
  [Authorize]
  public class TodoController : Controller
  {
    private readonly ITodoItemService _todoItemService;
    private readonly UserManager<ApplicationUser> _userManager;

    public TodoController(ITodoItemService todoItemService, UserManager<ApplicationUser> userManager)
    {
      _todoItemService = todoItemService;
      _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
      var currentUser = await _userManager.GetUserAsync(User);
      if (currentUser == null)
      {
        return Challenge();
      }

      var isAdmin = await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);

      TodoItem[] todoItems;
      if (!isAdmin)
      {
        todoItems = await _todoItemService.GetIncompleteItemsAsync(currentUser);
      }
      else
      {
        todoItems = await _todoItemService.GetAllItemsAsync();
      }

      var model = new TodoViewModel()
      {
        Items = todoItems
      };

      return View(model);
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(TodoItem newItem)
    {
      if (!ModelState.IsValid)
      {
        return RedirectToAction(nameof(Index));
      }

      var currentUser = await _userManager.GetUserAsync(User);
      if (currentUser == null)
      {
        return RedirectToAction(nameof(Index));
      }

      var successful = await _todoItemService.AddItemAsync(newItem, currentUser);
      if (!successful)
      {
        return BadRequest("Could not add item.");
      }

      return RedirectToAction(nameof(Index));
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkDone(string id)
    {
      if (string.IsNullOrEmpty(id))
      {
        return RedirectToAction(nameof(Index));
      }

      var currentUser = await _userManager.GetUserAsync(User);
      if (currentUser == null)
      {
        return RedirectToAction(nameof(Index));
      }

      var isAdmin = await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);
      if (!isAdmin)
        return BadRequest("Need to have Admin Permission to mark item as done.");

      var successful = await _todoItemService.MarkDoneAsync(id, currentUser);
      if (!successful)
      {
        return BadRequest("Could not mark item as done.");
      }

      return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(string id)
    {
      if (string.IsNullOrEmpty(id))
        return RedirectToAction(nameof(Index));

      var currentUser = await _userManager.GetUserAsync(User);
      if (currentUser == null)
      {
        return RedirectToAction("Index");
      }

      var isAdmin = await _userManager.IsInRoleAsync(currentUser, Constants.AdministratorRole);
      if (!isAdmin)
        return BadRequest("Need to have Admin Permission to delete item.");

      var status = await _todoItemService.DeleteItemAsync(id);
      if (!status)
        return BadRequest("Could not delete item");

      return RedirectToAction(nameof(Index));
    }
  }
}
