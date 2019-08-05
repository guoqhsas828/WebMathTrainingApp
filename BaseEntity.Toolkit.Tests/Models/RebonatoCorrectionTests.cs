//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Models.BGM;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class RebonatoCorrectionTests
  {
    #region Evaluation routines

    internal static double[] CalculateZeroPrices(
      IReadOnlyList<double> rates,
      IReadOnlyList<double> fractions)
    {
      var prices = new double[rates.Count];
      double price = 1.0;
      for (int i = 0; i < rates.Count; ++i)
      {
        price /= 1 + fractions[i] * rates[i];
        prices[i] = price;
      }
      return prices;
    }

    internal static double CalculateSwapRate(
      IReadOnlyList<double> discountRates,
      IReadOnlyList<double> projectRates,
      IReadOnlyList<double> notional,
      IReadOnlyList<double> fractions)
    {
      double price = 1.0, level = 0, floatValue = 0;
      for (int i = 0, n = discountRates.Count; i < n; ++i)
      {
        var frac = fractions[i];
        price /= 1 + frac * discountRates[i];
        var ntnl = frac * notional[i];
        level += ntnl * price;
        floatValue += ntnl * projectRates[i] * price;
      }
      return floatValue / level;
    }

    internal static double[] CalculateDerivativesFiniteDifference(
      IReadOnlyList<double> discountRates,
      IReadOnlyList<double> projectRates,
      IReadOnlyList<double> notional,
      IReadOnlyList<double> fractions)
    {
      var n = discountRates.Count;
      var bumpedDiscountRates = new double[n];
      var bumpedProjectRates = new double[n];
      for (int i = 0; i < n; ++i)
      {
        bumpedDiscountRates[i] = discountRates[i];
        bumpedProjectRates[i] = projectRates[i];
      }

      var derivatives = new double[n];
      for (int i = 0; i < n; ++i)
      {
        bumpedDiscountRates[i] = discountRates[i] + 1E-7;
        bumpedProjectRates[i] = projectRates[i] + 1E-7;
        var upValue = CalculateSwapRate(
          bumpedDiscountRates, bumpedProjectRates,
          notional, fractions);

        bumpedDiscountRates[i] = discountRates[i] - 1E-7;
        bumpedProjectRates[i] = projectRates[i] - 1E-7;
        var downValue = CalculateSwapRate(
          bumpedDiscountRates, bumpedProjectRates,
          notional, fractions);

        derivatives[i] = (upValue - downValue) / 2E-7;

        bumpedDiscountRates[i] = discountRates[i];
        bumpedProjectRates[i] = projectRates[i];
      }
      return derivatives;
    }

    internal static double[] CalculateDerivativesAnalytic(
      double[] discountRates,
      double[] projectRates,
      double[] notional,
      double[] fractions)
    {
      var derivatives = new double[discountRates.Length];
      double level = 0, swapRate = 0;
      BgmCalibrations.CalculateDerivatives(
        discountRates, projectRates, notional, fractions,
        derivatives, ref level, ref swapRate);
      return derivatives;
    }

    #endregion

    public enum RateSpec
    {
      ConstantRate,
      IncreasingRate,
      DecreasingRate,
      RandomRate,
    }

    public enum NotionalSpec
    {
      Regular,
      Amortizing,
      Accreting,
    }

    [TestCase(NotionalSpec.Regular, RateSpec.ConstantRate, 10, 0.03, 0.0)]
    [TestCase(NotionalSpec.Regular, RateSpec.ConstantRate, 10, 0.03, 0.01)]
    [TestCase(NotionalSpec.Regular, RateSpec.IncreasingRate, 10, 0.03, 0.0)]
    [TestCase(NotionalSpec.Regular, RateSpec.IncreasingRate, 10, 0.03, 0.01)]
    [TestCase(NotionalSpec.Regular, RateSpec.DecreasingRate, 10, 0.03, 0.0)]
    [TestCase(NotionalSpec.Regular, RateSpec.DecreasingRate, 10, 0.03, 0.01)]
    [TestCase(NotionalSpec.Regular, RateSpec.RandomRate, 10, 0.03, 0.0)]
    [TestCase(NotionalSpec.Regular, RateSpec.RandomRate, 10, 0.03, 0.01)]
    [TestCase(NotionalSpec.Amortizing, RateSpec.ConstantRate, 10, 0.03, 0.0)]
    [TestCase(NotionalSpec.Amortizing, RateSpec.ConstantRate, 10, 0.03, 0.01)]
    [TestCase(NotionalSpec.Amortizing, RateSpec.IncreasingRate, 10, 0.03, 0.0)]
    [TestCase(NotionalSpec.Amortizing, RateSpec.IncreasingRate, 10, 0.03, 0.01)]
    [TestCase(NotionalSpec.Amortizing, RateSpec.DecreasingRate, 10, 0.03, 0.0)]
    [TestCase(NotionalSpec.Amortizing, RateSpec.DecreasingRate, 10, 0.03, 0.01)]
    [TestCase(NotionalSpec.Amortizing, RateSpec.RandomRate, 10, 0.03, 0.0)]
    [TestCase(NotionalSpec.Amortizing, RateSpec.RandomRate, 10, 0.03, 0.01)]
    [TestCase(NotionalSpec.Accreting, RateSpec.ConstantRate, 10, 0.03, 0.0)]
    [TestCase(NotionalSpec.Accreting, RateSpec.ConstantRate, 10, 0.03, 0.01)]
    [TestCase(NotionalSpec.Accreting, RateSpec.IncreasingRate, 10, 0.03, 0.0)]
    [TestCase(NotionalSpec.Accreting, RateSpec.IncreasingRate, 10, 0.03, 0.01)]
    [TestCase(NotionalSpec.Accreting, RateSpec.DecreasingRate, 10, 0.03, 0.0)]
    [TestCase(NotionalSpec.Accreting, RateSpec.DecreasingRate, 10, 0.03, 0.01)]
    [TestCase(NotionalSpec.Accreting, RateSpec.RandomRate, 10, 0.03, 0.0)]
    [TestCase(NotionalSpec.Accreting, RateSpec.RandomRate, 10, 0.03, 0.01)]
    public static void Derivatives(
      NotionalSpec nspec, RateSpec rspec,
      int n, double meanRate, double basis)
    {
      var notional = GetNotional(n, nspec);

      var discountRates = new double[n];
      var projectRates = new double[n];
      FillRates(rspec, meanRate, basis, discountRates, projectRates);

      var prices = new double[n];
      var fractions = new double[n];
      double price = 1.0, level = 0;
      for (int i = 0; i < n; ++i)
      {
        var rate = discountRates[i];
        var frac = fractions[i] = 0.25 * (1 + (i % 3 - 1) / 100.0);
        price /= 1 + frac * rate;
        prices[i] = price;
        level += frac * price * notional[i];
      }

      var weights = new double[n];
      for (int i = 0; i < n; ++i)
        weights[i] = notional[i] * fractions[i] * prices[i] / level;

      var expects = CalculateDerivativesFiniteDifference(
        discountRates, projectRates, notional, fractions);
      if (rspec == RateSpec.ConstantRate)
      {
        Assert.That(expects, Is.EqualTo(weights).Within(5E-10));
      }
      var derivatives = CalculateDerivativesAnalytic(
        discountRates, projectRates, notional, fractions);
      var sum = derivatives.Sum();
      Assert.That(derivatives, Is.EqualTo(expects).Within(5E-10));
    }

    private static void FillRates(RateSpec spec,
      double rate, double basis,
      double[] discountRates, double[] projectRates)
    {
      var rnd = new Random();
      int n = discountRates.Length;
      if (spec == RateSpec.RandomRate)
      {
        for (int i = 0; i < n; ++i)
        {
          var r = discountRates[i] = rate * 2 * (0.5 - rnd.NextDouble());
          projectRates[i] = r + basis * 2 * (0.5 - rnd.NextDouble());
        }
        return;
      }

      double endRate, basisSpread;
      switch (spec)
      {
        case RateSpec.IncreasingRate:
          endRate = Math.Max(0.01, Math.Abs(rate));
          basisSpread = basis;
          break;
        case RateSpec.DecreasingRate:
          endRate = rate - Math.Max(0.01, Math.Abs(rate));
          basisSpread = basis;
          break;
        default:
          endRate = rate;
          basisSpread = 0;
          break;
      }

      for (int i = 0; i < n; ++i)
      {
        var r = discountRates[i] = rate + (endRate - rate) * i / n;
        projectRates[i] = r + basis * (1 + basisSpread * (0.5 - rnd.NextDouble()));
      }
      return;
    }

    private static double[] GetNotional(int n, NotionalSpec spec)
    {
      double endNotional;
      switch (spec)
      {
        case NotionalSpec.Amortizing:
          endNotional = 0.2;
          break;
        case NotionalSpec.Accreting:
          endNotional = 5;
          break;
        default:
          endNotional = 1;
          break;
      }
      var notional = new double[n];
      for (int i = 0; i < n; ++i)
        notional[i] = 1 + (endNotional - 1) * i / n;
      return notional;
    }
  }
}
