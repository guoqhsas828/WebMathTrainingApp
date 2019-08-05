//
// Copyright (c)    2018. All rights reserved.
//

using System;
using System.IO;
using BaseEntity.Configuration;
using NUnit.Framework;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BGM;
using BaseEntity.Toolkit.Tests.Cashflows;
using Xml = BaseEntity.Toolkit.Util.XmlSerialization;

namespace BaseEntity.Toolkit.Tests.Pricers
{
  [TestFixture]
  public class TestSwaptionPaymentSchedule
  {
    [TestCase("0847")]
    [TestCase("0848")]
    [TestCase("0851")]
    [TestCase("0858")]
    [TestCase("0855")]
    [TestCase("0860")]
    [TestCase("0869")]
    [TestCase("0880")]
    [TestCase("0889")]
    [TestCase("0894")]
    [TestCase("0955")]
    [TestCase("0964")]
    public void TestSwnpPaymentSchedule(string postFix)
    {
      var filePath = String.Format(@"toolkit\test\data\SwapPricers\SwapPricer{0}.xml", postFix);
      var file = Path.Combine(SystemContext.InstallDir, filePath);
      var pricer = Xml.ReadXmlFile(file) as SwapPricer;
      if (pricer?.SwaptionPricer == null)
        throw new ArgumentException("The swaption pricer is null");

      var bgmSwapPricer = pricer.SwaptionPricer as SwapBermudanBgmTreePricer;
      if (bgmSwapPricer != null)
      {
        TestSwapBermudanBgmTreePricer(bgmSwapPricer);
      }
    }

    [TestCase("1")]
    [TestCase("2")]
    [TestCase("3")]
    [TestCase("2960")]
    [TestCase("3071")]
    [TestCase("3072")]
    public void TestSbbtp(string postFix)
    {
      var pricerFile = @"toolkit\test\data\SwapPricers\SwapBermudanBgmPricer" + postFix + ".xml";
      var pricerPath = Path.Combine(SystemContext.InstallDir, pricerFile);

      var pricer = Xml.ReadXmlFile(pricerPath) as SwapBermudanBgmTreePricer;
      if (pricer != null)
      {
        TestSwapBermudanBgmTreePricer(pricer);
      }
    }

    [TestCase("1")]
    [TestCase("2")]
    [TestCase("3")]
    [TestCase("4")]
    public void TestSwaptionPv(string postFix)
    {
      var pricerFile = @"toolkit\test\data\Swaptions\SwaptionBlackPricer" + postFix + ".xml";
      var pricerPath = Path.Combine(SystemContext.InstallDir, pricerFile);

      var pricer = Xml.ReadXmlFile(pricerPath) as SwaptionBlackPricer;
      if (pricer != null)
      {
        CustomScheduleTests.SetUsePaymentScheduleForCashflow(true);
        var pv1 = pricer.Pv();
        CustomScheduleTests.SetUsePaymentScheduleForCashflow(false);
        var pv2 = pricer.Pv();
        Assert.AreEqual(pv1, pv2, pricer.Notional * 1E-15);
      }
    }

    private static void TestSwapBermudanBgmTreePricer(
      SwapBermudanBgmTreePricer pricer)
    {
      CustomScheduleTests.SetUsePaymentScheduleForCashflow(true);
      var swpnsP = pricer.BuildCoTerminalSwaptions(false).Item1;
      CustomScheduleTests.SetUsePaymentScheduleForCashflow(false);
      var swpnsC = pricer.BuildCoTerminalSwaptions(false).Item1;
      for (int i = 0; i < swpnsP.Length; i++)
      {
        Assert.AreEqual(Dt.Cmp(swpnsP[i].Date, swpnsC[i].Date), 0);
        Assert.AreEqual(swpnsP[i].Coupon, swpnsC[i].Coupon, 1E-14);
        Assert.AreEqual(swpnsP[i].Level, swpnsC[i].Level, 1E-14);
        Assert.AreEqual(swpnsP[i].Value, swpnsC[i].Value, 1E-14);
        Assert.AreEqual(swpnsP[i].Rate, swpnsC[i].Rate, 1E-14);
        Assert.AreEqual(swpnsP[i].Volatility, swpnsC[i].Volatility, 1E-14);
      }
    }
  }
}// namespace
