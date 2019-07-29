using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Ccr;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// Interface to evaluate exposures along simulated paths
  /// </summary>
  internal interface IExposurePathEvaluator
  {
    /// <summary>
    /// Evaluates the price values at all the exposure dates along the specified path.
    /// </summary>
    /// <param name="path">The simulated path</param>
    /// <param name="result">The exposure set to be filled with the evaluation results</param>
    /// <param name="loggingAction">The logger action taking two parameters
    ///  <c>(pathIndex, exposureDateIndex)</c></param>
    void EvaluatePath(SimulatedPath path,
      IExposureSet result, Action<int, int> loggingAction);
  }

  /// <summary>
  /// Interface for creating exposure path evaluator
  /// </summary>
  internal interface IExposurePathEvaluatorFactory
  {
    /// <summary>
    /// Gets the exposure path evaluator.
    /// </summary>
    /// <param name="pricers">The array of pricers to evaluate</param>
    /// <param name="marketEnvironment">The market environment</param>
    /// <param name="exposureDates">The list of exposure dates</param>
    /// <param name="simulationDates">The simulation dates</param>
    /// <returns>IExposurePathEvaluator</returns>
    IExposurePathEvaluator GetEvaluator(
      ISimulationPricer[] pricers,
      MarketEnvironment marketEnvironment,
      IReadOnlyList<IReadOnlyList<Dt>> exposureDates,
      Dt[] simulationDates);
  }
}
