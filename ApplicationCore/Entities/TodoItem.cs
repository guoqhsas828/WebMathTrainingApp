using System;
using System.ComponentModel.DataAnnotations;
using StoreManager.Models;

namespace WebMathTraining.Models
{
  public class TodoItem : CatalogEntityModel
  {
    public int TodoItemId { get { return Id; } set { Id = value; } }

    [MaxLength(900)]
    public string OwnerId { get; set; }
    
    public bool IsDone { get; set; }
    
    [Required]
    [MaxLength(1024)]
    public string Title { get; set; }

    public DateTimeOffset DueAt { get; set; } //?
  }

}
