using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Models.Simulations
{

  /// <summary>
  /// The weighting method used for estimating a quantile. For details on L-Estimators see e.g Mausser "Calculating Quantile-based Risk Analytics with L-estimators"
  /// </summary>
  public enum LEstimatorMethod
  {
    /// <summary>
    /// Upper Empirical Cumulative Distribution Function Value
    /// </summary>
    UECV,
    /// <summary>
    /// Empirical Cumulative Distribution Function Value
    /// </summary>
    ECV, 
    /// <summary>
    /// Harrell, F.E. and C.E. Davis, 1982, “A new distribution-free quantile estimator,” Biometrika, 69(3): 635–640
    /// </summary>
    HarrellDavis, 
    /// <summary>
    /// Interpolate linearly between UECV and ECV
    /// </summary>
    PiecewiseLinear
  }

  #region HistoricVarEngine

  /// <summary>
  /// HistoricVarEngine
  /// </summary>
  [Serializable]
  public class HistoricVarEngine
  {
    #region Properties

    /// <summary>
    /// As of date
    /// </summary>
    public Dt AsOf { get; private set; }

    /// <summary>
    /// Domestic Currency. Valuations are expressed in this currency. 
    /// </summary>
    public Currency DomesticCcy { get; private set; }

    /// <summary>
    /// The pricers to evaluate under each historic shift
    /// </summary>
    public CcrPricer[] Pricers { get; private set; }

    /// <summary>
    /// Curves to be shifted, these should be referenced by Pricers
    /// </summary>
    public Curve[] Curves { get; private set; }

    /// <summary>
    /// Tenors corresponding to each curve
    /// </summary>
    public Tenor[][] Tenors { get; private set; }

    /// <summary>
    /// Are shifts absolute or relative for Curves
    /// </summary>
    public bool[] CurveShiftRelative { get; private set; }

    /// <summary>
    /// Spot Fx Rates. 
    /// </summary>
    public FxRate[] FxRates { get; private set; }

    /// <summary>
    /// Are shifts absolute or relative for FXRates
    /// </summary>
    public bool[] FxShiftRelative { get; private set; }


    /// <summary>
    /// Stock price curves 
    /// </summary>
    public StockCurve[] StockCurves { get; private set; }

    /// <summary>
    /// Are shifts relative or absolute for stock prices 
    /// </summary>
    public bool[] StockShiftsRelative { get; private set; }

    /// <summary>
    /// Historic dates that shifts are provided for
    /// </summary>
    public Dt[] SimulationDates { get; private set; }

    /// <summary>
    /// Shifted Curve Values by [curveIdx][dtIdx][tenorIdx]
    /// </summary>
    private double[][][] ShiftedCurveValues { get; set; }

    /// <summary>
    /// Shifted FX Values by [fxIdx][dtIdx]
    /// </summary>
    private double[][] ShiftedFXValues { get; set; }


    /// <summary>
    /// Shifted Stock Values by [stockIdx][dtIdx]
    /// </summary>
    private double[][] ShiftedStockValues { get; set; }

    /// <summary>
    /// Simulated Losses by [dtIdx][pricerIdx]
    /// </summary>
    private double[][] SimulatedLosses { get; set; }

    /// <summary>
    /// Sorts scenarios by order of loss
    /// </summary>
    private int[] OrderIndex { get; set; }
    private Dictionary<Dt, int> DtMap { get; set; }
    private Dictionary<string, int> CurveNameMap { get; set; }
    private Dictionary<Currency, int> FXMap { get; set; }
    private Dictionary<string, int> StockNameMap { get; set; }
    private double[] CumulativeProbability { get; set; }
    private double DecayParam { get; set; }
    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">asOf date</param>
    /// <param name="domesticCcy">domestic currency</param>
    /// <param name="pricers"> </param>
    /// <param name="curves">Term structure of forward prices</param>
    /// <param name="tenors"> tenors that will be bumped for curve</param>
    /// <param name="curveShiftRelative">curve shift relative or absolute</param>
    /// <param name="fx">FX Spot rates</param>
    /// <param name="fxShiftRelative">FX shift relative or absolute</param>
    /// <param name="stocks"> Stock price curves</param>
    /// <param name="stockShiftRelative">stock price shift relative or absolute</param>
    /// <param name="shiftDts">historic observation dates that shifts are generated from</param>
    /// <param name="ageWeighted">weight scenarios with weight decaying exponentially with age</param>
    /// <param name="lambda">age weighted decay param (must be between 0 and 1)</param>
    public HistoricVarEngine(Dt asOf, Currency domesticCcy, IPricer[] pricers, Curve[] curves, Tenor[][] tenors, bool[] curveShiftRelative, FxRate[] fx, bool[] fxShiftRelative, StockCurve[] stocks, bool[] stockShiftRelative, Dt[] shiftDts, bool ageWeighted, double lambda)
    {
      AsOf = asOf;
      DomesticCcy = domesticCcy;
      var tuple = CloneUtil.CloneObjectGraph(pricers, curves, fx, stocks);
      Pricers = Array.ConvertAll(tuple.Item1, CcrPricer.Get);
      Curves = tuple.Item2;
      FxRates = tuple.Item3;
      StockCurves = tuple.Item4;
      Tenors = tenors;
      CurveShiftRelative = curveShiftRelative;
      FxShiftRelative = fxShiftRelative;
      StockShiftsRelative = stockShiftRelative;
      SimulationDates = shiftDts; 
      SimulatedLosses = new double[shiftDts.Length][];
      OrderIndex = new int[shiftDts.Length];
      ShiftedCurveValues = new double[curves.Length][][];
      CurveNameMap = new Dictionary<string, int>();
      for (int i = 0; i < curves.Length; i++)
      {
        ShiftedCurveValues[i] = new double[shiftDts.Length][];
        for (int j = 0; j < shiftDts.Length; j++)
        {
          ShiftedCurveValues[i][j] = new double[tenors[i].Length];
        }
        CurveNameMap[curves[i].Name] = i; 
      }
      FXMap = new Dictionary<Currency, int>();
      ShiftedFXValues = new double[fx.Length][];
      for (int i = 0; i < fx.Length; i++)
      {
        if(fx[i].FromCcy != domesticCcy)
          FXMap[fx[i].FromCcy] = i; 
        else
          FXMap[fx[i].ToCcy] = i; 
        ShiftedFXValues[i] = new double[shiftDts.Length];
      }
      DtMap = new Dictionary<Dt, int>();
      for (int i = 0; i < shiftDts.Length; i++)
      {
        DtMap[shiftDts[i]] = i; 
        SimulatedLosses[i] = new double[pricers.Length];
      }

      ShiftedStockValues = new double[stocks.Length][];
      StockNameMap = new Dictionary<string, int>();
      for (int i = 0; i < stocks.Length; i++)
      {
        ShiftedStockValues[i] = new double[shiftDts.Length];
        StockNameMap[stocks[i].Name] = i;
      }

      Conform();
      if (ageWeighted)
        DecayParam = lambda;
      else
        DecayParam = -1.0; 
    }

    #endregion

    #region Methods
    /// <summary>
    /// Run all scenarios and store simulated losses
    /// </summary>
    public void SimulateLosses()
    {
      var baselinePvs = Evaluate(Dt.Empty);
      double k  = SimulationDates.Length;
      var totalLosses = new double[SimulationDates.Length];
      double[] probs;
      if(DecayParam > 0.0)
      {
        double w = (1.0 - DecayParam) / (1.0 - Math.Pow(DecayParam, k));
        probs = ArrayUtil.Generate(SimulationDates.Length, n => w * Math.Pow(DecayParam, n)).Reverse().ToArray();
      }
      else
      {
        probs = ArrayUtil.Generate(SimulationDates.Length, n => 1.0 / k);
      }

      Parallel.ForEach(SimulationDates,
                       () => CloneUtil.CloneObjectGraph(Pricers, Curves, FxRates, StockCurves),
                       (dt, tuple) =>
                       {
                         var pvs = Evaluate(dt, tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
                         var losses = ArrayUtil.Generate(pvs.Length, i => pvs[i] - baselinePvs[i]);
                         SimulatedLosses[DtMap[dt]] = losses;
                         totalLosses[DtMap[dt]] = losses.Sum(); 
                       });

      var indexedLosses = totalLosses.Select((l, i) => new {Index = i, Loss = l, Prob = probs[i]}).ToList();
      var orderStatistics = from l in indexedLosses orderby l.Loss descending select l.Index;
      OrderIndex = orderStatistics.ToArray(); 

      var sortedProbs = (from l in indexedLosses orderby l.Loss descending select l.Prob).ToList();
      CumulativeProbability = new double[SimulationDates.Length];
      CumulativeProbability[0] = sortedProbs[0];
      for (int i = 1; i < k; i++)
      {
        CumulativeProbability[i] = CumulativeProbability[i - 1] + sortedProbs[i];
      }
 
      Debug.Assert(Math.Abs(1.0 - CumulativeProbability.Last()) < 1.0E-5,
                   String.Format("Probabilities should approximately sum to 1. Actual Cumulative total [{0}]", CumulativeProbability.Last()));
      CumulativeProbability[CumulativeProbability.Length - 1] = 1.0; 
    }

    /// <summary>
    /// Calculate VaR at selected quantile, allocated out by pricer
    /// </summary>
    /// <param name="alpha">the quantile</param>
    /// <param name="lEstimatorMethod">the weighting scheme for estimating quantile</param>
    public double[] AllocateValueAtRisk(double alpha, LEstimatorMethod lEstimatorMethod)
    {
      var allocatedVaR = new double[Pricers.Length];
      LEstimator lEstimator;
      switch (lEstimatorMethod)
      {
          case LEstimatorMethod.UECV:
            lEstimator = new UECVEstimator(alpha, CumulativeProbability);
            break;
          case LEstimatorMethod.ECV:
            lEstimator = new ECVEstimator(alpha, CumulativeProbability);
            break;
          case LEstimatorMethod.HarrellDavis:
            lEstimator = new HarrelDavisEstimator(alpha, CumulativeProbability);
            break;         
          case LEstimatorMethod.PiecewiseLinear:
            lEstimator = new PiecewiseLinearEstimator(alpha, CumulativeProbability);
            break;
          default: 
            throw new ToolkitException("Unsupported L-Estimator Method {0}", lEstimatorMethod);
      }
      for (int k = 1; k <= SimulationDates.Length; k++)
      {
        int kIdx = OrderIndex[k-1];
        var weightK = lEstimator.Weight(k);
        for (int j = 0; j < Pricers.Length; j++)
        {
          allocatedVaR[j] += weightK * SimulatedLosses[kIdx][j];
        }
      }
      return allocatedVaR; 
    }

    /// <summary>
    /// Calculate total VaR at selected quantile
    /// </summary>
    /// <param name="alpha">the quantile</param>
    /// <param name="lEstimatorMethod"> </param>
    public double ValueAtRisk(double alpha, LEstimatorMethod lEstimatorMethod)
    {
      var valueAtRisk = 0.0;
      LEstimator lEstimator;
      switch (lEstimatorMethod)
      {
        case LEstimatorMethod.UECV:
          lEstimator = new UECVEstimator(alpha, CumulativeProbability);
          break;
        case LEstimatorMethod.ECV:
          lEstimator = new ECVEstimator(alpha, CumulativeProbability);
          break;
        case LEstimatorMethod.HarrellDavis:
          lEstimator = new HarrelDavisEstimator(alpha, CumulativeProbability);
          break;
        case LEstimatorMethod.PiecewiseLinear:
          lEstimator = new PiecewiseLinearEstimator(alpha, CumulativeProbability);
          break;
        default:
          throw new ToolkitException("Unsupported L-Estimator Method {0}", lEstimatorMethod);
      }

      for (int k = 1; k <= SimulationDates.Length; k++)
      {
        int kIdx = OrderIndex[k - 1];
        var weightK = lEstimator.Weight(k);
        valueAtRisk += weightK * SimulatedLosses[kIdx].Sum();
      }
      return valueAtRisk;
    }

    /// <summary>
    /// Calculate ES at selected quantile, allocated out by pricer
    /// </summary>
    /// <param name="alpha">the quantile</param>
    public double[] AllocateExpectedShortfall(double alpha)
    {
      var allocatedES = new double[Pricers.Length];
      int M = SimulationDates.Length;
      var uecv = new UECVEstimator(alpha, CumulativeProbability);
      var kMax = uecv.K; 
      for (int j = 0; j < Pricers.Length; j++)
      {
        int kIdx = OrderIndex[kMax-1];
        var varJ = SimulatedLosses[kIdx][j];
        allocatedES[j] = ((double)kMax - M * alpha) * varJ; 
        for (int k = kMax + 1; k <= M; k++)
        {
          kIdx = OrderIndex[k-1];
          allocatedES[j] += SimulatedLosses[kIdx][j];
        }
        allocatedES[j] /= (double)M * (1.0 - alpha);
      }
      return allocatedES;
    }

    /// <summary>
    /// Calculate total ES at selected quantile
    /// </summary>
    /// <param name="alpha">the quantile</param>
    public double ExpectedShortfall(double alpha)
    {
      var es = 0.0;
      int M = SimulationDates.Length;
      var uecv = new UECVEstimator(alpha, CumulativeProbability);
      var kMax = uecv.K; 
      int kIdx = OrderIndex[kMax - 1];
      var varK = SimulatedLosses[kIdx].Sum();
      es = ((double)kMax - M * alpha) * varK;
      for (int k = kMax + 1; k <= M; k++)
      {
        kIdx = OrderIndex[k - 1];
        es += SimulatedLosses[kIdx].Sum();
      }
      es /= (double)M * (1.0 - alpha);
      return es;
    }


    /// <summary>
    /// Simulated losses by [dt][pricer]
    /// </summary>
    public double[][] SimulatedLossesByTrade
    {
      get { return Array.ConvertAll(SimulationDates, dt => SimulatedLosses[DtMap[dt]]); }
    }

    /// <summary>
    /// Evaluate Pv() for each Pricer under shifts for selected date. Returns values in DomesticCcy.
    /// </summary>
    private double[] Evaluate(Dt dt, CcrPricer[] pricers, Curve[] curves, FxRate[] fxRates, StockCurve[] stocks)
    {
      var pvs = new double[pricers.Length];
      if (!dt.IsEmpty())
        ApplyShifts(dt, curves, fxRates, stocks);
      for (int i = 0; i < pricers.Length; i++)
      {
        pvs[i] = pricers[i].FastPv(AsOf);
        var ccy = pricers[i].Ccy;
        if (ccy != DomesticCcy)
        {
          var fxIdx = FXMap[ccy];
          pvs[i] *= fxRates[fxIdx].GetRate(ccy, DomesticCcy);
        }
      }
      return pvs;
    }


    /// <summary>
    /// Evaluate Pv() for each Pricer under shifts for selected date. Returns values in DomesticCcy.
    /// </summary>
    /// <param name="dt"></param>
    private double[] Evaluate(Dt dt)
    {
      return Evaluate(dt, Pricers, Curves, FxRates, StockCurves);
    }

    /// <summary>
    /// Add shift records 1 at a time
    /// </summary>
    /// <param name="dt">shift date</param>
    /// <param name="name">Curve.Name</param>
    /// <param name="shift"></param>
    public void AddCurveShift(Dt dt, string name, double[] shift)
    {
      if(!DtMap.ContainsKey(dt))
        throw new ArgumentException(String.Format("Dt {0} is not a Simulation Date", dt));
      int dtIdx = DtMap[dt]; 
      if(!CurveNameMap.ContainsKey(name))
        throw new ArgumentException(String.Format("Curve {0} is not in environment", name));
      int curveIdx = CurveNameMap[name];
      if(shift.Length != Tenors[curveIdx].Length)
        throw new ArgumentException(String.Format("curve {0} has {1} tenors. Attempt to add shift with {2} tenors", name, Tenors[curveIdx].Length, shift.Length));

      bool relative = CurveShiftRelative[curveIdx];

      var curve = Curves[curveIdx];
      ShiftedCurveValues[curveIdx][dtIdx] = new double[shift.Length];
      for (int i = 0; i < shift.Length; i++)
      {
        double originalValue = curve.GetVal(i);
        double shiftAmount = shift[i];
        if (relative)
        {
          shiftAmount = originalValue * shiftAmount;
        }
        ShiftedCurveValues[curveIdx][dtIdx][i] = originalValue + shiftAmount;   
      }
      
    }

    /// <summary>
    /// Add shift records 1 at a time
    /// </summary>
    /// <param name="dt">shift date</param>
    /// <param name="name">Curve.Name</param>
    /// <param name="shift"></param>
    public void AddStockPriceShift(Dt dt, string name, double shift)
    {
      if (!DtMap.ContainsKey(dt))
        throw new ArgumentException(String.Format("Dt {0} is not a Simulation Date", dt));
      int dtIdx = DtMap[dt];
      if (!StockNameMap.ContainsKey(name))
        throw new ArgumentException(String.Format("Stock {0} is not in environment", name));
      int curveIdx = StockNameMap[name];
      
      bool relative = StockShiftsRelative[curveIdx];

      var curve = StockCurves[curveIdx];
      double originalValue = curve.SpotPrice; 
      double shiftAmount = shift;
      if (relative)
      {
        shiftAmount = originalValue * shiftAmount;
      }
      ShiftedStockValues[curveIdx][dtIdx] = originalValue + shiftAmount;
    }


    /// <summary>
    /// Add FX shift records 1 at a time
    /// </summary>
    /// <param name="dt">shift date</param>
    /// <param name="foreignCcy">the foreign currency</param>
    /// <param name="shift"></param>
    public void AddFXShift(Dt dt, Currency foreignCcy, double shift)
    {
      if (!DtMap.ContainsKey(dt))
        throw new ArgumentException(String.Format("Dt {0} is not a Simulation Date", dt));
      int dtIdx = DtMap[dt];
      if (!FXMap.ContainsKey(foreignCcy))
        throw new ArgumentException(String.Format("FX Rate {0}{1} is not in environment", foreignCcy, DomesticCcy));
      int fxIdx = FXMap[foreignCcy];
      bool relative = FxShiftRelative[fxIdx];
      var fxRate = FxRates[fxIdx];
      double originalValue = fxRate.Rate;
      double shiftAmount = shift; 
      if (relative)
      {
        shiftAmount = originalValue * shift;
      }
      ShiftedFXValues[fxIdx][dtIdx] = originalValue + shiftAmount;
    }


    /// <summary>
    /// Conform curve tenors to those explicitely provided.  
    /// </summary>
    public void Conform()
    {
      for (int i = 0; i < Curves.Length; i++)
      {
        var fx = Curves[i] as FxCurve;
        if(fx != null && Tenors[i].Length == 1)
          continue;
        Conform(Curves[i], Tenors[i]);
      }
    }

    //Other interpolations are too time consuming
    private static void ResetInterp(Curve curve)
    {
      var method = InterpMethod.Custom;
      try
      {
        method = curve.InterpMethod;
      }
      catch (Exception)
      {
      }
      if (method == InterpMethod.Weighted || method == InterpMethod.Linear)
        return;
      Extrap lower = new Const();
      Extrap upper = new Smooth();
      curve.Interp = new Linear(upper, lower);
    }


    /// <summary>
    /// Conform curve tenors to those explicitely provided
    /// </summary>
    /// <param name="curve">CalibratedCurve object</param>
    /// <param name="tenors">Given tenors</param>
    private void Conform(Curve curve, Tenor[] tenors)
    {
      if (curve == null)
        return;
      var dts = Array.ConvertAll(tenors, t => Dt.Roll(Dt.Add(AsOf, t), BDConvention.Following, Calendar.None));
      Array.Sort(dts);      
      if (dts[0] <= curve.AsOf)
        curve.AsOf = AsOf;
      double[] y = Array.ConvertAll(dts, curve.Interpolate);
      curve.Clear();
      ResetInterp(curve);
      curve.Add(dts, y);
    }


    private void ApplyShifts(Dt dt, Curve[] curves, FxRate[] fxRates, StockCurve[] stocks)
    {
      var dtIdx = DtMap[dt];
      for (int curveIdx = 0; curveIdx < curves.Length; curveIdx++)
      {
        var curve = curves[curveIdx];
        for (int tenorIdx = 0; tenorIdx < Tenors[curveIdx].Length; tenorIdx++)
        {
          curve.SetVal(tenorIdx, ShiftedCurveValues[curveIdx][dtIdx][tenorIdx]);
        }
      }

      for (int fxIdx = 0; fxIdx < fxRates.Length; fxIdx++)
      {
        var fx = fxRates[fxIdx];
        fx.SetRate(fx.FromCcy, fx.ToCcy, ShiftedFXValues[fxIdx][dtIdx]);
      }

      for (int stockIdx = 0; stockIdx < stocks.Length; stockIdx++)
      {
        var stockForwardCurve = stocks[stockIdx];
        stockForwardCurve.SetVal(0, ShiftedStockValues[stockIdx][dtIdx]);
        stockForwardCurve.Spot.Value = ShiftedStockValues[stockIdx][dtIdx];
      }
    }


    #endregion

    #region L-Estimators

    private abstract class LEstimator
    {
      public abstract double Weight(int k); 
    }

    private class HarrelDavisEstimator : LEstimator
    {
      private Beta _beta;
      private double[] _prob; 
      public HarrelDavisEstimator(double alpha, double[] cumulativeProb)
      {
        int S = cumulativeProb.Length; 
        var a = (S + 1) * alpha;
        var b = (S + 1) * (1.0 - alpha);
        _beta = new Beta(a, b);
        _prob = ArrayUtil.Generate(S + 1, (i) => i == 0 ? 0.0 : cumulativeProb[i - 1]);
      }

      public override double Weight(int k)
      {
        var weightK = _beta.cdf(_prob[k]) - _beta.cdf(_prob[k-1]);
        return weightK;
      }
    }

    private class UECVEstimator : LEstimator
    {
      /// <summary>
      /// Identify k where <m>(k-1)/S &lt;= alpha &lt; k/S</m> 
      /// </summary>
      private int _k;

      internal int K { get { return _k;  } }

      /// <summary>
      /// An L-Estimator function of Upper Empirical Cumulative Distribution Value 
      /// </summary>
      /// <param name="alpha">the quantile of interest</param>
      /// <param name="cumulativeProb">the cumulative prob of each sample</param>
      public UECVEstimator(double alpha, double[] cumulativeProb)
      {
        const double tolerance = 1.0E-15;
        int S = cumulativeProb.Length;
        if (alpha > cumulativeProb[S - 1] - tolerance)
        {
          _k = S;
          return;
        }
        else if (alpha <= cumulativeProb[0])
        {
          _k = 1;
          return;
        } 
        int idx = Array.BinarySearch(cumulativeProb, alpha);
        if (idx >= 0)
        {
          _k = idx + 2;
        }
        else
        {
          idx = ~idx;
          _k = idx + 1; 
        }
      }

      public UECVEstimator(double alpha, int S)
      {
        _k = (int)Math.Floor(S * alpha) + 1;
      }

      public override double Weight(int k)
      {
        var weightK = k == _k ? 1.0 : 0.0; 
        return weightK;
      }
    }

    private class ECVEstimator : LEstimator
    {
      /// <summary>
      /// Identify k where <m> (k-1)/S &lt; alpha &lt;= k/S </m>
      /// </summary>
      private int _k;

      /// <summary>
      /// An L-Estimator function of Empirical Cumulative Distribution Value 
      /// </summary>
      /// <param name="alpha">the quantile of interest</param>
      /// <param name="cumulativeProb">the cumulative prob of each sample</param>
      public ECVEstimator(double alpha, double[] cumulativeProb)
      {
        const double tolerance = 1.0E-15;
        int S = cumulativeProb.Length;
        if (alpha > cumulativeProb[S - 1] - tolerance)
        {
          _k = S;
          return;
        }
        else if (alpha <= cumulativeProb[0])
        {
          _k = 1;
          return;
        } 
        
        int idx = Array.BinarySearch(cumulativeProb, alpha);
        if (idx < 0)
        {
          idx = ~idx;
        }
        _k = idx+1; 

      }

      public ECVEstimator(double alpha, int S)
      {
        _k = (int)Math.Ceiling((S * alpha) + 1) - 1;
      }

      public override double Weight(int k)
      {
        var weightK = k == _k ? 1.0 : 0.0;
        return weightK;
      }
    }

    private class PiecewiseLinearEstimator : LEstimator
    {
      /// <summary>
      /// Identify k where <m> (k-1)/S &lt; alpha &lt;= k/S </m>
      /// </summary>
      private int _k;

      /// <summary>
      /// ratio of <m>(alpha - p[k-1])/(p[k] - p[k-1])</m>
      /// </summary>
      private double _p; 

      /// <summary>
      /// An L-Estimator function of Empirical Cumulative Distribution Value, interpolated linearly 
      /// </summary>
      /// <param name="alpha">the quantile of interest</param>
      /// <param name="cumulativeProb">the cumulative prob of each sample</param>
      public PiecewiseLinearEstimator(double alpha, double[] cumulativeProb)
      {
        const double tolerance = 1.0E-15;
        int S = cumulativeProb.Length;
        _p = 1.0;
        if (alpha > cumulativeProb[S - 1] - tolerance)
        {
          _k = S;
          return;
        }
        else if (alpha <= cumulativeProb[0])
        {
          _k = 1;
          return;
        }

        int idx = Array.BinarySearch(cumulativeProb, alpha);
        if (idx < 0)
        {
          idx = ~idx;
        }
        _k = idx + 1; 
        double dp = cumulativeProb[idx] - cumulativeProb[idx - 1];
        if (dp < tolerance)
        {
          return;
        }

        _p = (alpha - cumulativeProb[idx - 1]) / dp;
      }

      
      public override double Weight(int k)
      {
        if (k == _k)
          return _p;
        if (k == _k - 1)
          return 1.0 - _p; 
        return 0.0;
      }
    }

    #endregion
  }

  #endregion
}