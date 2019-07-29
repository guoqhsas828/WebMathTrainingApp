//
// RateVolatilityCube.cs
//   2005-2012. All rights reserved.
//
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  /// Interface for a generalised volatility surface
  /// </summary>
  /// <remarks>
  ///   <para>The generalised volatilty surface provides a powerful and unified cross-asset volatility
  ///   framework.</para>
  ///   <para>Volatility surfaces can be specified directly or calibrated
  ///   to market data using <see cref="IVolatilitySurfaceCalibrator">Volatility calibrators</see></para>
  ///   <para>Calibrated volatility surfaces derive from <see cref="CalibratedVolatilitySurface"/></para>
  ///   <para>Examples of volatility calibrators include:</para>
  ///   <list type="bullet">
  ///     <item><description><see cref="Toolkit.Calibrators.RateVolatilityCapSabrCalibrator">Cap-floor SABR volatility calibrator</see></description></item>
  ///     <item><description><see cref="Toolkit.Calibrators.Volatilities.FxOptionVannaVolgaCalibrator">Vanna Volga fx option volatility calibrator</see></description></item>
  ///     <item><description><see cref="Toolkit.Calibrators.SwaptionVolatilityBaseCalibrator">Swaption volatility calibrator</see></description></item>
  ///     <item><description><see cref="Toolkit.Calibrators.Volatilities.GenericBlackScholesCalibrator">Vanilla option Black-scholes volatility calibrator</see></description></item>
  ///   </list>
  /// </remarks>
  public interface IVolatilitySurface
  {
    /// <summary>
    /// Volatility surface
    /// </summary>
    /// <param name="maturity">The maturity.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>Volatility</returns>
    double Interpolate(Dt maturity, double strike);
  }
}
