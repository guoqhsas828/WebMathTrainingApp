/*
 * IRateVolatilityInterpolator.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  /// Interpolator for a rate volatility cube.
  /// </summary>
  public interface IRateVolatilityInterpolator
  {
    /// <summary>
    /// Interpolates the specified expiry.
    /// </summary>
    /// <param name="cube">The cube.</param>
    /// <param name="expiry">The expiry.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    double Interpolate(CalibratedVolatilitySurface cube, Dt expiry, double strike);
  }
}
