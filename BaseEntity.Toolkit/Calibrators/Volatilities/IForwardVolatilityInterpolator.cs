/*
 * IForwardVolatilityInterpolator.cs
 *
 *  -2011. All rights reserved.
 *
 */
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///   The interface to interpolate forward volatilitites.
  /// </summary>
  /// <remarks>
  ///  The derived interpolators should implement concrete, normally model-based
  ///   interpolation method.
  /// </remarks>
  public interface IForwardVolatilityInterpolator
  {
    /// <summary>
    /// Interpolates the volatility from a forward start date
    /// to the expiry date for an option with the specified strike.
    /// </summary>
    /// <param name="start">The start date.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>
    /// The volatility at the given date and strike.
    /// </returns>
      double Interpolate(Dt start, double strike);
  }
}
