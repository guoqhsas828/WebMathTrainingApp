//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Tests.Calibrators;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  using NUnit.Framework;

  [TestFixture]
  public class TestBondrYieldToWorstModel
  {
    [Test]
    public void ClearCallScheduel()
    {
      var cSched = new CallSchedule(new Dt(20190107), 1.02);
      var bond = GetBondWithSchedule(_asOf, _maturity, _coupon, cSched);
      var cloned = (Bond) bond.Clone();
      cloned.CallSchedule.Clear();
      Assert.AreEqual(bond.CallSchedule.Count, 1);
      Assert.AreEqual(cloned.CallSchedule.Count, 0);
    }

    [TestCase(20390507)]
    [TestCase(20190107)]
    public void TestWorkBond(int calldate)
    {
      Dt callDate = new Dt(calldate);
      var cSched = new CallSchedule(callDate, 1.02);
      var bond = GetBondWithSchedule(_asOf, _maturity, _coupon, cSched);
      var yield = 0.044;
      var workBond = BondPricer.GetWorkBond(bond, _asOf, _asOf, yield, 
        QuotingConvention.Yield);

      var flatPrice = 1.20;
      var workBond1 = BondPricer.GetWorkBond(bond, _asOf, _asOf, flatPrice, 
        QuotingConvention.FlatPrice);

      if (callDate >= _maturity)
      {
        Assert.That(workBond, Is.SameAs(bond));
        Assert.That(workBond1, Is.SameAs(bond));
        return;
      }

      Assert.AreEqual(callDate, workBond.Maturity);
      Assert.AreEqual(cSched.CallPrice, workBond.FinalRedemptionValue);
      Assert.AreEqual(callDate, workBond1.Maturity);
      Assert.AreEqual(cSched.CallPrice, workBond1.FinalRedemptionValue);
      Assert.AreEqual(_maturity, bond.Maturity);
      Assert.AreEqual(1.0, bond.FinalRedemptionValue);
    }

    [Test]
    public void TestZeroCallPrice()
    {
      var cSched = new CallSchedule(new Dt(20190107), 0.0);
      var bond = GetBondWithSchedule(_asOf, _maturity, _coupon, cSched);
      var yield = 0.044;
      var workBond = BondPricer.GetWorkBond(bond, _asOf, _asOf, yield, QuotingConvention.Yield);

      Assert.AreEqual(bond.Notional, workBond.FinalRedemptionValue);
    }

    [TestCase(0.044, QuotingConvention.Yield)]
    [TestCase(1.20, QuotingConvention.FlatPrice)]
    public void TestWorkBondCashFlow(double quote, QuotingConvention quoteConvention)
    {
      var cSched = new CallSchedule(new Dt(20190107), 1.10);
      var bond = GetBondWithSchedule(_asOf, _maturity, _coupon, cSched);
      var workBond = BondPricer.GetWorkBond(bond, _asOf, _asOf, quote, quoteConvention);
      var cf = workBond.GenerateCashflow(null, _asOf, _asOf, new DiscountCurve(_asOf, 0.02),
        null, null, 0.0, Dt.Empty, Dt.Empty, true);

      var last = cf.Count - 1;
      var lastAmount = cf.GetAmount(last);
      Assert.AreEqual(workBond.FinalRedemptionValue, lastAmount);
    }

    [TestCase(0.04250, QuotingConvention.Yield)]
    [TestCase(101.0, QuotingConvention.FlatPrice)]
    [TestCase(99.0, QuotingConvention.FlatPrice)]
    public void TestYieldToWorstBondModelPv01(double quote, QuotingConvention quoteConvention)
    {
      var tiny = 0.003;
      var callPrice = quoteConvention == QuotingConvention.Yield ? 1.1 : 1.0;
      quote = quoteConvention == QuotingConvention.Yield ? quote : quote/100.0;
      var cSched = new CallSchedule(new Dt(20190107), callPrice);

      //Test two work bond maturities don't change
      var shiftQuote = quote + tiny;
      TestTwoPvs(shiftQuote, quoteConvention, null);

      //Test two work bond maturities both change
      shiftQuote = quote - tiny;
      TestTwoPvs(shiftQuote, quoteConvention, cSched);

      //Test one changes, and the other doesn't(For yield). The Pv01 is not the 
      // continuous function of yield.In the discrete point, use the 
      // diffs of full prices besides the discrete point.
      if (quoteConvention == QuotingConvention.Yield)
      {
        var bond = GetBondWithSchedule(_asOf, _maturity, _coupon, cSched);
        var p1 = GetBondPricer(bond, _asOf, _asOf, quote, quoteConvention,
          CallableBondPricingMethod.YieldToWorst);
        var p2 = GetBondPricer(bond, _asOf, _asOf, quote + 0.0001, quoteConvention,
          CallableBondPricingMethod.YieldToWorst);
        var diff = p1.FullPrice() - p2.FullPrice();
        var pv01 = p1.PV01();
        Assert.AreEqual(diff, pv01, 1E-14);
        return;
      }

      var bond1= GetBondWithSchedule(_asOf, _maturity, _coupon, cSched);
      var workBond = BondPricer.GetWorkBond(bond1, _asOf, _asOf, quote, quoteConvention);
      var p3 = GetBondPricer(bond1, _asOf, _asOf, quote, quoteConvention,
          CallableBondPricingMethod.YieldToWorst);
      var p4 = GetBondPricer(bond1, _asOf, _asOf, quote, quoteConvention,
          CallableBondPricingMethod.None);
      var p5 = GetBondPricer(workBond, _asOf, _asOf, quote, quoteConvention,
        CallableBondPricingMethod.None);

      if(bond1.Maturity == workBond.Maturity)
        Assert.AreEqual(p3.PV01(), p4.PV01(), 1E-14);
      else
        Assert.AreEqual(p3.PV01(), p5.PV01(), 1E-14);
    }

    private void TestTwoPvs(double quote, QuotingConvention quoteConvention, CallSchedule cSched)
    {
      var bond = GetBondWithSchedule(_asOf, _maturity, _coupon, cSched);

      var bondPricerYtw = GetBondPricer(bond, _asOf, _asOf, quote, quoteConvention,
        CallableBondPricingMethod.YieldToWorst);
      if (cSched != null)
        bond = BondPricer.GetWorkBond(bond, _asOf, _asOf, quote, quoteConvention);
      var bondPricer = GetBondPricer(bond, _asOf, _asOf, quote, quoteConvention,
        CallableBondPricingMethod.None);

      var pv011 = bondPricerYtw.PV01();
      var pv012 = bondPricer.PV01();

      Assert.AreEqual(pv011, pv012, 1E-14);
    }

    [TestCase(0.01, QuotingConvention.Yield)]
    [TestCase(0.03, QuotingConvention.Yield)]
    [TestCase(0.07, QuotingConvention.Yield)]
    [TestCase(0.09, QuotingConvention.Yield)]
    [TestCase(0.99, QuotingConvention.FlatPrice)]
    [TestCase(0.995, QuotingConvention.FlatPrice)]
    [TestCase(1.012, QuotingConvention.FlatPrice)]
    [TestCase(1.022, QuotingConvention.FlatPrice)]
    public void TestYieldToWorstBondSwitchMaturityWithYield(double quote, 
      QuotingConvention quoteConvention)
    {
      var cSched = new CallSchedule(new Dt(20190107), 1.0);
      var bond = GetBondWithSchedule(_asOf, _maturity, _coupon, cSched);

      var workingBond = BondPricer.GetWorkBond(bond, _asOf, _asOf, quote, 
        quoteConvention);

      if (quoteConvention == QuotingConvention.Yield)
      {
        if (quote > bond.Coupon)
          Assert.AreEqual(bond.Maturity, workingBond.Maturity);
        else
          Assert.AreEqual(cSched.CallDate, workingBond.Maturity);
      }
      else
      {
        if(quote < cSched.CallPrice)
          Assert.AreEqual(bond.Maturity, workingBond.Maturity);
        else
          Assert.AreEqual(cSched.CallDate, workingBond.Maturity);
      }
    }

    [TestCase(0.04, QuotingConvention.Yield)]
    [TestCase(0.992, QuotingConvention.FlatPrice)]
    public void TestYieldToWorstBondScenarioShiftMaturity(double quote, 
      QuotingConvention quoteConvention)
    {
      double bumpSize = 200.0/10000;
      var cSched = new CallSchedule(new Dt(20190107), 1.0);
      var bondS = GetBondWithSchedule(_asOf, _maturity, _coupon, cSched);

      //Shift the maturity since the working bond maturity should be call date
      var bond = GetBondWithSchedule(_asOf, cSched.CallDate, _coupon, null);
      var pricer1 = new BondPricer(bond, _asOf, _asOf)
      {
        MarketQuote = quote,
        QuotingConvention = quoteConvention
      };
      var fullPrice1 = pricer1.FullPrice();

      //When we bump the yield, the maturity should shift back to the original one
      var pricer2 = new BondPricer(bondS, _asOf, _asOf)
      {
        MarketQuote = quote + bumpSize,
        QuotingConvention = quoteConvention
      };
      var fullPrice2 = pricer2.FullPrice();

      //Scenario calculation shifts the maturity
      var bondPricer = GetBondPricer(bondS, _asOf, _asOf, quote, quoteConvention,
        CallableBondPricingMethod.YieldToWorst);
      var yieldShift = new ScenarioValueShift(ScenarioShiftType.Absolute, bumpSize);
      var yieldShiftScenario = new ScenarioShiftPricerTerms(new[] { "MarketQuote" },
        new[] { yieldShift });
      var scenario = Scenarios.CalcScenario(bondPricer, "FullPrice", 
        new[] { yieldShiftScenario }, false, true);

      //Test
      var diff = fullPrice2 - fullPrice1;
      Assert.AreEqual(diff, scenario, 1E-14);
    }

    [TestCase(0.04, QuotingConvention.Yield)]
    [TestCase(0.992, QuotingConvention.FlatPrice)]
    public void TestOptionPrice(double quote, QuotingConvention quoteConvention)
    {
      var cSched = new CallSchedule(new Dt(20190107), 1.0);
      var bond = GetBondWithSchedule(_asOf, cSched.CallDate, _coupon, null);
      var pricer1 = new BondPricer(bond, _asOf, _asOf)
      {
        MarketQuote = quote,
        QuotingConvention = quoteConvention
      };
      var fullPrice1 = pricer1.FullPrice();

      var bondS = GetBondWithSchedule(_asOf, _maturity, _coupon, cSched);
      var pricer2 = new BondPricer(bondS, _asOf, _asOf)
      {
        MarketQuote = quote,
        QuotingConvention = quoteConvention
      };
      var fullPrice2 = pricer2.FullPrice();

      var bondPricer = GetBondPricer(bondS, _asOf, _asOf, quote, quoteConvention,
        CallableBondPricingMethod.YieldToWorst);

      var diff = fullPrice2 - fullPrice1;
      var optionPrice = RoundBondPrice(bondPricer.OptionPrice());

      Assert.AreEqual(diff, optionPrice, 1E-14);
    }

    private static double RoundBondPrice(double price)
    {
      return Math.Floor(price*Math.Pow(10, 6) + 0.5)/Math.Pow(10, 6);
    }

    private BondPricer GetBondPricer(Bond bond, Dt asOf, Dt settle, double quote,
       QuotingConvention quoteConvention, CallableBondPricingMethod callMethod)
    {
      var discountCurve = new RateCurveBuilder().CreateDiscountCurve(asOf);
      var bondPricer = new BondPricer(bond, asOf, settle, discountCurve, null, 0,
        TimeUnit.None, 0.4, callMethod)
      {
        MarketQuote = quote,
        QuotingConvention = quoteConvention
      };

      bondPricer.Validate();
      return bondPricer;
    }


    private Bond GetBondWithSchedule(Dt asOf, Dt maturity, double coupon,
      CallSchedule call)
    {
      var bond = new Bond(asOf, maturity,Currency.USD, BondType.USGovt, coupon,
        DayCount.ActualActualBond, CycleRule.None,
        Frequency.SemiAnnual, BDConvention.Following, Calendar.NYB);

      if (call != null)
      {
        var callDate = call.CallDate;
        bond.CallSchedule.Add(new CallPeriod(callDate, callDate, call.CallPrice,
          0, OptionStyle.Bermudan, 0));
      }
      bond.Validate();
      return bond;
    }

    #region CallSchedule
    private class CallSchedule
    {
      public CallSchedule(Dt callDate, double callPrice)
      {
        CallDate = callDate;
        CallPrice = callPrice;
      }
      public Dt CallDate { get; set; }

      public double CallPrice { get; set; }
    }

    #endregion CallSchedule

    #region Data

    private readonly Dt _asOf =new Dt(20160119), 
      _maturity = new Dt(20390107);

    private readonly double _coupon = 0.05;

    #endregion Data
  }
}
