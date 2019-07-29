/*
 * FxCurveSet.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Curves
{
  /// <summary>
  /// A class holding a set of curve data relevant to FX rate calculation,
  /// including the domestic and foreign discount curves, the forward FX
  /// rate curve (optional), the cross currency basis curve (optional),
  /// as well as the current spot rate.
  /// </summary>
  [Serializable]
  public class FxCurveSet : BaseEntityObject
  {
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="FxCurveSet"/> class.
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="fxRate">The FX rate object.</param>
    /// <param name="fxFactorCurve">The FX factor curve (can be null if 
    /// <paramref name="foreignCurve"/> is non-null).</param>
    /// <param name="domesticCurve">The domestic curve.</param>
    /// <param name="foreignCurve">The foreign curve (can be null if 
    /// <paramref name="fxFactorCurve"/> is non-null).</param>
    /// <param name="basisCurve">The basis curve (can be null).</param>
    /// <param name="ignoreSpotDays">The flag to decide whether to ignore 
    /// the previous spot days behavior.This is just for the backward-compatible only.
    /// We need to revisit this flag later on.</param>
    public FxCurveSet(
      Dt asOf,
      FxRate fxRate,
      FxCurve fxFactorCurve,
      DiscountCurve domesticCurve,
      DiscountCurve foreignCurve,
      DiscountCurve basisCurve,
      bool ignoreSpotDays = false)
    {
      //- <para>Sanity checks</para>
      //- Notes: since all the properties are read-only,
      //-   it is better to check the validity in the constructor, instead of
      //-   in the Validate() function.
      if (asOf.IsEmpty())
      {
        throw new ToolkitException("As-of date cannot be empty.");
      }
      if (fxRate == null)
      {
        throw new ToolkitException("FX spot rate cannot be null.");
      }
      if (domesticCurve == null)
      {
        throw new ToolkitException("The domestic discount curve cannot be null.");
      }
      if (fxFactorCurve == null && foreignCurve == null)
      {
        throw new ToolkitException(
          "The foreign discount curve and FX factor curve cannot be both null.");
      }
      asOf_ = asOf;
      fxRate_ = fxRate;
      fxFactorCurve_ = fxFactorCurve;
      domesticDiscount_ = domesticCurve;
      foreignDiscount_ = foreignCurve;
      basisCurve_ = basisCurve;
      _spotDays = ignoreSpotDays ? 0 : fxRate.SettleDays;
    }


    #endregion Constructors

    #region Methods
    /// <summary>
    ///  Calculate the forwards FX rate on the specified date.
    /// </summary>
    /// <param name="date">The forward date.</param>
    /// <returns>The forward FX rate</returns>
    /// <remarks>
    /// <para>Let <m>t</m> be the spot date, <m>S_t</m> the spot date, <m>T</m> the forward date,
    /// <m>F(t,T)</m> the forward FX rate.</para>
    /// <para>If the FX factor curve is null, the forward FX rate is calculated by<math>
    /// F(t,T) = S_t\, \frac{P^f(t,T)}{P^d(t,T)}\, b(t, T)
    /// </math>where
    ///  <m>P^f(t,T)</m> is the foreign discount factor,
    ///  <m>P^d(t,T)</m> is the domestic discount factor, and
    ///  <m>b(t,T)</m> is the basis adjustment.
    /// </para>
    /// <para>If the FX factor curve presents, then the following formula is used<math>
    /// F(t,T) = S_t\, f(t,T)\, b(t, T)
    /// </math>where
    ///  <m>f(t,T)</m> is the forward FX factor.
    /// </para>
    /// </remarks>
    public double ForwardFxRate(Dt date)
    {
      return SpotRate*ForwardFactor(date);
    }
    /// <summary>
    ///  Calculate the forwards FX factor on the specified date, basis adjusted.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The forward FX factor</returns>
    /// <remarks>
    /// <para>Let <m>t</m> be the spot date, <m>S_t</m> the spot date, <m>T</m> the forward date,
    /// <m>F(t,T)</m> the forward FX rate.  The forward FX factor is defined by<math>
    ///   F^{*}(t, T) \equiv \frac{F(t,T)}{S_t}
    /// </math>which, depending on whether the FX factor curve presents, is calculated by either<math>
    /// F^{*}(t,T) = \frac{P^f(t,T)}{P^d(t,T)}\, b(t, T)
    /// </math>or<math>
    /// F^{*}(t,T) = f(t,T)\, b(t, T)
    /// </math>where
    ///  <m>P^f(t,T)</m> is the foreign discount factor,
    ///  <m>P^d(t,T)</m> is the domestic discount factor,
    ///  <m>b(t,T)</m> is the basis adjustment, and
    ///  <m>f(t,T)</m> is the forward FX factor.
    /// </para>
    /// </remarks>
    public double ForwardFactor(Dt date)
    {
      Dt asOf = asOf_;
      if (fxFactorCurve_ == null)
      {
        return (Df(ForeignDiscountCurve, asOf, date)
          / Df(DomesticDiscountCurve, asOf, date)) * Df(BasisCurve, asOf, date);
      }
      return Df(FxFactorCurve, asOf, date) * Df(BasisCurve, asOf, date);
    }
    /// <summary>
    ///  Gets the effectives domestic discount factor.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The effective domestic discount factor.</returns>
    /// <remarks>
    /// This is simply <m>P^d(t,T)</m>, the discount factor implied by the domestic discount curve.
    /// </remarks>
    public double EffectiveDomesticDiscountFactor(Dt date)
    {
      return Df(DomesticDiscountCurve, asOf_, date);
    }
    /// <summary>
    /// Gets the effectives foreign discount factor, basis adjusted.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The effective foreign discount factor.</returns>
    /// <remarks>
    /// <para>This is actually not the discount factor based on the foreign discount curve,
    /// but the foreign discount factor as implied from the forward FX curve.<math>
    /// \bar{P}^f(t,T) \equiv \frac{F(t,T)}{S_t}\,P^d(t,T)
    /// </math></para>
    /// </remarks>
    public double EffectiveForeignDiscountFactor(Dt date)
    {
      Dt asOf = asOf_;
      if (fxFactorCurve_ == null)
      {
        return Df(ForeignDiscountCurve, asOf, date) * Df(BasisCurve, asOf, date);
      }
      return FxFactor(FxFactorCurve, asOf, date)*Df(BasisCurve, asOf, date)
        *Df(DomesticDiscountCurve, asOf, date);
    }
    /// <summary>
    ///   A class to hold some variables interested by the FxOption models.
    /// </summary>
    internal struct RatesSigmaTime
    {
      internal double Rd, Rf, Sigma, T;
    }
    /// <summary>
    ///   Calculates the time, domestic and foreign rates, and optionally the
    ///   Black volatility, in a consistent way.
    /// </summary>
    internal RatesSigmaTime GetRatesSigmaTime(Dt asOf, Dt expiry, Curve volCurve)
    {
      var timeToExpiration = (expiry - asOf) / 365.0;
      var expirySpot = _spotDays <= 0 ? expiry :
        FxUtil.FxSpotDate(expiry, _spotDays, FxRate.FromCcyCalendar, FxRate.ToCcyCalendar);
      var domesticDf = EffectiveDomesticDiscountFactor(expirySpot);
      var foreignDf = EffectiveForeignDiscountFactor(expirySpot);
      var rf = -Math.Log(foreignDf) / timeToExpiration;
      var rd = -Math.Log(domesticDf) / timeToExpiration;
      double vol = volCurve == null ? 0.0
        : volCurve.CalculateAverageVolatility(asOf, expiry);
      return new RatesSigmaTime { Rd = rd, Rf = rf, Sigma = vol, T = timeToExpiration };
    }
    /// <summary>
    ///  A simple utility function to calculate the discount factor with possibly null curve.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <param name="start">The start date.</param>
    /// <param name="end">The end date.</param>
    /// <returns>The discount factor.</returns>
    private double Df(Curve curve, Dt start, Dt end)
    {
      var fx = fxFactorCurve_;
      if (fx != null && fx.SpotDays != 0 && start < fx.SpotDate)
        start = fx.SpotDate;
      return curve != null ? curve.Interpolate(start, end) : 1.0;
    }

    internal static double FxFactor(FxCurve fx, Dt start, Dt end)
    {
      return fx == null || end < fx.SpotDate ? 1.0
        : fx.Interpolate(start <= fx.SpotDate ? fx.SpotDate : start, end);
    }
    #endregion Methods

    #region Properties
    /// <summary>
    /// FX rate object. 
    /// </summary>
    public FxRate FxRate
    {
      get { return fxRate_; }
    }

    /// <summary>
    /// FX spot rate. 
    /// </summary>
    public double SpotRate
    {
      get { return fxRate_.Rate; }
    }
    /// <summary>
    /// The Cross Currency Basis Curve
    /// </summary>
    public DiscountCurve BasisCurve
    {
      get { return basisCurve_; }
    }
    /// <summary>
    /// Gets the domestic discount curve.
    /// </summary>
    public DiscountCurve DomesticDiscountCurve
    {
      get { return domesticDiscount_; }
    }
    /// <summary>
    /// Gets the foreign discount curve.
    /// </summary>
    public DiscountCurve ForeignDiscountCurve
    {
      get { return foreignDiscount_; }
    }
    /// <summary>
    /// Gets the foreign discount curve.
    /// </summary>
    public FxCurve FxFactorCurve
    {
      get { return fxFactorCurve_; }
    }

    #endregion 

    #region Data
    private Dt asOf_;
    private readonly FxRate fxRate_;
    private readonly DiscountCurve basisCurve_;
    private readonly DiscountCurve foreignDiscount_;
    private readonly DiscountCurve domesticDiscount_;
    private readonly FxCurve fxFactorCurve_;
    private readonly int _spotDays;

    #endregion Data
  }
}
