using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using log4net;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// Class of forward curve process
  /// </summary>
  [Serializable]
  [ObjectLoggerEnabled]
  public class ForwardCurveProcess : IProcess
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(ForwardCurveProcess));
    [ObjectLogger(Name = "ForwardCurveSimulation", Description = "Simulated Forward Rates or Prices for each path and simulation date", Category = "Simulation")]
    private static readonly IObjectLogger BinaryLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(ForwardCurveProcess));

    /// <summary>
    /// Forward curve process
    /// </summary>
    public ForwardCurveProcess () { }

    /// <summary>
    /// Forward curve process
    /// </summary>
    /// <param name="projectionCurveCalibration">Projection curve calibration</param>
    /// <param name="domesticDiscountProcess">domestic discount  process</param>
    /// <param name="foreignDiscountProcess">foreign discount process</param>
    public ForwardCurveProcess(DiscountProcessCalibration projectionCurveCalibration, DomesticDiscountProcess domesticDiscountProcess, ForeignDiscountProcess foreignDiscountProcess = null)
    {
      AsOf = domesticDiscountProcess.AsOf;
      PathData = new Dictionary<int, IntPtr>();
      _simulator = new Lazy<Simulator>(InitSimulator);

      ForwardCurve = projectionCurveCalibration.DiscountCurve;
      Currency = ForwardCurve.Ccy;
      WeinerProcess = domesticDiscountProcess.WeinerProcess;
      DomesticDiscountProcess = domesticDiscountProcess;
      DomesticCurrency = domesticDiscountProcess.DomesticCurrency;
	    ForeignDiscountProcess = foreignDiscountProcess;
      Calibration = projectionCurveCalibration;
      Factorization = projectionCurveCalibration.Factorization;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="calibration">calibration</param>
    /// <param name="domesticDiscountProcess">domestic discount process</param>
    /// <param name="foreignDiscountProcess">foreign discount process</param>
    public ForwardCurveProcess(ForwardCurveProcessCalibration calibration, DomesticDiscountProcess domesticDiscountProcess, ForeignDiscountProcess foreignDiscountProcess = null)
    {
      AsOf = domesticDiscountProcess.AsOf;
      PathData = new Dictionary<int, IntPtr>();
      _simulator = new Lazy<Simulator>(InitSimulator);

      ForwardCurve = calibration.ForwardCurve;
      Currency = ForwardCurve.Ccy;
      WeinerProcess = domesticDiscountProcess.WeinerProcess;
      DomesticDiscountProcess = domesticDiscountProcess;
      DomesticCurrency = domesticDiscountProcess.DomesticCurrency;
      ForeignDiscountProcess = foreignDiscountProcess;

      Calibration = calibration;
      Factorization = calibration.Factorization; 
    }
    
    /// <summary>
    /// To date table function
    /// </summary>
    /// <returns></returns>
    public DataTable ToDataTable()
    {
      var dataTable = new DataTable(String.Format("{0}", ForwardCurve.Name));
      dataTable.Columns.Add("PathId", typeof(int));
      dataTable.Columns.Add("Tenor", typeof(string));

      foreach (var simulationDate in Simulator.SimulationDates)
      {
        dataTable.Columns.Add(simulationDate.ToString(), typeof(double));
      }
      for (int pathId = 0; pathId < PathCount; pathId++)
      {
        var pathData = PathDataView(pathId);
        var tenorDim = WeinerProcess.TenorDts.Length;
        for (int tenorIdx = 0; tenorIdx < tenorDim; tenorIdx++)
        {
          // hard coded cutoff for the size of recorded datatable to prevent OutOfBoundsException when serializing
          // ideally the datatable should come no where near this number as it will be diffcult to analysis the reported table
          if (dataTable.Rows.Count >= 10000)
          {
            continue;
          }
          var tenor = WeinerProcess.TenorDts[tenorIdx].ToString();
          var row = dataTable.NewRow();
          
          row["PathId"] = pathId;
          row["Tenor"] = tenor;
          for (int dateIdx = 0; dateIdx < Simulator.SimulationDates.Length; dateIdx++)
          {
            // +2 for key cols
            row[dateIdx + 2] = pathData[dateIdx * tenorDim + tenorIdx];
          }
          dataTable.Rows.Add(row);
        }
      }
      return dataTable;
    }

    /// <summary>
    /// Get path date
    /// </summary>
    /// <param name="pathId">path id</param>
    /// <returns></returns>
    public IntPtr GetPathData(int pathId)
    {
      Logger.VerboseFormat("Getting Forward Curve Path Data for projection curve {0} path {1}.", ForwardCurve.Name, pathId);
      if (PathData == null)
      {
        Logger.ErrorFormat("PathData is null in call to GetPathData. Forward Curve {0} path {1}. Likely object is being accessed after already Disposed.", ForwardCurve.Name, pathId);
        PathData = new Dictionary<int, IntPtr>();
      }
      if (PathData.ContainsKey(pathId))
        return PathData[pathId];
      Logger.VerboseFormat("Allocating {0} byte memory block for Forward Curve Simulation Data. Projection Curve {1} path {2}", BlockSize, ForwardCurve.Name, pathId);
      var pathData = UnmanagedMemory.Allocate(BlockSize);
      if (Simulator == null)
      {
        Logger.ErrorFormat("Call to ForwardCurveSimulation.GetPathData(), Curve {0} path {1}, null Simulator", ForwardCurve.Name, pathId);
        throw new ToolkitException("Null Simulator in GetPathData");
      }
      using (var path = Simulator.GetSimulatedPath())
      {
        if (path == null)
        {
          Logger.ErrorFormat("Null path returned by Simulator.GetSimulatedPath(). ForwardCurveSimulation {0} path {1}", ForwardCurve.Name, pathId);
          throw new ToolkitException("Null path returned by Simulator.GetSimulatedPath().");
        }
        path.SetWeinerIncrements(WeinerProcess.GetPathData(pathId));
        path.SetRates(0, DomesticDiscountProcess.GetPathData(pathId));
        if (Currency != DomesticCurrency && ForeignDiscountProcess != null)
        {
          path.SetRates(1, ForeignDiscountProcess.GetPathData(pathId));
        }
				path.SetForwards(0, pathData);
        path.GetForwards(0);
      }
      PathData[pathId] = pathData;

      // Log Simulator on final pass
      if (BinaryLogger.IsObjectLoggingEnabled && pathId == PathCount - 1)
      {
        var binaryLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(BinaryLogger, System.Reflection.MethodBase.GetCurrentMethod(), ForwardCurve.Name);
        binaryLogAggregator.Append(typeof(ForwardCurveProcess), ForwardCurve.Name, AppenderUtil.DataTableToDataSet(ToDataTable())).Log();
      }

      return pathData;
    }

    /// <summary>
    /// The data along path
    /// </summary>
    /// <param name="i">path index</param>
    /// <returns></returns>
		public IList<double> PathDataView(int i)
		{
			var ptr = GetPathData(i);
			return NativeUtil.DoubleArray(ptr, BlockSize / sizeof(double), null);
		}


    /// <summary>
    /// Block size
    /// </summary>
		public int BlockSize
		{
			get { return Simulator.SimulationDates.Length * (WeinerProcess.TenorDts.Length) * sizeof(double); }
		}
    
    /// <summary>
    /// Asof date
    /// </summary>
    public Dt AsOf { get; private set; }

    /// <summary>
    /// Path count
    /// </summary>
    public int PathCount => WeinerProcess.PathCount;

    /// <summary>
    /// Simulation dates
    /// </summary>
    public Dt[] SimulationDts => WeinerProcess.SimulationDts; 

    /// <summary>
    /// Boolean type. Quasi Monte Carlo
    /// </summary>
    public bool QuasiMonteCarlo { get { return WeinerProcess.QuasiMonteCarlo; }}

    /// <summary>
    /// Domestic discount curve
    /// </summary>
    public DiscountCurve DomesticDiscountCurve => DomesticDiscountProcess.DiscountCurve;

    /// <summary>
    /// Foreign discount curve
    /// </summary>
    public DiscountCurve ForeignDiscountCurve => ForeignDiscountProcess.ForeignDiscountCurve;

    /// <summary>
    /// Domestic currency
    /// </summary>
    public Currency DomesticCurrency { get; set; }

    /// <summary>
    /// currency
    /// </summary>
    public Currency Currency { get; set; }
    
    /// <summary>
    /// forward curve
    /// </summary>
	  public CalibratedCurve ForwardCurve { get; set;}

    /// <summary>
    /// calibration
    /// </summary>
    public IProcessCalibration Calibration { get; private set; }
    
    /// <summary>
    /// Factorization
    /// </summary>
    public MatrixFactorization Factorization { get; private set; }

    private Dictionary<int, IntPtr> PathData
    {
      get { return _pathData; }
      set { _pathData = value; }
    }

    /// <summary>
    /// simulator
    /// </summary>
    public Simulator Simulator
    {
      get
      {
        return _simulator.Value;
      }
    }

    private WeinerProcess WeinerProcess { get; set; }

    private DomesticDiscountProcess DomesticDiscountProcess { get; set; }

    private ForeignDiscountProcess ForeignDiscountProcess { get; set; }

    /// <summary>
    /// Volatility curves
    /// </summary>
    public VolatilityCurve[] Volatilities
    {
      get
      {
        if (_volatilities == null)
        {
          _volatilities = Calibration.Volatilities;
        }
        return _volatilities;
      }
      set { _volatilities = value; }
    }

    /// <summary>
    /// Factor loadings
    /// </summary>
    public double[,] FactorLoadings
    {
      get
      {
        if (_factorLoadings == null)
        {
          _factorLoadings = Calibration.FactorLoadings;
        }
        return _factorLoadings;
      }
      set { _factorLoadings = value; }
    }

    /// <summary>
    /// Market factor names
    /// </summary>
    public string[] MarketFactorNames
    {
      get
      {
        if (_marketFactorNames == null)
        {
          _marketFactorNames = Factorization.MarketFactorNames;
        }
        return _marketFactorNames;
      }
      set { _marketFactorNames = value; }
    }

    private Simulator InitSimulator()
	  {
      Logger.DebugFormat("InitSimulator() for FwdCurveProcess {0}{1}.", ForwardCurve.Name, ForwardCurve.AsOf);
      var factory = Simulator.CreateSimulatorFactory(PathCount, AsOf, WeinerProcess.SimulationDts, WeinerProcess.TenorDts, MarketFactorNames);
			factory.AddDomesticDiscount(DomesticDiscountCurve, DomesticDiscountProcess.Volatilities, DomesticDiscountProcess.FactorLoadings, true);
	    if (ForeignDiscountProcess != null)
	    {
        factory.AddDiscount(ForeignDiscountCurve, ForeignDiscountProcess.Volatilities, ForeignDiscountProcess.FactorLoadings, ForeignDiscountProcess.FxRate, ForeignDiscountProcess.FxVolatilities, ForeignDiscountProcess.FxFactorLoadings, true);
      }
      factory.AddForward(ForwardCurve, Volatilities, FactorLoadings, true);
      return factory.Simulator;
	  }
		
    /// <summary>
    /// dispose function
    /// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

    /// <summary>
    /// dispose function
    /// </summary>
    /// <param name="disposing">boolean type</param>
		public void Dispose(bool disposing)
		{
    //  Logger.DebugFormat("Disposing ForwardRateCurveSimulation. Rate Curve {0}. disposing = {1}", ForwardCurve != null ? ForwardCurve.Name : null, disposing);
			if (PathData != null)
			{
				foreach (var intPtr in PathData.Values)
				{
					if(intPtr != IntPtr.Zero)
						Marshal.FreeCoTaskMem(intPtr);
				}
        PathData.Clear();
			  PathData = null; 
			}
		}

    /// <summary>
    /// Destructor
    /// </summary>
		~ForwardCurveProcess()
		{
			Dispose(false);
		}


    [OnDeserialized, AfterFieldsCloned]
    private void OnClone(StreamingContext context)
    {
      // reinitialize path data after clone or deserialize event
      _pathData = new Dictionary<int, IntPtr>();
    }

    private readonly Lazy<Simulator> _simulator;
    private VolatilityCurve[] _volatilities;
    private double[,] _factorLoadings;
    private string[] _marketFactorNames;
    [NoClone, NonSerialized]
    private Dictionary<int, IntPtr> _pathData;
  }
}