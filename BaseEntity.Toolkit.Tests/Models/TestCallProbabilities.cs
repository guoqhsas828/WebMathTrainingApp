//
// Copyright (c)    2002-2018. All rights reserved.
//
using System.Collections.Generic;
using System.Linq;
using static System.Math;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Models.BGM.Native;
using static BaseEntity.Toolkit.Models.BGM.BgmBinomialTree;
using RateSystem = BaseEntity.Toolkit.Models.BGM.RateSystem;


namespace BaseEntity.Toolkit.Tests.Models
{
  using NUnit.Framework;

  [TestFixture]
  public class TestCallProbabilities
  {
    [Test]
    public void Simple()
    {
      Dt asOf = new Dt(20160308), maturity = new Dt(20161218);
      var swpns = GetSwaptionInfos(asOf, maturity, 2).ToArray();
      var tree = new RateSystem();
      BaseEntity.Toolkit.Models.BGM.Native.BgmBinomialTree.calibrateCoTerminalSwaptions(
        asOf, maturity, swpns, 1E-8, LogNormal, tree);

      var callInfo = new CallInfo[swpns.Length];
      double price = BgmTreeSwaptionEvaluation.
        CalculateBermudanPvWithCallProbabilities(swpns, tree, callInfo);
      var callProbability = callInfo.Aggregate(0.0, (p, c) => c.Probability + p);
      Assert.That(callProbability,
        Is.GreaterThanOrEqualTo(0.0).And.LessThanOrEqualTo(1.0));
    }

    private static IEnumerable<SwaptionInfo> GetSwaptionInfos(
      Dt asOf, Dt maturity, int n)
    {
      var months = (Dt.Diff(asOf, maturity)/30)/(n+1);
      for (int i = 0; i < n; ++i)
      {
        Dt begin = Dt.Add(maturity, (i-n)*months, TimeUnit.Months);
        yield return MakeInfo(asOf, begin, maturity, 0.02, 0.02, 0.4);
      }
    }

    private static SwaptionInfo MakeInfo(
      Dt asOf, Dt beginDt, Dt endDt,
      double rate, double coupon, double vol)
    {
      double begin = Dt.RelativeTime(asOf, beginDt),
        end = Dt.RelativeTime(asOf, endDt);
      var annuity = 0.99*(Exp(-rate*begin) - Exp(-rate*end))/rate;
      var value = annuity*Black.P(OptionType.Call, begin, rate, coupon, vol);
      return new SwaptionInfo
      {
        Date = beginDt,
        Coupon = coupon,
        Rate = rate,
        Level = annuity,
        Value = value,
        OptionType = OptionType.Call,
        Volatility = vol,
      };
    }

  }
}
