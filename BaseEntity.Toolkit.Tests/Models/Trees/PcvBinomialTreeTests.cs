//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using BaseEntity.Toolkit.Models.Trees;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Tests.Models.Trees
{
  using NUnit.Framework;

  [TestFixture]
  public class PcvBinomialTreeTests
  {
    [TestCase(0.5)]
    [TestCase(0.625)]
    [TestCase(0.75)]
    [TestCase(0.825)]
    [TestCase(1.0)]
    [TestCase(1.25)]
    [TestCase(1.5)]
    [TestCase(1.75)]
    [TestCase(2.0)]
    public static void LogNormalEuropean(double strike)
    {
      // We build a single tree with time varying volatilities.
      // Then we use it to price European options at different
      // expiry dates and requires them to match the analytic results.
      //
      // The purpose it to make sure the tree is time consistent
      // and hence it can be used to price path-dependent options.
      var periods = new[] {1.0, 1.0, 1.0};
      var steps = new[] {1280, 500, 180};
      var volatilities = new[] {0.8, 0.5, 0.3};
      var strikes = new[] {strike, strike, strike};
      var expects = CalculateBlackValues(
        periods.Length, periods, volatilities, strikes);

      var tree = PcvBinomialTree.Build(
        steps, periods, volatilities);
      var actuals = CalculateOptionValuesOnTree(
        tree, steps, strikes);

      Assert.That(actuals, Is.EqualTo(expects).Within(1E-4));
      return;
    }

    [TestCase(0.5)]
    [TestCase(0.625)]
    [TestCase(0.75)]
    [TestCase(0.825)]
    [TestCase(1.0)]
    [TestCase(1.25)]
    [TestCase(1.5)]
    [TestCase(1.75)]
    [TestCase(2.0)]
    public static void NormalEuropean(double strike)
    {
      // We build a single tree with time varying volatilities.
      // Then we use it to price European options at different
      // expiry dates and requires them to match the analytic results.
      //
      // The purpose it to make sure the tree is time consistent
      // and hence it can be used to price path-dependent options.
      var periods = new[] {1.0, 1.0, 1.0};
      var steps = new[] {1280, 500, 180};
      var volatilities = new[] {0.8, 0.5, 0.3};
      var strikes = new[] {strike, strike, strike};
      var expects = CalculateNormalBlackValues(
        periods.Length, periods, volatilities, strikes);

      var tree = PcvBinomialTree.Build(
        steps, periods, volatilities);
      var actuals = CalculateOptionValuesOnTree(
        tree, steps, strikes, true);

      Assert.That(actuals, Is.EqualTo(expects).Within(1E-4));
      return;
    }


    private static double[] CalculateOptionValuesOnTree(
      PcvBinomialTree tree, int[] steps,
      double[] strikes, bool isNormal = false)
    {
      var maps = tree.StepMaps;
      var martingales = new IReadOnlyList<double>[maps.Count];
      int lastMapIndex = maps.Count - 1;
      martingales[lastMapIndex] = isNormal
        ? tree.CalculateNormalTerminalValues(1.0, maps[lastMapIndex])
        : tree.CalculateLogNormalTerminalValues(1.0, maps[lastMapIndex]);
      for (int i = lastMapIndex; --i >= 0;)
      {
        martingales[i] = tree.PerformBackwardInduction(
          maps[i + 1], martingales[i + 1],
          (s, k, p, u, d) => d + p*(u - d), maps[i]);
      }

      var results = new double[steps.Length];
      for (int t = 0; t < maps.Count; ++t)
      {
        var strike = strikes[t];
        int stepIndex = maps[t];
        var probs = tree.GetProbabilities(stepIndex);
        Debug.Assert(probs.Count == stepIndex + 1);
        var values = martingales[t];
        Debug.Assert(values.Count == stepIndex + 1);
        double sum = 0, sump = 0;
        for (int i = 0, n = values.Count; i < n; ++i)
        {
          double p = probs[i];
          sump += p;
          double v = values[i] - strike;
          if (v > 0) sum += p*v;
        }
        results[t] = sum/sump;
      }
      return results;
    }

    private static double[] CalculateNormalBlackValues(
      int count, double[] periods, double[] volatilities,
      double[] strikes)
    {
      var results = new double[count];
      double v2 = 0;
      for (int t = 0; t < count; ++t)
      {
        double v = volatilities[t];
        v2 += periods[t]*v*v;
        var strike = strikes[t];
        results[t] = strike*BlackNormal(1/strike, Math.Sqrt(v2)/strike);
      }
      return results;
    }

    private static double[] CalculateBlackValues(
      int count, double[] periods, double[] volatilities,
      double[] strikes)
    {
      var results = new double[count];
      double v2 = 0;
      for (int t = 0; t < count; ++t)
      {
        double v = volatilities[t];
        v2 += periods[t]*v*v;
        var strike = strikes[t];
        results[t] = strike*Black(1/strike, Math.Sqrt(v2));
      }
      return results;
    }

    public static double BlackNormal(double f, double v)
    {
      f -= 1.0;
      double z = f/v;
      return (v/Constants.Sqrt2Pi)*Math.Exp(-z*z/2)
        + f*SpecialFunctions.NormalCdf(z);
    }

    public static double Black(double f, double v)
    {
      double h = Math.Log(f)/v + v/2;
      return f*SpecialFunctions.NormalCdf(h) - SpecialFunctions.NormalCdf(h - v);
    }
  }
}
