using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Commodity Average Price calculator
  /// </summary>
  [Serializable]
  public class CommodityAveragePriceCalculator : ArithmeticAvgRateCalculator
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">AsOf Date</param>
    /// <param name="referenceIndex">Reference index object</param>
    /// <param name="referenceCurve">Reference rate curve used for the calculation of the rate.</param>
    /// <param name="observationType">Observation type</param>
    /// <param name="numObservations">Number of observations for First and Last n day averages</param>
    /// <param name="rollExpiryDate">Roll expiry date on last trading date</param>
    /// <param name="weighted">Weight by number of business days</param>
    /// <param name="approximate">Approximate observation schedule</param>
    public CommodityAveragePriceCalculator(Dt asOf, ReferenceIndex referenceIndex, CommodityCurve referenceCurve,
      CommodityPriceObservationRule observationType, int numObservations, bool rollExpiryDate, bool weighted, bool approximate)
      : base(asOf, referenceIndex, referenceCurve)
    {
      CutOff = 0;
      Weighted = weighted;
      RollExpiryDate = rollExpiryDate;
      ReferenceCurve = referenceCurve;
      ObservationType = observationType;
      Observations = numObservations;
      Approximate = approximate;
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
      // Get the curve date function
      Func<Dt, Dt> curveFunc = null;
      if (ReferenceCurve.Tenors.All(o => o.Product is CommodityFuture || o.Product is SpotAsset))
        curveFunc = o => ContractDate(o, ReferenceCurve as CommodityCurve, RollExpiryDate);
      else if (ReferenceCurve.Tenors.All(o => o.Product is CommodityForward || o.Product is SpotAsset))
        curveFunc = o => o;
      else
        throw new ToolkitException($"Commodity Curve {ReferenceCurve.Name} must contain only futures quotes, or only forward quotes");

      switch (ObservationType)
      {
        case CommodityPriceObservationRule.None:
        case CommodityPriceObservationRule.First:
        case CommodityPriceObservationRule.Last:
          return AveragePriceUtils.GenerateAveragePriceObservations<CommodityAveragePriceFixingSchedule>(periodStart, periodEnd, 
            ObservationType == CommodityPriceObservationRule.First, Observations, curveFunc, ReferenceIndex, ResetLag, Weighted, CutOff, Approximate);
        case CommodityPriceObservationRule.All:
          return AveragePriceUtils.GenerateAveragePriceObservationSchedule<CommodityAveragePriceFixingSchedule>(periodStart, periodEnd, 
            curveFunc, ReferenceIndex, ResetLag, Weighted, CutOff, Approximate);
        default:
          throw new ToolkitException($"CommodityPriceObservationRule {ObservationType} not found");
      }
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
      return AveragePriceUtils.AveragedCommodityPriceFixing(AsOf, fixingSchedule, ReferenceIndex, ReferenceCurve as CommodityCurve, DiscountCurve,
                                                   HistoricalObservations, RollExpiryDate, UseAsOfResets, FixingFn, ArithmeticAverage);
    }

    private static double ArithmeticAverage(List<double> rates, List<double> wts, List<Dt> dates,
                                        List<RateResetState> resetStates, ReferenceIndex index,
                                        Func<double, Dt, Dt, double> rateFn)
    {
      var n = 0.0;
      var avg = 0.0;
      for (var i = 0; i < rates.Count; ++i)
      {
        var wt = wts?[i] ?? 1.0;
        avg += wt * rateFn(rates[i], dates[i], dates[i]);
        n += wt;
      }
      return avg / n;
    }

    /// <summary>
    ///  Get futures price for reset date
    /// </summary>
    /// <param name="asOf">valuation date</param>
    /// <param name="resetDt">Reset date</param>
    /// <param name="curveDt">Curve date</param>
    /// <param name="reference">Reference curve</param>
    /// <param name="resets">Commodity price resets</param>
    /// <param name="useAsOfResets">Use resets on as-of date</param>
    /// <param name="state">Output reset state</param>
    /// <param name="rollExpiryDate">Roll contract on expiry date</param>
    /// <returns></returns>
    public static double GetPrice(Dt asOf, Dt resetDt, Dt curveDt, CommodityCurve reference, RateResets resets, bool rollExpiryDate,
      bool useAsOfResets, out RateResetState state)
    {
      var rate = RateResetUtil.FindRate(resetDt, asOf, resets, useAsOfResets, out state);
      switch (state)
      {
        case RateResetState.Missing:
          throw new MissingFixingException($"Fixing resetting on date {resetDt} is missing.");
        case RateResetState.IsProjected:
          return reference.Interpolate(curveDt);
        case RateResetState.None:
        case RateResetState.ObservationFound:
        case RateResetState.ResetFound:
          return rate;
        default:
          throw new ArgumentOutOfRangeException(nameof(state), state, null);
      }
    }

    /// <summary>
    ///  Roll contract on expiry date
    /// </summary>
    public bool RollExpiryDate { get; private set; }

    /// <summary>
    ///  Observation type
    /// </summary>
    public CommodityPriceObservationRule ObservationType { get; set; }

    /// <summary>
    ///  Number of observations
    /// </summary>
    public int Observations { get; set; }

    /// <summary>
    ///  Returns commodity contract date
    /// </summary>
    /// <returns></returns>
    public Dt ContractDate(Dt resetDate, CommodityCurve refCurve, bool rollExpiryDate)
    {
      var tenors = refCurve.Tenors
        .Where(o => o.Product is CommodityFuture && o.Maturity > AsOf)
        .Select(o => o.Product as CommodityFuture)
        .ToArray();
      if (!tenors.Any())
        throw new ToolkitException($"No commodity futures found on {refCurve.Name} curve");
      var lastExpiry = tenors[0].LastTradingDate;
      if ((lastExpiry >= resetDate))
        return (lastExpiry == resetDate && rollExpiryDate) && refCurve.Tenors.Count > 1 ? tenors[1].Maturity : tenors[0].Maturity;
      return tenors.First(o => rollExpiryDate ? o.LastTradingDate > resetDate : o.LastTradingDate >= resetDate).Maturity;
    }

  }

}
