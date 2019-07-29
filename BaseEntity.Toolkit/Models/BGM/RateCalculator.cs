/*
 * RateCalculator.cs
 *
 *  -2010. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Models.BGM
{
  public enum NodeDateKind
  {
    None = 0,
    Ignore = 1,
    Exercisable = 2
  }

  /// <summary>
  ///   Utilities function to do rate calculations.
  /// </summary>
  public static class RateCalculator
  {
    public static double EvaluateBermudan(
      this IRateSystemDistributions rsd,
      IList<NodeDateKind> nodeDateKinds,
      Func<IRateSystemDistributions, int, int, double> getCoupon,
      Func<IRateSystemDistributions, int, int, double> getExerciseCost)
    {
      Dt[] dates = rsd.NodeDates;
      if (dates.Length != nodeDateKinds.Count)
      {
        throw new ArgumentException(
          "Array of node dates and node actions not match.");
      }
      int nextDate = -1;
      double[] fwdValues = null, values = null;
      for (int d = dates.Length - 1; d >= 0; --d)
      {
        if (nodeDateKinds[d] == NodeDateKind.Ignore)
          continue; // never update the next date

        int stateCount = rsd.GetStateCount(d);
        if (values == null)
          values = new double[stateCount];

        int i = d;
        var frac = rsd.GetFraction(i);

        bool isExercisable = (nodeDateKinds[d] == NodeDateKind.Exercisable);
        for (int s = 0; s < stateCount; ++s)
        {
          var cash = getCoupon(rsd, d, s)*frac*rsd.GetAnnuity(i, d, s);
          var continuation = fwdValues == null ? 0.0
            : rsd.CalculateExpectation(d, s, nextDate, fwdValues);
          values[s] = (cash += continuation);
          if (isExercisable)
          {
            var exerciseCost = getExerciseCost == null ? 0.0
              : getExerciseCost(rsd, d, s);
            if (exerciseCost + cash < 0)
              values[s] = -exerciseCost;
          }
        }
#if DEBUG
        // Zero all the values above state count.
        //   Not really needed.
        for (int s = stateCount; s < values.Length; ++s)
          values[s] = 0;
#endif
        // Swap values and fwdValues
        var tmp = fwdValues;
        fwdValues = values;
        values = tmp;
        // Record the date we just updated
        nextDate = d;
      }
      return fwdValues == null || fwdValues.Length == 0 ? 0.0 : fwdValues[0];
    }

    /// <summary>
    /// Evaluates the bermudan.
    /// </summary>
    /// <param name="rsd">The rate system distributions.</param>
    /// <param name="exercisables">Array of booleans indicating whether a date is exercisable.</param>
    /// <param name="exercisePayoff">The intrinsic payoff function if the option is exercised on a given date.</param>
    /// <returns>Value of the option at the pricing date.</returns>
    public static double EvaluateBermudan(
      this IRateSystemDistributions rsd,
      IList<bool> exercisables,
      Func<int, DiscountCurve, double> exercisePayoff)
    {
      Dt[] dates = rsd.NodeDates;
      if (dates.Length != exercisables.Count)
      {
        throw new ArgumentException(
          "Array of node dates and exercisables not match.");
      }
      int k = dates.Length;
      while(--k > 0)
      {
        if (exercisables[k])
          break;
      }
      if (k==0) return 0;

      var fwdValues = ListUtil.CreateList(rsd.GetStateCount(k),
        (i) =>
        {
          var dc = rsd.GetDiscountCurve(k, i);
          return dc.Measure*exercisePayoff(k, dc.Curve);
        }).ToArray();

      int f = k;
      while (--k >= 0)
      {
        if (k > 0 && !exercisables[k])
          continue;
        var curValues = ListUtil.CreateList(rsd.GetStateCount(k),
        (i) =>
        {
          var dc = rsd.GetDiscountCurve(k, i);
          return dc.Measure * exercisePayoff(k, dc.Curve);
        }).ToArray();
        var trans = TransitionProbability(rsd, k, f);
        fwdValues = BackwardInduction(
          fwdValues, curValues, trans).ToArray();
        f = k;
      }
      return fwdValues[0];
    }

    private static IList<double> BackwardInduction(
      IList<double> forwardValues,
      IList<double> intrinsicValues,
      Func<int, int, double> transitionProbability)
    {
      int n = forwardValues.Count;
      int m = intrinsicValues.Count;
      var continuationValues = new double[m];
      for (int i = 0; i < m; ++i)
      {
        double sum = 0;
        for (int j=0; j < n; ++j)
        {
          double p = transitionProbability(i, j);
          sum += p*forwardValues[j];
        }
        continuationValues[i] = sum;
      }
      return ListUtil.MaxElements(intrinsicValues, continuationValues);
    }

    private static Func<int, int, double> TransitionProbability(
      IRateSystemDistributions rsd, int curDate, int fwdDate)
    {
      return (i, j) => rsd.GetConditionalProbability(fwdDate, j, curDate, i);
    }

    /// <summary>
    /// Calculate the caplets the value.
    /// </summary>
    /// <param name="rsd">The RSD.</param>
    /// <param name="type">The type.</param>
    /// <param name="expiryDateIndex">Index of the expiry date.</param>
    /// <param name="rateIndex">Index of the rate.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="fraction">The fraction.</param>
    /// <returns></returns>
    public static double CalculateCapletValue(
      this IRateSystemDistributions rsd,
      OptionType type,
      int expiryDateIndex,
      int rateIndex,
      double strike, double fraction)
    {
      if (Double.IsNaN(fraction))
        fraction = rsd.GetFraction(rateIndex);
      var list = ListUtil.CreateList(
        rsd.GetStateCount(expiryDateIndex),
        (i) => new RateAnnuityProbability(
          rsd.GetRate(rateIndex, expiryDateIndex, i),
          rsd.GetAnnuity(rateIndex, expiryDateIndex, i),
          rsd.GetProbability(expiryDateIndex, i)));
      return fraction * EuropeanOptionValue(list, type, strike, true);
    }

    private static IList<double> GetFractions(
      this IRateSystemDistributions rsd)
    {
      return ListUtil.CreateList(
        rsd.GetRateCount(0), (i) => rsd.GetFraction(i));
    }

    /// <summary>
    /// Gets the discount curve.
    /// </summary>
    /// <param name="rsd">The rate system distributions.</param>
    /// <param name="dateIndex">Index of the date.</param>
    /// <param name="stateIndex">Index of the state.</param>
    /// <returns>The discount curve and the associated measure.</returns>
    public static CurveWithMeasure<DiscountCurve> GetDiscountCurve(
      this IRateSystemDistributions rsd,
      int dateIndex, int stateIndex)
    {
      Dt asOf = rsd.AsOf;
      Dt[] tenorDates = rsd.TenorDates;

      // Simple wrap of the discount factors
      const DayCount dayCount = DayCount.Actual365Fixed;

      int count = tenorDates.Length;
      var curve = ForwardRateCalibrator.CreateForwardRateCurve(
        asOf, dayCount, tenorDates, ListUtil.CreateList(
          count, (i) => rsd.GetRate(i, dateIndex, stateIndex)));
      double measure = rsd.GetAnnuity(count - 1, 0, 0)
        /curve.GetVal(count - 1);
      return new CurveWithMeasure<DiscountCurve>(curve, measure);
    }

    internal static DiscountCurve CreateDiscountCurve(
      Dt asOf, int count, DayCount dayCount, Frequency freq,
      Func<int, Dt> maturities, Func<int, double> discountFactors)
    {
      // Simple wrap of the discount factors
      var calibrator = new DiscountRateCalibrator(asOf, asOf);

      var dcurve = new DiscountCurve(calibrator);
      dcurve.Interp = InterpFactory.FromMethod(
        InterpMethod.Weighted, ExtrapMethod.Const);
      for (int i = 0; i < count; i++)
      {
        Dt maturity = maturities(i);
        dcurve.AddZeroYield(maturity, RateCalc.RateFromPrice(
          discountFactors(i), asOf, maturity, dayCount, freq),
          dayCount, freq);
      }
      dcurve.Fit();
      return dcurve;
    }

    /// <summary>
    /// Europeans the option value.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="type">The type.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="sorted">if set to <c>true</c> [sorted].</param>
    /// <returns></returns>
    public static double EuropeanOptionValue(
      this IList<RateAnnuityProbability> list,
      OptionType type, double strike,
      bool sorted)
    {
      int count = list.Count;
      double pv = 0;
      if (type == OptionType.Call)
      {
        for (int i = count - 1; i >= 0; --i)
        {
          var rap = list[i];
          if (sorted && rap.Rate <= strike) break;
          pv += (rap.Rate - strike) * rap.Annuity * rap.Probability;
        }
      }
      else if (type == OptionType.Put)
      {
        for (int i = 0; i < count; ++i)
        {
          var rap = list[i];
          if (sorted && rap.Rate >= strike) break;
          pv += (strike - rap.Rate) * rap.Annuity * rap.Probability;
        }
      }
      else
      {
        for (int i = 0; i < count; ++i)
        {
          var rap = list[i];
          pv += (rap.Rate - strike) * rap.Annuity * rap.Probability;
        }
      }
      return pv;
    }

    /// <summary>
    /// Swaptions the value.
    /// </summary>
    /// <param name="rsd">The RSD.</param>
    /// <param name="type">The type.</param>
    /// <param name="dateIndex">Index of the date.</param>
    /// <param name="firstRateIndex">First index of the rate.</param>
    /// <param name="lastRateIndex">Last index of the rate.</param>
    /// <param name="strike">The strike.</param>
    /// <returns></returns>
    public static double CalculateSwaptionValue(
      this IRateSystemDistributions rsd,
      OptionType type,
      int dateIndex,
      int firstRateIndex, int lastRateIndex,
      double strike)
    {
      var list = ListUtil.CreateList(
        rsd.GetStateCount(dateIndex),
        (i) => new RateAnnuityProbability(
          rsd.GetSwapRateAnnuity(firstRateIndex, lastRateIndex,
          dateIndex, i), rsd.GetProbability(dateIndex, i)));
      return EuropeanOptionValue(list, type, strike, true);
    }

    /// <summary>
    /// Gets the swap rate annuity.
    /// </summary>
    /// <param name="rsd">The RSD.</param>
    /// <param name="firstRateIndex">First index of the rate.</param>
    /// <param name="lastRateIndex">Last index of the rate.</param>
    /// <param name="dateIndex">Index of the date.</param>
    /// <param name="stateIndex">Index of the state.</param>
    /// <returns></returns>
    public static RateAnnuity GetSwapRateAnnuity(
      this IRateSystemDistributions rsd,
      int firstRateIndex, int lastRateIndex,
      int dateIndex, int stateIndex)
    {
      var fractions = GetFractions(rsd).ToArray();
      return rsd.GetSwapRateAnnuity(firstRateIndex,
        lastRateIndex, dateIndex, stateIndex, fractions);
    }

    private static RateAnnuity GetSwapRateAnnuity(
      this IRateSystemDistributions rsd,
      int firstRateIndex, int lastRateIndex,
      int dateIndex, int stateIndex,
      double[] fractions)
    {
      var a0 = rsd.GetAnnuity(firstRateIndex,
        dateIndex, stateIndex) * (1 +
        fractions[firstRateIndex] * rsd.GetRate(
        firstRateIndex, dateIndex, stateIndex));
      double annuity = fractions.Sum(
        firstRateIndex, lastRateIndex + 1,
        (i, f) => f * rsd.GetAnnuity(i, dateIndex, stateIndex));
      double rate = (a0 - rsd.GetAnnuity(
        lastRateIndex, dateIndex, stateIndex)) / annuity;
      return new RateAnnuity(rate, annuity);
    }

  }

  public class TreeNodeRateCalculator : IRateCalculator
  {
    public DiscountCurve ProjectionCurve;

    public double GetRateAt(Dt resetDate, ReferenceIndex rateIndex)
    {
      if (resetDate.IsEmpty() || rateIndex == null || ProjectionCurve == null)
        return 0.0;

      var swapIndex = rateIndex as SwapRateIndex;
      if (swapIndex != null)
        return SwapRate(resetDate, swapIndex, ProjectionCurve);

      if (rateIndex is InterestRateIndex ||
        rateIndex.GetType() == typeof(ReferenceIndex))
      {
        return LiborRate(resetDate, rateIndex, ProjectionCurve);
      }

      throw new NotImplementedException();
    }

    /// <summary>
    /// Get the LIBOR rate.
    /// </summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="index">The index.</param>
    /// <param name="projectionCurve">The projection curve.</param>
    /// <returns>System.Double.</returns>
    public static double LiborRate(Dt fixingDate,
      ReferenceIndex index, DiscountCurve projectionCurve)
    {
      return (fixingDate < projectionCurve.AsOf &&
        index.HistoricalObservations != null)
        ? ObservedRate(fixingDate, index, projectionCurve, ProjectLiborRate)
        : ProjectLiborRate(fixingDate, index, projectionCurve);
    }

    /// <summary>
    /// Get the swap rate.
    /// </summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="index">The index.</param>
    /// <param name="projectionCurve">The projection curve.</param>
    /// <returns>System.Double.</returns>
    public static double SwapRate(Dt fixingDate,
      SwapRateIndex index, DiscountCurve projectionCurve)
    {
      return (fixingDate < projectionCurve.AsOf &&
        index.HistoricalObservations != null)
        ? ObservedRate(fixingDate, index, projectionCurve, ProjectLiborRate)
        : ProjectSwapRate(fixingDate, index, projectionCurve);
    }

    /// <summary>
    /// Get the observed rate.
    /// </summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="index">The index.</param>
    /// <param name="projectionCurve">The projection curve.</param>
    /// <param name="projectRate">Projection rate Func</param>
    /// <returns>System.Double.</returns>
    private static double ObservedRate(Dt fixingDate,
      ReferenceIndex index, DiscountCurve projectionCurve,
      Func<Dt, ReferenceIndex, DiscountCurve, double> projectRate)
    {
      var rateResets = index.HistoricalObservations
        .OrderByDescending(r => r.Date).ToList();
      if (rateResets.Count > 0)
      {
        var rateReset = rateResets.FirstOrDefault(r => r.Date <= fixingDate)
          ?? rateResets.Last();
        return rateReset.Rate;
      }
      return projectRate(fixingDate, index, projectionCurve);
    }

    /// <summary>
    ///  Projects the rate.
    /// </summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="refindex">The index.</param>
    /// <param name="projectionCurve">The projection curve.</param>
    /// <returns>System.Double.</returns>
    private static double ProjectSwapRate(Dt fixingDate,
      ReferenceIndex refindex, DiscountCurve projectionCurve)
    {
      var index = (SwapRateIndex)refindex;
      var begin = Dt.Roll(Dt.Add(fixingDate, index.SettlementDays),
        index.Roll, index.Calendar);
      var couponCalculator = new SwapRateCalculator(
        fixingDate, index, projectionCurve);
      return couponCalculator.Fixing(couponCalculator.GetFixingSchedule(
        Dt.Empty, begin, begin, Dt.Empty)).Forward;
    }

    /// <summary>
    /// Projects the rate.
    /// </summary>
    /// <param name="fixingDate">The fixing date.</param>
    /// <param name="index">The index.</param>
    /// <param name="projectionCurve">The projection curve.</param>
    /// <returns>System.Double.</returns>
    private static double ProjectLiborRate(Dt fixingDate,
      ReferenceIndex index, DiscountCurve projectionCurve)
    {
      var begin = Dt.Roll(Dt.Add(fixingDate, index.SettlementDays),
        index.Roll, index.Calendar);
      var end = Dt.Roll(Dt.Add(begin, index.IndexTenor),
        index.Roll, index.Calendar);
      var price = projectionCurve.Interpolate(begin, end);
      var freq = index.IndexTenor.ToFrequency();
      var frac = Dt.Fraction(begin, end, begin, end, index.DayCount, freq);
      return (1 / price - 1) / frac;
    }
  }
}
