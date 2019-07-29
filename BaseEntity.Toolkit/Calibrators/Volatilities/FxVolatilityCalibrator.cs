/*
 * FxVolatilityCalibrator.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  /// Base class for the Fx Volatility calibration
  /// </summary>
  [Serializable]
  public abstract class FxVolatilityCalibrator : IVolatilitySurfaceCalibrator, IVolatilitySurfaceInterpolator
  {
    /// <summary>
    /// the Constructor of the FX Volatility calibrator
    /// </summary>
    /// <param name="asOf">The start date of volatility</param>
    /// <param name="domesticRateCurve">The domestic rate curve</param>
    /// <param name="foreignRateCurve">The foreign rate curve</param>
    /// <param name="fxCurve">The FX curve</param>
    protected FxVolatilityCalibrator(Dt asOf,
                                     DiscountCurve domesticRateCurve,
                                     DiscountCurve foreignRateCurve,
                                     FxCurve fxCurve)
    {
      AsOf = asOf;
      DomesticRateCurve = domesticRateCurve;
      ForeignRateCurve = foreignRateCurve;
      FxCurve = fxCurve;
    }

    #region IVolatilitySurfaceCalibrator Members

    /// <summary>
    /// Fit method to be overridden in the inheritors
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="idx">The idx.</param>
    public abstract void FitFrom(CalibratedVolatilitySurface surface, int idx);

    #endregion

    #region IVolatilitySurfaceInterpolator Members

    /// <summary>
    /// Interpolates the specified maturity.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="strike">The strike.</param>
    /// <returns></returns>
    public abstract double Interpolate(VolatilitySurface surface, Dt maturity, double strike);

    #endregion

    #region properties

    /// <summary>
    /// AsOf date
    /// </summary>
    public Dt AsOf { get; }

    /// <summary>
    /// Domestic Discount curve
    /// </summary>
    public FxCurve FxCurve { get; }

    /// <summary>
    /// Domestic Discount curve
    /// </summary>
    public DiscountCurve DomesticRateCurve { get; }

    /// <summary>
    /// Foreing discount curve
    /// </summary>
    public DiscountCurve ForeignRateCurve { get; }

    #endregion
  }

}