/*
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows.Expressions;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Models.Simulations;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  ///   Exposure calculator
  /// </summary>
  public partial class ExposureCalculator : IExposureCalculator
  {
    #region IExposureCalculator implementation

    /// <summary>
    /// Scenario Generation.
    /// </summary>
    /// <param name="exposureDates">Exposure dates</param>
    /// <param name="simulator">Simulator</param>
    /// <param name="generator">Random number generator</param>
    /// <param name="marketEnvironment">CCR Market Environment</param>
    /// <param name="nettingCount">The number of netting sets</param>
    /// <param name="portfolioData">An array of tuples consisting of CCR pricer, fxRate index and counterparty index</param>
    /// <param name="unilateral">Treat default unilaterally or jointly (first-to-default)</param>
    /// <returns>Simulation scenarios</returns>
    /// <remarks>
    /// We generate scenarios via simulation of risk factors. Each scenario is a joint realization of risk factors 
    /// at various points in time, including discount factor (df), numeraire asset (numeraire), counterparty default Radon Nikodym derivative (cptyRn),
    /// booking entity default Radon Nikodym derivative (ownRn), booking entity survival Radon Nikodym derivative (survivalRn), 
    /// counterparty credit spread (cptySpread), booking entity credit spread (ownSpread), lend curve spread (lendSpread), borrow curve spread (borrowSpread). 
    /// </remarks>
    public IEnumerable<ISimulatedPathValues> CalculateExposures(
      Dt[] exposureDates, Simulator simulator,
      MultiStreamRng generator,
      CCRMarketEnvironment marketEnvironment, int nettingCount,
      Tuple<CcrPricer, int, int>[] portfolioData,
      bool unilateral)
    {
      var exposureDateIndex = Array.ConvertAll(exposureDates,
        dt => Array.IndexOf(simulator.SimulationDates, dt));
      Action<int, SimulationThread> compute = (idx, thread) =>
      {
        SimulatedPath path = simulator.GetSimulatedPath(idx, thread.Rng);
        if (path == null)
          return;
        var nodes = thread.ValueNodes;
        var portfolio = thread.PortfolioData;
        var env = thread.MarketData;
        var dateCount = exposureDates.Length;
        var resultData = new SimulatedPathValues(dateCount, nettingCount);
        for (int time = 0; time < dateCount; ++time)
        {
          var t = exposureDateIndex[time];
          var dt = exposureDates[time];
          double df,
            numeraire,
            cptyRn,
            ownRn,
            survivalRn,
            cptySpread,
            ownSpread,
            lendSpread,
            borrowSpread;

          path.Evolve(t, dt, env, unilateral,
            out numeraire, out df, out cptyRn, out ownRn, out survivalRn,
            out cptySpread, out ownSpread, out lendSpread, out borrowSpread);
          ResetAll(nodes);
          var v = CalculateFwdValues(nettingCount, dt, time, env, portfolio);
          resultData.Add(v, df, numeraire, cptyRn, ownRn, survivalRn,
            cptySpread, ownSpread, lendSpread, borrowSpread, idx, path.Weight);
        }
        path.Dispose();
        thread.Paths.Add(resultData);
      };
      var marketPaths = new List<ISimulatedPathValues>();
      Parallel.For(0, simulator.PathCount,
        () => new SimulationThread(generator,
          marketEnvironment, exposureDates, portfolioData),
        (i, thread) => compute(i, thread),
        thread =>
        {
          if (thread == null || thread.Paths == null)
            return;
          marketPaths.AddRange(thread.Paths);
        });
      return marketPaths;
    }

    /// <summary>
    ///  Calculates the MtM values at the forward date
    /// </summary>
    /// <param name="nettingCount">Number of the netting groups</param>
    /// <param name="fwdDate">The forward date</param>
    /// <param name="dateIndex">Index of the forward date in the array of exposure dates</param>
    /// <param name="environment">Market environment</param>
    /// <param name="portfolio">The portfolio</param>
    /// <returns>An array of portfolio values by netting groups</returns>
    internal static double[] CalculateFwdValues(
      int nettingCount,
      Dt fwdDate, int dateIndex,
      MarketEnvironment environment,
      Tuple<IPvEvaluator, int, int>[] portfolio)
      // the first int is currency index, the second int is netting group index.
    {
      var values = new double[nettingCount];
      foreach (var p in portfolio)
      {
        var pv = p.Item1.FastPv(dateIndex, fwdDate);
        if (double.IsNaN(pv))
          continue;
        if (p.Item2 >= 0)
        {
          var fx = environment.FxRates[p.Item2];
          pv *= fx.GetRate(environment.DiscountCurves[p.Item2 + 1].Ccy,
            environment.DiscountCurves[0].Ccy);
        }
        values[p.Item3] += pv;
      }
      return values;
    }

    #endregion

    #region SimulationThread

    /// <summary>
    /// Simulation environment and storage. 
    /// Each new instance of this class makes a deep copy of the graph of the original market environment
    /// </summary>
    private class SimulationThread
    {
      /// <summary>
      /// Market environment
      /// </summary>
      internal readonly CCRMarketEnvironment MarketData;

      /// <summary>
      /// Store paths
      /// </summary>
      internal readonly List<ISimulatedPathValues> Paths;

      /// <summary>
      /// Pricing environment
      /// </summary>
      internal readonly Tuple<IPvEvaluator, int, int>[] PortfolioData;

      /// <summary>
      /// Random number generator
      /// </summary>
      internal readonly MultiStreamRng Rng;

      /// <summary>
      ///  A full list of all the computational nodes.
      /// </summary>
      internal readonly IResettable[] ValueNodes;

      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="rng">Random number generator</param>
      /// <param name="marketData">Market data</param>
      /// <param name="exposureDates">Exposure dates</param>
      /// <param name="portfolioData">Portfolio data</param>
      internal SimulationThread(
        MultiStreamRng rng,
        CCRMarketEnvironment marketData,
        Dt[] exposureDates,
        Tuple<CcrPricer, int, int>[] portfolioData)
      {
        Paths = new List<ISimulatedPathValues>();
        Rng = (rng == null) ? null : rng.Clone();
        var env = CloneUtil.CloneObjectGraph(marketData, portfolioData);
        MarketData = env.Item1;
        MarketData.Conform();
        using (Evaluable.PushVariants(GetSimulatedObjects(MarketData)))
        {
          PortfolioData = InitializePricers(exposureDates, env.Item2);
          ValueNodes = Evaluable.GetCommonEvaluables()
            .OfType<IResettable>().ToArray();
        }
      }
    }

    #endregion

    #region Value nodes management

    internal static IEnumerable<object> GetSimulatedObjects(
      MarketEnvironment mktEnv)
    {
      IEnumerable<object> list = mktEnv.DiscountCurves;
      list = Concat(list, mktEnv.CreditCurves);
      list = Concat(list, mktEnv.ForwardCurves);
      list = Concat(list, mktEnv.SpotBasedCurves);
      list = Concat(list, mktEnv.FxRates);
      return list;
    }

    private static IEnumerable<T> Concat<T>(
      IEnumerable<T> a, IEnumerable<T> b)
    {
      return a == null ? b : (b == null ? a : a.Concat(b));
    }


    internal static IPvEvaluator InitializePricer(ISimulationPricer pricer, bool enablePvEvaluator = true)
    {
      var ccrPricer = pricer as CcrPricer;
      if(!enablePvEvaluator)
        return CcrPricerWrapper.Get(ccrPricer);
      var p = PvEvaluator.Get(ccrPricer, pricer.ExposureDates)
        ?? CcrPricerWrapper.Get(ccrPricer);
      return p;
    }

    private static Tuple<IPvEvaluator, int, int>[] InitializePricers(
      Dt[] exposureDates,
      Tuple<CcrPricer, int, int>[] portfolioData)
    {
      int n = portfolioData.Length;
      var portfolio = new Tuple<IPvEvaluator, int, int>[n];
      for (int i = 0; i < n; ++i)
      {
        var t = portfolioData[i];
        var p = PvEvaluator.Get(t.Item1, exposureDates)
          ?? CcrPricerWrapper.Get(t.Item1);
        portfolio[i] = Tuple.Create(p, t.Item2, t.Item3);
      }
      return portfolio;
    }

    internal static void ResetAll(IResettable[] nodes)
    {
      if (nodes == null) return;

      for (int i = 0; i < nodes.Length; ++i)
      {
        nodes[i].Reset();
      }
    }

    #endregion

  }

}
