//
// Copyright (c)    2002-2016. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Bump;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Curves
{
  [TestFixture]
  public class TestCreditCurveBumpOverlay
  {
    #region Tests

    [TestCase(0)]
    [TestCase(1)]
    public void BumpInplaceVsOverlay(int caseNo)
    {
      var cdsCurve = GetCreditCurve(_data[caseNo], 0, 0);
      Dt asOf = cdsCurve.AsOf;
      var maturities = cdsCurve.Select(p => p.Date).ToArray();
      var graph = new CalibratedCurve[] {cdsCurve}
        .ToDependencyGraph(c => c.EnumeratePrerequisiteCurves());
      var selection = graph.SelectTenors((c, t) => t.Product is CDS, "Uniform");

      // Perform in-place bump 1bp and calculate the probabilities.
      var affected = new List<CalibratedCurve>();
      var bumped0 = graph.BumpTenors(selection, 1, BumpFlags.BumpInPlace, affected);
      var probs0 = maturities.Select(dt => cdsCurve.SurvivalProb(asOf, dt)).ToArray();
      affected.RestoreBaseCurves();
      affected.Clear();

      // Perform overlay bump 1bp and calculate the probabilities.
      var bumped1 = graph.BumpTenors(selection, 1, BumpFlags.None, affected);
      var probs1 = maturities.Select(dt => cdsCurve.SurvivalProb(asOf, dt)).ToArray();

      // Both probabilities should be exactly the same in the case of Weighted interpolation.
      AssertEqual("Bump size", bumped0, bumped1);
      for (int i = 0; i < probs0.Length; ++i)
        AssertEqual("SurvivalProbability[" + i + ']', probs0[i], probs1[i], 1E-14);
    }

    [TestCase(0)]
    [TestCase(1)]
    public void CashflowPvWithOverlay(int caseNo)
    {
      var curve = GetCreditCurve(_data[caseNo], 0, 0);
      Dt asOf = curve.AsOf;
      var pricer = (CDSCashflowPricer) curve.Calibrator.GetPricer(
        curve, curve.Tenors[curve.Count - 1].Product);
      var cf = pricer.Cashflow;

      // Calculate the probabilities and cashflow pv without overlay set.
      var probs0 = Enumerable.Range(0, cf.Count)
        .Select(i => curve.SurvivalProb(asOf, cf.GetDt(i)))
        .ToArray();
      var pv0 = pricer.Pv();

      // Set up overlay
      CurveBumpUtilityAccessor.SetUpShiftOverlay(curve);

      // Calculate the probabilities and cashflow pv with overlay set.
      var probs1 = Enumerable.Range(0, cf.Count)
        .Select(i => curve.SurvivalProb(asOf, cf.GetDt(i)))
        .ToArray();
      var pv1 = pricer.Pv();

      // Both should product exactly the same results.
      for (int i = 0; i < probs0.Length; ++i)
        AssertEqual("SurvivalProbability[" + i + ']', probs0[i], probs1[i], 1E-14);
      AssertEqual("Cashflow Pv", pv0, pv1, 1E-14);
    }

    /// <summary>
    ///   QuantoSurvivalCurveUtilities.ToDomesticForwardMeasure has serveral code paths
    ///   depending on sc.Calibrator == null or correlation == 0 and others.
    /// </summary>
    /// <param name="withoutCalibrator"></param>
    /// <param name="corr"></param>
    [TestCase(false, 0.0)]
    [TestCase(false, 0.25)]
    [TestCase(true, 0.0)]
    [TestCase(true, 0.25)]
    public void QuantoCdsModelWithOverlay(bool withoutCalibrator, double corr)
    {
      var sc = GetCreditCurve(_data[0], 0, 0);
      var dc = new DiscountCurve(sc.AsOf, 0.04) {Ccy = Currency.AED};
      var vc = new VolatilityCurve(sc.AsOf, 0.4);

      // domestic measure without overlay
      var dm0 = GetQuantoCdsDomesticMeasure(sc, withoutCalibrator, dc, vc, corr);
      AssertEqual("Ccy", dm0.Ccy, dc.Ccy);

      // Set up overlay
      CurveBumpUtilityAccessor.SetUpShiftOverlay(sc);

      // domestic measure with overlay
      var dm1 = GetQuantoCdsDomesticMeasure(sc, withoutCalibrator, dc, vc, corr);
      AssertEqual("Ccy", dm1.Ccy, dc.Ccy);

      AssertEqual("Count", dm0.Count, dm1.Count, 1E-14);
      for (int i = 0, n = dm0.Count; i < n; ++i)
      {
        Dt date = dm0.GetDt(i);
        AssertEqual("SurvivalProbability[" + i + ']',
          dm0.SurvivalProb(sc.AsOf, date), dm1.SurvivalProb(sc.AsOf, date),
          1E-14);
      }
    }

    #endregion

    #region Helpers

    private static SurvivalCurve GetQuantoCdsDomesticMeasure(
      SurvivalCurve sc, bool withoutCalibrator,
      DiscountCurve domesticDiscount,
      VolatilityCurve fxolatilityCurve, double corr)
    {
      var calibrator = sc.Calibrator;
      try
      {
        if (withoutCalibrator) sc.Calibrator = null;
        return sc.ToDomesticForwardMeasure(sc.GetDt(sc.Count - 1) + 5*365,
          domesticDiscount, fxolatilityCurve, corr, 0.0, 0, TimeUnit.None);
      }
      finally
      {
        sc.Calibrator = calibrator;
      }
    }

    /// <summary>
    ///   Create credit curve based on the case data
    /// </summary>
    /// <param name="data"></param>
    /// <param name="begin"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    private static SurvivalCurve GetCreditCurve(Data data, int begin, int count)
    {
      Dt asOf = Dt.Today();
      var roll = BDConvention.Modified;
      var calendar = Calendar.NYB;
      if (count == 0) count = data.Tenors.Length - begin;
      var tenors = data.Tenors.Skip(begin).Take(count).ToArray();
      var maturities = Array.ConvertAll(tenors, s => Dt.CDSMaturity(asOf, s));
      var spreads = data.Spreads.Skip(begin).Take(count).ToArray();
      var refiProbs = data.RefiProbs == null ? null
        : data.RefiProbs.Skip(begin).Take(count).ToArray();

      var refiCurve = refiProbs == null ? null
        : SurvivalCurve.FromProbabilitiesWithBond(asOf, Currency.USD, "",
          InterpMethod.Weighted, ExtrapMethod.Const, maturities,
          refiProbs, tenors, null, null, null, 0.0);
      return SurvivalCurve.FitLCDSQuotes(asOf, Currency.USD, "",
        DayCount.Actual360, Frequency.Quarterly, roll, calendar,
        InterpMethod.Weighted, ExtrapMethod.Const,
        NegSPTreatment.Allow, new DiscountCurve(asOf, 0.04),
        tenors, maturities, null, spreads, new[] {0.4}, 0.0,
        true, null, refiCurve, 0.0);
    }

    #endregion

    #region Data

    private struct Data
    {
      internal Dt AsOf;
      internal string[] Tenors;
      internal double[] Spreads, RefiProbs;
    }

    private readonly Data[] _data =
    {
      // without refinance curve
      new Data
      {
        Tenors = new[]
        {
          "6 Month",
          "1 Year",
          "2 Year",
          "3 Year",
          "5 Year",
          "7 Year",
          "10 Year",
        },
        Spreads = new[]
        {
          100.00,
          120.00,
          140.00,
          160.00,
          180.00,
          190.00,
          220.00,
        },
      },

      // with refinance curve
      new Data
      {
        Tenors = new[]
        {
          "6 Month",
          "1 Year",
          "2 Year",
          "3 Year",
          "5 Year",
          "7 Year",
          "10 Year",
        },
        Spreads = new[]
        {
          100.00,
          120.00,
          140.00,
          160.00,
          180.00,
          190.00,
          220.00,
        },
        RefiProbs = new[]
        {
          98.363/100,
          96.546/100,
          92.598/100,
          87.861/100,
          78.650/100,
          70.165/100,
          53.950/100,
        },
      }
    };

    #endregion
  }

  internal static class CurveBumpUtilityAccessor
  {
    /// <summary>
    ///   Access the private static method in CurveBumpUtility class.
    /// </summary>
    public static void SetUpShiftOverlay(CalibratedCurve curve)
    {
      if (_setUpShiftOver == null)
      {
        var method = typeof (CurveBumpUtility).GetMethod("SetUpShiftOverlay",
          BindingFlags.Static | BindingFlags.NonPublic);
        _setUpShiftOver = (Action<CalibratedCurve>) Delegate.CreateDelegate(
          typeof (Action<CalibratedCurve>), method);
      }
      _setUpShiftOver(curve);
    }

    private static Action<CalibratedCurve> _setUpShiftOver;
  }

}
