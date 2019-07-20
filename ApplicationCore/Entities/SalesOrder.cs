using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using StoreManager.Interfaces;

namespace StoreManager.Models
{
  public class SalesOrder : BaseEntityModel, IAggregateRoot
  {
    public int SalesOrderId
    {
      get { return Id; }
      set { Id = value; }
    }

    [Display(Name = "Order Number")]
    [MaxLength(128)]
    public string SalesOrderName { get; set; }

    [Display(Name = "Branch")] public int BranchId { get; set; }
    [Display(Name = "Customer")] public int CustomerId { get; set; }
    public DateTimeOffset OrderDate { get; set; }
    public DateTimeOffset DeliveryDate { get; set; }

    [Display(Name = "Currency")] public int CurrencyId { get; set; }

    [Display(Name = "Customer Ref. Number")]
    [MaxLength(128)]
    public string CustomerRefNumber { get; set; }

    [Display(Name = "Sales Type")] public int SalesTypeId { get; set; }

    [Display(Name = "Notes")]
    [MaxLength(1024)]
    public string Remarks { get; set; }

    public double Amount { get; set; }
    public double SubTotal { get; set; }
    public double Discount { get; set; }
    public double Tax { get; set; }
    public double Freight { get; set; }
    public double Total { get; set; }
    public List<SalesOrderLine> SalesOrderLines { get; set; } = new List<SalesOrderLine>();
  }
}
