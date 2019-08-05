// 
// Copyright (c)    2002-2012. All rights reserved.
// 

using System;
using System.Linq;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Util.Collections;
using static BaseEntity.Toolkit.Calibrators.Volatilities.HullWhiteParameterCalibration;
using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Calibrators
{

  [TestFixture]
  class HullWhiteShortRateModelTests1
  {
    /// <summary>
    ///  Validate the following relation with time-varying volatility
    ///  and mean reversion<math>
    ///   B(t, S) - B(t, T) = \frac{g(t)}{g(T)} B(T,S)
    ///  </math>
    /// </summary>
    [Test]
    public void ConsistentB()
    {
      // time grid
      var time = new[] { 0.25, 0.5, 1.0, 2.0, 5.0, 10.0 };
      //base on the time grid
      var timeInterval = Enumerable.Range(0, time.Length)
        .Select(i => time[i] - (i == 0 ? 0.0 : time[i - 1])).ToArray();
      // instantaneous volatilities
      var sigma = new[] { 1.5, 1.25, 1.0, 0.75, 0.5, 0.5 };
      // mean reversions
      var meanReversion = new[] { 0.5, 0.5, 0.5, 0.2, 0.2, 0.2 };
      var n = time.Length;
      var s = Enumerable.Repeat(1.0, n).ToArray();

      var data = new HullWhiteDataContainer();
      data.Times = time;
      data.Intervals = timeInterval;
      data.MeanReversions = meanReversion;
      data.Sigmas = sigma;
      data.Spreads = s;
      var b = data.B;
      var g = data.G;

      for (int i = -1, last = time.Length - 1; i < last; ++i)
      {
        var t = i < 0 ? 0 : i;
        for (int j = i + 1; j < last; ++j)
        {
          var T = j;
          for (int k = j + 1; k <= last; ++k)
          {
            var S = k;

            // Please get the values of B(t, S) and B(t, T) in your implementation
            var left =b[t+1, S] - b[t+1, T]; //<== B(t, S) - B(t, T);

            // Please get the values of g(t), g(T) and B(T, S) in your implementation
            var right = (t < 0 ? 1 : g[t])/g[T]*b[T+1, S]; //<==  g(t)/g(T)*B(T, S);

            Assert.AreEqual(left, right, 1E-15);
          }
        }
      }
    }

    /// <summary>
    ///  Validate our calculations against the simple and well-known formula
    ///  in the case of flat volatility/mean reversion.
    /// </summary>
    /// <param name="sigma">The flat instantaneous volatility</param>
    /// <param name="a">The flat mean reversion</param>
    [TestCase(0.5, 0.0)]
    [TestCase(0.8, 0.4)]
    [TestCase(1.5, 0.9)]
    public void Flat(double sigma, double a)
    {
      // time grid
      var time = new[] { 0.25, 0.50, 1.0, 2.0, 5.0, 10.0 };
      var timeInterval = new[] { 0.25, 0.25, 0.5, 1.0, 3.0, 5.0 };
      var n = time.Length;
      // piecewise flat forward rates (continues compounding)
      var f = new[] { .005, .01, .02, .025, 0.03, 0.04 };
      var s = Enumerable.Repeat(1.0, n).ToArray();

      // We calculate the zero prices at time 0 here
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

      var data = new HullWhiteDataContainer();
      data.Times = time;
      data.Intervals = timeInterval;
      data.MeanReversions = a.Repeat(n).ToArray();
      data.Sigmas = sigma.Repeat(n).ToArray();
      data.Forwards = f;
      data.DiscountFactors = zeroPrices;
      data.ProjFactors = zeroPrices;
      data.Spreads = s;
      var b = data.B;
      var vr = data.Vr;

      // The follow are test grid
      var sigma2 = sigma*sigma;
      for (int i = 0; i < n-1; ++i)
      {
        var t = i < 0 ? 0.0 : time[i];
        // The short rate volatility Vr(0, t) in simple formula 
        var expectVr0t = Expd1(-2*a*t)*t*sigma2;

        // Please get the value of Vr(0,t) in your implementation
        var actualVr0t = vr[i];

        Assert.AreEqual(expectVr0t, actualVr0t, 1E-15,
          "Short rate variance Vr(0,t)");

        var p0 = i < 0 ? 1.0 : zeroPrices[i];
        var f0 = f[i];
        for (int j = i + 1; j < n; ++j)
        {
          var T = time[j];

          // This is B(t, T) in simple formula
          var expectB = Expd1(-a*(T - t))*(T - t);

          // Please get the value of B(t, T) in your implementation
          var actualB = b[i+1, j]; //<= replace this value

          Assert.AreEqual(expectB, actualB,
            1E-15, "Affine volatility coefficient B(t,T)");

          // This is A(t, T) in simple formula
          var expectA = Math.Log(zeroPrices[j]/p0)
            + expectB*(f0 - 0.5*expectB*expectVr0t);

          // Please get the value of A(t, T) in your implementation
          var actualA = CalcA(i, j, data); //<= replace this value

          Assert.AreEqual(expectA, actualA, 1E-14,
            "Affine drift coefficient A(t,T)");

          // This is the volatility of the forward rate F(t, T) in simple formula
          var expectCapletVol = Math.Sqrt(expectVr0t)*expectB;

          // Please get the caplet volatility in your implementation
          var actualCapletVol = Math.Sqrt(CalcVp(i,j, data)); //<= replace this value

          Assert.AreEqual(expectCapletVol, actualCapletVol, 1E-15,
            "Total volatility of the forward rate Vp(0,t,T)");
        }
      }
      return;
    }

    /// <summary>
    /// Calculate the function
    ///  <m>\displaystyle\mathrm{d1}(x) = \frac{e^x - 1}{x}</m>
    /// </summary>
    /// <param name="x">The x value</param>
    /// <returns>System.Double.</returns>
    public static double Expd1(double x)
    {
      if (Math.Abs(x) < 1E-3)
        return 1 + x*(1 + x*(1 + x*(1 + x/5)/4)/3)/2;
      return (Math.Exp(x) - 1)/x;
    }

  }
}
