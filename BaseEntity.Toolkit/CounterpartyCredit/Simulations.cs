/*
 * Simulations.cs
 *
 *  -2010. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using BaseEntity.Configuration;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  ///   Simulations
  /// </summary>
  [ObjectLoggerEnabled]
  public static partial class Simulations
  {
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(Simulations));

    #region Static Constructors

    /// <summary>
    /// Simulate future exposures using the Monte Carlo model
    /// </summary>
    /// <param name="modelChoice">The specification of the simulation model 
    /// and the random number generator. 
    /// Must support jumps to arbitrary points in the sequence</param>
    /// <param name="asOf">As of date</param>
    /// <param name="portfolio">Pricers for the underlying transactions</param>
    /// <param name="nettingId">Id of the respective netting sets</param>
    /// <param name="counterparty">Survival curves for the counterparty, booking entity, borrow curve, and lend curve.</param>
    /// <param name="cptyRecovery">Recovery rate of the counterparty/booking entity</param>
    /// <param name="tenors">Forward term structures tenors. These are assumed the same for every term structure in the environment</param>
    /// <param name="discountCurves">Discount curve for each ccy (numeraire asset for each currency)</param>
    /// <param name="fxRates">Underlying fx rates</param>
    /// <param name="forwardTermStructures">Term structures for processes that are martingales under T-forward measure associated to corresponding numeraire asset</param>
    /// <param name="creditCurves">Survival curves</param>
    /// <param name="volatilities">Volatility of each underlying process</param>
    /// <param name="factorLoadings">Factor correlations of each underlying process to driving brownian motions</param>
    /// <param name="cptyDefaultTimeCorrelation">Default time correlation between counterparty and booking entity</param>
    /// <param name="numberOfPaths">Sample size</param>
    /// <param name="exposureDates">Dates at which to simulate exposure. 
    /// If not provided, a set of standard dates is generated</param>
    /// <param name="gridSize">Max grid size for simulation of the underlying processes</param>
    /// <param name="unilateral">treat default unilaterally or jointly (first-to-default) </param>
    /// <param name="jumpsOnDefault">The jumps on default</param>
    /// <returns>CCRCalculations object</returns>
    public static ICounterpartyCreditRiskCalculations SimulateCounterpartyCreditRisks(
      SimulationModelChoice modelChoice,
      Dt asOf,
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
      int numberOfPaths,
      Dt[] exposureDates,
      Tenor gridSize,
      bool unilateral,
      IEnumerable<IJumpSpecification> jumpsOnDefault = null)
    {
      SurvivalCurve[] allCredits;
      var cptyIdx = GetCptyIndex(creditCurves, counterparty, out allCredits);
      var maxMaturity = portfolio.MaxMaturity(asOf);
      exposureDates = IsEmpty(exposureDates)
                        ? BuildStandardExposureDates(asOf, portfolio.MaxMaturity(asOf))
                        : exposureDates.SafeSort();
      if (exposureDates.Last() < maxMaturity)
        throw new ArgumentException("Last exposure date must follow latest portfolio maturity");
      var environment = new CCRMarketEnvironment(asOf, tenors.SafeSort(), gridSize, cptyIdx, cptyRecovery, discountCurves,
                                                 forwardTermStructures.GetForwardBased(volatilities, factorLoadings),
                                                 allCredits, fxRates,
                                                 forwardTermStructures.GetSpotBased(volatilities, factorLoadings),
                                                 jumpsOnDefault);
      var ccy = environment.DiscountCurves.Skip(1).Select(d => d.Ccy).ToArray();
      var portfolioData = new PortfolioData(ccy, portfolio, nettingId, null);
      return new CCRCalculations(modelChoice.SdeModel, numberOfPaths, exposureDates, environment, volatilities,
                                 factorLoadings, modelChoice.RngType, portfolioData, cptyDefaultTimeCorrelation, unilateral);
    }


    /// <summary>
    /// Simulate future exposures (used by Risk)
    /// </summary>
    /// <param name="modelChoice">The specification of the simulation model 
    /// and the random number generator. 
    /// Must support jumps to arbitrary points in the sequence</param>
    /// <param name="asOf">As of date</param>
    /// <param name="portfolio">Pricers for the underlying transactions</param>
    /// <param name="nettingId">Id of the respective netting sets, parallel to portfolio array</param>
    /// <param name="netting">netting and collateral provisions</param>
    /// <param name="counterparty">Id of the counterparty/booking entity</param>
    /// <param name="cptyRecovery">Recovery rate of the counterparty/booking entity</param>
    /// <param name="tenors">Forward term structures tenors. These are assumed the same for every term structure in the environment</param>
    /// <param name="discountCurves">Discount curve for each ccy (numeraire asset for each currency)</param>
    /// <param name="fxRates">Underlying fx rates</param>
    /// <param name="forwardTermStructures">Term structures for processes that are martingales under T-forward measure associated to corresponding numeraire asset</param>
    /// <param name="creditCurves">Survival curves</param>
    /// <param name="volatilities">Volatility curves</param>
    /// <param name="factorLoadings">Factor correlations of each underlying process to driving brownian motions</param>
    /// <param name="cptyDefaultTimeCorrelation">Default time correlation between counterparty and booking entity</param>
    /// <param name="numberOfPaths">Sample size</param>
    /// <param name="exposureDates">Dates at which to simulate exposure. If not provided, a set of standard dates is generated</param>
    /// <param name="precalculatedPvs">optionally pass pvs already calculated for AMC pricers</param>
    /// <param name="gridSize">Max grid size for simulation of the underlying processes</param>
    /// <param name="unilateral">treat default unilaterally or jointly (first-to-default) </param>
    /// <param name="jumpsOnDefault">The jumps on default</param>
    /// <returns>CCRCalculations object</returns>
    public static IRunSimulationPath CreatePathSimulator(
      SimulationModelChoice modelChoice,
      Dt asOf,
      IPricer[] portfolio,
      string[] nettingId,
      Netting netting,
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
      int numberOfPaths,
      Dt[] exposureDates,
      IList<double[,]> precalculatedPvs,
      Tenor gridSize, 
      bool unilateral,
      IEnumerable<IJumpSpecification> jumpsOnDefault = null)
    {
      SurvivalCurve[] allCredits;
      var cptyIdx = GetCptyIndex(creditCurves, counterparty, out allCredits);
      var maxMaturity = portfolio.MaxMaturity(asOf);
      exposureDates = IsEmpty(exposureDates)
                        ? BuildStandardExposureDates(asOf, maxMaturity)
                        : exposureDates.SafeSort();
      if (exposureDates.Last() < maxMaturity)
        throw new ArgumentException("Last exposure date must follow latest portfolio maturity");
      var environment = new CCRMarketEnvironment(asOf, tenors.SafeSort(), gridSize, cptyIdx, cptyRecovery, discountCurves,
                                                 forwardTermStructures.GetForwardBased(volatilities, factorLoadings),
                                                 allCredits, fxRates, forwardTermStructures.GetSpotBased(volatilities, factorLoadings),
                                                 jumpsOnDefault);
      var ccy = environment.DiscountCurves.Skip(1).Select(d => d.Ccy).ToArray();
      var portfolioData = new PortfolioData(ccy, portfolio, nettingId, null);
      var simulDates = GenerateSimulationDates(asOf, exposureDates, tenors, gridSize);
      Simulator simulator = CreateSimulator(modelChoice.SdeModel, numberOfPaths, simulDates, environment, volatilities, factorLoadings, cptyDefaultTimeCorrelation);
      MultiStreamRng rng = MultiStreamRng.Create(modelChoice.RngType, simulator.Dimension, simulator.SimulationTimeGrid);
      var sim = new CCRPathSimulator(exposureDates, simulator, environment, volatilities, factorLoadings, portfolioData, netting, rng, unilateral);
      if (precalculatedPvs != null)
        sim.PrecalculatedPvs = precalculatedPvs;
      return sim;
    }

    /// <summary>
    /// Simulate future exposures using the semi-analytic model based on Markov projections
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="portfolio">Pricers for the underlying transactions</param>
    /// <param name="nettingId">Id of the respective netting sets</param>
    /// <param name="counterparty">Id of the counterparty/booking entity</param>
    /// <param name="cptyRecovery">Recovery rate of the counterparty/booking entity</param>
    /// <param name="tenors">Forward term structures tenors. These are assumed the same for every term structure in the environment</param>
    /// <param name="discountCurves">Discount curve for each ccy (numeraire asset for each currency)</param>
    /// <param name="fxRates">Underlying fx rates</param>
    /// <param name="forwardTermStructures">Term structures for processes that are martingales under T-forward measure associated to corresponding numeraire asset</param>
    /// <param name="creditCurves">Survival curves</param>
    /// <param name="volatilities">Volatility curve of each rate</param>
    /// <param name="factorLoadings">Factor correlations of each rate to driving brownian motions</param>
    /// <param name="cptyDefaultTimeCorrelation">Default time correlation between counterparty and booking entity</param>
    /// <param name="numberOfPaths">Sample size</param>
    /// <param name="exposureDates">Dates at which to simulate exposure. If not provided, a set of standard dates is generated</param>
    /// <param name="flags">Simulator flags</param>
    /// <param name="gridSize">Max grid size for simulation of the underlying processes</param>
    /// <param name="unilateral">treat default unilaterally or jointly (first-to-default) </param>
    /// <param name="jumpsOnDefault">The jumps on default</param>
    /// <returns>CCRCalculations object</returns>
    public static ICounterpartyCreditRiskCalculations SimulateCounterpartyCreditRisks(
      Dt asOf,
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
      int numberOfPaths,
      Dt[] exposureDates,
      SimulatorFlags flags,
      Tenor gridSize, 
      bool unilateral,
      IEnumerable<IJumpSpecification> jumpsOnDefault = null)
    {
      SurvivalCurve[] allCredits;
      var cptyIdx = GetCptyIndex(creditCurves, counterparty, out allCredits);
      var maxMaturity = portfolio.MaxMaturity(asOf);
      exposureDates = IsEmpty(exposureDates)
                        ? BuildStandardExposureDates(asOf, maxMaturity)
                        : exposureDates.Where(dt => dt < maxMaturity).Union(new[] {maxMaturity, Dt.Add(maxMaturity, new Tenor(Frequency.Weekly))}).ToArray().
                            SafeSort();
      var environment = new CCRMarketEnvironment(asOf, tenors.SafeSort(), gridSize, cptyIdx, cptyRecovery, discountCurves,
                                                 forwardTermStructures.GetForwardBased(volatilities, factorLoadings),
                                                 allCredits, fxRates, forwardTermStructures.GetSpotBased(volatilities, factorLoadings),
                                                 jumpsOnDefault);
      var ccy = environment.DiscountCurves.Skip(1).Select(d => d.Ccy).ToArray();
      var portfolioData = new PortfolioData(ccy, portfolio, nettingId, null);
      return new ProjectiveCCRCalculations(numberOfPaths, exposureDates, flags, environment, volatilities, factorLoadings, portfolioData,
                                           cptyDefaultTimeCorrelation, unilateral);
    }

    #endregion

    #region Methods
    private static Dt MaxMaturity(this IEnumerable<IPricer> portfolio, Dt asOf)
    {
      var retVal = asOf;
      foreach (var pricer in portfolio)
      {
        if (pricer.Product != null && pricer.Product.Maturity > retVal)
          retVal = pricer.Product.Maturity;
      }
      return retVal;
    }


    private static bool IsEmpty<T>(this ICollection<T> array)
    {
      return (array == null || array.Count == 0);
    }

    private static int[] GetCptyIndex(SurvivalCurve[] creditCurves, IEnumerable<SurvivalCurve> counterparty,
                                      out SurvivalCurve[] allCredits)
    {
      var cptyIdx = new List<int>();
      var creditsSets = new HashSet<SurvivalCurve>();

      if (creditCurves != null)
      {
        Array.ForEach(creditCurves, x => creditsSets.Add(x));
      }

      var credits = creditsSets.ToList();
      
      if (counterparty != null)
      {
        foreach (var cp in counterparty)
        {
          int idx;
          if ((idx = credits.IndexOf(cp)) < 0)  // if not found, returns -1
          {
            credits.Add(cp);  // add the new credit curve
            idx = ~(credits.Count - 1);  // then idx is negative number
          }
          cptyIdx.Add(idx);
        }
      }
      allCredits = credits.ToArray();
      return cptyIdx.ToArray();
    }

    private static T[] SafeSort<T>(this T[] array) where T : IComparable<T>
    {
      if (array == null)
        return null;
      Array.Sort(array);
      return array;
    }

    private static T[] SafeToArray<T>(this IEnumerable<T> list)
    {
      if (list == null)
        return new T[0];
      return list.ToArray();
    }

    /// <summary>
    /// Generate simulation dates in the simulation engine.
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="exposureDates">Exposure calculation dates</param>
    /// <param name="tenors">Forward tenors defining the forward libor rates</param>
    /// <param name="gridSize">Max simulation time step size</param>
    /// <returns>A set of simulation dates</returns>
    public static Dt[] GenerateSimulationDates(Dt asOf, Dt[] exposureDates, Dt[] tenors, Tenor gridSize)
    {
      tenors = tenors.Where(t => t <= exposureDates.Last()).ToArray();
      var simulDates = new UniqueSequence<Dt> {tenors, exposureDates};
      if (gridSize.IsEmpty || gridSize.Years > 1.0)
        gridSize = new Tenor(Frequency.Annual);
      var clone = (UniqueSequence<Dt>)simulDates.Clone();
      Dt dt = asOf;
      int tolerance = GetToleranceDays(gridSize);
      foreach (var next in clone)
      {
        for (;;)
        {
          dt = Dt.Roll(Dt.Add(dt, gridSize), BDConvention.Following, Calendar.None);
          if (dt + tolerance < next)
            simulDates.Add(dt);
          else
          {
            dt = next;
            break;
          }
        }
      }
      return simulDates.ToArray();
    }

    private static int GetToleranceDays(Tenor tenor)
    {
      var days = tenor.Days;
      return (days < 7 ? 0 : (days < 60 ? 3 : (days * 5 / 100)));

    }

    private static void GetDiscountData(MarketEnvironment environment,
                                        Tuple<IAmericanMonteCarloAdapter, int, int> pricer,
                                        out DiscountCurve[] discountCurves, out FxRate[] fxRates)
    {
      var p = pricer.Item1;
      if (environment.DiscountCurves.Length == 0)
      {
        discountCurves = p.DiscountCurves.SafeToArray();
        fxRates = p.FxRates.SafeToArray();
        return;
      }
      var numeraire = environment.DiscountCurves[0];
      if ((pricer.Item2 < 0) || (p.DiscountCurves.Contains(numeraire)))
      {
        discountCurves = p.DiscountCurves.SafeToArray();
        fxRates = p.FxRates.SafeToArray();
        return;
      }
      var dc = (p.DiscountCurves != null) ? new List<DiscountCurve>(p.DiscountCurves) : new List<DiscountCurve>();
      dc.Add(numeraire);
      var fx = (p.FxRates != null) ? new List<FxRate>(p.FxRates) : new List<FxRate>();
      fx.Add(environment.FxRates[pricer.Item2]);
      discountCurves = dc.SafeToArray();
      fxRates = fx.SafeToArray();
    }

    internal static double[,] GenerateLeastSquaresGrid(CCRMarketEnvironment environment, Dt[] simulationDates,
                                                       int pathCount, MultiStreamRng.Type rngType,
                                                       VolatilityCollection volatilities,
                                                       FactorLoadingCollection factorLoadings,
                                                       Tuple<IAmericanMonteCarloAdapter, int, int> pricer,
                                                       Dt[] exposureDates)
    {
      DiscountCurve[] discountCurves;
      FxRate[] fxRates;
      GetDiscountData(environment, pricer, out discountCurves, out fxRates);
      var amcP = pricer.Item1;
      return LeastSquaresMonteCarloCcrPricer.Calculate(environment.AsOf, environment.Tenors, Tenor.Empty, simulationDates, pathCount,
                                                       rngType,
                                                       discountCurves,
                                                       fxRates,
                                                       amcP.ReferenceCurves.SafeToArray(),
                                                       amcP.SurvivalCurves.SafeToArray(),
                                                       volatilities,
                                                       factorLoadings, amcP.Notional,
                                                       amcP.ValuationCurrency,
                                                       environment.DiscountCurves[0].Ccy,
                                                       amcP.Cashflow,
                                                       amcP.Basis,
                                                       amcP.CallEvaluator,
                                                       amcP.PutEvaluator,
                                                       1e-3,
                                                       exposureDates);
    }


    /// <summary>
    /// Scenario Generation.
    /// </summary>
    /// <param name="exposureDates">Exposure dates</param>
    /// <param name="simulator">Simulator</param>
    /// <param name="generator">Random number generator</param>
    /// <param name="marketEnvironment">CCR Market Environment</param>
    /// <param name="portfolioData">CCR pricers</param>
    /// <param name="unilateral">Treat default unilaterally or jointly (first-to-default)</param>
    /// <param name="enableCashflowOptimizer">Bool type to indicate enable cashflow optimizer or not. Default true</param>
    /// <returns>Simulation scenarios</returns>
    /// <remarks>
    /// We generate scenarios via simulation of risk factors. Each scenario is a joint realization of risk factors 
    /// at various points in time, including discount factor (df), numeraire asset (numeraire), counterparty default Radon Nikodym derivative (cptyRn),
    /// booking entity default Radon Nikodym derivative (ownRn), booking entity survival Radon Nikodym derivative (survivalRn), 
    /// counterparty credit spread (cptySpread), booking entity credit spread (ownSpread), lend curve spread (lendSpread), borrow curve spread (borrowSpread). 
    /// </remarks>
    internal static ISimulatedValues CalculateExposures(Dt[] exposureDates, Simulator simulator,
                                                        MultiStreamRng generator,
                                                        CCRMarketEnvironment marketEnvironment,
                                                        PortfolioData portfolioData,
                                                        bool unilateral,
                                                        bool enableCashflowOptimizer = true)
    {
      if (ExposureCalculator != null)
      {
        var paths = ExposureCalculator.CalculateExposures(exposureDates,
          simulator, generator, marketEnvironment, portfolioData.Map.Count,
          portfolioData.Portfolio, unilateral);
        return new SimulatedValues(exposureDates, portfolioData.Map,
          paths, simulator.PathCount,
          (generator != null) ? generator.RngType : MultiStreamRng.Type.MersenneTwister,
          marketEnvironment.GridSize);
      }

      var exposureDateIndex = Array.ConvertAll(exposureDates, dt => Array.IndexOf(simulator.SimulationDates, dt));
      Action<int, SimulationThread> compute = (idx, thread) =>
                                                {
                                                  SimulatedPath path = simulator.GetSimulatedPath(idx, thread.Rng);
                                                  if (path == null)
                                                    return;
                                                  var portfolio = thread.PortfolioData;
                                                  var env = thread.MarketData;
                                                  var nettingCount = portfolio.Map.Count;
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

                                                    path.Evolve(t, dt, env, unilateral, out numeraire, out df, out cptyRn, out ownRn,
                                                                out survivalRn,
                                                                out cptySpread, out ownSpread, out lendSpread, out borrowSpread);
                                                    var v = CalculateFwdValues(nettingCount, dt, env,
                                                                               portfolio.Portfolio);
                                                    resultData.Add(v, df, numeraire, cptyRn, ownRn, survivalRn,
                                                                   cptySpread,
                                                                   ownSpread, lendSpread, borrowSpread, idx, path.Weight);
                                                  }
                                                  path.Dispose();
                                                  thread.Paths.Add(resultData);
                                                };
      var marketPaths = new List<ISimulatedPathValues>();
      Parallel.For(0, simulator.PathCount, () => new SimulationThread(generator, marketEnvironment, portfolioData),
                   (i, thread) => compute(i, thread),
                   thread =>
                     {
                       if (thread == null || thread.Paths == null)
                         return;
                       marketPaths.AddRange(thread.Paths);
                     }
        );
      return new SimulatedValues(exposureDates, portfolioData.Map, marketPaths, simulator.PathCount,
                                 (generator != null) ? generator.RngType : MultiStreamRng.Type.MersenneTwister,
                                 marketEnvironment.GridSize);
    }

    /// <summary>
    /// Run simulation and calculate exposures and discount factors for a single pricer
    /// </summary>
    /// <returns>tuple containing exposures,discounts </returns>
    public static Tuple<double[,], double[,]> CalculateExposures(Simulator simulator,
                                                     MarketEnvironment marketEnvironment,
                                                     ISimulationPricer pricer, 
                                                     Func<int, SimulatedPath> getPath,
                                                     bool enableCashflowOptimizer = true
                                                     )
    { 
      var dateCount = pricer.ExposureDates.Length;
      var exposures = new double[simulator.PathCount, dateCount];
      var discountFactors = new double[simulator.PathCount, dateCount];
      var exposureDateIndex = Array.ConvertAll(pricer.ExposureDates, 
                                                (dt) => Array.BinarySearch(simulator.SimulationDates, dt));
      var ccy = marketEnvironment.DiscountCurves.Skip(1).Select(d => d.Ccy).ToArray();

      for (int pathIdx = 0; pathIdx < simulator.PathCount; pathIdx++)
      {
        SimulatedPath path = getPath(pathIdx);
        if (path == null)
          return null;

        for (int time = 0; time < dateCount; ++time)
        {
          var t = exposureDateIndex[time];
          var dt = pricer.ExposureDates[time];
          var days = Dt.FractDiff(simulator.AsOf, dt);
          path.Evolve(t, dt, days, marketEnvironment);

          var pv = pricer.FastPv(dt);
          if (double.IsNaN(pv))
          {
            Logger.VerboseFormat("Fast PV returned NaN");
            pv = 0.0;
          }
          if (double.IsInfinity(pv))
          {
            Logger.VerboseFormat("Fast PV returned Infinity");
            pv = 0.0;
          }

          var ccyIdx = Array.IndexOf(ccy, pricer.Ccy); 
          if (ccyIdx >= 0)
          {
            var fx = marketEnvironment.FxRates[ccyIdx];
            pv *= fx.GetRate(pricer.Ccy, marketEnvironment.DiscountCurves[0].Ccy);
          }
          exposures[pathIdx, time] = pv;
          discountFactors[pathIdx, time] = marketEnvironment.DiscountCurves[0].Interpolate(dt); 
        }
      }
      return new Tuple<double[,], double[,]>(exposures, discountFactors);
    }


    /// <summary>
    /// Run simulation and calculate exposures and discount factors for a swap pricer and swap leg pricer representing the a 1bp coupon added to one of the swap legs
    /// </summary>
    /// <returns>tuple containing exposures,,discounts </returns>
    public static Tuple<double[,], double[,], double[,]> CalculateExposuresAndCoupon01(Simulator simulator,
                                                     MarketEnvironment marketEnvironment,
                                                     ISimulationPricer pricer,
                                                     ISimulationPricer coupon01Pricer,
                                                     Func<int, SimulatedPath> getPath,
                                                     bool enableCashflowOptimizer = true
                                                     )
    {
      var dateCount = pricer.ExposureDates.Length;
      var exposures = new double[simulator.PathCount, dateCount];
      var coupon01Exposures = new double[simulator.PathCount, dateCount];
      var discountFactors = new double[simulator.PathCount, dateCount];
      var exposureDateIndex = Array.ConvertAll(pricer.ExposureDates,
                                                (dt) => Array.BinarySearch(simulator.SimulationDates, dt));
      var ccy = marketEnvironment.DiscountCurves.Skip(1).Select(d => d.Ccy).ToArray();

      for (int pathIdx = 0; pathIdx < simulator.PathCount; pathIdx++)
      {
        SimulatedPath path = getPath(pathIdx);
        if (path == null)
          return null;

        for (int time = 0; time < dateCount; ++time)
        {
          var t = exposureDateIndex[time];
          var dt = pricer.ExposureDates[time];
          var days = Dt.FractDiff(simulator.AsOf, dt);
          path.Evolve(t, dt, days, marketEnvironment);
          
          var pv = pricer.FastPv(dt);
          if (double.IsNaN(pv))
          {
            Logger.VerboseFormat("Fast PV returned NaN");
            pv = 0.0;
          }
          if (double.IsInfinity(pv))
          {
            Logger.VerboseFormat("Fast PV returned Infinity");
            pv = 0.0;
          }

          var ccyIdx = Array.IndexOf(ccy, pricer.Ccy);
          if (ccyIdx >= 0)
          {
            var fx = marketEnvironment.FxRates[ccyIdx];
            pv *= fx.GetRate(pricer.Ccy, marketEnvironment.DiscountCurves[0].Ccy);
          }
          exposures[pathIdx, time] = pv;

          var couponPv = coupon01Pricer.FastPv(dt);
          if (double.IsNaN(couponPv))
          {
            Logger.VerboseFormat("Fast CouponPv returned NaN");
            pv = 0.0;
          }
          if (double.IsInfinity(couponPv))
          {
            Logger.VerboseFormat("Fast CouponPv returned Infinity");
            pv = 0.0;
          }

          ccyIdx = Array.IndexOf(ccy, coupon01Pricer.Ccy);
          if (ccyIdx >= 0)
          {
            var fx = marketEnvironment.FxRates[ccyIdx];
            couponPv *= fx.GetRate(coupon01Pricer.Ccy, marketEnvironment.DiscountCurves[0].Ccy);
          }
          coupon01Exposures[pathIdx, time] = couponPv + pv;
          discountFactors[pathIdx, time] = marketEnvironment.DiscountCurves[0].Interpolate(dt);
        }
      }
      return new Tuple<double[,], double[,], double[,]>(exposures, coupon01Exposures, discountFactors);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pricers"></param>
    /// <returns></returns>
    public static Dt[] GetUnionOfExposureDates(ISimulationPricer[] pricers)
    {
      var lst = new UniqueSequence<Dt>();
      foreach (var simulationPricer in pricers)
      {
        lst.Add(simulationPricer.ExposureDates);
      }
      return lst.ToArray(); 
    }

    

    internal static double[] CalculateFwdValues(
      int nettingCount,
      Dt fwdDate,
      CCRMarketEnvironment environment,
      Tuple<CcrPricer, int, int>[] portfolio) // the first int is currency index, the second int is netting group index.
    {
      var values = new double[nettingCount];
      foreach (var p in portfolio)
      {
        var pv = p.Item1.FastPv(fwdDate);
        if (double.IsNaN(pv))
          continue;
        if (double.IsInfinity(pv))
          continue;
        if (p.Item2 >= 0)
        {
          var fx = environment.FxRates[p.Item2];
          pv *= fx.GetRate(environment.DiscountCurves[p.Item2 + 1].Ccy, environment.DiscountCurves[0].Ccy);
        }
        values[p.Item3] += pv;
      }
      return values;
    }

    internal static Tuple<double[], double[]> CalculateFwdValuesByTrade(
      int nettingCount,
      Dt fwdDate,
      CCRMarketEnvironment environment,
      Tuple<CcrPricer, int, int>[] portfolio)
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug(string.Format("Calculating Fwd Values for {0}...", fwdDate));
      }
      var nettedValues = new double[nettingCount];
      var tradeValues = new double[portfolio.Length];
      for (int i = 0; i < portfolio.Length; i++)
      {
        var p = portfolio[i];
        double pv = p.Item1.FastPv(fwdDate);
        if (double.IsNaN(pv))
        {
          Logger.Debug(string.Format("Pv index: {0} is NaN, setting to 0", i));
          tradeValues[i] = 0.0;
          continue;
        }
        if (double.IsInfinity(pv))
        {
          Logger.Debug(string.Format("Pv index: {0} is Infinity, setting to 0", i));
          tradeValues[i] = 0.0;
          continue;
        }

        if (p.Item2 >= 0)
        {
          var fx = environment.FxRates[p.Item2];
          pv *= fx.GetRate(environment.DiscountCurves[p.Item2 + 1].Ccy, environment.DiscountCurves[0].Ccy);
        }
        tradeValues[i] = pv;
        nettedValues[p.Item3] += pv;
      }
      if (Logger.IsDebugEnabled)
      {
        LogTradeAndNettedValues(nettedValues, tradeValues);
      }
      return new Tuple<double[], double[]>(nettedValues, tradeValues);
    }

    private static void LogTradeAndNettedValues(double[] nettedValues, double[] tradeValues)
    {
      LogDoubleArray(nettedValues, "Trade Values: ", "Trade Values Array is Null", "Trade Values Array Is Empty");
      LogDoubleArray(tradeValues, "Netted Values: ", "Netted Values Array is Null", "Netted Values Array Is Empty");
    }

    private static void LogDoubleArray(double[] array, string heading, string messageIfArrayIsNull, string messageIfArrayIsEmpty)
    {
      var builder = new StringBuilder();
      if (array == null)
      {
        Logger.Debug(messageIfArrayIsNull);
      }
      else if (array.Length == 0)
      {
        Logger.Debug(messageIfArrayIsEmpty);
      }
      else
      {
        builder.Append(heading);
        var i = 0;
        for (; i < array.Length - 1; ++i)
        {
          builder.Append(array[i] + ", ");
        }
        builder.Append(array[i]);
      }
      Logger.Debug(builder.ToString());
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
      internal readonly PortfolioData PortfolioData;

      /// <summary>
      /// Random number generator
      /// </summary>
      internal readonly MultiStreamRng Rng;

      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="rng">Random number generator</param>
      /// <param name="marketData">Market data</param>
      /// <param name="portfolioData">Portfolio data</param>
      internal SimulationThread(MultiStreamRng rng, CCRMarketEnvironment marketData, PortfolioData portfolioData)
      {
        Paths = new List<ISimulatedPathValues>();
        Rng = (rng == null) ? null : rng.Clone();
        var env = CloneUtil.CloneObjectGraph(marketData, portfolioData);
        MarketData = env.Item1;
        PortfolioData = env.Item2;
        MarketData.Conform();
      }
    }

    #endregion

    #region SimulatedValues

    /// <summary>
    /// Simulated values repository
    /// </summary>
    [Serializable]
    private class SimulatedValues : ISimulatedValues
    {
      #region Constructor

      /// <summary>
      /// Simulated values 
      /// </summary>
      /// <param name="exposureDates">Exposure dates</param>
      /// <param name="nettingMap">Map between netting group id and netting group index</param>
      /// <param name="paths">Realized scenarios</param>
      /// <param name="pathCount"></param>
      /// <param name="rngType">type of random number generator</param>
      /// <param name="gridSize">max time-step in simulated paths</param>
      internal SimulatedValues(Dt[] exposureDates, Dictionary<string, int> nettingMap,
                               IEnumerable<ISimulatedPathValues> paths, int pathCount, MultiStreamRng.Type rngType,
                               Tenor gridSize)
      {
        DateCount = exposureDates.Length;
        ExposureDates = exposureDates;
        NettingMap = nettingMap;
        Paths = paths;
        PathCount = pathCount;
        RngType = rngType;
        GridSize = gridSize;
      }

      #endregion

      #region Properties

      /// <summary>
      /// Max time-step used in simulation
      /// </summary>
      public Tenor GridSize { get; private set; }

      /// <summary>
      /// Number of dates
      /// </summary>
      public int DateCount { get; private set; }

      /// <summary>
      /// Exposure dates
      /// </summary>
      public Dt[] ExposureDates { get; private set; }

      /// <summary>
      /// Number of paths
      /// </summary>
      public int PathCount { get; private set; }

      /// <summary>
      /// Netting group information
      /// </summary>
      public Dictionary<string, int> NettingMap { get; private set; }

      /// <summary>
      /// List of simulated realizations
      /// </summary>
      public IEnumerable<ISimulatedPathValues> Paths { get; private set; }

      /// <summary>
      /// RngType 
      /// </summary>
      public MultiStreamRng.Type RngType { get; private set; }

      #endregion
    }

    #endregion
  }

  // class Simulations
}