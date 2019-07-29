using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Ccr
{
  public static partial class Simulations
  {
    #region SimulationThread

    /// <summary>
    /// Simulation thread and sensitivity results
    /// </summary>
    private class SimulationThreadAndResults
    {
      internal readonly SimulationThread BaseThread;
      internal readonly double[][] ExpExposure;
      internal readonly double[][] NegExpExposure;
      internal readonly double[][] OmegaB;
      internal readonly double[][] OmegaC;
      internal readonly SimulationThread PerturbedThread;

      internal SimulationThreadAndResults(MultiStreamRng rng, CCRMarketEnvironment environment,
                                          PortfolioData portfolioData,
                                          int count, int timeBuckets)
      {
        BaseThread = new SimulationThread(rng, environment, portfolioData);
        PerturbedThread = new SimulationThread(rng, environment, portfolioData);
        ExpExposure = new double[count][];
        NegExpExposure = new double[count][];
        OmegaC = new double[count][];
        OmegaB = new double[count][];
        for (int i = 0; i < count; ++i)
        {
          ExpExposure[i] = new double[timeBuckets];
          NegExpExposure[i] = new double[timeBuckets];
          OmegaC[i] = new double[timeBuckets];
          OmegaB[i] = new double[timeBuckets];
        }
      }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Calculate XVA theta
    /// </summary>
    /// <param name="exposure">Exposure</param>
    /// <param name="toAsOf">to asOf date</param>
    /// <param name="measure">measure</param>
    /// <param name="netting">nettings</param>
    /// <returns></returns>
    public static double XvaTheta(ICounterpartyCreditRiskCalculations exposure, Dt toAsOf, string measure, Netting netting)
    {
      double retVal = 0.0;
      switch (measure)
      {
        case "CVA":
          retVal = exposure.GetMeasure(CCRMeasure.CVATheta, netting ,toAsOf, 1.0);
          break;
        case "DVA":
          retVal = exposure.GetMeasure(CCRMeasure.DVATheta, netting, toAsOf, 1.0);
          break;
        case "FCA":
          retVal = exposure.GetMeasure(CCRMeasure.FCATheta, netting, toAsOf, 1.0);
          break;
        case "FBA":
          retVal = exposure.GetMeasure(CCRMeasure.FBATheta, netting, toAsOf, 1.0);
          break;
        case "FVA":
          retVal = exposure.GetMeasure(CCRMeasure.FVATheta, netting, toAsOf, 1.0);
          break;
        default:
          throw new NotSupportedException(String.Format("Measure {0} not iplemented", measure));
      }
      return retVal;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="exposure"></param>
    /// <param name="toAsOf"></param>
    /// <param name="measure"></param>
    /// <returns></returns>
    public static double XvaTheta(IRunSimulationPath exposure, Dt toAsOf, string measure)
    {
      double retVal = 0.0;
      switch (measure)
      {
        case "CVA":
          retVal = exposure.GetMeasure(CCRMeasure.CVATheta, toAsOf, 1.0);
          break;
        case "DVA":
          retVal = exposure.GetMeasure(CCRMeasure.DVATheta, toAsOf, 1.0);
          break;
        case "FCA":
          retVal = exposure.GetMeasure(CCRMeasure.FCATheta, toAsOf, 1.0);
          break;
        case "FBA":
          retVal = exposure.GetMeasure(CCRMeasure.FBATheta, toAsOf, 1.0);
          break;
        case "FVA":
          retVal = exposure.GetMeasure(CCRMeasure.FVATheta, toAsOf, 1.0);
          break;
        default:
          throw new NotSupportedException(String.Format("Measure {0} not iplemented", measure));
      }
      return retVal;
    }

    private static Tuple<Dt[], double[]>[] GetIntegrationKernel(Simulator simulator, int[] cptyIndex)
    {
      if (simulator == null)
        return null;
      return ArrayUtil.Generate(cptyIndex.Length,
                                i =>
                                {
                                  var krn = new double[simulator.SimulationDates.Length];
                                  simulator.DefaultKernel(i, krn);
                                  return new Tuple<Dt[], double[]>(simulator.SimulationDates, krn);
                                });
    }

    private static double[][][,] ProcessExoticTrades(
      CCRMarketEnvironment environment,
      VolatilityCollection volatilities,
      FactorLoadingCollection factorLoadings,
      MultiStreamRng rng,
      int sampleSize,
      Dt[] simulationDates,
      PortfolioData portfolio,
      Perturbation[] perturbations,
      Dt[] exposureDates)
    {
      int nettingCount = portfolio.Map.Count;
      int count = perturbations.Length;
      var exoticExposures = ArrayUtil.Generate(count + 1, i => new double[nettingCount][,]);
      foreach (var exoticPricer in portfolio.Exotics)
      {
        DiscountCurve[] discountCurves;
        FxRate[] fxRates;
        GetDiscountData(environment, exoticPricer, out discountCurves, out fxRates);
        var pricer = exoticPricer.Item1;
        var grids = LeastSquaresMonteCarloCcrPricer.Calculate(
          environment.AsOf, environment.Tenors,
          Tenor.Empty,
          simulationDates,
          sampleSize,
          rng.RngType,
          discountCurves,
          fxRates,
          pricer.ReferenceCurves.SafeToArray(),
          pricer.SurvivalCurves.SafeToArray(),
          volatilities,
          factorLoadings,
          pricer.Notional,
          pricer.ValuationCurrency,
          environment.DiscountCurves[0].Ccy,
          pricer.Cashflow, pricer.Basis,
          pricer.CallEvaluator,
          pricer.PutEvaluator, 1e-3, 
          perturbations, 
          exposureDates);
        int nettingId = exoticPricer.Item3;
        for (int i = 0; i < grids.Length; ++i)
        {
          double[,] g = grids[i];
          if (exoticExposures[i][nettingId] == null)
            exoticExposures[i][nettingId] = new double[g.GetLength(0),g.GetLength(1)];
          double[,] ee = exoticExposures[i][nettingId];
          for (int path = 0; path < g.GetLength(0); ++path)
          {
            for (int t = 0; t < g.GetLength(1); ++t)
            {
              ee[path, t] += g[path, t];
            }
          }
        }
      }
      return exoticExposures;
    }

    private static SimulatedPathValues ProcessBasePath(SimulationThreadAndResults thread, PathWiseExposure pe,
                                                       PathWiseExposure ne, Dt[] exposureDates, int[] exposureDatesIndex,
                                                       int nettingCount, SimulatedPath marketPath,
                                                       double[][][,] exoticPvs,
                                                       bool withExotics, bool unilateral)
    {
      var baseEnvironment = thread.BaseThread.MarketData;
      var basePortfolio = thread.BaseThread.PortfolioData;
      var basePath = new SimulatedPathValues(exposureDates.Length, nettingCount);
      for (int t = 0; t < exposureDates.Length; ++t)
      {
        double numeraire, df, cptyRn, ownRn, survivalRn, cptySpread, ownSpread, lendSpread, borrowSpread;
        var time = exposureDatesIndex[t];
        var dt = exposureDates[t];

        marketPath.Evolve(time, dt, baseEnvironment, unilateral, out numeraire, out df, out cptyRn, out ownRn, out survivalRn,
                          out cptySpread, out ownSpread, out lendSpread, out borrowSpread);
        var basePv = CalculateFwdValues(basePath.NettingCount, dt, baseEnvironment, basePortfolio.Portfolio);
        if (withExotics)
        {
          for (int j = 0; j < basePath.NettingCount; ++j)
          {
            if (exoticPvs[0][j] == null)
              continue;
            double[,] exoticExposure = exoticPvs[0][j];
            basePv[j] += exoticExposure[marketPath.Id, t];
          }
        }
        basePath.Add(basePv, df, numeraire, cptyRn, ownRn, survivalRn, cptySpread, ownSpread, lendSpread, borrowSpread, marketPath.Id,
                     marketPath.Weight);
        //compute base case EE, NEE
        double dPdT = 1.0 / (numeraire * df);
        double wt = marketPath.Weight;
        double w0 = wt * dPdT * cptyRn;
        double w1 = wt * dPdT * ownRn;
        thread.ExpExposure[0][t] += w0 * df * pe.Compute(basePath, t);
        thread.NegExpExposure[0][t] += w1 * df * ne.Compute(basePath, t);
        thread.OmegaC[0][t] += w0;
        thread.OmegaB[0][t] += w1;
      }
      return basePath;
    }

    private static SimulatedPathValues ProcessBasePath(SimulationThreadAndResults thread, PathWiseExposure pe,
                                                       PathWiseExposure ne, Dt[] exposureDates, int[] exposureDatesIndex,
                                                       SimulatedPath marketPath, SimulatedPathValues basePath,
                                                       bool unilateral)
    {
      var baseEnvironment = thread.BaseThread.MarketData;
      for (int t = 0; t < exposureDates.Length; ++t)
      {
        double numeraire, df, cptyRn, ownRn, survivalRn, cptySpread, ownSpread, lendSpread, borrowSpread;
        var time = exposureDatesIndex[t];
        marketPath.Evolve(time, exposureDates[t], baseEnvironment, unilateral, out numeraire, out df, out cptyRn, out ownRn,
                          out survivalRn,
                         out cptySpread, out ownSpread, out lendSpread, out borrowSpread);
        //compute base case EE, NEE
        double dPdT = 1.0 / (numeraire * df);
        double wt = marketPath.Weight;
        double w0 = wt * dPdT * cptyRn;
        double w1 = wt * dPdT * ownRn;
        thread.ExpExposure[0][t] += w0 * df * pe.Compute(basePath, t);
        thread.NegExpExposure[0][t] += w1 * df * ne.Compute(basePath, t);
        thread.OmegaC[0][t] += w0;
        thread.OmegaB[0][t] += w1;
      }
      return basePath;
    }

    private static void ProcessPerturbed(int i, int[] dependentIndex, SimulationThreadAndResults thread,
                                         PathWiseExposure pe, PathWiseExposure ne, Dt[] exposureDates,
                                         int[] exposureDatesIndex,
                                         int nettingCount, SimulatedPath marketPath, SimulatedPath perturbedMarketPath,
                                         ISimulatedPathValues basePath, double[][][,] exoticPvs, bool withExotics, bool unilateral)
    {
      var baseEnvironment = thread.BaseThread.MarketData;
      var perturbedEnvironment = thread.PerturbedThread.MarketData;
      var basePortfolio = thread.BaseThread.PortfolioData;
      var perturbedPortfolio = thread.PerturbedThread.PortfolioData;
      var perturbedPath = new SimulatedPathValues(exposureDates.Length, nettingCount);
      int idx = marketPath.Id;
      perturbedPath.Clear();
      for (int t = 0; t < exposureDates.Length; ++t)
      {
        double numeraire, df, cptyRn, ownRn, survivalRn, cptySpread, ownSpread, lendSpread, borrowSpread;
        var dt = exposureDates[t];
        var time = exposureDatesIndex[t];
        var perturbedNettedPv = new double[basePath.NettingCount];
        for (int j = 0; j < basePath.NettingCount; ++j)
          perturbedNettedPv[j] = basePath.GetPortfolioValue(t, j);
        marketPath.Evolve(time, dt, baseEnvironment, unilateral, out numeraire, out df, out cptyRn, out ownRn, out survivalRn,
                          out cptySpread, out ownSpread, out lendSpread, out borrowSpread);
        perturbedMarketPath.Evolve(time, dt, perturbedEnvironment, unilateral, out numeraire, out df, out cptyRn,
                                   out ownRn, out survivalRn, out cptySpread, out ownSpread, out lendSpread, out borrowSpread);
        //keep perturbed quantities
        foreach (int pricerIdx in dependentIndex)
        {
          int nettingGroup = basePortfolio.Portfolio[pricerIdx].Item3;
          int ccy = basePortfolio.Portfolio[pricerIdx].Item2;
          double pv = basePortfolio.Portfolio[pricerIdx].Item1.FastPv(dt);
          double ppv = perturbedPortfolio.Portfolio[pricerIdx].Item1.FastPv(dt);
          if (ccy >= 0)
          {
            pv *= baseEnvironment.FxRates[ccy].GetRate(baseEnvironment.DiscountCurves[ccy + 1].Ccy,
                                                       baseEnvironment.DiscountCurves[0].Ccy);
            ppv *=
              perturbedEnvironment.FxRates[ccy].GetRate(
                perturbedEnvironment.DiscountCurves[ccy + 1].Ccy,
                perturbedEnvironment.DiscountCurves[0].Ccy);
          }
          perturbedNettedPv[nettingGroup] += ppv - pv;
        }
        if (withExotics)
        {
          for (int j = 0; j < basePath.NettingCount; ++j)
          {
            if (exoticPvs[0][j] == null)
              continue;
            var perturbedExoticExposure = exoticPvs[i][j];
            var exoticExposure = exoticPvs[0][j];
            perturbedNettedPv[j] += perturbedExoticExposure[idx, t] - exoticExposure[idx, t];
          }
        }
        perturbedPath.Add(perturbedNettedPv, df, numeraire, cptyRn, ownRn, survivalRn, cptySpread, ownSpread, lendSpread, borrowSpread,
                          perturbedMarketPath.Id, perturbedMarketPath.Weight);

        double dPdT = 1.0 / (numeraire * df);
        double wt = perturbedMarketPath.Weight;
        double w0 = wt * dPdT * cptyRn;
        double w1 = wt * dPdT * ownRn;
        thread.ExpExposure[i][t] += w0 * df * pe.Compute(perturbedPath, t);
        thread.NegExpExposure[i][t] += w1 * df * ne.Compute(perturbedPath, t);
        thread.OmegaC[i][t] += w0;
        thread.OmegaB[i][t] += w1;
      }
    }

    private class PerturbedSimulators : IDisposable
    {
      internal PerturbedSimulators(IEnumerable<Tuple<Simulator, Tuple<Dt[], double[]>[], int[]>> data)
      {
        data_ = data.ToList();
      }

      private bool _disposed;
      private List<Tuple<Simulator, Tuple<Dt[], double[]>[], int[]>> data_;

      internal Tuple<Simulator, Tuple<Dt[], double[]>[], int[]> this[int i]
      {
        get { return data_[i]; }
      }

      public void Dispose()
      {
        if (!_disposed)
        {
          foreach (var tuple in data_)
          {
            if (tuple != null && tuple.Item1 != null)
              tuple.Item1.Dispose();
          }
          _disposed = true;
        }
        GC.SuppressFinalize(this);
      }
    }

    private static void CalculateExposureSensitivities(
      Dt[] exposureDates,
      Simulator baseSimulator,
      CCRMarketEnvironment environment,
      VolatilityCollection volatilities,
      FactorLoadingCollection factorLoadings,
      PortfolioData portfolioData,
      bool unilateral,
      MultiStreamRng rng,
      Perturbation[] perturbations,
      Netting netting,
      IEnumerable<ISimulatedPathValues> baseSimulatedPaths,
      out double[][] expExposure,
      out double[][] negExpExposure,
      out double[] cva,
      out double[] dva)
    {
      if (perturbations == null || perturbations.Length == 0)
      {
        expExposure = new double[0][];
        negExpExposure = new double[0][];
        cva = new double[0];
        dva = new double[0];
        return;
      }
      int nettingCount = portfolioData.Map.Count;
      int count = perturbations.Length;
      int dateCount = exposureDates.Length;
      var exposureDatesIndex = Array.ConvertAll(exposureDates, dt => Array.IndexOf(baseSimulator.SimulationDates, dt));
      bool withExotics = (portfolioData.Exotics != null && portfolioData.Exotics.Length > 0);
      double[][][,] exoticExposures = null;
      if (withExotics)
        exoticExposures = ProcessExoticTrades(environment, volatilities, factorLoadings, rng, baseSimulator.PathCount,
                                              baseSimulator.SimulationDates, portfolioData, perturbations, exposureDates);
      //then vanilla trades
      var cptyIndex = environment.CptyIndex;
      var cptyRec = environment.CptyRecoveries;
      var baseCpty = GetIntegrationKernel(baseSimulator, cptyIndex);
      var pe = new PathWiseExposure(exposureDates, portfolioData.Map, netting,
                                    PathWiseExposure.RiskyParty.Counterparty);
      var ne = new PathWiseExposure(exposureDates, portfolioData.Map, netting,
                                    PathWiseExposure.RiskyParty.BookingEntity);
      var portfolio = portfolioData.Portfolio.Select(p => p.Item1.Pricer).ToArray();
      using (var sims = new PerturbedSimulators(perturbations.Select(p => p.GetPerturbedSimulator(baseSimulator, portfolio)).Select(
        s => new Tuple<Simulator, Tuple<Dt[], double[]>[], int[]>(s.Item1, GetIntegrationKernel(s.Item1, cptyIndex), s.Item2))))
      {
        var expectedExposure = ArrayUtil.Generate(count + 1, delegate { return new double[dateCount]; });
        var negExpectedExposure = ArrayUtil.Generate(count + 1, delegate { return new double[dateCount]; });
        cva = new double[count + 1];
        dva = new double[count + 1];
        var omegaC = ArrayUtil.Generate(count + 1, delegate { return new double[dateCount]; });
        var omegaB = ArrayUtil.Generate(count + 1, delegate { return new double[dateCount]; });
        //then price vanilla trades
        bool useBasePaths = (baseSimulatedPaths != null);
        var basePaths = useBasePaths ? baseSimulatedPaths.ToArray() : new ISimulatedPathValues[0];
        Parallel.For(0, baseSimulator.PathCount,
                     () =>
                     new SimulationThreadAndResults(rng, environment, portfolioData, count + 1, dateCount),
                     (idx, thread) =>
                     {
                       var marketPath = baseSimulator.GetSimulatedPath(useBasePaths ? basePaths[idx].Id : idx,
                                                                       thread.BaseThread.Rng);
                       var basePath = useBasePaths
                                        ? ProcessBasePath(thread, pe, ne, exposureDates, exposureDatesIndex,
                                                          marketPath, (SimulatedPathValues)basePaths[idx], unilateral)
                                        : ProcessBasePath(thread, pe, ne, exposureDates, exposureDatesIndex,
                                                          nettingCount, marketPath, exoticExposures, withExotics, unilateral);
                       for (int i = 0; i < count; ++i)
                       {
                         var sim = sims[i].Item1;
                         var dependentIndex = sims[i].Item3;
                         var perturbedMarketPath = sim.GetSimulatedPath(marketPath);
                         ProcessPerturbed(i + 1, dependentIndex, thread, pe, ne, exposureDates, exposureDatesIndex,
                                          nettingCount, marketPath, perturbedMarketPath, basePath, exoticExposures,
                                          withExotics, unilateral);
                         perturbedMarketPath.Dispose();
                       }
                       marketPath.Dispose();
                     },
                     thread =>
                     {
                       for (int i = 0; i <= count; ++i)
                       {
                         for (int t = 0; t < dateCount; ++t)
                         {
                           expectedExposure[i][t] += thread.ExpExposure[i][t];
                           negExpectedExposure[i][t] += thread.NegExpExposure[i][t];
                           omegaC[i][t] += thread.OmegaC[i][t];
                           omegaB[i][t] += thread.OmegaB[i][t];
                         }
                       }
                     });
        for (int i = 0; i <= count; ++i)
        {
          var ee = expectedExposure[i];
          var nee = negExpectedExposure[i];
          var oC = omegaC[i];
          var oB = omegaB[i];
          for (int t = 0; t < dateCount; ++t)
          {
            ee[t] /= oC[t];
            nee[t] /= oB[t];
          }
          if (cptyIndex.Length > 0)
            cva[i] =
              -CCRCalculations.Integrate(idx => ee[idx], exposureDates, environment.AsOf, (i == 0) ? baseCpty[0] : sims[i - 1].Item2[0], cptyRec[0]);
          if (cptyIndex.Length > 1)
            dva[i] = CCRCalculations.Integrate(idx => nee[idx], exposureDates, environment.AsOf, (i == 0) ? baseCpty[1] : sims[i - 1].Item2[1], cptyRec[1]);
        }
        expExposure = expectedExposure;
        negExpExposure = negExpectedExposure;
      }
    }

    private static double CalcCvaIntrinsic(this SimulatedPathValues path, Dt from, Dt[] exposureDates, PathWiseExposure pe, Tuple<Dt[], double[]> kernel,
                                           double recovery)
    {
      return -CCRCalculations.Integrate(idx => pe.Compute(path, idx) * path.GetDiscountFactor(idx), exposureDates, from, kernel, recovery);
    }

    private static double[] CalculateCvaHedges(
      Dt rebalancingDate,
      Dt[] exposureDates,
      CCRMarketEnvironment environment,
      VolatilityCollection volatilities,
      FactorLoadingCollection factorLoadings,
      PortfolioData portfolioData,
      bool unilateral,
      CcrPricer[] hedgePortfolio,
      Simulator simulator,
      MultiStreamRng rng,
      Netting netting,
      IList<ISimulatedPathValues> simulatedPaths)
    {
      if (simulator.PathCount < hedgePortfolio.Length)
        throw new ArgumentException(String.Format("At least {0} paths are required to compute {0} hedges", hedgePortfolio.Length));
      var ccy = environment.DiscountCurves.Skip(1).Select(d => d.Ccy).ToList(); 
      var hedgeTuples = hedgePortfolio.Select(o => new Tuple<CcrPricer, int>(o, ccy.IndexOf(o.Ccy))).ToArray();
      if (simulatedPaths == null) //generate simulatedPath
      {
        simulatedPaths = CalculateExposures(exposureDates, simulator, rng, environment, portfolioData, unilateral).Paths.ToList();
        foreach (var p in portfolioData.Exotics)
        {
          int nettingSet = p.Item3;
          var grid = GenerateLeastSquaresGrid(environment, simulator.SimulationDates, simulator.PathCount, rng.RngType, volatilities, factorLoadings, p,
                                              exposureDates);
          foreach (var v in simulatedPaths)
          {
            int idx = v.Id;
            for (int t = 0; t < v.DateCount; ++t)
              v.SetPortfolioValue(t, nettingSet, v.GetPortfolioValue(t, nettingSet) + grid[idx, t]);
          }
        }
      }
      rebalancingDate = rebalancingDate.IsEmpty()
                          ? exposureDates.First(dt => dt > simulator.AsOf)
                          : exposureDates.First(dt => dt >= rebalancingDate && dt > simulator.AsOf);
      var rebalancingDateIdx = Array.IndexOf(simulator.SimulationDates, rebalancingDate);
      var pmf = new double[simulator.PathCount];
      var currCvaWorkspace = new double[simulator.PathCount];
      var cvaWorkSpace = new double[simulator.PathCount];
      var hedgeWorkSpace = new double[simulator.PathCount,hedgePortfolio.Length];
      var hedgePvs = hedgeTuples.Select(p =>
                                        {
                                          var pv = p.Item1.FastPv(simulator.AsOf);
                                          if (p.Item2 >= 0)
                                            pv *=
                                              environment.FxRates[p.Item2].GetRate(environment.DiscountCurves[p.Item2 + 1].Ccy,
                                                                                   environment.DiscountCurves[0].Ccy); //hedges in domestic ccy
                                          return pv;
                                        }).ToArray();
      var integrationKernel = GetIntegrationKernel(simulator, environment.CptyIndex).First();
      Parallel.ForEach(simulatedPaths.ToArray(),
                       () =>
                       {
                         var tuple = CloneUtil.CloneObjectGraph(environment, hedgeTuples);
                         tuple.Item1.Conform();
                         return new Tuple<CCRMarketEnvironment, Tuple<CcrPricer, int>[], MultiStreamRng, PathWiseExposure>(tuple.Item1, tuple.Item2, rng.Clone(),
                                                                                                                           new PathWiseExposure(exposureDates,
                                                                                                                                                portfolioData.
                                                                                                                                                  Map, netting,
                                                                                                                                                PathWiseExposure
                                                                                                                                                  .RiskyParty.
                                                                                                                                                  Counterparty));
                       },
                       (p, tuple) =>
                       {
                         double numeraire, discountFactor, cptyRn, cptySpread, ownRn, ownSpread, lendSpread, borrowSpread, survivalRn;
                         var env = tuple.Item1;
                         var hedges = tuple.Item2;
                         var gen = tuple.Item3;
                         var pe = tuple.Item4;
                         var path = simulator.GetSimulatedPath(p.Id, gen);
                         var kernelDensity = simulator.SimulationDates.Select((dt, i) =>
                                                                              {
                                                                                path.Evolve(i, dt, env, unilateral, out numeraire, out discountFactor, out cptyRn, out ownRn,
                                                                                            out survivalRn, out cptySpread, out ownSpread, out lendSpread, out borrowSpread);
                                                                                return cptyRn * numeraire * discountFactor * integrationKernel.Item2[i];
                                                                              }).ToArray();
                         currCvaWorkspace[p.Id] = CalcCvaIntrinsic((SimulatedPathValues)p, simulator.AsOf, exposureDates, pe,
                                                                   new Tuple<Dt[], double[]>(integrationKernel.Item1, kernelDensity),
                                                                   env.CptyRecovery(0));
                         cvaWorkSpace[p.Id] = CalcCvaIntrinsic((SimulatedPathValues)p, rebalancingDate, exposureDates, pe,
                                                               new Tuple<Dt[], double[]>(integrationKernel.Item1, kernelDensity), env.CptyRecovery(0));
                         path.Evolve(rebalancingDateIdx, rebalancingDate, env, unilateral, out numeraire, out discountFactor, out cptyRn, out ownRn, out survivalRn,
                                     out cptySpread, out ownSpread, out lendSpread, out borrowSpread);
                         pmf[p.Id] = numeraire * discountFactor * p.Weight;
                         for (int i = 0; i < hedges.Length; ++i)
                         {
                           var hedge = hedges[i];
                           var pv = hedge.Item1.FastPv(rebalancingDate);
                           if (hedge.Item2 >= 0)
                             pv *= env.FxRates[hedge.Item2].GetRate(environment.DiscountCurves[hedge.Item2 + 1].Ccy,
                                                                    environment.DiscountCurves[0].Ccy);
                           hedgeWorkSpace[p.Id, i] = pv - hedgePvs[i]; //express hedges in domestic ccy
                         }
                       });
      double cva = currCvaWorkspace.Select((v, i) => v * pmf[i]).Aggregate(0.0, (v, tot) => tot + v);
      double norm = pmf.Aggregate(0.0, (p, tot) => tot + p);
      cva /= norm;
      cvaWorkSpace = cvaWorkSpace.Select(v => v / cva - 1.0).ToArray(); //normalize for numerical stability
      var retVal = new double[hedgePortfolio.Length];
      var u0 = hedgeWorkSpace;
      var w0 = new double[u0.GetLength(1)];
      var v0 = new double[u0.GetLength(1),u0.GetLength(1)];
      LinearSolvers.FactorizeSVD(u0, w0, v0); // u0 u0* projection on subspace spanned by hedges
      LinearSolvers.SolveSVD(u0, w0, v0, cvaWorkSpace, retVal, 1e-3); //calc hedges
      for (int i = 0; i < retVal.Length; ++i)
      {
        var p = hedgeTuples[i];
        retVal[i] *= (p.Item2 < 0)
                       ? cva
                       : cva * environment.FxRates[p.Item2].GetRate(environment.DiscountCurves[p.Item2 + 1].Ccy, environment.DiscountCurves[0].Ccy);
        //hedge notionals in domestic ccy
      }
      return retVal;
    }

    #endregion

    #region Sensitivities Formatting

    private static DataTable GenerateSensitivitiesTable(
      CCRCalculations calculations,
      Netting netting,
      Tuple<Perturbation[], bool> perturbations,
      DataTable dataTable)
    {
      using (var engine = calculations.CreateSimulator())
      {
        return GenerateSensitivitiesTable(calculations.ExposureDates, engine, calculations.Environment,
                                          calculations.Volatilities,
                                          calculations.FactorLoadings,
                                          calculations.Portfolio,
                                          calculations.IsUnilateral,
                                          calculations.CreateRng(engine),
                                          netting,
                                          perturbations,
                                          null, dataTable);
      }
    }


    private static DataTable GenerateSensitivitiesTable(
      CCRPathSimulator calculations,
      Netting netting,
      Tuple<Perturbation[], bool> perturbations,
      IEnumerable<ISimulatedPathValues> paths,
      DataTable dataTable)
    {
      var engine = calculations.Simulator;
      return GenerateSensitivitiesTable(calculations.ExposureDates, engine, calculations.OriginalMarketData,
                                        calculations.Volatilities,
                                        calculations.FactorLoadings,
                                        calculations.OriginalPortfolioData,
                                        calculations.IsUnilateral,
                                        calculations.Rng,
                                        netting, perturbations,
                                        paths, dataTable);
    }

    private static DataTable GenerateHedgesTable(
      CCRCalculations calculations,
      Netting netting,
      Dt rebalancingDate,
      IList<IPricer> hedgePricers,
      IList<CalibratedCurve> underlyingCurves,
      string[] hedgeTenors,
      DataTable dataTable)
    {
      using (var engine = calculations.CreateSimulator())
      {
        return GenerateHedgesTable(rebalancingDate, calculations.ExposureDates, engine, calculations.Environment,
                                   calculations.Volatilities,
                                   calculations.FactorLoadings,
                                   calculations.Portfolio,
                                   calculations.IsUnilateral,
                                   calculations.CreateRng(engine),
                                   netting,
                                   hedgePricers,
                                   underlyingCurves,
                                   hedgeTenors,
                                   null,
                                   dataTable);
      }
    }

    private static DataTable GenerateHedgesTable(
      CCRPathSimulator calculations,
      Netting netting,
      Dt rebalancingDate,
      IList<IPricer> hedgePricers,
      IList<CalibratedCurve> underlyingCurves,
      string[] hedgeTenors,
      IEnumerable<ISimulatedPathValues> paths,
      DataTable dataTable)
    {
      return GenerateHedgesTable(rebalancingDate, calculations.ExposureDates, calculations.Simulator, calculations.OriginalMarketData,
                                 calculations.Volatilities, calculations.FactorLoadings, calculations.OriginalPortfolioData, calculations.IsUnilateral, calculations.Rng,
                                 netting, hedgePricers, underlyingCurves, hedgeTenors, paths, dataTable);
    }

    private static IPricer TryGetPricer(this CalibratedCurve calibratedCurve, IProduct product)
    {
      IPricer retVal;
      try
      {
        retVal = calibratedCurve.Calibrator.GetPricer(calibratedCurve, product);
      }
      catch (Exception)
      {
        return null;
      }
      return retVal;
    }


    private static DataTable GenerateHedgesTable(
      Dt rebalancinDate,
      Dt[] exposureDates,
      Simulator simulator,
      CCRMarketEnvironment environment,
      VolatilityCollection volatilities,
      FactorLoadingCollection factorLoadings,
      PortfolioData portfolioData,
      bool unilateral,
      MultiStreamRng rng,
      Netting netting,
      IList<IPricer> hedgePricers,
      IList<CalibratedCurve> underlyingCurves,
      string[] hedgeTenors,
      IEnumerable<ISimulatedPathValues> basePaths,
      DataTable dataTable)
    {
      var hedgeNames = (hedgePricers != null) ? hedgePricers.Select(p => p.Product.Description).ToList() : new List<string>();
      hedgePricers = hedgePricers ?? new List<IPricer>();
      Func<CurveTenor, bool> predicate;
      if (hedgeTenors != null && hedgeTenors.Any())
        predicate = ten => hedgeTenors.Contains(ten.Name);
      else
        predicate = ten => true;
      if (underlyingCurves != null && underlyingCurves.Any())
      {
        foreach (var curve in underlyingCurves.Where(cc => cc.Tenors != null && cc.Calibrator != null))
        {
          foreach (
            var p in
              curve.Tenors.Where(predicate).Select(ten => new Tuple<string, IPricer>(ten.Name, curve.TryGetPricer(ten.Product))).Where(p => p.Item2 != null))
          {
            hedgeNames.Add(p.Item1);
            hedgePricers.Add(p.Item2);
          }
        }
      }
      if (hedgePricers.Count == 0)
        throw new ArgumentException("No valid hedging instruments have been specified.");
      var retVal = CalculateCvaHedges(rebalancinDate, exposureDates, environment, volatilities, factorLoadings, portfolioData, unilateral,
                                      hedgePricers.Select(CcrPricer.Get).ToArray(), simulator, rng, netting, (basePaths == null) ? null : basePaths.ToList());
      return TabulateHedges(hedgeNames.ToArray(), retVal, dataTable);
    }

    private static DataTable GenerateSensitivitiesTable(
      Dt[] exposureDates,
      Simulator baseSimulator,
      CCRMarketEnvironment environment,
      VolatilityCollection volatilities,
      FactorLoadingCollection factorLoadings,
      PortfolioData portfolioData,
      bool unilateral,
      MultiStreamRng rng,
      Netting netting,
      Tuple<Perturbation[], bool> perturbations,
      IEnumerable<ISimulatedPathValues> basePaths,
      DataTable dataTable)
    {
      double[][] ee, nee;
      double[] cva, dva;
      CalculateExposureSensitivities(exposureDates, baseSimulator, environment, volatilities, factorLoadings, portfolioData, unilateral,
                                     rng, perturbations.Item1, netting, basePaths, out ee, out nee, out cva, out dva);
      return TabulateSensitivities(perturbations, exposureDates, ee, nee, cva, dva, dataTable);
    }

    private static DataTable TabulateHedges(string[] hedgeNames, double[] hedges, DataTable dataTable)
    {
      if (dataTable == null)
      {
        dataTable = new DataTable("Cva Hedges Report");
        dataTable.Columns.Add(new DataColumn("InstrumentName", typeof(string)));
        dataTable.Columns.Add(new DataColumn("HedgeNotional", typeof(double)));
      }
      for (int i = 0; i < hedgeNames.Length; ++i)
      {
        DataRow dataRow = dataTable.NewRow();
        dataRow["InstrumentName"] = hedgeNames[i];
        dataRow["HedgeNotional"] = hedges[i];
        dataTable.Rows.Add(dataRow);
      }
      return dataTable;
    }

    private static DataTable TabulateSensitivities(Tuple<Perturbation[], bool> perturbations, Dt[] exposureDts,
                                                   double[][] ee, double[][] nee, double[] cva, double[] dva,
                                                   DataTable dataTable)
    {
      // Create DataTable if we need to
      bool calcGamma = perturbations.Item2;
      int offset = calcGamma ? 2 : 1;
      Perturbation[] pert = perturbations.Item1;
      var datesEe = Array.ConvertAll(exposureDts, dt => string.Concat("EE", dt.ToString()));
      var datesNee = Array.ConvertAll(exposureDts, dt => string.Concat("NEE", dt.ToString()));
      if (dataTable == null)
      {
        dataTable = new DataTable("Curve Sensitivity Report");
        dataTable.Columns.Add(new DataColumn("InputName", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Tenor", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Measure", typeof(string)));
        dataTable.Columns.Add(new DataColumn("Delta", typeof(double)));
        if (calcGamma)
          dataTable.Columns.Add(new DataColumn("Gamma", typeof(double)));
      }
      for (int i = 0, j = 1; i < pert.Length; i += offset, j += offset)
      {
        DataRow dataRowCva = dataTable.NewRow();
        DataRow dataRowDva = dataTable.NewRow();
        dataRowCva["InputName"] = dataRowDva["InputName"] = pert[i].Id;
        dataRowCva["Tenor"] = dataRowDva["Tenor"] = pert[i].Tenor;
        dataRowCva["Measure"] = "CVA";
        dataRowDva["Measure"] = "DVA";
        dataRowCva["Delta"] = cva[j] - cva[0];
        dataRowDva["Delta"] = dva[j] - dva[0];
        if (calcGamma)
        {
          dataRowCva["Gamma"] = cva[j] + cva[j + 1] - 2 * cva[0];
          dataRowDva["Gamma"] = dva[j] + dva[j + 1] - 2 * dva[0];
        }
        dataTable.Rows.Add(dataRowCva);
        dataTable.Rows.Add(dataRowDva);
        for (int t = 0; t < exposureDts.Length; t++)
        {
          DataRow dataRowEE = dataTable.NewRow();
          DataRow dataRowNEE = dataTable.NewRow();
          dataRowEE["InputName"] = dataRowNEE["InputName"] = pert[i].Id;
          dataRowEE["Tenor"] = dataRowNEE["Tenor"] = pert[i].Tenor;
          dataRowEE["Measure"] = datesEe[t];
          dataRowNEE["Measure"] = datesNee[t];
          dataRowEE["Delta"] = ee[j][t] - ee[0][t];
          dataRowNEE["Delta"] = nee[j][t] - nee[0][t];
          if (calcGamma)
          {
            dataRowEE["Gamma"] = ee[j][t] + ee[j + 1][t] - 2 * ee[0][t];
            dataRowNEE["Gamma"] = nee[j][t] + nee[j + 1][t] - 2 * nee[0][t];
          }
          dataTable.Rows.Add(dataRowEE);
          dataTable.Rows.Add(dataRowNEE);
        }
      }
      return dataTable;
    }

    #endregion
  }
}