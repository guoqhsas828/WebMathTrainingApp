using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Observable swap rate used as a reference for constant maturity swaps or treasuries.
  /// </summary>
  [Serializable]
  public class SwapRateCalculator : CouponCalculator
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">AsOf Date</param>
    /// <param name="referenceIndex">Reference index object</param>
    /// <param name="referenceCurve">Reference rate curve used for the calculation of the rate.</param>
    public SwapRateCalculator(Dt asOf, SwapRateIndex referenceIndex, DiscountCurve referenceCurve)
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
    public SwapRateCalculator(Dt asOf, SwapRateIndex referenceIndex, DiscountCurve referenceCurve,
                              DiscountCurve discountCurve) :
                                base(asOf, referenceIndex, referenceCurve, discountCurve)
    {
      EndIsFixingDate = false;
      ForwardRateIndex = referenceIndex.ForwardRateIndex;
      if (ForwardRateIndex == null)
      {
        throw new ToolkitException("Forward rate index is not defined for reference index " + referenceIndex.IndexName);
      }
    }

    #endregion

    #region Methods 

    private static double GetAnnuityPv(Schedule sc, DiscountCurve discount, ReferenceIndex index)
    {
      double retVal = 0.0;
      for (int i = 0; i < sc.Count; ++i)
        retVal += discount.Interpolate(sc.GetPaymentDate(i))*sc.Fraction(i, index.DayCount);
      return retVal;
    }

    private static double GetFloatingLegPv(Schedule sc, DiscountCurve discount, CalibratedCurve referenceCurve,
                                           ReferenceIndex index)
    {
      double retVal = 0.0;
      for (int i = 0; i < sc.Count; ++i)
        retVal += discount.Interpolate(sc.GetPaymentDate(i))*
                  referenceCurve.F(sc.GetPeriodStart(i), sc.GetPeriodEnd(i), index.DayCount, Frequency.None)*
                  sc.Fraction(i, index.DayCount);
      return retVal;
    }

    private double CalculateSwapRate(SwapRateFixingSchedule sched, out RateResetState state, out double annuity)
    {
      double rate = RateResetUtil.FindRate(sched.ResetDate, AsOf, HistoricalObservations, UseAsOfResets, out state);
      annuity = 0.0;
      if ((state == RateResetState.Missing) && ((sched.ResetDate <= AsOf) && (sched.StartDate >= AsOf)))
        state = RateResetState.IsProjected;
      if (state == RateResetState.IsProjected)
      {
        double floatingLegPv = GetFloatingLegPv(sched.FloatingLegSchedule, DiscountCurve ?? (DiscountCurve)ReferenceCurve, ReferenceCurve, ForwardRateIndex);
        annuity = GetAnnuityPv(sched.FixedLegSchedule, DiscountCurve, ReferenceIndex);
        return floatingLegPv / annuity;
      }
      if (state == RateResetState.Missing)
        throw new MissingFixingException(String.Format("Fixing resetting on date {0} is missing.", sched.ResetDate));
      return rate;
    }


    /// <summary>
    /// Fixing on reset 
    /// </summary>
    /// <param name="fixingSchedule">fixing schedule</param>
    /// <returns>Fixing</returns>
    public override Fixing Fixing(FixingSchedule fixingSchedule)
    {
      var sched = fixingSchedule as SwapRateFixingSchedule;
      RateResetState state;
      double annuity;
      double swapRate = CalculateSwapRate(sched, out state, out annuity);
      return new SwapRateFixing {Forward = swapRate, RateResetState = state, Annuity = annuity};
    }

    /// <summary>
    /// Initialize fixing schedule
    /// </summary>
    /// <param name="prevPayDt">Previous payment date</param>
    /// <param name="periodStart">Period start</param>
    /// <param name="periodEnd">Period end</param>
    /// <param name="payDt">Payment date</param>
    /// <returns>Fixing schedule</returns>
    public override FixingSchedule GetFixingSchedule(Dt prevPayDt, Dt periodStart, Dt periodEnd, Dt payDt)
    {
      var fixingSchedule = new SwapRateFixingSchedule();
      var referenceIndex = (SwapRateIndex)ReferenceIndex;
      fixingSchedule.StartDate = EndIsFixingDate ? periodEnd : periodStart;
      fixingSchedule.ResetDate = RateResetUtil.ResetDate(fixingSchedule.StartDate, ReferenceIndex, ResetLag);
      fixingSchedule.FixedLegSchedule = new Schedule(fixingSchedule.StartDate, fixingSchedule.StartDate, Dt.Empty,
                                                     Dt.Empty,
                                                     Dt.Add(fixingSchedule.StartDate, referenceIndex.IndexTenor),
                                                     referenceIndex.IndexFrequency, referenceIndex.Roll,
                                                     referenceIndex.Calendar, CycleRule.None,
                                                     CashflowFlag.RollLastPaymentDate | CashflowFlag.AdjustLast |
                                                     CashflowFlag.RespectLastCoupon | CashflowFlag.StubAtEnd);
      fixingSchedule.FloatingLegSchedule = new Schedule(fixingSchedule.StartDate, fixingSchedule.StartDate, Dt.Empty,
                                                        Dt.Empty,
                                                        Dt.Add(fixingSchedule.StartDate, referenceIndex.IndexTenor),
                                                        ForwardRateIndex.IndexTenor.ToFrequency(),
                                                        ForwardRateIndex.Roll, ForwardRateIndex.Calendar,
                                                        CycleRule.None,
                                                        CashflowFlag.RollLastPaymentDate | CashflowFlag.AdjustLast |
                                                        CashflowFlag.RespectLastCoupon | CashflowFlag.StubAtEnd);
      return fixingSchedule;
    }

    /// <summary>
    /// Rate reset information
    /// </summary>
    /// <param name="schedule">Fixing schedule</param>
    /// <returns> Reset info for each component of the fixing</returns>
    public override List<RateResets.ResetInfo> GetResetInfo(FixingSchedule schedule)
    {
      var resetInfos = new List<RateResets.ResetInfo>();
      var sched = schedule as SwapRateFixingSchedule;
      if (sched == null)
        return resetInfos;
      RateResetState state;
      double rate = RateResetUtil.FindRate(sched.ResetDate, AsOf, HistoricalObservations, UseAsOfResets, out state);
      if ((state == RateResetState.Missing) && ((sched.ResetDate <= AsOf) && (sched.StartDate >= AsOf)))
        state = RateResetState.IsProjected;
      resetInfos.Add(new RateResets.ResetInfo(sched.ResetDate, rate, state));
      return resetInfos;
    }

    #endregion

    #region Properties

    internal InterestRateIndex ForwardRateIndex { get; set; }

    #endregion
  }
}