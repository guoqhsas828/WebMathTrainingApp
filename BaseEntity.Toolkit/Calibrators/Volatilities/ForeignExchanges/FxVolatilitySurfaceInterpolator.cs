/*
 *   2012. All rights reserved.
 */

using System;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Calibrators.Volatilities.ForeignExchanges
{
  /// <summary>
  ///  FX volatility surface interpolator.
  /// </summary>
  /// <remarks></remarks>
  [Serializable]
  public class FxVolatilitySurfaceInterpolator : IVolatilitySurfaceInterpolator
  {
    private readonly double[] _dates;
    private readonly Func<double, double>[] _smiles;
    private readonly Interp _timeInterp;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="timeInterp">Time interpolator</param>
    /// <param name="dates">dates</param>
    /// <param name="smiles">Smiles</param>
    protected internal FxVolatilitySurfaceInterpolator(
      Interp timeInterp,
      double[] dates,
      Func<double, double>[] smiles)
    {
      _timeInterp = timeInterp;
      _dates = dates;
      _smiles = smiles;
    }

    #region IVolatilityInterpolator Members

    /// <summary>
    /// Interpolates a volatility at given date and strike based on the specified surface.
    /// </summary>
    /// <param name="surface">The volatility surface.</param>
    /// <param name="expiry">The expiry date.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility at the given date and strike.</returns>
    /// <remarks></remarks>
    public double Interpolate(VolatilitySurface surface, Dt expiry, double strike)
    {
      Dt asOf = surface.AsOf;
      double[] vols = _smiles.Select(s => s(strike)).ToArray();
      return new Interpolator(_timeInterp, _dates, vols)
        .evaluate((expiry - asOf) / 365.25);
    }

    #endregion
  }
}