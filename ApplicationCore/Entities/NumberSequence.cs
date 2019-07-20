using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace StoreManager.Models
{
  public class NumberSequence
  {
    public int NumberSequenceId { get; set; }
    [Required] [MaxLength(128)] public string NumberSequenceName { get; set; }
    [Required] [MaxLength(1024)] public string Module { get; set; }
    [Required] [MaxLength(128)] public string Prefix { get; set; }
    public int LastNumber { get; set; }
  }
}
