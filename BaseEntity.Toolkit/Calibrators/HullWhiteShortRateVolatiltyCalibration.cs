using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{

  #region HullWhiteParameterCalibration
  /// <summary>
  /// Hull-White Parameters calibrated 
  /// </summary>
  public static class HullWhiteParameterCalibration
  {
    #region Optimizing Functions

    /// <summary>
    /// The function to calibrate hull white parameters from swaptions
    /// </summary>
    /// <param name="asOf">AsOf date</param>
    /// <param name="expiries">Expiry dates</param>
    /// <param name="tenors">Forward tenors</param>
    /// <param name="volatilities">Volatilities</param>
    /// <param name="volType">Volatility type</param>
    /// <param name="weights">Weights</param>
    /// <param name="rateCurve">Rate curve</param>
    /// <param name="muCurve">Upper bound curve for mean reversion</param>
    /// <param name="mlCurve">Lower bound curve for mean reversion</param>
    /// <param name="suCurve">Upper bound curve for sigma reversion</param>
    /// <param name="slCurve">Lower bound curve for sigma reversion</param>
    public static HullWhiteParameter CalibrateHullWhiteFromSwaptions
    (
      Dt asOf,
      Dt[] expiries,
      string[] tenors,
      double[,] volatilities,
      DistributionType volType,
      double[,] weights,
      DiscountCurve rateCurve,
      VolatilityCurve muCurve,
      VolatilityCurve mlCurve,
      VolatilityCurve suCurve,
      VolatilityCurve slCurve
    )
    {
      //if input boundary curves are null, set to default constraints.
      muCurve = muCurve ?? new VolatilityCurve(asOf, 10.0);
      mlCurve = mlCurve ?? new VolatilityCurve(asOf, -1.0);
      suCurve = suCurve ?? new VolatilityCurve(asOf, 10.0);
      slCurve = slCurve ?? new VolatilityCurve(asOf, 0.0);

      var data = new HullWhiteDataContainer(expiries, tenors, 
        volatilities, volType, weights, rateCurve);
      
      double[] muBounds, mlBounds, suBounds, slBounds;
      if (IsEqual(muCurve, mlCurve))
      {
        data.MeanVariables = data.MeanCurveDates
          .Select(d => muCurve.Interpolate(d)).ToArray();
        GetBoundaries(suCurve, slCurve, data.SigmaCurveDates, out suBounds, out slBounds);
        return Calibrate(data, HwOptimizeFlag.OptimizeSigma, data.SigmaVariables,
          suBounds, slBounds);
      }

      if (IsEqual(suCurve, slCurve))
      {
        data.SigmaVariables = data.SigmaCurveDates
          .Select(d => suCurve.Interpolate(d)).ToArray();
        GetBoundaries(muCurve, mlCurve, data.MeanCurveDates, out muBounds, out mlBounds);
        return Calibrate(data, HwOptimizeFlag.OptimizeMean, data.MeanVariables,
          muBounds, mlBounds);
      }

      GetBoundaries(muCurve, mlCurve, data.MeanCurveDates, out muBounds, out mlBounds);
      GetBoundaries(suCurve, slCurve, data.SigmaCurveDates, out suBounds, out slBounds);
      var uBounds = muBounds.Concat(suBounds).ToArray();
      var lBounds = mlBounds.Concat(slBounds).ToArray();
      return Calibrate(data, HwOptimizeFlag.OptimizeAll, data.Variables, uBounds, lBounds);
    }

    private static HullWhiteParameter Calibrate(HullWhiteDataContainer data,
      HwOptimizeFlag flag, double[] startPoint, double[] uBounds, double[] lBounds)
    {
      var fn = new HwOptimizingFn(data, flag);
      var status = fn.Fit(new HullWhiteCalibrationSettings(), startPoint, 
        uBounds, lBounds);
      if (status == CashflowCalibrator.OptimizerStatus.FailedForUnknownException)
      {
        var settings = new HullWhiteCalibrationSettings
        {
          MaxEvaluation = 1000,
          MaxIteration = 1000,
          FToleranceFactor = 1E-2,
          ToleranceGrad = 1E-4,
          ToleranceX = 1E-4
        };

        fn.Fit(settings, GetGuess(data, flag), uBounds, lBounds);
      }
      return new HullWhiteParameter(data.TimeGrids, data.RateCurve,
        data.MeanCurve, data.SigmaCurve);
    }

    #endregion Optimizing Functions

    #region Optimizing

    /// <summary>
    /// Hull-White optimzing functions
    /// </summary>
    public class HwOptimizingFn
    {
      private readonly HullWhiteDataContainer _data;
      private readonly HwOptimizeFlag _flag;

      /// <summary>
      /// Contructor
      /// </summary>
      /// <param name="data">Hull-White data container</param>
      /// <param name="flag">Optimizing flags, including OptimizeAll,
      /// OptimizeMean, OptimizeSigma</param>
      public HwOptimizingFn(HullWhiteDataContainer data,
        HwOptimizeFlag flag)
      {
        _data = data;
        _flag = flag;
      }
      
      public void MeanEvaluator(IReadOnlyList<double> x,
        IList<double> f, IList<double> g)
      {
        _data.MeanVariables = Enumerable.ToArray(x);

        var points = _data.Points;
        var zeroPrices = _data.DiscountFactors;
        var b = _data.B;

        for (int k = 0; k < points.Length - 1; k++)
        {
          var pk = points[k];
          var pk1 = points[k + 1];
          var weightRatio = pk1.Weight / pk.Weight;
          var ivr = Math.Sqrt(ImpliedVariancesRatio(pk.Begin,
            pk1.End, pk.End, zeroPrices, b));
          var volRatio = pk1.Volatility / pk.Volatility;
          f[k] = weightRatio * (ivr / volRatio - 1);
        }
      }

      public void SigmaEvaluator(IReadOnlyList<double> x,
        IList<double> f, IList<double> g)
      {
        if (_flag == HwOptimizeFlag.OptimizeSigma)
          _data.SigmaVariables = Enumerable.ToArray(x);
        else _data.Variables = Enumerable.ToArray(x);

        var points = _data.Points;
        for (int k = 0; k < points.Length; k++)
        {
          var pk = points[k];
          var modelPv = SwaptionModelPv(pk.Begin, pk.End,
            pk.SwapRate, _data);
          f[k] = pk.Weight*(modelPv/pk.MarketPv - 1);
        }
      }

      public CashflowCalibrator.OptimizerStatus Fit(
        HullWhiteCalibrationSettings settings, double[] guess, 
        double[] upperBounds, double[] lowerBounds)
      {
        int xDimension = guess.Length;
        int n = _data.Points.Length;
        int fDimension = _flag == HwOptimizeFlag.OptimizeMean
          ? n - 1 : n;
        var evaluator = GetEvaluator(_flag);
        if (upperBounds.Length == 1)
          upperBounds = upperBounds[0].Repeat(xDimension).ToArray();
        if (lowerBounds.Length == 1)
          lowerBounds = lowerBounds[0].Repeat(xDimension).ToArray();
        var opt = new NLS(xDimension);
        opt.setMaxEvaluations(settings.MaxEvaluation);
        opt.setMaxIterations(settings.MaxIteration);
        opt.setLowerBounds(lowerBounds);
        opt.setUpperBounds(upperBounds);

        opt.setToleranceF(settings.FToleranceFactor
          *Math.Sqrt(0.5*fDimension));
        opt.setToleranceGrad(settings.ToleranceGrad);
        opt.setToleranceX(settings.ToleranceX);

        opt.setInitialPoint(guess);
        var fn = DelegateOptimizerFn.Create(xDimension, fDimension, evaluator, false);
        return OptimizeUtil.RunOptimizer(opt, fn);
      }

      private Action<IReadOnlyList<double>, IList<double>, IList<double>> 
        GetEvaluator(HwOptimizeFlag flag)
      {
        if (flag == HwOptimizeFlag.OptimizeMean)
          return MeanEvaluator;
        return SigmaEvaluator;
      }
    }

    #endregion Optimizing

    #region Calculations

    public static double SwaptionModelPv(int bI, int eI,
      double strike, HullWhiteDataContainer data)
    {
      var dt = data.Intervals;
      var b = data.B;
      var dfs = data.DiscountFactors;
      var spreads = data.Spreads;

      int n = dt.Length;
      var arrayA = new double[n];
      var arrayC = new double[n];
      for (int i = bI + 1; i < eI; i++)
      {
        arrayA[i] = CalcA(bI, i, data);
        arrayC[i] = strike * dt[i] + 1 - spreads[i + 1];
      }
      arrayA[eI] = CalcA(bI, eI, data);
      arrayC[eI] = strike * dt[eI] + 1;

      Func<double, double> fn = r =>
      {
        var f = 0.0;
        for (int i = bI + 1; i <= eI; i++)
        {
          f += arrayC[i]*Math.Exp(arrayA[i] - b[bI + 1, i]*r);
        }
        return f;
      };

      var initialGuess = Math.Log((bI == 0 ? 1.0 : dfs[bI - 1])/dfs[bI])/dt[bI];

      var solver = new Brent2();
      solver.setToleranceF(1e-15);
      solver.setToleranceX(1e-15);
      var rStar = solver.solve(fn, null, spreads[bI + 1], initialGuess);

      var pv = 0.0;
      for (int i = bI + 1; i <= eI; i++)
      {
        var xi = Math.Exp(arrayA[i] - b[bI + 1, i]*rStar);
        pv += arrayC[i]*GetZeroBondPut(bI, i, xi, data);
      }
      return pv;
    }

    public static double CapletModelPrice(int bI, int eI,
      double strike, HullWhiteDataContainer data)
    {
      var dt = data.Times[eI] - data.Times[bI];
      var ratio = 1 + strike*dt;
      return ratio*GetZeroBondPut(bI, eI, 1/ratio, data);
    }

    public static double GetZeroBondPut(int bI, int eI, double x,
      HullWhiteDataContainer data)
    {
      var p = data.DiscountFactors;
      double pb = p[bI], pe = p[eI];
      var v = Math.Sqrt(CalcVp(bI, eI, data));
      if (v.AlmostEquals(0.0))
        return x*pb - pe;
      var dplus = Math.Log(pb*x/pe)/v + 0.5*v;
      var dminus = Math.Log(pb*x/pe)/v - 0.5*v;
      return x*pb*Normal.cumulative(dplus, 0.0, 1.0)
             - pe*Normal.cumulative(dminus, 0.0, 1.0);
    }

    public static double GetZeroBondCall(int bI, int eI, double x,
      HullWhiteDataContainer data)
    {
      var p = data.DiscountFactors;
      double pb = p[bI], pe = p[eI];
      var v = Math.Sqrt(CalcVp(bI, eI, data));
      if (v.AlmostEquals(0.0))
        return pe - x*pb;
      var dPlus = Math.Log(pe/(pb*x))/v + 0.5*v;
      var dMinus = dPlus - v;
      return pe*Normal.cumulative(dPlus, 0.0, 1.0)
             - x*pb*Normal.cumulative(dMinus, 0.0, 1.0);
    }


    public static double CalcVp(int bI, int eI, HullWhiteDataContainer data)
    {
      var b = data.B;
      return data.Vr[bI] * Math.Pow(b[bI + 1, eI], 2);
    }

    public static double CalcA(int bI, int eI, HullWhiteDataContainer data)
    {
      var forward = data.Forwards;
      var p = data.DiscountFactors;
      var b = data.B[bI + 1, eI];
      var p0 = bI < 0 ? 1.0 : p[bI];

      return Math.Log(p[eI]/p0) + b*forward[bI] - 0.5*b*b*data.Vr[bI];
    }

    public static double ImpliedVariancesRatio(int bI,
      int jI, int kI, double[] p, double[,] b)
    {
      var ratio = (p[bI] - p[kI])*b[bI + 1, jI]
                  /((p[bI] - p[jI])*b[bI + 1, kI]);

      return Math.Pow(ratio, 2);
    }

    #endregion Calculations

    #region Helpers

    private static void GetBoundaries(VolatilityCurve ubCurve, VolatilityCurve lbCurve,
      Dt[] dates, out double[] uBounds, out double[] lBounds)
    {
      uBounds = dates.Select(d => ubCurve.Interpolate(d)).ToArray();
      lBounds = dates.Select(d => lbCurve.Interpolate(d)).ToArray();
    }


    private static bool IsEqual(VolatilityCurve c1, VolatilityCurve c2)
    {
      bool equal = c1.DistributionType.Equals(c2.DistributionType);
      equal &= Dt.Cmp(c1.AsOf, c2.AsOf) == 0;
      equal &= c1.Frequency.Equals(c2.Frequency);
      var n = c1.Points.Count;
      var m = c2.Points.Count;
      equal &= n == m;
      if (n > 0 && m > 0 && n == m)
      {
        for (int i = 0; i < n; i++)
        {
          equal &= Dt.Cmp(c1.Points[i].Date, c2.Points[i].Date) == 0;
          equal &= c1.Points[i].Value.AlmostEquals(c2.Points[i].Value);
        }
      }
      return equal;
    }

    public static double[] GetGuess(HullWhiteDataContainer data, 
      HwOptimizeFlag flag)
    {
      switch (flag)
      {
        case HwOptimizeFlag.OptimizeMean:
          return data.MeanVariables;
        case HwOptimizeFlag.OptimizeSigma:
          return data.SigmaVariables;
        default:
          return data.Variables;
      }
    }


    #endregion Helpers
  }
  #endregion

  #region HWOptimzeFlag
  /// <summary>
  /// Optimizing flags
  /// </summary>
  public enum HwOptimizeFlag
  {
    /// <summary>
    /// Optimze both mean reversion and sigma simutaneously.
    /// </summary>
    OptimizeAll,

    /// <summary>
    /// Optimiz mean reversion only
    /// </summary>
    OptimizeMean,

    /// <summary>
    /// Fix mean reversion, optimize sigma only.
    /// </summary>
    OptimizeSigma
  }

  #endregion HWOptimizeFlag

  #region FittingPoints
  /// <summary>
  /// Default mean and sigma curve date points
  /// </summary>
  public static class FitPoints
  {
    /// <summary>
    /// Number of meanreversion curve fitting points
    /// </summary>
    public static int MeanFitPoints = 10;

    /// <summary>
    /// Number of sigma curve fitting points
    /// </summary>
    public static int SigmaFitPoints = 30;
  }

  #endregion FittingPoints

} //namespace
