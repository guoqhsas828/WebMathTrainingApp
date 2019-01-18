using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace WebMathTraining.Models
{
  public enum CloudContainer
  {
    None = 0,
    mathpicblobs = 1,
    mkg2012grp = 2,
    mkg2013grp = 3,
    mkgothers = 4,
    physicspicblobs = 8,
    misc = 9
  }

  [NotMapped]
  public class ActionPolicy
  {
    public string Department { get; set; }
    public string Manager { get; set; }
    public string Assistant { get; set; }
  }
}
