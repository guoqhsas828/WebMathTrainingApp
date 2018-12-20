using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WebMathTraining.Models
{
    public class TestQuestionViewModel
    {
        public string Id { get; set; }

        public TestCategory Category { get; set; }

        [Required]
        public int Level { get; set; }

        [Display(Name = "Question Name")]
        public string QuestionName { get; set; }

        [Required]
        public string QuestionId { get; set; }

      public string StatusMessage { get; set; }

  }
}
