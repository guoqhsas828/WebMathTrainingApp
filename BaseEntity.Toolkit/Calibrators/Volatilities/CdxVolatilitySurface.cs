/*
 * 
 */
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities.Bump;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  /// CDX volatility surface for specific strike type and volatility model
  /// </summary>
  /// <seealso cref="CalibratedVolatilitySurface" />
  [Serializable]
  public sealed class CdxVolatilitySurface : CalibratedVolatilitySurface
  {
    #region Constructors

    /// <summary>
    /// Creates the CDX volatility surface for specified strike type and volatility model
    /// </summary>
    /// <param name="surface">The surface containing the original quotes and calibrator</param>
    /// <param name="underlying">The CDX volatility underlying</param>
    /// <param name="modelType">The CDX option model type</param>
    /// <returns>CdxVolatilitySurface</returns>
    public static CdxVolatilitySurface Create(
      VolatilitySurface surface,
      CdxVolatilityUnderlying underlying,
      CDXOptionModelType? modelType = null)
    {
      var cdxSurface = new CdxVolatilitySurface(
        (surface as CdxVolatilitySurface)?.BaseSurface ?? surface,
        underlying, modelType);
      var bumped = surface.Interpolator as VolatilityBumpInterpolator;
      if (bumped != null)
      {
        cdxSurface.Interpolator = VolatilityBumpInterpolator.Create(
          cdxSurface.Interpolator, bumped.BumpSize, bumped.BumpFlags,
          bumped.Accumulator);
      }
      return cdxSurface;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CdxVolatilitySurface"/> class.
    /// </summary>
    /// <param name="baseSurface">The base surface containing the original quotes and calibrator</param>
    /// <param name="underlying">The CDX volatility underlying</param>
    /// <param name="modelType">The CDX option model type</param>
    private CdxVolatilitySurface(
      VolatilitySurface baseSurface,
      CdxVolatilityUnderlying underlying,
      CDXOptionModelType? modelType)
      : base(baseSurface.AsOf,
        (baseSurface as CalibratedVolatilitySurface)?.Tenors,
        WrappedCalibrator.Instance,
        CdxVolatilityInterpolator.Instance)
    {
      Underlying = underlying;
      BaseSurface = baseSurface;
      ModelType = modelType ?? underlying.ModelType;
      Name = baseSurface.Name;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets a value indicating whether the strike is price.
    /// </summary>
    /// <value><c>true</c> if strike is price; otherwise, <c>false</c>.</value>
    public bool StrikeIsPrice => Underlying.StrikeIsPrice;

    /// <summary>
    /// Gets the CDX option model type
    /// </summary>
    /// <value>The option model</value>
    public CDXOptionModelType ModelType { get; }

    /// <summary>
    /// Gets the volatility underlying.
    /// </summary>
    /// <value>The underlying.</value>
    public CdxVolatilityUnderlying Underlying { get; }

    /// <summary>
    /// Gets the base surface containing the original quotes and calibrator
    /// </summary>
    /// <value>The base surface</value>
    internal VolatilitySurface BaseSurface { get; }

    #endregion

    #region Nested type: WrappedCalibrator

    /// <summary>
    /// Class WrappedCalibrator.
    /// </summary>
    /// <seealso cref="IVolatilitySurfaceCalibrator" />
    [Serializable]
    private class WrappedCalibrator : SingletonBase<WrappedCalibrator>
      , IVolatilitySurfaceCalibrator
    {
      /// <summary>
      /// Fit a surface from the specified tenor point
      /// </summary>
      /// <param name="surface">The volatility surface to fit.</param>
      /// <param name="fromTenorIdx">The tenor index to start fit.</param>
      /// <remarks><para>Derived calibrators implement this to do the work of the fitting.</para>
      /// <para>Called by Fit() and Refit(), it can be assumed that the tenors have been validated.</para>
      /// <para>When the start index <paramref name="fromTenorIdx" /> is 0,
      /// a full fit is requested.  Otherwise a partial fit start from
      /// the given tenor is requested.  The derived class can do either
      /// a full fit a a partial fit as it sees appropriate.</para></remarks>
      public void FitFrom(CalibratedVolatilitySurface surface, int fromTenorIdx)
      {
        // Delegate all the works to the base surface
        var baseSurface = (surface as CdxVolatilitySurface)?
          .BaseSurface as CalibratedVolatilitySurface;
        baseSurface?.Calibrator?.FitFrom(baseSurface, fromTenorIdx);
      }
    }

    #endregion

    #region Nested Type: CdxVolatilityInterpolator

    /// <summary>
    /// Class CdxVolatilityInterpolator.
    /// </summary>
    /// <seealso cref="IVolatilitySurfaceInterpolator" />
    [Serializable]
    private class CdxVolatilityInterpolator
      : SingletonBase<CdxVolatilityInterpolator>
      , IVolatilitySurfaceInterpolator
    {
      /// <summary>
      /// Interpolates the volatility value from the specified surface.
      /// </summary>
      /// <param name="surface">The surface</param>
      /// <param name="expiry">The expiry date</param>
      /// <param name="strike">The strike value</param>
      /// <returns>System.Double.</returns>
      /// <exception cref="InvalidOperationException">CdxVolatilitySurface</exception>
      public double Interpolate(VolatilitySurface surface, Dt expiry, double strike)
      {
        var cdxVol = surface as CdxVolatilitySurface;
        if (cdxVol == null)
        {
          throw new InvalidOperationException(
            $"Expect {nameof(CdxVolatilitySurface)}, but got ${surface.GetType().Name}");
        }
        return CdxVolatilityUnderlying.InterpolateAndConvert(
          cdxVol.Underlying, cdxVol.BaseSurface, expiry, strike,
          cdxVol.StrikeIsPrice, cdxVol.ModelType);
      }
    }

    #endregion
  }
}
