//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Commodity Swap Leg Pricer tests
  /// </summary>
  [TestFixture, Smoke]//, Ignore("No parameter specified")]

  public class TestCommoditySwapLegPricer : ToolkitTestBase
  {
    #region data

    private Dt AsOf;
    private CommodityPriceIndex commodityIndex;
    private CommodityCurve commodityCurve;
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
      var rates = new IReferenceRate[] { CommodityReferenceRate.Get("WTI") };
      var quoteValues = new[,]
      {
        {
          101.14, 100.46, 99.62, 98.7, 97.77, 96.82, 95.93, 95.09, 94.18, 93.32, 92.57, 91.89, 91.31, 90.77, 90.14, 89.59, 89.14, 88.71, 88.36, 88.04, 87.55,
          87.08, 86.64, 86.25, 85.96, 85.71, 85.35, 85.04, 84.78, 84.57, 84.4, 84.27, 84, 83.75, 83.52, 83.29, 83.09, 82.91, 82.71, 82.56, 82.44, 82.35, 82.29,
          82.25, 82.11, 81.98, 81.86, 81.74, 81.63, 81.52, 81.38, 81.27, 81.17, 81.08, 81, 80.92, 80.83, 80.76, 80.69, 80.63, 80.58, 80.53, 80.46, 80.4, 80.35,
          80.3, 80.26, 80.22, 80.11, 80.01, 79.91, 79.81, 79.81, 79.81
        }
      };
      var quoteValues2 = quoteValues.Transpose();

      var quotes = CurveTenorFactory.BuildFuturesTenors(tenorNames, quoteValues2,
        new[] { QuotingConvention.ForwardFlatPrice },
        rates,
        null, null);

      discountCurve = new DiscountCurve(AsOf, 0.01);
      commodityIndex = new CommodityPriceIndex(CommodityReferenceRate.Get("WTI"));
      commodityCurve = CommodityCurve.Create("WTI_Crude_Curve", AsOf, null,
        new CalibratorSettings(new CurveFitSettings(AsOf)),
        discountCurve, null,
        commodityIndex,
        null, quotes);
    }

    #endregion

    #region Tests

    [Test]
    public void CommoditySwapLegAsianCalendarMonthAverage()
    {
      var notional = 10000.0;
      var effective = new Dt(1, 5, 2014);
      var maturity = new Dt(31, 5, 2014);
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex.Calendar, CycleRule.None, ProjectionType.AverageCommodityPrice, commodityIndex,
        CommodityPriceObservationRule.All, 0, false, 0, false);
      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, null);
      var value = notional * AveragePrice(effective, maturity) * discountCurve.Interpolate(Dt.Roll(maturity, BDConvention.Following, commodityIndex.Calendar));
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }

    [Test]
    public void CommoditySwapLegAsian()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var maturity = Dt.AddMonth(AsOf, 1, true);
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex,
        CommodityPriceObservationRule.All, 0, false, 0, false);
      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, null);
      var value = notional * AveragePrice(effective, maturity) * discountCurve.Interpolate(maturity);
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }

    [Test]
    public void CommoditySwapLegAsianWithFixings()
    {
      var notional = 10000.0;
      var resets = new RateResets();
      var resetValues = new[] { 100.0, 100.0, 100.0 };
      resets.Add(AsOf, resetValues[0]);
      var effective = Dt.AddDays(AsOf, -1, commodityIndex.Calendar);
      resets.Add(effective, resetValues[1]);
      effective = Dt.AddDays(effective, -1, commodityIndex.Calendar);
      resets.Add(effective, resetValues[2]);
      var maturity = Dt.AddMonth(effective, 1, true);
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex.Calendar, CycleRule.None, ProjectionType.AverageCommodityPrice, commodityIndex,
        CommodityPriceObservationRule.All, 0, false, 0, false);
      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, resets);
      var value = notional * AveragePrice(effective, maturity, false, resetValues)
        * discountCurve.Interpolate(Dt.Roll(maturity, BDConvention.Following, commodityIndex.Calendar));
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }

    [Test]
    public void CommoditySwapLegWeightedAsian()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var maturity = Dt.AddMonth(AsOf, 1, true);
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex,
        CommodityPriceObservationRule.All, 0, true, 0, false);
      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, null);
      var value = notional * WeightedAveragePrice(effective, maturity) * discountCurve.Interpolate(maturity);
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }

    [Test]
    public void CommoditySwapLegAsianTwoPayments()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var swap1End = Dt.AddMonth(AsOf, 1, true);
      var maturity = Dt.AddMonth(AsOf, 2, true);
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex,
        CommodityPriceObservationRule.All, 0, false, 0, false);
      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, null);
      var value1 = AveragePrice(effective, swap1End) * discountCurve.Interpolate(Dt.Roll(swap1End, BDConvention.Following, commodityIndex.Calendar));
      var value2 = AveragePrice(swap1End + 1, maturity) * discountCurve.Interpolate(Dt.Roll(maturity, BDConvention.Following, commodityIndex.Calendar));
      var value = notional * (value1 + value2);
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }

    [Test]
    public void CommoditySwapLegAsianTwoPaymentsWithRemainingNotionals()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var swap1End = Dt.AddMonth(AsOf, 1, true);
      var maturity = Dt.AddMonth(AsOf, 2, true);
      IList<Amortization> amortList = new List<Amortization>();
      amortList.Add(new Amortization(effective, AmortizationType.RemainingNotionalLevels, 1.0));
      amortList.Add(new Amortization(swap1End, AmortizationType.RemainingNotionalLevels, 0.5));
      
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex,
        CommodityPriceObservationRule.All, 0, false, 0, false);
      foreach (var o in amortList) swapLeg.AmortizationSchedule.Add(o);

      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, null);
      var value1 = AveragePrice(effective, swap1End) * discountCurve.Interpolate(Dt.Roll(swap1End, BDConvention.Following, commodityIndex.Calendar));
      var value2 = AveragePrice(swap1End + 1, maturity) * discountCurve.Interpolate(Dt.Roll(maturity, BDConvention.Following, commodityIndex.Calendar));
      var value = notional * (value1 + 0.5*value2);
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }


    [Test]
    public void CommoditySwapLegAsianRollExpiry()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var maturity = Dt.AddMonth(AsOf, 1, true);
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex,
        CommodityPriceObservationRule.All, 0, false, 0, true);
      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, null);

      var value = notional * AveragePrice(effective, maturity, true) * discountCurve.Interpolate(maturity);
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }

    [Test]
    public void CommoditySwapLegAsianPayLag()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var maturity = Dt.AddMonth(AsOf, 1, true);
      var payLag = 5;
      var bdconv = BDConvention.Following;
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, 0.0, Frequency.Monthly,
        bdconv, commodityIndex.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex,
        CommodityPriceObservationRule.All, 0, false, payLag, false);
      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, null);
      var df = discountCurve.Interpolate(Dt.Roll(Dt.AddDays(maturity, payLag, commodityIndex.Calendar), bdconv, commodityIndex.Calendar));
      var value = notional * AveragePrice(effective, maturity) * df;
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }

    [Test]
    public void CommoditySwapLegFirst()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var maturity = Dt.AddMonth(AsOf, 1, true);
      var nObs = 14;
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex,
        CommodityPriceObservationRule.First, nObs, false, 0, false);
      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, null);
      var value = notional * ForwardAveragePrice(effective, maturity, nObs) * discountCurve.Interpolate(maturity);
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }

    [Test]
    public void CommoditySwapLegFirstTwoPayments()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var nObs = 14;
      var swap1End = Dt.AddMonth(AsOf, 1, true);
      var maturity = Dt.AddMonth(AsOf, 2, true);
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex,
        CommodityPriceObservationRule.First, nObs, false, 0, false);
      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, null);
      var value1 = ForwardAveragePrice(effective, swap1End, nObs) * discountCurve.Interpolate(Dt.Roll(swap1End, BDConvention.Following, commodityIndex.Calendar));
      var value2 = ForwardAveragePrice(swap1End + 1, maturity, nObs) * discountCurve.Interpolate(Dt.Roll(maturity, BDConvention.Following, commodityIndex.Calendar));
      var value = notional * (value1 + value2);
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }

    [Test]
    public void CommoditySwapLegLast()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var maturity = Dt.AddMonth(AsOf, 1, true);
      var nObs = 14;
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex,
        CommodityPriceObservationRule.Last, nObs, false, 0, false);
      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, null);
      var value = notional * BackwardAveragePrice(effective, maturity, nObs) * discountCurve.Interpolate(maturity);
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }

    [Test]
    public void CommoditySwapLegLastTwoPayments()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var nObs = 14;
      var swap1End = Dt.AddMonth(AsOf, 1, true);
      var maturity = Dt.AddMonth(AsOf, 2, true);
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex,
        CommodityPriceObservationRule.Last, nObs, false, 0, false);
      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, null);
      var value1 = BackwardAveragePrice(effective, swap1End, nObs) * discountCurve.Interpolate(Dt.Roll(swap1End, BDConvention.Following, commodityIndex.Calendar));
      var value2 = BackwardAveragePrice(swap1End + 1, maturity, nObs) * discountCurve.Interpolate(Dt.Roll(maturity, BDConvention.Following, commodityIndex.Calendar));
      var value = notional * (value1 + value2);
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }

    [Test]
    public void CommoditySwapLegFirstOne()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var maturity = Dt.AddMonth(AsOf, 1, true);
      var nObs = 1;
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex,
        CommodityPriceObservationRule.First, 0, false, 0, false);
      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, null);
      var value = notional * ForwardAveragePrice(effective, maturity, nObs) * discountCurve.Interpolate(maturity);
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }

    [Test]
    public void CommoditySwapLegLastOne()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var maturity = Dt.AddMonth(AsOf, 1, true);
      var nObs = 1;
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, 0.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex.Calendar, CycleRule.EOM, ProjectionType.AverageCommodityPrice, commodityIndex,
        CommodityPriceObservationRule.Last, 0, false, 0, false);
      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, null);
      var value = notional * BackwardAveragePrice(effective, maturity, nObs) * discountCurve.Interpolate(maturity);
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }

    [Test]
    public void CommoditySwapLegFixed()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var maturity = Dt.AddMonth(AsOf, 1, true);
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, 100.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex.Calendar, CycleRule.None,  0, false);
      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, null);
      var value = notional * 100.0 * discountCurve.Interpolate(Dt.Roll(maturity, BDConvention.Following, commodityIndex.Calendar));
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }

    [Test]
    public void CommoditySwapLegFixedPayLag()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var maturity = Dt.AddMonth(AsOf, 1, true);
      var payLag = 5;
      var bdconv = BDConvention.Following;
      var price = 100.0;
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, price, Frequency.Monthly,
        bdconv, commodityIndex.Calendar, CycleRule.None, 5, false);
      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, null);
      var df = discountCurve.Interpolate(Dt.Roll(Dt.AddDays(maturity, payLag, commodityIndex.Calendar), bdconv, commodityIndex.Calendar));
      var value = notional * price * df;
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }

    [Test]
    public void CommoditySwapLegFixedTwoPaymentsWithRemainingNotionals()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var swap1End = Dt.AddMonth(AsOf, 1, true);
      var maturity = Dt.AddMonth(AsOf, 2, true);
      IList<Amortization> amortList = new List<Amortization>();
      amortList.Add(new Amortization(effective, AmortizationType.RemainingNotionalLevels, 1.0));
      amortList.Add(new Amortization(swap1End, AmortizationType.RemainingNotionalLevels, 0.5));
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, 100.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex.Calendar, CycleRule.None, 0, false);
      foreach (var o in amortList) swapLeg.AmortizationSchedule.Add(o);

      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, null);
      var value1 = 100.0 * discountCurve.Interpolate(Dt.Roll(swap1End, BDConvention.Following, commodityIndex.Calendar));
      var value2 = 100.0 * discountCurve.Interpolate(Dt.Roll(maturity, BDConvention.Following, commodityIndex.Calendar));
      var value = notional * (value1 + 0.5 * value2);
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }

    [Test]
    public void CommoditySwapLegFixedTwoPaymentsWithPriceSchedule()
    {
      var notional = 10000.0;
      var effective = AsOf;
      var swap1End = Dt.AddMonth(AsOf, 1, true);
      var maturity = Dt.AddMonth(AsOf, 2, true);
      IList<CouponPeriod> priceSched = new List<CouponPeriod>();
      priceSched.Add(new CouponPeriod(effective, 100.0));
      priceSched.Add(new CouponPeriod(swap1End, 50.0));
      var swapLeg = new CommoditySwapLeg(effective, maturity, commodityIndex.Currency, 100.0, Frequency.Monthly,
        BDConvention.Following, commodityIndex.Calendar, CycleRule.None, 0, false);
      foreach (var o in priceSched) swapLeg.PriceSchedule.Add(o);

      var swapLegPricer = new CommoditySwapLegPricer(swapLeg, AsOf, AsOf, notional, discountCurve,
        commodityIndex, commodityCurve, null);
      var value1 = 100.0 * discountCurve.Interpolate(Dt.Roll(swap1End, BDConvention.Following, commodityIndex.Calendar));
      var value2 = 50.0 * discountCurve.Interpolate(Dt.Roll(maturity, BDConvention.Following, commodityIndex.Calendar));
      var value = notional * (value1 + value2);
      var pv = swapLegPricer.Pv();
      Assert.AreEqual(value, pv, 1E-8);
    }


    #endregion

    #region Helper methods

    private double WeightedAveragePrice(Dt start, Dt end, bool rollExpiry = false, IReadOnlyList<double> resets = null)
    {
      var prev = start;
      var next = start;
      var futures = commodityCurve.Tenors.Where(o => o.Product is CommodityFuture)
        .Select(o => o.Product as CommodityFuture);
      var sum = 0.0;
      var count = 0;
      var weightedCount = 0;
      next = Dt.Roll(next, commodityIndex.Roll, commodityIndex.Calendar);
      while (next <= end)
      {
        if (next > end)
          break;
        var curveDate = futures.First(o => (!rollExpiry && o.LastTradingDate >= next) || o.LastTradingDate > next).LastDeliveryDate;
        var value = resets != null && count < resets.Count ? resets[count] : commodityCurve.Interpolate(curveDate);
        count++;
        var dayAfter = Dt.Roll(Dt.AddDays(next, 1, commodityIndex.Calendar), commodityIndex.Roll, commodityIndex.Calendar);
        var weight = Math.Max(1, Dt.Diff(next, dayAfter <= end ? dayAfter : end));
        weightedCount += weight;
        sum += weight * value;
        next = dayAfter;
      }
      return sum / (double)weightedCount;
    }

    private double AveragePrice(Dt start, Dt end, bool rollExpiry = false, IReadOnlyList<double> resets = null)
    {
      var next = start;
      var futures = commodityCurve.Tenors.Where(o => o.Product is CommodityFuture)
        .Select(o => o.Product as CommodityFuture);
      var sum = 0.0;
      var count = 0;
      while (next <= end)
      {
        next = Dt.Roll(next, commodityIndex.Roll, commodityIndex.Calendar);
        if (next > end)
          break;
        var curveDate = futures.First(o => (!rollExpiry && o.LastTradingDate >= next) || o.LastTradingDate > next).LastDeliveryDate;
        sum += resets != null && count < resets.Count ? resets[count] : commodityCurve.Interpolate(curveDate); count++;
        next = Dt.AddDays(next, 1, commodityIndex.Calendar);
      }
      return sum / (double)count;
    }

    private double ForwardAveragePrice(Dt start, Dt end, int nObs, bool rollExpiry = false)
    {
      nObs = nObs == 0 ? 1 : nObs;
      var next = start;
      var futures = commodityCurve.Tenors.Where(o => o.Product is CommodityFuture)
        .Select(o => o.Product as CommodityFuture);
      var sum = 0.0;
      var count = 0;
      while (next <= end)
      {
        next = Dt.Roll(next, commodityIndex.Roll, commodityIndex.Calendar);
        if (next > end)
          break;
        var curveDate = futures.First(o => (!rollExpiry && o.LastTradingDate >= next) || o.LastTradingDate > next).LastDeliveryDate;
        sum += commodityCurve.Interpolate(curveDate); count++;
        if ((int)count == nObs)
          break;
        next = Dt.AddDays(next, 1, commodityIndex.Calendar);
      }
      return sum / (double)count;
    }

    private double BackwardAveragePrice(Dt start, Dt end, int nObs, bool rollExpiry = false)
    {
      nObs = nObs == 0 ? 1 : nObs;
      var next = end;
      var futures = commodityCurve.Tenors.Where(o => o.Product is CommodityFuture)
        .Select(o => o.Product as CommodityFuture);
      var sum = 0.0;
      var count = 0;
      while (next >= start)
      {
        next = Dt.Roll(next, BDConvention.ModPreceding, commodityIndex.Calendar);
        if (next < start)
          break;
        var curveDate = futures.First(o => (!rollExpiry && o.LastTradingDate >= next) || o.LastTradingDate > next).LastDeliveryDate;
        sum += commodityCurve.Interpolate(curveDate); count++;
        if ((int)count == nObs)
          break;
        next = Dt.AddDays(next, -1, commodityIndex.Calendar);
      }
      return sum / (double)count;
    }

    #endregion
  }
}
