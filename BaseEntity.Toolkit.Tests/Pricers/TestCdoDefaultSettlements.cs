//
// Copyright (c)    2018. All rights reserved.
//

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  using NUnit.Framework;

  [TestFixture]
  public class TestCdoDefaultSettlements
  {
    // By design we want exact match, otherwise something wrong in the algorithm
    const double tolerance = 1E-15;

    public TestCdoDefaultSettlements()
    {
      var dc = discountCurve = new DiscountCurve(asOf, 0.1);
      curves = new[]{
        CreateCreditCurve(dc),
        CreateCreditCurve(dc)
      };
      cdo = new SyntheticCDO(effective, maturity, Currency.USD,
        DayCount.Actual360, Frequency.Quarterly, BDConvention.Modified,
        Calendar.NYB, 500, 0.0, 0.0, 1.0);
      baseValue = GetPricer().ProtectionPv()*
        (curves.Length - 1)/curves.Length;
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(1)]
    public void RecoverySettleVsPricerSettle(int days)
    {
      Dt recoverySettle = asOf + 1 + days;
      var defaultPv = baseValue - GetPricer(recoverySettle).ProtectionPv();
      var expect = days < 0 ? 0.0 : ((1 - recovery)*notional/curves.Length*
        discountCurve.DiscountFactor(asOf, recoverySettle));
      Assert.AreEqual(expect, defaultPv, tolerance*notional);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(5)]
    [TestCase(10)]
    public void RecoverySettledAfterMaturity(int days)
    {
      var recoverySettle = Dt.Add(maturity, days);
      var defaultPv = baseValue - GetPricer(recoverySettle).ProtectionPv();
      var expect = (1 - recovery)*notional/curves.Length*
        discountCurve.DiscountFactor(asOf, recoverySettle);
      Assert.AreEqual(expect, defaultPv, tolerance*notional);
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(1)]
    public void PricingAfterMaturity(int days)
    {
      Dt pricingDate = maturity + days,
        recoverySettle = pricingDate + 5;
      var defaultPv = - GetPricer(recoverySettle, pricingDate).ProtectionPv();
      var expect = (1 - recovery)*notional/curves.Length*
        discountCurve.DiscountFactor(pricingDate, recoverySettle);
      Assert.AreEqual(expect, defaultPv, tolerance*notional);
    }

    private SyntheticCDOPricer GetPricer(
      Dt recoverySettle = new Dt(), 
      Dt pricingDate = new Dt())
    {
      var sc = curves;
      if (!recoverySettle.IsEmpty())
      {
        sc =  Array.ConvertAll(sc, c=>c);
        sc[0] = CreateCreditCurve(discountCurve, recoverySettle);
      }
      if (pricingDate.IsEmpty())
      {
        pricingDate = asOf;
      }
      return BasketPricerFactory.CDOPricerSemiAnalytic(cdo,
        Dt.Empty, pricingDate, pricingDate + 1, discountCurve,
        null, sc, null, new Copula(CopulaType.Gauss),
        new SingleFactorCorrelation(new[] {"1", "2"}, 0.0),
        0, TimeUnit.None, 0, 0, notional, false, false);
    }

    private SurvivalCurve CreateCreditCurve(
      DiscountCurve dc, Dt recoverySettle = new Dt())
    {
      return SurvivalCurve.FitCDSQuotes(
        recoverySettle.IsEmpty() ? "regular" : "defaulted", asOf, asOf + 1,
        Currency.USD, "", CDSQuoteType.ParSpread, 500,
        SurvivalCurveParameters.GetDefaultParameters(), dc,
        new[] {"5Y"}, null, new[] {500.0}, new[] {recovery}, 0,
        recoverySettle.IsEmpty() ? null : new[] {defaultDate, recoverySettle},
        null, 0, false);
    }

    private Dt defaultDate = new Dt(20151210), asOf = new Dt(20151214),
      effective = new Dt(20101220), maturity = new Dt(20151220);

    private double recovery = 0.4, notional = 1000000;
    private DiscountCurve discountCurve;
    private SurvivalCurve[] curves;
    private SyntheticCDO cdo;
    private double baseValue;
  }
}
