using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Calculator of aussie inflation K factors
  /// </summary>
  [Serializable]
  public class InflationKFactorCalculator : CouponCalculator
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">AsOf Date</param>
    /// <param name="referenceIndex">Reference index object</param>
    /// <param name="nearFixingLag">Lag for near fixing period</param>
    /// <param name="farFixingLag">Lag for far fixing period</param>
    /// <param name="referenceCurve">Reference rate curve used for the calculation of the forward inflation price. </param>
    public InflationKFactorCalculator(Dt asOf, InflationIndex referenceIndex, Tenor nearFixingLag, Tenor farFixingLag, InflationCurve referenceCurve, Schedule productSchedule)
      : base(asOf, referenceIndex, referenceCurve)
    {
      NearFixingLag = nearFixingLag;
      FarFixingLag = farFixingLag;
      FullFixingSchedule = GetFixingSchedule(productSchedule); 
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
      var rateFixings = new List<InflationRateFixing>();
      RateResetState resetState = RateResetState.None;
      var kFactor = 1.0; 
      for (int i = 0; i < FullFixingSchedule.FixingDates.Length; i++)
      {
        if (FullFixingSchedule.FixingDates[i] <= sched.FixingEndDate)
        {
          var inflationRateFixing = RateFixing(FullFixingSchedule.FixingDates[i], FullFixingSchedule.PreviousFixingDates[i]);
          rateFixings.Add(inflationRateFixing);
          resetState = inflationRateFixing.RateResetState;
          if (inflationRateFixing.RateResetState == RateResetState.Missing)
          {
            break;
          }
          kFactor *= 1.0 + inflationRateFixing.Forward;
        }
        else
        {
          break;
        }
      }

      var retVal = new InflationKFactorFixing(){RateFixings = rateFixings, RateResetState =  resetState, Forward = kFactor};
      return retVal; 
    }


    private InflationRateFixing RateFixing(Dt fixingDt, Dt previousFixingDt)
    {
      var retVal = new InflationRateFixing();
      RateResetState sd, sn;
      var laggedAsOf = RateResetUtil.ResetDate(AsOf, null, ResetLag);
      double d = InflationUtils.InflationForwardFixing(laggedAsOf, new []{previousFixingDt},
                                                  previousFixingDt, ReferenceCurve, InflationIndex,
                                                  out sd);
      if (d <= 0)
        return retVal;
      double n = InflationUtils.InflationForwardFixing(laggedAsOf, new []{fixingDt}, fixingDt,
                                                       ReferenceCurve, InflationIndex, out sn);
      retVal.DenominatorResetState = sd;
      retVal.NumeratorResetState = sn;
      if (sd == RateResetState.Missing || sn == RateResetState.Missing)
        retVal.RateResetState = RateResetState.Missing;
      else if (sd == RateResetState.IsProjected || sn == RateResetState.IsProjected)
        retVal.RateResetState = RateResetState.IsProjected;
      else
        retVal.RateResetState = RateResetState.ObservationFound;
      var observationPeriod = FarFixingLag.Years - NearFixingLag.Years;
      var indexTenor = InflationIndex.IndexTenor.Years; 
      retVal.Forward = Math.Round((n / d - 1.0) * indexTenor/observationPeriod, 4); 
      retVal.InflationAtPayDt = n;
      retVal.InflationAtPreviousPayDt = d;
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
      var fixingDts = InflationUtils.InflationPeriod(RateResetUtil.ResetDate(payDt, null, NearFixingLag), InflationIndex.PublicationFrequency, InflationIndex.PublicationLag, IndexationMethod.AustralianCIB);
      var prevFixingDts = InflationUtils.InflationPeriod(RateResetUtil.ResetDate(payDt, null, FarFixingLag), InflationIndex.PublicationFrequency, InflationIndex.PublicationLag, IndexationMethod.AustralianCIB);
      var fixingSchedule = new InflationRateFixingSchedule {StartDate = prevFixingDts.First(), EndDate = fixingDts.First()};
      fixingSchedule.FixingDates = fixingDts;
      fixingSchedule.PreviousFixingDates = prevFixingDts;
      fixingSchedule.ResetDate = fixingSchedule.FixingDates.Last();
      return fixingSchedule;
    }

    /// <summary>
    /// Initialize fixing schedule
    /// </summary>
    /// <param name="productSchedule">the product schedule</param>
    /// <returns>complete fixing schedule</returns>
    public InflationRateFixingSchedule GetFixingSchedule(Schedule productSchedule)
    {
      var fixingSchedule = new InflationRateFixingSchedule { StartDate =  productSchedule.AsOf };
      var fixingDates = new List<Dt>();
      var prevFixingDates = new List<Dt>(); 
      for (int i = 0; i < productSchedule.Count; i++)
      {
        var nextCoupon = productSchedule.GetPaymentDate(i);
        var fixingDt = InflationUtils.InflationPeriod(RateResetUtil.ResetDate(nextCoupon, null, NearFixingLag), InflationIndex.PublicationFrequency, InflationIndex.PublicationLag, IndexationMethod.AustralianCIB).First();
        var prevFixingDt = InflationUtils.InflationPeriod(RateResetUtil.ResetDate(nextCoupon, null, FarFixingLag), InflationIndex.PublicationFrequency, InflationIndex.PublicationLag, IndexationMethod.AustralianCIB).First();
        fixingDates.Add(fixingDt);
        prevFixingDates.Add(prevFixingDt);
        fixingSchedule.EndDate = nextCoupon;
      }

      fixingSchedule.FixingDates = fixingDates.ToArray();
      fixingSchedule.PreviousFixingDates = prevFixingDates.ToArray();
      
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

      foreach (var dt in sched.PreviousFixingDates)
      {
        RateResetState s;
        double p = InflationUtils.FindReset(dt, AsOf, InflationIndex, out s);
        resetInfos.Add(new RateResets.ResetInfo(dt, p, s));
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
    /// How far to look back for near CPI fixing
    /// </summary>
    private Tenor NearFixingLag { get; set; }

    /// <summary>
    /// How far to look back for far CPI fixing
    /// </summary>
    public Tenor FarFixingLag { get; set; }

    /// <summary>
    /// Full fixing schedule
    /// </summary>
    public InflationRateFixingSchedule FullFixingSchedule { get; set; }
    #endregion
  }
}