/*
 * IVolatilityInterpolator.cs
 *
 *  -2011. All rights reserved.
 *
 */
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Curves.Volatilities
{
  /// <summary>
  ///   The interface of the volatility interpolator.
  /// </summary>
  /// <remarks>
  ///  The derived interpolators should implement concrete, normally model-based
  ///   interpolation method.
  /// </remarks>
  public interface IVolatilitySurfaceInterpolator
  {
    /// <summary>
    /// Interpolates a volatility at the specified date and strike.
    /// </summary>
    /// <param name="surface">The volatility surface.</param>
    /// <param name="expiry">The expiry date.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility at the given date and strike.</returns>
    double Interpolate(VolatilitySurface surface, Dt expiry, double strike);
  }

  /// <summary>
  /// Interface IExtendedVolatilitySurfaceInterpolator
  /// </summary>
  /// <seealso cref="IVolatilitySurfaceInterpolator" />
  public interface IExtendedVolatilitySurfaceInterpolator
    : IVolatilitySurfaceInterpolator
  {
    /// <summary>
    /// Interpolates the specified surface.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="expiry">The expiry date</param>
    /// <param name="smileInputValue">The smile input value.</param>
    /// <param name="smileInputKind">Kind of the smile input.</param>
    /// <returns>System.Double.</returns>
    double Interpolate(VolatilitySurface surface, Dt expiry,
      double smileInputValue, SmileInputKind smileInputKind);
  }
}