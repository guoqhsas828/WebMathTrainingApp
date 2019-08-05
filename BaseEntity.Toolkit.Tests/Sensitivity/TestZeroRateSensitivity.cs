//using System;
//using System.Linq;
//using BaseEntity.Toolkit.Base;
//using BaseEntity.Toolkit.Cashflows;
//using BaseEntity.Toolkit.Curves;
//using NUnit.Framework;

//namespace BaseEntity.Toolkit.Tests.Sensitivity
//{
//  [TestFixture]
//  public class TestZeroRateSensitivity
//  {
//    #region Set Up

//    [OneTimeSetUp]
//    public void Initialize()
//    {
//      var d = _data;
//      var calendar = new Calendar("NYB+LNB");
//      _maturities = d.Tenors.Select(a => Dt.Roll(
//        Dt.Add(d.AsOf, a), BDConvention.Modified, calendar)).ToArray();

//      _zeroRates = Enumerable.Range(0, d.DiscountFactors.Count())
//        .Select(i => RateCalc.RateFromPrice(d.DiscountFactors[i],
//          d.AsOf, _maturities[i], DayCount.Actual365Fixed, Frequency.None))
//        .ToArray();

//      _dc = CreateDiscountCurve();
//    }

//    private DiscountCurve CreateDiscountCurve()
//    {
//      var d = _data;
//      var asOf = d.AsOf;
//      var types = d.Types;
//      var tenors = d.Tenors;
//      var idx = new InterestRateIndex("USDLIBOR_3M_INDEX", Tenor.Parse("3M"), Currency.USD,
//        DayCount.Actual360, Calendar.NYB, BDConvention.Modified, Frequency.Daily, CycleRule.None, 0);

//      var term = new CurveTerms("USDLIBOR_3M", Currency.USD, Calendar.NYB, idx,
//        BDConvention.Modified, DayCount.Actual365Fixed, 0, DayCount.Thirty360,
//        Frequency.Quarterly, Frequency.Quarterly, Frequency.Quarterly);

//      var fitSettings = new CurveFitSettings(asOf)
//      {
//        CurveSpotDays = idx.SettlementDays,
//        CurveSpotCalendar = idx.Calendar,
//        Method = CashflowCalibrator.CurveFittingMethod.Bootstrap
//      };

//      var calibratorSettings = new CalibratorSettings(fitSettings)
//      {
//        Tolerance = 1e-14
//      };

//      return DiscountCurveFitCalibrator.DiscountCurveFit(asOf, term,
//        "USDLIBOR", _zeroRates, types, tenors, calibratorSettings);
//    }

//    #endregion

//    #region Tests

//    [Test]
//    public void RoundTripTest()
//    {
//      var d = _data;
//      var asOf = d.AsOf;
//      var dfs = d.DiscountFactors;
//      foreach (var diff in _maturities.Select(
//        (t, i) => _dc.DiscountFactor(asOf, t) - dfs[i]))
//      {
//        Assert.AreEqual(0.0, diff, 1E-15);
//      }
//    }

//    [Test]
//    public void ZeroRateSensitivity()
//    {
//      var d = _data;
//      var asOf = d.AsOf;
//      var tenors = d.Tenors;
//      var pricers = tenors.Select((s, i) => new Note(asOf, _maturities[i],
//        Currency.USD, _zeroRates[i], DayCount.Actual365Fixed, Frequency.None,
//        BDConvention.None, Calendar.None) {Description = s})
//        .Select(note => new NotePricer(note, asOf, asOf, 1.0, _dc))
//        .Cast<IPricer>().ToArray();

//      foreach (var pricer in pricers)
//      {
//        Assert.AreEqual(1.0, pricer.Pv(), 1E-15,
//          pricer.Product.Description + " Pv");
//      }

//      var bump = _data.BumpSize / 10000.0;
//      var manuallyBumpPvDiffs = _maturities.Select((dt, i) =>
//      {
//        var frac = Dt.Fraction(asOf, dt, DayCount.Actual365Fixed);
//        var df = 1/(1 + (_zeroRates[i] + bump)*frac);
//        return (1 + _zeroRates[i]*frac)*df - 1.0;
//      }).ToArray();

//      var table = Sensitivities2.Calculate(pricers, null, null, BumpTarget.InterestRates, 100, 0, BumpType.ByTenor,
//        false, null, false, false, "matching", false, false, null);

//      var count = table.Rows.Count;
//      Assert.AreEqual(tenors.Length*pricers.Length, count, "Row Count");

//      for (int i = 0; i < table.Rows.Count; i++)
//      {
//        var row = table.Rows[i];
//        var pricer = (string) row["Pricer"];
//        var tenor = (string) row["Curve Tenor"];
//        var delta = (double) row["Delta"];
//        if (tenor.EndsWith(pricer) && tenor[tenor.Length - pricer.Length - 1] == '_')
//        {
//          var idx = Array.IndexOf(tenors, pricer);
//          Assert.AreEqual(manuallyBumpPvDiffs[idx], delta, 1E-15, tenor + " / " + pricer);
//        }
//        else
//          Assert.AreEqual(0.0, delta, 1E-15, tenor + " / " + pricer);
//      }

//      return;
//    }

//    #endregion

//    #region Data

//    private class Data
//    {
//      public Dt AsOf;
//      public string[] Tenors, Types;
//      public double[] DiscountFactors;
//      public double BumpSize;
//    }

//    private static Data _data = new Data
//    {
//      AsOf = new Dt(20131125),
//      Tenors = new[]
//      {
//        "1Y",
//        "2Y",
//        "3Y",
//        "5Y",
//        "10Y",
//        "15Y",
//        "20Y"
//      },
//      Types = new[]
//      {"MM", "MM", "MM", "MM", "MM", "MM", "MM"},
//      DiscountFactors = new[]
//      {0.99, 0.97, 0.95, 0.92, 0.88, 0.84, 0.8},
//      BumpSize = 100,
//    };

//    // calculated data
//    private Dt[] _maturities;
//    private double[] _zeroRates;
//    private DiscountCurve _dc;

//    #endregion Data
//  }
//}
