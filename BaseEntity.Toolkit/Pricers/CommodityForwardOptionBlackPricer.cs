// 
// CommodityForwardOptionBlackPricer.cs
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
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.CommodityForwardOption">Commodity Forward Option</see>
  ///   using a <see cref="BaseEntity.Toolkit.Models.BlackScholes">Black-Scholes model</see>.</para>
  /// </summary>
  /// <remarks>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Products.CommodityForwardOption" />
  ///   <para><h2>Pricing</h2></para>
  ///   <inheritdoc cref="BlackScholesPricerBase" />
  ///   <para><h2>Pricing Details</h2></para>
  ///   <para>The option may be priced by specifying either the spot price and the lease rate or
  ///   the forward price curve.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.CommodityForwardOption">Commodity Future Product</seealso>
  /// <seealso cref="BlackScholesPricerBase">Commodity Future Product</seealso>
  [Serializable]
  public class CommodityForwardOptionBlackPricer : BlackScholesPricerBase
  {
    #region Constructors
  
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="rfr">Risk free rate</param>
    /// <param name="leaseRate">Lease rate (continuous comp, eg. 0.05)</param>
    /// <param name="volSurface">Volatility Surface</param>
    /// <param name="spotPrice">Spot price of underlying commodity</param>
    /// <param name="notional">Notional</param>
    public CommodityForwardOptionBlackPricer(
      CommodityForwardOption option, Dt asOf, Dt settle, double rfr, double leaseRate,
      IVolatilitySurface volSurface, double spotPrice, double notional
      )
      : base(option, asOf, settle)
    {
      DiscountCurve = new DiscountCurve(asOf).SetRelativeTimeRate(rfr);
      CommodityCurve = new CommodityCurve(asOf, spotPrice, DiscountCurve, leaseRate, null);
      VolatilitySurface = volSurface;
      Notional = notional;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="commodityCurve">Commodity curve</param>
    /// <param name="volSurface">Volatility Surface</param>
    /// <param name="notional">Notional</param>
    public CommodityForwardOptionBlackPricer(
      CommodityForwardOption option, Dt asOf, Dt settle, DiscountCurve discountCurve, CommodityCurve commodityCurve,
      IVolatilitySurface volSurface, double notional)
      : base(option, asOf, settle)
    {
      DiscountCurve = discountCurve;
      CommodityCurve = commodityCurve;
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
      if (CommodityCurve == null)
        InvalidValue.AddError(errors, this, "CommodityCurve", "Missing CommodityCurve - must be specified");
      if (DiscountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", "Invalid discount curve, Must be non null");
      // Allow empty VolatilitySurface curve as implied vol calculators do not need it
    }

    /// <summary>
    /// Clear cached calculation results
    /// </summary>
    public override void Reset()
    {
      base.Reset();
      _commodityForwardPricer = null;
    }

    #endregion Utility Methods

    #region Properties

    /// <summary>
    /// Time to expiration date in years
    /// </summary>
    public double Time
    {
      get { return Dt.RelativeTime(AsOf, CommodityForwardOption.Expiration).Value; }
    }

    /// <summary>
    /// Time to expiration date in days
    /// </summary>
    public double Days
    {
      get { return Dt.RelativeTime(AsOf, CommodityForwardOption.Expiration).Days; }
    }

    /// <summary>
    /// Flat risk free rate to option expiration
    /// </summary>
    /// <remarks>
    /// <para>Continuously compounded risk free rate to option expiration date.</para>
    /// </remarks>
    public double Rfr
    {
      get { return RateCalc.Rate(DiscountCurve, CommodityForwardOption.Expiration); }
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
      get { return VolatilitySurface.Interpolate(CommodityForwardOption.Expiration, AtmForwardPrice(), CommodityForwardOption.Strike); }
    }

    /// <summary>
    /// Volatility Surface
    /// </summary>
    public IVolatilitySurface VolatilitySurface { get; set; }

    /// <summary>
    /// Commodity forward option product
    /// </summary>
    public CommodityForwardOption CommodityForwardOption
    {
      get { return (CommodityForwardOption)Product; }
    }

    /// <summary>
    /// ReferenceCurve
    /// </summary>
    public CommodityCurve CommodityCurve { get; private set; }

    /// <summary>
    /// Pricer for underlying future
    /// </summary>
    private CommodityForwardPricer CommodityForwardPricer
    {
      get
      {
        if (_commodityForwardPricer == null)
        {
          _commodityForwardPricer = new CommodityForwardPricer(CommodityForwardOption.CommodityForward, AsOf, Settle, DiscountCurve, CommodityCurve);
          _commodityForwardPricer.Validate();
        }
        return _commodityForwardPricer;
      }
    }

    #region Black-Scholes Model Inputs

    /// <summary>Model time</summary>
    protected override double BlackScholesTime { get { return Time; } }
    /// <summary>Model underlying price for fair value and model sensitivities</summary>
    protected override double BlackScholesUnderlyingQuotedPrice { get { return AtmForwardPrice(); } }
    /// <summary>Model discount rate</summary>
    protected override double BlackScholesRfr { get { return Rfr; } }
    /// <summary>Model dividend rate</summary>
    protected override double BlackScholesDividend { get { return Rfr; } }
    /// <summary>Model volatility</summary>
    protected override double BlackScholesVolatility { get { return Volatility; } }

    #endregion Black-Scholes Model Inputs

    #endregion Properties

    #region Methods

    /// <summary>
    /// Lease rate implied from quoted forward price
    /// </summary>
    /// <remarks>
    /// <para>The lease rate is the difference between the commodity funding rate and the expected
    /// growth rate of the commodity. This rate may not be observable in the market unless the
    /// underlying commodity has an active lease market.</para>
    /// <para>See <see cref="Pricers.CommodityForwardPricer.ImpliedLeaseRate"/> for details.</para>
    /// </remarks>
    /// <returns>Implied lease rate</returns>
    public double ImpliedLeaseRate()
    {
      return CommodityForwardPricer.ImpliedLeaseRate();
    }

    /// <summary>
    /// Calculate ATM forward price for the underlying at the option expiration date
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="Pricers.CommodityForwardPricer.ModelPrice()" />
    /// </remarks>
    /// <returns>ATM forward price</returns>
    public double AtmForwardPrice()
    {
      return CommodityForwardPricer.ModelPrice();
    }

    #endregion Methods

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

    private CommodityForwardPricer _commodityForwardPricer;

    #endregion Data
  }
}
