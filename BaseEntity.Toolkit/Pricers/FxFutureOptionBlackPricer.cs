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
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.FxFutureOption">Fx Future Option</see>
  ///   using a <see cref="BaseEntity.Toolkit.Models.BlackScholes">Black-Scholes model</see>.</para>
  /// </summary>
  /// <remarks>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Products.FxFutureOption" />
  /// 
  ///   <para><h2>Pricing</h2></para>
  ///   <inheritdoc cref="BlackScholesPricerBase" />
  /// 
  ///   <para><h2>Fx Future Option Pricing Details</h2></para>
  ///   <para>The underlying future has a market quoted price along with a model price implied by the underlying
  ///   fx curve. If the futures price is used in the curve calibration then these two prices will be identical.</para>
  ///   <para><b>Futures Quoted Price</b></para>f
  ///   <para>The <see cref="QuotedFuturePrice">market quoted price</see> of the future can be specified directly,
  ///   otherwise it defaults to the price implied by the underlying fx curve.</para>
  ///   <para><b>Futures Model Price</b></para>
  ///   <para>The model price of the futures contract is the forward price of the underlying asset.
  ///   The model price is used for sensitivity calculations. See <see cref="FuturesModelPrice"/> for more details.</para>
  ///   <para>A <see cref="FuturesModelBasis">basis</see> between the quoted price and the model price can also
  ///   be specified. A method is provided to calculate the implied model basis. See <see cref="FuturesImpliedModelBasis"/>
  ///   for more details.</para>
  ///   <para><b>Sensitivities</b></para>
  ///   <para>Sensitivities use the model price to capture sensitivities to the underlying market factors. This
  ///   includes any factors used in calibrating the underlying asset curve.</para>
  ///   <seealso cref="BaseEntity.Toolkit.Products.FxFutureOption">Fx Future Option</seealso>
  /// </remarks>
  [Serializable]
  public class FxFutureOptionBlackPricer : BlackScholesPricerBase, IVolatilitySurfaceProvider
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    /// <para>The quoted future price defaults to that implied by the <see cref="FxCurve">Fx Curve</see>.</para>
    /// </remarks>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">Pricing (asOf) date</param>
    /// <param name="settle">Spot settlement date</param>
    /// <param name="discountCurve">Discount curve for valuation currency (or null to take from fx curve calibration)</param>
    /// <param name="fxCurve">Fx curve</param>
    /// <param name="volSurface">Volatility Surface</param>
    /// <param name="contracts">Number of contracts</param>
    public FxFutureOptionBlackPricer(
      FxFutureOption option, Dt asOf, Dt settle, DiscountCurve discountCurve, FxCurve fxCurve,
      IVolatilitySurface volSurface, double contracts
      )
      : base(option, asOf, settle)
    {
      Contracts = contracts;
      DiscountCurve = discountCurve;
      VolatilitySurface = volSurface;
      FxCurve = fxCurve;
      QuotedFuturePrice = FuturesModelPrice();
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
      // Allow empty FxCurve for market calculations
      // Allow empty VolatilitySurface curve as implied vol calculators do not need it
    }

    /// <summary>
    /// Clear cached calculation results
    /// </summary>
    public override void Reset()
    {
      base.Reset();
      _fxFuturePricer = null;
    }

    #endregion Utility Methods

    #region Properties

    /// <summary>
    /// Time to expiration in years
    /// </summary>
    public double Time
    {
      get { return Dt.RelativeTime(AsOf, FxFutureOption.Expiration).Value; }
    }

    /// <summary>
    /// Time to expiration in days
    /// </summary>
    public double Days
    {
      get { return Dt.RelativeTime(AsOf, FxFutureOption.Expiration).Days; }
    }

    /// <summary>
    /// Current quoted price price of underlying future
    /// </summary>
    /// <remarks>
    /// <para>If not specified, pricing is based on the futures price implied from <see cref="FxCurve"/>.</para>
    /// </remarks>
    public double QuotedFuturePrice
    {
      get { return _futurePrice.HasValue ? _futurePrice.Value : FuturesModelPrice(); }
      set { _futurePrice = value; }
    }

    /// <summary>
    /// Basis adjustment to use for futures pricing. Used to match futures model price to market price
    /// </summary>
    /// <remarks>
    /// <para>This is the basis used in the pricing of the futures contract for sensitivity calculations.
    /// This must be set explicitly. There are methods for calculating this implied basis.</para>
    /// <seealso cref="FuturesImpliedModelBasis"/>
    /// </remarks>
    public double FuturesModelBasis { get; set; }

    /// <summary>
    /// Fx Curve
    /// </summary>
    public FxCurve FxCurve { get; set; }

    /// <summary>
    /// Flat risk free rate to option expiration
    /// </summary>
    /// <remarks>
    /// <para>Continuously compounded risk free rate to option expiration date.</para>
    /// </remarks>
    public double Rfr
    {
      get { return RateCalc.Rate(DiscountCurve, FxFutureOption.Expiration); }
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
      get { return VolatilitySurface.Interpolate(FxFutureOption.Expiration, QuotedFuturePrice, FxFutureOption.Strike); }
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
      get { return Notional / FxFutureOption.FxFuture.ContractSize; }
      set { Notional = FxFutureOption.FxFuture.ContractSize * value; }
    }

    /// <summary>
    /// Fx Future Option product
    /// </summary>
    public FxFutureOption FxFutureOption
    {
      get { return (FxFutureOption)Product; }
    }

    /// <summary>
    /// Pricer for underlying future
    /// </summary>
    private FxFuturePricer FxFuturePricer
    {
      get
      {
        if (_fxFuturePricer == null)
        {
          if (FxCurve == null)
            throw new ArgumentException("Missing FxCurve. Must specify either a FxCurve or a quoted futures price");
          _fxFuturePricer = new FxFuturePricer(FxFutureOption.FxFuture, AsOf, Settle, FxFutureOption.Ccy, FxCurve, 1.0) { DiscountCurve = DiscountCurve };
          _fxFuturePricer.Validate();
        }
        return _fxFuturePricer;
      }
    }

    #region Black-Scholes Model Inputs

    /// <summary>Model time</summary>
    protected override double BlackScholesTime { get { return Time; } }
    /// <summary>Model underlying quoted price for fair value and model sensitivities</summary>
    protected override double BlackScholesUnderlyingQuotedPrice { get { return QuotedFuturePrice; } }
    /// <summary>Model underlying implied price for option pv</summary>
    protected override double BlackScholesUnderlyingModelPrice { get { return (FxCurve == null) ? QuotedFuturePrice : FuturesModelPrice() - FuturesModelBasis; } }
    /// <summary>Model discount rate</summary>
    protected override double BlackScholesRfr { get { return Rfr; } }
    /// <summary>Model dividend rate</summary>
    protected override double BlackScholesDividend { get { return Rfr; } }
    /// <summary>Model volatility</summary>
    protected override double BlackScholesVolatility { get { return Volatility; } }

    #endregion Black-Scholes Model Inputs

    #region Methods

    /// <summary>
    /// Model implied price of underlying futures
    /// </summary>
    /// <remarks>
    /// <para>The model price of the futures is the forward price of the underlying commodity.</para>
    /// <para>See <see cref="Pricers.FxFuturePricer.ModelPrice"/> for details.</para>
    /// </remarks>
    /// <returns>Model futures price</returns>
    public double FuturesModelPrice()
    {
      return FxFuturePricer.ModelPrice();
    }

    /// <summary>
    /// Basis between model and quoted futures price
    /// </summary>
    /// <remarks>
    /// <para>The model basis is the difference between the model futures price and the quoted futures price.</para>
    /// <para>See <see cref="Pricers.FxFuturePricer.ImpliedModelBasis"/> for details.</para>
    /// </remarks>
    /// <returns>Model basis</returns>
    public double FuturesImpliedModelBasis()
    {
      return FxFuturePricer.ImpliedModelBasis();
    }

    #endregion Methods

    #region IVolatilitySurfaceProvider Methods

    System.Collections.Generic.IEnumerable<IVolatilitySurface> IVolatilitySurfaceProvider.GetVolatilitySurfaces()
    {
      if (VolatilitySurface != null) yield return VolatilitySurface;
    }

    #endregion IVolatilitySurfaceProvider Methods

    #endregion Properties

    #region Data

    private double? _futurePrice;
    private FxFuturePricer _fxFuturePricer;

    #endregion Data
  }
}
