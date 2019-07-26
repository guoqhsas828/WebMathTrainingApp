using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Numerics
{
  public partial class DelegateOptimizerFn
  {
    /// <summary>
    /// Creates a delegate function as the object of optimization.
    /// </summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="evaluate">The evaluate function.</param>
    /// <param name="derivative">The gradient function.</param>
    /// <returns>DelegateOptimizerFn</returns>
    public static DelegateOptimizerFn Create(int dimension,
      Func<IReadOnlyList<double>, double> evaluate,
      Action<IReadOnlyList<double>, IList<double>> derivative)
    {
      Double_Vector_Fn eval = CreateDoubleVectorFn(evaluate, dimension);
      Void_Vector_Vector_Fn grad = CreateVoidVectorVectorFn(derivative, dimension);
      var fn = new DelegateOptimizerFn(dimension, eval, grad);
      fn.eval_ = eval;
      fn.grad_ = grad;
      return fn;
    }

    /// <summary>
    /// Creates a delegate function as the object of optimization.
    /// </summary>
    /// <param name="xdimension">The dimension of variables.</param>
    /// <param name="ydimension">The dimension of functions.</param>
    /// <param name="evaluate">The evaluate function.</param>
    /// <param name="derivativeImplemented">Whether the <c>evaluate</c> function implements the derivatives/Jacobians.</param>
    /// <returns>DelegateOptimizerFn</returns>
    public static DelegateOptimizerFn Create(
      int xdimension, int ydimension,
      Action<IReadOnlyList<double>, IList<double>, IList<double>> evaluate,
      bool derivativeImplemented)
    {
      Void_Vector_Vector_Vector_Fn eval = CreateVoidVectorVectorVectorFn(
        xdimension, ydimension, evaluate);
      var fn = new DelegateOptimizerFn(xdimension, ydimension, eval, derivativeImplemented);
      fn._evaluate = evaluate;
      fn.generic_ = eval;
      return fn;
    }

    private static unsafe Void_Vector_Vector_Vector_Fn CreateVoidVectorVectorVectorFn(
      int xdimension, int ydimension,
      Action<IReadOnlyList<double>, IList<double>, IList<double>> evaluate)
    {
      return (double* x, double* f, double* g) =>
      {
        evaluate(NativeUtil.DoubleArray(x, xdimension, null),
          NativeUtil.DoubleArray(f, ydimension, null),
          NativeUtil.DoubleArray(g, xdimension*ydimension, null));
      };
    }


    private static unsafe Void_Vector_Vector_Fn CreateVoidVectorVectorFn(
      Action<IReadOnlyList<double>, IList<double>> derivative, int n)
    {
      return (double* x, double* g) =>
      {
        derivative(NativeUtil.DoubleArray(x, n, null),
          NativeUtil.DoubleArray(g, n, null));
      };
    }

    private static unsafe Double_Vector_Fn CreateDoubleVectorFn(
      Func<IReadOnlyList<double>, double> evaluate, int n)
    {
      return (double* x) => evaluate(NativeUtil.DoubleArray(x, n, null));
    }

    private Action<IReadOnlyList<double>, IList<double>, IList<double>> _evaluate;
    private Double_Vector_Fn eval_;
    private Void_Vector_Vector_Fn grad_;
    private Void_Vector_Vector_Vector_Fn generic_;
  }
}
