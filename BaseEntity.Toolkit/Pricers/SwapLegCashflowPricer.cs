//
// SwapLegCashflowPricer.cs
//  -2008. All rights reserved.
//
// TBD: Support floating rate coupons in accrued calculation. RTD Jun05
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.SwapLeg">IR Swaps</see> using the
  ///   <see cref="BaseEntity.Toolkit.Models.CashflowModel">General Cashflow Model</see>.</para>
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.SwapLeg" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="CashflowPricer" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.SwapLeg">Swap Leg Product</seealso>
  /// <seealso cref="BaseEntity.Toolkit.Models.CashflowModel">Cashflow pricing model</seealso>
  /// <seealso cref="SwapPricer"/>
  [Serializable]
  public partial class SwapLegCashflowPricer : CashflowPricer
  {
    #region Constructors

    /// <summary>
    ///   Constructor for a raw Swapleg pricer
    /// </summary>
    ///
    /// <param name="product">SwapLeg to price</param>
    ///
    public SwapLegCashflowPricer(SwapLeg product)
      : base(product)
    {}

    /// <summary>
    ///   Construct a SwapLeg cashflow pricer
    /// </summary>
    ///
    /// <param name="product">SwapLeg to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    ///
    public SwapLegCashflowPricer(
      SwapLeg product, Dt asOf, Dt settle, DiscountCurve discountCurve
      )
      : base(product, asOf, settle, discountCurve, null)
    {}

    /// <summary>
    ///   Construct a SwapLeg cashflow pricer
    /// </summary>
    ///
    /// <param name="product">SwapLeg to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Floating rate reference curve (if required)</param>
    ///
    public SwapLegCashflowPricer(
      SwapLeg product, Dt asOf, Dt settle, DiscountCurve discountCurve, DiscountCurve referenceCurve
      )
      : base(product, asOf, settle, discountCurve, referenceCurve)
    {}

    /// <summary>
    ///   Construct a SwapLeg cashflow pricer
    /// </summary>
    ///
    /// <param name="product">SwapLeg to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Floating rate reference curve</param>
    /// <param name="rateResets">Float rate historical resets</param>
    ///
    public SwapLegCashflowPricer(
      SwapLeg product, Dt asOf, Dt settle, DiscountCurve discountCurve, DiscountCurve referenceCurve,
      IList<RateReset> rateResets
      )
      : base(product, asOf, settle, discountCurve, referenceCurve, rateResets)
    {}

    /// <summary>
    ///   Construct a SwapLeg pricer with counterparty exposure
    /// </summary>
    ///
    /// <param name="product">SwapLeg to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Floating rate reference curve (if required)</param>
    /// <param name="counterpartyCurve">Survival Curve of counterparty</param>
    ///
    public
    SwapLegCashflowPricer(SwapLeg product,
                           Dt asOf,
                           Dt settle,
                           DiscountCurve discountCurve,
                           DiscountCurve referenceCurve,
                           SurvivalCurve counterpartyCurve)
      : base(product, asOf, settle, discountCurve, referenceCurve)
    {
      CounterpartyCurve = counterpartyCurve;
    }


    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Calculate the present value <formula inline="true">Pv = Full Price \times Notional</formula> of the cash flow stream
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Cashflows after the settlement date are present valued back to the pricing
    ///   as-of date.</para>
    /// </remarks>
    ///
    /// <returns>Present value to the pricing as-of date of the cashflow stream</returns>
    ///
    public override double ProductPv()
    {
      double pv = base.ProductPv();
      // add initial exchange to PV
      if (SwapLeg.InitialExchange && SwapLeg.Effective > Settle)
      {
        // Even though the initial exchange appears in the cashflow, 
        // a design limitation in the CashflowModel.Pv() does not allow us to model 
        // a cash settle that is on or before the accrual start, so we override Pv() 
        // here to add it in for Pv before the initial exchange date
        double principalSettleDiscountFactor = DiscountCurve.DiscountFactor(AsOf, SwapLeg.Effective);
        double initialExchangePv = -1 * Notional * principalSettleDiscountFactor;
        pv += initialExchangePv;
      }

      return pv;
    }


    /// <summary>
    ///   Validate product
    /// </summary>
    ///
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    ///
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      if( SwapLeg.Floating && ReferenceCurve == null )
        InvalidValue.AddError(errors, this, "ReferenceCurve", "Floating swap missing reference curve");

      return;
    }

    /// <summary>
    ///   Return coupon rate at given date
    /// </summary>

    public double CouponAt(Dt date)
    {
      if (SwapLeg.Floating)
        return RateResetUtil.ResetAt(RateResets, date);
      else
        return SwapLeg.Coupon;
    }

    /// <summary>
    ///   Calculate the accrued premium for a Swap
    /// </summary>
    ///
    /// <returns>Accrued premium of Swap at the settlement date</returns>
    ///
    public override double
    Accrued()
    {
      return (Accrued(Settle) * CurrentNotional);
    }

    /// <summary>
    ///   Calculate the accrued premium for a Swap as a percentage of Notional
    /// </summary>
    ///
    /// <param name="settle">Settlement date</param>
    ///
    /// <returns>Accrued to settlement for Swap as a percentage of Notional</returns>
    ///
    public double
    Accrued(Dt settle)
    {
      SwapLeg swapLeg = this.SwapLeg;
      // Generate out payment dates from settlement.
      Schedule sched = new Schedule(settle, swapLeg.Effective, swapLeg.FirstCoupon,
                                    swapLeg.Maturity, swapLeg.Freq, swapLeg.BDConvention,
                                    swapLeg.Calendar);
      if (sched.Count == 0)
        return 0.0;

      // Calculate accrued to settlement.
      Dt start = sched.GetPeriodStart(0);
      Dt end = sched.GetPeriodEnd(0);
      // Note schedule currently includes last date in schedule period. This may get changed in
      // the future so to handle this we test if we are on a coupon date.
      if (Dt.Cmp(settle, start) == 0 || Dt.Cmp(settle, end) == 0)
        return 0.0;
      return Dt.Fraction(start, end, start, settle, swapLeg.DayCount, swapLeg.Freq) * CouponAt(settle);
    }

    /// <summary>
    ///   Calculates par coupon(swap rate) of an IR Swap
    /// </summary>
    ///
    /// <param name="fv">Interest Rate Swap MTM</param>
    /// <param name="fixedSwapLegPricer">Fixed Leg Swap Pricer</param>
    /// <param name="floatSwapLegPricer">Floating Leg Swap Pricer</param>
    ///
    /// <returns>Swap Rate of  IR Cap</returns>
    ///
    public double SwapRate(
      double fv,
      SwapLegCashflowPricer fixedSwapLegPricer,
			SwapLegCashflowPricer floatSwapLegPricer)
    {

      double savedFixedRate = fixedSwapLegPricer.SwapLeg.Coupon;
      Optimizer opt = new Optimizer(fixedSwapLegPricer,
        floatSwapLegPricer);
      double floatLegPv = floatSwapLegPricer.ProductPv();

      // Set up root finder
      Brent rf = new Brent();
      rf.setToleranceX(1e-3);
      rf.setToleranceF(1e-6);
      rf.setLowerBounds(1E-10);

      // Solve
      double res;
      try
      {
        double v = opt.EvaluatePv(0.1);
        if (v >= fv)
          res = rf.solve(opt.EvaluatePv, null, fv, 0.001, 0.10);
        else
          res = rf.solve(opt.EvaluatePv, null, fv, 0.1, 0.8);
      }
      finally
      {
        // Tidy up transient data
        fixedSwapLegPricer.SwapLeg.Coupon = savedFixedRate;
      }
      return res;
    }


    //-
    // Helper class optimizers
    //-
    // compute the value of the Swap given Swap Rate.
    // Called by solver in SwapRate().
    class Optimizer
    {
      public Optimizer(SwapLegCashflowPricer fixedSwapLegPricer,
        SwapLegCashflowPricer floatSwapLegPricer)
      {
        fixedSwapLegPricer_ = fixedSwapLegPricer;
        floatSwapLegPricer_ = floatSwapLegPricer;
        return;
      }

      public double EvaluatePv(double swapRate)
      {
        double v = 0.0;
        double factor = swapRate/fixedSwapLegPricer_.SwapLeg.Coupon;
        var savedCouponSchedule = new List<CouponPeriod>(fixedSwapLegPricer_.SwapLeg.CouponSchedule);
        double savedFixedRate = fixedSwapLegPricer_.SwapLeg.Coupon;
        try
        {
          fixedSwapLegPricer_.SwapLeg.Coupon = swapRate ;
          if(savedCouponSchedule.Count!=0)
          {
            fixedSwapLegPricer_.SwapLeg.CouponSchedule.Clear();
            savedCouponSchedule.
              ForEach(c =>
                      fixedSwapLegPricer_.SwapLeg.CouponSchedule.Add(
                        new CouponPeriod(c.Date,
                                         c.Coupon *
                                         factor)));
          }
          fixedSwapLegPricer_.Reset();
          v = fixedSwapLegPricer_.ProductPv() - floatSwapLegPricer_.ProductPv();
        }
        finally
        {
          fixedSwapLegPricer_.SwapLeg.CouponSchedule.Clear();
          savedCouponSchedule.ForEach(c => fixedSwapLegPricer_.SwapLeg.CouponSchedule.Add(c));
          fixedSwapLegPricer_.SwapLeg.Coupon = savedFixedRate;
          fixedSwapLegPricer_.Reset();
        }
        return v;
      }
      private SwapLegCashflowPricer floatSwapLegPricer_;
      private SwapLegCashflowPricer fixedSwapLegPricer_;
      //private List<CouponPeriod> coponSchedule_;
    }


    #endregion // Methods

    #region Properties

    /// <summary>
    ///   Swapleg Product
    /// </summary>
    public SwapLeg SwapLeg
    {
      get { return (SwapLeg)Product; }
    }

    /// <summary>
    ///   Cashflow flags
    /// </summary>
    [Browsable(false)]
    public CashflowFlag CashflowFlags
    {
      get { return flags_; }
      set { flags_ = value; }
    }

    #endregion Properties

    #region Data

    private CashflowFlag flags_;

    #endregion // Data

  } // class SwapLegCashflowPricer
}
