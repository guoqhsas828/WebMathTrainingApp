using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Ccr
{
  /// <summary>
  /// 
  /// </summary>
  public class IncrementalCCRExposureAggregator : IncrementalCCRExposureAggregatorManaged, ICCRMeasureSource
  {
    #region Data

    private CCRMeasureAccumulator _preIncrementalAcculator;
    private CCRMeasureAccumulator _postIncrementalAcculator;
    private IList<Tuple<Dt[], double[]>> _integrationKernels;
    private bool _reduced;
    
    private readonly bool _isUnilateral;
    private readonly bool _discountExposures;
    private readonly bool _wrongWayRisk;
    private readonly bool _fundingCostNoDefault;
    private readonly bool _fundingBenefitNoDefault;
    private readonly DataTable _diagnosticTable;
    private readonly double[,] _partialDiagnosticResults;
    private readonly bool _binaryLoggingEnabled;
    private Dt _maxExposureDt;
    private double[][][] _netPriorExposures;
    private double[][][] _netPostExposures; 
	  #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="incrementalExposures"></param>
    /// <param name="netting">Netting data</param>
    /// <param name="exposureDts">list of dates exposures are calculated</param>
    /// <param name="unilateral">treat default unilaterally or jointly (first-to-default) </param>
    /// <param name="asOf"></param>
    /// <param name="marketData"></param>
    /// <param name="preIncrementalExposures"></param>
    /// <param name="discountExposures">discount exposures to present value when reporting expectations</param>
    /// <param name="wrongWayRisk">adjust for correlation between default prob and exposure</param>
    /// <param name="fundingCostNoDefault">report FCA without discounting for default risk</param>
    /// <param name="fundingBenefitNoDefault">report FBA without discounting for default risk</param>
    /// <param name="binaryLoggingEnabled">enable diagnostic logging</param>
    /// <param name="modelOvercollateralization">is excess collateral at risk under default</param>
    public IncrementalCCRExposureAggregator(
			Dt asOf, 
			PrecalculatedMarketData marketData,
      PrecalculatedExposures preIncrementalExposures,
      PrecalculatedExposures incrementalExposures,
      Netting netting,
      Dt[] exposureDts,
      bool unilateral = false, 
      bool discountExposures = false, 
      bool wrongWayRisk = true, 
      bool fundingCostNoDefault = false, 
      bool fundingBenefitNoDefault = false, 
      bool binaryLoggingEnabled = false, 
      bool modelOvercollateralization = false
      )
	  {
		  AsOf = asOf;
      PreIncrementalNettingSets = preIncrementalExposures.NettingGroups;
      IncrementalNettingSets = incrementalExposures.NettingGroups; 
      ExposureDates = exposureDts;
      _isUnilateral = unilateral;
      _discountExposures = discountExposures;
      _wrongWayRisk = wrongWayRisk;
      _fundingCostNoDefault = fundingCostNoDefault;
      _fundingBenefitNoDefault = fundingBenefitNoDefault;
      _binaryLoggingEnabled = binaryLoggingEnabled;
		  PrecalculatedMarketData = marketData; 

      
			NettingMap = new Dictionary<string, int>();
      for (int i = 0; i < netting.NettingGroups.Length; i++)
      {
        NettingMap[netting.NettingGroups[i]] = i;
      }


      CounterpartyExposure = new PathWiseExposure(ExposureDates, NettingMap, netting,
        PathWiseExposure.RiskyParty.Counterparty, modelOvercollateralization);
      BookingEntityExposure = new PathWiseExposure(ExposureDates, NettingMap, netting,
        PathWiseExposure.RiskyParty.BookingEntity, modelOvercollateralization);
      
      PreIncrementalExposures = preIncrementalExposures;
      IncrementalExposures = incrementalExposures;
      
      if (_binaryLoggingEnabled)
	    {
	      _diagnosticTable = new DataTable();
        _diagnosticTable.Columns.Add("PathId", typeof(int));
        _diagnosticTable.Columns.Add("Key", typeof(string));
	      foreach (var date in ExposureDates)
	      {
          _diagnosticTable.Columns.Add(date.ToString(), typeof(double));
        }
        _partialDiagnosticResults = new double[4, ExposureDates.Length];
	    }
    }

    private IncrementalCCRExposureAggregator(IncrementalCCRExposureAggregator other, Tuple<Dt[], double[]>[] kernels)
    {
      _preIncrementalAcculator = other._preIncrementalAcculator.CloneObjectGraph(CloneMethod.FastClone);
      _postIncrementalAcculator = other._postIncrementalAcculator.CloneObjectGraph(CloneMethod.FastClone);
      _preIncrementalAcculator.IntegrationKernels = kernels;
      _postIncrementalAcculator.IntegrationKernels = kernels;

      _reduced = other._reduced;
      _isUnilateral = other._isUnilateral;
      _discountExposures = other._discountExposures;
      _wrongWayRisk = other._wrongWayRisk;
      _fundingCostNoDefault = other._fundingCostNoDefault;
      _fundingBenefitNoDefault = other._fundingBenefitNoDefault;
      _diagnosticTable = other._diagnosticTable;
      _partialDiagnosticResults = other._partialDiagnosticResults;
      _binaryLoggingEnabled = other._binaryLoggingEnabled;
      _maxExposureDt = other._maxExposureDt;
      _netPriorExposures = other._netPriorExposures;
      _netPostExposures = other._netPostExposures;
      AsOf = other.AsOf;
      CounterpartyExposure = other.CounterpartyExposure;
      BookingEntityExposure = other.BookingEntityExposure;
      ExposureDates = other.ExposureDates;
      NettingMap = other.NettingMap;
      PrecalculatedMarketData = other.PrecalculatedMarketData;
      PreIncrementalExposures = other.PreIncrementalExposures;
      IncrementalExposures = other.IncrementalExposures;
      PreIncrementalNettingSets = other.PreIncrementalNettingSets;
      IncrementalNettingSets = other.IncrementalNettingSets;
    }

    #endregion

    #region Properties
    /// <summary>
    /// Time zero, As of date
    /// </summary>
    public Dt AsOf { get; set; }

    /// <summary>
    /// Delegate to compute pathwise counterparty exposure
    /// </summary>
    public PathWiseExposure CounterpartyExposure { get; private set; }

    /// <summary>
    /// Delegate to compute pathwise own exposure
    /// </summary>
    public PathWiseExposure BookingEntityExposure { get; private set; }

    /// <summary>
    /// EffectiveSurvival[0] = Probability that counterparty survives to T and that default time of counterparty follows default time of the booking entity  
    /// EffectiveSurvival[1] = Probability that booking entity survives to T and that default time of counterparty precedes default time of the booking entity
    /// </summary>
    public IList<Tuple<Dt[], double[]>> IntegrationKernels {
	    get
	    {
	      if (_integrationKernels == null)
	        _integrationKernels = TransformIntegrationKernels(PrecalculatedMarketData.IntegrationKernels);
	      return _integrationKernels; 
	    }
    }

    /// <summary>
    /// Precalculated pvs
    /// </summary>
    public PrecalculatedExposures PreIncrementalExposures { get; set; }

    /// <summary>
    /// pvs for incremental trades
    /// </summary>
    public PrecalculatedExposures IncrementalExposures { get; set; }

		/// <summary>
    /// Netting set for each set of precalculated pvs
    /// </summary>
    public IList<string> PreIncrementalNettingSets { get; set; }

    /// <summary>
    /// Netting set for each set of precalculated pvs
    /// </summary>
    public IList<string> IncrementalNettingSets { get; set; }

    /// <summary>
    /// Get unilateral flag
    /// </summary>
    public bool IsUnilateral
    {
      get { return _isUnilateral; }
    }

    /// <summary>
    /// Diagnostics for the CCRExposure Aggregator
    /// </summary>
    public override DataTable DiagnosticsTable
    {
      get { return _diagnosticTable; }
    }

    /// <summary>
    /// Datatabke if the Integration Kernals
    /// </summary>
    public override DataTable IntegrationKernelsDataTable
    {
      get
      {
        if (_binaryLoggingEnabled && IntegrationKernels.Count != 0)
        {
          var dataTable = new DataTable();
          dataTable.Columns.Add("Kernels", typeof(string));

          foreach (var date in IntegrationKernels[0].Item1)
          {
            dataTable.Columns.Add(date.ToString(), typeof(double));
          }

          for (var i = 0; i < IntegrationKernels.Count; ++i)
          {
            var row = dataTable.NewRow();
            row["Kernels"] = GetIndex(i);
            for (var j = 0; j < IntegrationKernels[i].Item2.Length; ++j)
            {
              row[j + 1] = IntegrationKernels[i].Item2[j];
            }

            dataTable.Rows.Add(row);
          }

          return dataTable;
        }
        return null;
      }
    }

    #endregion

    #region Implementation of ISimulatedValues

    /// <summary>
    /// Number of exposure dates
    /// </summary>
    public int DateCount
    {
      get { return ExposureDates.Length; }
    }

    /// <summary>
    /// Exposure dates
    /// </summary>
    public Dt[] ExposureDates { private set; get; }

    /// <summary>
    ///   Number of paths.
    /// </summary>
    public int PathCount
    {
      get { return PreIncrementalExposures.PathCount; }
    }

    /// <summary>
    /// Netting group information
    /// </summary>
    public Dictionary<string, int> NettingMap { get; set; }

    /// <summary>
		/// 
		/// </summary>
		public PrecalculatedMarketData PrecalculatedMarketData { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public override Dt MaxExposureDate
    {
      get { return _maxExposureDt; }
    }

    #endregion

    #region 

    /// <summary>
    /// Generate ISimulatedPathValues list
    /// </summary>
    /// <returns></returns>
    public void RunSimulationPaths()
    {
      for (int i = 0; i < PathCount; i++)
      {
        RunSimulationPath(i, IsUnilateral, _preIncrementalAcculator, _postIncrementalAcculator);

        if (_binaryLoggingEnabled)
        {
          AppendToDataTable(i);
        }
      }
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="kernels"></param>
    public override IIncrementalExposureAggregator ChangeIntegrationKernels(IList<Tuple<Dt[], double[]>> kernels)
    {
      var newKernels = TransformIntegrationKernels(kernels);
      var newAgg = new IncrementalCCRExposureAggregator(this, newKernels);
      return newAgg;
    }

    private void AppendToDataTable(int path)
    {
      for (var i = 0; i < _partialDiagnosticResults.GetLength(0); ++i)
      {
        var row = _diagnosticTable.NewRow();
        row["PathId"] = path;
        row["Key"] = GetKey(i);
        for (var j = 0; j < _partialDiagnosticResults.GetLength(1); ++j)
        {
          row[j + 2] = _partialDiagnosticResults[i, j];
        }
        _diagnosticTable.Rows.Add(row);
      }
    }

    private static string GetKey(int keyId)
    {
      switch (keyId)
      {
        case 0:
          return "PE";
        case 1:
          return "NE";
        case 2:
          return "Received";
        case 3:
          return "Posted";
      }
      throw new ArgumentException(string.Format("Invalid Key Id: {0}", keyId));
    }

    private static string GetIndex(int keyId)
    {
      switch (keyId)
      {
        case 0:
          return "Cpty Default";
        case 1:
          return "Own Default";
        case 2:
          return "Survival";
        case 3:
          return "Ignore Default";
      }
      throw new ArgumentException(string.Format("Invalid Key Id: {0}", keyId));
    }

    private CCRMeasure ConvertMeasure(CCRMeasure input)
    {
      CCRMeasure measure = input; 
      switch (input)
      {
        case CCRMeasure.CVA:
          if (!_wrongWayRisk)
            measure = CCRMeasure.CVA0;
          break;
        case CCRMeasure.DVA:
          if (!_wrongWayRisk)
            measure = CCRMeasure.DVA0;
          break;
        case CCRMeasure.FCA:
          if (!_wrongWayRisk)
            measure = CCRMeasure.FCA0;
          if (_fundingCostNoDefault)
            measure = CCRMeasure.FCANoDefault;
          break;
        case CCRMeasure.FBA:
          if (!_wrongWayRisk)
            measure = CCRMeasure.FBA0;
          if (_fundingBenefitNoDefault)
            measure = CCRMeasure.FBANoDefault;
          break;
        case CCRMeasure.RWA:
          if (!_wrongWayRisk)
            measure = CCRMeasure.RWA0;
          break;
        case CCRMeasure.EE:
          if (!_wrongWayRisk)
          {
            measure = CCRMeasure.EE0;
            if (_discountExposures)
              measure = CCRMeasure.DiscountedEE0;
          }
          if (_discountExposures)
            measure = CCRMeasure.DiscountedEE;
          break;
        case CCRMeasure.NEE:
          if (!_wrongWayRisk)
          {
            measure = CCRMeasure.NEE0;
            if (_discountExposures)
              measure = CCRMeasure.DiscountedNEE0;
          }
          if (_discountExposures)
            measure = CCRMeasure.DiscountedNEE;
          break;
        case CCRMeasure.PFE:
          if (!_wrongWayRisk)
          {
            measure = CCRMeasure.PFE0;
            if (_discountExposures)
              measure = CCRMeasure.DiscountedPFE0;
          }
          if (_discountExposures)
            measure = CCRMeasure.DiscountedPFE;
          break;
        case CCRMeasure.PFNE:
          if (!_wrongWayRisk) // TODO: Add PFNE0 and DiscountedPFNE0 measures 
          {
            measure = CCRMeasure.PFNE;
            if (_discountExposures)
              measure = CCRMeasure.DiscountedPFNE;
          }
          if (_discountExposures)
            measure = CCRMeasure.DiscountedPFNE;
          break;
        case CCRMeasure.StdErrEE:
          if (_discountExposures)
            measure = CCRMeasure.StdErrDiscountedEE;
          break; 
        case CCRMeasure.StdErrNEE:
          if (_discountExposures)
            measure = CCRMeasure.StdErrDiscountedNEE;
          break;
        case CCRMeasure.ForwardEAD:
          if (!_wrongWayRisk)
            measure = CCRMeasure.ForwardEAD0;
          break;
      }
      return measure; 
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="ci"></param>
    public override void AddMeasureAccumulator(CCRMeasure measure, double ci)
    {
      if (_preIncrementalAcculator == null)
      {
        _preIncrementalAcculator = new CCRMeasureAccumulator(AsOf, ExposureDates,
          IntegrationKernels.ToArray(),
          PrecalculatedMarketData.Recoveries,
          null, PathCount);
      }

      if (_postIncrementalAcculator == null)
      {
        _postIncrementalAcculator = new CCRMeasureAccumulator(AsOf, ExposureDates,
          IntegrationKernels.ToArray(),
          PrecalculatedMarketData.Recoveries,
          null, PathCount);
      }
      measure = ConvertMeasure(measure);
      _preIncrementalAcculator.AddMeasureAccumulator(measure, ci);
      _postIncrementalAcculator.AddMeasureAccumulator(measure, ci);
    }


    /// <summary>
    /// Get change in CCRMeasure for portfolio prior to and including incremental trades
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="t"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    public override double GetIncrementalMeasure(CCRMeasure measure, int t, double ci)
    {
      return GetIncrementalMeasure(measure, ExposureDates[t], ci);
    }

    /// <summary>
    /// Get change in CCRMeasure for portfolio prior to and including incremental trades
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="dt"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    public override double GetIncrementalMeasure(CCRMeasure measure, Dt dt, double ci)
    {
      if (!_reduced)
        Reduce();
      measure = ConvertMeasure(measure);
      var pre = _preIncrementalAcculator.HasMeasureAccumulator(measure, ci) ? _preIncrementalAcculator.GetMeasure(measure, dt, ci) : 0.0;
      var post = _postIncrementalAcculator.HasMeasureAccumulator(measure, ci) ? _postIncrementalAcculator.GetMeasure(measure, dt, ci) : 0.0;
      return post - pre;
    }


    /// <summary>
    /// Get CCRMeasure for portfolio of prior + incremental trade
    /// </summary>
    /// <param name="measure"></param>
    /// <param name="t"></param>
    /// <param name="ci"></param>
    /// <returns></returns>
    public override double GetTotalMeasure(CCRMeasure measure, int t, double ci)
    {
      if (!_reduced)
        Reduce();
      measure = ConvertMeasure(measure);
      return _postIncrementalAcculator.HasMeasureAccumulator(measure, ci) ? _postIncrementalAcculator.GetMeasure(measure, ExposureDates[t], ci) : 0.0;
    }

    private void RunSimulationPath(int idx, bool unilateral, CCRMeasureAccumulator preIncrementalAccumulator, CCRMeasureAccumulator postIncrementalAccumulator)
    {
      int nettingCount = NettingMap.Count;
      var priorExposurePath = new SimulatedPathValues(DateCount, nettingCount);
      var postExposurePath = new SimulatedPathValues(DateCount, nettingCount);
      IList<double> df, numeraire, cptyRn, ownRn, survivalRn, cptySpread, ownSpread, lendSpread, borrowSpread;
      PrecalculatedMarketData.GetMarketDataForPath(idx, _wrongWayRisk, unilateral, out numeraire, out df, out cptyRn, out ownRn, out survivalRn,
        out cptySpread, out ownSpread, out lendSpread, out borrowSpread);
      
      for (int t = 0; t < DateCount; ++t)
      {
        var nettingSetExposures = _netPriorExposures[idx][t];
        priorExposurePath.Add(nettingSetExposures, df[t], numeraire[t], cptyRn[t], ownRn[t], survivalRn[t], cptySpread[t], ownSpread[t], lendSpread[t], borrowSpread[t], idx, 1.0);
        var eeColl = CounterpartyExposure.ComputeCollateralAmounts(priorExposurePath, priorExposurePath, t, nettingSetExposures, null);
        var neeColl = BookingEntityExposure.ComputeCollateralAmounts(priorExposurePath, priorExposurePath, t, nettingSetExposures, null);
        var pathEE = CounterpartyExposure.CapAndSumExposures(nettingSetExposures, eeColl);
        var pathNEE = BookingEntityExposure.CapAndSumExposures(nettingSetExposures, neeColl);
        pathEE.DateIdx = pathNEE.DateIdx = t;
        pathEE.Path = pathNEE.Path = priorExposurePath;
        pathEE.PathIdx = pathNEE.PathIdx = idx;
        preIncrementalAccumulator.AccumulateExposures(pathEE, pathNEE);

        nettingSetExposures = _netPostExposures[idx][t];
        postExposurePath.Add(nettingSetExposures, df[t], numeraire[t], cptyRn[t], ownRn[t], survivalRn[t], cptySpread[t], ownSpread[t], lendSpread[t], borrowSpread[t], idx, 1.0);
        eeColl = CounterpartyExposure.ComputeCollateralAmounts(postExposurePath, postExposurePath, t, nettingSetExposures, null);
        neeColl = BookingEntityExposure.ComputeCollateralAmounts(postExposurePath, postExposurePath, t, nettingSetExposures, null);
        pathEE = CounterpartyExposure.CapAndSumExposures(nettingSetExposures, eeColl);
        pathNEE = BookingEntityExposure.CapAndSumExposures(nettingSetExposures, neeColl);
        pathEE.DateIdx = pathNEE.DateIdx = t;
        pathEE.Path = pathNEE.Path = postExposurePath;
        pathEE.PathIdx = pathNEE.PathIdx = idx;
        postIncrementalAccumulator.AccumulateExposures(pathEE, pathNEE);
        
        if (_binaryLoggingEnabled)
        {
          // Positive Exposure
          _partialDiagnosticResults[0, t] = pathEE.Exposure;
          // Negative Exposure
          _partialDiagnosticResults[1, t] = pathNEE.Exposure;
          // Received Collateral
          _partialDiagnosticResults[2, t] = pathEE.Collateral;
          // Posted Collateral
          _partialDiagnosticResults[3, t] = pathNEE.Collateral;
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

      _netPriorExposures = PreIncrementalExposures.GetNetExposures(NettingMap, ExposureDates);
      _netPostExposures = IncrementalExposures.GetNetExposures(NettingMap, ExposureDates);
      for (int p = 0; p < PathCount; p++)
      {
        for (int d = 0; d < ExposureDates.Length; d++)
        {
          for (int n = 0; n < NettingMap.Count; n++)
          {
            _netPostExposures[p][d][n] += _netPriorExposures[p][d][n];
          }
        }
      }
      
      RunSimulationPaths();

      _preIncrementalAcculator.ReduceCumulativeValues();
      _postIncrementalAcculator.ReduceCumulativeValues();

      var maxDt = Dt.MinValue;
      for (int t = 0; t < IncrementalExposures.Count; t++)
      {
        maxDt = Dt.Later(maxDt, IncrementalExposures.MaxExposureDate(t));
      }
      for (int t = 0; t < PreIncrementalExposures.Count; t++)
      {
        maxDt = Dt.Later(maxDt, PreIncrementalExposures.MaxExposureDate(t));
      }
      _maxExposureDt = ExposureDates.Any(dt => dt >= maxDt) ? ExposureDates.First(dt => dt >= maxDt) : maxDt;
      _reduced = true;
    }

    #endregion

    #region Private Methods
    /// <summary>
    /// 
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    protected Tuple<Dt[], double[]>[] TransformIntegrationKernels(IList<Tuple<Dt[], double[]>> input)
    {
      // single curve only
      if (input.Count == 3)
      {
        return new[]
        {
            TransformIntegrationKernel(IsUnilateral ? input[1] : input[0]) // Cpty default
			    };
      }
      return new[]
      {
          TransformIntegrationKernel(IsUnilateral ? input[1] : input[0]), // Cpty default
					TransformIntegrationKernel(IsUnilateral ? input[3] : input[2]), // Own default
					TransformIntegrationKernel(IsUnilateral ? input[5] : input[4]), // survival 
				  TransformIntegrationKernel(input[6]) // ignore default
			  };

    }

    private Tuple<Dt[], double[]> TransformIntegrationKernel(Tuple<Dt[], double[]> orig)
	  {
		  var curve = new Curve(AsOf);
		  var cumulative = CumulativeSum(orig.Item2).ToArray(); 
			curve.Add(orig.Item1, cumulative);
		  var interpolated = new double[ExposureDates.Length];
			interpolated[0] = curve.Interpolate(ExposureDates[0]);
		  var cum = interpolated[0];
		  for (int i = 1; i < interpolated.Length; i++)
		  {
				interpolated[i] = curve.Interpolate(ExposureDates[i]) - cum;
			  cum += interpolated[i]; 
		  }
			var kern = new Tuple<Dt[], double[]>(ExposureDates, interpolated);
		  return kern; 
	  }

		private IEnumerable<double> CumulativeSum(IEnumerable<double> sequence)
		{
			double sum = 0;
			foreach (var item in sequence)
			{
				sum += item;
				yield return sum;
			}
		}
    #endregion

    double ICCRMeasureSource.GetMeasure(CCRMeasure measure, Dt date, double ci)
    {
      return GetIncrementalMeasure(measure, date, ci);
    }

    double[] ICCRMeasureSource.GetMeasureMarginal(CCRMeasure measure, Dt date, double ci)
    {
      throw new NotImplementedException();
    }
  }
}