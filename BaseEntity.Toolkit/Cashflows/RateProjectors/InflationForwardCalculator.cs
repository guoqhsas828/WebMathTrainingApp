using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows
{

  #region IndexationMethod

  ///<summary>
  /// The method to calculation index level for inflation
  ///</summary>
  public enum IndexationMethod
  {
    ///<summary>
    /// None
    ///</summary>
    None,

    ///<summary>
    /// The current international methodology, use interpolated RPI/CPI figures
    ///</summary>
    CanadianMethod,

    ///<summary>
    /// UK-Gilt inflation bond issued before year 2005 use static RPI index from 1st of the month in 8 months lag
    ///</summary>
    UKGilt_OldStyle,

    ///<summary>
    /// Australian Capital Indexed Bond method, K factors generated from  pairs of lagged rates
    ///</summary>
    AustralianCIB
  }

  #endregion

  #region InflationUtils

  /// <summary>
  /// Inflation utilities
  /// </summary>
  internal static class InflationUtils
  {
    private static double Average(Dt date, double p0, double p1)
    {
      var start = new Dt(1, date.Month, date.Year);
      Dt end = Dt.AddMonth(start, 1, false);
      double factor = Dt.Diff(start, date) / (double)Dt.Diff(start, end);
      return p0 * (1 - factor) + factor * p1;
    }

    /// <summary>
    /// Find historical inflation reset
    /// </summary>
    /// <param name="fixingDt">Reset/release date</param>
    /// <param name="asOf">As of date</param>
    /// <param name="inflationIndex">Inflation index</param>
    /// <param name="resetState">Overwritten by reset state</param>
    /// <returns>Historical reset or 0 if reset is projected</returns>
    internal static double FindReset(Dt fixingDt, Dt asOf, InflationIndex inflationIndex, out RateResetState resetState)
    {
      if (inflationIndex.HistoricalObservations != null)
      {
        bool found;
        double reset = inflationIndex.HistoricalObservations.GetRate(fixingDt, out found);
        if (found)
        {
          resetState = RateResetState.ObservationFound;
          return reset;
        }
      }
      if (fixingDt > asOf)
      {
        resetState = RateResetState.IsProjected;
        return 0.0;
      }
      resetState = RateResetState.Missing;
      return 0.0;
    }

    /// <summary>
    /// Calculate inflation fixing at reset
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="fixingDt">Reset date</param>
    /// <param name="inflationCurve">Forward inflation curve</param>
    /// <param name="inflationIndex">Inflation index</param>
    /// <param name="state">Reset state</param>
    /// <returns>Fixing</returns>
    internal static double ForwardInflationAtReset(Dt asOf, Dt fixingDt, InflationIndex inflationIndex, CalibratedCurve inflationCurve, out RateResetState state)
    {
      double price = FindReset(fixingDt, asOf, inflationIndex, out state);
      if (state == RateResetState.IsProjected)
        return inflationCurve.Interpolate(fixingDt);
      if (state == RateResetState.Missing)
        throw new ArgumentException(String.Format("Fixing resetting on date {0} is missing.", fixingDt));
      return price;
    }

    /// <summary>
    /// Publication date
    /// </summary>
    /// <param name="effective">Inflation effective</param>
    /// <param name="publicationFrequency">Publication frequency</param>
    /// <param name="publicationLag">Publication lag</param>
    /// <returns>Publication date</returns>
    internal static Dt PublicationDate(Dt effective, Frequency publicationFrequency, Tenor publicationLag)
    {
      if (!publicationLag.IsEmpty)
        effective = Dt.Add(effective, publicationLag);
      return effective;
    }

    /// <summary>
    /// The inflation fixing is constant during InflationPeriod     
    /// </summary>
    /// <param name="resetDate">Reset date</param>
    /// <param name="publicationFrequency">Reset frequency</param>
    /// <param name="publicationLag">Lag between inflation effective </param>
    /// <param name="indexationMethod">Indexation method</param>
    /// <returns>Bracketing period</returns>
    ///<remarks>
    /// Tuple.Item1 is the effective date of the inflation, which by convention is set to the first day of the observation period, 
    /// Tuple.Item2 is the (approximate) publication date of the inflation index relative to Tuple.Item1, which is publicationLag 
    /// after the end of the observation period  
    /// For instance, if publicationFrequency is monthly, the inflation effective date for month of January is January 1, 
    /// and if publicationLag is 3 weeks, publication date is February 21</remarks>
    internal static Dt[] InflationPeriod(Dt resetDate, Frequency publicationFrequency, Tenor publicationLag, IndexationMethod indexationMethod)
    {
      int month = resetDate.Month;
      int year = resetDate.Year;
      int startMonth;
      switch (publicationFrequency)
      {
        case Frequency.Annual:
          startMonth = 1;
          break;
        case Frequency.SemiAnnual:
          startMonth = 6 * ((month - 1) / 6) + 1;
          break;
        case Frequency.Quarterly:
          startMonth = 3 * ((month - 1) / 3) + 1;
          break;
        case Frequency.None:
        case Frequency.Monthly:
          startMonth = month;
          break;
        default:
          throw new ArgumentException("Frequency not handled");
      }
      var startDate = new Dt(1, startMonth, year);
      if (indexationMethod == IndexationMethod.UKGilt_OldStyle)
        return new[] {startDate};
      if(indexationMethod == IndexationMethod.AustralianCIB)
        return new[] { Dt.Add(Dt.Add(startDate, -1, TimeUnit.Days), publicationFrequency, true) };
      Dt endDate = Dt.AddMonth(startDate, 1, false);
      return new[] {startDate, endDate};
    }

    /// <summary>
    /// Inflation forward fixing
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="releaseDates">reset dates</param>
    /// <param name="pay">Payment date</param>
    /// <param name="inflationCurve">Inflation curve</param>
    /// <param name="inflationIndex">Inflation index</param>
    /// <param name="resetState">Reset state</param>
    /// <returns>Inflation fixing</returns>
    internal static double InflationForwardFixing(Dt asOf, Dt[] releaseDates, Dt pay, CalibratedCurve inflationCurve,
                                                  InflationIndex inflationIndex, out RateResetState resetState)
    {
      RateResetState s0, s1;
      double p0 = ForwardInflationAtReset(asOf, releaseDates[0], inflationIndex, inflationCurve, out s0);
      if (releaseDates.Length == 1)
      {
        resetState = s0;
        return p0;
      }
      double p1 = ForwardInflationAtReset(asOf, releaseDates[1], inflationIndex, inflationCurve, out s1);
      if (s0 == RateResetState.Missing || s1 == RateResetState.Missing)
        resetState = RateResetState.Missing;
      else if (s0 == RateResetState.IsProjected || s1 == RateResetState.IsProjected)
        resetState = RateResetState.IsProjected;
      else
        resetState = RateResetState.ObservationFound;
      return Average(pay, p0, p1);
    }
  }

  #endregion

  #region InflationForwardCalculator

  /// <summary>
  /// Inflation index calculator
  /// </summary>
  [Serializable]
  public class InflationForwardCalculator : CouponCalculator
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">AsOf Date</param>
    /// <param name="referenceIndex">Reference index object</param>
    /// <param name="referenceCurve">Reference rate curve used for the calculation of the forward inflation price. </param>
    /// <param name="indexationMethod">Indexation method</param>
    public InflationForwardCalculator(Dt asOf, InflationIndex referenceIndex, InflationCurve referenceCurve,
                                      IndexationMethod indexationMethod)
      : base(asOf, referenceIndex, referenceCurve)
    {
      IndexationMethod = indexationMethod;
    }

    #endregion Constructors

    #region Properties

    private InflationIndex InflationIndex
    {
      get { return (InflationIndex)ReferenceIndex; }
    }

    /// <summary>
    /// Indexation method
    /// </summary>
    public IndexationMethod IndexationMethod { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    /// Fixing on reset 
    /// </summary>
    /// <param name="fixingSchedule">fixing schedule</param>
    /// <returns></returns>
    public override Fixing Fixing(FixingSchedule fixingSchedule)
    {
      var sched = (ForwardInflationFixingSchedule)fixingSchedule;
      var retVal = new Fixing();
      RateResetState state;
      var laggedAsOf = RateResetUtil.ResetDate(AsOf, null, ResetLag);
      retVal.Forward = InflationUtils.InflationForwardFixing(laggedAsOf, sched.FixingDates, sched.EndDate, ReferenceCurve, InflationIndex, out state);
      retVal.RateResetState = state;
      return retVal;
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
      Dt fixingDate = RateResetUtil.ResetDate(periodEnd, null, ResetLag);
      var inflationPeriod = InflationUtils.InflationPeriod(fixingDate, InflationIndex.PublicationFrequency, InflationIndex.PublicationLag, IndexationMethod);
      var fixingSchedule = new ForwardInflationFixingSchedule
                           {
                             EndDate = payDt,
                             FixingDates = inflationPeriod,
                             ResetDate = inflationPeriod.Last()
                           };
      return fixingSchedule;
    }

    /// <summary>
    /// Rate reset information
    /// </summary>
    /// <param name="schedule">Fixing schedule</param>
    /// <returns> Reset info for each component of the fixing</returns>
    public override List<RateResets.ResetInfo> GetResetInfo(FixingSchedule schedule)
    {
      var resetInfos = new List<RateResets.ResetInfo>();
      var sched = (ForwardInflationFixingSchedule)schedule;
      foreach (var dt in sched.FixingDates)
      {
        RateResetState s;

        double p = InflationUtils.FindReset(dt, AsOf, InflationIndex, out s);
        resetInfos.Add(new RateResets.ResetInfo(dt, p, s));
      }
      return resetInfos;
    }

    #endregion Methods
  }

  #endregion
}