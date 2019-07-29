/*
 *  -2013. All rights reserved.
 */
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Calibrators.Volatilities.ForeignExchanges
{
  #region FxVolatilityTerm
  /// <summary>
  ///  FX rates specific volatility terms.
  /// </summary>
  [Serializable]
  public class FxVolatilityUnderlying : IVolatilityUnderlying
  {
    /// <summary>
    /// The quote term
    /// </summary>
    private readonly FxVolatilityQuoteTerm _quoteTerm;

    /// <summary>
    /// The foreign rate curve
    /// </summary>
    private readonly DiscountCurve _ccy1RateCurve;

    /// <summary>
    /// The FX curve
    /// </summary>
    private readonly ForeignDiscountCalculator _fxCalculator;

    /// <summary>
    /// Initializes a new instance of the <see cref="FxVolatilityQuoteTerm" /> class.
    /// </summary>
    /// <param name="quoteTerm">The quote term.</param>
    /// <param name="foreignRateCurve">The foreign rate curve.</param>
    /// <param name="domesticRateCurve">The domestic rate curve.</param>
    /// <param name="fxCurve">The fx curve.</param>
    /// <exception cref="System.ArgumentNullException">fxCurve</exception>
    public FxVolatilityUnderlying(
      FxVolatilityQuoteTerm quoteTerm,
      DiscountCurve foreignRateCurve,
      DiscountCurve domesticRateCurve,
      FxCurve fxCurve)
    {
      if (fxCurve == null)
        throw new ArgumentNullException("fxCurve");
      _quoteTerm = quoteTerm;
      _ccy1RateCurve = foreignRateCurve ?? fxCurve.Ccy1DiscountCurve;
      _fxCalculator = new ForeignDiscountCalculator(
        domesticRateCurve ?? fxCurve.Ccy2DiscountCurve, fxCurve);
    }

    #region Properties

    /// <summary>
    /// Gets the quote term.
    /// </summary>
    /// <value>The quote term.</value>
    public FxVolatilityQuoteTerm QuoteTerm
    {
      get { return _quoteTerm; }
    }

    /// <summary>
    /// Gets the domestic rate curve.
    /// </summary>
    /// <value>The domestic rate curve.</value>
    public DiscountCurve DomesticRateCurve
    {
      get { return _fxCalculator.DomesticDiscountCurve; }
    }

    /// <summary>
    /// Gets the foreign rate curve.
    /// </summary>
    /// <value>The foreign rate curve.</value>
    public DiscountCurve ForeignRateCurve
    {
      get { return _ccy1RateCurve; }
    }

    /// <summary>
    /// Gets the fx curve.
    /// </summary>
    /// <value>The fx curve.</value>
    public FxCurve FxCurve
    {
      get { return _fxCalculator.FxCurve; }
    }

    #endregion

    #region Nested type: ForeignDiscountCalculator

    /// <summary>
    /// Class ForeignDiscountInterpolator
    /// </summary>
    [Serializable]
    private class ForeignDiscountCalculator : IFactorCurve
    {
      /// <summary>
      /// The fx curve
      /// </summary>
      public readonly FxCurve FxCurve;

      /// <summary>
      /// The domestic discount curve
      /// </summary>
      public readonly DiscountCurve DomesticDiscountCurve;

      /// <summary>
      /// Initializes a new instance of the <see cref="ForeignDiscountCalculator"/> class.
      /// </summary>
      /// <param name="discountCurve">The discount curve.</param>
      /// <param name="fxCurve">The fx curve.</param>
      public ForeignDiscountCalculator(
        DiscountCurve discountCurve, FxCurve fxCurve)
      {
        DomesticDiscountCurve = discountCurve;
        FxCurve = fxCurve;
      }

      /// <summary>
      /// Calculates the effective foreign discount factor.
      /// </summary>
      /// <param name="begin">The begin date.</param>
      /// <param name="end">The end date.</param>
      /// <returns>System.Double.</returns>
      public double Interpolate(Dt begin, Dt end)
      {
        return GetFxRate(end) / GetFxRate(begin) *
          DomesticDiscountCurve.DiscountFactor(begin, end);
      }

      private double GetFxRate(Dt date)
      {
        return date <= FxCurve.SpotDate
          ? FxCurve.SpotRate
          : FxCurve.FxRate(date);
      }
    }

    #endregion

    #region IVolatilityUnderlying Members

    IFactorCurve IVolatilityUnderlying.Curve1
    {
      get { return _fxCalculator; }
    }

    IFactorCurve IVolatilityUnderlying.Curve2
    {
      get { return _ccy1RateCurve; }
    }

    ISpotCurve IVolatilityUnderlying.Spot
    {
      get { return (ConstantSpotCurve)FxCurve.SpotRate; }
    }

    ISpotCurve IVolatilityUnderlying.Deflator
    {
      get { return ConstantSpotCurve.One; }
    }

    #endregion
  }
  #endregion
}
