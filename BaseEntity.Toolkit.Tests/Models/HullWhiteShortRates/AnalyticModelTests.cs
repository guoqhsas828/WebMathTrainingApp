//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util.Collections;
using QMath = BaseEntity.Toolkit.Numerics.SpecialFunctions;

namespace BaseEntity.Toolkit.Tests.Models.HullWhiteShortRates
{
  using NUnit.Framework;

  [TestFixture]
  public class AnaluyticModelTests
  {
    #region Tests

    /// <summary>
    ///  Validate the following relation with time-varying volatility
    ///  and mean reversion<math>
    ///   B(t, S) - B(t, T) = \frac{g(t)}{g(T)} B(T,S)
    ///  </math>
    /// </summary>
    [TestCase(0)]
    [TestCase(1)]
    public void ConsistentB(int dataIndex)
    {
      var d = data[dataIndex];
      var calc = GetCalculator(d);
      Assert.AreEqual(d.time.Length, calc.Count, "Time grid count");

      Func<int, double> g = calc.CalculateG;
      Func<int, int, double> B = calc.CalculateB;
      for (int t = -1, last = calc.Count - 1; t < last; ++t)
      {
        for (int T = t + 1; T < last; ++T)
        {
          for (int S = T + 1; S <= last; ++S)
          {
            var left = B(t, S) - B(t, T);
            var right = g(t)/g(T)*B(T, S);
            Assert.AreEqual(left, right, 1E-15);
          }
        }
      }
    }

    /// <summary>
    ///  Validate our calculations against the simple and well-known formula
    ///  in the case of flat volatility/mean reversion.
    /// </summary>
    /// <param name="sigma">volatility</param>
    /// <param name="a">mean reversion</param>
    [TestCase(0.5, 0.0)]
    [TestCase(0.8, 0.4)]
    [TestCase(1.5, 0.9)]
    public void Flat(double sigma, double a)
    {
      var time = new[] {0.25, 0.5, 1.0, 2.0, 5.0, 10.0};
      var f = new[] {.005, .01, .02, .025, 0.03, 0.04};
      var zeroPrices = CreateZeroPrices(time, f);

      var n = time.Length;
      var calc = HullWhiteShortRatePcpCalculator.Create(
        time, sigma.Repeat(n), a.Repeat(n));

      var sigma2 = sigma*sigma;
      for (int i = -1; i < n - 1; ++i)
      {
        var t = i < 0 ? 0.0 : time[i];
        var expectVr0t = QMath.Expd1(-2*a*t)*t*sigma2;
        var actualVr0t = calc.CalculateShortRateVariance(i);
        Assert.AreEqual(expectVr0t, actualVr0t, 1E-15,
          "Short rate variance Vr(0,t)");

        var p0 = i < 0 ? 1.0 : zeroPrices[i];
        var f0 = f[i + 1];
        for (int j = i + 1; j < n; ++j)
        {
          var T = time[j];
          var expectB = QMath.Expd1(-a*(T - t))*(T - t);
          var actualB = calc.CalculateB(i, j);
          Assert.AreEqual(expectB, actualB,
            1E-15, "Affine volatility coefficient B(t,T)");

          var expectVrtT = sigma2*QMath.Expd1(-2*a*(T - t))*(T - t);
          var actualVrtT = calc.CalculateShortRateVariance(i, j);
          Assert.AreEqual(expectVrtT, actualVrtT, 1E-15,
            "Short rate variance Vr(t,T)");

          var expectCapletVol = Math.Sqrt(expectVr0t)*expectB;
          var actualCapletVol = calc.CalculateCapletVolatility(i, j);
          Assert.AreEqual(expectCapletVol, actualCapletVol, 1E-15,
            "Total volatility of the forward rate Vp(0,t,T)");

          var expectA = Math.Log(zeroPrices[j]/p0)
            + expectB*(f0 - 0.5*expectB*expectVr0t);
          var actualA = calc.CalculateA(i, j, zeroPrices, time);
          Assert.AreEqual(expectA, actualA, 1E-14,
            "Affine drift coefficient A(t,T)");
        }
      }
      return;
    }

    /// <summary>
    ///  Round trip tests based on the formula 
    ///   P(t,T) = exp(A(t,T) - B(t,T) r)
    /// </summary>
    /// <param name="dataIndex">The input data index</param>
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void ZeroPrices(int dataIndex)
    {
      var d = data[dataIndex];
      var calc = GetCalculator(d);
      var n = calc.Count;
      for (int i = -1; i < n - 1; ++i)
      {
        var p0 = i < 0 ? 1.0 : d.ZeroPrices[i];
        var f0 = d.forward[i + 1];
        var Vr = calc.CalculateShortRateVariance(i);
        for (int j = i + 1; j < n; ++j)
        {
          var B = calc.CalculateB(i, j);
          var r = f0 - 0.5*B*Vr;
          var A = calc.CalculateA(i, j, d.ZeroPrices, d.time);
          var price = Math.Exp(A - B*r);
          var expect = d.ZeroPrices[j]/p0;
          Assert.AreEqual(expect, price, 5E-15, "Zero Price");
        }
      }
    }

    /// <summary>
    ///  Compare the caplet values from Hull-White models
    ///  with those calculated by approximation.
    /// </summary>
    /// <param name="dataIndex">The input data index</param>
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void Caplet(int dataIndex)
    {
      var d = data[dataIndex];
      Dt asOf = Dt.Today();
      var dates = GetDates(asOf, d.time);
      var dayCount = DayCount.Actual360;

      var calc = GetCalculator(d);
      var n = calc.Count;
      for (int i = 0; i < n; ++i)
      {
        var begin = dates[i];
        for (int j = i + 1; j < n; ++j)
        {
          var end = dates[j];
          var frac = Dt.Fraction(begin, end, dayCount);
          var rate = (d.ZeroPrices[i]/d.ZeroPrices[j] - 1)/frac;
          var strike = rate*1.01;
          var premium = calc.EvaluateCaplet(i, j, strike, frac,
            d.ZeroPrices, d.time);

          var normalizedPrice = premium/(d.ZeroPrices[j]*frac);
          var impliedNormalVol = BlackNormal.ImpliedVolatility(
            OptionType.Call, d.time[i], 0, rate, strike, normalizedPrice);
          var approxBlackVol = Math.Abs(1 + frac*rate)/frac
            *calc.CalculateCapletVolatility(i, j)/Math.Sqrt(d.time[i]);
          Assert.AreEqual(impliedNormalVol, approxBlackVol,
            impliedNormalVol*2E-3, "Implied Volatility");

          var approxPremium = d.ZeroPrices[j]*frac*BlackNormal.P(
            OptionType.Call, d.time[i], 0, rate, strike, approxBlackVol);
          Assert.AreEqual(premium, approxPremium, premium*2E-3, "Premium");
        }
      }
    }

    #endregion

    #region Swaption tests

    /// <summary>
    ///  Compare the swaption values from Hull-White models
    ///  with those calculated by approximation.
    /// </summary>
    /// <param name="dataIndex">The input data index</param>
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void Swaption(int dataIndex)
    {
      var d = data[dataIndex];
      Dt asOf = Dt.Today();
      var dates = GetDates(asOf, d.time);
      var dayCount = DayCount.Actual360;

      var calc = GetCalculator(d);
      var n = calc.Count;

      var fractions = new double[n];
      for (int i = 0; i < n; ++i)
      {
        fractions[i] = Dt.Fraction(
          i == 0 ? asOf : dates[i - 1], dates[i], dayCount);
      }

      for (int i = 0; i < n; ++i)
      {
        var begin = dates[i];
        for (int j = i + 1; j < n; ++j)
        {
          var end = dates[j];
          var frac = Dt.Fraction(begin, end, dayCount);
          var rate = (d.ZeroPrices[i]/d.ZeroPrices[j] - 1)/frac;
          var strike = rate*1.01;
          var premium = calc.EvaluateSwaptionPayer(i, j, strike,
            d.ZeroPrices, d.time, fractions);
          var approx = ApproximateSwaptionPayer(
            i, j, strike, calc, fractions, d.ZeroPrices);
          if (j - i == 1)
            Assert.AreEqual(premium, approx, premium*1E-12, "Premium");
          else
            Assert.AreEqual(premium, approx, premium*1E-2, "Premium");
        }
      }
    }

    /// <summary>
    ///  Evaluate the swaption by freezing the initial discounting weights
    ///  <math>\begin{align}
    ///   \mathrm{Swaption}&amp;\mathrm{Payer}(K, T_i, T_j)
    ///     \equiv \mathrm{E}\left[P(0, T_i) (1 - R(T_i))^+ \right]
    ///     \\ &amp;\approx P(0, T_i)
    ///      \,\mathrm{Black}\!\left(1, R(0), \sqrt{V_R(0, T_i)}\right)
    ///    \\ V_R(0, T_i) &amp;= \left(
    ///      \sum_{k=i+1}^j\frac{c_k\,R_k(0)}{R(0)}B(T_i, T_k)\right)^2 V_r(0,T_i)
    ///    \\ R(t) &amp;= \sum_{k=i+1}^j c_k\,R_k(t)
    ///      ,\quad R_k(t) = \frac{P(t,T_k)}{P(t,T_i)}
    ///    \\ c_k &amp;= K\,\delta(T_{k-1},T_k)\quad k = i+1,\ldots, j-1
    ///    \\ c_j &amp;= 1 + K\,\delta(T_{j-1},T_j)
    ///  \end{align}</math>
    /// </summary>
    /// <param name="i">The index of the forward swap starting date</param>
    /// <param name="j">The index of the forward swap maturity date</param>
    /// <param name="strike">The fixed coupon level</param>
    /// <param name="calc">The short rate volatility calculator</param>
    /// <param name="fractions">The array of fractions by end dates</param>
    /// <param name="zeroPrices">The array of zero prices by dates</param>
    /// <returns>swaption value</returns>
    private static double ApproximateSwaptionPayer(
      int i, int j, double strike,
      HullWhiteShortRatePcpCalculator calc,
      IReadOnlyList<double> fractions,
      IReadOnlyList<double> zeroPrices)
    {
      var pi = (i < 0 ? 1.0 : zeroPrices[i]);
      double sumPcB = 0, sumPc = 0;
      for (int k = i + 1; k <= j; ++k)
      {
        var cB = strike*fractions[k]*zeroPrices[k];
        sumPcB += cB*calc.CalculateB(i, k);
        sumPc += cB;
      }
      sumPcB += zeroPrices[j]*calc.CalculateB(i, j);
      sumPc += zeroPrices[j];

      var v = (sumPcB/sumPc)*Math.Sqrt(calc.CalculateShortRateVariance(-1, i));
      return pi*QMath.Black(1, sumPc/pi, v);
    }

    #endregion

    #region Simple Utilities

    private static double[] CreateZeroPrices(
      double[] time, double[] f)
    {
      var n = time.Length;
      var zeroPrices = new double[n];

      double df = 1, t0 = 0;
      for (int i = 0; i < n; ++i)
      {
        df *= Math.Exp(-f[i]*(time[i] - t0));
        zeroPrices[i] = df;
        t0 = time[i];
      }

      return zeroPrices;
    }

    private static Dt[] GetDates(Dt asOf, double[] times)
    {
      var n = times.Length;
      var dates = new Dt[n];
      for (int i = 0; i < n; ++i)
      {
        dates[i] = new Dt(asOf, times[i]);
      }
      return dates;
    }

    #endregion

    #region Data

    private HullWhiteShortRatePcpCalculator GetCalculator(Data d)
    {
      var calc = _calculator;
      if (calc != null)
      {
        calc.Initialize(d.time, d.sigma, d.meanReversion);
        return calc;
      }
      return _calculator = HullWhiteShortRatePcpCalculator.Create(
        d.time, d.sigma, d.meanReversion);
    }

    private HullWhiteShortRatePcpCalculator _calculator;

    struct Data
    {
      private double[] _zeroPrices;
      public double[] time, sigma, meanReversion, forward;

      public double[] ZeroPrices
        => _zeroPrices ?? (_zeroPrices = CreateZeroPrices(time, forward));
    }

    private static Data[] data =
    {
      new Data
      {
        time = new[] {0.25, 0.5, 1.0, 2.0, 5.0, 10.0},
        sigma = new[] {0.008, 0.006, 0.005, 0.004, 0.003, 0.002},
        meanReversion = new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        forward = new[] {.005, .01, .02, .025, 0.03, 0.04},
      },
      new Data
      {
        time = new[] {0.25, 0.5, 1.0, 2.0, 5.0, 10.0},
        sigma = new[] {0.010, 0.009, 0.007, 0.006, 0.005, 0.004},
        meanReversion = new[] {0.5, 0.5, 0.5, 0.2, 0.2, 0.2},
        forward = new[] {.005, .01, .02, .025, 0.03, 0.04},
      },

      // With negative rates
      new Data
      {
        time = new[] {0.25, 0.5, 1.0, 2.0, 5.0, 10.0},
        sigma = new[] {0.008, 0.006, 0.005, 0.004, 0.003, 0.002},
        meanReversion = new[] {0.0, 0.0, 0.0, 0.0, 0.0, 0.0},
        forward = new[] {-.005, -.002, -0.001, .0125, 0.03, 0.04},
      },
      new Data
      {
        time = new[] {0.25, 0.5, 1.0, 2.0, 5.0, 10.0},
        sigma = new[] {0.010, 0.009, 0.007, 0.006, 0.005, 0.004},
        meanReversion = new[] {0.5, 0.5, 0.5, 0.2, 0.2, 0.2},
        forward = new[] {-.005, -.002, -0.001, .0125, 0.03, 0.04},
      },
    };

    #endregion
  }

}
