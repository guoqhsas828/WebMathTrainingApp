using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Numerics;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  /// Extension methods for ICounterpartyCreditRiskCalculations
  /// </summary>
  public static class IncrementalCCRExtensionMethods
  {
    /// <summary>
    /// Get new total exposure rather than incremental exposure when computing incremental CVA
    /// </summary>
    /// <param name="engine">Engine</param>
    /// <param name="measure">Measure</param>
    /// <param name="netting">Netting data</param>
    /// <param name="date">Date</param>
    /// <param name="alpha">Quantile</param>
    /// <returns>Measure</returns>
    public static double GetMeasureTotal(this ICounterpartyCreditRiskCalculations engine, CCRMeasure measure, Netting netting, Dt date, double alpha)
    {
      if (engine == null)
        return 0.0;
      var e = engine as IncrementalCCRCalculations;
      if (e != null)
        return e.GetMeasureTotal(measure, netting, date, alpha);
      return engine.GetMeasure(measure, netting, date, alpha);
    }
  }

  /// <summary>
  /// CCR calculations for incremental pricing
  /// </summary>
  [Serializable]
  internal class IncrementalCCRCalculations : CCRCalculations, ICounterpartyCreditRiskCalculations
  {
    #region Properties

    private ISimulatedValues OldPaths { get; set; }

    #endregion

    #region Constructor

    /// <summary>
    ///Constructor 
    /// </summary>
    /// <param name="oldPaths">Saved exposure cube</param>
    /// <param name="pathCount">number of paths to use</param>
    /// <param name="exposureDates">Exposure dates</param>
    /// <param name="environment">Market environment</param>
    /// <param name="volatilities">volatilities</param>
    /// <param name="factorLoadings">factor loadings</param>
    /// <param name="cptyDefaultTimeCorrelation">Correlation between default time of counterparty and booking entity</param>
    /// <param name="rngType">The type of random number generator</param>
    /// <param name="portfolio">Portfolio data</param>
    ///<param name="unilateral">treat default unilaterally or jointly (first-to-default) </param>
    internal IncrementalCCRCalculations(
      ISimulatedValues oldPaths,
      int pathCount,
      Dt[] exposureDates,
      CCRMarketEnvironment environment,
      VolatilityCollection volatilities,
      FactorLoadingCollection factorLoadings,
      double cptyDefaultTimeCorrelation,
      MultiStreamRng.Type rngType,
      PortfolioData portfolio, 
      bool unilateral)
      : base(null,
        pathCount, exposureDates, environment, volatilities, factorLoadings, rngType, portfolio, cptyDefaultTimeCorrelation, unilateral)
    {
      OldPaths = oldPaths;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Display CCR measure
    /// </summary>
    /// <param name="measure">Measure enum constant</param>
    /// <param name="netting">Netting rule</param>
    /// <param name="date">Future date (required only for time-bucketed measures)</param>
    /// <param name="alpha">Confidence level (required only for tail measures)</param>
    /// <returns>CCRMeasure</returns>
    public double GetMeasure(CCRMeasure measure, Netting netting, Dt date, double alpha)
    {
      Dictionary<string, int> map = Portfolio.Map;
      PathWiseExposure exposure;
      if (SimulatedValues == null)
        throw new ArgumentException("Must Execute before calculating risk measures");
      double newVal, oldVal, retVal = 0.0;
      switch (measure)
      {
        case CCRMeasure.CVA:
          if (DefaultKernel.Length >= 1)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
            retVal =
              -Integrate(IncrementalPv, true, exposure, CptyRn, alpha, DefaultKernel[0], Environment.CptyRecovery(0));
          }
          break;
        case CCRMeasure.DVA:
          if (DefaultKernel.Length >= 2)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
            retVal = Integrate(IncrementalPv, true, exposure, OwnRn, alpha, DefaultKernel[1], Environment.CptyRecovery(1));
          }
          break;
        case CCRMeasure.CVA0:
          if (DefaultKernel.Length >= 1)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
            retVal =
              -Integrate(IncrementalPv, true, exposure, ZeroRn, alpha, DefaultKernel[0], Environment.CptyRecovery(0));
          }
          break;
        case CCRMeasure.DVA0:
          if (DefaultKernel.Length >= 2)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
            retVal = Integrate(IncrementalPv, true, exposure, ZeroRn, alpha, DefaultKernel[1], Environment.CptyRecovery(1));
          }
          break;
        case CCRMeasure.FCA:
          if (SurvivalKernel != null)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
            retVal = -Integrate(IncrementalFundingCost, true, exposure, FundingRn, alpha, SurvivalKernel, 0.0);
          }
          break;
        case CCRMeasure.FCA0:
          if (SurvivalKernel != null)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
            retVal =
              -Integrate((d, df, ex, rn, pVal) => BorrowSpread(d, rn) * IncrementalPv(d, df, ex, rn, pVal), true, exposure,
                         ZeroRn, alpha, SurvivalKernel, 0.0);
          }
          break;
        case CCRMeasure.FCANoDefault:
          if (NoDefaultKernel != null)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
            retVal =
              -Integrate((d, df, ex, rn, pVal) => BorrowSpread(d, rn) * IncrementalPv(d, df, ex, rn, pVal), true, exposure,
                         ZeroRn, alpha, NoDefaultKernel, 0.0);
          }
          break;
        case CCRMeasure.FBA:
          if (SurvivalKernel != null)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
            retVal = Integrate(IncrementalFundingBenefit, true, exposure, FundingRn, alpha, SurvivalKernel, 0.0);
          }
          break;
        case CCRMeasure.FBA0:
          if (SurvivalKernel != null)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
            retVal =
              Integrate((d, df, ex, rn, pVal) => LendSpread(d, rn) * IncrementalPv(d, df, ex, rn, pVal), true, exposure,
                         ZeroRn, alpha, SurvivalKernel, 0.0);
          }
          break;
        case CCRMeasure.FBANoDefault:
          if (NoDefaultKernel != null)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
            retVal =
              Integrate((d, df, ex, rn, pVal) => LendSpread(d, rn) * IncrementalPv(d, df, ex, rn, pVal), true, exposure,
                         ZeroRn, alpha, NoDefaultKernel, 0.0);
          }
          break;
        case CCRMeasure.FVA:
          {
            retVal = this.GetMeasure(CCRMeasure.FCA, netting, Dt.Empty, 0.0) + this.GetMeasure(CCRMeasure.FBA, netting, Dt.Empty, 0.0);
          }
          break;
        case CCRMeasure.FVA0:
          {
            retVal = this.GetMeasure(CCRMeasure.FCA0, netting, Dt.Empty, 0.0) + this.GetMeasure(CCRMeasure.FBA0, netting, Dt.Empty, 0.0);
          }
          break;
        case CCRMeasure.FVANoDefault:
          {
            retVal = this.GetMeasure(CCRMeasure.FCANoDefault, netting, Dt.Empty, 0.0) + this.GetMeasure(CCRMeasure.FBANoDefault, netting, Dt.Empty, 0.0);
          }
          break;
        case CCRMeasure.EC:
          if (DefaultKernel.Length >= 1)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
            newVal = -Integrate(
              (d, df, ex, rn, pVal) => Pfe(d, df, ex, rn, pVal) - Pv(d, df, ex, rn, pVal), true, exposure,
              CptyRn, alpha,
              DefaultKernel[0], Environment.CptyRecovery(0));
            oldVal = -Integrate(
              (d, df, ex, rn, pVal) => OldPfe(d, df, ex, rn, pVal) - OldPv(d, df, ex, rn, pVal), true,
              exposure,
              CptyRn, alpha,
              DefaultKernel[0], Environment.CptyRecovery(0));
            retVal = newVal - oldVal;
          }
          break;
        case CCRMeasure.EC0:
          if (DefaultKernel.Length >= 1)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
            newVal = -Integrate(
              (d, df, ex, rn, pVal) => Pfe(d, df, ex, rn, pVal) - Pv(d, df, ex, rn, pVal), true, exposure,
              ZeroRn, alpha,
              DefaultKernel[0], Environment.CptyRecovery(0));
            oldVal = -Integrate(
              (d, df, ex, rn, pVal) => OldPfe(d, df, ex, rn, pVal) - OldPv(d, df, ex, rn, pVal), true,
              exposure,
              ZeroRn, alpha,
              DefaultKernel[0], Environment.CptyRecovery(0));
            retVal = newVal - oldVal;
          }
          break;
        case CCRMeasure.DiscountedEPV:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.None);
          retVal = Interpolate(IncrementalPv, date, true, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.EPV:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.None);
          retVal = Interpolate(IncrementalPv, date, false, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.EE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = Interpolate(IncrementalPv, date, false, exposure, CptyRn, alpha);
          break;
        case CCRMeasure.CE:
        case CCRMeasure.EE0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = Interpolate(IncrementalPv, date, false, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.DiscountedEE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = Interpolate(IncrementalPv, date, true, exposure, CptyRn, alpha);
          break;
        case CCRMeasure.DiscountedCE:
        case CCRMeasure.DiscountedEE0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          retVal = Interpolate(IncrementalPv, date, true, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.NEE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          retVal = Interpolate(IncrementalPv, date, false, exposure, OwnRn, alpha);
          break;
        case CCRMeasure.DiscountedNEE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          retVal = Interpolate(IncrementalPv, date, true, exposure, OwnRn, alpha);
          break;
        case CCRMeasure.NEE0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          retVal = Interpolate(IncrementalPv, date, false, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.DiscountedNEE0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          retVal = Interpolate(IncrementalPv, date, true, exposure, ZeroRn, alpha);
          break;
        case CCRMeasure.PFE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          newVal = Interpolate(Pfe, date, false, exposure, CptyRn, alpha);
          oldVal = Interpolate(OldPfe, date, false, exposure, CptyRn, alpha);
          retVal = newVal - oldVal;
          break;
        case CCRMeasure.DiscountedPFE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          newVal = Interpolate(Pfe, date, true, exposure, CptyRn, alpha);
          oldVal = Interpolate(OldPfe, date, true, exposure, CptyRn, alpha);
          retVal = newVal - oldVal;
          break;
        case CCRMeasure.PFE0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          newVal = Interpolate(Pfe, date, false, exposure, ZeroRn, alpha);
          oldVal = Interpolate(OldPfe, date, false, exposure, ZeroRn, alpha);
          retVal = newVal - oldVal;
          break;
        case CCRMeasure.DiscountedPFE0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          newVal = Interpolate(Pfe, date, true, exposure, ZeroRn, alpha);
          oldVal = Interpolate(OldPfe, date, true, exposure, ZeroRn, alpha);
          retVal = newVal - oldVal;
          break;
        case CCRMeasure.PFNE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          newVal = Interpolate(Pfe, date, false, exposure, OwnRn, alpha);
          oldVal = Interpolate(OldPfe, date, false, exposure, OwnRn, alpha);
          retVal = newVal - oldVal;
          break;
        case CCRMeasure.DiscountedPFNE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          newVal = Interpolate(Pfe, date, true, exposure, OwnRn, alpha);
          oldVal = Interpolate(OldPfe, date, true, exposure, OwnRn, alpha);
          retVal = newVal - oldVal;
          break;
        case CCRMeasure.Sigma:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          newVal = Interpolate(Sigma, date, true, exposure, CptyRn, alpha);
          oldVal = Interpolate(OldSigma, date, true, exposure, CptyRn, alpha);
          retVal = newVal - oldVal;
          break;
        case CCRMeasure.EEE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          newVal = RunningMax(Pv, date, false, exposure, CptyRn, alpha);
          oldVal = RunningMax(OldPv, date, false, exposure, CptyRn, alpha);
          retVal = newVal - oldVal;
          break;
        case CCRMeasure.EPE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          newVal = TimeAverage(Pv, date, false, exposure, CptyRn, alpha);
          oldVal = TimeAverage(OldPv, date, false, exposure, CptyRn, alpha);
          retVal = newVal - oldVal;
          break;
        case CCRMeasure.ENE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
          newVal = TimeAverage(Pv, date, false, exposure, OwnRn, alpha);
          oldVal = TimeAverage(OldPv, date, false, exposure, OwnRn, alpha);
          retVal = newVal - oldVal;
          break;
        case CCRMeasure.EEPE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          newVal = TimeAverage((d, df, ex, rn, pVal) => RunningMax(Pv, ExposureDates[d], df, ex, rn, pVal),
                               date, false, exposure, CptyRn, alpha);
          oldVal = TimeAverage((d, df, ex, rn, pVal) => RunningMax(OldPv, ExposureDates[d], df, ex, rn, pVal),
                               date, false, exposure, CptyRn, alpha);
          retVal = newVal - oldVal;
          break;
        case CCRMeasure.MPFE:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          newVal = RunningMax(Pfe, ExposureDates.Last(), false, exposure, CptyRn, alpha);
          oldVal = RunningMax(OldPfe, ExposureDates.Last(), false, exposure, CptyRn, alpha);
          retVal = newVal - oldVal;
          break;
        case CCRMeasure.EffectiveMaturity:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          newVal = EffectiveMaturity(Pv, exposure, CptyRn);
          oldVal = EffectiveMaturity(OldPv, exposure, CptyRn);
          retVal = newVal - oldVal;
          break;
        case CCRMeasure.EffectiveMaturity0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          newVal = EffectiveMaturity(Pv, exposure, ZeroRn);
          oldVal = EffectiveMaturity(OldPv, exposure, ZeroRn);
          retVal = newVal - oldVal;
          break;
        case CCRMeasure.RWA:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          newVal = RiskWeightedAssets(Pv, exposure, CptyRn);
          oldVal = RiskWeightedAssets(OldPv, exposure, CptyRn);
          retVal = newVal - oldVal;
          break;
        case CCRMeasure.RWA0:
          exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
          newVal = RiskWeightedAssets(Pv, exposure, ZeroRn);
          oldVal = RiskWeightedAssets(OldPv, exposure, ZeroRn);
          retVal = newVal - oldVal;
          break;
        case CCRMeasure.BucketedCVA:
          if (DefaultKernel.Length >= 0)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
            newVal = BucketAverageExposure(Pv, Environment.AsOf, date, ExposureDates, true, exposure, CptyRn, alpha, DefaultKernel[0],
                                           Environment.CptyRecovery(0));
            oldVal = BucketAverageExposure(OldPv, Environment.AsOf, date, ExposureDates, true, exposure, CptyRn, alpha, DefaultKernel[0],
                                           Environment.CptyRecovery(0));
            retVal = -newVal + oldVal;
          }
          break;
        case CCRMeasure.BucketedCVA0:
          if (DefaultKernel.Length >= 0)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.Counterparty);
            newVal = BucketAverageExposure(Pv, Environment.AsOf, date, ExposureDates, true, exposure, ZeroRn, alpha, DefaultKernel[0],
                                           Environment.CptyRecovery(0));
            oldVal = BucketAverageExposure(OldPv, Environment.AsOf, date, ExposureDates, true, exposure, ZeroRn, alpha, DefaultKernel[0],
                                           Environment.CptyRecovery(0));
            retVal = -newVal + oldVal;
          }
          break;
        case CCRMeasure.BucketedDVA:
          if (DefaultKernel.Length >= 1)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
            newVal = BucketAverageExposure(Pv, Environment.AsOf, date, ExposureDates, true, exposure, OwnRn, alpha, DefaultKernel[1],
                                           Environment.CptyRecovery(1));
            oldVal = BucketAverageExposure(OldPv, Environment.AsOf, date, ExposureDates, true, exposure, OwnRn, alpha, DefaultKernel[1],
                                           Environment.CptyRecovery(1));
            retVal = newVal - oldVal;

          }
          break;
        case CCRMeasure.BucketedDVA0:
          if (DefaultKernel.Length >= 1)
          {
            exposure = new PathWiseExposure(ExposureDates, map, netting, PathWiseExposure.RiskyParty.BookingEntity);
            newVal = BucketAverageExposure(Pv, Environment.AsOf, date, ExposureDates, true, exposure, ZeroRn, alpha, DefaultKernel[1],
                                           Environment.CptyRecovery(1));
            oldVal = BucketAverageExposure(OldPv, Environment.AsOf, date, ExposureDates, true, exposure, ZeroRn, alpha, DefaultKernel[1],
                                           Environment.CptyRecovery(1));
            retVal = newVal - oldVal;
          }
          break;
        default:
          throw new NotSupportedException(String.Format("Measure {0} not implemented", measure));
      }
      return retVal;
    }

    /// <summary>
    /// Create simulation engine
    /// </summary>
    /// <returns>Simulation engine</returns>
    internal override Simulator CreateSimulator()
    {
      var simulDates = Simulations.GenerateSimulationDates(Environment.AsOf, ExposureDates, Environment.Tenors,
                                                           Environment.GridSize);
      return Simulations.CreateSimulator(SimulationModel, SampleSize, simulDates, Environment, Volatilities, FactorLoadings, CptyDefaultTimeCorrelation);
    }

    /// <summary>
    /// Perform calculations
    /// </summary>
    protected override void Simulate()
    {
      using (var engine = CreateSimulator())
      {
        MultiStreamRng rng = CreateRng(engine);
        var pathsToUse = new List<ISimulatedPathValues>();
        pathsToUse.AddRange(from p in OldPaths.Paths where p.Id < engine.PathCount select p);
        SimulatedValues = Simulations.CalculateExposures(pathsToUse, ExposureDates, engine, rng, Environment, Portfolio, IsUnilateral);
        foreach (var p in Portfolio.Exotics)
        {
          int nettingSet = p.Item3;
          var grid = Simulations.GenerateLeastSquaresGrid(Environment, engine.SimulationDates, engine.PathCount, RngType,
                                                          Volatilities, FactorLoadings, p, ExposureDates);
          foreach (var v in SimulatedValues.Paths)
          {
            int idx = v.Id;
            for (int t = 0; t < v.DateCount; ++t)
              v.SetPortfolioValue(t, nettingSet, v.GetPortfolioValue(t, nettingSet) + grid[idx, t]);
          }
        }

        //Simulate the Default Kernel.
        if (Environment.CptyCcy.Length >= 2 && IsUnilateral)
          DefaultKernel = ArrayUtil.Generate(2, i =>
          {
            var krn =
              new double[engine.SimulationDates.Length];
            engine.DefaultKernel(i + 3, krn);
            return
              new Tuple<Dt[], double[]>(
                engine.SimulationDates, krn);
          });
        else
          DefaultKernel = ArrayUtil.Generate(2, i =>
          {
            var krn =
              new double[engine.SimulationDates.Length];
            engine.DefaultKernel(i, krn);
            return
              new Tuple<Dt[], double[]>(
                engine.SimulationDates, krn);
          });

        //Simulate the Survival Kernel. 
        if (Environment.CptyIndex.Length >= 2)
        // we have both cpty curve and own curve
        {
          var krn = new double[engine.SimulationDates.Length];
          engine.SurvivalKernel((IsUnilateral) ? 1 : 2, krn);
          SurvivalKernel = new Tuple<Dt[], double[]>(engine.SimulationDates, krn);
        }
        else if (Environment.CptyIndex.Length == 1)
        // we don't have own curve, by default, own survival probability = 1.
        {
          var krn = new double[engine.SimulationDates.Length];
          engine.SurvivalKernel((IsUnilateral) ? 3 : 4, krn);
          SurvivalKernel = new Tuple<Dt[], double[]>(engine.SimulationDates, krn);
        }

        //Simulate the NoDefault Kernel.
        var pretime = Environment.AsOf;
        var krn0 = new double[engine.SimulationDates.Length];
        for (int t = 0; t < engine.SimulationDates.Length; ++t)
        {
          krn0[t] = (engine.SimulationDates[t] - pretime) / 365.0;
          pretime = engine.SimulationDates[t];
        }
        NoDefaultKernel = new Tuple<Dt[], double[]>(engine.SimulationDates, krn0);

      }
    }


    private double OldPv(int d, bool discount, PathWiseExposure exposure, RadonNikodymDerivative radonNikodym,
                         double pVal)
    {
      double retVal = 0.0;
      double norm = 0.0;
      var oldPaths = OldPaths.Paths.ToList();
      var newPaths = SimulatedValues.Paths.ToList();
      oldPaths.Sort((path, otherPath) => path.Id.CompareTo(otherPath.Id));
      newPaths.Sort((path, otherPath) => path.Id.CompareTo(otherPath.Id));
      for (int i = 0; i < newPaths.Count; i++)
      {
        var oldPath = oldPaths[i];
        var newPath = (SimulatedPathValues) newPaths[i];
        double wt = newPath.Weight;
        double rn = radonNikodym(newPath, d);
        double w = wt*rn;
        double df = newPath.GetDiscountFactor(d);
        double e = exposure.ComputeOld(oldPath, newPath, d);
        retVal += w*df*e;
        norm += discount ? w : w*df;
      }
      return (norm <= 0.0) ? 0.0 : retVal/norm;
    }

    private double IncrementalFundingCost(int d, bool discount, PathWiseExposure exposure,
                                       RadonNikodymDerivative radonNikodym, double pVal)
    {
      double retVal = 0.0;
      double norm = 0.0;
      var newPaths = SimulatedValues.Paths.ToList();
      for (int i = 0; i < newPaths.Count; ++i)
      {
        var newPath = (SimulatedPathValues) newPaths[i];
        double wt = newPath.Weight;
        double rn = radonNikodym(newPath, d);
        double w = wt*rn;
        double df = newPath.GetDiscountFactor(d);
        double e = exposure.ComputeIncremental(newPath, d) * newPath.GetBorrowSpread(d);
        retVal += w * df * e;
        norm += discount ? w : df * w;
      }
      return (norm <= 0.0) ? 0.0 : retVal / norm;
    }

    private double IncrementalFundingBenefit(int d, bool discount, PathWiseExposure exposure,
                                       RadonNikodymDerivative radonNikodym, double pVal)
    {
      double retVal = 0.0;
      double norm = 0.0;
      var newPaths = SimulatedValues.Paths.ToList();
      for (int i = 0; i < newPaths.Count; ++i)
      {
        var newPath = (SimulatedPathValues)newPaths[i];
        double wt = newPath.Weight;
        double rn = radonNikodym(newPath, d);
        double w = wt * rn;
        double df = newPath.GetDiscountFactor(d);
        double e = exposure.ComputeIncremental(newPath, d) * newPath.GetLendSpread(d);
        retVal += w * df * e;
        norm += discount ? w : df * w;
      }
      return (norm <= 0.0) ? 0.0 : retVal / norm;
    }

    private double IncrementalPv(int d, bool discount, PathWiseExposure exposure, RadonNikodymDerivative radonNikodym,
                              double pVal)
    {
      double retVal = 0.0;
      double norm = 0.0;
      var newPaths = SimulatedValues.Paths.ToList();
      for (int i = 0; i < newPaths.Count; i++)
      {
        var newPath = (SimulatedPathValues) newPaths[i];
        double wt = newPath.Weight;
        double rn = radonNikodym(newPath, d);
        double w = wt*rn;
        double df = newPath.GetDiscountFactor(d);
        double e = exposure.ComputeIncremental(newPath, d);
        retVal += w*df*e;
        norm += discount ? w : w*df;
      }
      return (norm <= 0.0) ? 0.0 : retVal/norm;
    }

    private double OldSigma(int d, bool discount, PathWiseExposure exposure,
                            RadonNikodymDerivative radonNikodym, double pVal)
    {
      double retVal = 0.0;
      double retVal2 = 0.0;
      double norm = 0.0;
      var oldPaths = OldPaths.Paths.ToList();
      var newPaths = SimulatedValues.Paths.ToList();
      oldPaths.Sort((path, otherPath) => path.Id.CompareTo(otherPath.Id));
      newPaths.Sort((path, otherPath) => path.Id.CompareTo(otherPath.Id));
      for (int i = 0; i < newPaths.Count; i++)
      {
        var oldPath = oldPaths[i];
        var newPath = (SimulatedPathValues) newPaths[i];
        double wt = newPath.Weight;
        double rn = radonNikodym(newPath, d);
        double w = wt*rn;
        double df = newPath.GetDiscountFactor(d);
        double e = exposure.ComputeOld(oldPath, newPath, d);
        retVal += w*df*e;
        retVal2 += discount ? w*df*df*e*e : w*df*e*e;
        norm += discount ? w : df*w;
      }
      return (norm <= 0.0) ? 0.0 : Math.Sqrt(Math.Max(retVal2/norm - retVal/norm*retVal/norm, 0.0));
    }

    private double OldPfe(int d, bool discount, PathWiseExposure exposure, RadonNikodymDerivative radonNikodym,
                          double pVal)
    {
      double mass = 0, norm = 0.0;
      var oldPaths = OldPaths.Paths.ToList();
      var newPaths = SimulatedValues.Paths.ToList();
      oldPaths.Sort((path, otherPath) => path.Id.CompareTo(otherPath.Id));
      newPaths.Sort((path, otherPath) => path.Id.CompareTo(otherPath.Id));
      var pdf = new List<Tuple<double, double>>();
      for (int i = 0; i < newPaths.Count; i++)
      {
        var oldPath = oldPaths[i];
        var newPath = (SimulatedPathValues) newPaths[i];
        double wt = newPath.Weight;
        double rn = radonNikodym(newPath, d);
        double w = wt*rn;
        if (w <= 0.0)
          continue;
        double df = newPath.GetDiscountFactor(d);
        double e = exposure.ComputeOld(oldPath, newPath, d);
        if (discount)
          e *= df;
        else
          w *= df;
        norm += w;
        if (e <= 0)
          continue;
        pdf.Add(new Tuple<double, double>(e, w));
        mass += w;
      }
      if (mass <= 0.0)
        return 0.0;
      if (mass < norm)
        pdf.Add(new Tuple<double, double>(0.0, norm - mass));
      pdf.Sort((x, y) => (x.Item1 < y.Item1) ? -1 : (x.Item1 > y.Item1) ? 1 : 0);
      var mtm = new List<double>();
      var cdf = new List<double>();
      double pv = pdf[0].Item1;
      double pm = pdf[0].Item2/norm;
      mtm.Add(pv);
      cdf.Add(pm);
      for (int i = 1; i < pdf.Count; ++i)
      {
        double pvNew = pdf[i].Item1;
        pm += pdf[i].Item2/norm;
        if (pvNew > pv)
        {
          mtm.Add(pvNew);
          cdf.Add(pm);
        }
        else
          cdf[cdf.Count - 1] = pm;
        pv = pvNew;
      }
      var distribution = new EmpiricalDistribution(mtm.ToArray(), cdf.ToArray());
      return distribution.Quantile(pVal);
    }


    ///<summary>
    /// New totals, rather than incremental deltas
    ///</summary>
    ///<param name="measure"></param>
    ///<param name="netting"></param>
    ///<param name="date"></param>
    ///<param name="alpha"></param>
    ///<returns></returns>
    public double GetMeasureTotal(CCRMeasure measure, Netting netting, Dt date, double alpha)
    {
      return GetMeasureImpl(measure, netting, date, alpha);
    }

    #endregion
  }
}