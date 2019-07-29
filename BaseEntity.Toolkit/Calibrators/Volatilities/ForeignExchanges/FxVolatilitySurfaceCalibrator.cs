/*
 *   2012. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;

//TODO: Make this generic, replace stock sample vol surface.
//TODO: Make FX Market Terms..


namespace BaseEntity.Toolkit.Calibrators.Volatilities.ForeignExchanges
{
  /// <summary>
  ///   FX Volatility Surface Calibrator
  /// </summary>
  [Serializable]
  public sealed class FxVolatilitySurfaceCalibrator : BlackScholesSurfaceCalibrator
  {
    private readonly DiscountCurve _domesticRateCurve, _foreignRateCurve;
    private readonly FxCurve _fxCurve;

    /// <summary>
    /// constructor for the Fx Volatility calibrator
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="volatilityTimeInterp">The volatility time interpolation method.</param>
    /// <param name="volatilitySmileInterp">The volatility smile interpolation method.</param>
    /// <param name="domesticRateCurve">The domestic rate curve.</param>
    /// <param name="foreignRateCurve">The foreign rate curve.</param>
    /// <param name="fxCurve">The fx curve.</param>
    public FxVolatilitySurfaceCalibrator(
      Dt asOf,
      Interp volatilityTimeInterp,
      Interp volatilitySmileInterp,
      DiscountCurve domesticRateCurve,
      DiscountCurve foreignRateCurve,
      FxCurve fxCurve)
      : base(asOf, volatilitySmileInterp,volatilityTimeInterp,SmileModel.QuadraticRegression)
    {
      _domesticRateCurve = domesticRateCurve;
      _foreignRateCurve = foreignRateCurve;
      _fxCurve = fxCurve;
    }

    /// <summary>
    /// Gets the Black-Scholes model parameters for the specified forward date.
    /// </summary>
    /// <param name="date">The forward date.</param>
    /// <returns>The Black-Scholes model parameters.</returns>
    /// <remarks></remarks>
    public override IBlackScholesParameterData GetParameters(Dt date)
    {
      return BlackScholesCalibration.GetParameters(AsOf, date, GetTime,
        _domesticRateCurve, _fxCurve);
    }
  }
}