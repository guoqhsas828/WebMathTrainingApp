using System;
using System.ComponentModel.DataAnnotations;

namespace StoreManager.Models
{
  public class PaymentType
  {
    public int PaymentTypeId { get; set; }
    [Required] [MaxLength(128)] public string PaymentTypeName { get; set; }
    [MaxLength(1024)] public string Description { get; set; }
  }
}
