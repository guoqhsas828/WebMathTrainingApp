//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Linq;
using System.Threading;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Numerics;

using NUnit.Framework;

namespace BaseEntity.Toolkit.Tests.Models
{
  [TestFixture]
  public class RateCurveSimulationTests
  {
    private const MultiStreamRng.Type Mt = MultiStreamRng.Type.MersenneTwister;
    private readonly string[] _tenors = { "6M", "1Y", "2Y", "5Y", "10Y", "20Y" };
    private readonly double[] _rates = { 0.015, 0.02, 0.03, 0.04, 0.04, 0.04 };

    #region Simulation tests

    [Test]
    public void SimulateRates()
    {
      var asOf = Dt.Today();
      Simulate(asOf, _tenors, _rates, 0.0, 0.8, 128);
      return;
    }

    [Test]
    public void SimulateRatesWithCredit()
    {
      var asOf = Dt.Today();
      Simulate(asOf, _tenors, _rates, 0.04, 0.8, 20);
      return;
    }

    private static void Simulate(Dt asOf,
      string[] tenors, double[] mmRates, double hazardRate,
      double rateVolatility, int pathCount)
    {
      var discountCurve = CreateRateCurve(asOf, tenors, mmRates);
      var creditCurve = CreateCreditCurve(discountCurve, hazardRate);
      var fvs = CreateVolatility(discountCurve, rateVolatility,
        creditCurve, rateVolatility);
      var env = CreateMarketEnvironment(
        discountCurve, creditCurve, fvs);
      var dates = env.Tenors;
      var serialResults = ArrayUtil.Generate(pathCount + 1,
        i => i == 0 ? null : new double[2 * dates.Length]);
      var parallelResults = ArrayUtil.Generate(pathCount + 1,
        i => i == 0 ? null : new double[2 * dates.Length]);

      using (var simulator = Simulator.Create(pathCount, dates, env,
        fvs.Volatilities, fvs.FactorLoadings, EmptyArray<int>.Instance, -100))
      {
        // Run simulation in serial
        serialResults[0] = dates
          .Select(d => discountCurve.DiscountFactor(asOf, d))
          .Concat(dates.Select(d => creditCurve.SurvivalProb(asOf, d)))
          .ToArray();
        using (var rng = MultiStreamRng.Create(Mt, 1, Array.ConvertAll(dates, d => d - asOf)))
        {
          var dc = discountCurve.CloneObjectGraph();
          var sc = creditCurve.CloneObjectGraph();
          for (int i = 0; i < pathCount; ++i)
          {
            var rng1 = rng.Clone();
            GeneratePath(simulator, rng1, serialResults[i + 1], i, dc, sc);
          }
        }

        // Run simulation in parallel
        parallelResults[0] = dates
          .Select(d => discountCurve.DiscountFactor(asOf, d))
          .Concat(dates.Select(d => creditCurve.SurvivalProb(asOf, d)))
          .ToArray();
        using (var rng = MultiStreamRng.Create(Mt, 1, Array.ConvertAll(dates, d => d - asOf)))
        {
          // ReSharper disable AccessToDisposedClosure
          ParallelFor(0, pathCount,
            () => System.Tuple.Create(env.CloneObjectGraph(), rng.Clone()),
            (i, a) =>
            {
              var dc = a.Item1.DiscountCurves[0];
              var sc = a.Item1.CreditCurves[0];
              GeneratePath(simulator, a.Item2,
                parallelResults[i + 1], i, dc, sc);
            });
          // ReSharper restore AccessToDisposedClosure
        }
      }

      // The results should match exactly
      Assert.That(parallelResults,Is.EqualTo(serialResults));
    }

    private static void GeneratePath(
      Simulator simulator, MultiStreamRng rng, double[] results,
      int pathIndex, DiscountCurve domestic, SurvivalCurve sc)
    {
      int i = pathIndex;
      var path = simulator.GetSimulatedPath(i, rng);
      var asOf = domestic.AsOf;
      var dates = simulator.SimulationDates;
      int n = dates.Length;
      results[0] = domestic.DiscountFactor(asOf, dates[0]);
      for (int t = 1; t < n; ++t)
      {
        Thread.Yield(); // simulate race condition.
        double numeraire, fxRate, time = dates[t - 1] - asOf;
        path.EvolveDiscount(0, time, t - 1, domestic,
          out fxRate, out numeraire);
        path.EvolveCredit(0, time, t - 1, sc);
        results[t] = domestic.DiscountFactor(asOf, dates[t]);
        results[n + t - 1] = sc.SurvivalProb(asOf, dates[t - 1]);
      }
      path.EvolveCredit(0, dates[n - 1] - asOf, n - 1, sc);
      results[n + n - 1] = sc.SurvivalProb(asOf, dates[n - 1]);
    }

    #endregion

    #region My Parallel for

    // Each action runs on its own thread.
    // Simulate overcrowded threading.
    static void ParallelFor<U>(int start, int stop,
      Func<U> init, Action<int, U> action)
    {
      int count = stop - start;
      if (count <= 0) return;

      var states = new U[count];
      for (int i = 0; i < count; ++i)
        states[i] = init();

      var threads = new Thread[count];
      var cde = new CountdownEvent(count);
      for (int i = 0; i < count; ++i)
        threads[i] = new Thread((o) =>
        {
          var tuple = (Tuple<int, U>)o;
          action(tuple.Item1, tuple.Item2);
          cde.Signal();
        });

      for (int i = 0; i < count; ++i)
        threads[i].Start(System.Tuple.Create(i, states[i]));

      cde.Wait();
    }

    #endregion

    #region Utilities
    private static MarketEnvironment CreateMarketEnvironment(
      DiscountCurve discountCurve,
      SurvivalCurve creditCurve,
      FactorizedVolatilitySystem fvs)
    {
      return MarketEnvironment.Create(discountCurve.AsOf,
        discountCurve.Select(p => p.Date).ToArray(), Tenor.Empty,
        discountCurve.Ccy, discountCurve.Ccy,
        fvs.Volatilities, fvs.FactorLoadings,
        new[] { discountCurve }, EmptyArray<FxRate>.Instance,
        EmptyArray<CalibratedCurve>.Instance,
        new[] { creditCurve });
    }

    private static DiscountCurve CreateRateCurve(
      Dt asOf, string[] tenors, double[] mmRates)
    {
      var calibrator = new DiscountRateCalibrator(asOf, asOf);
      var dcurve = new DiscountCurve(calibrator);
      dcurve.Interp = InterpFactory.FromMethod(
        InterpMethod.Weighted, ExtrapMethod.Const);
      const DayCount dc = DayCount.Actual365Fixed;
      var ccy = Currency.USD;
      dcurve.Ccy = ccy;
      dcurve.Name = String.Format("{0}Libor_3M", ccy);
      for (int i = 0; i < tenors.Length; ++i)
      {
        Dt maturity = Dt.Add(asOf, tenors[i]);
        dcurve.AddMoneyMarket(tenors[i], maturity, mmRates[i], dc);
      }
      dcurve.Fit();
      return dcurve;
    }

    private static SurvivalCurve CreateCreditCurve(
      DiscountCurve discountCurve, double hazardRate)
    {
      var asOf = discountCurve.AsOf;
      var sc = SurvivalCurve.FromHazardRate(
        asOf, discountCurve, "5Y", hazardRate, 0.4, true);
      var points = discountCurve
        .Select(p => sc.SurvivalProb(asOf, p.Date)).ToArray();
      sc.Shrink(0);
      for (int i = 0, n = points.Length; i < n; ++i)
      {
        sc.Add(discountCurve.GetDt(i), points[i]);
      }
      sc.Name = "Credit";
      return sc;
    }

    private static FactorizedVolatilitySystem CreateVolatility(
      DiscountCurve discountCurve, double rateVolatility,
      SurvivalCurve creditCurve, double creditVolatility)
    {
      var asOf = discountCurve.AsOf;
      var tenors = discountCurve.Tenors
        .Select(t => Tenor.Parse(t.Name)).ToArray();
      var volCurves = ArrayUtil.Generate(tenors.Length,
        i => new VolatilityCurve(asOf, rateVolatility));
      var volatilities = new VolatilityCollection(tenors);
      var factorLoadings = new FactorLoadingCollection(
        new[] { "f1" }, tenors);

      var name = discountCurve.Name;
      volatilities.Add(discountCurve, volCurves);
      var factors = Enumerable.Range(0, tenors.Length)
        .Aggregate(new double[tenors.Length, 1],
        (a, i) => a.Set(i, 0, 1));
      factorLoadings.AddFactors(discountCurve, factors);

      if (creditCurve != null)
      {
        volatilities.Add(creditCurve, new VolatilityCurve(asOf, creditVolatility));
        factorLoadings.AddFactors(creditCurve, factors);
      }
      return new FactorizedVolatilitySystem(factorLoadings, volatilities);
    }
    #endregion
  }
}
