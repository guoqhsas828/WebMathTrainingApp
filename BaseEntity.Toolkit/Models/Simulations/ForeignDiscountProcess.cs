using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using log4net;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using System.Linq;
using static BaseEntity.Toolkit.Models.Simulations.MarketPathExtensions;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// Foreign discount process
  /// </summary>
  [Serializable]
  [ObjectLoggerEnabled]
  public class ForeignDiscountProcess : IProcess
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(ForeignDiscountProcess));
    [ObjectLogger(Name = "ForeignDiscountSimulation", Description = "Simulated Foreign Discount Forward Rates and Fx Rate for each path and simulation date", Category = "Simulation")]
    private static readonly IObjectLogger BinaryLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(ForeignDiscountProcess));

    /// <summary>
    /// Constructor
    /// </summary>
    public ForeignDiscountProcess() { }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="foreignDiscountProcessCalibration">foreign discount process calibration</param>
    /// <param name="fxProcessCalibration">fx process calibration</param>
    /// <param name="domesticDiscountProcess">domestic discount process</param>
    public ForeignDiscountProcess(DiscountProcessCalibration foreignDiscountProcessCalibration, FxProcessCalibration fxProcessCalibration, DomesticDiscountProcess domesticDiscountProcess)
    {
      AsOf = domesticDiscountProcess.AsOf;
      PathData = new Dictionary<int, IntPtr>();
      _simulator = new Lazy<Simulator>(InitSimulator);

      WeinerProcess = domesticDiscountProcess.WeinerProcess;
      DomesticDiscountProcess = domesticDiscountProcess;
      DomesticCurrency = domesticDiscountProcess.DomesticCurrency;
      DomesticDiscountCurve = DomesticDiscountProcess.DiscountCurve; // fetch from here as it may have been bumped locally
      DomesticDiscountCalibration = domesticDiscountProcess.Calibration;
      ForeignDiscountCurve = foreignDiscountProcessCalibration.DiscountCurve;
      ForeignDiscountCalibration = foreignDiscountProcessCalibration;
      FxCurve = fxProcessCalibration.FxCurve;
      ForeignCurrency = ForeignDiscountCurve.Ccy;
      FxCalibration = fxProcessCalibration;
      Factorization = domesticDiscountProcess.Factorization; 
    }

    /// <summary>
    /// Help function. ToDataTable
    /// </summary>
    /// <returns></returns>
    public DataTable ToDataTable()
    {
      var dataTable = new DataTable(String.Format("{0}", ForeignDiscountCurve.Name));
      dataTable.Columns.Add("PathId", typeof(int));
      dataTable.Columns.Add("Tenor", typeof(string));

      foreach (var simulationDate in Simulator.SimulationDates)
      {
        dataTable.Columns.Add(simulationDate.ToString(), typeof(double));
      }
      for (int pathId = 0; pathId < PathCount; pathId++)
      {
        var pathData = PathDataView(pathId);
        var tenorDim = WeinerProcess.TenorDts.Length + 1; // +1 for FX Rate
        for (int tenorIdx = 0; tenorIdx < tenorDim; tenorIdx++)
        {
          // hard coded cutoff for the size of recorded datatable to prevent OutOfBoundsException when serializing
          // ideally the datatable should come no where near this number as it will be diffcult to analysis the reported table
          if (dataTable.Rows.Count >= 10000)
          {
            continue;
          }

          string tenor = "FX Rate";
          if (tenorIdx < tenorDim - 1)
            tenor = WeinerProcess.TenorDts[tenorIdx].ToString();
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
    /// Get data from a path.
    /// </summary>
    /// <param name="i">path id</param>
    /// <returns></returns>
    public IList<double> PathDataView(int i)
    {
      var ptr = GetPathData(i);
      return NativeUtil.DoubleArray(ptr, BlockSize / sizeof(double), null);
    }

    /// <summary>
    /// Get path data
    /// </summary>
    /// <param name="pathId">path id</param>
    /// <returns></returns>
    public IntPtr GetPathData(int pathId)
    {
      Logger.VerboseFormat("Getting Foreign Discount Path Data for discount curve {0} path {1}.", ForeignDiscountCurve.Name, pathId);
      if (PathData == null)
      {
        Logger.ErrorFormat("PathData is null in call to GetPathData. Foreign Discount Curve {0} path {1}. Likely object is being accessed after already Disposed.", ForeignDiscountCurve.Name, pathId);
        PathData = new Dictionary<int, IntPtr>();
      }

      if (PathData.ContainsKey(pathId))
        return PathData[pathId];
      Logger.VerboseFormat("Allocating {0} byte memory block for Foreign Discount Simulation Data. Discount Curve {1} path {2}", BlockSize, ForeignDiscountCurve.Name, pathId);
      var pathData = UnmanagedMemory.Allocate(BlockSize);
      if (Simulator == null)
      {
        Logger.ErrorFormat("Call to ForeignDiscountSimulation.GetPathData(), Discount Curve {0} path {1}, null Simulator", ForeignDiscountCurve.Name, pathId);
        throw new ToolkitException("Null Simulator in GetPathData");
      }
      using (var path = Simulator.GetSimulatedPath())
      {
        if (path == null)
        {
          Logger.ErrorFormat("Null path returned by Simulator.GetSimulatedPath(). Foreign Discount Simulation {0} path {1}", ForeignDiscountCurve.Name, pathId);
          throw new ToolkitException("Null path returned by Simulator.GetSimulatedPath().");
        }

        path.SetWeinerIncrements(WeinerProcess.GetPathData(pathId));
        path.SetRates(0, DomesticDiscountProcess.GetPathData(pathId));
        path.SetRates(1, pathData);
        path.GetRates(1);
      }
      PathData[pathId] = pathData;

      // Log Simulator on final pass
      if (BinaryLogger.IsObjectLoggingEnabled && pathId == PathCount - 1)
      {
        var binaryLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(BinaryLogger, System.Reflection.MethodBase.GetCurrentMethod(), ForeignDiscountCurve.Name);
        binaryLogAggregator.Append(typeof(ForeignDiscountProcess), ForeignDiscountCurve.Name, AppenderUtil.DataTableToDataSet(ToDataTable())).Log();
      }

      if (pathId == PathCount - 1)
        LogTable(ToDataTable());
      return pathData;
    }

    private void LogTable(DataTable dt)
    {
      IEnumerable<string> columnNames = dt.Columns.Cast<DataColumn>().
                                        Select(column => column.ColumnName);
      Logger.Debug(string.Join(",", columnNames));
      foreach (DataRow row in dt.Rows)
      {
        IEnumerable<string> fields = row.ItemArray.Select(field => field.ToString());
        Logger.Debug(string.Join(",", fields));
      }
    }

    /// <summary>
    /// Block size
    /// </summary>
    public int BlockSize
    {
      get { return Simulator.SimulationDates.Length * (WeinerProcess.TenorDts.Length + 1) * sizeof(double); }
    }
    
    /// <summary>
    /// Asof Date
    /// </summary>
    public Dt AsOf { get; private set; }

    /// <summary>
    /// path count
    /// </summary>
    public int PathCount => WeinerProcess.PathCount;

    /// <summary>
    /// simulation dates
    /// </summary>
    public Dt[] SimulationDts => WeinerProcess.SimulationDts; 

    /// <summary>
    /// Quasi Monte Carlo
    /// </summary>
    public bool QuasiMonteCarlo => WeinerProcess.QuasiMonteCarlo;

    /// <summary>
    /// Domestic discount curve
    /// </summary>
    public DiscountCurve DomesticDiscountCurve { get; set; }

    /// <summary>
    /// Foreign discount curve
    /// </summary>
    public DiscountCurve ForeignDiscountCurve { get; set; }
    
    /// <summary>
    /// Domestic currency
    /// </summary>
    public Currency DomesticCurrency { get; set; }

    /// <summary>
    /// Foreign currency
    /// </summary>
    public Currency ForeignCurrency { get; set; }

    /// <summary>
    /// Fx curve
    /// </summary>
    public FxCurve FxCurve { get; set; }

    /// <summary>
    /// Fx rate
    /// </summary>
    public FxRate FxRate
    {
      get
      {
        if (_fxRate == null)
          _fxRate = FxCurve.SpotFxRate;
        return _fxRate;
      }
      set { _fxRate = value; }
    }

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
    /// Foreign discount calibration
    /// </summary>
    public DiscountProcessCalibration ForeignDiscountCalibration { get; private set; }

    /// <summary>
    /// Domestic discount calibration
    /// </summary>
    public DiscountProcessCalibration DomesticDiscountCalibration { get; private set; }

    /// <summary>
    /// Fx calibration
    /// </summary>
    public FxProcessCalibration FxCalibration { get; private set; }

    /// <summary>
    /// Factorization
    /// </summary>
    public MatrixFactorization Factorization { get; private set; }

    /// <summary>
    /// Volatility curves
    /// </summary>
    public VolatilityCurve[] Volatilities
    {
      get
      {
        if (_volatilities == null)
        {
          _volatilities = ForeignDiscountCalibration.Volatilities;
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
          _factorLoadings = ForeignDiscountCalibration.FactorLoadings;
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

    /// <summary>
    /// Fx factor loadings
    /// </summary>
    public double[,] FxFactorLoadings
    {
      get
      {
        if (_fxFactorLoadings == null)
        {
          _fxFactorLoadings = FxCalibration.FactorLoadings;
        }
        return _fxFactorLoadings;
      }
      set { _fxFactorLoadings = value; }
    }

    /// <summary>
    /// Fx volatility curves
    /// </summary>
    public VolatilityCurve[] FxVolatilities
    {
      get
      {
        if (_fxVolatilities == null)
        {
          _fxVolatilities = FxCalibration.Volatilities;
        }
        return _fxVolatilities;
      }
      set { _fxVolatilities = value; }
    }
    private Simulator InitSimulator()
    {
      Logger.DebugFormat("InitSimulator() for ForeignDiscountProcess {0}{1}. {2}", ForeignDiscountCurve.Name, ForeignDiscountCurve.AsOf, FxRate);
      var factory = Simulator.CreateSimulatorFactory(PathCount, AsOf, WeinerProcess.SimulationDts, WeinerProcess.TenorDts, MarketFactorNames);
      factory.AddDomesticDiscount(DomesticDiscountCurve, DomesticDiscountProcess.Volatilities, DomesticDiscountProcess.FactorLoadings, true);
      factory.AddDiscount(ForeignDiscountCurve, Volatilities, FactorLoadings, FxRate, FxVolatilities, FxFactorLoadings, true);
      return factory.Simulator;
    }

    /// <summary>
    /// Dispose function
    /// </summary>
    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose function
    /// </summary>
    /// <param name="disposing">bool type</param>
    public void Dispose(bool disposing)
    {
      Logger.DebugFormat("Disposing ForeignDiscountProcess. Discount Curve {0}. disposing = {1}",
        SafeGetCurveName(ForeignDiscountCurve, !disposing), disposing);

      if (PathData != null)
      {
        foreach (var intPtr in PathData.Values)
        {
          if (intPtr != IntPtr.Zero)
            Marshal.FreeCoTaskMem(intPtr);
        }
        PathData.Clear();
        PathData = null;
      }
      if (_simulator != null && _simulator.IsValueCreated && disposing)
        _simulator.Value.Dispose();
    }

    /// <summary>
    /// Destructor
    /// </summary>
    ~ForeignDiscountProcess()
    {
      Dispose(false);
    }


    [OnDeserialized, AfterFieldsCloned]
    private void OnClone(StreamingContext context)
    {
      // reinitialize path data after clone or deserialize event
      _pathData = new Dictionary<int, IntPtr>();
    }


    //Lazy<T> implements double checked locking to make initialization thread safe
    private readonly Lazy<Simulator> _simulator;
    private FxRate _fxRate;
    private VolatilityCurve[] _volatilities;
    private VolatilityCurve[] _fxVolatilities;
    private double[,] _factorLoadings;
    private double[,] _fxFactorLoadings;
    private string[] _marketFactorNames;
    [NoClone, NonSerialized]
    private Dictionary<int, IntPtr> _pathData;
    
  }
}