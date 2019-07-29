using System.Collections.Generic;
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util.Configuration;


namespace BaseEntity.Toolkit.Models.BGM
{
  /// <summary>
  /// Underlying cap data
  /// </summary>
  public sealed class RateOptionParamCollection : Native.RateOptionParamCollection
  {
    private RateOptionParamCollection(int size): base(size) { }

    #region Methods
    /// <summary>
    /// Static constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="capMaturities">Cap maturities</param>
    /// <param name="discountCurve">Funding curve</param>
    /// <param name="referenceCurve">Reference curve/index tuple for each underlying cap</param>
    /// <returns></returns>
    internal static RateOptionParamCollection Factory(Dt asOf, Dt[] capMaturities, DiscountCurve discountCurve, Func<Dt, Tuple<InterestRateIndex, DiscountCurve>> referenceCurve)
    {
      if (capMaturities == null || capMaturities.Length == 0)
        return new RateOptionParamCollection(0);
      var target = TargetCurve(capMaturities, referenceCurve);
      var retVal = new RateOptionParamCollection(capMaturities.Length);
      for (int i = 0; i < capMaturities.Length; ++i)
      {
        var maturity = capMaturities[i];
        var reference = referenceCurve(maturity);
        retVal.AddCap(i, asOf, maturity, discountCurve, reference.Item2, reference.Item1, target.Item2, target.Item1);
      }
      return retVal;
    }

    /// <summary>
    /// Number of underlying caps
    /// </summary>
    internal int Count
    {
      get { return count(); }
    }

    /// <summary>
    /// Payment schedule of cap with given maturity
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="maturity">Maturity</param>
    /// <param name="referenceCurve">Reference curve</param>
    /// <param name="referenceIndex">Reference index</param>
    /// <returns>Payment schedule</returns>
    internal static IEnumerable<CapletPayment> GetPaymentSchedule(Dt asOf, Dt maturity, DiscountCurve referenceCurve, InterestRateIndex referenceIndex)
    {
      Dt effective = Cap.StandardEffective(asOf, referenceIndex);
      var cap = new Cap(effective, maturity, referenceIndex.Currency, CapFloorType.Cap, 0.0, referenceIndex.DayCount, referenceIndex.IndexTenor.ToFrequency(),
                        referenceIndex.Roll, referenceIndex.Calendar) {AccrueOnCycle = true, CycleRule = CycleRule.None};
      foreach (CapletPayment caplet in cap.GetPaymentSchedule(asOf, new RateResets(new List<RateReset>())))
      {
        caplet.Rate = referenceCurve.F(caplet.RateFixing, caplet.TenorDate, cap.DayCount, Frequency.None);
        yield return caplet;
      }
    }

    /// <summary>
    /// Add Cap
    /// </summary>
    /// <param name="idx">Index in native array</param>
    /// <param name="asOf">As of date</param>
    /// <param name="maturity">Maturity</param>
    /// <param name="discountCurve">DiscountCurve</param>
    /// <param name="referenceCurve">Caplet reference curve</param>
    /// <param name="referenceIndex">Caplet reference index</param>
    /// <param name="targetCurve">Target curve</param>
    /// <param name="targetIndex">Target index</param>
    private void AddCap(
      int idx,
      Dt asOf,
      Dt maturity,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve,
      InterestRateIndex referenceIndex,
      DiscountCurve targetCurve,
      InterestRateIndex targetIndex)
    {
      referenceCurve = referenceCurve ?? discountCurve;
      foreach (var caplet in GetPaymentSchedule(asOf, maturity, referenceCurve, referenceIndex))
      {
        var T = CapFloorPricer.CalculateTime(asOf, caplet.Expiry, referenceIndex.DayCount);
        var discountFactor = (discountCurve == null) ? 1.0 : discountCurve.DiscountFactor(asOf, caplet.PayDt);
        var level = caplet.PeriodFraction * discountFactor;
        int compoundingPeriods;
        if ((targetIndex == null) || (targetCurve == null) || (referenceIndex.Equals(targetIndex)) 
          || ((compoundingPeriods = (int)Math.Ceiling(referenceIndex.IndexTenor.Years / targetIndex.IndexTenor.Years)) < 2))
        {
          add(idx, caplet.RateFixing, caplet.Rate, level, T, new Dt[0], new double[0], new double[0]);
          continue;
        }
        var cmpnts = new double[compoundingPeriods];
        var fracs = new double[compoundingPeriods];
        var resets = new Dt[compoundingPeriods];
        Dt next = caplet.RateFixing;
        for (int i = 0; i < compoundingPeriods; ++i)
        {
          Dt prev = next;
          next = Dt.Add(prev, targetIndex.IndexTenor);
          cmpnts[i] = targetCurve.F(prev, next, targetIndex.DayCount, Frequency.None);
          fracs[i] = Dt.Fraction(prev, next, targetIndex.DayCount);
          resets[i] = prev;
        }
        add(idx, caplet.RateFixing, caplet.Rate, level, T, resets, cmpnts, fracs);
      }
    }

    /// <summary>
    /// Extract projection curve with shortest index tenor
    /// </summary>
    /// <param name="maturities">Cap maturities</param>
    /// <param name="selector">Selector</param>
    /// <returns></returns>
    private static Tuple<InterestRateIndex, DiscountCurve> TargetCurve(Dt[] maturities, Func<Dt, Tuple<InterestRateIndex, DiscountCurve>> selector)
    {
      var retVal = selector(maturities[0]);
      for (int i = 0; ++i < maturities.Length; )
      {
        var pi = selector(maturities[i]);
        if (pi.Item1.IndexTenor.Days < retVal.Item1.IndexTenor.Days)
          retVal = pi;
      }
      return retVal;
    }

    #endregion
  }
}
