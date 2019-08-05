//
// Copyright (c)    2018. All rights reserved.
//

using System;
using log4net;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Ccr;

using NUnit.Framework;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Regression tests for Swaption Pricers.
  /// </summary>
  [TestFixture("TestSwaptionVanilla1")]
  [TestFixture("TestSwaptionVanilla1_normal")]
  [TestFixture("TestSwaptionVanilla10")]
  [TestFixture("TestSwaptionVanilla11")]
  [TestFixture("TestSwaptionVanilla12")]
  [TestFixture("TestSwaptionVanilla13")]
  [TestFixture("TestSwaptionVanilla14")]
  [TestFixture("TestSwaptionVanilla15")]
  [TestFixture("TestSwaptionVanilla16")]
  [TestFixture("TestSwaptionVanilla17")]
  [TestFixture("TestSwaptionVanilla18")]
  [TestFixture("TestSwaptionVanilla19")]
  [TestFixture("TestSwaptionVanilla2")]
  [TestFixture("TestSwaptionVanilla2_normal")]
  [TestFixture("TestSwaptionVanilla20")]
  [TestFixture("TestSwaptionVanilla3")]
  [TestFixture("TestSwaptionVanilla4")]
  [TestFixture("TestSwaptionVanilla5")]
  [TestFixture("TestSwaptionVanilla6")]
  [TestFixture("TestSwaptionVanilla7")]
  [TestFixture("TestSwaptionVanilla8")]
  [TestFixture("TestSwaptionVanilla9")] 
  [Smoke]
  public class TestSwaptionBlackPricerRegression : SensitivityTest
  {
    #region Data
    //logger
    private static ILog Log = LogManager.GetLogger(typeof(TestSwaptionBlackPricerRegression));

    private SwaptionBlackPricer pricer_;
    #endregion

    #region Constructors
    /// <summary>
    /// Default Constructor
    /// </summary>
    public TestSwaptionBlackPricerRegression(string name) : base(name)
    {
    }

    /// <summary>
    /// Initializes this instance.
    /// </summary>
    [OneTimeSetUp]
    public void Initialize()
    {
      // Setup      
      Dt asOf = ToDt(AsOf);
      Dt settle = ToDt(Settle);
      Dt effective = Dt.Add(settle, Tenor.Parse(EffectiveTenor));
      Dt expiration = Dt.Add(effective, Tenor.Parse(ExpirationTenor));
      var type = (PayerReceiver)Enum.Parse(typeof(PayerReceiver), PayerReceiver, true);
      double strike = double.Parse(Strike);
      int notifyDays = int.Parse(NotificationDays);
      Dt swapEffective = Dt.AddDays(expiration, notifyDays, Calendar.None);
      Dt swapMaturity = Dt.Add(swapEffective, Tenor.Parse(SwapMaturityTenor));
      double vol = Volatility;
      double fwdRate = double.Parse(ForwardRate);
      var rateIndex = (InterestRateIndex)StandardReferenceIndices.Create("USDLIBOR_3M");

      // Swap
      var fixedLeg = new SwapLeg(swapEffective, swapMaturity, Currency.USD, strike, DayCount.Actual360,
                                 Frequency.Quarterly, BDConvention.None, Calendar.NYB, false);
      var floatingLeg = new SwapLeg(swapEffective, swapMaturity, Frequency.Quarterly, 0.0, rateIndex);

      // Option
      var swaption = new Swaption(effective, expiration, Currency.USD, fixedLeg, floatingLeg, notifyDays, type,
                                  OptionStyle.European, strike);

      // Vol
      var volCube = new VolatilityCurve(asOf, vol) {DistributionType = Distribution};
      
      // Rate Curve
      var rateCalibrator = new DiscountRateCalibrator(asOf, settle);
      var rateCurve = new DiscountCurve(rateCalibrator);
      rateCurve.AddZeroYield(Dt.Add(settle, 50, TimeUnit.Years), fwdRate, DayCount.Actual365Fixed, Frequency.Continuous);
      rateCurve.Fit();

      // Pricer
      pricer_ = new SwaptionBlackPricer(swaption, asOf, settle, rateCurve, rateCurve, volCube);

      // Validate
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
        obj => ((SwaptionBlackPricer)obj).Pv());
    }

    [Test, Smoke, Category("Pricing")]
    public void ImpliedLogNormalVolatility()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((SwaptionBlackPricer) obj).IVol(((SwaptionBlackPricer) obj).Pv()));
    }

    [Test, Smoke, Category("Pricing")]
    public void DV01()
    {
      TestNumeric(pricer_, "", obj => ((SwaptionBlackPricer)obj).DV01());
    }

    [Test, Smoke, Category("Pricing")]
    public void Gamma()
    {
      TestNumeric(pricer_, "", obj => ((SwaptionBlackPricer)obj).Gamma());
    }

    [Test, Smoke, Category("Pricing")]
    public void DeltaHedge()
    {
      TestNumeric(pricer_, "", obj => ((SwaptionBlackPricer)obj).DeltaHedge());
    }

    [Test, Smoke, Category("Pricing")]
    public void Vega()
    {
      TestNumeric(pricer_, "", obj => ((SwaptionBlackPricer)obj).Vega());
    }

    [Test, Smoke, Category("Pricing")]
    public void Theta()
    {
      Dt toAsOf = Dt.AddDays(pricer_.AsOf, 1, Calendar.None);
      Dt toSettle = Dt.AddDays(pricer_.Settle, 1, Calendar.None);
      TestNumeric(pricer_, "", obj => ((SwaptionBlackPricer) obj).Theta(toAsOf, toSettle));
    }


    [Test, Category("Pricing")]
    public void FastPv()
    {
      CcrPricer ccrPricer = CcrPricer.Get(pricer_);
      SwaptionBlackPricer pricer = (SwaptionBlackPricer)pricer_.ShallowCopy();
      Dt settle = pricer_.Settle;
      while(settle < pricer_.Swaption.Expiration)
      {
        pricer.AsOf = pricer.Settle = settle;
        double pv = pricer.Pv();
        double fastPv = ccrPricer.FastPv(settle);
        Assert.AreEqual(pv, fastPv, pv*1.0e-3);
        settle = Dt.AddWeeks(settle, 1, CycleRule.Monday);
      }
    }


    #endregion

    #region Properties
    public int AsOf { get; set; }
    public int Settle { get; set; }

    public string PayerReceiver { get; set; }
    public string EffectiveTenor { get; set; }
    public string ExpirationTenor { get; set; }
    public string Strike { get; set; }
    public string SwapMaturityTenor { get; set; }
    public string NotificationDays { get; set; }
    
    public double Volatility { get; set; }
    public string ForwardRate { get; set; }
    public DistributionType Distribution { get; set; }

    #endregion
  }
}

