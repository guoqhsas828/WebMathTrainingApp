/*
 *   2017. All rights reserved.
 */
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  /// Interface ISwapVolatilitySurfaceInterpolator
  /// </summary>
  /// <seealso cref="BaseEntity.Toolkit.Curves.Volatilities.IVolatilitySurfaceInterpolator" />
  public interface ISwapVolatilitySurfaceInterpolator : IVolatilitySurfaceInterpolator
  {
    /// <summary>
    /// Interpolates the volatility at the specified expiry date and rate duration.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="expiry">The expiry.</param>
    /// <param name="duration">The duration.</param>
    /// <returns>The volatility.</returns>
    double Interpolate(RateVolatilitySurface surface, Dt expiry, double duration);

    /// <summary>
    /// Interpolates the volatility at the specified expiry date,
    ///  rate duration and strike skew.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="expiry">The expiry.</param>
    /// <param name="skew">The strike skew.</param>
    /// <param name="duration">The duration.</param>
    /// <returns>System.Double.</returns>
    double Interpolate(RateVolatilitySurface surface, Dt expiry, double skew, double duration);
  }
}
