//
// Copyright (c)    2002-2016. All rights reserved.
//

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Products.StandardProductTerms;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Curves
{
  [TestFixture]
  public class TestStandardProductsCommodityCurve
  {
    [Test]
    public void CommodityFuturesCurveRoundtrip()
    {
      var asOf = new Dt(31, 3, 2014);

      ReferenceRate.CacheInitialise();
      ToolkitCache.StandardProductTermsCache.Initialise();

      var tenorNames = new[]
      {
        "May14", "Jun14", "Jul14", "Aug14", "Sep14", "Oct14", "Nov14", "Dec14",
        "Jan15", "Feb15", "Mar15", "Apr15", "May15", "Jun15", "Jul15", "Aug15", "Sep15", "Oct15", "Nov15", "Dec15",
        "Jan16", "Feb16", "Mar16", "Apr16", "May16", "Jun16", "Jul16", "Aug16", "Sep16", "Oct16", "Nov16", "Dec16",
        "Jan17", "Feb17", "Mar17", "Apr17", "May17", "Jun17", "Jul17", "Aug17", "Sep17", "Oct17", "Nov17", "Dec17",
        "Jan18", "Feb18", "Mar18", "Apr18", "May18", "Jun18", "Jul18", "Aug18", "Sep18", "Oct18", "Nov18", "Dec18",
        "Jan19", "Feb19", "Mar19", "Apr19", "May19", "Jun19", "Jul19", "Aug19", "Sep19", "Oct19", "Nov19", "Dec19",
        "Jan20", "Feb20", "Mar20", "Apr20", "May20", "Jun20"
      };

      var commodityRate = CommodityReferenceRate.Get("WTI");
      var quoteValues = new[]
      {
          101.14, 100.46, 99.62, 98.7, 97.77, 96.82, 95.93, 95.09, 94.18, 93.32, 92.57, 91.89, 91.31, 90.77, 90.14, 89.59, 89.14, 88.71, 88.36, 88.04, 87.55,
          87.08, 86.64, 86.25, 85.96, 85.71, 85.35, 85.04, 84.78, 84.57, 84.4, 84.27, 84, 83.75, 83.52, 83.29, 83.09, 82.91, 82.71, 82.56, 82.44, 82.35, 82.29,
          82.25, 82.11, 81.98, 81.86, 81.74, 81.63, 81.52, 81.38, 81.27, 81.17, 81.08, 81, 80.92, 80.83, 80.76, 80.69, 80.63, 80.58, 80.53, 80.46, 80.4, 80.35,
          80.3, 80.26, 80.22, 80.11, 80.01, 79.91, 79.81, 79.81, 79.81
      };

      var quotes = CurveTenorFactory.BuildFuturesTenors(tenorNames, quoteValues,
        new[] {QuotingConvention.ForwardFlatPrice},
        commodityRate,
        null, null);

      var discountCurve = new DiscountCurve(asOf, 0.01);
      var commodityIndex = new Toolkit.Base.ReferenceIndices.CommodityPriceIndex(commodityRate);
      var commodityCurve = CommodityCurve.Create("WTI_Crude_Curve", asOf, null, new CalibratorSettings(new CurveFitSettings(asOf)), discountCurve,
        null, commodityIndex, null, quotes);

      var contracts = 1000.0;
      for (int i=0; i<tenorNames.Length; i++)
      {
        var future = StandardProductTermsUtil.GetStandardFuture<CommodityFuture>("CL", "CME", asOf, tenorNames[i]) as CommodityFuture;
        var contractSize = future.ContractSize;
        var pricerPv = new CommodityFuturesPricer(future, asOf, asOf, commodityCurve, contracts).Pv();
        var quoteScaled = contractSize * quoteValues[i] * contracts;
        NUnit.Framework.Assert.AreEqual(quoteScaled, pricerPv, quoteScaled * 1e-6);
      }
    }
  }
}
