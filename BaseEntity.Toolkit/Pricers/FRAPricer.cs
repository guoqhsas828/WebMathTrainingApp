/*
 * FRAPricer.cs
 *   2014. All rights reserved.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;
using CompoundingPeriod =
  System.Tuple<BaseEntity.Toolkit.Base.Dt, BaseEntity.Toolkit.Base.Dt, BaseEntity.Toolkit.Cashflows.FixingSchedule>;

namespace BaseEntity.Toolkit.Pricers
{
  ///<summary>
  /// Toolkit pricer for Forward Rate Agreement product
  ///</summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.FRA" />
  /// </remarks>
  [Serializable]
  public partial class FRAPricer : PricerBase, IPricer
  {
 
    #region Constructors

    ///<summary>
    /// Constructor based on the product and market information
    ///</summary>
    ///<param name="product">The underlying product</param>
    ///<param name="asOf">Pricing date</param>
    ///<param name="settle">Pricer settlement date</param>
    ///<param name="discountCurve">Discount curve</param>
    ///<param name="referenceCurve">Reference curve</param>
    ///<param name="notional">Amount</param>
    public FRAPricer(FRA product, Dt asOf, Dt settle, DiscountCurve discountCurve, DiscountCurve referenceCurve, double notional)
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      ReferenceCurve = referenceCurve;
      Notional = notional;
      ReferenceIsDiscount = Equals(ReferenceCurve, DiscountCurve) || (ReferenceCurve.Name == DiscountCurve.Name);
      SettlementRate = product.SettlementRate;
    }

    ///<summary>
    /// Constructor with final settlement rate
    ///</summary>
    ///<param name="product">The underlying product</param>
    ///<param name="asOf">Pricing date</param>
    ///<param name="settle">Pricer settlement date</param>
    ///<param name="discountCurve">Discount curve</param>
    ///<param name="referenceCurve">Reference curve</param>
    ///<param name="notional">Amount</param>
    ///<param name="settlementRate">Final settlement rate</param>
    public FRAPricer(FRA product, Dt asOf, Dt settle, DiscountCurve discountCurve, DiscountCurve referenceCurve, double notional, double settlementRate)
      : this(product, asOf, settle, discountCurve, referenceCurve, notional)
    {
      SettlementRate = settlementRate;
    }

    #endregion
    
    #region Properties

    /// <summary>
    /// Settlement rate <m>L(0,T, T + \Delta) - K</m>
    /// </summary>
    private double? SettlementRate { get; set; }
    
    
    ///<summary>
    /// The forward rate between FRA maturity and contract maturity, at initialization this rate is equal to the strike
    ///</summary>
    public double ImpliedFraRate
    {
      get
      {
        var paymentSchedule = GetPaymentSchedule(null, AsOf);
        var ip = paymentSchedule.First() as FloatingInterestPayment;
        if(ip != null)
          return ip.IndexFixing;
        return 0.0;
      }
    }


    ///<summary>
    /// The adjustment to payment because it happens at the period beginning instead of end.
    ///</summary>
    public double AdjustmentFactor
    {
      get
      {
        var paymentSchedule = GetPaymentSchedule(null, AsOf);
        var fip = paymentSchedule.First() as FraInterestPayment;
        if (fip != null)
          return fip.AdjustmentFactor;
        return 1.0;
      }
    }

    ///<summary>
    /// Discount curve
    ///</summary>
    public DiscountCurve DiscountCurve { get; private set; }


    ///<summary>
    /// Reference curve
    ///</summary>
    public DiscountCurve ReferenceCurve { get; private set; }

    ///<summary>
    /// Flag to indicate whether reference curve is the same as discount curve
    ///</summary>
    public bool ReferenceIsDiscount { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public FRA FRA
    {
      get { return (FRA) Product; }
    }

    /// <summary>
    /// The Payment pricer
    /// </summary>
    public override IPricer PaymentPricer
    {
      get
      {
        if (Payment != null)
        {
          if (paymentPricer_ == null)
            paymentPricer_ = BuildPaymentPricer(Payment, DiscountCurve);
        }
        return paymentPricer_;
      }
    }
    #endregion

    #region Methods
    /// <summary>
    ///   Net present value of the product, excluding the value
    ///   of any additional payment.
    /// </summary>
    /// <returns>Pv of Fra</returns>
    public override double ProductPv()
    {
      if (Settle >= Product.Maturity)
        return 0.0;

      var ps = GetPaymentSchedule(null, AsOf);
      return CurrentNotional * ps.CalculatePv(AsOf, Settle, DiscountCurve, null,
               null, 0.0, 0, TimeUnit.None, AdapterUtil.CreateFlags(false, false, false));
    }

    /// <summary>
    ///   Get Payment Schedule for this product from the specified date
    /// </summary>
    /// <remarks>
    ///   <para>Derived pricers may implement this, otherwise a NotImplementedException is thrown.</para>
    /// </remarks>
    /// <param name="ps">Payment schedule</param>
    /// <param name="from">Date to generate Payment Schedule from</param>
    /// <returns>PaymentSchedule from the specified date or null if not supported</returns>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from)
    {
      return FRA.GetPaymentSchedule(AsOf, ps, from, ReferenceCurve, DiscountCurve, SettlementRate);
    }

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(ArrayList errors)
    {
      if (FRA == null)
        return;

      if (FRA.FixingDate != Dt.Empty && AsOf > FRA.FixingDate && !SettlementRate.HasValue)
      {
        InvalidValue.AddError(errors, this, "SettlementRate", "Missing settlement rate");
      }

      if (FRA.FixingDate != Dt.Empty && AsOf < FRA.FixingDate && SettlementRate.HasValue)
      {
        InvalidValue.AddError(errors, this, "SettlementRate", string.Format("SettlementRate shall not be entered before the contract fixing day [{0}]",
                                            FRA.FixingDate));
      }

    }

    #endregion
  }
}