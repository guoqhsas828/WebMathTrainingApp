using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Coupon calculator for computing forward par-yield
  /// </summary>
  [Serializable]
  public class ForwardYieldCalculator : CouponCalculator
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">AsOf Date</param>
    /// <param name="referenceIndex">Reference index object</param>
    /// <param name="referenceCurve">Reference rate curve used for the calculation of the rate.</param>
    public ForwardYieldCalculator(Dt asOf, ForwardYieldIndex referenceIndex, DiscountCurve referenceCurve)
      : this(asOf, referenceIndex, referenceCurve, referenceCurve)
    {
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">AsOf Date</param>
    /// <param name="referenceIndex">Reference index object</param>
    /// <param name="referenceCurve">Reference rate curve used for the calculation of the rate.</param>
    /// <param name="discountCurve">Curve used for discounting</param>
    public ForwardYieldCalculator(Dt asOf, ForwardYieldIndex referenceIndex, DiscountCurve referenceCurve,
                              DiscountCurve discountCurve) :
                                base(asOf, referenceIndex, referenceCurve, discountCurve)
    {
      EndIsFixingDate = false;
    }

    #region Methods
    /// <summary>
    /// Fixing on reset 
    /// </summary>
    /// <param name="fixingSchedule">fixing schedule</param>
    /// <returns>Fixing</returns>
    public override Fixing Fixing(FixingSchedule fixingSchedule)
    {
      var sched = fixingSchedule as ForwardParYieldFixingSchedule;
      RateResetState state;
      var parYield = CalcForwardYield(sched, out state);
      var retVal = new Fixing { Forward = parYield, RateResetState = state };
      return retVal;
    }

    /// <summary>
    /// Find out the coupon rate that the treasury bond shall pay to get price of being par
    /// </summary>
    /// <param name="sched">Fixing schedule</param>
    /// <param name="state">State</param>
    /// <returns>Forward par yield</returns>
    private double CalcForwardYield(ForwardParYieldFixingSchedule sched, out RateResetState state)
    {
      double rate = RateResetUtil.FindRate(sched.ResetDate, AsOf, HistoricalObservations, UseAsOfResets, out state);
      if ((state == RateResetState.Missing) && ((sched.ResetDate <= AsOf) && (sched.StartDate >= AsOf)))
        state = RateResetState.IsProjected;

      if (state == RateResetState.IsProjected)
      {
        var referenceIndex = ReferenceIndex as ForwardYieldIndex;
        if (referenceIndex == null)
          throw new ToolkitException("ReferenceIndex needs to be ForwardYieldIndex type for projecting forward yield");
        var den = GetAnnuityPv(sched.BondSchedule, (DiscountCurve)ReferenceCurve, referenceIndex.DayCount);
        var num = 1.0 - GetParAmountPv(sched.BondSchedule, (DiscountCurve)ReferenceCurve);
        rate = num/den;
      }
      if (state == RateResetState.Missing)
        throw new MissingFixingException(String.Format("Fixing resetting on date {0} is missing.", sched.ResetDate));

      return rate;
    }

    /// <summary>
    /// Get fixing schedule
    /// </summary>
    /// <param name="prevPayDt"></param>
    /// <param name="periodStart"></param>
    /// <param name="periodEnd"></param>
    /// <param name="payDt"></param>
    /// <returns></returns>
    public override FixingSchedule GetFixingSchedule(Dt prevPayDt, Dt periodStart, Dt periodEnd, Dt payDt)
    {
      var fixingSchedule = new ForwardParYieldFixingSchedule();
      var referenceIndex = (ForwardYieldIndex)ReferenceIndex;
      fixingSchedule.StartDate = EndIsFixingDate ? periodEnd : periodStart;
      fixingSchedule.ResetDate = RateResetUtil.ResetDate(fixingSchedule.StartDate, ReferenceIndex, ResetLag);
      fixingSchedule.BondSchedule = new Schedule(fixingSchedule.StartDate, fixingSchedule.StartDate, Dt.Empty,
                                                     Dt.Empty,
                                                     Dt.Add(fixingSchedule.StartDate, referenceIndex.IndexTenor),
                                                     referenceIndex.IndexFrequency, referenceIndex.Roll,
                                                     referenceIndex.Calendar, CycleRule.None,
                                                     CashflowFlag.RollLastPaymentDate | CashflowFlag.AdjustLast |
                                                     CashflowFlag.RespectLastCoupon | CashflowFlag.StubAtEnd);

      return fixingSchedule;

    }

    /// <summary>
    /// Get reset info
    /// </summary>
    /// <param name="schedule"></param>
    /// <returns></returns>
    public override List<RateResets.ResetInfo> GetResetInfo(FixingSchedule schedule)
    {
      var resetInfos = new List<RateResets.ResetInfo>();
      var sched = schedule as ForwardParYieldFixingSchedule;
      if (sched == null)
      {
        throw new ToolkitException("Cannot cast fixing schedule of type {0} to ForwardParYieldFixingSchedule", schedule?.GetType().Name ?? "<null>");
      }

      RateResetState state;
      double rate = RateResetUtil.FindRate(sched.ResetDate, AsOf, HistoricalObservations, UseAsOfResets, out state);
      if ((state == RateResetState.Missing) && ((sched.ResetDate <= AsOf) && (sched.StartDate >= AsOf)))
        state = RateResetState.IsProjected;
      resetInfos.Add(new RateResets.ResetInfo(sched.ResetDate, rate, state));
      return resetInfos;
    }

    private static double GetAnnuityPv(Schedule sc, DiscountCurve discount, DayCount dc)
    {
      double retVal = 0.0;
      for (int i = 0; i < sc.Count; ++i)
        retVal += discount.Interpolate(sc.AsOf, sc.GetPaymentDate(i)) * sc.Fraction(i, dc);
      return retVal;
    }

    private static double GetParAmountPv(Schedule sc, DiscountCurve discount)
    {
      var retVal = discount.Interpolate(sc.AsOf, sc.GetPaymentDate(sc.Count - 1));
      return retVal;
    }

    #endregion

  }
}
