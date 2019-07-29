/*
 * Simulator.PartialProxy.cs
 *
 *  -2010. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;
using static BaseEntity.Toolkit.Models.Simulations.MarketEnvironment;
using InputType = BaseEntity.Toolkit.Models.Simulations.Native.Simulator.InputType;

namespace BaseEntity.Toolkit.Models.Simulations
{

  #region SimulatorFlags

  /// <summary>
  /// Simulator flags
  /// </summary>
  [Flags]
  public enum SimulatorFlags
  {
    /// <summary>
    /// Libor rates
    /// </summary>
    Rate = 0x01,
    /// <summary>
    /// Survival probabilities
    /// </summary>
    Credit = 0x02,
    /// <summary>
    /// Forward prices
    /// </summary>
    Forward = 0x04,
    /// <summary>
    /// Forward prices
    /// </summary>
    Spot = 0x08,
  }

  #endregion

  /// <summary>
  /// General interface for N-currency, M-credits, K-forward prices market scenario simulations 
  /// </summary>
  public partial class Simulator : IDisposable
  {
    #region Constructore

    private Native.Simulator _native;

    internal Simulator(IntPtr cPtr, bool cMemoryOwn)
    {
      _native = new Native.Simulator(cPtr, cMemoryOwn);
    }

    /// <summary>Implicit cast</summary>
    public static implicit operator Native.Simulator(Simulator sim)
    {
      return sim._native;
    }

    /// <summary>
    ///  Free native memory
    /// </summary>
    public void Dispose()
    {
      if (_native == null) return;
      _native.Dispose();
      _native = null;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Generate a simulated market path
    /// </summary>
    /// <param name="idx">Integer identifying the path</param>
    /// <param name="rng">Random number generator</param>
    /// <returns>Simulated path</returns>
    /// <remarks>Generator should be able to jump to the appropriate index in the pseudo(quasi) random sequence</remarks>
    public SimulatedPath GetSimulatedPath(int idx, MultiStreamRng rng)
    {
      SimulatedPath path;
      if ((path = _native.GetPath(idx, rng)) == null)
        return null;
      path = new SimulatedPath(SimulatedPath.getCPtr(path).Handle, true);
      return path;
    }

    /// <summary>
    /// Generate an empty simulated market path
    /// </summary>
    public SimulatedPath GetSimulatedPath()
    {
      SimulatedPath path;
      if ((path = _native.GetPath()) == null)
        return null;
      path = new SimulatedPath(SimulatedPath.getCPtr(path).Handle, true);
      return path;
    }

    /// <summary>
    /// Generates a simulated market path re-using information from oldPath. 
    /// Used in sensitivity computations
    /// </summary>
    /// <param name="oldPath">Original simulated path</param>
    /// <returns>Simulated realization</returns>
    public SimulatedPath GetSimulatedPath(SimulatedPath oldPath)
    {
      SimulatedPath path;
      if ((path = _native.GetPath(oldPath)) == null)
        return oldPath;
      path = new SimulatedPath(SimulatedPath.getCPtr(path).Handle, true);
      return path;
    }

    /// <summary>
    /// Get <m>P(\tau_C \in [t_{i-1}, t_i], \tau_B > t_i)</m> i.e. the probability that counterparty C defaults in <m>[t_{i-1}, t_i]</m>
    /// and the default time of counterparty B occurs after <m>t_i</m> 
    /// </summary>
    /// <param name="index">Counterparty index (0 = cpty, 1 = booking entity)</param>
    /// <param name="retVal"></param>
    public void DefaultKernel(int index, double[] retVal)
    {
      _native.DefaultKernel(index, retVal);
    }

    /// <summary>
    /// Get <m>\int_{t_i-1}^{t_i} P(\tau_C > t, \tau_B > t) dt</m> i.e. the survival weighted duration
    /// </summary>
    /// <param name="retVal"></param>
    public void SurvivalKernel(int index, double[] retVal)
    {
      _native.SurvivalKernel(index, retVal);
    }

    /// <summary>
    /// Populates an array with the survival sigma values
    /// </summary>
    /// <remarks>
    /// The array passed in must be constructed using the number of exposure dates considered by the simulator
    /// </remarks>
    public void GetSurvivalSigma(int index, double[] retVal)
    {
      _native.GetSurvivalSigma(index, retVal);
    }

    /// <summary>
    /// Populates an array with the survival sigma values
    /// </summary>
    /// <remarks>
    /// The array passed in must be constructed using the number of exposure dates considered by the simulator
    /// </remarks>
    public void GetSurvivalThreshold(int index, double[] retVal)
    {
      _native.GetSurvivalThreshold(index, retVal);
    }

    #endregion

    #region Static Constructors

    /// <summary>
    /// Create a perturbed simulator
    /// </summary>
    /// <param name="simulator">Unperturbed simulator</param>
    /// <param name="perturbedTermStructures">Perturbed term structures</param>
    /// <param name="processIdx">Term structure index</param>
    /// <param name="inputType">Input type</param>
    /// <returns>Perturbed simulator</returns>
    /// <remarks>This method creates a simulator that only re-evolves the processes affected by the perturbations</remarks>
    public static Simulator PerturbTermStructures(Simulator simulator, CalibratedCurve[] perturbedTermStructures,
                                                  int[] processIdx, InputType[] inputType)
    {
      var iT = Array.ConvertAll(inputType, i => (int) i);
      var engine = Native.Simulator.PerturbTermStructures(
        simulator, perturbedTermStructures, processIdx, iT);
      if (engine == null)
        return simulator;
      Debug.Assert(engine.OwnMemory == false);
      return new Simulator(engine.Handle, true)
               {
                 AsOf = simulator.AsOf,
                 SimulationDates = simulator.SimulationDates,
                 Tenors = simulator.Tenors,
                 Map = simulator.Map,
                 PathCount = simulator.PathCount,
                 QuadRule = simulator.QuadRule
               };
    }

    /// <summary>
    /// Create a perturbed simulator
    /// </summary>
    /// <param name="simulator">Unperturbed simulator</param>
    /// <param name="perturbedVols">Perturbed volatility curves</param>
    /// <param name="processIdx">Term structure index</param>
    /// <param name="tenorIdx">Tenor point index</param>
    /// <param name="inputType">Input type</param>
    /// <returns>Perturbed simulator</returns>
    /// <remarks>This method creates a simulator that only re-evolves the processes affected by the perturbations</remarks>
    public static Simulator PerturbVolatilities(
      Simulator simulator, VolatilityCurve[] perturbedVols,
      int[] processIdx, int[] tenorIdx, InputType[] inputType)
    {
      var iT = Array.ConvertAll(inputType, i => (int) i);
      var engine = Native.Simulator.PerturbVolatilities(
        simulator, perturbedVols, processIdx, tenorIdx, iT);
      if (engine == null)
        return simulator;
      Debug.Assert(engine.OwnMemory == false);
      return new Simulator(engine.Handle, true)
               {
                 AsOf = simulator.AsOf,
                 SimulationDates = simulator.SimulationDates,
                 Tenors = simulator.Tenors,
                 Map = simulator.Map,
                 PathCount = simulator.PathCount,
                 QuadRule = simulator.QuadRule
               };
    }

    /// <summary>
    /// Create a perturbed simulator
    /// </summary>
    /// <param name="simulator">Unperturbed simulator</param>
    /// <param name="perturbedFactors">Perturbed factor loadings</param>
    /// <param name="processIdx">Term structure index</param>
    /// <param name="tenorIdx">Tenor point index</param>
    /// <param name="inputType">Input type</param>
    /// <returns>Perturbed simulator</returns>
    /// <remarks>This method creates a simulator that only re-evolves the processes affected by the perturbations</remarks>
    public static Simulator PerturbFactors(
      Simulator simulator, double[,] perturbedFactors,
      int[] processIdx, int[] tenorIdx, InputType[] inputType)
    {
      var iT = Array.ConvertAll(inputType, i => (int) i);
      var engine = Native.Simulator.PerturbFactors(
        simulator, perturbedFactors, processIdx, tenorIdx, iT);
      if (engine == null)
        return simulator;
      Debug.Assert(engine.OwnMemory == false);
      return new Simulator(engine.Handle, true)
               {
                 AsOf = simulator.AsOf,
                 SimulationDates = simulator.SimulationDates,
                 Tenors = simulator.Tenors,
                 Map = simulator.Map,
                 PathCount = simulator.PathCount,
                 QuadRule = simulator.QuadRule
               };
    }

    /// <summary>
    /// Create a factory for LIBOR market model simulation
    /// </summary>
    /// <param name="numberOfPaths">Sample size</param>
    /// <param name="asOf">As of date</param>
    /// <param name="datesToSimulate">Exposure dates</param>
    /// <param name="forwardTenors">Forward tenors to simulate. 
    /// The same tenors will be simulated for all relevant term structures</param>
    /// <param name="factorNames">Id of driving factors (Brownian motions)</param>
    /// <returns>Simulator Factory</returns>
    public static ISimulatorFactory CreateSimulatorFactory(
      int numberOfPaths,
      Dt asOf,
      Dt[] datesToSimulate,
      Dt[] forwardTenors,
      string[] factorNames)
    {
      return SimulationModels.LiborMarketModel.CreateFactory(numberOfPaths,
        factorNames.Length, asOf, datesToSimulate, forwardTenors);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="numberOfPaths"></param>
    /// <param name="datesToSimulate"></param>
    /// <param name="env"></param>
    /// <param name="volatilities"></param>
    /// <param name="factorLoadings"></param>
    /// <param name="cptyIndex"></param>
    /// <param name="cptyDefaultTimeCorrelation"></param>
    /// <returns></returns>
    public static Simulator Create(
      int numberOfPaths,
      Dt[] datesToSimulate,
      MarketEnvironment env,
      VolatilityCollection volatilities,
      FactorLoadingCollection factorLoadings,
      int[] cptyIndex,
      double cptyDefaultTimeCorrelation
      )
    {
      return Create(SimulationModels.LiborMarketModel,
        numberOfPaths, datesToSimulate, env, volatilities, factorLoadings,
        cptyIndex, cptyDefaultTimeCorrelation);
    }


    internal static Dt[] GetDefaultSimulationDates(Dt asOf, IEnumerable<Dt> simulationDates,
                                                  IEnumerable<ICashflowNode> underlierCashflow,
                                                  ExerciseEvaluator call, ExerciseEvaluator put,
                                                  Tenor gridSize)
    {
      Dt[] dates;
      if (simulationDates == null)
      {
        var allDts = new UniqueSequence<Dt> { asOf };
        if (underlierCashflow != null)
          foreach (var cf in underlierCashflow)
            allDts.Add(cf.PayDt);
        if (call != null && call.Cashflow != null)
          foreach (var cf in call.Cashflow)
            allDts.Add(cf.PayDt);
        if (put != null && put.Cashflow != null)
          foreach (var cf in put.Cashflow)
            allDts.Add(cf.PayDt);
        if (call != null)
          foreach (var dt in call.ExerciseDates)
            allDts.Add(dt);
        if (put != null)
          foreach (var dt in put.ExerciseDates)
            allDts.Add(dt);
        if (!gridSize.IsEmpty)
        {
          var clone = (UniqueSequence<Dt>)allDts.Clone();
          Dt dt = asOf;
          foreach (var next in clone)
          {
            for (; ; )
            {
              dt = Dt.Add(dt, gridSize);
              if (dt < next)
                allDts.Add(dt);
              else
              {
                dt = next;
                break;
              }
            }
          }
        }
        dates = allDts.ToArray();
      }
      else
        dates = simulationDates.ToArray();
      if (dates.Length == 0)
        throw new ToolkitException("Require non empty SimulationDates");
      return dates;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="datesToSimulate">Exposure dates</param>
    /// <param name="forwardTenors">Forward tenors to simulate.</param> 
    /// <param name="quadRule">Quadrature points</param> 
    /// <param name="factorNames">Id of driving factors (brownian motions)</param>
    /// <returns>Simulator</returns>
    /// <remarks> 
    /// The expectation is computed by numerical integration rather than MC simulation by taking low-dimensional 
    /// Markovian projections of the underlying processes.
    /// </remarks>
    public static ISimulatorFactory CreateProjectiveSimulatorFactory(
      Dt asOf,
      Dt[] datesToSimulate,
      Dt[] forwardTenors,
      int quadRule,
      string[] factorNames)
    {
      var timesToSimulate = Array.ConvertAll(datesToSimulate, dt => Dt.FractDiff(asOf, dt));
      var tenors = Array.ConvertAll(forwardTenors, dt => Dt.FractDiff(asOf, dt));
      var engine = Native.Simulator.CreateProjectiveSimulator(
        timesToSimulate, tenors, factorNames.Length);
      Debug.Assert(engine.OwnMemory == false);
      var simulator = new Simulator(engine.Handle, true)
             {
               AsOf = asOf,
               QuadRule = quadRule,
               PathCount = (int)Math.Pow(quadRule, factorNames.Length),
               SimulationDates = datesToSimulate,
               Tenors = forwardTenors,
               Map = new Dictionary<object, Tuple<CalibratedCurve, InputType, int>>()
             };
      return new LiborMarketSimulatorFactory(simulator);
    }

    #endregion

    #region Properties
    /// <summary>
    /// As of date
    /// </summary>
    public Dt AsOf { get; private set; }
    
    /// <summary>
    /// Number of underlying factors 
    /// </summary>
    public int Dimension
    {
      get { return _native.Dim(); }
    }

    /// <summary>
    /// Number of paths
    /// </summary>
    public int PathCount { get; set; }

    /// <summary>
    /// Simulation dates and associated index
    /// </summary>
    public Dt[] SimulationDates { get; private set; }

    /// <summary>
    /// Simulated term structure tenors 
    /// </summary>
    public Dt[] Tenors { get; private set; }

    /// <summary>
    /// Map object - simulator index
    /// </summary>
    internal Dictionary<object, Tuple<CalibratedCurve, InputType, int>> Map { get; private set; }
    /// <summary>
    /// Quadrature rule
    /// </summary>
    internal int QuadRule { get; private set; }

    /// <summary>
    /// Simulation time grid and associated index
    /// </summary>
    public double[] SimulationTimeGrid => _simulationTimeGrid
      ?? (_simulationTimeGrid = _native.GetSimulationGrid());

    private double[] _simulationTimeGrid;

    #endregion

    #region Nested type: LiborMarketSimulatorFactory

    class LiborMarketSimulatorFactory : ISimulatorFactory
    {
      /// <summary>
      /// Add domestic discount curve 
      /// </summary>
      /// <param name="discountCurve">Discount curve</param>
      /// <param name="volatilityCurves">Volatility for each tenor</param>
      /// <param name="factorLoadings">Factor loadings for each tenor</param>
      /// <param name="active">If non active, the underlying process is assumed constant and equal to its time zero value</param>
      public void AddDomesticDiscount(DiscountCurve discountCurve, VolatilityCurve[] volatilityCurves,
        double[,] factorLoadings, bool active)
      {
        if (Map.ContainsKey(discountCurve))
          return;
        int index = Native.Simulator.AddDomesticDiscountProcess(Simulator, NoCorrectiveOverlay(discountCurve),
          Validate(volatilityCurves), factorLoadings,
          volatilityCurves.All(v => v != null && v.DistributionType == DistributionType.Normal), active);
        Map.Add(discountCurve,
          new Tuple<CalibratedCurve, InputType, int>(discountCurve, InputType.DiscountRateInput, index));
      }

      /// <summary>
      /// Add foreign discount curve
      /// </summary>
      /// <param name="discountCurve">Discount curve</param>
      /// <param name="volatilityCurves">Volatility for each tenor</param>
      /// <param name="factorLoadings">Factor loadings for each tenor</param>
      /// <param name="fxRate">FX rate</param>
      /// <param name="fxVolatility">Volatility of spot FX rate</param>
      /// <param name="fxFactorLoadings">Factor loadings of spot FX rate</param>
      /// <param name="active">If non active, the underlying process is assumed constant and equal to its time zero value</param>
      public void AddDiscount(DiscountCurve discountCurve, VolatilityCurve[] volatilityCurves, double[,] factorLoadings,
        FxRate fxRate, VolatilityCurve[] fxVolatility, double[,] fxFactorLoadings, bool active)
      {
        if (Map.ContainsKey(discountCurve))
          return;
        int index = Native.Simulator.AddDiscountProcess(Simulator, NoCorrectiveOverlay(discountCurve),
          Validate(volatilityCurves), factorLoadings, fxRate.Rate,
          (fxRate.ToCcy == discountCurve.Ccy), fxVolatility[0],
          fxFactorLoadings, volatilityCurves.All(v => v != null && v.DistributionType == DistributionType.Normal),
          active);
        Map.Add(discountCurve,
          new Tuple<CalibratedCurve, InputType, int>(discountCurve, InputType.DiscountRateInput, index));
        Map.Add(fxRate, new Tuple<CalibratedCurve, InputType, int>(discountCurve, InputType.FxRateInput, index));
      }

      /// <summary>
      /// Add survival curve
      /// </summary>
      /// <param name="survivalCurve">Survival curve</param>
      /// <param name="volatilityCurves">Volatility of forward survival probability</param>
      /// <param name="factorLoadings">Factor loadings of forward survival probabilities</param>
      /// <param name="active">If non active, the underlying process is assumed constant and equal to its time zero value</param>
      public void AddSurvival(SurvivalCurve survivalCurve, VolatilityCurve[] volatilityCurves, double[,] factorLoadings,
        bool active)
      {
        if (Map.ContainsKey(survivalCurve))
          return;
        int index = Native.Simulator.AddSurvivalProcess(Simulator, NoCorrectiveOverlay(survivalCurve),
          Validate(volatilityCurves), factorLoadings, active);
        Map.Add(survivalCurve, new Tuple<CalibratedCurve, InputType, int>(survivalCurve, InputType.CreditInput, index));
      }

      /// <summary>
      /// Add term structure of forward prices (any process that is a martingale under its domestic T-forward measure)
      /// </summary>
      /// <param name="forwardCurve">Term structure of forward prices</param>
      /// <param name="volatilityCurves">Volatility for each tenor</param>
      /// <param name="factorLoadings">Factor loadings for each tenor</param>
      /// <param name="active">If non active, the underlying process is assumed constant and equal to its time zero value</param>
      public void AddForward(CalibratedCurve forwardCurve, VolatilityCurve[] volatilityCurves, double[,] factorLoadings,
        bool active)
      {
        if (Map.ContainsKey(forwardCurve))
          return;
        int index = Native.Simulator.AddForwardProcess(Simulator, NoCorrectiveOverlay(forwardCurve),
          Validate(volatilityCurves), factorLoadings,
          forwardCurve is DiscountCurve, active);
        Map.Add(forwardCurve,
          new Tuple<CalibratedCurve, InputType, int>(forwardCurve, InputType.ForwardPriceInput, index));
      }

      /// <summary>
      /// Add forward price process (any process that is a martingale under its domestic Q-forward measure)
      /// </summary>
      /// <param name="spot">Spot asset</param>
      /// <param name="referenceCurve">Reference curve for pricer dependency</param>
      /// <param name="volatility">Volatility process parameters</param>
      /// <param name="factorLoadings">Factor loadings for each tenor</param>
      /// <param name="carryCostAdjustment">Instantaneous basis over funding cost </param>
      /// <param name="dividendSchedule">Proportional, discretely paid dividends</param>
      /// <param name="active">If non active, the underlying process is assumed constant and equal to its time zero value</param>
      public void AddSpot(ISpot spot, CalibratedCurve referenceCurve,
        IVolatilityProcessParameter volatility, double[,] factorLoadings,
        Func<Dt, Dt, double> carryCostAdjustment,
        IList<Tuple<Dt, double>> dividendSchedule, bool active)
      {
        if (Map.ContainsKey(spot))
          return;
        var dividends = (dividendSchedule == null || dividendSchedule.Count == 0)
          ? new double[0]
          : SimulationDates.Select((dt, i) => dividendSchedule
            .Where(p => (p.Item1 > ((i == 0) ? AsOf : SimulationDates[i - 1]) && p.Item1 <= dt))
            .Sum(p => p.Item2)).ToArray();
        var carryAdj = (carryCostAdjustment == null)
          ? new double[0]
          : SimulationDates.Select((dt, i) => carryCostAdjustment(
            i == 0 ? AsOf : SimulationDates[i - 1], dt)).ToArray();
        int index = AddSpot(Simulator, spot.Value, (int) spot.Ccy,
          volatility, factorLoadings, carryAdj, dividends, active);
        Map.Add(spot, new Tuple<CalibratedCurve, InputType, int>(referenceCurve, InputType.SpotPriceInput, index));
      }

      /// <summary>
      /// Add Radon-Nykodim density process for wrong/right way risk calculations 
      /// </summary>
      /// <param name="cptyIndex">Index of counterparty curve and booking entity</param>
      /// <param name="cptyCreditCurves">Survival curve of counterparty and booking entity</param>
      /// <param name="defaultTimeCorrelation">Default time correlation between counterparty and booking entity</param>
      public void AddRadonNykodim(int[] cptyIndex, SurvivalCurve[] cptyCreditCurves, double defaultTimeCorrelation)
      {
        Native.Simulator.AddRnDensityProcess(Simulator, cptyIndex, cptyCreditCurves,
          (Math.Abs(defaultTimeCorrelation) <= 1.0) ? Math.Max(Math.Min(defaultTimeCorrelation, 0.999), -0.999) : 1000.0);
      }

      private Curve[] Validate(VolatilityCurve[] volatilities)
      {
        if ((Tenors.Length != volatilities.Length) && (volatilities.Length != 1))
        {
          throw new ArgumentException(String.Format("volatilities expected of size {0} or 1", Tenors.Length));
        }
        return volatilities;
      }

      public Simulator Simulator { get; }

      /// <summary>
      /// Simulation dates and associated index
      /// </summary>
      private Dt AsOf => Simulator.AsOf;

      /// <summary>
      /// Simulation dates and associated index
      /// </summary>
      private Dt[] SimulationDates => Simulator.SimulationDates;

      /// <summary>
      /// Simulation dates and associated index
      /// </summary>
      private Dt[] Tenors => Simulator.Tenors;

      /// <summary>
      /// Map object - simulator index
      /// </summary>
      private Dictionary<object, Tuple<CalibratedCurve, InputType, int>> Map => Simulator.Map;

      internal LiborMarketSimulatorFactory(Simulator simulator) { Simulator = simulator; }

      #region Backward interfaces

      private static int AddSpot(
        Simulator simulator, double spot, int ccy,
        IVolatilityProcessParameter volatility, double[,] factorLoadings,
        double[] additionalCarryRate, double[] dividends, bool active)
      {
        var pinned = VolatilityParameter.Pin(volatility);
        try
        {
          return Native.Simulator.AddSpotProcess(simulator, spot, ccy, ref pinned.Data,
            factorLoadings, additionalCarryRate, dividends, active);
        }
        finally { pinned.Dispose(); }
      }

      #endregion
    }

    #endregion

  }
}