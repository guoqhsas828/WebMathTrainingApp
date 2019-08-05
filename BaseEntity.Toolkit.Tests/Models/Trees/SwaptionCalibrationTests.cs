//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models.BGM.Native;
using BaseEntity.Toolkit.Models.Trees;
using BaseEntity.Toolkit.Numerics;
using DistributionKind = BaseEntity.Toolkit.Calibrators.Volatilities.DistributionType;
using ArrayUtility = BaseEntity.Toolkit.Util.Collections.ListUtil;
using BgmBinomialTree = BaseEntity.Toolkit.Models.BGM.BgmBinomialTree;

namespace BaseEntity.Toolkit.Tests.Models.Trees
{
  using NUnit.Framework;

  [TestFixture]
  public class SwaptionCalibrationTests
  {
    [TestCase(DistributionKind.LogNormal)]
    [TestCase(DistributionKind.Normal)]
    public void Bermudan(DistributionKind distribution)
    {
      var data = _data;
      var asOf = data.AsOf;
      var maturity = data.Maturity;

      var count = data.Swaptions.Length;
      var expects = distribution == DistributionKind.LogNormal
        ? new[]
        {
          0.18876490098495122,
          0.19536635728009400,
          0.19229565725104281,
          0.20502281302673170,
        }
        : new[]
        {
          0.18876490105075644,
          0.20274872621134449,
          0.20565372260777892,
          0.20650125829648561,
        };
      var bermudans = new double[count];
      for (int i = count; --i >= 0; )
      {
        var calibrator = new CoterminalSwaptionCalibrator(
          data.Swaptions.Take(i + 1), asOf, maturity,
          distribution, 15, 5, 1E-9);
        calibrator.Fit();
        for (int j = 0; j <= i; ++j)
        {
          var v = calibrator.CalculateSwaptionValue(j);
          Assert.That(v, Is.EqualTo(data.Swaptions[j].Value).Within(1E-6));
        }
        bermudans[i] = calibrator.CalculateBermudanValue();
      }
      Assert.That(bermudans, Is.EqualTo(expects).Within(1E-15));

      var bermudans1 = new double[count];
      {
        var calibrator = new CoterminalSwaptionCalibrator(
          data.Swaptions, asOf, maturity,
          distribution, 15, 5, 1E-6);
        calibrator.Fit();
        for (int i = count; --i >= 0; )
          bermudans1[i] = calibrator.CalculateBermudanValue(0, i);
      }
      Assert.That(bermudans[0],
        Is.EqualTo(expects[0]).Within(1E-6));
      Assert.That(bermudans[count - 1],
        Is.EqualTo(expects[count - 1]).Within(1E-15));

      var swpn = data.Swaptions[1];
      var expiry = (swpn.Date - asOf) / 365.0;
      var sqrt = Math.Sqrt(expiry);
      var fraction = (maturity - swpn.Date) / 365.0;
      var strike = swpn.Coupon;
      var rate = swpn.Rate;
      var level = swpn.Level;

      var sigmas = new[] { 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 };
      expects = ArrayUtility.MapAll(sigmas,
        v => level * strike * Black(rate / strike, v * sqrt));

      var tree = PcvBinomialTree.Build(
        new[] { 100 }, new[] { expiry }, new[] { 1.0 });
      var actuals = ArrayUtility.ConvertAll(sigmas,
        v => CalculateOptionValue(tree, v, rate, strike) * level);

      Assert.That(actuals, Is.EqualTo(expects).Within(1E-3));
      return;
    }

    private static double Black(double f, double v)
    {
      return SpecialFunctions.Black(f, 1, v);
    }

    private static double CalculateOptionValue(
      PcvBinomialTree tree, double beta,
      double rate, double strike)
    {
      var rates = tree.CalculateLogNormalTerminalValues(
        rate, tree.StepMaps[0], beta);
      var probs = tree.GetProbabilities(tree.StepMaps[0]);
      var pv = 0.0;
      for (int i = rates.Count; --i >= 0; )
      {
        var v = rates[i] - strike;
        if (v <= 0) continue;
        pv += probs[i] * v;
      }
      return pv;
    }

    private static readonly Data _data = new Data
    {
      AsOf = _D(2015, 12, 31),
      Maturity = _D(2050, 12, 31),
      Swaptions = new[]
      {
        new SwaptionInfo
        {
          Accuracy = 0.000001,
          Coupon = 0.01,
          Date = _D(2030, 12, 31),
          Level = 13.836757403327095,
          OptionType = Call,
          Rate = 0.018065281937906554,
          Steps = 150,
          Value = 0.1887649009636598,
          Volatility = 0.005850694063926941,
        },
        new SwaptionInfo
        {
          Accuracy = 0.000001,
          Coupon = 0.009999999999999995,
          Date = _D(2035, 12, 31),
          Level = 9.9127504365450125,
          OptionType = Call,
          Rate = 0.016710343264381314,
          Steps = 50,
          Value = 0.13895334058795186,
          Volatility = 0.0057807389162561583,
        },
        new SwaptionInfo
        {
          Accuracy = 0.000001,
          Coupon = 0.0099999999999999985,
          Date = _D(2040, 12, 31),
          Level = 6.3382484791481124,
          OptionType = Call,
          Rate = 0.016098134500784789,
          Steps = 50,
          Value = 0.093813781168076038,
          Volatility = 0.0057608702791461415,
        },
        new SwaptionInfo
        {
          Accuracy = 0.000001,
          Coupon = 0.0099999999999999985,
          Date = _D(2045, 12, 31),
          Level = 3.0463836264032689,
          OptionType = Call,
          Rate = 0.015911144612442321,
          Steps = 50,
          Value = 0.046989638161670323,
          Volatility = 0.00560078860898138,
        },
      },
    };

    private static readonly Data _data2 = new Data
    {
      AsOf = _D(2016, 5, 6),
      Maturity = _D(2024, 6, 24),
      Swaptions = new[]
      {
        new SwaptionInfo
        {
          Accuracy = 0.000001,
          Coupon = 0.0032607273100443441,
          Date = _D(2017, 06, 12),
          Level = 7.0609466928023092,
          OptionType = Put,
          Rate = 0.0036413555396702761,
          Steps = 0,
          Value = 0.013446692458201815,
          Volatility = 0.0051733879781420758,
        },
        new SwaptionInfo
        {
          Accuracy = 0.000001,
          Coupon = 0.0035305065915794145,
          Date = _D(2018, 06, 11),
          Level = 6.0552291980152413,
          OptionType = Put,
          Rate = 0.00452090585600679,
          Steps = 0,
          Value = 0.015671498831597526,
          Volatility = 0.0053903561643835621,
        },
        new SwaptionInfo
        {
          Accuracy = 0.000001,
          Coupon = 0.0036103749088791867,
          Date = _D(2019, 06, 10),
          Level = 5.0457515335665883,
          OptionType = Put,
          Rate = 0.0056017122464879629,
          Steps = 0,
          Value = 0.015614643034945207,
          Volatility = 0.005787534246575343,
        },
        new SwaptionInfo
        {
          Accuracy = 0.000001,
          Coupon = 0.0037351776239658071,
          Date = _D(2020, 06, 10),
          Level = 4.0310767642787413,
          OptionType = Put,
          Rate = 0.0068314332648868369,
          Steps = 0,
          Value = 0.014393118823067288,
          Volatility = 0.0062067582417582425,
        },
        new SwaptionInfo
        {
          Accuracy = 0.000001,
          Coupon = 0.0039401247570402956,
          Date = _D(2021, 06, 10),
          Level = 3.0163027471741657,
          OptionType = Put,
          Rate = 0.0081350082089377673,
          Steps = 0,
          Value = 0.01191239752700351,
          Volatility = 0.0064957534246575348,
        },
        new SwaptionInfo
        {
          Accuracy = 0.000001,
          Coupon = 0.0043531813103973912,
          Date = _D(2022, 06, 10),
          Level = 2.0041905983213306,
          OptionType = Put,
          Rate = 0.0094980603696485852,
          Steps = 0,
          Value = 0.00861748683405035,
          Volatility = 0.00669400544959128,
        },
        new SwaptionInfo
        {
          Accuracy = 0.000001,
          Coupon = 0.0055992690267166476,
          Date = _D(2023, 06, 12),
          Level = 0.99233831862336708,
          OptionType = Put,
          Rate = 0.010815397203440602,
          Steps = 0,
          Value = 0.0049369410908645063,
          Volatility = 0.0068913387978142091,
        },

      }
    };

    private const OptionType Call = OptionType.Call;
    private const OptionType Put = OptionType.Put;

    private class Data
    {
      public DateTime AsOf, Maturity;
      public SwaptionInfo[] Swaptions;
    }

    private static DateTime _D(int year, int month, int day)
    {
      return new DateTime(year, month, day);
    }
  }
}
