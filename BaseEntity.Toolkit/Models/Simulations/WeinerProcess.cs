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
using BaseEntity.Shared.Dynamic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  /// Interface for a simulated process object. Manages memory allocation and caching of simulated paths
  /// </summary>
  public interface IProcess : IDisposable
  {
    /// <summary>
    /// Fetch the IntPtr to the memory where path data is cached
    /// </summary>
    /// <param name="pathId"></param>
    /// <returns></returns>
    IntPtr GetPathData(int pathId);

    /// <summary>
    /// Number of simulated paths
    /// </summary>
    int PathCount { get; }
    
    /// <summary>
    /// Simulation time steps
    /// </summary>
    Dt[] SimulationDts { get; } 
    
  }
  
  /// <summary>
  /// Weiner process class
  /// </summary>
  [Serializable]
  [ObjectLoggerEnabled]
  public class WeinerProcess : IProcess
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(WeinerProcess));
    [ObjectLogger(Name = "BrownianPathsSimulation", Description = "Random increments of Brownian Motions for each path and simulation date", Category = "Simulation")]
    private static readonly IObjectLogger BinaryLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(WeinerProcess));

    /// <summary>
    /// Constructor
    /// </summary>
    public WeinerProcess()
    {
      
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="name">name</param>
    /// <param name="asOf">asof</param>
    /// <param name="pathCount">path count</param>
    /// <param name="qmc">bool type. quasi monter carlo</param>
    /// <param name="tenorDts">tenor date</param>
    /// <param name="simDts">simulation date</param>
    /// <param name="factorization">factorization</param>
    public WeinerProcess(string name, Dt asOf, int pathCount, bool qmc, Dt[] tenorDts, Dt[] simDts, MatrixFactorization factorization)
    {
      Name = name;
      AsOf = asOf;
      PathCount = pathCount;
      QuasiMonteCarlo = qmc;
      TenorDts = tenorDts;
      SimulationDts = simDts;
      PathData = new Dictionary<int, IntPtr>();
			_simulator = new Lazy<ISimulatorFactory>(InitSimulator);
      Factorization = factorization; 
    }
    
    /// <summary>
    /// Help function. ToDataTable.
    /// </summary>
    /// <returns></returns>
    public DataTable ToDataTable()
    {
      var dataTable = new DataTable(Name);
      dataTable.Columns.Add("PathId", typeof(int));
      dataTable.Columns.Add("FactorName", typeof(string));

      foreach (var simulationDate in Simulator.SimulationDates)
      {
        dataTable.Columns.Add(simulationDate.ToString(), typeof(double));
      }
      for (int pathId = 0; pathId < PathCount; pathId++)
      {
        var pathData = PathDataView(pathId);
        var dim = Simulator.Dimension;
        for (int dwIdx = 0; dwIdx < dim; dwIdx++)
        {
          // hard coded cutoff for the size of recorded datatable to prevent OutOfBoundsException when serializing
          // ideally the datatable should come no where near this number as it will be diffcult to analysis the reported table
          if (dataTable.Rows.Count >= 10000)
          {
            continue;
          }
          var row = dataTable.NewRow();
          row["PathId"] = pathId;
          row["FactorName"] = String.Format("dW_{0}", dwIdx);
          for (int dateIdx = 0; dateIdx < Simulator.SimulationDates.Length; dateIdx++)
          {
            // +2 for key cols
            row[dateIdx + 2] = pathData[dateIdx * dim + dwIdx];
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
    /// Get data from a path
    /// </summary>
    /// <param name="pathId">path id</param>
    /// <returns></returns>
    public IntPtr GetPathData(int pathId)
    {
      Logger.VerboseFormat("Getting Brownian Path Data for path {0}.", pathId);
      if (PathData == null)
      {
        Logger.ErrorFormat("PathData is null in call to BrownianPathsSimulation.GetPathData() path {0}. Likely object is being accessed after already Disposed.", pathId);
        PathData = new Dictionary<int, IntPtr>();
      }

      if (PathData.ContainsKey(pathId))
        return PathData[pathId];
      Logger.VerboseFormat("Allocating {0} byte memory block for Brownian Path Data. Path {1}", BlockSize, pathId);
      var pathData = UnmanagedMemory.Allocate(BlockSize);
      if (Simulator == null)
      {
        Logger.ErrorFormat("Call to BrownianPathsSimulations path {0}, null Simulator", pathId);
        throw new ToolkitException("Null Simulator in GetPathData");
      }

      using (var path = Simulator.GetSimulatedPath())
      {
        if (path == null)
        {
          Logger.ErrorFormat("Null path returned by Simulator.GetSimulatedPath(). path {0}", pathId);
          throw new ToolkitException("Null path returned by Simulator.GetSimulatedPath().");
        }

				path.SetWeinerIncrements(pathData);
				path.GetWeinerIncrements(pathId, RandomNumberGenerator);
      }
      PathData[pathId] = pathData;

      // Log Simulator on final pass
      if (BinaryLogger.IsObjectLoggingEnabled && pathId == PathCount - 1)
      {
        var binaryLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(BinaryLogger, System.Reflection.MethodBase.GetCurrentMethod());
        binaryLogAggregator.Append(typeof(WeinerProcess), AppenderUtil.DataTableToDataSet(ToDataTable())).Log();
      }

      return pathData; 
    }

    /// <summary>
    /// name
    /// </summary>
	  public string Name { get; private set; }

    /// <summary>
    /// Asof date
    /// </summary>
    public Dt AsOf { get; private set; }

    /// <summary>
    /// path count
    /// </summary>
    public int PathCount { get; private set; }
    
    /// <summary>
    /// Boolean type. Quasi monte carlo
    /// </summary>
    public bool QuasiMonteCarlo { get; private set; }

    /// <summary>
    /// Tenor date
    /// </summary>
    public Dt[] TenorDts { get; private set; }

    /// <summary>
    /// Simulation date
    /// </summary>
    public Dt[] SimulationDts { get; private set; }

    /// <summary>
    /// Random number generator type
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
    /// Factorization
    /// </summary>
    public MatrixFactorization Factorization { get; private set; }

    private Dictionary<int, IntPtr> PathData
    {
      get { return _pathData; }
      set { _pathData = value; }
    }

    private int BlockSize
	  {
		  get { return Simulator.SimulationDates.Length * Simulator.Dimension * sizeof(double);  }
	  }

    /// <summary>
    /// Simulator
    /// </summary>
    public Simulator Simulator
    {
      get { return _simulator.Value.Simulator; }
    }

    /// <summary>
    /// Simulator factory
    /// </summary>
    public ISimulatorFactory SimulatorFactory
    {
      get { return _simulator.Value; }
    }

    private ISimulatorFactory InitSimulator()
		{
      var simulator = Simulator.CreateSimulatorFactory(PathCount, AsOf, SimulationDts, TenorDts, Factorization.MarketFactorNames);
			return simulator;
		}

    /// <summary>
    /// Help function. Dispose
    /// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

    /// <summary>
    /// Dispose
    /// </summary>
    /// <param name="disposing">bool type. Dispose values</param>
		public void Dispose(bool disposing)
		{
      Logger.DebugFormat("Disposing BrownianPathsSimulation. disposing = {0}", disposing);

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
		  if (disposing && _simulator != null && _simulator.IsValueCreated)
		    _simulator.Value.Simulator.Dispose();
    }

    /// <summary>
    /// destructor
    /// </summary>
		~WeinerProcess()
		{
			Dispose(false);
		}

    [OnDeserialized, AfterFieldsCloned]
    private void OnClone(StreamingContext context)
    {
      // reinitialize path data after clone or deserialize event
      _pathData = new Dictionary<int, IntPtr>();
    }
    
    private readonly Lazy<ISimulatorFactory> _simulator;
    private MultiStreamRng _rng;
	  [NoClone, NonSerialized]
    private Dictionary<int, IntPtr> _pathData;
  }
}