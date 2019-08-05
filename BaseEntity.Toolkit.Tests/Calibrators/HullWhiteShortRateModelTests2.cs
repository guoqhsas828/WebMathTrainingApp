// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Models;
using static BaseEntity.Toolkit.Calibrators.Volatilities.HullWhiteParameterCalibration;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Calibrators
{

  [TestFixture]
  public class HullWhiteShortRateModelTests2
  {

    [Test]
    public void Caplet()
    {
      // We want to make sure it works with both positive and negative rates
      var time = new[] { 0.25, 0.5, 1.0, 2.0, 5.0, 10.0 };
      var timeInterval = Enumerable.Range(0, time.Length)
        .Select(i => time[i] - (i == 0 ? 0.0 : time[i - 1])).ToArray();
      var sigma = new[] { 0.010, 0.009, 0.007, 0.006, 0.005, 0.004 };
      var meanReversion = new[] { 0.5, 0.5, 0.5, 0.2, 0.2, 0.2 };
      var f = new[] { -.005, -.002, -0.001, .0125, 0.03, 0.04 };
      // We calculate the zero prices at time 0 here
      var n = time.Length;
      var s = Enumerable.Repeat(1.0, n).ToArray();
      var zeroPrices = new double[n];
      {
        double df = 1, t0 = 0;
        for (int i = 0; i < n; ++i)
        {
          df *= Math.Exp(-f[i] * (time[i] - t0));
          zeroPrices[i] = df;
          t0 = time[i];
        }
      }

      var data = new HullWhiteDataContainer
      {
        Times = time,
        Intervals = timeInterval,
        MeanReversions = meanReversion,
        Sigmas = sigma,
        Forwards = f,
        DiscountFactors = zeroPrices,
        ProjFactors = zeroPrices,
        Spreads = s
      };


      for (int i = 0; i < n; ++i)
      {
        var Ti = time[i];
        for (int j = i + 1; j < n; ++j)
        {
          var Tj = time[j];

          // Let's use fixed-365 day count for rate/strike.
          // Our calculation should work with all the different day counts.
          var frac = (Tj - Ti);
          var rate = (zeroPrices[i] / zeroPrices[j] - 1) / frac;
          var strike = rate * 1.01;

          var hwPremium = CapletModelPrice(i, j, strike, data);

          // Now we implied a Black normal volatility from the Hull-White premium
          var normalizedPrice = hwPremium / (zeroPrices[j] * frac);
          var impliedNormalVol = BlackNormal.ImpliedVolatility(
            OptionType.Call, time[i], 0, rate, strike, normalizedPrice);

          var hwVp = CalcVp(i, j, data);  

          // This is the approximate Black normal volatility by freezing initial rates.
          var approxBlackVol = Math.Abs(1 + frac * rate) / frac * Math.Sqrt(hwVp / time[i]);

          // Check if we get close enough values
          Assert.AreEqual(impliedNormalVol, approxBlackVol,
            impliedNormalVol * 2E-3, "Implied Volatility");

          // Calculate the approximate caplet values
          var approxPremium = zeroPrices[j] * frac * BlackNormal.P(
            OptionType.Call, time[i], 0, rate, strike, approxBlackVol);

          // Check if we get close enough values
          Assert.AreEqual(hwPremium, approxPremium, hwPremium * 2E-3, "Premium");
        }
      }
    }

    [Test]
    public void TestSwaptionModelPv()
    {
      // We want to make sure it works with both positive and negative rates
      var time = new[] { 0.25, 0.5, 1.0, 2.0, 5.0, 10.0 };
      var timeInterval = Enumerable.Range(0, time.Length)
         .Select(i => time[i] - (i == 0 ? 0.0 : time[i - 1])).ToArray();
      var sigma = new[] { 0.010, 0.009, 0.007, 0.006, 0.005, 0.004 };
      var meanReversion = new[] { 0.5, 0.5, 0.5, 0.2, 0.2, 0.2 };
      var f = new[] { -.005, -.002, -0.001, .0125, 0.03, 0.04 };
      // We calculate the zero prices at time 0 here
      var n = time.Length;
      var s = Enumerable.Repeat(1.0, n).ToArray();
      var zeroPrices = new double[n];
      {
        double df = 1, t0 = 0;
        for (int i = 0; i < n; ++i)
        {
          df *= Math.Exp(-f[i] * (time[i] - t0));
          zeroPrices[i] = df;
          t0 = time[i];
        }
      }

      var data = new HullWhiteDataContainer
      {
        Times = time,
        Intervals = timeInterval,
        MeanReversions = meanReversion,
        Sigmas = sigma,
        Forwards = f,
        DiscountFactors = zeroPrices,
        ProjFactors = zeroPrices,
        Spreads = s
      };

      const double percentage = 7E-2;

      for (int i = 0; i < n; ++i)
      {
        for (int j = i + 1; j < n; ++j)
        {
          double annunity;
          var swapRate = HullWhiteTestUtil.SingleCurveSwapRate(i, j, zeroPrices, time, out annunity);

          var hwPremium = SwaptionModelPv(i, j, swapRate, data);
          
          // Now we implied a Black normal volatility from the Hull-White premium
          var normalizedPrice = hwPremium / annunity;
          var impliedNormalVol = BlackNormal.ImpliedVolatility(
            OptionType.Call, time[i], 0, swapRate, swapRate, normalizedPrice);

          var hwVp = CalcVp(i, j, data); 

          // This is the approximate Black normal volatility by freezing initial rates.
          var approxBlackVol = Math.Abs(swapRate*zeroPrices[i]/(zeroPrices[i] - zeroPrices[j]))
                               *Math.Sqrt(hwVp/time[i]);

          // Check if we get close enough values
          Assert.AreEqual(impliedNormalVol, approxBlackVol,
            impliedNormalVol * percentage, "Implied Volatility (" + i + ", " + j + ')');

          // Calculate the approximate caplet values
          var approxPremium = annunity* BlackNormal.P(
            OptionType.Call, time[i], 0, swapRate, swapRate, approxBlackVol);

          // Check if we get close enough values
          Assert.AreEqual(hwPremium, approxPremium, hwPremium * percentage, "Premium");
        }
      }
    }


    [Test]
    public void TestSwaptionModelPvVsCpltPv()
    {
      // We want to make sure it works with both positive and negative rates
      var time = new[] {0.25, 0.5, 1.0, 2.0, 5.0, 10.0};
      var timeInterval = Enumerable.Range(0, time.Length)
         .Select(i => time[i] - (i == 0 ? 0.0 : time[i - 1])).ToArray();
      var sigma = new[] {0.010, 0.009, 0.007, 0.006, 0.005, 0.004};
      var meanReversion = new[] {0.5, 0.5, 0.5, 0.2, 0.2, 0.2};
      var f = new[] {-.005, -.002, -0.001, .0125, 0.03, 0.04};
      // We calculate the zero prices at time 0 here
      var n = time.Length;
      var s = Enumerable.Repeat(1.0, n).ToArray();
      var zeroPrices = new double[n];
      {
        double df = 1, t0 = 0;
        for (int i = 0; i < n; ++i)
        {
          df *= Math.Exp(-f[i]*(time[i] - t0));
          zeroPrices[i] = df;
          t0 = time[i];
        }
      }

      var data = new HullWhiteDataContainer
      {
        Times =time,
        Intervals = timeInterval,
        MeanReversions = meanReversion,
        Sigmas = sigma,
        Forwards =f,
        DiscountFactors = zeroPrices,
        ProjFactors = zeroPrices,
        Spreads = s
      };
      
      for (int i = 0; i < n; ++i)
      {
        var ti = time[i];
        for (int j = i + 1; j < n; ++j)
        {
          double annunity;
          var swapRate = HullWhiteTestUtil.SingleCurveSwapRate(i, j, zeroPrices, time, out annunity);

          var tj = time[j];

          // Please get the caplet value from your Hull-White model implementation
          var hwPremium = SwaptionModelPv(i, j, swapRate, data);
          if (j == i + 1)
          {
            var frac = tj - ti;
            var rate = (zeroPrices[i]/zeroPrices[j] - 1)/frac;

            Assert.AreEqual(rate, swapRate, 1E-15, "rate");
            Assert.AreEqual(frac*zeroPrices[j], annunity, 1E-15, "annunity");

            var capletModelPv = CapletModelPrice(i, j, rate, data);
            Assert.AreEqual(capletModelPv, hwPremium, 1E-10, "capletPv");
          }
        }
      }
    }
  }
}

