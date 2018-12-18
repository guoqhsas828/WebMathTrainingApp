using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using WebMathTraining.Data;
using WebMathTraining.Models;

namespace WebMathTraining.Services
{
    public class TodoItemService : ITodoItemService
    {
        private readonly ApplicationDbContext _context;

        public TodoItemService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> AddItemAsync(TodoItem newItem, ApplicationUser user)
        {
            newItem.Id = Guid.NewGuid().ToString();
            newItem.OwnerId = user.Id;
            newItem.IsDone = false;
            newItem.DueAt = DateTimeOffset.Now.AddDays(3).ToString();

            _context.TodoItems.Add(newItem);

            var saveResult = await _context.SaveChangesAsync();
            return saveResult == 1;
        }

        public async Task<TodoItem[]> GetIncompleteItemsAsync(ApplicationUser user)
        {
            return await _context.TodoItems
                .Where(x => string.Compare(x.OwnerId, user.Id, StringComparison.InvariantCultureIgnoreCase) ==0).OrderBy(x => x.IsDone) // x.IsDone == false &&
                .ToArrayAsync();
        }

        public async Task<bool> MarkDoneAsync(Guid id, ApplicationUser user)
        {
          var idStr = id.ToString();
            var item = await _context.TodoItems
                .Where(x => (string.Compare(x.Id, idStr, StringComparison.InvariantCultureIgnoreCase) ==0) && x.OwnerId == user.Id)
                .SingleOrDefaultAsync();

            if (item == null) return false;

            item.IsDone = true;

            var saveResult = await _context.SaveChangesAsync();
            return saveResult == 1; // One entity should have been updated
        }
    }
}
