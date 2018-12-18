using System;
using System.ComponentModel.DataAnnotations;

namespace WebMathTraining.Models
{
    public class TodoItem
    {
        public string Id { get; set; }

        public string OwnerId { get; set; }
        
        public bool IsDone { get; set; }

        [Required]
        public string Title { get; set; }

        public string DueAt { get; set; } //DateTimeOffset?
  }
}
