// 
//   2017. All rights reserved.
// 

using System;
using System.Collections;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.StirFutureOption">STIR Future Option</see>
  ///   using a <see cref="BaseEntity.Toolkit.Models.BlackScholes">Black-Scholes model</see>.</para>
  /// </summary>
  /// <remarks>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Products.StirFutureOption" />
  /// 
  ///   <para><h2>Pricing</h2></para>
  ///   <inheritdoc cref="BlackScholesPricerBase" />
  /// 
  ///   <para><h2>Bond Future Option Pricing Details</h2></para>
  ///   <para>For calculating the fair value of the option, the quoted price of the underlying future is used. To calculate
  ///   sensitivies (from the option pv), the model implied futures price is used.</para>
  ///   <para>See <see cref="Pricers.StirFuturePricer.ModelPrice"/> for more details.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.StirFutureOption">Bond Future Option</seealso>
  /// <seealso cref="BlackScholesPricerBase">Bond Future Option</seealso>
  [Serializable]
  public class StirFutureOptionBlackPricer : BlackScholesPricerBase, IVolatilitySurfaceProvider
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="futuresPrice">Market quoted price of underlying future</param>
    /// <param name="rfr">Risk free rate</param>
    /// <param name="volatility">Volatility</param>
    /// <param name="contracts">Number of contracts</param>
    public StirFutureOptionBlackPricer(
      StirFutureOption option, Dt asOf, Dt settle, double futuresPrice, double rfr, double volatility, double contracts)
      : base(option, asOf, settle)
    {
      Contracts = contracts;
      DiscountCurve = new DiscountCurve(asOf).SetRelativeTimeRate(rfr);
      VolatilitySurface = CalibratedVolatilitySurface.FromFlatVolatility(asOf, volatility);
      FuturesQuotedPrice = futuresPrice;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="futuresPrice">Underlying futures price</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="refCurve">Reference rate curve</param>
    /// <param name="volSurface">Volatility Surface</param>
    /// <param name="contracts">Number of contracts</param>
    public StirFutureOptionBlackPricer(
      StirFutureOption option, Dt asOf, Dt settle, double futuresPrice, DiscountCurve discountCurve, DiscountCurve refCurve, IVolatilitySurface volSurface,
      double contracts
      )
      : base(option, asOf, settle)
    {
      FuturesQuotedPrice = futuresPrice;
      DiscountCurve = discountCurve;
      ReferenceCurve = refCurve;
      VolatilitySurface = volSurface;
      Notional = contracts * option.StirFuture.ContractSize;
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
      if (FuturesQuotedPrice <= 0.0)
        InvalidValue.AddError(errors, this, "UnderlyingPrice", String.Format("Invalid underlying price {0}. must be >= 0", FuturesQuotedPrice));
      if (DiscountCurve == null && Rfr < 0.0)
        InvalidValue.AddError(errors, this, "Rfr", String.Format("Invalid discount rate {0}, Must be >= 0", Rfr));
      if (VolatilitySurface == null && Volatility < 0.0)
        InvalidValue.AddError(errors, this, "Volatility", String.Format("Invalid volatility {0}, Must be >= 0", Volatility));
    }

    /// <summary>
    /// Clear cached calculation results
    /// </summary>
    public override void Reset()
    {
      base.Reset();
      _stirFuturePricer = null;
    }

    #endregion Utility Methods

    #region Properties

    /// <summary>
    /// Time to expiration date in years
    /// </summary>
    public double Time => Dt.RelativeTime(AsOf, StirFutureOption.Expiration).Value;

    /// <summary>
    /// Time to expiration date in days
    /// </summary>
    public double Days => Dt.RelativeTime(AsOf, StirFutureOption.Expiration).Days;

    /// <summary>
    /// Quoted Price price of underlying future
    /// </summary>
    public double FuturesQuotedPrice { get; set; }

    /// <summary>
    /// Basis adjustment to use for futures pricing. Used to match futures model price to market price
    /// </summary>
    /// <remarks>
    /// <para>This is the basis used in the pricing of the futures contract for sensitivity calculations.
    /// This must be set explicitly. There are methods for calculating this implied basis.</para>
    /// <seealso cref="FuturesImpliedModelBasis"/>
    /// </remarks>
    public double FuturesModelBasis
    {
      get { return StirFuturePricer.ModelBasis; }
      set { StirFuturePricer.ModelBasis = value; }
    }

    /// <summary>
    /// Flat risk free rate to option expiration
    /// </summary>
    /// <remarks>
    /// <para>Continuously compounded risk free rate to option expiration date.</para>
    /// </remarks>
    public double Rfr => RateCalc.Rate(DiscountCurve, StirFutureOption.Expiration);

    /// <summary>
    /// Discount Curve
    /// </summary>
    public DiscountCurve DiscountCurve { get; set; }

    /// <summary>
    /// Reference Curve
    /// </summary>
    public DiscountCurve ReferenceCurve { get; set; }

    /// <summary>
    /// Rate model parameters used for convexity adjustments
    /// </summary>
    public RateModelParameters RateModelParameters { get; set; }

    /// <summary>
    /// Flat volatility to option expiration
    /// </summary>
    /// <remarks>
    /// <para>Flat volatility to option expiration date.</para>
    /// </remarks>
    public double Volatility => VolatilitySurface.Interpolate(StirFutureOption.Expiration, FuturesQuotedPrice, StirFutureOption.Strike);

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
      get { return Notional / StirFutureOption.StirFuture.ContractSize; }
      set { Notional = StirFutureOption.StirFuture.ContractSize * value; }
    }

    /// <summary>
    /// Commodity forward option product
    /// </summary>
    public StirFutureOption StirFutureOption => (StirFutureOption)Product;

    /// <summary>
    /// Pricer for underlying future
    /// </summary>
    private StirFuturePricer StirFuturePricer
    {
      get
      {
        if (_stirFuturePricer == null)
        {
          if (ReferenceCurve == null)
            throw new ArgumentException("Missing FxCurve. Must specify either a FxCurve or a quoted futures price");
          _stirFuturePricer = new StirFuturePricer(StirFutureOption.StirFuture, AsOf, Settle, 1.0, DiscountCurve, ReferenceCurve)
          {
            QuotedPrice = FuturesQuotedPrice,
            RateModelParameters = RateModelParameters
          };
          _stirFuturePricer.Validate();
        }
        return _stirFuturePricer;
      }
    }

    #region Black-Scholes Model Inputs

    /// <summary>Model time</summary>
    protected override double BlackScholesTime => Time;

    /// <summary>Model underlying quoted price for fair value and model sensitivities</summary>
    protected override double BlackScholesUnderlyingQuotedPrice => FuturesQuotedPrice;

    /// <summary>Model underlying implied price for option pv</summary>
    protected override double BlackScholesUnderlyingModelPrice => FuturesModelPrice() - FuturesModelBasis;

    /// <summary>Model discount rate</summary>
    protected override double BlackScholesRfr => Rfr;

    /// <summary>Model dividend rate</summary>
    protected override double BlackScholesDividend => Rfr;

    /// <summary>Model volatility</summary>
    protected override double BlackScholesVolatility => Volatility;

    #endregion Black-Scholes Model Inputs

    #endregion Properties

    #region Methods

    /// <summary>
    /// Model implied price of underlying future
    /// </summary>
    /// <remarks>
    /// <para>The model price of the futures contract is the forward price of the STIR future.</para>
    /// <para>See <see cref="Pricers.StirFuturePricer.ModelPrice"/> for details.</para>
    /// <para>If no CTD bond is specified, the quoted futures price is returned.</para>
    /// </remarks>
    /// <returns>Model price of future</returns>
    public double FuturesModelPrice()
    {
      return StirFuturePricer.ModelPrice();
    }

    /// <summary>
    /// Basis between model and quoted futures price
    /// </summary>
    /// <remarks>
    /// <para>The model basis is the difference between the model futures price implied by the model forward
    /// price of the CTD bond and the conversion factor and the quoted futures price.</para>
    /// <para>See <see cref="Pricers.StirFuturePricer.ModelBasis"/> for details.</para>
    /// </remarks>
    /// <returns>Model basis</returns>
    public double FuturesImpliedModelBasis()
    {
      return StirFuturePricer.ImpliedModelBasis();
    }

    #endregion Methods

    #region IVolatilitySurfaceProvider Methods

    System.Collections.Generic.IEnumerable<IVolatilitySurface> IVolatilitySurfaceProvider.GetVolatilitySurfaces()
    {
      if (VolatilitySurface != null) yield return VolatilitySurface;
    }

    #endregion IVolatilitySurfaceProvider Methods

    #region Data

    private StirFuturePricer _stirFuturePricer;

    #endregion Data
  }
}
