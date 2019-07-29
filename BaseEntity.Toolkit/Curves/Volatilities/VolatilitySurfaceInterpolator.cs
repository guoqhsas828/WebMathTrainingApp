/*
 *   2012. All rights reserved.
 */

using System;
using System.Runtime.Serialization;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.Serialization;

namespace BaseEntity.Toolkit.Curves.Volatilities
{
  /// <summary>
  ///   A simple implementation of the volatility surface based on two delegates.
  /// </summary>
  [Serializable]
  public sealed class VolatilitySurfaceInterpolator : IExtendedVolatilitySurfaceInterpolator
  {
    /// <summary>
    ///   Initializes a new instance of the <see cref="VolatilitySurfaceInterpolator" /> class.
    /// </summary>
    /// <param name="timeCalculationFn">The time calculation function.  This may adapt to various business time convention.<br /></param>
    /// <param name="surfaceInterpolationFn">The surface interpolation function.</param>
    /// <remarks>
    /// </remarks>
    public VolatilitySurfaceInterpolator(
      Func<Dt, Dt, double> timeCalculationFn,
      Func<double, double, SmileInputKind, double> surfaceInterpolationFn)
    {
      SurfaceFunction = surfaceInterpolationFn;
      TimeCalculator = timeCalculationFn;
    }

    /// <summary>
    ///   Gets or sets the interpolation function.
    /// </summary>
    /// <value>The interpolation function.</value>
    public Func<double, double, SmileInputKind, double> SurfaceFunction { get; internal set; }

    /// <summary>
    ///   Gets or sets the time calculator.
    /// </summary>
    /// <value>The time calculator.</value>
    /// <remarks>
    /// </remarks>
    public Func<Dt, Dt, double> TimeCalculator { get; internal set; }

    /// <summary>
    /// Interpolates the specified surface.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="expiry">The expiry date.</param>
    /// <param name="input">The input level.</param>
    /// <param name="kind">The input kind (strike, moneyness, or log-moneyness).</param>
    /// <returns>System.Double.</returns>
    public double Interpolate(VolatilitySurface surface, Dt expiry,
      double input, SmileInputKind kind)
    {
      return SurfaceFunction(TimeCalculator(surface.AsOf, expiry),
        input, kind);
    }


    #region IVolatilitySurfaceInterpolator Members

    /// <summary>
    ///   Interpolates a volatility at given date and strike based on the specified surface.
    /// </summary>
    /// <param name="surface">The volatility surface.</param>
    /// <param name="expiry">The expiry date.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility at the given date and strike.</returns>
    /// <remarks>
    /// </remarks>
    public double Interpolate(VolatilitySurface surface, Dt expiry, double strike)
    {
      return Interpolate(surface, expiry, strike, surface.SmileInputKind);
    }

    #endregion

    #region Serialization events

    [OnSerializing]
    void WrapDelegates(StreamingContext context)
    {
      SurfaceFunction = SurfaceFunction.WrapSerializableDelegate();
      TimeCalculator = TimeCalculator.WrapSerializableDelegate();
    }

    [OnSerialized, OnDeserialized]
    void UnwrapDelegates(StreamingContext context)
    {
      SurfaceFunction = SurfaceFunction.UnwrapSerializableDelegate();
      TimeCalculator = TimeCalculator.UnwrapSerializableDelegate();
    }

    #endregion
  }
}
