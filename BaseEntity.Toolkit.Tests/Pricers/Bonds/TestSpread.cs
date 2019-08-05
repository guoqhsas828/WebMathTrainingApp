//
// Copyright (c)    2018. All rights reserved.
//

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers.Bonds
{
  [TestFixture]
  public class TestSpread : ToolkitTestBase
  {
    const double epsilon = 0.000005;

    [Test, Smoke]
    public void ParBondPricing()
    {
      Dt asOfDate = new Dt(25, 8, 2001);
      Dt maturity = new Dt(25, 8, 2003);
      Bond bond = new Bond(asOfDate, maturity, Currency.USD, BondType.USGovt,
                           0.05, DayCount.Actual365Fixed, CycleRule.None, Frequency.SemiAnnual, BDConvention.Modified, Calendar.NYB);
      DiscountCurve discountCurve = new DiscountCurve(asOfDate);
      discountCurve.Frequency = Frequency.SemiAnnual;
      discountCurve.Add(maturity, Math.Pow(1.025, -4));
      string dump = discountCurve.ToString();
      SurvivalCurve survivalCurve = new SurvivalCurve(asOfDate);
      survivalCurve.Add(maturity, 1.0);

      BondPricer pricer = new BondPricer(bond);
      pricer.AsOf = asOfDate;
      pricer.Settle = asOfDate;
      pricer.DiscountCurve = discountCurve;
      pricer.SurvivalCurve = survivalCurve;
      pricer.StepSize = 2;
      pricer.StepUnit = TimeUnit.Weeks;
      pricer.RecoveryCurve = new RecoveryCurve(asOfDate,0.4);

      // Actual results
      double price = pricer.Pv();

      // Comparison
      Assert.AreEqual( 1.0, price, epsilon );
    }

    [Test, Smoke]
    public void ParBondPricingWithSpread()
    {
      double expect = CalculatePrice(0.05, 0.0);
      double actual = CalculatePrice(0.0, 0.05);

      // Comparison
      Assert.AreEqual( expect, actual, epsilon );
    }

    private double CalculatePrice(double rate, double spread)
    {
      Dt asOfDate = new Dt(25, 8, 2001);
      Dt maturity = new Dt(25, 8, 2003);
      Bond bond = new Bond(asOfDate, maturity, Currency.USD, BondType.USGovt,
                           0.05, DayCount.Actual365Fixed, CycleRule.None, Frequency.SemiAnnual, BDConvention.Modified, Calendar.NYB);
      DiscountCurve discountCurve = new DiscountCurve(asOfDate);
      discountCurve.Frequency = Frequency.SemiAnnual;
      /// discountCurve.Add(maturity, Math.Pow(1.025, -4));
      discountCurve.Add(maturity, RateCalc.PriceFromRate(
        rate, asOfDate, maturity, discountCurve.DayCount, discountCurve.Frequency));
      discountCurve.Spread = spread;
      string dump = discountCurve.ToString();
      SurvivalCurve survivalCurve = new SurvivalCurve(asOfDate);
      survivalCurve.Add(maturity, 1.0);

      BondPricer pricer = new BondPricer(bond);
      pricer.AsOf = asOfDate;
      pricer.Settle = asOfDate;
      pricer.DiscountCurve = discountCurve;
      pricer.SurvivalCurve = survivalCurve;
      pricer.StepSize = 2;
      pricer.StepUnit = TimeUnit.Weeks;
      pricer.RecoveryCurve = new RecoveryCurve(asOfDate, 0.4);

      // Actual results
      double price = pricer.Pv();

      return price;
    }

  }
}
