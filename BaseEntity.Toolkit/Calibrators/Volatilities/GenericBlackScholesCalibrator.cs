using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  /// Class GenericBlackScholesCalibrator
  /// </summary>
  public class GenericBlackScholesCalibrator : BlackScholesSurfaceCalibrator
  {
    /// <summary>
    /// The spot price or rate.
    /// </summary>
    private ISpotCurve _spot;
    /// <summary>
    /// The rate or forward price curves.
    /// </summary>
    private IFactorCurve _curve1, _curve2;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericBlackScholesCalibrator" /> class.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="spot">The spot.</param>
    /// <param name="curve1">The curve1.</param>
    /// <param name="curve2">The curve2.</param>
    /// <param name="smileInterp">The smile interp.</param>
    /// <param name="timeInterp">The time interp.</param>
    /// <param name="interpolationSpace">The interpolation space.  The interpolation can be made in strike space, moneyness space or log-moneyness space.</param>
    public GenericBlackScholesCalibrator(Dt asOf,
      ISpotCurve spot, IFactorCurve curve1, IFactorCurve curve2,
      Interp smileInterp, Interp timeInterp, SmileModel interpolationSpace)
      : base(asOf, smileInterp, timeInterp, interpolationSpace)
    {
      _spot = spot;
      _curve1 = curve1;
      _curve2 = curve2;
    }

    /// <summary>
    /// Gets the Black-Scholes model parameters for the specified forward date.
    /// </summary>
    /// <param name="date">The forward date.</param>
    /// <returns>The Black-Scholes model parameters.</returns>
    /// <remarks>The derived classes must implement this method for their specific products.</remarks>
    public override IBlackScholesParameterData GetParameters(
      Dt date)
    {
      return BlackScholesSurfaceBuilder.GetParameters(AsOf, date,
        GetTime, _spot.Interpolate(date), _curve1, _curve2);
    }
  }
}
