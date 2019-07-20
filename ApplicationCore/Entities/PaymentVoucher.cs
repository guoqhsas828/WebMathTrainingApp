using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace StoreManager.Models
{
  public class PaymentVoucher
  {
    public int PaymentVoucherId { get; set; }

    [Display(Name = "Payment Voucher Number")]
    [MaxLength(128)]
    public string PaymentVoucherName { get; set; }

    [Display(Name = "Bill")] public int BillId { get; set; }
    public DateTimeOffset PaymentDate { get; set; }
    [Display(Name = "Payment Type")] public int PaymentTypeId { get; set; }
    public double PaymentAmount { get; set; }
    [Display(Name = "Payment Source")] public int CashBankId { get; set; }
    [Display(Name = "Full Payment")] public bool IsFullPayment { get; set; } = true;
  }
}
