// 
//  -2013. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Pricer for a <see cref="BaseEntity.Toolkit.Products.StockFuture">Stock Future</see>
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.StockFuture" />
  /// <para><h2>Pricing</h2></para>
  /// <para>The futures has a market quoted price along with a model price implied by the underlying
  /// asset curve. If the futures price is used in the curve calibration then these two prices will
  /// be identical.</para>
  /// <para><b>Quoted Price</b></para>
  /// <para>The <see cref="QuotedPrice">market quoted price</see> of the future is used for margin related
  /// calculations can be specified directly,
  /// otherwise it defaults to the price implied by the underlying asset curve.</para>
  /// <para><b>Model Price</b></para>
  /// <para>The model price of the futures contract is the forward price of the underlying asset.
  /// The model price is used for sensitivity calculations. See <see cref="ModelPrice"/> for more details.</para>
  /// <para>A <see cref="ModelBasis">basis</see> between the quoted price and the model price can also
  /// be specified. A method is provided to calculate the implied model basis. See <see cref="ImpliedModelBasis"/>
  /// for more details.</para>
  /// <para><b>Sensitivities</b></para>
  /// <para>Sensitivities use the model price to capture sensitivities to the underlying market factors. This
  /// includes any factors used in calibrating the underlying asset curve.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.StockFuture">Stock Future Product</seealso>
  [Serializable]
  public class StockFuturePricer : PricerBase, IPricer, ISupportModelBasis
  {

    #region EquityFuturesSettlement

    /// <summary>
    /// Equity futures settlement payment
    /// </summary>
    [Serializable]
    private class EquityFuturesSettlement : Payment
    {
      public EquityFuturesSettlement(Dt resetDt, Dt payDt, Currency ccy, IForwardPriceCurve referenceCurve, RateModelParameters modelParameters)
        : base(payDt, ccy)
      {
        ResetDt = resetDt;
        ReferenceCurve = referenceCurve;
        ModelParameters = modelParameters;
      }
      private Dt ResetDt { get; set; }
      private RateModelParameters ModelParameters { get; set; }
      private IForwardPriceCurve ReferenceCurve { get; set; }
      protected override double ComputeAmount()
      {
        var f = ReferenceCurve.Interpolate(ResetDt);
        if (ModelParameters == null)
          return f;
        return f + ForwardAdjustment.Qadjustment(VolatilityStartDt.IsValid() ? VolatilityStartDt : ReferenceCurve.DiscountCurve.AsOf,
          ResetDt, ResetDt, PayDt, f, ReferenceCurve.DiscountCurve, ModelParameters);
      }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="future">Equity future to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="rfr">Risk free rate</param>
    /// <param name="divYield">Dividend yield</param>
    /// <param name="spotPrice">Spot price of underlying stock</param>
    /// <param name="contracts">Number of contracts</param>
    public StockFuturePricer(StockFuture future, Dt asOf, Dt settle, double rfr, double divYield, double spotPrice, double contracts)
      : base(future, asOf, settle)
    {
      var discountCurve = new DiscountCurve(asOf, rfr);
      StockCurve = new StockCurve(asOf, spotPrice, discountCurve, divYield, null);
      Contracts = contracts;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="future">Future contract</param>
    /// <param name="asOf">Pricing as of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="referenceCurve">Stock forward curve</param>
    /// <param name="contracts">Number of contracts</param>
    public StockFuturePricer(StockFuture future, Dt asOf, Dt settle, StockCurve referenceCurve, double contracts)
      : base(future, asOf, settle)
    {
      StockCurve = referenceCurve;
      Contracts = contracts;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="future">Future contract</param>
    /// <param name="asOf">Pricing as of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="referenceCurve">Stock forward curve</param>
    /// <param name="futurePrice">Quoted futures price</param>
    /// <param name="contracts">Number of contracts</param>
    public StockFuturePricer(StockFuture future, Dt asOf, Dt settle, double futurePrice, StockCurve referenceCurve, double contracts)
      : base( future, asOf, settle)
    {
      StockCurve = referenceCurve;
      QuotedPrice = futurePrice;
      Contracts = contracts;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Validate pricer inputs
    /// </summary>
    /// <remarks>
    /// This tests only relationships between fields of the pricer that
    /// cannot be validated in the property methods.
    /// </remarks> 
    /// <param name="errors">Error list </param>
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;
      base.Validate(errors);
      if (StockCurve == null && !QuotedPriceIsSpecified)
        InvalidValue.AddError(errors, this, "StockCurve", "Either a stock curve or a futures price must be specified. It is recommended to specify both");
      // Note: Allow empty stock curve for market calculations
    }

    #endregion Utilities

    #region Properties

    /// <summary>
    /// Market quote of future contract
    /// </summary>
    public double QuotedPrice
    {
      get
      {
        if (!_quotedPrice.HasValue)
          throw new ToolkitException("QuotedPrice has not been set");
        return _quotedPrice.Value;
      }
      set { _quotedPrice = value; }
    }

    /// <summary>
    /// Future price has been specified (rather than implied)
    /// </summary>
    public bool QuotedPriceIsSpecified
    {
      get { return _quotedPrice.HasValue; }
    }

    /// <summary>
    /// Basis adjustment to use for model pricing. Used to match model price to market price
    /// </summary>
    /// <remarks>
    /// <para>This is the basis used in the pricing of the contract for sensitivity calculations.
    /// This must be set explicitly. There are methods for calculating this implied basis.</para>
    /// <seealso cref="ImpliedModelBasis"/>
    /// </remarks>
    public double ModelBasis { get; set; }

    /// <summary>
    /// The position value of Model Basis
    /// </summary>
    public double ModelBasisValue
    {
      get { return StockCurve != null ? Contracts * ContractMarginValue( -ModelBasis) : 0.0; }
    }

    /// <summary>
    /// Underlying stock forward curve
    /// </summary>
    public StockCurve StockCurve { get; set; }

    /// <summary>
    /// Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return (StockCurve != null) ? StockCurve.DiscountCurve : null; }
    }

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
      get { return Notional / StockFuture.ContractSize; }
      set { Notional = StockFuture.ContractSize * value; }
    }

    /// <summary>
    /// Future being priced
    /// </summary>
    public StockFuture StockFuture
    {
      get { return (StockFuture)Product; }
    }

    /// <summary>
    /// Accessor for model parameters used for convexity adjustments
    /// </summary>
    public RateModelParameters FwdModelParameters { get; private set; }

    /// <summary>
    /// The Payment pricer
    /// </summary>
    public override IPricer PaymentPricer
    {
      get { return null; }
    }

    #endregion Properties

    #region Methods

    /// <summary>
    /// Net present value of the product, excluding the value of any additional payment.
    /// </summary>
    /// <remarks>
    ///   <para>For futures this is the number of contracts times the value of each
    ///   futures contract given the model price minus the model basis.</para>
    ///   <formula>
    ///     V = \text{MarginValue}( F^m - ModelBasis ) * Contracts
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">F^m</formula> is the implied futures price</description></item>
    ///     <item><description><formula inline="true">ModelBasis</formula> is the basis between the quoted futures price and the model futures price</description></item>
    ///     <item><description><formula inline="true">Contracts</formula> is the number of futures contracts</description></item>
    ///   </list>
    ///   <note>The model basis must be explicitly set and is not automatically calculated. There are methods for calculating the model basis.</note>
    ///   <note>If the <see cref="StockCurve"/> is not set then the <see cref="QuotedPrice"/> is used.</note>
    /// </remarks>
    /// <seealso cref="ImpliedModelBasis"/>
    /// <seealso cref="ModelBasis"/>
    /// <seealso cref="ModelPrice"/>
    /// <returns>PV of product</returns>
    public override double ProductPv()
    {
      if (StockFuture.LastTradingDate != Dt.Empty && Settle > StockFuture.LastTradingDate)
        return 0.0;
      return Contracts * ContractMarginValue(StockCurve != null ? (ModelPrice() - ModelBasis) : QuotedPrice);
    }

    /// <summary>
    /// Model implied price of future
    /// </summary>
    /// <remarks>
    ///   <para>The model price of the futures contract is the forward price implied from the underlying curve.</para>
    ///   <note>If the <see cref="StockCurve"/> is not set then the <see cref="QuotedPrice"/> is returned.</note>
    /// </remarks>
    /// <returns>Model price of future</returns>
    public double ModelPrice()
    {
      if (StockCurve == null)
        throw new ArgumentException("The StockCurve must be specified for model calculations.");
      return ((IEnumerable<Payment>) GetPaymentSchedule(null, AsOf)).Aggregate(0.0, (pv, pay) => pv + pay.Amount);
    }

    /// <summary>
    /// Basis between model and quoted futures price
    /// </summary>
    /// <remarks>
    ///   <para>The model basis is the difference between the model implied futures price and the quoted futures price.</para>
    ///   <formula>
    ///     Basis = F^m - Price
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">F^m</formula> is the implied futures price</description></item>
    ///     <item><description><formula inline="true">Price</formula> is the quoted futures price</description></item>
    ///   </list>
    ///   <para>This method calculates the implied basis. The actual basis used during pricing must be explicitly set. The
    ///   default for the basis used in pricing is 0.</para>
    ///   <seealso cref="ModelBasis"/>
    /// </remarks>
    /// <returns>Model basis</returns>
    public double ImpliedModelBasis()
    {
      if (!QuotedPriceIsSpecified || StockCurve == null)
        throw new ToolkitException("QuotedPrice and StockCurve must be set to calculate the implied model basis");
      return QuotedPriceIsSpecified ? ModelPrice() - QuotedPrice : 0.0;
    }

    /// <summary>
    /// The total nominal value of the future contract based on the current market quote
    /// </summary>
    /// <returns>Total value</returns>
    public double Value()
    {
      return _quotedPrice.HasValue ? Contracts * ContractMarginValue(_quotedPrice.Value) : 0.0;
    }

    /// <summary>
    /// Value of a single tick per contract. Generally this is the tick size times the contract size.
    /// </summary>
    /// <returns>Value of a single tick</returns>
    public double TickValue()
    {
      return StockFuture.TickValue;
    }

    /// <summary>
    /// Value of a single point. This is the tick value divided by the tick size
    /// </summary>
    /// <returns>Value of a point</returns>
    public double PointValue()
    {
      return StockFuture.PointValue;
    }

    /// <summary>
    /// Return commodity rate on startDate
    /// </summary>
    /// <returns>Forward stock price at As-Of date</returns>
    public double SpotPrice()
    {
      return StockCurve != null ? StockCurve.F(AsOf, AsOf) : QuotedPrice;
    }

    /// <summary>
    /// Sensitivity of futures value to underlying futures price.
    /// </summary>
    /// <remarks>
    /// <para>This is provided as a convenience comparison to options on Stock or stock index futures.</para>
    /// </remarks>
    /// <returns>Futures delta</returns>
    public double Delta()
    {
      return Contracts * ContractMarginValue(1.0);
    }

    #region Margin Calculations

    /// <summary>
    /// Margin payment
    /// </summary>
    /// <remarks>
    ///   <para>Calculated margin payment for traded contracts from previous futures
    ///   price to current futures price</para>
    ///   <para>The margin is:</para>
    ///   <formula>
    ///     M = \left( V(F_t) - V(F_{t-1} \right) * C
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">M</formula> is the futures margin</description></item>
    ///     <item><description><formula inline="true">V(F_t)</formula> is the current futures contract value</description></item>
    ///     <item><description><formula inline="true">V(F_{t-1})</formula> is the previous futures contract value</description></item>
    ///     <item><description><formula inline="true">C</formula> is the number of contracts</description></item>
    ///   </list>
    /// </remarks>
    /// <param name="prevPrice">Previous quoted futures price</param>
    public double Margin(double prevPrice)
    {
      if (!QuotedPriceIsSpecified)
        throw new ToolkitException("Quoted price must be specified for margin calculations");
      return (ContractMarginValue(QuotedPrice) - ContractMarginValue(prevPrice)) * Contracts;
    }

    /// <summary>
    /// Value of each futures contract for margin calculation
    /// </summary>
    /// <remarks>
    ///   <para>The futures contract value is the current price value for the quoted futures price.</para>
    ///   <para>For most futures contracts the value for each contract is rounded to a cent.</para>
    /// </remarks>
    /// <param name="price">Futures price</param>
    /// <returns>Futures contract value</returns>
    public double ContractMarginValue(double price)
    {
      return price / StockFuture.TickSize * StockFuture.TickValue;
    }

    /// <summary>
    /// Margin value as a percentage of notional
    /// </summary>
    /// <param name="price">Quote</param>
    /// <returns>Margin value as a percenage of notional</returns>
    public double PercentageMarginValue(double price)
    {
      return ContractMarginValue(price) / StockFuture.ContractSize;
    }

    #endregion Margin Calculations

    #region Payment Calculations

    /// <summary>
    /// Generate payment schedule
    /// </summary>
    /// <remarks>
    /// <para>Because of the marking to the market mechanism the cashflows of a future are really daily settlements reflecting the 
    /// changes in the market rate/price. The generated payment schedule is an equivalent one time interest payment that pays the value of the 
    /// futures at maturity, i.e. the expected value of its payoff under the risk neutral measure.</para>
    /// </remarks>
    /// <param name="ps">payment schedule</param>
    /// <param name="from">Start date for generation of payments</param>
    /// <returns>Payment schedule for the ed future</returns>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from)
    {
      if (ps == null)
        ps = new PaymentSchedule();
      else
        ps.Clear();
      if (from > StockFuture.LastDeliveryDate)
        return ps;
      var p = new EquityFuturesSettlement(StockFuture.LastDeliveryDate, StockFuture.LastDeliveryDate, StockFuture.Ccy, StockCurve, FwdModelParameters);
      ps.AddPayment(p);
      return ps;
    }

    /// <summary>
    /// Present value of any additional payment associated with the pricer.
    /// </summary>
    /// <remarks>
    ///   <para>The reason that a "payment" pv is needed is that in risk measure term, Pv of rate
    ///   futures are defined as contract value reflecting market implied price compared against
    ///   exchange closing price, while in curve calibration or some other matters, 
    ///   Pv of rate futures is purely the contract value reflecting market implied price; thus
    ///   putting the comparing reference into "payment" pv is an effective workaround to address
    ///   the double needs in toolkit pricer.</para>
    /// </remarks>
    ///<returns></returns>
    public override double PaymentPv()
    {
      if (StockFuture.LastTradingDate != Dt.Empty && Settle >= StockFuture.LastTradingDate)
        return 0.0;
      return (Payment != null) ? CurrentNotional * Payment.Amount : 0.0;
    }

    ///<summary>
    /// Generate a "fake" payment to properly allocation PnL between MTM and CF PL, the payment is future contract value based on price level
    ///</summary>
    ///<param name="tradeSettle">Trade settlement date</param>
    ///<param name="tradedLevel">Traded price level</param>
    ///<param name="ccy">Currency of trade</param>
    ///<returns></returns>
    public Payment GeneratePayment(Dt tradeSettle, double tradedLevel, Currency ccy)
    {
      return new UpfrontFee(tradeSettle, -1 * PercentageMarginValue(tradedLevel), ccy);
    }

    #endregion Payment Calculations

    #endregion

    #region Data

    private double? _quotedPrice;

    #endregion
  }
}
