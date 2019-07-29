using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using log4net;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  /// 
  /// </summary>
  public class NoNetExposureAggregator : CCRExposureAggregatorManaged
  {
    #region Data

    private IList<CCRMeasureAccumulator> _standaloneMeasureAccumulators;
    private CCRMeasureAccumulator _totalMeasureAccumulator;
    private bool _reduced;
    IList<double>[] _df, _numeraire, _cptyRn, _ownRn, _survivalRn, _cptySpread, _ownSpread, _lendSpread, _borrowSpread;
    private double[,] _totalPositiveExposures;
    private double[,] _totalNegativeExposures;
    private static readonly ILog Logger = LogManager.GetLogger(typeof(NoNetExposureAggregator));

    private static readonly CCRMeasure[] NonAdditiveMeasures = { CCRMeasure.PFE, CCRMeasure.PFE0, CCRMeasure.DiscountedPFE, CCRMeasure.DiscountedPFE0, CCRMeasure.PFCSA, CCRMeasure.PFNCSA, CCRMeasure.PFNE, CCRMeasure.DiscountedPFNE, CCRMeasure.EEE, CCRMeasure.EEPE, CCRMeasure.ENE, CCRMeasure.EPE, CCRMeasure.StdErrEE, CCRMeasure.StdErrNEE, CCRMeasure.StdErrDiscountedEE, CCRMeasure.StdErrDiscountedNEE, CCRMeasure.EffectiveMaturity};
    private static readonly CCRMeasure[] NegativeNonAdditiveMeasures = { CCRMeasure.PFNCSA, CCRMeasure.PFNE, CCRMeasure.DiscountedPFNE, CCRMeasure.ENE, CCRMeasure.StdErrNEE, CCRMeasure.StdErrDiscountedNEE };
    private DataTable _diagnosticTable;
    private readonly bool _binaryLoggingEnabled;
    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="exposureDts">list of dates exposures are calculated</param>
    /// <param name="unilateral">treat default unilaterally or jointly (first-to-default) </param>
    /// <param name="asOf"></param>
    /// <param name="marketData"></param>
    /// <param name="exposures"></param>
    /// <param name="discountExposures">discount exposures to present value when reporting expectations</param>
    /// <param name="wrongWayRisk">adjust for correlation between default prob and exposure</param>
    /// <param name="fundingCostNoDefault">report FCA without discounting for default risk</param>
    /// <param name="fundingBenefitNoDefault">report FBA without discounting for default risk</param>
    /// <param name="binaryLoggingEnabled">enable diagnostic logging</param>
    /// <param name="modelOvercollateralization"></param>
    /// <param name="cptyCurve">The Counterparty Curve required to compute RWA</param>
    public NoNetExposureAggregator(
      Dt asOf,
      PrecalculatedMarketData marketData,
      PrecalculatedExposures exposures,
      Dt[] exposureDts,
      bool unilateral = false,
      bool discountExposures = false,
      bool wrongWayRisk = true,
      bool fundingCostNoDefault = false,
      bool fundingBenefitNoDefault = false,
      bool binaryLoggingEnabled = false,
      bool modelOvercollateralization = false,
      SurvivalCurve cptyCurve = null
      )
    {
      AsOf = asOf;
      NettingSets = exposures.NettingGroups;
      ExposureDates = exposureDts;
      IsUnilateral = unilateral;
      DiscountExposures = discountExposures;
      WrongWayRisk = wrongWayRisk;
      FundingCostNoDefault = fundingCostNoDefault;
      FundingBenefitNoDefault = fundingBenefitNoDefault;
      PrecalculatedMarketData = marketData;
      CptyCurve = cptyCurve;
      IntegrationKernels = TransformIntegrationKernels(marketData.IntegrationKernels, AsOf, ExposureDates, IsUnilateral);
      _binaryLoggingEnabled = binaryLoggingEnabled;

      NettingMap = new Dictionary<string, int>();
      for (int i = 0; i < exposures.NettingGroups.Count; i++)
      {
        NettingMap[exposures.NettingGroups[i]] = i;
      }

      if(NettingMap.Count < exposures.Count)
        throw new NotSupportedException("NoNetAggregator does not support netting");

      var netting = new Netting(exposures.NettingGroups.ToArray(), null, null);

      CounterpartyExposure = new PathWiseExposure(ExposureDates, NettingMap, netting,
        PathWiseExposure.RiskyParty.Counterparty, modelOvercollateralization);
      BookingEntityExposure = new PathWiseExposure(ExposureDates, NettingMap, netting,
        PathWiseExposure.RiskyParty.BookingEntity, modelOvercollateralization);
   
      PrecalculatedPvs = exposures;
    }


    private NoNetExposureAggregator(NoNetExposureAggregator other, Tuple<Dt[], double[]>[] kernels)
    {
      _standaloneMeasureAccumulators = other._standaloneMeasureAccumulators.CloneObjectGraph(CloneMethod.FastClone);
      foreach (var accumulator in _standaloneMeasureAccumulators)
      {
        accumulator.IntegrationKernels = kernels;  
      }
      _totalMeasureAccumulator = other._totalMeasureAccumulator.CloneObjectGraph(CloneMethod.FastClone);
      _totalMeasureAccumulator.IntegrationKernels = kernels; 

      _totalPositiveExposures = other._totalPositiveExposures;
      _totalNegativeExposures = other._totalNegativeExposures;

      _reduced = other._reduced;
      IsUnilateral = other.IsUnilateral;
      DiscountExposures = other.DiscountExposures;
      WrongWayRisk = other.WrongWayRisk;
      FundingCostNoDefault = other.FundingCostNoDefault;
      FundingBenefitNoDefault = other.FundingBenefitNoDefault;
      _df = other._df;
      _numeraire = other._numeraire;
      _cptyRn = other._cptyRn;
      _ownRn = other._ownRn;
      _survivalRn = other._survivalRn;
      _cptySpread = other._cptySpread;
      _ownSpread = other._ownSpread;
      _lendSpread = other._lendSpread;
      _borrowSpread = other._borrowSpread;
      AsOf = other.AsOf;
      CounterpartyExposure = other.CounterpartyExposure;
      BookingEntityExposure = other.BookingEntityExposure;
      PrecalculatedPvs = other.PrecalculatedPvs;
      NettingSets = other.NettingSets;
      ExposureDates = other.ExposureDates;
      NettingMap = other.NettingMap;
      PrecalculatedMarketData = other.PrecalculatedMarketData;
      _diagnosticTable = other._diagnosticTable;
      _binaryLoggingEnabled = other._binaryLoggingEnabled;
    }

    /// <summary>
    /// Indicates if the Exposure Aggregator supports diagnostic logging
    /// </summary>
    public override bool DiagnosticsSupported => _binaryLoggingEnabled;

    #endregion

    #region

    private void RunSimulationPaths()
    {
      Logger.DebugFormat("Preinitializing market data");
      for (var p = 0; p < PathCount; ++p)
      {
        IList<double> df, numeraire, cptyRn, ownRn, survivalRn, cptySpread, ownSpread, lendSpread, borrowSpread;
        PrecalculatedMarketData.GetMarketDataForPath(p, WrongWayRisk, IsUnilateral, out numeraire, out df, out cptyRn, out ownRn, out survivalRn,
          out cptySpread, out ownSpread, out lendSpread, out borrowSpread);
        _df[p] = df;
        _numeraire[p] = numeraire;
        _cptyRn[p] = cptyRn;
        _ownRn[p] = ownRn;
        _survivalRn[p] = survivalRn;
        _cptySpread[p] = cptySpread;
        _ownSpread[p] = ownSpread;
        _lendSpread[p] = lendSpread;
        _borrowSpread[p] = borrowSpread;
      }

      Parallel.For(0, _standaloneMeasureAccumulators.Count,
      () =>
      {
        var posExposures = new double[PathCount, DateCount];
        var negExposures = new double[PathCount, DateCount];
        return new Tuple<double[,],double[,]>(posExposures, negExposures);
      },  
      (i, threadData) =>
      {
        var measureAccumulator = _standaloneMeasureAccumulators[i];
        Logger.DebugFormat("Getting exposures for trade {0}", NettingSets[i]);
        var tradeExposures = PrecalculatedPvs.GetExposures(i, ExposureDates);
        Logger.DebugFormat("Accumulating Additive Measures");
        for (int p = 0; p < PathCount; p++)
        {
          RunSimulationPath(p, tradeExposures, measureAccumulator, threadData.Item1, threadData.Item2);
        }
        Logger.DebugFormat("Reducing Accumulated Values");
        measureAccumulator.ReduceCumulativeValues();
      },
      (threadData) =>
      {
        Logger.DebugFormat("Merging Total Exposures");
      
        for (int p = 0; p < PathCount; p++)
        {
          for (int d = 0; d < DateCount; d++)
          {
            _totalPositiveExposures[p, d] += threadData.Item1[p,d];
            _totalNegativeExposures[p, d] += threadData.Item2[p, d]; 
          }
        }
      }
      );

      if (_totalMeasureAccumulator != null)
      {
        Logger.DebugFormat("Accumulating Non Additive Measures");
        for (int p = 0; p < PathCount; p++)
        {
          RunSimulationPath(p, _totalMeasureAccumulator, _totalPositiveExposures, _totalNegativeExposures);
        }
        Logger.DebugFormat("Reducing Accumulated Values");
        _totalMeasureAccumulator.ReduceCumulativeValues();  
      }
      
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="ci"></param>
    public override void AddMeasureAccumulator(CCRMeasure measure, double ci)
    {
      measure = ConvertMeasure(measure);
      if (NonAdditiveMeasures.Contains(measure))
      {
        if(_totalMeasureAccumulator == null)
        {
          _totalMeasureAccumulator = new CCRMeasureAccumulator(AsOf, ExposureDates,
                                                                IntegrationKernels.ToArray(),
                                                                PrecalculatedMarketData.Recoveries,
                                                                CptyCurve, PathCount);
        }
        _totalMeasureAccumulator.AddMeasureAccumulator(measure, ci);

        if (NegativeNonAdditiveMeasures.Contains(measure))
        {
          // we use standalone NEE weights to allocate NegativeNonAdditiveMeasures
          // so make sure NEE is added
          AddMeasureAccumulator(CCRMeasure.NEE, ci);
        }
        else 
        {
          // we use standalone EE weights to allocate positive NonAdditiveMeasures
          // so make sure EE is added
          AddMeasureAccumulator(CCRMeasure.EE, ci);
        }

        return;
      }

      if (_standaloneMeasureAccumulators == null)
      {
        _standaloneMeasureAccumulators = new List<CCRMeasureAccumulator>();
        foreach (var trade in NettingSets)
        {
          var m = new CCRMeasureAccumulator(AsOf, ExposureDates,
          IntegrationKernels.ToArray(),
          PrecalculatedMarketData.Recoveries,
          CptyCurve, PathCount);
          _standaloneMeasureAccumulators.Add(m);
        }
      }

      foreach (var m in _standaloneMeasureAccumulators)
      {
        m.AddMeasureAccumulator(measure, ci);
      }
    }


    /// <summary>
    /// Get CCRMeasure for portfolio
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="t"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    public override double GetMeasure(CCRMeasure measure, int t, double ci)
    {
      return GetMeasure(measure, ExposureDates[t], ci);
    }

    /// <summary>
    /// Get CCRMeasure for portfolio
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="dt"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    public override double GetMeasure(CCRMeasure measure, Dt dt, double ci)
    {
      if (!_reduced)
      {
        Reduce();
      }
      measure = ConvertMeasure(measure);
      if (NonAdditiveMeasures.Contains(measure))
      {
        if (_totalMeasureAccumulator == null || !_totalMeasureAccumulator.HasMeasureAccumulator(measure, ci))
          return 0;
        return _totalMeasureAccumulator.GetMeasure(measure, dt, ci);
      }

      if (!_standaloneMeasureAccumulators.Any(m => m.HasMeasureAccumulator(measure, ci)))
        return 0;
      var total = _standaloneMeasureAccumulators.Sum(m => m.GetMeasure(measure, dt, ci));
      return total;
    }

    /// <summary>
    /// Get marginal allocations of CCRMeasure for all trades
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="dt"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    public override double[] GetMeasureMarginal(CCRMeasure measure, Dt dt, double ci)
    {
      if (!ExposureDates.Contains(dt))
        throw new ArgumentException(@"dt not found in ExposureDates", nameof(dt));
      return GetMeasureMarginal(measure, Array.IndexOf(ExposureDates, dt), ci);
    }

    /// <summary>
    /// Get marginal allocations of CCRMeasure for all trades
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="t"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    public override double[] GetMeasureMarginal(CCRMeasure measure, int t, double ci)
    {
      if (!_reduced)
        Reduce();
      measure = ConvertMeasure(measure);
      if (NonAdditiveMeasures.Contains(measure))
      {
        if (_totalMeasureAccumulator == null || !_totalMeasureAccumulator.HasMeasureAccumulator(measure, ci))
          return new double[NettingSets.Count];
        double[] weights;
        if (NegativeNonAdditiveMeasures.Contains(measure))
        {
          var nee = _standaloneMeasureAccumulators.Select(m => m.GetMeasure(ConvertMeasure(CCRMeasure.NEE), ExposureDates[t], ci)).ToArray();
          var norm = nee.Sum();
          weights = nee.Select(e => e / norm).ToArray(); 
        }
        else
        {
          var ee = _standaloneMeasureAccumulators.Select(m => m.GetMeasure(ConvertMeasure(CCRMeasure.EE), ExposureDates[t], ci)).ToArray();
          var norm = ee.Sum();
          weights = ee.Select(e => e / norm).ToArray(); 
        }

        var total = _totalMeasureAccumulator.GetMeasure(measure, ExposureDates[t], ci);
        return weights.Select(w => w * total).ToArray(); 
      }
      
      if (!_standaloneMeasureAccumulators.Any(m => m.HasMeasureAccumulator(measure, ci)))
        return new double[NettingSets.Count];
      return _standaloneMeasureAccumulators.Select(m => m.GetMeasure(measure, ExposureDates[t], ci)).ToArray();
    }



    /// <summary>
    /// 
    /// </summary>
    /// <param name="kernels"></param>
    public override CCRExposureAggregator ChangeIntegrationKernels(IList<Tuple<Dt[], double[]>> kernels)
    {
      var newKernels = TransformIntegrationKernels(kernels, AsOf, ExposureDates, IsUnilateral);
      var newAgg = new NoNetExposureAggregator(this, newKernels);
      return newAgg;
    }


    private void RunSimulationPath(int pathIdx, double[,] tradeExposures, CCRMeasureAccumulator accumulator, double[,] totalPositiveExposures, double[,] totalNegativeExposures)
    {
      var resultData = new SimulatedPathValues(DateCount, 1);
      for (int t = 0; t < DateCount; ++t)
      {
        var pv = tradeExposures[pathIdx, t]; 
        resultData.Add(new []{pv}, _df[pathIdx][t], _numeraire[pathIdx][t], _cptyRn[pathIdx][t], _ownRn[pathIdx][t], _survivalRn[pathIdx][t], _cptySpread[pathIdx][t], _ownSpread[pathIdx][t], _lendSpread[pathIdx][t], _borrowSpread[pathIdx][t], pathIdx, 1.0);
        var pe = CounterpartyExposure.ExposureFn(pv);
        var ne = BookingEntityExposure.ExposureFn(pv);
        accumulator.AccumulateExposures(resultData, t, pe, 0, ne, 0);
        totalPositiveExposures[pathIdx,t] += pe;
        totalNegativeExposures[pathIdx,t] += ne;
      }
    }

    private void RunSimulationPath(int pathIdx, CCRMeasureAccumulator accumulator, double[,] totalPositiveExposures, double[,] totalNegativeExposures)
    {
      var resultData = new SimulatedPathValues(DateCount, 1);
      for (int t = 0; t < DateCount; ++t)
      {
        var pv = totalPositiveExposures[pathIdx, t];
        resultData.Add(new[] { pv }, _df[pathIdx][t], _numeraire[pathIdx][t], _cptyRn[pathIdx][t], _ownRn[pathIdx][t], _survivalRn[pathIdx][t], _cptySpread[pathIdx][t], _ownSpread[pathIdx][t], _lendSpread[pathIdx][t], _borrowSpread[pathIdx][t], pathIdx, 1.0);
        var pe = totalPositiveExposures[pathIdx, t];
        var ne = totalNegativeExposures[pathIdx, t];
        accumulator.AccumulateExposures(resultData, t, pe, 0, ne, 0);
      }
    }
    
    
    /// <summary>
    /// Run simulations and aggregate measures
    /// </summary>
    public override void Reduce()
    {
      if (_reduced) return;
      _cptyRn = new IList<double>[PathCount];
      _ownRn = new IList<double>[PathCount];
      _df = new IList<double>[PathCount];
      _numeraire = new IList<double>[PathCount];
      _borrowSpread = new IList<double>[PathCount];
      _lendSpread = new IList<double>[PathCount];
      _ownSpread = new IList<double>[PathCount];
      _survivalRn = new IList<double>[PathCount];
      _cptySpread = new IList<double>[PathCount];
      _totalPositiveExposures = new double[PathCount, DateCount];
      _totalNegativeExposures = new double[PathCount, DateCount];

      Logger.DebugFormat("Running Simulation Paths");
      RunSimulationPaths();
      _reduced = true;
    }

    /// <summary>
    /// Diagnostics for the CCRExposure Aggregator
    /// </summary>
    public override DataTable DiagnosticsTable
    {
      get
      {
        if (DiagnosticsSupported && _diagnosticTable == null)
        {
          _diagnosticTable = new DataTable();
          _diagnosticTable.Columns.Add("PathId", typeof(int));
          _diagnosticTable.Columns.Add("Key", typeof(string));
          foreach (var date in ExposureDates)
          {
            _diagnosticTable.Columns.Add(date.ToString(), typeof(double));
          }
          for (var pathId = 0; pathId < PathCount; ++pathId)
          {
            for (var i = 0; i < 4; ++i)
            {
              var key = GetKey(i);
              var row = _diagnosticTable.NewRow();
              row["PathId"] = pathId;
              row["Key"] = key;
              for (var dateIndex = 0; dateIndex < DateCount; ++dateIndex)
              {
                switch (key)
                {
                  case "PE":
                    row[dateIndex + 2] = _totalPositiveExposures[pathId, dateIndex];
                    break;
                  case "NE":
                    row[dateIndex + 2] = _totalNegativeExposures[pathId, dateIndex];
                    break;
                  default:
                    row[dateIndex + 2] = 0.0;
                    break;
                }
              }
              _diagnosticTable.Rows.Add(row);
            }
          }
        }
        return _diagnosticTable;
      }
    }

    #endregion
  }
}