/*
 * DelegateSolverFn.cs
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
  ///   An adapter class to provide a function pointer interface to
  ///   the Solver class.
  /// </summary>
  ///   <example>
  ///   <code>
  ///   // Declare functions to implement function to solve for.
  ///   // In this case its f(x) = exp(x);
  ///   double evaluate( double x ) { return exp(x); }
  ///   double derivative( double x ) { return exp(x); }
  ///   // Define solver we want
  ///   Solvers::Generic solver;
  ///   // Define instance of interface to function
  ///   DelegateSolverFn fn(evaluate, derivative);
  ///   // Find x where f(x) = 2.0;
  ///   double x = solver.solve(fn, 2.0);
  ///   </code>
  /// </example>
  [Serializable]
  public class DelegateSolverFn : SolverFn
  {
    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="evaluate">Function to evaluate objective function f(x)</param>
    /// <param name="derivative">Function to evaluate f'(x)</param>
    public DelegateSolverFn( Double_Double_Fn evaluate, Double_Double_Fn derivative )
    {
      evaluate_ = evaluate;
      derivative_ = derivative;
    }

		/// <summary>
		///  Evaluate delegate function at x.
		/// </summary>
		/// <param name="x">Point for evaluation</param>
		/// <returns>Function value at x.</returns>
    public override double evaluate( double x )
    {
      string exceptDesc;
      double y = evaluate_( x, out exceptDesc );
      if (exceptDesc != null)
      {
        throw new ToolkitException(exceptDesc);
      }
      return y;
    }

		/// <summary>
		///  Evaluate derivative of delegate function at x.
		/// </summary>
		/// <param name="x">Point for evaluation</param>
		/// <returns>Function derivative value at x.</returns>
		public override double derivative(double x)
    {
      string exceptDesc;
      double y = derivative_( x, out exceptDesc );
      if (exceptDesc != null)
      {
        throw new ToolkitException(exceptDesc);
      }
      return y;
    }

		/// <summary>
		/// True if derivative method is implemented
		/// </summary>
		/// <returns>True if derivative is implemented</returns>
    public override bool isDerivativeImplemented()
    {
      return (derivative_ == null ? false : true);
    }

    Double_Double_Fn evaluate_;
    Double_Double_Fn derivative_;
  }

}

