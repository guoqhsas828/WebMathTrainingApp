/*
 * Generic.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  ///   This class implements the Solver interface
  ///   and automatically chooses an appropriate solver
  ///   method for the specified function.
  ///   This class best for general-purpose use.
  /// </summary>
  [Serializable]
  public class Generic : Solver
  {

    /// <summary>
    ///   Constructor
    /// </summary>
    public Generic()
    {
    }

    /// <summary>
    ///   Required definition of the abstract function.
    ///   Implements the bisection solver algorithm.
    ///   See Solver::doSolve() for more details.
    /// </summary>
    public override void doSolve(SolverFn fn, double yTarget, ref double x, ref double fx, ref int numIter, ref int numEvals) 
    {
      // Use appropriate solver depending on if derivative implemented
      Solver solver;
      if ( fn.isDerivativeImplemented() == true )
        solver = new Newton();
      else
        solver = new Brent();

      // Set parameters for solver
      solver.setMaxIterations(getMaxIterations());
      solver.setMaxEvaluations(getMaxEvaluations());
      solver.setInitialPoint(getCurrentSolution());
      solver.setLowerBounds(getLowerBounds());
      solver.setUpperBounds(getUpperBounds());
      solver.setLowerBracket(getLowerBracket());
      solver.setUpperBracket(getUpperBracket());
      solver.setToleranceX(getToleranceX());
      solver.setToleranceF(getToleranceF());

      // Call solver
      solver.doSolve(fn, yTarget, ref x, ref fx, ref numIter, ref numEvals);

      return;
    }


  }
}
