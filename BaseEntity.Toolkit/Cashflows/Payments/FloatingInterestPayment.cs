/*
 * FloatingInterestPayment.cs
*/


using System;
using System.Linq;
using System.Collections.Generic;
using System.Data;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows.Expressions;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;
using CompoundingPeriod =
  System.Tuple<BaseEntity.Toolkit.Base.Dt, BaseEntity.Toolkit.Base.Dt, BaseEntity.Toolkit.Cashflows.FixingSchedule>;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Floating interest payment
  /// </summary>
  public interface IFloatingPayment
  {
    /// <summary>
    /// 
    /// </summary>
    Dt ResetDate { get; }

    /// <summary>
    /// 
    /// </summary>
    double EffectiveRate { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    IRateProjector RateProjector { get; }

  }

  /// <summary>
  /// Floating interest payment 
  /// </summary>
  [Serializable]
  public class FloatingInterestPayment : InterestPayment
    , IFloatingPayment, IHasForwardAdjustment
  {
    #region Constructors

    /// <summary>
    /// Floating rate payment
    /// </summary>
    /// <param name="prevPayDt">Previous Payment Date or Accrual Start</param>
    /// <param name="payDt">Payment Date</param>
    /// <param name="ccy">Currency of payment</param>
    /// <param name="cycleStart">Start of payment cycle</param>
    /// <param name="cycleEnd">End of payment cycle</param>
    /// <param name="periodStart">Start of accrual period</param>
    /// <param name="periodEnd">End of accrual period</param>
    /// <param name="exDivDt">Ex dividend date</param>
    /// <param name="notional">notional for this payment</param>
    /// <param name="spread">fixed spread for this payment over the floating rate</param>
    /// <param name="dc">Day Count convention for accrual factor</param>
    /// <param name="compoundingFreq">Compounding frequency</param>
    /// <param name="compoundingConvention">Compounding convention</param>
    /// <param name="rateProjector">Engine for projection of forward fixings</param>
    /// <param name="forwardAdjustment">Engine for calculation of convexity adjustments</param>
    /// <param name="indexMultiplier">Rate multiplier for rate</param>
    public FloatingInterestPayment(Dt prevPayDt, Dt payDt, Currency ccy, Dt cycleStart, Dt cycleEnd, Dt periodStart,
      Dt periodEnd, Dt exDivDt, double notional, double spread, DayCount dc, Frequency compoundingFreq,
      CompoundingConvention compoundingConvention, IRateProjector rateProjector,
      IForwardAdjustment forwardAdjustment, double indexMultiplier = 1.0)
      : this(prevPayDt, payDt, ccy, cycleStart, cycleEnd, periodStart, periodEnd,
      exDivDt, notional, spread, dc, compoundingFreq, compoundingConvention,
      rateProjector, forwardAdjustment, CycleRule.None, indexMultiplier)
    {
    }

    /// <summary>
    /// Floating rate payment
    /// </summary>
    /// <param name="prevPayDt">Previous Payment Date or Accrual Start</param>
    /// <param name="payDt">Payment Date</param>
    /// <param name="ccy">Currency of payment</param>
    /// <param name="cycleStart">Start of payment cycle</param>
    /// <param name="cycleEnd">End of payment cycle</param>
    /// <param name="periodStart">Start of accrual period</param>
    /// <param name="periodEnd">End of accrual period</param>
    /// <param name="exDivDt">Ex dividend date</param>
    /// <param name="notional">notional for this payment</param>
    /// <param name="spread">fixed spread for this payment over the floating rate</param>
    /// <param name="dc">Day Count convention for accrual factor</param>
    /// <param name="compoundingFreq">Compounding frequency</param>
    /// <param name="compoundingConvention">Compounding convention</param>
    /// <param name="rateProjector">Engine for projection of forward fixings</param>
    /// <param name="forwardAdjustment">Engine for calculation of convexity adjustments</param>
    /// <param name="cycleRule">Compounding cycle rule</param>
    /// <param name="indexMultiplier">Rate multiplier</param>
    public FloatingInterestPayment(Dt prevPayDt, Dt payDt, Currency ccy, Dt cycleStart, Dt cycleEnd, Dt periodStart,
      Dt periodEnd, Dt exDivDt, double notional, double spread, DayCount dc, Frequency compoundingFreq,
      CompoundingConvention compoundingConvention, IRateProjector rateProjector,
      IForwardAdjustment forwardAdjustment, CycleRule cycleRule, double indexMultiplier)
      : base(prevPayDt, payDt, ccy, cycleStart, cycleEnd, periodStart, periodEnd, 
      exDivDt, notional, dc, compoundingFreq, cycleRule)
    {
      FixedCoupon = spread;
      CompoundingConvention = compoundingConvention;
      RateProjector = rateProjector;
      ForwardAdjustment = forwardAdjustment;
      CompoundingPeriods = GenerateCompoundingPeriods(prevPayDt, periodStart,
        periodEnd, payDt,(CouponCalculator) RateProjector, CompoundingFrequency, CompoundingConvention, cycleRule);
      if (CompoundingPeriods.Count > 1 && ToolkitConfigurator.Settings.
        InterestCouponCalculator.ImplicitUseDiscountRateForCompounding)
      {
        UseDiscountRateForCompounding = !ReferenceIsDiscount() &&
          ((CouponCalculator) RateProjector).DiscountCurve != null;
      }
      IndexMultiplier = indexMultiplier;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Coupon type
    /// </summary>
    protected virtual string CouponLabel
    {
      get { return "Spread"; }
    }

    /// <summary>
    /// Fixing type (rate or price)
    /// </summary>
    protected virtual string IndexFixingLabel
    {
      get { return "Index Rate"; }
    }

    /// <summary>
    /// Fixing type (rate or price)
    /// </summary>
    protected virtual string IndexMultiplierLabel
    {
      get { return "Index Multiplier"; }
    }

    /// <summary>
    /// Forward adjustment type (rate or price)
    /// </summary>
    protected virtual string ConvexityAdjLabel
    {
      get { return "Forward Rate Adj"; }
    }

    /// <summary>
    /// Forward adjustment type (rate or price)
    /// </summary>
    protected virtual string ResetDateLabel
    {
      get { return "Reset Date"; }
    }

    /// <summary>
    /// Forward adjustment type (rate or price)
    /// </summary>
    protected virtual string IsProjectedLabel
    {
      get { return "Is Projected"; }
    }

    /// <summary>
    /// Forward adjustment type (rate or price)
    /// </summary>
    protected virtual string AmountLabel
    {
      get { return "Amount"; }
    }

    /// <summary>
    /// Override the effective rate
    /// </summary>
    public double? EffectiveRateOverride { get; set; }

    /// <summary>
    /// True if the rate is a projected future rate
    /// </summary>
    public override bool IsProjected
    {
      get
      {
        RateResetState state = RateResetState;
        return (state == RateResetState.IsProjected);
      }
    }

    /// <summary>
    /// Convexity adjustment 
    /// </summary>
    public virtual double ConvexityAdjustment
    {
      get { return GetConvexityAdjustment(); }
    }

    /// <summary>
    /// Reference rate fixing for this payment
    /// </summary>
    public virtual double IndexFixing
    {
      get { return GetIndexFixing(); }
    }

    /// <summary>
    /// Rate reset state
    /// </summary>
    public virtual RateResetState RateResetState
    {
      get
      {
        if (EffectiveRateOverride.HasValue)
          return RateResetState.ResetFound;

        var resetInfos = GetRateResetComponents();
        bool missing = false;
        bool projected = false;
        if (resetInfos == null || resetInfos.Count == 0)
          return RateResetState.Missing;

        foreach (RateResets.ResetInfo resetInfo in resetInfos)
        {
          missing |= (resetInfo.State == RateResetState.Missing);
          projected |= (resetInfo.State == RateResetState.IsProjected);
        }

        return missing
                 ? RateResetState.Missing
                 : projected ? RateResetState.IsProjected : RateResetState.ObservationFound;
      }

    }

    /// <summary>
    /// True if coupon is a multiplicative factor of the fixing (this is the case for some CIPS issues) 
    /// </summary>
    public SpreadType SpreadType { get; set; }

    /// <summary>
    /// Compounding convention   
    /// </summary>
    public CompoundingConvention CompoundingConvention { get; set; }

    /// <summary>
    /// Cap for the floating rate 
    /// </summary>
    public double? Cap { get; set; }

    /// <summary>
    /// Floor for the floating rate
    /// </summary>
    public double? Floor { get; set; }

    /// <summary>
    /// Rate multiplier for the floating rate
    /// </summary>
    public double IndexMultiplier
    {
      get { return _indexMultiplier ?? 1.0; }
      set { _indexMultiplier = value; }
    }

    /// <summary>
    /// Effective Interest Rate for the period
    /// </summary>
    public override double EffectiveRate
    {
      get { return GetEffectiveRate(); }
      set { SetEffectiveRate(value); }
    }

    /// <summary>
    /// Computes the reset date for the forward: for rates that are a composition of several projections, 
    /// it returns the last reset date needed to calculate the fixing 
    /// </summary>
    /// <returns>Reset date</returns>
    public Dt ResetDate
    {
      get { return CompoundingPeriods.Last().Item3.ResetDate; }
      set
      {
        // Override computed Reset Date (only allowed in some cases)
        if (!(RateProjector is ForwardRateCalculator || RateProjector is SwapRateCalculator))
          throw new ToolkitException("Overriding the Reset Date is not supported for this type of rate projection.");
        if (CompoundingPeriods != null && CompoundingPeriods.Count > 0)
        {
          CompoundingPeriods.Last().Item3.ResetDate = value;
        }
      }
    }

    /// <summary>
    /// Set/Get AsOf date of ForwardAdjustment 
    /// </summary>
    public override Dt VolatilityStartDt
    {
      get
      {
        if (ForwardAdjustment == null)
          return base.VolatilityStartDt;
        return ((ForwardAdjustment) ForwardAdjustment).AsOf;
      }
      set
      {
        if (ForwardAdjustment == null)
          return;
        ((ForwardAdjustment) ForwardAdjustment).AsOf = value;
      }
    }

    /// <summary>
    /// Processor for projection of floating rate
    /// </summary>
    public IRateProjector RateProjector { get; set; }

    /// <summary>
    /// Processor for projection of floating rate
    /// </summary>
    public IForwardAdjustment ForwardAdjustment { get; set; }

    /// <summary>
    /// Schedules for projection of floating rate
    /// </summary>
    public List<CompoundingPeriod> CompoundingPeriods { get; private set; }

    /// <summary>
    /// </summary>
    public bool UseDiscountRateForCompounding { get; private set; }

    IForwardAdjustment IHasForwardAdjustment.ForwardAdjustment
    {
      get { return ForwardAdjustment; }
      set { ForwardAdjustment = value; }
    }

    #endregion

    #region Data

    [Mutable] private double? _indexMultiplier;

    #endregion Data

    #region Methods

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
        return Notional * FloatingInterestEvaluation.CalculateAccrual(
          this, UseDiscountRateForCompounding, fwd, IndexMultiplier);
      }

      return base.GetEvaluableAmount();
    }

    /// <summary>
    /// Convert a payment to a cashflow node used to simulate the 
    /// realized payment amount 
    /// </summary>
    /// <param name="notional">Notional</param>
    /// <param name="discountCurve">Discount curve to discount payment</param>
    /// <param name="survivalFunction">Surviving notional</param>
    /// <returns>ICashflowNode</returns>
    /// <remarks>
    /// Rather than the expected payment amount,  
    /// the cashflow node computes the realized payment amount.
    /// </remarks>
    public override ICashflowNode ToCashflowNode(double notional, DiscountCurve discountCurve, Func<Dt,double> survivalFunction)
    {
      if (EffectiveRateOverride.HasValue)
        return base.ToCashflowNode(notional, discountCurve, survivalFunction);
      return new FloatingCouponCashflowNode(this, notional, discountCurve, survivalFunction);
    }

    private bool ReferenceIsDiscount()
    {
      var cc = RateProjector as CouponCalculator;
      if (cc != null)
        return (cc.DiscountCurve == cc.ReferenceCurve);
      return true;
    }

    /// <summary>
    /// Gets reset info for the period
    /// </summary>
    ///<returns>Return a list of reset information (reset date, fixing value, reset state). 
    /// If the state is projected or missing the fixing value is zero by default 
    /// (only found past or overridden fixings are displayed)</returns>
    public List<RateResets.ResetInfo> GetRateResetComponents()
    {
      var resetInfos = new List<RateResets.ResetInfo>();
      if (EffectiveRateOverride.HasValue)
      {
        resetInfos.Add(new RateResets.ResetInfo(ResetDate, EffectiveRateOverride.Value, RateResetState.ResetFound));
        return resetInfos;
      }
      if (RateProjector == null)
        return null;
      return CompoundingPeriods.SelectMany(cp => RateProjector.GetResetInfo(cp.Item3)).ToList();
    }

    /// <summary>
    /// Gets reset info for the period
    /// </summary>
    ///<returns>Return a list of reset information (reset date, fixing value, reset state). 
    /// If the state is projected or missing the fixing value is zero by default 
    /// (only found past or overridden fixings are displayed)</returns>
    public RateResets.ResetInfo GetRateResetInfo()
    {
      if (EffectiveRateOverride.HasValue)
      {
        return new RateResets.ResetInfo(ResetDate, EffectiveRateOverride.Value, RateResetState.ResetFound);
      }
      if (RateProjector == null)
        return null;
      var resetInfos = CompoundingPeriods.SelectMany(cp => RateProjector.GetResetInfo(cp.Item3)).ToList();
      return new RateResets.ResetInfo(ResetDate, EffectiveRate, RateResetState) {ResetInfos = resetInfos};
    }

    /// <summary>
    /// Add Data Columns
    /// </summary>
    /// <param name="collection">DataColumns</param>
    public override void AddDataColumns(DataColumnCollection collection)
    {
      base.AddDataColumns(collection);
      if (!collection.Contains(ResetDateLabel))
        collection.Add(new DataColumn(ResetDateLabel, typeof (string)));
      if (!collection.Contains(CouponLabel))
        collection.Add(new DataColumn(CouponLabel, typeof (double)));
      if (!collection.Contains(IndexFixingLabel))
        collection.Add(new DataColumn(IndexFixingLabel, typeof (double)));
      if (!IndexMultiplier.AlmostEquals(1.0) && !collection.Contains(IndexMultiplierLabel))
        collection.Add(new DataColumn(IndexMultiplierLabel, typeof(double)));
      if (!collection.Contains(IsProjectedLabel))
        collection.Add(new DataColumn(IsProjectedLabel, typeof (bool)));
      if (!collection.Contains(ConvexityAdjLabel))
        collection.Add(new DataColumn(ConvexityAdjLabel, typeof (double)));
      if (!collection.Contains(AmountLabel))
        collection.Add(new DataColumn(AmountLabel, typeof (double)));
    }

    /// <summary>
    /// Create Data Values
    /// </summary>
    /// <param name="row">row to add values</param>
    /// <param name="dtFormat">Date format</param>
    public override void AddDataValues(DataRow row, string dtFormat)
    {
      base.AddDataValues(row, dtFormat);
      row[AmountLabel] = Amount;
      row[ResetDateLabel] = ResetDate.ToStr(dtFormat);
      row[CouponLabel] = FixedCoupon;
      row[IndexFixingLabel] = IndexFixing;

      if (!IndexMultiplier.AlmostEquals(1.0))
        row[IndexMultiplierLabel] = IndexMultiplier;
      row[IsProjectedLabel] = IsProjected;
      row[ConvexityAdjLabel] = ConvexityAdjustment;
    }

    #region Properties Getters and Setters

    private double GetConvexityAdjustment()
    {
      if (EffectiveRateOverride.HasValue || ForwardAdjustment == null)
        return 0.0;
      RateResetState state;
      double f, ca, ov;
      Process(PayDt, CompoundingPeriods, CompoundingConvention, RateProjector, 
        ForwardAdjustment, FixedCoupon, SpreadType, null, null, IndexMultiplier, 
        UseDiscountRateForCompounding, ApproximateDailyCompoundingEnabled,
        out f, out ca, out ov, out state);
      return ca;
    }

    private double GetIndexFixing()
    {
      if (EffectiveRateOverride.HasValue)
      {
        return EffectiveRateOverride.Value - FixedCoupon;
      }
      RateResetState state;
      double f, ca, ov;
      Process(PayDt, CompoundingPeriods, CompoundingConvention, RateProjector, null, 0.0,
        SpreadType.Additive, null, null, IndexMultiplier, 
        UseDiscountRateForCompounding, 
        ApproximateDailyCompoundingEnabled,
        out f, out ca, out ov, out state);
      return f;
    }

    private double GetEffectiveRate()
    {
      if (EffectiveRateOverride.HasValue)
        return EffectiveRateOverride.Value;
      double f, ca, ov;
      try
      {
        RateResetState state;
        Process(PayDt, CompoundingPeriods, CompoundingConvention, RateProjector, ForwardAdjustment,
          FixedCoupon, SpreadType, Cap, Floor, IndexMultiplier, 
          UseDiscountRateForCompounding, 
          ApproximateDailyCompoundingEnabled,
          out f, out ca, out ov, out state);
      }
      catch (Exception ex)
      {
        if (ex is MissingFixingException)
          throw; // This type of exception is self-explanatory.
        throw new ToolkitException(String.Format(
          "Error computing Effective Rate for the date {0} : {1}",
          PayDt, ex.Message), ex);
      }
      return f + ca + ov;
    }

    private void SetEffectiveRate(double? effectiveRate)
    {
      EffectiveRateOverride = effectiveRate;
    }

    #endregion Properties Getters and Setters

    #endregion

    #region Utilities

    /// <summary>
    /// </summary>
    protected static void Process(Dt payDt, List<CompoundingPeriod> compoundingPeriods,
      CompoundingConvention compoundingConvention, IRateProjector rateProjector,
      IForwardAdjustment forwardAdjustment, double coupon, SpreadType spreadType,
      double? cap, double? floor, double indexMultiplier, bool useDiscountRate,
      out double f, out double ca, out double ov, out RateResetState state)
    {
      Process(payDt, compoundingPeriods, compoundingConvention, rateProjector, forwardAdjustment,
        coupon, spreadType, cap, floor, indexMultiplier, useDiscountRate, 
        false, out f, out ca, out ov, out state);
    }

    /// <summary>
    /// </summary>
    private static void Process(Dt payDt, List<CompoundingPeriod> compoundingPeriods,
      CompoundingConvention compoundingConvention, IRateProjector rateProjector,
      IForwardAdjustment forwardAdjustment, double coupon, SpreadType spreadType,
      double? cap, double? floor, double indexMultiplier, bool useDiscountRate, 
      bool approxDailyCompound, out double f, out double ca, 
      out double ov, out RateResetState state)
    {
      var couponCalculator = rateProjector as CouponCalculator;
      bool multiplicative = (spreadType == SpreadType.Multiplicative && !coupon.AlmostEquals(0.0));
      if (compoundingPeriods.Count > 1) //optionality not supported for compounded payments
      {
        switch (compoundingConvention)
        {
          case CompoundingConvention.ISDA:
          case CompoundingConvention.FlatISDA:
            if (approxDailyCompound)
              ApproximateCompound(payDt, compoundingPeriods, multiplicative ? 0.0 : coupon,
                indexMultiplier, couponCalculator, forwardAdjustment, useDiscountRate,
                compoundingConvention == CompoundingConvention.FlatISDA,
                out state, out f, out ca);
            else
              Compound(payDt, compoundingPeriods, multiplicative ? 0.0 : coupon, indexMultiplier,
                couponCalculator, forwardAdjustment, useDiscountRate,
                compoundingConvention == CompoundingConvention.FlatISDA, true,
                out state, out f, out ca);
            break;
          case CompoundingConvention.Simple:
            Compound(payDt, compoundingPeriods, multiplicative ? 0.0 : coupon, indexMultiplier,
              couponCalculator, forwardAdjustment, useDiscountRate, false, false,
              out state, out f, out ca);

            break;
          default:
            throw new ArgumentException("Compounding convention not supported");
        }
        if (multiplicative && !coupon.AlmostEquals(0.0))
        {
          f *= coupon;
          ca *= coupon;
        }
        ov = 0.0;
        return;
      }
      FixingSchedule schedule = compoundingPeriods[0].Item3;
      Fixing fixing = rateProjector.Fixing(schedule);
      state = fixing.RateResetState;
      if (state == RateResetState.IsProjected)
      {
        CalcSimple(payDt, fixing, coupon, multiplicative, forwardAdjustment,
          compoundingPeriods[0].Item3, floor, cap, indexMultiplier, out f, out ca, out ov);
        return;
      }
      f = multiplicative ? coupon*fixing.Forward*indexMultiplier : fixing.Forward*indexMultiplier + coupon;
      if (cap.HasValue && f > cap.Value)
        f = cap.Value;
      if (floor.HasValue && f < floor.Value)
        f = floor.Value;
      ca = ov = 0.0;
    }

    private static void CalcSimple(Dt payDt, Fixing fixing, double coupon, bool multiplicative, 
      IForwardAdjustment forwardAdjustment, FixingSchedule sched, double? floor,
      double? cap, double indexMultiplier, out double f, out double ca, out double ov)
    {
      f = multiplicative ? coupon * indexMultiplier * fixing.Forward : fixing.Forward * indexMultiplier + coupon;
      ca = ov = 0.0;
      bool nonZeroIndexMultiplier = !indexMultiplier.ApproximatelyEqualsTo(0.0);
      if (forwardAdjustment == null)
        return;
      if (multiplicative)
      {
        coupon = (!coupon.AlmostEquals(0.0)) ? coupon : 1.0;
        double mu = forwardAdjustment.ConvexityAdjustment(payDt, sched, fixing);
        ca = coupon * mu * indexMultiplier;
        if (floor.HasValue && nonZeroIndexMultiplier)
          ov += coupon * indexMultiplier * forwardAdjustment.FloorValue(sched, 
            fixing, floor.Value / coupon / indexMultiplier, 0.0, mu);
        if (cap.HasValue && nonZeroIndexMultiplier)
          ov += coupon * indexMultiplier * forwardAdjustment.CapValue(sched, 
            fixing, cap.Value / coupon / indexMultiplier, 0.0, mu);
        return;
      }
      ca = forwardAdjustment.ConvexityAdjustment(payDt, sched, fixing);
      if (floor.HasValue && nonZeroIndexMultiplier)
        ov += indexMultiplier * forwardAdjustment.FloorValue(sched, 
          fixing, floor.Value / indexMultiplier, coupon / indexMultiplier, ca);
      if (cap.HasValue && nonZeroIndexMultiplier)
        ov += indexMultiplier * forwardAdjustment.CapValue(sched, 
          fixing, cap.Value / indexMultiplier, coupon / indexMultiplier, ca);
      ca *= indexMultiplier;
    }


    private static List<CompoundingPeriod> GenerateCompoundingPeriods(Dt prevPayDt,
      Dt periodStart, Dt periodEnd, Dt payDt, CouponCalculator couponCalculator,
      Frequency compoundingFreq, CompoundingConvention compoundingConvention, CycleRule cycleRule)
    {
      if (compoundingConvention != CompoundingConvention.Simple &&
        ToolkitConfigurator.Settings.InterestCouponCalculator.NoCycleRuleForCompounding)
      {
        cycleRule = CycleRule.None;
      }
      var retVal = new List<CompoundingPeriod>();
      if (compoundingConvention == CompoundingConvention.None || compoundingFreq == Frequency.None)
      {
        retVal.Add(new CompoundingPeriod(periodStart, periodEnd,
          couponCalculator.GetFixingSchedule(prevPayDt, periodStart, periodEnd, payDt)));
        return retVal;
      }
      var end = periodStart;
      for (int i = 0; i < int.MaxValue; ++i)
      {
        var start = end;
        if (compoundingFreq == Frequency.Daily)
        {
          end = Dt.AddDays(start, 1, couponCalculator.ReferenceIndex.Calendar);
        }
        else
        {
          end = Dt.Roll(Dt.Add(start, compoundingFreq, 1, cycleRule),
            couponCalculator.ReferenceIndex.Roll, couponCalculator.ReferenceIndex.Calendar);
        }
        if (end >= periodEnd)
        {
          if (i > 0 && IgnorePeriod(start, periodEnd, compoundingFreq, couponCalculator.ReferenceIndex.Calendar))
            break;
          retVal.Add(new CompoundingPeriod(start, periodEnd,
            couponCalculator.GetFixingSchedule(prevPayDt, start, periodEnd, payDt)));
          break;
        }
        retVal.Add(new CompoundingPeriod(start, end,
          couponCalculator.GetFixingSchedule(prevPayDt, start, end, payDt)));
      }
      return retVal;
    }

    private static bool IgnorePeriod(Dt begin, Dt end, Frequency freq, Calendar cal)
    {
      var diff = Dt.BusinessDays(begin, end, cal);
      switch (freq)
      {
        case Frequency.None:
        case Frequency.Daily:
          return false;
        case Frequency.Weekly:
        case Frequency.BiWeekly:
          return diff < 2;
        default:
          return diff < 3;
      }
    }

    private static void Compound(Dt payDt, IList<CompoundingPeriod> fixingSchedule, 
      double coupon, double indexMultiplier,CouponCalculator couponCalculator, 
      IForwardAdjustment forwardAdjustment, bool useDiscountRate, bool flat, bool isCompound, 
      out RateResetState state, out double cmpnFwd, out double cvxyAdj)
    {
      cmpnFwd = 0.0;
      cvxyAdj = 0.0;
      state = RateResetState.None;
      double periodFrac = Dt.Fraction(fixingSchedule[0].Item1, fixingSchedule[fixingSchedule.Count - 1].Item2,
        couponCalculator.ReferenceIndex.DayCount);
      if (periodFrac <= 0.0)
        return;
      bool withCvxyAdj = forwardAdjustment != null;
      double ca = 0.0, cmpnExpectedFwd = 0.0;
      bool projected = false, missing = false;

      foreach (var sched in fixingSchedule)
      {
        var fixing = couponCalculator.Fixing(sched.Item3);
        if (withCvxyAdj && fixing.RateResetState == RateResetState.IsProjected)
          ca = forwardAdjustment.ConvexityAdjustment(payDt, sched.Item3, fixing);
        projected |= (fixing.RateResetState == RateResetState.IsProjected);
        missing |= (fixing.RateResetState == RateResetState.Missing);
        double frac = Dt.Fraction(sched.Item1, sched.Item2, couponCalculator.ReferenceIndex.DayCount);
        double cmpnRate = useDiscountRate
                            ? fixing.RateResetState == RateResetState.IsProjected
                                ? couponCalculator.DiscountCurve.F(sched.Item1, sched.Item2,
                                                                   couponCalculator.ReferenceIndex.DayCount,
                                                                   Frequency.None)
                                : fixing.Forward
                            : fixing.Forward;
        cmpnFwd = (isCompound) ? cmpnFwd + (fixing.Forward * indexMultiplier + coupon) * frac + 
          cmpnFwd * (cmpnRate * indexMultiplier + (flat ? 0.0 : coupon)) * frac
          : cmpnFwd + (fixing.Forward * indexMultiplier + coupon) * frac;
        if (withCvxyAdj)
          cmpnExpectedFwd = (isCompound) ? cmpnExpectedFwd + ((fixing.Forward + ca) * indexMultiplier + coupon) * frac +
                             cmpnExpectedFwd * ((cmpnRate + ca) * indexMultiplier + (flat ? 0.0 : coupon)) * frac
                             : cmpnExpectedFwd + ((fixing.Forward + ca) * indexMultiplier + coupon) * frac;
      }
      state = missing
                ? RateResetState.Missing
                : projected
                    ? RateResetState.IsProjected
                    : RateResetState.ObservationFound;
      cmpnFwd /= periodFrac;
      if (!withCvxyAdj || (state != RateResetState.IsProjected))
        return;
      cmpnExpectedFwd /= periodFrac;
      cvxyAdj = cmpnExpectedFwd - cmpnFwd;
    }

    #region Approximate daily compounding rates

    public bool IsCalibrating { get; set; }

    public bool ApproximateDailyCompoundingEnabled
    {
      get
      {
        if (!IsCalibrating
          || CompoundingConvention == CompoundingConvention.None
          || CompoundingFrequency != Frequency.Daily)
        {
          return false;
        }
        var gc = RateProjector as GeometricAvgRateCalculator;
        return gc != null && gc.Approximate;
      }
    }

    private static void ApproximateCompound(
      Dt payDt, IList<CompoundingPeriod> fixingSchedule, double coupon, double indexMultiplier,
      CouponCalculator couponCalculator, IForwardAdjustment forwardAdjustment,
      bool useDiscountRate, bool flat,
      out RateResetState state, out double cmpnFwd, out double cvxyAdj)
    {
      cmpnFwd = 0.0;
      cvxyAdj = 0.0;
      state = RateResetState.None;
      var dayCount = couponCalculator.ReferenceIndex.DayCount;
      int count = fixingSchedule.Count;
      var schedLast = fixingSchedule[count - 1];
      var schedFirst = fixingSchedule[0];

      // We do approximation only when all rates are projected.
      if (schedFirst.Item3.ResetDate < couponCalculator.AsOf)
      {
        Compound(payDt, fixingSchedule, coupon, indexMultiplier, couponCalculator,
          forwardAdjustment, useDiscountRate, flat, true,
          out state, out cmpnFwd, out cvxyAdj);
        return;
      }

      // Calculate the compound forward rate.
      Dt begin = schedFirst.Item1, end = schedLast.Item2;
      double periodFrac = Dt.Fraction(begin, end, dayCount);
      if (periodFrac <= 0.0)
        return;
      double days = end - begin;
      var df = couponCalculator.DiscountCurve.DiscountFactor(begin, end);
      if (indexMultiplier.ApproximatelyEqualsTo(1.0) && (flat || Math.Abs(coupon) < 1E-16))
      {
        cmpnFwd = (1 / df - 1) / periodFrac + coupon;
      }
      else
      {
        // The daily rate plus daily coupon (spread)
        var c = (Math.Pow(df, -1 / days) - 1) * indexMultiplier + coupon * periodFrac / days;
        cmpnFwd = Compound(c, days) / periodFrac;
      }

      // Calculate the compound convexity adjustment.
      if (forwardAdjustment != null)
      {
        // Use the begin and end convex adjustments as approximate
        var ca0 = forwardAdjustment.ConvexityAdjustment(payDt,
          schedFirst.Item3, couponCalculator.Fixing(schedFirst.Item3));
        var ca1 = forwardAdjustment.ConvexityAdjustment(payDt,
          schedLast.Item3, couponCalculator.Fixing(schedLast.Item3));
        var ca = (ca0 + ca1) / 2;
        // Calculate the compounded convexity adjustment.
        if (ca > 0.0)
        {
          var c = (Math.Pow(df, -1 / days) - 1) * indexMultiplier +
            ((flat ? 0.0 : coupon) + ca * indexMultiplier) * periodFrac / days;
          cvxyAdj = Compound(c, days) / periodFrac + (flat ? coupon : 0.0)
            - cmpnFwd;
        }
      }
      state = RateResetState.IsProjected;
    }


    // Calculate ((1+x)^n - 1), accurate at least 10^-9
    private static double Compound(double x, double n)
    {
      if (n > 10 && Math.Abs(n * x) < 0.1)
      {
        // Taylor expansion of (1+c)^days - 1
        return n * x * (1 + (n - 1) / 2 * x * (1 + (n - 2) / 3 * x * (
          1 + (n - 3) / 4 * x * (1 + (n - 4) / 5 * x * (
            1 + (n - 5) / 6 * x * (1 + n - 6) / 7 * x * (
              1 + (n - 7) / 8 * x * (1 + (n - 8) / 9 * x)))))));
      }
      return Math.Pow(1 + x, n) - 1;
    }

    #endregion

    #endregion

    #region FloatingCouponCashflowNode
    /// <summary>
    ///  Floating coupon node
    /// </summary>
    [Serializable]
    private class FloatingCouponCashflowNode : CouponCashflowNode
    {
      #region Properties
      /// <summary>
      /// Coupon amount
      /// </summary>
      protected override double Amount
      {
        get
        {
          double f, ca, ov;
          RateResetState state;
          var indexFactor = IndexMultiplier;
          Process(PayDt, CompoundingPeriods, CompoundingConvention, RateProjector, null, Coupon, SpreadType, null,
                  null, indexFactor, UseDiscountRateForCompounding, out f, out ca, out ov, out state);
          if (Cap.HasValue)
            f = Math.Min(f, Cap.Value);
          if (Floor.HasValue)
            f = Math.Max(f, Floor.Value);
          return f * AccrualFactor;
        }
      }

      /// <summary>
      /// Accrual factor
      /// </summary>
      private double AccrualFactor { get; set; }
      /// <summary>
      /// Spread type
      /// </summary>
      private SpreadType SpreadType { get; set; }
      /// <summary>
      /// Coupon
      /// </summary>
      private double Coupon { get; set; }
      /// <summary>
      /// Compounding periods
      /// </summary>
      private List<CompoundingPeriod> CompoundingPeriods { get; set; }
      /// <summary>
      /// Compounding convention
      /// </summary>
      private CompoundingConvention CompoundingConvention { get; set; }
      /// <summary>
      /// Rate projector
      /// </summary>
      private IRateProjector RateProjector { get; set; }
      
      /// <summary>
      /// Cap
      /// </summary>
      private double? Cap { get; set; }
      
      /// <summary>
      /// Floor
      /// </summary>
      private double? Floor { get; set; }

      /// <summary>
      /// Rate multiplier for the floating rate
      /// </summary>
      private double IndexMultiplier
      {
        get { return _indexMultiplier ?? 1.0; }
        set { _indexMultiplier = value; }
      }

      /// <summary>
      /// Compounding flag
      /// </summary>
      private bool UseDiscountRateForCompounding { get; set; }
      #endregion

      #region Data

      [Mutable]private double? _indexMultiplier;

      #endregion Data


      #region Constructor
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="notional">Notional</param>
      /// <param name="payment">Payment</param>
      /// <param name="discountCurve">Discount curve to discount payment</param>
      /// <param name="survivalFunction">Surviving notional</param>

      public FloatingCouponCashflowNode(FloatingInterestPayment payment, double notional, 
        DiscountCurve discountCurve, Func<Dt,double> survivalFunction)
        : base(payment, notional, discountCurve, survivalFunction)
      {
        ResetDt = payment.ResetDate;
        Coupon = payment.FixedCoupon;
        SpreadType = payment.SpreadType;
        CompoundingPeriods = payment.CompoundingPeriods;
        CompoundingConvention = payment.CompoundingConvention;
        RateProjector = payment.RateProjector;
        AccrualFactor = payment.AccrualFactor * payment.Notional;
        UseDiscountRateForCompounding = payment.UseDiscountRateForCompounding;
        Cap = payment.Cap;
        Floor = payment.Floor;
        IndexMultiplier = payment.IndexMultiplier;
      }

      #endregion
    }
    #endregion
  }
}