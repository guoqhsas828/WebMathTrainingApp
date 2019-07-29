using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Config settings class for the interest coupon calculator class
  /// </summary>
  [Serializable]
  public class InterestCouponCalculatorConfig
  {
    /// <exclude />
    [ToolkitConfig(
      "backward compatibility flag , determines whether the end of period or the end of deposit should be used for calcuation of the forward rate"
      )] public readonly bool EndSetByIndexTenor = true;

    /// <exclude />
    [ToolkitConfig(
      "backward compatibility flag, whether to use discount rate for compounding whenever the discount curve is different than the reference curve"
      )]
    public readonly bool ImplicitUseDiscountRateForCompounding = false;

    /// <exclude />
    [ToolkitConfig(
      "backward compatibility flag, whether to use None as the cycle rule for compounding"
      )]
    public readonly bool NoCycleRuleForCompounding = false;
  }

  /// <summary>
  /// Observable rate used as a reference for interest rate swaps (Libor, Ibor etc.)
  /// </summary>
  [Serializable]
  public class ForwardRateCalculator : CouponCalculator
  {
    private ToolkitConfigSettings settings_ => ToolkitConfigurator.Settings;

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">AsOf Date</param>
    /// <param name="referenceIndex">Reference index object</param>
    /// <param name="referenceCurve">Reference rate curve used for the calculation of the rate.</param>
    public ForwardRateCalculator(Dt asOf, InterestRateIndex referenceIndex, DiscountCurve referenceCurve) :
      base(asOf, referenceIndex, referenceCurve)
    {
      EndSetByIndexTenor = settings_.InterestCouponCalculator.EndSetByIndexTenor;
      ResetLag = Tenor.Empty;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Calculate simple forward rate
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="resetDt">Reset date</param>
    /// <param name="start">Start date</param>
    /// <param name="end">End date</param>
    /// <param name="reference">Reference date</param>
    /// <param name="dc">Daycount</param>
    /// <param name="freq">The frequency.</param>
    /// <param name="resets">Historical resets</param>
    /// <param name="useAsOfResets">Use reset at asOf date rather than projecting</param>
    /// <param name="state">State</param>
    /// <returns>Libor rate</returns>
    internal static double CalculateForwardRate(Dt asOf,
                                                Dt resetDt, Dt start, Dt end, CalibratedCurve reference,
                                                DayCount dc, Frequency freq, RateResets resets, bool useAsOfResets,
                                                out RateResetState state)
    {
      return CalculateForwardRate(asOf, resetDt, start, end, reference, dc, freq,
                                  resets, useAsOfResets, CashflowFlag.SimpleProjection, out state);
    }

    private static double CalculateForwardRate(Dt asOf,
                                               Dt resetDt, Dt start, Dt end, CalibratedCurve reference,
                                               DayCount dc, Frequency freq, RateResets resets,
                                               bool useAsOfResets, CashflowFlag flag,
                                               out RateResetState state)
    {
      double rate = RateResetUtil.FindRate(resetDt, asOf, resets, useAsOfResets, out state);
      if (state == RateResetState.IsProjected)
        return CalculateForwardRate(reference, start, end, dc, freq, flag);
      if (state == RateResetState.Missing)
      {
        if (RateResetUtil.ProjectMissingRateReset(resetDt, asOf, start))
        {
          state = RateResetState.IsProjected;
          return CalculateForwardRate(reference, start, end, dc, freq, flag);
        }
      }
      return rate;
    }

    internal static double CalculateForwardRate(CalibratedCurve reference,
                                               Dt start, Dt end, DayCount dc, Frequency freq, CashflowFlag flag)
    {
      if ((flag & CashflowFlag.SimpleProjection) == 0)
      {
        var price = reference.Interpolate(start, end);
        var T = Dt.Fraction(start, end, dc);
        return RateCalc.RateFromPrice(price, T, freq);
      }
      return reference.F(start, end, dc, Frequency.None); //in averaging swaps (both geometric and arithmetic) 
      //the weighting scheme implies always linear compounding over weekends, so this is more appropriate
    }

    /// <summary>
    /// Fixing on reset 
    /// </summary>
    /// <param name="fixingSchedule">fixing schedule</param>
    public override Fixing Fixing(FixingSchedule fixingSchedule)
    {
      var sched = (ForwardRateFixingSchedule)fixingSchedule;
      RateResetState state;
      var retVal = new Fixing
                   {
                     Forward = CalculateForwardRate(AsOf, sched.ResetDate, sched.StartDate, sched.EndDate, ReferenceCurve,
                                                    ReferenceIndex.DayCount, ReferenceIndex.IndexTenor.ToFrequency(), HistoricalObservations, UseAsOfResets,
                                                    CashflowFlag, out state),
                     RateResetState = state
                   };
      if (state == RateResetState.Missing && !RateResetUtil.ProjectMissingRateReset(sched.ResetDate, AsOf, sched.StartDate))
        throw new MissingFixingException(String.Format("Fixing resetting on date {0} is missing.", sched.ResetDate));

      return retVal;
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
      var fixingSchedule = new ForwardRateFixingSchedule {StartDate = EndIsFixingDate ? periodEnd : periodStart};
      fixingSchedule.ResetDate = RateResetUtil.ResetDate(fixingSchedule.StartDate, ReferenceIndex, ResetLag);
      if (EndIsFixingDate || EndSetByIndexTenor)
        fixingSchedule.EndDate =
          Dt.Roll(Dt.Add(fixingSchedule.StartDate, ReferenceIndex.IndexTenor), ReferenceIndex.Roll,
                  ReferenceIndex.Calendar);
      else
        fixingSchedule.EndDate = periodEnd;
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
      var sched = (ForwardRateFixingSchedule)schedule;
      RateResetState state;
      double rate = RateResetUtil.FindRate(sched.ResetDate, AsOf, HistoricalObservations, UseAsOfResets, out state);
      if (state == RateResetState.Missing && RateResetUtil.ProjectMissingRateReset(sched.ResetDate, AsOf, sched.StartDate))
        state = RateResetState.IsProjected;
      resetInfos.Add(new RateResets.ResetInfo(sched.ResetDate, rate, state) {AccrualStart = sched.StartDate, AccrualEnd = sched.EndDate});

      return resetInfos;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Determines whether the end of period is determined by the tenor of the index
    /// </summary>
    public bool EndSetByIndexTenor { get; set; }

    #endregion
  }
}