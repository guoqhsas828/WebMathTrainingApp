using System;
using System.ComponentModel.DataAnnotations;

namespace StoreManager.Models
{
  public class CustomerType
  {
    public int CustomerTypeId { get; set; }
    [Required] [MaxLength(64)] public string CustomerTypeName { get; set; }
    [MaxLength(1024)] public string Description { get; set; }
  }
}
