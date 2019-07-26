using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Numerics
{
  public unsafe partial class Optimizer
  {
    /// <summary>
    /// Minimizes the specified objective function.
    /// </summary>
    /// <param name="fn">The objective function.</param>
    /// <returns>Solution.</returns>
    public IReadOnlyList<double> Minimize(OptimizerFn fn)
    {
      return NativeUtil.DoubleArray(minimize(fn), getDimension(), this);
    }

    /// <summary>
    /// Perform minimization on the given function
    /// </summary>
    /// <param name="evaluate">The delegate to evaluate the function values</param>
    /// <param name="xdimension">The number of independent variables</param>
    /// <param name="ydimension">The number of functions, which should be one except for <see cref="NLS"/></param>
    /// <param name="derivativeImplemented">If set to <c>true</c>,
    ///  the delegate can also calculate the derivatives;
    ///  otherwise, use the built-in finite difference evaluator for derivatives</param>
    /// <returns>OptimizerStatus</returns>
    public OptimizerStatus Minimize(
      Action<IReadOnlyList<double>, IList<double>, IList<double>> evaluate,
      int xdimension, int ydimension = 1,
      bool derivativeImplemented = false)
    {
      var optFn = DelegateOptimizerFn.Create(
        xdimension, ydimension, evaluate, derivativeImplemented);
      try
      {
        minimize(optFn);
      }
      catch (ApplicationException e)
      {
        if (getNumEvaluations() >= getMaxEvaluations())
          return OptimizerStatus.MaximumEvaluationsReached;
        if (getNumIterations() >= getMaxIterations())
          return OptimizerStatus.MaximumIterationsReached;
        if (e.Message.Contains("Irregular derivatives found"))
          return OptimizerStatus.IrregularDerivativesFound;

        // re-throw any unknown exceptions
        throw;
      }
      return OptimizerStatus.Converged;
    }

    #region Properties

    /// <summary>
    /// Gets the current solution.
    /// </summary>
    /// <value>The current solution.</value>
    public IReadOnlyList<double> CurrentSolution
    {
      get { return NativeUtil.DoubleArray(getCurrentSolution(), getDimension(), this); }
    }

    /// <summary>
    /// Gets the evaluation count
    /// </summary>
    /// <value>The evaluation count</value>
    public int EvaluationCount => getNumEvaluations();

    /// <summary>
    /// Gets the iteration count
    /// </summary>
    /// <value>The iteration count</value>
    public int IterationCount => getNumIterations();

    /// <summary>
    /// Gets the variable count
    /// </summary>
    /// <value>The variable count</value>
    public int VariableCount => getDimension();

    #endregion
  }

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
    ExactSolutionNotFound = 4,
    /// <summary>
    /// For Newton method based optimizers, the functions may not be differentiable.
    /// </summary>
    IrregularDerivativesFound = 5,
  }

  #endregion

}
