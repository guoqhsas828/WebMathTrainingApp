//
// Copyright (c)    2018. All rights reserved.
//

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.BasketForNtdPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Test NTD pricers with various default scenarios.
  /// The test covers all possible products from NTD 1/1,
  /// NTD 1/2, ..., util NTD 10/10, for a 10 names basket.
  /// </summary>
  [TestFixture]
  public class TestDefaultedNTD : SensitivityTest
  {
    #region Data

    const int N = 10;
    const double interestRate = 0.04;
    const double hazardRate = 0.2;
    const double recoveryRate = 0.4;
    Dt asOf_, settle_;
    FTDPricer[] pricers_;
    double[] protections_;
    private static readonly int[] Days = { 0, 1, 2, 3, 10, 90 };

    #endregion Data

    #region Methods

    /// <summary>
    /// Set up this instance.
    /// </summary>
    [OneTimeSetUp]
    public void Init()
    {
      Dt asOf = asOf_ = Dt.Today();
      Dt settle = settle_ = Dt.Add(asOf, 1);
      Dt effective = Dt.SNACFirstAccrualStart(
        Dt.Add(asOf, -1), Calendar.None);
      Dt maturity = Dt.CDSMaturity(effective,"5Y");

      DiscountCurve discountCurve =
        new DiscountCurve(asOf, interestRate);
      SurvivalCurve[] survivalCurves =
        ArrayUtil.Generate<SurvivalCurve>(N, (i) =>
        {
          SurvivalFitCalibrator cal = new SurvivalFitCalibrator(
            asOf, settle, recoveryRate, discountCurve);
          SurvivalCurve sc = new SurvivalCurve(cal);
          sc.Set(new SurvivalCurve(asOf, hazardRate * (1 - ((double)i) / N)));
          sc.Name = "curve_" + (i + 1);
          return sc;
        });
      double[] principals = ArrayUtil.NewArray(N, 10.0);

      FTDPricer[] pricers = new FTDPricer[N * (N + 1) / 2];
      for (int i = 0, idx = 0; i < N; ++i)
      {
        int cover = i+1;
        for (int j = 0; j < N - i; ++idx, ++j)
        {
          int first = j + 1;
          FTD ntd = new FTD(effective, maturity, Currency.None,
            0.05, DayCount.Actual360, Frequency.Quarterly,
            BDConvention.Following, Calendar.NYB, first, cover);
          ntd.Description = "NTD " + first + '/' + cover;
          pricers[idx] = new FTDPricer(ntd, new 
            SemiAnalyticBasketForNtdPricer(
            asOf, settle, maturity, survivalCurves, null, principals,
            new Copula(), new SingleFactorCorrelation(new string[N], 1.0),
            3, TimeUnit.Months), discountCurve, cover);
        }
      }
      pricers_ = pricers;

      double[] protections = ArrayUtil.Generate<double>(
        N, (i) => pricers[i].ProtectionPv());
      protections_ = protections;
    }
    
    #endregion Methods

   
    #region Tests

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(5)]
    [TestCase(N)]
    public void TestDefault(int defaults)
    {
      SurvivalCurve[] curves = pricers_[0].SurvivalCurves;
      SurvivalCurve[] saved = ArrayUtil.Generate<SurvivalCurve>
        (defaults, (i) => (SurvivalCurve)curves[i].Clone());

      // The case of WillDefault:
      //   default on pricer.Settle date.
      Dt dfltDate = pricers_[0].Settle;
      //TestDefault(".WillDefault.", defaults,
      //  dfltDate, Dt.Empty, curves);

      // The case of settled defaults:
      //  defaulted in the past and settled on the pricer.Settle.
      dfltDate = pricers_[0].AsOf;
      Dt dfltSettle = pricers_[0].Settle;
      TestDefault(".Settled.", defaults,
        dfltDate, dfltSettle, curves);

      // The case of unsettled defaults:
      //  defaulted in the past and settled on the pricer.Settle + 1.
      dfltSettle = Dt.Add(dfltSettle, 1);
      TestDefault(".Unsettled.", defaults,
        dfltDate, dfltSettle, curves);

      // The case of unsettled defaults:
      // defaulted in the past and will settle on/after the maturity date.
      foreach (var day in Days)
      {
        dfltSettle = Dt.Add(pricers_[0].Basket.Maturity, day);
        TestDefault(".Unsettle.", defaults,
          dfltDate, dfltSettle, curves);
      }

      for (int i = 0; i < defaults; ++i)
        SurvivalCurveSet(curves[i], saved[i]);
      return;
    }

    #endregion

    #region Helpers
    private void TestDefault(string name,
      int defaults, Dt dfltDate, Dt dfltSettle,
      SurvivalCurve[] curves)
    {
      bool unsettled = dfltSettle.IsEmpty()
        || dfltSettle > pricers_[0].Settle;
      double df = pricers_[0].DiscountCurve
        .DiscountFactor(pricers_[0].AsOf,
        dfltSettle.IsEmpty() ? dfltDate : dfltSettle);
      for (int i = 0; i < defaults; ++i)
        SetDefaulted(curves[i], dfltDate, dfltSettle);
      for (int i = 0; i < pricers_.Length; ++i)
      {
        pricers_[i].Reset();
        FTD ntd = pricers_[i].FTD;
        // Test protection pv.
        double expect = CalculateProtectionPv(
          ntd, defaults, df, unsettled);
        double actual = pricers_[i].ProtectionPv();
        Assert.AreEqual(expect, actual, 1E-12,
          ntd.Description + name + "ProtPv");
        // Test current notional
        expect = CalculateCurrentNotional(ntd, defaults);
        actual = pricers_[i].CurrentNotional;
        Assert.AreEqual(expect, actual, 1E-12,
          ntd.Description + name + "CurrentNotional");
        // Test Effective Notional.
        if (unsettled) expect = pricers_[i].Notional;
        actual = pricers_[i].EffectiveNotional;
        Assert.AreEqual(expect, actual, 1E-12,
          ntd.Description + name + "EffectiveNotional");
      }

      return;
    }

    private double CalculateProtectionPv(
      FTD ntd, int defaults, double df, bool unsettled)
    {
      double sum = 0;
      int start = ntd.First - 1;
      int stop = start + ntd.NumberCovered;
      for (int i = start; i < stop; ++i)
      {
        if (i < defaults)
        {
          if (unsettled) sum += (recoveryRate - 1) * df;
        }
        else
          sum += protections_[i];
      }
      return sum;
    }

    private double CalculateCurrentNotional(
      FTD ntd, int defaults)
    {
      double sum = 0;
      int start = ntd.First - 1;
      int stop = start + ntd.NumberCovered;
      for (int i = start; i < stop; ++i)
      {
        if (i >= defaults)
          sum += 1;
      }
      return sum;
    }

    private static void SetDefaulted(
      SurvivalCurve curve, Dt dfltDate, Dt dfltSettle)
    {
      curve.DefaultDate = dfltDate;
      if (dfltDate != dfltSettle && !dfltSettle.IsEmpty())
        curve.SurvivalCalibrator.RecoveryCurve.JumpDate = dfltSettle;
    }

    private static void SurvivalCurveSet(SurvivalCurve dst, SurvivalCurve src)
    {
      // Restore the dst curve from src
      dst.Copy(src);
    }
    #endregion Helpers
  }
}
