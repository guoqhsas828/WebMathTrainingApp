using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace StoreManager.Models
{
  public class BillType
  {
    public int BillTypeId { get; set; }

    [Required] [MaxLength(900)] public string BillTypeName { get; set; }

    [MaxLength(1024)] public string Description { get; set; }
  }
}
