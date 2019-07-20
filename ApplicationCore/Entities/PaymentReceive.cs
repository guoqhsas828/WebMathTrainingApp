using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace StoreManager.Models
{
  public class PaymentReceive
  {
    public int PaymentReceiveId { get; set; }

    [Display(Name = "Payment Number")]
    [MaxLength(128)]
    public string PaymentReceiveName { get; set; }

    [Display(Name = "Invoice")] public int InvoiceId { get; set; }
    public DateTimeOffset PaymentDate { get; set; }
    [Display(Name = "Payment Type")] public int PaymentTypeId { get; set; }
    public double PaymentAmount { get; set; }
    [Display(Name = "Full Payment")] public bool IsFullPayment { get; set; } = true;
  }
}
