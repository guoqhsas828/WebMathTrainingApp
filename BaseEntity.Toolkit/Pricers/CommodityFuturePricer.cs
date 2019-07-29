// 
//  -2013. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
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
  /// Pricer for a <see cref="BaseEntity.Toolkit.Products.CommodityFuture">Commodity Future</see>
  /// </summary>
  /// <remarks>
  /// <para><b>Commodity Futures</b></para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.CommodityFuture" />
  /// <para><b>Pricing</b></para>
  /// <para>The futures has a market quoted price along with a model price implied by the underlying
  /// asset curve. If the futures price is used in the curve calibration then these two prices will
  /// be identical.</para>
  /// <para><b>Quoted Price</b></para>
  /// <para>The <see cref="QuotedPrice">market quoted price</see> of the future can be specified directly,
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
  /// <seealso cref="BaseEntity.Toolkit.Products.CommodityFuture">Commodity Future Product</seealso>
  [Serializable]
  public class CommodityFuturesPricer : PricerBase, IPricer, ISupportModelBasis
  {
    #region CommodityFuturesSettlement

    [Serializable]
    private class CommodityFuturesSettlement : Payment
    {
      public CommodityFuturesSettlement(Dt resetDt, Dt payDt, Currency ccy, IForwardPriceCurve referenceCurve, RateModelParameters modelParameters)
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
        return f +
               ForwardAdjustment.Qadjustment(VolatilityStartDt.IsValid() ? VolatilityStartDt : ReferenceCurve.DiscountCurve.AsOf, ResetDt, ResetDt, PayDt, f,
                                             ReferenceCurve.DiscountCurve, ModelParameters);
      }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="future">Commodity forward to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="rfr">Risk free rate</param>
    /// <param name="leaseRate">Lease rate (continuous comp, eg. 0.05)</param>
    /// <param name="spotPrice">Spot price of underlying commodity</param>
    /// <param name="contracts">Number of contracts</param>
    public CommodityFuturesPricer(CommodityFuture future, Dt asOf, Dt settle, double rfr, double leaseRate, double spotPrice, double contracts)
      : base(future, asOf, settle)
    {
      var discountCurve = new DiscountCurve(asOf, rfr);
      CommodityCurve = new CommodityCurve(asOf, spotPrice, discountCurve, leaseRate, null);
      Contracts = contracts;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="future">Future contract</param>
    /// <param name="asOf">Pricing as of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="commodityCurve">Commodity curve</param>
    /// <param name="contracts">Number of contracts</param>
    public CommodityFuturesPricer(CommodityFuture future, Dt asOf, Dt settle, CommodityCurve commodityCurve, double contracts)
      : base(future, asOf, settle)
    {
      CommodityCurve = commodityCurve;
      Contracts = contracts;
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
      //allow empty commodity curve for market calculations
    }

    /// <summary>
    /// Clear cached calculation results
    /// </summary>
    public override void Reset()
    {
      base.Reset();
      _quotedPrice = null;
    }

    #endregion Utility Methods
    
    #region Properties

    /// <summary>
    /// Market quoted price of futures contract
    /// </summary>
    /// <summary>
    /// <para>If not specified, implied from <see cref="CommodityCurve"/></para>
    /// </summary>
    public double QuotedPrice
    {
      get { return _quotedPrice.HasValue ? _quotedPrice.Value : ImpliedSettlement(); }
      set { _quotedPrice = value; }
    }

    /// <summary>
    /// Adjustment to use for model pricing. Used to match model futures price to market futures price
    /// </summary>
    /// <remarks>
    /// <para>This is the basis used in the pricing of the contract to match quote (if available).
    /// This must be set explicitly. There are methods for calculating this implied basis.</para>
    /// <seealso cref="ImpliedModelBasis"/>
    /// </remarks>
    public double ModelBasis { get; set; }

    /// <summary>
    /// The position value of Model Basis
    /// </summary>
    public double ModelBasisValue
    {
      get { return ModelBasis.AlmostEquals(0.0) ? 0.0 : Contracts * ContractMarginValue(-ModelBasis); }
    }

    /// <summary>
    /// Underying commodity curve
    /// </summary>
    public CommodityCurve CommodityCurve { get; private set; }

    /// <summary>
    /// Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve { get { return (CommodityCurve != null) ? CommodityCurve.DiscountCurve : null; } }

    /// <summary>
    /// Accessor for model parameters used for convexity adjustments
    /// </summary>
    public RateModelParameters FwdModelParameters { get; set; }

    /// <summary>
    /// The Payment pricer
    /// </summary>
    public override IPricer PaymentPricer
    {
      get { return null; }
    }

    ///<summary>
    /// The price used to calculate future contract PnL
    ///</summary>
    public double Price
    {
      get { return _quotedPrice.HasValue ? _quotedPrice.Value : ImpliedSettlement(); }
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
      get { return Notional / CommodityFuture.ContractSize; }
      set { Notional = CommodityFuture.ContractSize * value; }
    }

    /// <summary>
    /// Future being priced
    /// </summary>
    public CommodityFuture CommodityFuture
    {
      get { return (CommodityFuture)Product; }
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
    ///   <para>Note that the model basis must be explicitly set. There are methods for calculating the model basis.</para>
    ///   <seealso cref="ImpliedModelBasis"/>
    ///   <seealso cref="ModelBasis"/>
    /// </remarks>
    /// <seealso cref="ModelPrice"/>
    /// <returns>PV of product</returns>
    public override double ProductPv()
    {
      if (_quotedPrice.HasValue)
        return ContractMarginValue(_quotedPrice.Value) * Contracts;
      return ContractMarginValue(ModelPrice() - ModelBasis) * Contracts;
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
    /// Model implied price of future
    /// </summary>
    /// <remarks>
    ///   <para>The model price of the futures contract is the forward price implied from the commodity curve.</para>
    /// </remarks>
    /// <returns>Model price of future</returns>
    public double ModelPrice()
    {
      return ImpliedSettlement();
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
      if (!_quotedPrice.HasValue || CommodityCurve == null)
        throw new ToolkitException("QuotedPrice and CommodityCurve must be set to calculate the implied model basis");
      return _quotedPrice.HasValue ? ModelPrice() - QuotedPrice : 0.0;
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
      return price / CommodityFuture.TickSize * CommodityFuture.TickValue;
    }

    /// <summary>
    /// Margin value as a percentage of notional
    /// </summary>
    /// <param name="price">Quote</param>
    /// <returns>Margin value as a percenage of notional</returns>
    public double PercentageMarginValue(double price)
    {
      return ContractMarginValue(price) / CommodityFuture.ContractSize;
    }

    #endregion Margin Calculations

    ///<summary>
    /// Present value 
    ///</summary>
    ///<returns></returns>
    public override double Pv()
    {
      var p = (FutureBase)Product;
      if (p.LastTradingDate != Dt.Empty && Settle >= p.LastTradingDate)
        return 0.0;
      return ProductPv() + PaymentPv();
    }

    /// <summary>
    /// Implied futures commodity price based on market information
    /// </summary>
    /// <returns>The implied forward rate </returns>
    public double ImpliedSettlement()
    {
      if (CommodityCurve == null)
        throw new ArgumentException("Require non null Commodity Curve for model calculations");
      return ((IEnumerable<Payment>)GetPaymentSchedule(null, AsOf)).Aggregate(0.0,
                                                                              (pv, pay) =>
                                                                              pv + pay.Amount );
    }

    ///<summary>
    /// Present value of any additional payment associated with the pricer.
    /// The reason that a "payment" pv is needed is that in risk measure term, Pv of rate futures are defined as contract value reflecting
    /// market implied price compared against exchange closing price, while in curve calibration or some other matters, 
    /// Pv of rate futures is purely the contract value reflecting market implied price; thus putting the comparing reference into "payment" pv
    /// is an effective workaround to address the double needs in toolkit pricer
    /// 
    ///</summary>
    ///<returns></returns>
    public override double PaymentPv()
    {
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

    ///<summary>
    /// The method return the market value of future contract based on provided market quote
    ///</summary>
    ///<param name="quote">Market quote</param>
    ///<returns>Mark-to-market value</returns>
    public double MtM(double quote)
    {
     return Contracts * ContractMarginValue(quote) + PaymentPv();
    }

    /// <summary>
    /// Value of a single tick per contract. Generally this is the tick size times the contract size.
    /// </summary>
    /// <returns>Value of a single tick</returns>
    public double TickValue()
    {
      return CommodityFuture.TickValue;
    }

    /// <summary>
    /// Value of a single point. This is the tick value divided by the tick size
    /// </summary>
    /// <returns>Value of a point</returns>
    public double PointValue()
    {
      return CommodityFuture.PointValue;
    }

    /// <summary>
    /// Return commodity rate on startDate
    /// </summary>
    /// <returns>Forward fx rate at ValueDate</returns>
    public double CommoditySpotPrice()
    {
      return CommodityCurve.CommoditySpotPrice;
    }

    /// <summary>
    /// Return forward commodity rate on startDate
    /// </summary>
    /// <returns>Forward points at ValueDate</returns>
    public double ForwardPoints()
    {
      return (ImpliedSettlement() - CommoditySpotPrice());
    }

    /// <summary>
    /// Generate payment schedule
    /// </summary>
    /// <param name="ps">payment schedule</param>
    /// <param name="from">Start date for generation of payments</param>
    /// <returns>Payment schedule for the ed future</returns>
    /// <remarks>Because of the marking to the market mechanism the cashflows of a future are really daily settlements reflecting the 
    /// changes in the market rate/price. The generated payment schedule is an equivalent one time interest payment that pays the value of the 
    /// futures at maturity, i.e. the expected value of its payoff under the risk neutral measure.
    /// </remarks>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from)
    {
      if (ps == null)
        ps = new PaymentSchedule();
      else 
        ps.Clear(); 
      var future = CommodityFuture;
      var maturity = Dt.Roll(future.LastDeliveryDate, future.BDConvention, future.Calendar);
      if (from > maturity)
        return ps;
      var p = new CommodityFuturesSettlement(future.LastDeliveryDate, maturity, future.Ccy, CommodityCurve, FwdModelParameters);
      if (future.SettlementResets != null)
      {
        if (future.SettlementResets.HasCurrentReset)
          p.Amount = future.SettlementResets.CurrentReset;
      }
      ps.AddPayment(p);
      return ps;
    }

    #endregion

    #region Data

    private double? _quotedPrice;

    #endregion Data
  }
}
