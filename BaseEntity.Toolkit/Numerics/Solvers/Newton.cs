/*
 * Newton.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Numerics
{
    /// <summary>
    ///   Solver based Newton method
    /// </summary>
    /// <remarks>
    ///   This class implements the Solver interface and
    ///   uses a damped Newton's method (see Numerical Recipes for details)
    ///   to solve for a given function.  This
    ///   method is among the fastest general purpose methods if
    ///   a good starting point is chosen.  If started from
    ///   a bad point, however, the method can be unstable.  
    ///   <para>This class requires the specified Function to be
    ///   differentiable, to properly implement
    ///   the Function.derivative() method, and that the the
    ///   Function.isDerivativeImplemented() method to return
    ///   <c>true</c>.</para>
    /// </remarks>
  [Serializable]
  public class Newton : Solver
  {

    /// <summary>
    ///   Constructor
    /// </summary>
    public Newton()
    {
    }

    /// <summary>
    ///   Required definition of the abstract function.
    ///   Implements the bisection solver algorithm.
    ///   See Solver::doSolve() for more details.
    /// </summary>
    public override void doSolve(SolverFn fn, double yTarget, ref double x, ref double fx, ref int numIter, ref int numEvals) 
    {
      if ( !fn.isDerivativeImplemented())
        throw new ArgumentException("function does not have derivative implemented" );

      // adapted from numerical recipes rtsafe function
      double xlow = getLowerBracket();
      double xhigh = getUpperBracket();
      double tolX = getToleranceX();
      double tolF = getToleranceF();
      double maxIterations = getMaxIterations();
      double maxEvaluations = getMaxEvaluations();
      double df,dx,dxold,fh,fl;
      double xh,xl;

      numEvals = 0;
      fl = fn.evaluate(xlow) - yTarget;
      numEvals++;
      fh = fn.evaluate(xhigh) - yTarget;
      numEvals++;

      // Orient search
      if( (fl*fh) >= 0.0 )
      {
        throw new ToolkitException("SolverExceptionRange");
      }
      if( fl < 0.0 )
      {
        xl = xlow;
        xh = xhigh;
      }
      else
      {
        xh = xlow;
        xl = xhigh;
      }
      x = 0.5*(xlow+xhigh);
      dxold = Math.Abs(xhigh-xlow);
      dx = dxold;

      fx = fn.evaluate(x) - yTarget;
      numEvals++;
      df = fn.derivative(x);
      for( numIter = 1; (numIter <= maxIterations) && (numEvals <= maxEvaluations); numIter++ )
      {
        if ( (((x-xh)*df-fx) * ((x-xl)*df-fx) > 0.0)
             || (Math.Abs(2.0*fx) > Math.Abs(dxold*df)) )
        {
          dxold = dx;
          dx = 0.5*(xh-xl);
          x = xl+dx;
        }
        else
        {
          dxold = dx;
          dx = fx/df;
          x -= dx;
        }
        if( Math.Abs(dx) < tolX || Math.Abs(fx) < tolF )
          return;
        fx = fn.evaluate(x) - yTarget;
        numEvals++;
        df = fn.derivative(x);
        if( fx < 0.0 )
          xl = x;
        else
          xh = x;
      }

      // Gone too far, throw exception
      if( numIter > maxIterations )
        throw new ToolkitException("SolverExceptionIterations");
      else
        throw new ToolkitException("SolverExceptionEvaluations");
    }


  }
}
