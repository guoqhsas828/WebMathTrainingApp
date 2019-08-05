//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Numerics.Rng;

using NUnit.Framework;
using BaseEntity.Toolkit.Tests.Helpers.Legacy;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class TestBasketOptimizedSampler : ToolkitTestBase
  {
    public void TestWholeBasket(int n, double tol)
    {
      Dt start = Dt.Today();
      Dt end = Dt.Add(start, 5, TimeUnit.Years);
      int[] strata = ArrayUtil.Generate<int>(n + 1,
        delegate(int i) { return i; });
      int[] alloca = new int[n+1]; alloca[0] = 1;
      double[] factors = new double[n];

      SurvivalCurve sc = CreateSurvivalCurve(start, hazardRate);
      double probability = 1.0 - sc.Interpolate(start, end);
      SurvivalCurve[] survivalCurves = ArrayUtil.Generate<SurvivalCurve>(
        n, delegate(int i) { return sc; });

      //- For 0 correlation, n+1 paths should give
      //- the exact result on the expected number of defaults
      TimeToDefaultOptimizedRng rng = new TimeToDefaultOptimizedRng(
        n+1, start, end, survivalCurves, null, end, strata, alloca,
        factors, new RandomNumberGenerator());

      StatisticsBuilder stat = new StatisticsBuilder();
      int nDefaults;
      while ((nDefaults = rng.Draw()) >= 0)
        stat.Add(rng.Stratum, rng.Weight, (double)nDefaults);
      double mean = stat.Mean;
      double stderr = stat.EstimatorStdError;
      //Assert.AreEqual("mean@0", n * probability, mean, 1E-6);
      //Assert.AreEqual("se@0", 0.0, stderr, 1E-6);

      for (int r = 1; r < 10; ++r)
      {
        for (int i = 0; i < n; ++i)
          factors[i] = r / 10.0;
        rng = new TimeToDefaultOptimizedRng(
          100000, start, end, survivalCurves, null, end, strata, alloca,
          factors, new RandomNumberGenerator());
        stat = new StatisticsBuilder();
        while ((nDefaults = rng.Draw()) >= 0)
          stat.Add(rng.Stratum, rng.Weight, (double)nDefaults);
        mean = stat.Mean;
        stderr = stat.EstimatorStdError;
        Assert.AreEqual(n*probability, mean, tol, "mean@" + factors[0]);
        Assert.AreEqual(0.0, stderr, tol, "se@" + factors[0]);
      }
      return;
    }

    private static SurvivalCurve
    CreateSurvivalCurve(Dt asOfDate,
                         double hazardRate)
    {
      SurvivalCurve SurvCurve = new SurvivalCurve(asOfDate);
      SurvCurve.Add(asOfDate, 1.0);
      for (int i = 1; i <= 6; ++i)
        SurvCurve.Add(Dt.Add(asOfDate, i, TimeUnit.Years),
                      Math.Exp(-i * hazardRate));
      SurvCurve.Add(Dt.Add(asOfDate, 10, TimeUnit.Years),
                    Math.Exp(-10 * hazardRate));
      return SurvCurve;
    }

    [Test,Ignore("Not work yet")]
    public void SingleNameBasket()
    {
      TestWholeBasket(1, 1E-3);
    }

    [Test,Ignore("Not work yet")]
    public void TenNamesBasket()
    {
      TestWholeBasket(10, 1E-3);
    }

    [Test]
    public void HundredNamesBasket()
    {
      TestWholeBasket(100, 2E-2);
    }

    const double epsilon = 1.0e-4;
    const int nBasket = 100;
    const double principal = 10000000;
    const double recoveryRate = 0.4;
    const double corr = 0.0;
    const double hazardRate = 0.005;
    const int stepSize = 1;
    const TimeUnit stepUnit = TimeUnit.Months;
    const int simulationRuns = 50000;

    const int IntegrationPointsFirst = 64;
    const int IntegrationPointsSecond = 5;

  }

}
