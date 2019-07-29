using System;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;
using NativeCurve = BaseEntity.Toolkit.Curves.Native.Curve;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  /// Parametric curve interpolation function
  /// </summary>
  [Serializable]
  public abstract class ParametricCurveFn : BaseEntityObject, ICurveInterpolator
  {
    /// <summary>
    /// Parametric function
    /// </summary>
    public enum Type
    {
      /// <summary>
      /// Svensson parametric form 
      /// </summary>
      Svensson = 2,
      /// <summary>
      /// NelsonSiegel parametric form
      /// </summary>
      NelsonSiegel = 3,
      /// <summary>
      /// Micex parametric form
      /// </summary>
      Micex = 7
    }

    /// <summary>
    /// Factory
    /// </summary>
    /// <param name="type">Parametric function type</param>
    /// <param name="parameters">Parameters</param>
    /// <returns>Parametric function</returns>
    public static ParametricCurveFn Create(Type type, double[] parameters)
    {
      if (type == Type.Svensson)
        return new SvenssonFn(parameters);
      if (type == Type.NelsonSiegel)
        return new NelsonSiegelFn(parameters);
      throw new ArgumentException("Parametric form not supported.");
    }

    /// <summary>
    /// Parametric function for curve
    /// </summary>
    /// <param name="parameters">Parameters</param>
    protected ParametricCurveFn(double[] parameters)
    {
      Parameters = parameters;
    }

    /// <summary>
    ///  Parameters 
    /// </summary>
    public double[] Parameters { get; protected set; }

    /// <summary>
    /// Evaluate the parametric functions
    /// </summary>
    /// <param name="t">Time (in days)</param>
    /// <returns>f(t)</returns>
    protected abstract double Evaluate(double t);

    /// <summary>
    /// Lower bound for parameters in optimization
    /// </summary>
    public virtual double[] LowerBounds
    {
      get { return null; }
    }

    /// <summary>
    /// Upper bound for parameters in optimization
    /// </summary>
    public virtual double[] UpperBounds
    {
      get { return null; }
    }
    
    #region ICurveInterpolator Members
    /// <summary>
    /// Initialize
    /// </summary>
    /// <param name="curve">Curve</param>
    public void Initialize(NativeCurve curve)
    {}

    /// <summary>
    /// Evaluates the function value at point t.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <param name="t">The value of the variable.</param>
    /// <param name="index">The index of the predefined intervals where t locates.</param>
    /// <returns>The curve value at t.</returns>
    public double Evaluate(NativeCurve curve, double t, int index)
    {
      return Evaluate(t);
    }
    
    /// <summary>
    /// Component curves (N/A)
    /// </summary>
    /// <returns></returns>
    public System.Collections.Generic.IEnumerable<Curve> EnumerateComponentCurves()
    {
      yield break;
    }

    #endregion
  }

  /// <summary>
  /// Parametric curve interpolation function
  /// </summary>
  [Serializable]
  public class NelsonSiegelFn : ParametricCurveFn
  {
    /// <summary>
    /// NelsonSiegel function
    /// </summary>
    /// <param name="parameters"></param>
    public NelsonSiegelFn(double[] parameters)
      : base(parameters)
    {
      if (Parameters == null)
      {
        Parameters = new double[] { 0.01, 0.0, 0.5, 0.5 }; //default parameters
        return;
      }
      if (Parameters.Length != 4)
        throw new ToolkitException("Function expects an array of size 4");
    }

    private static double Expd1(double x)
    {
      if (Math.Abs(x) < 1E-5)
        return 1 + x * (1 + x * (1 + x / 4) / 3) / 2;
      return (Math.Exp(x) - 1) / x;
    }

    private static double Expd2(double x)
    {
      if (Math.Abs(x) < 1E-5)
        return 0.5 + x * (1.0 / 3 + x / 8);
      double ex = Math.Exp(x);
      return (ex - (ex - 1) / x) / x;
    }

    /// <exclude></exclude>
    protected override double Evaluate(double t)
    {
      t /= 365.0;
      double a1 = Parameters[0];
      double a2 = Parameters[1];
      double a3 = Parameters[2];
      double a4 = Parameters[3];
      double integral = a1 * t + a2 * t * Expd1(-a4 * t) + a3 * t * t * Expd2(-a4 * t);
      return Math.Exp(-integral);
    }
  }

  /// <summary>
  /// Parametric curve interpolation function
  /// </summary>
  [Serializable]
  public class SvenssonFn : ParametricCurveFn
  {

    /// <summary>
    /// Svennson function
    /// </summary>
    /// <param name="parameters"></param>
    public SvenssonFn(double[] parameters)
      : base(parameters)
    {
      if (Parameters == null)
      {
        Parameters = new[] { 0.01, 0.01, 0.5, 0.5, 0.01, 0.01 }; //default parameters
        return;
      }
      if (Parameters.Length != 6)
        throw new ToolkitException("Function expects an array of size 6");
    }

    private static double Expd1(double x)
    {
      if (Math.Abs(x) < 1E-5)
        return 1 + x * (1 + x * (1 + x / 4) / 3) / 2;
      return (Math.Exp(x) - 1) / x;
    }

    private static double Expd2(double x)
    {
      if (Math.Abs(x) < 1E-5)
        return 0.5 + x * (1.0 / 3 + x / 8);
      double ex = Math.Exp(x);
      return (ex - (ex - 1) / x) / x;
    }

    /// <exclude></exclude>
    protected override double Evaluate(double t)
    {
      t /= 365.0;
      double a1 = Parameters[0];
      double a2 = Parameters[1];
      double a3 = Parameters[2];
      double a4 = Parameters[3];
      double a5 = Parameters[4];
      double a6 = Parameters[5];
      double integral = a1 * t + a2 * t * Expd1(-a4 * t)
        + a3 * t * t * Expd2(-a4 * t) + a5 * t * t * Expd2(-a6 * t);
      return Math.Exp(-integral);
    }
  }
}
