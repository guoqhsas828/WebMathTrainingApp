//
// DelayedPayment.cs
// 
//

using System;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows
{
  #region Delayed coupon payments

  /// <summary>
  ///  Reinvestment payment, accrue coupon payment on payment date.
  ///  Required for Sell/Buy backs
  /// </summary>
  [Serializable]
  public class DelayedPayment : OneTimePayment
  {
    #region Constructors

    /// <summary>
    /// Create a simple delayed payment
    /// </summary>
    /// <param name="couponDt">Coupon (or Dividend) payment date</param>
    /// <param name="payDt">Payment Date</param>
    /// <param name="coupon"></param>
    /// <param name="ccy">Currency of payment</param>
    public DelayedPayment(Dt couponDt, Dt payDt, double coupon, Currency ccy)
      : base(payDt, ccy)
    {
      Amount = coupon;
      CouponDt = couponDt;
    }

    #endregion

    /// <summary>
    /// Asset coupon date
    /// </summary>
    private Dt CouponDt { get; set; }
  }

  #endregion

}
