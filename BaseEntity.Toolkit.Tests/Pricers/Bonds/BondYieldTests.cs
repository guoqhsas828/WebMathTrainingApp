//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers.Bonds
{
  using NUnit.Framework;
  using static CallableBondFactory;

  [TestFixture]
  public class BondYieldTests
  {

    [Test]
    public void YieldToMaturity()
    {
      var yield = Pricer.CalculateYields(null).First().Yield;
      Assert.AreEqual(0.044537, yield);
    }

    [Test]
    public void YieldWithSinglePartialCoupon()
    {
      var maturity = new Dt(20180524);
      var bond = new Bond(new Dt(20160224), maturity,
          Currency.USD, BondType.USCorp, 0.0315, DayCount.Thirty360,
          CycleRule.TwentyFourth, Frequency.SemiAnnual,
          BDConvention.Modified, Calendar.NYB)
        {FirstCoupon = new Dt(20160824)};
      bond.CashflowFlag |= CashflowFlag.StubAtEnd
        | CashflowFlag.RespectAllUserDates;

      var schedule = bond.Schedule;
      Dt settle = new Dt(20180515);
      var ai = BondModelUtil.AccruedInterest(settle, bond, bond.AccrualDayCount,
        bond.Coupon, bond.CumDiv(settle, settle), true);
      var yield = BondModelUtil.YieldToMaturity(settle, bond,
        new Dt(20180224), schedule.GetNextCouponDate(settle), maturity,
        schedule.NumberOfCouponsRemaining(settle), 1.001, ai,
        1.0, bond.Coupon, 0.3, false, true);
      Assert.AreEqual(-0.0084, yield, 5E-5);
    }

    [Test]
    public void YieldToCalls()
    {
      var periods = new[]
      {
        Call(new Dt(20160315), 1.03),
        Call(new Dt(20170316), 1.026250),
        Call(new Dt(20180315), 1.013130),
        Call(new Dt(20190315), 1.0),
        Call(new Dt(20210315), 1.0),
      };

      // Yield to calls
      using (new CheckStates(
        true, new IPricer[] {Pricer}))
      {
        var expects = new[]
        {
          new {Date = new Dt(20170316), Yield = 0.0143112 /* BB is 0.01432117!! */},
          new {Date = new Dt(20180315), Yield = 0.036521},
          new {Date = new Dt(20190315), Yield = 0.037671},
          new {Date = new Dt(20210315), Yield = 0.04453704},
        };

        var results = Pricer.CalculateYields(periods).ToArray();
        Assert.AreEqual(expects.Length, results.Length, "# of yields");
        Assert.AreEqual(Array.ConvertAll(expects, e => e.Date),
          Array.ConvertAll(results, r => r.Date), "Dates");
        Assert.That(Array.ConvertAll(results, r => r.Yield),
          Is.EqualTo(Array.ConvertAll(expects, e => e.Yield)).Within(1E-6),
          "Yields");
      }

      // The worst yield 01
      var pricer = AddCalls(Pricer, periods);
      using (new CheckStates(
        true, new IPricer[] {pricer}))
      {
        var result = pricer.YieldToWorst01();
        Assert.AreEqual(periods[1].StartDate, result.Date, "Worst yield date");
        Assert.AreEqual(1.0121437E-5, result.Yield01, 1E-11, "Worst yield 01");
      }

      // Amortizing bond
      var amortizations = new[]
      {
        RemainingNotional(0.875, new Dt(20160915)),
        RemainingNotional(0.750, new Dt(20170915)),
        RemainingNotional(0.50, new Dt(20180915)),
      };
      pricer = AddAmortization(pricer, amortizations);
      using (new CheckStates(
        true, new IPricer[] {pricer}))
      {
        var expects = new[]
        {
          // The first yield should be the same as in the non-amortization case
          new {Date = new Dt(20170316), Yield = 0.0143112},
          // The others are different but similar
          new {Date = new Dt(20180315), Yield = 0.0336414},
          new {Date = new Dt(20190315), Yield = 0.0347280},
          new {Date = new Dt(20210315), Yield = 0.0413570},
        };

        var results = pricer.CalculateYields(periods).ToArray();
        Assert.AreEqual(expects.Length, results.Length,
          "# of yields with amortization");
        Assert.AreEqual(Array.ConvertAll(expects, e => e.Date),
          Array.ConvertAll(results, r => r.Date), "Dates");
        Assert.That(Array.ConvertAll(results, r => r.Yield),
          Is.EqualTo(Array.ConvertAll(expects, e => e.Yield)).Within(1E-6),
          "Yields with amortization");
      }
    }

    #region Create bond pricer

    // This is an example from Bloomberg
    internal static BondPricer CreateBondPricer()
    {
      Dt effective = new Dt(20140210),
        maturity = new Dt(20210315),
        firstCoupon = new Dt(20140315),
        asOf = new Dt(20170213),
        settle = new Dt(20170216);
      double coupon = 0.0525, quotedPrice = 1.029375;
      var bond = new Bond(
        effective, maturity, Currency.USD, BondType.USCorp, coupon,
        DayCount.Thirty360, CycleRule.None, Frequency.SemiAnnual,
        BDConvention.Following, Calendar.NYB)
      { FirstCoupon = firstCoupon };
      return new BondPricer(bond, asOf, settle)
      {
        DiscountCurve = new DiscountCurve(asOf, 0),
        MarketQuote = quotedPrice,
        QuotingConvention = QuotingConvention.FlatPrice
      };
    }

    private BondPricer Pricer { get; } = CreateBondPricer();

    #endregion
  }
}
