/*
 * 
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;
using InputType = BaseEntity.Toolkit.Models.Simulations.Native.Simulator.InputType;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  ///   Interface to create a simulator factory
  /// </summary>
  public interface ISimulationModel
  {
    /// <summary>
    ///  Create a simulator
    /// </summary>
    /// <param name="pathCount">Number of paths to generate</param>
    /// <param name="factorCount">Number of driving forces (Brownian motions)</param>
    /// <param name="asOf">The base date representing time 0</param>
    /// <param name="simulationDates">Simulation time grid</param>
    /// <param name="forwardTenorDates">Forward tenors representing the term structures to simulate</param>
    /// <returns>A simulator</returns>
    ISimulatorFactory CreateFactory(int pathCount, int factorCount,
      Dt asOf, Dt[] simulationDates, Dt[] forwardTenorDates);
  }

  /// <summary>
  ///   Interface to set up a simulator
  /// </summary>
  public interface ISimulatorFactory
  {
    /// <summary>
    ///  Gets the simulator
    /// </summary>
    Simulator Simulator { get; }

    /// <summary>
    /// Add domestic discount curve 
    /// </summary>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="volatilityCurves">Volatility for each tenor</param>
    /// <param name="factorLoadings">Factor loadings for each tenor</param>
    /// <param name="active">If non active, the underlying process is assumed constant and equal to its time zero value</param>
    void AddDomesticDiscount(DiscountCurve discountCurve,
      VolatilityCurve[] volatilityCurves, double[,] factorLoadings, bool active);

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
    void AddDiscount(DiscountCurve discountCurve, VolatilityCurve[] volatilityCurves,
      double[,] factorLoadings, FxRate fxRate, VolatilityCurve[] fxVolatility,
      double[,] fxFactorLoadings, bool active);

    /// <summary>
    /// Add survival curve
    /// </summary>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="volatilityCurves">Volatility of forward survival probability</param>
    /// <param name="factorLoadings">Factor loadings of forward survival probabilities</param>
    /// <param name="active">If non active, the underlying process is assumed constant and equal to its time zero value</param>
    void AddSurvival(SurvivalCurve survivalCurve, VolatilityCurve[] volatilityCurves,
      double[,] factorLoadings, bool active);

    /// <summary>
    /// Add term structure of forward prices (any process that is a martingale under its domestic T-forward measure)
    /// </summary>
    /// <param name="forwardCurve">Term structure of forward prices</param>
    /// <param name="volatilityCurves">Volatility for each tenor</param>
    /// <param name="factorLoadings">Factor loadings for each tenor</param>
    /// <param name="active">If non active, the underlying process is assumed constant and equal to its time zero value</param>
    void AddForward(CalibratedCurve forwardCurve, VolatilityCurve[] volatilityCurves,
      double[,] factorLoadings, bool active);

    /// <summary>
    /// Add forward price process (any process that is a martingale under its domestic Q-forward measure)
    /// </summary>
    /// <param name="spot">Spot asset</param>
    /// <param name="referenceCurve">Reference curve for pricer dependency</param>
    /// <param name="volatility">Volatility for each tenor</param>
    /// <param name="factorLoadings">Factor loadings for each tenor</param>
    /// <param name="carryCostAdjustment">Instantaneous basis over funding cost </param>
    /// <param name="dividendSchedule">Proportional, discretely paid dividends</param>
    /// <param name="active">If non active, the underlying process is assumed constant and equal to its time zero value</param>
    void AddSpot(ISpot spot, CalibratedCurve referenceCurve,
      IVolatilityProcessParameter volatility, double[,] factorLoadings,
      Func<Dt, Dt, double> carryCostAdjustment,
      IList<Tuple<Dt, double>> dividendSchedule, bool active);

    /// <summary>
    /// Add Radon-Nykodim density process for wrong/right way risk calculations 
    /// </summary>
    /// <param name="cptyIndex">Index of counterparty curve and booking entity</param>
    /// <param name="cptyCreditCurves">Survival curve of counterparty and booking entity</param>
    /// <param name="defaultTimeCorrelation">Default time correlation between counterparty and booking entity</param>
    void AddRadonNykodim(int[] cptyIndex,
      SurvivalCurve[] cptyCreditCurves, double defaultTimeCorrelation);
  }

  /// <summary>
  ///   Built-in simulator factories
  /// </summary>
  public static class SimulationModels
  {
    #region Data and properties

    private static readonly ConcurrentDictionary<string, ISimulationModel> Models
      = new ConcurrentDictionary<string, ISimulationModel>();

    /// <summary>
    ///   The factory to create simulators for LIBOR market model.
    /// </summary>
    public static readonly ISimulationModel LiborMarketModel
      = new Simulator.LiborMarketSimulationModel();

    /// <summary>
    ///   The factory to create simulators for Hull-White model.
    /// </summary>
    public static readonly ISimulationModel HullWhiteModel
      = new Simulator.HullWhiteSimulationModel();

    #endregion

    #region Methods

    /// <summary>
    ///   Add a user-defined simulation model.
    /// </summary>
    /// <param name="name">The name of the model</param>
    /// <param name="model">The simulator factory representing the model</param>
    /// <param name="replace">If true, replace the existing one with the input model;
    ///   otherwise, it throws exception if a model with the specified name exists</param>
    public static void Add(string name, ISimulationModel model, bool replace = false)
    {
      if (replace)
      {
        Models.AddOrUpdate(name, model, (k, f) => model);
        return;
      }
      if (!Models.TryAdd(name, model))
      {
        throw new ToolkitException(String.Format(
          "A simulator factory with key '{0}' already exists", name));
      }
    }

    /// <summary>
    ///  Get the model with the specified name
    /// </summary>
    /// <param name="modelName">The model name</param>
    /// <returns>The model as a simulator factory</returns>
    public static ISimulationModel GetModel(string modelName)
    {
      // this allows to override predefined models.
      ISimulationModel model;
      if (Models.TryGetValue(modelName, out model))
        return model;

      // check predefined models
      const StringComparison nocase = StringComparison.OrdinalIgnoreCase;
      if (string.Compare(modelName, "LiborMarket", nocase) == 0)
        return LiborMarketModel;
      if (string.Compare(modelName, "HullWhite", nocase) == 0)
        return HullWhiteModel;

      throw new ToolkitException(String.Format(
        "A simulator factory with key '{0}' not found", modelName));
    }

    #endregion

    #region Extension methods

    internal static void AddSpot(this ISimulatorFactory factory,
      ISpot spot, CalibratedCurve referenceCurve,
      VolatilityCurve[] volatilityCurves, double[,] factorLoadings,
      Func<Dt, Dt, double> carryCostAdjustment,
      IList<Tuple<Dt, double>> dividendSchedule, bool active)
    {
      factory.AddSpot(spot, referenceCurve,
        new StaticVolatilityCurves(volatilityCurves),
        factorLoadings, carryCostAdjustment, dividendSchedule, active);
    }

    #endregion
  }

  //   Defining the built-in models
  partial class Simulator
  {
    #region Create simulator

    /// <summary>
    ///   Create a simulator
    /// </summary>
    /// <param name="simulationModel">Simulation model</param>
    /// <param name="numberOfPaths">Sample size</param>
    /// <param name="datesToSimulate">Exposure dates</param>
    /// <param name="env"></param>
    /// <param name="volatilities"></param>
    /// <param name="factorLoadings"></param>
    /// <param name="cptyIndex"></param>
    /// <param name="cptyDefaultTimeCorrelation"></param>
    /// <returns></returns>
    public static Simulator Create(
      ISimulationModel simulationModel,
      int numberOfPaths,
      Dt[] datesToSimulate,
      MarketEnvironment env,
      VolatilityCollection volatilities,
      FactorLoadingCollection factorLoadings,
      int[] cptyIndex,
      double cptyDefaultTimeCorrelation
      )
    {
      Dt asOf = env.AsOf;
      var cptyIdx = Array.ConvertAll(cptyIndex, i => (i >= 0) ? i : ~i);
      var factory = simulationModel.CreateFactory(numberOfPaths,
        factorLoadings.FactorCount, asOf, datesToSimulate, env.Tenors);
      if (env.DiscountCurves.Length > 0)
      {
        // support time shifts. Otherwise avoid conforming curves until later
        if (env.DiscountCurves[0].AsOf != asOf)
        {
          // clone to keep changes localized
          env = env.CloneObjectGraph();
          // conform will move the curve dates
          env.Conform();
        }

        factory.AddDomesticDiscount(env.DiscountCurves[0],
          volatilities.GetVols(env.DiscountCurves[0]),
          factorLoadings.GetFactors(env.DiscountCurves[0]),
          true);
      }
      for (int i = 1; i < env.DiscountCurves.Length; ++i)
      {
        factory.AddDiscount(env.DiscountCurves[i],
          volatilities.GetVols(env.DiscountCurves[i]),
          factorLoadings.GetFactors(env.DiscountCurves[i]),
          env.FxRates[i - 1], volatilities.GetVols(env.FxRates[i - 1]),
          factorLoadings.GetFactors(env.FxRates[i - 1]),
          true);
      }
      foreach (var fc in env.ForwardCurves)
      {
        factory.AddForward(fc, volatilities.GetVols(fc),
          factorLoadings.GetFactors(fc), true);
      }
      foreach (var cc in env.SpotBasedCurves)
      {
        var sp = cc as IForwardPriceCurve;
        if (sp == null || sp.Spot == null)
          continue;
        if (sp.CarryCashflow.Any(d => d.Item2 == DividendSchedule.DividendType.Fixed))
        {
          throw new ArgumentException(
            "Only proportional (discrete) dividends are supported.");
        }
        factory.AddSpot(sp.Spot, cc, volatilities.GetVolatilityData(sp.Spot),
          factorLoadings.GetFactors(sp.Spot),
          sp.CarryRateAdjustment,
          sp.CarryCashflow.Select(d => new Tuple<Dt, double>(d.Item1, d.Item3)).ToArray(),
          true);
      }
      for (int i = 0; i < env.CreditCurves.Length; ++i)
      {
        var sc = env.CreditCurves[i];
        int offset = Array.IndexOf(cptyIdx, i);
        bool active = (offset < 0 || cptyIndex[offset] >= 0);
        factory.AddSurvival(env.CreditCurves[i],
          active
            ? volatilities.GetVols(sc)
            : volatilities.TryGetVols(sc, new VolatilityCurve[1]),
          active
            ? factorLoadings.GetFactors(sc)
            : factorLoadings.TryGetFactors(sc, new double[1, factorLoadings.FactorCount]),
          active);
      }
      if (cptyIdx.Length > 0)
        factory.AddRadonNykodim(cptyIdx,
          Array.ConvertAll(cptyIdx, i => env.CreditCurves[i]), cptyDefaultTimeCorrelation);
      return factory.Simulator;
    }

    #endregion

    #region Nested types: simulator models

    // We need nested types to access the private members.
    // In particular, we don't want to mess with swig handles
    // outside this class.

    internal class LiborMarketSimulationModel : ISimulationModel
    {
      public ISimulatorFactory CreateFactory(
        int pathCount, int factorCount,
        Dt asOf, Dt[] simulationDates, Dt[] forwardTenorDates)
      {
        return new LiborMarketSimulatorFactory(SetUpSimulator(
          pathCount, asOf, simulationDates, forwardTenorDates,
          (dates, tenors) => Native.Simulator.CreateSimulator(
            pathCount, dates, tenors, factorCount)));
      }
    }

    internal class HullWhiteSimulationModel : ISimulationModel
    {
      public ISimulatorFactory CreateFactory(int pathCount, int factorCount,
        Dt asOf, Dt[] simulationDates, Dt[] forwardTenorDates)
      {
        return new HullWhiteSimulatorFactory(SetUpSimulator(
          pathCount, asOf, simulationDates, forwardTenorDates,
          (dates, tenors) => HullWhiteSimulatorFactory.CreateSimulator(
            dates, tenors, factorCount, 365.0)));
      }
    }

    private static Simulator SetUpSimulator(
      int pathCount, Dt asOf, Dt[] simulationDates, Dt[] forwardTenorDates,
      Func<double[], double[], Native.Simulator> create)
    {
      var timesToSimulate = Array.ConvertAll(
        simulationDates, dt => Dt.FractDiff(asOf, dt));
      var tenors = Array.ConvertAll(forwardTenorDates,
        dt => Dt.FractDiff(asOf, dt));
      var engine = create(timesToSimulate, tenors);
      Debug.Assert(engine.OwnMemory == false);
      return new Simulator(engine.HandleRef.Handle, true)
      {
        AsOf = asOf,
        PathCount = pathCount,
        SimulationDates = simulationDates,
        Tenors = forwardTenorDates,
        Map = new Dictionary<object, Tuple<CalibratedCurve, InputType, int>>()
      };
    }

    #endregion
  }
}
