/*
 * General interfaces of mathematical functions
 *
 *
 */

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  ///   Interface of univariate function
  /// </summary>
  public interface IUnivariateFunction
  {
    /// <summary>
    ///   Evaluate function
    /// </summary>
    /// <param name="x">variable</param>
    /// <returns>function value</returns>
    double evaluate(double x);
  };

  /// <summary>
  ///   Interface of bivariate functions
  /// </summary>
  interface IBivariateFunction
  {
    /// <summary>
    ///   Evaluate function
    /// </summary>
    /// <param name="x">variable x</param>
    /// <param name="y">variable y</param>
    /// <returns>function value</returns>
    double evaluate(double x, double y);
  };

}
