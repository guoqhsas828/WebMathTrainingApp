// 
//  -2013. All rights reserved.
// 
// TBD: Discrete dividends and continuous dividend should possibly be passed through to low level model. Currently implied from StockCurve. RTD Mar'13

using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.StockOption">Stock Option</see>
  ///   using a <see cref="BaseEntity.Toolkit.Models.BlackScholes">Black-Scholes model</see>.</para>
  /// </summary>
  /// <remarks>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Products.StockOption" />
  /// 
  ///   <para><h2>Pricing</h2></para>
  ///   <inheritdoc cref="BlackScholesPricerBase" />
  /// 
  ///   <para><h2>Pricing Details</h2></para>
  ///   <para>The option may be priced by specifying either the spot price and the dividend or the forward price curve.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.StockOption"/>
  /// <seealso cref="BlackScholesPricerBase"/>
  [Serializable]
  public class StockOptionPricer : BlackScholesPricerBase, IVolatilitySurfaceProvider
  {

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="stockPrice">Underlying stock price</param>
    /// <param name="rfr">Risk free rate</param>
    /// <param name="dividend">Dividend rate (continuous comp, eg. 0.05)</param>
    /// <param name="volatility">Volatility</param>
    public StockOptionPricer(
      StockOption option, Dt asOf, Dt settle, double stockPrice,
      double rfr, double dividend, double volatility
      )
      : base( option, asOf, settle)
    {
      DiscountCurve = new DiscountCurve(asOf).SetRelativeTimeRate(rfr);
      StockCurve = new StockCurve(asOf, stockPrice, DiscountCurve, dividend, option.Stock);
      StockCurve.ImpliedYieldCurve.SetRelativeTimeRate(dividend);
      VolatilitySurface = CalibratedVolatilitySurface.FromFlatVolatility(asOf, volatility);
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    /// <para>The stock price and dividend rate are implied from the stock forward curve.</para>
    /// </remarks>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="underlyingCurve">Term structure of stock forward prices</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="volSurface">Volatility Surface</param>
    public StockOptionPricer(
      StockOption option, Dt asOf, Dt settle, StockCurve underlyingCurve,
      DiscountCurve discountCurve, IVolatilitySurface volSurface)
      : base(option, asOf, settle)
    {
      StockCurve = underlyingCurve;
      DiscountCurve = (discountCurve ?? underlyingCurve.DiscountCurve);
      VolatilitySurface = volSurface;
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
      if (StockCurve == null)
        InvalidValue.AddError(errors, this, "StockCurve", "Missing StockCurve - must be specified");
      if (DiscountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", "Missing DiscountCurve - must be specified");
      // Allow empty VolatilitySurface curve as implied vol calculators do not need it
      return;
    }

    #endregion Utility Methods

    #region Properties

    /// <summary>
    /// Time to expiration date in years
    /// </summary>
    public double Time
    {
      get { return Dt.RelativeTime(AsOf, StockOption.Expiration).Value; }
    }

    /// <summary>
    /// Time to expiration date in days
    /// </summary>
    public double Days
    {
      get { return Dt.RelativeTime(AsOf, StockOption.Expiration).Days; }
    }

    /// <summary>
    /// Current quoted price of underlying stock from <see cref="StockCurve"/>
    /// </summary>
    public double UnderlyingPrice
    {
      get { return StockCurve.SpotPrice; }
    }

    /// <summary>
    /// Flat dividend yield to option expiration
    /// </summary>
    /// <remarks>
    /// <para>Continuously compounded dividend yield to option expiration date.</para>
    /// </remarks>
    public double Dividend
    {
      get { return StockCurve.ImpliedYieldCurve == null ? 0 : RateCalc.Rate(StockCurve.ImpliedYieldCurve, Settle, StockOption.Expiration); }
    }


    /// <summary>
    /// Discrete dividend schedule
    /// </summary>
    public IReadOnlyList<Stock.Dividend> DividendSchedule
    {
      get { return StockCurve.Stock.DeclaredDividends; }
    }

    /// <summary>
    /// Forward curve of underlying asset
    /// </summary>
    public StockCurve StockCurve { get; private set; }

    /// <summary>
    /// Flat risk free rate to option expiration
    /// </summary>
    /// <remarks>
    /// <para>Continuously compounded risk free rate to option expiration date.</para>
    /// </remarks>
    public double Rfr
    {
      get { return RateCalc.Rate(DiscountCurve, AsOf, StockOption.Expiration); }
    }

    /// <summary>
    /// Discount Curve
    /// </summary>
    public DiscountCurve DiscountCurve { get; set; }

    /// <summary>
    /// Flat volatility to option expiration
    /// </summary>
    /// <remarks>
    /// <para>Flat volatility to option expiration date.</para>
    /// </remarks>
    public double Volatility
    {
      get { return VolatilitySurface.Interpolate(StockOption.Expiration, AtmForwardPrice(), StockOption.Strike); }
    }

    /// <summary>
    /// Volatility Surface
    /// </summary>
    public IVolatilitySurface VolatilitySurface { get; set; }

    /// <summary>
    /// Stock Option product
    /// </summary>
    public StockOption StockOption
    {
      get { return (StockOption)Product; }
    }

    #region Black-Scholes Model Inputs

    /// <summary>Model time</summary>
    protected override double BlackScholesTime { get { return Time; } }
    /// <summary>Model underlying price for fair value model sensitivities</summary>
    protected override double BlackScholesUnderlyingQuotedPrice { get { return UnderlyingPrice; } }
    /// <summary>Model discount rate</summary>
    protected override double BlackScholesRfr { get { return Rfr; } }
    /// <summary>Model dividend</summary>
    protected override double BlackScholesDividend { get { return Dividend; } }
    /// <summary>Model discrete dividends</summary>
    protected override DividendSchedule BlackScholesDivs { get { return StockCurve.Dividends; } }
    /// <summary>Model volatility</summary>
    protected override double BlackScholesVolatility { get { return Volatility; } }

    #endregion Black-Scholes Model Inputs

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

    #endregion Properties

    #region Methods

    /// <summary>
    /// Calculate ATM forward price for the underlying at the option expiration date
    /// </summary>
    /// <remarks>
    /// <para>The ATM forward price is implied from the spot price of the underlying, any dividends,
    /// and the funding rate.</para>
    /// </remarks>
    /// <returns>ATM forward price</returns>
    public double AtmForwardPrice()
    {
      return StockCurve.Interpolate(StockOption.Expiration);
    }

    #endregion Methods

    #region IVolatilitySurfaceProvider Methods

    System.Collections.Generic.IEnumerable<IVolatilitySurface> IVolatilitySurfaceProvider.GetVolatilitySurfaces()
    {
      if (VolatilitySurface != null) yield return VolatilitySurface;
    }

    #endregion IVolatilitySurfaceProvider Methods
  }
}
