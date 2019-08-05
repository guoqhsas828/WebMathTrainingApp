//
// Compare various basket loss distribution results
// Copyright (c)    2002-2018. All rights reserved.
//

// Enable this test efficiency
//#define TEST_TIMING

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;
using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;
using static BaseEntity.Toolkit.Tests.Helpers.Legacy.Assertions;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture, Smoke]
  public class TestComovementBasketModel : ToolkitTestBase
  {
    #region Setup
    [OneTimeSetUp]
    public void SetUp()
    {
      asOf_ = Dt.Today();
      date_ = Dt.Add(asOf_, "5Y");
    }
    #endregion Setup

    #region Data
    private Dt asOf_;
    private Dt date_;
    private double gridSize_ = 0;
    private double correlation_ = 1;
    #endregion Data

    #region Helpers
    SurvivalCurve CreateSurvivalCurve(double sp)
    {
      SurvivalCurve curve = new SurvivalCurve(asOf_);
      curve.Add(date_, sp);
      return curve;
    }

    Curve2D CreateCurve2D(double[] levels)
    {
      Curve2D dist = new Curve2D(asOf_);
      dist.Initialize(2, levels.Length);
      dist.SetDate(0, asOf_);
      dist.SetDate(1, date_);
      for (int i = 0; i < levels.Length; ++i)
        dist.SetLevel(i, levels[i]);
      return dist;
    }

    /// <summary>
    ///   Check distribution with a single uniform survival curves
    /// </summary>
    /// <param name="sp">Survival probability at maturity</param>
    /// <param name="recoveryRate">recovery rate</param>
    /// <param name="shortNames">number of short names</param>
    /// <param name="maturedNames">number of names matured at the as-of date</param>
    /// <param name="withRefinance">include riskless refinance curves to test LCDO engine</param>
    void CheckUniformSurvivalProbability(
      double sp, double recoveryRate,
      int shortNames, int maturedNames,
      bool withRefinance)
    {
      CheckUniformSurvivalProbability(sp, recoveryRate, shortNames, maturedNames, withRefinance, false);
    }


    /// <summary>
    ///   Check distribution with a single uniform survival curves
    /// </summary>
    /// <param name="sp">Survival probability at maturity</param>
    /// <param name="recoveryRate">recovery rate</param>
    /// <param name="shortNames">number of short names</param>
    /// <param name="maturedNames">number of names matured at the as-of date</param>
    /// <param name="withRefinance">include riskless refinance curves to test LCDO engine</param>
    /// <param name="useSemiAnalytic">use semi-analytic model</param>
    void CheckUniformSurvivalProbability(
      double sp, double recoveryRate,
      int shortNames, int maturedNames,
      bool withRefinance, bool useSemiAnalytic)
    {
      const int N = 10;
      const double tolerance = 1E-10;
      double[] levels = new double[N + 1];
      for (int i = 0; i <= N; ++i)
        levels[i] = 1.0 * i / N;
      Curve2D lossDistribution = CreateCurve2D(levels);
      Curve2D amorDistribution = CreateCurve2D(levels);

      double[] recoveryRates = new double[N];
      double[] recoveryDispersions = new double[N];
      double[] principals = new double[N];
      for (int i = 0; i < N; ++i)
      {
        recoveryRates[i] = recoveryRate;
        principals[i] = i < shortNames ? -1.0 : 1.0;
      }

      SurvivalCurve[] survivalCurves = new SurvivalCurve[N];
      SurvivalCurve sc = CreateSurvivalCurve(sp);
      for (int i = 0; i < N; ++i)
        survivalCurves[i] = sc;

      SurvivalCurve[] refinanceCurves = new SurvivalCurve[0];
      if (withRefinance)
      {
        // Create a curve with zero refinance probability
        refinanceCurves = new SurvivalCurve[N];
        SurvivalCurve rc = CreateSurvivalCurve(1.0);
        for (int i = 0; i < N; ++i)
          refinanceCurves[i] = rc;
      }

      double[] earlymaturites = new double[0];
      if (maturedNames > 0)
      {
        earlymaturites = new double[N];
        for (int i = 1; i <= maturedNames; ++i)
          earlymaturites[N - i] = asOf_.ToDouble();
      }
      double maturedLevel = Math.Min(1.0, ((double)maturedNames) / (N - 2 * shortNames));
      double md = 1.0 - maturedLevel;
      double[] corrData = new double[] { correlation_ > 1 ? correlation_ : 1.0 };
      int[] corrDates = new int[] { 1 };

      // Check the expectations
      if (useSemiAnalytic)
        SemiAnalyticBasketModel.ComputeDistributions(
          false, 0, 2, CopulaType.Gauss, 0, 0, new double[1], corrData, corrDates, 1, 1,
           earlymaturites, survivalCurves, principals, recoveryRates, recoveryDispersions,
           refinanceCurves, 0, gridSize_,
           lossDistribution, amorDistribution);
      else
        ComovementBasketModel.ComputeDistributions(
          false, 0, 2, survivalCurves, principals, recoveryRates, recoveryDispersions,
          refinanceCurves, earlymaturites, gridSize_, lossDistribution, amorDistribution);
      for (int i = 0; i <= N; ++i)
      {
        AssertEqual("Loss" + (withRefinance ? "R " : " ") + i,
          (1 - sp) * (N - 2 * shortNames) * Math.Min(md, levels[i] / Math.Max(1E-6, 1 - recoveryRate)) * (1 - recoveryRate),
          lossDistribution.GetValue(1, i), tolerance);
        double li = levels[i] * (N - 2 * shortNames);
        AssertEqual("Amor" + (withRefinance ? "R " : " ") + i,
          li < maturedNames ? li : (maturedNames + (1 - sp) * (N - 2 * shortNames)
            * Math.Min(md, (levels[i] - maturedLevel) / Math.Max(1E-6, recoveryRate)) * recoveryRate),
          amorDistribution.GetValue(1, i), tolerance);
      }

      // Check the cumulative probabilities
      if (useSemiAnalytic)
        SemiAnalyticBasketModel.ComputeDistributions(
          true, 0, 2, CopulaType.Gauss, 0, 0, new double[1], corrData, corrDates, 1, 1,
           earlymaturites, survivalCurves, principals, recoveryRates, recoveryDispersions,
           refinanceCurves, 0, gridSize_,
           lossDistribution, amorDistribution);
      else
        ComovementBasketModel.ComputeDistributions(
          true, 0, 2, survivalCurves, principals, recoveryRates, recoveryDispersions,
          refinanceCurves, earlymaturites, gridSize_, lossDistribution, amorDistribution);
      for (int i = 0; i <= N; ++i)
      {
        AssertEqual("LProb" + (withRefinance ? "R " : " ") + i,
          levels[i] / Math.Max(1E-6, 1 - recoveryRate) < md ? sp : 1.0,
          lossDistribution.GetValue(1, i), tolerance);
        AssertEqual("AProb" + (withRefinance ? "R " : " ") + i,
          levels[i] < maturedLevel ? 0 : ((levels[i] - maturedLevel) / Math.Max(1E-6, recoveryRate) < md ? sp : 1.0),
          amorDistribution.GetValue(1, i), tolerance);
      }

      return;
    }

    /// <summary>
    ///   Check distribution with a single uniform survival curves
    /// </summary>
    /// <param name="survProb">Survival probability at maturity</param>
    /// <param name="recoveryRate">recovery rate</param>
    /// <param name="shortNames">number of short names</param>
    /// <param name="maturedNames">number of names matured at the as-of date</param>
    /// <param name="withRefinance">include riskless refinance curves to test LCDO engine</param>
    void CheckDifferentialSurvivalProbability(
      double survprob, double recoveryRate,
      int shortNames, int maturedNames,
      bool withRefinance, bool useSemiAnalytic)
    {
      const int N = 10;
      const int basketsize = N;
      const double tolerance = 1E-9;
      double[] levels = new double[N + 1];
      for (int i = 0; i <= N; ++i)
        levels[i] = 1.0 * i / N;
      Curve2D lossDistribution = CreateCurve2D(levels);
      Curve2D amorDistribution = CreateCurve2D(levels);

      double[] recoveryRates = new double[N];
      double[] recoveryDispersions = new double[N];
      double[] principals = new double[N];
      for (int i = 0; i < N; ++i)
      {
        recoveryRates[i] = recoveryRate;
        principals[i] = i < shortNames ? -1.0 : 1.0;
      }

      SurvivalCurve[] survivalCurves = new SurvivalCurve[N];
      double[] probabilities = new double[N + 1];
      for (int i = 0; i < N; ++i)
      {
        probabilities[i] = survprob > 0.5
          ? (0.5 + (survprob - 0.5) * (i + 1.0) / N)
          : (survprob * (i + 1.0) / N);
        SurvivalCurve sc = CreateSurvivalCurve(probabilities[i]);
        survivalCurves[i] = sc;
      }
      probabilities[N] = 1;

      SurvivalCurve[] refinanceCurves = new SurvivalCurve[0];
      if (withRefinance)
      {
        // Create a curve with zero refinance probability
        refinanceCurves = new SurvivalCurve[N];
        SurvivalCurve rc = CreateSurvivalCurve(1.0);
        for (int i = 0; i < N; ++i)
          refinanceCurves[i] = rc;
      }

      double[] earlymaturites = new double[0];
      if (maturedNames > 0)
      {
        earlymaturites = new double[N];
        Dt em = new Dt((asOf_.ToDouble() + asOf_.ToDouble()) / 2);
        for (int i = 1; i <= maturedNames; ++i)
        {
          earlymaturites[N - i] = em.ToDouble();
          probabilities[N - i] = survivalCurves[N - i].Interpolate(em);
        }
      }
      double maturedLevel = Math.Min(1.0, ((double)maturedNames) / (N - 2 * shortNames));
      double md = 1.0 - maturedLevel;
      double[] corrData = new double[] { correlation_ > 1 ? correlation_ : 1.0 };
      int[] corrDates = new int[] { 1 };

      double[] losses = new double[levels.Length];
      double[] amorts = new double[levels.Length];

      // Analytical calculation of base tranche losses and amorts
      for (int i = 0; i < levels.Length; ++i)
      {
        double ed = expectedLoss(
          toNumDefaults(levels[i], 1 - recoveryRate, basketsize, shortNames),
          probabilities, shortNames, maturedNames);
        losses[i] = ed * (1 - recoveryRate);
        ed = expectedAmor(levels[i] * (basketsize - 2 * shortNames),
          probabilities, shortNames, maturedNames, recoveryRate);
        amorts[i] = ed;
      }

      // Check the expectations
      if (useSemiAnalytic)
        SemiAnalyticBasketModel.ComputeDistributions(
          false, 0, 2, CopulaType.Gauss, 0, 0, new double[1], corrData, corrDates, 1, 1,
           earlymaturites, survivalCurves, principals, recoveryRates, recoveryDispersions,
           refinanceCurves, 0, gridSize_,
           lossDistribution, amorDistribution);
      else
        ComovementBasketModel.ComputeDistributions(
          false, 0, 2, survivalCurves, principals, recoveryRates, recoveryDispersions,
          refinanceCurves, earlymaturites, gridSize_, lossDistribution, amorDistribution);
      for (int i = 0; i <= N; ++i)
      {
        AssertEqual("Loss" + (withRefinance ? "R " : " ") + i,
          losses[i], lossDistribution.GetValue(1, i), tolerance);
        double li = levels[i] * (N - 2 * shortNames);
        AssertEqual("Amor" + (withRefinance ? "R " : " ") + i,
          amorts[i], amorDistribution.GetValue(1, i), tolerance);
      }

      // Analytical calculation of cumulative probabilities
      for (int i = 0; i < levels.Length; ++i)
      {
        double cp = cumulativeLoss(
          toNumDefaults(levels[i], 1 - recoveryRate, basketsize, shortNames),
          probabilities, shortNames, maturedNames);
        losses[i] = cp;
        cp = cumulativeAmor(levels[i] * (basketsize - 2 * shortNames),
          probabilities, shortNames, maturedNames, recoveryRate);
        amorts[i] = cp;
      }

      // Check the cumulative probabilities
      if (useSemiAnalytic)
        SemiAnalyticBasketModel.ComputeDistributions(
          true, 0, 2, CopulaType.Gauss, 0, 0, new double[1], corrData, corrDates, 1, 1,
           earlymaturites, survivalCurves, principals, recoveryRates, recoveryDispersions,
           refinanceCurves, 0, gridSize_,
           lossDistribution, amorDistribution);
      else
        ComovementBasketModel.ComputeDistributions(
          true, 0, 2, survivalCurves, principals, recoveryRates, recoveryDispersions,
          refinanceCurves, earlymaturites, gridSize_, lossDistribution, amorDistribution);
      for (int i = 0; i <= N; ++i)
      {
        AssertEqual("LProb" + (withRefinance ? "R " : " ") + i,
          losses[i], lossDistribution.GetValue(1, i), tolerance);
        double li = levels[i] * (N - 2 * shortNames);
        AssertEqual("AProb" + (withRefinance ? "R " : " ") + i,
          amorts[i], amorDistribution.GetValue(1, i), tolerance);
      }

      return;
    }
    
    private static double toNumDefaults(double level, double rate, int basketSize, int shortNames)
    {
      if (Math.Abs(level) < 1E-9)
        return 0;
      basketSize -= 2 * shortNames;
      if (rate < 1E-12)
        return basketSize;
      double d = basketSize * level / rate;
      return Math.Min(d, basketSize);
    }


    private static double cumulativeLoss(
      double level, double[] probabilities, int shortNames, int maturedNames)
    {
      int K = Math.Min(shortNames * 2, probabilities.Length - 1);
      double cp = probabilities[K], a = 0;
      for (int i = K; i < probabilities.Length - 1; ++i)
      {
        a += 1;
        if (a > level)
          return cp;
        cp = probabilities[i + 1];
      }
      return 1;
    }

    private static double expectedLoss(
      double level, double[] probabilities, int shortNames, int maturedNames)
    {
      double sp = 1.0 - probabilities[0];
      int K = Math.Min(shortNames * 2, probabilities.Length - 1);
      for (int i = 0; i < K; ++i)
      {
        double p = probabilities[i+1] - probabilities[i];
        sp -= p;
      }

      double a = 0, ce = 0;
      int B = probabilities.Length - 2 - maturedNames;
      for (int i = K; i <= B; ++i)
      {
        a += 1;
        if (a > level)
          return ce + sp * level;
        double p = (i < B ? probabilities[i+1] : 1.0) - probabilities[i];
        sp -= p;
        ce += p * a;
      }
      return ce;
    }

    private static double cumulativeAmor(
      double level, double[] probabilities, int shortNames, int maturedNames,
      double recoveryRate)
    {
      double maxdefault = Math.Max(0, probabilities.Length - 1 - maturedNames - 2 * shortNames);
      double maxlevel = maxdefault * recoveryRate + maturedNames;

      double cp = 0, a = 0, p0 = 0;
      int B = probabilities.Length - 1 - maturedNames;
      for (int i = 0; i < B; ++i)
      {
        double amor = a * recoveryRate + maturedNames;
        double p = probabilities[i];
        if (amor < level + 1E-10)
          cp += (p - p0);
        p0 = p;
        a += (i < shortNames ? -1 : 1);
      }
      if (maxlevel < level + 1E-10)
        cp += (1 - p0);
      return cp;
    }

    private static double expectedAmor(
      double level, double[] probabilities, int shortNames, int maturedNames,
      double recoveryRate)
    {
      double maxdefault = Math.Max(0, probabilities.Length - 1 - maturedNames - 2 * shortNames);
      double maxlevel = maxdefault * recoveryRate + maturedNames;

      double ce = 0, a = 0, p0 = 0;
      int B = probabilities.Length - 1 - maturedNames;
      for (int i = 0; i < B; ++i)
      {
        double amor = a * recoveryRate + maturedNames;
        if (amor > level)
          amor = level;
        double p = probabilities[i];
        if (amor > 0)
          ce += (p - p0) * amor;
        p0 = p;
        a += (i < shortNames ? -1 : 1);
      }
      ce += (1 - p0) * Math.Min(level, maxlevel);
      return ce;
    }

    #endregion Helpers

    #region Tests
    [Test, Smoke]
    public void Uniform0()
    {
      // almost default
      CheckUniformSurvivalProbability(1E-10, 0.2, 0, 0, false);
      CheckUniformSurvivalProbability(1E-10, 0.2, 0, 0, true);
      CheckUniformSurvivalProbability(1E-10, 0.2, 0, 0, false, true);
      CheckUniformSurvivalProbability(1E-10, 0.2, 0, 0, true, true);
    }

    [Test, Smoke]
    public void Uniform1()
    {
      // the middle case 1
      CheckUniformSurvivalProbability(0.7, 0.8, 0, 0, false);
      CheckUniformSurvivalProbability(0.7, 0.8, 0, 0, true);
      CheckUniformSurvivalProbability(0.7, 0.8, 0, 0, false, true);
      CheckUniformSurvivalProbability(0.7, 0.8, 0, 0, true, true);
    }

    [Test, Smoke]
    public void Uniform2()
    {
      // the middle case 2
      CheckUniformSurvivalProbability(0.7, 0.2, 0, 0, false);
      CheckUniformSurvivalProbability(0.7, 0.2, 0, 0, true);
      CheckUniformSurvivalProbability(0.7, 0.2, 0, 0, false, true);
      CheckUniformSurvivalProbability(0.7, 0.2, 0, 0, true, true);
    }

    [Test, Smoke]
    public void Uniform3()
    {
      // riskless case
      CheckUniformSurvivalProbability(1.0, 0.8, 0, 0, false);
      CheckUniformSurvivalProbability(1.0, 0.8, 0, 0, true);
      CheckUniformSurvivalProbability(1.0, 0.8, 0, 0, false, true);
      CheckUniformSurvivalProbability(1.0, 0.8, 0, 0, true, true);
    }

    [Test, Smoke]
    public void UniShortNames0()
    {
      // almost default
      CheckUniformSurvivalProbability(1E-10, 0.2, 2, 0, false);
      CheckUniformSurvivalProbability(1E-10, 0.2, 2, 0, true);
      CheckUniformSurvivalProbability(1E-10, 0.2, 2, 0, false, true);
      CheckUniformSurvivalProbability(1E-10, 0.2, 2, 0, true, true);
    }

    [Test, Smoke]
    public void UniShortNames1()
    {
      // the middle case 1
      CheckUniformSurvivalProbability(0.7, 0.8, 2, 0, false);
      CheckUniformSurvivalProbability(0.7, 0.8, 2, 0, true);
      CheckUniformSurvivalProbability(0.7, 0.8, 2, 0, false, true);
      CheckUniformSurvivalProbability(0.7, 0.8, 2, 0, true, true);
    }

    [Test, Smoke]
    public void UniShortNames2()
    {
      // the middle case 2
      CheckUniformSurvivalProbability(0.7, 0.2, 2, 0, false);
      CheckUniformSurvivalProbability(0.7, 0.2, 2, 0, true);
      CheckUniformSurvivalProbability(0.7, 0.2, 2, 0, false, true);
      CheckUniformSurvivalProbability(0.7, 0.2, 2, 0, true, true);
    }

    [Test, Smoke]
    public void UniShortNames3()
    {
      // riskless case
      CheckUniformSurvivalProbability(1.0, 0.8, 2, 0, false);
      CheckUniformSurvivalProbability(1.0, 0.8, 2, 0, true);
      CheckUniformSurvivalProbability(1.0, 0.8, 2, 0, false, true);
      CheckUniformSurvivalProbability(1.0, 0.8, 2, 0, true, true);
    }

    [Test, Smoke]
    public void UniEarlyMaturity0()
    {
      // almost default
      CheckUniformSurvivalProbability(1E-10, 0.2, 0, 2, false);
      CheckUniformSurvivalProbability(1E-10, 0.2, 0, 2, true);
      CheckUniformSurvivalProbability(1E-10, 0.2, 0, 2, false, true);
      CheckUniformSurvivalProbability(1E-10, 0.2, 0, 2, true, true);
    }

    [Test, Smoke]
    public void UniEarlyMaturity1()
    {
      // the middle case 1
      CheckUniformSurvivalProbability(0.7, 0.8, 0, 2, false);
      CheckUniformSurvivalProbability(0.7, 0.8, 0, 2, true);
      CheckUniformSurvivalProbability(0.7, 0.8, 0, 2, false, true);
      CheckUniformSurvivalProbability(0.7, 0.8, 0, 2, true, true);
    }

    [Test, Smoke]
    public void UniEarlyMaturity2()
    {
      // the middle case 2
      CheckUniformSurvivalProbability(0.7, 0.2, 0, 2, false);
      CheckUniformSurvivalProbability(0.7, 0.2, 0, 2, true);
      CheckUniformSurvivalProbability(0.7, 0.2, 0, 2, false, true);
      CheckUniformSurvivalProbability(0.7, 0.2, 0, 2, true, true);
    }

    [Test, Smoke]
    public void UniEarlyMaturity3()
    {
      // riskless case
      CheckUniformSurvivalProbability(1.0, 0.8, 0, 2, false);
      CheckUniformSurvivalProbability(1.0, 0.8, 0, 2, true);
      CheckUniformSurvivalProbability(1.0, 0.8, 0, 2, false, true);
      CheckUniformSurvivalProbability(1.0, 0.8, 0, 2, true, true);
    }

    [Test, Smoke]
    public void UniShortAndEarlyMaturity0()
    {
      // almost default
      CheckUniformSurvivalProbability(1E-10, 0.2, 2, 1, false);
      CheckUniformSurvivalProbability(1E-10, 0.2, 2, 1, true);
      CheckUniformSurvivalProbability(1E-10, 0.2, 2, 1, false, true);
      CheckUniformSurvivalProbability(1E-10, 0.2, 2, 1, true, true);
    }

    [Test, Smoke]
    public void UniShortAndEarlyMaturity1()
    {
      // the middle case 1
      CheckUniformSurvivalProbability(0.7, 0.8, 2, 1, false);
      CheckUniformSurvivalProbability(0.7, 0.8, 2, 1, true);
      CheckUniformSurvivalProbability(0.7, 0.8, 2, 1, false, true);
      CheckUniformSurvivalProbability(0.7, 0.8, 2, 1, true, true);
    }

    [Test, Smoke]
    public void UniShortAndEarlyMaturity2()
    {
      // the middle case 2
      CheckUniformSurvivalProbability(0.7, 0.2, 2, 1, false);
      CheckUniformSurvivalProbability(0.7, 0.2, 2, 1, true);
      CheckUniformSurvivalProbability(0.7, 0.2, 2, 1, false, true);
      CheckUniformSurvivalProbability(0.7, 0.2, 2, 1, true, true);
    }

    [Test, Smoke]
    public void UniShortAndEarlyMaturity3()
    {
      // riskless case
      CheckUniformSurvivalProbability(1.0, 0.8, 2, 1, false);
      CheckUniformSurvivalProbability(1.0, 0.8, 2, 1, true);
      CheckUniformSurvivalProbability(1.0, 0.8, 2, 1, false, true);
      CheckUniformSurvivalProbability(1.0, 0.8, 2, 1, true, true);
    }


    [Test, Smoke]
    public void HeteroSurv0()
    {
      // almost default
      CheckDifferentialSurvivalProbability(1E-10, 0.2, 0, 0, false, false);
      CheckDifferentialSurvivalProbability(1E-10, 0.2, 0, 0, true, false);
      CheckDifferentialSurvivalProbability(1E-10, 0.2, 0, 0, false, true);
      CheckDifferentialSurvivalProbability(1E-10, 0.2, 0, 0, true, true);
    }

    [Test, Smoke]
    public void HeteroSurv1()
    {
      // high recovery
      CheckDifferentialSurvivalProbability(0.7, 0.8, 0, 0, false, false);
      CheckDifferentialSurvivalProbability(0.7, 0.8, 0, 0, true, false);
      CheckDifferentialSurvivalProbability(0.7, 0.8, 0, 0, false, true);
      CheckDifferentialSurvivalProbability(0.7, 0.8, 0, 0, true, true);
    }

    [Test, Smoke]
    public void HeteroSurv2()
    {
      // low recovery
      CheckDifferentialSurvivalProbability(0.7, 0.2, 0, 0, false, false);
      CheckDifferentialSurvivalProbability(0.7, 0.2, 0, 0, true, false);
      CheckDifferentialSurvivalProbability(0.7, 0.2, 0, 0, false, true);
      CheckDifferentialSurvivalProbability(0.7, 0.2, 0, 0, true, true);
    }

    [Test, Smoke]
    public void HeteroSurv3()
    {
      // riskless case
      CheckDifferentialSurvivalProbability(1.0, 0.8, 0, 0, false, false);
      CheckDifferentialSurvivalProbability(1.0, 0.8, 0, 0, true, false);
      CheckDifferentialSurvivalProbability(1.0, 0.8, 0, 0, false, true);
      CheckDifferentialSurvivalProbability(1.0, 0.8, 0, 0, true, true);
    }

    [Test, Smoke]
    public void HeteroSurvShortNames0()
    {
      // almost default
      CheckDifferentialSurvivalProbability(1E-10, 0.2, 2, 0, false, false);
      CheckDifferentialSurvivalProbability(1E-10, 0.2, 2, 0, true, false);
      CheckDifferentialSurvivalProbability(1E-10, 0.2, 2, 0, false, true);
      CheckDifferentialSurvivalProbability(1E-10, 0.2, 2, 0, true, true);
    }

    [Test, Smoke]
    public void HeteroSurvShortNames1()
    {
      // high recovery
      CheckDifferentialSurvivalProbability(0.7, 0.8, 2, 0, false, false);
      CheckDifferentialSurvivalProbability(0.7, 0.8, 2, 0, true, false);
      CheckDifferentialSurvivalProbability(0.7, 0.8, 2, 0, false, true);
      CheckDifferentialSurvivalProbability(0.7, 0.8, 2, 0, true, true);
    }

    [Test, Smoke]
    public void HeteroSurvShortNames2()
    {
      // low recovery
      CheckDifferentialSurvivalProbability(0.7, 0.2, 2, 0, false, false);
      CheckDifferentialSurvivalProbability(0.7, 0.2, 2, 0, true, false);
      CheckDifferentialSurvivalProbability(0.7, 0.2, 2, 0, false, true);
      CheckDifferentialSurvivalProbability(0.7, 0.2, 2, 0, true, true);
    }

    [Test, Smoke]
    public void HeteroSurvShortNames3()
    {
      // riskless case
      CheckDifferentialSurvivalProbability(1.0, 0.8, 2, 0, false, false);
      CheckDifferentialSurvivalProbability(1.0, 0.8, 2, 0, true, false);
      CheckDifferentialSurvivalProbability(1.0, 0.8, 2, 0, false, true);
      CheckDifferentialSurvivalProbability(1.0, 0.8, 2, 0, true, true);
    }

    [Test, Smoke]
    public void HeteroSurvEarlyMaturity0()
    {
      // almost default
      CheckDifferentialSurvivalProbability(1E-10, 0.2, 0, 2, false, false);
      CheckDifferentialSurvivalProbability(1E-10, 0.2, 0, 2, true, false);
      CheckDifferentialSurvivalProbability(1E-10, 0.2, 0, 2, false, true);
      CheckDifferentialSurvivalProbability(1E-10, 0.2, 0, 2, true, true);
    }

    [Test, Smoke]
    public void HeteroSurvEarlyMaturity1()
    {
      // high recovery
      CheckDifferentialSurvivalProbability(0.7, 0.8, 0, 2, false, false);
      CheckDifferentialSurvivalProbability(0.7, 0.8, 0, 2, true, false);
      CheckDifferentialSurvivalProbability(0.7, 0.8, 0, 2, false, true);
      CheckDifferentialSurvivalProbability(0.7, 0.8, 0, 2, true, true);
    }

    [Test, Smoke]
    public void HeteroSurvEarlyMaturity2()
    {
      // low recovery
      CheckDifferentialSurvivalProbability(0.7, 0.2, 0, 2, false, false);
      CheckDifferentialSurvivalProbability(0.7, 0.2, 0, 2, true, false);
      CheckDifferentialSurvivalProbability(0.7, 0.2, 0, 2, false, true);
      CheckDifferentialSurvivalProbability(0.7, 0.2, 0, 2, true, true);
    }

    [Test, Smoke]
    public void HeteroSurvEarlyMaturity3()
    {
      // riskless case
      CheckDifferentialSurvivalProbability(1.0, 0.8, 0, 2, false, false);
      CheckDifferentialSurvivalProbability(1.0, 0.8, 0, 2, true, false);
      CheckDifferentialSurvivalProbability(1.0, 0.8, 0, 2, false, true);
      CheckDifferentialSurvivalProbability(1.0, 0.8, 0, 2, true, true);
    }

    [Test, Smoke]
    public void HeteroSurvShortAndEarlyMaturity00()
    {
      // almost default
      CheckDifferentialSurvivalProbability(1E-10, 0.2, 2, 1, false, false);
      CheckDifferentialSurvivalProbability(1E-10, 0.2, 2, 1, true, false);
      CheckDifferentialSurvivalProbability(1E-10, 0.2, 2, 1, false, true);
      CheckDifferentialSurvivalProbability(1E-10, 0.2, 2, 1, true, true);
    }

    [Test, Smoke]
    public void HeteroSurvShortAndEarlyMaturity01()
    {
      // high recovery
      CheckDifferentialSurvivalProbability(0.7, 0.8, 2, 1, false, false);
      CheckDifferentialSurvivalProbability(0.7, 0.8, 2, 1, true, false);
      CheckDifferentialSurvivalProbability(0.7, 0.8, 2, 1, false, true);
      CheckDifferentialSurvivalProbability(0.7, 0.8, 2, 1, true, true);
    }

    [Test, Smoke]
    public void HeteroSurvShortAndEarlyMaturity02()
    {
      // low recovery
      CheckDifferentialSurvivalProbability(0.7, 0.2, 2, 1, false, false);
      CheckDifferentialSurvivalProbability(0.7, 0.2, 2, 1, true, false);
      CheckDifferentialSurvivalProbability(0.7, 0.2, 2, 1, false, true);
      CheckDifferentialSurvivalProbability(0.7, 0.2, 2, 1, true, true);
    }

    [Test, Smoke]
    public void HeteroSurvShortAndEarlyMaturity03()
    {
      // riskless case
      CheckDifferentialSurvivalProbability(1.0, 0.8, 2, 1, false, false);
      CheckDifferentialSurvivalProbability(1.0, 0.8, 2, 1, true, false);
      CheckDifferentialSurvivalProbability(1.0, 0.8, 2, 1, false, true);
      CheckDifferentialSurvivalProbability(1.0, 0.8, 2, 1, true, true);
    }

    [Test]
    public void CorrelationLargerThanOne()
    {
      correlation_ = 1.1;
      CheckDifferentialSurvivalProbability(0.7, 0.2, 0, 0, false, true);
    }
    #endregion Tests
  }
}
