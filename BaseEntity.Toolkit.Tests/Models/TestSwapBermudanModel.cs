//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers.BGM;
using BaseEntity.Toolkit.Products;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestSwapBermudanModel : ToolkitTestBase
  {
    private double sigma_ = 0.4;
    private double interestRate_ = 0.05;
    private double strike = 0.05;
    private double hazardRate_ = 0.5;
    private double recoveryRate_ = 0.4;
    private double treeTol = 1E-12;

    private void SimpleTest(Dt asOf, OptionType optype, DistributionType volType)
    {
      Dt settle = Dt.AddDays(asOf, 1, Calendar.None);
      Dt maturity = Dt.Add(settle, "5Y");
      var volatilityObject = new FlatVolatility
      {
        Volatility = volType == DistributionType.Normal ? (sigma_ * interestRate_) : sigma_,
        DistributionType = volType
      };
      var discountCurve = new DiscountCurve(asOf, interestRate_);
      var referenceCurve = discountCurve;

      var index = "USDLIBOR_3M";
      var rateIndex = StandardReferenceIndices.Create(index);
      var floatLeg = new SwapLeg(settle, maturity, Frequency.Quarterly, 0.0,rateIndex);
      floatLeg.Validate();

      var fixedLeg = new SwapLeg(settle, maturity, rateIndex.Currency, strike,
        DayCount.Actual360, Frequency.SemiAnnual, BDConvention.Following,
        Calendar.None, false);
      fixedLeg.Validate();

      var swaption = new Swaption(asOf, maturity, rateIndex.Currency,
        fixedLeg, floatLeg, 0, PayerReceiver.Payer, OptionStyle.European,
        Double.NaN);
      swaption.Validate();

      for (int i = 0; ++i < 19; )
      {
        Dt expiry = Dt.Add(asOf, Frequency.Monthly, i*3, CycleRule.None);
        if (optype == OptionType.Call && volType == DistributionType.Normal
          && asOf == new Dt(20120130) && expiry == new Dt(20160730))
        {
          //TODO: A strange date, expires on holiday.
          continue;
        }
        var pricer = new SwapBermudanBgmTreePricer(swaption, asOf, settle,
          discountCurve, referenceCurve, null,
          new[]
          {
            optype == OptionType.Call
              ? (IOptionPeriod)
                new CallPeriod(expiry, expiry, 1, 1, OptionStyle.European, 0)
              : (IOptionPeriod)
                new PutPeriod(expiry, expiry, 1, OptionStyle.European)
          }, volatilityObject);
        pricer.NoConversionLogNormal = true;
        pricer.Validate();
        var infos = pricer.BuildCoTerminalSwaptions(false).Item1;
        if(infos.Length!=1)
          Assert.AreEqual(1, infos.Length, "Count");
        var actual = pricer.ProductPv();
        if (Math.Abs(infos[0].Value - actual) > 1E-5)
        {
          Assert.AreEqual(infos[0].Value, actual, 2E-5, "Pv@" + (i * 3) + 'M');
          if (Math.Abs(infos[0].Value - actual) > 1E-3) 
            Console.WriteLine("Pv at {0}: actual {1}, expect {2}",
              asOf, actual, infos[0].Value);
        }
      }
      return;
    }

    private void SimpleTest(OptionType optype, DistributionType volType)
    {
      Dt begin = new Dt(20110907);
      Dt end = new Dt(20120910);
      while(begin < end)
      {
        SimpleTest(begin, optype, volType);
        begin = Dt.AddDays(begin, 1, Calendar.None);
      }
    }

    [Test, Smoke]
    public void SwaptionCallLogNormal()
    {
      SimpleTest(OptionType.Call, DistributionType.LogNormal);
    }

    [Test, Smoke]
    public void SwaptionPutLogNormal()
    {
      SimpleTest(OptionType.Put, DistributionType.LogNormal);
    }

    [Test, Smoke]
    public void SwaptionCallNormal()
    {
      SimpleTest(OptionType.Call, DistributionType.Normal);
    }

    [Test, Smoke]
    public void SwaptionPutNormal()
    {
      SimpleTest(OptionType.Put, DistributionType.Normal);
    }
  }
}
