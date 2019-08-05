//
// Copyright (c)    2002-2016. All rights reserved.
//

using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Curves
{
  [TestFixture]
  public class TestStandardProductsInterestRateCurveDualCurve
  {
    private InterestReferenceRate fedFundsRate;
    private DiscountCurve discountCurve;
    private InterestReferenceRate usdLiborRate;
    private DiscountCurve referenceCurve;
    private Dt asOf;

    [SetUp]
    public void CreateCurves()
    {
      asOf = new Dt(31, 3, 2014);

      ReferenceRate.CacheInitialise();
      ToolkitCache.StandardProductTermsCache.Initialise();

      var tenorNames = new[]
      {
        "1D", "1W", "2W", "1M", "2M", "3M", "4M", "5M", "6M", "9M", "1Yr", // MM
        "4 X 7", "6 X 9", "9 X 12", // FRAs
        "M4", "U4", "Z4", "H5", "M5", // STIR Futures
        "1Yr", "2Yr", "3Yr", "4Yr", "5Yr", "6Yr", "7Yr", "8Yr", "9Yr", "10Yr", "11Yr", "12Yr", "15Yr", "20Yr", "25Yr", "30Yr", "40Yr", "50Yr", // Swaps
        "1Yr", "2Yr", "3Yr", "4Yr", "5Yr", "6Yr", "7Yr", "8Yr", "9Yr", "10Yr", "12Yr", "15Yr", "20Yr", "25Yr", "30Yr" // Basis Swaps
      };

      var instruments = new[]
      {
        "MM", "MM", "MM", "MM", "MM", "MM", "MM", "MM", "MM", "MM", "MM",
        "FRA", "FRA", "FRA",
        "FUT", "FUT", "FUT", "FUT", "FUT",
        "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap", "Swap",
        "Basis", "Basis", "Basis", "Basis", "Basis", "Basis", "Basis", "Basis", "Basis", "Basis", "Basis", "Basis", "Basis", "Basis", "Basis"
      };

      fedFundsRate = InterestReferenceRate.Get("FEDFUNDS");
      usdLiborRate = InterestReferenceRate.Get("USDLIBOR");

      var rates = new []{ fedFundsRate, usdLiborRate };
      var tenors = new[]{ "1D", "3M" };
      var basisRates = new [] { usdLiborRate, null };
      var basisTenors = new[]{ "3M", "" };

      var quoteValues = new[,]
      {
        {
          double.NaN, 0.00075, 0.00075, 0.00079, 0.00082, 0.000825, 0.00073, 0.00086, 0.00088, 0.00098,
          double.NaN, double.NaN, double.NaN,
          double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
          double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
          -15.3, -18.9, -20, -22.6, -24, -25.4, -26.5, -27.2, -27.8, -28.2, -19.6, -7.9, -11.3, -20.8, -24.7
        },
        {
          double.NaN, 0.001195, double.NaN, 0.00152, 0.0019325, 0.002306, double.NaN, double.NaN, 0.003289, double.NaN,
          double.NaN, double.NaN, double.NaN,
          double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
          0.00269, 0.00545, 0.009918, 0.01432, 0.018025, 0.02108, 0.023555, 0.025485, 0.02708, 0.02842, double.NaN, 0.0296, 0.030535, 0.03261, 0.03433, 0.03508, 0.035415, 0.035395,
          double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN
        }
      };

      var quoteValues2 = quoteValues.Transpose();

      var quotes = CurveTenorFactory.BuildTenors(tenorNames, instruments, quoteValues2,
        null,
        rates, tenors, null,
        basisRates, basisTenors, null).ToList();

      // Create calibrator settings
      var calibratorSettings = new CalibratorSettings(new CurveFitSettings());
      calibratorSettings.FwdModelParameters = null;

      discountCurve = MultiRateCurveFitCalibrator.Fit(
        "FEDFUNDS_Curve", "", asOf,
        rates[0], Tenor.OneDay, Tenor.Empty,
        basisRates[0], Tenor.ThreeMonths, Tenor.Empty,
        Tenor.ThreeMonths, quotes, calibratorSettings, null);

      referenceCurve = MultiRateCurveFitCalibrator.Fit(
        "USDLIBOR_3M_Curve", "", asOf,
        rates[1], Tenor.ThreeMonths, Tenor.Empty,
        null, Tenor.Empty, Tenor.Empty,
        Tenor.ThreeMonths, quotes, calibratorSettings, new[] { discountCurve });
    }

    [Test]
    public void DiscountCurveMMRoundtrip()
    {
      var settle = Dt.AddDays(asOf, fedFundsRate.DaysToSpot, fedFundsRate.Calendar);
      foreach (var product in discountCurve.Tenors.Where(o => o.Product is Note).Select(o => o.Product))
      {
        var pricerPv = new NotePricer(product as Note, settle, settle, 1.0, discountCurve).Pv();
        NUnit.Framework.Assert.AreEqual(1.0, pricerPv, 1e-10);
      }
    }

    [Test]
    public void ReferenceCurveMMRoundtrip()
    {
      var settle = Dt.AddDays(asOf, usdLiborRate.DaysToSpot, usdLiborRate.Calendar);
      foreach (var product in referenceCurve.Tenors.Where(o => o.Product is Note).Select(o => o.Product))
      {
        var pricerPv = new NotePricer(product as Note, settle, settle, 1.0, referenceCurve).Pv();
        NUnit.Framework.Assert.AreEqual(1.0, pricerPv, 1e-10);
      }
    }

    [Test]
    public void ReferenceCurveSwapRoundtrip()
    {
      foreach (var swap in discountCurve.Tenors.Where(o => o.Product is Swap).Select(o => o.Product as Swap))
      {
        if (swap == null || !swap.IsFixedAndFloating) continue;
        var pricerPv =
          new SwapPricer(swap, asOf, swap.Effective, 1.0, discountCurve,
            new CalibratedCurve[]
            {
              swap.IsPayerFixed ? referenceCurve : null,
              swap.IsPayerFixed ? null : referenceCurve
            },
            new RateResets[] {null, null}, null, null).Pv();

        NUnit.Framework.Assert.AreEqual(0.0, pricerPv, 1e-12);
      }
    }

    [Test]
    public void DiscountCurveBasisSwapRoundtrip()
    {
      foreach (var swap in discountCurve.Tenors.Where(o => o.Product is Swap).Select(o => o.Product as Swap))
      {
        if (swap == null || !swap.IsBasisSwap) continue;
        var leg1Curve = new CalibratedCurve[] { referenceCurve, discountCurve }.First(o => o.ReferenceIndex.Name == swap.ReceiverLeg.ReferenceIndex.Name);
        var leg2Curve = new CalibratedCurve[] { referenceCurve, discountCurve }.First(o => o.ReferenceIndex.Name == swap.PayerLeg.ReferenceIndex.Name);
        var pricerPv =
          new SwapPricer(swap, asOf, swap.Effective, 1.0, discountCurve, new[] { leg1Curve, leg2Curve },
            new RateResets[] { null, null }, null, null).Pv();  
        NUnit.Framework.Assert.AreEqual(0.0, pricerPv, 1e-12);
      }
    }
  }
}
