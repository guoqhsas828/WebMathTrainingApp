// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Curves;

namespace BaseEntity.Toolkit.Tests.Calibrators
{
  [TestFixture]
  public class TestDiscountFitDualCurve
  {
    private double[] swapQuotes = new[]
      {
        0.0,     0.00493, 0.00647, 0.00913, 0.01212,
        0.01492, 0.01732, 0.01929, 0.02094, 0.02235,
        0.02466, 0.02680, 0.02880, 0.02974, 0.03028
      };

    private string[] swapTenors = new[]
      {
        "1 Yr", "2 Yr", "3 Yr", "4 Yr", "5 Yr",
        "6 Yr", "7 Yr", "8 Yr", "9 Yr", "10 Yr",
        "12 Yr", "15 Yr", "20 Yr", "25 Yr", "30 Yr"
      };

    private double[] basisQuotes = new[]
      {
        49.1, 50.9, 51.25, 51.0, 49.25,
        47.0, 42.0, 35.30, 32.6, 29.60,
        26.25, 24.1, 22.50, 22.0, 20
      };

    private string[] basisTenors = new[]
      {
        "1Yr", "2Yr", "3Yr", "4Yr", "5Yr", "6 Yr",
        "7Yr", "8Yr", "9Yr", "10Yr", "12 Yr",
        "15Yr", "20Yr", "25Yr", "30Yr"
      };

    private void DoTest(int n)
    {
      var settings = new CalibratorSettings();
      var fundingIndexName = "FEDFUNDS_1D";
      var projectionIndexName = "USDLIBOR_3M";
      var fundingIndex = new InterestRateIndex(fundingIndexName,
        Tenor.Parse("1D"), Currency.USD, DayCount.Thirty360,
        Calendar.NYB, BDConvention.Modified, 2);
      var funding_basis = new BasisSwapAssetCurveTerm(2, Calendar.Parse("NYB+LNB"), ProjectionType.SimpleProjection, Frequency.Quarterly, Frequency.None,
                                                      CompoundingConvention.None, null, ProjectionType.None, Frequency.None, Frequency.None,
                                                      CompoundingConvention.None, null, true);
      var fundingTerms = new CurveTerms(fundingIndexName + "_Terms",
        Currency.USD, fundingIndex, new[] { funding_basis });
      var projectionIndex = new InterestRateIndex(projectionIndexName,
        Tenor.Parse("3M"), Currency.USD, DayCount.Thirty360,
        Calendar.LNB, BDConvention.Modified, 2);
      var projection_mm = new AssetRateCurveTerm(InstrumentType.MM,
        2, BDConvention.Modified, DayCount.Thirty360,
        Calendar.NYB, Frequency.None, ProjectionType.None, null);
      var projection_swap = new SwapAssetCurveTerm(2, BDConvention.Modified, DayCount.Actual360, Calendar.Parse("NYB+LNB"), Frequency.SemiAnnual, Frequency.None,
                                                   ProjectionType.SimpleProjection, Frequency.Quarterly, Frequency.None, CompoundingConvention.None, null);
      var projectionTerms = new CurveTerms(projectionIndexName + "_Terms",
        Currency.USD, projectionIndex, new AssetCurveTerm[]{ projection_mm, projection_swap });
      var instrumentNames = Enumerable.Repeat("Swap", n).Concat(Enumerable.Repeat("BasisSwap", n)).ToArray();
      var terms = fundingTerms.Merge(instrumentNames, projectionTerms, true);
      Dt asOf = new Dt(20110916);
      var discountCurve = DiscountCurveFitCalibrator.DiscountCurveFit(
        asOf, terms, fundingIndexName + "_Curve",
        swapQuotes.Take(n).Concat(basisQuotes.Take(n)).ToArray(),
        instrumentNames, swapTenors.Take(n).Concat(basisTenors.Take(n)).ToArray(), settings);
      var assetTypes = Enumerable.Repeat("Swap", n).ToArray();
      var paymentSettings = assetTypes.Select(name => RateCurveTermsUtil.GetPaymentSettings(projectionTerms, name)).ToArray();
      var projectionCurve = ProjectionCurveFitCalibrator.ProjectionCurveFit(
        asOf, projectionTerms, discountCurve, projectionIndexName + "_Curve",
        swapQuotes.Take(n).ToArray(),
        assetTypes,  
        swapTenors.Take(n).ToArray(),
        paymentSettings, settings);
      var pricers = new[] { projectionCurve, discountCurve }.GetTenorPricers(t => true);
      var basePvs = pricers.Select(p => p.Pv()).ToArray();
      for (int i = 0; i < basePvs.Length; ++i)
      {
        Assert.AreEqual(0.0, basePvs[i], 1E-7,
          "" + projectionCurve.Count + " tenors: " + pricers[i].Product.Description);
      }
      return;
    }

    [Test]
    public void RoundTrip()
    {
      for(int n = 2; n <= swapQuotes.Length;++n)
        DoTest(n);
    }
  }
}
