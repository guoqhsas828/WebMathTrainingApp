//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Models.Trees;
using DistributionKind = BaseEntity.Toolkit.Calibrators.Volatilities.DistributionType;
using ArrayUtility = BaseEntity.Toolkit.Util.Collections.ListUtil;

namespace BaseEntity.Toolkit.Tests.Models.Trees
{
  using NUnit.Framework;

  [TestFixture(CaseName.LogNormal)]
  [TestFixture(CaseName.Normal)]
  public class LmmBinomialTreeTests
  {
    private readonly Case _data;

    public LmmBinomialTreeTests(CaseName name)
    {
      _data = Data.First(d => d.Match(name));
    }

    [Test]
    public void StoreBetasByReference()
    {
      var data = _data;
      var tree = data.BuildTree(10);
      Assert.That(tree.Betas, Is.SameAs(data.Sigmas));
    }

    [Test]
    public void ResetTimesAndSteps()
    {
      var data = _data;
      var tree = data.BuildTree(20);
      var tenors = data.Tenors;
      var rates = data.Rates;
      Debug.Assert(rates.Count == tenors.Length - 1);
      for (int i = 0, n = rates.Count; i < n; ++i)
      {
        var rateIndex = tree.GetCurrentRateIndexAtTime(tenors[i]);
        Assert.That(rateIndex, Is.EqualTo(rateIndex), "Rate Index");
        var time = tree.GetResetTime(rateIndex);
        Assert.That(time, Is.EqualTo(tenors[i]), "Reset Time");
        var steps = tree.GetStepIndexAtTime(tenors[i]);
        var count = tree.GetResetStepCount(i);
        Assert.That(steps, Is.EqualTo(count), "Step Count");
      }
    }

    public void StepIndexAtTime()
    {

    }

    [Test]
    public void RoundTripLiborRates()
    {
      var data = _data;
      var tree = data.BuildTree(500);
      var fractions = data.Fractions;
      var dfs = data.DiscountFactors;
      var initialRates = data.Rates;
      int index = initialRates.Count;
      foreach (var rates in tree.EnumerateRates())
      {
        --index;
        var annuity = tree.CalculateExpectationAtExpiry(
          index, i => rates[i].Annuity);
        var floatValue = tree.CalculateExpectationAtExpiry(
          index, i => rates[i].Value);
        var rate = floatValue/annuity/fractions[index];
        Assert.That(annuity, Is.EqualTo(dfs[index + 1]).Within(5E-8), "Annuity");
        Assert.That(rate, Is.EqualTo(initialRates[index]).Within(5E-8), "Rate");
        Assert.IsTrue(rates.All(r => r.Annuity >= 0));
      }
    }

    #region Test data

    private static readonly Case[] Data =
    {
      new Case(
        name: CaseName.LogNormal,
        tenors: new[] {1.0, 2.0, 5.0, 10, 15, 20},
        rates: new[] {0.01, 0.02, 0.025, 0.03, 0.04, 0.05},
        sigmas: new[] {0.9, 0.8, 0.7, 0.6, 0.5},
        distribution: DistributionKind.LogNormal),
      new Case(
        name: CaseName.Normal,
        tenors: new[] {1.0, 2.0, 5.0, 10, 15, 20},
        rates: new[] {0.01, 0.02, 0.025, 0.03, 0.04, 0.05},
        sigmas: new[] {0.015, 0.015, 0.015, 0.015, 0.015},
        distribution: DistributionKind.Normal),
    };

    #endregion

    #region Nested types

    public enum CaseName
    {
      LogNormal,
      Normal,
      ShifitedLogNormal
    }

    private class Case
    {
      public Case(CaseName name,
        double[] tenors,
        double[] rates,
        double[] sigmas,
        DistributionKind distribution)
      {
        Name = name;
        Tenors = tenors;
        _rates = rates;
        Sigmas = sigmas;
        Distribution = distribution;
      }

      public bool Match(CaseName name)
      {
        return Name == name;
      }

      public LmmBinomialTree BuildTree(int stepsPerYear)
      {
        var steps = ArrayUtility.NewArray(Sigmas.Length,
          i => (int) Math.Ceiling(stepsPerYear*Interval(i)));
        return LmmBinomialTree.Create(Tenors, DiscountFactors,
          steps, Distribution, Sigmas);
      }

      public IReadOnlyList<double> Fractions
      {
        get
        {
          var t = Tenors;
          return ArrayUtility.NewArray(t.Length - 1, i => t[i + 1] - t[i]);
        }
      }

      public IReadOnlyList<double> DiscountFactors
      {
        get
        {
          return ArrayUtility.PartialSums(_rates.Length,
            (v, i) => v/(1 + ScaledRate(i)), 1.0);
        }
      }

      public IReadOnlyList<double> Rates
      {
        get { return ArrayUtility.CreateList(_rates.Length - 1, i => _rates[i + 1]); }
      }

      private double ScaledRate(int i)
      {
        return _rates[i]*Interval(i);
      }

      private double Interval(int i)
      {
        return (i == 0 ? Tenors[i] : (Tenors[i] - Tenors[i - 1]));
      }

      private readonly CaseName Name;
      public readonly double[] Tenors;
      private readonly double[] _rates;
      public readonly double[] Sigmas;
      private readonly DistributionKind Distribution;
    }

    #endregion

  }
}
