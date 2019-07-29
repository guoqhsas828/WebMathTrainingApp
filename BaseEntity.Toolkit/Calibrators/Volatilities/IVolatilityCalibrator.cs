//
// VolatilityCalibrator.cs
//  -2014. All rights reserved.
//

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  /// Interface for a generalised volatility calibrator
  /// </summary>
  /// <remarks>
  ///   <para>The generalised volatilty surface provides a powerful and unified cross-asset volatility
  ///   framework.</para>
  ///   <para><see cref="CalibratedVolatilitySurface">Volatility surfaces</see> are calibrated to market data
  ///   using volatility calibrators.</para>
  ///   <para>Examples of volatility calibrators include:</para>
  ///   <list type="bullet">
  ///     <item><description><see cref="Toolkit.Calibrators.RateVolatilityCapSabrCalibrator">Cap-floor SABR volatility calibrator</see></description></item>
  ///     <item><description><see cref="Toolkit.Calibrators.Volatilities.FxOptionVannaVolgaCalibrator">Vanna Volga fx option volatility calibrator</see></description></item>
  ///     <item><description><see cref="Toolkit.Calibrators.SwaptionVolatilityBaseCalibrator">Swaption volatility calibrator</see></description></item>
  ///     <item><description><see cref="Toolkit.Calibrators.Volatilities.GenericBlackScholesCalibrator">Vanilla option Black-scholes volatility calibrator</see></description></item>
  ///   </list>
  /// </remarks>
  public interface IVolatilitySurfaceCalibrator
  {
    /// <summary>
    /// Fit a surface from the specified tenor point
    /// </summary>
    /// <remarks>
    /// 	<para>Derived calibrators implement this to do the work of the fitting.</para>
    /// 	<para>Called by Fit() and Refit(), it can be assumed that the tenors have been validated.</para>
    ///   <para>When the start index <paramref name="fromTenorIdx"/> is 0,
    ///   a full fit is requested.  Otherwise a partial fit start from
    ///   the given tenor is requested.  The derived class can do either
    ///   a full fit a a partial fit as it sees appropriate.</para>
    /// </remarks>
    /// <param name="surface">The volatility surface to fit.</param>
    /// <param name="fromTenorIdx">The tenor index to start fit.</param>
    void FitFrom(CalibratedVolatilitySurface surface, int fromTenorIdx);
  }
}
