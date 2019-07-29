/*
 * Simulations.cs
 *
 *  -2010. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  ///   Simulations
  /// </summary>
  public static partial class Simulations
  {
    #region Static Constructors

    /// <summary>
    /// Construct a simulator for Monte Carlo method.
    /// </summary>
    /// <param name="simulatorModel">Simulation model (i.e., LIBOR market model, Hull-White model)</param>
    /// <param name="numberOfPaths">Sample size</param>
    /// <param name="datesToSimulate">Exposure dates</param>
    /// <param name="env">Market environment (term structures)</param>
    /// <param name="volatilities">Libor rate volatility curves</param>
    /// <param name="factorLoadings">Libor rate factor loadings</param>
    /// <param name="cptyDefaultTimeCorrelation">Correlation between default time of counterparty and booking entity</param>
    /// <returns>Simulator</returns>
    /// <remarks>Forward prices are all processes that are martingales under the appropriate T forward measure</remarks>
    internal static Simulator CreateSimulator(
      ISimulationModel simulatorModel,
      int numberOfPaths,
      Dt[] datesToSimulate,
      CCRMarketEnvironment env,
      VolatilityCollection volatilities,
      FactorLoadingCollection factorLoadings,
      double cptyDefaultTimeCorrelation
      )
    {
      return Simulator.Create(
        simulatorModel ?? SimulationModels.LiborMarketModel,
        numberOfPaths, datesToSimulate,
        env, volatilities, factorLoadings,
        env.CptyIndex, cptyDefaultTimeCorrelation);
    }

    /// <summary>
    /// Construct a simulator for  Semi-Analytic method.
    /// </summary>
    /// <param name="quadraturePoints">Number of quadrature points</param>
    /// <param name="datesToSimulate">Exposure dates</param>
    /// <param name="flags">Simulator flags</param>
    /// <param name="env">Market environment (term structures)</param>
    /// <param name="volatilities">underlyings' volatilities</param>
    /// <param name="factorLoadings">underlyings' factor loadings</param>
    /// <param name="cptyDefaultTimeCorrelation">Correlation between default time of counterparty and booking entity</param>
    /// <returns>Simulator</returns>
    /// <remarks> 
    /// The exposure distribution is computed by numerical integration rather than MC simulation by taking low-dimensional 
    /// Markovian projections of the underlying processes.</remarks>
    internal static Simulator CreateProjectiveSimulator(
      int quadraturePoints,
      Dt[] datesToSimulate,
      SimulatorFlags flags,
      CCRMarketEnvironment env,
      VolatilityCollection volatilities,
      FactorLoadingCollection factorLoadings,
      double cptyDefaultTimeCorrelation
      )
    {
      var cptyIdx = Array.ConvertAll(env.CptyIndex, i => (i >= 0) ? i : ~i);
      if (factorLoadings.FactorCount > 2)
        throw new ArgumentException(
          String.Format("{0} factors semi-analytic model not supported",
            factorLoadings.FactorCount));
      var engine = Simulator.CreateProjectiveSimulatorFactory(env.AsOf, datesToSimulate,
        env.Tenors, quadraturePoints, factorLoadings.MarketFactorNames);
      if ((flags & SimulatorFlags.Credit) != 0)
      {
        if (env.DiscountCurves.Length > 0)
        {
          var tenorCount = env.Tenors.Length;
          engine.AddDomesticDiscount(env.DiscountCurves[0],
            new VolatilityCurve[tenorCount],
            new double[tenorCount, factorLoadings.FactorCount], false);
        }
        for (int i = 0; i < env.CreditCurves.Length; ++i)
        {
          var sc = env.CreditCurves[i];
          int offset = Array.IndexOf(cptyIdx, i);
          bool active = (offset < 0 || env.CptyIndex[offset] >= 0);
          engine.AddSurvival(env.CreditCurves[i], volatilities.GetVols(sc),
            factorLoadings.GetFactors(sc), active);
        }
      }
      else if ((flags & SimulatorFlags.Forward) != 0)
      {
        if (env.DiscountCurves.Length > 0)
        {
          engine.AddDomesticDiscount(env.DiscountCurves[0],
            volatilities.GetVols(env.DiscountCurves[0]),
            factorLoadings.GetFactors(env.DiscountCurves[0]), true);
        }
        foreach (var fc in env.ForwardCurves)
        {
          engine.AddForward(fc, volatilities.GetVols(fc), factorLoadings.GetFactors(fc),
            true);
        }
      }
      else if ((flags & SimulatorFlags.Rate) != 0 && env.DiscountCurves.Length == 1)
      {
        engine.AddDomesticDiscount(env.DiscountCurves[0],
          volatilities.GetVols(env.DiscountCurves[0]),
          factorLoadings.GetFactors(env.DiscountCurves[0]),
          true);
      }
      else if ((flags & SimulatorFlags.Rate) != 0 && env.DiscountCurves.Length > 1)
      {
        engine.AddDomesticDiscount(env.DiscountCurves[0],
          volatilities.GetVols(env.DiscountCurves[0]),
          factorLoadings.GetFactors(env.DiscountCurves[0]), true);
        for (int i = 1; i < env.DiscountCurves.Length; ++i)
        {
          engine.AddDiscount(env.DiscountCurves[i],
            volatilities.GetVols(env.DiscountCurves[i]),
            factorLoadings.GetFactors(env.DiscountCurves[i]),
            env.FxRates[i - 1], volatilities.GetVols(env.FxRates[i - 1]),
            factorLoadings.GetFactors(env.FxRates[i - 1]), true);
        }
      }
      else if ((flags & SimulatorFlags.Spot) != 0)
      {
        if (env.DiscountCurves.Length > 0)
          engine.AddDomesticDiscount(env.DiscountCurves[0],
            volatilities.GetVols(env.DiscountCurves[0]),
            factorLoadings.GetFactors(env.DiscountCurves[0]), true);

        foreach (var cc in env.SpotBasedCurves)
        {
          var sp = cc as IForwardPriceCurve;
          if (sp == null || sp.Spot == null)
            continue;
          if (sp.CarryCashflow.Any(d => d.Item2 == DividendSchedule.DividendType.Fixed))
            throw new ArgumentException(
              "Only proportional (discrete) dividends are supported.");
          engine.AddSpot(sp.Spot, cc, volatilities.GetVols(sp.Spot),
            factorLoadings.GetFactors(sp.Spot),
            sp.CarryRateAdjustment,
            sp.CarryCashflow.Select(d => new Tuple<Dt, double>(d.Item1, d.Item3)).ToArray(),
            true);
        }
      }
      else
        throw new ArgumentException("Simulator not supported");
      if (cptyIdx.Length > 0)
      {
        foreach (var idx in cptyIdx)
        {
          if (engine.Simulator.Map.ContainsKey(env.CreditCurves[idx]))
            continue;
          engine.AddSurvival(env.CreditCurves[idx],
            volatilities.GetVols(env.CreditCurves[idx]),
            factorLoadings.GetFactors(env.CreditCurves[idx]), false);
        }
        engine.AddRadonNykodim(cptyIdx,
          Array.ConvertAll(cptyIdx, i => env.CreditCurves[i]), cptyDefaultTimeCorrelation);
      }
      return engine.Simulator;
    }

    #endregion

    #region Utils

    /// <summary>
    ///  Build standard exposure dates in case that the user doesn't specify the exposure dates. 
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="terminal">terminal date</param>
    /// <returns>A set of exposure dates</returns>
    /// <remarks>
    /// If not provided, a set of standard exposure dates is generated by the following steps: 
    /// <list>
    /// <item><description>Set the asOf date as the first exposure date in the set.</description></item>
    /// <item><description>Add the dates of the first four weeks from the asOf Date to the set.</description></item>
    /// <item><description>Add the dates of the first eleven months from the last exposure date to the set.</description></item>
    /// <item><description>Add the dates before the terminal date that are years from the last exposure date to the set.</description></item>
    /// <item><description>Add the terminal date and the date that is one week after the terminal date to the set. </description></item>
    /// </list>.
    /// </remarks>
    /// <example>
    /// If the asOf date is 19-Jul-10 and terminal date is 19-Jul-15, 
    /// the set of standard exposure dates is given by: 
    /// {19-Jul-10, 26-Jul-10, 02-Aug-10, 09-Aug-10, 16-Aug-10, 16-Sep-10, 16-Oct-10, 16-Nov-10, 16-Dec-10, 
    /// 16-Jan-11, 16-Feb-11, 16-Mar-11, 16-Apr-11, 16-May-11, 16-Jun-11, 16-Jul-11, 16-Jul-12, 16-Jul-13,
    /// 16-Jul-13, 16-Jul-14, 16-Jul-15, 19-Jul-15, 26-Jul-15}.
    /// </example>
    private static Dt[] BuildStandardExposureDates(Dt asOf, Dt terminal)
    {
      var dts = new List<Dt> {asOf};
      Dt start = asOf;
      int weeks = 0;
      int months = 0;
      for (;;)
      {
        if (weeks < 4)
        {
          weeks++;
          start = Dt.Add(start, 1, TimeUnit.Weeks);
          if (start >= terminal)
            break;
          months = weeks/4;
          dts.Add(start);
        }
        else if (months < 12)
        {
          months++;
          start = Dt.Add(start, 1, TimeUnit.Months);
          if (start >= terminal)
            break;
          dts.Add(start);
        }
        else
        {
          start = Dt.Add(start, 1, TimeUnit.Years);
          if (start >= terminal)
            break;
          dts.Add(start);
        }
      }
      dts.Add(terminal);
      dts.Add(Dt.Add(terminal, new Tenor(1, TimeUnit.Weeks)));
      return dts.ToArray();
    }

    #endregion
  }
}