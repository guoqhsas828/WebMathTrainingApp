/*
 * Bisection.cs
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
  ///   This class implements the Solver interface and
  ///   finds zeros of a specified function using the bisection method.
  ///   This method requires the targeted function to be
  ///   continuous but not necessarily differentiable.  It
  ///   is among the slowest, yet most robust, practical methods available.
  /// </summary>
  [Serializable]
  public class Bisection : Solver
  {

    /// <summary>
    ///   Constructor
    /// </summary>
    public Bisection()
    {
    }

    /// <summary>
    ///   Required definition of the abstract function.
    ///   Implements the bisection solver algorithm.
    ///   See Solver::doSolve() for more details.
    /// </summary>
    public override void doSolve(SolverFn fn, double yTarget, ref double x, ref double fx, ref int numIter, ref int numEvals) 
    {
      double xlow = getLowerBracket();
      double xhigh = getUpperBracket();
      double tolX = getToleranceX();
      double tolF = getToleranceF();
      double maxIterations = getMaxIterations();
      double maxEvaluations = getMaxEvaluations();
      double dx, xCur;

      // Start at minX, ignore x0.
      numEvals = 0;
      x = xlow;
      fx = fn.evaluate(x) - yTarget;
      numEvals++;

      // Orient the search so that f>0 lies at xCur+dx
      if( fx < 0.0 )
      {
        dx = xhigh - xlow;
        xCur = xlow;
      } else {
        dx = xlow - xhigh;
        xCur = xhigh;
      }

      for( numIter = 1; (numIter <= maxIterations) && (numEvals <= maxEvaluations); numIter++ )
      {
        dx /= 2.0;
        x = xCur + dx;
        fx = fn.evaluate(x) - yTarget;
        numEvals++;

        if( fx <= 0.0 )
          xCur = x; 

        if( (Math.Abs(dx) < tolX) || (Math.Abs(fx) < tolF) )
        {
          return;
        }
      }

      // Gone too far, throw exception
      if( numIter > maxIterations )
        throw new ToolkitException("SolverExceptionIterations");
      else
        throw new ToolkitException("SolverExceptionEvaluations");
    }


  }
}
