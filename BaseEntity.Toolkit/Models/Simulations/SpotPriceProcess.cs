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
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// Spotprice process
  /// </summary>
  [Serializable]
  [ObjectLoggerEnabled]
  public class SpotPriceProcess : IProcess
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(SpotPriceProcess));
    [ObjectLogger(Name = "SpotPriceSimulation", Description = "Simulated Spot Price for each path and simulation date", Category = "Simulation")]
    private static readonly IObjectLogger BinaryLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(SpotPriceProcess));

    /// <summary>
    /// Constructor
    /// </summary>
    public SpotPriceProcess () { }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="calibration"></param>
    /// <param name="domesticDiscountProcess"></param>
    /// <param name="foreignDiscountProcess"></param>
    public SpotPriceProcess(SpotProcessCalibration calibration, DomesticDiscountProcess domesticDiscountProcess, ForeignDiscountProcess foreignDiscountProcess = null)
    {
      AsOf = domesticDiscountProcess.AsOf;
      PathData = new Dictionary<int, IntPtr>();
      
      WeinerProcess = domesticDiscountProcess.WeinerProcess;
      DomesticDiscountProcess = domesticDiscountProcess;
      DomesticCurrency = domesticDiscountProcess.DomesticCurrency;
      Calibration = calibration;
      SpotCurve = Calibration.ForwardCurve;
      Currency = SpotCurve.Ccy; 

      if (Currency != DomesticCurrency && foreignDiscountProcess != null)
      {
        ForeignDiscountProcess = foreignDiscountProcess;
      }
      Factorization = domesticDiscountProcess.Factorization;
      _simulator = new Lazy<Simulator>(InitSimulator);
    }
    
    /// <summary>
    /// Get data from a path
    /// </summary>
    /// <param name="pathId">path id</param>
    /// <returns></returns>
    public IntPtr GetPathData(int pathId)
    {
      Logger.VerboseFormat("Getting Spot Price Simulation Path Data for {0} path {1}.", SpotCurve.Name, pathId);
      if (PathData == null)
      {
        Logger.ErrorFormat("PathData is null in call to GetPathData. Domestic Discount Curve {0} path {1}. Likely object is being accessed after already Disposed.", SpotCurve.Name, pathId);
        PathData = new Dictionary<int, IntPtr>();
      }
      if (PathData.ContainsKey(pathId))
        return PathData[pathId];
      Logger.VerboseFormat("Allocating {0} byte memory block for Spot Price Simulation Data {1} path {2}", BlockSize, SpotCurve.Name, pathId);
      var pathData = UnmanagedMemory.Allocate(BlockSize);
      if (Simulator == null)
      {
        Logger.ErrorFormat("Call to SpotPriceSimulation.GetPathData(), Spot {0} path {1}, null Simulator", SpotCurve.Name, pathId);
        throw new ToolkitException("Null Simulator in GetPathData");
      }
      using (var path = Simulator.GetSimulatedPath())
      {
        if (path == null)
        {
          Logger.ErrorFormat("Null path returned by Simulator.GetSimulatedPath(). SpotPriceSimulation {0} path {1}", SpotCurve.Name, pathId);
          throw new ToolkitException("Null path returned by Simulator.GetSimulatedPath().");
        }
        path.SetWeinerIncrements(WeinerProcess.GetPathData(pathId));
        path.SetRates(0, DomesticDiscountProcess.GetPathData(pathId));
        if (Currency != DomesticCurrency && ForeignDiscountProcess != null)
        {
          path.SetRates(1, ForeignDiscountProcess.GetPathData(pathId));
        }
				path.SetSpot(0, pathData);
        path.GetSpot(0);
      }
      PathData[pathId] = pathData;

      // Log Simulator on final pass
      if (BinaryLogger.IsObjectLoggingEnabled && pathId == PathCount - 1)
      {
        var binaryLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(BinaryLogger, System.Reflection.MethodBase.GetCurrentMethod());
        binaryLogAggregator.Append(typeof(SpotPriceProcess), SpotCurve.Name, AppenderUtil.DataTableToDataSet(ToDataTable())).Log();
      }

      return pathData;
    }

    /// <summary>
    /// Help function. ToDataTable
    /// </summary>
    /// <returns></returns>
    public DataTable ToDataTable()
    {
      var dataTable = new DataTable(String.Format("{0}", SpotCurve.Name));
      dataTable.Columns.Add("PathId", typeof(int));

      foreach (var simulationDate in Simulator.SimulationDates)
      {
        dataTable.Columns.Add(simulationDate.ToString(), typeof(double));
      }

      for (int pathId = 0; pathId < PathCount; pathId++)
      {
        // hard coded cutoff for the size of recorded datatable to prevent OutOfBoundsException when serializing
        // ideally the datatable should come no where near this number as it will be diffcult to analysis the reported table
        if (dataTable.Rows.Count >= 10000)
        {
          continue;
        }
        var pathData = PathDataView(pathId);
        var row = dataTable.NewRow();

        row["PathId"] = pathId;
        for (int dateIdx = 0; dateIdx < Simulator.SimulationDates.Length; dateIdx++)
        {
          // +1 for key col
          row[dateIdx + 1] = pathData[dateIdx];
        }
        dataTable.Rows.Add(row);
      }
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
      get { return sizeof(double) * Simulator.SimulationDates.Length;  }
    }

    /// <summary>
    /// Asof
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
    /// Boolean type. QuasiMonteCarlo
    /// </summary>
    public bool QuasiMonteCarlo { get { return WeinerProcess.QuasiMonteCarlo; } }

    /// <summary>
    /// Spot curve
    /// </summary>
    public ForwardPriceCurve SpotCurve { get; set; }

    /// <summary>
    /// Domestic currency
    /// </summary>
    public Currency DomesticCurrency { get; set; }

    /// <summary>
    /// Currency
    /// </summary>
    public Currency Currency { get; set; }

    /// <summary>
    /// Calibration
    /// </summary>
    public SpotProcessCalibration Calibration { get; private set; }

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
    /// Simulator
    /// </summary>
    public Simulator Simulator => _simulator.Value;
    
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
    /// MarketFactorNames
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
      var factory = Simulator.CreateSimulatorFactory(PathCount, AsOf, WeinerProcess.SimulationDts, WeinerProcess.TenorDts, MarketFactorNames);
      factory.AddDomesticDiscount(DomesticDiscountProcess.DiscountCurve, DomesticDiscountProcess.Volatilities, DomesticDiscountProcess.FactorLoadings, true);
      if (Currency != DomesticCurrency && ForeignDiscountProcess != null)
      {
        factory.AddDiscount(ForeignDiscountProcess.ForeignDiscountCurve, ForeignDiscountProcess.Volatilities, ForeignDiscountProcess.FactorLoadings, ForeignDiscountProcess.FxRate, ForeignDiscountProcess.FxVolatilities, ForeignDiscountProcess.FxFactorLoadings, true);
      }
      factory.AddSpot(SpotCurve.Spot, SpotCurve, Volatilities, FactorLoadings, SpotCurve.CarryRateAdjustment, SpotCurve.CarryCashflow.Select(d => new Tuple<Dt, double>(d.Item1, d.Item3)).ToArray(), true);
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
    /// <param name="disposing">boolean type</param>
		public void Dispose(bool disposing)
		{
   //   Logger.DebugFormat("Disposing SpotPriceSimulation {0}. disposing = {1}", SpotCurve != null ? SpotCurve.Name : null, disposing);
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
		~SpotPriceProcess()
		{
			Dispose(false);
		}

    [OnDeserialized, AfterFieldsCloned]
    private void OnClone(StreamingContext context)
    {
      // reinitialize path data after clone or deserialize event
      _pathData = new Dictionary<int, IntPtr>();
    }
    private Lazy<Simulator> _simulator;
    [NoClone, NonSerialized]
    private Dictionary<int, IntPtr> _pathData;
    private VolatilityCurve[] _volatilities;
    private double[,] _factorLoadings;
    private string[] _marketFactorNames;

  }
}