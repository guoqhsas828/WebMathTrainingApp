//
// Copyright (c)    2002-2016. All rights reserved.
//

using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Curves
{
  [TestFixture]
  public class TestStandardProductForCurves
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
        "4 X 7", "6 X 9", "9 X 12", // FRAs
        "6Yr", "7Yr", "8Yr", "9Yr", "10Yr", // Swaps
      };

      var instruments = new[]
      {
        "FRA", "FRA", "FRA",
        "Swap", "Swap", "Swap", "Swap", "Swap"
      };

      fedFundsRate = InterestReferenceRate.Get("FEDFUNDS");
      usdLiborRate = InterestReferenceRate.Get("USDLIBOR");

      var rates = new [] {fedFundsRate, usdLiborRate};
      var tenors = new[] {"1D", "3M"};
      var basisRates = new [] {usdLiborRate, null};
      var basisTenors = new[] {"3M", ""};

      var quoteValues = new[,]
      {
        {
          -0.00095, -0.00092, -0.00085,
          0.00269, 0.00545, 0.009918, 0.01432, 0.018025 // swap
        },
        {
          -0.00097, -0.00095, -0.00090,
          0.00170, 0.00245, 0.003918, 0.00732, 0.010025 //swap
        }
      };

      var quoteValues2 = quoteValues.Transpose();

      var quotes = CurveTenorFactory.BuildTenors(tenorNames, instruments, quoteValues2,
        null, rates, tenors, null, basisRates, basisTenors, null).ToList();

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
        Tenor.ThreeMonths, quotes, calibratorSettings, new[]
        {
          discountCurve
        });
    }

    [Test]
    public void DiscountCurveFRARoundtrip()
    {
      var settle = Dt.AddDays(asOf, fedFundsRate.DaysToSpot, fedFundsRate.Calendar);
      foreach (var product in discountCurve.Tenors.Where(o => o.Product is FRA)
        .Select(o => o.Product))
      {
        var pricerPv = new FRAPricer(product as FRA, settle, settle,
          discountCurve, referenceCurve, 1.0).Pv();
        NUnit.Framework.Assert.AreEqual(0.0, pricerPv, 5e-5);
      }
    }


    [Test]
    public void ReferenceCurveFRARoundtrip()
    {
      var settle = Dt.AddDays(asOf, usdLiborRate.DaysToSpot, usdLiborRate.Calendar);
      foreach (var product in referenceCurve.Tenors.Where(o => o.Product is FRA)
        .Select(o => o.Product))
      {
        var pricerPv = new FRAPricer(product as FRA, settle, settle,
          discountCurve, referenceCurve, 1.0).Pv();
        NUnit.Framework.Assert.AreEqual(0.0, pricerPv, 1e-10);
      }
    }
  }
}
