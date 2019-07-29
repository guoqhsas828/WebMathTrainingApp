using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows.Expressions;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Util.Configuration;
using static BaseEntity.Toolkit.Models.Simulations.ExposureSimulator;

namespace BaseEntity.Toolkit.Models.Simulations
{

  internal static class ExposureEvaluatorFactory
  {
    internal static IExposurePathEvaluatorFactory OptimizerFactory { get; set; }

    /// <summary>
    /// Initializes static members of the <see cref="ExposureEvaluatorFactory"/> class.
    /// </summary>
    /// <exception cref="ToolkitConfigException">Unable to load optimizer factory</exception>
    static ExposureEvaluatorFactory()
    {
      // Try load OptimizerFactory from configuration
      var typeName = ToolkitConfigurator.Settings?.CcrPricer.OptimizerFactory;
      InitOptimizerFactory(typeName);
    }

    internal static void InitOptimizerFactory(string typeName)
    {
      if (string.IsNullOrEmpty(typeName)) return;

      var type = Type.GetType(typeName, false);
      if (type == null)
      {
        throw new ToolkitConfigException($"Unable to load {typeName}", null);
      }
      OptimizerFactory = (IExposurePathEvaluatorFactory)
        Activator.CreateInstance(type);
    }

    internal static IExposurePathEvaluator GetEvaluator(
      MarketEnvironment marketEnvironment,
      Dt[] exposureDates,
      Dt[] simulationDates,
      bool enableCashflowOptimizer,
      params ISimulationPricer[] pricers)
    {
      var factory = enableCashflowOptimizer
        ? (OptimizerFactory ?? OptimizedEvaluatorFactory.Instance)
        : PlainEvaluatorFactory.Instance;
      return factory.GetEvaluator(
        pricers ?? EmptyArray<ISimulationPricer>.Instance,
        marketEnvironment, new[] {exposureDates}, simulationDates);
    }

    internal static FxRate GetFxRate(
      ISimulationPricer pricer, MarketEnvironment mktenv)
    {
      if (pricer == null) return null;

      var ccy = pricer.Ccy;
      var rateCount = mktenv.DiscountCurves?.Length ?? 0;
      for (int i = 1; i < rateCount; ++i)
      {
        // ReSharper disable once PossibleNullReferenceException
        if (mktenv.DiscountCurves[i].Ccy != ccy) continue;
        return mktenv.FxRates[i - 1];
      }
      return null;
    }

  }

  internal class PlainEvaluatorFactory : IExposurePathEvaluatorFactory
  {
    internal static PlainEvaluatorFactory Instance
      = new PlainEvaluatorFactory();

    public IExposurePathEvaluator GetEvaluator(
      ISimulationPricer[] pricers,
      MarketEnvironment marketEnvironment,
      IReadOnlyList<IReadOnlyList<Dt>> exposureDateSet,
      Dt[] simulationDates)
    {
      Debug.Assert(exposureDateSet.Count == 1);
      var exposureDates = exposureDateSet[0];
      Debug.Assert(pricers != null);
      FixVolatilityForConcexityAdjustment(exposureDates, pricers);
      return ExposurePathEvaluator.Get(false, pricers,
        marketEnvironment, exposureDates, simulationDates);
    }

    private static void FixVolatilityForConcexityAdjustment(
      IReadOnlyList<Dt> exposureDates,
      ISimulationPricer[] pricers)
    {
      if (pricers.IsNullOrEmpty() || !ToolkitConfigurator.Settings.
        CcrPricer.FixVolatilityForConvexityAdjustment)
      {
        return;
      }
      // Evaluate PVs to force fixing the volatilities by exposure dates.
      // It must be performed after all the exposure dates are known.
      foreach (var pricer in pricers)
      {
        for (int i = 0, n = exposureDates.Count; i < n; ++i)
        {
          pricer.FastPv(exposureDates[i]);
        }
      }
    }

  }

  internal class OptimizedEvaluatorFactory : IExposurePathEvaluatorFactory
  {
    internal static IExposurePathEvaluatorFactory Instance
      = new OptimizedEvaluatorFactory();

    public IExposurePathEvaluator GetEvaluator(
      ISimulationPricer[] pricers,
      MarketEnvironment marketEnvironment,
      IReadOnlyList<IReadOnlyList<Dt>> exposureDateSet,
      Dt[] simulationDates)
    {
      Debug.Assert(exposureDateSet.Count == 1);
      var exposureDates = exposureDateSet[0];
      return ExposurePathEvaluator.Get(true, pricers,
        marketEnvironment, exposureDates, simulationDates);
    }
  }

  internal class ExposurePathEvaluator : IExposurePathEvaluator
  {
    private readonly IPvEvaluator[] _pricers;
    private readonly FxRate[] _fxRates;
    private readonly IResettable[] _nodes;
    private readonly MarketEnvironment _marketEnvironment;
    private readonly int[] _exposureDateMaps;

    private ExposurePathEvaluator(
      MarketEnvironment marketEnvironment,
      IPvEvaluator[] pricers, FxRate[] fxRates,
      IResettable[] nodes,
      int[] exposureDateMaps)
    {
      _pricers = pricers;
      _fxRates = fxRates;
      _nodes = nodes;
      _marketEnvironment = marketEnvironment;
      _exposureDateMaps = exposureDateMaps;
    }

    internal static ExposurePathEvaluator Get(
      bool enableOptimizer,
      ISimulationPricer[] pricers,
      MarketEnvironment marketEnvironment,
      IReadOnlyList<Dt> exposureDates,
      Dt[] simulationDates)
    {
      Debug.Assert(pricers != null);

      var fxRates = Array.ConvertAll(pricers,
        p => ExposureEvaluatorFactory.GetFxRate(p, marketEnvironment));

      using (Evaluable.PushVariants(ExposureCalculator
        .GetSimulatedObjects(marketEnvironment)))
      {
        var evaluators = Array.ConvertAll(pricers, p => p == null ? null
          : ExposureCalculator.InitializePricer(p, enableOptimizer));
        var nodes = Evaluable.GetCommonEvaluables()
          .OfType<IResettable>().ToArray();
        return new ExposurePathEvaluator(marketEnvironment, evaluators,
          fxRates, nodes, ArrayUtil.ConvertAll(exposureDates,
            dt => Array.BinarySearch(simulationDates, dt)));
      }
    }

    internal void Update(SimulatedPath path, int dateIndex, Dt date)
    {
      if (path != null)
      {
        var mktenv = _marketEnvironment;
        var days = Dt.FractDiff(mktenv.AsOf, date);
        path.Evolve(dateIndex, date, days, mktenv);
      }
      ExposureCalculator.ResetAll(_nodes);
    }

    internal double Evaluate(int priceIndex, int dateIndex, Dt exposureDate)
    {
      var pricer = _pricers[priceIndex];
      if (pricer == null) return 0.0;

      var pv = pricer.FastPv(dateIndex, exposureDate);
      var fx = _fxRates[priceIndex];
      if (fx == null) return pv;

      return pv*fx.GetRate(pricer.Ccy, _marketEnvironment.DiscountCurves[0].Ccy);
    }

    public void EvaluatePath(SimulatedPath path,
      IExposureSet exposureSet, Action<int, int> loggingAction)
    {
      var result = (ExposureSet) exposureSet;
      var pathIdx = path.Id;
      var exposures = result.Exposures;
      var discountFactors = result.DiscountFactors;
      var recordDiscountFactors = discountFactors != null;
      var coupon01Exposures = (result as IncrementalExposureSet)?.Coupon01Exposures;
      if (coupon01Exposures != null && _pricers.Length < 2)
      {
        throw new ToolkitException(
          $"Expect 2 pricers, but got only {_pricers.Length}");
      }

      var exposureDateMaps = _exposureDateMaps;
      var exposureDates = result.ExposureDates;
      var dateCount = exposureDates.Length;
      if (exposureDateMaps.Length != dateCount)
      {
        throw new ToolkitException(
          $"Length of {nameof(exposureDateMaps)} ({exposureDateMaps.Length})"
          + $" not match {exposureDates} ({dateCount})");
      }

      for (int time = 0; time < dateCount; ++time)
      {
        var t = exposureDateMaps[time];
        var dt = exposureDates[time];
        Update(path, t, dt);

        loggingAction?.Invoke(pathIdx, time);

        var pv = Evaluate(0, time, dt);
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
        exposures[pathIdx, time] = pv;

        if (coupon01Exposures != null)
        {
          var couponPv = Evaluate(1, time, dt);
          if (double.IsNaN(couponPv))
          {
            Logger.VerboseFormat("Fast CouponPv returned NaN");
            couponPv = 0.0;
          }
          if (double.IsInfinity(couponPv))
          {
            Logger.VerboseFormat("Fast CouponPv returned Infinity");
            couponPv = 0.0;
          }

          coupon01Exposures[pathIdx, time] = couponPv + pv;
        }

        if (recordDiscountFactors)
          discountFactors[pathIdx, time] = DiscountFactor(dt);
      }
      return;
    }

    private double DiscountFactor(Dt exposureDate)
    {
      return _marketEnvironment.DiscountCurves[0].Interpolate(exposureDate);
    }

  }
}
