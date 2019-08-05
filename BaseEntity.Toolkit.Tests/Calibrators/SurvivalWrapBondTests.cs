// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System.Data;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Calibrators
{

  [TestFixture]
  public class SurvivalWrapBondTests
  {
    private static readonly object[,] Data = 
    {
      // these dates are intentionally set on weekends
      {"1M", 20150425, 0.995},
      {"4M", 20150725, 0.982},
      {"7M", 20151025, 0.97},
      {"1Y", 20160326, 0.95},
    };

    private readonly Dt _asOf = new Dt(20150321);
    private readonly string[] _tenorNames;
    private readonly Dt[] _maturities;
    private readonly double[] _probabilities;

    public SurvivalWrapBondTests()
    {
      _tenorNames = Data.Column(0).Cast<string>().ToArray();
      _maturities = Data.Column(1).Select(d => new Dt((int)d)).ToArray();
      _probabilities = Data.Column(2).Cast<double>().ToArray();
    }

    [Test]
    public void TestExactDateWrap()
    {
      var curve = GetWrappedCurve();
      for(int i = 0, n = curve.Count; i < n; ++i)
      {
        Assert.AreEqual(_maturities[i], curve.GetDt(i));
        Assert.AreEqual(_probabilities[i], curve.GetVal(i), 1E-15);
      }

      // Test get, set and bump quotes
      for (int i = 0, n = curve.Count; i < n; ++i)
      {
        var tenor = curve.Tenors[i];
        var originalCpn = ((Note)tenor.Product).Coupon;
        var quote = tenor.CurrentQuote;
        Assert.AreEqual(QuotingConvention.CreditSpread,quote.Type);
        tenor.SetQuote(quote);
        Assert.AreEqual(originalCpn, ((Note)tenor.Product).Coupon, 1E-16);
        tenor.BumpQuote(0.0, BumpFlags.None);
        Assert.AreEqual(originalCpn, ((Note)tenor.Product).Coupon, 1E-16);
      }

      // Test refit
      curve.Fit();
      for (int i = 0, n = curve.Count; i < n; ++i)
      {
        Assert.AreEqual(_maturities[i], curve.GetDt(i));
        Assert.AreEqual(_probabilities[i], curve.GetVal(i), 1E-16);
      }

      return;
    }

    [TestCase(BumpFlags.None)]
    [TestCase(BumpFlags.BumpInPlace)]
    public void TestSensitivity(BumpFlags flags)
    {
      var survivalCurve = GetWrappedCurve();
      var pricers = survivalCurve.Tenors.Select(t => t.GetPricer(
        survivalCurve, survivalCurve.Calibrator)).ToArray();
      for (int i = 0; i < pricers.Length; ++i)
        Assert.AreEqual(1.0, pricers[i].Pv(), 1E-15, "Pv_" + i);

      DataTable table;
      using (new CheckStates(true, pricers))
      {
        var timer = new Timer();
        timer.start();
        table = Sensitivities2.Calculate(pricers, null, null,
          BumpTarget.CreditQuotes, 4, 4, BumpType.ByTenor,
          flags, null, true, true,
          "matching", true, true, null);
        timer.stop();
        var t1 = timer.getElapsed();
      }

      table.AssertTenorConsistency(1E-14, null);
      return;
    }

    [TestCase(CDXScalingMethod.Spread)]
    [TestCase(CDXScalingMethod.Model)]
    [TestCase(CDXScalingMethod.Duration)]
    public void TestScaling(CDXScalingMethod method)
    {
      const int n = 10;
      var curves = new SurvivalCurve[n];
      for (int i = 0; i < n; ++i)
        curves[i] = GetWrappedCurve();
      var baseSpread = curves[0].Tenors[_maturities.Length - 1]
        .CurrentQuote.Value;

      var cdx = new[]
      {
        new CDX(_asOf, Dt.Add(_asOf, "1Y"), Currency.None, 100/10000.0,
          DayCount.Actual360, Frequency.Quarterly, BDConvention.Following,
          Calendar.NYB, null)
      };
      // Bump hazard rates
      {
        var calc = new IndexScalingCalibrator(_asOf, _asOf, cdx,
          null, new[] { baseSpread }, false, new[] { method },
          false, true, curves[0].SurvivalCalibrator.DiscountCurve,
          curves, null, 0.4);
        var bumps = calc.GetScalingFactors();
        Assert.AreEqual(0.0, bumps[0], 0.0005, "Bump hazards");
      }
      // Bump spreads
      {
        var calc = new IndexScalingCalibrator(_asOf, _asOf, cdx,
          null, new[] { baseSpread }, false, new[] { method },
          false, false, curves[0].SurvivalCalibrator.DiscountCurve,
          curves, null, 0.4);
        var bumps = calc.GetScalingFactors();
        Assert.AreEqual(0.0, bumps[0], 1, "Bump spreads");
      }
      return;
    }

    private SurvivalCurve GetWrappedCurve()
    {
      return SurvivalCurve.FromProbabilitiesWithBond(_asOf,
        Currency.None, "", InterpMethod.Weighted, ExtrapMethod.Const,
        _maturities, _probabilities, _tenorNames,
        new[] { DayCount.Actual365Fixed }, null,
        new[] { 0.4 }, 0, true);
    }
  }

}
