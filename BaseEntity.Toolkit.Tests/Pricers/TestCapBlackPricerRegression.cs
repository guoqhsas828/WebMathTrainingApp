//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.Collections.Generic;
using log4net;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Ccr;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  /// <summary>
  /// Regression tests for Caps/Floors Pricers.
  /// </summary>
  [TestFixture("TestCapBlack10Cap")]
  [TestFixture("TestCapBlack10Floor")]
  [TestFixture("TestCapBlack1Cap")]
  [TestFixture("TestCapBlack1Floor")]
  [TestFixture("TestCapBlack2Cap")]
  [TestFixture("TestCapBlack2Floor")]
  [TestFixture("TestCapBlack3Cap")]
  [TestFixture("TestCapBlack3Floor")]
  [TestFixture("TestCapBlack4Cap")]
  [TestFixture("TestCapBlack4Floor")]
  [TestFixture("TestCapBlack5Cap")]
  [TestFixture("TestCapBlack5Floor")]
  [TestFixture("TestCapBlack6Cap")]
  [TestFixture("TestCapBlack6Floor")]
  [TestFixture("TestCapBlack7Cap")]
  [TestFixture("TestCapBlack7Floor")]
  [TestFixture("TestCapBlack8Cap")]
  [TestFixture("TestCapBlack8Floor")]
  [TestFixture("TestCapBlack9Cap")]
  [TestFixture("TestCapBlack9Floor")]
  [TestFixture("TestDigitalCapBlack10Cap")]
  [TestFixture("TestDigitalCapBlack10Floor")]
  [TestFixture("TestDigitalCapBlack1Cap")]
  [TestFixture("TestDigitalCapBlack1Floor")]
  [TestFixture("TestDigitalCapBlack2Cap")]
  [TestFixture("TestDigitalCapBlack2Floor")]
  [TestFixture("TestDigitalCapBlack3Cap")]
  [TestFixture("TestDigitalCapBlack3Floor")]
  [TestFixture("TestDigitalCapBlack4Cap")]
  [TestFixture("TestDigitalCapBlack4Floor")]
  [TestFixture("TestDigitalCapBlack5Cap")]
  [TestFixture("TestDigitalCapBlack5Floor")]
  [TestFixture("TestDigitalCapBlack6Cap")]
  [TestFixture("TestDigitalCapBlack6Floor")]
  [TestFixture("TestDigitalCapBlack7Cap")]
  [TestFixture("TestDigitalCapBlack7Floor")]
  [TestFixture("TestDigitalCapBlack8Cap")]
  [TestFixture("TestDigitalCapBlack8Floor")]
  [TestFixture("TestDigitalCapBlack9Cap")]
  [TestFixture("TestDigitalCapBlack9Floor")]
  [TestFixture("TestDigitalCapNormalBlack10Cap")]
  [TestFixture("TestDigitalCapNormalBlack10Floor")]
  [TestFixture("TestDigitalCapNormalBlack1Cap")]
  [TestFixture("TestDigitalCapNormalBlack1Floor")]
  [TestFixture("TestDigitalCapNormalBlack2Cap")]
  [TestFixture("TestDigitalCapNormalBlack2Floor")]
  [TestFixture("TestDigitalCapNormalBlack3Cap")]
  [TestFixture("TestDigitalCapNormalBlack3Floor")]
  [TestFixture("TestDigitalCapNormalBlack4Cap")]
  [TestFixture("TestDigitalCapNormalBlack4Floor")]
  [TestFixture("TestDigitalCapNormalBlack5Cap")]
  [TestFixture("TestDigitalCapNormalBlack5Floor")]
  [TestFixture("TestDigitalCapNormalBlack6Cap")]
  [TestFixture("TestDigitalCapNormalBlack6Floor")]
  [TestFixture("TestDigitalCapNormalBlack7Cap")]
  [TestFixture("TestDigitalCapNormalBlack7Floor")]
  [TestFixture("TestDigitalCapNormalBlack8Cap")]
  [TestFixture("TestDigitalCapNormalBlack8Floor")]
  [TestFixture("TestDigitalCapNormalBlack9Cap")]
  [TestFixture("TestDigitalCapNormalBlack9Floor")]
  public class TestCapBlackPricerRegression : SensitivityTest
  {

    public TestCapBlackPricerRegression(string name) : base(name)
    {
    }

    #region Data
    //logger
    private static ILog Log = LogManager.GetLogger(typeof(TestCapBlackPricerRegression));

    private CapFloorPricer pricer_;
    private IPricer[] pricers_;
    private string[] pricerNames_;
    #endregion

    #region Constructors
    /// <summary>
    /// Set up
    /// </summary>
    [OneTimeSetUp]
    public void Initialize()
    {
      // Setup      
      Dt asOf = ToDt(AsOf);
      Dt settle = ToDt(Settle);
      Dt effective = ToDt(Effective);
      Dt maturity = ToDt(Maturity);
      var type = (CapFloorType)Enum.Parse(typeof(CapFloorType), Type, true);
      var digitalType = String.IsNullOrEmpty(OptionDigitalType) ? Toolkit.Base.OptionDigitalType.None : (OptionDigitalType) Enum.Parse(typeof (OptionDigitalType), OptionDigitalType, true);
      double digitalFixedPayout = String.IsNullOrEmpty(DigitalFixedPayout) ? 0.0 : double.Parse(DigitalFixedPayout);
      double strike = double.Parse(Strike);
      int offset = int.Parse(OffsetDays);
      var volatilityType = String.IsNullOrEmpty(VolType)
                             ? VolatilityType.LogNormal
                             : (VolatilityType) Enum.Parse(typeof (VolatilityType), VolType, true);
      double vol = double.Parse(Volatility);
      double curRate = double.Parse(CurrentRate);
      double fwdRate = double.Parse(ForwardRate);
      if (volatilityType == VolatilityType.Normal)
        vol *= fwdRate;
      var cap = new Cap(
                         effective,
                         maturity,
                         Currency.USD,
                         type,
                         strike,
                         DayCount.Actual360,
                         Frequency.Quarterly,
                         BDConvention.Following,
                         Calendar.NYB)
                       {
                         RateResetOffset = offset,
                         DigitalFixedPayout = digitalFixedPayout,
                         OptionDigitalType = digitalType
                       }; 
      

      var volCube = RateVolatilityCube.CreateFlatVolatilityCube(asOf, new[] { asOf }, new[] { vol },
                                                                          volatilityType, cap.RateIndex);
      volCube.ExpiryTenors = new [] { Tenor.Parse("1Y"), Tenor.Parse("3Y"), Tenor.Parse("5Y"), Tenor.Parse("7Y") ,Tenor.Parse("10Y"),Tenor.Parse("30Y")};
      var rateCalibrator = new DiscountRateCalibrator(asOf, settle);
      var rateCurve = new DiscountCurve(rateCalibrator);
      rateCurve.AddZeroYield(Dt.Add(settle, 50, TimeUnit.Years), fwdRate, DayCount.Actual365Fixed, Frequency.Continuous);
      rateCurve.Fit();

      pricer_ = new CapFloorPricer(cap,
                                   asOf,
                                   settle,
                                   rateCurve,
                                   rateCurve,
                                   volCube);
      pricer_.Resets.Add(new RateReset(pricer_.LastExpiry, curRate));

      pricers_ = new IPricer[] {pricer_};
      pricerNames_ = new [] {Type};
    }

    #endregion

    #region Tests
    [Test, Smoke, Category("Pricing")]
    public void Pv()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((CapFloorPricer) obj).Pv());
    }

    [Test, Smoke, Category("Pricing")]
    public void ImpliedLogNormalVolatility()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((CapFloorPricer)obj).ImpliedVolatility(VolatilityType.LogNormal));
    }

    [Test, Smoke, Category("Pricing")]
    public void ImpliedNormalVolatility()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((CapFloorPricer)obj).ImpliedVolatility(VolatilityType.Normal));
    }

    [Test, Smoke, Category("Pricing")]
    public void CapDeltaBlack()
    {
      TestNumeric(pricer_, "", obj => ((CapFloorPricer) obj).DeltaBlack());
    }

    [Test, Smoke, Category("Pricing")]
    public void CapDeltaSabr()
    {
      TestNumeric(pricer_, "", obj => ((CapFloorPricer) obj).DeltaSabr());
    }

    [Test, Smoke, Category("Pricing")]
    public void ImpliedVolatility01()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((CapFloorPricer) obj).ImpliedVolatility01(String.IsNullOrEmpty(VolType)
                                                            ? VolatilityType.LogNormal
                                                            : (VolatilityType)
                                                              Enum.Parse(typeof (VolatilityType), VolType, true)));
    }

    [Test, Smoke, Category("Pricing")]
    public void Intrinsic()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((CapFloorPricer)obj).Intrinsic());
    }

    [Test, Smoke, Category("Sensitivities")]
    public void Rate01()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((CapFloorPricer)obj).Rate01());
    }

    [Test, Smoke, Category("Sensitivities")]
    public void RateGamma()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((CapFloorPricer)obj).RateGamma());
    }

    [Test, Smoke, Category("Sensitivities")]
    public void VegaBlack()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((CapFloorPricer)obj).VegaBlack());
    }

    [Test, Smoke, Category("Sensitivities")]
    public void Vega()
    {
      TestNumeric(
        pricer_,
        "",
        obj => ((CapFloorPricer)obj).Vega());
    }

    [Test, Smoke, Category("Sensitivities")]
    public void PricerTheta()
    {
      Dt toAsOf = ToDt(Settle);
      Dt toSettle = Dt.AddDays(toAsOf, 1, Calendar.None);

      TestNumeric(
        pricer_,
        "",
        obj => ((CapFloorPricer)obj).Theta(toAsOf,toSettle));
    }

    [Test, Smoke, Category("Sensitivities")]
    [Ignore("Failing for no apparent reason")]
    public void RateDelta()
    {
      IR01(pricers_, pricerNames_);
    }

    [Test, Smoke, Category("Sensitivities")]
    [Ignore("Failing for no apparent reason")]
    public void Theta()
    {
      Theta(pricers_, pricerNames_);
    }

    [Test, Smoke, Category("Sensitivities")]
    public void CapletDeltaBlack()
    {
      var caplets = new List<CapletPayment>(pricer_.Caplets.GetPaymentsByType<CapletPayment>());
      //IList<string> labels = CollectionUtil.ConvertAll(caplets, caplet => caplet.Expiry.ToString("dd-MMM-yy"));
      IList<string> labels = CollectionUtil.ConvertAll(caplets, caplet => caplet.Expiry.ToString("dd-MMM-yyyy"));
      TestNumeric(
        caplets,
        labels,
        obj => pricer_.CapletDeltaBlack((CapletPayment)obj));
    }

    [Test, Smoke, Category("Sensitivities")]
    public void CapletGammaBlack()
    {
      var caplets = new List<CapletPayment>(pricer_.Caplets.GetPaymentsByType<CapletPayment>());
      //IList<string> labels = CollectionUtil.ConvertAll(caplets, caplet => caplet.Expiry.ToString("dd-MMM-yy"));
      IList<string> labels = CollectionUtil.ConvertAll(caplets, caplet => caplet.Expiry.ToString("dd-MMM-yyyy"));
      TestNumeric(
        caplets,
        labels,
        obj => pricer_.CapletGamma((CapletPayment) obj));
    }

    [Test, Smoke, Category("Sensitivities")]
    public void CapletVegaBlack()
    {
      var caplets = new List<CapletPayment>(pricer_.Caplets.GetPaymentsByType<CapletPayment>());
      //IList<string> labels = CollectionUtil.ConvertAll(caplets, caplet => caplet.Expiry.ToString("dd-MMM-yy"));
      IList<string> labels = CollectionUtil.ConvertAll(caplets, caplet => caplet.Expiry.ToString("dd-MMM-yyyy"));
      TestNumeric(
        caplets,
        labels,
        obj => pricer_.CapletVegaBlack((CapletPayment)obj));
    }

    [Test, Smoke, Category("Sensitivities")]
    public void CapletThetaBlack()
    {
      var caplets = new List<CapletPayment>(pricer_.Caplets.GetPaymentsByType<CapletPayment>());
      //IList<string> labels = CollectionUtil.ConvertAll(caplets, caplet => caplet.Expiry.ToString("dd-MMM-yy"));
      IList<string> labels = CollectionUtil.ConvertAll(caplets, caplet => caplet.Expiry.ToString("dd-MMM-yyyy"));
      TestNumeric(
        caplets,
        labels,
        obj => pricer_.CapletTheta((CapletPayment)obj));
    }

    [Test, Smoke, Category("Sensitivities")]
    public void CapletVegaSabr()
    {
      var caplets = new List<CapletPayment>(pricer_.Caplets.GetPaymentsByType<CapletPayment>());
      //IList<string> labels = CollectionUtil.ConvertAll(caplets, caplet => caplet.Expiry.ToString("dd-MMM-yy"));
      IList<string> labels = CollectionUtil.ConvertAll(caplets, caplet => caplet.Expiry.ToString("dd-MMM-yyyy"));
      TestNumeric(
        caplets,
        labels,
        obj => pricer_.CapletVegaSabr((CapletPayment)obj));
    }

    [Test, Smoke, Category("Sensitivities")]
    public void CapletDeltaSabr()
    {
      var caplets = new List<CapletPayment>(pricer_.Caplets.GetPaymentsByType<CapletPayment>());
      //IList<string> labels = CollectionUtil.ConvertAll(caplets, caplet => caplet.Expiry.ToString("dd-MMM-yy"));
      IList<string> labels = CollectionUtil.ConvertAll(caplets, caplet => caplet.Expiry.ToString("dd-MMM-yyyy"));
      TestNumeric(
        caplets,
        labels,
        obj => pricer_.CapletDeltaSabr((CapletPayment)obj));
    }

    [Test, Smoke, Category("Sensitivities")]
    public void CapletVannaSabr()
    {
      var caplets = new List<CapletPayment>(pricer_.Caplets.GetPaymentsByType<CapletPayment>());
      //IList<string> labels = CollectionUtil.ConvertAll(caplets, caplet => caplet.Expiry.ToString("dd-MMM-yy"));
      IList<string> labels = CollectionUtil.ConvertAll(caplets, caplet => caplet.Expiry.ToString("dd-MMM-yyyy"));
      TestNumeric(
        caplets,
        labels,
        obj => pricer_.CapletVannaSabr((CapletPayment)obj));
    }

    [Test, Smoke, Category("Sensitivities")]
    public void CapletVolgaSabr()
    {
      var caplets = new List<CapletPayment>(pricer_.Caplets.GetPaymentsByType<CapletPayment>());
      //IList<string> labels = CollectionUtil.ConvertAll(caplets, caplet => caplet.Expiry.ToString("dd-MMM-yy"));
      IList<string> labels = CollectionUtil.ConvertAll(caplets, caplet => caplet.Expiry.ToString("dd-MMM-yyyy"));
      TestNumeric(
        caplets,
        labels,
        obj => pricer_.CapletVolgaSabr((CapletPayment)obj));
    }


    [Test, Category("Pricing")]
    public void FastPv()
    {
      CcrPricer ccrPricer = CcrPricer.Get(pricer_);
      CapFloorPricer pricer = (CapFloorPricer) pricer_.ShallowCopy();
      Dt settle = pricer_.Settle;
      pricer.AsOf = pricer.Settle = settle;
      double pv = pricer.Pv();
      double fastPv = ccrPricer.FastPv(settle);
      Assert.AreEqual(pv, fastPv, pv*1.0e-3);
    }

    #endregion

    #region Properties
    public int AsOf { get; set; }
    public int Settle { get; set; }
    public string Volatility { get; set; }
    public string VolType { get; set; }
    public int Effective { get; set; }
    public int Maturity { get; set; }
    public string Strike { get; set; }
    public string Type { get; set; }
    public string OffsetDays { get; set; }
    public string CurrentRate { get; set; }
    public string ForwardRate { get; set; }
    public string OptionDigitalType { get; set; }
    public string DigitalFixedPayout { get; set; }
    #endregion
  }
}
