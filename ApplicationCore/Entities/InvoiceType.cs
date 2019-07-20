using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace StoreManager.Models
{
  public class InvoiceType
  {
    public int InvoiceTypeId { get; set; }
    [Required] [MaxLength(128)] public string InvoiceTypeName { get; set; }
    [MaxLength(1024)] public string Description { get; set; }
  }
}
