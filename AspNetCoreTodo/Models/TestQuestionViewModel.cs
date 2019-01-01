using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace WebMathTraining.Models
{
  public class TestQuestionViewModel
  {
    public Guid Id { get; set; }

    public TestCategory Category { get; set; }

    [Required]
    public int Level { get; set; }

    [Display(Name = "Image Name")]
    public string ImageName { get { return Image?.Name ?? ""; } }

    [Required]
    public string ImageId { get { return Image?.Id.ToString() ?? ""; } }

    public TestImage Image { get; set; }
    public string StatusMessage { get; set; }

  }
}
