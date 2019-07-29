// 
//  -2015. All rights reserved.
// 

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using System;

namespace BaseEntity.Toolkit.Cashflows.Payments
{
  /// <summary>
  ///   Represent a payment scaled by a constant notional
  /// </summary>
  [Serializable]
  public class ScaledPayment : Payment
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="ScaledPayment"/> class.
    /// </summary>
    /// <param name="underlyingPayment">The underlying payment.</param>
    /// <param name="notional">The notional amount</param>
    public ScaledPayment(
      Payment underlyingPayment,
      double notional)
      : base(underlyingPayment.PayDt, underlyingPayment.Ccy)
    {
      if (!underlyingPayment.CutoffDate.IsEmpty())
        CutoffDate = underlyingPayment.CutoffDate;
      FXCurve = underlyingPayment.FXCurve;
      UnderlyingPayment = underlyingPayment;
      Notional = notional;
    }

    /// <summary>
    /// Gets the underlying payment.
    /// </summary>
    /// <value>The underlying payment.</value>
    public Payment UnderlyingPayment { get; private set; }

    /// <summary>
    /// Gets the notional.
    /// </summary>
    /// <value>The notional.</value>
    public double Notional { get; }

    /// <summary>
    /// True if the payment is projected
    /// </summary>
    /// <value><c>true</c> if this instance is projected; otherwise, <c>false</c>.</value>
    public override bool IsProjected
    {
      get { return UnderlyingPayment.IsProjected; }
    }

    /// <summary>
    /// Compute the payment amount
    /// </summary>
    /// <returns>Payment amount</returns>
    protected override double ComputeAmount()
    {
      UnderlyingPayment.FXRate = FXRate;
      return UnderlyingPayment.Amount * Notional;
    }

    /// <summary>
    /// Accrued payment up to date
    /// </summary>
    /// <param name="date">Date</param>
    /// <param name="accrual">Coupon - Accrued</param>
    /// <returns>Accrued amount</returns>
    public override double Accrued(Dt date, out double accrual)
    {
      var accrued = UnderlyingPayment.Accrued(date, out accrual);
      accrual *= Notional;
      return accrued * Notional;
    }

    /// <summary>
    /// Risky discount payment amount to date <m>E^T(\beta_T (1 - L_T))</m> where <m>L_T</m> is the cumulative loss process.
    /// </summary>
    /// <param name="discountFunction">Discount curve</param>
    /// <param name="survivalFunction">Expected surviving notional as function of time</param>
    /// <returns>Risky discount</returns>
    public override double RiskyDiscount(Func<Dt, double> discountFunction,
      Func<Dt, double> survivalFunction)
    {
      return UnderlyingPayment.RiskyDiscount(discountFunction, survivalFunction);
    }

    /// <summary>
    /// Scale the payment appropriately
    /// </summary>
    /// <param name="factor">Scaling factor</param>
    public override void Scale(double factor)
    {
      UnderlyingPayment.Scale(factor);
    }
  }



}
