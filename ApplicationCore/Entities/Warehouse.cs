using System;
using System.ComponentModel.DataAnnotations;


namespace StoreManager.Models
{
  public class Warehouse
  {
    public int WarehouseId { get; set; }
    [Required] [MaxLength(128)] public string WarehouseName { get; set; }
    [MaxLength(1024)] public string Description { get; set; }
    [Display(Name = "Branch")] public int BranchId { get; set; }
  }
}
