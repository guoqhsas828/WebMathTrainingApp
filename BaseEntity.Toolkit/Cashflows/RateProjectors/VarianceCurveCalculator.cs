using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Variance Price Curve calculator
  /// </summary>
  [Serializable]
  public class VarianceCurveCalculator : ArithmeticAvgRateCalculator
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">AsOf Date</param>
    /// <param name="referenceIndex">Reference index object</param>
    /// <param name="referenceCurve">Reference rate curve used for the calculation of the rate.</param>
    /// <param name="volCurve">Volatility Curve</param>
    /// <param name="weighted">Weight by number of business days</param>
    /// <param name="approximate">Approximate observation schedule</param>
    /// <param name="useAsOfResets">Use resets on the AsOf date</param>
    public VarianceCurveCalculator(Dt asOf, ReferenceIndex referenceIndex, CalibratedCurve referenceCurve,
      VolatilityCurve volCurve, bool weighted, bool approximate, bool useAsOfResets)
      : base(asOf, referenceIndex, referenceCurve)
    {
      CutOff = 0;
      Weighted = weighted;
      ReferenceCurve = referenceCurve;
      VarianceSwapCurve = volCurve;
      Approximate = approximate;
      UseAsOfResets = useAsOfResets;
      AnnualizationFactor = 252.0;
    }

    /// <summary>
    /// Transformed rate from libor
    /// </summary>
    /// <param name="rate">Libor rate</param>
    /// <param name="start">Start date</param>
    /// <param name="end">End date</param>
    /// <returns>Rate</returns>
    public override double FixingFn(double rate, Dt start, Dt end)
    {
      return rate;
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
      return AveragePriceUtils.GenerateVarianceObservationSchedule<VarianceFixingSchedule>(periodStart, periodEnd,
        ReferenceIndex, ResetLag, Weighted, CutOff, Approximate);
    }

    /// <summary>
    /// Rate reset information
    /// </summary>
    /// <param name="schedule">Fixing schedule</param>
    /// <returns> Reset info for each component of the fixing</returns>
    public override List<RateResets.ResetInfo> GetResetInfo(FixingSchedule schedule)
    {
      return RateAveragingUtils.GetResetInfo(AsOf, schedule, HistoricalObservations, UseAsOfResets);
    }

    /// <summary>
    /// Fixing on reset 
    /// </summary>
    /// <param name="fixingSchedule">fixing schedule</param>
    /// <returns></returns>
    public override Fixing Fixing(FixingSchedule fixingSchedule)
    {
      return AveragePriceUtils.VariancePriceFixing(AsOf, fixingSchedule, ReferenceIndex, ReferenceCurve, VarianceSwapCurve, DiscountCurve,
                                                   HistoricalObservations, UseAsOfResets, AnnualizationFactor, Variance);
    }

    private static double Variance(List<double> rates, List<double> wts, List<Dt> dates,
                                        List<RateResetState> resetStates, ReferenceIndex index, VolatilityCurve curve, double annualizationfactor)
    {
      var n = 0;
      var N = dates.Count;
      var vol = curve.Interpolate(dates[0], dates[dates.Count - 1]);
      var impliedVariance = vol * vol;

      for (var i = 0; i < N; ++i)
      {
        if (resetStates[i] == RateResetState.IsProjected)
        {
          vol = curve.Interpolate(dates[i], dates[dates.Count - 1]);
          impliedVariance = vol * vol;
          break;
        }
        n++;
      }
      // Realized volatility defined without mean as in Variance Swap documentation
      var variance = 0.0;
      for (var i = 1; i < n; ++i)
      {
        var diff = Math.Log(rates[i] / rates[i - 1]);
        variance += diff * diff;
      }
      var realizedVariance = n == 0 ? 0.0 : annualizationfactor / n * variance;

      return realizedVariance * n / N + impliedVariance * (N - n) / N;
    }

    /// <summary>
    ///  Get futures price for reset date
    /// </summary>
    /// <param name="asOf">valuation date</param>
    /// <param name="resetDt">Reset date</param>
    /// <param name="reference">Reference curve</param>
    /// <param name="resets">Commodity price resets</param>
    /// <param name="useAsOfResets">Use resets on as-of date</param>
    /// <param name="state">Output reset state</param>
    /// <returns></returns>
    public static double GetPrice(Dt asOf, Dt resetDt, CalibratedCurve reference, RateResets resets, 
      bool useAsOfResets, out RateResetState state)
    {
      var rate = RateResetUtil.FindRate(resetDt, asOf, resets, useAsOfResets, out state);
      switch (state)
      {
        case RateResetState.Missing:
          throw new MissingFixingException($"Fixing resetting on date {resetDt} is missing.");
        case RateResetState.IsProjected:
          return 0.0;
        case RateResetState.None:
        case RateResetState.ObservationFound:
        case RateResetState.ResetFound:
          return rate;
        default:
          throw new ArgumentOutOfRangeException(nameof(state), state, null);
      }
    }

    /// <summary>
    ///  Variance Swap Curve (implied)
    /// </summary>
    public VolatilityCurve VarianceSwapCurve { get; set; }

    /// <summary>
    ///  Annualization factor (typically 252)
    /// </summary>
    public double AnnualizationFactor { get; set; }

  }

}
