using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using log4net;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  /// 
  /// </summary>
  public class CollateralizedExposureAggregator : CCRExposureAggregatorManaged
  {
    #region Data

    private readonly double[][][] _allocatedEeWeights;
    private readonly double[][][] _allocatedNeeWeights;
    private readonly double[][][] _allocatedEpvWeights;
    private readonly double[] _bookingEntityWeightTotal;
    private readonly double[] _counterpartyWeightTotal;
    private readonly double[] _epvWeightTotal;
    private readonly int[][] _reversePortfolioMap;
    private CCRMeasureAccumulator _calculator;
    private bool _reduced;
    private Netting _netting;
    private readonly bool _allocateExposures;
    private double[][][] _nettingSetExposures;
    private double[][][] _nettingSetExposuresOnCollateralDate;
    private double[][][] _nettingSetRcvCollateral;
    private double[][][] _nettingSetPayCollateral;
    IList<double>[] _df, _numeraire, _cptyRn, _ownRn, _survivalRn, _cptySpread, _ownSpread, _lendSpread, _borrowSpread;
    private DataTable _diagnosticTable;
    private readonly bool _binaryLoggingEnabled;
    private readonly ConcurrentDictionary<int, double[,]> _diagnosticResults;

    private static readonly ILog Logger = LogManager.GetLogger(typeof(CollateralizedExposureAggregator));

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="netting">Netting data</param>
    /// <param name="exposureDts">list of dates exposures are calculated</param>
    /// <param name="unilateral">treat default unilaterally or jointly (first-to-default) </param>
    /// <param name="asOf"></param>
    /// <param name="marketData"></param>
    /// <param name="exposures"></param>
    /// <param name="discountExposures">discount exposures to present value when reporting expectations</param>
    /// <param name="wrongWayRisk">adjust for correlation between default prob and exposure</param>
    /// <param name="fundingCostNoDefault">report FCA without discounting for default risk</param>
    /// <param name="fundingBenefitNoDefault">report FBA without discounting for default risk</param>
    /// <param name="allocateExposures">allocate marginal exposures for each trade</param>
    /// <param name="binaryLoggingEnabled">enable diagnostic logging</param>
    /// <param name="modelOvercollateralization"></param>
    /// <param name="cptyCurve">The Counterparty Curve required to compute RWA</param>
    public CollateralizedExposureAggregator(
      Dt asOf,
      PrecalculatedMarketData marketData,
      PrecalculatedExposures exposures,
      Netting netting,
      Dt[] exposureDts,
      bool unilateral = false,
      bool discountExposures = false,
      bool wrongWayRisk = true,
      bool fundingCostNoDefault = false,
      bool fundingBenefitNoDefault = false,
      bool allocateExposures = true,
      bool binaryLoggingEnabled = false, 
      bool modelOvercollateralization = false,
      SurvivalCurve cptyCurve = null
      )
    {
      AsOf = asOf;
      NettingSets = exposures.NettingGroups;
      _netting = netting; 
      ExposureDates = exposureDts;
      IsUnilateral = unilateral;
      DiscountExposures = discountExposures;
      WrongWayRisk = wrongWayRisk;
      FundingCostNoDefault = fundingCostNoDefault;
      FundingBenefitNoDefault = fundingBenefitNoDefault;
      _allocateExposures = allocateExposures;
      _binaryLoggingEnabled = binaryLoggingEnabled;
      PrecalculatedMarketData = marketData;
      CptyCurve = cptyCurve;
      IntegrationKernels = TransformIntegrationKernels(marketData.IntegrationKernels, AsOf, ExposureDates, IsUnilateral);

      NettingMap = new Dictionary<string, int>();
      for (int i = 0; i < netting.NettingGroups.Length; i++)
      {
        NettingMap[netting.NettingGroups[i]] = i;
      }


      CounterpartyExposure = new PathWiseExposure(ExposureDates, NettingMap, netting,
        PathWiseExposure.RiskyParty.Counterparty, modelOvercollateralization);
      BookingEntityExposure = new PathWiseExposure(ExposureDates, NettingMap, netting,
        PathWiseExposure.RiskyParty.BookingEntity, modelOvercollateralization);
      // count trades in each netting group
      var tradeCounts = new int[NettingMap.Count];
      foreach (var nettingId in NettingSets)
        tradeCounts[NettingMap[nettingId]]++;

      // initialize jagged arrays
      if (_allocateExposures)
      {
        _reversePortfolioMap = new int[NettingMap.Count][];
        _allocatedEeWeights = new double[NettingMap.Count][][];
        _allocatedNeeWeights = new double[NettingMap.Count][][];
        _allocatedEpvWeights = new double[NettingMap.Count][][];
        for (int nettingIdx = 0; nettingIdx < NettingMap.Count; ++nettingIdx)
        {
          var tradeCount = tradeCounts[nettingIdx];
          _reversePortfolioMap[nettingIdx] = new int[tradeCount];
          _allocatedEeWeights[nettingIdx] = new double[tradeCount][];
          _allocatedNeeWeights[nettingIdx] = new double[tradeCount][];
          _allocatedEpvWeights[nettingIdx] = new double[tradeCount][];

          for (int tradeIdx = 0; tradeIdx < tradeCount; tradeIdx++)
          {
            _allocatedEeWeights[nettingIdx][tradeIdx] = new double[DateCount];
            _allocatedNeeWeights[nettingIdx][tradeIdx] = new double[DateCount];
            _allocatedEpvWeights[nettingIdx][tradeIdx] = new double[DateCount];
          }
          tradeCounts[nettingIdx] = 0;
        }
        int t = 0;
        for (; t < exposures.Count; ++t)
        {
          var nettingId = NettingMap[NettingSets[t]];
          _reversePortfolioMap[nettingId][tradeCounts[nettingId]++] = t;
        }

        _counterpartyWeightTotal = new double[DateCount];
        _bookingEntityWeightTotal = new double[DateCount];
        _epvWeightTotal = new double[DateCount];
      }


      PrecalculatedPvs = exposures;

      if (_binaryLoggingEnabled)
      {
        _diagnosticResults = new ConcurrentDictionary<int, double[,]>();
      }
    }


    private CollateralizedExposureAggregator(CollateralizedExposureAggregator other, Tuple<Dt[], double[]>[] kernels)
    {
      _calculator = other._calculator.CloneObjectGraph(CloneMethod.FastClone);
      _calculator.IntegrationKernels = kernels;
      IntegrationKernels = kernels;
      _allocatedEeWeights = other._allocatedEeWeights;
      _allocatedNeeWeights = other._allocatedNeeWeights;
      _allocatedEpvWeights = other._allocatedEpvWeights;
      _bookingEntityWeightTotal = other._bookingEntityWeightTotal;
      _counterpartyWeightTotal = other._counterpartyWeightTotal;
      _epvWeightTotal = other._epvWeightTotal;
      _reversePortfolioMap = other._reversePortfolioMap;
      _reduced = other._reduced;
      IsUnilateral = other.IsUnilateral;
      DiscountExposures = other.DiscountExposures;
      WrongWayRisk = other.WrongWayRisk;
      FundingCostNoDefault = other.FundingCostNoDefault;
      FundingBenefitNoDefault = other.FundingBenefitNoDefault;
      _allocateExposures = other._allocateExposures;
      _df = other._df;
      _numeraire = other._numeraire;
      _cptyRn = other._cptyRn;
      _ownRn = other._ownRn;
      _survivalRn = other._survivalRn;
      _cptySpread = other._cptySpread;
      _ownSpread = other._ownSpread;
      _lendSpread = other._lendSpread;
      _borrowSpread = other._borrowSpread;
      _diagnosticTable = other._diagnosticTable;
      _binaryLoggingEnabled = other._binaryLoggingEnabled;
      _diagnosticResults = other._diagnosticResults;
      AsOf = other.AsOf;
      CounterpartyExposure = other.CounterpartyExposure;
      BookingEntityExposure = other.BookingEntityExposure;
      PrecalculatedPvs = other.PrecalculatedPvs;
      NettingSets = other.NettingSets;
      ExposureDates = other.ExposureDates;
      NettingMap = other.NettingMap;
      PrecalculatedMarketData = other.PrecalculatedMarketData;
    }
    #endregion

    #region Properties

    /// <summary>
    /// Diagnostics for the CCRExposure Aggregator
    /// </summary>
    public override DataTable DiagnosticsTable
    {
      get
      {
        if (_diagnosticTable == null)
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
            double[,] pathDiagnostics;
            _diagnosticResults.TryGetValue(pathId, out pathDiagnostics);
            if (pathDiagnostics == null)
            {
              continue;
            }
            for (var i = 0; i < pathDiagnostics.GetLength(0); ++i)
            {
              var row = _diagnosticTable.NewRow();
              row["PathId"] = pathId;
              row["Key"] = GetKey(i);
              for (var j = 0; j < pathDiagnostics.GetLength(1); ++j)
              {
                row[j + 2] = pathDiagnostics[i, j];
              }
              _diagnosticTable.Rows.Add(row);
            }
          }
        }
        return _diagnosticTable;
      }
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
      for (var i = 0; i < PathCount; ++i)
      {
        IList<double> df, numeraire, cptyRn, ownRn, survivalRn, cptySpread, ownSpread, lendSpread, borrowSpread;
        PrecalculatedMarketData.GetMarketDataForPath(i, WrongWayRisk, IsUnilateral, out numeraire, out df, out cptyRn, out ownRn, out survivalRn,
          out cptySpread, out ownSpread, out lendSpread, out borrowSpread);
        _df[i] = df;
        _numeraire[i] = numeraire;
        _cptyRn[i] = cptyRn;
        _ownRn[i] = ownRn;
        _survivalRn[i] = survivalRn;
        _cptySpread[i] = cptySpread;
        _ownSpread[i] = ownSpread;
        _lendSpread[i] = lendSpread;
        _borrowSpread[i] = borrowSpread;
      }

      Logger.DebugFormat("Accumulating Exposures");
  
      Parallel.For(0, PathCount,
        () =>
        {
          var clonedCalc = _calculator.CloneObjectGraph(CloneMethod.FastClone);
          return clonedCalc;
        },
        (i, threadData) =>
        {
          double[,] pathDiagnostics;
          RunSimulationPath(i, threadData, out pathDiagnostics);
          if (_binaryLoggingEnabled)
          {
            _diagnosticResults.TryAdd(i, pathDiagnostics);
          }
        },
        (threadData) =>
        {
          _calculator.MergeCumulativeValues(threadData);
        }
      );
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="ci"></param>
    public override void AddMeasureAccumulator(CCRMeasure measure, double ci)
    {
      if (_calculator == null)
      {
        _calculator = new CCRMeasureAccumulator(AsOf, ExposureDates,
          IntegrationKernels.ToArray(),
          PrecalculatedMarketData.Recoveries,
          CptyCurve, PathCount);
      }
      measure = ConvertMeasure(measure);
      _calculator.AddMeasureAccumulator(measure, ci);
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
      if (_calculator.HasMeasureAccumulator(measure, ci))
        return _calculator.GetMeasure(measure, dt, ci);
      return 0.0;
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
      if(!ExposureDates.Contains(dt))
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

      if (!_allocateExposures)
        throw new ToolkitException("Cannot call GetMeasureAllocatedByTrade when allocateExposures set to false in constructor.");

      var allocatedValues = new double[PrecalculatedPvs.Count];
      int nettingCount = NettingMap.Count;
      double[][][] allocatedWeightsByNettingSet;
      bool isIntegral = false;
      bool useCVAWeights = false;
      measure = ConvertMeasure(measure);
      switch (measure)
      {
        case CCRMeasure.DVA:
        case CCRMeasure.DVATheta:
        case CCRMeasure.DVA0:
        case CCRMeasure.FBA:
        case CCRMeasure.FBATheta:
        case CCRMeasure.FBA0:
        case CCRMeasure.FBANoDefault:
          isIntegral = true;
          allocatedWeightsByNettingSet = _allocatedNeeWeights;
          break;
        case CCRMeasure.NEE:
        case CCRMeasure.NEE0:
        case CCRMeasure.ENE:
        case CCRMeasure.DiscountedNEE:
        case CCRMeasure.DiscountedNEE0:
        case CCRMeasure.PFNE:
        case CCRMeasure.MPFNE:
        case CCRMeasure.PFNCSA:
        case CCRMeasure.DiscountedPFNE:
        case CCRMeasure.StdErrNEE:
        case CCRMeasure.StdErrDiscountedNEE:
          allocatedWeightsByNettingSet = _allocatedNeeWeights;
          break;
        case CCRMeasure.CVA:
        case CCRMeasure.CVATheta:
        case CCRMeasure.CVA0:
        case CCRMeasure.EC:
        case CCRMeasure.EC0:
        case CCRMeasure.FVA:
        case CCRMeasure.FVA0:
        case CCRMeasure.FCA:
        case CCRMeasure.FCATheta:
        case CCRMeasure.FCA0:
        case CCRMeasure.FCANoDefault:
          isIntegral = true;
          allocatedWeightsByNettingSet = _allocatedEeWeights;
          break;
        case CCRMeasure.RWA:
        case CCRMeasure.RWA0:
          isIntegral = true;
          allocatedWeightsByNettingSet = _allocatedEeWeights;
          useCVAWeights = true;
          break;
        case CCRMeasure.EPV:
        case CCRMeasure.DiscountedEPV:
          allocatedWeightsByNettingSet = _allocatedEpvWeights;
          break;
        default:
          allocatedWeightsByNettingSet = _allocatedEeWeights;
          break;
      }

      if (!_calculator.HasMeasureAccumulator(measure, ci))
        return allocatedValues;
      var incrementalCalc = _calculator;
      if (isIntegral)
      {
        for (int n = 0; n < nettingCount; n++)
        {
          int tradeCount = _reversePortfolioMap[n].Length;
          for (int j = 0; j < tradeCount; j++)
          {
            var tradeWeights = allocatedWeightsByNettingSet[n][j];
            allocatedValues[_reversePortfolioMap[n][j]] =
              incrementalCalc.GetMeasure(useCVAWeights ? ConvertMeasure(CCRMeasure.CVA) : measure, ExposureDates[t], ci, tradeWeights);
          }
        }
        if (useCVAWeights)
        {
          var totalCVA = allocatedValues.Sum();
          var rwa = incrementalCalc.GetMeasure(measure, Dt.Empty, 0.0);
          for (int i = 0; i < allocatedValues.Length; i++)
          {
            if(allocatedValues[i] != 0.0 && totalCVA != 0.0)
              allocatedValues[i] = (allocatedValues[i] / totalCVA) * rwa;
          }
        }
      }
      else
      {
        double total = incrementalCalc.GetMeasure(measure, ExposureDates[t], ci);
        for (int n = 0; n < nettingCount; n++)
        {
          int tradeCount = _reversePortfolioMap[n].Length;
          for (int j = 0; j < tradeCount; j++)
          {
            double weight = allocatedWeightsByNettingSet[n][j][t];
            allocatedValues[_reversePortfolioMap[n][j]] = total * weight;
          }
        }
      }
      return allocatedValues;
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="kernels"></param>
    public override CCRExposureAggregator ChangeIntegrationKernels(IList<Tuple<Dt[], double[]>> kernels)
    {
      var newKernels = TransformIntegrationKernels(kernels, AsOf, ExposureDates, IsUnilateral);
      var newAgg = new CollateralizedExposureAggregator(this, newKernels);
      return newAgg;
    }

    
    private void RunSimulationPath(int idx, CCRMeasureAccumulator accumulator, out double[,] pathDiagnostics)
    {
      pathDiagnostics = null;
      if (_binaryLoggingEnabled)
      {
        pathDiagnostics = new double[4, ExposureDates.Length];
      }
      int nettingCount = NettingMap.Count;
      var resultData = new SimulatedPathValues(DateCount, nettingCount);
     
      _nettingSetRcvCollateral[idx] = new double[DateCount][];
      _nettingSetPayCollateral[idx] = new double[DateCount][];

      for (int t = 0; t < DateCount; ++t)
      {
        var nettingSetExposures = _nettingSetExposures[idx][t];
        var nettingSetExposuresOnCollateralDate = _nettingSetExposuresOnCollateralDate[idx][t];
        resultData.Add(nettingSetExposuresOnCollateralDate, _df[idx][t], _numeraire[idx][t], _cptyRn[idx][t], _ownRn[idx][t], _survivalRn[idx][t], _cptySpread[idx][t], _ownSpread[idx][t], _lendSpread[idx][t], _borrowSpread[idx][t], idx, 1.0);
        var eeColl = CounterpartyExposure.ComputeCollateral(resultData, nettingSetExposures, nettingSetExposuresOnCollateralDate, t);
        var neeColl = BookingEntityExposure.ComputeCollateral(resultData, nettingSetExposures, nettingSetExposuresOnCollateralDate, t);
        var eePoint = CounterpartyExposure.CapAndSumExposures(nettingSetExposures, eeColl);
        var neePoint = BookingEntityExposure.CapAndSumExposures(nettingSetExposures, neeColl);
        eePoint.DateIdx = neePoint.DateIdx = t;
        eePoint.Path = neePoint.Path = resultData;
        eePoint.PathIdx = neePoint.PathIdx = idx;
        accumulator.AccumulateExposures(eePoint, neePoint);
        _nettingSetRcvCollateral[idx][t] = eeColl.Select(c => c.IndependentAmount + c.VariationMargin).ToArray();
        _nettingSetPayCollateral[idx][t] = neeColl.Select(c => c.IndependentAmount + c.VariationMargin).ToArray();

        if (_binaryLoggingEnabled)
        {
          // Positive Exposure
          pathDiagnostics[0, t] = eePoint.Exposure;
          // Negative Exposure
          pathDiagnostics[1, t] = neePoint.Exposure;
          // Received Collateral
          pathDiagnostics[2, t] = eePoint.Collateral;
          // Posted Collateral
          pathDiagnostics[3, t] = neePoint.Collateral;
        }
      }
    }
    
    private void AllocateExposures()
    {
      var cptyCollateralizedExposures = new double[PathCount][][];
      var bookingCollateralizedExposures = new double[PathCount][][];
      for (int p = 0; p < PathCount; p++)
      {
        cptyCollateralizedExposures[p] = new double[DateCount][];
        bookingCollateralizedExposures[p] = new double[DateCount][];
        Parallel.For(0, DateCount, (d) =>
        {
          double cptyWeight = _cptyRn[p][d];
          double ownWeight = _ownRn[p][d];
          double epvWeight = 1.0;
          _counterpartyWeightTotal[d] += cptyWeight;
          _bookingEntityWeightTotal[d] += ownWeight;
          _epvWeightTotal[d] += epvWeight;
          // collateralized exposures at exposure date, includes super group netting
          cptyCollateralizedExposures[p][d] = CounterpartyExposure.CollateralizedTotals(_nettingSetExposures[p][d], _nettingSetRcvCollateral[p][d]);
          bookingCollateralizedExposures[p][d] = BookingEntityExposure.CollateralizedTotals(_nettingSetExposures[p][d], _nettingSetPayCollateral[p][d]);
        });
      }

      int nettingCount = NettingMap.Count;
      Parallel.For(0, nettingCount, (n) =>
      {
        // reorder arrays for better access
        var nettingSetCollateralizedPE = new double[PathCount][];
        var nettingSetCollateralizedNE = new double[PathCount][];
        var nettingSetValues = new double[PathCount][];
        var nettingSetValuesChange = new double[PathCount][];
        Parallel.For(0, PathCount, (p) =>
        {
          nettingSetCollateralizedPE[p] = new double[DateCount];// at exposure date
          nettingSetCollateralizedNE[p] = new double[DateCount];// at exposure date
          nettingSetValues[p] = new double[DateCount];// at exposure date
          nettingSetValuesChange[p] = new double[DateCount];// change between collateral call and exposure date
          for (int d = 0; d < DateCount; ++d)
          {
            nettingSetCollateralizedPE[p][d] = cptyCollateralizedExposures[p][d][n];
            nettingSetCollateralizedNE[p][d] = bookingCollateralizedExposures[p][d][n];
            nettingSetValues[p][d] = _nettingSetExposuresOnCollateralDate[p][d][n];
            nettingSetValuesChange[p][d] = _nettingSetExposures[p][d][n] - _nettingSetExposuresOnCollateralDate[p][d][n];
          }
        });


        int tradeCount = _reversePortfolioMap[n].Length;
        Parallel.For(0, tradeCount, (t) =>
        {
          var tradeIdx = _reversePortfolioMap[n][t];
          var valuesAtExposureDate = PrecalculatedPvs.GetExposures(tradeIdx, ExposureDates);
          var valuesAtCollateralDate = valuesAtExposureDate;
          var mpor = _netting.CollateralMaps != null ? _netting.CollateralMaps[n].MarginPeriodOfRisk : Tenor.Empty; 
          if(!mpor.IsEmpty && mpor.N != 0)
            valuesAtCollateralDate = PrecalculatedPvs.GetExposures(tradeIdx, ExposureDates, mpor);

          for (int p = 0; p < PathCount; p++)
          {
            for (int d = 0; d < DateCount; ++d)
            {
              double cptyWeight = _cptyRn[p][d];
              double ownWeight = _ownRn[p][d];
              double df = _df[p][d];
              var v_i = valuesAtCollateralDate[p, d];
              var deltaVi = valuesAtExposureDate[p, d] - v_i;
              var collateralizedPE = nettingSetCollateralizedPE[p][d];
              var collateralizedNE = nettingSetCollateralizedNE[p][d];
              var v = nettingSetValues[p][d];
              var dV = nettingSetValuesChange[p][d];
              var marginalV = v.AlmostEquals(0.0) ? 0.0 : v_i / v;
              if (collateralizedPE > 0) // collateralizedPE(t)-dV gives H = V(t-dt)-C(t-dt)
                _allocatedEeWeights[n][t][d] += (marginalV * (collateralizedPE-dV) + deltaVi) * df * cptyWeight;
              if (collateralizedNE < 0)
                _allocatedNeeWeights[n][t][d] += (marginalV * (collateralizedNE-dV) + deltaVi) * df * ownWeight;
              _allocatedEpvWeights[n][t][d] += (v_i+deltaVi) * df;
            }
          }
        });
      });

      var totalEE = new double[DateCount];
      var totalNEE = new double[DateCount];
      var totalEPV = new double[DateCount];
      // reduce each weighted sum to expectation
      for (int n = 0; n < nettingCount; n++)
      {
        int tradeCount = _reversePortfolioMap[n].Length;
        for (int t = 0; t < tradeCount; t++)
        {
          for (int d = 0; d < DateCount; ++d)
          {
            _allocatedEeWeights[n][t][d] /= _counterpartyWeightTotal[d];
            _allocatedNeeWeights[n][t][d] /= _bookingEntityWeightTotal[d];
            _allocatedEpvWeights[n][t][d] /= _epvWeightTotal[d];
            totalEE[d] += _allocatedEeWeights[n][t][d];
            totalNEE[d] += _allocatedNeeWeights[n][t][d];
            totalEPV[d] += _allocatedEpvWeights[n][t][d];
          }
        }
      }

      // now change expectation for each trade to proportion of total expected exposure
      for (int n = 0; n < nettingCount; n++)
      {
        int tradeCount = _reversePortfolioMap[n].Length;
        for (int t = 0; t < tradeCount; t++)
        {
          for (int d = 0; d < DateCount; ++d)
          {

            if (!totalEE[d].AlmostEquals(0.0))
              _allocatedEeWeights[n][t][d] /= totalEE[d];
            if (!totalNEE[d].AlmostEquals(0.0))
              _allocatedNeeWeights[n][t][d] /= totalNEE[d];
            if (!totalEPV[d].AlmostEquals(0.0))
              _allocatedEpvWeights[n][t][d] /= totalEPV[d];
          }
        }
      }
    }

    /// <summary>
    /// Run simulations and aggregate measures
    /// </summary>
    public override void Reduce()
    {
      if (_reduced)
        return;
      Logger.Debug("Netting Exposures");
      _nettingSetExposures = PrecalculatedPvs.GetNetExposures(NettingMap, ExposureDates);
      _nettingSetExposuresOnCollateralDate = PrecalculatedPvs.GetNetExposures(NettingMap, ExposureDates, _netting);
      _nettingSetRcvCollateral = new double[PathCount][][];
      _nettingSetPayCollateral = new double[PathCount][][];
      _cptyRn = new IList<double>[PathCount];
      _ownRn = new IList<double>[PathCount];
      _df = new IList<double>[PathCount];
      _numeraire = new IList<double>[PathCount];
      _borrowSpread = new IList<double>[PathCount];
      _lendSpread = new IList<double>[PathCount];
      _ownSpread = new IList<double>[PathCount];
      _survivalRn = new IList<double>[PathCount];
      _cptySpread = new IList<double>[PathCount];
      Logger.DebugFormat("Running Simulation Paths");
      RunSimulationPaths();

      if (_allocateExposures)
      {
        Logger.DebugFormat("Allocating Exposures");
        AllocateExposures();
      }
      
      Logger.DebugFormat("Reducing Accumulated Values");
      _calculator.ReduceCumulativeValues();
      _reduced = true;
    }

    #endregion

  }
}