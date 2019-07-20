using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace StoreManager.Models
{
  public class Bill : BaseEntityModel
  {
    public int BillId
    {
      get { return Id; }
      set { Id = value; }
    }

    [Display(Name = "Bill / Invoice Number")]
    [MaxLength(64)]
    public string BillName { get; set; }

    [Display(Name = "GRN")] public int GoodsReceivedNoteId { get; set; }

    [Display(Name = "Vendor Delivery Order #")]
    [MaxLength(900)]
    public string VendorDONumber { get; set; }

    [Display(Name = "Vendor Bill / Invoice #")]
    [MaxLength(900)]
    public string VendorInvoiceNumber { get; set; }

    [Display(Name = "Bill Date")] public DateTimeOffset BillDate { get; set; }
    [Display(Name = "Bill Due Date")] public DateTimeOffset BillDueDate { get; set; }
    [Display(Name = "Bill Type")] public int BillTypeId { get; set; }
  }
}
