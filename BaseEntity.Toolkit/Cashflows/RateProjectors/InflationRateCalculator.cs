using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Calculator of forward inflation rate
  /// </summary>
  [Serializable]
  public class InflationRateCalculator : CouponCalculator
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">AsOf Date</param>
    /// <param name="referenceIndex">Reference index object</param>
    /// <param name="rateTenor">Rate tenor. </param>
    /// <param name="referenceCurve">Reference rate curve used for the calculation of the forward inflation price. </param>
    /// <param name="indexationMethod">Indexation method</param>
    public InflationRateCalculator(Dt asOf, InflationIndex referenceIndex, Tenor rateTenor, InflationCurve referenceCurve,
                                   IndexationMethod indexationMethod)
      : base(asOf, referenceIndex, referenceCurve)
    {
      RateTenor = rateTenor.IsEmpty ? referenceIndex.IndexTenor : rateTenor;
      IndexationMethod = indexationMethod;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Fixing on reset 
    /// </summary>
    /// <param name="fixingSchedule">fixing schedule</param>
    /// <returns>Fixing at reset date</returns>
    public override Fixing Fixing(FixingSchedule fixingSchedule)
    {
      var sched = (InflationRateFixingSchedule)fixingSchedule;
      var retVal = new InflationRateFixing();
      RateResetState sd, sn;
      double d;
      var laggedAsOf = RateResetUtil.ResetDate(AsOf, null, ResetLag);
      if (ZeroCoupon && !IsLagged(sched.PreviousFixingDates, laggedAsOf))
      {
        d = ((InflationCurve)ReferenceCurve).SpotInflation;
        sd = RateResetState.ObservationFound;
      }
      else
        d = InflationUtils.InflationForwardFixing(laggedAsOf, sched.PreviousFixingDates,
                                                  sched.StartDate, ReferenceCurve, InflationIndex,
                                                  out sd);
      if (d <= 0)
        return retVal;
      double n = InflationUtils.InflationForwardFixing(laggedAsOf, sched.FixingDates, sched.EndDate,
                                                       ReferenceCurve, InflationIndex, out sn);
      retVal.DenominatorResetState = sd;
      retVal.NumeratorResetState = sn;
      if (sd == RateResetState.Missing || sn == RateResetState.Missing)
        retVal.RateResetState = RateResetState.Missing;
      else if (sd == RateResetState.IsProjected || sn == RateResetState.IsProjected)
        retVal.RateResetState = RateResetState.IsProjected;
      else
        retVal.RateResetState = RateResetState.ObservationFound;
      double accrualFactor = sched.Frac;
      retVal.Forward = (n / d - 1.0) / accrualFactor;
      retVal.InflationAtPayDt = n;
      retVal.InflationAtPreviousPayDt = d;
      return retVal;
    }

    static bool IsLagged(Dt[] dates, Dt asOf)
    {
      return (!dates.IsNullOrEmpty()) && dates.Max() < asOf;
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
      var fixingSchedule = new InflationRateFixingSchedule { EndDate = payDt };
      if (ZeroCoupon || RateTenor.IsEmpty)
      {
        fixingSchedule.StartDate = prevPayDt;
        fixingSchedule.Frac = Dt.Fraction(periodStart, periodEnd, ReferenceIndex.DayCount); 
      }
      else
      {
        Tenor tenor = RateTenor;
        fixingSchedule.StartDate = Dt.Add(payDt, -tenor.N, tenor.Units);
        fixingSchedule.Frac = tenor.Years; //typically YoY 
      }
      fixingSchedule.FixingDates = InflationUtils.InflationPeriod(
        RateResetUtil.ResetDate(fixingSchedule.EndDate, null, ResetLag), InflationIndex.PublicationFrequency, InflationIndex.PublicationLag, IndexationMethod);
      fixingSchedule.PreviousFixingDates = InflationUtils.InflationPeriod(
        RateResetUtil.ResetDate(fixingSchedule.StartDate, null, ResetLag),
        InflationIndex.PublicationFrequency, InflationIndex.PublicationLag, IndexationMethod);
      fixingSchedule.ResetDate = fixingSchedule.FixingDates.Last();
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
      var sched = (InflationRateFixingSchedule)schedule;

      if (ZeroCoupon)
        resetInfos.Add(new RateResets.ResetInfo(AsOf, ((InflationCurve)ReferenceCurve).SpotInflation,
                                                RateResetState.ObservationFound));
      else
      {
        foreach (var dt in sched.PreviousFixingDates)
        {
          RateResetState s;
          double p = InflationUtils.FindReset(dt, AsOf, InflationIndex, out s);
          resetInfos.Add(new RateResets.ResetInfo(dt, p, s));
        }
      }
      foreach (var dt in sched.FixingDates)
      {
        RateResetState s;
        double p = InflationUtils.FindReset(dt, AsOf, InflationIndex, out s);
        resetInfos.Add(new RateResets.ResetInfo(dt, p, s));
      }
      return resetInfos;
    }

    #endregion Methods

    #region Properties

    private InflationIndex InflationIndex
    {
      get { return (InflationIndex)ReferenceIndex; }
    }

    /// <summary>
    /// Indexation method for computation of forward inflation fixing
    /// </summary>
    public IndexationMethod IndexationMethod { get; private set; }

    /// <summary>
    /// If index tenor is not provided, infer it from coupon schedule
    /// </summary>
    private Tenor RateTenor { get; set; }

    /// <summary>
    /// ZeroCoupon rate
    /// </summary>
    public bool ZeroCoupon { get; set; }

    #endregion
  }
}