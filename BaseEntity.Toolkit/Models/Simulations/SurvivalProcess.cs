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
using static BaseEntity.Toolkit.Models.Simulations.MarketPathExtensions;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// survival process
  /// </summary>
  [Serializable]
  [ObjectLoggerEnabled]
  public class SurvivalProcess : IProcess
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(SurvivalProcess));
    [ObjectLogger(Name = "SurvivalCurveSimulation", Description = "Simulated Survival Probabilities, Factors and Vols for each path and simulation date", Category = "Simulation")]
    private static readonly IObjectLogger BinaryLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(SurvivalProcess));

    /// <summary>
    /// Constructor
    /// </summary>
    public SurvivalProcess () { }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="survivalProcessCalibration">survival process calibration</param>
    /// <param name="domesticDiscountProcess">domestic discount process</param>
    /// <param name="foreignDiscountProcess">foreign discount process</param>
    public SurvivalProcess(SurvivalProcessCalibration survivalProcessCalibration, DomesticDiscountProcess domesticDiscountProcess, ForeignDiscountProcess foreignDiscountProcess = null)
    {
      AsOf = domesticDiscountProcess.AsOf;
      PathData = new Dictionary<int, IntPtr>();
      _simulator = new Lazy<Simulator>(InitSimulator);

      WeinerProcess = domesticDiscountProcess.WeinerProcess;
      DomesticDiscountProcess = domesticDiscountProcess;
      DomesticCurrency = domesticDiscountProcess.DomesticCurrency;
	    DomesticDiscountCurve = DomesticDiscountProcess.DiscountCurve;
      SurvivalCurve = survivalProcessCalibration.SurvivalCurve;
      Currency = SurvivalCurve.Ccy;
      if (Currency != DomesticCurrency && foreignDiscountProcess != null)
      {
        ForeignDiscountProcess = foreignDiscountProcess;
	      ForeignDiscountCurve = foreignDiscountProcess.ForeignDiscountCurve;
      }
      Calibration = survivalProcessCalibration;
      Factorization = Calibration.Factorization; 
    }

    /// <summary>
    /// Help function. ToDataTable
    /// </summary>
    /// <returns></returns>
    public DataTable ToDataTable()
    {
      var dataTable = new DataTable(String.Format("{0}", SurvivalCurve.Name));
      dataTable.Columns.Add("PathId", typeof(int));
      dataTable.Columns.Add("Tenor", typeof(string));
      dataTable.Columns.Add("Label", typeof(string));

      foreach (var simulationDate in Simulator.SimulationDates)
      {
        dataTable.Columns.Add(simulationDate.ToString(), typeof(double));
      }
      for (var pathId = 0; pathId < PathCount; pathId++)
      {
        var pathData = PathDataView(pathId);
        var dim = 2 * WeinerProcess.TenorDts.Length;
        for (var tenorIdx = 0; tenorIdx < dim; tenorIdx++)
        {
          var label = tenorIdx % 2 == 0 || tenorIdx == 0 ? "Survival Prob" : "Factor";

          // hard coded cutoff for the size of recorded datatable to prevent OutOfBoundsExceptoin when serializing
          // ideally the datatable should come no where near this number as it will be diffcult to analysis the reported table
          if (dataTable.Rows.Count >= 10000)
          {
            continue;
          }

          var row = dataTable.NewRow();
          row["PathId"] = pathId;
          row["Tenor"] = Simulator.Tenors[tenorIdx / 2].ToString();
          row["Label"] = label;
          for (var dateIdx = 0; dateIdx < Simulator.SimulationDates.Length; dateIdx++)
          {
            // +3 for key cols
            row[dateIdx + 3] = pathData[dateIdx * dim + tenorIdx];
          }
          dataTable.Rows.Add(row);
        }
      }

      var array = new double[Simulator.SimulationDates.Length];
      Simulator.GetSurvivalSigma(0, array);

      var finalRow = dataTable.NewRow();
      finalRow["PathId"] = -1;
      finalRow["Tenor"] = new Tenor(Frequency.None).ToString();
      finalRow["Label"] = "SurvivalSigmaArray";

      for (var i = 0; i < Simulator.SimulationDates.Length; ++i)
      {
        finalRow[i + 3] = array[i];
      }
      dataTable.Rows.Add(finalRow);

      return dataTable;
    }

    /// <summary>
    /// Get data from a path
    /// </summary>
    /// <param name="pathId">path id</param>
    /// <returns></returns>
    public IntPtr GetPathData(int pathId)
    {
      Logger.VerboseFormat("Getting Path Data for survival curve {0} path {1}.", SurvivalCurve.Name, pathId);
      if (PathData == null)
      {
        Logger.ErrorFormat("PathData is null in call to GetPathData. Survival Curve {0} path {1}. Likely object is being accessed after already Disposed.", SurvivalCurve.Name, pathId);
        PathData = new Dictionary<int, IntPtr>();
      }

      if (PathData.ContainsKey(pathId))
        return PathData[pathId];
      Logger.VerboseFormat("Allocating {0} byte memory block for SurvivalCurveSimulation Data {1} path {2}", BlockSize, SurvivalCurve.Name, pathId);
      var pathData = UnmanagedMemory.Allocate(BlockSize);
      if (Simulator == null)
      {
        Logger.ErrorFormat("Call to SurvivalCurveSimulation.GetPathData(), Curve {0} path {1}, null Simulator", SurvivalCurve.Name, pathId);
        throw new ToolkitException("Null Simulator in GetPathData");
      }
      using (var path = Simulator.GetSimulatedPath())
      {
        if (path == null)
        {
          Logger.ErrorFormat("Null path returned by Simulator.GetSimulatedPath(). SurvivalCurveSimulation {0} path {1}", SurvivalCurve.Name, pathId);
          throw new ToolkitException("Null path returned by Simulator.GetSimulatedPath().");
        }
        path.SetWeinerIncrements(WeinerProcess.GetPathData(pathId));
        path.SetRates(0, DomesticDiscountProcess.GetPathData(pathId));
        if (Currency != DomesticCurrency)
        {
          path.SetRates(1, ForeignDiscountProcess.GetPathData(pathId));
        }
        path.SetSurvivals(0, pathData);
				path.GetSurvivals(0);
      }
      PathData[pathId] = pathData;
      
      // Log Simulator on final pass
      if (BinaryLogger.IsObjectLoggingEnabled && pathId == PathCount - 1)
      {
        var binaryLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(BinaryLogger, System.Reflection.MethodBase.GetCurrentMethod(), SurvivalCurve.Name);
        binaryLogAggregator.Append(typeof(SurvivalProcess), string.Format("PathData.{0}",SurvivalCurve.Name), AppenderUtil.DataTableToDataSet(ToDataTable()));
        binaryLogAggregator.Append(typeof(SurvivalProcess), string.Format("SurvivalThresholds.{0}",SurvivalCurve.Name), AppenderUtil.DataTableToDataSet(SurvivalThresholdsDataTable())).Log();
      }

      return pathData;
    }

    private DataTable SurvivalThresholdsDataTable()
    {
      var array = new double[WeinerProcess.TenorDts.Length];
      Simulator.GetSurvivalThreshold(0, array);

      var dataTable = new DataTable(string.Format("{0}.{1}", SurvivalCurve.Name, "SurvivalThresholds"));
      dataTable.Columns.Add("SurvivalThresholds", typeof(string));
      foreach (var tenors in Simulator.Tenors)
      {
        dataTable.Columns.Add(tenors.ToString(), typeof(double));
      }
      var row = dataTable.NewRow();
      for (var i = 0; i < Simulator.Tenors.Count(); ++i)
      {
        row[i+1] = array[i];
      }
      dataTable.Rows.Add(row);
      
      return dataTable;
    }

    /// <summary>
    /// Get Data from a path
    /// </summary>
    /// <param name="i">path id</param>
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
			//toolkit holds array of struct survival_type{double survival; double factor;} treat this as 2 doubles as layout in memory is equivalent
      get { return Simulator.SimulationDates.Length * (WeinerProcess.TenorDts.Length) * 2 * sizeof(double); }
		}
    
    /// <summary>
    /// AsOf date
    /// </summary>
		public Dt AsOf { get; private set; }

    /// <summary>
    /// Path count
    /// </summary>
    public int PathCount => WeinerProcess.PathCount;

    /// <summary>
    /// simulation date
    /// </summary>
    public Dt[] SimulationDts => WeinerProcess.SimulationDts; 

    /// <summary>
    /// Simulation environment name
    /// </summary>
    public string SimulationEnvironmentName { get; private set; }
    
    /// <summary>
    /// Boolean type. QuasiMonteCarlo 
    /// </summary>
    public bool QuasiMonteCarlo { get { return WeinerProcess.QuasiMonteCarlo; }}

    /// <summary>
    /// Domestic discount curve
    /// </summary>
    public DiscountCurve DomesticDiscountCurve { get; set; }

    /// <summary>
    /// Foreign discount curve
    /// </summary>
    public DiscountCurve ForeignDiscountCurve { get; set; }

    /// <summary>
    /// Survival curve
    /// </summary>
    public SurvivalCurve SurvivalCurve
    {
      get; set;
    }

    /// <summary>
    /// Calibration
    /// </summary>
    public SurvivalProcessCalibration Calibration { get; private set; }

    /// <summary>
    /// Factorization
    /// </summary>
    public MatrixFactorization Factorization { get; private set; }

    /// <summary>
    /// Domestic currency
    /// </summary>
    public Currency DomesticCurrency { get; set; }

    /// <summary>
    /// Currency
    /// </summary>
    public Currency Currency { get; set; }

    private Dictionary<int, IntPtr> PathData
    {
      get { return _pathData; }
      set { _pathData = value; }
    }

    /// <summary>
    /// Simulator
    /// </summary>
    public Simulator Simulator
    {
      get
      {
        return _simulator.Value;
      }
    }

    /// <summary>
    /// Weiner process
    /// </summary>
    public WeinerProcess WeinerProcess { get; private set; }

    /// <summary>
    /// Domestic discount process
    /// </summary>
		public DomesticDiscountProcess DomesticDiscountProcess { get; private set; }

    /// <summary>
    /// Foreign discount process
    /// </summary>
		public ForeignDiscountProcess ForeignDiscountProcess { get; private set; }

    /// <summary>
    /// Boolean type. CounterpartyOnly or not.
    /// </summary>
    public bool CounterpartyOnly { get; private set; }
    
    /// <summary>
    /// Boolean type. isActive.
    /// </summary>
    public bool IsActive
    {
      get
      {
        if (Factorization.ContainsReference(SurvivalCurve))
        {
          return !CounterpartyOnly; // if curve process is only used for rn, don't need to simulate 
        }
        return false;  // if curve is not in sim env, treat as inactive with 0 vol
      }
    }

    /// <summary>
    /// Volatiilty curves
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
      Logger.DebugFormat("InitSimulator() for SurvivalProcess {0}{1}.", SurvivalCurve.Name, SurvivalCurve.AsOf);
      var factory = Simulator.CreateSimulatorFactory(PathCount, AsOf, WeinerProcess.SimulationDts, WeinerProcess.TenorDts, Factorization.MarketFactorNames);
      factory.AddDomesticDiscount(DomesticDiscountCurve, DomesticDiscountProcess.Volatilities, DomesticDiscountProcess.FactorLoadings, true);
      if (Currency != DomesticCurrency && ForeignDiscountProcess != null)
        factory.AddDiscount(ForeignDiscountCurve, ForeignDiscountProcess.Volatilities, ForeignDiscountProcess.FactorLoadings, ForeignDiscountProcess.FxRate, ForeignDiscountProcess.FxVolatilities, ForeignDiscountProcess.FxFactorLoadings, true);
		  factory.AddSurvival(SurvivalCurve, Volatilities, FactorLoadings, IsActive);
		  return factory.Simulator;
	  }

    /// <summary>
    /// dispose funcion
    /// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

    /// <summary>
    /// Dispose function
    /// </summary>
    /// <param name="disposing">boolean type. dispose value or not</param>
		public void Dispose(bool disposing)
		{
      Logger.DebugFormat("Disposing SurvivalCurveProcess {0}. disposing = {1}",
        SafeGetCurveName(SurvivalCurve, !disposing), disposing);

      Clear();
      PathData = null;
		  if (_simulator != null && _simulator.IsValueCreated && disposing)
		    _simulator.Value.Dispose();
    }

    /// <summary>
    /// Clear path data. If GetPathData() is called again, data will be resimulated. 
    /// </summary>
    public void Clear()
    {
      if (PathData != null)
      {
        foreach (var intPtr in PathData.Values)
        {
          if (intPtr != IntPtr.Zero)
            Marshal.FreeCoTaskMem(intPtr);
        }
        PathData.Clear();
      } 
    }

    /// <summary>
    /// Destructor
    /// </summary>
		~SurvivalProcess()
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