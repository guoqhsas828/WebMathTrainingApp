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
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.StockFutureOption">Option on Stock Future or Stock Index Future</see>
  ///   using a <see cref="BaseEntity.Toolkit.Models.BlackScholes">Black-Scholes model</see>.</para>
  /// </summary>
  /// <remarks>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Products.StockFutureOption" />
  /// 
  ///   <para><h2>Pricing</h2></para>
  ///   <inheritdoc cref="BlackScholesPricerBase" />
  /// 
  ///   <para><h2>Stock Future Pricing Details</h2></para>
  ///   <para>The underlying futures has a market quoted price along with a model price implied by the underlying
  ///   asset curve. If the futures price is used in the curve calibration then these two prices will
  ///   be identical.</para>
  ///   <para><b>Futures Quoted Price</b></para>
  ///   <para>The <see cref="QuotedFuturePrice">market quoted price</see> of the future can be specified directly,
  ///   otherwise it defaults to the price implied by the underlying asset curve.</para>
  ///   <para><b>Futures Model Price</b></para>
  ///   <para>The model price of the futures contract is the forward price of the underlying asset.
  ///   The model price is used for sensitivity calculations. See <see cref="FuturesModelPrice"/> for more details.</para>
  ///   <para>A <see cref="FuturesModelBasis">basis</see> between the quoted price and the model price can also
  ///   be specified. A method is provided to calculate the implied model basis. See <see cref="FuturesImpliedModelBasis"/>
  ///   for more details.</para>
  ///   <para><b>Sensitivities</b></para>
  ///   <para>Sensitivities use the model price to capture sensitivities to the underlying market factors. This
  ///   includes any factors used in calibrating the underlying asset curve.</para>
  ///   <seealso cref="BaseEntity.Toolkit.Products.StockFutureOption">Stock or Stock Index Future</seealso>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.StockFutureOption"/>
  /// <seealso cref="Pricers.StockFuturePricer"/>
  ///
  /// <example>
  ///   Construct a StockFutureOption pricer
  ///   <code>
  ///     StockFutureOption option;              // Stock future option
  ///     Dt pricingDate;                        // Pricing date
  ///     Dt settleDate,                         // Settlement date
  ///     StockCurve stockCurve;                 // Stock curve
  ///     CalibratedVolatilitySurface vol;       // Volatility surface
  ///     DiscountCurve discountCurve;           // Discount curve
  ///     double contracts = 100;                // Number of contracts
  ///     double futuresPrice = 89.25;           // Current futures price
  ///
  ///     // Create required data
  ///     // ...
  /// 
  ///     // Create pricer
  ///      var pricer = new StockFutureOptionBlackPricer(option, pricingDate, settleDate,
  ///        futuresPrice, stockCurve, discountCurve, volatilitySurface, contracts);
  ///     // Validate pricer
  ///     pricer.Validate();
  ///     // Fit model to current futures price by setting model basis
  ///     pricer.FuturesModelBasis = pricer.FuturesImpliedModelBasis();
  ///   </code>
  /// </example>
  [Serializable]
  public class StockFutureOptionBlackPricer : BlackScholesPricerBase, IVolatilitySurfaceProvider
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    /// <para>Simplied constructor for market calculations only. Sensitivity methods
    /// cannot be used unless <see cref="StockCurve"/> is set.</para>
    /// </remarks>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="futurePrice">Futures price</param>
    /// <param name="rfr">Risk free rate</param>
    /// <param name="volatility">Volatility</param>
    public StockFutureOptionBlackPricer(
      StockFutureOption option, Dt asOf, Dt settle, double futurePrice, double rfr, double volatility
      )
      : base(option, asOf, settle)
    {
      Contracts = 1.0;
      DiscountCurve = new DiscountCurve(asOf).SetRelativeTimeRate(rfr);
      VolatilitySurface = CalibratedVolatilitySurface.FromFlatVolatility(asOf, volatility);
      StockFuturePricer = new StockFuturePricer(StockFutureOption.StockFuture, asOf, settle, futurePrice, null, 1.0);
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    /// <para>This is the preferred constructor for <see cref="StockFutureOptionBlackPricer"/></para>
    /// <para>Sets FuturesModelBasis based from quoted <paramref cref="futurePrice"/></para>
    /// <para>All market and sensitivity functions can be performed.</para>
    /// </remarks>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="futurePrice">Quoted stock future price</param>
    /// <param name="stockCurve">Underlying stock forward curve</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="volSurface">Volatility Surface</param>
    /// <param name="contracts">Number of contracts</param>
    public StockFutureOptionBlackPricer(
      StockFutureOption option, Dt asOf, Dt settle, double futurePrice, StockCurve stockCurve,
      DiscountCurve discountCurve, IVolatilitySurface volSurface, double contracts)
      : base(option, asOf, settle)
    {
      Contracts = contracts;
      DiscountCurve = discountCurve;
      VolatilitySurface = volSurface;
      StockFuturePricer = new StockFuturePricer(StockFutureOption.StockFuture, asOf, settle, futurePrice, stockCurve, 1.0);
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    /// <para>Both market and sensitivity methods can be used. The price for the underlying Future
    /// will be implied from the <paramref cref="stockCurve"/> unless
    /// <see cref="QuotedFuturePrice"/> is set.</para>
    /// </remarks>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="stockCurve">Underlying stock forward curve</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="volSurface">Volatility Surface</param>
    /// <param name="contracts">Number of contracts</param>
    public StockFutureOptionBlackPricer(
      StockFutureOption option, Dt asOf, Dt settle, StockCurve stockCurve,
      DiscountCurve discountCurve, IVolatilitySurface volSurface, double contracts)
      : base(option, asOf, settle)
    {
      Contracts = contracts;
      DiscountCurve = discountCurve;
      VolatilitySurface = volSurface;
      StockFuturePricer = new StockFuturePricer(StockFutureOption.StockFuture, asOf, settle, stockCurve, 1.0);
    }

    #endregion Constructors

    #region Utility Methods

    /// <summary>
    /// Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;
      base.Validate(errors);
      StockFuturePricer.Validate();
      if (DiscountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", "Missing DiscountCurve - must be specified");
      // Allow empty stock forward curve for market calcs
      // Allow empty VolatilitySurface curve as implied vol calculators do not need it
      return;
    }

    /// <summary>
    /// Clear cached calculation results
    /// </summary>
    public override void Reset()
    {
      base.Reset();
      StockFuturePricer.Reset();
    }

    #endregion Utility Methods

    #region Properties

    /// <summary>
    /// Time to expiration in years
    /// </summary>
    public double Time
    {
      get { return Dt.RelativeTime(AsOf, StockFutureOption.Expiration).Value; }
    }

    /// <summary>
    /// Time to expiration in days
    /// </summary>
    public double Days
    {
      get { return Dt.RelativeTime(AsOf, StockFutureOption.Expiration).Days; }
    }

    /// <summary>
    /// Underlying futures quoted price
    /// </summary>
    public double QuotedFuturePrice
    {
      get { return StockFuturePricer.QuotedPrice; }
      set { StockFuturePricer.QuotedPrice = value; }
    }

    /// <summary>
    /// Underlying futures price has been specified
    /// </summary>
    public bool QuotedFuturePriceIsSpecified
    {
      get { return StockFuturePricer.QuotedPriceIsSpecified; }
    }

    /// <summary>
    /// Underlying futures basis adjustment. Used to match futures model price to market price
    /// </summary>
    /// <remarks>
    /// <para>This is the basis used in the pricing of the futures contract for sensitivity calculations.
    /// This must be set explicitly. There are methods for calculating this implied basis.</para>
    /// <seealso cref="FuturesImpliedModelBasis"/>
    /// </remarks>
    public double FuturesModelBasis
    {
      get { return StockFuturePricer.ModelBasis; }
      set { StockFuturePricer.ModelBasis = value; }
    }

    /// <summary>
    /// Underlying stock curve used for model pricing
    /// </summary>
    public StockCurve StockCurve
    {
      get { return StockFuturePricer.StockCurve; }
      set { StockFuturePricer.StockCurve = value; }
    }

    /// <summary>
    /// Flat risk free rate to option expiration
    /// </summary>
    /// <remarks>
    /// <para>Continuously compounded risk free rate to option expiration date.</para>
    /// </remarks>
    public double Rfr
    {
      get { return RateCalc.Rate(DiscountCurve, AsOf, StockFutureOption.Expiration); }
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
      get { return VolatilitySurface.Interpolate(StockFutureOption.Expiration, QuotedFuturePriceIsSpecified ? QuotedFuturePrice : FuturesModelPrice(), StockFutureOption.Strike); }
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
      get { return Notional / StockFutureOption.StockFuture.ContractSize; }
      set { Notional = StockFutureOption.StockFuture.ContractSize * value; }
    }

    /// <summary>
    /// Stock Future Option product
    /// </summary>
    public StockFutureOption StockFutureOption { get { return (StockFutureOption)Product; } }

    /// <summary>
    /// Pricer for underlying future
    /// </summary>
    private StockFuturePricer StockFuturePricer { get; set; }

    #region Black-Scholes Model Inputs

    /// <summary>Model time</summary>
    protected override double BlackScholesTime { get { return Time; } }
    /// <summary>Model underlying price for fair value and model sensitivities</summary>
    protected override double BlackScholesUnderlyingQuotedPrice { get { return QuotedFuturePriceIsSpecified ? QuotedFuturePrice : FuturesModelPrice(); } }
    /// <summary>Model underlying implied price for option pv</summary>
    protected override double BlackScholesUnderlyingModelPrice { get { return (StockCurve != null) ? FuturesModelPrice() - FuturesModelBasis : QuotedFuturePrice; } }
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
    /// Model implied price of underlying future
    /// </summary>
    /// <remarks>
    /// <para>The model price of the futures contract is the forward price of the stock or index
    /// implied by the spot price and the dividend schedule.</para>
    /// </remarks>
    /// <returns>Model price of future</returns>
    public double FuturesModelPrice()
    {
      return StockFuturePricer.ModelPrice();
    }

    /// <summary>
    /// Basis between model and quoted futures price
    /// </summary>
    /// <remarks>
    /// <para>The model basis is the difference between the model futures price implied by the model forward
    /// price of the CTD bond and the conversion factor and the quoted futures price.</para>
    /// </remarks>
    /// <returns>Model basis</returns>
    public double FuturesImpliedModelBasis()
    {
      return StockFuturePricer.ImpliedModelBasis();
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
