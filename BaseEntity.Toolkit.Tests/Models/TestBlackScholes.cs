//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;

using NUnit.Framework;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestBlackScholes : ToolkitTestBase
  {
    private static readonly double[] sigmas = { 0.05, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8 };
    private static readonly double[] moneyness = { 0.5, 0.6, 0.7, 0.8, 0.9, 0.95, 1.0, 1.05, 1.1, 1.2, 1.3, 1.4, 1.5 };

    [Test]
    public void ConvertNormalVolatility()
    {
      int m = sigmas.Length, n = moneyness.Length;
      double time = 0.1, f = 1.2, r = 0.0, q = 0.0;
      for (int i = 0; i < m; ++i)
      {
        double sigma = sigmas[i];
        for (int j = 0; j < n; ++j)
        {
          double k = moneyness[j]*f;
          double p = BlackScholes.P(OptionStyle.European, OptionType.Call, time, f, k, r, q, sigma);
          double sn = BlackScholes.ToNormalVolatility(sigma, time, f, k);
          double pn = BlackNormal.P(OptionType.Call, time, r, f, k, sn);
          Assert.AreEqual(p, pn, 1E-9, "Price");

          double sb = BlackScholes.FromNormalVolatility(sn, time, f, k);
          Assert.AreEqual(sigma, sb, 1E-9, "Sigma");
        }
      }
    }

    [Test]
    public void ImpliedVolatilityAccuracy()
    {
      ImpliedVolatility(false);
    }

    [Test, Category("Timing")]
    public void ImpliedVolatilityTiming()
    {
      ImpliedVolatility(true);
    }

    private void ImpliedVolatility(bool checkTiming)
    {
      int m = sigmas.Length, n = moneyness.Length;
      double[,] prices = new double[m, n], results0 = new double[m, n],
        results1 = new double[m, n], results2 = new double[m, n];

      if (checkTiming)
      {
        GC.Collect();
        System.Threading.Thread.Sleep(100);
      }

      double time = 1.2, spot = 1.0, r = 0.02, q = 0.01;
      for (int i = 0; i < m; ++i)
      {
        double sigma = sigmas[i];
        for (int j = 0; j < n; ++j)
        {
          double k = moneyness[j];
          prices[i, j] = BlackScholes.P(OptionStyle.European, OptionType.Call, time, spot, k, r, q, sigma);
        }
      }

      double time0 = Timing(() =>
      {
        for (int ii = 0; ii < 50 * m; ++ii)
        {
          int i = ii % m;
          for (int jj = 0; jj < 50 * n; ++jj)
          {
            int j = jj % n;
            double k = moneyness[j];
            results0[i, j] = NewImpliedVolaility(time, spot, k, r, q, prices[i, j], 0);
          }
        }
      });

      double time1 = Timing(() =>
      {
        for (int ii = 0; ii < 50 * m; ++ii)
        {
          int i = ii % m;
          for (int jj = 0; jj < 50 * n; ++jj)
          {
            int j = jj % n;
            double k = moneyness[j];
            results1[i, j] = NewImpliedVolaility(time, spot, k, r, q, prices[i, j], 1E-8);
          }
        }
      });

      double time2 = Timing(() =>
      {
        for (int ii = 0; ii < 50 * m; ++ii)
        {
          int i = ii % m;
          for (int jj = 0; jj < 50 * n; ++jj)
          {
            int j = jj % n;
            double k = moneyness[j];
            results2[i, j] = BlackScholes.ImpliedVolatility(OptionStyle.European, OptionType.Call,
              time, spot, k, r, q, prices[i, j]);
          }
        }
      });

      if (checkTiming)
      {
        // With the same accuracy, the new implied volatility is at least 2 times faster the old one.
        Assert.Greater(time2, 2*time1, String.Format(
          "Old time {0}, new time {1}", time2, time1));

        // The new implied volatility with machine accuracy is at least 1.5 times faster the old one.
        Assert.Greater(time2, 1.5*time0, String.Format(
          "Old time {0}, new time {1}", time2, time0));
      }

      // The new implied volatility has 10 times less error in prices  the old one.
      var err1 = MaxError(OptionType.Call, results1, time, spot, r, q);
      var err2 = MaxError(OptionType.Call, results2, time, spot, r, q);
      Assert.Greater(err2, err1 * 10, String.Format(
        "Old error {0}, new error {1}", err2, err1));

      // Do we have it close to machine accuracy?
      var err0 = MaxError(OptionType.Call, results0, time, spot, r, q);
      Assert.Greater(2E-15, err0, String.Format(
        "Old error {0}, new error {1}", err2, err0));
    }

    [Test]
    public void ImpliedVolatilityCall()
    {
      ImpliedVolatility(OptionType.Call, false);
    }

    [Test]
    public void ImpliedVolatilityPut()
    {
      ImpliedVolatility(OptionType.Put, false);
    }

    [Test, Category("Timing")]
    public void ImpliedVolatilityCallPutTiming()
    {
      ImpliedVolatility(OptionType.Call, true);
      ImpliedVolatility(OptionType.Put, true);
    }

    private void ImpliedVolatility(OptionType otype, bool timing)
    {
      int m = sigmas.Length, n = moneyness.Length;
      double[,] prices = new double[m, n], 
        results1 = new double[m, n], results2 = new double[m, n];
      GC.Collect();
      System.Threading.Thread.Sleep(100);

      double time = 1.2, spot = 1.0, r = 0.02, q = 0.01;
      for (int i = 0; i < m; ++i)
      {
        double sigma = sigmas[i];
        for (int j = 0; j < n; ++j)
        {
          double k = moneyness[j];
          prices[i,j] = BlackScholes.P(OptionStyle.European, otype, time, spot, k, r, q, sigma);
        }
      }

      double time1 = Timing(() =>
      {
        for (int ii = 0; ii < 50 * m; ++ii)
        {
          int i = ii % m;
          for (int jj = 0; jj < 50 * n; ++jj)
          {
            int j = jj % n;
            double k = moneyness[j];
            results1[i, j] = BlackScholes.TryImplyVolatility(
              OptionStyle.European, otype, time, spot, k, r, q,
              prices[i, j]);
          }
        }
      });

      double time2 = Timing(() =>
      {
        for (int ii = 0; ii < 50 * m; ++ii)
        {
          int i = ii % m;
          for (int jj = 0; jj < 50 * n; ++jj)
          {
            int j = jj % n;
            double k = moneyness[j];
            try
            {
              results2[i, j] = BlackScholes.ImpliedVolatility(OptionStyle.European, otype,
                time, spot, k, r, q, prices[i, j]);
            }
            catch (ApplicationException)
            {
              results2[i, j] = Double.NaN;
            }
          }
        }
      });

      // With the same accuracy, the new implied volatility is at least 2 times faster the old one.
      if (timing)
      {
        Assert.Greater(time2, 2*time1, String.Format(
          "Old time {0}, new time {1}", time2, time1));
      }

      // The new implied volatility has 10 times less error in prices  the old one.
      var err1 = MaxError(otype, results1, time, spot, r, q);
      var err2 = MaxError(otype, results2, time, spot, r, q);
      Assert.Greater(err2, err1*10, String.Format(
        "Old error {0}, new error {1}", err2, err1));
    }

    private static double MaxError(OptionType otype, double[,] v, double time, double spot, double r, double q)
    {
      double max = 0;
      int m = sigmas.Length, n = moneyness.Length;
      for (int i = 0; i < m; ++i)
      {
        double sigma = sigmas[i];
        for (int j = 0; j < n; ++j)
        {
          if (!(v[i,j] > 0)) continue;
          double k = moneyness[j];
          double expect = BlackScholes.P(OptionStyle.European, otype, time, spot, k, r, q, sigma);
          double actual = BlackScholes.P(OptionStyle.European, otype, time, spot, k, r, q, v[i,j]);
          double err = Math.Abs(actual - expect);
          if (err > max) max = err;
        }
      }
      return max;
    }

    private static double NewImpliedVolaility(double T, double S, double K,
      double r, double q, double price, double eps)
    {
      var a = S * Math.Exp(-q * T);
      var m = K * Math.Exp(-r * T) / a;
      var v = SpecialFunctions.BlackScholesImpliedVolatility(true, price / a, m, eps*a);
      return v / Math.Sqrt(T);
    }
  }
}
