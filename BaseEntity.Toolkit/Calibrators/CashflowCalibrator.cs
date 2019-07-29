//
// CashflowCalibrator.cs
//  -2008. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using log4net;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Interface implemented by objects containing a CashflowCalibrator 
  /// </summary>
  public interface IHasCashflowCalibrator
  {
    /// <summary>
    /// Returns the cashflow calibrator settings
    /// </summary>
    CashflowCalibrator.CashflowCalibratorSettings CashflowCalibratorSettings { get; }

    /// <summary>
    /// Curve fit settings for cashflow calibrator
    /// </summary>
    CalibratorSettings CurveFitSettings { get; }
  }

  /// <summary>
  /// CashflowCalibrator
  /// </summary>
  [Serializable]
  public class CashflowCalibrator
  {
    private static readonly ILog logger = LogManager.GetLogger(typeof(CashflowCalibrator));

    #region Calibration Settings

    /// <summary>
    /// Calibrator settings
    /// </summary>
    [Serializable]
    public class CashflowCalibratorSettings
    {
      /// <summary>
      /// Default constructor
      /// </summary>
      public CashflowCalibratorSettings()
      {
        CurvatureWeightTolerance = 1e-32;
        MaximumOptimizerFnEvaluations = 3000;
        MaximumOptimizerIterations = 2000;
        MaximumSolverIterations = 20;
        OptimizerTolerance = 1e-8;
        SlopeWeigthTolerance = 1e-32;
        SolverStoppingRule = 1e-10;
        SlopeWeigthTolerance = 1e-32;
        SolverTolerance = 1e-10;
      }

      /// <summary>
      /// Lower bound for curvature constraint
      /// </summary>
      public double CurvatureWeightTolerance { get; set; }

      /// <summary>
      /// Maximum number of function evaluation for optimizer 
      /// </summary>
      public int MaximumOptimizerFnEvaluations { get; set; }

      /// <summary>
      /// Maximum number of iterations for optimizer 
      /// </summary>
      public int MaximumOptimizerIterations { get; set; }

      /// <summary>
      /// Maximum number of iterations for iterative bootstrap 
      /// </summary>
      public int MaximumSolverIterations { get; set; }

      /// <summary>
      /// Tolerance for optimizer
      /// </summary>
      public double OptimizerTolerance { get; set; }

      /// <summary>
      /// Lower bound for for slope constraint
      /// </summary>
      public double SlopeWeigthTolerance { get; set; }

      /// <summary>
      /// Stopping rule for iterative bootstrap
      /// </summary>
      public double SolverStoppingRule { get; set; }

      /// <summary>
      /// Tolerance for solver used in the bootstrap
      /// </summary>
      public double SolverTolerance { get; set; }
    }

    #endregion

    #region CurveFittingMethod

    /// <summary>
    ///   Curve fitting method
    /// </summary>
    public enum CurveFittingMethod
    {
      /// <summary>
      /// Performing repeated bootstrapping until the convergence of curve points
      /// or the maximum number of iterations reached.
      /// </summary>
      Bootstrap = 0,
      /// <summary>
      /// Iterative Bootstrap, now the same as Bootstrap.
      /// </summary>
      [Obsolete("Now the same as Bootstrap.")]
      IterativeBootstrap = 1,
      /// <summary>
      /// Svensson parametric form 
      /// Parametric form where the instantaneous forward rate is given by Svenson model
      /// <formula>
      ///   f(t) = \alpha_0 + \alpha_1 \exp\!\left(-\frac{t}{\beta_1}\right) +
      ///   \alpha_2 \frac{t}{\beta_1} \exp\!\left(-\frac{t}{\beta_1}\right)
      ///   + \alpha_3 \frac{t}{\beta_2} \exp\!\left(-\frac{t}{\beta_2}\right)
      /// </formula>
      /// </summary>
      Svensson = 2,
      /// <summary>
      /// Nelson-Siegel parametric form
      /// <para>Parametric form where the instantaneous forward rate is given by Nelson-Siegel model.</para>
      /// <formula>
      ///   f(t) = \alpha_0 + \alpha_1 \exp\!\left(-\frac{t}{\beta}\right) +
      ///   \alpha_2 \frac{t}{\beta} \exp\!\left(-\frac{t}{\beta}\right)
      /// </formula>
      /// </summary>
      NelsonSiegel = 3,
      /// <summary>
      /// Micex parametric form
      /// <para>Micex parametric form (not implemented yet).</para>
      /// </summary>
      Micex = 7,
      /// <summary>
      /// Smooth forwards
      /// <para>Fitting of spline with penalty on slope and curvature of the forward rate
      /// levels in order to smooth the curve.</para>
      /// </summary>
      SmoothForwards = 4,
      /// <summary>
      /// Smooth futures
      /// <para>Fitting of spline with penalty on slope and curvature of the futures rate levels
      /// (via a convexity adjustment) in order to smooth the curve.</para>
      /// </summary>
      SmoothFutures = 5,
      /// <summary>
      /// Least quares best fit
      /// <para>Fitting the curve points by minimizing the sum of squared pricing errors of the
      /// instruments.</para>
      /// </summary>
      LeastSquaresFit = 6,
    }

    #endregion

    #region Optimizer status

    /// <summary>
    /// Outcome of minimization/solution
    /// </summary>
    public enum OptimizerStatus
    {
      /// <summary>
      /// Solution is found to the desired accuracy
      /// </summary>
      Converged = 0,
      /// <summary>
      /// Reached limit for number of objective function evaluations
      /// </summary>
      MaximumEvaluationsReached = 1,
      /// <summary>
      /// Reached limit for number of iteration
      /// </summary>
      MaximumIterationsReached = 2,
      /// <summary>
      /// For optimizers: failed for non specified reason
      /// </summary>
      FailedForUnknownException = 3,
      /// <summary>
      /// For solvers. The solver did not converge to the desired accuracy
      /// </summary>
      ExactSolutionNotFound = 4
    }

    #endregion

    #region Data Class

    private class Data : IEquatable<Data>
    {
      internal readonly InterestPayment[][] AccruedPayments;
      internal readonly Dt CurveDt;
      internal readonly DiscountCurve DiscountCurve;
      internal readonly bool[] MultiThreaded;
      internal readonly Payment[][] Payments;
      internal readonly Dt Settle;
      internal readonly double Target;
      internal readonly double Weight;

      internal Data(Dt curveDt, Dt settle, DiscountCurve discountCurve,
        Payment[] receiverPayments, Payment[] payerPayments,
        bool receiverMultithreaded, bool payerMultithreaded,
        double target, double weight)
      {
        CurveDt = curveDt;
        Settle = settle;
        Target = target;
        Weight = weight;
        DiscountCurve = discountCurve;
        AccruedPayments = new InterestPayment[2][];
        Payments = new []{receiverPayments, payerPayments};
        MultiThreaded = new[] { receiverMultithreaded, payerMultithreaded };
      }

      internal Data(Dt curveDt, Dt settle, DiscountCurve discountCurve, PaymentSchedule receiverSchedule, PaymentSchedule payerSchedule, bool discountingAccrued, bool receiverMultithreaded, bool payerMultithreaded, double target, double weight)
      {
        CurveDt = curveDt;
        Settle = settle;
        Target = target;
        Weight = weight;
        DiscountCurve = discountCurve;
        AccruedPayments = new InterestPayment[2][];
        Payments = new Payment[2][];
        ToArray(receiverSchedule, settle, out AccruedPayments[0], out Payments[0], discountingAccrued && (discountCurve != null));
        ToArray(payerSchedule, settle, out AccruedPayments[1], out Payments[1], discountingAccrued && (discountCurve != null));
        MultiThreaded = new[] { receiverMultithreaded, payerMultithreaded };
      }

      internal Data(Dt curveDt, Dt settle, DiscountCurve discountCurve, PaymentSchedule paymentSchedule,
                    bool discountingAccrued, bool multiThreaded,
                    double target, double weight)
      {
        CurveDt = curveDt;
        Settle = settle;
        Target = target;
        Weight = weight;
        DiscountCurve = discountCurve;
        AccruedPayments = new InterestPayment[1][];
        Payments = new Payment[1][];
        ToArray(paymentSchedule, settle, out AccruedPayments[0], out Payments[0], discountingAccrued && (discountCurve != null));
        MultiThreaded = new[] { multiThreaded };
      }

      #region IEquatable<Data> Members

      public bool Equals(Data other)
      {
        return CurveDt.Equals(other.CurveDt);
      }
      #endregion
    }

    #endregion

    #region Data
    private readonly List<Data> data_;
    private bool overlap_;

    #endregion

    #region Constructor
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asof">As of date</param>
    public CashflowCalibrator(Dt asof)
    {
      AsOf = asof;
      data_ = new List<Data>();
    }
    #endregion

    #region Properties
    /// <summary>
    /// Asof date
    /// </summary>
    public Dt AsOf { get; private set; }

    /// <summary>
    /// Upper bound for curve points/parameters
    /// </summary>
    public double? Upper { get; set; }

    /// <summary>
    /// Lower bound for curve points parameters
    /// </summary>
    public double? Lower { get; set; }

    /// <summary>
    /// Number of calibration nodes
    /// </summary>
    public int Count { get { return data_.Count; } }

    #endregion

    #region PvFunction

    /// <exclude></exclude>
    internal static double ParallelPv(Dt settle, InterestPayment[] accruedPayments, Payment[] regularPayments,
                                      DiscountCurve discount)
    {
      double pv = 0.0, accr = 0.0;
      bool mtm = (discount == null);
      double dfAtSettle = mtm ? 1.0 : discount.Interpolate(settle);
      if (accruedPayments != null)
      {
        for (int i = 0; i < accruedPayments.Length; ++i)
        {
          var ip = accruedPayments[i];
          double accrual;
          accr += ip.Accrued(settle, out accrual);
          pv += mtm ? accrual : accrual * discount.Interpolate(ip.PayDt);
        }
      }
      if (regularPayments != null)
        pv += Parallel.StableSum(0, regularPayments.Length, i =>
                                                            {
                                                              Payment p = regularPayments[i];
                                                              return mtm
                                                                       ? p.DomesticAmount
                                                                       : p.DomesticAmount *
                                                                         discount.Interpolate(p.PayDt);
                                                            });
      if (mtm)
        return accr + pv;
      return accr + pv / dfAtSettle;
    }


    public static double Pv(Dt settle, InterestPayment[] accruedPayments, Payment[] regularPayments,
                              DiscountCurve discount)
    {
      double pv = 0.0, accr = 0.0;
      bool mtm = (discount == null);
      if (accruedPayments != null)
      {
        for (int i = 0; i < accruedPayments.Length; ++i)
        {
          var ip = accruedPayments[i];
          double accrual;
          accr += ip.Accrued(settle, out accrual);
          pv += mtm ? accrual : accrual * discount.Interpolate(ip.PayDt);
        }
      }
      if (regularPayments != null)
      {
        if (mtm)
          for (int i = 0; i < regularPayments.Length; ++i)
            pv += regularPayments[i].DomesticAmount;
        else
          for (int i = 0; i < regularPayments.Length; ++i)
          {
            var p = regularPayments[i];
            pv += p.DomesticAmount * discount.Interpolate(p.PayDt);
          }
      }
      if (mtm)
        return accr + pv;
      return accr + pv / discount.Interpolate(settle);
    }

    /// <exclude></exclude>
    internal delegate double PvFunction(
      Dt settle, InterestPayment[] accruedPayments, Payment[] regularPayments, DiscountCurve discount);

    #endregion

    #region Bootstrap

    private static class Bootstrap
    {
      #region Solver objective Fn

      private class SolverObjectiveFn : SolverFn
      {
        private readonly Data data_;
        private readonly int idx_;
        private readonly PvFunction[] pvFn_;
        private readonly Curve tgtCurve_;

        public SolverObjectiveFn(Curve tgtCurve, Data data, int idx)
        {
          tgtCurve_ = tgtCurve;
          data_ = data;
          idx_ = idx;
          pvFn_ = Array.ConvertAll<bool, PvFunction>(data.MultiThreaded, b =>
          {
            if (b) return ParallelPv;
            return Pv;
          });
        }

        // The result need long time calculation should never be made a property.
        // Otherwise, it is extremely difficult to inspect object states in debugger
        // because of property evaluation.
        internal double GetError()
        {
          return CalculateError(data_, pvFn_);
        }

        public void SetVal(double x)
        {
          tgtCurve_.SetVal(idx_, x);
        }

        internal Dt CurveDt
        {
          get { return data_.CurveDt; }
        }

        internal Dt SettleDt
        {
          get { return data_.Settle; }
        }

        private static double CalculateError(Data data, PvFunction[] pvFn)
        {
          double tgt = data.Target;
          tgt -= pvFn[0](data.Settle, data.AccruedPayments[0], data.Payments[0], data.DiscountCurve);
          if (pvFn.Length == 2)
            tgt += pvFn[1](data.Settle, data.AccruedPayments[1], data.Payments[1], data.DiscountCurve);
          return -tgt;
        }

        public override double evaluate(double x)
        {
          tgtCurve_.SetVal(idx_, x);
          return GetError();
        }
      }

      #endregion

      private static bool Converged(SolverObjectiveFn[] objectiveFn, double solverTolerance, double[] pricingErrors, out double errorNorm)
      {
        errorNorm = 0.0;
        for (int i = 0; i < objectiveFn.Length; ++i)
        {
          double err = pricingErrors[i] = Math.Abs(objectiveFn[i].GetError());
          if (err > errorNorm)
            errorNorm = err;
        }
        return (errorNorm < solverTolerance);
      }

      private static bool StartStep(Curve tgtCurve, SolverObjectiveFn[] objectiveFn,
                                    Solver solver, CashflowCalibratorSettings settings, bool forceFit, out double[] guess,
                                    out double[] pricingErrors, out double errorNorm)
      {
        using (new CurveReformat(tgtCurve, objectiveFn.Select(f=>f.SettleDt)))
        {
          bool useAsInitialGuess = (tgtCurve.Count >= objectiveFn.Length);
          guess = new double[objectiveFn.Length];
          pricingErrors = new double[objectiveFn.Length];
          if (useAsInitialGuess)
            for (int i = 0; i < guess.Length; ++i)
              guess[i] = tgtCurve.Interpolate(objectiveFn[i].CurveDt);
          tgtCurve.Clear();
          double x = 1.0;
          for (int i = 0; i < objectiveFn.Length; ++i)
          {
            double y = useAsInitialGuess ? guess[i] : x;
            if (y - 1e-3 <= solver.getLowerBounds() || y + 1e-3 >= solver.getUpperBounds())
              y = x;
            tgtCurve.Add(objectiveFn[i].CurveDt, y);
            if (forceFit && i > 0)
            {
              try
              {
                x = guess[i] = solver.solve(objectiveFn[i], 0.0, y - 1e-3, y + 1e-3);
              }
              catch (SolverException)
              {
                var tmpCurve = tgtCurve.clone();
                tmpCurve.Shrink(i);
                tgtCurve.SetVal(i, tmpCurve.Interpolate(objectiveFn[i].CurveDt));
              }
            }
            else
            {
              x = guess[i] = solver.solve(objectiveFn[i], 0.0, y - 1e-3, y + 1e-3);
            }
          }
        }
        tgtCurve.FixTensionFactors();
        return Converged(objectiveFn, settings.SolverTolerance, pricingErrors, out errorNorm);
      }

      private static bool Iterate(SolverObjectiveFn[] objectiveFn, Solver solver, CashflowCalibratorSettings settings, double[] guessNew,
                                  double[] pricingErrors, double error)
      {
        int count = objectiveFn.Length;
        var saved = (double[])guessNew.Clone(); //keep best sup-norm solution
        var errorNorm = error;
        var guessOld = new double[count];
        try
        {
          for (int iter = 0; iter < settings.MaximumSolverIterations; ++iter)
          {
            double maxChange = 0.0;
            for (int i = 0; i < count; ++i)
            {
              guessOld[i] = guessNew[i];
              guessNew[i] = solver.solve(objectiveFn[i], 0.0, guessOld[i] - 1e-3, guessOld[i] + 1e-3);
              double change = Math.Abs(guessNew[i] - guessOld[i]);
              maxChange = maxChange > change ? maxChange : change;
            }
            if (Converged(objectiveFn, settings.SolverTolerance, pricingErrors, out error) || maxChange <= settings.SolverStoppingRule)
              return true;
            if (error <= errorNorm)
            {
              errorNorm = error;
              guessNew.CopyTo(saved, 0);
            }
            else //restore previous iteration
            {
              for (int i = 0; i < count; ++i)
                objectiveFn[i].SetVal(saved[i]);
              return false;
            }
          }
          return false;
        }
        catch (Exception)
        {
          //if any exception is thrown set the best solution in sup-norm
          for (int i = 0; i < objectiveFn.Length; ++i)
            objectiveFn[i].SetVal(saved[i]);
          return false;
        }
      }

      internal static OptimizerStatus Fit(Curve tgtCurve, IEnumerable<Data> data, double? lower,
                                          double? upper, CashflowCalibratorSettings settings, bool startStepOnly,
                                          out double[] guess, out double[] priceErrors)
      {
        Stopwatch timer = null;
        if (logger.IsInfoEnabled)
        {
          timer = new Stopwatch();
          timer.Start();
        }
        var fn = data.Select((d, i) => new SolverObjectiveFn(tgtCurve, d, i)).ToArray();
        var solver = new Brent2();
        solver.setToleranceF(settings.SolverTolerance);
        solver.setToleranceX(settings.SolverTolerance);
        if (lower.HasValue)
          solver.setLowerBounds(lower.Value);
        if (upper.HasValue)
          solver.setUpperBracketF(upper.Value);
        double error;
        bool success = StartStep(tgtCurve, fn, solver, settings, startStepOnly, out guess, out priceErrors, out error);
        if (!(success || startStepOnly))
          success = Iterate(fn, solver, settings, guess, priceErrors, error);
        if (timer != null)
        {
          timer.Stop();
          logger.InfoFormat("Completed Bootstraping {0} in {1} seconds, {2}Converged",
            tgtCurve.Name, timer.ElapsedMilliseconds/1000.0, success ? "" : "not ");
        }
        return success ? OptimizerStatus.Converged : OptimizerStatus.ExactSolutionNotFound;
      }
    }

    #endregion

    #region Global fit

    private static class GlobalFit
    {
      #region SplineFitObjectiveFn

      private class FitSplineFn
      {
        public readonly double[] Error;
        protected readonly Data[] data_;
        protected readonly Curve tgtCurve_;
        private readonly int dateCount_;
        private readonly double[] hs_;
        private readonly CashflowCalibratorSettings settings_;
        private readonly double[] volatility_;
        private readonly double[] wcurv_;
        private readonly double[] wslop_;

        public FitSplineFn(Curve tgtCurve, IList<Data> data, IModelParameter volatility, Curve slopeWts,
                           Curve curvatureWts, CashflowCalibratorSettings settings)
        {
          tgtCurve_ = tgtCurve;
          data_ = data.ToArray();
          settings_ = settings;
          CountObjectiveFn = data.Count;
          // initialize time deltas
          if (CountObjectiveFn > 0)
          {
            Error = new double[CountObjectiveFn];
            dateCount_ = tgtCurve.Count;
            hs_ = new double[dateCount_];
            Dt date0 = tgtCurve.AsOf;
            for (int i = 0; i < dateCount_; ++i)
            {
              Dt date = tgtCurve.GetDt(i);
              hs_[i] = (date - date0) / 365.0;
              date0 = date;
            }
            if (volatility != null)
              volatility_ = tgtCurve.Where(p => p.Date > tgtCurve.AsOf).Select(d => d.Value).ToArray();
            CountConstraints = 0;
            int slopeCount = dateCount_ - 1;
            if (slopeWts != null || curvatureWts != null)
            {
              wslop_ = new double[slopeCount];
              wcurv_ = new double[slopeCount];
            }
            if (slopeWts != null)
            {
              for (int i = 0; i < slopeCount; ++i)
              {
                double w = slopeWts.Interpolate(tgtCurve.GetDt(i));
                if (w > settings_.SlopeWeigthTolerance)
                {
                  ++CountConstraints;
                  wslop_[i] = w;
                }
              }
            }
            if (curvatureWts != null)
            {
              for (int i = 1; i < slopeCount; ++i)
              {
                double w = curvatureWts.Interpolate(tgtCurve.GetDt(i));
                if (w > settings_.CurvatureWeightTolerance)
                {
                  ++CountConstraints;
                  wcurv_[i] = w;
                }
              }
            }
          }
        }

        public int CountObjectiveFn { get; private set; }

        public int CountConstraints { get; private set; }

        protected static double CalculateError(Data data)
        {
          double tgt = data.Target;
          tgt -= Pv(data.Settle, data.AccruedPayments[0], data.Payments[0], data.DiscountCurve);
          if (data.Payments.Length == 2)
            tgt += Pv(data.Settle, data.AccruedPayments[1], data.Payments[1], data.DiscountCurve);
          return -data.Weight * tgt;
        }

        private void SetCurvePoints(IReadOnlyList<double> x)
        {
          // Here x is the simple forward rates
          double df = 1.0;
          for (int i = 0; i < dateCount_; ++i)
          {
            df /= 1 + hs_[i] * x[i] / 100;
            tgtCurve_.SetVal(i, df);
          }
#if DEBUG
          double[] xx = x.ToArray();
#endif
        }

        internal void SetInitialPointsFromPrices(Optimizer opt, double[] prices)
        {
          var x0 = new double[dateCount_];
          double df0 = 1.0;
          for (int i = 0; i < dateCount_; ++i)
          {
            var df = prices[i];
            if (!(hs_[i].ApproximatelyEqualsTo(0.0) && df.ApproximatelyEqualsTo(0.0)))
            {
              x0[i] = 100 * (df0 / df - 1) / hs_[i];
            }
            df0 = df;
          }
          opt.setInitialPoint(x0);
        }

        private void SetConstraints(IReadOnlyList<double> x, IList<double> fn)
        {
          if (dateCount_ < 2)
            return;
          Debug.Assert(wslop_ != null && wslop_.Length >= dateCount_ - 1
                       && wcurv_ != null && wcurv_.Length >= dateCount_ - 1);
          double h = hs_[1]; //Dt.Diff(curveDts_[0], curveDts_[1])/365.0;
          double s0 = (x[1] - x[0]) / h / 100;
          int idx = CountObjectiveFn;
          if (wslop_[0] > settings_.SlopeWeigthTolerance)
            fn[idx++] = wslop_[0] * s0;
          int slopeCount = dateCount_ - 1;
          for (int i = 1; i < slopeCount; ++i)
          {
            h = hs_[i + 1]; // Dt.Diff(curveDts_[i], curveDts_[i + 1]) / 365.0;
            double s = (x[i + 1] - x[i]) / h / 100;
            if (volatility_ != null)
              s += volatility_[i];
            if (wslop_[i] > settings_.SlopeWeigthTolerance)
              fn[idx++] = wslop_[i] * s;
            if (wcurv_[i] > settings_.CurvatureWeightTolerance)
              fn[idx++] = wcurv_[i] * (s - s0);
            s0 = s;
          }
        }

        public virtual void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
        {
          SetCurvePoints(x);
          Parallel.For(0, CountObjectiveFn, i =>
                                              {
                                                double e = Error[i] = CalculateError(data_[i]);
                                                f[i] = e;
                                              });
#if DEBUG
          double[] y = f.ToArray();
#endif
          if ((wslop_ != null && wslop_.Length > 0) || (wcurv_ != null && wcurv_.Length > 0))
            SetConstraints(x, f);
        }
      }

      #endregion

      #region FitParametricFn

      private class FitParametricFn : FitSplineFn
      {
        internal FitParametricFn(Curve tgtCurve, IList<Data> data)
          : base(tgtCurve, data, null, null, null, null)
        {
        }

        public override void Evaluate(IReadOnlyList<double> x, IList<double> f, IList<double> g)
        {
          var fn = tgtCurve_.CustomInterpolator as ParametricCurveFn;
          if (fn == null)
            throw new ArgumentException("Routine can only calibrate ParametricCurveFn parametric forms");
          for (int i = 0; i < x.Count; ++i)
            fn.Parameters[i] = x[i];
          Parallel.For(0, CountObjectiveFn, i =>
                                              {
                                                Error[i] = CalculateError(data_[i]);
                                                f[i] = Error[i];
                                              });
        }
      }

      #endregion

      internal static OptimizerStatus Fit(CurveFittingMethod method, double[] guess, Curve tgtCurve,
                                          IList<Data> data, Curve slopeWts, Curve curvatureWts,
                                          IModelParameter volatility, CashflowCalibratorSettings settings,
                                          out double[] priceErrors)
      {
        int xDimension = guess.Length;
        var opt = new NLS(xDimension);
        opt.setMaxEvaluations(settings.MaximumOptimizerFnEvaluations);
        opt.setMaxIterations(settings.MaximumOptimizerIterations);
        FitSplineFn ofn;
        if (method == CurveFittingMethod.LeastSquaresFit || method == CurveFittingMethod.SmoothForwards
          || method == CurveFittingMethod.SmoothFutures)
        {
          ofn = new FitSplineFn(tgtCurve, data, volatility, slopeWts, curvatureWts, settings);
          opt.setLowerBounds(-100); // cannot lose more than 100%
          opt.setUpperBounds(1000); // ceiling at 1000%
          ofn.SetInitialPointsFromPrices(opt, guess);
        }
        else
        {
          var pfn = tgtCurve.CustomInterpolator as ParametricCurveFn;
          if (pfn == null)
            throw new ArgumentException("CustomInterpolator of type ParametricCurveFn expected");
          tgtCurve.Add(Dt.MaxValue, 0.0);
          //only to prevent out of range exception, Remove when Hehui fixes CustomInterpolator
          ofn = new FitParametricFn(tgtCurve, data);
          if (pfn.LowerBounds != null)
            opt.setLowerBounds(pfn.LowerBounds);
          if (pfn.UpperBounds != null)
            opt.setUpperBounds(pfn.UpperBounds);
          opt.setInitialPoint(guess);
        }
        int fDimension = ofn.CountObjectiveFn + ofn.CountConstraints;
        if (settings.OptimizerTolerance > 0)
        {
          opt.setToleranceF(settings.OptimizerTolerance * Math.Sqrt(0.5 * fDimension));
          opt.setToleranceGrad(settings.OptimizerTolerance);
          opt.setToleranceX(settings.OptimizerTolerance);
        }
        var fn = DelegateOptimizerFn.Create(xDimension, fDimension, ofn.Evaluate, false);
        OptimizerStatus retVal = RunOptimizer(opt, fn);
        priceErrors = new double[ofn.Error.Length];
        for (int i = 0; i < priceErrors.Length; ++i)
          priceErrors[i] = ofn.Error[i];
        return retVal;
      }
    }

    #endregion

    #region Methods
    /// <summary>
    /// Add data to calibration
    /// </summary>
    /// <param name="tgt">Target</param>
    /// <param name="paymentSchedule">Payment schedules for the product</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount for the product cashflows</param>
    /// <param name="curveDt">Curve date</param>
    /// <param name="wt">Weight for the tenor</param>
    /// <param name="discountingAccrued">True if accrued is discounted</param>
    /// <param name="multiThreaded">True to use parallel calculation. Recommend only for compute intensive projection calculations</param>
    public void Add(double tgt, PaymentSchedule paymentSchedule, Dt settle, DiscountCurve discountCurve, Dt curveDt,
                    double wt, bool discountingAccrued, params bool[] multiThreaded)
    {
      if ((wt > 0) && (curveDt > AsOf))
      {
        var data = new Data(curveDt, settle, discountCurve, paymentSchedule, discountingAccrued,
                            (multiThreaded != null) && (multiThreaded.Length > 0) && multiThreaded[0], tgt, wt);
        if (data_.Contains(data))
          overlap_ = true;
        data_.Add(data);
      }
    }

    /// <summary>
    /// Add data to calibration
    /// </summary>
    /// <param name="tgt">Target</param>
    /// <param name="receiverSchedule">Payment schedules for the product</param>
    /// <param name="payerSchedule">Payment schedules for the product</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount for the product cashflows</param>
    /// <param name="curveDt">Curve date</param>
    /// <param name="wt">Weight for the tenor</param>
    /// <param name="discountingAccrued">True if accrued is discounted</param>
    /// <param name="multiThreaded">True to use parallel calculation. Recommend only for compute intensive projection calculations</param>
    public void Add(double tgt, PaymentSchedule receiverSchedule, PaymentSchedule payerSchedule, Dt settle, DiscountCurve discountCurve, Dt curveDt,
                    double wt, bool discountingAccrued, params bool[] multiThreaded)
    {
      MarkCalibrating(receiverSchedule);
      MarkCalibrating(payerSchedule);
      if ((wt > 0) && (curveDt > AsOf))
      {
        var data = new Data(curveDt, settle, discountCurve, receiverSchedule, payerSchedule, discountingAccrued,
                            (multiThreaded != null) && (multiThreaded.Length > 0) && multiThreaded[0],
                            (multiThreaded != null) && (multiThreaded.Length > 1) && multiThreaded[1], tgt, wt);
        if (data_.Contains(data))
          overlap_ = true;
        data_.Add(data);
      }
    }

    internal void Add(double tgt,
      Payment[] receiverSchedule, Payment[] payerSchedule,
      Dt settle, DiscountCurve discountCurve, Dt curveDt,
      double wt, params bool[] multiThreaded)
    {
      MarkCalibrating(receiverSchedule);
      MarkCalibrating(payerSchedule);
      if ((wt > 0) && (curveDt > AsOf))
      {
        var data = new Data(curveDt, settle, discountCurve, receiverSchedule, payerSchedule,
          (multiThreaded != null) && (multiThreaded.Length > 0) && multiThreaded[0],
          (multiThreaded != null) && (multiThreaded.Length > 1) && multiThreaded[1], tgt, wt);
        if (data_.Contains(data))
          overlap_ = true;
        data_.Add(data);
      }
    }

    private static void MarkCalibrating(IEnumerable<Payment> paymentSchedule)
    {
      if (paymentSchedule == null)
        return;
      foreach (var payment in paymentSchedule)
      {
        var fip = payment as FloatingInterestPayment;
        if (fip != null) fip.IsCalibrating = true;
      }
    }
    ///<exclude/>
    private static void ToArray(PaymentSchedule paymentSchedule, Dt settle, out InterestPayment[] accruedPayments,
                                out Payment[] regularPayments, bool discountingAccrued)
    {
      if (paymentSchedule == null)
      {
        accruedPayments = null;
        regularPayments = null;
        return;
      }
      if (discountingAccrued)
      {
        accruedPayments = null;
        regularPayments = paymentSchedule.ToArray<Payment>(null);
        return;
      }
      var regList = new List<Payment>();
      var accrList = new List<InterestPayment>();
      foreach (Payment pay in paymentSchedule)
      {
        var ip = pay as InterestPayment;
        if ((ip != null) && (ip.AccrualStart < settle && ip.AccrualEnd > settle))
          accrList.Add(ip);
        else
          regList.Add(pay);
      }
      accruedPayments = accrList.ToArray();
      regularPayments = regList.ToArray();
    }

    ///<exclude/>
    internal static OptimizerStatus RunOptimizer(Optimizer opt, DelegateOptimizerFn optFn)
    {
      try
      {
        opt.Minimize(optFn);
      }
      catch (Exception)
      {
        if (opt.getNumEvaluations() >= opt.getMaxEvaluations())
          return OptimizerStatus.MaximumEvaluationsReached;
        if (opt.getNumIterations() > opt.getMaxIterations())
          return OptimizerStatus.MaximumIterationsReached;
        return OptimizerStatus.FailedForUnknownException;
      }
      return OptimizerStatus.Converged;
    }

    /// <summary>
    /// Calibrate a calibrated curve
    /// </summary>
    /// <param name="method">CurveFittingMethod</param>
    /// <param name="curveToCalibrate">Target calibrated curve</param>
    /// <param name="slopeWts">Weights to be assigned to the slope constrains : only applies to SmoothForwards and SmoothFutures methods</param>
    /// <param name="curvatureWts">Weights to be assigned to the curvature constraints : only applies to SmoothForwards and SmoothFutures methods</param>
    /// <param name="volatility">Volatility curve for SmoothFutures method</param>
    /// <param name="priceErrors">Pricing errors</param>
    /// <param name="settings">Settings for calibrator parameters</param>
    /// <returns>Optimizer status, i.e. outcome of the calibration</returns>
    public OptimizerStatus Calibrate(CurveFittingMethod method, CalibratedCurve curveToCalibrate, Curve slopeWts,
                                     Curve curvatureWts, IModelParameter volatility, out double[] priceErrors,
                                     CashflowCalibratorSettings settings)
    {
      OptimizerStatus status;
      var tgtCurve = curveToCalibrate.ShiftOverlay ?? curveToCalibrate;
      switch (method)
      {
        case CurveFittingMethod.Bootstrap:
        case CurveFittingMethod.IterativeBootstrap:
          if (overlap_)
            throw new ToolkitException(
              "Cannot bootstrap calibration tenors with overlapping curve dates. Try specifying an OverlapTreatmentOrder or a global CurveFittingMethod");
          {
            double[] guess;
            status = Bootstrap.Fit(tgtCurve, data_, Lower, Upper, settings, false, out guess,
                                   out priceErrors);
          }
          break;
        case CurveFittingMethod.LeastSquaresFit:
        case CurveFittingMethod.SmoothForwards:
        case CurveFittingMethod.SmoothFutures:
          {
            IModelParameter vol = null;
            if (method == CurveFittingMethod.SmoothFutures)
              vol = volatility;
            else if (method == CurveFittingMethod.LeastSquaresFit)
              slopeWts = curvatureWts = null;
            settings.MaximumSolverIterations = 0;
            if (overlap_)
              status = FitWithOverlap(data_, method, slopeWts, curvatureWts, vol, settings, tgtCurve, out priceErrors);
            else
            {
              double[] guess;
              status = Bootstrap.Fit(tgtCurve, data_, Lower, Upper, settings, true, out guess, out priceErrors);
              if (status == OptimizerStatus.Converged && method == CurveFittingMethod.LeastSquaresFit)
                break;
              status = GlobalFit.Fit(method, guess, tgtCurve, data_, slopeWts, curvatureWts, vol,
                                     settings, out priceErrors);
            }
          }
          break;
        case CurveFittingMethod.Svensson:
        case CurveFittingMethod.NelsonSiegel:
        case CurveFittingMethod.Micex:
          var fn = tgtCurve.CustomInterpolator as ParametricCurveFn;
          if (fn == null)
            throw new ToolkitException("CustomInterpolator of type ParametricCurveFn expected");
          status = GlobalFit.Fit(method, fn.Parameters, tgtCurve, data_, null, null, null, settings,
                                 out priceErrors);
          break;
        default:
          throw new ToolkitException("Calibration method not supported");
      }
      return status;
    }

    private OptimizerStatus FitWithOverlap(IEnumerable<Data> data, CurveFittingMethod method, Curve slopeWts, Curve curvatureWts,
                                           IModelParameter vol, CashflowCalibratorSettings settings, Curve tgtCurve,
                                           out double[] priceErrors)
    {
      var distinct = data.Distinct().ToList();
      double[] guess;
      Bootstrap.Fit(tgtCurve, distinct, Lower, Upper, settings, true, out guess, out priceErrors);
      return GlobalFit.Fit(method, guess, tgtCurve, data_, slopeWts, curvatureWts, vol, settings,
                           out priceErrors);
    }

    #endregion

    #region CurveReformat: change curve interp and as-of in the start step for accuracy and efficiency

    private class CurveReformat : IDisposable
    {
      private readonly Curve _curve;
      private readonly Interp _originalInterp;
      private readonly Dt _originalAsOf;

      internal CurveReformat(Curve curve, IEnumerable<Dt> settles)
      {
        if (curve == null) return;
        var interp = curve.Interp.GetRealInterp();
        if (interp is Tension)
        {
          _originalInterp = curve.Interp.clone();
          curve.Interp = new Weighted(new Const(), new Const());
          _curve = curve;
        }
        if (settles == null || !settles.Any() || !IsMultiplicativeCurve(curve))
          return;
        var minSettle = settles.Min();
        var asOf = curve.AsOf;
        if (asOf >= minSettle) return;
        _originalAsOf = asOf;
        _curve = curve;
        ChangeBaseDate(curve, minSettle);
      }

      static bool IsMultiplicativeCurve(Curve curve)
      {
        return curve.Frequency == Frequency.Continuous
          && curve.DayCount == DayCount.Actual365Fixed;
      }

      private static void ChangeBaseDate(Curve curve, Dt toAsOf)
      {
        if (toAsOf.IsEmpty() || curve == null)
          return;
        if (curve.Count == 0)
        {
          curve.AsOf = toAsOf;
          return;
        }
        // We need to adjust curve points to reflect the new values.
        var df0 = curve.Interpolate(toAsOf);
        var points = Enumerable.Range(0, curve.Count)
          .Select(i=>curve.Points[i]).ToArray();
        curve.Shrink(0);
        curve.AsOf = toAsOf;
        for (int i = 0, n = points.Length; i < n; ++i)
          curve.Add(points[i].Date, points[i].Value / df0);
      }

      #region IDisposable Members

      public void Dispose()
      {
        ChangeBaseDate(_curve, _originalAsOf);
        if (_originalInterp != null) _curve.Interp = _originalInterp;
      }

      #endregion
    }
    #endregion
  }
}