// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.FxForwardOption">Fx Forward Option</see>
  ///   using a <see cref="BaseEntity.Toolkit.Models.BlackScholes">Black-Scholes model</see>.</para>
  /// </summary>
  /// <remarks>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Products.FxForward" />
  /// 
  ///   <para><h2>Pricing</h2></para>
  ///   <inheritdoc cref="BlackScholesPricerBase" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.FxForwardOption">Fx Forward Product</seealso>
  /// <seealso cref="BlackScholesPricerBase">Fx Curve</seealso>
  [Serializable]
  public class FxForwardOptionBlackPricer : BlackScholesPricerBase
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="valuationCcy">Currency to value trade in (or none to use fxCurve1.Cc1)</param>
    /// <param name="discountCurve">Discount curve for valuation currency (or null to imply from curves used to calibrate fx curves)</param>
    /// <param name="fxCurve1">First fx curve</param>
    /// <param name="fxCurve2">Second fx curve (if triangulation is required)</param>
    /// <param name="volSurface">Volatility surface</param>
    /// <param name="notional">Notional of trade</param>
    public FxForwardOptionBlackPricer(
      FxForwardOption option, Dt asOf, Dt settle,
      Currency valuationCcy, DiscountCurve discountCurve, FxCurve fxCurve1, FxCurve fxCurve2,
      CalibratedVolatilitySurface volSurface, double notional
      )
      : base(option, asOf, settle)
    {
      _valuationCurrency = (valuationCcy != Currency.None) ? valuationCcy : fxCurve1.Ccy1;
      DiscountCurve = discountCurve ?? FxUtil.DiscountCurve(valuationCcy, fxCurve1, fxCurve2);
      FxCurve1 = fxCurve1;
      FxCurve2 = fxCurve2;
      VolatilitySurface = volSurface;
      Notional = notional;
    }

    #endregion Constructors

    #region Utility Methods

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;
      base.Validate(errors);
      if( DiscountCurve == null )
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Must be specified or able to be implied from currency and fx calibration"));
      if( !FxUtil.CanGetFxRate(FxForwardOption.Ccy1, FxForwardOption.Ccy2, FxCurve1, FxCurve2) )
        InvalidValue.AddError(errors, this, "FxCurve1", String.Format("Unable to calculate [{0}]/[{1}] fx rate with curves given",
          FxForwardOption.Ccy1, FxForwardOption.Ccy2));
      if( VolatilitySurface == null && Volatility < 0.0)
        InvalidValue.AddError(errors, this, "Volatility", String.Format("Invalid volatility {0}, Must be >= 0", Volatility));
    }

    /// <summary>
    /// Clear cached calculation results
    /// </summary>
    public override void Reset()
    {
      base.Reset();
      _vol = _underlyingPrice = null;
    }
    
    #endregion Utility Methods

    #region Properties

    /// <summary>
    /// Time to expiration in years
    /// </summary>
    /// <remarks>
    /// <para>Setting time updates the expiration date.</para>
    /// </remarks>
    public double Time
    {
      get { return Dt.RelativeTime(AsOf, FxForwardOption.Expiration).Value; }
    }

    /// <summary>
    /// Time to expiration in days
    /// </summary>
    /// <remarks>
    /// <para>Setting Days updates the expiration date.</para>
    /// </remarks>
    public double Days
    {
      get { return Dt.RelativeTime(AsOf, FxForwardOption.Expiration).Days; }
    }

    /// <summary>
    /// Underlying fx forward rate
    /// </summary>
    public double UnderlyingPrice
    {
      get
      {
        return _underlyingPrice.HasValue ? _underlyingPrice.Value :
          FxUtil.ForwardFxRate(FxForwardOption.FxForward.ValuationDate, FxForwardOption.Ccy1, FxForwardOption.Ccy2, FxCurve2, FxCurve1);
      }
      set { _underlyingPrice = value; }
    }
     
    /// <summary>
    /// Risk free rate (5 percent = 0.05)
    /// </summary>
    /// <remarks>
    /// <para>Returns risk free rate interpolated from DiscountCurve.</para>
    /// </remarks>
    public double Rfr
    {
      get { return RateCalc.Rate(DiscountCurve, FxForwardOption.Expiration); }
    }

    /// <summary>
    /// Discount Curve for valuation
    /// </summary>
    public DiscountCurve DiscountCurve { get; set; }

    /// <summary>
    /// Currency of the Pv calculation
    /// </summary>
    override public Currency ValuationCurrency { get { return _valuationCurrency; } }

    /// <summary>
    /// First Fx curve
    /// </summary>
    public FxCurve FxCurve1 { get; set; }

    /// <summary>
    /// Second Fx curve (may be needed for triangulation)
    /// </summary>
    public FxCurve FxCurve2 { get; set; }

    /// <summary>
    /// Volatility (5 percent = 0.05)
    /// </summary>
    /// <remarks>
    /// <para>Returns specified flat volatility or if none set, interpolates from VolatilitySurface.</para>
    /// </remarks>
    public double Volatility
    {
      get { return (_vol.HasValue) ? _vol.Value : VolatilitySurface.Interpolate(FxForwardOption.Expiration, UnderlyingPrice, FxForwardOption.Strike); }
      set { _vol = value; }
    }

    /// <summary>
    /// Volatility Surface
    /// </summary>
    public CalibratedVolatilitySurface VolatilitySurface { get; set; }

    /// <summary>
    /// Fx Forward Option product
    /// </summary>
    public FxForwardOption FxForwardOption
    {
      get { return (FxForwardOption)Product; }
    }

    #region Black-Scholes Model Inputs

    /// <summary>Model time</summary>
    protected override double BlackScholesTime { get { return Time; } }
    /// <summary>Model underlying price for fair value and model sensitivities</summary>
    protected override double BlackScholesUnderlyingQuotedPrice { get { return UnderlyingPrice; } }
    /// <summary>Model underlying implied price for option pv</summary>
    protected override double BlackScholesUnderlyingModelPrice { get { return UnderlyingPrice; } }
    /// <summary>Model discount rate</summary>
    protected override double BlackScholesRfr { get { return Rfr; } }
    /// <summary>Model dividend rate</summary>
    protected override double BlackScholesDividend { get { return Rfr; } }
    /// <summary>Model volatility</summary>
    protected override double BlackScholesVolatility { get { return Volatility; } }

    #endregion Black-Scholes Model Inputs

    #endregion Properties

    #region IPricer

    ///<summary>
    /// Pricer that will be used to price any additional (e.g. upfront) payment
    /// associated with the pricer.
    ///</summary>
    /// <remarks>
    /// It is the responsibility of derived classes to build this
    /// pricer with the appropriate discount curve for the payment's currency
    /// and to decide whether it can cache the pricer between uses.
    /// </remarks>
    ///<exception cref="NotImplementedException"></exception>
    public override IPricer PaymentPricer
    {
      get
      {
        if (Payment != null)
        {
          if (paymentPricer_ == null)
            paymentPricer_ = BuildPaymentPricer(Payment, DiscountCurve);
        }
        return paymentPricer_;
      }
    }

    #endregion IPricer

    #region Data

    private double? _underlyingPrice;
    private double? _vol;
    private readonly Currency _valuationCurrency;

    #endregion Data
  }
}
