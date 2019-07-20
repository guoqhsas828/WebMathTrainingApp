using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace StoreManager.Models
{
  public class Currency
  {
    public int CurrencyId { get; set; }
    [Required] [MaxLength(64)] public string CurrencyName { get; set; }
    [Required] [MaxLength(8)] public string CurrencyCode { get; set; }
    [MaxLength(1024)] public string Description { get; set; }
  }
}
