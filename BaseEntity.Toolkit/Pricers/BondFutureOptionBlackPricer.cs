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
  ///   <para>Price a <see cref="BaseEntity.Toolkit.Products.BondFutureOption">Bond Future Option</see>
  ///   using a <see cref="BaseEntity.Toolkit.Models.BlackScholes">Black-Scholes model</see>.</para>
  /// </summary>
  /// <remarks>
  ///   <inheritdoc cref="BaseEntity.Toolkit.Products.BondFutureOption" />
  /// 
  ///   <para><h2>Pricing</h2></para>
  ///   <inheritdoc cref="BlackScholesPricerBase" />
  /// 
  ///   <para><h2>Bond Future Option Pricing Details</h2></para>
  ///   <para>For calculating the fair value of the option, the quoted price of the underlying future is used. To calculate
  ///   sensitivies (from the option pv), the model implied futures price is used.</para>
  ///   <para>The model price of the futures contract is the forward price of the CTD bond divided by the
  ///   conversion factor.</para>
  ///   <para>The forward price of the CTD bond is calculated from the model price of the bond so that sensitivities
  ///   to interest rates can be generated. To be clear, this effectively captures all sensitivity to interest
  ///   rates to the maturity of the CTD bond.</para>
  ///   <para>See <see cref="Pricers.BondFuturePricer.ModelPrice"/> for more details.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.BondFutureOption">Bond Future Option</seealso>
  /// <seealso cref="BlackScholesPricerBase">Bond Future Option</seealso>
  [Serializable]
  public class BondFutureOptionBlackPricer : BlackScholesPricerBase, IVolatilitySurfaceProvider
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
    public BondFutureOptionBlackPricer(
      BondFutureOption option, Dt asOf, Dt settle, double futuresPrice, double rfr, double volatility, double contracts)
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
    /// <param name="volSurface">Volatility Surface</param>
    /// <param name="contracts">Number of contracts</param>
    public BondFutureOptionBlackPricer(
      BondFutureOption option, Dt asOf, Dt settle, double futuresPrice, DiscountCurve discountCurve, IVolatilitySurface volSurface,
      double contracts
      )
      : base(option, asOf, settle)
    {
      FuturesQuotedPrice = futuresPrice;
      DiscountCurve = discountCurve;
      VolatilitySurface = volSurface;
      Notional = contracts * option.BondFuture.ContractSize;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="option">Option to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="futuresPrice">Underlying futures price</param>
    /// <param name="discountCurve">Discount/term repo curve pricing</param>
    /// <param name="volSurface">Volatility Surface</param>
    /// <param name="ctdBond">Cheapest to delivery bond</param>
    /// <param name="ctdMarketQuote">Market quote of CTD bond</param>
    /// <param name="ctdQuotingConvention">Market quote convention of CTD bond</param>
    /// <param name="ctdConversionFactor">Conversion factor for cheapest to delivery bond</param>
    /// <param name="contracts">Number of contracts</param>
    public BondFutureOptionBlackPricer(
      BondFutureOption option, Dt asOf, Dt settle, double futuresPrice, DiscountCurve discountCurve, IVolatilitySurface volSurface,
      Bond ctdBond, double ctdMarketQuote, QuotingConvention ctdQuotingConvention, double ctdConversionFactor,
      double contracts)
      : base(option, asOf, settle)
    {
      FuturesQuotedPrice = futuresPrice;
      DiscountCurve = discountCurve;
      VolatilitySurface = volSurface;
      CtdBond = ctdBond;
      CtdMarketQuote = ctdMarketQuote;
      CtdQuotingConvention = ctdQuotingConvention;
      CtdConversionFactor = ctdConversionFactor;
      Contracts = contracts;
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
      // Allow no CTD bond. Some calculations don't need one. Exception is thrown when trying to calculate.
      if (CtdBond != null)
      {
        if (CtdMarketQuote <= 0.0)
          InvalidValue.AddError(errors, this, "CtdMarketQuote", String.Format("Invalid CTD market quote {0}, must be >= 0", CtdMarketQuote));
        if (CtdQuotingConvention == QuotingConvention.None)
          InvalidValue.AddError(errors, this, "CtdQuotingConvention", String.Format("Invalid CTD quoting convention {0}", CtdQuotingConvention));
        if (CtdConversionFactor <= 0.0)
          InvalidValue.AddError(errors, this, "CtdConversionFactor", String.Format("Invalid CTD conversion factor {0}, must be >= 0", CtdConversionFactor));
        // Step up bonds and floating rate bonds are not supported.
        if (CtdBond.Floating)
          InvalidValue.AddError(errors, this, "CtdBond", "Float rate CTD bonds are not supported");
        if (CtdBond.Convertible)
          InvalidValue.AddError(errors, this, "CtdBond", "Convertable CTD bonds are not supported");
        if (CtdBond.Amortizes)
          InvalidValue.AddError(errors, this, "CtdBond", "Amortizing CTD bonds are not supported");
        if (CtdBond.StepUp)
          InvalidValue.AddError(errors, this, "CtdBond", "Step up CTD bonds are not supported");
        if (CtdBond.BondType == BondType.AUSGovt)
          InvalidValue.AddError(errors, this, "CtdBond", "Aus TBond futures not supported by this pricer");
      }
    }

    /// <summary>
    /// Clear cached calculation results
    /// </summary>
    public override void Reset()
    {
      base.Reset();
      _bondFuturePricer = null;
    }

    #endregion Utility Methods

    #region Properties

    /// <summary>
    /// Time to expiration date in years
    /// </summary>
    public double Time
    {
      get { return Dt.RelativeTime(AsOf, BondFutureOption.Expiration).Value; }
    }

    /// <summary>
    /// Time to expiration date in days
    /// </summary>
    public double Days
    {
      get { return Dt.RelativeTime(AsOf, BondFutureOption.Expiration).Days; }
    }

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
    public double FuturesModelBasis { get; set; }

    /// <summary>
    /// Flat risk free rate to option expiration
    /// </summary>
    /// <remarks>
    /// <para>Continuously compounded risk free rate to option expiration date.</para>
    /// </remarks>
    public double Rfr
    {
      get { return RateCalc.Rate(DiscountCurve, BondFutureOption.Expiration); }
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
      get { return VolatilitySurface.Interpolate(BondFutureOption.Expiration, FuturesQuotedPrice, BondFutureOption.Strike); }
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
      get { return Notional / BondFutureOption.BondFuture.ContractSize; }
      set { Notional = BondFutureOption.BondFuture.ContractSize * value; }
    }

    /// <summary>
    /// Commodity forward option product
    /// </summary>
    public BondFutureOption BondFutureOption
    {
      get { return (BondFutureOption)Product; }
    }

    #region Cheapest to Deliver

    /// <summary>
    /// Basis adjustment between Ctd model price and CTD quoted price.
    /// </summary>
    public double CtdModelBasis { get; set; }

    /// <summary>
    /// The cheapest to deliver (CTD) bond
    /// </summary>
    public Bond CtdBond { get; set; }

    /// <summary>
    /// Market quote for CTD bond
    /// </summary>
    public double CtdMarketQuote { get; set; }

    /// <summary>
    /// Quoting convention for CTD bond
    /// </summary>
    public QuotingConvention CtdQuotingConvention { get; set; }

    ///<summary>
    /// Conversion factor for CTD bond
    ///</summary>
    public double CtdConversionFactor { get; set; }

    #endregion Cheapest to Deliver

    /// <summary>
    /// Pricer for underlying future
    /// </summary>
    private BondFuturePricer BondFuturePricer
    {
      get
      {
        if (_bondFuturePricer == null)
        {
          // Throw exception here if we are trying to calculate something that requires the CTD but we don't have one
          if (CtdBond == null)
            throw new ArgumentException("Cheapest to deliver bond not specified");
          _bondFuturePricer = new BondFuturePricer(BondFutureOption.BondFuture, AsOf, Settle, 1.0, FuturesQuotedPrice, DiscountCurve, CtdBond, CtdMarketQuote,
                                                   CtdQuotingConvention, CtdConversionFactor) {QuotedPrice = FuturesQuotedPrice};
        }
        return _bondFuturePricer;
      }
    }

    #region Black-Scholes Model Inputs

    /// <summary>Model time</summary>
    protected override double BlackScholesTime { get { return Time; } }
    /// <summary>Model underlying quoted price for fair value and model sensitivities</summary>
    protected override double BlackScholesUnderlyingQuotedPrice { get { return FuturesQuotedPrice; } }
    /// <summary>Model underlying implied price for option pv</summary>
    protected override double BlackScholesUnderlyingModelPrice { get { return FuturesModelPrice() - FuturesModelBasis; } }
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
    /// <para>The model price of the futures contract is the forward price of the CTD bond divided by the
    /// conversion factor.</para>
    /// <para>See <see cref="Pricers.BondFuturePricer.ModelPrice"/> for details.</para>
    /// <para>If no CTD bond is specified, the quoted futures price is returned.</para>
    /// </remarks>
    /// <returns>Model price of future</returns>
    public double FuturesModelPrice()
    {
      return BondFuturePricer.ModelPrice() * 100.0;
    }

    /// <summary>
    /// Basis between model and quoted futures price
    /// </summary>
    /// <remarks>
    /// <para>The model basis is the difference between the model futures price implied by the model forward
    /// price of the CTD bond and the conversion factor and the quoted futures price.</para>
    /// <para>See <see cref="Pricers.BondFuturePricer.ModelBasis"/> for details.</para>
    /// </remarks>
    /// <returns>Model basis</returns>
    public double FuturesImpliedModelBasis()
    {
      return BondFuturePricer.ImpliedModelBasis();
    }

    #region CTD Sensitivity Calculations

    /// <summary>
    /// Calculate the theoretical change in the option value for a 1 dollar increase (0.01) in the CTD bond price
    /// </summary>
    /// <remarks>
    /// <para>The change in the option value is driven by the change in the futures price.
    /// The theoretical change in the futures value is the change in the
    /// CTD forward bond price divided by the conversion factor times the tick value.</para>
    /// <para>The Price01 is expressed as a percentage of notional.</para>
    /// </remarks>
    /// <returns>Price 01 as a percentage of notional</returns>
    public double CtdPrice01()
    {
      return BondFuturePricer.Price01() * Delta();
    }

    /// <summary>
    /// Calculate the theoretical change in the option value for a 1bp drop in CTD bond yield
    /// </summary>
    /// <remarks>
    /// <para>The change in the option value is driven by the change in the futures price.
    /// The theoretical change in the futures value is the change in the
    /// CTD forward bond price from a 1bp drop in the CTD yield divided by the conversion
    /// factor times the tick value.</para>
    /// <para>The Pv01 is expressed as a percentage of notional.</para>
    /// </remarks>
    /// <returns>Pv01 as a percentage of notional</returns>
    public double CtdPv01()
    {
      return BondFuturePricer.Pv01() * Delta();
    }

    /// <summary>
    /// Calculate the theoretical change in the option value for a 1bp drop in the CTD term repo rate
    /// </summary>
    /// <remarks>
    /// <para>The change in the option value is driven by the change in the futures price.
    /// The Repo 01 is the theoretical change in the futures value given a 1bp drop
    /// in the term repo rate (calculated on a 25bp shift and scaled).</para>
    /// <para>The Repo 01 assumes no change in the bond price.</para>
    /// <para>The Repo01 is expressed as a percentage of notional.</para>
    /// </remarks>
    /// <param name="dayCount">Repo rate daycount</param>
    /// <returns>Repo 01 as a percentage of notional</returns>
    public double CtdRepo01(DayCount dayCount)
    {
      return BondFuturePricer.Repo01(dayCount) * Delta();
    }

    #endregion CTD Sensitivity Calculations

    #endregion Methods

    #region IVolatilitySurfaceProvider Methods

    System.Collections.Generic.IEnumerable<IVolatilitySurface> IVolatilitySurfaceProvider.GetVolatilitySurfaces()
    {
      if (VolatilitySurface != null) yield return VolatilitySurface;
    }

    #endregion IVolatilitySurfaceProvider Methods

    #region Data

    private BondFuturePricer _bondFuturePricer;

    #endregion Data
  }
}
