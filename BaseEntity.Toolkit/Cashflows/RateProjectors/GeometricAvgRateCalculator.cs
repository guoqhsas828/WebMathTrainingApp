using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// OIS coupon calculator
  /// </summary>
  [Serializable]
  public abstract class GeometricAvgRateCalculator : CouponCalculator
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">AsOf Date</param>
    /// <param name="referenceIndex">Reference index object</param>
    /// <param name="referenceCurve">Reference rate curve used for the calculation of the rate.</param>
    protected GeometricAvgRateCalculator(Dt asOf, InterestRateIndex referenceIndex, DiscountCurve referenceCurve) :
      base(asOf, referenceIndex, referenceCurve)
    {
      Weighted = ReferenceIndex.PublicationFrequency == Frequency.Daily ||
                 ReferenceIndex.PublicationFrequency == Frequency.None;
      CutOff = 0;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Fixing function for given rate
    /// </summary>
    /// <param name="liborRate">libor rate</param>
    /// <param name="start">Start date</param>
    /// <param name="end">End date</param>
    /// <returns>Single component of the average</returns>
    public abstract double FixingFn(double liborRate, Dt start, Dt end);

    internal static double DaysInYear(DayCount dc)
    {
      double daysInYear;
      switch (dc)
      {
        case DayCount.Actual365Fixed:
          daysInYear = 365.0;
          break;
        case DayCount.Actual360:
        case DayCount.Thirty360:
        case DayCount.Thirty360Isma:
        case DayCount.ThirtyE360:
        case DayCount.ThirtyEP360:
          daysInYear = 360.0;
          break;
        case DayCount.Actual366:
          daysInYear = 366.0;
          break;
        default:
          daysInYear = 360.0;
          break;
      }
      return daysInYear;
    }


    private static double ApproxGeometricAverage(List<double> rates, List<double> wts, List<Dt> start, List<Dt> end,
                                                 List<RateResetState> resetStates,
                                                 ReferenceIndex index, Func<double, Dt, Dt, double> rateFn)
    {
      double days = DaysInYear(index.DayCount);
      double n = 0.0;
      double avg = 1.0;
      int idx = 0;
      for (int i = 0; i < rates.Count; ++i)
      {
        if (resetStates[i] == RateResetState.IsProjected)
          break;
        ++idx;
        avg *= (1 + wts[i]/days*rateFn(rates[i], start[i], end[i]));
        n += wts[i];
      }
      double integral = 0.0; //quadrature portion
      for (int i = idx; i < rates.Count; ++i)
      {
        integral += wts[i]*rateFn(rates[i], start[i], end[i]);
        n += wts[i];
      }
      return (avg*Math.Exp(integral/days) - 1.0)*days/n;
    }


    private static double GeometricAverage(List<double> rates, List<double> wts, List<Dt> start, List<Dt> end,
                                           List<RateResetState> resetStates,
                                           ReferenceIndex index, Func<double, Dt, Dt, double> rateFn)
    {
      double n = 0.0;
      double avg = 1.0;
      double days = DaysInYear(index.DayCount);
      for (int i = 0; i < rates.Count; ++i)
      {
        avg *= (1 + wts[i]/days*rateFn(rates[i], start[i], end[i]));
        n += wts[i];
      }
      return (avg - 1.0)*days/n;
    }

    /// <summary>
    /// Fixing on reset 
    /// </summary>
    /// <param name="fixingSchedule">fixing schedule</param>
    /// <returns></returns>
    public override Fixing Fixing(FixingSchedule fixingSchedule)
    {
      if (Approximate)
        return RateAveragingUtils.AveragedRateFixing(AsOf, fixingSchedule, ReferenceIndex, ReferenceCurve, DiscountCurve,
                                                     HistoricalObservations, UseAsOfResets, FixingFn,
                                                     ApproxGeometricAverage);
      return RateAveragingUtils.AveragedRateFixing(AsOf, fixingSchedule, ReferenceIndex, ReferenceCurve, DiscountCurve,
                                                   HistoricalObservations, UseAsOfResets, FixingFn, GeometricAverage);
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
      if (Approximate)
        return RateAveragingUtils.InitializeApproxFixingSchedule(AsOf, periodStart, periodEnd, ReferenceIndex, ResetLag,
                                                                 Weighted, CutOff);
      return RateAveragingUtils.InitializeFixingSchedule(periodStart, periodEnd, ReferenceIndex, ResetLag, Weighted,
                                                         CutOff);
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

    #endregion

    #region Properties

    /// <summary>
    /// Approximate average with high sampling frequency by numerical quadrature rules
    /// </summary>
    public bool Approximate { get; set; }

    /// <summary>
    /// Weighted
    /// </summary>
    public bool Weighted { get; set; }

    /// <summary>
    /// Cutoff period for averaging rate
    /// </summary>
    public int CutOff { get; set; }

    #endregion
  }

  /// <summary>
  /// OIS rate calculator
  /// </summary>
  /// <remarks>
  /// <para>The rate for a Reset Date, will be the rate of return of a daily compound interest investment.</para>
  /// <math>
  ///   \left[ \prod_{i=1}^{n_0} \left( 1 + \frac{R_i * n_i}{P} \right) - 1 \right] * \frac{P}{d}
  /// </math>
  ///   <para>where</para>
  ///   <list type="bullet">
  ///			<item><description><m>n_0</m> is the number of business days in the calculation period</description></item>
  ///			<item><description><m>R_i</m> is OIS reference rate</description></item>
  ///     <item><description><m>n_i</m> is 1, except where the business day is the day immediately preceding
  ///       a day which is not a business day, in which case it is the number of calendar days from, and
  ///       including, that business day to, but excluding, the next business day</description></item>
  ///     <item><description><m>P</m> is the number of days in a year for daycount purposes (eg 360 or 365).</description></item>
  ///     <item><description><m>d</m> is the number of calendar days in the calculation period</description></item>
  ///   </list>
  /// </remarks>
  [Serializable]
  public class OisRateCalculator : GeometricAvgRateCalculator
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">AsOf Date</param>
    /// <param name="referenceIndex">Reference index object</param>
    /// <param name="referenceCurve">Reference rate curve used for the calculation of the rate.</param>
    public OisRateCalculator(Dt asOf, InterestRateIndex referenceIndex, DiscountCurve referenceCurve) :
      base(asOf, referenceIndex, referenceCurve)
    {
      Weighted = ReferenceIndex.PublicationFrequency == Frequency.Daily ||
                 ReferenceIndex.PublicationFrequency == Frequency.None;
      CutOff = 0;
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
  }
}