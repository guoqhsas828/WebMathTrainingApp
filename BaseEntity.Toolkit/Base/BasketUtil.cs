/*
 * BasketUtil.cs
 *
 *   2005-2008. All rights reserved.
 *
 */

using System;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// collection of Utility methods for a group of credit curves
  /// </summary>
  public abstract class BasketUtil
  {

    /// <summary>
    ///   Returns a set of basket measures 
    /// </summary>
    /// 
    /// <remarks> 
    ///   <para>The parameter <paramref name="measure"/> has the following choices</para>
    ///   <list type="table">
    ///     <item><term>AvgSpread</term>
    ///       <description>Calculates the implied average spread of the basket</description></item>
    ///     <item><term>DurWeightedAvgSpread</term>
    ///       <description>Calculates the duration-weighted implied average spread of the basket</description></item>
    ///     <item><term>ExpectedLoss</term>
    ///       <description>Calculates the Expected Loss of the basket.</description></item>
    ///     <item><term>ExpectedLossPv</term>
    ///       <description>Calculates the Expected Loss PV of the basket.</description></item>
    ///   </list>
    /// 
    /// <para>Uses default CDS Terms 
    /// <list type="bullet">
    /// <item>Daycount = Actual360</item>
    /// <item>Frequency = Quarterly </item>
    /// <item>Roll = Modified</item>
    /// <item>Calendar = None</item>
    /// </list>
    /// </para>
    ///   The expected loss calculations are based on a 1 unit basket notional
    /// </remarks>
    ///
    /// <param name="maturity">Maturity of basket</param>
    /// <param name="survivalCurves">Survival Curves</param>
    /// <param name="weights">Participation levels in the basket</param>
    /// <param name="measure">Measure</param>
    /// 
    /// <returns>Measure Value</returns>
    public static double CalcBasketMeasure(Dt maturity, SurvivalCurve[] survivalCurves, double[] weights, BasketMeasure measure)
    {
      return CalcBasketMeasure(maturity, survivalCurves, weights, measure, CurveUtil.DEFAULT_DAYCOUNT, CurveUtil.DEFAULT_FREQUENCY,
                               CurveUtil.DEFAULT_ROLL, CurveUtil.DEFAULT_CALENDAR);
    }

    /// <summary>
    ///   Returns a set of basket measures 
    /// </summary>
    /// 
    /// <remarks> 
    ///   <para>The parameter <paramref name="measure"/> has the following choices</para>
    ///   <list type="table">
    ///     <item><term>AvgSpread</term>
    ///       <description>Calculates the implied average spread of the basket</description></item>
    ///     <item><term>DurWeightedAvgSpread</term>
    ///       <description>Calculates the duration-weighted implied average spread of the basket</description></item>
    ///     <item><term>ExpectedLoss</term>
    ///       <description>Calculates the Expected Loss of the basket.</description></item>
    ///     <item><term>ExpectedLossPv</term>
    ///       <description>Calculates the Expected Loss PV of the basket.</description></item>
    ///   </list>
    ///   The expected loss calculations are based on a 1 unit basket notional
    /// </remarks>
    ///
    /// <param name="maturity">Maturity of basket</param>
    /// <param name="survivalCurves">Survival Curves</param>
    /// <param name="weights">Participation levels in the basket</param>
    /// <param name="measure">Measure</param>
    /// <param name="dayCount">Daycount of premium accrual</param>
    /// <param name="frequency">Frequency of premium payment</param>
    /// <param name="roll">Business day convention for premium payment</param>
    /// <param name="calendar">Calendar for premium payment</param>
   /// 
    /// <returns>Measure Value</returns>
    public static double CalcBasketMeasure(Dt maturity, SurvivalCurve[] survivalCurves, double[] weights, BasketMeasure measure, DayCount dayCount, Frequency frequency, BDConvention roll, Calendar calendar)
    {
      switch (measure)
      {
        case BasketMeasure.AverageSpread:
          return CalcAvgImpliedSpread(maturity, survivalCurves, weights, dayCount, frequency, roll, calendar);
        case BasketMeasure.DurationWeightedAvgSpread:
          return CalcDurationWeightedAvgImpliedSpread(maturity, survivalCurves, weights, dayCount, frequency, roll, calendar);
        case BasketMeasure.DurationWeightedSpreadDispersion:
          return CalcDWeightedISpreadDispersion(maturity, survivalCurves, weights, dayCount, frequency, roll, calendar);
        case BasketMeasure.ExpectedLoss:
          return CalcExpectedLoss(maturity, survivalCurves, weights);
        case BasketMeasure.ExpectedLossPv:
          return CalcExpectedLossPv(maturity, survivalCurves, weights, dayCount, frequency, roll, calendar);
        default:
          throw new SystemException("The specified basket measure is not supported ");
      }
    }

    /// <summary>
    /// Calculates the average Duration Weighted Implied Spread of the names in the basket.
    /// </summary>
    /// <remarks><para>Uses default CDS Terms 
    /// <list type="bullet">
    /// <item>Daycount = Actual360</item>
    /// <item>Frequency = Quarterly </item>
    /// <item>Roll = Modified</item>
    /// <item>Calendar = None</item>
    /// </list>
    /// </para></remarks>
    /// <param name="maturity">Maturity of basket</param>
    /// <param name="survivalCurves">Survival Curves</param>
    /// <param name="weights">Participation levels in the basket</param>
    /// <returns>duration weight average implied spread in bps</returns>
    public static double CalcDurationWeightedAvgImpliedSpread(Dt maturity, SurvivalCurve[] survivalCurves, double[] weights)
    {
      return CalcDurationWeightedAvgImpliedSpread(maturity, survivalCurves, weights, CurveUtil.DEFAULT_DAYCOUNT,
                                                  CurveUtil.DEFAULT_FREQUENCY, CurveUtil.DEFAULT_ROLL, CurveUtil.DEFAULT_CALENDAR);
    }

    /// <summary>
    /// Calculates the average Duration Weighted Implied Spread of the names in the basket.
    /// </summary>
    /// <param name="maturity">Maturity of basket</param>
    /// <param name="survivalCurves">Survival Curves</param>
    /// <param name="weights">Participation levels in the basket</param>
    /// <param name="dayCount">Daycount of premium accrual</param>
    /// <param name="frequency">Frequency of premium payment</param>
    /// <param name="roll">Business day convention for premium payment</param>
    /// <param name="calendar">Calendar for premium payment</param>
    /// <returns>duration weight average implied spread in bps</returns>
    public static double CalcDurationWeightedAvgImpliedSpread(Dt maturity, SurvivalCurve[] survivalCurves, double[] weights, DayCount dayCount, Frequency frequency, BDConvention roll, Calendar calendar)
    {
      double sumWeights = 0;
      int N = survivalCurves.Length;

      NormalizeWeights(ref weights, ref sumWeights, N);

      double weightedSpread = 0;
      double durationSum = 0.0;

      for (int i = 0; i < N; i++)
      {
        double weight = weights == null ? 1.0/N : weights[i]/sumWeights;
        double spread = 0;
        double duration = 0;
        SurvivalCurve curve = survivalCurves[i];
        if (curve.Defaulted != Defaulted.HasDefaulted)
        {
          Dt settle = curve.Calibrator.Settle;
          if (curve.Defaulted == Defaulted.WillDefault && curve.DefaultDate <= settle)
            continue;
          if (curve.Tenors.Count == 0)
            continue;
          if(curve.AsOf >= maturity)
            return 0.0;

          spread = CurveUtil.ImpliedSpread(curve, maturity, dayCount, frequency, roll, calendar);
          duration = CurveUtil.ImpliedDuration(curve, curve.AsOf, maturity, dayCount, frequency, roll, calendar);
        }

        weightedSpread += duration*spread*weight;
        durationSum += duration*weight;
      }
      weightedSpread /= durationSum;

      return weightedSpread;
    }



    /// <summary>
    /// Calculates the Duration Weighted Implied Spread Dispersion of the names in the basket.
    /// </summary>
    /// <param name="maturity">Maturity of basket</param>
    /// <param name="survivalCurves">Survival Curves</param>
    /// <param name="weights">Participation levels in the basket</param>
    /// <param name="dayCount">Daycount of premium accrual</param>
    /// <param name="frequency">Frequency of premium payment</param>
    /// <param name="roll">Business day convention for premium payment</param>
    /// <param name="calendar">Calendar for premium payment</param>
    /// <returns>duration weight average implied spread in bps</returns>
    /// <remarks>The fomula we use to calculate is: 
    /// <formula>\sqrt{\frac{\sum{w_i s_i^2} * \sum{w_i}-(\sum{w_is_i})^2}{(\sum{w_i})^2}}</formula>
    /// where <m>w_i</m> is the weights[i] * duration, <m>s_i</m> is the spread of the ith name.
    /// </remarks>

    public static double CalcDWeightedISpreadDispersion(
      Dt maturity, SurvivalCurve[] survivalCurves, double[] weights, 
      DayCount dayCount, Frequency frequency, BDConvention roll, Calendar calendar)
    {
      double sumWeights = 0;
      int N = survivalCurves.Length;

      NormalizeWeights(ref weights, ref sumWeights, N);

      double wSpread1 = 0;
      double wSpread2 = 0;
      double dWeightSum = 0.0;

      for (int i = 0; i < N; i++)
      {
        double spread = 0;
        double duration = 0;
        double weight = weights == null ? 1.0 / N : weights[i] / sumWeights;
        SurvivalCurve curve = survivalCurves[i];
        if (curve.Defaulted != Defaulted.HasDefaulted)
        {
          Dt settle = curve.Calibrator.Settle;
          if (curve.Defaulted == Defaulted.WillDefault && curve.DefaultDate <= settle)
            continue;
          if (curve.Tenors.Count == 0)
            continue;
          if (curve.AsOf >= maturity)
            return 0.0;

          spread = CurveUtil.ImpliedSpread(curve, maturity, dayCount, frequency, roll, calendar);
          duration = CurveUtil.ImpliedDuration(curve, curve.AsOf, maturity, dayCount, frequency, roll, calendar);
        }

        double dWeight = duration * weight;
        dWeightSum += dWeight;
        wSpread1 += dWeight * spread;
        wSpread2 += dWeight * spread * spread;
      }

      return Math.Sqrt((wSpread2 * dWeightSum - wSpread1 * wSpread1) / (dWeightSum * dWeightSum));
    }

    /// <summary>
    /// Calculates the average Implied Spread of the names in the basket.
    /// </summary>
    /// <remarks><para>Uses default CDS Terms 
    /// <list type="bullet">
    /// <item>Daycount = Actual360</item>
    /// <item>Frequency = Quarterly </item>
    /// <item>Roll = Modified</item>
    /// <item>Calendar = None</item>
    /// </list>
    /// </para></remarks>
    /// <param name="maturity">Maturity of basket</param>
    /// <param name="survivalCurves">Survival Curves</param>
    /// <param name="weights">Participation levels in the basket</param>
    /// <returns>weighted average implied spread in bps</returns>
    public static double CalcAvgImpliedSpread(Dt maturity, SurvivalCurve[] survivalCurves, double[] weights)
    {
      return CalcAvgImpliedSpread(maturity, survivalCurves, weights, CurveUtil.DEFAULT_DAYCOUNT, CurveUtil.DEFAULT_FREQUENCY, CurveUtil.DEFAULT_ROLL,
                                  CurveUtil.DEFAULT_CALENDAR);
    }

    /// <summary>
    /// Calculates the average Implied Spread of the names in the basket.
    /// </summary>
    /// <param name="maturity">Maturity of basket</param>
    /// <param name="survivalCurves">Survival Curves</param>
    /// <param name="weights">Participation levels in the basket</param>
    /// <param name="dayCount">Daycount of premium accrual</param>
    /// <param name="frequency">Frequency of premium payment</param>
    /// <param name="roll">Business day convention for premium payment</param>
    /// <param name="calendar">Calendar for premium payment</param>
    /// <returns>weighted average implied spread in bps</returns>
    public static double CalcAvgImpliedSpread(Dt maturity, SurvivalCurve[] survivalCurves, double[] weights, DayCount dayCount, Frequency frequency, BDConvention roll, Calendar calendar)
    {
      double sumWeights = 0;
      double remainingNotional = 0; 

      int N = survivalCurves.Length;

      NormalizeWeights(ref weights, ref sumWeights, N);

      double weightedSpread = 0;

      for (int i = 0; i < survivalCurves.Length; i++)
      {
        double weight = weights == null ? 1.0/N : weights[i]/sumWeights;
        SurvivalCurve curve = survivalCurves[i];
        if (curve.Defaulted != Defaulted.HasDefaulted)
        {
          Dt settle = curve.Calibrator.Settle;
          if (curve.Defaulted == Defaulted.WillDefault && curve.DefaultDate <= settle)
            continue;
          if (curve.Tenors.Count == 0)
            continue;
          if (curve.AsOf >= maturity)
            return 0.0;

          double spread = CurveUtil.ImpliedSpread(curve, maturity, dayCount, frequency, roll, calendar);
          weightedSpread += spread * weight;
          remainingNotional += weight;
        }
        
      }
      return weightedSpread/remainingNotional;
    }

    /// <summary>
    /// Calculates the ExpectedLoss for the basket of names.
    /// </summary>
    /// <param name="maturity">Maturity of basket</param>
    /// <param name="survivalCurves">Survival Curves</param>
    /// <param name="weights">Participation levels in the basket</param>
    /// <returns>The expected loss based on a 1 unit basket notional</returns>
    public static double CalcExpectedLoss(Dt maturity, SurvivalCurve[] survivalCurves, double[] weights)
    {
      double sumWeights = 0;
      int N = survivalCurves.Length;

      NormalizeWeights(ref weights, ref sumWeights, N);

      double sum = 0;

      for (int i = 0; i < survivalCurves.Length; ++i)
      {
        double principal = weights == null ? 1.0/N : (double) weights[i]/sumWeights;
        double recovery = survivalCurves[i].SurvivalCalibrator.RecoveryCurve.RecoveryRate(maturity);
        sum += principal*survivalCurves[i].DefaultProb(maturity)*(1 - recovery);
      }
      return sum;
    }

    /// <summary>
    /// Calculates the ExpectedLossPv for the basket of names.
    /// </summary>
    /// <param name="maturity">Maturity of basket</param>
    /// <param name="survivalCurves">Survival Curves</param>
    /// <param name="weights">Participation levels in the basket</param>
    /// <param name="dayCount">Daycount of premium accrual</param>
    /// <param name="frequency">Frequency of premium payment</param>
    /// <param name="roll">Business day convention for premium payment</param>
    /// <param name="calendar">Calendar for premium payment</param>
    /// <returns>The expected loss PV based on a 1 unit basket notional</returns>
    public static double CalcExpectedLossPv(Dt maturity, SurvivalCurve[] survivalCurves, double[] weights, DayCount dayCount, Frequency frequency, BDConvention roll, Calendar calendar)
    {
      double sumWeights = 0;
      int N = survivalCurves.Length;

      NormalizeWeights(ref weights, ref sumWeights, N);

      double sum = 0;

      for (int i = 0; i < N; ++i)
      {
        double principal = weights == null ? 1.0 / N : (double)weights[i] / sumWeights;
        double spread = 0;
        double duration = 0;
        SurvivalCurve curve = survivalCurves[i];
        if (curve.Defaulted != Defaulted.HasDefaulted)
        {
          Dt settle = curve.Calibrator.Settle;
          if (curve.Defaulted == Defaulted.WillDefault && curve.DefaultDate <= settle)
            continue;
          if (curve.Tenors.Count == 0)
            continue;
          if (curve.AsOf >= maturity)
            return 0.0;

          spread = CurveUtil.ImpliedSpread(curve, maturity, dayCount, frequency, roll, calendar);
          duration = CurveUtil.ImpliedDuration(curve, curve.AsOf, maturity, dayCount, frequency, roll, calendar);
        }

        sum += principal * spread * duration;
      }
      return sum;
    }


    /// <summary>
    /// Calculates the ExpectedLossPv for the basket of names.
    /// </summary>
    /// <remarks>
    /// <para>Uses default CDS Terms 
    /// <list type="bullet">
    /// <item>Daycount = Actual360</item>
    /// <item>Frequency = Quarterly </item>
    /// <item>Roll = Modified</item>
    /// <item>Calendar = None</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="maturity">Maturity of basket</param>
    /// <param name="survivalCurves">Survival Curves</param>
    /// <param name="weights">Participation levels in the basket</param>
    /// <returns>The expected loss PV based on a 1 unit basket notional</returns>
    public static double CalcExpectedLossPv(Dt maturity, SurvivalCurve[] survivalCurves, double[] weights)
    {
      return CalcExpectedLossPv(maturity, survivalCurves, weights, CurveUtil.DEFAULT_DAYCOUNT, CurveUtil.DEFAULT_FREQUENCY, CurveUtil.DEFAULT_ROLL, CurveUtil.DEFAULT_CALENDAR);
    }

    private static void NormalizeWeights(ref double[] weights, ref double sumWeights, int N)
    {
      // normalize weights
      if (weights != null)
      {
        if (weights.Length != N)
          throw new ArgumentException(" A weight for each name is required ");
        for (int i = 0; i < N; ++i)
          sumWeights += weights[i];
      }
      else
      {
        weights = new double[N];
        for (int i = 0; i < N; ++i)
        {
          weights[i] = 1.0/N;
        }
        sumWeights = 1.0;
      }
    }

  }
}