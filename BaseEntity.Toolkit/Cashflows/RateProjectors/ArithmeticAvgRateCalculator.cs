using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Generic arithmetic average rate
  /// </summary>
  [Serializable]
  public abstract class ArithmeticAvgRateCalculator : CouponCalculator
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">AsOf Date</param>
    /// <param name="referenceIndex">Reference index object</param>
    /// <param name="referenceCurve">Reference rate curve used for the calculation of the rate.</param>
    protected ArithmeticAvgRateCalculator(Dt asOf, ReferenceIndex referenceIndex, CalibratedCurve referenceCurve) :
      base(asOf, referenceIndex, referenceCurve)
    {
      Weighted = ReferenceIndex == null || (ReferenceIndex.PublicationFrequency == Frequency.Daily ||
                                            ReferenceIndex.PublicationFrequency == Frequency.None);
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


    private static double ArithmeticAverage(List<double> rates, List<double> wts, List<Dt> start, List<Dt> end,
                                            List<RateResetState> resetStates, ReferenceIndex index,
                                            Func<double, Dt, Dt, double> rateFn)
    {
      double n = 0.0;
      double avg = 0.0;
      for (int i = 0; i < rates.Count; ++i)
      {
        avg += wts[i]*rateFn(rates[i], start[i], end[i]);
        n += wts[i];
      }
      return avg/n;
    }

    /// <summary>
    /// Fixing on reset 
    /// </summary>
    /// <param name="fixingSchedule">fixing schedule</param>
    /// <returns></returns>
    public override Fixing Fixing(FixingSchedule fixingSchedule)
    {
      return RateAveragingUtils.AveragedRateFixing(AsOf, fixingSchedule, ReferenceIndex, ReferenceCurve, DiscountCurve,
                                                   HistoricalObservations, UseAsOfResets, FixingFn, ArithmeticAverage);
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
  /// Fed funds rate calculator
  /// </summary>
  [Serializable]
  public class FedFundsRateCalculator : ArithmeticAvgRateCalculator
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">AsOf Date</param>
    /// <param name="referenceIndex">Reference index object</param>
    /// <param name="referenceCurve">Reference rate curve used for the calculation of the rate.</param>
    public FedFundsRateCalculator(Dt asOf, ReferenceIndex referenceIndex, DiscountCurve referenceCurve) :
      base(asOf, referenceIndex, referenceCurve)
    {
      CutOff = 0;
      Weighted = true;
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

  /// <summary>
  /// Commercial Paper rate calculator
  /// </summary>
  [Serializable]
  public class CpRateCalculator : ArithmeticAvgRateCalculator
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">AsOf Date</param>
    /// <param name="referenceIndex">Reference index object</param>
    /// <param name="referenceCurve">Reference rate curve used for the calculation of the rate.</param>
    public CpRateCalculator(Dt asOf, InterestRateIndex referenceIndex, DiscountCurve referenceCurve) :
      base(asOf, referenceIndex, referenceCurve)
    {
      Weighted = false;
      CutOff = 2;
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
      return (1.0/(1.0 - rate/12.0) - 1.0)*12.0;
    }
  }

  /// <summary>
  /// T-Bill rate calculator
  /// </summary>
  [Serializable]
  public class TBillRateCalculator : ArithmeticAvgRateCalculator
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">AsOf Date</param>
    /// <param name="referenceIndex">Reference index object</param>
    /// <param name="referenceCurve">Reference rate curve used for the calculation of the rate.</param>
    public TBillRateCalculator(Dt asOf, InterestRateIndex referenceIndex, DiscountCurve referenceCurve) :
      base(asOf,
      new InterestRateIndex(referenceIndex.IndexName, referenceIndex.IndexTenor, referenceIndex.Currency, referenceIndex.DayCount,
        referenceIndex.Calendar, referenceIndex.Roll, Frequency.Weekly, CycleRule.Monday, referenceIndex.SettlementDays)
        { HistoricalObservations = referenceIndex.HistoricalObservations }, referenceCurve)
    {
      Weighted = true;
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
      // conversion to BEY
      // see e.g. http://www.bondtutor.com/btchp4/topic4/topic4.htm
      // or http://mortgage-x.com/general/indexes/t-bill_index_faq.asp
      // or http://www.iijournals.com/doi/abs/10.3905/jpm.9.3.58
      return (365*rate)/(360 - (rate*Dt.Diff(start, end)));
    }
  }
}