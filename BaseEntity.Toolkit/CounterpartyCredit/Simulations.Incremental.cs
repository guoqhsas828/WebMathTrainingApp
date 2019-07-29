/*
 * Simulations.cs
 *
 *  -2010. All rights reserved.
 *
 */

using System;
using System.Linq;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Pricers;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  ///   Simulations
  /// </summary>
  public static partial class Simulations
  {
    #region Static constructors

    /// <summary>
    /// Simulate incremental exposures using the full monte carlo model and saved paths
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="oldPaths">Saved exposure cube</param>
    /// <param name="pathCount"> </param>
    /// <param name="portfolio">Pricers for the underlying transactions</param>
    /// <param name="nettingId">Id of the respective netting sets</param>
    /// <param name="counterparty">Id of the counterparty/booking entity</param>
    /// <param name="cptyRecovery">Recovery rate of the counterparty/booking entity</param>
    /// <param name="tenors">Forward term structures tenors. These are assumed the same for every term structure in the environment</param>
    /// <param name="discountCurves">Discount curve for each ccy (numeraire asset for each currency)</param>
    /// <param name="fxRates">Underlying fx rates</param>
    /// <param name="forwardTermStructures">Term structures for processes that are martingales under T-forward measure associated to corresponding numeraire asset</param>
    /// <param name="creditCurves">Survival curves</param>
    /// <param name="volatilities">Volatility curve of each underlying</param>
    /// <param name="factorLoadings">Factor correlations of each underlying to driving brownian motions</param>
    /// <param name="cptyDefaultTimeCorrelation">Correlation between counterparty and booking entity</param>
    /// <param name="unilateral">treat default unilaterally or jointly (first-to-default)</param>
    /// <param name="jumpsOnDefault">The jumps on default</param>
    /// <returns>CCRCalculations object</returns>
    /// <remarks>If n discount curves are provided, fxRates[i], for i from 0 to n-1 should be the fx rate between denomination currency of rateCurves[0],
    /// and the the denomination currency of rateCurves[i+1].
    /// Volatilities and correlations are needed only for such n-1 fx rates.  
    /// </remarks>
    public static ICounterpartyCreditRiskCalculations SimulateCounterpartyCreditRisks(
      Dt asOf,
      ISimulatedValues oldPaths,
      int pathCount,
      IPricer[] portfolio,
      string[] nettingId,
      SurvivalCurve[] counterparty,
      double[] cptyRecovery,
      Dt[] tenors,
      DiscountCurve[] discountCurves,
      FxRate[] fxRates,
      CalibratedCurve[] forwardTermStructures,
      SurvivalCurve[] creditCurves,
      VolatilityCollection volatilities,
      FactorLoadingCollection factorLoadings,
      double cptyDefaultTimeCorrelation, 
      bool unilateral,
      IEnumerable<IJumpSpecification> jumpsOnDefault = null)
    {
      SurvivalCurve[] allCredits;
      var cptyIdx = GetCptyIndex(creditCurves, counterparty, out allCredits);
      var dateShift = Dt.Diff(oldPaths.ExposureDates[0], asOf);
      var incrementalDates = (dateShift <= 0)
                            ? oldPaths.ExposureDates
                            : ArrayUtil.Generate(oldPaths.DateCount,
                                                 i => Dt.Add(oldPaths.ExposureDates[i], dateShift, TimeUnit.Days));
      var environment = new CCRMarketEnvironment(asOf, tenors, oldPaths.GridSize, cptyIdx, cptyRecovery, discountCurves,
                                                 forwardTermStructures.GetForwardBased(volatilities, factorLoadings),
                                                 allCredits, fxRates, forwardTermStructures.GetSpotBased(volatilities, factorLoadings),
                                                 jumpsOnDefault);

      var ccy = environment.DiscountCurves.Skip(1).Select(d => d.Ccy).ToArray(); 
      var portfolioData = new PortfolioData(ccy, portfolio, nettingId, oldPaths.NettingMap);
      return new IncrementalCCRCalculations(oldPaths, pathCount, incrementalDates, environment, volatilities, factorLoadings, cptyDefaultTimeCorrelation,
                                         oldPaths.RngType, portfolioData, unilateral);
    }

    #endregion

    #region Methods

    internal static ISimulatedValues CalculateExposures(IList<ISimulatedPathValues> oldPaths, 
                                                        Dt[] exposureDates,
                                                        Simulator simulator,
                                                        MultiStreamRng generator, CCRMarketEnvironment marketEnvironment,
                                                        PortfolioData portfolioData,
                                                        bool unilateral)
    {
      var exposureDateIndex = Array.ConvertAll(exposureDates, dt => Array.IndexOf(simulator.SimulationDates, dt));
      Action<ISimulatedPathValues, SimulationThread> compute = (oldPath, thread) =>
                                                                 {
                                                                   var path =
                                                                     simulator.GetSimulatedPath(oldPath.Id, thread.Rng);
                                                                   if (path == null)
                                                                     return;
                                                                   var portfolio = thread.PortfolioData;
                                                                   var env = thread.MarketData;
                                                                   int dateCount = exposureDates.Length;
                                                                   int nettingCount = portfolio.Map.Count;
                                                                   var resultData = new SimulatedPathValues(dateCount,
                                                                                                            nettingCount);

                                                                   for (int time = 0; time < dateCount; ++time)
                                                                   {
                                                                     double df,
                                                                            numeraire,
                                                                            cptyRn,
                                                                            ownRn,
                                                                            survivalRn,
                                                                            cptySpread,
                                                                            ownSpread,
                                                                            lendSpread,
                                                                            borrowSpread;
                                                                     Dt dt = exposureDates[time];
                                                                     var t = exposureDateIndex[time];
                                                                     path.Evolve(t, dt, env, unilateral, out numeraire, out df,
                                                                                 out cptyRn, out ownRn, out survivalRn,
                                                                                 out cptySpread, out ownSpread, out lendSpread, out borrowSpread);
                                                                     var v = CalculateFwdValues(nettingCount,
                                                                                                dt, env,
                                                                                                portfolio.Portfolio);
                                                                     for (int i = 0; i < oldPath.NettingCount; ++i)
                                                                       v[i] += oldPath.GetPortfolioValue(time, i);
                                                                     resultData.Add(v, df, numeraire, cptyRn, ownRn,
                                                                                    survivalRn,
                                                                                    cptySpread, ownSpread, lendSpread, borrowSpread,
                                                                                    path.Id,
                                                                                    path.Weight);
                                                                   }
                                                                   path.Dispose();
                                                                   resultData.OldPath = oldPath;
                                                                   thread.Paths.Add(resultData);
                                                                 };
      var marketPaths = new List<ISimulatedPathValues>();
      Parallel.Enumerate(oldPaths, () => new SimulationThread(generator, marketEnvironment, portfolioData),
                         (path, thread) => compute(path, thread), (thread) =>
                                                                    {
                                                                      if (thread == null || thread.Paths == null)
                                                                        return;
                                                                      marketPaths.AddRange(thread.Paths);
                                                                    });
      return new SimulatedValues(exposureDates, portfolioData.Map, marketPaths, oldPaths.Count, generator.RngType,
                                 marketEnvironment.GridSize);
    }

    #endregion
  }
}