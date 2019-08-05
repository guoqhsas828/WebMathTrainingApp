//
// Copyright (c)    2018. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util.Configuration;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestBillPricer
  {
    #region Tests

    /// <summary>
    /// Price
    /// </summary>
    [NUnit.Framework.TestCase(20010528, 20010827, 0.0556, DayCount.Actual360, ExpectedResult = 0.98594556)]
    [NUnit.Framework.TestCase(20000911, 20010827, 0.0587, DayCount.Actual360, ExpectedResult = 0.94293056)]
    [NUnit.Framework.TestCase(20020314, 20020411, 0.0176, DayCount.Actual360, ExpectedResult = 0.99863111)]
    [NUnit.Framework.TestCase(20020314, 20020613, 0.0179, DayCount.Actual360, ExpectedResult = 0.99547528)]
    [NUnit.Framework.TestCase(20020314, 20020912, 0.0198, DayCount.Actual360, ExpectedResult = 0.98999000)]
    [NUnit.Framework.TestCase(20010911, 20020307, 0.0312, DayCount.Actual360, ExpectedResult = 0.98466000)]
    public double Price(int settle, int maturity, double discountRate, DayCount dayCount)
    {
      return Math.Round(Pricer(settle, maturity, discountRate, dayCount).Price(), 8);
    }

    /// <summary>
    /// Discount as a percentage of notional
    /// </summary>
    [NUnit.Framework.TestCase(20010528, 20010827, 0.0556, DayCount.Actual360, ExpectedResult = 0.01405444)]
    [NUnit.Framework.TestCase(20000911, 20010827, 0.0587, DayCount.Actual360, ExpectedResult = 0.05706944)]
    [NUnit.Framework.TestCase(20020314, 20020411, 0.0176, DayCount.Actual360, ExpectedResult = 0.00136889)]
    [NUnit.Framework.TestCase(20020314, 20020613, 0.0179, DayCount.Actual360, ExpectedResult = 0.00452472)]
    [NUnit.Framework.TestCase(20020314, 20020912, 0.0198, DayCount.Actual360, ExpectedResult = 0.01001000)]
    [NUnit.Framework.TestCase(20010911, 20020307, 0.0312, DayCount.Actual360, ExpectedResult = 0.01534000)]
    public double Discount(int settle, int maturity, double discountRate, DayCount dayCount)
    {
      return Math.Round(Pricer(settle, maturity, discountRate, dayCount).Discount(), 8);
    }

    /// <summary>
    /// Money market (CD) equivalent yield 
    /// </summary>
    [NUnit.Framework.TestCase(20010528, 20010827, 0.0556, DayCount.Actual360, ExpectedResult = 0.05639257)]
    [NUnit.Framework.TestCase(20000911, 20010827, 0.0587, DayCount.Actual360, ExpectedResult = 0.06225273)]
    [NUnit.Framework.TestCase(20020314, 20020411, 0.0176, DayCount.Actual360, ExpectedResult = 0.01762413)]
    [NUnit.Framework.TestCase(20020314, 20020613, 0.0179, DayCount.Actual360, ExpectedResult = 0.01798136)]
    [NUnit.Framework.TestCase(20020314, 20020912, 0.0198, DayCount.Actual360, ExpectedResult = 0.02000020)]
    [NUnit.Framework.TestCase(20010911, 20020307, 0.0312, DayCount.Actual360, ExpectedResult = 0.03168606)]
    public double MoneyMarketYield(int settle, int maturity, double discountRate, DayCount dayCount)
    {
      return Math.Round(Pricer(settle, maturity, discountRate, dayCount).MoneyMarketYield(DayCount.Actual360), 8);
    }

    /// <summary>
    /// Bond equivalent yield (Treasury convention)
    /// </summary>
    [NUnit.Framework.TestCase(20010528, 20010827, 0.0556, DayCount.Actual360, ExpectedResult = 0.05717580)]
    [NUnit.Framework.TestCase(20000911, 20010827, 0.0587, DayCount.Actual360, ExpectedResult = 0.06219183)]
    [NUnit.Framework.TestCase(20020314, 20020411, 0.0176, DayCount.Actual360, ExpectedResult = 0.01786890)]
    [NUnit.Framework.TestCase(20020314, 20020613, 0.0179, DayCount.Actual360, ExpectedResult = 0.01823110)]
    [NUnit.Framework.TestCase(20020314, 20020912, 0.0198, DayCount.Actual360, ExpectedResult = 0.02027798)]
    [NUnit.Framework.TestCase(20010911, 20020307, 0.0312, DayCount.Actual360, ExpectedResult = 0.03212615)]
    public double BondEquivalentYield(int settle, int maturity, double discountRate, DayCount dayCount)
    {
      return Math.Round(Pricer(settle, maturity, discountRate, dayCount).BondEquivalentYield(), 8);
    }

    /// <summary>
    /// Bond (basis) yield
    /// </summary>
    [NUnit.Framework.TestCase(20010528, 20010827, 0.0556, DayCount.Actual360, ExpectedResult = 0.05758556)]
    [NUnit.Framework.TestCase(20000911, 20010827, 0.0587, DayCount.Actual360, ExpectedResult = 0.06222954)]
    [NUnit.Framework.TestCase(20020314, 20020411, 0.0176, DayCount.Actual360, ExpectedResult = 0.01793662)]
    [NUnit.Framework.TestCase(20020314, 20020613, 0.0179, DayCount.Actual360, ExpectedResult = 0.01827276)]
    [NUnit.Framework.TestCase(20020314, 20020912, 0.0198, DayCount.Actual360, ExpectedResult = 0.02027826)]
    [NUnit.Framework.TestCase(20010911, 20020307, 0.0312, DayCount.Actual360, ExpectedResult = 0.03213389)]
    public double BondYield(int settle, int maturity, double discountRate, DayCount dayCount)
    {
      return Math.Round(Pricer(settle, maturity, discountRate, dayCount).BondYield(DayCount.Actual365Fixed, Frequency.SemiAnnual), 8);
    }

    /// <summary>
    /// Change in value for a 1bp drop in discount rate
    /// </summary>
    [NUnit.Framework.TestCase(20010528, 20010827, 0.0556, DayCount.Actual360, ExpectedResult = 0.2528)]
    [NUnit.Framework.TestCase(20000911, 20010827, 0.0587, DayCount.Actual360, ExpectedResult = 0.9722)]
    [NUnit.Framework.TestCase(20020314, 20020411, 0.0176, DayCount.Actual360, ExpectedResult = 0.0778)]
    [NUnit.Framework.TestCase(20020314, 20020613, 0.0179, DayCount.Actual360, ExpectedResult = 0.2528)]
    [NUnit.Framework.TestCase(20020314, 20020912, 0.0198, DayCount.Actual360, ExpectedResult = 0.5056)]
    [NUnit.Framework.TestCase(20010911, 20020307, 0.0312, DayCount.Actual360, ExpectedResult = 0.4917)]
    public double Pv01(int settle, int maturity, double discountRate, DayCount dayCount)
    {
      return Math.Round(Pricer(settle, maturity, discountRate, dayCount).Pv01(), 4);
    }

    /// <summary>
    /// Change in value for a 1bp drop in SABB Yield
    /// </summary>
    public double BondPv01(int settle, int maturity, double discountRate, DayCount dayCount)
    {
      return Math.Round(Pricer(settle, maturity, discountRate, dayCount).BondPv01(DayCount.ActualActualBond, Frequency.SemiAnnual), 8);
    }

    /// <summary>
    /// Calculates duration as the weighted average time to cash flows
    /// </summary>
    [NUnit.Framework.TestCase(20010528, 20010827, 0.0556, DayCount.Actual360, ExpectedResult = 0.24931507)]
    [NUnit.Framework.TestCase(20000911, 20010827, 0.0587, DayCount.Actual360, ExpectedResult = 0.95890411)]
    [NUnit.Framework.TestCase(20020314, 20020411, 0.0176, DayCount.Actual360, ExpectedResult = 0.07671233)]
    [NUnit.Framework.TestCase(20020314, 20020613, 0.0179, DayCount.Actual360, ExpectedResult = 0.24931507)]
    [NUnit.Framework.TestCase(20020314, 20020912, 0.0198, DayCount.Actual360, ExpectedResult = 0.49863014)]
    [NUnit.Framework.TestCase(20010911, 20020307, 0.0312, DayCount.Actual360, ExpectedResult = 0.48493151)]
    public double Duration(int settle, int maturity, double discountRate, DayCount dayCount)
    {
      return Math.Round(Pricer(settle, maturity, discountRate, dayCount).Duration(), 8);
    }

    /// <summary>
    /// Calculates modified duration as the percentage price change for a 1bp drop in yield
    /// </summary>
    [NUnit.Framework.TestCase(20010528, 20010827, 0.0556, DayCount.Actual360, ExpectedResult = 0.25638107)]
    [NUnit.Framework.TestCase(20000911, 20010827, 0.0587, DayCount.Actual360, ExpectedResult = 1.03106450)]
    [NUnit.Framework.TestCase(20020314, 20020411, 0.0176, DayCount.Actual360, ExpectedResult = 0.07788439)]
    [NUnit.Framework.TestCase(20020314, 20020613, 0.0179, DayCount.Actual360, ExpectedResult = 0.25392673)]
    [NUnit.Framework.TestCase(20020314, 20020912, 0.0198, DayCount.Actual360, ExpectedResult = 0.51066734)]
    [NUnit.Framework.TestCase(20010911, 20020307, 0.0312, DayCount.Actual360, ExpectedResult = 0.49932633)]
    public double ModDuration(int settle, int maturity, double discountRate, DayCount dayCount)
    {
      return Math.Round(Pricer(settle, maturity, discountRate, dayCount).ModDuration(), 8);
    }

    /// <summary>
    /// Calculates modified duration as the percentage price change for a 1bp drop in yield
    /// </summary>
    public double BondModDuration(int settle, int maturity, double discountRate, DayCount dayCount)
    {
      return Math.Round(Pricer(settle, maturity, discountRate, dayCount).BondModDuration(DayCount.ActualActualBond, Frequency.SemiAnnual), 8);
    }
 
    /// <summary>
    /// Number of days from settlement to maturity
    /// </summary>
    [NUnit.Framework.TestCase(20010528, 20010827, 0.0556, DayCount.Actual360, ExpectedResult = 91)]
    [NUnit.Framework.TestCase(20000911, 20010827, 0.0587, DayCount.Actual360, ExpectedResult = 350)]
    [NUnit.Framework.TestCase(20020314, 20020411, 0.0176, DayCount.Actual360, ExpectedResult = 28)]
    [NUnit.Framework.TestCase(20020314, 20020613, 0.0179, DayCount.Actual360, ExpectedResult = 91)]
    [NUnit.Framework.TestCase(20020314, 20020912, 0.0198, DayCount.Actual360, ExpectedResult = 182)]
    [NUnit.Framework.TestCase(20010911, 20020307, 0.0312, DayCount.Actual360, ExpectedResult = 177)]
    public int DaysToMaturity(int settle, int maturity, double discountRate, DayCount dayCount)
    {
      return Pricer(settle, maturity, discountRate, dayCount).DaysToMaturity();
    }

    #endregion Tests

    #region Utils

    private BillPricer Pricer(int settle, int maturity, double discountRate, DayCount dayCount)
    {
      ToolkitConfigurator.Init();
      var bill = new Bill(new Dt(settle), new Dt(maturity), Currency.None);
      var pricer = new BillPricer(bill, new Dt(settle), new Dt(settle), QuotingConvention.DiscountRate, discountRate, dayCount, null, null, null, 1.0);
      return pricer;
    }

    #endregion Utils
  }
}
