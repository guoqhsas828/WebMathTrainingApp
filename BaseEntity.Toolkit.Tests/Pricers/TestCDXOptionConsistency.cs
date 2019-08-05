//
// Copyright (c)    2018. All rights reserved.
//

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Calibrators;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Verify accuracy of various option values/greeks based on numerical integration
  /// </summary>
  [TestFixture]
  public class TestCDXOptionConsistency : ToolkitTestBase
  {
    #region Constants
    const int dataCount = 100;
    const double dataDelta = 0.0025;

    const int quadPoints = 100;
    double[] points_ = null;
    double[] weights_ = null;

    const double bumpSize = 0.1;

    const double interestRate = 0.04;
    const double notional = 875000000;

    double[] strikes_ = null;
    double volatility_ = 0.35;
    #endregion //Constants

    #region Properties
    /// <summary>
    ///   Deal premium
    /// </summary>
    public double DealPremium { get; set; } = 280;

    /// <summary>
    ///  Market quote: spread or price
    /// </summary>
    public double MarketQuote { get; set; } = 274.5;

    /// <summary>
    ///   Whether quote in price
    /// </summary>
    public bool QuoteIsPrice { get; set; } = false;

    /// <summary>
    ///   Array of strikes
    /// </summary>
    public double[] Strikes
    {
      get
      {
        if (strikes_ == null)
          strikes_ = new double[] { 275.0 };
        return strikes_;
      }
      set { strikes_ = value; }
    }

    /// <summary>
    ///   Whether strike is price
    /// </summary>
    public bool StrikeIsPrice { get; set; } = false;

    /// <summary>
    ///   Expiry of the option
    /// </summary>
    public int ExpiryDate { get; set; } = 0;

    #endregion Properties

    #region Pricers
    // delegate type
    delegate double Calculator(OptionType type, double s, double sigma);

    private DiscountCurve CreateIRCurve()
    {
      Dt asOf = PricingDate != 0 ? new Dt(PricingDate) : new Dt(20061218);
      Dt settle = SettleDate != 0 ? new Dt(SettleDate) : asOf;

      string[] mmTenors = new string[] {
        "1 D","1 W","2 W","1 M","2 M","3 M","4 M","5 M","6 M","9 M","1 Y" };
      double[] mmRates = new double[] {
        0.0536875,0.053225,0.05325,0.0533438,0.0542,0.0548063,0.0551688,
        0.05555,0.0558938,0.0564938,0.0569313 };
      Dt[] mmMaturities = new Dt[mmTenors.Length];
      for (int i = 0; i < mmTenors.Length; i++)
        mmMaturities[i] = Dt.Add(asOf, mmTenors[i]);
      DayCount mmDayCount = DayCount.Actual360;

      string[] swapTenors = new string[] {
        "2 Yr","3 Yr","4 Yr","5 Yr","6 Yr","7 Yr","8 Yr","9 Yr","10 Yr" };
      double[] swapRates = new double[] { 0.056245,0.05616,0.0563,0.05648,
        0.05665,0.05683,0.056995,0.05713,0.057295 };
      Dt[] swapMaturities = new Dt[swapTenors.Length];
      for (int i = 0; i < swapTenors.Length; i++)
        swapMaturities[i] = Dt.Add(asOf, swapTenors[i]);
      DayCount swapDayCount = DayCount.Actual360;

      DiscountBootstrapCalibrator calibrator = new DiscountBootstrapCalibrator(asOf, settle);
      calibrator.SwapInterp = InterpFactory.FromMethod(InterpMethod.Cubic, ExtrapMethod.Const);

      DiscountCurve curve = new DiscountCurve(calibrator);
      curve.Interp = InterpFactory.FromMethod(InterpMethod.Weighted, ExtrapMethod.Smooth);
      curve.Ccy = Currency.USD;
      curve.Category = "None";
      curve.Name = "USDLIBOR";

      // Add MM rates
      for (int i = 0; i < mmTenors.Length; i++)
        if (mmRates[i] > 0.0)
          curve.AddMoneyMarket(mmTenors[i], mmMaturities[i], mmRates[i], mmDayCount);

      // Add swap rates
      for (int i = 0; i < swapTenors.Length; i++)
        if (swapRates[i] > 0.0)
          curve.AddSwap(swapTenors[i], swapMaturities[i], swapRates[i], swapDayCount,
                         Frequency.SemiAnnual, BDConvention.None, Calendar.None);

      curve.Fit();

      return curve;
    }

    private CDXOption CreateCDXOption(OptionType type, double strike, bool strikeIsPrice)
    {
      Dt asOf = PricingDate != 0 ? new Dt(PricingDate) : new Dt(20061218);
      Dt settle = SettleDate != 0 ? new Dt(SettleDate) : asOf;
      Dt effective = EffectiveDate != 0 ? new Dt(EffectiveDate) : new Dt(20060921);
      Dt maturity = MaturityDate != 0 ? new Dt(EffectiveDate) : Dt.CDSMaturity(effective, "5Y");
      Dt expiry = ExpiryDate != 0 ? new Dt(ExpiryDate) : Dt.CDSMaturity(settle, "6M");
      CDX cdx = new CDX(
        effective, maturity, Currency.USD, DealPremium / 10000.0, DayCount.Actual360,
        Frequency.Quarterly, BDConvention.Following, Calendar.NYB);
      CDXOption cdxo = new CDXOption(settle, Currency.USD, cdx, expiry,
        type == OptionType.Put ? PayerReceiver.Payer : PayerReceiver.Receiver,
        OptionStyle.European,
        strikeIsPrice ? strike : strike / 10000.0, strikeIsPrice);
      return cdxo;
    }

    private CDXOptionPricer CreateCDXOptionPricer(
      CDXOption cdxo, DiscountCurve discountCurve, bool modified)
    {
      return (CDXOptionPricer)CreateCDXOptionPricer(cdxo, discountCurve, modified
        ? CDXOptionModelType.ModifiedBlack : CDXOptionModelType.Black,
        false);
    }

    private ICreditIndexOptionPricer CreateCDXOptionPricer(
      CDXOption cdxo,
      DiscountCurve discountCurve,
      CDXOptionModelType cdxOptionModel,
      bool newPricer)
    {
      Dt asOf = PricingDate != 0 ? new Dt(PricingDate) : new Dt(20061218);
      Dt settle = SettleDate != 0 ? new Dt(SettleDate) : Dt.Add(asOf, 1);

      if (newPricer)
      {
        var quote = QuoteIsPrice
          ? new MarketQuote(MarketQuote/100, QuotingConvention.FlatPrice)
          : new MarketQuote(MarketQuote/10000, QuotingConvention.CreditSpread);
        return cdxo.CreatePricer(asOf, settle, discountCurve,
          quote, Dt.Empty, 0.4, 0, null, cdxOptionModel, null,
          CalibratedVolatilitySurface.FromFlatVolatility(asOf, volatility_),
          notional, null);
      }

      double spread = MarketQuote;
      if (QuoteIsPrice)
      {
        CDX cdx = cdxo.CDX;
        CDXPricer cdxPricer = new CDXPricer(cdx, cdxo.Expiration, cdxo.Expiration,
          discountCurve, 0.01);
        spread = cdxPricer.PriceToSpread(spread/100);
      }
      else
        spread /= 10000.0;

      double sigma = volatility_;

      CDXOptionPricer pricer = null;
      if (cdxOptionModel == CDXOptionModelType.ModifiedBlack)
      {
        pricer = new CDXOptionPricerModifiedBlack(cdxo, asOf, settle, discountCurve,
          null, spread, sigma);
        ((CDXOptionPricerModifiedBlack) pricer).Center = 0.0;
      }
      else
        pricer = new CDXOptionPricerBlack(cdxo, asOf, settle, discountCurve, null,
          spread, sigma);
      //pricer.Spread = marketQuote;
      pricer.Notional = notional;
      return pricer;
    }

    #endregion // Pricers

    #region PutCallParity
    void DoTestPutCallParity(double strike, CDXOptionModelType model, bool newModel)
    {
      CDXOption cdxo = CreateCDXOption(OptionType.Call, strike, StrikeIsPrice);
      DiscountCurve discountCurve = CreateIRCurve();
      var pricer = CreateCDXOptionPricer(cdxo, discountCurve, model, newModel);
      var df = pricer.DiscountCurve.DiscountFactor(pricer.AsOf,pricer.CDXOption.Expiration);
      double EV = df*(pricer.ExistingLoss*pricer.Notional +
        pricer.AtTheMoneyForwardValue*pricer.CurrentNotional);
      //double KV = pricer is CDXOptionPricerModifiedBlack ?
      //  (pricer.StrikeValue() * notional) :
      //  ((1 - pricer.ExercisePrice()/100) * notional);
      double KV = df*pricer.ForwardStrikeValue*pricer.EffectiveNotional;
      double CV = pricer.MarketValue();

      pricer.CDXOption.Type = OptionType.Put;
      double PV = pricer.MarketValue();

      //pricer.CDXOption.Type = OptionType.Call;
      //pricer.CDXOption.Strike = 1E-8;
      //double EV0 = pricer.MarketValue();

      // The following are not real tests, just to record numbers in results
      Assert.AreEqual(EV, EV);
      Assert.AreEqual(KV, KV);
      Assert.AreEqual(CV, CV);
      Assert.AreEqual(PV, PV);

      // This is the real test of Call Put Parity
      // The tolerance has to be 1E-8
      Assert.AreEqual(EV - KV, PV - CV, 1E-10*Math.Abs(EV));
    }

    [TestCase(false, Category = "Smoke")]
    [TestCase(true, Category = "Smoke")]
    [TestCase(CDXOptionModelType.Black, Category = "Smoke")]
    [TestCase(CDXOptionModelType.ModifiedBlack, Category = "Smoke")]
    [TestCase(CDXOptionModelType.FullSpread, Category = "Smoke")]
    [TestCase(CDXOptionModelType.BlackPrice, Category = "Smoke")]
    public void TestPutCallParity(object modelSpec)
    {
      CDXOptionModelType model;
      var newModel = true;
      if (modelSpec.Equals(false))
      {
        model = CDXOptionModelType.Black;
        newModel = false;
      }
      else if (modelSpec.Equals(true))
      {
        model = CDXOptionModelType.ModifiedBlack;
        newModel = false;
      }
      else
      {
        model = (CDXOptionModelType) modelSpec;
      }
      foreach (double strike in Strikes)
        DoTestPutCallParity(strike, model, newModel);
    }
    #endregion // CallPutParity

    #region Price Spread Conversion
    /// <summary>
    ///   Test that strikes on spreads and on quivalent prices yield
    ///   the same results.
    /// </summary>
    /// <param name="label">Label of the is test</param>
    /// <param name="strike">Strike value to use</param>
    /// <param name="type">Type of the option</param>
    /// <param name="modified">Models to use</param>
    /// <param name="roundCheck">Perform round robin test of spread-price conversion</param>
    private void TestPriceSpreadEquivalence(string label,
      double strike,  OptionType type, bool modified, bool roundCheck)
    {
      const double tol = 1E-7;
      DiscountCurve discountCurve = CreateIRCurve();
      CDXOption cdxoSpread, cdxoPrice;
      if (StrikeIsPrice)
      {
        CDXOption cdxo = cdxoPrice = CreateCDXOption(type, strike, true);
        CDX cdx = cdxo.CDX;
        CDXPricer cdxPricer = new CDXPricer(cdx, cdxo.Expiration, cdxo.Expiration, discountCurve, 0.01);
        double spread = cdxPricer.PriceToSpread(strike) * 10000;
        if (roundCheck)
        {
          double price = cdxPricer.SpreadToPrice(spread / 10000);
          AssertEqual("Price Strike", strike, price, tol);
        }
        cdxoSpread = CreateCDXOption(type, spread, false);
      }
      else
      {
        CDXOption cdxo = cdxoSpread = CreateCDXOption(type, strike, false);
        CDX cdx = cdxo.CDX;
        CDXPricer cdxPricer = new CDXPricer(cdx, cdxo.Expiration, cdxo.Expiration, discountCurve, 0.01);
        double price = cdxPricer.SpreadToPrice(strike / 10000);
        if (roundCheck)
        {
          double spread = cdxPricer.PriceToSpread(price) * 10000;
          AssertEqual("Spread Strike", strike, spread, tol);
        }
        cdxoPrice = CreateCDXOption(type, price, true);
      }

      CDXOptionPricer p0 = CreateCDXOptionPricer(cdxoSpread, discountCurve, modified);
      CDXOptionPricer p1 = CreateCDXOptionPricer(cdxoPrice, discountCurve, modified);
      AssertEqual("FairValue - " + label, p0.FairPrice(), p1.FairPrice(), tol);
      AssertEqual("Delta - " + label, p0.MarketDelta(0.0001), p1.MarketDelta(0.0001), tol);
    }

    /// <summary>
    ///   Test the equivalence of strike on price and strike on spread
    /// </summary>
    [Test, Smoke]
    public void TestStrikesPriceVsSpread()
    {
      foreach (double strike in Strikes)
      {
        TestPriceSpreadEquivalence("Black,Payer", strike, OptionType.Put, false, true);
        TestPriceSpreadEquivalence("Black,Receiver", strike, OptionType.Call, false, false);
        TestPriceSpreadEquivalence("Modified,Payer", strike, OptionType.Put, true, false);
        TestPriceSpreadEquivalence("Modified,Receiver", strike, OptionType.Call, true, false);
      }
      return;
    }

    #endregion Price Spread Conversion

    #region Helpers
    /// <summary>
    ///   Calculate value of a simple option by Gauss quadrature
    /// </summary>
    /// 
    /// <remarks>
    ///   For put option we calculate E{ [s e(X) - 1]^+ },
    ///   where e(X) = exp(X - var(X)/2) and X is normal.
    /// </remarks>
    /// 
    /// <param name="type">Option type (call or put)</param>
    /// <param name="s">Forward price</param>
    /// <param name="k">Strike price</param>
    /// <param name="sigma">Standard deviation</param>
    /// 
    /// <returns>the option value</returns>
    private double gauss(OptionType type, double s, double sigma)
    {
      double sigma2 = sigma*sigma/2;
      double v = 0;
      for (int i = 0; i < quadPoints; ++i)
      {
        double xi = sigma*points_[i] - sigma2;
        double d = s * Math.Exp(xi) - 1;
        if (type == OptionType.Call && d > 0)
          v += d * weights_[i];
        else if (type == OptionType.Put && d < 0)
          v -= d * weights_[i];
      }
      return v;
    }

    /// <summary>
    ///   Calculate option value analytically
    /// </summary>
    /// 
    /// <param name="type">Option type (call or put)</param>
    /// <param name="s">Forward price</param>
    /// <param name="k">Strike price</param>
    /// <param name="sigma">Standard deviation</param>
    /// 
    /// <returns>the option value</returns>
    private double analytic(OptionType type, double s, double sigma)
    {
      if (type == OptionType.Call)
        return BaseEntity.Toolkit.Models.Black.B(s, sigma);
      else if (type == OptionType.Put)
        return s * BaseEntity.Toolkit.Models.Black.B(1.0 / s, sigma);
      else
        return 0.0;
    }

    private double BlackCalc(
      Calculator f, OptionType type, double s, double sigma,
      ref double delta, ref double gamma)
    {
      points_ = new double[quadPoints];
      weights_ = new double[quadPoints];
      Quadrature.Normal(0.0, 1.0, true, points_, weights_);

      double baseValue = f(type, s, sigma);
      double bump = s * bumpSize;
      double upValue = gauss(type, s + bump, sigma);
      double downValue = gauss(type, s - bump, sigma);

      delta = (upValue - downValue) / 2 / bump;
      gamma = (upValue + downValue - 2 * baseValue) / bump / bump;
      return baseValue;
    }

    private void CompareResults(Calculator f, OptionType type, double s, double sigma, double epsilon)
    {
      double value0 = 0, delta0 = 0, gamma0 = 0, theta0 = 0, vega0 = 0;
      value0 = BaseEntity.Toolkit.Models.Black.P(type, 1.0, s, 1.0, sigma, ref delta0, ref gamma0, ref theta0, ref vega0);
      //value0 = Black(analytic, type, s, sigma, ref delta0, ref gamma0);

      double value = 0, delta = 0, gamma = 0;
      //value = Black(gauss, type, s, sigma, ref delta, ref gamma);
      value = BlackCalc(f, type, s, sigma, ref delta, ref gamma);

      Assert.AreEqual(s, s);
      Assert.AreEqual(value0, value, epsilon);
      Assert.AreEqual(delta0, delta, 0.01);
      Assert.AreEqual(gamma0, gamma, 0.1);
    }

    #endregion /// Helpers

    #region Tests
    [Test, Smoke]
    public void TestCallOptionFiniteDiff()
    {
      const double start = 1.0 - dataDelta * dataCount / 2;
      for (int i = 0; i < dataCount; ++i)
        CompareResults(analytic, OptionType.Call, start + i * dataCount, 0.35, 1.0E-12);
    }
    [Test, Smoke]
    public void TestPutOptionFiniteDiff()
    {
      const double start = 1.0 - dataDelta * dataCount / 2;
      for (int i = 0; i < dataCount; ++i)
        CompareResults(analytic, OptionType.Put, start + i * dataCount, 0.35, 1.0E-12);
    }

    [Test, Smoke]
    public void TestCallOptionGaussQuad()
    {
      const double start = 1.0 - dataDelta*dataCount/2;
      for(int i = 0; i < dataCount;++i)
        CompareResults(gauss, OptionType.Call, start + i*dataDelta, 0.35, 5.0E-4);
    }

    [Test, Smoke]
    public void TestPutOptionGaussQuad()
    {
      const double start = 1.0 - dataDelta * dataCount / 2;
      for (int i = 0; i < dataCount; ++i)
        CompareResults(gauss, OptionType.Put, start + i * dataDelta, 0.35, 5.0E-4);
    }
    #endregion //Tests
  }
}
