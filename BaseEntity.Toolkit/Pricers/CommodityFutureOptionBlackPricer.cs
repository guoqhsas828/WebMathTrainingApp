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
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.CommodityFutureOption">Commodity Future Option</see>
  ///   using a <see cref="BaseEntity.Toolkit.Models.BlackScholes">Black-Scholes model</see>.</para>
  /// </summary>
  /// <remarks>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Products.CommodityFutureOption" />
  /// 
  ///   <para><h2>Pricing</h2></para>
  ///   <inheritdoc cref="BlackScholesPricerBase" />
  /// 
  ///   <para><b>Pricing Details</b></para>
  ///   <para>The underlying futures has a market quoted price along with a model price implied by the underlying
  ///   asset curve. If the futures price is used in the curve calibration then these two prices will
  ///   be identical.</para>
  /// 
  ///   <para><b>Futures Quoted Price</b></para>
  ///   <para>The <see cref="QuotedFuturePrice">market quoted price</see> of the future can be specified directly,
  ///   otherwise it defaults to the price implied by the underlying asset curve.</para>
  /// 
  ///   <para><b>Futures Model Price</b></para>
  ///   <para>The model price of the futures contract is the forward price of the underlying asset.
  ///   The model price is used for sensitivity calculations. See <see cref="FuturesModelPrice"/> for more details.</para>
  ///   <para>A <see cref="FuturesModelBasis">basis</see> between the quoted price and the model price can also
  ///   be specified. A method is provided to calculate the implied model basis. See <see cref="FuturesImpliedModelBasis"/>
  ///   for more details.</para>
  /// 
  ///   <para><h2>Sensitivities</h2></para>
  ///   <para>Sensitivities use the model price to capture sensitivities to the underlying market factors. This
  ///   includes any factors used in calibrating the underlying asset curve.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.CommodityFutureOption">Commodity Future Product</seealso>
  /// <seealso cref="BlackScholesPricerBase">Commodity Future Product</seealso>
  [Serializable]
  public class CommodityFutureOptionBlackPricer : BlackScholesPricerBase, IVolatilitySurfaceProvider
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="futuresPrice">Underlying futures price</param>
    /// <param name="rfr">Risk free rate</param>
    /// <param name="volatility">Volatility</param>
    /// <param name="contracts">Number of contracts</param>
    public CommodityFutureOptionBlackPricer(
      CommodityFutureOption option, Dt asOf, Dt settle, double futuresPrice, double rfr, double volatility, double contracts
      )
      : base( option, asOf, settle)
    {
      Contracts = contracts;
      DiscountCurve = new DiscountCurve(asOf).SetRelativeTimeRate(rfr);
      CommodityCurve = new CommodityCurve(asOf, futuresPrice);
      VolatilitySurface = CalibratedVolatilitySurface.FromFlatVolatility(asOf, volatility);
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Underlying discount curve</param>
    /// <param name="commodityCurve">Underlying commodity curve</param>
    /// <param name="volSurface">Volatility Surface</param>
    /// <param name="contracts">Number of contracts</param>
    public CommodityFutureOptionBlackPricer(
      CommodityFutureOption option, Dt asOf, Dt settle, DiscountCurve discountCurve, CommodityCurve commodityCurve,
      IVolatilitySurface volSurface, double contracts)
      : base(option, asOf, settle)
    {
      Contracts = contracts;
      DiscountCurve = discountCurve;
      CommodityCurve = commodityCurve;
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
      if (DiscountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", "Missing DiscountCurve - must be specified");
      // Allow empty VolatilitySurface curve as implied vol calculators do not need it
      // Allow empty CommodityCurve for market calculations 
    }

    /// <summary>
    /// Clear cached calculation results
    /// </summary>
    public override void Reset()
    {
      base.Reset();
      _commodityFuturePricer = null;
    }

    #endregion Utility Methods

    #region Properties

    /// <summary>
    /// Time to expiration date in years
    /// </summary>
    public double Time
    {
      get { return Dt.RelativeTime(AsOf, CommodityFutureOption.Expiration).Value; }
    }

    /// <summary>
    /// Time to expiration date in days
    /// </summary>
    public double Days
    {
      get { return Dt.RelativeTime(AsOf, CommodityFutureOption.Expiration).Days; }
    }

    /// <summary>
    /// Current quoted price price of underlying future
    /// </summary>
    /// <remarks>
    /// <para>If not specified, pricing is based on the futures price implied from <see cref="CommodityCurve"/>.</para>
    /// </remarks>
    public double QuotedFuturePrice
    {
      get { return _futurePrice.HasValue ? _futurePrice.Value : FuturesModelPrice(); }
      set { _futurePrice = value; }
    }

    /// <summary>
    /// Basis adjustment to use for model pricing. Used to match model price to market price
    /// </summary>
    /// <remarks>
    /// <para>This is the basis used in the pricing of the futures contract for sensitivity calculations.
    /// This must be set explicitly. There are methods for calculating this implied basis.</para>
    /// <seealso cref="FuturesImpliedModelBasis"/>
    /// </remarks>
    public double FuturesModelBasis { get; set; }

    /// <summary>
    /// Underlying commodity curve used for model pricing
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
      get { return RateCalc.Rate(DiscountCurve, CommodityFutureOption.Expiration); }
    }

    /// <summary>
    /// Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve { get; private set; }

    /// <summary>
    /// Flat volatility to option expiration
    /// </summary>
    /// <remarks>
    /// <para>Flat volatility to option expiration date.</para>
    /// </remarks>
    public double Volatility
    {
      get { return VolatilitySurface.Interpolate(CommodityFutureOption.Expiration, QuotedFuturePrice, CommodityFutureOption.Strike); }
    }

    /// <summary>
    /// Volatility Surface
    /// </summary>
    public IVolatilitySurface VolatilitySurface { get; set; }
    
    /// <summary>
    /// Number of contracts
    /// </summary>
    /// <remarks>
    ///   <para>The <see cref="PricerBase.Notional">Notional</see> is equal to
    ///   the <see cref="FutureBase.ContractSize">Contract size</see> times the
    ///   <see cref="Contracts">Number of Contracts</see>.</para>
    /// </remarks>
    public double Contracts
    {
      get { return Notional / CommodityFutureOption.CommodityFuture.ContractSize; }
      set { Notional = CommodityFutureOption.CommodityFuture.ContractSize * value; }
    }

    /// <summary>
    /// Commodity forward option product
    /// </summary>
    public CommodityFutureOption CommodityFutureOption { get { return (CommodityFutureOption)Product; } }

    /// <summary>
    /// Pricer for underlying future
    /// </summary>
    private CommodityFuturesPricer CommodityFuturePricer
    {
      get
      {
        if (_commodityFuturePricer == null)
        {
          if (CommodityCurve == null)
            throw new ArgumentException("Missing CommodityCurve. Must specify a valid CommodityCurve for model calculations");
          _commodityFuturePricer = new CommodityFuturesPricer(CommodityFutureOption.CommodityFuture, AsOf, Settle, CommodityCurve, 1.0);
          _commodityFuturePricer.Validate();
        }
        return _commodityFuturePricer;
      }
    }

    #region Black-Scholes Model Inputs

    /// <summary>Model time</summary>
    protected override double BlackScholesTime { get { return Time; } }
    /// <summary>Model underlying quoted price for fair value and model sensitivities</summary>
    protected override double BlackScholesUnderlyingQuotedPrice { get { return QuotedFuturePrice; } }
    /// <summary>Model underlying implied price for option pv</summary>
    protected override double BlackScholesUnderlyingModelPrice { get { return (CommodityCurve == null) ? QuotedFuturePrice : FuturesModelPrice() - FuturesModelBasis; } }
    /// <summary>Model discount rate</summary>
    protected override double BlackScholesRfr{ get {return Rfr; } }
    /// <summary>Model dividend rate</summary>
    protected override double BlackScholesDividend { get { return Rfr; } }
    /// <summary>Model volatility</summary>
    protected override double BlackScholesVolatility { get { return Volatility; } }

    #endregion Black-Scholes Model Inputs

    #endregion Properties

    #region Methods

    /// <summary>
    /// Model implied price of underlying futures
    /// </summary>
    /// <remarks>
    /// <para>The model price of the futures is the forward price of the underlying commodity.</para>
    /// <para>See <see cref="Pricers.CommodityFuturesPricer.ModelPrice"/> for details.</para>
    /// </remarks>
    /// <returns>Model futures price</returns>
    public double FuturesModelPrice()
    {
      return CommodityFuturePricer.ModelPrice();
    }

    /// <summary>
    /// Basis between model and quoted futures price
    /// </summary>
    /// <remarks>
    /// <para>The model basis is the difference between the model futures price and the quoted futures price.</para>
    /// <para>See <see cref="Pricers.CommodityFuturesPricer.ImpliedModelBasis"/> for details.</para>
    /// </remarks>
    /// <returns>Model basis</returns>
    public double FuturesImpliedModelBasis()
    {
      return CommodityFuturePricer.ImpliedModelBasis();
    }

    ///<summary>
    /// Calculate MTM by passing current underlying futures price into the black-scholes formula
    ///</summary>
    public double MTM()
    {
      double pv = (IsTerminated) ? 0.0 : FairValue();
      pv += PaymentPv();
      return pv;
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

    #endregion Methods

    #region IVolatilitySurfaceProvider Methods

    System.Collections.Generic.IEnumerable<IVolatilitySurface> IVolatilitySurfaceProvider.GetVolatilitySurfaces()
    {
      if (VolatilitySurface != null) yield return VolatilitySurface;
    }

    #endregion IVolatilitySurfaceProvider Methods
    
    #region Data

    private double? _futurePrice;
    private CommodityFuturesPricer _commodityFuturePricer;

    #endregion Data
    
  }
}
