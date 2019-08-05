//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using BaseEntity.Configuration;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using DayOfWeek=BaseEntity.Toolkit.Base.DayOfWeek;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestSwapLegPricerAccrued
  {
    [TestCase("FB51756", 20130606, 20280430, 0, 0, CycleRule.None, DayCount.Actual360, Frequency.SemiAnnual, BDConvention.Modified, "TGT", true, 20170428, 0,
      0.0, 1e-13)]
    [TestCase("FB51756aocFalse", 20130606, 20280430, 0, 0, CycleRule.None, DayCount.Actual360, Frequency.SemiAnnual, BDConvention.Modified, "TGT", false,
      20170428, 0, 0.0, 1e-13)]
    [TestCase("FB49166", 20071022, 20190630, 20071231, 20181231, CycleRule.Thirtieth, DayCount.Actual365Fixed, Frequency.SemiAnnual, BDConvention.Modified,
      "TGT", false, 20160630, 0, 0.0, 1e-13)] // non-customized schedule equivalent
    public void Accrued(string testName, int effective, int maturity, int firstCoupon, int lastCoupon, CycleRule rule, DayCount dc, Frequency freq,
      BDConvention bdc, string cal, bool accrueOnCycle, int asOf, int daysToSettle, double expected, double tolerance)
    {
      var calendar = new Calendar(cal);
      var asOfDt = new Dt(asOf);
      var settleDt = Dt.AddDays(asOfDt, daysToSettle, calendar);
      var discountData = SwapLegTestUtils.GetDiscountData(asOfDt);

      var sl = new SwapLeg(new Dt(effective), new Dt(maturity), Currency.None, 0.05, dc, freq, bdc,
        calendar, accrueOnCycle);
      if (firstCoupon != 0)
        sl.FirstCoupon = new Dt(firstCoupon);
      if (lastCoupon != 0)
        sl.LastCoupon = new Dt(lastCoupon);
      if (rule != CycleRule.None)
        sl.CycleRule = rule;

      var slp = new SwapLegPricer(sl, asOfDt, settleDt, 1e6, discountData.GetDiscountCurve(), null, null, null, null, null);
      Assert.AreEqual(expected, slp.Accrued(), tolerance, testName);
    }

    [TestCase("FB52261_0", 20161220, 20170315, 0, 0, CycleRule.None, DayCount.Actual360, Frequency.Quarterly, BDConvention.Modified, "NYB", true, 20170314, 1,
      47222.2222, 0.0001)]
    [TestCase("FB52261_1", 20161220, 20170315, 0, 0, CycleRule.None, DayCount.Actual360, Frequency.Quarterly, BDConvention.Modified, "NYB", true, 20170316, 1,
      47222.2222, 0.0001)]
    public void AccruedWithPaymentLag(string testName, int effective, int maturity, int firstCoupon, int lastCoupon, CycleRule rule, DayCount dc, Frequency freq,
      BDConvention bdc, string cal, bool accrueOnCycle, int asOf, int daysToSettle, double expected, double tolerance)
    {
      var calendar = new Calendar(cal);
      var asOfDt = new Dt(asOf);
      var settleDt = Dt.AddDays(asOfDt, daysToSettle, calendar);
      var discountData = SwapLegTestUtils.GetDiscountData(asOfDt);

      var sl = new SwapLeg(new Dt(effective), new Dt(maturity), freq, 0.0, SwapLegTestUtils.GetLiborIndex("3M"))
      {
        AccrueOnCycle = accrueOnCycle,
        PaymentLagRule = new PayLagRule(3, true)
      };
      if (firstCoupon != 0)
        sl.FirstCoupon = new Dt(firstCoupon);
      if (lastCoupon != 0)
        sl.LastCoupon = new Dt(lastCoupon);
      if (rule != CycleRule.None)
        sl.CycleRule = rule;

      var slp = new SwapLegPricer(sl, asOfDt, settleDt, 20e6, discountData.GetDiscountCurve(), sl.ReferenceIndex, discountData.GetDiscountCurve(), new RateResets(0.01, 0.01), null, null)
      {
        TradeSettle = Dt.AddMonths(asOfDt, -1, CycleRule.None)
      };
      var accrued = slp.Accrued();
      Assert.AreEqual(expected, accrued, tolerance, testName);
    }
  }

  /// <summary>
  /// Test the swap leg pricer
  /// </summary>
  [TestFixture("01")]
  [TestFixture("02")]
  [Smoke]
  public class TestSwapLegPricer : ToolkitTestBase
  {
    public TestSwapLegPricer(string name): base(name) {}

    #region Tests

    protected static Dt AsOf { get; set; }
    protected static DiscountData DiscountData { get; set; }

    [SetUp]
    public void SetUpDiscount()
    {
      AsOf = new Dt(1, 1, 2001);
      DiscountData = SwapLegTestUtils.GetDiscountData(AsOf);
    }

    [Test]
    public void TestClone()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();

      {
        ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
        SwapLegPricer sp = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
        bool ok = true;
        try
        {
          SwapLegPricer clone = CloneUtil.CloneObjectGraph(sp, CloneMethod.Serialization);
        }
        catch (Exception)
        {
          ok = false;
        }
        Assert.IsTrue(ok, "Failed to clone a LIBOR swap leg pricer");
      }

      {
        ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
        SwapLegPricer sp1 = SwapLegTestUtils.GetFixedSwapPricer(AsOf, dc, 0.05, -1.0);
        SwapLegPricer sp2 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
        var sp = new SwapPricer(sp1, sp2);
        bool ok = true;
        try
        {
          SwapPricer clone = CloneUtil.CloneObjectGraph(sp, CloneMethod.Serialization);
        }
        catch (Exception)
        {
          ok = false;
        }
        Assert.IsTrue(ok, "Failed to clone a swap pricer (made from 2 legs)");
      }
    }

    /// <summary>
    /// Test par coupon calcs for simple LIBOR swaps against the swap rates used to bootstrap a curve
    /// </summary>
    [Test]
    public void VanillaParCouponsVsLiborCurve()
    {
      DiscountCurve discountCurve = DiscountData.GetDiscountCurve();

      double notional = 1e6; // 1MM

      int i = 0;
      foreach (string tenor in DiscountData.Bootst.SwapTenors)
      {
        var fixedLeg = new SwapLeg(AsOf,
                                   Dt.Add(AsOf, tenor),
                                   Currency.USD,
                                   0.10, // fixed coupon
                                   DiscountData.Bootst.MmDayCount,
                                   DiscountData.Bootst.SwapFrequency,
                                   BDConvention.Modified,
                                   Calendar.NYB, false,
                                   Tenor.Parse(tenor),
                                   null);

        var floatingLeg = new SwapLeg(AsOf,
                                      Dt.Add(AsOf, tenor),
                                      Currency.USD,
                                      0.0, // spread
                                      DiscountData.Bootst.SwapDayCount,
                                      Frequency.Quarterly,
                                      BDConvention.Modified,
                                      Calendar.NYB, false,
                                      Tenor.Parse("3M"),
                                      "LIBOR");


        var fixedLegPricer = new SwapLegPricer(fixedLeg,
                                               AsOf,
                                               AsOf,
                                               -notional,
                                               discountCurve,
                                               null,
                                               null,
                                               null,
                                               null, null);


        var floatingLegPricer = new SwapLegPricer(floatingLeg,
                                                  AsOf,
                                                  AsOf,
                                                  notional,
                                                  discountCurve,
                                                  SwapLegTestUtils.GetLiborIndex("3M"),
                                                  discountCurve,
                                                  new RateResets(0.0, 0.0),
                                                  null, null);

        var swapPricer = new SwapPricer(floatingLegPricer, fixedLegPricer);

        double parCoupon = swapPricer.ParCoupon();

        double tolerance = 0.0001; // 1 bp
        double specialCaseToleranceForOneYearSwap = tolerance*1.28;
        // hate to do this this. Hope this all goes away in 9.4 with a better calibrator.
        switch (tenor)
        {
          case "1Y":
            Assert.AreEqual(DiscountData.Bootst.SwapRates[i++], parCoupon,
              specialCaseToleranceForOneYearSwap, "ParCoupon for swap tenor " + tenor);
            break;
          default:
            Assert.AreEqual(DiscountData.Bootst.SwapRates[i++],
              parCoupon, tolerance, "ParCoupon for swap tenor " + tenor);
            break;
        }
      }
      return;
    }

    /// <summary>
    /// Make sure break even spread ties out for two very 
    /// similar floating legs that only differ by spread
    /// </summary>
    [Test]
    public void BreakEvenSpread()
    {
      var sl1 = new SwapLeg(AsOf, Dt.Add(AsOf, "5 Y"), Currency.None, 0, DayCount.Actual360, Frequency.Quarterly,
                            BDConvention.Following, Calendar.None, false, Tenor.Parse("3M"), "LIBOR");
      var sl2 = (SwapLeg) sl1.ShallowCopy();
      sl1.Coupon = .0001; // 1 bp

      var sp1 = new SwapLegPricer(sl1, AsOf, AsOf, 1, DiscountData.GetDiscountCurve(),
                                  SwapLegTestUtils.GetLiborIndex("3M"),
                                  DiscountData.GetDiscountCurve(), new RateResets(0.0, 0.0), null, null);
      var sp2 = new SwapLegPricer(sl2, AsOf, AsOf, -1, DiscountData.GetDiscountCurve(),
                                  SwapLegTestUtils.GetLiborIndex("3M"),
                                  DiscountData.GetDiscountCurve(), new RateResets(0.0, 0.0), null, null);

      var sp = new SwapPricer(sp2, sp1);
      Assert.AreEqual(0, sp.ParCoupon());
    }

    /// <summary>
    /// Make sure that a swap leg with an index multiplier is equal to a swap 
    /// leg without margin multiplied by the index multiplier
    /// and a swap leg with coupons only
    /// </summary>
    [Test]
    public void TestIndexMultiplierSwapWithProjectedRates()
    {
      var indexMultiplier = 1.5;
      var effective = AsOf;
      var maturity = Dt.Add(AsOf, "5Y");
      var index = SwapLegTestUtils.GetLiborIndex("3M");

      var swapLegWithMultiplier = GetSwapLeg(effective, maturity, 0.05, indexMultiplier);
      var swapLegNoMarginNoMultiplier = GetSwapLeg(effective, maturity, 0.0, 1.0);
      var swapLegMarginOnly = GetSwapLeg(effective, maturity, 0.05, 0.0);
      var swapPricerWithMultiplier = GetPricer(swapLegWithMultiplier, index);
      var swapPricerNoMarginNoMultiplier = GetPricer(swapLegNoMarginNoMultiplier, index);
      var swapPricerMarginOnly = GetPricer(swapLegMarginOnly, index);
      var actual = swapPricerWithMultiplier.Pv();
      var expect = swapPricerNoMarginNoMultiplier.Pv() * indexMultiplier 
        + swapPricerMarginOnly.Pv();

      Assert.AreEqual(actual, expect);
    }

    //Test that if index factor is zero, then the floating leg can be 
    //considered as a fixed leg. Yet for the ISDA compounding convention, 
    // this is not true.
    [TestCase(CompoundingConvention.None, Frequency.None)]
    [TestCase(CompoundingConvention.FlatISDA, Frequency.None)]
    [TestCase(CompoundingConvention.Simple, Frequency.None)]
    [TestCase(CompoundingConvention.FlatISDA, Frequency.BiWeekly)]
    [TestCase(CompoundingConvention.Simple, Frequency.BiWeekly)]
    [TestCase(CompoundingConvention.FlatISDA, Frequency.Monthly)]
    [TestCase(CompoundingConvention.Simple, Frequency.Monthly)]
    [TestCase(CompoundingConvention.FlatISDA, Frequency.Quarterly)]
    [TestCase(CompoundingConvention.Simple, Frequency.Quarterly)]
    public void TestIndexFactorCmpd(CompoundingConvention cmpdCont, Frequency freq)
    {
      var effective = AsOf;
      var maturity = Dt.Add(AsOf, "3M");
      var index3M = SwapLegTestUtils.GetLiborIndex("3M");
      var swapleg = GetSwapLeg(effective, maturity, 1.0, 0.0);
      var pricer3M = GetPricer(swapleg, index3M);
      pricer3M.SwapLeg.CompoundingConvention = cmpdCont;
      pricer3M.SwapLeg.CompoundingFrequency = freq;

      var fixedSwapLeg = new SwapLeg(effective, maturity, Currency.None, 1.0,
        DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, 
        Calendar.None, false)
      {
        CompoundingConvention = cmpdCont,
        CompoundingFrequency = freq
      };
      var fixedPricer = GetPricer(fixedSwapLeg, null);
      var pv1 = pricer3M.Pv();
      var pv2 = fixedPricer.Pv();
      Assert.AreEqual(pv1, pv2, 1E-14);
    }


    //The first order derivative of index factor at zero point for the 
    //two-period compounding should equal to the (L1*fraction1+L2*fraction2)*DF
    [TestCase(1E-8)]
    [TestCase(-1E-8)]
    public void TestIndexFactorCmpdFirstOrderDerivative(double indexFactor)
    {
      var effecitve = AsOf;
      var matuiry = Dt.Add(effecitve, Tenor.Parse("6M"));
      var rateResets= new RateResets(new Dt(20001223), 0.1);
      var swapLeg = GetSwapLeg(effecitve, matuiry, 0.0, indexFactor);
      swapLeg.Freq = Frequency.SemiAnnual;
      var index = SwapLegTestUtils.GetLiborIndex("6M");
      var pricer6M = GetPricer(swapLeg, index, rateResets);
      pricer6M.SwapLeg.CompoundingConvention = CompoundingConvention.ISDA;
      pricer6M.SwapLeg.CompoundingFrequency = Frequency.Quarterly;

      var value1 = pricer6M.Pv()/indexFactor;
      pricer6M.SwapLeg.IndexMultiplierSchedule.Add(new CouponPeriod(Dt.MinValue, 1.0));
      pricer6M.SwapLeg.CompoundingConvention = CompoundingConvention.Simple;

      var value2 = pricer6M.Pv();

      value2.IsExpected(To.Match(value1).Within(1E-12));
    }

    /// <summary>
    /// Make sure that a swap leg with an index multiplier is equal to a swap 
    /// leg without margin multiplied by the index multiplier and a swap leg with 
    /// coupons only
    /// </summary>
    [Test]
    public void TestIndexMultiplierSwapWithCap()
    {
      var indexMultiplier = 1.5;
      var effective = AsOf + 1;
      var maturity = Dt.Add(effective, "5Y");
      var index = SwapLegTestUtils.GetLiborIndex("3M");

      var swapLegWithMultiplier = GetSwapLeg(effective, maturity, 0.05, indexMultiplier);
      swapLegWithMultiplier.Cap = 2.0;
      swapLegWithMultiplier.Floor = 2.0;

      var swapLegNoMarginNoMultiplier = GetSwapLeg(effective, maturity, 0.0, 1.0);
      var swapLegMarginOnly = GetSwapLeg(effective, maturity, 0.05,  0.0);

      var swapPricer1 = GetPricer(swapLegWithMultiplier, index);
      var swapPricerNoMarginNoMultiplier = GetPricer(swapLegNoMarginNoMultiplier, index);
      var swapPricerMarginOnly = GetPricer(swapLegMarginOnly, index);

      Assert.AreEqual(swapPricer1.Pv(), swapPricerNoMarginNoMultiplier.Pv() * indexMultiplier 
        + swapPricerMarginOnly.Pv(), 1E-14);
    }

    /// <summary>
    /// Make sure that a swap leg with an index multiplier is equal to a swap leg 
    /// without margin multiplied by the index multiplier
    /// and a swap leg with coupons only
    /// </summary>
    [Test]
    public void TestIndexMultiplierSwapWithFixings()
    {
      var effective = AsOf - 200;
      var maturity = Dt.Add(effective, "5Y");
      var indexMultiplier = 1.5;
      var index = SwapLegTestUtils.GetLiborIndex("3M");

      var swapLegWithMultiplier = GetSwapLeg(effective, maturity, 0.005, indexMultiplier);
      var swapLegNoMarginNoMultiplier = GetSwapLeg(effective, maturity, 0.0, 1.0);
      var swapLegMarginOnly = GetSwapLeg(effective, maturity, 0.005, 0.0); 

      var swapPricer1 =GetPricer(swapLegWithMultiplier, index,
        new RateResets(new Dt(13, 12, 2000), 0.005));
      var swapPricerNoMarginNoMultiplier =GetPricer(swapLegNoMarginNoMultiplier, index,
        new RateResets(new Dt(13, 12, 2000), 0));
      var swapPricerMarginOnly =GetPricer(swapLegMarginOnly, index,
        new RateResets(new Dt(13, 12, 2000), 0.005));

      Assert.AreEqual(swapPricer1.Pv(), swapPricerNoMarginNoMultiplier.Pv() * indexMultiplier 
        + swapPricerMarginOnly.Pv(), 1E-14);
    }

    /// <summary>
    /// Make sure that a swap leg with an index multiplier is equal to a 
    /// swap leg without margin multiplied by the index multiplier
    /// and a swap leg with coupons only
    /// </summary>
    [Test]
    public void TestIdxMltplierSchedSwapWithProjectedRates()
    {
      var effective = AsOf + 1;
      Dt fiveYearDate = Dt.Add(effective, "5Y");
      Dt tenYearDate = Dt.Add(effective, "10Y");
      var indexMultiplier1 = 1.5;
      var indexMultiplier2 = 2.0;
      var index = SwapLegTestUtils.GetLiborIndex("3M");

      var indexMultiplierSched = new List<CouponPeriod>();
      indexMultiplierSched.Add(new CouponPeriod(effective, indexMultiplier1));
      indexMultiplierSched.Add(new CouponPeriod(fiveYearDate, indexMultiplier2));

      var swapLegWithMultiplier = GetSwapLeg(effective, tenYearDate, 0.05, indexMultiplier1);
      swapLegWithMultiplier.IndexMultiplierSchedule = indexMultiplierSched;

      var swapLegNoMarginNoMultiplier1 = GetSwapLeg(effective, fiveYearDate, 0.0, 1.0);
      var swapLegNoMarginNoMultiplier2 = GetSwapLeg(fiveYearDate, tenYearDate, 0.0, 1.0);
      var swapLegMarginOnly = GetSwapLeg(effective, tenYearDate, 0.05, 0.0);

      var swapPricer1 =GetPricer(swapLegWithMultiplier, index);
      var swapPricerNoMarginNoMultiplier1 =GetPricer(swapLegNoMarginNoMultiplier1, index);
      var swapPricerNoMarginNoMultiplier2 =GetPricer(swapLegNoMarginNoMultiplier2, index);
      var swapPricerMarginOnly =GetPricer(swapLegMarginOnly, index);

      Assert.AreEqual(swapPricer1.Pv(), swapPricerNoMarginNoMultiplier1.Pv() * indexMultiplier1 
        + swapPricerNoMarginNoMultiplier2.Pv() * indexMultiplier2  + swapPricerMarginOnly.Pv(),
        1E-14);
    }

    /// <summary>
    /// Make sure that a swap leg with an index multiplier is equal to a swap 
    /// leg without margin multiplied by the index multiplier
    /// and a swap leg with coupons only
    /// </summary>
    [Test]
    public void TestIndexMultiplierScheduleSwapWithFixings()
    {
      var effective = AsOf + 1;
      Dt fiveYearDate = Dt.Add(effective, "5Y");
      Dt tenYearDate = Dt.Add(effective, "10Y");
      var index = SwapLegTestUtils.GetLiborIndex("3M");

      var indexMultiplier1 = 1.5;
      var indexMultiplier2 = 2.0;
      var indexMultiplierSched = new List<CouponPeriod>();
      indexMultiplierSched.Add(new CouponPeriod(effective, indexMultiplier1));
      indexMultiplierSched.Add(new CouponPeriod(fiveYearDate, indexMultiplier2));

      var swapLegWithMultiplier = GetSwapLeg(effective, tenYearDate, 0.05, 1.2);
      swapLegWithMultiplier.IndexMultiplierSchedule = indexMultiplierSched;

      var swapLegNoMarginNoMultiplier1 = GetSwapLeg(effective, fiveYearDate, 0.0, 1.0);
      var swapLegNoMarginNoMultiplier2 = GetSwapLeg(fiveYearDate, tenYearDate, 0.0, 1.0);
      var swapLegMarginOnly = GetSwapLeg(effective, tenYearDate, 0.05, 0.0);

      var swapPricerWithMultiplier =GetPricer(swapLegWithMultiplier, 
        index, new RateResets(new Dt(13, 12, 2000), 0.005));
      var swapPricerNoMarginNoMultiplier1 =GetPricer(swapLegNoMarginNoMultiplier1, 
        index, new RateResets(new Dt(13, 12, 2000), 0));
      var swapPricerNoMarginNoMultiplier2 =GetPricer(swapLegNoMarginNoMultiplier2, 
        index,  new RateResets(new Dt(13, 12, 2000), 0));
      var swapPricerMarginOnly =GetPricer(swapLegMarginOnly, 
        index, new RateResets(new Dt(13, 12, 2000), 0.005));

      Assert.AreEqual(swapPricerWithMultiplier.Pv(), swapPricerNoMarginNoMultiplier1.Pv() * indexMultiplier1 
        + swapPricerNoMarginNoMultiplier2.Pv() * indexMultiplier2 + swapPricerMarginOnly.Pv(), 1E-14);
    }

    /// <summary>
    /// Make sure that a swap leg with an index multiplier is equal to a swap 
    /// leg without margin multiplied by the index multiplier
    /// and a swap leg with coupons only
    /// </summary>
    [Test]
    public void TestIndexMultiplierScheduleSwapWithCapAndFixings()
    {
      Dt fiveYearDate = Dt.Add(AsOf + 1, "5 Y");
      Dt tenYearDate = Dt.Add(AsOf + 1, "10 Y");
      var index = SwapLegTestUtils.GetLiborIndex("3M");

      var indexMultiplier1 = 1.5;
      var indexMultiplier2 = 2.0;
      var indexMultiplierSched = new List<CouponPeriod>();
      indexMultiplierSched.Add(new CouponPeriod(AsOf + 1, indexMultiplier1));
      indexMultiplierSched.Add(new CouponPeriod(fiveYearDate, indexMultiplier2));
      var swapLegWithMultiplier = GetSwapLeg(AsOf + 1, tenYearDate, 0.05, 1.2);
      swapLegWithMultiplier.Cap = 2.0;
      swapLegWithMultiplier.Floor = 2.0;

      swapLegWithMultiplier.IndexMultiplierSchedule = indexMultiplierSched;

      var swapLegNoMarginNoMultiplier1 =
        GetSwapLeg(AsOf + 1, fiveYearDate, 0.0, 1.0);
      var swapLegNoMarginNoMultiplier2 =
        GetSwapLeg(fiveYearDate, tenYearDate, 0.0, 1.0);
      var swapLegMarginOnly = 
        GetSwapLeg(AsOf + 1, tenYearDate, 0.05, 0.0);

      var swapPricer1 =
        GetPricer(swapLegWithMultiplier, index, new RateResets(new Dt(13, 12, 2000), 0.005));
      var swapPricerNoMarginNoMultiplier1 =
        GetPricer(swapLegNoMarginNoMultiplier1, index, new RateResets(new Dt(13, 12, 2000), 0));
      var swapPricerNoMarginNoMultiplier2 =
        GetPricer(swapLegNoMarginNoMultiplier2, index, new RateResets(new Dt(13, 12, 2000), 0));
      var swapPricerMarginOnly =
        GetPricer(swapLegMarginOnly, index, new RateResets(new Dt(13, 12, 2000), 0.005));

      Assert.AreEqual(swapPricer1.Pv(), swapPricerNoMarginNoMultiplier1.Pv() * indexMultiplier1 
        + swapPricerNoMarginNoMultiplier2.Pv() * indexMultiplier2 + swapPricerMarginOnly.Pv(), 1E-14);
    }

    private static SwapLeg GetSwapLeg(Dt effective, Dt maturity, double spread, double? indexFactor)
    {
      var swapleg =new  SwapLeg(effective, maturity, Currency.None, spread, DayCount.Actual360,
        Frequency.Quarterly, BDConvention.Following, Calendar.None, false, Tenor.Parse("3M"), "LIBOR");
      
      swapleg.IndexMultiplierSchedule.Add(indexFactor.HasValue
        ? new CouponPeriod(Dt.MinValue, indexFactor.Value)
        : new CouponPeriod(Dt.MinValue, 1.0));
      return swapleg;
    }


    private static SwapLegPricer GetPricer(SwapLeg swapLeg, 
      ReferenceIndex index, RateResets rateResets = null)
    {
      var rateReset = rateResets ?? new RateResets(0.0, 0.0);
      return new SwapLegPricer(swapLeg, AsOf, AsOf, 1,
        DiscountData.GetDiscountCurve(), index,
        DiscountData.GetDiscountCurve(), rateReset, null, null);
    }

    #endregion Tests

    #region Helpers

    #endregion Helpers

    #region Properties

    #endregion Properties
  }

  [TestFixture]
  public class TestSwapLegPricerEx
  {
    [Test]
    public void TestIboxxTrs()
    {
      const double expect = 81220.317460317121;
      var path = Path.Combine(SystemContext.InstallDir,
        @"toolkit/test/data/iboxtrs7050.xml");

      var pricer = XmlSerialization.ReadXmlFile(path) as AssetReturnSwapPricer;
      if (pricer != null)
      {
        var pv = pricer.Pv();
        NUnit.Framework.Assert.AreEqual(expect, pv, 1e-14);
      }
    }
  }


  /// <exclude></exclude>
  internal class SwapLegTestUtils
  {
    /// <exclude></exclude>
    internal static double InitialInflation() 
    {
      return 100;
    }
    
    /// <exclude></exclude>
    internal static InflationIndex GetCPIIndex(Dt asOf)
    {
      return new InflationIndex("CPI", Currency.None, DayCount.Actual360, Calendar.NYB,
                                BDConvention.Following, Frequency.Monthly, Tenor.Empty);
    }

    /// <exclude></exclude>
    internal static InflationIndex GetCPIIndex(Dt asOf, Tenor releaseLag)
    {
      return new InflationIndex("CPI", Currency.None, DayCount.Actual360, Calendar.NYB,
                                BDConvention.Following, Frequency.Monthly, releaseLag);
    }

    /// <exclude></exclude>
    internal static InflationCurve GetCPICurve(Dt asOf, DiscountCurve dc, InflationIndex index)
    {

      Dt effective = Dt.Add(asOf, 2);
      var inflationFactor = new InflationFactorCurve(asOf)
                            {
                              Calibrator = new InflationCurveFitCalibrator(asOf, asOf, dc, index, new CalibratorSettings())
                            };
      var retVal = new InflationCurve(asOf, InitialInflation(), inflationFactor, null);
      for (int i = 1; i <= 10; ++i)
      {
        var fix = new SwapLeg(effective, Dt.Add(effective, i, TimeUnit.Years), Currency.None, 0.0,
                              DayCount.Thirty360, Frequency.None, BDConvention.Following, Calendar.NYB, false)
                  {
                    IsZeroCoupon = true,
                    CompoundingFrequency = Frequency.Annual
                  };
        var floating = new SwapLeg(effective, Dt.Add(effective, i, TimeUnit.Years), Frequency.None, 0.0, index,
                                   Currency.None, DayCount.Actual360, BDConvention.Following, Calendar.NYB);
        retVal.AddInflationSwap(new Swap(fix, floating), 0.05);
      }
      retVal.Fit();
      return retVal;
    }

    /// <exclude></exclude>
    internal static DiscountData GetDiscountData(Dt AsOf)
    {
      return new DiscountData
               {
                 AsOf = AsOf.ToStr("%D"),
                 Bootst = new DiscountData.Bootstrap
                            {
                              MmDayCount = DayCount.Actual360,
                              MmTenors = new[] { "1M", "2M", "3M", "6M", "9M" },
                              MmRates = new[] { 0.011, 0.012, 0.013, 0.016, 0.019 },
                              SwapDayCount = DayCount.Actual360,
                              SwapFrequency = Frequency.SemiAnnual,
                              SwapInterp = InterpMethod.Cubic,
                              SwapExtrap = ExtrapMethod.Const,
                              // of fixed leg of swap
                              SwapTenors =
                                new[] { "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y", "10Y", "15Y", "30Y" },
                              SwapRates =
                                new[] { 0.022, 0.023, 0.024, 0.025, 0.026, 0.027, 0.028, 0.029, 0.030, 0.035, 0.04 }
                            },
                 Category = "Empty",
                 // null works badly
                 Name = "MyDiscountCurve" // ditto
               };
    }

    /// <exclude></exclude>
    internal static DiscountCurve GetTSYCurve(Dt asOf)
    {
      var dd = new DiscountData
                 {
                   AsOf = asOf.ToStr("%D"),
                   Bootst = new DiscountData.Bootstrap
                              {
                                MmDayCount = DayCount.Thirty360,
                                MmTenors = new[] { "1D", "3M", "6M", "1Y", "2Y", "3Y", "5Y", "7Y", "10Y", "30Y" },
                                MmRates =
                                  new[]
                                    {
                                      0.00261, 0.00289, 0.00299, 0.00318, 0.00475, 0.00639, 0.00875, 0.01059, 0.01180,
                                      0.01358
                                    }
                              },
                   Category = "CMT",
                   // null works badly
                   Name = "TSYDiscountCurve" // ditto
                 };

      return dd.GetDiscountCurve();
    }

    /// <exclude></exclude>
    internal static InterestRateIndex GetLiborIndex(string tenor)
    {
      return new InterestRateIndex("USDLIBOR" + tenor, Tenor.Parse(tenor), Currency.USD,
                                   DayCount.Actual360, Calendar.NYB, BDConvention.Following, 2);
    }

    /// <exclude></exclude>
    internal static ReferenceIndex GetCMSIndex(string tenor)
    {
      var fwdIndex = new InterestRateIndex("ForwardRateIndex", new Tenor(Frequency.SemiAnnual),
                                                 Currency.USD, DayCount.Actual360,
                                                 Calendar.NYB, BDConvention.Following, 0);
      return new SwapRateIndex("CMS" + tenor, Tenor.Parse(tenor), Frequency.SemiAnnual, Currency.USD, DayCount.Actual360,
                               Calendar.NYB, BDConvention.Following, 2, fwdIndex);
    }

    /// <exclude></exclude>
    internal static ReferenceIndex GetCMTIndex(string tenor)
    {
      var fwdIndex = new InterestRateIndex("ForwardRateIndex", new Tenor(Frequency.SemiAnnual),
                                                 Currency.USD, DayCount.Thirty360,
                                                 Calendar.NYB, BDConvention.Following, 0);
      return new SwapRateIndex("CMT" + tenor, Tenor.Parse(tenor), Frequency.SemiAnnual, Currency.USD, DayCount.Thirty360,
                               Calendar.NYB, BDConvention.Following, 2, fwdIndex);
    }


    /// <exclude></exclude>
    internal static RateResets GetCPIResets(double initial, Dt asOf)
    {
      var rr = new RateResets();
      var rdt = new Dt(1, asOf.Month, asOf.Year);
      for (int i = 0; i < 100; i++)
      {
        rr.AllResets.Add(rdt, initial*(1.0 - i*1e-3));
        rdt = Dt.Add(rdt, -1, TimeUnit.Months);
      }
      return rr;
    }


    /// <exclude></exclude>
    internal static RateResets GetHistoricalResets(Dt asOf, Frequency freq, CycleRule rule, Calendar calendar, string tenor)
    {
      List<RateReset> rateResets = new List<RateReset>();
      Dt past = Dt.Add(asOf, -5, TimeUnit.Years);
      if (freq == Frequency.Weekly)
        past = Dt.AddWeeks(past, -1, rule);
      else if (freq == Frequency.Monthly)
        past = Dt.AddMonths(past, -1, rule);
      else
        past = Dt.AddDays(past, -1, calendar);
      Dt rd = past;
      int i = 0;
      while (rd < asOf)
      {
        ++i;
        rateResets.Add(new RateReset(rd, 0.025 + (i % 10) * 0.001));
        rd = (freq == Frequency.Weekly)
               ? Dt.AddWeeks(rd, 1, rule)
               : (freq == Frequency.Monthly) ? Dt.AddMonths(rd, 1, rule) : Dt.AddDays(rd, 1, calendar);
      }
      RateResets rr = new RateResets(rateResets);
      return rr;
    }


    /// <exclude></exclude>
    internal static RateModelParameters GetBGMRateModelParameters(Dt asOf, Tenor tenor)
    {
      var sigma = new Curve(asOf);
      sigma.Add(Dt.Add(asOf,365), 0.25);
      sigma.Add(Dt.Add(asOf,2*365),0.20);
      return new RateModelParameters(RateModelParameters.Model.BGM, new[] { RateModelParameters.Param.Sigma },
                                     new[] { sigma },
                                     tenor, Currency.USD);
    }

    /// <exclude></exclude>
    internal static RateModelParameters GetSABRRateModelParameters(Dt asOf, Tenor tenor)
    {
      var sigma = new Curve(asOf);
      sigma.Add(Dt.Add(asOf, 365), 0.25);
      sigma.Add(Dt.Add(asOf, 2 * 365), 0.20);
      var beta = new Curve(asOf, 0.5);
      var alpha = new Curve(asOf, 0.2);
      var rho = new Curve(asOf, 0.8);
      var rateModelParameters = new RateModelParameters(RateModelParameters.Model.SABR,
                                                        new[]
                                                          {
                                                            RateModelParameters.Param.Nu,
                                                            RateModelParameters.Param.Alpha
                                                            , RateModelParameters.Param.Beta,
                                                            RateModelParameters.Param.Rho
                                                          },
                                                        new[] { sigma, alpha, beta, rho }, new Tenor(6, TimeUnit.Months),
                                                        Currency.USD);
      return rateModelParameters;
    }

    /// <exclude></exclude>
    internal static RateModelParameters GetSABRBGMRateModelParameters(Dt asOf, Tenor tenor)
    {
      var sigma = new Curve(asOf);
      sigma.Add(Dt.Add(asOf, 365), 0.25);
      sigma.Add(Dt.Add(asOf, 2*365), 0.20);
      var beta = new Curve(asOf, 0.5);
      var alpha = new Curve(asOf, 0.2);
      var rho = new Curve(asOf, 0.8);
      var ppar = new IModelParameter[] {alpha, beta, sigma, rho};
      var fpar = new IModelParameter[] {sigma};
      var ppN = new[]
                  {
                    RateModelParameters.Param.Alpha,
                    RateModelParameters.Param.Beta,
                    RateModelParameters.Param.Nu,
                    RateModelParameters.Param.Rho
                  };
      RateModelParameters.Param[] fpN = new RateModelParameters.Param[] {RateModelParameters.Param.Sigma};
      var fundingPar = new RateModelParameters(RateModelParameters.Model.BGM, fpN, fpar, new Tenor(Frequency.Quarterly),
                                               Currency.USD);

      var fwdparameters =
        new RateModelParameters(fundingPar, RateModelParameters.Model.SABR, ppN, ppar, new Curve(asOf, 0.75),
                                new Tenor(Frequency.SemiAnnual));
      return fwdparameters;
    }

    /// <exclude></exclude>
    internal static InflationBondPricer GetInflationBondPricer(Dt asOf, DiscountCurve dc, CalibratedCurve referenceCurve, ReferenceIndex referenceIndex, double spread, double sign)
    {
      Dt effective = Dt.Add(asOf, 2);
      InflationBond bond = new InflationBond(effective, Dt.Add(effective, "5Y"), Currency.None, BondType.None, spread,
                                             DayCount.Actual360, CycleRule.None, Frequency.Quarterly,
                                             BDConvention.Following, Calendar.NYB,
                                             (InflationIndex) referenceIndex, InitialInflation(), Tenor.Parse("3M"));
      var pars = GetSABRBGMRateModelParameters(asOf, Tenor.Parse("3M"));
      var pricer = new InflationBondPricer(bond, asOf, asOf, 1e8*sign, dc, null, (InflationCurve) referenceCurve, null,
                                           pars);
      return pricer;
      
    }

    internal static InflationBondPricer GetOffTheRunInflationBondPricer(Dt asOf, DiscountCurve dc, CalibratedCurve referenceCurve, ReferenceIndex referenceIndex, double spread, double sign)
    {
      Dt effective = Dt.Add(asOf, -1, TimeUnit.Months);
      var bond = new InflationBond(effective, Dt.Add(effective, "5Y"), Currency.None, BondType.None, spread,
                                             DayCount.Actual360, CycleRule.None, Frequency.Quarterly,
                                             BDConvention.Following, Calendar.NYB, (InflationIndex)referenceIndex, InitialInflation(), Tenor.Parse("3M"));
      var pars = GetSABRBGMRateModelParameters(asOf, Tenor.Parse("3M"));
      var pricer = new InflationBondPricer(bond, asOf, asOf, 1e8 * sign, dc, null, (InflationCurve)referenceCurve, null, pars);
      return pricer;

    }


    /// <exclude></exclude>
    internal static SwapLegPricer GetFixedSwapPricer(Dt asOf, DiscountCurve dc, double coupon, double sign)
    {
      Dt effective = Dt.Add(asOf, 2);
      var sl2 = new SwapLeg(effective, Dt.Add(effective, "5Y"), Currency.None, coupon, DayCount.Thirty360,
                            Frequency.SemiAnnual, BDConvention.Modified, Calendar.NYB, false);
      return new SwapLegPricer(sl2, asOf, asOf, sign*1e8, dc, null, null, null, null, null);
    }

    /// <exclude></exclude>
    internal static SwapLegPricer GetFloatingSwapPricer(Dt asOf, DiscountCurve dc, ReferenceIndex referenceIndex, double spread, double sign)
    {
      Dt effective = Dt.Add(asOf, 2);
      Frequency freq = Frequency.Quarterly;
      var sl2 = new SwapLeg(effective, Dt.Add(effective, "5Y"), Frequency.Quarterly, spread, referenceIndex,
                            Currency.None, DayCount.Actual360, BDConvention.Following, Calendar.NYB);
      RateModelParameters rateModelParameters = GetBGMRateModelParameters(asOf, new Tenor(freq));
      return new SwapLegPricer(sl2, asOf, asOf, sign*1e8, dc, referenceIndex, dc, new RateResets(0.0, 0.0),
                               rateModelParameters, null);
    }

    /// <exclude></exclude>
    internal static SwapLegPricer GetFloatingSwapPricer(Dt asOf, DiscountCurve dc, CalibratedCurve referenceCurve, ReferenceIndex referenceIndex, double spread, double sign)
    {
      Dt effective = Dt.Add(asOf, 2);
      Frequency freq = Frequency.Quarterly;
      var sl2 = new SwapLeg(effective, Dt.Add(effective, "5Y"), Frequency.Quarterly, spread, referenceIndex,
                            Currency.None, DayCount.Actual360, BDConvention.Following, Calendar.NYB);
      RateModelParameters rateModelParameters = GetBGMRateModelParameters(asOf, new Tenor(freq));
      return new SwapLegPricer(sl2, asOf, asOf, sign * 1e8, dc, referenceIndex, referenceCurve, new RateResets(0.0, 0.0), rateModelParameters, null);
    }

    /// <exclude></exclude>
    internal static SwapLegPricer GetFixedSwapPricerNotionalSchedule(Dt asOf, DiscountCurve dc, double coupon, double sign)
    {
      Dt effective = Dt.Add(asOf, 2);
      var sl2 = new SwapLeg(effective, Dt.Add(effective, "5Y"), Currency.None, coupon, DayCount.Thirty360,
                            Frequency.SemiAnnual, BDConvention.Modified, Calendar.NYB, false)
                  {
                    InitialExchange = true,
                    IntermediateExchange = true,
                    FinalExchange = true
                  };
      var pricer = new SwapLegPricer(sl2, asOf, asOf, sign * 1e8, dc, null, null, null, null, null);
      var ps = pricer.GetPaymentSchedule(null, effective);
      var ntl = sl2.Notional;
      sl2.AmortizationSchedule.Add(new Amortization(effective, AmortizationType.RemainingNotionalLevels, ntl));
      int i = 2;
      foreach (var d in ps.GetPaymentDates().Where(d => d != effective))
      {
        sl2.AmortizationSchedule.Add(new Amortization(d, AmortizationType.RemainingNotionalLevels,
                                                      Math.Pow(-1.0, i-1)*ntl*i++));
      }
      sl2.AmortizationSchedule.Remove(sl2.AmortizationSchedule.Last());
      return pricer;
    }
  }

    
    
  /// <summary>
  /// Test the swap leg pricer
  /// </summary>
  //[TestFixture]
  [TestFixture("InflationBondExDivTest1")]
  [TestFixture("InflationBondExDivTest10")]
  [TestFixture("InflationBondExDivTest11")]
  [TestFixture("InflationBondExDivTest12")]
  [TestFixture("InflationBondExDivTest2")]
  [TestFixture("InflationBondExDivTest3")]
  [TestFixture("InflationBondExDivTest4")]
  [TestFixture("InflationBondExDivTest5")]
  [TestFixture("InflationBondExDivTest6")]
  [TestFixture("InflationBondExDivTest7")]
  [TestFixture("InflationBondExDivTest8")]
  [TestFixture("InflationBondExDivTest9")]
  [TestFixture("TestSwapLegPricerParameterized")]
  public class TestSwapLegPricerParameterized : ToolkitTestBase
  {
    public TestSwapLegPricerParameterized(string name) : base(name) {}

    /// <summary>
    /// Setup
    /// </summary>
    [SetUp]
    public void SetUpDiscount()
    {
      AsOf = new Dt(2, 8, 2011);
      DiscountData = SwapLegTestUtils.GetDiscountData(AsOf);
    }

    private ResultData GetResultData(DataTable dt)
    {
      int numResultsPopulated;
      ResultData rd = GetResultData(dt, 0, out numResultsPopulated);
      return rd;
    }

    private ResultData GetResultData(DataTable dt, int numAdditionalsResults, out int numResultsPopulated)
    {
      numResultsPopulated = 0;
      ResultData rd = LoadExpects();
      //1st col is the label, rest are actual data points
      int cols = 0;
      DataColumn labelCol = dt.Columns["Payment Date"];
      foreach (DataColumn column in dt.Columns)
      {
        // if (labelCol == null)
        // {
        //   labelCol = column;
        //   continue;
        // }
        if (column.DataType == typeof(double))
          cols++;
      }

      if (rd.Results == null || rd.Results.Length < cols + numAdditionalsResults)
      {
        rd.Results = new ResultData.ResultSet[cols + numAdditionalsResults];
      }
      numResultsPopulated = cols;

      for (int j = 0; j < cols; ++j)
      {
        if (rd.Results[j] == null)
          rd.Results[j] = new ResultData.ResultSet();
      }

      if (cols + numAdditionalsResults < rd.Results.Length)
      {
        ResultData.ResultSet[] temp = rd.Results;
        rd.Results = new ResultData.ResultSet[cols];

        for (int j = 0; j < cols; ++j)
        {
          rd.Results[j] = temp[j];
        }
      }


      if (cols > 1)
      {
        int i = 0;

        foreach (DataColumn column in dt.Columns)
        {
          if (column == labelCol)
            continue;

          if (column.DataType != typeof(double))
            continue;

          rd.Results[i].Name = column.ColumnName;
          rd.Results[i].Labels = new string[dt.Rows.Count];
          rd.Results[i].Actuals = new double[dt.Rows.Count];
          int j = 0;
          foreach (DataRow row in dt.Rows)
          {
            rd.Results[i].Labels[j] = (string)row[labelCol.ColumnName];
            rd.Results[i].Actuals[j] = row.IsNull(column.ColumnName)? 0.0 : (double)row[column.ColumnName];
            j++;
          }
          i++;
        }
      }
      else
      {
        throw new ArgumentException(
          "Not enough data in the table given: must have one label column and one column containing doubles.");
      }
      return rd;
    }


    public ResultData GenerateResults(SwapLegPricer pay, SwapLegPricer receive)
    {
      var labels = new string[8];
      var values = new double[8];

      SwapPricer sp = null;

      if (pay != null && receive != null)
      {
        sp = new SwapPricer(receive, pay);
      }

      var t = new Timer();
      t.Start();

      if (pay != null)
      {
        labels[0] = "MTM1";
        values[0] = pay.Pv();
        labels[1] = "Accrued1";
        values[1] = pay.Accrued();
        labels[2] = "IR011";
        var p = ((SwapLeg) pay.Product);
        p.InitialExchange = true;
        p.IntermediateExchange = true;
        p.FinalExchange = true;
        values[2] = Sensitivities.IR01(pay, "Pv", 1.0, 0, true);
      }

      if (receive != null)
      {
        labels[3] = "MTM2";
        values[3] = receive.Pv();
        labels[4] = "Accrued2";
        values[4] = receive.Accrued();
        var p = ((SwapLeg) receive.Product);
        p.InitialExchange = true;
        p.IntermediateExchange = true;
        p.FinalExchange = true;
        labels[5] = "IR012";
        values[5] = Sensitivities.IR01(receive, "Pv", 1.0, 0, true);
      }

      if (sp != null)
      {
        labels[6] = "ParCoupon";
        values[6] = sp.ParCoupon();
        labels[7] = "PV";
        values[7] = sp.Pv();
      }

      t.Stop();

      return ToResultData(values, labels, t.Elapsed);
    }


    #region Reset Tests
    private ResultData FromPaymentSchedule(PaymentSchedule paymentSchedule)
    {
      List<string> payDts = new List<string>();
      List<double> payResets = new List<double>();
      List<double> payResetStates = new List<double>();
      List<string> resetDates = new List<string>();
      List<double> resetStates = new List<double>();
      List<double> resets = new List<double>();
      int idx = 0;
      foreach (Payment pay in paymentSchedule)
      {
        var p = pay as FloatingInterestPayment;
        if (p != null)
        {
          payDts.Add(p.PayDt.ToString());
          payResets.Add(p.EffectiveRate);
          payResetStates.Add((double)p.RateResetState);

          var info = p.GetRateResetComponents();
          if (info != null )
            foreach (var i in info)
            {
              resetDates.Add(String.Format("{0}-{1}", idx, i.Date));
              resets.Add(i.Rate);
              resetStates.Add((double) i.State);
            }
        }
        ++idx;
      }
      ResultData rd = LoadExpects();
      rd.Accuracy = 1e-10;

      if (rd.Results.Length != 4 || rd.Results[0].Expects == null)
      {
        rd.Results = new ResultData.ResultSet[4];
        rd.Results[0] = new ResultData.ResultSet();
        rd.Results[1] = new ResultData.ResultSet();
        rd.Results[2] = new ResultData.ResultSet();
        rd.Results[3] = new ResultData.ResultSet();
      }
      {
        rd.Results[0].Name = "ResetValue";
        rd.Results[0].Labels = payDts.ToArray();
        rd.Results[0].Actuals = payResets.ToArray();
      }
      {
        rd.Results[1].Name = "ResetState";
        rd.Results[1].Labels = payDts.ToArray();
        rd.Results[1].Actuals = payResetStates.ToArray();
      }
      
      
      {
        rd.Results[2].Name = "DetailedResetValue";
        rd.Results[2].Labels = resetDates.ToArray();
        rd.Results[2].Actuals = resets.ToArray();
      }
      {
        rd.Results[3].Name = "DetailedResetState";
        rd.Results[3].Labels = resetDates.ToArray();
        rd.Results[3].Actuals = resetStates.ToArray();
      }
      return rd;
    }
    
    public ResultData ResetsTestRegular(SwapLegPricer pricer)
    {
      PaymentSchedule paymentSchedule = pricer.GetPaymentSchedule(null, pricer.SwapLeg.Effective);
      return FromPaymentSchedule(paymentSchedule);
    }


    public ResultData ResetsTestWithHistoricalObservations(SwapLegPricer pricer, RateResets resets)
    {
      pricer.ReferenceIndex.HistoricalObservations = resets;
      pricer.SwapLeg.Effective = Dt.Add(pricer.SwapLeg.Effective, -365);
      PaymentSchedule paymentSchedule = pricer.GetPaymentSchedule(null, pricer.SwapLeg.Effective);
      foreach (FloatingInterestPayment ip in paymentSchedule)
      {
        bool found;
        if (ip.RateResetState == RateResetState.ResetFound)
          Assert.AreEqual(resets.GetRate(ip.ResetDate, out found), ip.IndexFixing, 1e-10);
      }
      return FromPaymentSchedule(paymentSchedule);
    }


    public ResultData ResetsTestWithOverriddenObservations(SwapLegPricer pricer, RateResets resets, RateResets pricerResets)
    {
      pricer.RateResets.AllResets = pricerResets.AllResets;
      pricer.ReferenceIndex.HistoricalObservations = resets;
      pricer.SwapLeg.Effective = Dt.Add(pricer.SwapLeg.Effective,  -365);
      PaymentSchedule paymentSchedule = pricer.GetPaymentSchedule(null, pricer.SwapLeg.Effective);
      foreach (FloatingInterestPayment ip in paymentSchedule)
      {
        bool found;
        if (ip.RateResetState == RateResetState.ResetFound)
          Assert.AreEqual(resets.GetRate(ip.ResetDate, out found), ip.EffectiveRate, 1e-10);
      }
      return FromPaymentSchedule(paymentSchedule);
    }

    public ResultData ResetsTestWithCurrentObservation(SwapLegPricer pricer, double fixing)
    {
      pricer.RateResets.CurrentReset = fixing;
      PaymentSchedule paymentSchedule = pricer.GetPaymentSchedule(null, pricer.AsOf);
      foreach (FloatingInterestPayment ip in paymentSchedule)
      {
        bool found;
        if (ip.RateResetState == RateResetState.ResetFound)
          Assert.AreEqual(fixing, ip.EffectiveRate, 1e-10);
      }
      return FromPaymentSchedule(paymentSchedule);
    }



    public void ResetsTestHandleMissing(SwapLegPricer pricer)
    {
      pricer.AsOf = Dt.Add(pricer.SwapLeg.Effective,7);
      pricer.Settle = Dt.Add(pricer.AsOf, 2);
      pricer.ReferenceIndex.HistoricalObservations = null;
      bool hasErr = false;
      string message = string.Empty;
      try
      {
        pricer.ProductPv();
      }
      catch (Exception ex)
      {
        hasErr = true;
        message = ex.Message;
      }
      Assert.AreEqual(true, hasErr, message);
    }

    
    [Test]
    public void ResetTestInArrearsSwapRegular()
    {
      ReferenceIndex index = SwapLegTestUtils.GetLiborIndex("3M");
      SwapLegPricer p = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, DiscountData.GetDiscountCurve(), index, 10*1e-4, 1.0);
      p.SwapLeg.InArrears = true;
      ResultData rd = ResetsTestRegular(p);
      MatchExpects(rd);
    }

    [Test]
    public void ResetTestInArrearsWithCurrentObservation()
    {
      ReferenceIndex index = SwapLegTestUtils.GetLiborIndex("3M");
      SwapLegPricer p = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, DiscountData.GetDiscountCurve(), index, 10*1e-4, 1.0);
      p.SwapLeg.InArrears = true;
      ResultData rd = ResetsTestWithCurrentObservation(p, 0.05);
      MatchExpects(rd);
    }

    [Test]
    public void ResetTestInArrearsSwapWithHistoricalObservations()
    {
      ReferenceIndex index = SwapLegTestUtils.GetLiborIndex("3M");
      SwapLegPricer p = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, DiscountData.GetDiscountCurve(), index, 10 * 1e-4, 1.0);
      p.SwapLeg.InArrears = true;
      RateResets rr = SwapLegTestUtils.GetHistoricalResets(AsOf, Toolkit.Base.Frequency.Daily, CycleRule.None,
                                                           p.ReferenceIndex.Calendar, "3M");
      ResultData rd = ResetsTestWithHistoricalObservations(p, rr);
      MatchExpects(rd);
    }

    [Test]
    public void ResetTestInArrearsSwapWithOverriddenObservations()
    {
      ReferenceIndex index = SwapLegTestUtils.GetLiborIndex("3M");
      SwapLegPricer p = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, DiscountData.GetDiscountCurve(), index, 10 * 1e-4, 1.0);
      p.SwapLeg.InArrears = true;
      RateResets rr = SwapLegTestUtils.GetHistoricalResets(AsOf, Toolkit.Base.Frequency.Daily, CycleRule.None,
                                                           p.ReferenceIndex.Calendar, "3M");
      ResultData rd = ResetsTestWithOverriddenObservations(p, rr, rr);
      MatchExpects(rd);
    }


    [Test]
    public void ResetTestVanillaSwapRegular()
    {
      ReferenceIndex index = SwapLegTestUtils.GetLiborIndex("3M");
      SwapLegPricer p = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, DiscountData.GetDiscountCurve(), index, 10 * 1e-4, 1.0);
      ResultData rd = ResetsTestRegular(p);
      MatchExpects(rd);
    }

    [Test]
    public void ResetTestVanillaSwapWithCurrentObservation()
    {
      ReferenceIndex index = SwapLegTestUtils.GetLiborIndex("3M");
      SwapLegPricer p = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, DiscountData.GetDiscountCurve(), index, 10 * 1e-4, 1.0);
      ResultData rd = ResetsTestWithCurrentObservation(p, 0.05);
      MatchExpects(rd);
    }

    [Test]
    public void ResetTestVanillaSwapWithHistoricalObservations()
    {
      ReferenceIndex index = SwapLegTestUtils.GetLiborIndex("3M");
      SwapLegPricer p = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, DiscountData.GetDiscountCurve(), index, 10 * 1e-4, 1.0);
      RateResets rr = SwapLegTestUtils.GetHistoricalResets(AsOf, Toolkit.Base.Frequency.Daily, CycleRule.None,
                                                           p.ReferenceIndex.Calendar, "3M");
      ResultData rd = ResetsTestWithHistoricalObservations(p, rr);
      MatchExpects(rd);
    }

    [Test]
    public void ResetTestVanillaSwapWithOverriddenObservations()
    {
      ReferenceIndex index = SwapLegTestUtils.GetLiborIndex("3M");
      SwapLegPricer p = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, DiscountData.GetDiscountCurve(), index, 10 * 1e-4, 1.0);
      RateResets rr = SwapLegTestUtils.GetHistoricalResets(AsOf, Toolkit.Base.Frequency.Daily, CycleRule.None,
                                                           p.ReferenceIndex.Calendar, "3M");
      ResultData rd = ResetsTestWithOverriddenObservations(p, rr, rr);
      MatchExpects(rd);
    }


    [Test]
    public void ResetTestVanillaSwapMissingHandling()
    {
      ReferenceIndex index = SwapLegTestUtils.GetLiborIndex("3M");
      SwapLegPricer p = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, DiscountData.GetDiscountCurve(), index, 10 * 1e-4, 1.0);
      p.AsOf = Dt.Add(p.AsOf, 2);
      p.Settle = Dt.Add(p.AsOf, 2);
      ResetsTestHandleMissing(p);
    }


    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void ResetTestDailyArithmeticAverageDailyRatesRegular()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("1D");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.ArithmeticAverageRate;
      ResultData rd = ResetsTestRegular(sp1);
      MatchExpects(rd);
    }


    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void ResetTestDailyArithmeticAverageDailyWithCurrentObservation()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("1D");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.ArithmeticAverageRate;
      ResultData rd = ResetsTestWithCurrentObservation(sp1, 0.05);
      MatchExpects(rd);
    }


    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void ResetTestDailyArithmeticAverageDailyRatesWithHistoricalObservations()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("1D");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.ArithmeticAverageRate;
      RateResets rr = SwapLegTestUtils.GetHistoricalResets(AsOf, Frequency.Daily, CycleRule.None, Calendar.NYB, "1D");
      ResultData rd = ResetsTestWithHistoricalObservations(sp1, rr);
      MatchExpects(rd);
    }


    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void ResetTestDailyArithmeticAverageDailyRatesWithOverriddenObservations()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("1D");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.ArithmeticAverageRate;
      RateResets rr = SwapLegTestUtils.GetHistoricalResets(AsOf, Frequency.Daily, CycleRule.None, Calendar.NYB, "1D");
      ResultData rd = ResetsTestWithOverriddenObservations(sp1, rr, rr);
      MatchExpects(rd);
    }
   
    #endregion 
    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void LiborInArrears()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      var sp1 = SwapLegTestUtils.GetFixedSwapPricer(AsOf, dc, 0.05, -1);
      var sp2 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1);
      sp2.SwapLeg.InArrears = true;
      ResultData rd = GenerateResults(sp1, sp2);
      MatchExpects(rd);
    }



    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void BgmLibor3mFvs6mT()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("6M");
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      var sp1 = SwapLegTestUtils.GetFixedSwapPricer(AsOf, dc, 0.05, -1);
      var sp2 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1);
      sp2.FwdRateModelParameters = SwapLegTestUtils.GetBGMRateModelParameters(AsOf, new Tenor("3M"));
      ResultData rd = GenerateResults(sp1, sp2);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void SabrLibor3mFvs6mT()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("6M");
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      var sp1 = SwapLegTestUtils.GetFixedSwapPricer(AsOf, dc, 0.05, -1);
      var sp2 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1);
      sp2.FwdRateModelParameters = SwapLegTestUtils.GetSABRRateModelParameters(AsOf, ri.IndexTenor);
      ResultData rd = GenerateResults(sp1, sp2);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void LiborWithResets()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      var sp1 = SwapLegTestUtils.GetFixedSwapPricer(AsOf, dc, 0.05, -1);
      var sp2 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1);
      sp2.RateResets.CurrentReset = 0.03;
      sp2.RateResets.NextReset = 0.05;
      var ps = sp2.GetPaymentSchedule(null, AsOf);
      Dt[] dts = ps.GetPaymentDates().ToArray();
      int i = 0;
      foreach (var payment in ps.GetPaymentsOnDate(dts[0]))
      {
        var fip = payment as FloatingInterestPayment;
        Assert.AreEqual(sp2.RateResets.CurrentReset, fip.EffectiveRate);
      }
      foreach (var payment in ps.GetPaymentsOnDate(dts[1]))
      {
        var fip = payment as FloatingInterestPayment;
        Assert.AreEqual(sp2.RateResets.NextReset, fip.EffectiveRate);
      }
      ResultData rd = GenerateResults(sp1, sp2);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void Libor6mFv3mTCompounded()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      var sp1 = SwapLegTestUtils.GetFixedSwapPricer(AsOf, dc, 0.05, -1);
      var sp2 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 10*1e-4, 1);
      sp2.SwapLeg.Freq = Frequency.SemiAnnual;
      sp2.SwapLeg.CompoundingFrequency = Frequency.Quarterly;
      sp2.SwapLeg.CompoundingConvention = CompoundingConvention.ISDA;
      ResultData rd = GenerateResults(sp2, sp1);
      MatchExpects(rd);
    }
   
    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void FixedLeg()
    {
      SwapLegPricer sp1 = SwapLegTestUtils.GetFixedSwapPricer(AsOf, DiscountData.GetDiscountCurve(), 0.05, 1);
      ResultData rd = GenerateResults(sp1, null);
      MatchExpects(rd);
    }

    [Test]
    public void FixedCFCheck()
    {
      SwapLegPricer sp1 = SwapLegTestUtils.GetFixedSwapPricer(AsOf, DiscountData.GetDiscountCurve(), 0.05, 1);
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    [Test]
    public void FixedAmortizingCFCheck()
    {
      SwapLegPricer sp1 = SwapLegTestUtils.GetFixedSwapPricerNotionalSchedule(AsOf, DiscountData.GetDiscountCurve(), 0.05, 1);
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    [Test]
    public void LiborCheck()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      SwapLegPricer sp2 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 10*1e-4, 1.0);
      ResultData rd = GenerateResults(sp2, null);
      MatchExpects(rd);
    }

    [Test]
    public void LiborCFCheck()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      SwapLegPricer sp2 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 10 * 1e-4, 1.0);
      DataTable dt = sp2.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    [Test]
    public void AccruedCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      SwapLegPricer sp2 = SwapLegTestUtils.GetFixedSwapPricer(AsOf, dc, 0.05, 1.0);
      Dt settle = sp2.Settle = Dt.Add(AsOf, 85, TimeUnit.Days);
      double accrued = sp2.Accrued();
      Dt start = sp2.SwapLeg.Schedule.GetPeriodStart(0);
      Dt end = sp2.SwapLeg.Schedule.GetPeriodEnd(0);
      double expect = sp2.Notional*Dt.Fraction(start, settle, sp2.SwapLeg.DayCount)*sp2.SwapLeg.Coupon;
      Assert.AreEqual(expect, accrued, 5e-10);
    }


    /// <summary>
    /// Capture results: expects compounding and non compounding to be equivalent with zero spread
    /// </summary>
    [Test]
    public void LiborCompoundedWtZeroCouponCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri6 = SwapLegTestUtils.GetLiborIndex("6M");
      var sp66 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri6, 0, 1.0);
      sp66.SwapLeg.Freq = Frequency.SemiAnnual;
      PaymentSchedule psNonCompounded = sp66.GetPaymentSchedule(null, sp66.AsOf);
      ReferenceIndex ri3 = SwapLegTestUtils.GetLiborIndex("3M");
      var sp36 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri3, 0, 1.0);
      sp36.SwapLeg.Freq = Frequency.SemiAnnual;
      sp36.SwapLeg.CompoundingFrequency = Frequency.Quarterly;
      sp36.SwapLeg.CompoundingConvention = CompoundingConvention.ISDA;
      PaymentSchedule psCompoundedIsda = sp36.GetPaymentSchedule(null, sp36.AsOf);
      sp36.SwapLeg.CompoundingConvention = CompoundingConvention.FlatISDA;
      PaymentSchedule psCompoundedFlatIsda = sp36.GetPaymentSchedule(null, sp36.AsOf);
      double diff = 0.0;
      foreach (Dt d in psNonCompounded.GetPaymentDates())
      {
        double fNotCmpn =
          ((List<FloatingInterestPayment>) psNonCompounded.GetPaymentsByType<FloatingInterestPayment>(d))[0].
            EffectiveRate;
        double fCmpnIsdaFlat =
          ((List<FloatingInterestPayment>) psCompoundedFlatIsda.GetPaymentsByType<FloatingInterestPayment>(d))[0].
            EffectiveRate;
        double fCmpnIsda =
          ((List<FloatingInterestPayment>) psCompoundedIsda.GetPaymentsByType<FloatingInterestPayment>(d))[0].
            EffectiveRate;
        diff += (Math.Abs(fNotCmpn - fCmpnIsdaFlat) + Math.Abs(fNotCmpn - fCmpnIsda));
      }
      Assert.AreEqual(0.0, diff, 6.5e-5);
    }


    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void LiborCompoundedISDAFlatCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var sp36 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0, 1.0);
      sp36.SwapLeg.Freq = Frequency.SemiAnnual;
      sp36.SwapLeg.CompoundingFrequency = Frequency.Quarterly;
      sp36.SwapLeg.CompoundingConvention = CompoundingConvention.FlatISDA; 
      DataTable dt = sp36.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void LiborCompoundedISDACFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var sp36 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0, 1.0);
      sp36.SwapLeg.Freq = Frequency.SemiAnnual;
      sp36.SwapLeg.CompoundingFrequency = Frequency.Quarterly;
      sp36.SwapLeg.CompoundingConvention = CompoundingConvention.ISDA;
      DataTable dt = sp36.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void LiborCappedCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("6M");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 10*1e-4, 1.0);
      sp1.SwapLeg.Cap = 1e-2;
      var ps = sp1.GetPaymentSchedule(null, AsOf);
      foreach (FloatingInterestPayment ip in ps.GetPaymentsByType<FloatingInterestPayment>())
      {
        Assert.Greater(sp1.SwapLeg.Cap.Value + 1e-8, ip.EffectiveRate);
      }
      DataTable dt = ps.ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    [Test]
    public void LiborFlooredCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("6M");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 10*1e-4, 1.0);
      sp1.SwapLeg.Floor = 3e-2;
      var ps = sp1.GetPaymentSchedule(null, AsOf);
      foreach (FloatingInterestPayment ip in ps.GetPaymentsByType<FloatingInterestPayment>())
      {
        Assert.Greater(ip.EffectiveRate, 3e-2 - 1e-8);
      }
      DataTable dt = ps.ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    [Test]
    public void LiborInArrearsCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("6M");
      SwapLegPricer sp2 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0, 1.0);
      sp2.SwapLeg.InArrears = true;
      ResultData rd = GenerateResults(sp2, null);
      MatchExpects(rd);
    }

    [Test]
    public void LiborInArrearsCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("6M");
      SwapLegPricer sp2 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0, 1.0);
      sp2.SwapLeg.InArrears = true;
      DataTable dt = sp2.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }


    [Test]
    public void LiborInArrearsISDAFlatCompoundedCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      SwapLegPricer sp2 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0, 1.0);
      sp2.SwapLeg.InArrears = true;
      sp2.SwapLeg.Freq = Frequency.SemiAnnual;
      sp2.SwapLeg.CompoundingFrequency = Frequency.Quarterly;
      sp2.SwapLeg.CompoundingConvention = CompoundingConvention.FlatISDA;
      DataTable dt = sp2.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    [Test]
    public void LiborInArrearsISDACompoundedCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      SwapLegPricer sp2 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0, 1.0);
      sp2.SwapLeg.InArrears = true;
      sp2.SwapLeg.Freq = Frequency.SemiAnnual;
      sp2.SwapLeg.CompoundingFrequency = Frequency.Quarterly;
      sp2.SwapLeg.CompoundingConvention = CompoundingConvention.ISDA;
      DataTable dt = sp2.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    [Test]
    public void LiborInArrearsCappedCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("6M");
      SwapLegPricer sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0, 1.0);
      sp1.SwapLeg.InArrears = true;
      sp1.SwapLeg.Cap = 1e-2;
      var ps = sp1.GetPaymentSchedule(null, AsOf);
      foreach (FloatingInterestPayment ip in ps.GetPaymentsByType<FloatingInterestPayment>())
      {
        Assert.Greater(sp1.SwapLeg.Cap.Value + 1e-8, ip.EffectiveRate);
      }
      DataTable dt = ps.ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    [Test]
    public void LiborInArrearsFlooredCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("6M");
      SwapLegPricer sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0, 1.0);
      sp1.SwapLeg.InArrears = true;
      sp1.SwapLeg.Floor = 3e-2;
      var ps = sp1.GetPaymentSchedule(null, AsOf);
      foreach (FloatingInterestPayment ip in ps.GetPaymentsByType<FloatingInterestPayment>())
      {
        Assert.Greater(ip.EffectiveRate, sp1.SwapLeg.Floor.Value - 1e-8);
      }
      DataTable dt = ps.ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }
    
    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void LiborWithDelayCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      SwapLegPricer sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0, 1.0);
      sp1.SwapLeg.Freq = Frequency.SemiAnnual;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    [Test]
    public void CMSCheck()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetCMSIndex("2Y");
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      var sp2 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 10*1e-4, 1.0);
      sp2.SwapLeg.ProjectionType = ProjectionType.SwapRate;
      ResultData rd = GenerateResults(sp2, null);
      MatchExpects(rd);
    }

    [Test]
    public void CMS_CFCheck()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetCMSIndex("2Y");
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      var sp2 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 10 * 1e-4, 1.0);
      sp2.SwapLeg.ProjectionType = ProjectionType.SwapRate;
      DataTable dt = sp2.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }


    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void CMSCompoundedFlatISDACFCheck()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetCMSIndex("5Y");
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 10*1e-4, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.SwapRate;
      sp1.SwapLeg.Freq = Frequency.SemiAnnual;
      sp1.SwapLeg.CompoundingFrequency = Frequency.Quarterly;
      sp1.SwapLeg.CompoundingConvention = CompoundingConvention.FlatISDA;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void CMSCompoundedISDACFCheck()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetCMSIndex("5Y");
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 10 * 1e-4, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.SwapRate;
      sp1.SwapLeg.Freq = Frequency.SemiAnnual;
      sp1.SwapLeg.CompoundingFrequency = Frequency.Quarterly;
      sp1.SwapLeg.CompoundingConvention = CompoundingConvention.ISDA;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void CMSCompoundedFlatISDADisNotProjCFCheck()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetCMSIndex("5Y");
      DiscountCurve dc = SwapLegTestUtils.GetTSYCurve(AsOf);
      DiscountCurve proj = DiscountData.GetDiscountCurve();

      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, proj, ri, 10*1e-4, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.SwapRate;
      sp1.SwapLeg.Freq = Frequency.SemiAnnual;
      sp1.SwapLeg.CompoundingFrequency = Frequency.Quarterly;
      sp1.SwapLeg.CompoundingConvention = CompoundingConvention.FlatISDA;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void CMSCompoundedISDADisNotProjCFCheck()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetCMSIndex("5Y");
      DiscountCurve dc = SwapLegTestUtils.GetTSYCurve(AsOf);
      DiscountCurve proj = DiscountData.GetDiscountCurve();

      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, proj, ri, 10 * 1e-4, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.SwapRate;
      sp1.SwapLeg.Freq = Frequency.SemiAnnual;
      sp1.SwapLeg.CompoundingFrequency = Frequency.Quarterly;
      sp1.SwapLeg.CompoundingConvention = CompoundingConvention.ISDA;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void CMSFlooredCFCheck()
    {
      ReferenceIndex ri = SwapLegTestUtils.GetCMSIndex("2Y");
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 10*1e-4, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.SwapRate;
      sp1.SwapLeg.Floor = 3e-2;
      var ps = sp1.GetPaymentSchedule(null, AsOf);
      foreach (FloatingInterestPayment ip in ps.GetPaymentsByType<FloatingInterestPayment>())
      {
        if (ip.ResetDate > AsOf)
          Assert.Greater(ip.EffectiveRate, 3e-2 - 1e-8);
      }
      DataTable dt = ps.ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    /// <returns></returns>
    [Test]
    public void CMTCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      DiscountCurve tsy = SwapLegTestUtils.GetTSYCurve(AsOf);
      SwapLegPricer sp2 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, tsy, dc, ri, 10*1e-4, 1);
      ResultData rd = GenerateResults(sp2, null);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    /// <returns></returns>
    [Test]
    public void CMT_CFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      DiscountCurve tsy = SwapLegTestUtils.GetTSYCurve(AsOf);
      SwapLegPricer sp2 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, tsy, dc, ri, 10 * 1e-4, 1);
      DataTable dt = sp2.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void YoYCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);
      
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, cpiCurve, cpi, 0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.InflationRate;
      sp1.FwdRateModelParameters = SwapLegTestUtils.GetSABRBGMRateModelParameters(AsOf, new Tenor("3M"));
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void YoYWithSpreadCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);
     
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, cpiCurve, cpi, 10*1e-4, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.InflationRate;
      sp1.FwdRateModelParameters = SwapLegTestUtils.GetSABRBGMRateModelParameters(AsOf, new Tenor("3M"));
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    
    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void YoYCompoundedFlatISDACFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, cpiCurve, cpi, 10*1e-4, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.InflationRate;
      sp1.FwdRateModelParameters = SwapLegTestUtils.GetSABRBGMRateModelParameters(AsOf, new Tenor("3M"));
      sp1.SwapLeg.Freq = Frequency.SemiAnnual;
      sp1.SwapLeg.CompoundingConvention = CompoundingConvention.FlatISDA;
      sp1.SwapLeg.CompoundingFrequency = Frequency.Quarterly;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void YoYCompoundedISDACFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, cpiCurve, cpi, 10 * 1e-4, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.InflationRate;
      sp1.FwdRateModelParameters = SwapLegTestUtils.GetSABRBGMRateModelParameters(AsOf, new Tenor("3M"));
      sp1.SwapLeg.Freq = Frequency.SemiAnnual;
      sp1.SwapLeg.CompoundingConvention = CompoundingConvention.ISDA;
      sp1.SwapLeg.CompoundingFrequency = Frequency.Quarterly;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void YoYFlooredCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);
     
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, cpiCurve, cpi, 0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.InflationRate;
      sp1.FwdRateModelParameters = SwapLegTestUtils.GetSABRBGMRateModelParameters(AsOf, new Tenor("3M"));
      sp1.SwapLeg.Floor = 2e-2;
      var ps = sp1.GetPaymentSchedule(null, AsOf);
      foreach (FloatingInterestPayment ip in ps.GetPaymentsByType<FloatingInterestPayment>())
      {
        Assert.Greater(ip.EffectiveRate, 2e-2 - 1e-8);
      }
      DataTable dt = ps.ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void YoYCappedCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);
     
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, cpiCurve, cpi, 0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.InflationRate;
      sp1.FwdRateModelParameters = SwapLegTestUtils.GetSABRBGMRateModelParameters(AsOf, new Tenor("3M"));
      sp1.SwapLeg.Cap = 1e-2;
      var ps = sp1.GetPaymentSchedule(null, AsOf);
      foreach (FloatingInterestPayment ip in ps.GetPaymentsByType<FloatingInterestPayment>())
      {
        Assert.Greater(sp1.SwapLeg.Cap.Value + 1e-8, ip.EffectiveRate);
      }
      DataTable dt = ps.ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void InflationBondReleaseLagCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf, new Tenor(Frequency.Monthly));
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, Dt.Add(AsOf, -1, TimeUnit.Months));
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);
      Assert.AreEqual(cpiCurve.SpotPrice, rr.AllResets.Last(r => r.Key <= AsOf).Value);
      var sp1 = SwapLegTestUtils.GetOffTheRunInflationBondPricer(AsOf, dc, cpiCurve, cpi, 0.05, 1.0);
      sp1.InflationBond.ProjectionType = ProjectionType.InflationForward;
      sp1.InflationBond.FlooredNotional = true;
      var ps = sp1.GetPaymentSchedule(null, AsOf);
      DataTable dt = ps.ToDataTable();
      ResultData rd = GetResultData(dt);
      var payment = (FloatingInterestPayment)ps.First();
      Assert.AreEqual(payment.RateResetState, RateResetState.IsProjected, "ResetState");
      MatchExpects(rd);
    }


    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void InflationBondCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);

      var sp1 = SwapLegTestUtils.GetInflationBondPricer(AsOf, dc, cpiCurve, cpi, 0.05, 1.0);
      sp1.InflationBond.ProjectionType = ProjectionType.InflationForward;
      sp1.InflationBond.FlooredNotional = true;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Test UK-gilt style inflation bond with an ex-div rule.
    /// Manipulate the maturity date so that the pricer settle falls before the ex-div date, on ex-div date,
    /// between ex-div and next coupon date, on the next coupon date.
    /// Also pass the trade settle date.
    /// Besides the cash flows, also check Accrued, MTM and Pv.
    /// </summary>
    [Test]
    public void InflationBondExDivCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);

      InflationBondPricer sp1 = GetInflationBondPricerForExDivBond(dc, cpiCurve, cpi);
      sp1.InflationBond.ProjectionType = ProjectionType.InflationForward;
      sp1.InflationBond.FlooredNotional = true;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      double pv = sp1.Pv();
      double mtm = sp1.CalculateMTM();
      double accrued = sp1.Accrued(); // Inflation-adjusted accrued
      int numAdditionalsResults = 1;
      int numResultsPopulated;
      ResultData rd = GetResultData(dt, numAdditionalsResults, out numResultsPopulated);
      int resind = numResultsPopulated;
      if (rd.Results[resind] == null)
      {
        rd.Results[resind] = new ResultData.ResultSet();
        rd.Results[resind].Name = "Pricer Outputs";
        rd.Results[resind].Labels = new string[] {"Pv", "MTM", "Accrued"};
      }
      rd.Results[resind].Actuals = new double[] { pv, mtm, accrued };
      MatchExpects(rd);
    }

    private InflationBondPricer GetInflationBondPricerForExDivBond(DiscountCurve dc, CalibratedCurve referenceCurve, ReferenceIndex referenceIndex)
    {
      Dt tradeSettle = Dt.AddDays(TradeDate, 2, Calendar.NYB);
      Dt pricerSettle = Dt.AddDays(AsOf, 2, Calendar.NYB);
      Tenor indexationLag = Tenor.Parse(IndexationLag);
      InflationBond bond = new InflationBond(Effective, Maturity, Currency.USD, BondType.UKGilt, 0.04,
                                             DayCount.Thirty360, CycleRule.None, Frequency.SemiAnnual,
                                             BDConvention.Following, Calendar.NYB,
                                             (InflationIndex)referenceIndex, SwapLegTestUtils.InitialInflation(), indexationLag)
                           {
                             ProjectionType = ProjectionType.InflationForward,  // like TIPS
                             FlooredNotional = false,
                             IndexationMethod = this.IndexationMethod,
                             BondExDivRule = new ExDivRule(ExDivDays, true)
                           };
      RateModelParameters pars = SwapLegTestUtils.GetSABRBGMRateModelParameters(AsOf, Tenor.Parse("3M"));
      double notional = 100000000;
      var pricer = new InflationBondPricer(bond, AsOf, pricerSettle, notional, dc, null, (InflationCurve)referenceCurve, null,
                                           pars);
      pricer.SetMarketQuote(0.96, QuotingConvention.FlatPrice, dc, Double.NaN);
      double tradePayment = 99000000;
      pricer.Payment = new UpfrontFee(tradeSettle, -1 * tradePayment, Currency.USD);
      pricer.TradeSettle = tradeSettle;
      return pricer;
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void InflationBondRiskyCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);

      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);
      var sp1 = SwapLegTestUtils.GetInflationBondPricer(AsOf, dc, cpiCurve, cpi, 0.05, 1.0);
      sp1.InflationBond.ProjectionType = ProjectionType.InflationForward;
      sp1.InflationBond.FlooredNotional = true;
      double pv = sp1.Pv();
      SurvivalCurve sc = new SurvivalCurve(AsOf, 0.02);
      sp1.SurvivalCurve = sc;
      double riskyPv = sp1.Pv();
      Assert.AreNotEqual(pv, riskyPv);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void InflationBondCFCheckFloored()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);

      var sp1 = SwapLegTestUtils.GetInflationBondPricer(AsOf, dc, cpiCurve, cpi, 0.05, 1.0);
      sp1.InflationBond.ProjectionType = ProjectionType.InflationForward;
      sp1.InflationBond.Floor = 0.02;
      sp1.InflationBond.FlooredNotional = true;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void InflationBondCFCheckCapped()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);

      var sp1 = SwapLegTestUtils.GetInflationBondPricer(AsOf, dc, cpiCurve, cpi, 0.05, 1.0);
      sp1.InflationBond.ProjectionType = ProjectionType.InflationForward;
      sp1.InflationBond.Cap = 0.045;
      sp1.InflationBond.FlooredNotional = true;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }


    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void InflationBondIOCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);

      var sp1 = SwapLegTestUtils.GetInflationBondPricer(AsOf, dc, cpiCurve, cpi, 0.05, 1.0);
      sp1.InflationBond.ProjectionType = ProjectionType.InflationForward;
      sp1.InflationBond.FlooredNotional = true;
      sp1.InflationBond.InterestOnly = true;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void InflationRateBondAccretingFlooredNotionalCappedCouponCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);
      var sp1 = SwapLegTestUtils.GetInflationBondPricer(AsOf, dc, cpiCurve, cpi, 20e-4, 1.0);
      List<InflationBond.Accretion> accretions = new List<InflationBond.Accretion>();
      accretions.Add(new InflationBond.Accretion(){Date = Dt.Add(AsOf,  "1Y")});
      accretions.Add(new InflationBond.Accretion(){Date = Dt.Add(AsOf,  "2Y")});
      accretions.Add(new InflationBond.Accretion() { Date = Dt.Add(AsOf, "3Y") });
      accretions.Add(new InflationBond.Accretion() { Date = Dt.Add(AsOf, "4Y") });
      accretions.Add(new InflationBond.Accretion() { Date = Dt.Add(AsOf, "5Y") });
      sp1.InflationBond.Freq = Frequency.Annual;
      sp1.InflationBond.SpreadType = SpreadType.Additive;
      sp1.InflationBond.FlooredNotional = true;
      accretions.ForEach(a => sp1.InflationBond.AccretionSchedule.Add(a));
      sp1.InflationBond.ProjectionType = ProjectionType.InflationRate;
      sp1.InflationBond.Cap = 0.03;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }


    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void InflationRateBondAccretingCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);
      var sp1 = SwapLegTestUtils.GetInflationBondPricer(AsOf, dc, cpiCurve, cpi, 0.0, 1.0);
      List<InflationBond.Accretion> accretions = new List<InflationBond.Accretion>();
      accretions.Add(new InflationBond.Accretion() { Date = Dt.Add(AsOf, "1Y") });
      accretions.Add(new InflationBond.Accretion() { Date = Dt.Add(AsOf, "2Y") });
      accretions.Add(new InflationBond.Accretion() { Date = Dt.Add(AsOf, "3Y") });
      accretions.Add(new InflationBond.Accretion() { Date = Dt.Add(AsOf, "4Y") });
      accretions.Add(new InflationBond.Accretion() { Date = Dt.Add(AsOf, "5Y") });
      sp1.InflationBond.Freq = Frequency.Annual;
      accretions.ForEach(a => sp1.InflationBond.AccretionSchedule.Add(a));
      sp1.InflationBond.ProjectionType = ProjectionType.InflationRate;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void InflationBondMultiplicativeSpreadCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);
      var sp1 = SwapLegTestUtils.GetInflationBondPricer(AsOf, dc, cpiCurve, cpi, 1.1, 1.0);
      sp1.InflationBond.ProjectionType = ProjectionType.InflationRate;
      sp1.InflationBond.SpreadType = SpreadType.Multiplicative;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void InflationBondMultiplicativeSpreadFlooredCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);
      var sp1 = SwapLegTestUtils.GetInflationBondPricer(AsOf, dc, cpiCurve, cpi, 1.1, 1.0);
      sp1.InflationBond.ProjectionType = ProjectionType.InflationRate;
      sp1.InflationBond.SpreadType = SpreadType.Multiplicative;
      sp1.InflationBond.Floor = 0.06;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }


    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void InflationBondMultiplicativeSpreadCappedCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);
      var sp1 = SwapLegTestUtils.GetInflationBondPricer(AsOf, dc, cpiCurve, cpi, 1.1, 1.0);
      sp1.InflationBond.ProjectionType = ProjectionType.InflationRate;
      sp1.InflationBond.SpreadType = SpreadType.Multiplicative;
      sp1.InflationBond.Cap = 0.03;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }


    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void InflationBondAccretingNotionalCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);
      var sp1 = SwapLegTestUtils.GetInflationBondPricer(AsOf, dc, cpiCurve, cpi, 1.1, 1.0);
      Dt effective = sp1.InflationBond.Effective;
      Dt dt = effective;
      var accretionSchedule = new List<InflationBond.Accretion>();
      while(dt < sp1.InflationBond.Maturity)
      {
        dt = Dt.Add(dt, "1Y");
        accretionSchedule.Add(new InflationBond.Accretion(){Date = dt});
      }
      accretionSchedule.ForEach(a => sp1.InflationBond.AccretionSchedule.Add(a));
      sp1.InflationBond.FlooredNotional = false;
      DataTable dtb = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dtb);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void InflationBondAccretingNotionalFlooredCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      InflationIndex cpi = SwapLegTestUtils.GetCPIIndex(AsOf);
      var rr = SwapLegTestUtils.GetCPIResets(InitialInflation, AsOf);
      cpi.HistoricalObservations = rr;
      InflationCurve cpiCurve = SwapLegTestUtils.GetCPICurve(AsOf, dc, cpi);
      var sp1 = SwapLegTestUtils.GetInflationBondPricer(AsOf, dc, cpiCurve, cpi, 1.1, 1.0);
      Dt effective = sp1.InflationBond.Effective;
      Dt dt = effective;
      var accretionSchedule = new List<InflationBond.Accretion>();
      while (dt < sp1.InflationBond.Maturity)
      {
        dt = Dt.Add(dt, "1Y");
        accretionSchedule.Add(new InflationBond.Accretion() { Date = dt });
      }
      accretionSchedule.ForEach(a => sp1.InflationBond.AccretionSchedule.Add(a));
      sp1.InflationBond.FlooredNotional = true;
      DataTable dtb = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dtb);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void DailyArithmeticAverageDailyRatesCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("1D");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.ArithmeticAverageRate;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }


    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void DailyArithmeticAverageDailyRatesFlooredCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("1D");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.ArithmeticAverageRate;
      sp1.SwapLeg.Floor = 2e-2;
      var ps = sp1.GetPaymentSchedule(null, AsOf);
      foreach (FloatingInterestPayment ip in ps.GetPaymentsByType<FloatingInterestPayment>())
      {
        Assert.Greater(ip.EffectiveRate, 2e-2 - 1e-8);
      }
      DataTable dt = ps.ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void DailyArithmeticAverageDailyRatesCappedCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("1D");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.ArithmeticAverageRate;
      sp1.SwapLeg.Cap = 1e-2;
      var ps = sp1.GetPaymentSchedule(null, AsOf);
      foreach (FloatingInterestPayment ip in ps.GetPaymentsByType<FloatingInterestPayment>())
      {
        Assert.Greater(sp1.SwapLeg.Cap.Value + 1e-8, ip.EffectiveRate);
      }
      DataTable dt = ps.ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }
   
    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void ApproxDailyArithmeticAverageDailyRatesCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("1D");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.ArithmeticAverageRate;
      sp1.ApproximateForFastCalculation = true;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void TBillBasisSwapRatesCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = new InterestRateIndex("Treasury", new Tenor(3, TimeUnit.Months), Currency.USD,
                                                DayCount.Actual360, Calendar.NYB, BDConvention.Modified,
                                                Frequency.Weekly, CycleRule.Friday, 2);
      ri.HistoricalObservations = SwapLegTestUtils.GetHistoricalResets(AsOf, Toolkit.Base.Frequency.Weekly,
                                                                       CycleRule.Friday, Calendar.NYB, "3M");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.TBillArithmeticAverageRate;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void TBillApproxBasisSwapRatesCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = new InterestRateIndex("Treasury", new Tenor(3, TimeUnit.Months), Currency.USD,
                                                DayCount.Actual360, Calendar.NYB, BDConvention.Modified,
                                                Frequency.Weekly, CycleRule.Friday, 2);
      ri.HistoricalObservations = SwapLegTestUtils.GetHistoricalResets(AsOf, Toolkit.Base.Frequency.Weekly,
                                                                       CycleRule.Friday, Calendar.NYB, "3M");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.TBillArithmeticAverageRate;
      sp1.ApproximateForFastCalculation = true;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Check to make sure the first compounding period start date is on Monday for TBill rate averaging
    /// </summary>
    /// <returns></returns>
    [Test]
    public void TBillBasisSwapCompoundingCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = new InterestRateIndex("Treasury", Tenor.Parse("3M"), Toolkit.Base.Currency.USD,
                                                DayCount.Actual360, Calendar.NYB, BDConvention.Modified,
                                                Frequency.Weekly, CycleRule.Monday, 2);
      ri.HistoricalObservations = SwapLegTestUtils.GetHistoricalResets(AsOf, Frequency.Weekly, CycleRule.Monday,
                                                                       Toolkit.Base.Calendar.NYB, "1W");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.TBillArithmeticAverageRate;
      sp1.SwapLeg.InArrears = true;
      Dt refDate = Dt.AddMonths(AsOf, -3, CycleRule.None);
      sp1.SwapLeg.Effective = Dt.NthWeekDay(refDate.Month, refDate.Year, 1, DayOfWeek.Thursday);
      sp1.SwapLeg.Maturity = Dt.Add(sp1.SwapLeg.Effective, "2Y");
      sp1.ApproximateForFastCalculation = false;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void CpBasisSwapRatesCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = new InterestRateIndex("Treasury", new Tenor(3, TimeUnit.Months), Currency.USD,
                                                DayCount.Actual360, Calendar.NYB, BDConvention.Modified,
                                                Frequency.Weekly, CycleRule.Friday, 2);
      ri.HistoricalObservations = SwapLegTestUtils.GetHistoricalResets(AsOf, Toolkit.Base.Frequency.Weekly,
                                                                       CycleRule.Friday, Calendar.NYB, "3M");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.CPArithmeticAverageRate;
      sp1.SwapLeg.Freq = Frequency.SemiAnnual;
      sp1.SwapLeg.CompoundingFrequency = Frequency.Quarterly;
      sp1.SwapLeg.CompoundingConvention = CompoundingConvention.FlatISDA;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void CpApproxBasisSwapRatesCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = new InterestRateIndex("Treasury", new Tenor(3, TimeUnit.Months), Currency.USD,
                                                DayCount.Actual360, Calendar.NYB, BDConvention.Modified,
                                                Frequency.Weekly, CycleRule.Friday, 2);
      ri.HistoricalObservations = SwapLegTestUtils.GetHistoricalResets(AsOf, Toolkit.Base.Frequency.Weekly,
                                                                       CycleRule.Friday, Calendar.NYB, "3M");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.CPArithmeticAverageRate;
      sp1.SwapLeg.Freq = Frequency.SemiAnnual;
      sp1.SwapLeg.CompoundingFrequency = Frequency.Quarterly;
      sp1.SwapLeg.CompoundingConvention = CompoundingConvention.FlatISDA;
      sp1.ApproximateForFastCalculation = true;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void ApproxVsExactDailyArithmeticAverageDailyRatesCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("1D");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.ArithmeticAverageRate;
      sp1.ApproximateForFastCalculation = true;
      PaymentSchedule psApprox = sp1.GetPaymentSchedule(null, AsOf);
      sp1.ApproximateForFastCalculation = false;
      PaymentSchedule psExact = sp1.GetPaymentSchedule(null, AsOf);
      foreach (Dt d in psExact.GetPaymentDates())
      {
        double rateApprox = ((List<InterestPayment>) psApprox.GetPaymentsByType<InterestPayment>(d))[0].EffectiveRate;
        double rateExact = ((List<InterestPayment>) psExact.GetPaymentsByType<InterestPayment>(d))[0].EffectiveRate;
        double diff = Math.Abs(rateExact - rateApprox);
        Assert.AreEqual(0.0, diff, 1e-3);
      }
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void DailyArithmeticAverageQuarterlyRatesCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.ArithmeticAverageRate;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void ApproxDailyArithmeticAverageQuarterlyRatesCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.ArithmeticAverageRate;
      sp1.ApproximateForFastCalculation = true;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void ApproxVsExactDailyArithmeticAverageQuarterlyRatesCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.ArithmeticAverageRate;
      sp1.ApproximateForFastCalculation = true;
      PaymentSchedule psApprox = sp1.GetPaymentSchedule(null, AsOf);
      sp1.ApproximateForFastCalculation = false;
      PaymentSchedule psExact = sp1.GetPaymentSchedule(null, AsOf);
      foreach (Dt d in psExact.GetPaymentDates())
      {
        double rateApprox = ((List<InterestPayment>) psApprox.GetPaymentsByType<InterestPayment>(d))[0].EffectiveRate;
        double rateExact = ((List<InterestPayment>) psExact.GetPaymentsByType<InterestPayment>(d))[0].EffectiveRate;
        double diff = Math.Abs(rateExact - rateApprox);
        Assert.AreEqual(0.0, diff, 1e-3);
      }
    }


    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void DailyGeometricAverageDailyRatesCFCheck()
    {
       DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("1D");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.GeometricAverageRate;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void DailyGeometricAverageDailyRatesFlooredCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("1D");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.GeometricAverageRate;
      sp1.SwapLeg.Floor = 2e-2;
      var ps = sp1.GetPaymentSchedule(null, AsOf);
      foreach (FloatingInterestPayment ip in ps.GetPaymentsByType<FloatingInterestPayment>())
      {
        Assert.Greater(ip.EffectiveRate, 2e-2 - 1e-8);
      }
      DataTable dt = ps.ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void DailyGeometricAverageDailyRatesCappedCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("1D");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.GeometricAverageRate;
      sp1.SwapLeg.Cap = 1e-2;
      var ps = sp1.GetPaymentSchedule(null, AsOf);
      foreach (FloatingInterestPayment ip in ps.GetPaymentsByType<FloatingInterestPayment>())
      {
        Assert.Greater(sp1.SwapLeg.Cap.Value + 1e-8, ip.EffectiveRate);
      }
      DataTable dt = ps.ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void ApproxDailyGeometricAverageDailyRatesCFCheck()
    {
        DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("1D");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.GeometricAverageRate;
      sp1.ApproximateForFastCalculation = true;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void ApproxVsExactDailyGeometricAverageDailyRatesCFCheck()
    {
       DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("1D");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.GeometricAverageRate;
      sp1.ApproximateForFastCalculation = true;
      PaymentSchedule psApprox = sp1.GetPaymentSchedule(null, AsOf);
      sp1.ApproximateForFastCalculation = false;
      PaymentSchedule psExact = sp1.GetPaymentSchedule(null, AsOf);
      foreach (Dt d in psExact.GetPaymentDates())
      {
        double rateApprox = ((List<InterestPayment>) psApprox.GetPaymentsByType<InterestPayment>(d))[0].EffectiveRate;
        double rateExact = ((List<InterestPayment>) psExact.GetPaymentsByType<InterestPayment>(d))[0].EffectiveRate;
        double diff = Math.Abs(rateExact - rateApprox);
        Assert.AreEqual(0.0, diff, 1e-3);
      }
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void DailyGeometricAverageQuarterlyRatesCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.GeometricAverageRate;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void ApproxDailyGeometricAverageQuarterlyRatesCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.GeometricAverageRate;
      sp1.ApproximateForFastCalculation = true;
      DataTable dt = sp1.GetPaymentSchedule(null, AsOf).ToDataTable();
      ResultData rd = GetResultData(dt);
      MatchExpects(rd);
    }

    /// <summary>
    /// Capture results
    /// </summary>
    [Test]
    public void ApproxVsExactDailyGeometricAverageQuarterlyRatesCFCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();
      ReferenceIndex ri = SwapLegTestUtils.GetLiborIndex("3M");
      var sp1 = SwapLegTestUtils.GetFloatingSwapPricer(AsOf, dc, ri, 0.0, 1.0);
      sp1.SwapLeg.ProjectionType = ProjectionType.GeometricAverageRate;
      sp1.ApproximateForFastCalculation = true;
      PaymentSchedule psApprox = sp1.GetPaymentSchedule(null, AsOf);
      sp1.ApproximateForFastCalculation = false;
      PaymentSchedule psExact = sp1.GetPaymentSchedule(null, AsOf);
      foreach (Dt d in psExact.GetPaymentDates())
      {
        double rateApprox = ((List<InterestPayment>) psApprox.GetPaymentsByType<InterestPayment>(d))[0].EffectiveRate;
        double rateExact = ((List<InterestPayment>) psExact.GetPaymentsByType<InterestPayment>(d))[0].EffectiveRate;
        double diff = Math.Abs(rateExact - rateApprox);
        Assert.AreEqual(0.0, diff, 1e-3);
      }
    }

    /// <summary>
    /// Check amortizing dates with accrue on cycle set to true and modified roll
    /// </summary>
    [Test]
    public void CheckAmortizingCashflowsAOCTrueModified()
    {
      bool accrueOnCycle = true;
      var asof = new Dt(15, 1, 2009);
      var effective = new Dt(15, 1, 2009);
      var maturity = new Dt(15, 1, 2018);
      var discount = new DiscountCurve(asof, 0.04);
      double coupon = 0.05;
      var swp = new SwapLeg(effective, maturity, Currency.USD, coupon, DayCount.Actual360, Frequency.Quarterly,
                            BDConvention.Modified, Calendar.NYB, accrueOnCycle);
      swp.FinalExchange = true;
      swp.IntermediateExchange = true;
      swp.InitialExchange = true;
      var pricer = new SwapLegPricer(swp, asof, asof, 1, discount, null, null, null, null, null);
      Cashflow cf = pricer.GenerateCashflow(null, asof);
      for (int i = 0; i < cf.Count; i++)
      {
        Dt accrualstart = cf.GetStartDt(i);
        swp.AmortizationSchedule.Add(new Amortization(accrualstart, AmortizationType.RemainingNotionalLevels, 1 - 0.05*i));
      }
      pricer.Reset();
      cf = pricer.GenerateCashflow(null, asof);
      double diff = 0;
      double init = 1;
      for (int i = 0; i < cf.Count; i++)
      {
        diff += Math.Abs(init*coupon*cf.GetPeriodFraction(i) - cf.GetAccrued(i));
        init -= 0.05;
      }
      double diff1 = 0.0;
      for (int i = 1; i < cf.Count - 1; i++)
      {
        diff1 += Math.Abs(0.05 - cf.GetAmount(i));
      }
      Assert.AreEqual(diff, 0.0, 1e-10);
      Assert.AreEqual(diff1, 0.0, 1e-10);
    }

    /// <summary>
    /// Check amortizing dates with accrue on cycle set to false and modified roll
    /// </summary>
    [Test]
    public void CheckAmortizingCashflowsAOCFalseModified()
    {
      bool accrueOnCycle = false;
      var asof = new Dt(15, 1, 2009);
      var effective = new Dt(15, 1, 2009);
      var maturity = new Dt(15, 1, 2018);
      var discount = new DiscountCurve(asof, 0.04);
      double coupon = 0.05;
      var swp = new SwapLeg(effective, maturity, Currency.USD, coupon, DayCount.Actual360, Frequency.Quarterly,
                            BDConvention.Modified, Calendar.NYB, accrueOnCycle);
      swp.FinalExchange = true;
      swp.IntermediateExchange = true;
      swp.InitialExchange = true;
      var pricer = new SwapLegPricer(swp, asof, asof, 1, discount, null, null, null, null, null);
      Cashflow cf = pricer.GenerateCashflow(null, asof);
      for (int i = 0; i < cf.Count; i++)
      {
        Dt accrualstart = cf.GetStartDt(i);
        swp.AmortizationSchedule.Add(new Amortization(accrualstart, AmortizationType.RemainingNotionalLevels, 1 - 0.05*i));
      }
      pricer.Reset();
      cf = pricer.GenerateCashflow(null, asof);
      double diff = 0;
      double init = 1;
      for (int i = 0; i < cf.Count; i++)
      {
        diff += Math.Abs(init*coupon*cf.GetPeriodFraction(i) - cf.GetAccrued(i));
        init -= 0.05;
      }
      double diff1 = 0.0;
      for (int i = 1; i < cf.Count - 1; i++)
      {
        diff1 += Math.Abs(0.05 - cf.GetAmount(i));
      }
      Assert.AreEqual(diff, 0.0, 1e-10);
      Assert.AreEqual(diff1, 0.0, 1e-10);
    }

    /// <summary>
    /// Check amortizing dates with accrue on cycle set to true and following roll
    /// </summary>
    [Test]
    public void CheckAmortizingCashflowsAOCTrueFollowing()
    {
      bool accrueOnCycle = true;
      var asof = new Dt(15, 1, 2009);
      var effective = new Dt(15, 1, 2009);
      var maturity = new Dt(15, 1, 2018);
      var discount = new DiscountCurve(asof, 0.04);
      double coupon = 0.05;
      var swp = new SwapLeg(effective, maturity, Currency.USD, coupon, DayCount.Actual360,
                            Frequency.Quarterly, BDConvention.Following, Calendar.NYB, accrueOnCycle);
      swp.FinalExchange = true;
      swp.IntermediateExchange = true;
      swp.InitialExchange = true;
      var pricer = new SwapLegPricer(swp, asof, asof, 1, discount, null, null, null, null, null);
      Cashflow cf = pricer.GenerateCashflow(null, asof);
      for (int i = 0; i < cf.Count; i++)
      {
        Dt accrualstart = cf.GetStartDt(i);
        swp.AmortizationSchedule.Add(new Amortization(accrualstart, AmortizationType.RemainingNotionalLevels, 1 - 0.05*i));
      }
      pricer.Reset();
      cf = pricer.GenerateCashflow(null, asof);
      double diff = 0;
      double init = 1;
      for (int i = 0; i < cf.Count; i++)
      {
        diff += Math.Abs(init*coupon*cf.GetPeriodFraction(i) - cf.GetAccrued(i));
        init -= 0.05;
      }
      double diff1 = 0.0;
      for (int i = 1; i < cf.Count - 1; i++)
      {
        diff1 += Math.Abs(0.05 - cf.GetAmount(i));
      }
      Assert.AreEqual(diff, 0.0, 1e-10);
      Assert.AreEqual(diff1, 0.0, 1e-10);
    }

    /// <summary>
    /// Check amortizing dates with accrue on cycle set to false and following roll
    /// </summary>
    [Test]
    public void CheckAmortizingCashflowsAOCFalseFollowing()
    {
      bool accrueOnCycle = false;
      var asof = new Dt(15, 1, 2009);
      var effective = new Dt(15, 1, 2009);
      var maturity = new Dt(15, 1, 2018);
      var discount = new DiscountCurve(asof, 0.04);
      double coupon = 0.05;
      var swp = new SwapLeg(effective, maturity, Currency.USD, coupon, DayCount.Actual360,
                            Frequency.Quarterly, BDConvention.Following, Calendar.NYB, false);
      swp.FinalExchange = true;
      swp.IntermediateExchange = true;
      swp.InitialExchange = true;
      var pricer = new SwapLegPricer(swp, asof, asof, 1, discount, null, null, null, null, null);
      Cashflow cf = pricer.GenerateCashflow(null, asof);
      for (int i = 0; i < cf.Count; i++)
      {
        Dt accrualstart = cf.GetStartDt(i);
        swp.AmortizationSchedule.Add(new Amortization(accrualstart, AmortizationType.RemainingNotionalLevels, 1 - 0.05*i));
      }
      pricer.Reset();
      cf = pricer.GenerateCashflow(null, asof);
      double diff = 0;
      double init = 1;
      for (int i = 0; i < cf.Count; i++)
      {
        diff += Math.Abs(init*coupon*cf.GetPeriodFraction(i) - cf.GetAccrued(i));
        init -= 0.05;
      }
      double diff1 = 0.0;
      for (int i = 1; i < cf.Count - 1; i++)
      {
        diff1 += Math.Abs(0.05 - cf.GetAmount(i));
      }
      Assert.AreEqual(diff, 0.0, 1e-10);
      Assert.AreEqual(diff1, 0.0, 1e-10);
    }


    /// <summary>
    /// Check coupon dates with accrue on cycle set to true and modified roll
    /// </summary>
    [Test]
    public void CheckStepUpCashflowsAOCTrueModified()
    {
      bool accrueOnCycle = true;
      var asof = new Dt(15, 1, 2009);
      var effective = new Dt(15, 1, 2009);
      var maturity = new Dt(15, 1, 2018);
      var discount = new DiscountCurve(asof, 0.04);
      var swp = new SwapLeg(effective, maturity, Currency.USD, 0.0, DayCount.Thirty360,
                            Frequency.Quarterly, BDConvention.Modified, Calendar.NYB, accrueOnCycle);
      swp.FinalExchange = true;
      swp.IntermediateExchange = true;
      swp.InitialExchange = true;
      var pricer = new SwapLegPricer(swp, asof, asof, 1, discount, null, null, null, null, null);
      Cashflow cf = pricer.GenerateCashflow(null, asof);
      for (int i = 0; i < cf.Count; i++)
        swp.CouponSchedule.Add(new CouponPeriod(cf.GetStartDt(i), 0.05*(i + 1)));
      pricer.Reset();
      cf = pricer.GenerateCashflow(null, asof);
      double diff = 0;
      double coupon = 0.05;
      for (int i = 0; i < cf.Count; i++)
      {
        diff += Math.Abs(coupon*cf.GetPeriodFraction(i) - cf.GetAccrued(i));
        coupon += 0.05;
      }
      Assert.AreEqual(diff, 0.0, 1e-10);
    }

    /// <summary>
    /// Check coupon dates with accrue on cycle set to false and modified roll
    /// </summary>
    [Test]
    public void CheckStepUpCashflowsAOCFalseModified()
    {
      bool accrualOnCycle = false;
      var asof = new Dt(15, 1, 2009);
      var effective = new Dt(15, 1, 2009);
      var maturity = new Dt(15, 1, 2018);
      var discount = new DiscountCurve(asof, 0.04);
      var swp = new SwapLeg(effective, maturity, Currency.USD, 0.0, DayCount.Thirty360, Frequency.Quarterly,
                            BDConvention.Modified, Calendar.NYB, accrualOnCycle);
      swp.FinalExchange = true;
      swp.IntermediateExchange = true;
      swp.InitialExchange = true;
      var pricer = new SwapLegPricer(swp, asof, asof, 1, discount, null, null, null, null, null);
      Cashflow cf = pricer.GenerateCashflow(null, asof);
      for (int i = 0; i < cf.Count; i++)
        swp.CouponSchedule.Add(new CouponPeriod(cf.GetStartDt(i), 0.05*(i + 1)));
      cf = pricer.GenerateCashflow(null, asof);
      double diff = 0;
      double coupon = 0.05;
      for (int i = 0; i < cf.Count; i++)
      {
        diff += Math.Abs(coupon*cf.GetPeriodFraction(i) - cf.GetAccrued(i));
        coupon += 0.05;
      }
      Assert.AreEqual(diff, 0.0, 1e-10);
    }

    /// <summary>
    /// Check coupon dates with accrue on cycle set to false and modified roll
    /// </summary>
    [Test]
    public void CheckStepUpCashflowsOACFalseFollowing()
    {
      bool accrualOnCycle = false;
      var asof = new Dt(15, 1, 2009);
      var effective = new Dt(15, 1, 2009);
      var maturity = new Dt(15, 1, 2018);
      var discount = new DiscountCurve(asof, 0.04);
      var swp = new SwapLeg(effective, maturity, Currency.USD, 0.0, DayCount.Thirty360,
                            Frequency.Quarterly, BDConvention.Following, Calendar.NYB, accrualOnCycle);
      swp.FinalExchange = true;
      swp.IntermediateExchange = true;
      swp.InitialExchange = true;
      var pricer = new SwapLegPricer(swp, asof, asof, 1, discount, null, null, null, null, null);
      Cashflow cf = pricer.GenerateCashflow(null, asof);
      for (int i = 0; i < cf.Count; i++)
        swp.CouponSchedule.Add(new CouponPeriod(cf.GetStartDt(i), 0.05*(i + 1)));
      cf = pricer.GenerateCashflow(null, asof);
      double diff = 0;
      double coupon = 0.05;
      for (int i = 0; i < cf.Count; i++)
      {
        diff += Math.Abs(coupon*cf.GetPeriodFraction(i) - cf.GetAccrued(i));
        coupon += 0.05;
      }
      Assert.AreEqual(diff, 0.0, 1e-10);
    }

    private static RateModelParameters GetForwardRateModelParameters(Dt asOf)
    {
      var sigma = new Curve(asOf, .2);
      return new RateModelParameters(RateModelParameters.Model.BGM, new[] { RateModelParameters.Param.Sigma },
                                     new[] { sigma },
                                     new Tenor(3, TimeUnit.Months), Toolkit.Base.Currency.USD);
    }

    [Test]
    public void EDFutureWtForwardModelCFCheck()
    {
      var t = new Timer();
      t.Start();
      var dc = DiscountData.GetDiscountCurve();
      var lastDelivery = Dt.Add(AsOf, 6, TimeUnit.Months);
      var tenor = new Tenor(3, TimeUnit.Months);
      var depositAccrualStart = lastDelivery;
      var depositAccrualEnd = Dt.Roll(Dt.Add(lastDelivery, tenor), BDConvention.Following, Calendar.NYB);
      var index = new InterestRateIndex(String.Empty, tenor, Currency.USD, DayCount.Actual360, Calendar.NYB, BDConvention.Following, 2);
      var future = new StirFuture(RateFutureType.MoneyMarketCashRate, lastDelivery, depositAccrualStart, depositAccrualEnd, index,
        1000000, 0.5 / 1e4, 12.5);
      var pricer = new StirFuturePricer(future, AsOf, AsOf, 1, dc, dc) { RateModelParameters = GetForwardRateModelParameters(AsOf) };
      var dt = pricer.GetPaymentSchedule(null, AsOf).ToDataTable();
      var rd = GetResultData(dt);
      MatchExpects(rd);
    }

    [Test]
    public void EDFutureWtSpotModelCFCheck()
    {
      var t = new Timer();
      t.Start();
      var dc = DiscountData.GetDiscountCurve();
      var lastDelivery = Dt.Add(AsOf, 6, TimeUnit.Months);
      var tenor = new Tenor(3, TimeUnit.Months);
      var depositAccrualStart = lastDelivery;
      var depositAccrualEnd = Dt.Roll(Dt.Add(lastDelivery, tenor), BDConvention.Following, Calendar.LNB);
      var index = new InterestRateIndex(String.Empty, tenor, Currency.USD, DayCount.Actual360, Calendar.LNB, BDConvention.Following, 2);
      var future = new StirFuture(RateFutureType.MoneyMarketCashRate, lastDelivery, depositAccrualStart, depositAccrualEnd, index,
        1000000, 0.5 / 1e4, 12.5);
      var pricer = new StirFuturePricer(future, AsOf, AsOf, 1, dc, dc) { RateModelParameters =  GetForwardRateModelParameters(AsOf)};
      var dt = pricer.GetPaymentSchedule(null, AsOf).ToDataTable();
      var rd = GetResultData(dt);
      MatchExpects(rd);
    }

    //[Test] This result have not been validated
    public void FFFutureWtForwardModelCFCheck()
    {
      var t = new Timer();
      t.Start();
      var dc = DiscountData.GetDiscountCurve();
      /* Old (Wrong) example
      var lastDelivery = Dt.Roll(Dt.Add(AsOf, 6, TimeUnit.Months), BDConvention.Following, Calendar.NYB);
      var tenor = new Tenor(3, TimeUnit.Months);
      var depositAccrualStart = lastDelivery;
      var depositAccrualEnd = Dt.Roll(Dt.Add(lastDelivery, tenor), BDConvention.Following, Calendar.NYB);
       * */
      var lastDelivery = Dt.DayOfMonth(1, AsOf.Year + 1, DayOfMonth.Last, BDConvention.Preceding, Calendar.NYB);
      var tenor = new Tenor(1, TimeUnit.Days);
      var depositAccrualStart = Dt.DayOfMonth(lastDelivery.Month, lastDelivery.Year, DayOfMonth.First, BDConvention.Following, Calendar.NYB);
      var depositAccrualEnd = lastDelivery;
      var index = new InterestRateIndex(String.Empty, tenor, Currency.USD, DayCount.Actual360, Calendar.NYB, BDConvention.Following, 1);
      var future = new StirFuture(RateFutureType.ArithmeticAverageRate, lastDelivery, depositAccrualStart, depositAccrualEnd, index,
        5000000, 0.5 / 1e4, 41.67);
      var pricer = new StirFuturePricer(future, AsOf, AsOf, 1, dc, dc) {RateModelParameters = GetForwardRateModelParameters(AsOf)};
      var dt = pricer.GetPaymentSchedule(null, AsOf).ToDataTable();
      var rd = GetResultData(dt);
      MatchExpects(rd);
    }

    //[Test] This result have not been validated
    public void OISFutureWtForwardModelCFCheck()
    {
      var t = new Timer();
      t.Start();
      var dc = DiscountData.GetDiscountCurve();
      var lastTradingDate = Dt.DayOfMonth(1, AsOf.Year + 1, DayOfMonth.Last, BDConvention.Preceding, Calendar.NYB);
      var tenor = new Tenor(1, TimeUnit.Days);
      var depositAccrualStart = Dt.DayOfMonth(lastTradingDate.Month, lastTradingDate.Year, DayOfMonth.First, BDConvention.Following, Calendar.NYB);
      var depositAccrualEnd = lastTradingDate;
      var index = new InterestRateIndex(String.Empty, tenor, Currency.USD, DayCount.Actual360, Calendar.LNB, BDConvention.Following, 1);
      var future = new StirFuture(RateFutureType.MoneyMarketCashRate, lastTradingDate, depositAccrualStart, depositAccrualEnd, index,
        1000000, 0.25 / 1e4, 24.66);
      var pricer = new StirFuturePricer(future, AsOf, AsOf, 1, dc, dc) { RateModelParameters = GetForwardRateModelParameters(AsOf) };
      var dt = pricer.GetPaymentSchedule(null, AsOf).ToDataTable();
      var rd = GetResultData(dt);
      MatchExpects(rd);
    }

    [Test]
    public void CheckStepUpCashflowsOACTrueFollowing()
    {
      bool accrualOnCycle = true;
      var asof = new Dt(15, 1, 2009);
      var effective = new Dt(15, 1, 2009);
      var maturity = new Dt(15, 1, 2018);
      var discount = new DiscountCurve(asof, 0.04);
      var swp = new SwapLeg(effective, maturity, Currency.USD, 0.0, DayCount.Thirty360,
                            Frequency.Quarterly, BDConvention.Following, Calendar.NYB, accrualOnCycle);
      swp.FinalExchange = true;
      swp.IntermediateExchange = true;
      swp.InitialExchange = true;
      var pricer = new SwapLegPricer(swp, asof, asof, 1, discount, null, null, null, null, null);
      Cashflow cf = pricer.GenerateCashflow(null, asof);
      for (int i = 0; i < cf.Count; i++)
        swp.CouponSchedule.Add(new CouponPeriod(cf.GetStartDt(i), 0.05*(i + 1)));

      cf = pricer.GenerateCashflow(null, asof);
      double diff = 0;
      double coupon = 0.05;
      for (int i = 0; i < cf.Count; i++)
      {
        diff += Math.Abs(coupon*cf.GetPeriodFraction(i) - cf.GetAccrued(i));
        coupon += 0.05;
      }
      Assert.AreEqual(diff, 0.0, 1e-10);
    }

    [Test]
    public void DiscountCurveCheck()
    {
      DiscountCurve dc = DiscountData.GetDiscountCurve();

      var labels = new string[10];
      var values = new double[10];


      var t = new Timer();
      t.Start();

      labels[0] = "DF1";
      values[0] = dc.DiscountFactor(new Dt(2, 7, 2001));
      labels[1] = "DF2";
      values[1] = dc.DiscountFactor(new Dt(2, 1, 2002));
      labels[2] = "DF3";
      values[2] = dc.DiscountFactor(new Dt(1, 7, 2002));
      labels[3] = "DF4";
      values[3] = dc.DiscountFactor(new Dt(2, 1, 2003));
      labels[4] = "DF5";
      values[4] = dc.DiscountFactor(new Dt(1, 7, 2003));
      labels[5] = "DF6";
      values[5] = dc.DiscountFactor(new Dt(2, 1, 2004));
      labels[6] = "DF7";
      values[6] = dc.DiscountFactor(new Dt(1, 7, 2004));
      labels[7] = "DF8";
      values[7] = dc.DiscountFactor(new Dt(3, 1, 2005));
      labels[8] = "DF9";
      values[8] = dc.DiscountFactor(new Dt(1, 7, 2005));
      labels[9] = "DF10";
      values[9] = dc.DiscountFactor(new Dt(1, 1, 2006));

      t.Stop();

      ResultData rd = ToResultData(values, labels, t.Elapsed);
      MatchExpects(rd);
    }

    protected double InitialInflation => SwapLegTestUtils.InitialInflation();
    public Dt AsOf { get; set; }
    protected DiscountData DiscountData { get; set; }

    // Additional parameters for the benefit of InflationBondExDivCheck():
    public Dt Effective { get; set; }
    public Dt Maturity { get; set; }
    public Dt TradeDate { get; set; }
    public int ExDivDays { get; set; }
    public IndexationMethod IndexationMethod { get; set; }
    public string IndexationLag { get; set; }
  }
}