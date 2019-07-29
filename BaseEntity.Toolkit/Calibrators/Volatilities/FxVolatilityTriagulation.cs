/*
 *   2004-2017. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;
using static System.Math;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  /// Utility methods for FX volatility triangulation.
  /// </summary>
  /// 
  /// <remarks>
  /// <para>Consider three currencies, <c>ccy0</c>, <c>ccy1</c> and <c>ccy2</c>.
  /// Let <m>X_1</m> be <c>ccy1/ccy0</c> rate,
  /// <m>X_2</m> be <c>ccy0/ccy2</c>.
  /// By triangulation, the <c>FX</c> rate <c>ccy1/ccy2</c>,
  /// denoted by <m>X_3</m>, is given by<math>
  ///   X_3 = {X_1}\cdot{X_2}
  /// </math></para>
  /// 
  /// <para>Let <m>\sigma_1(T, K_1)</m> and <m>\sigma_2(T, K_2)</m> represent
  /// the volatility surfaces of the <abbr>FX</abbr> rates <m>X_1</m> and <m>X_2</m>,
  /// respectively, where <m>\sigma_i(T, K_i)</m> gives the Black volatility
  /// at the strike <m>K_i</m> and expiry <m>T</m>.</para>
  /// 
  /// <para>We assume that the dynamics of <m>X_i</m> can be approximated by
  ///  <math>\begin{align}
  ///   \frac{d X_1(t)}{X_1(t)} &amp;= (r_0 - r_1)\, dt + \sigma_1(T,K_1)\, dW_1(t)
  ///   \\ \frac{d X_2(t)}{X_2(t)} &amp;= (r_2 - r_0)\, dt + \sigma_2(T,K_2)\, dW_2(t)
  ///  \end{align}</math>where <m>W_i</m>, <m>i=1,2</m>,
  ///  are correlated standard Brownian motions.
  /// </para>
  /// 
  /// <para>Since<math>
  ///  \frac{d X_3(t)}{X_3(t)} = \frac{d X_1t)}{X_1(t)} + \frac{d X_2(t)}{X_2(t)}
  ///  </math>
  ///  The total variance of <m>X_3(T)</m> is apparently<math>
  ///   \mathrm{var}\left(X_3(T)\right)
  ///    = \sigma_1(T,K_1)^2 T + \sigma_2(T,K_2)^2 T
  ///    + 2T\,\rho_T\,\sigma_1(T,K_1)\,\sigma_2(T,K_2)
  ///  </math>where <m>\rho_T</m> is the total correlation at time T.
  ///  <math>
  ///   \rho_T = \frac{1}{T}\,\int_0^T \langle dW_1(t), dW_2(t)\rangle\,dt
  /// </math></para>
  /// 
  /// <para>To build a volatility surface for <m>X_3</m>, we need to map the strike <m>K</m>
  ///  in rate <m>X_3</m> to some pairs <m>(K_1, K_2)</m> such that <m>K_1{\cdot}K_2 = K</m>.
  /// </para>
  /// 
  /// <para>There are many maps satisfying the above condition.
  ///  For simplicity, we pick a pair which minimizes the distance
  ///  of <m>(K_1, K_2)</m> to the ATM strike pair in logarithmic space.
  ///  Denote this pair by <m>(\kappa_1(K), \kappa_2(K))</m>.</para>
  /// 
  /// <para>Hence the resulting volatility surface of <m>X_3</m> can be written as
  ///  <math>
  ///   \sigma_3(T,K) \equiv \sqrt{
  ///   \sigma_1(T, \kappa_1(K))^2 + \sigma_2(T, \kappa_2(K))^2
  ///   + 2\rho_T\,\sigma_1(T, \kappa_1(K))\, \sigma_2(T, \kappa_2(K))
  ///   }
  /// </math></para>
  /// 
  /// </remarks>
  public static class FxVolatilityTriangulation
  {
    #region static builders

    /// <summary>
    /// Creates the volatility surface of the FX rate
    /// <c>ccy1/ccy2</c> by triangulation.
    /// </summary>
    /// 
    /// <param name="fxCurve1">The curve of the FX rate containing the first currency</param>
    /// <param name="fxSurface1">The volatility surface of the FX rate containing the first currency</param>
    /// <param name="fxCurve2">The curve of the FX rate containing the second currency</param>
    /// <param name="fxSurface2">The volatility surface of the FX rate containing the second currency</param>
    /// <param name="correlation">The correlation curve</param>
    /// <returns>CalibratedVolatilitySurface</returns>
    public static CalibratedVolatilitySurface Create(
      FxCurve fxCurve1, VolatilitySurface fxSurface1,
      FxCurve fxCurve2, VolatilitySurface fxSurface2,
      Func<Dt, double> correlation)
    {
      ValidateCorrelation(correlation);

      Dt asOf = fxSurface1.AsOf;
      Debug.Assert(asOf == fxSurface2.AsOf);

      var calibrator = new Calibrator(
        fxCurve1, fxSurface1, fxCurve2, fxSurface2, correlation);
      return new CalibratedVolatilitySurface(asOf, 
        EmptyArray<IVolatilityTenor>.Instance, calibrator, calibrator);
    }

    /// <summary>
    /// Creates the volatility surface of the FX rate
    /// <c>ccy1/ccy2</c> by triangulation.
    /// </summary>
    /// 
    /// <param name="fxCurve1">The curve of the FX rate containing the first currency</param>
    /// <param name="fxSurface1">The volatility surface of the FX rate containing the first currency</param>
    /// <param name="fxCurve2">The curve of the FX rate containing the second currency</param>
    /// <param name="fxSurface2">The volatility surface of the FX rate containing the second currency</param>
    /// <param name="correlation">The correlation</param>
    /// <returns>CalibratedVolatilitySurface</returns>
    public static CalibratedVolatilitySurface Create(
      FxCurve fxCurve1, VolatilitySurface fxSurface1,
      FxCurve fxCurve2, VolatilitySurface fxSurface2,
      double correlation)
    {
      ValidateCorrelation(correlation);
      return Create(fxCurve1, fxSurface1, fxCurve2, fxSurface2,
        DelegateFactory.ConstantFn<Dt,double>(correlation));
    }

    #endregion

    #region Validation

    private static void ValidateCorrelation(Func<Dt, double> fn)
    {
      var curve = fn.Target as Curve;
      if (curve == null) return;
      for (int i = 0, n = curve.Count; i < n; ++i)
      {
        var idx = i;
        var corr = curve.GetVal(idx);
        ValidateCorrelation(corr, 
          () => $"Invalid correlation {corr} at curve point {idx}");
      }
    }

    private static void ValidateCorrelation(
      double correlation, Func<string> msgFn = null)
    {
      if (correlation >= -1 && correlation <= 1) return;
      var msg = msgFn != null ? msgFn()
        : $"Invalid correlation {correlation}";
      throw new ArgumentException(msg);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Gets the common currency in the two FX rates.
    /// </summary>
    /// <param name="ccy11">Currency 1 of FX rate 1</param>
    /// <param name="ccy12">Currency 2 of FX rate 1</param>
    /// <param name="ccy21">Currency 1 of FX rate 2</param>
    /// <param name="ccy22">Currency 2 of FX rate 2</param>
    /// <returns>Currency</returns>
    /// <exception cref="System.ArgumentException"></exception>
    private static Currency GetCommonCurrency(
      Currency ccy11, Currency ccy12,
      Currency ccy21, Currency ccy22)
    {
      if (ccy11 == ccy21 || ccy11 == ccy22)
        return ccy11;
      if (ccy12 != ccy21 && ccy12 != ccy22)
      {
        throw new ArgumentException(
          $"Unable to triangulate {ccy11}/{ccy12} and {ccy21}/{ccy22}");
      }
      return ccy12;
    }

    /// <summary>
    /// Maps the specified strike of the triangulated FX rate
    /// to the strikes of the two underlying FX rates.
    /// </summary>
    /// <param name="logK">The logarithm of the strike</param>
    /// <param name="logX1">The logarithm of the ATM FX rate 1</param>
    /// <param name="logX2">The logarithm of the ATM FX rate 2</param>
    /// <param name="logK1">The logarithm of strike 1</param>
    /// <param name="logK2">The logarithm of strike 2</param>
    /// <remarks>
    ///  Let <m>X_1</m> and <m>X_2</m> be the FX rates <c>cc1/ccy0</c>
    ///  and <c>ccy0/ccy2</c>, respectively.  Given the strike <m>K</m>
    ///  of the FX rate <c>ccy1/ccr2</c>, this method find two strike
    ///  <m>K_1</m> and <m>K_2</m> which solve the minimization problem
    ///  <math>
    ///  \min_{K_1,K_2} (\log{K_1}-\log{X_1})^2 + (\log{K_2} - \log{X_2})^2
    ///  \quad\text{s.t.}\ K_1{\cdot}K_2 = K
    ///  </math>
    /// </remarks>
    private static void MapStrike(
      double logK,
      double logX1, double logX2,
      out double logK1, out double logK2)
    {
      var d = logX1 - logX2;
      logK1 = (logK + d)/2;
      logK2 = (logK - d)/2;
    }

    /// <summary>
    /// Combines the volatility.
    /// </summary>
    /// <param name="sigma1">The sigma1.</param>
    /// <param name="sigma2">The sigma2.</param>
    /// <param name="rho">The rho.</param>
    /// <returns>System.Double.</returns>
    private static double CombineVolatility(
      double sigma1, double sigma2, double rho)
    {
      if (rho < -1) rho = -1;
      else if (rho > 1) rho = 1;
      var s2 = sigma1*sigma1 + sigma2*sigma2 + 2*rho*sigma1*sigma2;
      return s2 < 1E-16 && s2 > -1E-16 ? 0.0 : Sqrt(s2);
    }

    #endregion

    #region Nested type: calibrator/interpolator

    /// <summary>
    /// The simple calibrator and interpolator for FX volatility triangulation.
    /// </summary>
    /// <seealso cref="IVolatilitySurfaceCalibrator" />
    /// <seealso cref="IExtendedVolatilitySurfaceInterpolator" />
    private class Calibrator : IVolatilitySurfaceCalibrator
      , IExtendedVolatilitySurfaceInterpolator
      , IVolatilitySurfaceProvider
    {
      /// <summary>
      /// Initializes a new instance of the <see cref="Calibrator" /> class.
      /// </summary>
      /// <param name="fxCurve1">The fx curve1.</param>
      /// <param name="surface1">The surface1.</param>
      /// <param name="fxCurve2">The fx curve2.</param>
      /// <param name="surface2">The surface2.</param>
      /// <param name="correlation">The correlation.</param>
      internal Calibrator(
        FxCurve fxCurve1, VolatilitySurface surface1,
        FxCurve fxCurve2, VolatilitySurface surface2,
        Func<Dt, double> correlation)
      {
        FxCurve1 = fxCurve1;
        Surface1 = surface1;
        FxCurve2 = fxCurve2;
        Surface2 = surface2;

        var common = CommonCurrency = GetCommonCurrency(
          fxCurve1.Ccy1, fxCurve1.Ccy2, fxCurve2.Ccy1, FxCurve2.Ccy2);

        // Let the common currency be ccy0.
        // We normalize the correlation to be between
        // the rates ccy1/ccy0 and ccy0/ccy2
        // by flipping the sign.
        int sign = 1;
        if (fxCurve1.Ccy2 == common)
        {
          Currency1 = fxCurve1.Ccy1;
        }
        else
        {
          Currency1 = fxCurve1.Ccy2;
          sign *= -1;
        }

        if (fxCurve2.Ccy1 == common)
        {
          Currency2 = fxCurve2.Ccy2;
        }
        else
        {
          Currency2 = fxCurve2.Ccy1;
          sign *= -1;
        }

        Sign = sign;
        Correlation = correlation;
      }

      /// <summary>Gets the common currency</summary>
      private Currency CommonCurrency { get; }

      /// <summary>Gets the currency 1</summary>
      private Currency Currency1 { get; }

      /// <summary>Gets the currency 2</summary>
      private Currency Currency2 { get; }

      /// <summary>Gets the sign applying to the correlation</summary>
      private int Sign { get; }

      /// <summary>Gets the correlation of the normalized FX rate</summary>
      private Func<Dt, double> Correlation { get; }

      /// <summary>Gets the FX curve 1</summary>
      private FxCurve FxCurve1 { get; }

      /// <summary>Gets the FX curve 2</summary>
      private FxCurve FxCurve2 { get; }

      /// <summary>Gets the volatility surface of FX rate 1</summary>
      private VolatilitySurface Surface1 { get; }

      /// <summary>Gets the volatility surface of FX rate 2</summary>
      private VolatilitySurface Surface2 { get; }

      /// <summary>
      /// Fit a surface from the specified tenor point
      /// </summary>
      /// <param name="surface">The volatility surface to fit.</param>
      /// <param name="fromTenorIdx">The tenor index to start fit.</param>
      /// <exception cref="System.NotImplementedException"></exception>
      /// <remarks><para>Derived calibrators implement this to do the work of the fitting.</para>
      /// <para>Called by Fit() and Refit(), it can be assumed that the tenors have been validated.</para>
      /// <para>When the start index <paramref name="fromTenorIdx" /> is 0,
      /// a full fit is requested.  Otherwise a partial fit start from
      /// the given tenor is requested.  The derived class can do either
      /// a full fit a a partial fit as it sees appropriate.</para></remarks>
      public void FitFrom(CalibratedVolatilitySurface surface, int fromTenorIdx)
      {
        // nothing to do
      }

      /// <summary>
      /// Interpolates a volatility at the specified date and strike.
      /// </summary>
      /// <param name="surface">The volatility surface.</param>
      /// <param name="expiry">The expiry date.</param>
      /// <param name="strike">The strike.</param>
      /// <returns>The volatility at the given date and strike.</returns>
      public double Interpolate(
        VolatilitySurface surface,
        Dt expiry, double strike)
      {
        return Interpolate(surface, expiry,
          strike, SmileInputKind.Strike);
      }

      /// <summary>
      /// Interpolates a volatility at the specified date and strike.
      /// </summary>
      /// <param name="surface">The surface.</param>
      /// <param name="expiry">The expiry date</param>
      /// <param name="smileInputValue">The smile input value.</param>
      /// <param name="smileInputKind">Kind of the smile input.</param>
      /// <returns>System.Double.</returns>
      /// <exception cref="System.ArgumentException"></exception>
      public double Interpolate(
        VolatilitySurface surface, Dt expiry,
        double smileInputValue, SmileInputKind smileInputKind)
      {
        var fx1 = FxCurve1.FxRate(expiry, Currency1, CommonCurrency);
        var fx2 = FxCurve2.FxRate(expiry, CommonCurrency, Currency2);

        double logK;
        switch (smileInputKind)
        {
        case SmileInputKind.Strike:
          logK = Log(smileInputValue);
          break;
        case SmileInputKind.Moneyness:
          logK = Log(smileInputValue*fx1*fx2);
          break;
        case SmileInputKind.LogMoneyness:
          logK = smileInputValue + Log(fx1*fx2);
          break;
        default:
          throw new ArgumentException(
            $"Unknown smile input kind {smileInputKind}");
        }

        double logK1, logK2;
        MapStrike(logK, Log(fx1), Log(fx2), out logK1, out logK2);
        var sigma1 = Surface1.Interpolate(expiry, Exp(logK1));
        var sigma2 = Surface2.Interpolate(expiry, Exp(logK2));
        return CombineVolatility(sigma1, sigma2, Sign*Correlation(expiry));
      }

      /// <summary>
      /// Gets the component volatility surfaces, used by the sensitivity
      ///  codes to bump the tenors on the underlying surfaces.
      /// </summary>
      /// <returns>IEnumerable{CalibratedVolatilitySurface}</returns>
      IEnumerable<IVolatilitySurface> IVolatilitySurfaceProvider.GetVolatilitySurfaces()
      {
        yield return Surface1;
        yield return Surface2;
      }
    }

    #endregion
  }

}
