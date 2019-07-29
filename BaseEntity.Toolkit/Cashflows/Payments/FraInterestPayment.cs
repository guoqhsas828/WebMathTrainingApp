using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows.Expressions;
using BaseEntity.Toolkit.Curves;
using CompoundingPeriod =
  System.Tuple<BaseEntity.Toolkit.Base.Dt, BaseEntity.Toolkit.Base.Dt, BaseEntity.Toolkit.Cashflows.FixingSchedule>;

namespace BaseEntity.Toolkit.Cashflows
{
  #region Nested type: FraInterestPayment

  internal class FraInterestPayment : FloatingInterestPayment
  {
    #region Constructor
    /// <summary>
    /// Floating rate payment
    /// </summary>
    /// <param name="payDt">Payment Date</param>
    /// <param name="ccy">Currency of payment</param>
    /// <param name="cycleStart">Start of payment cycle</param>
    /// <param name="cycleEnd">End of payment cycle</param>
    /// <param name="periodStart">Start of accrual period</param>
    /// <param name="periodEnd">End of accrual period</param>
    /// <param name="notional">notional for this payment</param>
    /// <param name="spread">fixed spread for this payment over the floating rate</param>
    /// <param name="strike">strike rate of the FRA</param>
    /// <param name="dc">Day Count convention for calcing Accrual</param>
    /// <param name="rateProjector">Coupon calculator for rate calculation</param>
    /// <param name="forwardAdjustment">Calculator for convexity adjustment</param>
    public FraInterestPayment(Dt payDt, Currency ccy, Dt cycleStart, Dt cycleEnd, Dt periodStart, Dt periodEnd, double notional,
      double spread, double strike, DayCount dc, IRateProjector rateProjector, IForwardAdjustment forwardAdjustment)
      : base(Dt.Empty, payDt, ccy, cycleStart, cycleEnd, periodStart, periodEnd, Dt.Empty, notional, spread, dc, Frequency.None, CompoundingConvention.None,
      rateProjector, forwardAdjustment)
    {
      AccrueOnCycle = true;
      Strike = strike;
    }
    #endregion

    #region Methods
    /// <summary>
    /// Convert a payment to a cashflow node used to simulate the 
    /// realized payment amount 
    /// </summary>
    /// <param name="notional">Notional amount</param>
    /// <param name="discountCurve">Discount curve to discount payment</param>
    /// <param name="survivalFunction">Surviving principal if coupon is credit contingent</param>
    /// <returns>ICashflowNode</returns>
    /// <remarks>
    /// Rather than the expected payment amount,  
    /// the cashflow node computes the realized payment amount.
    /// </remarks>
    public override ICashflowNode ToCashflowNode(double notional, 
      DiscountCurve discountCurve, Func<Dt, double> survivalFunction)
    {
      if (EffectiveRateOverride.HasValue)
        return base.ToCashflowNode(notional, discountCurve, survivalFunction);
      return new FraCashflowNode(this, notional, discountCurve, survivalFunction);
    }
    #endregion

    #region Properties
    /// <summary>
    /// Strike rate of the FRA
    /// </summary>
    private double Strike
    {
      get;
      set;
    }

    /// <summary>
    /// Adjust to account for payment at beginning of forward period
    /// </summary>
    public double AdjustmentFactor
    {
      get
      {
        return (1.0 + AccrualFactor * (EffectiveRate + Strike));
      }
    }

    /// <summary>
    /// Effective Fra rate versus contract rate spread for the period
    /// </summary>
    public override double EffectiveRate
    {
      get
      {
        if (EffectiveRateOverride.HasValue)
          return EffectiveRateOverride.Value;
        return (IndexFixing - Strike);
      }
      set
      {
        EffectiveRateOverride = value;
      }
    }

    /// <summary>
    /// Effective Fra rate for the period
    /// </summary>
    public override double IndexFixing
    {
      get
      {
        RateResetState state;
        double f, ca, ov;
        Process(PayDt, CompoundingPeriods, CompoundingConvention, RateProjector, null, 0.0,
                SpreadType.Additive, null, null, 1.0, UseDiscountRateForCompounding, out f, out ca,
                out ov, out state);
        return f;
      }
    }

    /// <summary>
    /// Payment Amount
    /// </summary>
    protected override double ComputeAmount()
    {
      return base.ComputeAmount() / AdjustmentFactor;
    }

    /// <summary>
    /// Get an evaluable expression for amount calculation
    /// </summary>
    /// <returns>Evaluable</returns>
    public override Evaluable GetEvaluableAmount()
    {
      if (AmountOverride.HasValue || !IsProjected)
        return Evaluable.Constant(Amount);

      var fwd = RateProjector as ForwardRateCalculator;
      if (fwd != null)
      {
        var rateByTime = FloatingInterestEvaluation.CalculateAccrual(
          this, UseDiscountRateForCompounding, fwd, IndexMultiplier);
        return Notional*(rateByTime - Strike*AccrualFactor)/(1 + rateByTime);
      }
      return base.GetEvaluableAmount();
    }


    #endregion

    #region FraCashflowNode
    /// <summary>
    /// FraCashflowNode
    /// </summary>
    [Serializable]
    private class FraCashflowNode : CouponCashflowNode
    {
      #region Properties
      /// <summary>
      /// Payment amount
      /// </summary>
      protected override double Amount
      {
        get
        {
          double f, cv, ov;
          RateResetState state;
          Process(PayDt, CompoundingPeriods, CompoundingConvention, RateProjector, null, 0.0,
                  SpreadType.Additive, null, null, 1.0, UseDiscountRateForCompounding, out f, out cv, out ov,
                  out state);
          return (f - Strike) * AccrualFactor / (1.0 + AccrualFactor * f);

        }
      }
      /// <summary>
      /// Accrual factor
      /// </summary>
      private double AccrualFactor { get; set; }
      /// <summary>
      /// Projector
      /// </summary>
      private IRateProjector RateProjector { get; set; }
      /// <summary>
      /// Strike
      /// </summary>
      private double Strike { get; set; }
      /// <summary>
      /// Compounding periods
      /// </summary>
      private List<CompoundingPeriod> CompoundingPeriods { get; set; }
      /// <summary>
      /// Compounding convention
      /// </summary>
      private CompoundingConvention CompoundingConvention { get; set; }
      /// <summary>
      /// Discounting flag
      /// </summary>
      private bool UseDiscountRateForCompounding { get; set; }
      #endregion

      #region Constructor
      /// <summary>
      /// Constructor 
      /// </summary>
      /// <param name="payment">Payment</param>
      /// <param name="discountCurve">Discount curve</param>
      /// <param name="notional">Discount curve</param>
      /// <param name="survivalFunction">Surviving principal</param>
      internal FraCashflowNode(FraInterestPayment payment, double notional, DiscountCurve discountCurve, Func<Dt, double> survivalFunction)
        : base(payment, notional, discountCurve, survivalFunction)
      {
        ResetDt = payment.ResetDate;
        AccrualFactor = payment.AccrualFactor * payment.Notional;
        RateProjector = payment.RateProjector;
        Strike = payment.Strike;
        CompoundingPeriods = payment.CompoundingPeriods;
        CompoundingConvention = payment.CompoundingConvention;
        UseDiscountRateForCompounding = payment.UseDiscountRateForCompounding;
      }
      #endregion

    }
    #endregion
  }


  #endregion
}
