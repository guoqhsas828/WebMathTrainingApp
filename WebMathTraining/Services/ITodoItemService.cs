using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StoreManager.Models;
using WebMathTraining.Models;

namespace WebMathTraining.Services
{
  public interface ITodoItemService
  {
    Task<TodoItem[]> GetIncompleteItemsAsync(ApplicationUser user);

    Task<bool> AddItemAsync(TodoItem newItem, ApplicationUser user);

    Task<bool> MarkDoneAsync(string id, ApplicationUser user);

    Task<TodoItem[]> GetAllItemsAsync();

    Task<bool> DeleteItemAsync(string id);
  }
}
