//
// VolatilitySurface.cs
//  -2014. All rights reserved.
//
using System;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves.Volatilities
{
  /// <summary>
  /// Generalized volatility surface
  /// </summary>
  /// <remarks>
  ///   <para>The generalized volatility surface provides a powerful and unified cross-asset volatility
  ///   framework.</para>
  ///   <para>Volatility surfaces can be specified directly or calibrated
  ///   to market data using <see cref="IVolatilitySurfaceCalibrator">Volatility calibrators</see></para>
  ///   <para>Calibrated volatility surfaces derive from <see cref="CalibratedVolatilitySurface"/></para>
  ///   <para>Examples of volatility calibrators include:</para>
  ///   <list type="bullet">
  ///     <item><description><see cref="RateVolatilityCapSabrCalibrator">Cap-floor SABR volatility calibrator</see></description></item>
  ///     <item><description><see cref="FxOptionVannaVolgaCalibrator">Vanna Volga FX option volatility calibrator</see></description></item>
  ///     <item><description><see cref="SwaptionVolatilityBaseCalibrator">Swaption volatility calibrator</see></description></item>
  ///     <item><description><see cref="GenericBlackScholesCalibrator">Vanilla option Black-Scholes volatility calibrator</see></description></item>
  ///   </list>
  /// </remarks>
  [Serializable]
  public class VolatilitySurface : BaseEntityObject, IVolatilitySurface
  {
    #region Instance Methods

    /// <summary>
    /// constructor for the volatility surface
    /// </summary>
    /// <param name="asOf">As of date.</param>
    /// <param name="interpolator">The interpolator.</param>
    /// <param name="smileInputKind">The smile input kind (strike or moneyness).</param>
    protected internal VolatilitySurface(Dt asOf,
      IVolatilitySurfaceInterpolator interpolator,
      SmileInputKind smileInputKind)
    {
      AsOf = asOf;
      Interpolator = interpolator;
      SmileInputKind = smileInputKind;
    }

    /// <summary>
    /// Interpolates the volatility at the specified date and input level. 
    ///  The input kind is specified by the property <c>SmileInputKind</c>
    ///  and it can be strike, moneyness or log-moneyness.
    /// </summary>
    /// <param name="date">The date.</param>-
    /// <param name="input">The input level.</param>
    /// <returns>The interpolated volatility</returns>
    public double Interpolate(Dt date, double input)
    {
      if (Interpolator == null)
      {
        throw new Exception("No interpolator found!");
      }
      return Interpolator.Interpolate(this, date, input);
    }

    /// <summary>
    /// Interpolates the volatility at the specified date and input (strike, moneyness or log-moneyness) level.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="input">The input level.</param>
    /// <param name="kind">The input kind (strike, moneyness, or log-moneyness).</param>
    /// <returns>System.Double.</returns>
    /// <exception cref="System.Exception"></exception>
    /// <exception cref="ToolkitException"></exception>
    internal double Interpolate(Dt date, double input, SmileInputKind kind)
    {
      if (Interpolator == null)
      {
        throw new Exception("No interpolator found!");
      }
      if (kind == SmileInputKind)
        return Interpolate(date, input);
      var surfaceInterp = Interpolator as IExtendedVolatilitySurfaceInterpolator;
      if (surfaceInterp != null)
        return surfaceInterp.Interpolate(this, date, input, kind);
      throw new ToolkitException(String.Format(
        "Unable to interpolate with input kind {0}", kind));
    }

    /// <summary>
    /// Return a new object with a deep copy of all the tenor data.
    /// </summary>
    /// <returns>Cloned object</returns>
    /// <remarks>
    /// This method does not clone the calibrator and interpolator.
    /// The user must make sure the later is update.
    /// </remarks>
    public override object Clone()
    {
      var obj = (CalibratedVolatilitySurface) base.Clone();
      // Following calibrated curve convention,
      // Calibrator and interpolator are NOT cloned.
      return obj;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets as of date.
    /// </summary>
    /// <value>As of date.</value>
    public Dt AsOf { get; }

    /// <summary>
    /// Gets the interpolator.
    /// </summary>
    /// <value>The interpolator.</value>
    public IVolatilitySurfaceInterpolator Interpolator { get; set; }

    /// <summary>
    /// Gets the kind of the smile input.
    /// </summary>
    /// <remarks></remarks>
    public SmileInputKind SmileInputKind { get; }

    /// <summary>
    /// Gets or sets the name of this object.
    /// </summary>
    /// <value>The name.</value>
    public string Name { get; set; }

    #endregion
  }
}
