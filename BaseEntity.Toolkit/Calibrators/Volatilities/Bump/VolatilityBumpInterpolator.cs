/*
 * 
 */
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;

namespace BaseEntity.Toolkit.Calibrators.Volatilities.Bump
{
  /// <summary>
  /// Add bump to existing volatility interpolator.
  /// </summary>
  /// <seealso cref="IVolatilitySurfaceInterpolator" />
  public class VolatilityBumpInterpolator : IVolatilitySurfaceInterpolator
  {
    /// <summary>
    /// Creates an interpolator which add the specified bump to the base interpolator.
    /// </summary>
    /// <param name="baseInterpolator">The base interpolator.</param>
    /// <param name="bumpSize">Size of the bump</param>
    /// <param name="flags">The flags controlling how to perform bump</param>
    /// <param name="accumulator">The accumulator to record the actual bump amounts</param>
    /// <returns>VolatilityBumpInterpolator.</returns>
    public static VolatilityBumpInterpolator Create(
      IVolatilitySurfaceInterpolator baseInterpolator,
      double bumpSize, BumpFlags flags, BumpAccumulator accumulator)
    {
      var extended = baseInterpolator as IExtendedVolatilitySurfaceInterpolator;
      return extended == null
        ? new VolatilityBumpInterpolator(
          baseInterpolator, bumpSize, flags, accumulator)
        : new VolatilityBumpSmileInterpolator(
          extended, bumpSize, flags, accumulator);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VolatilityBumpInterpolator"/> class.
    /// </summary>
    /// <param name="baseInterpolator">The base interpolator.</param>
    /// <param name="bumpSize">Size of the bump.</param>
    /// <param name="flags">The flags.</param>
    /// <param name="accumulator">The accumulator.</param>
    internal VolatilityBumpInterpolator(
      IVolatilitySurfaceInterpolator baseInterpolator,
      double bumpSize, BumpFlags flags, BumpAccumulator accumulator)
    {
      BaseInterpolator = baseInterpolator;
      BumpSize = bumpSize;
      BumpFlags = flags;
      Accumulator = accumulator;
    }

    /// <summary>
    /// Interpolates a volatility at the specified date and strike.
    /// </summary>
    /// <param name="surface">The volatility surface.</param>
    /// <param name="expiry">The expiry date.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility at the given date and strike.</returns>
    public double Interpolate(VolatilitySurface surface, Dt expiry, double strike)
    {
      var interpolated = BaseInterpolator.Interpolate(surface, expiry, strike);
      return ApplyBump(surface.Name, interpolated);
    }

    /// <summary>
    /// Applies the bump.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="baseValue">The base value.</param>
    /// <returns>System.Double.</returns>
    internal double ApplyBump(string name, double baseValue)
    {
      bool up = (BumpFlags & BumpFlags.BumpDown) == 0,
        relative = (BumpFlags & BumpFlags.BumpRelative) != 0;
      var bumpAmt = VolatilityBumpUtility.CalculateBumpAmount(name,
        baseValue, BumpSize, relative, up, !up); // allow up bump from zero
      if (bumpAmt > 0 || bumpAmt < 0)
      {
        Accumulator.Add(up ? bumpAmt : -bumpAmt);
        return baseValue + bumpAmt;
      }
      return baseValue;
    }

    /// <summary>
    /// Gets the base interpolator.
    /// </summary>
    /// <value>The base interpolator.</value>
    internal IVolatilitySurfaceInterpolator BaseInterpolator { get; }

    /// <summary>
    /// Gets the size of the bump.
    /// </summary>
    /// <value>The size of the bump.</value>
    internal double BumpSize { get; }

    /// <summary>
    /// Gets the bump flags.
    /// </summary>
    /// <value>The bump flags.</value>
    internal BumpFlags BumpFlags { get; }

    /// <summary>
    /// Gets the accumulator.
    /// </summary>
    /// <value>The accumulator.</value>
    internal BumpAccumulator Accumulator { get; }
  }

  /// <summary>
  /// Add the specified bump to the base interpolator
  ///  and handle various types of the smile inputs.
  /// </summary>
  /// <seealso cref="VolatilityBumpInterpolator" />
  /// <seealso cref="IExtendedVolatilitySurfaceInterpolator" />
  internal class VolatilityBumpSmileInterpolator
    : VolatilityBumpInterpolator, IExtendedVolatilitySurfaceInterpolator
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="VolatilityBumpSmileInterpolator"/> class.
    /// </summary>
    /// <param name="baseInterpolator">The base interpolator.</param>
    /// <param name="bumpSize">Size of the bump.</param>
    /// <param name="flags">The flags.</param>
    /// <param name="accumulator">The accumulator.</param>
    public VolatilityBumpSmileInterpolator(
      IExtendedVolatilitySurfaceInterpolator baseInterpolator,
      double bumpSize, BumpFlags flags, BumpAccumulator accumulator)
      : base(baseInterpolator, bumpSize, flags, accumulator)
    {
    }

    /// <summary>
    /// Interpolates volatility from the specified surface.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="expiry">The expiry date</param>
    /// <param name="smileInputValue">The smile input value.</param>
    /// <param name="smileInputKind">Kind of the smile input.</param>
    /// <returns>System.Double.</returns>
    public double Interpolate(VolatilitySurface surface, Dt expiry,
      double smileInputValue, SmileInputKind smileInputKind)
    {
      var interpolated = Interpolator.Interpolate(
        surface, expiry, smileInputValue, smileInputKind);
      return ApplyBump(surface.Name, interpolated);
    }

    /// <summary>
    /// Gets the interpolator.
    /// </summary>
    /// <value>The interpolator.</value>
    private IExtendedVolatilitySurfaceInterpolator Interpolator
      => (IExtendedVolatilitySurfaceInterpolator) BaseInterpolator;
  }

  /// <summary>
  /// Class SwapVolatilityBumpInterpolator.
  /// </summary>
  /// <seealso cref="VolatilityBumpInterpolator" />
  /// <seealso cref="ISwapVolatilitySurfaceInterpolator" />
  public class SwapVolatilityBumpInterpolator
    : VolatilityBumpInterpolator, ISwapVolatilitySurfaceInterpolator
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="SwapVolatilityBumpInterpolator"/> class.
    /// </summary>
    /// <param name="baseInterpolator">The base interpolator.</param>
    /// <param name="bumpSize">Size of the bump.</param>
    /// <param name="flags">The flags.</param>
    /// <param name="accumulator">The accumulator.</param>
    public SwapVolatilityBumpInterpolator(
      ISwapVolatilitySurfaceInterpolator baseInterpolator,
      double bumpSize, BumpFlags flags, BumpAccumulator accumulator)
      : base(baseInterpolator, bumpSize, flags, accumulator)
    {
    }

    /// <summary>
    /// Interpolates the volatility at the specified expiry date and rate duration.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="expiry">The expiry.</param>
    /// <param name="duration">The duration.</param>
    /// <returns>The volatility.</returns>
    public double Interpolate(RateVolatilitySurface surface, Dt expiry, double duration)
    {
      var interpolated = Interpolator.Interpolate(surface, expiry, duration);
      return ApplyBump(surface.Name, interpolated);
    }

    /// <summary>
    /// Interpolates the volatility at the specified expiry date,
    /// rate duration and strike skew.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="expiry">The expiry.</param>
    /// <param name="skew">The strike skew.</param>
    /// <param name="duration">The duration.</param>
    /// <returns>System.Double.</returns>
    public double Interpolate(RateVolatilitySurface surface, Dt expiry, double skew, double duration)
    {
      var interpolated = Interpolator.Interpolate(surface, expiry, skew, duration);
      return ApplyBump(surface.Name, interpolated);
    }

    /// <summary>
    /// Gets the interpolator.
    /// </summary>
    /// <value>The interpolator.</value>
    private ISwapVolatilitySurfaceInterpolator Interpolator
      => (ISwapVolatilitySurfaceInterpolator)BaseInterpolator;
  }
}
