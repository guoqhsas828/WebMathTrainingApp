// 
//  -2013. All rights reserved.
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
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.CommodityOption">Commodity Option</see>
  ///   using a <see cref="BaseEntity.Toolkit.Models.BlackScholes">Black-Scholes model</see>.</para>
  /// </summary>
  /// <remarks>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Products.CommodityOption" />
  /// 
  ///   <para><h2>Pricing</h2></para>
  ///   <inheritdoc cref="BlackScholesPricerBase" />
  /// 
  ///   <para><h2>Commodity Option Pricing Details</h2></para>
  ///   <para>The option may be priced by specifying either the spot price and the lease rate or
  ///   the forward price curve.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.CommodityOption">Commodity Swap Leg</seealso>
  /// <seealso cref="BlackScholesPricerBase">Commodity Swap Leg</seealso>
  [Serializable]
  public class CommodityOptionPricer : BlackScholesPricerBase, IVolatilitySurfaceProvider
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="underlyingPrice">Underlying commodity price</param>
    /// <param name="leaseRate">Lease rate (continuous comp, eg. 0.05)</param>
    /// <param name="rfr">Risk free rate</param>
    /// <param name="volatility">Volatility (eg. 0.2)</param>
    /// <param name="notional">Notional of contract</param>
    public CommodityOptionPricer(
      CommodityOption option, Dt asOf, Dt settle, double underlyingPrice,
      double leaseRate, double rfr, double volatility, double notional
      )
      : base(option, asOf, settle)
    {
      Notional = notional;
      DiscountCurve = new DiscountCurve(asOf).SetRelativeTimeRate(rfr);
      CommodityCurve = new CommodityCurve(asOf, underlyingPrice, DiscountCurve, leaseRate, null);
      VolatilitySurface = CalibratedVolatilitySurface.FromFlatVolatility(asOf, volatility);
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="commodityCurve">Underlying commodity curve</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="volSurface">Volatility Surface</param>
    /// <param name="notional">Notional of contract</param>
    public CommodityOptionPricer(
      CommodityOption option, Dt asOf, Dt settle, CommodityCurve commodityCurve,
      DiscountCurve discountCurve, IVolatilitySurface volSurface, double notional)
      : base(option, asOf, settle)
    {
      Notional = notional;
      CommodityCurve = commodityCurve;
      DiscountCurve = discountCurve;
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
      if (CommodityCurve == null)
        InvalidValue.AddError(errors, this, "CommodityCurve", "Missing CommodityCurve - must be specified");
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
      get { return Dt.RelativeTime(AsOf, CommodityOption.Expiration).Value; }
    }

    /// <summary>
    /// Time to expiration date in days
    /// </summary>
    public double Days
    {
      get { return Dt.RelativeTime(AsOf, CommodityOption.Expiration).Days; }
    }

    /// <summary>
    /// Current quoted price price of underlying asset from <see cref="CommodityCurve"/>
    /// </summary>
    public double UnderlyingPrice
    {
      get { return CommodityCurve.CommoditySpotPrice; }
    }

    /// <summary>
    /// Flat lase rate
    /// </summary>
    /// <remarks>
    ///   <para>The lease rate is the difference between the commodity funding rate and the expected
    ///   growth rate of the commodity. This rate may not be observable in the market unless the
    ///   underlying commodity has an active lease market.</para>
    /// </remarks>
    public double LeaseRate
    {
      get { return CommodityCurve.EquivalentLeaseRate(CommodityOption.Expiration); }
    }

    /// <summary>
    /// Underlying commodity forward curve
    /// </summary>
    public CommodityCurve CommodityCurve { get; private set; }

    /// <summary>
    /// Flat risk free rate to option expiration
    /// </summary>
    /// <remarks>
    /// <para>Continuously compounded risk free rate to option expiration date.</para>
    /// </remarks>
    public double Rfr
    {
      get { return RateCalc.Rate(DiscountCurve, CommodityOption.Expiration); }
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
      get { return VolatilitySurface.Interpolate(CommodityOption.Expiration, AtmForwardPrice(), CommodityOption.Strike); }
    }

    /// <summary>
    /// Volatility Surface
    /// </summary>
    public IVolatilitySurface VolatilitySurface { get; set; }
    
    /// <summary>
    /// Commodity Option product
    /// </summary>
    public CommodityOption CommodityOption
    {
      get { return (CommodityOption)Product; }
    }

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

    #region Black-Scholes Model Inputs

    /// <summary>Model time</summary>
    protected override double BlackScholesTime { get { return Time; } }
    /// <summary>Model underlying quoted price for fair value and model sensitivities</summary>
    protected override double BlackScholesUnderlyingQuotedPrice { get { return UnderlyingPrice; } }
    /// <summary>Model discount rate</summary>
    protected override double BlackScholesRfr { get { return Rfr; } }
    /// <summary>Model dividend rate</summary>
    protected override double BlackScholesDividend { get { return LeaseRate; } } 
    /// <summary>Model volatility</summary>
    protected override double BlackScholesVolatility { get { return Volatility; } }

    #endregion Black-Scholes Model Inputs

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
      return CommodityCurve.Interpolate(CommodityOption.Expiration);
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
