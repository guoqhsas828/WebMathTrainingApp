using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace StoreManager.Models
{
  public class GoodsReceivedNote
  {
    public int GoodsReceivedNoteId { get; set; }

    [Display(Name = "GRN Number")]
    [MaxLength(128)]
    public string GoodsReceivedNoteName { get; set; }

    [Display(Name = "Purchase Order")] public int PurchaseOrderId { get; set; }
    [Display(Name = "GRN Date")] public DateTimeOffset GRNDate { get; set; }

    [Display(Name = "Vendor Delivery Order #")]
    [MaxLength(128)]
    public string VendorDONumber { get; set; }

    [Display(Name = "Vendor Bill / Invoice #")]
    [MaxLength(128)]
    public string VendorInvoiceNumber { get; set; }

    [Display(Name = "Warehouse")] public int WarehouseId { get; set; }
    [Display(Name = "Full Receive")] public bool IsFullReceive { get; set; } = true;
  }
}
