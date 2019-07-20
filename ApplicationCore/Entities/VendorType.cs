using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace StoreManager.Models
{
  public class VendorType
  {
    public int VendorTypeId { get; set; }
    [Required] [MaxLength(128)] public string VendorTypeName { get; set; }
    [MaxLength(1024)] public string Description { get; set; }
  }
}
