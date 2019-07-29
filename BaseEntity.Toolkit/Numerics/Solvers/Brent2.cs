//
// Brent2.cs
//  -2008. All rights reserved.
//

using System;

namespace BaseEntity.Toolkit.Numerics
{

  /// <summary>
  /// Implementation of improved Brent solver
  /// </summary>
  /// <remarks>
  /// <para>This class implements the Solver interface and uses the method of Brent (see Numerical Recipes for
  /// details) to solve for some target y for a specified function. This method requires the specified function to
  /// be continuous but not necessarily differentiable. It is considerably faster than the bisection method
  /// and is suitible for general-purpose use.</para>
  /// <para><see cref="Brent2"/> conforms better to Brent's original treatment of tolerance test, i.e., the iteration
  /// stops when the width of the bracket interval is less than <m>2 eps |b| + 0.5 tolX</m> where
  /// <m>eps</m> is machine epsilon, while <see cref="Brent"/> class
  /// <m>2 tolX |b| + 0.5 tolF</m> as the test, causing the iteration stops prematurely
  /// when F-value and X-value have very different magnitudes.</para>
  /// <para>The tolerance levels is treated as relative accuracy in the sense that the iteration stops either when</para>
  /// <math>
  ///   \frac{|f(x) - target|}{1 + |target|} \leq tolF
  /// </math>
  /// <para>or</para>
  /// <math>
  ///   0.5 \frac{|\delta x|}{1 + |a - b|} \leq 2 eps |x| + 0.5 tolX
  /// </math>
  /// </remarks>
  /// <exclude/>
  [Serializable]
  public class Brent2 : Solver
  {

    /// <summary>
    ///   Constructor
    /// </summary>
    public Brent2()
    {
    }

    /// <summary>
    ///   Required definition of the abstract function.
    ///   Implements the bisection solver algorithm.
    ///   See Solver::doSolve() for more details.
    /// </summary>
    public override void doSolve(SolverFn fn, double yTarget, ref double b, ref double fb, ref int numIter, ref int numEvals)
    {
      const double eps = Double.Epsilon;

      // adapted from numerical recipes.
      // note that this method reverts to bisection if
      // it stops making progress, so it is fairly robust.
      double xlow = getLowerBracket();
      double xhigh = getUpperBracket();

      // We treat tolerances as the relative levels
      double tolX = getToleranceX() * (1 + Math.Abs(xhigh-xlow));
      double tolF = getToleranceF() * (1 + Math.Abs(yTarget));

      double maxIterations = getMaxIterations();
      double maxEvaluations = getMaxEvaluations();
      double a = xlow, c, d, e, min1, min2;
      double fa;
      double fc, p, q, r, s, tol1, xm;

      // Start at high point
      b = xhigh;

      numEvals = 0;
      if (getBracketFnReady())
      {
        // Since some evaluation may be very cost,
        // here we save two evaluations whenever it is possible.
        fa = getLowerBracketF();
        fb = getUpperBracketF();
      }
      else
      {
        fa = fn.evaluate(a) - yTarget;
        numEvals++;
        fb = fn.evaluate(b) - yTarget;
        numEvals++;
      }
      fc = fb;

      c = a;
      fc = fa;
      e = d = b - a;

      for (numIter = 1; (numIter <= maxIterations) && (numEvals < maxEvaluations); numIter++)
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
        tol1 = 2.0 * eps * Math.Abs(b) + 0.5 * tolX;
        xm = 0.5 * (c - b);
        // Why was this the test?  Asking for exact solutions is silly. MEF 2004-10-08
        // if( fabs(xm) <= tol1 || fb == 0.0 )
        if (Math.Abs(xm) <= tol1 || Math.Abs(fb) <= tolF)
        {
          if (a < b)
          {
            setLowerBracket(a);
            setLowerBracketF(fa);
            setUpperBracket(b);
            setUpperBracketF(fb);
          }
          else
          {
            setLowerBracket(b);
            setLowerBracketF(fb);
            setUpperBracket(a);
            setUpperBracketF(fa);
          }
          return;
        }
        if (Math.Abs(e) >= tol1 && Math.Abs(fa) > Math.Abs(fb))
        {
          s = fb / fa;
          if (a == c)
          {
            p = 2.0 * xm * s;
            q = 1.0 - s;
          }
          else
          {
            q = fa / fc;
            r = fb / fc;
            p = s * (2.0 * xm * q * (q - r) - (b - a) * (r - 1.0));
            q = (q - 1.0) * (r - 1.0) * (s - 1.0);
          }
          if (p > 0.0)
            q = -q;
          p = Math.Abs(p);
          min1 = 3.0 * xm * q - Math.Abs(tol1 * q);
          min2 = Math.Abs(e * q);
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
        fb = fn.evaluate(b) - yTarget;
        numEvals++;
      }

      // remember the bracket
      if (a < b)
      {
        setLowerBracket(a);
        setLowerBracketF(fa);
        setUpperBracket(b);
        setUpperBracketF(fb);
      }
      else
      {
        setLowerBracket(b);
        setLowerBracketF(fb);
        setUpperBracket(a);
        setUpperBracketF(fa);
      }

      // Gone too far, throw exception
      if (numIter > maxIterations)
        throw new SolverException("SolverExceptionIterations");
      else
        throw new SolverException("SolverExceptionEvaluations");
    }


  }
}
