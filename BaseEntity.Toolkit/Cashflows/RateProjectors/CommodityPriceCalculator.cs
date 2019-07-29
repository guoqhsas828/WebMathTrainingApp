// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Rule for commodity price determination
  /// </summary>
  public enum CommodityPriceObservationRule
  {
    /// <summary>
    /// Observe price on every (business) day in the period
    /// </summary>
    None,

    /// <summary>
    /// Observe price on every (business) day in the period
    /// </summary>
    All,

    /// <summary>
    /// Observe price only on the first n days in the period
    /// </summary>
    First,

    /// <summary>
    /// Observe price only on the last n days in the period
    /// </summary>
    Last
  }

  [Serializable]
  internal class CommodityPriceFixingScheme
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="CommodityPriceFixingScheme" /> class.
    /// </summary>
    /// <param name="commodityPriceObservationRule">The commodity price observation rule.</param>
    /// <param name="numDates">Number of dates to observe on.</param>
    /// <param name="calendar">Business day calendar.</param>
    public CommodityPriceFixingScheme(CommodityPriceObservationRule commodityPriceObservationRule, int numDates, Calendar calendar)
    {
      CommodityPriceObservationRule = commodityPriceObservationRule;
      NumDates = numDates;
      Calendar = calendar;
    }

    public CommodityPriceObservationRule CommodityPriceObservationRule { get; private set; }
    public int NumDates { get; private set; }
    public Calendar Calendar { get; private set; }
  }

  /// <summary>
  /// Commodity price calculator
  /// </summary>
  [Serializable]
  internal class CommodityPriceCalculator : CouponCalculator
  {
    public CommodityPriceCalculator(Dt asOf, CommodityPriceFixingScheme priceFixingScheme, CommodityPriceIndex referenceIndex, CalibratedCurve referenceCurve)
      : base(asOf, referenceIndex, referenceCurve)
    {
      PriceFixingScheme = priceFixingScheme;
    }

    public CommodityPriceFixingScheme PriceFixingScheme { get; private set; }

    #region Overrides of CouponCalculator

    /// <summary>
    /// Fixing on reset
    /// </summary>
    /// <param name="fixingSchedule">fixing schedule</param>
    /// <returns>
    /// Fixing for the period
    /// </returns>
    public override Fixing Fixing(FixingSchedule fixingSchedule)
    {
      var ret = new AveragedRateFixing();
      var sched = (CommodityAveragePriceFixingSchedule)fixingSchedule;
      bool projected = false;
      foreach (var date in sched.ObservationDates)
      {
        RateResetState state;
        var price = CalculateForwardPrice(AsOf, date, ReferenceCurve, HistoricalObservations, UseAsOfResets, out state);
        if (state == RateResetState.Missing)
          throw new MissingFixingException($"Fixing resetting on date {date} is missing.");
        ret.Components.Add(price);
        ret.ResetStates.Add(state);
        projected |= (state == RateResetState.IsProjected);
      }
      ret.Forward = ret.Components.Average();
      ret.RateResetState = projected ? RateResetState.IsProjected : RateResetState.ObservationFound;
      return ret;
    }

    /// <summary>
    /// Initialize fixing schedule
    /// </summary>
    /// <param name="prevPayDt">Previous payment date</param>
    /// <param name="periodStart">Period start</param>
    /// <param name="periodEnd">Period end</param>
    /// <param name="payDt">Payment date</param>
    /// <returns>
    /// Fixing schedule
    /// </returns>
    public override FixingSchedule GetFixingSchedule(Dt prevPayDt, Dt periodStart, Dt periodEnd, Dt payDt)
    {
      var fixingSchedule = new CommodityAveragePriceFixingSchedule();
      switch (PriceFixingScheme.CommodityPriceObservationRule)
      {
        case CommodityPriceObservationRule.First:
          GatherForward(periodStart, fixingSchedule);
          break;
        case CommodityPriceObservationRule.Last:
          GatherBackward(periodEnd, fixingSchedule);
          break;
        case CommodityPriceObservationRule.All:
          GatherAll(periodStart, periodEnd, fixingSchedule);
          break;
        default:
          throw new ArgumentOutOfRangeException(String.Format("DayDistribution type {0} unknown for generating fixing schedule",
                                                              PriceFixingScheme.CommodityPriceObservationRule));
      }

      fixingSchedule.ResetDate = fixingSchedule.ObservationDates.LastOrDefault();
      return fixingSchedule;
    }


    /// <summary>
    /// Rate reset information
    /// </summary>
    /// <param name="schedule">Fixing schedule</param>
    /// <returns>
    /// Reset info for each component of the fixing
    /// </returns>
    public override List<RateResets.ResetInfo> GetResetInfo(FixingSchedule schedule)
    {
      var resetInfos = new List<RateResets.ResetInfo>();
      var sched = (CommodityAveragePriceFixingSchedule)schedule;
      RateResetState state;
      double rate = RateResetUtil.FindRate(sched.ResetDate, AsOf, HistoricalObservations, UseAsOfResets, out state);
      if (state == RateResetState.Missing && RateResetUtil.ProjectMissingRateReset(sched.ResetDate, AsOf, sched.FixingEndDate))
        state = RateResetState.IsProjected;
      resetInfos.Add(new RateResets.ResetInfo(sched.ResetDate, rate, state) {AccrualStart = sched.FixingEndDate, AccrualEnd = sched.FixingEndDate});
      return resetInfos;
    }

    #endregion

    #region Private Methods

    private static double CalculateForwardPrice(Dt asOf, Dt resetDate, CalibratedCurve referenceCurve, RateResets resets, bool useAsOfResets,
                                                out RateResetState state)
    {
      double price = RateResetUtil.FindRate(resetDate, asOf, resets, out state);
      if (state == RateResetState.IsProjected)
        return CalculateForwardPrice(referenceCurve, resetDate);
      return price;
    }

    private static double CalculateForwardPrice(CalibratedCurve reference,
                                                Dt date)
    {
      return reference.Interpolate(date);
    }

    private void GatherAll(Dt periodStart, Dt periodEnd, CommodityAveragePriceFixingSchedule fixingSchedule)
    {
      var date = periodStart;
      while (date <= periodEnd)
      {
        fixingSchedule.ObservationDates.Add(date);
        date = Dt.AddDays(date, 1, PriceFixingScheme.Calendar);
      }
    }

    private void GatherBackward(Dt date, CommodityAveragePriceFixingSchedule fixingSchedule)
    {
      int i = PriceFixingScheme.NumDates;
      while (i-- > 0)
      {
        fixingSchedule.ObservationDates.Add(date);
        date = Dt.AddDays(date, -1, PriceFixingScheme.Calendar);
      }
      fixingSchedule.ObservationDates.Sort();
    }

    private void GatherForward(Dt date, CommodityAveragePriceFixingSchedule fixingSchedule)
    {
      int i = PriceFixingScheme.NumDates;
      while (i-- > 0)
      {
        fixingSchedule.ObservationDates.Add(date);
        date = Dt.AddDays(date, 1, PriceFixingScheme.Calendar);
      }
    }

    #endregion
  }
}