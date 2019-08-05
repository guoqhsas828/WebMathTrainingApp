//
// Copyright (c)    2018. All rights reserved.
//

using log4net;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Ccr;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Regression tests for FxOption Pricers.
  /// </summary>
  [TestFixture("TestSingleBarrier1")]
  [TestFixture("TestSingleBarrier-Ccy1Prem-1")]
  [TestFixture("TestSingleBarrier10")]
  [TestFixture("TestSingleBarrier11")]
  [TestFixture("TestSingleBarrier12")]
  [TestFixture("TestSingleBarrier2")]
  [TestFixture("TestSingleBarrier3")]
  [TestFixture("TestSingleBarrier4")]
  [TestFixture("TestSingleBarrier5")]
  [TestFixture("TestSingleBarrier6")]
  [TestFixture("TestSingleBarrier7")]
  [TestFixture("TestSingleBarrier8")]
  [TestFixture("TestSingleBarrier9")]
  public class TestFxOptionSingleBarrierPricer : SensitivityTest
  {
    public TestFxOptionSingleBarrierPricer(string name) : base(name)
    {}

    #region Data
    //logger
    private static ILog Log = LogManager.GetLogger(typeof(TestFxOptionSingleBarrierPricer));

    private FxOptionSingleBarrierPricer pricer_;
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

      // Single Barrier
      if (BarrierType!=OptionBarrierType.None)
      {
        var barrierType = BarrierType;
        var barrierVal = Barrier;
        var monitoringFreq = MonitoringFreq;
        fxOption.Barriers.Add(new Barrier());
        fxOption.Barriers[0].BarrierType = barrierType;
        fxOption.Barriers[0].Value = barrierVal;
        fxOption.Barriers[0].MonitoringFrequency = monitoringFreq;
      }
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
      pricer_ = new FxOptionSingleBarrierPricer(fxOption, asOf, settle, toCurve, fromCurve, fxCurve, volatilitySurface, 0);
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
        obj => ((IFxOptionPricer) obj).Pv());
    }

    [Test, Category("CcrPricerFastPv")]
    public void FastPv()
    {
      var ccrPricer = CcrPricer.Get(pricer_);
      Dt asOf = pricer_.AsOf;
      double pv = pricer_.Pv();
      double fpv = ccrPricer.FastPv(asOf)
        / pricer_.DiscountCurve.DiscountFactor(asOf, pricer_.Settle);
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

    [Test, Smoke, Category("Pricing")]
    public void NoTouchProbability()
    {
      TestNumeric(
        pricer_,
        "",
        obj => pricer_.NoTouchProbability());
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

    /// <summary>
    ///   Barrier type.
    /// </summary>
    public OptionBarrierType BarrierType { get; set; }

    /// <summary>
    ///  Barrier level
    /// </summary>
    public double Barrier { get; set; }

    /// <summary>
    ///   Monitory Frequency.
    /// </summary>
    public Frequency MonitoringFreq { get; set; }

    /// <summary>
    ///   Monitory Frequency.
    /// </summary>
    public SmileAdjustmentMethod SmileAdjustment { get; set; } = SmileAdjustmentMethod.VolatilityInterpolation;

    #endregion
  }
}
