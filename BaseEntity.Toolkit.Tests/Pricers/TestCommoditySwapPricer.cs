//
// Copyright (c)    2018. All rights reserved.
//

using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  using NUnit.Framework;

  [TestFixture, Smoke]//, Ignore("No parameter specified")]

  public class TestCommoditySwapPricer : ToolkitTestBase
  {
    #region data

    private Dt AsOf;
    private CommodityPriceIndex commodityIndex1;
    private CommodityPriceIndex commodityIndex2;
    private CommodityCurve commodityCurve1;
    private CommodityCurve commodityCurve2;
    private DiscountCurve discountCurve;

    #endregion

    #region SetUp

    [SetUp]
    public void SetUpCurves()
    {
      AsOf = new Dt(31, 3, 2014);

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
      var rates = new IReferenceRate[]{CommodityReferenceRate.Get("WTI"), CommodityReferenceRate.Get("BRENT")};
      var exchanges = new[]{"CME","CME"};

      var quoteValues = new[,]
      {
        {
          101.14, 100.46, 99.62, 98.7, 97.77, 96.82, 95.93, 95.09, 94.18, 93.32, 92.57, 91.89, 91.31, 90.77, 90.14, 89.59, 89.14, 88.71, 88.36, 88.04, 87.55,
          87.08, 86.64, 86.25, 85.96, 85.71, 85.35, 85.04, 84.78, 84.57, 84.4, 84.27, 84, 83.75, 83.52, 83.29, 83.09, 82.91, 82.71, 82.56, 82.44, 82.35, 82.29,
          82.25, 82.11, 81.98, 81.86, 81.74, 81.63, 81.52, 81.38, 81.27, 81.17, 81.08, 81, 80.92, 80.83, 80.76, 80.69, 80.63, 80.58, 80.53, 80.46, 80.4, 80.35,
          80.3, 80.26, 80.22, 80.11, 80.01, 79.91, 79.81, 79.81, 79.81
        },
        {
          107.76, 107.65, 107.4, 107, 106.43, 105.89, 105.39, 104.9, 104.48, 104.09, 103.71, 103.37, 102.99, 102.57, 102.22, 101.85, 101.4, 100.99, 100.62,
          100.25, 99.91, 99.57, 99.25, 98.97, 98.68, 98.31, 98.06, 97.81, 97.48, 97.21, 96.95, 96.69, 96.48, 96.28, 96.08, 95.88, 95.68, 95.48, 95.27, 95.07,
          94.87, 94.67, 94.47, 94.27, 94.11, 93.95, 93.79, 93.63, 93.48, 93.33, 93.17, 93.01, 92.85, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
          0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0
        }
      };
      var quoteValues2 = quoteValues.Transpose();

      var quotes = CurveTenorFactory.BuildFuturesTenors(tenorNames, quoteValues2,
        new[] { QuotingConvention.ForwardFlatPrice },
        rates, null, exchanges);

      discountCurve = new DiscountCurve(AsOf, 0.01);
      commodityIndex1 = new CommodityPriceIndex(CommodityReferenceRate.Get("WTI"));
      commodityCurve1 = CommodityCurve.Create("WTI_Crude_Curve", AsOf, null,
        new CalibratorSettings(new CurveFitSettings(AsOf)),
        new DiscountCurve(AsOf, 0.01), null,
        commodityIndex1,
        null, quotes);
      commodityIndex2 = new CommodityPriceIndex(CommodityReferenceRate.Get("BRENT"));
      commodityCurve2 = CommodityCurve.Create("BRENT_Curve", AsOf, null,
        new CalibratorSettings(new CurveFitSettings(AsOf)),
        new DiscountCurve(AsOf, 0.01), null,
        commodityIndex2,
        null, quotes);
    }

    #endregion

    #region Tests

    [Test]
    public void CommodityFixedFloatSwap()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var maturity = Dt.AddMonth(AsOf, 1, true);
      var fixedLeg = new CommoditySwapLeg(effective, maturity, commodityIndex1.Currency, 100.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex1.Calendar, CycleRule.None, 0, false);
      var fixedLegPricer = new CommoditySwapLegPricer(fixedLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex1, commodityCurve1, null);
      var floatLeg = new CommoditySwapLeg(effective, maturity, commodityIndex1.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex1.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex1,
        CommodityPriceObservationRule.All, 0, false, 0, false);
      var floatLegPricer = new CommoditySwapLegPricer(floatLeg, AsOf, AsOf, -notional, discountCurve,
        commodityIndex1, commodityCurve1, null);
      var swapPricer = new CommoditySwapPricer(fixedLegPricer, floatLegPricer);
      var swapLegPv = fixedLegPricer.Pv() + floatLegPricer.Pv();
      var swapPv = swapPricer.Pv();
      Assert.AreEqual(swapLegPv, swapPv, 1E-8);
    }

    [Test]
    public void CommodityFloatFixedSwap()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var maturity = Dt.AddMonth(AsOf, 1, true);
      var fixedLeg = new CommoditySwapLeg(effective, maturity, commodityIndex1.Currency, 100.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex1.Calendar, CycleRule.None, 0, false);
      var fixedLegPricer = new CommoditySwapLegPricer(fixedLeg, AsOf, AsOf, -notional, discountCurve,
        commodityIndex1, commodityCurve1, null);
      var floatLeg = new CommoditySwapLeg(effective, maturity, commodityIndex1.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex1.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex1,
        CommodityPriceObservationRule.All, 0, false, 0, false);
      var floatLegPricer = new CommoditySwapLegPricer(floatLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex1, commodityCurve1, null);
      var swapPricer = new CommoditySwapPricer(floatLegPricer, fixedLegPricer);
      var swapLegPv = fixedLegPricer.Pv() + floatLegPricer.Pv();
      var swapPv = swapPricer.Pv();
      Assert.AreEqual(swapLegPv, swapPv, 1E-8);
    }

    [Test]
    public void CommodityBasisSwap()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var maturity = Dt.AddMonth(AsOf, 1, true);
      var floatLeg1 = new CommoditySwapLeg(effective, maturity, commodityIndex1.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex1.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex1,
        CommodityPriceObservationRule.All, 0, false, 0, false);
      var floatLegPricer1 = new CommoditySwapLegPricer(floatLeg1, AsOf, AsOf, -notional, discountCurve,
        commodityIndex1, commodityCurve1, null);
      var floatLeg2 = new CommoditySwapLeg(effective, maturity, commodityIndex2.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex2.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex2,
        CommodityPriceObservationRule.All, 0, false, 0, false);
      var floatLegPricer2 = new CommoditySwapLegPricer(floatLeg2, AsOf, AsOf, -notional, discountCurve,
        commodityIndex2, commodityCurve2, null);
      var swapPricer = new CommoditySwapPricer(floatLegPricer1, floatLegPricer2);
      var swapLegPv = floatLegPricer1.Pv() + floatLegPricer2.Pv();
      var swapPv = swapPricer.Pv();
      Assert.AreEqual(swapLegPv, swapPv, 1E-8);
    }

    [Test]
    public void CommoditySwapFixedFloatParCoupon()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var maturity = Dt.AddMonth(AsOf, 1, true);
      var fixedLeg = new CommoditySwapLeg(effective, maturity, commodityIndex1.Currency, 100.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex1.Calendar, CycleRule.None, 0, false);
      var fixedLegPricer = new CommoditySwapLegPricer(fixedLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex1, commodityCurve1, null);
      var floatLeg = new CommoditySwapLeg(effective, maturity, commodityIndex1.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex1.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex1,
        CommodityPriceObservationRule.All, 0, false, 0, false);
      var floatLegPricer = new CommoditySwapLegPricer(floatLeg, AsOf, AsOf, -notional, discountCurve,
        commodityIndex1, commodityCurve1, null);
      var swapPricer = new CommoditySwapPricer(fixedLegPricer, floatLegPricer);
      var parCoupon = swapPricer.ParCoupon();
      var parfixedLeg = new CommoditySwapLeg(effective, maturity, commodityIndex1.Currency, parCoupon, Frequency.Monthly,
       BDConvention.Following, commodityIndex1.Calendar, CycleRule.None, 0, false);
      var parfixedLegPricer = new CommoditySwapLegPricer(parfixedLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex1, commodityCurve1, null);
      var parSwapPricer = new CommoditySwapPricer(parfixedLegPricer, floatLegPricer);
      var parSwapPv = parSwapPricer.Pv();
      Assert.AreEqual(0.0, parSwapPv, 1E-8);
    }

    [Test]
    public void CommoditySwapFloatFixedParCoupon()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var maturity = Dt.AddMonth(AsOf, 1, true);
      var fixedLeg = new CommoditySwapLeg(effective, maturity, commodityIndex1.Currency, 100.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex1.Calendar, CycleRule.None, 0, false);
      var fixedLegPricer = new CommoditySwapLegPricer(fixedLeg, AsOf, AsOf, -notional, discountCurve,
        commodityIndex1, commodityCurve1, null);
      var floatLeg = new CommoditySwapLeg(effective, maturity, commodityIndex1.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex1.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex1,
        CommodityPriceObservationRule.All, 0, false, 0, false);
      var floatLegPricer = new CommoditySwapLegPricer(floatLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex1, commodityCurve1, null);
      var swapPricer = new CommoditySwapPricer(floatLegPricer, fixedLegPricer);
      var parCoupon = swapPricer.ParCoupon();
      var parfixedLeg = new CommoditySwapLeg(effective, maturity, commodityIndex1.Currency, parCoupon, Frequency.Monthly,
       BDConvention.Following, commodityIndex1.Calendar, CycleRule.None, 0, false);
      var parfixedLegPricer = new CommoditySwapLegPricer(parfixedLeg, AsOf, AsOf, -notional, discountCurve,
        commodityIndex1, commodityCurve1, null);
      var parSwapPricer = new CommoditySwapPricer(floatLegPricer, parfixedLegPricer);
      var parSwapPv = parSwapPricer.Pv();
      Assert.AreEqual(0.0, parSwapPv, 1E-8);
    }



    [Test]
    public void CommodityBasisSwapParCoupon()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var maturity = Dt.AddMonth(AsOf, 1, true);
      var floatLeg1 = new CommoditySwapLeg(effective, maturity, commodityIndex1.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex1.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex1,
        CommodityPriceObservationRule.All, 0, false, 0, false);
      var floatLegPricer1 = new CommoditySwapLegPricer(floatLeg1, AsOf, AsOf, notional, discountCurve,
        commodityIndex1, commodityCurve1, null);
      var floatLeg2 = new CommoditySwapLeg(effective, maturity, commodityIndex2.Currency, 10.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex2.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex2,
        CommodityPriceObservationRule.All, 0, false, 0, false);
      var floatLegPricer2 = new CommoditySwapLegPricer(floatLeg2, AsOf, AsOf, -notional, discountCurve,
        commodityIndex2, commodityCurve2, null);
      var swapPricer = new CommoditySwapPricer(floatLegPricer1, floatLegPricer2);
      var parCoupon = swapPricer.ParCoupon();
      var parLeg = new CommoditySwapLeg(effective, maturity, commodityIndex2.Currency, parCoupon, Frequency.Monthly,
        BDConvention.Following, commodityIndex2.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex2,
        CommodityPriceObservationRule.All, 0, false, 0, false);
      var parLegPricer = new CommoditySwapLegPricer(parLeg, AsOf, AsOf, -notional, discountCurve,
        commodityIndex2, commodityCurve2, null);
      var parSwapPricer = new CommoditySwapPricer(floatLegPricer1, parLegPricer);
      var parSwapPv = parSwapPricer.Pv();
      Assert.AreEqual(0.0, parSwapPv, 1E-8);
    }

    #endregion
  }
}
