using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///  Class VannaVolgaOverhedgeCalculator provides the methods to calculate
  ///  the over hedge coefficients based on the Vanna-Volga methodology.
  /// </summary>
  static class VannaVolgaOverhedgeCalculator
  {
    private const double DefaultDelta = 0.25;

    /// <summary>
    /// Calculates the over hedge coefficients.
    /// </summary>
    /// <param name="surface">Volatility surface.</param>
    /// <param name="fxCurveSet">The FX curve set.</param>
    /// <param name="date">The date.</param>
    /// <param name="smileAdjustment">The smile adjustment method.</param>
    /// <returns>VannaVolgaCoefficientsCollection.</returns>
    public static VannaVolgaCoefficientsCollection GetOverhedgeCoefs(
      this IVolatilitySurface surface,
      FxCurveSet fxCurveSet,
      Dt date, SmileAdjustmentMethod smileAdjustment)
    {
      var cs = surface as CalibratedVolatilitySurface;
      if (cs == null) return null;
      return GetOverhedgeCoefs(cs.Calibrator as FxOptionVannaVolgaCalibrator,
          cs, date, smileAdjustment) ??
        GetOverhedgeCoefs(cs.Calibrator as IBlackScholesParameterDataProvider,
          Double.NaN, DeltaStyle.None, cs, date, smileAdjustment) ??
        GetOverhedgeCoefs(cs.Interpolator as IExtendedVolatilitySurfaceInterpolator,
          Double.NaN, DeltaStyle.None, cs, fxCurveSet, date, smileAdjustment);
    }

    /// <summary>
    /// Calculates the over hedge coefficients.
    /// </summary>
    /// <param name="data">The Black-Scholes parameter data.</param>
    /// <param name="deltaSize">The delta size for the call and put strikes.</param>
    /// <param name="deltaStyle">The delta style.</param>
    /// <param name="smileInLogMoneyness">The smile function in terms of log-moneyness.</param>
    /// <param name="smileAdjustmentMethod">The smile adjustment method.</param>
    /// <returns>VannaVolgaCoefficientsCollection.</returns>
    public static VannaVolgaCoefficientsCollection GetOverhedgeCoefs(
      this IBlackScholesParameterData data,
      double deltaSize, DeltaStyle deltaStyle,
      Func<double, double> smileInLogMoneyness,
      SmileAdjustmentMethod smileAdjustmentMethod)
    {
      return GetOverhedgeCoefs(data, deltaSize, deltaStyle,
        Math.Log(data.GetForward()), smileInLogMoneyness,
        smileAdjustmentMethod);
    }

    private static VannaVolgaCoefficientsCollection GetOverhedgeCoefs(
      IExtendedVolatilitySurfaceInterpolator interpolator,
      double delta, DeltaStyle deltaStyle,
      VolatilitySurface surface, FxCurveSet fx,
      Dt date, SmileAdjustmentMethod smileAdjustment)
    {
      if (interpolator == null) return null;
      var input = fx.GetRatesSigmaTime(surface.AsOf, date, null);
      var data = new BlackScholesParameterData(
        input.T, fx.SpotRate, input.Rd, input.Rf);
      var logfwd = Math.Log(data.GetForward());
      const SmileInputKind kind = SmileInputKind.LogMoneyness;
      return GetOverhedgeCoefs(data, delta, deltaStyle, logfwd,
        m => interpolator.Interpolate(surface, date, m, kind),
        smileAdjustment);
    }

    private static VannaVolgaCoefficientsCollection GetOverhedgeCoefs(
      FxOptionVannaVolgaCalibrator vvCalibrator,
      CalibratedVolatilitySurface surface,
      Dt date, SmileAdjustmentMethod smileAdjustment)
    {
      if (vvCalibrator == null) return null;
      return vvCalibrator.OverHedgeCoefs(
        smileAdjustment == SmileAdjustmentMethod.OverHedgeCostsMarket,
        surface, date);
    }

    private static VannaVolgaCoefficientsCollection GetOverhedgeCoefs(
      IBlackScholesParameterDataProvider calibrator,
      double delta, DeltaStyle deltaStyle,
      VolatilitySurface surface,
      Dt date, SmileAdjustmentMethod smileAdjustment)
    {
      if (calibrator == null) return null;
      var data = calibrator.GetParameters(date);

      var logfwd = Math.Log(data.GetForward());
      switch (surface.SmileInputKind)
      {
      case SmileInputKind.LogMoneyness:
        return GetOverhedgeCoefs(data, delta, deltaStyle, logfwd,
          m => surface.Interpolate(date, m, SmileInputKind.Strike),
          smileAdjustment);
      case SmileInputKind.Moneyness:
        return GetOverhedgeCoefs(data, delta, deltaStyle, logfwd,
          m => surface.Interpolate(date, Math.Exp(m), SmileInputKind.Strike),
          smileAdjustment);
      }
      return GetOverhedgeCoefs(data, delta, deltaStyle, logfwd,
        m => surface.Interpolate(date, Math.Exp(m + logfwd), SmileInputKind.Strike),
        smileAdjustment);
    }

    private static VannaVolgaCoefficientsCollection GetOverhedgeCoefs(
      this IBlackScholesParameterData data,
      double delta, DeltaStyle deltaStyle,
      double logfwd, Func<double, double> smileInLogMoneyness,
      SmileAdjustmentMethod smileAdjustmentMethod)
    {
      if (Double.IsNaN(logfwd))
        throw new ArgumentException("Log forward is NaN");
      if (data == null || Double.IsNaN(data.Spot))
        return null;

      if (Double.IsNaN(delta))
      {
        delta = DefaultDelta;
        deltaStyle = data.Time < 1.0 ? DeltaStyle.None : DeltaStyle.Forward;
      }

      double katm, sigatm;
      data.GetAtmStrikeAndVolatility(smileInLogMoneyness,
        AtmKind.Forward, (deltaStyle & DeltaStyle.PremiumIncluded) != 0,
        out katm, out sigatm);

      if ((deltaStyle & DeltaStyle.Forward) == 0)
        delta = data.ConvertSpotToForwardDelta(delta);

      var mput = BlackScholes.GetLogMoneynessFromDelta(
        smileInLogMoneyness, delta, Math.Sqrt(data.Time), -1);
      var kput = Math.Exp(mput + logfwd);
      var sigput = smileInLogMoneyness(mput);

      var mcall = BlackScholes.GetLogMoneynessFromDelta(
        smileInLogMoneyness, delta, Math.Sqrt(data.Time), 1);
      var kcall = Math.Exp(mcall + logfwd);
      var sigcall = smileInLogMoneyness(mcall);

      return VannaVolgaCalculator.GetCoefficients(
        smileAdjustmentMethod == SmileAdjustmentMethod.OverHedgeCostsMarket,
        data.Spot, data.Time, data.Rate2, data.Rate1,
        sigput, sigatm, sigcall, kput, katm, kcall);
    }
  }
}
