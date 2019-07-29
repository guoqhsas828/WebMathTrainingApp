using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using log4net;
using BaseEntity.Configuration;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// Radon Nikodym process
  /// </summary>
  [Serializable]
  [ObjectLoggerEnabled]
  public class RadonNikodymProcess :  IProcess
  {
    [ObjectLogger(Name = "RadonNikodymSimulation", Description = "Measure changes for each path and simulation date", Category = "Simulation")]
    private static readonly IObjectLogger BinaryLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(RadonNikodymProcess));
    [ObjectLogger(Name = "CounterpartyLevelCalibration", Description = "Input market curves and output Factorized Volatility System at the Counterparty Level", Category = "Calibration")]
    private static readonly IObjectLogger ObjectLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(RadonNikodymProcess), "CounterpartyLevelCalibration");
    
    private static readonly ILog Logger = LogManager.GetLogger(typeof(RadonNikodymProcess));

    /// <summary>
    /// Constructor
    /// </summary>
    public RadonNikodymProcess() { }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="survivalProcessCalibrators">survival process calibrations</param>
    /// <param name="recoveries">recoveries</param>
    /// <param name="domesticDiscountProcess">domestic discount process</param>
    public RadonNikodymProcess(SurvivalProcessCalibration[] survivalProcessCalibrators, double[] recoveries, DomesticDiscountProcess domesticDiscountProcess)
    {
      AsOf = domesticDiscountProcess.AsOf;
      PathData = new ConcurrentDictionary<int, IntPtr>();
      ReportingDtRadons = new Dictionary<int, IntPtr>();
      ExposureDtRadons = new Dictionary<int, IntPtr>();
      _simulationMarketEnvironment = new Lazy<Tuple<CCRMarketEnvironment, FactorLoadingCollection, VolatilityCollection>>(InitMarketEnvironment);

      WeinerProcess = domesticDiscountProcess.WeinerProcess;
      DomesticDiscountProcess = domesticDiscountProcess;
      DomesticCurrency = domesticDiscountProcess.DomesticCurrency;

      Calibrations = survivalProcessCalibrators; 

      _riskyPartiesSurvivalCurves = Calibrations.Select(c => c.SurvivalCurve).ToArray();
      RiskyPartiesRecoveryAssumptions = recoveries; 
      if (survivalProcessCalibrators.Length > 0)
      {
        CounterpartyCurve = survivalProcessCalibrators[0].SurvivalCurve;
        if (recoveries.Length > 0)
          CounterpartyRecovery = recoveries[0];
      }
      if (survivalProcessCalibrators.Length > 1)
      {
        OwnCurve = survivalProcessCalibrators[1].SurvivalCurve;
        if (recoveries.Length > 1)
          OwnRecovery = recoveries[1];
      }
      if (survivalProcessCalibrators.Length > 2)
      {
        BorrowingCurve = survivalProcessCalibrators[2].SurvivalCurve;
        if (recoveries.Length > 2)
          BorrowingRecovery = recoveries[2];
      }
      if (survivalProcessCalibrators.Length > 3)
      {
        LendingCurve = survivalProcessCalibrators[3].SurvivalCurve;
        if (recoveries.Length > 3)
          LendingRecovery = recoveries[3];
      }


      Factorization = domesticDiscountProcess.Factorization; 

      _simulator = new Lazy<Simulator>(InitSimulator);
      _simulationMarketEnvironment = new Lazy<Tuple<CCRMarketEnvironment, FactorLoadingCollection, VolatilityCollection>>(InitMarketEnvironment);
      _simDtRadons = new Lazy<IntPtr[]>(InitSimDtRadons);
      _exposureDtRadons = new Lazy<IntPtr[]>(InitExposureDtRadons);
      _reportingDtRadons = new Lazy<IntPtr[]>(InitReportingDtRadons);
    }

    /// <summary>
    /// Help function. ToDataTable
    /// </summary>
    /// <returns></returns>
    public DataTable ToDataTable()
    {
      var dataTable = new DataTable(String.Format("{0}", _counterpartyCurve.Name));
      dataTable.Columns.Add("PathId", typeof(int));
      dataTable.Columns.Add("MeasureChange", typeof(string));
      string[] measureChange = new[]
      {
        "Bilateral Cpty Dflt", 
        "Bilateral Own Dflt", 
        "Bilateral Survival", 
        "Cpty Spread", 
        "Own Spread", 
        "Unilateral Cpty Dflt", 
        "Unilateral Own Dflt",
        "Unilateral Survival", 
        "Borrow Spread", 
        "Lend Spread"
      };


      foreach (var simulationDate in Simulator.SimulationDates)
      {
        dataTable.Columns.Add(simulationDate.ToString(), typeof(double));
      }
      for (int pathId = 0; pathId < PathCount; pathId++)
      {
        var pathData = PathDataView(pathId);
        var dim = RnDimensionCount;
        for (int rnIdx = 0; rnIdx < dim; rnIdx++)
        {
          // hard coded cutoff for the size of recorded datatable to prevent OutOfBoundsException when serializing
          // ideally the datatable should come no where near this number as it will be diffcult to analysis the reported table
          if (dataTable.Rows.Count >= 10000)
          {
            continue;
          }
          var row = dataTable.NewRow();

          row["PathId"] = pathId;
          row["MeasureChange"] = measureChange[rnIdx];
          var dateCount = Simulator.SimulationDates.Length; 
          for (int dateIdx = 0; dateIdx < dateCount; dateIdx++)
          {
            // +2 for key cols
            row[dateIdx + 2] = pathData[rnIdx*dateCount+dateIdx];
          }
          dataTable.Rows.Add(row);
        }
      }
      return dataTable;
    }

    /// <summary>
    /// Get data from a path
    /// </summary>
    /// <param name="i">path id</param>
    /// <returns>double list</returns>
    public IList<double> PathDataView(int i)
    {
      var ptr = GetPathData(i);
      return NativeUtil.DoubleArray(ptr, BlockSize / sizeof(double), null);
    }
    
    /// <summary>
    /// Get data from a path
    /// </summary>
    /// <param name="pathId">path id</param>
    /// <returns>pointer</returns>
    public IntPtr GetPathData(int pathId)
    {
      Logger.VerboseFormat("Getting Radon Nik Path Data for counterparty {0} path {1}.", _counterpartyCurve.Name, pathId);
      if (PathData == null)
      {
        Logger.ErrorFormat("PathData is null in call to GetPathData. Counterparty {0} path {1}. Likely object is being accessed after already Disposed.", _counterpartyCurve.Name, pathId);
        PathData = new ConcurrentDictionary<int, IntPtr>();
      }

      if (PathData.ContainsKey(pathId))
      {
        var returnValue = IntPtr.Zero;
        PathData.TryGetValue(pathId, out returnValue);
        return returnValue;
      }

      Logger.VerboseFormat("Allocating {0} byte memory block for RN Data. Counterparty {1} path {2}", BlockSize, _counterpartyCurve.Name, pathId);
      var pathData = UnmanagedMemory.Allocate(BlockSize);
      if (Simulator == null)
      {
        Logger.ErrorFormat("Call to Counterparty {0} path {1}, null Simulator", _counterpartyCurve.Name, pathId);
        throw new ToolkitException("Null Simulator in GetPathData");
      }

      using (var path = Simulator.GetSimulatedPath())
      {
        if (path == null)
        {
          Logger.ErrorFormat("Null path returned by Simulator.GetSimulatedPath(). Counterparty {0} path {1}", _counterpartyCurve.Name, pathId);
          throw new ToolkitException("Null path returned by Simulator.GetSimulatedPath().");
        }

        path.SetWeinerIncrements(WeinerProcess.GetPathData(pathId));
        path.SetRates(0, DomesticDiscountProcess.GetPathData(pathId));
        path.SetRnDensities(pathData);
        path.GetRnDensities();
      }
      PathData.TryAdd(pathId, pathData);
      
      // Log Simulator on final pass
      if (BinaryLogger.IsObjectLoggingEnabled && pathId == PathCount - 1)
      {
        var binaryLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(BinaryLogger, System.Reflection.MethodBase.GetCurrentMethod(), _counterpartyCurve.Name);
        binaryLogAggregator.Append(typeof(RadonNikodymProcess), _counterpartyCurve.Name, AppenderUtil.DataTableToDataSet(ToDataTable())).Log();
      }

      return pathData;
    }

    /// <summary>
    /// Get reporting data from a path
    /// </summary>
    /// <param name="pathId">path id</param>
    /// <returns>double list</returns>
    public IList<double> GetReportingDtRadons(int pathId)
    {
      Logger.DebugFormat("Getting Radon Nik Path Data for counterparty {0} path {1}.", _counterpartyCurve.Name, pathId);
      if (ReportingDtRadons == null)
      {
        Logger.ErrorFormat("ReportingDtRadons is null in call to GetReportingDtRadons. Counterparty {0} path {1}. Likely object is being accessed after already Disposed.", _counterpartyCurve.Name, pathId);
        ReportingDtRadons = new Dictionary<int, IntPtr>();
      }

      if (_reportingDates == null)
      {
        _reportingDates = DomesticDiscountProcess.ReportingDates;
        Logger.DebugFormat("Interpolating RN Densities for Reporting Dates {0}", String.Join(",", _reportingDates));
      }

      if (ReportingDtRadons.ContainsKey(pathId))
        return NativeUtil.DoubleArray(ReportingDtRadons[pathId], ReportingBlockSize / sizeof(double), null); 

      Logger.DebugFormat("Allocating {0} byte memory block for interpolated RN Data. Counterparty {1} path {2}", ReportingBlockSize, _counterpartyCurve.Name, pathId);
      var pathData = UnmanagedMemory.Allocate(ReportingBlockSize);
      if (Simulator == null)
      {
        Logger.ErrorFormat("Call to Counterparty {0} path {1}, null Simulator", _counterpartyCurve.Name, pathId);
        throw new ToolkitException("Null Simulator in GetPathData");
      }

      using (var path = Simulator.GetSimulatedPath())
      {
        if (path == null)
        {
          Logger.ErrorFormat("Null path returned by Simulator.GetSimulatedPath(). Counterparty {0} path {1}", _counterpartyCurve.Name, pathId);
          throw new ToolkitException("Null path returned by Simulator.GetSimulatedPath().");
        }

        path.SetWeinerIncrements(WeinerProcess.GetPathData(pathId));
        path.SetRates(0, DomesticDiscountProcess.GetPathData(pathId));
        path.SetRnDensities(GetPathData(pathId));
        Logger.DebugFormat("Interping RN {0} along {1} dates. Retrieving densities.", _counterpartyCurve.Name, _reportingDates.Length);
        path.EvolveInterpolatedRnDensities(Simulator,_reportingDates, pathData);
        ReportingDtRadons[pathId] = pathData;
      }
      
      return NativeUtil.DoubleArray(pathData, ReportingBlockSize / sizeof(double), null);
    }

    /// <summary>
    /// Get exposure data from a path
    /// </summary>
    /// <param name="pathId">path id</param>
    /// <returns>double list</returns>
    public IList<double> GetExposureDtRadons(int pathId)
    {
      Logger.DebugFormat("Getting Radon Nik Path Data for counterparty {0} path {1}.", _counterpartyCurve.Name, pathId);
      if (ExposureDtRadons == null)
      {
        Logger.ErrorFormat("ExposureDtRadons is null in call to GetExposureDtRadons. Counterparty {0} path {1}. Likely object is being accessed after already Disposed.", _counterpartyCurve.Name, pathId);
        ExposureDtRadons = new Dictionary<int, IntPtr>();
      }

      if (ExposureDts == null)
      {
        throw new ToolkitException("Call to GetExposureDtRadons() but ExposureDts property is null.");
      }

      if (ExposureDtRadons.ContainsKey(pathId))
        return NativeUtil.DoubleArray(ExposureDtRadons[pathId], ExposureBlockSize / sizeof(double), null);

      Logger.DebugFormat("Allocating {0} byte memory block for interpolated RN Data. Counterparty {1} path {2}", ExposureBlockSize, _counterpartyCurve.Name, pathId);
      var pathData = UnmanagedMemory.Allocate(ExposureBlockSize);
      if (Simulator == null)
      {
        Logger.ErrorFormat("Call to Counterparty {0} path {1}, null Simulator", _counterpartyCurve.Name, pathId);
        throw new ToolkitException("Null Simulator in GetPathData");
      }

      using (var path = Simulator.GetSimulatedPath())
      {
        if (path == null)
        {
          Logger.ErrorFormat("Null path returned by Simulator.GetSimulatedPath(). Counterparty {0} path {1}", _counterpartyCurve.Name, pathId);
          throw new ToolkitException("Null path returned by Simulator.GetSimulatedPath().");
        }

        path.SetWeinerIncrements(WeinerProcess.GetPathData(pathId));
        path.SetRates(0, DomesticDiscountProcess.GetPathData(pathId));
        path.SetRnDensities(GetPathData(pathId));
        Logger.DebugFormat("Interping RN {0} along {1} dates. Retrieving densities.", _counterpartyCurve.Name, ExposureDts.Count);
        path.EvolveInterpolatedRnDensities(Simulator, ExposureDts.ToArray(), pathData);
        ExposureDtRadons[pathId] = pathData; 
      }

      return NativeUtil.DoubleArray(pathData, ExposureBlockSize / sizeof(double), null);
    }

    /// <summary>
    /// IntPtr to Rn densities for all paths
    /// </summary>
    public IntPtr[] SimulationDtRadonPointers => _simDtRadons.Value;


    /// <summary>
    /// IntPtr to Rn densities for all paths
    /// </summary>
    public IntPtr[] ExposureDtRadonPointers => _exposureDtRadons.Value;


    /// <summary>
    /// IntPtr to Rn densities for all paths
    /// </summary>
    public IntPtr[] ReportingDtRadonPointers => _reportingDtRadons.Value;

    /// <summary>
    /// Block size
    /// </summary>
    public int BlockSize
    {
      get { return sizeof(double) * Simulator.SimulationDates.Length * RnDimensionCount; }
    }

    /// <summary>
    /// reporting block size
    /// </summary>
    private int ReportingBlockSize
    {
      get { return sizeof(double) * _reportingDates.Length * RnDimensionCount; }
    }

    /// <summary>
    /// exposure block size
    /// </summary>
    private int ExposureBlockSize
    {
      get { return sizeof(double) * ExposureDts.Count * RnDimensionCount; }
    }

    /// <summary>
    /// Asof
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
    /// Boolean type. Quasi Monte Carlo.
    /// </summary>
    public bool QuasiMonteCarlo => WeinerProcess.QuasiMonteCarlo;

    /// <summary>
    /// Domestic currency
    /// </summary>
    public Currency DomesticCurrency { get; set; }

    /// <summary>
    /// Credit curve of Counterparty
    /// </summary>
    public SurvivalCurve CounterpartyCurve
    {
      get
      {
        return _counterpartyCurve;
      }
      set { _counterpartyCurve = value; }
    }

    /// <summary>
    /// Recovery Rate of Counterparty
    /// </summary>
    public double CounterpartyRecovery
    {
      get; set;
    }

    ///<summary>
    /// Credit curve of Booking Entity
    ///</summary>
    public SurvivalCurve OwnCurve
    {
      get
      {
        return _ownCurve;
      }
      set { _ownCurve = value; }
    }

    /// <summary>
    /// Recovery Rate of Booking Entity
    /// </summary>
    public double OwnRecovery
    {
      get; set;
    }

    ///<summary>
    /// Borrowing Credit curve of Booking Entity
    ///</summary>
    public SurvivalCurve BorrowingCurve
    {
      get
      {
        return _borrowingCurve;
      }
      set { _borrowingCurve = value; }
    }

    /// <summary>
    /// Recovery Rate of Borrowing Curve
    /// </summary>
    public double BorrowingRecovery
    {
      get; set;
    }

    ///<summary>
    /// Lending Credit curve of Booking Entity
    ///</summary>
    public SurvivalCurve LendingCurve
    {
      get
      {
        return _lendingCurve;
      }
      set { _lendingCurve = value; }
    }

    /// <summary>
    /// Recovery Rate of Lending Curve
    /// </summary>
    public double LendingRecovery
    {
      get; set;
    }
    
    private ConcurrentDictionary<int, IntPtr> PathData { get; set; }

    private Dictionary<int, IntPtr> ReportingDtRadons { get; set; }

    private Dictionary<int, IntPtr> ExposureDtRadons { get; set; }

    /// <summary>
    /// Exposure dates
    /// </summary>
    public IList<Dt> ExposureDts { get; set; }

    /// <summary>
    /// Factorization
    /// </summary>
    public MatrixFactorization Factorization{ get; private set; }

    /// <summary>
    /// Array of calibrations
    /// </summary>
    public SurvivalProcessCalibration[] Calibrations { get; private set; }

    /// <summary>
    /// Simulator
    /// </summary>
    public Simulator Simulator => _simulator.Value;

    /// <summary>
    /// Boolean type. Has counterparty vols or not.
    /// </summary>
    public bool HasCounterpartyVols => CounterpartyCurve != null && Calibrations != null && Calibrations.Length > 0 && Calibrations[0] != null && Calibrations[0].Volatilities != null && Calibrations[0].Volatilities.Length > 0 && Calibrations[0].Volatilities[0] != null;

    /// <summary>
    /// Boolean type. Has own vols or not
    /// </summary>
    public bool HasOwnVols => CounterpartyCurve != null && Calibrations != null && Calibrations.Length > 1 && Calibrations[1] != null && Calibrations[1].Volatilities != null && Calibrations[1].Volatilities.Length > 0 && Calibrations[1].Volatilities[0] != null;

    /// <summary>
    /// Boolean type. Has Borrowing vols or not
    /// </summary>
    public bool HasBorrowingVols => BorrowingCurve != null && Calibrations != null && Calibrations.Length > 2 && Calibrations[2] != null && Calibrations[2].Volatilities != null && Calibrations[2].Volatilities.Length > 0 && Calibrations[2].Volatilities[0] != null;

    /// <summary>
    /// Boolean type. Has lending vols or not.
    /// </summary>
    public bool HasLendingVols => LendingCurve != null && Calibrations != null && Calibrations.Length > 3 && Calibrations[3] != null && Calibrations[3].Volatilities != null && Calibrations[3].Volatilities.Length > 0 && Calibrations[3].Volatilities[0] != null;

    /// <summary>
    /// Boolean  type. Has domestic discount vols or not.
    /// </summary>
    public bool HasDomesticDiscountVols => DomesticDiscountProcess != null && DomesticDiscountProcess.Volatilities != null && DomesticDiscountProcess.Volatilities.Length > 0 && DomesticDiscountProcess.Volatilities[0] != null;

    /// <summary>
    /// Counterparty factor loadings
    /// </summary>
    public double[,] CounterpartyFactorLoadings
    {
      get
      {
        if (_counterpartyLoadings == null && HasCounterpartyVols)
        {
          _counterpartyLoadings = Calibrations[0].FactorLoadings;
        }
        return _counterpartyLoadings;
      }
      set { _counterpartyLoadings = value; }
    }

    /// <summary>
    /// Own factor loadings
    /// </summary>
    public double[,] OwnFactorLoadings
    {
      get
      {
        if (_ownLoadings == null && HasOwnVols)
        {
          _ownLoadings = Calibrations[1].FactorLoadings;
        }
        return _ownLoadings;
      }
      set { _ownLoadings = value; }
    }

    /// <summary>
    /// Borrowing factor loadings
    /// </summary>
    public double[,] BorrowingFactorLoadings
    {
      get
      {
        if (_borrowingLoadings == null && HasBorrowingVols)
        {
          _borrowingLoadings = Calibrations[2].FactorLoadings;
        }
        return _borrowingLoadings;
      }
      set { _borrowingLoadings = value; }
    }

    /// <summary>
    /// Lending factor loadings
    /// </summary>
    public double[,] LendingFactorLoadings
    {
      get
      {
        if (_lendingLoadings == null && HasLendingVols)
        {
          _lendingLoadings = Calibrations[3].FactorLoadings;
        }
        return _lendingLoadings;
      }
      set { _lendingLoadings = value; }
    }

    /// <summary>
    /// Counterparty volatility curves
    /// </summary>
    public VolatilityCurve[] CounterpartyVolatilityCurves
    {
      get
      {
        if (_counterpartyVolatilityCurves == null && HasCounterpartyVols)
          _counterpartyVolatilityCurves = Calibrations[0].Volatilities;
        return _counterpartyVolatilityCurves;
      }
      set { _counterpartyVolatilityCurves = value; }
    }

    /// <summary>
    /// Own volatility curves
    /// </summary>
    public VolatilityCurve[] OwnVolatilityCurves
    {
      get
      {
        if (_ownVolatilityCurves == null && HasOwnVols)
          _ownVolatilityCurves = Calibrations[1].Volatilities;
        return _ownVolatilityCurves;
      }
      set { _ownVolatilityCurves = value; }
    }

    /// <summary>
    /// Borrowing volatility curves
    /// </summary>
    public VolatilityCurve[] BorrowingVolatilityCurves
    {
      get
      {
        if (_borrowingVolatilityCurves == null && HasBorrowingVols)
          _borrowingVolatilityCurves = Calibrations[2].Volatilities;
        return _borrowingVolatilityCurves;
      }
      set { _borrowingVolatilityCurves = value; }
    }

    /// <summary>
    /// lending volatility curves
    /// </summary>
    public VolatilityCurve[] LendingVolatilityCurves
    {
      get
      {
        if (_lendingVolatilityCurves == null && HasLendingVols)
          _lendingVolatilityCurves = Calibrations[3].Volatilities;
        return _lendingVolatilityCurves;
      }
      set { _lendingVolatilityCurves = value; }
    }

    /// <summary>
    /// Market Factor names
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

    private void RetainCounterpartiesCalibrationData()
    {
      if (ObjectLogger.IsObjectLoggingEnabled)
      {
        var objectLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(ObjectLogger, System.Reflection.MethodBase.GetCurrentMethod(), _counterpartyCurve.Name);

        var fvs = new FactorizedVolatilitySystem(FactorLoadingCollection, VolatilityCollection);
        objectLogAggregator.Append("FactorizedVolatilitySystem", fvs);

        objectLogAggregator.Log();
      }
    }

    /// <summary>
    /// Function to retrieve a simulated path populated with cached simulation data
    /// </summary>
    public Func<int, SimulatedPath> GetPathFunc
    {
      get
      {
        var path = Simulator.GetSimulatedPath();
        var getPath = new Func<int, SimulatedPath>((int pathId) =>
        {
          path.SetWeinerIncrements(WeinerProcess.GetPathData(pathId));
          path.SetRates(0, DomesticDiscountProcess.GetPathData(pathId));
          path.SetRnDensities(GetPathData(pathId));
          return path;
        });

        return getPath;

      }
    }

    private Simulator InitSimulator()
    {
      Logger.Verbose("Constructing Simulator for RadonNikodymSimulation");
      var simulator = Simulator.Create(PathCount, WeinerProcess.SimulationDts, SimulationMarketEnvironment, VolatilityCollection, FactorLoadingCollection, SimulationMarketEnvironment.CptyIndex, -100.0);
      if (ObjectLogger.IsObjectLoggingEnabled)
      {
        RetainCounterpartiesCalibrationData();
      }
      return simulator;
    }

    private Tuple<CCRMarketEnvironment, FactorLoadingCollection, VolatilityCollection>  InitMarketEnvironment()
    {
      Logger.DebugFormat("Constructing Simulation Market Environment for RadonNikodymSimulation. Counterparty {0}. Booking Entity {1}", CounterpartyCurve?.Name, OwnCurve?.Name);

      var discountCurves = new HashSet<DiscountCurve>();
      var survivalCurves = new HashSet<SurvivalCurve>();
      var recoveries = new List<double>(); 
      var vols = new VolatilityCollection(DomesticDiscountProcess.Calibration.ForwardTenors);
      var factorLoadings = new FactorLoadingCollection(MarketFactorNames, DomesticDiscountProcess.Calibration.ForwardTenors);
      Logger.DebugFormat("Adding Domestic Discount {0}", DomesticDiscountProcess.DiscountCurve.Name);
      discountCurves.Add(DomesticDiscountProcess.DiscountCurve);
      vols.Add(DomesticDiscountProcess.DiscountCurve, DomesticDiscountProcess.Volatilities);
      factorLoadings.AddFactors(DomesticDiscountProcess.DiscountCurve, DomesticDiscountProcess.FactorLoadings);

      var riskyPartiesIdx = RiskyPartiesSurvivalCurves.Select((curve, i) => i).ToArray();
      Logger.DebugFormat("Adding CounterpartyCurve {0}", CounterpartyCurve.Name);
      survivalCurves.Add(CounterpartyCurve);
      recoveries.Add(CounterpartyRecovery);
      riskyPartiesIdx[0] = ~riskyPartiesIdx[0];
      if (HasCounterpartyVols)
      {
        Logger.DebugFormat("Adding Factorized Vols for CounterpartyCurve {0}", CounterpartyCurve.Name);
        vols.Add(CounterpartyCurve, CounterpartyVolatilityCurves);
        factorLoadings.AddFactors(CounterpartyCurve, CounterpartyFactorLoadings);
      }
      

      if (OwnCurve != null)
      {
        Logger.DebugFormat("Adding OwnCurve {0}", OwnCurve.Name);
        survivalCurves.Add(OwnCurve);
        riskyPartiesIdx[1] = ~riskyPartiesIdx[1];
        recoveries.Add(OwnRecovery);
        if (HasOwnVols)
        {
          Logger.DebugFormat("Adding Factorized Vols for OwnCurve {0}", OwnCurve.Name);
          vols.Add(OwnCurve, OwnVolatilityCurves);
          factorLoadings.AddFactors(OwnCurve, OwnFactorLoadings);
        }
      }
      
      if (BorrowingCurve != null)
      {
        Logger.DebugFormat("Adding BorrowingCurve {0}", BorrowingCurve.Name);
        survivalCurves.Add(BorrowingCurve);
        // handle dups, e.g. BorrowCurve == OwnCurve
        // http://stackoverflow.com/questions/2471588/how-to-get-index-using-linq
        int idx = survivalCurves.TakeWhile(c => c != BorrowingCurve).Count();
        if (riskyPartiesIdx.Length > 2 && idx != riskyPartiesIdx[2])
          riskyPartiesIdx[2] = idx;
        riskyPartiesIdx[2] = ~riskyPartiesIdx[2];
        if (HasBorrowingVols)
        {
          Logger.DebugFormat("Adding Factorized Vols for BorrowingCurve {0}", BorrowingCurve.Name);
          vols.Add(BorrowingCurve, BorrowingVolatilityCurves);
          factorLoadings.AddFactors(BorrowingCurve, BorrowingFactorLoadings);
        }
        recoveries.Add(BorrowingRecovery);
      }
      if (LendingCurve != null)
      {
        Logger.DebugFormat("Adding LendingCurve {0}", LendingCurve.Name);
        survivalCurves.Add(LendingCurve);
        // handle dups, e.g. LendCurve == OwnCurve
        // http://stackoverflow.com/questions/2471588/how-to-get-index-using-linq
        int idx = survivalCurves.TakeWhile(c => c != LendingCurve).Count();
        if (riskyPartiesIdx.Length > 3 && idx != riskyPartiesIdx[3])
          riskyPartiesIdx[3] = idx;
        riskyPartiesIdx[3] = ~riskyPartiesIdx[3];
        if (HasLendingVols)
        {
          Logger.DebugFormat("Adding Factorized Vols for LendingCurve {0}", LendingCurve.Name);
          vols.Add(LendingCurve, LendingVolatilityCurves);
          factorLoadings.AddFactors(LendingCurve, LendingFactorLoadings);
        }
        recoveries.Add(LendingRecovery);
      }

      Logger.DebugFormat("Total {0} distinct survival curves.", survivalCurves.Count);
      Logger.DebugFormat("Risky Party Indexes: {0}", string.Join(",", riskyPartiesIdx));
      Logger.DebugFormat("Recovery Assumptions {0}", string.Join(" , ", recoveries));
      var simulationMarketEnvironment = new CCRMarketEnvironment(AsOf, WeinerProcess.TenorDts, Tenor.Empty, riskyPartiesIdx, recoveries.ToArray(), discountCurves.ToArray(), new CalibratedCurve[0], survivalCurves.ToArray(), new FxRate[0], new CalibratedCurve[0]);
      return new Tuple<CCRMarketEnvironment, FactorLoadingCollection, VolatilityCollection>(simulationMarketEnvironment, factorLoadings, vols);
    }

    private IntPtr[] InitSimDtRadons()
    {
      var radons = new IntPtr[PathCount];
      for (int i = 0; i < PathCount; i++)
      {
        radons[i] = GetPathData(i);
      }
      return radons;
    }
    private IntPtr[] InitExposureDtRadons()
    {
      var radons = new IntPtr[PathCount];
      for (int i = 0; i < PathCount; i++)
      {
        if (!ExposureDtRadons.ContainsKey(i))
          GetExposureDtRadons(i);
        radons[i] = ExposureDtRadons[i];
      }
      return radons;
    }
    private IntPtr[] InitReportingDtRadons()
    {
      var radons = new IntPtr[PathCount];
      for (int i = 0; i < PathCount; i++)
      {
        if (!ReportingDtRadons.ContainsKey(i))
          GetReportingDtRadons(i);
        radons[i] = ReportingDtRadons[i];
      }
      return radons;
    }

    /// <summary>
    /// Simulation market environmnet
    /// </summary>
    public CCRMarketEnvironment SimulationMarketEnvironment => _simulationMarketEnvironment.Value.Item1;

    /// <summary>
    /// Factor loading collection
    /// </summary>
    public FactorLoadingCollection FactorLoadingCollection => _simulationMarketEnvironment.Value.Item2;

    /// <summary>
    /// Volatility collection
    /// </summary>
    public VolatilityCollection VolatilityCollection => _simulationMarketEnvironment.Value.Item3;

    /// <summary>
    /// Risky party survival curves
    /// </summary>
    public SurvivalCurve[] RiskyPartiesSurvivalCurves
    {
      get
      {
        return _riskyPartiesSurvivalCurves;
      }
    }

    /// <summary>
    /// risky parties recovery assumptions
    /// </summary>
    public double[] RiskyPartiesRecoveryAssumptions { get; set; }

    private int RnDimensionCount
    {
      get
      {
        switch (RiskyPartiesSurvivalCurves.Length)
        {
          case 1:
            return 2; // just cpty rn and spread
          //case 2:
          //	return 8; // both parties rn + survival Rn (3): unilateral and bilateral (x2) + both parties spread (2)
          //case 3:
          //	return 9; // both parties rn + survival Rn (3): unilateral and bilateral (x2) + both parties spread (2) + borrow spread (1)
          //case 4:
          //	return 10; // both parties rn + survival Rn (3): unilateral and bilateral (x2) + both parties spread (2) + borrow and lend spread (2)
          default:
            return 10; 
        }
      }
    }

    private WeinerProcess WeinerProcess { get; set; }

    /// <summary>
    /// Domestic discount process
    /// </summary>
    public DomesticDiscountProcess DomesticDiscountProcess { get; set; }

    /// <summary>
    /// Handle to pinned array of discounts on union of trade specific dates
    /// </summary>
    public GCHandle ExposureDtDiscountHandle { get; private set;  }

    /// <summary>
    /// Pin discounts on union of trade specific dates so they can be passed to unmanaged aggregator
    /// </summary>
    public double[,] ExposureDtDiscounts
    {
      get { return _exposureDtDiscounts; }
      set
      {
        _exposureDtDiscounts = value;
        ExposureDtDiscountHandle = GCHandle.Alloc(value, GCHandleType.Pinned);
      }
    }

    /// <summary>
    /// EffectiveSurvival[0] = Probability that counterparty survives to T and that default time of counterparty follows default time of the booking entity  
    /// EffectiveSurvival[1] = Probability that booking entity survives to T and that default time of counterparty precedes default time of the booking entity
    /// </summary>
    public IList<System.Tuple<Dt[], double[]>> IntegrationKernels {
      get
      {
        var integrationKernels = new List<System.Tuple<Dt[], double[]>>(); 
        if (RiskyPartiesSurvivalCurves.Length == 1)
        {
          var krn = new double[Simulator.SimulationDates.Length];
          //bilateral cpty default kernel
          Simulator.DefaultKernel(0, krn);
          integrationKernels.Add(new System.Tuple<Dt[], double[]>(Simulator.SimulationDates, krn));
          //unilateral cpty default kernel
          krn = new double[Simulator.SimulationDates.Length];
          Simulator.DefaultKernel(3, krn);
          integrationKernels.Add(new System.Tuple<Dt[], double[]>(Simulator.SimulationDates, krn));
        }
        else
        {
          //bilateral cpty default kernel
          var krn = new double[Simulator.SimulationDates.Length];
          Simulator.DefaultKernel(0, krn);
          integrationKernels.Add(new System.Tuple<Dt[], double[]>(Simulator.SimulationDates, krn));
          //unilateral cpty default kernel
          krn = new double[Simulator.SimulationDates.Length];
          Simulator.DefaultKernel(3, krn);
          integrationKernels.Add(new System.Tuple<Dt[], double[]>(Simulator.SimulationDates, krn));
          //bilateral own default kernel
          krn = new double[Simulator.SimulationDates.Length];
          Simulator.DefaultKernel(1, krn);
          integrationKernels.Add(new System.Tuple<Dt[], double[]>(Simulator.SimulationDates, krn));
          //unilateral own default kernel
          krn = new double[Simulator.SimulationDates.Length];
          Simulator.DefaultKernel(4, krn);
          integrationKernels.Add(new System.Tuple<Dt[], double[]>(Simulator.SimulationDates, krn));
          //bilateral survival kernel
          krn = new double[Simulator.SimulationDates.Length];
          Simulator.SurvivalKernel(2, krn);
          integrationKernels.Add(new System.Tuple<Dt[], double[]>(Simulator.SimulationDates, krn));
          //unilateral survival kernel
          krn = new double[Simulator.SimulationDates.Length];
          Simulator.SurvivalKernel(1, krn);
          integrationKernels.Add(new System.Tuple<Dt[], double[]>(Simulator.SimulationDates, krn));

          var pretime = AsOf;
          var krn0 = new double[Simulator.SimulationDates.Length];
          for (int t = 0; t < Simulator.SimulationDates.Length; ++t)
          {
            krn0[t] = (Simulator.SimulationDates[t] - pretime) / 365.0;
            pretime = Simulator.SimulationDates[t];
          }
          var noDefaultKernel = new System.Tuple<Dt[], double[]>(Simulator.SimulationDates, krn0);
          integrationKernels.Add(noDefaultKernel);
        }

      
        return integrationKernels; 
      }
    }


    private SurvivalCurve[] _riskyPartiesSurvivalCurves;
    private readonly Lazy<Simulator> _simulator;
    private readonly Lazy<Tuple<CCRMarketEnvironment, FactorLoadingCollection, VolatilityCollection>> _simulationMarketEnvironment;
    private Action<IntPtr, byte, int> _memSetAction;
    private SurvivalCurve _counterpartyCurve;
    private SurvivalCurve _ownCurve;
    private SurvivalCurve _borrowingCurve;
    private SurvivalCurve _lendingCurve;
    private VolatilityCurve[] _counterpartyVolatilityCurves;
    private VolatilityCurve[] _ownVolatilityCurves;
    private VolatilityCurve[] _borrowingVolatilityCurves;
    private VolatilityCurve[] _lendingVolatilityCurves;
    private double[,] _counterpartyLoadings;
    private double[,] _ownLoadings;
    private double[,] _borrowingLoadings;
    private double[,] _lendingLoadings;
    private Dt[] _reportingDates;
    private string[] _marketFactorNames;
    private Lazy<IntPtr[]> _simDtRadons;
    private Lazy<IntPtr[]> _exposureDtRadons;
    private Lazy<IntPtr[]> _reportingDtRadons;
    private double[,] _exposureDtDiscounts;

    /// <summary>
    /// Dispose
    /// </summary>
    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose
    /// </summary>
    /// <param name="disposing"></param>
    public void Dispose(bool disposing)
    {
     // Logger.DebugFormat("Disposing RadonNikodymSimulation. Counterparty {0}. BookingEntity {1}. disposing = {2}", CounterpartyCurve != null ? CounterpartyCurve.Name : null, OwnCurve != null ? OwnCurve.Name : null, disposing);
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

      if (ReportingDtRadons != null)
      {
        foreach (var intPtr in ReportingDtRadons.Values)
        {
          if (intPtr != IntPtr.Zero)
            Marshal.FreeCoTaskMem(intPtr);
        }
        ReportingDtRadons.Clear();
        ReportingDtRadons = null; 
      }


      if (ExposureDtRadons != null)
      {
        foreach (var intPtr in ExposureDtRadons.Values)
        {
          if (intPtr != IntPtr.Zero)
            Marshal.FreeCoTaskMem(intPtr);
        }
        ExposureDtRadons.Clear();
        ExposureDtRadons = null;
      }

      if (ExposureDtDiscountHandle.IsAllocated)
      {
        ExposureDtDiscountHandle.Free();
      }
      
      if(_simulator.IsValueCreated && disposing)
        _simulator.Value?.Dispose();
    }

    /// <summary>
    /// Distructor
    /// </summary>
    ~RadonNikodymProcess()
    {
      Dispose(false);
    }
    

  }



}