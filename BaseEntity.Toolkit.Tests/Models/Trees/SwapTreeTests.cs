// 
// Copyright (c)    2002-2016. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using BaseEntity.Toolkit.Models.Trees;
using BaseEntity.Toolkit.Numerics;
using Distribution = BaseEntity.Toolkit.Calibrators.Volatilities.DistributionType;
using Ax = BaseEntity.Toolkit.Util.Collections.ListUtil;

namespace BaseEntity.Toolkit.Tests.Models.Trees
{
  using NUnit.Framework;

  [TestFixture]
  public class SwapTreeTests
  {
    [Test]
    public static void SwaptionValues()
    {
      var steps = new[] {1280, 980, 720};
      var tenors = new[] {1.0, 2.0, 3.0, 4.0};
      var zeroPrices = new[] {1.0, 1/1.02, 1/1.04/1.02, 1/1.05/1.04/1.02};
      var sigmas = new[] {0.8, 0.7, 0.6};
      var betas = new[] {1.0, 0.75, 0.7};

      double swapRate, swapAnnuity;
      //CalculateSwapRateAndAnnuity(0, rates, out swapRate, out swapAnnuity);

      int rateCount = zeroPrices.Length - 1;
      var periods = Ax.NewArray(rateCount,
        i => tenors[i] - (i == 0 ? 0 : tenors[i - 1]));
      var swapVolatility = CalculateVolatility(0,
        Ax.CreateList(rateCount,
          i => zeroPrices[i]/zeroPrices[i + 1] - 1),
        periods, sigmas, betas,
        out swapRate, out swapAnnuity);
      swapAnnuity *= zeroPrices[rateCount];
      var strike = swapRate;
      var swaptionPv = swapAnnuity*SpecialFunctions.Black(
        swapRate, strike, swapVolatility);

      var tree = LmmBinomialTree.Create(tenors, zeroPrices, steps,
        Distribution.LogNormal, betas,
        BlackVolatility(tenors, sigmas));

      var swapRateAnnuities = tree.CalculateSwapRates(0, periods);
      var swapAnnuityByTree = tree.CalculateExpectationAtExpiry(
        0, i => swapRateAnnuities[i].Annuity);
      var swapRateByTree = tree.CalculateExpectationAtExpiry(
        0, i => swapRateAnnuities[i].Value)/swapAnnuityByTree;
      Assert.That(swapAnnuityByTree, Is.EqualTo(swapAnnuity).Within(1E-14));
      Assert.That(swapRateByTree, Is.EqualTo(swapRate).Within(1E-14));

      var swaptionPvByTree = tree.CalculateExpectationAtExpiry(
        0, i => swapRateAnnuities[i].Intrinsic(strike, 1));
      Assert.That(swaptionPvByTree, Is.EqualTo(swaptionPv).Within(5E-4));
    }

    private static Func<double, double> BlackVolatility(
      double[] ts, double[] sigmas)
    {
      Debug.Assert(ts != null && ts.Length > 0);
      Debug.Assert(sigmas != null && sigmas.Length > 0);
      var n = (ts.Length > sigmas.Length ? sigmas.Length : ts.Length);
      var vs = new double[n];
      double v0 = 0, t0 = 0;
      for (int i = 0; i < n; ++i)
      {
        v0 += sigmas[i]*sigmas[i]*(ts[i] - t0);
        vs[i] = v0;
        t0 = ts[i];
      }
      return t =>
      {
        if (t <= ts[0]) return sigmas[0];
        int last = n - 1;
        for (int i = 0; i < last; ++i)
        {
          if (t > ts[i + 1]) continue;
          var v2 = vs[i] + (t - ts[i])/(ts[i + 1] - ts[i])*(vs[i + 1] - vs[i]);
          return Math.Sqrt(v2/t);
        }
        return sigmas[last];
      };
    }

    private static Func<double, double> Flat(
      double[] xs, double[] ys, bool leftContinues = false)
    {
      Debug.Assert(xs != null && xs.Length > 0);
      Debug.Assert(ys != null && ys.Length > 0);
      var n = (xs.Length > ys.Length ? ys.Length : xs.Length) - 1;
      return x =>
      {
        if (leftContinues)
        {
          for (int i = 0; i < n; ++i)
            if (x < xs[i + 1]) return ys[i];
          return ys[n];
        }
        else
        {
          for (int i = n; --i >= 0;)
            if (x > xs[i]) return ys[i + 1];
          return ys[0];
        }
      };
    }

    private static double CalculateVolatility(
      int startRateIndex,
      IReadOnlyList<double> rates,
      IReadOnlyList<double> periods,
      IReadOnlyList<double> commonVolatilities,
      IReadOnlyList<double> betas,
      out double swapRate, out double swapAnnuity)
    {
      Debug.Assert(startRateIndex >= 0);
      Debug.Assert(startRateIndex < rates.Count);
      Debug.Assert(rates.Count == commonVolatilities.Count);
      Debug.Assert(rates.Count == betas.Count);

      // Calculate the common variance
      double commonVariance = 0;
      for (int i = 0; i <= startRateIndex; ++i)
      {
        var v = commonVolatilities[i];
        commonVariance += periods[i]*v*v;
      }

      var annuities = new double[rates.Count];
      double enume = 0, denom = 0, sumv2 = 0, annuity = 1.0;
      for (int i = rates.Count; --i >= startRateIndex;)
      {
        denom += (annuities[i] = annuity);

        var sum = annuity*rates[i]*betas[i];
        for (int j = rates.Count; --j > i;)
        {
          sum += 2*annuities[j]*rates[j]*betas[j];
        }
        sumv2 += sum*annuity*rates[i]*betas[i];
        enume += rates[i]*annuity;
        annuity *= (1 + rates[i]);
      }

      swapRate = enume/denom;
      swapAnnuity = denom; // denom/annuity for spot measure
      var swapVolatility = Math.Sqrt(sumv2*commonVariance)/enume;
      return swapVolatility;
    }
  }
}
