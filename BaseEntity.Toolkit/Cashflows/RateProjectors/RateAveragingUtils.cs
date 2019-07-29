using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Generic averaged rate coupon calculator utilities. 
  /// </summary>
  public static class RateAveragingUtils
  {
    #region Nested type: AveragedRateFn

    /// <summary>
    /// Delegate for calculation of average from resets
    /// </summary>
    /// <param name="rates">Rates</param>
    /// <param name="wts">Weights</param>
    /// <param name="start">Start dates</param>
    /// <param name="end">End dates</param>
    /// <param name="states">Rate reset state</param>
    /// <param name="index">Reference index</param>
    /// <param name="map">Map to transform libor to desired rate</param>
    /// <returns>Averaged rate</returns>
    internal delegate double AveragedRateFn(
      List<double> rates, List<double> wts, List<Dt> start, List<Dt> end, List<RateResetState> states,
      ReferenceIndex index, Func<double, Dt, Dt, double> map);

    #endregion

    #region Methods

    /// <summary>
    /// Calculate reset 
    /// </summary>
    /// <param name="asOf">As of </param>
    /// <param name="fixingSchedule">Fixing schedule</param>
    /// <param name="referenceIndex">Index</param>
    /// <param name="reference">Reference curve</param>
    /// <param name="discount">Discount</param>
    /// <param name="resets">Resets</param>
    /// <param name="useAsOfResets">True to use as of resets</param>
    /// <param name="rateFn">Rate function</param>
    /// <param name="averagedRateFn">Delegate the computes the average out the resets</param>
    /// <returns></returns>
    internal static Fixing AveragedRateFixing(Dt asOf, FixingSchedule fixingSchedule, ReferenceIndex referenceIndex,
      CalibratedCurve reference, DiscountCurve discount, RateResets resets,
      bool useAsOfResets, Func<double, Dt, Dt, double> rateFn,
      AveragedRateFn averagedRateFn)
    {
      var retVal = new AveragedRateFixing();

      var sched = fixingSchedule as AverageRateFixingSchedule;
      if (sched == null)
        throw new ArgumentException("AverageRateFixingSchedule expected");
      var freq = referenceIndex.IndexTenor.ToFrequency();
      bool projected = false;
      for (int i = 0; i < sched.ResetDates.Count; ++i)
      {
        RateResetState state;
        double rate = ForwardRateCalculator.CalculateForwardRate(asOf,
          sched.ResetDates[i], sched.StartDates[i], sched.EndDates[i], reference,
          referenceIndex.DayCount, freq, resets, useAsOfResets, out state);
        if (state == RateResetState.Missing && !RateResetUtil.ProjectMissingRateReset(sched.ResetDates[i], asOf, sched.StartDates[i]))
          throw new MissingFixingException(String.Format("Fixing resetting on date {0} is missing.", sched.ResetDates[i]));

        projected |= (state == RateResetState.IsProjected);
        retVal.Components.Add(rate);
        retVal.ResetStates.Add(state);
      }
      retVal.RateResetState = projected ? RateResetState.IsProjected : RateResetState.ObservationFound;
      //if one rate is projected, the coupon is considered projected
      retVal.Forward = averagedRateFn(retVal.Components, sched.Weights, sched.StartDates, sched.EndDates,
        retVal.ResetStates, referenceIndex, rateFn);
      return retVal;
    }

    /// <summary>
    /// Delegate for calculation of average from resets
    /// </summary>
    /// <param name="rates">Rates</param>
    /// <param name="wts">Weights</param>
    /// <param name="dates">Dates</param>
    /// <param name="states">Rate reset state</param>
    /// <param name="index">Reference index</param>
    /// <param name="map">Map to transform libor to desired rate</param>
    /// <returns>Averaged rate</returns>
    internal delegate double AveragedPriceFn(
      List<double> rates, List<double> wts, List<Dt> dates, List<RateResetState> states,
      ReferenceIndex index, Func<double, Dt, Dt, double> map);

    /// <summary>
    ///  Variance function with option replication
    /// </summary>
    /// <param name="rates">Rates</param>
    /// <param name="wts">Weights</param>
    /// <param name="dates">Dates</param>
    /// <param name="resetStates">Rate reset state</param>
    /// <param name="index">Reference index</param>
    /// <param name="curve">Variance (volatility) curve</param>
    /// <param name="annualizationFactor">Annualization factor, typically 252</param>
    /// <returns></returns>
    internal delegate double VarianceFn(List<double> rates, List<double> wts, List<Dt> dates,
      List<RateResetState> resetStates, ReferenceIndex index, VolatilityCurve curve, double annualizationFactor);

    /// <summary>
    ///  Variance function
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="rates">Rates</param>
    /// <param name="wts">Weights</param>
    /// <param name="dates">Dates</param>
    /// <param name="resetStates">Rate reset state</param>
    /// <param name="index">Reference index</param>
    /// <param name="discount">Discount curve</param>
    /// <param name="stockCurve">Stock curve</param>
    /// <param name="volSurface">Volatility surface</param>
    /// <param name="annualizationFactor">Annualization factor, typically 252</param>
    /// <returns></returns>
    internal delegate double VarianceReplicationFn(Dt asOf, Dt maturity, List<double> rates, List<double> wts, List<Dt> dates,
      List<RateResetState> resetStates, ReferenceIndex index, DiscountCurve discount, StockCurve stockCurve, IVolatilitySurface volSurface,
      double annualizationFactor);


    /// <summary>
    /// Fixing schedule for averaging rates
    /// </summary>
    /// <param name="periodStart">Period start</param>
    /// <param name="periodEnd">Period end</param>
    /// <param name="index">Reference index</param>
    /// <param name="resetLag"> Delay </param>
    /// <param name="weighted">True for weighted</param>
    /// <param name="cutOff">Cutoff </param>
    /// <returns>Fixing schedule for averaging rates</returns>
    public static FixingSchedule InitializeFixingSchedule(Dt periodStart, Dt periodEnd, ReferenceIndex index,
      Tenor resetLag, bool weighted, int cutOff)
    {
      var fixingSchedule = new AverageRateFixingSchedule();
      Dt cutOffDt = Dt.Add(periodEnd, -cutOff);
      if (index.PublicationFrequency >= Frequency.Daily)
        GenerateDailySchedule(periodStart, cutOffDt, index, resetLag, weighted, ref fixingSchedule);
      else
        GenerateCycleDrivenSchedule(periodStart, periodEnd, cutOffDt, index, resetLag, weighted, ref fixingSchedule);
      if (fixingSchedule.ResetDates.Count > 0)
        fixingSchedule.ResetDate = fixingSchedule.ResetDates[fixingSchedule.ResetDates.Count - 1]; //last reset Dt
      return fixingSchedule;
    }


    private static RateResets.ResetInfo ResetInfoPerComponent(Dt resetDt, Dt asOf, Dt start, RateResets resets,
      bool useAsOfResets)
    {
      RateResetState s;
      double rate = RateResetUtil.FindRate(resetDt, asOf, resets, useAsOfResets, out s);
      if ((resetDt <= asOf) && (start >= asOf && s == RateResetState.Missing))
        s = RateResetState.IsProjected;
      return new RateResets.ResetInfo(resetDt, rate, s);
    }

    /// <summary>
    /// Rate reset information
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="schedule">Fixing schedule</param>
    /// <param name="resets">Rate resets</param>
    /// <param name="useAsOfResets">Use historical reset at asof</param>
    /// <returns> Reset info for each component of the fixing</returns>
    public static List<RateResets.ResetInfo> GetResetInfo(Dt asOf, FixingSchedule schedule, RateResets resets,
      bool useAsOfResets)
    {
      var resetInfos = new List<RateResets.ResetInfo>();
      var sched = schedule as AverageRateFixingSchedule;
      if (sched != null)
      {
        resetInfos.AddRange(sched.ResetDates.Select((t, i) => ResetInfoPerComponent(t, asOf, sched.StartDates[i], resets, useAsOfResets)));
        return resetInfos;
      }
      var sched2 = schedule as AveragePriceFixingSchedule;
      if (sched2 != null)
      {
        resetInfos.AddRange(sched2.ObservationDates.Select(t => ResetInfoPerComponent(t, asOf, t, resets, useAsOfResets)));
        return resetInfos;
      }
      
      return resetInfos;
    }

    /// <summary>
    /// Approx. fixing schedule for averaging rates
    /// </summary>
    /// <param name="asOf">AsOf date</param> 
    /// <param name="periodStart">Period start</param>
    /// <param name="periodEnd">Period end</param>
    /// <param name="index">Reference index</param>
    /// <param name="resetLag"> Delay </param>
    /// <param name="weighted">True for weighted</param>
    /// <param name="cutOff">Cutoff </param>
    /// <returns>Fixing schedule for averaging rates</returns>
    public static FixingSchedule InitializeApproxFixingSchedule(Dt asOf, Dt periodStart, Dt periodEnd,
      ReferenceIndex index, Tenor resetLag, bool weighted,
      int cutOff)
    {
      AverageRateFixingSchedule fixingSchedule;
      if (asOf >= periodEnd)
        return InitializeFixingSchedule(periodStart, periodEnd, index, resetLag, weighted, cutOff);
      if (asOf >= periodStart)
      {
        fixingSchedule =
          (AverageRateFixingSchedule)InitializeFixingSchedule(periodStart, asOf, index, resetLag, true, 0);
        periodStart = asOf;
      }
      else
        fixingSchedule = new AverageRateFixingSchedule();
      GenerateApproxFixingSchedule(periodStart, periodEnd, index, ref fixingSchedule);
      fixingSchedule.ResetDate = fixingSchedule.ResetDates[fixingSchedule.ResetDates.Count - 1]; //last reset Dt
      return fixingSchedule;
    }

    #endregion

    #region Utils

    private static void GenerateDailySchedule(Dt periodStart, Dt cutOffDt, ReferenceIndex index,
      Tenor resetLag, bool weighted, ref AverageRateFixingSchedule fixingSchedule)
    {
      Dt next = periodStart;
      while (next < cutOffDt)
      {
        Dt prev = next;
        fixingSchedule.StartDates.Add(prev);
        Dt end = Dt.Add(prev, index.IndexTenor);
        Dt rolled = Dt.Roll(end, index.Roll, index.Calendar);
        while (rolled <= prev)
        {
          end = Dt.Add(end, 1);
          rolled = Dt.Roll(end, index.Roll, index.Calendar);
        }
        fixingSchedule.EndDates.Add(rolled);
        fixingSchedule.ResetDates.Add(RateResetUtil.ResetDate(prev, index, resetLag));
        next = Dt.AddDays(prev, 1, index.Calendar);
        fixingSchedule.Weights.Add(weighted ? Dt.Diff(prev, next) : 1.0);
      }
    }

    private static void GenerateCycleDrivenSchedule(Dt periodStart, Dt periodEnd, Dt cutOffDt, ReferenceIndex index,
      Tenor resetLag, bool weighted,
      ref AverageRateFixingSchedule fixingSchedule)
    {
      Dt anchorDt = RateResetUtil.ResetDate(periodStart, index, resetLag); //settle dates are driven by resets
      Dt next = Dt.Roll(Dt.Add(anchorDt, index.SettlementDays), index.Roll, index.Calendar);
      while (next <= cutOffDt)
      {
        Dt prev = next;
        fixingSchedule.ResetDates.Add(anchorDt);
        fixingSchedule.StartDates.Add(prev);
        fixingSchedule.EndDates.Add(Dt.Roll(Dt.Add(prev, index.IndexTenor), index.Roll, index.Calendar));
        anchorDt = Dt.Add(anchorDt, index.PublicationFrequency, 1, index.ResetDateRule);
        next = Dt.Roll(Dt.Add(anchorDt, index.SettlementDays), index.Roll, index.Calendar);
        fixingSchedule.Weights.Add(weighted ? Dt.Diff(prev, next > periodEnd ? periodEnd : next) : 1.0);
      }
    }

    private static void GenerateApproxFixingSchedule(Dt start, Dt end, ReferenceIndex index,
      ref AverageRateFixingSchedule fixingSchedule)
    {
      const double l = 0.0;
      double u = Dt.FractDiff(start, end);
      int n = (int)(u - l) / 30; //monthly quadrature points
      var x = new double[n + 2];
      var w = new double[n + 2];
      Quadrature.GaussLegendre(false, true, x, w);
      double xm = 0.5 * u, xl = 0.5 * u;
      for (int i = 0; i < x.Length; ++i)
      {
        var xi = (int)(xm + x[i] * xl);
        Dt starti = Dt.Add(start, xi);
        Dt endi = Dt.Add(starti, index.IndexTenor);
        fixingSchedule.ResetDates.Add(starti);
        fixingSchedule.StartDates.Add(starti);
        fixingSchedule.EndDates.Add(endi);
        fixingSchedule.Weights.Add(xl * w[i]);
      }
    }
    #endregion
  }
}