/*
 * ISimulatedValues.cs
 *
 *  -2010. All rights reserved.
 *
 */

using System.Collections.Generic;
using System.Data;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models.Simulations;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  /// Calculate CCRMeasures from raw path data
  /// </summary>
  public interface ICounterpartyCreditRiskCalculations
  {
    /// <summary>
    /// Get stored simulation paths
    /// </summary>
    ISimulatedValues SimulatedValues { get; }

    /// <summary>
    /// Reset internal states
    /// </summary>
    void Reset();

    /// <summary>
    /// Perform calculations
    /// </summary>
    void Execute();

    /// <summary>
    /// Calculate CCRMeasures from simulation results
    /// </summary>
    ///<param name="measure">CCRMeasure to be computed</param>
    /// <param name="date">Date</param>
    /// <param name="netting">Netting rule</param>
    /// <param name="ci">Confidence level for distribution measures</param>
    /// <returns>Calculated CCRMeasure</returns>
    ///<remarks>netting.Item1 contains the indices of all netting groups associated to the counterparty of interest, 
    /// netting.Item2 contains the netting rule among the netting sets.
    /// For instance, if netting.Item[1] := {1,5,7,9}, then the corresponding netting sets refer all to the same counterparty.
    /// A netting rule among the sets can be written as netting.Item[2] := {0,1,1,2}, meaning that netting set 5 and 7 belong 
    /// to the same netting superset should therefore be netted</remarks>   
    double GetMeasure(CCRMeasure measure, Netting netting, Dt date, double ci);
  }


  /// <summary>
  ///  A interface for a collection of simulated results in a single path.
  /// </summary>
  public interface ISimulatedPathValues
  {
    /// <summary>
    /// Path weight
    /// </summary>
    double Weight { get; }

    /// <summary>
    /// Unique Id of the path  
    /// </summary>
    int Id { get; }

    /// <summary>
    /// Number of dates
    /// </summary>
    int DateCount { get; }

    /// <summary>
    /// Number of netting groups
    /// </summary>
    int NettingCount { get; }

    /// <summary>
    /// Gets the undiscounted value of the pv of a netting group at the given simulation date
    /// </summary>
    /// <param name="dateIndex">Date index</param>
    /// <param name="nettingGroupIndex">Index of the netting group</param>
    /// <returns>Netted value of the portfolio</returns>
    /// <remarks>Portfolio value is discounted only to the future simulation date</remarks>
    double GetPortfolioValue(int dateIndex, int nettingGroupIndex);

    /// <summary>
    /// Sets the undiscounted value of the pv of a netting group at the given simulation date
    /// </summary>
    /// <param name="dateIndex">Date index</param>
    /// <param name="nettingGroupIndex">Index of the netting group</param>
    /// <param name="value">Portfolio value to set</param>
    /// <remarks>Portfolio value is discounted only to the future simulation date</remarks>
    void SetPortfolioValue(int dateIndex, int nettingGroupIndex, double value);
  }


  /// <summary>
  ///  A interface for a collection of simulated results.
  /// </summary>
  public interface ISimulatedValues
  {
    /// <summary>
    /// Number of exposure dates
    /// </summary>
    int DateCount { get; }

    /// <summary>
    /// Exposure dates
    /// </summary>
    Dt[] ExposureDates { get; }

    /// <summary>
    ///   Number of paths.
    /// </summary>
    int PathCount { get; }

    /// <summary>
    /// Netting group information
    /// </summary>
    Dictionary<string, int> NettingMap { get; }

    /// <summary>
    ///   Simulated realizations.
    /// </summary>
    IEnumerable<ISimulatedPathValues> Paths { get; }

    ///<summary>
    /// Type of Random Number Generator used.
    ///</summary>
    MultiStreamRng.Type RngType { get; }

    /// <summary>
    /// Max time-step in MonteCarlo path simulation
    /// </summary>
    Tenor GridSize { get; }
  }


  /// <summary>
  /// Interface used for running individual paths of a simulation separately
  /// </summary>
  public interface IRunSimulationPath : ISimulatedValues
  {
    /// <summary>
    /// Run a contiguous set of paths identified by idx
    /// </summary>
    /// <param name="from">the first path number</param>
    /// <param name="to">the last path number</param>
    /// <returns>ISimulatedPathValues result for the simulated path</returns>
    IList<ISimulatedPathValues> RunSimulationPaths(int from, int to);


    ///<summary>
    /// Accumulate the specified measure when RunSimulationPath is called
    ///</summary>
    ///<param name="measure"></param>
    ///<param name="ci"></param>
    void AddMeasureAccumulator(CCRMeasure measure, double ci);

    ///<summary>
    /// Merge the accumulated measures from another IRunSimulationPath into the instance
    ///</summary>
    ///<param name="other"></param>
    void Merge(IRunSimulationPath other);

    ///<summary>
    /// precalculates grids of pvs for "exotic" pricers that need to use American Monte Carlo
    ///</summary>
    IList<double[,]> PrecalculateExotics();


    ///<summary>
    /// Return the specified measure allocated by trade
    ///</summary>
    ///<param name="measure"></param>
    ///<param name="t">index of the simulation date, used only for time bucketed measures</param>
    ///<param name="ci">confidence interval, used only for distribution based measures</param>
    ///<returns>allocated results in order matching initial input</returns>
    double[] GetMeasureAllocatedByTrade(CCRMeasure measure, int t, double ci);

    ///<summary>
    /// Return the specified measure (Counterparty total)
    ///</summary>
    ///<param name="measure"></param>
    ///<param name="dt">the date, used only for time bucketed measures</param>
    ///<param name="ci">confidence interval, used only for distribution based measures</param>
    double GetMeasure(CCRMeasure measure, Dt dt, double ci);
  }

  ///<summary>
  ///</summary>
  public interface IMeasureAccumulator
  {
    ///<summary>
    ///</summary>
    ///<param name="path"></param>
    ///<param name="exposure">the netted/collateralized exposure</param>
    void AccumulatePath(ISimulatedPathValues path, double exposure);

    //  void Merge(IMeasureAccumulator ); //??
    ///<summary>
    ///</summary>
    ///<returns></returns>
    double Reduce();
  }
}