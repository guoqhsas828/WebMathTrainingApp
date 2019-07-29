/*
 * SolverFn.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  ///   An adapter class to represent a function and its derivative.
  /// </summary>
  ///
  /// <remarks>
  ///   Target functions derived from this class must implement
  ///   evaluate() and may also implement derivative() and
  ///   isDerivativeImplemented().
  /// </remarks>
  ///
  /// <example>
  ///   <code language="C#">
  ///   // Declare functions to implement function to solve for.
  ///   // In this case its f(x) = exp(x);
  ///   double Evaluate( double x )
  ///   {
  ///     return exp(x);
  ///   }
  ///
  ///   double Derivative( double x )
  ///   {
  ///     return exp(x);
  ///   }
  ///
  ///   // Define a function doing the real job
  ///   double FindSolution()
  ///   {
  ///     // Define solver we want
  ///     Solver solver = new Generic();
  ///
  ///     // Define an instance of interface to function
  ///     Double_Double_Fn evaluate = new Double_Double_Fn(Evaluate);
  ///     Double_Double_Fn evaluate = new Double_Double_Fn(Derivative);
  ///     DelegateSolverFn solveFn = new DelegateSolverFn(evaluate, derivative);
  ///
  ///     // Find x where f(x) = 2.0;
  ///     double x = solver.solve(fn, 2.0);
  ///
  ///     // return result
  ///     return x;
  ///   }
  ///   </code>
  /// </example>
  [Serializable]
  public abstract class SolverFn
  {
    /// <summary>
    ///  Core method providing target function values.
    /// </summary>
    /// <returns>evaluated objective function f(x)</returns>
    public abstract double evaluate(double x);

    /// <summary>
    ///  Indicates if target function has a derivative implementation
    ///  with appropriate API for Solver class.
    /// </summary>
    /// <returns>true if f'(x) is implemented. Default is false.</returns>
    public virtual bool isDerivativeImplemented() 
    {
      return false;
    }

    /// <summary>
    ///   Evaluates f'(x)
    ///   Defaults to calculation of f' using finite
    ///   difference approximation
    /// </summary>
    /// <param name="x">x input</param>
    /// <returns>f'(x)</returns>
    public virtual double derivative(double x) 
    {
      double step = Math.Sqrt(Double.Epsilon);

      double fm = evaluate(x-step);
      double fp = evaluate(x+step);
      return ( (fp-fm)/(2*step) );
    }
  } // SolverFn
}
