using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using BaseEntity.Configuration;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.Serialization;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;
using BaseEntity.Toolkit.Util.Configuration;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Models.Simulations
{
  /// <summary>
  ///   Types of simulated events 
  /// </summary>
  public enum EventKind
  {
    /// <summary>
    ///   It happens when path-dependent evaluators get notified
    ///   to update their states.
    /// </summary>
    Updated,
    /// <summary>
    ///   It happens when the call option is exercised.
    /// </summary>
    CallExercised,
    /// <summary>
    ///   It happens when the put option is exercised.
    /// </summary>
    PutExercised,
  }

  #region IAmcForwardValueProcessor

  /// <summary>
  /// Interface to process the simulated forward values before regression
  /// </summary>
  internal interface IAmcForwardValueProcessor
  {
    /// <summary>
    /// Processes the forward values.
    /// </summary>
    /// <param name="dates">The simulation dates</param>
    /// <param name="getValuesAtDate">
    ///  The function get a list of values by path at the specified date index
    /// </param>
    /// <param name="getDiscountFactorsAtDate">
    ///  The function get a list of discount factors by path at the specified date index
    /// </param>
    void ProcessForwardValues(IList<Dt> dates,
      Func<int, IList<double>> getValuesAtDate,
      Func<int, IList<double>> getDiscountFactorsAtDate);
  }

  #endregion

  #region ExerciseEvaluator

  /// <summary>
  /// Exercise right
  /// </summary>
  [Serializable]
  public abstract class ExerciseEvaluator
  {
    /// <summary>
    /// Constructor  
    /// </summary>
    /// <param name="exerciseDates">Exercise dates</param>
    /// <param name="cashSettled">Settlement type</param>
    /// <param name="terminationDate">Termination date of underlying asset (for physically settled options)</param>
    /// <param name="valueIsConditionalMean">if set to <c>true</c>, the exercise value is the conditional mean of futer cash flows.</param>
    protected ExerciseEvaluator(IEnumerable<Dt> exerciseDates,
      bool cashSettled, Dt terminationDate, bool valueIsConditionalMean = false)
    {
      ExerciseDates = exerciseDates ?? new List<Dt>();
      CashSettled = cashSettled;
      TerminationDate = terminationDate;
      IsConditionalExpectation = valueIsConditionalMean;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="cashflow">Cashflow of asset obtained at exercise</param>
    /// <param name="exerciseDates">Exercise dates</param>
    /// <param name="cashSettled">Settlement type</param>
    protected ExerciseEvaluator(IEnumerable<ICashflowNode> cashflow, IEnumerable<Dt> exerciseDates, bool cashSettled)
    {
      Cashflow = cashflow;
      ExerciseDates = exerciseDates ?? new List<Dt>();
      CashSettled = cashSettled;
      TerminationDate = Cashflow.Max(cf => cf.PayDt);
    }

    internal Dt TerminationDate { get; private set; }

    /// <summary>
    /// True if call value can be computed as analytic function of market environment
    /// </summary>
    internal protected virtual bool Analytic
    {
      get { return WithoutCashflow; }
    }

    /// <summary>
    /// True if call value can be computed as analytic function of market environment
    /// </summary>
    internal bool WithoutCashflow
    {
      get { return Cashflow == null || !Cashflow.Any(); }
    }

    /// <summary>
    /// True for cash-settlement at exercise  
    /// </summary>
    public bool CashSettled { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this instance is conditional expectation.
    /// </summary>
    /// <value><c>true</c> if this instance is conditional expectation; otherwise, <c>false</c>.</value>
    public bool IsConditionalExpectation { get; private set; }

    /// <summary>
    /// Exercise dates  
    /// </summary>
    public IEnumerable<Dt> ExerciseDates { get; set; }

    /// <summary>
    /// Exercise instrument cash flows, if no analytic expression is available for exercise value.
    /// </summary>
    public IEnumerable<ICashflowNode> Cashflow { get; private set; }

    /// <summary>
    /// If the underling is path dependent, reset path functionals
    /// </summary>
    public virtual void Reset()
    { }

    /// <summary>
    /// If the underling is path dependent, dates at which path functionals are to be updated  
    /// </summary>
    public IEnumerable<Dt> EventDates { get; set; }

    /// <summary>
    /// Update path functionals
    /// </summary>
    /// <param name="eventKind">Event kind</param>
    /// <param name="eventDate">Event date</param>
    public virtual void Notify(EventKind eventKind, Dt eventDate)
    { }

    /// <summary>
    /// Exercise value
    /// </summary>
    /// <param name="date">Exercise date</param>
    /// <returns>Value of the asset obtained by exercising at date</returns>
    ///<example>For American put the exercise value is stock price.</example>
    ///<example>For Bermudan swaption the exercise value is the Value of the underlying swap</example>
    public virtual double Value(Dt date)
    {
      throw new NotImplementedException("Derived classes must either contain a valid Cashflow or override Value method.");
    }

    /// <summary>
    /// Exercise price/cost
    /// </summary>
    /// <param name="date">Date</param>
    /// <returns>Exercise price</returns>
    ///<example>For American put the exercise price is the option strike.</example>
    ///<example>For callable swap the exercise price is the premium payable at exercise</example>
    public virtual double Price(Dt date)
    {
      return 0.0;
    }

    internal virtual double Value(int dateIndex, Dt date)
    {
      return Value(date);
    }
    internal virtual double Price(int dateIndex, Dt date)
    {
      return Price(date);
    }
  }

  #endregion

  #region BasisFunctions

  /// <summary>
  /// Basis functions of the linear subspace used to estimate conditional expectation.
  /// </summary>
  [Serializable]
  public abstract class BasisFunctions
  {
    #region Instance members

    /// <summary>
    /// Number of linearly independent basis functions
    /// </summary>
    public int Dimension { get; protected set; }

    /// <summary>
    /// Generate basis function realization
    /// </summary>
    /// <param name="date">Future date</param>
    /// <param name="retVal">Store realized basis functions </param>
    /// <returns>Realized basis functions</returns>
    /// 
    /// <remarks>This function will be called sequentially in the time dimension.</remarks>
    public abstract void Generate(Dt date, double[] retVal);

    /// <summary>
    /// Basis are likely to be used across several paths 
    /// and may be functionals of the whole path up to date. 
    /// Reset mechanism will re-initialize path dependent 
    /// accumulated quantities to their time 0 value
    /// </summary>
    public virtual void Reset()
    {
    }

    internal virtual void Generate(int dateIndex, Dt date, double[] retVal)
    {
      Generate(date, retVal);
    }
    #endregion

    #region Factory Methods

    /// <summary>
    /// Default basis functions
    /// </summary>
    /// <param name="underlierCashflow">Cashflow received until exercise</param>
    /// <param name="call">Call exercise evaluator </param>
    /// <param name="put">Put exercise evaluator </param>
    /// <returns></returns>
    public static BasisFunctions GetDefaultBasisFunctions(
      IList<ICashflowNode> underlierCashflow, ExerciseEvaluator call,
      ExerciseEvaluator put)
    {
      return new DefaultBasisFunctions(underlierCashflow, call, put);
    }

    #endregion

    #region DefaultBasisFunctions

    [Serializable]
    private class DefaultBasisFunctions : BasisFunctions
    {
      #region Data

      private readonly int _n;
      private readonly ICashflowNode[] _underlierCashflow, _callCashflow, _putCashflow;
      private readonly ExerciseEvaluator _call, _put;

      #endregion

      #region Constructors

      public DefaultBasisFunctions(IEnumerable<ICashflowNode> underlierCashflow,
        ExerciseEvaluator call, ExerciseEvaluator put)
      {
        if (underlierCashflow != null)
        {
          _underlierCashflow = underlierCashflow.OrderBy(cf => cf.PayDt).ToArray();
          ++_n;
        }
        if (call != null)
        {
          _call = call;
          if (call.Cashflow != null)
            _callCashflow = call.Cashflow.OrderBy(cf => cf.PayDt).ToArray();
          ++_n;
        }
        if (put != null)
        {
          _put = put;
          if (put.Cashflow != null)
            _putCashflow = put.Cashflow.OrderBy(cf => cf.PayDt).ToArray();
          ++_n;
        }
        if (_n == 0)
          throw new ToolkitException(
            "Cannot generate default basis functions for this product");
        Dimension = 1 + _n + _n*(_n + 1)/2 + _n*(_n*(_n + 3) + 2)/6;
      }

      #endregion

      #region Methods

      public override void Generate(Dt date, double[] retVal)
      {
        retVal[0] = 1.0;
        var index = 1;
        if (_underlierCashflow != null)
        {
          retVal[index] = Pv(_underlierCashflow, date);
          ++index;
        }
        if (_call != null)
        {
          retVal[index] = _callCashflow == null
            ? _call.Value(date)
            : Pv(_callCashflow, date);
          ++index;
        }
        if (_put != null)
        {
          retVal[index] = _putCashflow == null
            ? _put.Value(date)
            : Pv(_putCashflow, date);
          ++index;
        }
        for (int i = 1; i <= _n; ++i)
          for (int j = 1; j <= i; ++j, ++index)
            retVal[index] = retVal[i]*retVal[j];
        for (int i = 1; i <= _n; ++i)
          for (int j = 1; j <= i; ++j)
            for (int k = 1; k <= j; ++k, ++index)
              retVal[index] = retVal[i]*retVal[j]*retVal[k];
      }

      private static double Pv(ICashflowNode[] cashflow, Dt from)
      {
        double f = 0.0;
        for (int i = cashflow.Length; --i >= 0; )
        {
          var cf = cashflow[i];
          if (cf.PayDt <= from)
            break;
          f += cf.FxRate() * cf.RealizedAmount() * cf.RiskyDiscount();
        }
        return f;
      }

      #endregion
    }

    #endregion
  }

  #endregion

  #region LeastSquaresMonteCarloPricingEngine

  /// <summary>
  /// General purpose Least Squares MonteCarlo pricing engine 
  /// </summary>
  /// <remarks></remarks>
  [ObjectLoggerEnabled]
  public static class LeastSquaresMonteCarloPricingEngine
  {
    private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(LeastSquaresMonteCarloPricingEngine));
    [ObjectLogger(Name = "LeastSquaresMonteCarloPricingEngine_BackwardAnalysis", Description = "Measure changes for each path and simulation date", Category = "Exposures")]
    private static readonly IObjectLogger BackwardAnalysisObjectLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(LeastSquaresMonteCarloPricingEngine), "Backward_Analysis");
    [ObjectLogger(Name = "LeastSquaresMonteCarloPricingEngine_ForwardAnalysis", Description = "Measure changes for each path and simulation date", Category = "Exposures")]
    private static readonly IObjectLogger ForwardAnalysisObjectLogger = ObjectLoggerUtil.CreateObjectLogger(typeof(LeastSquaresMonteCarloPricingEngine), "Forward_Analysis");

    #region SimulatedPath extension methods

    /// <summary>
    /// Evolve the market environment to reflect the evolution up to dtIdx
    /// </summary>
    /// <param name="path">Path</param>
    /// <param name="dtIdx">Exposure date index or bitwise complement of the index of the simulation date</param>
    /// <param name="t">Exposure date in days from asOf immediately succeeding dt if dt does not belong to the simulation dates</param>
    /// <param name="dt">Exposure date</param>
    /// <param name="environment">Market environment to evolve</param>
    internal static void Evolve(this SimulatedPath path, int dtIdx, double t, Dt dt, MarketEnvironment environment)
    {
      if (environment.DiscountCurves.Length != 0)
      {
        double numeraire, fxRate;
        var domestic = ResetCache(environment.DiscountCurves[0]);
        path.EvolveDiscount(0, t, dtIdx, domestic, out fxRate, out numeraire);
        double ddf = domestic.Interpolate(dt);
        for (int i = 1; i < environment.DiscountCurves.Length; ++i)
        {
          var df = ResetCache(environment.DiscountCurves[i]);
          path.EvolveDiscount(i, t, dtIdx, df, out fxRate, out numeraire);
          var fdf = df.Interpolate(dt);
          var fx = environment.FxRates[i - 1];
          fx.Update(dt, df.Ccy, domestic.Ccy, fxRate * fdf / ddf);
        }
      }
      // update foreign/foreign fx rates by triangulation through domestic ccy 
      for (int i = environment.DiscountCurves.Length - 1; i < environment.FxRates.Length; ++i)
      {
        var domesticCcy = environment.DiscountCurves[0].Ccy;
        var f1f2 = environment.FxRates[i];
        var f1Domestic = environment.FxRates.Where(f1 => (f1.FromCcy == f1f2.FromCcy && f1.ToCcy == domesticCcy)
                                                      || (f1.ToCcy == f1f2.FromCcy && f1.FromCcy == domesticCcy)).First().GetRate(f1f2.FromCcy, domesticCcy);

        var domesticF2 = environment.FxRates.Where(f2 => (f2.FromCcy == f1f2.ToCcy && f2.ToCcy == domesticCcy)
                                                      || (f2.ToCcy == f1f2.ToCcy && f2.FromCcy == domesticCcy)).First().GetRate(domesticCcy, f1f2.ToCcy);

        var fx = f1Domestic * domesticF2;
        f1f2.Update(dt, f1f2.FromCcy, f1f2.ToCcy, fx);
      }

      for (int i = 0; i < environment.CreditCurves.Length; ++i)
      {
        var sc = ResetCache(environment.CreditCurves[i]);
        path.EvolveCredit(i, t, dtIdx, sc);
      }
      for (int i = 0; i < environment.ForwardCurves.Length; ++i)
      {
        var fc = ResetCache(environment.ForwardCurves[i]);
        path.EvolveForward(i, t, dtIdx, fc);
      }
      for (int i = 0; i < environment.SpotBasedCurves.Length; ++i)
      {
        var sp = ResetCache(environment.SpotBasedCurves[i]) as IForwardPriceCurve;
        if (sp == null || sp.Spot == null)
          continue;
        var df = sp.DiscountCurve.Interpolate(dt);
        sp.Spot.Spot = dt;
        sp.Spot.Value = path.EvolveSpotPrice(i, t, dtIdx) / df;
      }
    }

    private static T ResetCache<T>(T curve) where T : Curve
    {
      ResetCache(curve.NativeCurve);
      return curve;
    }

    private static void ResetCache(Curves.Native.Curve curve)
    {
      if (curve.CacheEnabled)
      {
        curve.ClearCache();
      }

      if (curve.CustomInterpolator is MultiplicativeOverlay mo)
        ResetCache(mo.BaseCurve);
    }

    #endregion

    #region PartitionFlag

    [Flags]
    internal enum PartitionFlag
    {
      None = 0x000,

      CallExerciseDate = 0x001,

      PutExerciseDate = 0x002,

      MtMDate = 0x004,

      CallValueDate = 0x008,

      PutValueDate = 0x010,

      CallResetDate = 0x020,

      PutResetDate = 0x040,

      ReplaceCashflow = 0x080
    }

    private const PartitionFlag ValueDate = PartitionFlag.MtMDate | PartitionFlag.CallValueDate | PartitionFlag.PutValueDate;

    private const PartitionFlag ExerciseDate = PartitionFlag.CallExerciseDate | PartitionFlag.PutExerciseDate;

    #endregion

    #region Partition

    internal class Partition
    {
      public Partition(Dt dt, Dt asOf, Dt[] simulationDates, PartitionFlag flag, int exposureIdx)
      {
        Flag = flag;
        Date = dt;
        Time = Dt.FractDiff(asOf, dt);
        Idx = (dt >= simulationDates.Last()) ? simulationDates.Length - 1 : Array.BinarySearch(simulationDates, dt);
        ExposureIdx = exposureIdx;
      }

      /// <summary>
      /// Partition flag
      /// </summary>
      public PartitionFlag Flag;

      /// <summary>
      /// Time
      /// </summary>
      public readonly double Time;

      /// <summary>
      /// Date
      /// </summary>
      public readonly Dt Date;

      /// <summary>
      /// Index in simulation dates or binary complement of the index of immediately following simulation date
      /// </summary>
      public readonly int Idx;

      /// <summary>
      /// Index in exposure dates
      /// </summary>
      public int ExposureIdx;

      /// <summary>
      /// Produces a string reprentation of the Partition class
      /// </summary>
      /// <returns>a string reprentation of the Partition class</returns>
      public override string ToString()
      {
        // as Date is read only
        var date = Date;
        return "[" + date.ToString("yyyy-MM-dd") + ", " + Flag + "]";
      }
    }

    #endregion

    #region ResetNodeWrapper
    /// <summary>
    /// Cashflow object. 
    /// </summary>
    [Serializable]
    internal class ResetNodeWrapper
    {
      #region Constructor

      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="asOf">As of date</param>
      /// <param name="simulationDates">Simulation dates</param>
      /// <param name="node">ICashflowNode</param>
      public ResetNodeWrapper(Dt asOf, Dt[] simulationDates, IResetNode node)
      {
        Node = node;
        ResetTime = Dt.FractDiff(asOf, node.Date);
        ResetDtIndex = Array.BinarySearch(simulationDates, node.Date);
      }

      #endregion

      #region Data

      /// <summary>
      /// Reset date
      /// </summary>
      public readonly IResetNode Node;

      /// <summary>
      /// Index of ResetDt in simulation dates (or binary complement of immediately following simulation date) 
      /// </summary>
      public readonly int ResetDtIndex;

      /// <summary>
      /// Reset time in days
      /// </summary>
      public readonly double ResetTime;

      #endregion
    }
    #endregion

    #region CashflowNodeWrapper

    /// <summary>
    /// Cashflow object. 
    /// </summary>
    [Serializable]
    internal class CashflowNodeWrapper
    {
      #region Constructor

      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="asOf">As of date</param>
      /// <param name="simulationDates">Simulation dates</param>
      /// <param name="cashflowNode">ICashflowNode</param>
      /// <param name="partition">ICashflowNode</param>
      public CashflowNodeWrapper(Dt asOf, Dt[] simulationDates, ICashflowNode cashflowNode, IEnumerable<Partition> partition)
      {
        CashflowNode = cashflowNode;
        ResetTime = Dt.FractDiff(asOf, cashflowNode.ResetDt);
        PayTime = Dt.FractDiff(asOf, cashflowNode.PayDt);
        ResetDtIndex = Array.BinarySearch(simulationDates, cashflowNode.ResetDt);
        PayDtIndex = Array.BinarySearch(simulationDates, cashflowNode.PayDt);
        if (partition != null)
          WorkspaceIndex = partition.Count(u => u.Date < cashflowNode.PayDt);

        var pathDependent = cashflowNode as IMultiResetCashflowNode;
        if (pathDependent == null) return;
        ResetNodes = pathDependent.ResetNodes == null ? null : pathDependent
          .ResetNodes.Where(n => n.Date > asOf)
          .Select(n => new ResetNodeWrapper(asOf, simulationDates, n))
          .ToList();
      }

      #endregion

      #region Properties

      /// <summary>
      /// Reset date
      /// </summary>
      public ICashflowNode CashflowNode { get; private set; }

      public readonly IEnumerable<ResetNodeWrapper> ResetNodes;

      /// <summary>
      /// Index of PayDt in simulation dates (or binary complement of immediately following simulation date) 
      /// </summary>
      public int PayDtIndex { get; private set; }

      /// <summary>
      /// Index of ResetDt in simulation dates (or binary complement of immediately following simulation date) 
      /// </summary>
      public int ResetDtIndex { get; private set; }

      /// <summary>
      /// Payment time in days 
      /// </summary>
      public double PayTime { get; private set; }

      /// <summary>
      /// Reset time in days
      /// </summary>
      public double ResetTime { get; private set; }

      /// <summary>
      /// Index of PayDt in partition
      /// </summary>
      public int WorkspaceIndex { get; private set; }


      #endregion
    }

    #endregion

    #region Workspace

    internal class Workspace
    {
      public Workspace(Simulator simulator, BasisFunctions basis, IList<Partition> partition, bool foreign)
      {
        var pathCount = simulator.PathCount;
        MtM = partition.Select(pi => new double[pathCount]).ToArray();
        Df = partition.Select((pi, i) => (i > 0) ? new double[pathCount] : null).ToArray();
        if (basis != null)
        {
          var dimension = basis.Dimension;
          U = partition.Select((pi, i) => (i > 0) ? new double[pathCount, dimension] : null).ToArray();
          W = partition.Select((pi, i) => (i > 0) ? new double[dimension] : null).ToArray();
          V = partition.Select((pi, i) => (i > 0) ? new double[dimension, dimension] : null).ToArray();
        }
        if (foreign)
          Fx = partition.Select((pi, i) => ((i > 0) && ((pi.Flag & PartitionFlag.MtMDate) != 0)) ? new double[pathCount] : null).ToArray();
        if (partition.Any(pi => (pi.Flag & PartitionFlag.CallValueDate) != 0))
          CallValue = partition.Select(pi => ((pi.Flag & PartitionFlag.CallValueDate) != 0) ? new double[pathCount] : null).ToArray();
        if (partition.Any(pi => (pi.Flag & PartitionFlag.PutValueDate) != 0))
          PutValue = partition.Select(pi => ((pi.Flag & PartitionFlag.PutValueDate) != 0) ? new double[pathCount] : null).ToArray();
      }

      public Workspace DeepCopy()
      {
        var retVal = (Workspace)MemberwiseClone();
        retVal.U = (U == null) ? null : U.Select(u => (u == null) ? null : (double[,])u.Clone()).ToArray();
        retVal.W = (W == null) ? null : W.Select(w => (w == null) ? null : (double[])w.Clone()).ToArray();
        retVal.V = (V == null) ? null : V.Select(v => (v == null) ? null : (double[,])v.Clone()).ToArray();
        retVal.MtM = (MtM == null) ? null : MtM.Select(mtm => (mtm == null) ? null : (double[])mtm.Clone()).ToArray();
        retVal.CallValue = (CallValue == null) ? null : CallValue.Select(c => (c == null) ? null : (double[])c.Clone()).ToArray();
        retVal.PutValue = (PutValue == null) ? null : PutValue.Select(p => (p == null) ? null : (double[])p.Clone()).ToArray();
        retVal.Df = (Df == null) ? null : Df.Select(df => (df == null) ? null : (double[])df.Clone()).ToArray();
        retVal.Fx = (Fx == null) ? null : Fx.Select(fx => (fx == null) ? null : (double[])fx.Clone()).ToArray();
        return retVal;
      }

      internal void RetainWorkspace(ILogAggregator objectLogAggregator, IList<Partition> partition, string prefex, int pathCount)
      {
        if (objectLogAggregator == null || prefex == null)
        {
          return;
        }
        objectLogAggregator.Append(typeof(LeastSquaresMonteCarloPricingEngine), string.Format("{0}_MTM", prefex),
        AppenderUtil.DataTableToDataSet(DataTableFromArray(MtM, partition, string.Format("{0}_MTM", prefex), pathCount)));
        objectLogAggregator.Append(typeof(LeastSquaresMonteCarloPricingEngine), string.Format("{0}_CallValue", prefex),
          AppenderUtil.DataTableToDataSet(DataTableFromArray(CallValue, partition, string.Format("{0}_CallValue", prefex), pathCount)));
        objectLogAggregator.Append(typeof(LeastSquaresMonteCarloPricingEngine), string.Format("{0}_PutValue", prefex),
          AppenderUtil.DataTableToDataSet(DataTableFromArray(PutValue, partition, string.Format("{0}_PutValue", prefex), pathCount)));
        objectLogAggregator.Append(typeof(LeastSquaresMonteCarloPricingEngine), string.Format("{0}_Df", prefex),
          AppenderUtil.DataTableToDataSet(DataTableFromArray(Df, partition, string.Format("{0}_Df", prefex), pathCount)));
        objectLogAggregator.Append(typeof(LeastSquaresMonteCarloPricingEngine), string.Format("{0}_Fx", prefex),
          AppenderUtil.DataTableToDataSet(DataTableFromArray(Fx, partition, string.Format("{0}_Fx", prefex), pathCount)));
        objectLogAggregator.Log();
      }

      private static DataTable DataTableFromArray(double[][] array, IList<Partition> partition, string key, int pathCount)
      {
        if (partition == null)
        {
          throw new ToolkitException("Partition is undefined.");
        }
        var dataTable = new DataTable(key);
        if (array == null || array.Length == 0)
        {
          return dataTable;
        }

        dataTable.Columns.Add("Path Id", typeof(int));

        for (var i = 0; i < array.Length; ++i)
        {
          dataTable.Columns.Add(partition[i].Date.ToString(), typeof(double));
        }

        for (var i = 0; i < pathCount; ++i)
        {
          var row = dataTable.NewRow();
          row[0] = i;
          for (var j = 0; j < array.Length; ++j)
          {
            row[j + 1] = array[j] == null ? 0.0 : array[j][i];
          }
          dataTable.Rows.Add(row);
        }

        return dataTable;
      }

      public double[][,] U;
      public double[][] W;
      public double[][,] V;
      public double[][] MtM;
      public double[][] CallValue;
      public double[][] PutValue;
      public double[][] Df;
      public double[][] Fx;
    }

    #endregion

    #region PricingData

    /// <summary>
    /// Pricing data for forward sweep
    /// </summary>
    [Serializable]
    internal class PricingData
    {
      #region Constructor

      public PricingData(
        Currency numeraireCcy,
        Simulator simulator,
        MarketEnvironment environment,
        IEnumerable<ICashflowNode> underlierCashflow,
        ExerciseEvaluator call,
        ExerciseEvaluator put,
        BasisFunctions basis,
        IList<Partition> partition)
      {
        Environment = environment;
        DiscountCurve = environment.DiscountCurves.FirstOrDefault();
        if (DiscountCurve == null)
          throw new ArgumentException("DiscountCurve not found");
        Currency = DiscountCurve.Ccy;
        if (Currency != numeraireCcy)
        {
          FxRate = GetFxData(environment, DiscountCurve.Ccy, numeraireCcy);
          NumeraireCcy = numeraireCcy;
          Foreign = true;
        }
        if (underlierCashflow != null)
        {
          UnderlierCashflow = underlierCashflow.Select(cf =>
            new CashflowNodeWrapper(simulator.AsOf, simulator.SimulationDates, cf, partition))
            .ToArray().Sort();
          UnderlierMc = true;
        }
        if ((call != null) && call.ExerciseDates.Any())
        {
          Call = call;
          CallCashflow = GenerateCashflow(call, simulator.AsOf, simulator.SimulationDates, partition);
          CallAnalytic = call.Analytic;
          CallMc = !CallAnalytic;
        }
        if ((put != null) && put.ExerciseDates.Any())
        {
          Put = put;
          PutCashflow = GenerateCashflow(put, simulator.AsOf, simulator.SimulationDates, partition);
          PutAnalytic = put.Analytic;
          PutMc = !PutAnalytic;
        }
        BasisFunctions = new Tuple<BasisFunctions, double[]>(basis, new double[basis.Dimension]);
      }

      #endregion

      #region Methods

      private static CashflowNodeWrapper[] GenerateCashflow(ExerciseEvaluator exerciseEvaluator, Dt asOf, Dt[] simulationDates, IEnumerable<Partition> partition)
      {
        if (exerciseEvaluator == null || exerciseEvaluator.WithoutCashflow)
          return null;
        return exerciseEvaluator.Cashflow.Select(cf => new CashflowNodeWrapper(asOf, simulationDates, cf, partition)).ToArray().Sort();
      }

      #endregion

      #region Data

      public readonly Currency Currency;
      public readonly Currency NumeraireCcy;
      public readonly CashflowNodeWrapper[] UnderlierCashflow;
      public readonly CashflowNodeWrapper[] PutCashflow;
      public readonly CashflowNodeWrapper[] CallCashflow;
      public readonly ExerciseEvaluator Call;
      public readonly ExerciseEvaluator Put;
      public readonly Tuple<BasisFunctions, double[]> BasisFunctions;
      public readonly MarketEnvironment Environment;
      public readonly DiscountCurve DiscountCurve;
      public readonly FxRate FxRate;
      public readonly bool UnderlierMc;
      public readonly bool CallMc;
      public readonly bool PutMc;
      public readonly bool CallAnalytic;
      public readonly bool PutAnalytic;
      public readonly bool Foreign;

      #endregion
    }

    #endregion

    #region Methods

    private static void Add(this SortedDictionary<Dt, Partition> dict, Dt dt, int exposureIdx, PartitionFlag flag, Simulator simulator)
    {
      Partition partition;
      if (dict.TryGetValue(dt, out partition))
      {
        if (exposureIdx >= 0)
          partition.ExposureIdx = exposureIdx;
        partition.Flag |= flag;
      }
      else
        dict[dt] = new Partition(dt, simulator.AsOf, simulator.SimulationDates, flag, exposureIdx);
    }

    private static bool IsActive(Dt dt, ExerciseEvaluator exerciseEvaluator)
    {
      if ((exerciseEvaluator == null) || !exerciseEvaluator.ExerciseDates.Any() || exerciseEvaluator.CashSettled)
        return false;
      var terminationDt = exerciseEvaluator.TerminationDate.IsValid() ? exerciseEvaluator.TerminationDate : Dt.MaxValue;
      return (dt > exerciseEvaluator.ExerciseDates.First() && dt <= terminationDt);
    }

    private static void Conform(this PricingData data)
    {
      if (data != null && data.Environment != null)
        data.Environment.Conform();
    }

    private static Partition[] GeneratePartition(
      Simulator simulator,
      IList<Dt> exposureDates,
      IEnumerable<ICashflowNode> underlierCashflow,
      ExerciseEvaluator call,
      ExerciseEvaluator put)
    {
      var schedule = new SortedDictionary<Dt, Partition>();
      schedule.Add(simulator.AsOf, -1, PartitionFlag.MtMDate, simulator);
      var callFlag = PartitionFlag.CallValueDate;
      if (call != null)
      {
        if (call.IsConditionalExpectation)
          callFlag |= PartitionFlag.ReplaceCashflow;
        foreach (var date in call.ExerciseDates.Where(dt => dt >= simulator.AsOf))
          schedule.Add(date, -1, PartitionFlag.CallExerciseDate | callFlag, simulator);
        if (call.EventDates != null)
        {
          foreach (var date in call.EventDates.Where(dt => dt >= simulator.AsOf))
            schedule.Add(date, -1, PartitionFlag.CallResetDate, simulator);
        }
      }
      var putFlag = PartitionFlag.PutValueDate;
      if (put != null)
      {
        if (put.IsConditionalExpectation)
          putFlag |= PartitionFlag.ReplaceCashflow;
        foreach (var date in put.ExerciseDates.Where(dt => dt >= simulator.AsOf))
          schedule.Add(date, -1, PartitionFlag.PutExerciseDate | putFlag, simulator);
        if (put.EventDates != null)
        {
          foreach (var date in put.EventDates.Where(dt => dt >= simulator.AsOf))
            schedule.Add(date, -1, PartitionFlag.PutResetDate, simulator);
        }
      }
      if (exposureDates != null && exposureDates.Any())
      {
        var lastCfDt = (underlierCashflow == null) ? Dt.Empty : underlierCashflow.Max(cf => cf.PayDt);
        for (int i = 0; i < exposureDates.Count; ++i)
        {
          var date = exposureDates[i];
          if (date < simulator.AsOf)
            continue;
          var flag = PartitionFlag.None;
          bool cvd = IsActive(date, call);
          bool pvd = IsActive(date, put);
          if (cvd)
            flag |= callFlag;
          if (pvd)
            flag |= putFlag;
          if ((lastCfDt.IsValid() && (date <= lastCfDt)) || cvd || pvd)
            flag |= PartitionFlag.MtMDate;
          schedule.Add(date, i, flag, simulator);
        }
      }
      var last = schedule.Values.Last(node => (node.Flag | ValueDate) != 0).Date;
      if (Logger.IsDebugEnabled)
      {
        LogPartitions(schedule.Values.ToList());
      }
      return schedule.Values.Where(pi => pi.Date <= last).ToArray();
    }

    private static void LogPartitions(List<Partition> partitions)
    {
      var stringBuilder = new StringBuilder();
      stringBuilder.Append("Generated Partitions: ");
      var counter = 0;
      foreach (Partition partition in partitions)
      {
        if (partition == null)
        {
          stringBuilder.Append("Parition is Null");
          if (++counter != partitions.Count)
          {
            stringBuilder.Append(", ");
          }
        }
        else
        {
          stringBuilder.Append(partition.ToString());    
          if (++counter != partitions.Count)
          {
            stringBuilder.Append(", ");
          }
        }
      }
      Logger.Debug(stringBuilder.ToString());
    }

    private static void ResetAllNodes(this IEnumerable<CashflowNodeWrapper> cashflow)
    {
      // Reset all the cashflow nodes
      foreach (var cf in cashflow)
        cf.CashflowNode.Reset();
    }

    private static void GenerateRealization(
      this IEnumerable<CashflowNodeWrapper> cashflow,
      SimulatedPath path, MarketEnvironment environment,
      double[][] workspace)
    {
      Dt date = Dt.Empty;
      foreach (var node in cashflow)
      {
        if (node.ResetNodes != null)
        {
          foreach (var reset in node.ResetNodes)
          {
            var resetDate = reset.Node.Date;
            if (resetDate != date)
            {
              path.Evolve(reset.ResetDtIndex, reset.ResetTime, resetDate, environment);
              date = resetDate;
            }
            reset.Node.Update();
          }
        }
        var cf = node.CashflowNode;
        if (cf.ResetDt != date)
        {
          path.Evolve(node.ResetDtIndex, node.ResetTime, cf.ResetDt, environment);
          date = cf.ResetDt;
        }
        double cpn = cf.RealizedAmount();
        //Partition knows how to compute the coupon amount, given updated market and sequential nature of the calls
        if (cf.PayDt != date)
        {
          path.Evolve(node.PayDtIndex, node.PayTime, cf.PayDt, environment);
          date = cf.PayDt;
        }
        double amount = cpn * cf.RiskyDiscount() * cf.FxRate(); //normalized cashflow in domestic currency
        for (int j = node.WorkspaceIndex; --j >= 0; )
        {
          if (workspace[j] != null)
            workspace[j][path.Id] += amount; //cf in (t_j, t_N] 
        }
      }
    }

    private static void Next(this ExerciseEvaluator exerciseEvaluator, int pathId, Dt date, double[] workspace)
    {
      workspace[pathId] = exerciseEvaluator.Value(date);
    }

    private static void Reset(this Tuple<BasisFunctions, double[]> basisFunctions)
    {
      if (basisFunctions != null)
        basisFunctions.Item1.Reset();
    }

    private static void Next(this Tuple<BasisFunctions, double[]> basisFunctions, int pathId, Dt dt, double[,] workspace)
    {
      if ((basisFunctions == null) || (workspace == null))
        return;
      basisFunctions.Item1.Generate(dt, basisFunctions.Item2);
      for (int i = 0; i < basisFunctions.Item1.Dimension; ++i)
        workspace[pathId, i] = basisFunctions.Item2[i];
    }

    private static CashflowNodeWrapper[] Sort(this CashflowNodeWrapper[] cashflowNodes)
    {
      Array.Sort(cashflowNodes, (x, y) =>
        {
          if (x.CashflowNode.PayDt == y.CashflowNode.PayDt)
            return Dt.Cmp(x.CashflowNode.ResetDt, y.CashflowNode.ResetDt);
          return Dt.Cmp(x.CashflowNode.PayDt, y.CashflowNode.PayDt);
        });
      return cashflowNodes;
    }

    private static bool SvdIterate(double[,] u, double[] w, double[,] v, 
      double[] y, double min, double max, int n)
    {
      if (n > 10)
        return false;
      n++;
      for (int i = 0; i < y.Length; ++i)
        y[i] = Math.Min(Math.Max(y[i], min), max);
      LinearSolvers.ProjectSVD(u, w, v, y, y);
      double supErr = -1.0;
      double absMax = Math.Abs(max);
      double absMin = Math.Abs(min);
      bool maxNonZero = absMax > 1e-12;
      bool minNonZero = absMin > 1e-12;
      foreach (var yi in y)
      {
        if (yi > max)
        {
          double e = yi - max;
          if (maxNonZero)
            e /= Math.Abs(max);
          if (e > supErr)
            supErr = e;
        }
        if (yi < min)
        {
          double e = min - yi;
          if (minNonZero)
            e /= absMin;
          if (e > supErr)
            supErr = e;
        }
      }
      if (supErr < 1e-5)
        return true;
      return SvdIterate(u, w, v, y, min, max, n);
    }

    private static void SvdSolve(double[,] u, double[] w, double[,] v, double[] b, double[] y)
    {
      const int n = 0;
      double min = b[0];
      double max = b[0];
      for (int i = 1; i < b.Length; ++i)
      {
        if (b[i] > max)
          max = b[i];
        if (b[i] < min)
          min = b[i];
      }
      if (min >= max)
      {
        for (int i = 0; i < y.Length; ++i)
          y[i] = max;
        return;
      }
      LinearSolvers.ProjectSVD(u, w, v, b, y);
      if (!y.Any(yi => yi < min || yi > max))
        return;
      SvdIterate(u, w, v, y, min, max, n);
      for (int i = 0; i < y.Length; ++i)
        y[i] = Math.Max(Math.Min(y[i], max), min);
    }

    private static void ResetEvaluators(PricingData pricingData)
    {
      if (pricingData.UnderlierMc)
        pricingData.UnderlierCashflow.ResetAllNodes();

      if (pricingData.CallCashflow != null)
        pricingData.CallCashflow.ResetAllNodes();
      else if (pricingData.Call != null)
        pricingData.Call.Reset();

      if (pricingData.PutCashflow != null)
        pricingData.PutCashflow.ResetAllNodes();
      else if (pricingData.Put != null)
        pricingData.Put.Reset();

      pricingData.BasisFunctions.Reset();
    }

    private static void GenerateRealization(this SimulatedPath path, PricingData pricingData, IList<Partition> partition, Workspace workspace)
    {
      if (Logger.IsDebugEnabled)
      {
        if (path != null)
        {
          Logger.Debug(string.Format("Generating Realization for path {0}...", path.Id));
        }
        else
        {
          Logger.Debug("Generating Realization error path is null...");
        }
      }
      // First reset everything before evolve the path.
      // We should avoid calling any value function between two reset calls,
      // for different cash flow nodes and evaluators may share the same
      // evaluation context and interleaving reset and valuation may mess up
      // the states.
      ResetEvaluators(pricingData);

      // Then begin evolve and generate realizations.
      if (pricingData.UnderlierMc)
        pricingData.UnderlierCashflow.GenerateRealization(path, pricingData.Environment, workspace.MtM); //coupons are discounted to time 0
      if (pricingData.CallCashflow != null)
        pricingData.CallCashflow.GenerateRealization(path, pricingData.Environment, workspace.CallValue); //coupons are discounted to time 0
      if (pricingData.PutCashflow != null)
        pricingData.PutCashflow.GenerateRealization(path, pricingData.Environment, workspace.PutValue); //coupons are discounted to time 0

      Debug.Assert(partition.Count == workspace.MtM.Length);

      for (int t = 1, n = partition.Count; t < n; ++t)
      {
        var node = partition[t];
        path.Evolve(node.Idx, node.Time, node.Date, pricingData.Environment);
        if ((node.Flag & PartitionFlag.CallResetDate) != 0)
        {
          if (Logger.IsDebugEnabled)
          {
            Logger.Debug(string.Format("Notifying Reset on {0}", node.Date));
          }
          pricingData.Call.Notify(EventKind.Updated, node.Date);
        }
        if ((node.Flag & PartitionFlag.PutResetDate) != 0)
        {
          if (Logger.IsDebugEnabled)
          {
            Logger.Debug(string.Format("Notifying Reset on {0}", node.Date));
          }
          pricingData.Put.Notify(EventKind.Updated, node.Date);
        }
        if ((node.Flag & ValueDate) != 0)
        {
          if (pricingData.Foreign && (node.Flag & PartitionFlag.MtMDate) != 0)
          {
            workspace.Fx[t][path.Id] = pricingData.FxRate.GetRate(pricingData.Currency, pricingData.NumeraireCcy);
          }
          if (pricingData.CallCashflow == null && (node.Flag & PartitionFlag.CallValueDate) != 0)
          {
            pricingData.Call.Next(path.Id, node.Date, workspace.CallValue[t]);
            if (Logger.IsDebugEnabled)
            {
              Logger.Debug(string.Format("Value data {0}, Path Id {1}, Call value {2},", node.Date, path.Id, workspace.CallValue[t][path.Id]));
            }
          }
          if (pricingData.PutCashflow == null && (node.Flag & PartitionFlag.PutValueDate) != 0)
          {
            pricingData.Put.Next(path.Id, node.Date, workspace.PutValue[t]);
            if (Logger.IsDebugEnabled)
            {
              Logger.Debug(string.Format("Value data {0}, Path Id {1}, Put value {2}", node.Date, path.Id, workspace.PutValue[t][path.Id]));
            }
          }
        }
        // We need expectations on all the exposure dates.
        workspace.Df[t][path.Id] = pricingData.DiscountCurve.Interpolate(node.Date);
        pricingData.BasisFunctions.Next(path.Id, node.Date, workspace.U[t]);
      }
    }

    private static Workspace ProcessForwardValues(Workspace workspace,
      PricingData pricingData, IList<Partition> partition)
    {
      var processor = pricingData.Put as IAmcForwardValueProcessor;
      if (processor != null)
      {
        processor.ProcessForwardValues(
          ListUtil.CreateList(partition.Count, i => partition[i].Date),
          t => workspace.PutValue[t], t => workspace.Df[t]);
      }
      processor = pricingData.Call as IAmcForwardValueProcessor;
      if (processor != null)
      {
        processor.ProcessForwardValues(
          ListUtil.CreateList(partition.Count, i => partition[i].Date),
          t => workspace.CallValue[t], t => workspace.Df[t]);
      }
      return workspace;
    }

    private static Workspace DoForward(Simulator simulator, MultiStreamRng randGen, PricingData pricingData, IList<Partition> partition)
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Running Forward Valuation Pass...");
      }
      var workspace = new Workspace(simulator, pricingData.BasisFunctions.Item1, partition, pricingData.Foreign);
      Action<int, Tuple<PricingData, MultiStreamRng>> compute = (i, thread) =>
        {
          var data = thread.Item1;
          var rng = thread.Item2;
          var path = simulator.GetSimulatedPath(i, rng);
          path.GenerateRealization(data, partition, workspace);
          path.Dispose();
        };
      Parallel.For(0, simulator.PathCount, () =>
        {
          var data = pricingData.CloneObjectGraph();
          data.Conform();
          return new Tuple<PricingData, MultiStreamRng>(data, randGen.Clone());
        },
        (i, thread) => compute(i, thread));
      return ProcessForwardValues(workspace, pricingData, partition);
    }

		private static Workspace DoForward(Simulator simulator, PricingData pricingData, IList<Partition> partition, Func<int, SimulatedPath> getPath)
		{
			var data = pricingData.CloneObjectGraph();
			data.Conform();
		  var workspace = new Workspace(simulator, data.BasisFunctions.Item1, partition, data.Foreign);
		  var generator = GetRealizationGenerator(data, partition, simulator.SimulationDates);
			for (var i = 0; i < simulator.PathCount; i++)
			{
				var path = getPath(i);
			  generator.GetRealization(path, workspace);
      }
		  WorkspaceLogger?.Invoke(workspace);
		  ForwardValuation(workspace, partition, simulator);
      var toReturn = ProcessForwardValues(workspace, data, partition);
      ProcessForwardValuation(toReturn, partition, simulator);
      return toReturn;
    }

    private static void ForwardValuation(Workspace workspace, IList<Partition> partition, Simulator simulator)
    {
      if (ForwardAnalysisObjectLogger.IsObjectLoggingEnabled)
      {
        var objectLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(ForwardAnalysisObjectLogger, System.Reflection.MethodBase.GetCurrentMethod());
        workspace.RetainWorkspace(objectLogAggregator, partition, "ForwardValuation", simulator.PathCount);
      }
    }

    private static void ProcessForwardValuation(Workspace workspace, IList<Partition> partition, Simulator simulator)
    {
      if (ForwardAnalysisObjectLogger.IsObjectLoggingEnabled)
      {
        var objectLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(ForwardAnalysisObjectLogger, System.Reflection.MethodBase.GetCurrentMethod());
        workspace.RetainWorkspace(objectLogAggregator, partition, "ProcessForwardValuation", simulator.PathCount);
      }
    }

    private static IEnumerable<Workspace> DoForward(Simulator simulator,
      Simulator[] perturbedSimulators, MultiStreamRng randGen,
      PricingData pricingData, IList<Partition> partition)
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Running Forward Valuation Pass...");
      }
      var workspace = new Workspace(simulator, pricingData.BasisFunctions.Item1, partition, pricingData.Foreign);
      var allWorkspaces = new List<Workspace> { workspace };
      allWorkspaces.AddRange(perturbedSimulators.Select(sim => (sim == null) ? null : workspace.DeepCopy()));
      Action<int, Tuple<PricingData, MultiStreamRng>> compute = (idx, thread) =>
        {
          var data = thread.Item1;
          var rng = thread.Item2;
          var path = simulator.GetSimulatedPath(idx, rng);
          path.GenerateRealization(data, partition, workspace);
          for (int i = 0; i < perturbedSimulators.Length; ++i)
          {
            var psim = perturbedSimulators[i];
            if (psim == null)
              continue;
            var perturbedPath = psim.GetSimulatedPath(path);
            perturbedPath.GenerateRealization(data, partition, allWorkspaces[i + 1]);
            perturbedPath.Dispose();
          }
          path.Dispose();
        };
      Parallel.For(0, simulator.PathCount, () =>
        {
          var data = pricingData.CloneObjectGraph();
          data.Conform();
          return new Tuple<PricingData, MultiStreamRng>(data, randGen.Clone());
        }, (i, thread) => compute(i, thread));
      return allWorkspaces;
    }

    private static double DoBackward(Partition[] partition,
      Workspace workspace, PricingData data, Simulator simulator,
      double tolerance, int[] exerciseIndex, int[] exerciseDecision)
    {
      if (Logger.IsDebugEnabled)
      {
        Logger.Debug("Running Backward Valuation Pass...");
      }
      var stateVariables = (workspace.U == null) ? null : workspace.U.Select(u => (u == null) ? null : (double[,])u.Clone()).ToArray();
      Action<int, Partition, double[]> svdSweep = (idx, pi, val) =>
        {
          // idx is date index.
          if ((workspace.U == null) || (workspace.U[idx] == null))
            return;
          LinearSolvers.FactorizeSVD(workspace.U[idx], workspace.W[idx], workspace.V[idx]);
          var disc = workspace.Df[idx];
          if (data.CallMc && (pi.Flag & PartitionFlag.CallValueDate) != 0)
          {
            var call = workspace.CallValue[idx];
            SvdSolve(workspace.U[idx], workspace.W[idx], workspace.V[idx], call, val);
            for (int i = 0; i < val.Length; ++i)
              call[i] = val[i]/disc[i];
          }
          if (data.PutMc && (pi.Flag & PartitionFlag.PutValueDate) != 0)
          {
            var put = workspace.PutValue[idx];
            SvdSolve(workspace.U[idx], workspace.W[idx], workspace.V[idx], put, val);
            for (int i = 0; i < val.Length; ++i)
              put[i] = val[i]/disc[i];
          }
        };
      //perform SVD decomposition in parallel
      var lastExerciseIdx = Array.FindLastIndex(partition, node => (node.Flag & ExerciseDate) != 0);
      int pathCount = simulator.PathCount;
      var callPrices = partition.Select(pi => ((pi.Flag & PartitionFlag.CallExerciseDate) != 0) ? data.Call.Price(pi.Date) : 0.0).ToArray();
      var putPrices = partition.Select(pi => ((pi.Flag & PartitionFlag.PutExerciseDate) != 0) ? data.Put.Price(pi.Date) : 0.0).ToArray();
      Parallel.For(1, lastExerciseIdx + 1, () => new double[pathCount], (t, ev) => svdSweep(t, partition[t], ev));
      var continuationValue = new double[pathCount];
      var cf = (lastExerciseIdx >= 0) ? (double[])workspace.MtM[lastExerciseIdx].Clone() : workspace.MtM[0]; //cf paid after last exercise date

      double[,] regressionBasedValueTable = null;
      double[,] continuationValueTable = null;
      double[,] callIntrinsicTable = null;
      double[,] putIntrinsicTable = null;
      double[,] mtmTable = null;
      double[,] stateVariableTable = null;

      if (BackwardAnalysisObjectLogger.IsObjectLoggingEnabled)
      {
        regressionBasedValueTable = new double[pathCount, lastExerciseIdx + 1];
        continuationValueTable = new double[pathCount, lastExerciseIdx + 1];
        callIntrinsicTable = new double[pathCount, lastExerciseIdx + 1];
        putIntrinsicTable = new double[pathCount, lastExerciseIdx + 1];
        mtmTable = new double[pathCount, lastExerciseIdx + 1];
        stateVariableTable = new double[pathCount, lastExerciseIdx + 1];
      }

      for (int t = lastExerciseIdx; t > 0; --t)
      {
        var pi = partition[t];
        bool callable = (pi.Flag & PartitionFlag.CallExerciseDate) != 0;
        bool putable = (pi.Flag & PartitionFlag.PutExerciseDate) != 0;
        bool replaceable = (pi.Flag & PartitionFlag.ReplaceCashflow) != 0;
        if ((workspace.U != null) && (workspace.U[t] != null))
          SvdSolve(workspace.U[t], workspace.W[t], workspace.V[t], cf, continuationValue);
        var df = workspace.Df[t];
        var mtmNext = workspace.MtM[t];
        var mtmPrev = workspace.MtM[t - 1];
        if (callable && putable) //game
        {
          var call = workspace.CallValue[t];
          var put = workspace.PutValue[t];
          double callPrice = callPrices[t];
          double putPrice = putPrices[t];
          for (int i = 0; i < pathCount; ++i)
          {
            double coupon = mtmPrev[i] - mtmNext[i]; //coupons paid between (T_{t-1}, T_t]
            double cv = continuationValue[i] / df[i]; //bring forward
            double callValue = call[i];
            double putValue = put[i];
            double callIntrinsic = callValue - callPrice;
            double putIntrinsic = putValue - putPrice;

            if (BackwardAnalysisObjectLogger.IsObjectLoggingEnabled && regressionBasedValueTable != null)
            {
              regressionBasedValueTable[i, t] = cf[i];
            }

            if (cv > callIntrinsic)
            {
              cf[i] = coupon + callIntrinsic * df[i]; //discount to time zero
              exerciseIndex[i] = t;
              exerciseDecision[i] = 1;
              mtmNext[i] = callIntrinsic;
            }
            else if (cv < putIntrinsic)
            {
              cf[i] = coupon + putIntrinsic * df[i];
              exerciseIndex[i] = t;
              exerciseDecision[i] = -1;
              mtmNext[i] = putIntrinsic;
            }
            else
            {
              if (replaceable)
                cf[i] += coupon + Math.Min(Math.Max(cf[i], putIntrinsic * df[i]), callIntrinsic * df[i]);
              else
                cf[i] += coupon;             
              mtmNext[i] = cv;
            }

            if (BackwardAnalysisObjectLogger.IsObjectLoggingEnabled
              && continuationValueTable != null && callIntrinsicTable != null && putIntrinsicTable != null
              && mtmTable != null && stateVariableTable != null)
            {
              continuationValueTable[i, t] = cv;
              callIntrinsicTable[i, t] = callIntrinsic;
              putIntrinsicTable[i, t] = putIntrinsic;
              mtmTable[i, t] = mtmNext[i];
              stateVariableTable[i, t] = stateVariables[t][i, 1];
            }

          }
        }
        else if (callable) //callable only
        {
          var call = workspace.CallValue[t];
          double callPrice = callPrices[t];
          for (int i = 0; i < pathCount; ++i)
          {
            double coupon = mtmPrev[i] - mtmNext[i]; //coupons paid between (T_{t-1}, T_t]
            double cv = continuationValue[i] / df[i];
            double callValue = call[i];
            double callIntrinsic = callValue - callPrice;

            if (BackwardAnalysisObjectLogger.IsObjectLoggingEnabled && regressionBasedValueTable != null)
            {
              regressionBasedValueTable[i, t] = cf[i];
            }

            if (cv > callIntrinsic)
            {
              cf[i] = coupon + callIntrinsic * df[i];
              exerciseIndex[i] = t;
              exerciseDecision[i] = 1;
              mtmNext[i] = callIntrinsic;
            }
            else
            {
              if (replaceable)
                cf[i] = coupon + Math.Min(cf[i], callIntrinsic * df[i]);
              else
                cf[i] += coupon;
              mtmNext[i] = cv;
            }

            if (BackwardAnalysisObjectLogger.IsObjectLoggingEnabled
              && continuationValueTable != null && callIntrinsicTable != null
              && mtmTable != null && stateVariableTable != null)
            {
              continuationValueTable[i, t] = cv;
              callIntrinsicTable[i, t] = callIntrinsic;
              mtmTable[i, t] = mtmNext[i];
              stateVariableTable[i, t] = stateVariables[t][i, 1];
            }
          }
        }
        else if (putable) //putable only
        {
          var put = workspace.PutValue[t];
          double putPrice = putPrices[t];
          for (int i = 0; i < pathCount; ++i)
          {
            double coupon = mtmPrev[i] - mtmNext[i]; //coupons paid between (T_{t-1}, T_t]
            double cv = continuationValue[i] / df[i];
            double putValue = put[i];
            double putIntrinsic = putValue - putPrice;

            if (BackwardAnalysisObjectLogger.IsObjectLoggingEnabled && regressionBasedValueTable != null)
            {
              regressionBasedValueTable[i, t] = cf[i];
            }

            if (cv < putIntrinsic)
            {
              cf[i] = coupon + putIntrinsic * df[i];
              exerciseIndex[i] = t;
              exerciseDecision[i] = -1;
              mtmNext[i] = putIntrinsic;
            }
            else
            {
              if (replaceable)
                cf[i] = coupon + Math.Max(cf[i], putIntrinsic * df[i]);
              else
                cf[i] += coupon;
              mtmNext[i] = cv;
            }

            if (BackwardAnalysisObjectLogger.IsObjectLoggingEnabled
              && continuationValueTable != null && putIntrinsicTable != null
              && mtmTable != null && stateVariableTable != null)
            {
              continuationValueTable[i, t] = cv;
              putIntrinsicTable[i, t] = putIntrinsic;
              mtmTable[i, t] = mtmNext[i];
              stateVariableTable[i, t] = stateVariables[t][i, 1];
            }
          }
        }
        else //not an exercise date
        {
          for (int i = 0; i < pathCount; ++i)
          {
            if (BackwardAnalysisObjectLogger.IsObjectLoggingEnabled && regressionBasedValueTable != null)
            {
              regressionBasedValueTable[i, t] = cf[i];
            }

            double coupon = mtmPrev[i] - mtmNext[i]; //coupons paid between (T_{t-1}, T_t] 
            double cv = continuationValue[i] / df[i];
            cf[i] += coupon;
            mtmNext[i] = cv;

            if (BackwardAnalysisObjectLogger.IsObjectLoggingEnabled
              && continuationValueTable != null
              && mtmTable != null && stateVariableTable != null)
            {
              continuationValueTable[i, t] = cv;
              mtmTable[i, t] = mtmNext[i];
              stateVariableTable[i, t] = stateVariables[t][i, 1];
            }
          }
        }
      }

      BackwardValuation(stateVariableTable, regressionBasedValueTable, continuationValueTable, callIntrinsicTable, putIntrinsicTable, mtmTable, partition);

      double retVal = 0.0;
      for (int i = 0, count = 1; i < pathCount; ++i, ++count)
        retVal += (cf[i] - retVal) / count;
      var currentMtM = workspace.MtM[0];
      for (int i = 0; i < pathCount; ++i)
        currentMtM[i] = retVal;

      if (Logger.IsDebugEnabled)
      {
        for (var i = 0; i < pathCount; ++i)
        {
          Logger.Debug(string.Format("Path {0}, Exercise Decision {1}>, exercise index {2}", i, exerciseDecision[i], exerciseIndex[i]));
        }
        Logger.Debug(string.Format("Fair Price {0}", retVal));
      }

      return retVal;
    }

    private static void BackwardValuation(double[,] stateVariableTable, double[,] regressionBasedValueTable, double[,] continuationValueTable,
            double[,] callIntrinsicTable, double[,] putIntrinsicTable, double[,] mtmTable, Partition[] partition)
    {
      if (BackwardAnalysisObjectLogger.IsObjectLoggingEnabled)
      {
        var objectLogAggregator = ObjectLoggerUtil.CreateObjectLogAggregator(BackwardAnalysisObjectLogger, System.Reflection.MethodBase.GetCurrentMethod());
        objectLogAggregator.Append(typeof(LeastSquaresMonteCarloPricingEngine), "StateVariableTable",
          AppenderUtil.DataTableToDataSet(DataTableFromArray(stateVariableTable, partition, "StateVariableTable")));
        objectLogAggregator.Append(typeof(LeastSquaresMonteCarloPricingEngine), "RegressionBasedValueTable",
          AppenderUtil.DataTableToDataSet(DataTableFromArray(regressionBasedValueTable, partition, "RegressionBasedValueTable")));
        objectLogAggregator.Append(typeof(LeastSquaresMonteCarloPricingEngine), "ContinuationValueTable",
          AppenderUtil.DataTableToDataSet(DataTableFromArray(continuationValueTable, partition, "ContinuationValueTable")));
        objectLogAggregator.Append(typeof(LeastSquaresMonteCarloPricingEngine), "CallIntrinsicTable",
          AppenderUtil.DataTableToDataSet(DataTableFromArray(callIntrinsicTable, partition, "CallIntrinsicTable")));
        objectLogAggregator.Append(typeof(LeastSquaresMonteCarloPricingEngine), "PutIntrinsicTable",
          AppenderUtil.DataTableToDataSet(DataTableFromArray(putIntrinsicTable, partition, "PutIntrinsicTable")));
        objectLogAggregator.Append(typeof(LeastSquaresMonteCarloPricingEngine), "mtmTable",
          AppenderUtil.DataTableToDataSet(DataTableFromArray(mtmTable, partition, "mtmTable")));
        objectLogAggregator.Log();
      }
    }

    private static DataTable DataTableFromArray(double[,] array, IReadOnlyList<Partition> partition, string key)
    {
      if (partition == null)
      {
        throw new ToolkitException("Partition is undefined.");
      }
      var dataTable = new DataTable(key);
      dataTable.Columns.Add("Path Id", typeof(int));

      for (var i = 0; i < array.GetLength(1); ++i)
      {
        dataTable.Columns.Add(partition[i].Date.ToString(), typeof(double));
      }

      for (var i = 0; i < array.GetLength(0); ++i)
      {
        var row = dataTable.NewRow();
        row[0] = i;
        for (var j = 0; j < array.GetLength(1); ++j)
        {
          row[j + 1] = array[i, j];
        }
        dataTable.Rows.Add(row);
      }

      return dataTable;
    }

    private static void DoPostProcessing(Partition[] partition,
      Workspace workspace, PricingData data, Simulator simulator,
      double tolerance, int[] exerciseIndex, int[] exerciseDecision)
    {
      int pathCount = simulator.PathCount;
      Action<int, Partition, double[]> svdSweep = (idx, pi, val) =>
        {
          if ((pi.Flag & ValueDate) == 0 || (workspace.U == null) || (workspace.U[idx] == null))
            return;
          LinearSolvers.FactorizeSVD(workspace.U[idx], workspace.W[idx], workspace.V[idx]);
          var df = workspace.Df[idx];
          if (data.UnderlierMc && (pi.Flag & PartitionFlag.MtMDate) != 0)
          {
            var mtm = workspace.MtM[idx];
            SvdSolve(workspace.U[idx], workspace.W[idx], workspace.V[idx], mtm, val);
            for (int i = 0; i < pathCount; ++i)
              mtm[i] = val[i]/df[i]; //bring forward
          }
          if (data.CallMc && (pi.Flag & PartitionFlag.CallValueDate) != 0)
          {
            var call = workspace.CallValue[idx];
            SvdSolve(workspace.U[idx], workspace.W[idx], workspace.V[idx], call, val);
            for (int i = 0; i < pathCount; ++i)
              call[i] = val[i]/df[i]; //bring forward
          }
          if (data.PutMc && (pi.Flag & PartitionFlag.PutValueDate) != 0)
          {
            var put = workspace.PutValue[idx];
            SvdSolve(workspace.U[idx], workspace.W[idx], workspace.V[idx], put, val);
            for (int i = 0; i < pathCount; ++i)
              put[i] = val[i]/df[i]; //bring forward
          }
        };
      var lastExerciseIdx = Math.Max(Array.FindLastIndex(partition, node => (node.Flag & ExerciseDate) != 0), 0);
      Parallel.For(lastExerciseIdx + 1, partition.Length, () => new double[pathCount], (t, ev) => svdSweep(t, partition[t], ev));
      if (exerciseDecision != null && exerciseIndex != null)
      {
        Action<int> process = i =>
          {
            int exerciseIdx = exerciseIndex[i];
            int exerciseDir = exerciseDecision[i];
            if (exerciseDir == 0)
              return;
            bool callExercised = (exerciseDir == 1) && !data.Call.CashSettled;
            bool putExercised = (exerciseDir == -1) && !data.Put.CashSettled;
            for (int t = exerciseIdx + 1; t < partition.Length; ++t)
            {
              if (callExercised && workspace.CallValue[t] != null)
                workspace.MtM[t][i] = workspace.CallValue[t][i];
              else if (putExercised && workspace.PutValue[t] != null)
                workspace.MtM[t][i] = workspace.PutValue[t][i];
              else
                workspace.MtM[t][i] = 0.0;
            }
          };
        Parallel.For(0, pathCount, process);
      }
    }

    private static FxRate GetFxData(MarketEnvironment environment, Currency pvCurrency, Currency numeraireCurrency)
    {
      if (numeraireCurrency != pvCurrency)
      {
        var fxRate = Array.Find(environment.FxRates,
          fx =>
            (fx.FromCcy == pvCurrency && fx.ToCcy == numeraireCurrency) ||
              (fx.FromCcy == numeraireCurrency && fx.ToCcy == pvCurrency));
        if (fxRate == null)
          throw new ToolkitException("FxRate pvCurrency/numeraireCurrency not found");
        return fxRate;
      }
      return null;
    }

    private static double[,] GenerateExposureGrid(
      IList<Partition> partition, Workspace workspace, double spotFx,
      double notional, int pathCount, int exposureCount)
    {
      var retVal = new double[pathCount, exposureCount];
      for (int lastIdx = -1, t = 0; t < partition.Count; ++t)
      {
        var idx = partition[t].ExposureIdx;
        if (idx < 0)
          continue;
        var mtm = workspace.MtM[t];
        var fx = (workspace.Fx != null) ? workspace.Fx[t] : null;
        for (int i = 0; i < pathCount; ++i)
        {
          double pv = mtm[i];
          if (t == 0)
            pv *= spotFx;
          else if (fx != null)
            pv *= fx[i];
          pv *= notional;
          for (int j = lastIdx + 1; j < idx; ++j)
          {
            retVal[i, j] = pv;
          }
          retVal[i, idx] = pv;
        }
        lastIdx = idx;
      }

      if (Logger.IsDebugEnabled)
      {
        if (retVal.GetLength(0) > 0 && retVal.GetLength(1) > 0)
        {
          var builder = new StringBuilder();
          builder.Append("[");
          for (var i = 0; i < retVal.GetLength(0) - 1; ++i)
          {
            var j = 0;
            builder.Append("[");
            for (; j < retVal.GetLength(1) -1; ++j)
            {
              builder.Append(retVal[i, j] + ",");
            }
            builder.Append(retVal[i, j] + "]");
          }
          builder.Append("]");
          Logger.Debug(builder.ToString());
        }
      }

      return retVal;
    }

		private static Tuple<double[,],double[,]> GenerateExposuresAndDiscounts(
		IList<Partition> partition, Workspace workspace, double spotFx,
		double notional, int pathCount, int exposureCount)
		{
			var exposures = new double[pathCount, exposureCount];
			var dfs = new double[pathCount, exposureCount];
			for (int t = 0; t < partition.Count; ++t)
			{
				var idx = partition[t].ExposureIdx;
				if (idx < 0)
					continue;
				var mtm = workspace.MtM[t];
				var fx = (workspace.Fx != null) ? workspace.Fx[t] : null;
				for (int i = 0; i < pathCount; ++i)
				{
					double pv = mtm[i];
					if (t == 0)
						pv *= spotFx;
					else if (fx != null)
						pv *= fx[i];
					pv *= notional;
					exposures[i, idx] = pv;
					dfs[i, idx] = t == 0 ? 1.0 : workspace.Df[t][i];
				}
			}
			return new Tuple<double[,],double[,]>(exposures,dfs) ;
		}

    private static bool Validate(int pathCount, ExerciseEvaluator call, ExerciseEvaluator put, BasisFunctions basis, IEnumerable<Dt> exposureDates)
    {
      //validation
      bool retVal = false;
      if (call != null)
      {
        if (!call.ExerciseDates.Any())
          throw new ToolkitException("Must specify call schedule if instrument is callable");
        retVal = true;
      }
      if (put != null)
      {
        if (!put.ExerciseDates.Any())
          throw new ToolkitException("Must specify put schedule if instrument is putable");
        retVal = true;
      }
      if (basis == null)
      {
        if (retVal || exposureDates.Any())
          throw new ArgumentNullException("basis");
      }
      if (basis != null)
      {
        if (basis.Dimension > pathCount)
          throw new ArgumentException(String.Format("At least {0} paths required.", basis.Dimension));
      }
      return retVal;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Compute Pv by Least Squares Monte Carlo using a multi-factor, multi-currency, multi-asset class market model.
    /// Price generic products whose fair price is given by <m>\sup_{\eta}\inf_{\tau}E\left(\sum_{t_i \leq \eta \wedge \tau} \beta_{t_i}c_i + \beta_{\tau} C_{\tau} I_{\{\tau \leq \eta \}} + \beta_{\eta} P_{\eta} I_{\{\eta \leq \tau\}}\right)</m>
    /// Where <m>c_i</m> is the coupon process, <m>P_t</m> is the put price process, and <m>C_t</m> is the call price process
    /// </summary>
    /// <param name="notional">Trade notional</param>
    /// <param name="environment">MarketEnvironment to evolve</param>
    /// <param name="simulator">Simulator for the given MarketEnvironment</param>
    /// <param name="rngType">Type of MultiStream random number generator</param>
    /// <param name="numeraireCurrency">Numeraire currency</param>
    /// <param name="underlyingCashflow">Stream of cashflows to be paid up to exercise date</param>
    /// <param name="basis">Basis</param>
    /// <param name="call">Call exercise price</param>
    /// <param name="put">Put exercise price</param>
    /// <param name="svdTolerance">The singular values whose magnitude is less than svdTolerance * maximum singular value are zeroed in order to make the Least Squares 
    /// problem stable.</param>
    /// <param name="reportDates">If want the grid of continuation values/pv by time/omega specify output time grid (must be a subset of simulator.SimulationDates)</param>
    ///<remarks>Pv is converted to the numeraire currency</remarks>
    public static double[,] Calculate(
      double notional,
      MarketEnvironment environment,
      Simulator simulator,
      MultiStreamRng.Type rngType,
      Currency numeraireCurrency,
      IList<ICashflowNode> underlyingCashflow,
      BasisFunctions basis,
      ExerciseEvaluator call,
      ExerciseEvaluator put,
      double svdTolerance,
      Dt[] reportDates)
    {
      var withOptionality = Validate(simulator.PathCount, call, put, basis, reportDates);
      var rng = MultiStreamRng.Create((rngType == MultiStreamRng.Type.None)
        ? MultiStreamRng.Type.MersenneTwister
        : rngType,
        simulator.Dimension, simulator.SimulationTimeGrid);
      var partition = GeneratePartition(simulator, reportDates, underlyingCashflow, call, put);
      var pricingData = new PricingData(numeraireCurrency, simulator, environment, underlyingCashflow, call, put, basis, partition);
      var exerciseIndex = withOptionality ? new int[simulator.PathCount] : null;
      var exerciseDecision = withOptionality ? new int[simulator.PathCount] : null;
      var workspace = DoForward(simulator, rng, pricingData, partition);
      var fairPrice = DoBackward(partition, workspace, pricingData, simulator, svdTolerance, exerciseIndex, exerciseDecision);
      if (reportDates == null || !reportDates.Any())
        return new[,] { { notional * fairPrice } };
      DoPostProcessing(partition, workspace, pricingData, simulator, svdTolerance, exerciseIndex, exerciseDecision);
      return GenerateExposureGrid(partition, workspace, (pricingData.FxRate == null)
        ? 1.0
        : pricingData.FxRate.GetRate(pricingData.Currency, pricingData.NumeraireCcy),
        notional, simulator.PathCount, reportDates.Length);
    }

	  ///  <summary>
	  ///  Compute Pv by Least Squares Monte Carlo using a multi-factor, multi-currency, multi-asset class market model.
	  ///  Price generic products whose fair price is given by <m>\sup_{\eta}\inf_{\tau}E\left(\sum_{t_i \leq \eta \wedge \tau} \beta_{t_i}c_i + \beta_{\tau} C_{\tau} I_{\{\tau \leq \eta \}} + \beta_{\eta} P_{\eta} I_{\{\eta \leq \tau\}}\right)</m>
	  ///  Where <m>c_i</m> is the coupon process, <m>P_t</m> is the put price process, and <m>C_t</m> is the call price process
	  ///  </summary>
	  ///  <param name="notional">Trade notional</param>
	  ///  <param name="environment">MarketEnvironment to evolve</param>
	  ///  <param name="simulator">Simulator for the given MarketEnvironment</param>
	  ///  <param name="numeraireCurrency">Numeraire currency</param>
	  ///  <param name="underlyingCashflow">Stream of cashflows to be paid up to exercise date</param>
	  ///  <param name="basis">Basis</param>
	  ///  <param name="call">Call exercise price</param>
	  ///  <param name="put">Put exercise price</param>
	  ///  <param name="svdTolerance">The singular values whose magnitude is less than svdTolerance * maximum singular value are zeroed in order to make the Least Squares 
	  ///  problem stable.</param>
	  ///  <param name="reportDates">If want the grid of continuation values/pv by time/omega specify output time grid (must be a subset of simulator.SimulationDates)</param>
	  /// <param name="getPath">callback for retrieving cached path</param>
	  /// <remarks>Pv is converted to the numeraire currency</remarks>
		public static Tuple<double[,], double[,]> Calculate(
			double notional,
			MarketEnvironment environment,
			Simulator simulator,
			Currency numeraireCurrency,
			IList<ICashflowNode> underlyingCashflow,
			BasisFunctions basis,
			ExerciseEvaluator call,
			ExerciseEvaluator put,
			double svdTolerance,
			Dt[] reportDates, 
			Func<int, SimulatedPath> getPath)
	  {
		  var withOptionality = Validate(simulator.PathCount, call, put, basis, reportDates);
			var partition = GeneratePartition(simulator, reportDates, underlyingCashflow, call, put);
			var pricingData = new PricingData(numeraireCurrency, simulator, environment, underlyingCashflow, call, put, basis, partition);
			var exerciseIndex = withOptionality ? new int[simulator.PathCount] : null;
			var exerciseDecision = withOptionality ? new int[simulator.PathCount] : null;
			var workspace = DoForward(simulator, pricingData, partition, getPath);
			var fairPrice = DoBackward(partition, workspace, pricingData, simulator, svdTolerance, exerciseIndex, exerciseDecision);
			if (reportDates == null || !reportDates.Any())
				return new Tuple<double[,], double[,]>(new[,] { { notional * fairPrice } }, new[,] { { 1.0 } });
			DoPostProcessing(partition, workspace, pricingData, simulator, svdTolerance, exerciseIndex, exerciseDecision);
			return GenerateExposuresAndDiscounts(partition, workspace, (pricingData.FxRate == null) ? 1.0 : pricingData.FxRate.GetRate(pricingData.Currency, pricingData.NumeraireCcy), notional, simulator.PathCount, reportDates.Length);
	  }


    /// <summary>
    /// Compute array of perturbed Pvs by Least Squares Monte Carlo using a multi-factor, multi-currency, multi-asset class market model.
    /// Price generic products whose fair price is given by <m>\sup_{\eta}\inf_{\tau}E\left(\sum_{t_i \leq \eta \wedge \tau} c_i + C_{\tau} I_{\{\tau \leq \eta \}} + P_{\eta} I_{\{\eta \leq \tau\}}\right)</m>
    /// Where <m>P_t</m> is the put price process, and <m>C_t</m> is the call price process
    /// </summary>
    /// <param name="notional">Trade notional</param>
    /// <param name="environment">MarketEnvironment to evolve</param>
    /// <param name="simulator">Simulator for the given MarketEnvironment</param>
    /// <param name="perturbedSimulators">Perturbed simulators</param>
    /// <param name="rngType">Type of MultiStream random number generator</param>
    /// <param name="numeraireCurrency">Numeraire currency</param>
    /// <param name="underlyingCashflow">Stream of cashflows to be paid up to exercise date</param>
    /// <param name="basis">Basis</param>
    /// <param name="call">Call exercise price</param>
    /// <param name="put">Put exercise price</param>
    /// <param name="svdTolerance">The singular values whose magnitude is less than svdTolerance * maximum singular value are zeroed in order to make the Least Squares 
    /// problem stable.</param>
    /// <param name="reportDates">If want the grid of continuation values/pv by time/omega specify output time grid </param>
    ///<remarks>Pv is converted to the numeraire currency</remarks>
    public static IEnumerable<double[,]> Calculate(
      double notional,
      MarketEnvironment environment,
      Simulator[] perturbedSimulators,
      Simulator simulator,
      MultiStreamRng.Type rngType,
      Currency numeraireCurrency,
      IList<ICashflowNode> underlyingCashflow,
      BasisFunctions basis,
      ExerciseEvaluator call,
      ExerciseEvaluator put,
      double svdTolerance,
      Dt[] reportDates)
    {
      var withOptionality = Validate(simulator.PathCount, call, put, basis, reportDates);
      var rng = MultiStreamRng.Create((rngType == MultiStreamRng.Type.None) ? MultiStreamRng.Type.MersenneTwister : rngType, simulator.Dimension,
                                      simulator.SimulationTimeGrid);
      var partition = GeneratePartition(simulator, reportDates, underlyingCashflow, call, put);
      var pricingData = new PricingData(numeraireCurrency, simulator, environment, underlyingCashflow, call, put, basis, partition);
      var perturbedWorkspaces = DoForward(simulator, perturbedSimulators, rng, pricingData, partition);
      double[,] baseGrid = null;
      foreach (var workspace in perturbedWorkspaces)
      {
        if (workspace != null)
        {
          var exerciseIndex = withOptionality ? new int[simulator.PathCount] : null;
          var exerciseDecision = withOptionality ? new int[simulator.PathCount] : null;
          double fairPrice = DoBackward(partition, workspace, pricingData, simulator, svdTolerance, exerciseIndex, exerciseDecision);
          if (reportDates == null || !reportDates.Any())
          {
            var retVal = new[,] { { notional * fairPrice } };
            if (baseGrid == null)
              baseGrid = retVal;
            yield return retVal;
          }
          else
          {
            DoPostProcessing(partition, workspace, pricingData, simulator, svdTolerance, exerciseIndex, exerciseDecision);
            var retVal = GenerateExposureGrid(partition, workspace,
              (pricingData.FxRate == null) ? 1.0 : pricingData.FxRate.GetRate(pricingData.Currency, pricingData.NumeraireCcy),
              notional, simulator.PathCount, reportDates.Length);
            if (baseGrid == null)
              baseGrid = retVal;
            yield return retVal;
          }
        }
        else
          yield return baseGrid;
      }
    }

    #endregion

    #region Methods without MarketEnvironments
    /// <summary>
    /// Compute Pv by Least Squares Monte Carlo using a multi-factor, multi-currency, multi-asset class market model.
    /// Price generic products whose fair price is given by <m>\sup_{\eta}\inf_{\tau}E\left(\sum_{t_i \leq \eta \wedge \tau} c_i + C_{\tau} I_{\{\tau \leq \eta \}} + P_{\eta} I_{\{\eta \leq \tau\}}\right)</m>
    /// Where <m>P_t</m> is the put price process, and <m>C_t</m> is the call price process
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="gridSize">Grid size for simulation step</param>
    /// <param name="simulationDates">Dates at which to simulate the market environment. 
    /// Market environments on interim dates are generated by as the <m>L^2</m> projection, 
    /// on immediately preceding and immediately following realized market environments.
    /// </param>
    /// <param name="pathCount">Number of realizations</param>
    /// <param name="rngType">Type of MultiStream random number generator</param>
    /// <param name="rateCurves">Underlying discount curves</param>
    /// <param name="volatilities">Libor rate volatilities</param>
    /// <param name="factorLoadings">Libor rate factor loadings</param>
    /// <param name="fxRates">Underlying spot FX rates</param>
    /// <param name="fwdPriceCurves">Underlying forward price curves</param>
    /// <param name="creditCurves">Underlying survival curves</param>
    /// <param name="notional">Trade notional in numeraire currency</param>
    /// <param name="pvCurrency">Denomination currency of underlyingAsset, Call and Put</param>
    /// <param name="numeraireCurrency">Numeraire currency</param>
    /// <param name="underlyingCashflow">Stream of cashflows to be paid up to exercise date</param>
    /// <param name="basis">Basis</param>
    /// <param name="call">Call exercise exerciseEvaluator</param>
    /// <param name="put">Put exercise exerciseEvaluator</param>
    /// <param name="svdTolerance">The singular values whose magnitude is less than svdTolerance * maximum singular value are zeroed in order to make the Least Squares problem stable.</param>
    /// <param name="reportDates">Report pv per path on report dates</param>
    ///<remarks>Pv is converted to the numeraire currency</remarks>
    internal static double[,] Calculate(
      Dt asOf, Dt[] tenors,
      Tenor gridSize,
      IList<Dt> simulationDates,
      int pathCount,
      MultiStreamRng.Type rngType,
      DiscountCurve[] rateCurves,
      FxRate[] fxRates,
      CalibratedCurve[] fwdPriceCurves,
      SurvivalCurve[] creditCurves,
      VolatilityCollection volatilities,
      FactorLoadingCollection factorLoadings,
      double notional,
      Currency pvCurrency,
      Currency numeraireCurrency,
      IList<ICashflowNode> underlyingCashflow,
      BasisFunctions basis,
      ExerciseEvaluator call,
      ExerciseEvaluator put,
      double svdTolerance,
      Dt[] reportDates)
    {
      if (volatilities == null || factorLoadings == null)
        throw new ToolkitException("Require non null rateVolatilities and rateFactorCorrelations");
      var environment = MarketEnvironment.Create(asOf, tenors, gridSize, pvCurrency, numeraireCurrency, volatilities, factorLoadings, rateCurves,
                                       fxRates, fwdPriceCurves, creditCurves);
      var partition = Simulator.GetDefaultSimulationDates(asOf, simulationDates, underlyingCashflow, call, put, gridSize);
      using (var simulator = Simulator.Create(pathCount, partition, environment,
                                                         volatilities, factorLoadings, EmptyArray<int>.Instance, -100.0))
      {

        var retVal = Calculate(notional, environment, simulator, rngType,
          numeraireCurrency, underlyingCashflow,
          basis ?? BasisFunctions.GetDefaultBasisFunctions(underlyingCashflow, call, put),
          call, put, svdTolerance, reportDates);
        return retVal;
      }
    }

    #endregion

    #region Nested types: Realization Generators

    internal interface IRealizationGenerator
    {
      /// <summary>
      /// Generates the realizations and fills them in the workspace
      /// </summary>
      /// <param name="path">The path.</param>
      /// <param name="workspace">The workspace.</param>
      void GetRealization(SimulatedPath path, Workspace workspace);
    }

    internal interface IRealizationGeneratorBuilder
    {
      /// <summary>
      /// Gets the realization generator for the specified pricing data.
      /// </summary>
      /// <param name="pricingData">The pricing data.</param>
      /// <param name="partition">The partition.</param>
      /// <param name="simulationDates">The simulation dates.</param>
      /// <returns>IRealizationGenerator.</returns>
      IRealizationGenerator GetGenerator(PricingData pricingData,
        IList<Partition> partition, Dt[] simulationDates);
    }

    private class PlainRealizationGenerator : IRealizationGenerator
    {
      private readonly PricingData _data;
      private readonly IList<Partition> _partition;

      internal PlainRealizationGenerator(
        PricingData pricingData, IList<Partition> partition)
      {
        _data = pricingData;
        _partition = partition;
      }

      /// <summary>
      /// Generates the realizations and fills them in the workspace
      /// </summary>
      /// <param name="path">The path.</param>
      /// <param name="workspace">The workspace.</param>
      public void GetRealization(SimulatedPath path, Workspace workspace)
      {
        GenerateRealization(path, _data, _partition, workspace);
      }
    }

    private class PlainGeneratorBuilder : IRealizationGeneratorBuilder
    {
      /// <summary>
      /// Gets the realization generator for the specified pricing data.
      /// </summary>
      /// <param name="pricingData">The pricing data.</param>
      /// <param name="partition">The partition.</param>
      /// <param name="simulationDates">The simulation dates.</param>
      /// <returns>IRealizationGenerator.</returns>
      public IRealizationGenerator GetGenerator(PricingData pricingData,
        IList<Partition> partition, Dt[] simulationDates)
        => new PlainRealizationGenerator(pricingData, partition);
    }

    private static IRealizationGenerator GetRealizationGenerator(
      PricingData pricingData, IList<Partition> partition,
      Dt[] simulationDates)
    {
      var builder = GetBuilder();
      return builder?.GetGenerator(pricingData, partition, simulationDates)
        ?? PlainBuilder.GetGenerator(pricingData, partition, simulationDates);
    }

    private static IRealizationGeneratorBuilder GetBuilder()
    {
      var builder = Builder;
      if (builder != null) return builder;

      // Currently PEO-AMC is not enabled when PEO in general is not enabled.
      var settings = ToolkitConfigurator.Settings.CcrPricer;
      if (string.IsNullOrEmpty(settings.OptimizerFactory) ||
        !settings.OptimizerFactory.Contains("BaseEntity.Toolkit.Peo"))
      {
        return null;
      }

      // Load the generator specified through configuration.
      var s = settings.AmcRealizationGeneratorBuilder;
      if (string.IsNullOrEmpty(s)) return null;

      var type = Type.GetType(s);
      if (type == null)
      {
        // TODO: logging an error?
        return null;
      }
      Builder = builder = (IRealizationGeneratorBuilder)Activator.CreateInstance(type);
      return builder;
    }

    internal static readonly IRealizationGeneratorBuilder PlainBuilder
      = new PlainGeneratorBuilder();

    internal static IRealizationGeneratorBuilder Builder { get; set; }

    internal static bool IsValueDate(Partition node)
    {
      return (node.Flag & ValueDate) != 0;
    }

    internal static bool IsPutExerciseDate(Partition node)
    {
      return (node.Flag & PartitionFlag.PutExerciseDate) != 0;
    }

    // This is mainly for debugging and testing
    internal static Action<Workspace> WorkspaceLogger { get; set; }

    #endregion
  }

  #endregion

  #region PricerExerciseEvaluator

  /// <summary>
  /// Wrap pricer and schedule in ExerciseEvaluator object.
  /// </summary>
  [Serializable]
  internal sealed class PricerExerciseEvaluator : ExerciseEvaluator
  {
    #region Properties

    /// <summary>
    /// Pricer
    /// </summary>
    public IPricer Underlier { get; private set; }

    /// <summary>
    /// Exercise price
    /// </summary>
    public Func<Dt, double> ExercisePrice { get; private set; }

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="exerciseDates">Exercise dates</param>
    /// <param name="cashSettled">Settlement type</param>
    /// <param name="pricer">Underling pricer</param>
    /// <param name="exercisePrice">Schedule of exercise prices</param>
    public PricerExerciseEvaluator(IEnumerable<Dt> exerciseDates, bool cashSettled, IPricer pricer, Func<Dt, double> exercisePrice)
      : base(exerciseDates, cashSettled, pricer.Product.Maturity)
    {
      Underlier = pricer;
      ExercisePrice = exercisePrice;
    }
    #endregion

    [OnSerializing]
    void WrapDelegates(StreamingContext context)
    {
      ExercisePrice = ExercisePrice.WrapSerializableDelegate();
    }

    [OnSerialized, OnDeserialized]
    void UnwrapDelegates(StreamingContext context)
    {
      ExercisePrice = ExercisePrice.UnwrapSerializableDelegate();
    }

  }

  #endregion
}
