using System;
using System.ComponentModel.DataAnnotations;

namespace WebMathTraining.Models
{
    public class NewTodoItem
    {
        [Required]
        public string Title { get; set; }
    }
}
