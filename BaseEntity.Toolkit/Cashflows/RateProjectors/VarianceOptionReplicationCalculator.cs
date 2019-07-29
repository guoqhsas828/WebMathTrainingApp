using System;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Variance Price Option Replication calculator
  /// </summary>
  [Serializable]
  public class VarianceOptionReplicationCalculator : ArithmeticAvgRateCalculator
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">AsOf Date</param>
    /// <param name="referenceIndex">Reference index object</param>
    /// <param name="discount">Discount curve</param>
    /// <param name="referenceCurve">Reference rate curve used for the calculation of the rate.</param>
    /// <param name="volSurface">Volatility surface</param>
    /// <param name="weighted">Weight by number of business days</param>
    /// <param name="approximate">Approximate observation schedule</param>
    /// <param name="useAsOfResets">Use resets on the AsOf date</param>
    public VarianceOptionReplicationCalculator(Dt asOf, ReferenceIndex referenceIndex, DiscountCurve discount, 
      CalibratedCurve referenceCurve, IVolatilitySurface volSurface, bool weighted, bool approximate, bool useAsOfResets)
      : base(asOf, referenceIndex, referenceCurve)
    {
      CutOff = 0;
      Weighted = weighted;
      ReferenceCurve = referenceCurve;
      VolatilitySurface = volSurface;
      Approximate = approximate;
      DiscountCurve = discount;
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
      return AveragePriceUtils.VarianceReplicationPriceFixing(AsOf, fixingSchedule, ReferenceIndex, ReferenceCurve as StockCurve, VolatilitySurface, DiscountCurve,
                                                   HistoricalObservations, UseAsOfResets, AnnualizationFactor, Variance);
    }

    private static double Variance(Dt asOf, Dt maturity, List<double> rates, List<double> wts, List<Dt> dates,
                                        List<RateResetState> resetStates, ReferenceIndex index, DiscountCurve discount, 
                                        StockCurve stockCurve, IVolatilitySurface volSurface, double annualizationFactor)
    {
      // Value date
      var valDate = dates[dates.Count - 1];
      var atmF = stockCurve.Interpolate(valDate);
      var rfr = RateCalc.Rate(discount, asOf, valDate);
      var dividend = stockCurve.ImpliedDividendYield(asOf, valDate);
      var spot = stockCurve.SpotPrice;
      var T = Dt.TimeInYears(asOf, valDate);
      var df = discount.DiscountFactor(asOf, maturity);

      var n = 0;
      var impliedVariance = 0.0;
      var N = dates.Count;

      if (resetStates[0] == RateResetState.IsProjected)
      {
        var dK = ReplicationParams.MoneynessStep * atmF;
        var sum = 0.0;
        var m = ReplicationParams.MoneynessMin;
        while (m <= ReplicationParams.MoneynessMax)
        {
          var k = m * atmF;
          var optionType = m >= 1.0 ? OptionType.Call : OptionType.Put;
          var vol = volSurface.Interpolate(valDate, atmF, k);
          var optionPv = BlackScholes.P(OptionStyle.European, optionType, T, spot, k, rfr, dividend, stockCurve.Dividends, vol);
          if (m.AlmostEquals(1.0))
            optionPv = 0.5 * (optionPv + BlackScholes.P(OptionStyle.European, OptionType.Put, T, spot, k,
                                rfr, dividend, stockCurve.Dividends, vol));
          var wi = dK / (k * k);
          sum += wi * optionPv;
          m += ReplicationParams.MoneynessStep;

        }

        if (maturity != valDate)
        {
          var dfVal = discount.DiscountFactor(asOf, valDate);
          sum *= df / dfVal;
        }

        impliedVariance = sum * sum;
      }

      for (var i = 1; i < N; ++i)
      {
        if (resetStates[i] == RateResetState.IsProjected)
          break;
        n++;
      }
      // Realized volatility defined without mean as in Variance Swap documentation
      var variance = 0.0;
      for (var i = 1; i <= n; ++i)
      {
        var diff = Math.Log(rates[i] / rates[i - 1]);
        variance += diff * diff;
      }
      var realizedVariance = n == 0 ? 0.0 : annualizationFactor / n * variance;
      
      return realizedVariance * n / N + impliedVariance * (N - n) / N;
    }

    /// <summary>
    ///  Variance Swap Curve (implied)
    /// </summary>
    public IVolatilitySurface VolatilitySurface { get; set; }

    /// <summary>
    ///  Annualization factor (typically 252)
    /// </summary>
    public double AnnualizationFactor { get; set; }

    #region Data

    /// <summary>
    ///  Replication Params
    /// </summary>
    private static class ReplicationParams
    {
      public const double MoneynessMin = 0.50;
      public const double MoneynessMax = 1.50;
      public const double MoneynessStep = 0.01;
    }

    #endregion
  }

}
