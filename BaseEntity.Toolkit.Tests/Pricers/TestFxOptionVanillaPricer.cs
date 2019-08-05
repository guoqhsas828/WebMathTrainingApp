//
// Copyright (c)    2018. All rights reserved.
//

using NUnit.Framework;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using log4net;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Regression tests for FxOption Pricers.
  /// </summary>
  [TestFixture("TestVanilla1")]
  [TestFixture("TestVanilla-Ccy1Prem-1")]
  [TestFixture("TestVanilla10")]
  [TestFixture("TestVanilla11")]
  [TestFixture("TestVanilla2")]
  [TestFixture("TestVanilla3")]
  [TestFixture("TestVanilla4")]
  [TestFixture("TestVanilla5")]
  [TestFixture("TestVanilla6")]
  [TestFixture("TestVanilla7")]
  [TestFixture("TestVanilla8")]
  [TestFixture("TestVanilla9")]

  public class TestFxOptionVanillaPricer : SensitivityTest
  {
    public TestFxOptionVanillaPricer(string name) : base(name)
    {}

    #region Data

    //logger
    private static ILog Log = LogManager.GetLogger(typeof(TestFxOptionVanillaPricer));

    private IFxOptionPricer pricer_;

    #endregion

    #region Constructors

    [OneTimeSetUp]
    public void Initialize()
    {
      // Setup
      Dt asOf = AsOf;
      Dt settle = Settle;
      Dt effective = Effective;
      Dt maturity = Expiration;
      var optionType = OptionType;
      var payoffType = PayoffType;
      var flags = payoffType == OptionPayoffType.Digital
        ? OptionBarrierFlag.Digital : 0;
      double strike = Strike;
      double spotFx = SpotFxRate;
      double fwdFx = ForwardFxRate;
      double fromRate = FromRate;
      double toRate = ToRate;

      var fxOption = new FxOption()
                     {
                       Effective = effective,
                       Maturity = maturity,
                       Flags = flags,
                       Strike = strike,
                       Style = OptionStyle.European,
                       Type = optionType,
                       ReceiveCcy = PremiumInBaseCcy ? Currency.EUR : Currency.USD,
                       PayCcy = PremiumInBaseCcy ? Currency.USD : Currency.EUR,
                       Notional = 1.0
                     };
      fxOption.Validate();

      // From Rate Curve
      var fromCalibrator = new DiscountRateCalibrator(asOf, settle);
      var fromCurve = new DiscountCurve(fromCalibrator);
      fromCurve.AddZeroYield(Dt.Add(settle, 50, TimeUnit.Years), fromRate, DayCount.Actual365Fixed, Frequency.Continuous);
      fromCurve.Fit();

      // To Rate Curve
      var toCalibrator = new DiscountRateCalibrator(asOf, settle);
      var toCurve = new DiscountCurve(toCalibrator);
      toCurve.AddZeroYield(Dt.Add(settle, 50, TimeUnit.Years), toRate, DayCount.Actual365Fixed, Frequency.Continuous);
      toCurve.Fit();

      // Fx
      var fxRate = new FxRate(settle, 0, Currency.EUR, Currency.USD, spotFx, Calendar.None, Calendar.None);
      var fxCurve = new FxCurve(fxRate, new[] {maturity}, new[] {fwdFx}, new[] {"Maturity"}, null);

      // Basis
      var basisCurve = new DiscountCurve(asOf, 0.0);

      // Vol
      var volatilitySurface = PremiumInBaseCcy
        ? Volatility.ToFxVolatilitySurface(asOf, settle, maturity, fromCurve, toCurve, fxCurve)
        : Volatility.ToFxVolatilitySurface(asOf, settle, maturity, toCurve, fromCurve, fxCurve);

      // Pricer
      toCurve.Ccy = fxOption.ReceiveCcy;
      fromCurve.Ccy = fxOption.PayCcy;
      pricer_ = new FxOptionVanillaPricer(fxOption, asOf, settle, toCurve, fromCurve, fxCurve, volatilitySurface);
      pricer_.Validate();
    }

    #endregion

    #region Tests

    [Test, Smoke, Category("Pricing")]
    public void Pv()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((IFxOptionPricer)obj).Pv());
    }

    [Test, Category("CcrPricerFastPv")]
    public void FastPv()
    {
      var pricer = (FxOptionVanillaPricer) pricer_;
      var ccrPricer = CcrPricer.Get(pricer);
      Dt asOf = pricer.AsOf;
      double pv = pricer.Pv();
      double fpv = ccrPricer.FastPv(asOf)
        / pricer.DiscountCurve.DiscountFactor(asOf, pricer.Settle);
      Assert.AreEqual(pv, fpv, 1e-7);
    }

    [Test, Smoke, Category("Pricing")]
    public void FlatVolatility()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((IFxOptionPricer)obj).FlatVolatility());
    }

    [Test, Smoke, Category("Pricing")]
    public void ForwardFx()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((IFxOptionPricer)obj).ForwardFxRate());
    }

    [Test, Smoke, Category("Sensitivities")]
    public void Delta()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((IFxOptionPricer)obj).Delta());
    }

    [Test, Smoke, Category("Sensitivities")]
    public void Gamma()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((IFxOptionPricer)obj).Gamma());
    }

    [Test, Smoke, Category("Sensitivities")]
    public void Vega()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((IFxOptionPricer)obj).Vega());
    }

    [Test, Smoke, Category("Sensitivities")]
    public void Vanna()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((IFxOptionPricer)obj).Vanna());
    }

    [Test, Smoke, Category("Sensitivities")]
    public void Volga()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((IFxOptionPricer)obj).Volga());
    }

    [Test, Smoke, Category("Sensitivities")]
    public void Theta()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((IFxOptionPricer)obj).Theta());
    }

    [Test, Smoke, Category("Sensitivities")]
    public void RateSensitivity()
    {
      Rate(new[] {pricer_});
    }

    #endregion

    #region Properties

    public Dt AsOf { get; set; }
    public Dt Settle { get; set; }
    public Dt Effective { get; set; }
    public Dt Expiration { get; set; }

    public bool PremiumInBaseCcy { get; set; }

    /// <summary>
    ///  Strike level.
    /// </summary>
    public double Strike { get; set; }

    /// <summary>
    ///   Option Type (call/put)
    /// </summary>
    public OptionType OptionType { get; set; }

    /// <summary>
    ///   Payoff Type
    /// </summary>
    public OptionPayoffType PayoffType { get; set; }

    /// <summary>
    ///   Foreign interest rate.
    /// </summary>
    public double FromRate { get; set; }

    /// <summary>
    ///  Domestic Interest rate.
    /// </summary>
    public double ToRate { get; set; }

    /// <summary>
    ///   Spot FX rate
    /// </summary>
    public double SpotFxRate { get; set; }

    /// <summary>
    ///   Forward Fx Rate
    /// </summary>
    public double ForwardFxRate { get; set; }

    /// <summary>
    ///   Volatility data
    /// </summary>
    public string[,] Volatility { get; set; }

    #endregion
  }
}