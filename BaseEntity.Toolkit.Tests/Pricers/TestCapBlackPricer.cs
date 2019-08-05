//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Base.ReferenceIndices;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Test cap/floor pricing with black model
  /// </summary>
  [TestFixture]
  public class TestCapBlackPricer : ToolkitTestBase
  {
    [Test]
    public void TestOnEffective()
    {
      Dt asOf = new Dt(21, 10, 2010);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.NYB);
      Dt effective = Dt.Add(settle, new Tenor(3, TimeUnit.Months));

      var cap = new Cap(
        effective,
        Dt.Add(effective, 5, TimeUnit.Years),
        Currency.USD,
        CapFloorType.Cap, 
        0.05,
        DayCount.Actual360,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB);
      cap.RateResetOffset = 2;
      cap.RateIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"), Currency.USD,
                                            DayCount.Actual360,
                                            Calendar.NYB, BDConvention.Following, 2);

      var volCube = RateVolatilityCube.CreateFlatVolatilityCube(asOf, new Dt[] { asOf }, new double[] { 0.5 },
                                                                          VolatilityType.LogNormal, cap.RateIndex);

      var pricer = new CapFloorPricer(cap,
                                      asOf,
                                      settle, 
                                      new DiscountCurve(asOf, 0.01),
                                      new DiscountCurve(asOf, 0.01),
                                      volCube);

      Assert.Greater(pricer.Pv(), 0.0, "PV should be greater than 0");
    }

    [Test]
    public void TestResetOnAsOf()
    {
      Dt asOf = new Dt(21, 10, 2010);
      Dt settle = Dt.AddDays(asOf, 2, Calendar.NYB);
      Dt effective = settle;

      var cap = new Cap(
        effective,
        Dt.Add(effective, 3, TimeUnit.Months),
        Currency.USD,
        CapFloorType.Cap,
        0.05,
        DayCount.Actual360,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB)
                  {
                    RateResetOffset = 2,
                    RateIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"), Currency.USD,
                                                      DayCount.Actual360,
                                                      Calendar.NYB, BDConvention.Following, 2)
                  };

      var volCube = RateVolatilityCube.CreateFlatVolatilityCube(asOf, new Dt[] { asOf }, new double[] { 0.5 },
                                                                          VolatilityType.LogNormal, cap.RateIndex);

      var pricer = new CapFloorPricer(cap,
                                      asOf,
                                      settle,
                                      new DiscountCurve(asOf, 0.01),
                                      new DiscountCurve(asOf, 0.01),
                                      volCube);
      pricer.Resets.Add(new RateReset(asOf, 0.06)); // bigger than strike, bigger than projected rate off curve

      Assert.Greater(pricer.Pv(), 0.0, "PV should be greater than 0");
    }


    [Test]
    public void TestFirstPeriod()
    {
      Dt asOf = new Dt(21, 10, 2010);
      Dt settle = Dt.AddDays(asOf, 2, Calendar.NYB);
      Dt effective = Dt.Add(asOf, -2, TimeUnit.Months);

      var cap = new Cap(
        effective,
        Dt.Add(effective, 5, TimeUnit.Years),
        Currency.USD,
        CapFloorType.Cap, 
        0.05,
        DayCount.Actual360,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB);
      cap.RateResetOffset = 0;
      cap.RateIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"), Currency.USD,
                                            DayCount.Actual360,
                                            Calendar.NYB, BDConvention.Following, 2);


      var volCube = RateVolatilityCube.CreateFlatVolatilityCube(asOf, new Dt[] { asOf }, new double[] { 0.5 },
                                                                          VolatilityType.LogNormal, cap.RateIndex);

      var pricer = new CapFloorPricer(cap,
                                      asOf,
                                      settle,
                                      new DiscountCurve(asOf, 0.01),
                                      new DiscountCurve(asOf, 0.01),
                                      volCube);
      pricer.Resets.Add(new RateReset(pricer.LastExpiry, 0.04));

      Assert.Greater(pricer.Pv(), 0.0, "PV should be greater than 0");
    }

    [Test]
    public void TestFirstPeriodWithOffset()
    {
      Dt asOf = new Dt(21, 10, 2010);
      Dt settle = Dt.AddDays(asOf, 2, Calendar.NYB);
      Dt effective = Dt.Add(asOf, -3, TimeUnit.Months);

      var cap = new Cap(
        effective,
        Dt.Add(effective, 5, TimeUnit.Years),
        Currency.USD,
        CapFloorType.Cap, 
        0.05,
        DayCount.Actual360,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB);
      cap.RateResetOffset = 2;

      var volCube = RateVolatilityCube.CreateFlatVolatilityCube(asOf, new Dt[] { asOf }, new double[] { 0.5 },
                                                                          VolatilityType.LogNormal, cap.RateIndex);
      
      
      volCube.Fit();

      var pricer = new CapFloorPricer(cap,
                                      asOf,
                                      settle,
                                      new DiscountCurve(asOf, 0.01),
                                      new DiscountCurve(asOf, 0.01),
                                      volCube);
      pricer.Resets.Add(new RateReset(pricer.LastExpiry, 0.04));

      Assert.Greater(pricer.Pv(), 0.0, "PV should be greater than 0");
    }

    [Test]
    public void TestPutCallParity()
    {
      double fixedRateStrike = 0.05;
      Dt asOf = new Dt(21, 10, 2010);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.NYB);
      Dt effective = Dt.Add(asOf, -2, TimeUnit.Months);
      Dt maturity = Dt.Add(effective, 5, TimeUnit.Years);
      var discountCurve = new DiscountCurve(asOf, fixedRateStrike);
      var projectionCurve = new DiscountCurve(asOf, fixedRateStrike);
      var referenceIndex = new Toolkit.Base.ReferenceIndices.InterestRateIndex("LIBOR", Tenor.Parse("3M"),
                                                                  Currency.USD, DayCount.Actual360, Calendar.NYB,
                                                                  BDConvention.Following, 0);

      var volCube = RateVolatilityCube.CreateFlatVolatilityCube(asOf, new Dt[] { asOf }, new double[] { 0.5 },
                                                                          VolatilityType.LogNormal, referenceIndex);
      var resets = new RateResets(new List<RateReset>() { new RateReset(effective, fixedRateStrike) });
      
      // Setup Cap
      var cap = new Cap(
        effective,
        maturity,
        Currency.USD,
        CapFloorType.Cap, 
        fixedRateStrike,
        DayCount.Actual360,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB);

      // Floor
      var floor = new Cap(
        effective,
        maturity,
        Currency.USD,
        CapFloorType.Floor, 
        fixedRateStrike,
        DayCount.Actual360,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB);

      // Swap
      var fixedLeg = new SwapLeg(
        effective,
        maturity,
        Currency.USD,
        fixedRateStrike,
        DayCount.Actual360,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB,
        false);
      var floatingLeg = new SwapLeg(
        effective,
        maturity,
        Frequency.Quarterly,
        0,
        referenceIndex);
      var swap = new Swap(floatingLeg, fixedLeg);

      // Pricers
      var capPricer = new CapFloorPricer(cap,
                                         asOf,
                                         settle,
                                         discountCurve,
                                         projectionCurve,
                                         volCube);
      capPricer.Resets.Add(new RateReset(capPricer.LastExpiry, fixedRateStrike));

      var floorPricer = new CapFloorPricer(floor,
                                           asOf,
                                           settle,
                                           discountCurve,
                                           projectionCurve,
                                           volCube);
      floorPricer.Resets.Add(new RateReset(floorPricer.LastExpiry, fixedRateStrike));

      var floatingLegPricer = new SwapLegPricer(
        floatingLeg,
        asOf,
        settle,
        1.0,
        discountCurve,
        referenceIndex,
        projectionCurve,
        resets,
        null, null);
      var fixedLegPricer = new SwapLegPricer(
        fixedLeg,
        asOf,
        settle,
        -1.0,
        discountCurve, 
        null, null, null, null, null);
      var swapPricer = new SwapPricer(floatingLegPricer, fixedLegPricer);

      // Test put/call parity when fixed swap rate = cap/floor strike
      // Cap-Floor=Swap
      var capValue = capPricer.Pv();
      var floorValue = floorPricer.Pv();
      var swapValue = swapPricer.Pv();
      Assert.AreEqual(swapValue, capValue-floorValue, 1e-4, "Put/Call parity failed!");
    }

    [Test]
    public void TestCapFloorImpliedVol()
    {
      Dt asOf = new Dt(21, 10, 2010);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.NYB);
      Dt effective = Dt.Add(asOf, -3, TimeUnit.Months);
      Dt maturity = Dt.Add(effective, 5, TimeUnit.Years);
      var discountCurve = new DiscountCurve(asOf, 0.01);
      var projectionCurve = new DiscountCurve(asOf, 0.01);
      var rateIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"),
                                                                  Currency.USD, DayCount.Actual360, Calendar.NYB,
                                                                  BDConvention.Following, 0);

      var volCube = RateVolatilityCube.CreateFlatVolatilityCube(asOf, new Dt[] { asOf }, new double[] { 0.5 },
                                                                          VolatilityType.LogNormal, rateIndex);

      double fixedRateStrike = 0.05;

      // Setup Cap
      var cap = new Cap(
        effective,
        maturity,
        Currency.USD,
        CapFloorType.Cap, 
        fixedRateStrike,
        DayCount.Actual360,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB);

      // Floor
      var floor = new Cap(
        effective,
        maturity,
        Currency.USD,
        CapFloorType.Floor, 
        fixedRateStrike,
        DayCount.Actual360,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB);

      // Pricers
      var capPricer = new CapFloorPricer(cap,
                                         asOf,
                                         settle,
                                         discountCurve,
                                         projectionCurve,
                                         volCube);
      capPricer.Resets.Add(new RateReset(capPricer.LastExpiry, 0.04));

      var floorPricer = new CapFloorPricer(floor,
                                           asOf,
                                           settle,
                                           discountCurve,
                                           projectionCurve,
                                           volCube);
      floorPricer.Resets.Add(new RateReset(floorPricer.LastExpiry, 0.04));

      // Cap and Floor should have same implied vol if they have the same strike
      var capVol = capPricer.ImpliedVolatility(capPricer.Pv());
      var floorVol = floorPricer.ImpliedVolatility(floorPricer.Pv());
      Assert.AreEqual(0, capVol-floorVol, 1e-6, "Cap and Floor implied volatility should be equal!");
    }

    [Test]
    [Ignore("Need to sort through this")]
    public void TestZeroStrikeCap()
    {
      double fixedRateStrike = 0.0;
      Dt asOf = new Dt(21, 10, 2010);
      Dt settle = Dt.AddDays(asOf, 2, Calendar.NYB);
      Dt swapEffective = Dt.Add(settle, 3, TimeUnit.Months);
      Dt swapMaturity = Dt.Add(swapEffective, 5, TimeUnit.Years);
      Dt capEffective = Dt.Add(settle, 3, TimeUnit.Months);
      Dt capMaturity = Dt.Add(settle, 5, TimeUnit.Years);
      var discountCurve = new DiscountCurve(asOf, 0.01);
      var projectionCurve = new DiscountCurve(asOf, 0.01);
      
      var referenceIndex = new Toolkit.Base.ReferenceIndices.InterestRateIndex("LIBOR", Tenor.Parse("3M"),
                                                                  Currency.USD, DayCount.Actual360, Calendar.NYB,
                                                                  BDConvention.Following, 2);

      var volCube = RateVolatilityCube.CreateFlatVolatilityCube(asOf, new Dt[] { asOf }, new double[] { 0.5 },
                                                                          VolatilityType.LogNormal, referenceIndex);
      var resets = new RateResets(fixedRateStrike, 0);
      resets.AllResets = new SortedDictionary<Dt, double>();
      resets.AllResets.Add(new Dt(19, 8, 2010), fixedRateStrike);

      // Setup Cap
      var cap = new Cap(
        capEffective,
        capMaturity,
        Currency.USD,
        CapFloorType.Cap, 
        fixedRateStrike,
        DayCount.Actual360,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB);

      // Floating leg of swap
      var floatingLeg = new SwapLeg(
        swapEffective,
        swapMaturity,
        Frequency.Quarterly,
        0,
        referenceIndex);

      // Pricers
      var capPricer = new CapFloorPricer(cap,
                                         asOf,
                                         settle,
                                         discountCurve,
                                         projectionCurve,
                                         volCube);

      var floatingLegPricer = new SwapLegPricer(
        floatingLeg,
        asOf,
        settle,
        1.0,
        discountCurve,
        referenceIndex,
        projectionCurve,
        resets,
        null, null);

      // Test 0% strike cap should be equal to floating leg
      var capValue = capPricer.Pv();
      var swapValue = floatingLegPricer.Pv();
      Assert.AreEqual(swapValue, capValue, 1e-4, "Cap value should match floating leg!");
    }

    [Test]
    public void TestCapDecreasingStrike()
    {
      Dt asOf = new Dt(21, 10, 2010);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.NYB);
      Dt effective = settle;
      var strikes = new [] {0.08, 0.07, 0.06, 0.05, 0.04, 0.03, 0.02, 0.01, 0.0};

      var cap = new Cap(
        effective,
        Dt.Add(effective, 5, TimeUnit.Years),
        Currency.USD,
        CapFloorType.Cap,
        0.0,
        DayCount.Actual360,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB);
      cap.RateResetOffset = 2;

      var referenceIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"),
                                                                  Currency.USD, DayCount.Actual360, Calendar.NYB,
                                                                  BDConvention.Following, 2);

      var volCube = RateVolatilityCube.CreateFlatVolatilityCube(asOf, new Dt[] { asOf }, new double[] { 0.5 },
                                                                          VolatilityType.LogNormal, referenceIndex);
      
      var rateCurve = new DiscountCurve(asOf, 0.01);

      // Calculate pvs
      var values = new double[strikes.Length];
      for (int i = 0; i < strikes.Length; i++)
      {
        cap.Strike = strikes[i];
        var pricer = new CapFloorPricer(cap,
                                        asOf,
                                        settle,
                                        rateCurve,
                                        rateCurve,
                                        volCube);
        pricer.Resets.Add(new RateReset(pricer.LastExpiry, 0.04));
        values[i] = pricer.Pv();
      }

      // Test that decreasing strikes have an increasing pv (greater chance of being in the money)
      for (int i = 1; i < strikes.Length; i++)
        Assert.Less(
          values[i - 1], values[i],
          String.Format("PV at strike {0:P} should be less than at strike {1:P}", strikes[i - 1], strikes[i]));
    }

    [Test]
    public void TestFloorDecreasingStrike()
    {
      Dt asOf = new Dt(21, 10, 2010);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.NYB);
      Dt effective = settle;
      var strikes = new[] { 0.08, 0.07, 0.06, 0.05, 0.04, 0.03, 0.02, 0.01, 0.0 };

      var cap = new Cap(
        effective,
        Dt.Add(effective, 5, TimeUnit.Years),
        Currency.USD,
        CapFloorType.Floor,
        0.0,
        DayCount.Actual360,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB);
      cap.RateResetOffset = 2;

      var volCube = RateVolatilityCube.CreateFlatVolatilityCube(asOf, new[] { asOf }, new double[] { 0.5 },
                                                                          VolatilityType.LogNormal, cap.RateIndex);
      
      var rateCurve = new DiscountCurve(asOf, 0.01);

      // Calculate pvs
      var values = new double[strikes.Length];
      for (int i = 0; i < strikes.Length; i++)
      {
        cap.Strike = strikes[i];
        var pricer = new CapFloorPricer(cap,
                                        asOf,
                                        settle,
                                        rateCurve,
                                        rateCurve,
                                        volCube);
        pricer.Resets.Add(new RateReset(pricer.LastExpiry, 0.04));
        values[i] = pricer.Pv();
      }

      // Test that decreasing strikes have an increasing pv (greater chance of being in the money)
      for (int i = 1; i < strikes.Length; i++)
        Assert.Greater(
          values[i - 1], values[i],  
          String.Format("PV at strike {0:P} should be greater than at strike {1:P}", strikes[i - 1], strikes[i]));
    }



    [Test]
    public void TestCapFloorIndexMultiplier()
    {
      Dt asOf = new Dt(21, 10, 2010);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.NYB);
      Dt effective = Dt.Add(asOf, -3, TimeUnit.Months);
      Dt maturity = Dt.Add(effective, 5, TimeUnit.Years);
      var discountCurve = new DiscountCurve(asOf, 0.01);
      var projectionCurve = new DiscountCurve(asOf, 0.01);
      var rateIndex = new InterestRateIndex("LIBOR", Tenor.Parse("3M"),
        Currency.USD, DayCount.Actual360, Calendar.NYB,
        BDConvention.Following, 0);

      var volCube = RateVolatilityCube.CreateFlatVolatilityCube(asOf, new Dt[] {asOf}, new double[] {0.5},
        VolatilityType.LogNormal, rateIndex);

      double fixedRateStrike = 0.05;
      double indexMultiplier = 1.5;
      var indexFactor = 1.2;

      var indexMultSched = new List<CouponPeriod>();
      indexMultSched.Add(new CouponPeriod(settle-10, indexFactor));
      indexMultSched.Add(new CouponPeriod(settle + 90, indexFactor));

      // Setup Cap with index multiplier
      var capWithMultiplier = GetCap(effective, maturity, fixedRateStrike,
        indexMultiplier, indexMultSched);

      indexMultiplier = CouponPeriodUtil.CouponAt(indexMultSched, indexMultiplier, settle);

      var cap = GetCap(effective, maturity, fixedRateStrike/indexMultiplier, 1.0, null);
      
      // Pricers
      var capWithMultiplierPricer =GetPricer(capWithMultiplier, asOf, settle,
        discountCurve, projectionCurve, volCube);
      capWithMultiplierPricer.Resets.Add(new RateReset(capWithMultiplierPricer.LastExpiry, 0.04));

      var capPricer = GetPricer(cap, asOf, settle,
        discountCurve, projectionCurve, volCube);
      capPricer.Resets.Add(new RateReset(capPricer.LastExpiry, 0.04));

      var expect = capWithMultiplierPricer.Pv();
      var actual = indexMultiplier*capPricer.Pv();

      // Cap and replication should have the same PV
      Assert.AreEqual(indexMultiplier, indexFactor, 1E-14);
      Assert.AreEqual(expect, actual, 1E-14);
    }


    private static Cap GetCap(Dt effective, Dt maturity, double strike,
      double indexMultiplier, List<CouponPeriod> indexMultSched)
    {
      return new Cap(
        effective,
        maturity,
        Currency.USD,
        CapFloorType.Cap,
        strike,
        DayCount.Actual360,
        Frequency.Quarterly,
        BDConvention.Following,
        Calendar.NYB)
      {
        IndexMultiplierSchedule = indexMultSched
      };
    }

    private static CapFloorPricer GetPricer(Cap cap, Dt asOf, Dt settle, 
      DiscountCurve disCurve, DiscountCurve projCurve, RateVolatilityCube volCube)
    {
      return new CapFloorPricer(cap,asOf,settle,disCurve,projCurve,
        volCube);
    }




    [TestCase(false)]
    [TestCase(true)]
    public void TestCapDigitalPayoutSchedule(bool hasCustom)
    {
      Dt asOf = new Dt(21, 10, 2010);
      Dt settle = Dt.AddDays(asOf, 1, Calendar.NYB);
      Dt effective = settle;
      Dt maturity = Dt.Add(effective, 5, TimeUnit.Years);

      var cap = GetCapFloor(CapFloorType.Cap, effective, maturity);
      if (hasCustom)
      {
        cap.AmortizationSchedule = GetDefaultAmortizationSchedule(effective, "3M", 10);
        cap.StrikeSchedule = GetDefaultCouponSchedule(effective, "3M", 10);
      }

      int cc = 0;
      var onePeriodCaps = new List<Cap>();
      foreach (var period in cap.Schedule.Periods)
      {
        var payOff = 0.01 * cc;
        cap.DigitalFixedPayoutSchedule.Add(new Tuple<Dt, double>(period.AccrualBegin, payOff));

        var caplet = GetCapFloor(CapFloorType.Cap, period.AccrualBegin, period.AccrualEnd);
        caplet.DigitalFixedPayout = payOff;
        if (hasCustom)
        {
          caplet.AmortizationSchedule = GetDefaultAmortizationSchedule(effective, "3M", 10);
          caplet.StrikeSchedule = GetDefaultCouponSchedule(effective, "3M", 10);
        }
        onePeriodCaps.Add(caplet);
        ++cc;
      }

      RateVolatilityCube volCube = RateVolatilityCube.CreateFlatVolatilityCube(
        asOf, new[] {asOf}, new[] {0.5}, VolatilityType.LogNormal, cap.RateIndex);

      var rateCurve = new DiscountCurve(asOf, 0.01);

      var pricer = new CapFloorPricer(cap, asOf, settle, rateCurve,rateCurve, volCube);

      pricer.Resets.Add(new RateReset(pricer.LastExpiry, 0.04));
      double pv = pricer.Pv();
      double pv2 = 0.0;
      foreach (Cap c in onePeriodCaps)
      {
        var pxer = new CapFloorPricer(c, asOf, settle, rateCurve, rateCurve, volCube);
        pxer.Resets.Add(new RateReset(pricer.LastExpiry, 0.04));
        pv2 += pxer.Pv();
      }

      Assert.AreEqual(pv, pv2, 2e-6, "Pv of cap = sum of pv of one-period caps");
    }

    [TestCase(0.02, 0.04, CapFloorType.Cap)] //strike < rate
    [TestCase(0.02, 0.02, CapFloorType.Cap)] //strike = rate
    [TestCase(0.04, 0.02, CapFloorType.Cap)] // strike > rate
    [TestCase(0.02, 0.4, CapFloorType.Cap)] //strike << rate
    [TestCase(0.4, 0.02, CapFloorType.Cap)] // strike >> rate
    [TestCase(-0.04, 0.02, CapFloorType.Cap)] // negative strike
    [TestCase(-0.04, -0.05, CapFloorType.Cap)] // negative strike
    [TestCase(0.02, 0.04, CapFloorType.Floor)] //strike < rate
    [TestCase(0.02, 0.02, CapFloorType.Floor)] //strike = rate
    [TestCase(0.04, 0.02, CapFloorType.Floor)] // strike > rate
    [TestCase(0.02, 0.4, CapFloorType.Floor)] //strike << rate
    [TestCase(0.4, 0.02, CapFloorType.Floor)] // strike >> rate
    [TestCase(-0.04, 0.02, CapFloorType.Floor)] // negative strike
    [TestCase(-0.04, -0.05, CapFloorType.Floor)] // negative strike

    public void TestCapletDigitalPayoffSchedule(double strike, double rate, CapFloorType type)
    {
      const double payoff = 0.01;
      Dt asOf = new Dt(20160310);
      Dt maturity =  Dt.Add(asOf, 3, TimeUnit.Months);
      var capFloor = GetCapFloor(type, asOf, maturity, strike);
      capFloor.DigitalFixedPayoutSchedule.Add(new Tuple<Dt, double>(asOf, payoff));
      var curve = new DiscountCurve(asOf, 0.02);
      var pricer = new CapFloorPricer(capFloor, asOf, asOf, curve, curve, GetFlatVol(asOf, 0.5));
      pricer.Resets.Add(new RateReset(pricer.LastExpiry, rate));
      var pv = pricer.Pv();
      var value = curve.DiscountFactor(asOf, maturity)
                  *Dt.Fraction(asOf, maturity, capFloor.DayCount)*payoff;
      var expect = type == CapFloorType.Cap
        ? (rate > strike ? value : 0.0)
        : (rate >= strike ? 0.0 : value);
      pv.IsExpected(To.Match(expect).Within(1E-14));
    }

    private static RateVolatilityCube GetFlatVol(Dt asOf, double vol)
    {
      var index = StandardReferenceIndices.Create("USDLIBOR_3M") as InterestRateIndex;
      return RateVolatilityCube.CreateFlatVolatilityCube(
       asOf, new[] { asOf }, new[] { vol }, VolatilityType.LogNormal, index);
    }

    private static Cap GetCapFloor(CapFloorType type, Dt effective, 
      Dt maturity, double strike = 0.0)
    {
      return new Cap(effective, maturity, Currency.USD, type, strike, DayCount.Actual360,
        Frequency.Quarterly, BDConvention.Following, Calendar.NYB)
      {
        OptionDigitalType = OptionDigitalType.Cash,
        RateResetOffset = 0,
      };
    }

    private static IList<Amortization> GetDefaultAmortizationSchedule(
      Dt begin, string freq, int periods)
    {
      var retVal = new List<Amortization>();
      for (int i = 0; i < periods; i++)
      {
        var date = Dt.Add(begin, Tenor.Parse(freq));
        retVal.Add(new Amortization(date, AmortizationType.PercentOfInitialNotional, 0.01));
        begin = date;
      }
      return retVal;
    }

    private static IList<CouponPeriod> GetDefaultCouponSchedule(
      Dt begin, string freq, int periods)
    {
      var retVal = new List<CouponPeriod>();
      for (int i = 0; i < periods; i++)
      {
        var date = Dt.Add(begin, Tenor.Parse(freq));
        retVal.Add(new CouponPeriod(date, 0.025));
        begin = date;
      }
      return retVal;
    }

  }
}
