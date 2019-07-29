/*
 * ExposureSimulator.cs
 *
 *   2010-17. All rights reserved.
 *
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  ///   Computes the counterparty credit risk for a trade.
  /// </summary>
  ///
  [Serializable]
  [ObjectLoggerEnabled]
  public class ExposureSimulator : BaseEntityObject
  {
    internal static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(ExposureSimulator));
    [ObjectLogger(Name = "ExposureSimulator", Description = "Exposures and Discount Factors for each trade.", Category = "Exposures")]
    private static readonly IObjectLogger ObjectLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(ExposureSimulator));
    [ObjectLogger(Name = "SimulatorsPricer", Description = "Exposures Simulators Pricer.", Category = "Exposures")]
    private static readonly IObjectLogger PricerObjectLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(ExposureSimulator), "Pricer");
    [ObjectLogger(Name = "Pricers", Description = "Parent for Pricers. Required for individual pricing models.", Category = "Exposures")]
    private static readonly IObjectLogger PricerDiagnosticsLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(ExposureSimulator), "PricerDiagnostics");
    [ObjectLogger(Name = "DomesticDiscountFactors", Description = "Simulated Domestic Discount Factors.", Category = "Simulation")]
    private static readonly IObjectLogger DomesticDiscountFactorLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(ExposureSimulator), "DomesticDiscountFactors");
    [ObjectLogger(Name = "ForeignDiscountFactors", Description = "Simulated Foreign Discount Factors.", Category = "Simulation")]
    private static readonly IObjectLogger ForeignDiscountFactorLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(ExposureSimulator), "ForeignDiscountFactors");

    #region Constructors


    /// <summary>
    /// Constructor
    /// </summary>
    public ExposureSimulator(string name, 
                                DomesticDiscountProcess domesticDiscountProcess,
                                IList<ForeignDiscountProcess> foreignDiscountSimulations,
                                IList<SurvivalProcess> survivalCurveSimulations,
                                IList<ForwardCurveProcess> forwardCurveSimulations,
                                IList<SpotPriceProcess> spotPriceSimulations, 
                                IList<FxRate> crossFxRates, 
                                string optimizerFactoryTypeName = null, 
                                bool enableNewRateStepSolver = false
                                )
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Constructing ExposureSimulator");
      }
      Name = name;
      DomesticDiscountProcess = domesticDiscountProcess;
      ForeignDiscountSimulations = foreignDiscountSimulations ?? new List<ForeignDiscountProcess>();
      CrossFxRates = crossFxRates ?? new List<FxRate>();
      SurvivalCurveSimulations = survivalCurveSimulations ?? new List<SurvivalProcess>();
      SpotPriceSimulations = spotPriceSimulations ?? new List<SpotPriceProcess>();
      ForwardCurveSimulations = forwardCurveSimulations ?? new List<ForwardCurveProcess>();
      WeinerProcess = domesticDiscountProcess.WeinerProcess; 
      _simulationData = new Lazy<Tuple<Simulator, FactorLoadingCollection, VolatilityCollection>>(InitSimulation);
      _marketEnv = new Lazy<MarketEnvironment>(InitMarketEnv);
      if(!string.IsNullOrEmpty(optimizerFactoryTypeName))
        ExposureEvaluatorFactory.InitOptimizerFactory(optimizerFactoryTypeName);
      NewRateStepSolverEnabled = enableNewRateStepSolver;
    }

    #endregion Constructors


    #region Properties
    /// <summary>
    /// Name identifying this simulator
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Random increments of Brownian Motions for each path and simulation date
    /// </summary>
    public WeinerProcess WeinerProcess { get; private set; }

    /// <summary>
    /// Simulated Domestic Discount Forward Rates for each path and simulation date
    /// </summary>
    public DomesticDiscountProcess DomesticDiscountProcess { get; private set; }

    /// <summary>
    /// Simulated Foreign Discount Forward Rates and Fx Rate for each path and simulation date
    /// </summary>
    public IList<ForeignDiscountProcess> ForeignDiscountSimulations { get; private set; }

    
    /// <summary>
    /// Simulated Forward Rates for projection curves for each path and simulation date
    /// </summary>
    public IList<ForwardCurveProcess> ForwardCurveSimulations { get; private set; }

    /// <summary>
    /// Simulated Spot Price for each path and simulation date
    /// </summary>
    public IList<SpotPriceProcess> SpotPriceSimulations { get; private set; }

    /// <summary>
    /// Simulated Survival Probabilities, Factors and Vols for each path and simulation date
    /// </summary>
    public IList<SurvivalProcess> SurvivalCurveSimulations { get; private set; }

    /// <summary>
    /// FxRates between 2 foreign currencies
    /// </summary>
    public IList<FxRate> CrossFxRates { get; private set; } 
    /// <summary>
    /// The engine for the market data simulation
    /// </summary>
    public Simulator Simulator
    {
      get
      {
        return _simulationData.Value.Item1;
      }
    }

    private FactorLoadingCollection FactorLoadingCollection { get { return _simulationData.Value.Item2; } }

    private VolatilityCollection VolatilityCollection { get { return _simulationData.Value.Item3; } }

    /// <summary>
    /// The collection of market data to simulate
    /// </summary>
    public MarketEnvironment MarketEnvironment
    {
      get
      {
        return _marketEnv.Value;
      }
    }

    /// <summary>
    /// Number of simulated paths
    /// </summary>
    public int PathCount { get { return WeinerProcess.PathCount; } }

    /// <summary>
    /// Asof date
    /// </summary>
    public Dt AsOf { get { return WeinerProcess.AsOf; } }

    private bool NewRateStepSolverEnabled { get; set; }
    #endregion

    /// <summary>
    /// Calibrated factorloadings and vols
    /// </summary>
    /// <summary>
    /// Simulate exposures
    /// </summary>
    /// <param name="iPricer"></param>
    /// <param name="exposureDts">dates to generate exposures. If not supplied, will generate dates suitable for supplied IPricer</param>
    /// <param name="enableCashflowOptimizer"></param>
    /// <param name="recordDiscountFactors">include simulated discount factors in returned ExposureSet</param>
    /// <param name="loggingKey">override for the Object Logging file name for the calibrated factorloadings, vols, exposures and discount factors</param>
    public ExposureSet SimulateExposures(IPricer iPricer, Dt[] exposureDts = null, bool enableCashflowOptimizer = true, bool recordDiscountFactors = false, string loggingKey = null)
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Calculating Exposures");
      }
      var env = MarketEnvironment;
      if (Simulator == null)
      {
        Logger.ErrorFormat("Null Simulator in call to ExposureSimulator.CalculateExposures(), Name {0}, Pricer {1} ", Name, iPricer.Product.Description);
        throw new ToolkitException("Null Simulator in CalculateExposures()");
      }

      var path = Simulator.GetSimulatedPath();
      var getPath = GenerateSimulationPaths(path);

      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Loaded Paths Successfully");
      }
      
      var amcPricer = GetAmcAdapter(iPricer);
      if (amcPricer != null && amcPricer.Exotic)
      {
        var exposureSet = CalculateAmc(iPricer, amcPricer, env, getPath, exposureDts, recordDiscountFactors, loggingKey);
        return exposureSet;
      }

      var pricer = GetSimulationPricer(iPricer);
      var clone = CloneUtil.CloneObjectGraph(new Tuple<MarketEnvironment, ISimulationPricer>(env, pricer), CloneMethod.FastClone);
      env = clone.Item1;
      env.Conform();
      pricer = clone.Item2;
      if (exposureDts != null && exposureDts.Any())
      {
        pricer.ExposureDates = exposureDts; 
      }
      var pricerExposureDts = pricer.ExposureDates;
      if (pricerExposureDts.Last() > Simulator.SimulationDates.Last())
      {
        throw new ToolkitException(String.Format("Last Exposure for {0} later than last simulation date in ExposureSimulator {1}", iPricer.Product.Description, Name));
      }

      Logger.VerboseFormat("Using Exposure Dates {0} for {1}.", String.Join(",", pricerExposureDts), iPricer.Product.Description);
      
      var exposures = Calculate(pricer, env, getPath, recordDiscountFactors, enableCashflowOptimizer);

      RetainExposures(exposures.Exposures, recordDiscountFactors ? exposures.DiscountFactors : null, exposures.ExposureDates, iPricer, loggingKey);

      path.Dispose();
      exposures.Id = iPricer.Product.Description; 
      return exposures;
    }

    /// <summary>
    /// Calibrated factorloadings and vols
    /// </summary>
    /// <summary>
    /// Simulate exposures
    /// </summary>
    /// <param name="iPricer">pricer for incremental trade</param>
    /// <param name="coupon01Pricer">pricer to value a 1bp coupon stream on the incremental trade cashflow dates</param>
    /// <param name="exposureDts">dates to generate exposures. If not supplied, will generate dates suitable for supplied IPricer</param>
    /// <param name="enableCashflowOptimizer"></param>
    /// <param name="recordDiscountFactors">include simulated discount factors in returned ExposureSet</param>
    /// <param name="loggingKey">override for the Object Logging file name for the calibrated factorloadings, vols, exposures and discount factors</param>
    public IncrementalExposureSet SimulateIncrementalExposures(IPricer iPricer, IPricer coupon01Pricer  = null, Dt[] exposureDts = null, bool enableCashflowOptimizer = true, bool recordDiscountFactors = false, string loggingKey = null)
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Calculating Exposures");
      }
      var env = MarketEnvironment;
      if (Simulator == null)
      {
        Logger.ErrorFormat("Null Simulator in call to ExposureSimulator.CalculateExposures(), Name {0}, Pricer {1} ", Name, iPricer.Product.Description);
        throw new ToolkitException("Null Simulator in CalculateExposures()");
      }

      var path = Simulator.GetSimulatedPath();
      var getPath = GenerateSimulationPaths(path);

      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Loaded Paths Successfully");
      }

      var amcPricer = GetAmcAdapter(iPricer);
      if (amcPricer != null && amcPricer.Exotic)
      {
        var exposureSet = CalculateAmc(iPricer, amcPricer, env, getPath, exposureDts, recordDiscountFactors);
        return new IncrementalExposureSet()
        {
          Exposures = exposureSet.Exposures,
          DiscountFactors = exposureSet.DiscountFactors,
          ExposureDates = exposureSet.ExposureDates,
          Id = iPricer.Product.Description
        }; 
      }

      var simulationPricer = GetSimulationPricer(iPricer);
      var simulationCouponPricer = GetSimulationPricer(coupon01Pricer);
      var clone = CloneUtil.CloneObjectGraph(new Tuple<MarketEnvironment, ISimulationPricer, ISimulationPricer>(env, simulationPricer, simulationCouponPricer), CloneMethod.FastClone);
      env = clone.Item1;
      env.Conform();
      simulationPricer = clone.Item2;
      simulationCouponPricer = clone.Item3;
      if (exposureDts != null && exposureDts.Any())
      {
        simulationPricer.ExposureDates = exposureDts;
      }
      var pricerExposureDts = simulationPricer.ExposureDates;
      simulationCouponPricer.ExposureDates = simulationPricer.ExposureDates;
      if (pricerExposureDts.Last() > Simulator.SimulationDates.Last())
      {
        throw new ToolkitException($"Last Exposure for {iPricer.Product.Description} later than last simulation date in ExposureSimulator {Name}");
      }

      Logger.VerboseFormat("Using Exposure Dates {0} for {1}.", String.Join(",", pricerExposureDts), iPricer.Product.Description);

      var exposures = CalculateExposuresAndCoupon01(simulationPricer, simulationCouponPricer, env, getPath, recordDiscountFactors, enableCashflowOptimizer);

      RetainExposures(exposures.Exposures, recordDiscountFactors ? exposures.DiscountFactors : null, pricerExposureDts, iPricer, loggingKey);

      path.Dispose();
      exposures.Id = iPricer.Product.Description;
      return exposures;
    }


    /// <summary>
    /// Run simulation and calculate exposures for a single pricer
    /// </summary>
    private ExposureSet Calculate(ISimulationPricer pricer, MarketEnvironment marketEnvironment, Func<int, SimulatedPath> getPath, bool recordDiscountFactors = false, bool enableCashflowOptimizer = true)
    {
      double[,,,] rawDiscountFactors = null;
      Action<int, int> loggingAction = null;
      var exposureDates = ExposureDatesAsOf(pricer, AsOf);
      if (DomesticDiscountFactorLogger.IsObjectLoggingEnabled || ForeignDiscountFactorLogger.IsObjectLoggingEnabled)
      {
        rawDiscountFactors = new double[marketEnvironment.DiscountCurves.Length, PathCount, exposureDates.Length, marketEnvironment.Tenors.Length + 1];
        loggingAction = (p, t) => RecordPath(p, t, marketEnvironment, rawDiscountFactors);
      }
      var dateCount = exposureDates.Length;
      var exposures = new double[PathCount, dateCount];
      var discountFactors = recordDiscountFactors ? new double[PathCount, dateCount] : null; 

      var evaluator = ExposureEvaluatorFactory.GetEvaluator(
        marketEnvironment, exposureDates, Simulator.SimulationDates,
        enableCashflowOptimizer, pricer);

      var exposureSet = new ExposureSet
      {
        ExposureDates = exposureDates,
        Exposures = exposures,
        DiscountFactors = discountFactors
      };

      for (int pathIdx = 0; pathIdx < PathCount; pathIdx++)
      {
        SimulatedPath path = getPath(pathIdx);
        if (path == null)
          return null;

        if (PricerDiagnosticsLogger.IsObjectLoggingEnabled)
        {
          ObjectLoggerUtil.SetPath(pathIdx, "CCRPricerPath");
        }

        evaluator.EvaluatePath(path, exposureSet, loggingAction);
      }
      if (DomesticDiscountFactorLogger.IsObjectLoggingEnabled || ForeignDiscountFactorLogger.IsObjectLoggingEnabled)
      {
        LogDiscountFactors(rawDiscountFactors, marketEnvironment, pricer);
      }

      // if there are no exposure dates it means that the TimeShift has been extended beyond the maturity horizon
      // and therefore a single exposure is injected as zero 
      if (!exposureDates.Any())
      {
        exposureSet.Exposures = new double[exposures.GetLength(0), 1];
        exposureSet.ExposureDates = new[] {AsOf};
      }
      return exposureSet;
    }

    private ExposureSet CalculateAmc(IPricer iPricer, IAmericanMonteCarloAdapter amcPricer, MarketEnvironment env,
      Func<int, SimulatedPath> getPath, Dt[] exposureDts = null, bool recordDiscountFactors = false, string loggingKey = null)
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug(String.Format("Calculating exposures for {0} by AMC.", iPricer.Product.Description));
      }
      Dt[] pricerExposureDts;
      var cashflows = CashflowsAsOf(amcPricer.Cashflow, AsOf);
      // not ideal but for now assume PaymentPricer is a SimpleCashflowPricer
      var paymentPricer = iPricer.PaymentPricer as SimpleCashflowPricer;
      var paymentDates = new UniqueSequence<Dt>();
      if (paymentPricer != null)
      {
        if (cashflows == null)
          cashflows = new List<ICashflowNode>();
        paymentDates = LoadPaymentSchedule(amcPricer, paymentPricer, cashflows, GetMaturity(iPricer));
      }

      if (exposureDts != null && exposureDts.Any())
      {
        // user defined dates
        // set user dates onto amc pricer, pricer will still add 
        // any trade specific dates prior to first user defined date
        amcPricer.ExposureDates = exposureDts;

        var uniqueDts = new UniqueSequence<Dt>();
        // retrieves the union of all user dts and trade specific dts prior to first user dt 
        uniqueDts.Add(amcPricer.ExposureDates);

        // if there are payment dates prior to first user defined date, include those dates
        var start = exposureDts.First();
        if (paymentDates.Any(dt => dt < start))
          uniqueDts.Add(paymentDates.Where(dt => dt < start).ToArray());

        pricerExposureDts = uniqueDts.ToArray();
        amcPricer.ExposureDates = uniqueDts.ToArray();
      }
      else
      {
        // retrieve trade specific dts from amcPricer
        var uniqueDts = new UniqueSequence<Dt>(amcPricer.ExposureDates);
        // include all payment dts too
        uniqueDts.Add(paymentDates.ToArray());

        pricerExposureDts = uniqueDts.ToArray();
        amcPricer.ExposureDates = pricerExposureDts;
      }

      if (pricerExposureDts.Last() > Simulator.SimulationDates.Last())
      {
        throw new ToolkitException(String.Format("Last Exposure for {0} later than last simulation date in ExposureSimulator {1}", iPricer.Product.Description,
          Name));
      }

      if (Logger.IsDebugEnabled)
      {
        Logger.Debug(String.Format("Using Exposure Dates {0} for {1}.", String.Join(",", pricerExposureDts), iPricer.Product.Description));
      }

      var tuple = LeastSquaresMonteCarloCcrPricer.Calculate(amcPricer.Notional, DomesticDiscountProcess.DomesticCurrency, cashflows, amcPricer.Basis,
        amcPricer.CallEvaluator,
        amcPricer.PutEvaluator, 1e-3, amcPricer.ExposureDates, getPath, env, Simulator);

      RetainExposures(tuple.Item1, recordDiscountFactors ? tuple.Item2 : null, pricerExposureDts, iPricer, loggingKey);

      var exposureSet = new ExposureSet() {ExposureDates = pricerExposureDts, Exposures = tuple.Item1, Id = iPricer.Product.Description};
      if (recordDiscountFactors)
        exposureSet.DiscountFactors = tuple.Item2;
      return exposureSet;
    }
    private Dt GetMaturity(IPricer iPricer)
    {
      var expiry = iPricer.Product.Maturity;
      var swaption = iPricer.Product as Swaption;
      if (swaption != null)
        expiry = Dt.AddDays(swaption.Maturity, -swaption.NotificationDays, swaption.NotificationCalendar);
      return expiry;
    }
    private UniqueSequence<Dt> LoadPaymentSchedule(IAmericanMonteCarloAdapter amcPricer, SimpleCashflowPricer paymentPricer, IList<ICashflowNode> cashflows, Dt expiry)
    {
      var paymentDates = new UniqueSequence<Dt>();
      var paymentSched = paymentPricer?.GetPaymentSchedule(null, AsOf);
      if (paymentSched != null)
      {
        foreach (var payment in paymentSched)
        {
          if (payment.PayDt >= expiry)
          {
            payment.PayDt = expiry;
          }
          payment.Scale(1 / amcPricer.Notional);
          var cashflowNode = payment.ToCashflowNode(1, paymentPricer.DiscountCurve, null);
          cashflows.Add(cashflowNode);
          var priorDt = Dt.Add(cashflowNode.PayDt, -1);
          if (priorDt >= AsOf)
            paymentDates.Add(priorDt);
          paymentDates.Add(cashflowNode.PayDt);
        }
      }
      return paymentDates;
    }

    /// <summary>
    /// Run simulation and calculate exposures and discount factors for a swap pricer and swap leg pricer representing the a 1bp coupon added to one of the swap legs
    /// </summary>
    private IncrementalExposureSet CalculateExposuresAndCoupon01(ISimulationPricer pricer, ISimulationPricer coupon01Pricer, MarketEnvironment marketEnvironment, Func<int, SimulatedPath> getPath, bool recordDiscountFactors = false, bool enableCashflowOptimizer = true)
    {
      double[,,,] rawDiscountFactors = null;
      Action<int, int> loggingAction = null;
      if (DomesticDiscountFactorLogger.IsObjectLoggingEnabled || ForeignDiscountFactorLogger.IsObjectLoggingEnabled)
      {
        rawDiscountFactors = new double[marketEnvironment.DiscountCurves.Length, PathCount, pricer.ExposureDates.Length, marketEnvironment.Tenors.Length + 1];
        loggingAction = (p, t) => RecordPath(p, t, marketEnvironment, rawDiscountFactors);
      }

      var dateCount = pricer.ExposureDates.Length;
      var exposures = new double[PathCount, dateCount];
      var coupon01Exposures = coupon01Pricer != null ? new double[PathCount, dateCount] : null;
      var discountFactors = recordDiscountFactors ? new double[PathCount, dateCount] : null;

      var evaluator = ExposureEvaluatorFactory.GetEvaluator(
        marketEnvironment, pricer.ExposureDates, Simulator.SimulationDates,
        enableCashflowOptimizer, pricer, coupon01Pricer);

      var exposureSet = new IncrementalExposureSet
      {
        ExposureDates = pricer.ExposureDates,
        Exposures = exposures,
        Coupon01Exposures = coupon01Exposures,
        DiscountFactors = discountFactors
      };

      for (int pathIdx = 0; pathIdx < PathCount; pathIdx++)
      {
        SimulatedPath path = getPath(pathIdx);
        if (path == null)
          return null;

        if (PricerDiagnosticsLogger.IsObjectLoggingEnabled)
        {
          ObjectLoggerUtil.SetPath(pathIdx, "CCRPricerPath");
        }

        evaluator.EvaluatePath(path, exposureSet, loggingAction);
      }
      if (DomesticDiscountFactorLogger.IsObjectLoggingEnabled || ForeignDiscountFactorLogger.IsObjectLoggingEnabled)
      {
        LogDiscountFactors(rawDiscountFactors, marketEnvironment, pricer);
      }
      return exposureSet;
    }

    private Func<int, SimulatedPath> GenerateSimulationPaths(SimulatedPath path)
    {
      var getPath = new Func<int, SimulatedPath>((int pathId) =>
      {
        path.SetWeinerIncrements(WeinerProcess.GetPathData(pathId));
        path.SetRates(0, DomesticDiscountProcess.GetPathData(pathId));
        for (var i = 0; i < ForeignDiscountSimulations.Count; i++)
        {
          path.SetRates(i + 1, ForeignDiscountSimulations[i].GetPathData(pathId));
        }
        for (var i = 0; i < ForwardCurveSimulations.Count; i++)
        {
          path.SetForwards(i, ForwardCurveSimulations[i].GetPathData(pathId));
        }
        for (var i = 0; i < SpotPriceSimulations.Count; i++)
        {
          path.SetSpot(i, SpotPriceSimulations[i].GetPathData(pathId));
        }
        for (var i = 0; i < SurvivalCurveSimulations.Count; i++)
        {
          path.SetSurvivals(i, SurvivalCurveSimulations[i].GetPathData(pathId));
        }
        path.Id = pathId;
        return path;
      });
      return getPath;
    }

    private Tuple<Simulator, FactorLoadingCollection, VolatilityCollection> InitSimulation()
    {
      EnableNewRateStepSolver(NewRateStepSolverEnabled);

      var factory = Simulator.CreateSimulatorFactory(PathCount, AsOf, WeinerProcess.SimulationDts, WeinerProcess.TenorDts, DomesticDiscountProcess.MarketFactorNames);
      var fls = new FactorLoadingCollection(DomesticDiscountProcess.MarketFactorNames, DomesticDiscountProcess.Calibration.ForwardTenors);
      var vols = new VolatilityCollection(DomesticDiscountProcess.Calibration.ForwardTenors);
      var discountCurve = DomesticDiscountProcess.DiscountCurve;
      factory.AddDomesticDiscount(discountCurve, DomesticDiscountProcess.Volatilities, DomesticDiscountProcess.FactorLoadings, true);
      fls.AddFactors(DomesticDiscountProcess.DiscountCurve, DomesticDiscountProcess.FactorLoadings);
      vols.Add(DomesticDiscountProcess.DiscountCurve, DomesticDiscountProcess.Volatilities);

      foreach (var foreignDiscountSimulation in ForeignDiscountSimulations)
      {
        discountCurve = foreignDiscountSimulation.ForeignDiscountCurve;
        var fxRate = foreignDiscountSimulation.FxRate;
        factory.AddDiscount(discountCurve, foreignDiscountSimulation.Volatilities, foreignDiscountSimulation.FactorLoadings, fxRate, foreignDiscountSimulation.Volatilities, foreignDiscountSimulation.FactorLoadings, true);
        fls.AddFactors(foreignDiscountSimulation.ForeignDiscountCurve, foreignDiscountSimulation.FactorLoadings);
        fls.AddFactors(foreignDiscountSimulation.FxRate, foreignDiscountSimulation.FactorLoadings);
        vols.Add(foreignDiscountSimulation.ForeignDiscountCurve, foreignDiscountSimulation.Volatilities);
        vols.Add(foreignDiscountSimulation.FxRate, foreignDiscountSimulation.Volatilities);
      }
      foreach (var forwardCurveSimulation in ForwardCurveSimulations)
      {
        var fwdCurve = forwardCurveSimulation.ForwardCurve;
        factory.AddForward(fwdCurve, forwardCurveSimulation.Volatilities, forwardCurveSimulation.FactorLoadings, true);
        fls.AddFactors(forwardCurveSimulation.ForwardCurve, forwardCurveSimulation.FactorLoadings);
        vols.Add(forwardCurveSimulation.ForwardCurve, forwardCurveSimulation.Volatilities);
      }
      foreach (var spotPriceSimulation in SpotPriceSimulations)
      {
        var spotCurve = spotPriceSimulation.SpotCurve;
        factory.AddSpot(spotCurve.Spot, spotCurve, spotPriceSimulation.Volatilities, spotPriceSimulation.FactorLoadings, spotCurve.CarryRateAdjustment, spotCurve.CarryCashflow.Select(d => new Tuple<Dt, double>(d.Item1, d.Item3)).ToArray(), true);
        fls.AddFactors(spotPriceSimulation.SpotCurve, spotPriceSimulation.FactorLoadings);
        vols.Add(spotPriceSimulation.SpotCurve, spotPriceSimulation.Volatilities);
      }
      foreach (var survivalCurveSimulation in SurvivalCurveSimulations)
      {
        var survivalCurve = survivalCurveSimulation.SurvivalCurve;
        factory.AddSurvival(survivalCurve, survivalCurveSimulation.Volatilities, survivalCurveSimulation.FactorLoadings, survivalCurveSimulation.IsActive);
        fls.AddFactors(survivalCurveSimulation.SurvivalCurve, survivalCurveSimulation.FactorLoadings);
        vols.Add(survivalCurveSimulation.SurvivalCurve, survivalCurveSimulation.Volatilities);
      }
      return new Tuple<Simulator, FactorLoadingCollection, VolatilityCollection>(factory.Simulator, fls, vols);
    }

    private MarketEnvironment InitMarketEnv()
    {
      Logger.DebugFormat("Initializing MarketEnvironment");
      var discountCurves = new List<DiscountCurve>(); 
      discountCurves.Add(DomesticDiscountProcess.Calibration.DiscountCurve);
      foreach (var foreignDiscountSimulation in ForeignDiscountSimulations)
      {
        discountCurves.Add(foreignDiscountSimulation.ForeignDiscountCalibration.DiscountCurve);
      }
      var fxRates = ForeignDiscountSimulations.Select(sim => sim.FxCalibration.FxRate).ToList();
      fxRates.AddRange(CrossFxRates);
      var fwdCurves =
        ForwardCurveSimulations.Select(
          sim =>
            sim.Calibration is ForwardCurveProcessCalibration
              ? ((ForwardCurveProcessCalibration)sim.Calibration).ForwardCurve
              : ((DiscountProcessCalibration)sim.Calibration).DiscountCurve).ToArray();
      var spotCurves = SpotPriceSimulations.Select(sim => sim.Calibration.ForwardCurve as CalibratedCurve).ToArray();
      var survivalCurves = SurvivalCurveSimulations.Select(sim => sim.Calibration.SurvivalCurve).ToArray();
      return new MarketEnvironment(AsOf, WeinerProcess.TenorDts, discountCurves.ToArray(), fwdCurves, survivalCurves, fxRates.ToArray(), spotCurves);
    }

    private void RetainExposures(double[,] exposures, double[,] discountFactors, IList<Dt> exposureDts, IPricer iPricer, string loggingKey)
    {
      if (!ObjectLogger.IsObjectLoggingEnabled) return;
      var binaryLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(ObjectLogger, System.Reflection.MethodBase.GetCurrentMethod(), loggingKey ?? iPricer.Product.Description);
      binaryLogAggregator.Append(typeof(ExposureSimulator), "Exposures", AppenderUtil.DataTableToDataSet(GetExposuresDataTable(exposures, exposureDts)));
      if (discountFactors != null)
      {
        binaryLogAggregator.Append(typeof(ExposureSimulator), "DiscountFactors", AppenderUtil.DataTableToDataSet(GetDiscountFactorsDataTable(discountFactors, exposureDts)));
      }
      binaryLogAggregator.Log();
      RetainTradesCalibrationData(iPricer, loggingKey);
    }

    private void RetainTradesCalibrationData(IPricer iPricer, string loggingKey)
    {
      if (!PricerObjectLogger.IsObjectLoggingEnabled) return;
      var objectLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(PricerObjectLogger, System.Reflection.MethodBase.GetCurrentMethod(), loggingKey ?? iPricer.Product.Description);

      objectLogAggregator.Append("FactorizedVolatilitySystem", new FactorizedVolatilitySystem(FactorLoadingCollection, VolatilityCollection));
      AppenderUtil.AppendCurves(MarketEnvironment.DiscountCurves, objectLogAggregator);
      AppenderUtil.AppendCurves(MarketEnvironment.CreditCurves, objectLogAggregator);
      AppenderUtil.AppendSpots(MarketEnvironment.FxRates, objectLogAggregator);
      AppenderUtil.AppendCurves(MarketEnvironment.ForwardCurves, objectLogAggregator);
      AppenderUtil.AppendCurves(MarketEnvironment.SpotBasedCurves, objectLogAggregator);
      AppenderUtil.AppendDates(WeinerProcess.TenorDts, objectLogAggregator);
      objectLogAggregator.Append("Pricer", iPricer);
        
      objectLogAggregator.Log();
    }

    private DataTable GetExposuresDataTable(double[,] rawExposures, IList<Dt> exposureDates)
    {
      var dataTable = new DataTable(String.Format("{0}.{1}", Name, "Exposures"));
      dataTable.Columns.Add("PathId", typeof(int));
      for (int i = 0; i < exposureDates.Count; ++i)
      {
        dataTable.Columns.Add(exposureDates[i].ToString(), typeof(double));
      }

      for (int i = 0; i < rawExposures.GetLength(0); ++i)
      {
        var row = dataTable.NewRow();
        row["PathId"] = i;
        for (int j = 0; j < rawExposures.GetLength(1); ++j)
        {
          row[j + 1] = rawExposures[i, j];
        }
        dataTable.Rows.Add(row);
      }

      return dataTable;
    }

    private DataTable GetDiscountFactorsDataTable(double[,] rawDiscountFactors, IList<Dt> exposureDates)
    {
      var dataTable = new DataTable(String.Format("{0}.{1}", Name, "DiscountFactors"));
      dataTable.Columns.Add("PathId", typeof(int));
      for (int i = 0; i < exposureDates.Count; ++i)
      {
        dataTable.Columns.Add(exposureDates[i].ToString(), typeof(double));
      }

      for (int i = 0; i < rawDiscountFactors.GetLength(0); ++i)
      {
        var row = dataTable.NewRow();
        row["PathId"] = i;
        for (int j = 0; j < rawDiscountFactors.GetLength(1); ++j)
        {
          row[j + 1] = rawDiscountFactors[i, j];
        }
        dataTable.Rows.Add(row);
      }

      return dataTable;
    }

    private IAmericanMonteCarloAdapter GetAmcAdapter(IPricer p)
    {
      var amcPricer = p as IAmericanMonteCarloAdapter;
      if (amcPricer != null) return amcPricer;
      var provider = p as IAmericanMonteCarloAdapterProvider;
      return provider != null ? provider.GetAdapter() : null;
    }

    private ISimulationPricer GetSimulationPricer(IPricer p)
    {
      if (p == null)
        return null; 
      return CcrPricer.Get(p);
    }

    /// <summary>
    /// Returns the set of Exposure Dates which occur on and after a specified as of date   
    /// </summary>
    /// <param name="pricer">The Toolkit pricer for the Trade</param>
    /// <param name="asOf">the as of date for the Simulation (may be different to the AsOf date of the risk run in the case of the Time Shift Scenario)</param>
    /// <returns></returns>
    private static Dt[] ExposureDatesAsOf(ISimulationPricer pricer, Dt asOf)
    {
      return pricer.ExposureDates.Where(exposureDate => exposureDate >= asOf).ToArray();
    }

    /// <summary>
    /// Returns the set of Cashflow nodes which occur on and after a specified as of date   
    /// </summary>
    /// <param name="cashflows">A list of Cashflows generated from a AMC Pricer</param>
    /// <param name="asOf">the as of date for the Simulation (may be different to the AsOf date of the risk run in the case of the Time Shift Scenario)</param>
    /// <returns></returns>
    private static IList<ICashflowNode> CashflowsAsOf(IEnumerable<ICashflowNode> cashflows, Dt asOf)
    {
      return cashflows?.Where(cashflow => cashflow.PayDt >= asOf && cashflow.ResetDt >= asOf).ToList();
    }

    private static void LogDiscountFactors(double[,,,] rawDiscountFactors, MarketEnvironment marketEnvironment, ISimulationPricer pricer)
    {
      var asOf = marketEnvironment.AsOf;
      for (int i = 0; i < rawDiscountFactors.GetLength(0); ++i)
      {
        DataTable dataTable = new DataTable("DiscountFactors");
        dataTable.Columns.Add("Path", typeof(int));
        dataTable.Columns.Add("Curve Name", typeof(string));
        dataTable.Columns.Add("Tenors", typeof(string));

        for (int c = 0; c < pricer.ExposureDates.Count(); ++c)
        {
          dataTable.Columns.Add(pricer.ExposureDates[c].ToString(), typeof(double));
        }

        for (int j = 0; j < rawDiscountFactors.GetLength(1); ++j)
        {
          for (int k = 0; k < rawDiscountFactors.GetLength(3); ++k)
          {
            if (i == 0 && k == rawDiscountFactors.GetLength(3) - 1)
            {
              // no need to include the fx rate for domestic discount curve
              continue;
            }
            // hard coded cutoff for the size of recorded datatable to prevent OutOfBoundsException when serializing
            // ideally the datatable should come no where near this number as it will be diffcult to analysis the reported table
            if (dataTable.Rows.Count >= 10000)
            {
              continue;
            }
            var row = dataTable.NewRow();
            row["Path"] = j;
            row["Curve Name"] = marketEnvironment.DiscountCurves[i].Name;
            if (k != rawDiscountFactors.GetLength(3) - 1)
            {
              row["Tenors"] = Tenor.FromDateInterval(asOf, marketEnvironment.Tenors[k]);
            }
            else
            {
              row["Tenors"] = "FxRate";
            }
            for (int m = 0; m < rawDiscountFactors.GetLength(2); ++m)
            {
              row[m + 3] = rawDiscountFactors[i, j, m, k];
            }
            dataTable.Rows.Add(row);
          }
        }
        if (i == 0)
        {
          // domestic discount factors
          // write datatable here and then start again
          var binaryLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(DomesticDiscountFactorLogger, System.Reflection.MethodBase.GetCurrentMethod(), string.Format("DomesticDiscountFactors.{0}", marketEnvironment.DiscountCurves[i].Name));
          binaryLogAggregator.Append(typeof(ExposureSimulator), string.Format("{0}.DiscountFactors", marketEnvironment.DiscountCurves[i].Name), AppenderUtil.DataTableToDataSet(dataTable)).Log();

        }
        else
        {
          // foreign discount factors
          // write datatable here and then start again
          var binaryLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(ForeignDiscountFactorLogger, System.Reflection.MethodBase.GetCurrentMethod(), string.Format("ForeignDiscountFactors.{0}", marketEnvironment.DiscountCurves[i].Name));
          binaryLogAggregator.Append(typeof(ExposureSimulator), string.Format("{0}.ForeignDiscountFactors", marketEnvironment.DiscountCurves[i].Name), AppenderUtil.DataTableToDataSet(dataTable)).Log();
        }
      }
    }

    #region Object Logging

    private static void RecordPath(int pathIdx, int time,
      MarketEnvironment marketEnvironment, double[,,,] rawDiscountFactors)
    {
      for (int i = 0; i < marketEnvironment.DiscountCurves.Length; ++i)
      {
        if (rawDiscountFactors == null)
        {
          continue;
        }
        var nativeCurve = MarketEnvironment.GetNative(marketEnvironment.DiscountCurves[i]);
        for (int j = 0; j < marketEnvironment.Tenors.Length; ++j)
        {
          rawDiscountFactors[i, pathIdx, time, j] = nativeCurve.GetVal(j);
        }
        if (i == 0)
        {
          rawDiscountFactors[i, pathIdx, time, marketEnvironment.Tenors.Length] = 1;
        }
        else
        {
          rawDiscountFactors[i, pathIdx, time, marketEnvironment.Tenors.Length] = marketEnvironment.FxRates[i - 1].Value;
        }
      }
    }

    #endregion

    #region Validation

    /// <summary>
    ///   Validation
    /// </summary>
    /// <param name="errors"></param>
    public override void Validate(ArrayList errors)
    {

      base.Validate(errors);
      if(DomesticDiscountProcess == null)
        InvalidValue.AddError(errors,this, "DomesticDiscountSimulation", "DomesticDiscountSimulation property cannot be null");

    }

    #endregion

    #region Data
    private readonly Lazy<Tuple<Simulator, FactorLoadingCollection, VolatilityCollection>> _simulationData;
    private readonly Lazy<MarketEnvironment> _marketEnv;
    #endregion Data

    #region Configuration

    private static void EnableNewRateStepSolver(bool enable)
    {
      qn_EnableNewLognormalRateStepSolver(enable ? 1 : 0);
    }

    [System.Runtime.InteropServices.DllImport("BaseEntityNative")]
    private static extern IntPtr qn_EnableNewLognormalRateStepSolver(int enable);

    #endregion
  } // class ExposureSimulator


} 
