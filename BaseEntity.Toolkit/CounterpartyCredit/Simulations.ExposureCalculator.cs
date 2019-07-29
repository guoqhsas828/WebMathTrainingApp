/*
 * Simulations.cs
 *
 * 
 *
 */

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Models.Simulations;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  ///   Interface allowing different implementation of the exposure calculations.
  /// </summary>
  public interface IExposureCalculator
  {
    /// <summary>
    /// Scenario Generation.
    /// </summary>
    /// <param name="exposureDates">Exposure dates</param>
    /// <param name="simulator">Simulator</param>
    /// <param name="generator">Random number generator</param>
    /// <param name="marketEnvironment">CCR Market Environment</param>
    /// <param name="nettingCount">The number of netting sets</param>
    /// <param name="portfolio">An array of tuples consisting of CCR pricer, fxRate index and counter party index</param>
    /// <param name="unilateral">Treat default unilaterally or jointly (first-to-default)</param>
    /// <returns>Simulation scenarios</returns>
    /// <remarks>
    /// We generate scenarios via simulation of risk factors. Each scenario is a joint realization of risk factors 
    /// at various points in time, including discount factor (df), numeraire asset (numeraire), counter party default Radon Nikodym derivative (cptyRn),
    /// booking entity default Radon Nikodym derivative (ownRn), booking entity survival Radon Nikodym derivative (survivalRn), 
    /// counter party credit spread (cptySpread), booking entity credit spread (ownSpread), lend curve spread (lendSpread), borrow curve spread (borrowSpread). 
    /// </remarks>
    IEnumerable<ISimulatedPathValues> CalculateExposures(
      Dt[] exposureDates,
      Simulator simulator,
      MultiStreamRng generator,
      CCRMarketEnvironment marketEnvironment,
      int nettingCount,
      Tuple<CcrPricer, int, int>[] portfolio,
      bool unilateral);
  }


  partial class Simulations
  {
    private static IExposureCalculator _exposureCalculator;

    private static IExposureCalculator ExposureCalculator
    {
      get
      {
        try
        {
          if (!BaseEntity.Toolkit.Util.Configuration.ToolkitConfigurator.
            Settings.CcrPricer.EnableOptimizedExposureCalculator)
          {
            return null;
          }
          if (_exposureCalculator == null)
            _exposureCalculator = new BaseEntity.Toolkit.Ccr.ExposureCalculator();
        }
        catch (Exception e)
        {
          Logger.WarnFormat("Error loading exposure calculator: {0}",
            e.Message);
        }
        return _exposureCalculator;
      }
    }
  }
}
