// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using BaseEntity.Shared;
using BaseEntity.Shared.Dynamic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;

namespace BaseEntity.Toolkit.Tests.Calibrators.Volatilities
{
  [TestFixture]
  public class ImpliedVolatilityTermStructTests
  {
    #region Data and constructor

    private readonly Dt asOf = Dt.Today();
    private readonly VolatilityCurve _vcurve;

    private static readonly string[] _volatilityTenors =
    {
      "1W", "2W", "3W",
      "1M", "2M", "3M", "4M", "5M", "6M", "9M",
      "1Y", "2Y", "5Y", "10Y", "15Y", "20Y", "30Y", "50Y"
    };

    private static readonly double[] _volatilities =
    {
      0.8, 0.75, 0.7,
      0.65, 0.64, 0.63, 0.62, 0.61, 0.6, 0.58,
      0.56, 0.53, 0.48, 0.43, 0.42, 0.41, 0.40, 0.40
    };

    public ImpliedVolatilityTermStructTests()
    {
      var curve = new VolatilityCurve(asOf)
      {
        Name = "SingleVolatilityCurve"
      };
      for (int i = 0; i < _volatilityTenors.Length; ++i)
      {
        curve.Add(Dt.Add(asOf, _volatilityTenors[i]), _volatilities[i]);
      }
      _vcurve = curve;
    }

    #endregion

    #region Setup and tear down

    // These tests require EnableForwardVolatilityTermStructure to be true.
    // We enable it before any test run and restore the settings afterward.

    private IDisposable _enabled;

    [OneTimeSetUp]
    public void EnableVolatilityTermStruct()
    {
      _enabled = new ConfigItems
      {
        {"CcrPricer.EnableForwardVolatilityTermStructure", true}
      }.Update();
    }

    [OneTimeTearDown]
    public void RestoreConfigurationSettings()
    {
      if (_enabled == null) return;
      _enabled.Dispose();
      _enabled = null;
    }

    #endregion

    #region Test Helpers

    private static Func<Dt, double> GetForwardVolatilityCalculator(
      VolatilityCurve vc, Dt expiry)
    {
      var asOf = vc.AsOf;
      return start =>
      {
        var t1 = Dt.RelativeTime(asOf, start);
        var v1 = vc.Interpolate(start);
        var t2 = Dt.RelativeTime(asOf, expiry);
        var v2 = vc.Interpolate(expiry);
        return Math.Sqrt((v2*v2*t2 - v1*v1*t1)/(t2 - t1));
      };
    }

    private void ValidateCcrForwardVolatilities(IPricer optionPricer,
      Func<Dt, double> getForwardVolatility = null,
      double pvTolerance = 1E-14, double volTolerance = 2E-16)
    {
      var pv0 = optionPricer.Pv();
      var option = (IOptionProduct) optionPricer.Product.CloneObjectGraph();
      var pricer = CcrPricer.Get(optionPricer);
      var pv1 = pricer.FastPv(asOf); // make sure it is initialized.
      Assert.That(pv1, Is.EqualTo(pv0).Within(pvTolerance));

      var underlier = pricer.GetValue<IUnderlier>("Underlier");
      var vc = _vcurve;
      var expiry = option.Expiration;
      if (getForwardVolatility == null)
        getForwardVolatility = GetForwardVolatilityCalculator(vc, expiry);
      double lastVol = 0.0;
      for (int i = 0, n = vc.Count; i < n; ++i)
      {
        var settle = vc.GetDt(i);
        // Do we get the expected volatility?
        var fwdvol = underlier.Vol(settle);
        var expect = settle >= expiry ? lastVol
          : (lastVol = getForwardVolatility(settle));
        Assert.That(fwdvol,Is.EqualTo(expect).Within(volTolerance));
      }

      // Make sure the pricer is 
    }

    #endregion

    #region Tests

    [TestCase(CDXOptionModelType.Black)]
    [TestCase(CDXOptionModelType.BlackPrice)]
    public void TestCdxOption(CDXOptionModelType model)
    {
      const string underlyingName = "CDX.NA.IG.23-V1.5Y";
      const double quote = 100 / 10000.0, strike = 100.0 / 10000;
      var expiry = Dt.Add(asOf, "1Y");
      var defn = underlyingName.LookUpCreditIndexDefinition();
      var discountCurve = new DiscountCurve(asOf, 0.03);
      var cdxo = new CDXOption(asOf, defn.Currency, defn.CDX, expiry,
        PayerReceiver.Payer, OptionStyle.European, strike,
        defn.QuotingConvention == QuotingConvention.CreditConventionalUpfront);
      var pricer = cdxo.CreatePricer(asOf, asOf, discountCurve,
        new MarketQuote(quote, QuotingConvention.CreditConventionalSpread),
        asOf + 1, defn.RecoveryRate, defn.EntityCount, null, model, null,
        CalibratedVolatilitySurface.FromCurve(_vcurve), 100.0, null);
      // We use pvTolerance = 1E-6, because CDX pricer to spread conversion
      // has built-in tolerance 1e-6.
      ValidateCcrForwardVolatilities(pricer,
        pvTolerance: 1E-6, volTolerance: 5E-13);
    }

    [Test]
    public void TestFxOption()
    {
      var optionMaturity = Dt.Add(asOf, "10Y");
      double price = 88, strike = 100;
      var discountCurve = new DiscountCurve(asOf, 0.03);
      var curve = new FxCurve(
        new FxRate(asOf, asOf, Currency.USD, Currency.JPY, price),
        null, discountCurve, discountCurve, "USDJPY_Curve");
      var option = new FxOption()
      {
        Maturity = optionMaturity,
        Strike = strike,
      };
      ValidateCcrForwardVolatilities(new FxOptionVanillaPricer(option,
        asOf, asOf, discountCurve, discountCurve, curve,
        CalibratedVolatilitySurface.FromCurve(_vcurve)));
    }

    [Test]
    public void TestStockOption()
    {
      var optionMaturity = Dt.Add(asOf, "10Y");
      double price = 88, strike = 100;
      var curve = new StockCurve(asOf, price,
        new DiscountCurve(asOf, 0.03), 0.0, null);
      var option = new StockOption(optionMaturity,
        OptionType.Call, OptionStyle.European, strike);
      ValidateCcrForwardVolatilities(new StockOptionPricer(option,
        asOf, asOf, curve, curve.DiscountCurve, _vcurve));
    }

    [Test]
    public void TestCommodityOption()
    {
      var optionMaturity = Dt.Add(asOf, "10Y");
      double price = 88, strike = 100;
      var curve = new CommodityCurve(asOf, price,
        new DiscountCurve(asOf, 0.03), 0.0, null);
      var option = new CommodityOption(optionMaturity,
        OptionType.Call, OptionStyle.European, strike);
      ValidateCcrForwardVolatilities(new CommodityOptionPricer(option,
        asOf, asOf, curve, curve.DiscountCurve, _vcurve, 1.0));
    }

    [Test]
    public void TestCommodityForwardOption()
    {
      var optionMaturity = Dt.Add(asOf, "10Y");
      double price = 88, strike = 100;
      var curve = new CommodityCurve(asOf, price,
        new DiscountCurve(asOf, 0.03), 0.0, null);
      var option = new CommodityForwardOption(optionMaturity, optionMaturity,
        optionMaturity, OptionType.Call, OptionStyle.European, strike);
      ValidateCcrForwardVolatilities(new CommodityForwardOptionBlackPricer(
        option, asOf, asOf, curve.DiscountCurve, curve, _vcurve, 1.0));
    }

    [Test]
    public void TestCommodityFutureOption()
    {
      var optionMaturity = Dt.Add(asOf, "10Y");
      double price = 88, strike = 100;
      var curve = new CommodityCurve(asOf, price,
        new DiscountCurve(asOf, 0.03), 0.0, null);
      var option = new CommodityFutureOption(optionMaturity,
        1, optionMaturity, OptionType.Call, OptionStyle.European, strike);
      ValidateCcrForwardVolatilities(new CommodityFutureOptionBlackPricer(
        option, asOf, asOf, curve.DiscountCurve, curve, _vcurve, 1.0));
    }

    [Test]
    public void TestSwaption()
    {
      var index = StandardReferenceIndices.Create("USDLIBOR_3M");
      var ccy = index.Currency;
      var freq = index.IndexTenor.ToFrequency();
      var effective = Dt.AddDays(asOf, index.SettlementDays, index.Calendar);
      var swapStart = Dt.Add(effective, "10Y");
      var swapMaturity = Dt.Add(effective, "20Y");
      var optionMaturity = swapStart;
      double coupon = 0.03, strike = 0.03;
      var swap = new Swap(swapStart, swapMaturity, coupon, ccy, index.DayCount,
        freq, index.Roll, index.Calendar, freq, index);
      var swpn = new Swaption(asOf, optionMaturity, ccy, swap.PayerLeg,
        swap.ReceiverLeg, 2, PayerReceiver.Payer, OptionStyle.European, strike);
      ValidateCcrForwardVolatilities(new SwaptionBlackPricer(swpn, asOf, asOf,
        new DiscountCurve(asOf, 0.025), new DiscountCurve(asOf, 0.03),
        null, _vcurve));
    }

    [Test]
    public void TestSwaptionBgm()
    {
      var index = StandardReferenceIndices.Create("USDLIBOR_3M");
      var ccy = index.Currency;
      var freq = index.IndexTenor.ToFrequency();
      var effective = Dt.AddDays(asOf, index.SettlementDays, index.Calendar);
      var swapStart = Dt.Add(effective, "5Y");
      var swapMaturity = Dt.Add(effective, "10Y");
      var optionMaturity = swapStart;
      double coupon = 0.03, strike = 0.03;
      var swap = new Swap(swapStart, swapMaturity, coupon, ccy, index.DayCount,
        freq, index.Roll, index.Calendar, freq, index);
      var swpn = new Swaption(asOf, optionMaturity, ccy, swap.PayerLeg,
        swap.ReceiverLeg, 2, PayerReceiver.Payer, OptionStyle.European, strike);
      var discountCurve = new DiscountCurve(asOf, 0.025);
      var vo = CascadingFitSwapVolatilities(discountCurve);
      ValidateCcrForwardVolatilities(new SwaptionBlackPricer(
        swpn, asOf, asOf, discountCurve, discountCurve, null, vo),
        ((IVolatilityCalculatorProvider) vo).GetCalculator(swpn));
    }

    #endregion

    #region Swaption volatilities

    private BgmForwardVolatilitySurface CascadingFitSwapVolatilities(
      DiscountCurve discountCurve)
    {
      string[] tenors =
      {
        "1Y", "2Y", "3Y", "4Y", "5Y", "6Y", "7Y", "8Y", "9Y", "10Y"
      };
      var expiries = tenors;
      double[,] data =
      {
        {40.00, 37.30, 33.20, 30.60, 28.90, 27.30, 26.30, 25.60, 25.20, 24.80},
        {31.20, 27.90, 25.90, 24.70, 23.80, 23.10, 22.50, 22.10, 21.90, 21.80},
        {25.70, 23.50, 22.10, 21.40, 21.00, 20.50, 20.10, 19.90, 19.70, 19.70},
        {22.00, 20.60, 19.70, 19.40, 19.20, 18.90, 18.50, 18.30, 18.10, 18.10},
        {20.50, 19.50, 19.00, 18.40, 17.70, 17.40, 17.20, 17.00, 17.00, 16.90},
        {19.40, 18.40, 18.85, 16.75, 16.65, 16.40, 16.25, 16.15, 16.15, 16.10},
        {18.00, 17.00, 16.60, 16.00, 15.60, 15.40, 15.30, 15.30, 15.30, 15.30},
        {17.10, 16.50, 15.60, 15.57, 15.20, 14.88, 14.59, 14.33, 14.09, 13.88},
        {16.20, 15.70, 15.20, 14.90, 14.55, 14.25, 13.97, 13.72, 13.49, 13.29},
        {15.75, 15.00, 14.50, 14.35, 14.02, 13.72, 13.46, 13.22, 13.00, 12.81}
      };
      var par = new BgmCalibrationParameters
      {
        CalibrationMethod = VolatilityBootstrapMethod.Cascading,
        Tolerance = 0.0001,
        PsiUpperBound = 1.0,
        PsiLowerBound = 0.0,
        PhiUpperBound = 1.002,
        PhiLowerBound = 0.9
      };
      var correlations = BgmCorrelation.CreateBgmCorrelation(
        BgmCorrelationType.PerfectCorrelation, expiries.Length, new double[0, 0]);
      return BgmForwardVolatilitySurface.Create(
        asOf, par, discountCurve, expiries, tenors, CycleRule.None,
        BDConvention.None, Calendar.None, correlations,
        data, DistributionType.LogNormal);
      //var fwdVols = ((BgmCalibratedVolatilities)retVal.CalibratedVolatilities)
      //  .GetForwardVolatilities();
      //var resetDates = retVal.CalibratedVolatilities.ResetDates;
      //Assert.AreEqual("RateCount", fwdVols.GetLength(0), resetDates.Length);
      //var curves = ((BgmCalibratedVolatilities)retVal.CalibratedVolatilities)
      //  .BuildForwardVolatilityCurves();
      //Assert.AreEqual("CurveCount", curves.Length, resetDates.Length);
    }

    #endregion
  }
}
