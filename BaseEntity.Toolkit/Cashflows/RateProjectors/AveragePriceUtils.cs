using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  ///  Average price fixing schedule utils for commodity swaps
  /// </summary>
  public static class AveragePriceUtils
  {
    /// <summary>
    /// Generate average rate observation schedule
    /// </summary>
    /// <param name="periodStart">Observation period start</param>
    /// <param name="periodEnd">Observation period end</param>
    /// <param name="curveDateFunc">Curve date function</param>
    /// <param name="index">Commodity index</param>
    /// <param name="resetLag">Reset lag</param>
    /// <param name="weighted">Time weighted or equally weighted</param>
    /// <param name="cutOff">Cutoff</param>
    /// <param name="approximate">Approximate schedule using only unique reset dates</param>
    public static T GenerateAveragePriceObservationSchedule<T>(Dt periodStart, Dt periodEnd, Func<Dt, Dt> curveDateFunc,
      ReferenceIndex index, Tenor resetLag, bool weighted, int cutOff, bool approximate) where T : AveragePriceFixingSchedule, new()
    {
      var fixingSchedule = new T();
      var cutOffDt = Dt.Add(periodEnd, -cutOff);
      GenerateAveragePriceObservationSchedule(periodStart, cutOffDt, curveDateFunc, index, resetLag, weighted, approximate, ref fixingSchedule);
      if (fixingSchedule.ObservationDates.Count > 0)
        fixingSchedule.ResetDate = fixingSchedule.ObservationDates[fixingSchedule.ObservationDates.Count - 1]; //last reset Dt
      return fixingSchedule;
    }

    /// <summary>
    /// Generate average rate observation schedule
    /// </summary>
    /// <param name="periodStart">Observation period start</param>
    /// <param name="periodEnd">Observation period end</param>
    /// <param name="index">Commodity index</param>
    /// <param name="resetLag">Reset lag</param>
    /// <param name="weighted">Time weighted or equally weighted</param>
    /// <param name="cutOff">Cutoff</param>
    /// <param name="approximate">Approximate schedule using only unique reset dates</param>
    public static T GenerateVarianceObservationSchedule<T>(Dt periodStart, Dt periodEnd, 
      ReferenceIndex index, Tenor resetLag, bool weighted, int cutOff, bool approximate) where T : AveragePriceFixingSchedule, new()
    {
      var fixingSchedule = new T();
      var cutOffDt = Dt.Add(periodEnd, -cutOff);
      GenerateAveragePriceObservationSchedule(periodStart, cutOffDt, o => o, index, resetLag, weighted, approximate, ref fixingSchedule);
      if (fixingSchedule.ObservationDates.Count > 0)
        fixingSchedule.ResetDate = fixingSchedule.ObservationDates[fixingSchedule.ObservationDates.Count - 1]; //last reset Dt
      return fixingSchedule;
    }

    /// <summary>
    /// Generate average rate observation schedule
    /// </summary>
    /// <param name="periodStart">Observation period start</param>
    /// <param name="periodEnd">Observation period end</param>
    /// <param name="observations">Number of observations</param>
    /// <param name="curveDateFunc">Curve date function</param>
    /// <param name="index">Commodity index</param>
    /// <param name="resetLag">Reset lag</param>
    /// <param name="weighted">Weighted</param>
    /// <param name="cutOff">Cutoff</param>
    /// <param name="startPeriod">Period at start or end</param>
    /// <param name="approximate">Approximate</param>
    public static T GenerateAveragePriceObservations<T>(Dt periodStart, Dt periodEnd, bool startPeriod, int observations,
      Func<Dt, Dt> curveDateFunc, ReferenceIndex index, Tenor resetLag, bool weighted, int cutOff, bool approximate) where T : AveragePriceFixingSchedule, new()
    {
      var fixingSchedule = new T();
      var start = startPeriod ? periodStart : Dt.AddDays(Dt.Roll(periodEnd, BDConvention.ModPreceding, index.Calendar), -(observations - 1), index.Calendar);
      var end = startPeriod
        ? Dt.AddDays(Dt.Roll(start, BDConvention.Following, index.Calendar),
          (observations - 1) - cutOff, index.Calendar)
        : Dt.AddDays(Dt.Roll(periodEnd, BDConvention.ModPreceding, index.Calendar), -cutOff, index.Calendar);
      GenerateAveragePriceObservationSchedule(start, end, curveDateFunc, index, resetLag, weighted, approximate, ref fixingSchedule);
      if (fixingSchedule.ObservationDates.Count > 0)
        fixingSchedule.ResetDate = fixingSchedule.ObservationDates[fixingSchedule.ObservationDates.Count - 1]; //last reset Dt
      return fixingSchedule;
    }

    private static void GenerateAveragePriceObservationSchedule<T>(Dt periodStart, Dt cutOffDt, Func<Dt, Dt> curveDateFunc,
      ReferenceIndex index, Tenor resetLag, bool weighted, bool approximate,
      ref T fixingSchedule) where T : AveragePriceFixingSchedule
    {
      var endDate = resetLag != Tenor.Empty ? Dt.Add(cutOffDt, -Math.Abs(resetLag.N), resetLag.Units) : cutOffDt;
      var next = resetLag != Tenor.Empty ? Dt.Add(periodStart, -Math.Abs(resetLag.N), resetLag.Units) : periodStart;
      var rolledNext = Dt.Roll(next, index.Roll, index.Calendar);
      next = rolledNext;

      fixingSchedule.ObservationDates.Add(rolledNext);
      fixingSchedule.CurveDates.Add(curveDateFunc(rolledNext));
      fixingSchedule.Weights.Add(1.0);

      // Single observation
      if (periodStart == cutOffDt)
        return;

      var approxStart = next;
      // Do not add period start date
      while (next <= endDate)
      {
        var prev = next;
        var end = Dt.Add(prev, Tenor.OneDay);
        var rolled = Dt.Roll(end, index.Roll, index.Calendar);
        if (rolled > endDate)
          break;
        while (rolled <= prev)
        {
          end = Dt.Add(end, 1);
          rolled = Dt.Roll(end, index.Roll, index.Calendar);
        }

        // Time weighted or equally weighted
        var nextDay = Dt.AddDays(rolled, 1, index.Calendar);
        var weight = weighted ? nextDay > endDate ? Math.Max(1, Dt.Diff(rolled, endDate)) : Dt.Diff(rolled, nextDay) : 1.0;

        if (!approximate)
        {
          if (fixingSchedule.ObservationDates.Contains(rolled))
            fixingSchedule.Weights[fixingSchedule.Weights.Count() - 1] = fixingSchedule.Weights[fixingSchedule.Weights.Count() - 1] + weight;
          else
          {
            fixingSchedule.ObservationDates.Add(rolled);
            fixingSchedule.CurveDates.Add(curveDateFunc(rolled));
            fixingSchedule.Weights.Add(weight);
          }
        }
        else
        {
          if (curveDateFunc(rolled) != curveDateFunc(nextDay) || nextDay > cutOffDt)
          {
            GenerateApproxCommodityFixingSchedule(approxStart, rolled, curveDateFunc, ref fixingSchedule);
            approxStart = nextDay;
          }
        }

        next = Dt.AddDays(prev, 1, index.Calendar);
      }
    }

    private static void GenerateApproxCommodityFixingSchedule<T>(Dt start, Dt end, Func<Dt, Dt> curveFunc,
      ref T fixingSchedule) where T : AveragePriceFixingSchedule
    {
      const double l = 0.0;
      var u = Dt.FractDiff(start, end);
      var n = (int)(u - l) / 7; //weekly quadrature points
      var x = new double[n + 2];
      var w = new double[n + 2];
      Quadrature.GaussLegendre(false, true, x, w);
      double xm = 0.5 * u, xl = 0.5 * u;
      for (int i = 0; i < x.Length; ++i)
      {
        var xi = (int)(xm + x[i] * xl);
        var starti = Dt.Add(start, xi);
        fixingSchedule.ObservationDates.Add(starti);
        fixingSchedule.CurveDates.Add(curveFunc(starti));
        fixingSchedule.Weights.Add(xl * w[i]);
      }
    }

    #region Commodity specific utils 

    /// <summary>
    /// Calculate reset 
    /// </summary>
    /// <param name="asOf">As of </param>
    /// <param name="fixingSchedule">Fixing schedule</param>
    /// <param name="referenceIndex">Index</param>
    /// <param name="reference">Reference curve</param>
    /// <param name="discount">Discount</param>
    /// <param name="resets">Resets</param>
    /// <param name="rollExpiryDate"></param>
    /// <param name="useAsOfResets">True to use as of resets</param>
    /// <param name="rateFn">Rate function</param>
    /// <param name="averagedRateFn">Delegate the computes the average out the resets</param>
    /// <returns></returns>
    internal static Fixing AveragedCommodityPriceFixing(Dt asOf, FixingSchedule fixingSchedule, ReferenceIndex referenceIndex,
      CommodityCurve reference, DiscountCurve discount, RateResets resets,
      bool rollExpiryDate,
      bool useAsOfResets, Func<double, Dt, Dt, double> rateFn,
      RateAveragingUtils.AveragedPriceFn averagedRateFn)
    {
      var retVal = new AveragedRateFixing();

      var sched = fixingSchedule as CommodityAveragePriceFixingSchedule;
      if (sched == null)
        throw new ArgumentException("AverageRateFixingSchedule expected");
      var projected = false;
      for (var i = 0; i < sched.ObservationDates.Count(); i++)
      {
        RateResetState state;
        var rate = CommodityAveragePriceCalculator.GetPrice(asOf, sched.ObservationDates[i], sched.CurveDates[i], reference, resets, rollExpiryDate,
          useAsOfResets, out state);

        projected |= (state == RateResetState.IsProjected);
        retVal.Components.Add(rate);
        retVal.ResetStates.Add(state);
      }
      retVal.RateResetState = projected ? RateResetState.IsProjected : RateResetState.ObservationFound;
      //if one rate is projected, the coupon is considered projected
      retVal.Forward = averagedRateFn(retVal.Components, sched.Weights, sched.ObservationDates,
        retVal.ResetStates, referenceIndex, rateFn);
      return retVal;
    }

    #endregion

    #region Variance Swap specific utils 

    /// <summary>
    /// Calculate reset 
    /// </summary>
    /// <param name="asOf">As of </param>
    /// <param name="fixingSchedule">Fixing schedule</param>
    /// <param name="referenceIndex">Index</param>
    /// <param name="reference">Reference curve</param>
    /// <param name="volCurve">Volatility curve</param>
    /// <param name="discount">Discount</param>
    /// <param name="resets">Resets</param>
    /// <param name="useAsOfResets">True to use as of resets</param>
    /// <param name="annualizationFactor">Annualization factor, typically 252</param>
    /// <param name="varianceFn">Delegate the computes the variance out the resets and volatility curve</param>
    /// <returns></returns>
    internal static Fixing VariancePriceFixing(Dt asOf, FixingSchedule fixingSchedule, ReferenceIndex referenceIndex,
      CalibratedCurve reference, VolatilityCurve volCurve, DiscountCurve discount, RateResets resets,
      bool useAsOfResets, double annualizationFactor, RateAveragingUtils.VarianceFn varianceFn)
    {
      var retVal = new AveragedRateFixing();

      var sched = fixingSchedule as VarianceFixingSchedule;
      if (sched == null)
        throw new ArgumentException("AverageRateFixingSchedule expected");
      var projected = false;
      for (var i = 0; i < sched.ObservationDates.Count(); i++)
      {
        RateResetState state;
        var rate = VarianceCurveCalculator.GetPrice(asOf, sched.ObservationDates[i], volCurve, resets, 
          useAsOfResets, out state);

        projected |= (state == RateResetState.IsProjected);
        retVal.Components.Add(rate);
        retVal.ResetStates.Add(state);
      }
      retVal.RateResetState = projected ? RateResetState.IsProjected : RateResetState.ObservationFound;
      //if one rate is projected, the coupon is considered projected
      retVal.Forward = varianceFn(retVal.Components, sched.Weights, sched.ObservationDates,
        retVal.ResetStates, referenceIndex, volCurve, annualizationFactor);
      return retVal;
    }

    /// <summary>
    /// Calculate reset 
    /// </summary>
    /// <param name="asOf">As of </param>
    /// <param name="fixingSchedule">Fixing schedule</param>
    /// <param name="referenceIndex">Index</param>
    /// <param name="stockCurve">Stock curve</param>
    /// <param name="volSurface">Volatility surface</param>
    /// <param name="discount">Discount</param>
    /// <param name="resets">Resets</param>
    /// <param name="useAsOfResets">True to use as of resets</param>
    /// <param name="annualizationFactor">Annualization factor, typically 252</param>
    /// <param name="varianceFn">Delegate the computes the variance out the resets and volatility curve</param>
    /// <returns></returns>
    internal static Fixing VarianceReplicationPriceFixing(Dt asOf, FixingSchedule fixingSchedule, ReferenceIndex referenceIndex,
      StockCurve stockCurve, IVolatilitySurface volSurface, DiscountCurve discount, RateResets resets,
      bool useAsOfResets, double annualizationFactor, RateAveragingUtils.VarianceReplicationFn varianceFn)
    {
      var retVal = new AveragedRateFixing();

      var sched = fixingSchedule as VarianceFixingSchedule;
      if (sched == null)
        throw new ArgumentException("AverageRateFixingSchedule expected");
      var projected = false;
      for (var i = 0; i < sched.ObservationDates.Count(); i++)
      {
        RateResetState state;
        var rate = VarianceCurveCalculator.GetPrice(asOf, sched.ObservationDates[i], stockCurve, resets,
          useAsOfResets, out state);

        projected |= (state == RateResetState.IsProjected);
        retVal.Components.Add(rate);
        retVal.ResetStates.Add(state);
      }
      retVal.RateResetState = projected ? RateResetState.IsProjected : RateResetState.ObservationFound;
      //if one rate is projected, the coupon is considered projected
      retVal.Forward = varianceFn(asOf, sched.ObservationDates[sched.ObservationDates.Count()-1], retVal.Components, sched.Weights, sched.ObservationDates,
        retVal.ResetStates, referenceIndex, discount, stockCurve, volSurface, annualizationFactor);
      return retVal;
    }

    #endregion
  }
}
