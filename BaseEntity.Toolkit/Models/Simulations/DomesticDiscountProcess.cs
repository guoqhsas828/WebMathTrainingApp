using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using log4net;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using static BaseEntity.Toolkit.Models.Simulations.MarketPathExtensions;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// Domestic discount process
  /// </summary>
  [Serializable]
  [ObjectLoggerEnabled]
  public class DomesticDiscountProcess : IProcess
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(DomesticDiscountProcess));
    [ObjectLogger(Name = "DomesticDiscountSimulation", Description ="Simulated Domestic Discount Forward Rates for each path and simulation date", Category = "Simulation")]
    private static readonly IObjectLogger BinaryLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(DomesticDiscountProcess));

    /// <summary>
    /// constructor
    /// </summary>
    public DomesticDiscountProcess() { }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="weinerProcess">weiner process</param>
    /// <param name="calibration">calibration</param>
    /// <param name="reportingDts">reporting dates</param>
    public DomesticDiscountProcess(WeinerProcess weinerProcess, DiscountProcessCalibration calibration, Dt[] reportingDts = null)
    {
      AsOf = weinerProcess.AsOf;
      PathData = new Dictionary<int, IntPtr>();
      ReportingDiscounts = new ConcurrentDictionary<int, IList<double>>();
      SimulatedDiscounts = new ConcurrentDictionary<int, IList<double>>();
      _simulator = new Lazy<Simulator>(InitSimulator);
      Factorization = weinerProcess.Factorization;
      Calibration = calibration; 
      WeinerProcess = weinerProcess;
      DiscountCurve = Calibration.DiscountCurve;
      DomesticCurrency = DiscountCurve.Ccy;
      _reportingDates = reportingDts; 
      _reportingDateDiscountDataPointer = new Lazy<IntPtr>(InitReportingDtDiscounts);
      _simulationDateDiscountDataPointer = new Lazy<IntPtr>(InitSimDtDiscounts);
    }

    /// <summary>
    /// Help function. ToDataTable
    /// </summary>
    /// <returns></returns>
    public DataTable ToDataTable()
    {
      var dataTable = new DataTable(DiscountCurve.Name);
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
          if (tenorIdx < tenorDim - 1)
          {
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
      }
      return dataTable;
    }

    /// <summary>
    /// Get Data from a path
    /// </summary>
    /// <param name="i">path number</param>
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
      Logger.VerboseFormat("Getting Domestic Discount Path Data for discount curve {0} path {1}.", DiscountCurve.Name, pathId);
		  
      if (PathData == null)
		  {
        Logger.ErrorFormat("PathData is null in call to GetPathData. Domestic Discount Curve {0} path {1}. Likely object is being accessed after already Disposed.", DiscountCurve.Name, pathId);
        PathData = new Dictionary<int, IntPtr>();
		  }
      if (PathData.ContainsKey(pathId))
        return PathData[pathId];
      Logger.VerboseFormat("Allocating {0} byte memory block for Domestic Discount Simulation Data. Discount Curve {1} path {2}", BlockSize, DiscountCurve.Name, pathId);
      var pathData = UnmanagedMemory.Allocate(BlockSize);
      if (Simulator == null)
      {
        Logger.ErrorFormat("Call to DomesticDiscountSimulation.GetPathData(), Discount Curve {0} path {1}, null Simulator", DiscountCurve.Name, pathId);
        throw new ToolkitException("Null Simulator in GetPathData");
      }
      using (var path = Simulator.GetSimulatedPath())
      {
        if (path == null)
        {
          Logger.ErrorFormat("Null path returned by Simulator.GetSimulatedPath(). Domestic Discount Simulation {0} path {1}", DiscountCurve.Name, pathId);
          throw new ToolkitException("Null path returned by Simulator.GetSimulatedPath().");
        }
        path.SetWeinerIncrements(WeinerProcess.GetPathData(pathId));
				path.SetRates(0,pathData);
        path.GetRates(0);
      }
      PathData[pathId] = pathData;

      // Log Simulator on final pass
      if (BinaryLogger.IsObjectLoggingEnabled && pathId == PathCount - 1)
      {
        var binaryLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(BinaryLogger, System.Reflection.MethodBase.GetCurrentMethod(), DiscountCurve.Name);
        binaryLogAggregator.Append(typeof(DomesticDiscountProcess), DiscountCurve.Name, AppenderUtil.DataTableToDataSet(ToDataTable())).Log();
      }

      return pathData;
    }

    /// <summary>
    /// Get reporting discount factors
    /// </summary>
    /// <param name="pathId">path id</param>
    /// <returns></returns>
    public IList<double> GetReportingDiscountFactors(int pathId)
    {
      IList<double> returnValue = null;
      if (_reportingDates == null)
      {
        _reportingDates = WeinerProcess.SimulationDts;
        Logger.VerboseFormat("Interpolating discount factors for Reporting Dates {0}", String.Join(",", _reportingDates));
      }
      if (_conformedDiscountCurve == null)
      {
        if (Simulator == null)
        {
          Logger.ErrorFormat("Call to DomesticDiscountSimulation.GetReportingDiscountFactors(), Discount Curve {0} path {1}, null Simulator", DiscountCurve.Name, pathId);
          throw new ToolkitException("Null Simulator in GetReportingDiscountFactors");
        }
        _conformedDiscountCurve = Simulator.GetConformedDiscountCurve(DiscountCurve);
      }
      int pathSize = _reportingDates.Length;

      if (ReportingDiscounts == null)
      {
        Logger.ErrorFormat("ReportingDiscounts is null in call to GetReportingDiscountFactors. Domestic Discount Curve {0} path {1}. Likely object is being accessed after already Disposed.", DiscountCurve.Name, pathId);
        ReportingDiscounts = new ConcurrentDictionary<int, IList<double>>();
      }

      if (ReportingDiscounts.ContainsKey(pathId))
      {
        ReportingDiscounts.TryGetValue(pathId, out returnValue);
        return returnValue;
      }
      if (_reportingDateDiscountData == IntPtr.Zero)
      {
        // allocate one contiguous block of mem for all paths
        int totalSize = pathSize * PathCount * sizeof(double);
        Logger.VerboseFormat("Allocating {0} byte memory block for Reporting Date Discount Factors. Discount Curve {1}", totalSize, DiscountCurve.Name);
        _reportingDateDiscountData = UnmanagedMemory.Allocate(totalSize);
      }
      // use ptr arithmetic to move IntPtr to start of current path
      var currentPathPtr = _reportingDateDiscountData + (pathSize * pathId * sizeof(double));
      if (Simulator == null)
      {
        Logger.ErrorFormat("Null Simulator in call to DomesticDiscountSimulation.GetReportingDiscountFactors(), Discount Curve {0} path {1}.", DiscountCurve.Name, pathId);
        throw new ToolkitException("Null Simulator in GetReportingDiscountFactors");
      }
      using (var path = Simulator.GetSimulatedPath())
      {
        if (path == null)
        {
          Logger.ErrorFormat("Null path returned by Simulator.GetSimulatedPath(). Domestic Discount Simulation {0} path {1}", DiscountCurve.Name, pathId);
          throw new ToolkitException("Null path returned by Simulator.GetSimulatedPath().");
        }
     
        path.SetWeinerIncrements(WeinerProcess.GetPathData(pathId));
        path.SetRates(0, GetPathData(pathId));
        Logger.VerboseFormat("Evolving Discount Curve {0} along {1} dates. Retrieving discount factors.", DiscountCurve.Name, _reportingDates.Length);
        path.EvolveInterpolatedDiscountFactors(Simulator, _conformedDiscountCurve, _reportingDates, currentPathPtr);
      }
      ReportingDiscounts.TryAdd(pathId, NativeUtil.DoubleArray(currentPathPtr, pathSize, null));
      ReportingDiscounts.TryGetValue(pathId, out returnValue);
      return returnValue;
    }

    private Dt[] SimulationTimeGrid { get; set; }

    /// <summary>
    /// Get simulated discount factor
    /// </summary>
    /// <param name="pathId">path id</param>
    /// <returns></returns>
    public IList<double> GetSimulatedDiscountFactors(int pathId)
    {
      IList<double> returnValue = null;
      
      if (SimulatedDiscounts == null)
      {
        Logger.ErrorFormat("SimulatedDiscounts is null in call to GetSimulatedDiscountFactors. Domestic Discount Curve {0} path {1}. Likely object is being accessed after already Disposed.", DiscountCurve.Name, pathId);
        SimulatedDiscounts = new ConcurrentDictionary<int, IList<double>>();
      }

      if (SimulatedDiscounts.ContainsKey(pathId))
      {
        SimulatedDiscounts.TryGetValue(pathId, out returnValue);
        return returnValue;
      }

      if (SimulationTimeGrid == null)
        SimulationTimeGrid = WeinerProcess.SimulationDts;
      var simulationDts = SimulationTimeGrid;
      if (Logger.IsVerboseEnabled())
        Logger.VerboseFormat("Retrieving discount factors for Simulation Dates {0}", String.Join(",", simulationDts));

      if (_conformedDiscountCurve == null)
      {
        if (Simulator == null)
        {
          Logger.ErrorFormat("Call to DomesticDiscountSimulation.GetSimulatedDiscountFactors(), Discount Curve {0} path {1}, null Simulator", DiscountCurve.Name, pathId);
          throw new ToolkitException("Null Simulator in GetSimulatedDiscountFactors");
        }

        _conformedDiscountCurve = Simulator.GetConformedDiscountCurve(DiscountCurve);
      }
      int pathSize = simulationDts.Length;


      if (_simulationDateDiscountData == IntPtr.Zero)
      {
        // allocate one contiguous block of mem for all paths
        int totalSize = pathSize * PathCount * sizeof(double);
        Logger.VerboseFormat("Allocating {0} byte memory block for Simulation Date Discount Factors. Discount Curve {1}", totalSize, DiscountCurve.Name);
        _simulationDateDiscountData = UnmanagedMemory.Allocate(totalSize);
      }
      // use ptr arithmetic to move IntPtr to start of current path
      var currentPathPtr = _simulationDateDiscountData + (pathSize * pathId * sizeof(double));
      if (Simulator == null)
      {
        Logger.ErrorFormat("Null Simulator in call to DomesticDiscountSimulation.GetSimulatedDiscountFactors(), Discount Curve {0} path {1}.", DiscountCurve.Name, pathId);
        throw new ToolkitException("Null Simulator in GetSimulatedDiscountFactors");
      }
      using (var path = Simulator.GetSimulatedPath())
      {
        if (path == null)
        {
          Logger.ErrorFormat("Null path returned by Simulator.GetSimulatedPath(). Domestic Discount Simulation {0} path {1}", DiscountCurve.Name, pathId);
          throw new ToolkitException("Null path returned by Simulator.GetSimulatedPath().");
        }

        path.SetWeinerIncrements(WeinerProcess.GetPathData(pathId));
        path.SetRates(0, GetPathData(pathId));
        Logger.VerboseFormat("Evolving Discount Curve {0} along {1} dates. Retrieving discount factors.", DiscountCurve.Name, simulationDts.Length);
        // should not actually use BB Interp as all dates are simulation dates
        path.EvolveInterpolatedDiscountFactors(Simulator, _conformedDiscountCurve, simulationDts, currentPathPtr);
      }
      SimulatedDiscounts.TryAdd(pathId, NativeUtil.DoubleArray(currentPathPtr, pathSize, null));
      SimulatedDiscounts.TryGetValue(pathId, out returnValue);
      return returnValue;
    }

    /// <summary>
    /// Block size
    /// </summary>
	  public int BlockSize
	  {
			get { return Simulator.SimulationDates.Length * (WeinerProcess.TenorDts.Length + 1) * sizeof(double);  }
	  }

    /// <summary>
    /// Asof date
    /// </summary>
		public Dt AsOf { get; private set; }

    /// <summary>
    /// path count
    /// </summary>
    public int PathCount => WeinerProcess.PathCount;

    /// <summary>
    /// Simulation dates
    /// </summary>
    public Dt[] SimulationDts => WeinerProcess.SimulationDts;

    /// <summary>
    /// Simulation environment name
    /// </summary>
    public string SimulationEnvironmentName { get; private set; }

    /// <summary>
    /// Quasi Monte Carlo or not
    /// </summary>
    public bool QuasiMonteCarlo { get { return WeinerProcess.QuasiMonteCarlo; } }

    /// <summary>
    /// Rng type
    /// </summary>
    public MultiStreamRng.Type RngType { get { return QuasiMonteCarlo ? MultiStreamRng.Type.Sobol : MultiStreamRng.Type.MersenneTwister; } }

    /// <summary>
    /// Random number generator
    /// </summary>
    public MultiStreamRng RandomNumberGenerator
    {
      get
      {
        if (_rng == null)
        {
          _rng = MultiStreamRng.Create(RngType, Simulator.Dimension, Simulator.SimulationTimeGrid);
        }
        return _rng;
      }
    }
    
    /// <summary>
    /// Discount curve
    /// </summary>
	  public DiscountCurve DiscountCurve { get; set; }

    /// <summary>
    /// Domestic Currency
    /// </summary>
	  public Currency DomesticCurrency { get; set; }

    /// <summary>
    /// Factorization
    /// </summary>
    public MatrixFactorization Factorization { get; private set; }

    /// <summary>
    /// Discount process calibration
    /// </summary>
    public DiscountProcessCalibration Calibration { get; private set; }

    private Dictionary<int, IntPtr> PathData
    {
      get { return _pathData; }
      set { _pathData = value; }
    }

    private ConcurrentDictionary<int, IList<double>> ReportingDiscounts { get; set; }

    private ConcurrentDictionary<int, IList<double>> SimulatedDiscounts { get; set; }

    /// <summary>
    /// Simulator
    /// </summary>
    public Simulator Simulator
    {
      get { return _simulator.Value;  }
    }

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

    /// <summary>
    /// Weiner process
    /// </summary>
    public WeinerProcess WeinerProcess { get; private set; }

    /// <summary>
    /// Reporting dates
    /// </summary>
    public Dt[] ReportingDates
    {
      get { return _reportingDates; }
    }

    /// <summary>
    /// Discount factors for each simulation date and path are stored in a single constiguous block of unmanaged memory. This pointer references the beginning of that block. 
    /// </summary>
    public IntPtr SimulationDateDiscountDataPointer => _simulationDateDiscountDataPointer.Value;

    /// <summary>
    /// Discount factors for each reporting date and path are stored in a single constiguous block of unmanaged memory. This pointer references the beginning of that block. 
    /// </summary>
    public IntPtr ReportingDateDiscountDataPointer => _reportingDateDiscountDataPointer.Value;

    private Simulator InitSimulator()
	  {
      Logger.DebugFormat("InitSimulator() for DomesticDiscountProcess {0}{1}", DiscountCurve.Name, DiscountCurve.AsOf);
      var factory = Simulator.CreateSimulatorFactory(PathCount, AsOf, WeinerProcess.SimulationDts, WeinerProcess.TenorDts, MarketFactorNames);
      factory.AddDomesticDiscount(DiscountCurve, Volatilities, FactorLoadings, true);
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
    /// <param name="disposing">boolean type to indicate dispose</param>
    public void Dispose(bool disposing)
    {
      var name = SafeGetCurveName(DiscountCurve, !disposing);
      Logger.DebugFormat("Disposing DomesticDiscountProcess. Discount Curve {0}. disposing = {1}", name, disposing);
      Clear();
      PathData = null;
      ReportingDiscounts = null;
      SimulatedDiscounts = null;
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

      if (ReportingDiscounts != null)
      {
        if (_reportingDateDiscountData != IntPtr.Zero)
          Marshal.FreeCoTaskMem(_reportingDateDiscountData);
        ReportingDiscounts.Clear();
      }

      if (SimulatedDiscounts != null)
      {
        if (_simulationDateDiscountData != IntPtr.Zero)
          Marshal.FreeCoTaskMem(_simulationDateDiscountData);
        SimulatedDiscounts.Clear();
      }
    }

    /// <summary>
    /// Destructor
    /// </summary>
		~DomesticDiscountProcess()
		{
			Dispose(false);
		}

    [OnDeserialized, AfterFieldsCloned]
    private void OnClone(StreamingContext context)
    {
      // reinitialize path data after clone or deserialize event
      _pathData = new Dictionary<int, IntPtr>();
    }

    private IntPtr InitSimDtDiscounts()
    {
      // make sure data has been filled along every path
      for (int p = 0; p < PathCount; p++)
      {
        if (!SimulatedDiscounts.ContainsKey(p))
          GetSimulatedDiscountFactors(p);
      }
      return _simulationDateDiscountData;
    }

    private IntPtr InitReportingDtDiscounts()
    {
      // make sure data has been filled along every path
      for (int p = 0; p < PathCount; p++)
      {
        if (!ReportingDiscounts.ContainsKey(p))
          GetReportingDiscountFactors(p);
      }
      return _reportingDateDiscountData;
    }

    private readonly Lazy<Simulator> _simulator;
    private MultiStreamRng _rng;
    private Action<IntPtr, byte, int> _memSetAction;
    private IntPtr _reportingDateDiscountData;
    private IntPtr _simulationDateDiscountData;
    private Dt[] _reportingDates;
    private DiscountCurve _conformedDiscountCurve;
    [NoClone, NonSerialized]private Dictionary<int, IntPtr> _pathData;
    private double[,] _factorLoadings;
    private string[] _marketFactorNames;
    private VolatilityCurve[] _volatilities;
    private Lazy<IntPtr> _reportingDateDiscountDataPointer;
    private Lazy<IntPtr> _simulationDateDiscountDataPointer;
  }

  /// <summary>
  /// Class of MarketPathExtensions
  /// </summary>
  public static class MarketPathExtensions
  {
    internal static string SafeGetCurveName(Curve curve, bool isFinalizing)
    {
      if (isFinalizing && curve != null && !curve.NativeCurve.swigCMemOwn)
      {
        return "[Disposed]";
      }
      return curve?.Name;
    }

    internal static void EvolveInterpolatedDiscountFactors(this SimulatedPath path, Simulator sim, DiscountCurve curve, Dt[] dts, IntPtr ptr)
    {
      var fracs = dts.Select(dt => Dt.FractDiff(sim.AsOf, dt)).ToArray();
      var idxs = dts.Select(dt => Array.BinarySearch(sim.SimulationDates, dt)).ToArray();
      path.EvolveInterpolatedDiscounts(0, dts, fracs, idxs, curve, ptr);
    }
    
    internal static void EvolveInterpolatedRnDensities(this SimulatedPath path, Simulator sim, Dt[] dts, IntPtr ptr)
    {
      var fracs = new List<double>();
      fracs.Add(0);
      fracs.AddRange(dts.Select(dt => Dt.FractDiff(sim.AsOf, dt)));
      var idxs = new List<int>();
      idxs.Add(0);
      idxs.AddRange(dts.Select(dt =>
      {
        var i = Array.BinarySearch(sim.SimulationDates, dt);
        return i < 0 ? ~i : i;
      }));
      // for each exposure date in dts we want rn between dts[t-1] and dts[t] for t = 1,...,n 
      // and for t = 0, we want rn between t_0 and dts[0] 
      // the native function interpolates rn between fracs[i] and fracs[i+1] for i = 0,...,n-1
      // so we add an extra element at fracs[0] for rn between t_0 and dts[0]
      path.EvolveInterpolatedRnDensities(fracs.ToArray(), idxs.ToArray(), ptr);
    }

    internal static DiscountCurve GetConformedDiscountCurve(this Simulator sim, DiscountCurve sourceCurve)
    {
      var curve = sourceCurve.Clone() as DiscountCurve ?? new DiscountCurve(sourceCurve.AsOf);
      curve.Clear();
      ResetInterp(curve);
      var y = new double[sim.Tenors.Length];
      curve.Add(sim.Tenors, y);
      return curve; 
    }

    //Other interpolations are too time consuming
    internal static void ResetInterp(CalibratedCurve curve)
    {
      var method = InterpMethod.Custom;
      try
      {
        method = curve.InterpMethod;
      }
      catch (Exception)
      { }
      if (method == InterpMethod.Weighted || method == InterpMethod.Linear)
        return;
      Extrap lower = new Const();
      Extrap upper = new Smooth();
      curve.Interp = new Linear(upper, lower);
    }

  }
}