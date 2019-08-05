//
// Copyright (c)    2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models.Simulations;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{

  [TestFixture]
  public class TestBondOption : ToolkitTestBase
  {
    #region Set up and tear down

    private IDisposable _tempConfig;

    [OneTimeSetUp]
    public void SetUp()
    {
      _tempConfig = new ConfigItems
      {
        {"Simulations.EnableCorrectionForCurveTenorChange", true}
      }.Update();
    }

    [OneTimeTearDown]
    public void DearDown()
    {
      if (_tempConfig == null) return;
      _tempConfig.Dispose();
      _tempConfig = null;
    }

    #endregion

    #region Forward price validation

    public enum CreditRisk { Riskless, Defaultable}

    [TestCase(-3, -2, CreditRisk.Riskless)]  // expiry before issue
    [TestCase(-3, -2, CreditRisk.Defaultable)]
    [TestCase(-2, 0, CreditRisk.Riskless)]  //expiry on issue
    [TestCase(-2,0, CreditRisk.Defaultable)]
    [TestCase(-1, 1, CreditRisk.Riskless)]  //SettleBeforeAndExpiryAfterIssue
    [TestCase(-1, 1, CreditRisk.Defaultable)]
    [TestCase(-1, 6, CreditRisk.Riskless)]  //SettleBeforeIssueExpiryOnFirstCoupon
    [TestCase(-1, 6, CreditRisk.Defaultable)]
    [TestCase(-1, 9, CreditRisk.Riskless)]  //SettleBeforeIssueExpiryAfterFirstCoupon
    [TestCase(-1, 9, CreditRisk.Defaultable)]
    [TestCase(-1, 11, CreditRisk.Riskless)]  //SettleBeforeIssueExpiryOnSecondCoupo
    [TestCase(-1, 11, CreditRisk.Defaultable)]
    [TestCase(1, 3, CreditRisk.Riskless)]  //SettleAfterIssueExpiryBeforeFirstCoupon
    [TestCase(1, 3, CreditRisk.Defaultable)]
    [TestCase(2, 6, CreditRisk.Riskless)]  //SettleAfterIssueExpiryOnFirstCoupo
    [TestCase(2, 6, CreditRisk.Defaultable)]
    [TestCase(6, 9, CreditRisk.Riskless)]  //SettleAfterIssueExpiryAfterFirstCoupon
    [TestCase(6, 9, CreditRisk.Defaultable)]
    [TestCase(9, 12, CreditRisk.Riskless)]  //SettleAfterIssueExpiryOnSecondCoupon
    [TestCase(9, 12, CreditRisk.Defaultable)]
    public void Tests(int asOfMonths, int expiryMonth, CreditRisk risk)
    {
      ValidateForwardPrice(asOfMonths, expiryMonth, risk);
    }

    private static void ValidateForwardPrice(
      int asOfMonths, int expiryMonths, CreditRisk risk)
    {
      bool withDefault = risk == CreditRisk.Defaultable;
      var bond = GetUnderlyingBond(Currency.USD);
      //bond.PaymentLagRule = new PayLagRule(0, false);
      var expiry = Dt.AddMonths(bond.Effective, expiryMonths, CycleRule.None);
      var asOf = Dt.AddMonths(bond.Effective, asOfMonths, CycleRule.None);

      var option = new BondOption(bond, expiry,
        OptionType.Call, OptionStyle.European, 1);
      var pricer = new BondOptionBlackPricer(option,
        asOf, asOf, double.NaN, QuotingConvention.FlatPrice,
        new DiscountCurve(asOf, 0.02),
        new FlatVolatility {Volatility = 0.0}, 1.0);
      if (withDefault)
      {
        pricer.SurvivalCurve = new SurvivalCurve(asOf, 0.05)
        {
          Calibrator = new SurvivalFitCalibrator(asOf)
          {
            RecoveryCurve = new RecoveryCurve(asOf, 0.4),
          },
        };
      }
      pricer.BondQuote = CalculateFlatPrice(pricer.BondPricer, asOf);
      pricer.Reset();

      // Check the forward price
      var fwdPrice = CalculateFlatPrice(pricer.BondPricer, expiry);
      var actual = pricer.BondForwardModelValue();
      Assert.AreEqual(fwdPrice, actual, 1E-15, "Forward Price");

      option.Strike = 0.75*fwdPrice;
      pricer.Reset();

      var intrinsic = CalculateIntrinsicValue(pricer, asOf);
      var optionPv = pricer.ProductPv();
      Assert.AreEqual(intrinsic, optionPv, 1E-15, "Intrinsic Value");

      var ccrPricer = CcrPricer.Get(pricer);
      var ccrFastPv = ccrPricer.FastPv(asOf);
      Assert.AreEqual(intrinsic, ccrFastPv, 1E-15, "CCR Fast Value");

      // Try move date forward
      Dt date = Dt.AddMonth(asOf, 1, false),
        cashSettleDate = Dt.Roll(expiry, bond.BDConvention, bond.Calendar);
      while (date <= expiry)
      {
        if (date >= cashSettleDate) date -= 1;
        intrinsic = CalculateIntrinsicValue(pricer, date);
        ccrFastPv = ccrPricer.FastPv(date);
        Assert.AreEqual(intrinsic, ccrFastPv, 1E-15, "CCR Fast Value at " + date);
        date = Dt.AddMonth(date, 1, false);
      }
    }

    private static double CalculateIntrinsicValue(
      BondOptionBlackPricer pricer, Dt settle)
    {
      var opt = pricer.BondOption;
      var strike = opt.Strike;
      var fwdPrice = pricer.BondForwardModelValue();
      Dt expiry = opt.Expiration;
      var sc = pricer.SurvivalCurve;
      return (sc == null ? 1.0 : sc.SurvivalProb(settle, expiry))
        *pricer.DiscountCurve.DiscountFactor(settle, expiry)
        *(fwdPrice - strike);
    }

    private static double CalculateFlatPrice(BondPricer pricer, Dt settle)
    {
      return CalculatePrice(pricer.Cashflow, settle,
        pricer.DiscountCurve, pricer.SurvivalCurve)
        - pricer.AccruedInterest(settle, settle);
    }

    private static double CalculatePrice(Cashflow cf,
      Dt settle, DiscountCurve dc, SurvivalCurve sc)
    {
      double settleDf = dc.DiscountFactor(settle),
        settleSurvival = SurvivalProb(sc, settle),
        prevDf = settleDf, prevSurvival = settleSurvival;
      Dt effective = cf.Effective;
      if (effective > settle)
      {
        prevDf = dc.DiscountFactor(effective);
        prevSurvival = SurvivalProb(sc, effective);
      }

      double pv = 0;
      for (int i = 0, n = cf.Count; i < n; ++i)
      {
        var end = cf.GetEndDt(i);
        if (end <= settle) continue;

        var df = dc.DiscountFactor(end);
        var survival = SurvivalProb(sc, end);
        var protection = 0.5*(prevDf + df)*
          (prevSurvival - survival)*cf.GetDefaultAmount(i);
        pv += protection;

        Dt payDt = cf.GetDt(i);
        var feeDf = payDt == end ? df : dc.DiscountFactor(payDt);
        var accrual = feeDf*survival*(cf.GetAmount(i) + cf.GetAccrued(i));
        pv += accrual;

        // prepare for the next loop
        prevDf = df;
        prevSurvival = survival;
      }
      return pv/settleDf/settleSurvival;
    }

    private static double SurvivalProb(SurvivalCurve sc, Dt date)
    {
      return sc == null ? 1.0 : sc.SurvivalProb(date);
    }

    #endregion

    [Test]
    public void DefaultableBond()
    {
      var bond = GetUnderlyingBond(Currency.GBP);
      Dt asOf = new Dt(20160209),
        expiry = Dt.Roll(new Dt(20160520), bond.BDConvention, bond.Calendar);
      var timeToExpiry = (expiry - asOf) / 365.25;
      var discountCurve = new DiscountCurve(asOf, 0.02);
      var survivalCurve = new SurvivalCurve(asOf, 0.05);
      var currentPrice = FlatPv(new BondPricer(bond, asOf, asOf, discountCurve,
        survivalCurve, 0, TimeUnit.None, -1));
      var fwdPrice = FlatPv(new BondPricer(bond, expiry, expiry, discountCurve,
        survivalCurve, 0, TimeUnit.None, -1));

      var strike = fwdPrice * 0.8;
      var option = new BondOption(bond, expiry, OptionType.Call,
        OptionStyle.European, strike);
      var optionPricer1 = new BondOptionBlackPricer(option, asOf, asOf,
        currentPrice, QuotingConvention.FlatPrice, discountCurve,
        new FlatVolatility { Volatility = 0.0 }, 1.0)
      { SurvivalCurve = survivalCurve };

      var optionPv1 = optionPricer1.ProductPv();
      var expect1 = survivalCurve.SurvivalProb(asOf, expiry)
        *discountCurve.DiscountFactor(asOf, expiry)*(fwdPrice - strike);
      optionPv1.IsExpected(To.Match(expect1).Within(1E-15));

      var ccrPricer1 = CcrPricer.Get(optionPricer1);
      ccrPricer1.FastPv(asOf).IsExpected(To.Match(expect1).Within(1E-15));

      double sigma = 0.2;
      var optionPricer2 = new BondOptionBlackPricer(option, asOf, asOf,
        currentPrice, QuotingConvention.FlatPrice, discountCurve,
        new FlatVolatility { Volatility = sigma }, 1.0)
      { SurvivalCurve = survivalCurve };
      var optionPv2 = optionPricer2.ProductPv();
      double sigmaSqrtT = sigma * Math.Sqrt(timeToExpiry);
      double d1 = Math.Log(fwdPrice / strike) / sigmaSqrtT + 0.5 * sigmaSqrtT;
      double d2 = d1 - sigmaSqrtT;
      var expect2 = survivalCurve.SurvivalProb(asOf, expiry) * discountCurve.DiscountFactor(asOf, expiry)
          * (fwdPrice * Normal.cumulative(d1, 0.0, 1.0) - strike * Normal.cumulative(d2, 0.0, 1.0));
      optionPv2.IsExpected(To.Match(expect2).Within(1E-15));

      var ccrPricer2 = CcrPricer.Get(optionPricer2);
      ccrPricer2.FastPv(asOf).IsExpected(To.Match(expect2).Within(1E-15));
    }

    private static double FlatPv(BondPricer pricer)
    {
      return pricer.ProductPv() - pricer.Accrued();
    }

    private static Bond GetUnderlyingBond(Currency ccy)
    {
      if (ccy == Currency.GBP)
      {
        Dt effective = new Dt(20150320), maturity = new Dt(20250907);
        BondType type = BondType.UKGilt;
        DayCount dayCount = DayCount.ActualActualBond;
        Calendar calendar = Calendar.LNB;
        Frequency freq = Frequency.SemiAnnual;
        BDConvention roll = BDConvention.Following;
        double coupon = 0.02;
        return new Bond(effective, maturity, ccy, type,
          coupon, dayCount, CycleRule.None, freq, roll, calendar)
        {
          Description = "2% Treasury Gilt 2025"
        };
      }
      if (ccy == Currency.USD)
      {
        Dt effective = new Dt(20110115), maturity = new Dt(20160115);
        BondType type = BondType.USCorp;
        DayCount dayCount = DayCount.ActualActualBond;
        Calendar calendar = Calendar.LNB;
        Frequency freq = Frequency.SemiAnnual;
        BDConvention roll = BDConvention.Following;
        double coupon = 0.02;
        return new Bond(effective, maturity, ccy, type,
          coupon, dayCount, CycleRule.None, freq, roll, calendar)
        {
          Description = "US Corp"
        };
      }

      throw new ApplicationException("No bond for currency " + ccy);
    }

    #region Consistency before and after conform

    [Test]
    public static void ConsistencyBeforeAfterConform()
    {
      Dt effective = new Dt(20240715), maturity = new Dt(20340115);
      const Currency ccy = Currency.USD;
      const BondType type = BondType.USCorp;
      const DayCount dayCount = DayCount.ActualActualBond;
      const Frequency freq = Frequency.SemiAnnual;
      const BDConvention roll = BDConvention.None;
      const double coupon = 0.02;
      var calendar = Calendar.LNB;
      var bond = new Bond(effective, maturity, ccy, type,
        coupon, dayCount, CycleRule.None, freq, roll, calendar)
      {
        Description = "US Corp"
      };

      var expiry = new Dt(20240117);
      var asOf = _asOf;

      var option = new BondOption(bond, expiry,
        OptionType.Call, OptionStyle.European, 1);
      var pricer = new BondOptionBlackPricer(option,
        asOf, asOf, double.NaN, QuotingConvention.FlatPrice,
        GetDiscountCurve(asOf, ccy, _discountFactors),
        new FlatVolatility {Volatility = 0.28}, 1.0)
      {
        SurvivalCurve = GetSurvivalCurve(asOf, _suvivalProbabilities),
      };
      pricer.BondQuote = 0.83;

      pricer.Reset();
      var pv = pricer.ProductPv();
      var ccrPricer = CcrPricer.Get(pricer);
      var ccrFastPv = ccrPricer.FastPv(asOf);
      Assert.AreEqual(pv, ccrFastPv, 1E-15, "CCR PV before Conform");

      var tenors = new[]
      {
        "1M", "3M", "6M", "9M", "1Y",
        "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "12Y", "15Y",
      }
        .Select(Tenor.Parse).ToArray();
      var env = MarketEnvironment.Create(asOf,
        tenors.Select(s => Dt.Add(asOf, s)).ToArray(),
        Tenor.Empty, ccy, ccy,
        new VolatilityCollection(null),
        new FactorLoadingCollection(null, null),
        new[] {pricer.DiscountCurve}, null, null,
        new[] {pricer.SurvivalCurve});
      env.Conform();

      pricer.Reset();
      var pv2 = pricer.ProductPv();
      var ccrFastPv2 = ccrPricer.FastPv(asOf);
      Assert.AreEqual(pv2, ccrFastPv2, 1E-15, "CCR PV after Conform");
      Assert.AreEqual(pv, pv2, 1E-15, "PV before vs after Conform");
      return;
    }

    private static SurvivalCurve GetSurvivalCurve(
      Dt asOf, IEnumerable<DateAndValue<double>> points)
    {
      var curve = new SurvivalCurve(asOf)
      {
        Calibrator = new SurvivalFitCalibrator(asOf)
        {
          RecoveryCurve = new RecoveryCurve(asOf, 0.4),
        },
      };
      foreach (var pt in points)
      {
        curve.Add(pt.Date, pt.Value);
      }
      return curve;
    }

    private static DiscountCurve GetDiscountCurve(
      Dt asOf, Currency ccy,
      IEnumerable<DateAndValue<double>> points)
    {
      var curve = new DiscountCurve(asOf) {Ccy = ccy};
      foreach (var pt in points)
      {
        curve.Add(pt.Date, pt.Value);
      }
      return curve;
    }

    private static Dt _D(string s)
    {
      return s.ParseDt();
    }

    private static DateAndValue<double> _P(Dt date, double value)
    {
      return DateAndValue.Create(date, value);
    }

    private static Dt _asOf = _D("20-Dec-2012");

    private static DateAndValue<double>[] _suvivalProbabilities =
    {
      _P(_D("21-Dec-2013"), 0.982509476057659),
      _P(_D("23-Dec-2014"), 0.964072948246106),
      _P(_D("22-Dec-2015"), 0.944171537812688),
      _P(_D("21-Dec-2016"), 0.923805614963069),
      _P(_D("21-Dec-2017"), 0.904186102716737),
      _P(_D("21-Dec-2019"), 0.875362918549691),
      _P(_D("21-Dec-2022"), 0.836905542351146),
    };

    private static DateAndValue<double>[] _discountFactors =
    {
      _P(_D("21-Dec-2014"), 0.995812313873591),
      _P(_D("21-Dec-2015"), 0.989123961710202),
      _P(_D("21-Dec-2016"), 0.97498581464568),
      _P(_D("21-Dec-2017"), 0.951656448778235),
      _P(_D("21-Dec-2018"), 0.92604118638301),
      _P(_D("21-Dec-2019"), 0.897284310543164),
      _P(_D("21-Dec-2020"), 0.867715000871695),
      _P(_D("21-Dec-2021"), 0.838740830901407),
      _P(_D("21-Dec-2022"), 0.810029777629386),
      _P(_D("21-Dec-2024"), 0.753213024473141),
      _P(_D("21-Dec-2027"), 0.677001509494129),
      _P(_D("21-Dec-2032"), 0.568096733432018),
      _P(_D("21-Dec-2037"), 0.470819144685919),
      _P(_D("21-Dec-2042"), 0.398554945238764),
    };

    #endregion
  }
}

