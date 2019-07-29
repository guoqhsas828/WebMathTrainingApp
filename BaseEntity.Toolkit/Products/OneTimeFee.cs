/*
 * OneTimeFee.cs
 *
 *   2010. All rights reserved.
 *
 */


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Product to represent a single future fee of fixed size.
  /// </summary>
  [Serializable]
  public class OneTimeFee : Product
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="ccy"></param>
    /// <param name="amount"></param>
    /// <param name="paymentSettle"></param>
    /// <param name="description"></param>
    public OneTimeFee(Currency ccy, double amount, Dt paymentSettle, string description)
    {
      Ccy = ccy;
      Amount = amount;
      Maturity = paymentSettle;
      Description = description;
    }

    /// <summary>
    /// Date that fee is made
    /// </summary>
    public Dt PaymentSettle { get { return Maturity; } set { Maturity = value; } }

    /// <summary>
    /// Amount of Fee
    /// </summary>
    public double Amount { get { return Notional; } set { Notional = value; } }
  }
}
