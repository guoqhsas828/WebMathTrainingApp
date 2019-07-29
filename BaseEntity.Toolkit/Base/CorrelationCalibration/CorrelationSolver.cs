/*
 * CorrelationSolver.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;

using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Sensitivity;

namespace BaseEntity.Toolkit.Calibrators.BaseCorrelation
{
  /// <summary>
  ///   Univariate function
  /// </summary>
  /// <param name="x">Variable</param>
  /// <returns>Function value</returns>
  /// <exclude />
  public delegate double UnivariateFunction(double x);

  /// <summary>
  ///   Correlation Solver
  ///   <preliminary/>
  /// </summary>
  /// <exclude />
  public class CorrelationSolver
  {
    #region CDOPriceEvaluator
    private class CDOPriceEvaluator
    {
      public CDOPriceEvaluator(
        SyntheticCDOPricer pricer, Double_Pricer_Fn func)
      {
        pricer_ = pricer;
        fn_ = func;
      }

      public double evaluate(double x)
      {
        BasketPricer basket = pricer_.Basket;
        int savedPoints = 0;
        double pv = 0.0;
        try
        {
          if (x > 0.945 && basket.IntegrationPointsFirst < 40)
          {
            savedPoints = basket.IntegrationPointsFirst;
            basket.IntegrationPointsFirst = 40;
            basket.Reset();
          }
          basket.SetFactor(x);
          pv = fn_(pricer_);
        }
        finally
        {
          if (savedPoints > 0)
          {
            basket.IntegrationPointsFirst = savedPoints;
            basket.Reset();
          }
        }

        return pv;
      }

      private SyntheticCDOPricer pricer_;
      private Double_Pricer_Fn fn_;
    }
    #endregion // CDOPriceEvaluator

    #region Methods

    /// <summary>
    ///   Solve for correlation to match a price measure
    /// </summary>
    /// <param name="measure">Price measure</param>
    /// <param name="target">Target value</param>
    /// <param name="pricer">Pricer</param>
    /// <param name="toleranceF">Tolerance of value</param>
    /// <param name="toleranceX">Tolerance of correlation</param>
    /// <param name="min">Minimum value of correlation</param>
    /// <param name="max">Maximum value of correlation</param>
    /// <returns>Correlation</returns>
    public static double Solve(
      string measure, double target,
      SyntheticCDOPricer pricer,
      double toleranceF, double toleranceX,
      double min, double max)
    {
      CorrelationSolver solver = new CorrelationSolver();
      CDOPriceEvaluator eval = new CDOPriceEvaluator(
        pricer, DoublePricerFnBuilder.CreateDelegate(
          pricer.GetType(), measure));
      UnivariateFunction fn = eval.evaluate;
      if (pricer.CDO.Attachment <=1E-15)
        solver.BracketBaseTranche(fn, target, toleranceF, min, max);
      else
        solver.BracketGeneralTranche(fn, target, 0, 0.99, toleranceX, toleranceF);
      double factor = solver.BrentSolve(
        fn, target, toleranceF, toleranceX);
      return factor * factor;
    }

    /// <summary>
    ///   Solve for correlation to match a price measure
    /// </summary>
    /// <param name="evalFn">Evaluate function</param>
    /// <param name="target">Target value</param>
    /// <param name="pricer">Pricer</param>
    /// <param name="toleranceF">Tolerance of value</param>
    /// <param name="toleranceX">Tolerance of correlation</param>
    /// <param name="min">Minimum value of correlation</param>
    /// <param name="max">Maximum value of correlation</param>
    /// <returns>Correlation</returns>
    internal static double Solve(
      Double_Pricer_Fn evalFn, double target,
      SyntheticCDOPricer pricer,
      double toleranceF, double toleranceX,
      double min, double max)
    {
      CorrelationSolver solver = new CorrelationSolver();
      CDOPriceEvaluator eval = new CDOPriceEvaluator(pricer, evalFn);
      UnivariateFunction fn = eval.evaluate;
      if (pricer.CDO.Attachment <= 1E-15)
        solver.BracketBaseTranche(fn, target, toleranceF, min, max);
      else
        solver.BracketGeneralTranche(fn, target, 0, 0.99,
          Math.Max(0.005, toleranceX), toleranceF);
      double factor = solver.BrentSolve(
        fn, target, toleranceF, toleranceX);
      return factor * factor;
    }

    /// <summary>
    ///  Bracket the target using the monotonic property 
    /// </summary>
    /// <param name="fn">Function</param>
    /// <param name="target">Target value</param>
    /// <param name="toleranceF">Tolerance of function value difference</param>
    /// <param name="max">max</param>
    /// <param name="min">min</param>
    public void BracketBaseTranche(
      UnivariateFunction fn,
      double target,
      double toleranceF,
      double min, double max)
    {
      xl = min > 0.40 ? min : 0.40;
      fl = fn(xl);
      if (target <= fl)
      {
        if (xl == min)
          throw new SolverException("Cannot bracket the break even correlation");

        xh = xl; fh = fl;
        xl = min;
        fl = fn(xl);
        if (target <= fl)
        {
          if (fl - target < toleranceF)
          {
            xh = xl;
            fh = fl;
            return;
          }
          throw new SolverException("Cannot bracket the break even correlation");
        }
      }
      else // target > fl 
      {
        xh = 0.80 > max ? max : 0.80;
        fh = fn(xh);
        if (target >= fh)
        {
          if (xh == max)
            throw new SolverException("Cannot bracket the break even correlation");

          xl = xh; fl = fh;
          xh = max;
          fh = fn(xh);
          if (target >= fh)
          {
            if (target - fh < toleranceF)
            {
              xl = xh;
              fl = fh;
              return;
            }
            throw new SolverException("Cannot bracket the break even correlation");
          }
        }
      }

      return;
    }

    /// <summary>
    ///   Refine brackets using cached values
    /// </summary>
    /// <param name="fn">Function</param>
    /// <param name="target">Target value</param>
    /// <param name="xlist">List of cached x values for which we already have function values</param>
    /// <param name="toleranceF">Tolerence of function values</param>
    public void RefineBaseTrancheBracket(
      UnivariateFunction fn,
      double target,
      IList<double> xlist,
      double toleranceF)
    {
      int l = 0, h = xlist.Count - 1;
      while (h - l > 1)
      {
        int m = (l + h) / 2;
        double xm = xlist[m];
        if (xm <= xl)
          l = m;
        else if (xm >= xh)
          h = m;
        else
        {
          double fm = fn(xm);
          if (fm > target + toleranceF)
          {
            h = m;
            xh = xm;
            fh = fm;
          }
          else if (fm < target - toleranceF)
          {
            l = m;
            xl = xm;
            fl = fm;
          }
          else
          {
            xl = xh = xm;
            fl = fh = fm;
            return;
          }
        }
      } // end while (h - l > 1)
      return;
    }

    /// <summary>
    ///   Solve correlation by Brent algorithm
    /// </summary>
    /// <param name="fn">Function</param>
    /// <param name="target">Target value</param>
    /// <param name="toleranceF">Function tolerance</param>
    /// <param name="toleranceX">Variable tolerance</param>
    /// <returns></returns>
    public double BrentSolve(
      UnivariateFunction fn,
      double target,
      double toleranceF,
      double toleranceX)
    {
      const double eps = 2.22045e-016;
      const int maxIterations = 1000;

      if (xl == xh)
        return xl;

      double a  = xl;
      double fa = fl - target;
      double b  = xh;
      double fb = fh - target;
      double fc = fb;
      double c = b;
      double d, e;
      e = d = b - a;
      for (int numIter = 1; numIter <= maxIterations; numIter++)
      {
        if ((fb * fc) >= 0.0)
        {
          c = a;
          fc = fa;
          e = d = b - a;
        }
        if (Math.Abs(fc) <= Math.Abs(fb))
        {
          a = b;
          b = c;
          c = a;
          fa = fb;
          fb = fc;
          fc = fa;
        }
        double tol1 = 2.0 * eps * Math.Abs(b) + 0.5 * toleranceX;
        double xm = 0.5 * (c - b);
        if (Math.Abs(xm) <= tol1 || Math.Abs(fb) <= toleranceF)
          return b;
        if (Math.Abs(e) >= tol1 && Math.Abs(fa) > Math.Abs(fb))
        {
          double p, q;
          double s = fb / fa;
          if (a == c)
          {
            p = 2.0 * xm * s;
            q = 1.0 - s;
          }
          else
          {
            double r = fb / fc;
            q = fa / fc;
            p = s * (2.0 * xm * q * (q - r) - (b - a) * (r - 1.0));
            q = (q - 1.0) * (r - 1.0) * (s - 1.0);
          }
          if (p > 0.0)
            q = -q;
          p = Math.Abs(p);
          double min1 = 3.0 * xm * q - Math.Abs(tol1 * q);
          double min2 = Math.Abs(e * q);
          if (2.0 * p < (min1 < min2 ? min1 : min2))
          {
            e = d;
            d = p / q;
          }
          else
          {
            d = xm;
            e = d;
          }
        }
        else
        {
          d = xm;
          e = d;
        }
        a = b;
        fa = fb;
        if (Math.Abs(d) > tol1)
          b += d;
        else
          b += (xm > 0.0 ? Math.Abs(tol1) : -Math.Abs(tol1));
        fb = fn(b) - target;
      }

      return b;
    }


    /// <summary>
    ///   Bracket the target without monotonicity
    /// </summary>
    /// <param name="fn">Function</param>
    /// <param name="target">Target</param>
    /// <param name="xMin">Lower bound of variable</param>
    /// <param name="xMax">Upper bound of variable</param>
    /// <param name="toleranceA">Variable tolerance</param>
    /// <param name="toleranceF">Function tolerance</param>
    public void BracketGeneralTranche(
      UnivariateFunction fn,
      double target,
      double xMin, double xMax,
      double toleranceA,
      double toleranceF)
    {
      double xa, fa, xb, fb;

      // Check the lower point
      //   (xl, fl) and (xa, fa) have lower points
      xa = xl = xMin;
      fa = fl = fn(xl);
      double dl = fl - target;
      if (dl >= -toleranceF && dl <= toleranceF)
      {
        xh = xa;
        fh = fa;
        return;
      }

      // Check the middle point
      //   (xh, fh) has middle point
      xh = xMin + 0.7 * (xMax - xMin);
      fh = fn(xh);
      double dh = fh - target;
      if (dh >= -toleranceF && dh <= toleranceF)
      {
        xl = xh;
        fl = fh;
        return;
      }

      if (dl * dh < 0)
        return;

      // Check the upper point.
      //   (xl, fl) has middle point
      //   (xh, fh) and (xb, fb) have upper points
      xl = xh; fl = fh; dl = dh;
      xb = xh = xMax;
      fb = fh = fn(xh);
      dh = fh - target;
      if (dh >= -toleranceF && dh <= toleranceF)
      {
        xl = xh; fl = fh;
        return;
      }

      if (dl * dh < 0)
        return;

      // Try find a bracket between lower and middle points
      //   (xh, fh) and (xa, fa) have middle points
      //   (xl, fl) has lower point
      xh = xl; fh = fl;
      xl = xa; fl = fa;
      xa = xh; fa = fh;
      if (DoBracketTranche(fn, target, toleranceA, toleranceF))
        return;

      // Try find a bracket between middle and upper points
      //   (xl, fl) and (xa, fa) have middle points
      //   (xh, fh) has (xb, fb) have upper points
      xl = xa;
      fl = fa;
      xh = xb;
      fh = fb;
      if (!DoBracketTranche(fn, target, toleranceA, toleranceF))
        throw new SolverException("Fail to bracket a solution");

      return;
    }

    private bool DoBracketTranche(
      UnivariateFunction fn,
      double target,
      double toleranceA,
      double toleranceF)
    {
      double dl = fl - target;
      double dh = fh - target;

      if (xh - xl < toleranceA)
        return false;

      // (x, f) have the middle point
      double x = (xl + xh) / 2;
      double f = fn(x);
      double d = f - target;
      if (d >= -toleranceF && d <= toleranceF)
      {
        xl = xh = x;
        fl = fh = f;
        return true;
      }

      // save the upper point
      double xb = xh;

      // try bracket the left region
      xh = x;
      fh = f;
      if (dl * d < 0)
        return true;
      if(DoBracketTranche(fn, target, toleranceA, toleranceF))
        return true;

      // try bracket the right region
      xl = x;
      fl = f;
      xh = xb;
      fh = dh + target;
      return DoBracketTranche(fn, target, toleranceA, toleranceF);
    }

    #endregion Methods

    #region Data

    private double xl, fl, xh, fh;

    #endregion Data
  } // class CorrelationSolver
}
