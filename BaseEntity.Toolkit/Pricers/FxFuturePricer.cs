// 
//  -2013. All rights reserved.
// 

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Pricer for a <see cref="BaseEntity.Toolkit.Products.FxFuture">Fx Future</see>
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.FxFuture" />
  /// 
  /// <para><h2>Pricing</h2></para>
  /// <para>The futures has a market quoted price along with a model price implied by the underlying
  /// fx curve. If the futures price is used in the curve calibration then these two prices will
  /// be identical.</para>
  /// <para><b>Quoted Price</b></para>
  /// <para>The <see cref="QuotedPrice">market quoted price</see> of the future can be specified directly,
  /// otherwise it defaults to the price implied by the fx curve.</para>
  /// <para><b>Model Price</b></para>
  /// <para>The model price of the futures contract is the forward price of the underlying asset.
  /// The model price is used for sensitivity calculations. See <see cref="ModelPrice"/> for more details.</para>
  /// <para>A <see cref="ModelBasis">basis</see> between the quoted price and the model price can also
  /// be specified. A method is provided to calculate the implied model basis. See <see cref="ImpliedModelBasis"/>
  /// for more details.</para>
  /// <para><b>Sensitivities</b></para>
  /// <para>Sensitivities use the model price to capture sensitivities to the underlying market factors. This
  /// includes any factors used in calibrating the underlying curves.</para>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.FxFuture">Fx Future Product</seealso>
  [Serializable]
  public class FxFuturePricer : PricerBase, IPricer
  {
    #region FxFuturesSettlement

    [Serializable]
    private class FxFuturesSettlement : Payment
    {
      public FxFuturesSettlement(Dt resetDt, Dt payDt, Currency ccy, Currency ccy1, Currency ccy2, FxCurve referenceCurve, RateModelParameters modelParameters)
        : base(payDt, ccy)
      {
        ResetDt = resetDt;
        Ccy1 = ccy1;
        Ccy2 = ccy2;
        ReferenceCurve = referenceCurve;
        ModelParameters = modelParameters;
      }

      private Dt ResetDt { get; set; }
      private Currency Ccy1 { get; set; }
      private Currency Ccy2 { get; set; }
      private RateModelParameters ModelParameters { get; set; }
      private FxCurve ReferenceCurve { get; set; }

      protected override double ComputeAmount()
      {
        var f = FxUtil.ForwardFxRate(ResetDt, Ccy1, Ccy2, ReferenceCurve, null);
        if (ModelParameters == null)
          return f;
        var discountCurve = (Ccy2 == ReferenceCurve.Ccy2) ? ReferenceCurve.Ccy2DiscountCurve : ReferenceCurve.Ccy1DiscountCurve;
        if (discountCurve == null)
          return f;
        return f + ForwardAdjustment.Qadjustment(VolatilityStartDt.IsValid() ? VolatilityStartDt : discountCurve.AsOf, ResetDt, ResetDt, PayDt, f,
                                                 discountCurve, ModelParameters);
      }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <remarks>
    ///   <para>Discount curve is defaulted to discount curve for fx ccy1.</para>
    /// </remarks>
    /// <param name="future">Futures contract</param>
    /// <param name="asOf">Pricing (asOf) date</param>
    /// <param name="settle">Spot settlement date</param>
    /// <param name="valuationCcy">Currency to value trade in (or none for fx pay currency)</param>
    /// <param name="fxCurve">Fx curve</param>
    /// <param name="contracts">Number of contracts</param>
    public FxFuturePricer(FxFuture future, Dt asOf, Dt settle, Currency valuationCcy, FxCurve fxCurve, double contracts)
      : base(future, asOf, settle)
    {
      Contracts = contracts;
      _valuationCurrency = (valuationCcy != Currency.None) ? valuationCcy : fxCurve.Ccy1;
      DiscountCurve = FxUtil.DiscountCurve(_valuationCurrency, fxCurve, null);
      FxCurve = fxCurve;
    }

    #endregion Constructors

    #region Utilities

    /// <summary>
    /// Validate pricer inputs
    /// </summary>
    /// <param name="errors">Error list </param>
    /// <remarks>
    /// This tests only relationships between fields of the pricer that
    /// cannot be validated in the property methods.
    /// </remarks> 
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;
      base.Validate(errors);
      // Allow empty discount and FX curves for model calculations
    }

    #endregion Utilities

    #region Properties

    ///<summary>
    /// Market quote of future contract
    ///</summary>
    public double QuotedPrice { get; set; }

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
    /// Currency of the Pv calculation
    /// </summary>
    /// <remarks>
    /// <para>If not specified, Cc1 of <see cref="FxCurve"/> is used.</para>
    /// </remarks>
    public override Currency ValuationCurrency
    {
      get { return (_valuationCurrency != Currency.None) ? _valuationCurrency : FxCurve.Ccy1; }
    }

    /// <summary>
    /// Discount curve of valuation currency
    /// </summary>
    public DiscountCurve DiscountCurve { get; set; }

    /// <summary>
    /// Fx Curve
    /// </summary>
    public FxCurve FxCurve { get; set; }

    /// <summary>
    /// Rate model parameters for convexity adjustments
    /// </summary>
    public RateModelParameters RateModelParameters { get; set; }

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
      get { return Notional / FxFuture.ContractSize; }
      set { Notional = FxFuture.ContractSize * value; }
    }

    /// <summary>
    /// Future being priced
    /// </summary>
    public FxFuture FxFuture
    {
      get { return (FxFuture)Product; }
    }

    #endregion Properties

    #region Methods

    /// <summary>
    /// Net present value of the product, excluding the value of any additional payment.
    /// </summary>
    /// <remarks>
    ///   <para>This is the number of contracts times the value of each
    ///   futures contract given the model price minus the model basis.The model price
    ///   of the futures contract is implied from the forward model price of the underlying.</para>
    ///   <formula>
    ///     V = \text{Margin}( F^m - B_m ) * N
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">F^m</formula> is the implied futures price from the fx curve</description></item>
    ///     <item><description><formula inline="true">B_m</formula> is the basis between the quoted futures price and the model futures price</description></item>
    ///     <item><description><formula inline="true">N</formula> is the number of futures contracts</description></item>
    ///   </list>
    ///   <para>Note that the model basis must be explicitly set. There are methods for calculating the model basis.</para>
    ///   <seealso cref="ImpliedModelBasis"/>
    ///   <seealso cref="ModelBasis"/>
    /// </remarks>
    /// <seealso cref="ModelPrice"/>
    /// <returns>PV of product</returns>
    public override double ProductPv()
    {
      if (!IsActive())
        return 0.0;
      return ContractMarginValue(ModelPrice() - ModelBasis) * Contracts;
    }

    /// <summary>
    /// Model implied price of future
    /// </summary>
    /// <remarks>
    ///   <para>The model price of the future contract is the forward price of the deliverable commodity.</para>
    ///   <para>With no market friction, forward exchange rates are implied
    ///   by the relative deposit rates in each currency.</para>
    ///   <para>For example, investing in a domestic deposit is equivalent to
    ///   exchanging money at the spot rate, investing in a foreign deposit and
    ///   exchanging back the foreign deposit proceeds at the forward exchange rate.</para>
    ///   <formula>
    ///     \left( 1 + r_d * \frac{t}{360} \right) = \frac{fx_fwd}{fx_spot} * \left( 1 + r_f * \frac{t}{360} \right)
    ///   </formula>
    ///   <para>or</para>
    ///   <formula>
    ///     fx_fwd = fx_spot * \frac{\left( 1 + r_d * \frac{t}{360} \right)}{\left( 1 + r_f * \frac{t}{360} \right)}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><formula inline="true">r_d</formula> is the domestic deposit rate</description></item>
    ///     <item><description><formula inline="true">r_f</formula> is the foreign deposit rate</description></item>
    ///     <item><description><formula inline="true">t</formula> is the number of days</description></item>
    ///	    <item><description><formula inline="true">fx_spot</formula> is the spot exchange rate (domestic/foreign)</description></item>
    ///		  <item><description><formula inline="true">fx_fwd</formula> is the forward exchange rate (domestic/foreign)</description></item>
    ///   </list>
    ///   <para>Taking into account intermediate cashflows, credit and market friction requires are
    ///   more complex parity relationship that includes cross currency basis swaps.</para>
    /// </remarks>
    /// <returns>Model price of future</returns>
    public double ModelPrice()
    {
      if (FxCurve == null)
        throw new ArgumentException("Require non null FxCurve for model calculations");
      return ((IEnumerable<Payment>)GetPaymentSchedule(null, AsOf)).Aggregate(0.0, (pv, pay) => pv + pay.Amount);
    }

    /// <summary>
    /// Basis between model and quoted futures price
    /// </summary>
    /// <remarks>
    ///   <para>The model basis is the difference between the model futures price and the quoted futures price.</para>
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
      return ModelPrice() - QuotedPrice;
    }

    /// <summary>
    /// The total nominal value of the future contract based on the current market quote
    /// </summary>
    /// <returns>Total value</returns>
    public double Value()
    {
      return Contracts * ContractMarginValue();
    }

    /// <summary>
    /// Return spot fx rate
    /// </summary>
    /// <param name="ccy1">Base (domestic/base/unit/transaction/source/to) currency</param>
    /// <param name="ccy2">Quoting (foreign/quote/price/payment/destination/from) currency</param>
    /// <returns>Spot fx rate</returns>
    public double SpotFxRate(Currency ccy1, Currency ccy2)
    {
      return FxUtil.SpotFxRate(ccy1, ccy2, FxCurve, null);
    }

    /// <summary>
    /// Return forward fx rate on ValueDate
    /// </summary>
    /// <param name="ccy1">Base (domestic/base/unit/transaction/source/to) currency</param>
    /// <param name="ccy2">Quoting (foreign/quote/price/payment/destination/from) currency</param>
    /// <returns>Forward fx rate at ValueDate</returns>
    public double ForwardFxRate(Currency ccy1, Currency ccy2)
    {
      return FxUtil.ForwardFxRate(FxFuture.LastDeliveryDate, ccy1, ccy2, FxCurve, null);
    }

    /// <summary>
    /// Return forward fx points on ValueDate
    /// </summary>
    /// <param name="ccy1">Base (domestic/base/unit/transaction/source/to) currency</param>
    /// <param name="ccy2">Quoting (foreign/quote/price/payment/destination/from) currency</param>
    /// <returns>Forward fx points at ValueDate</returns>
    public double ForwardFxPoints(Currency ccy1, Currency ccy2)
    {
      return (ForwardFxRate(ccy1, ccy2) - SpotFxRate(ccy1, ccy2)) * 10000.0;
    }

    /// <summary>
    /// Amount paid in pay currency
    /// </summary>
    /// <returns>Amount paid in pay currency</returns>
    public double PayAmount()
    {
      return Notional * QuotedPrice;
    }

    /// <summary>
    /// Amount received in receive currency
    /// </summary>
    /// <returns>Amount received in receive currency</returns>
    public double ReceiveAmount()
    {
      return Notional;
    }

    /// <summary>
    /// Valuation currency discount factor from ValueDate to pricing date
    /// </summary>
    /// <returns>Valuation currency discount factor from ValueDate</returns>
    public double DiscountFactor()
    {
      return DiscountCurve.DiscountFactor(FxFuture.LastDeliveryDate);
    }

    /// <summary>
    /// Value of a single tick per contract. Generally this is the tick size times the contract size.
    /// </summary>
    /// <returns>Value of a single tick</returns>
    public double TickValue()
    {
      return FxFuture.TickValue;
    }

    /// <summary>
    /// Value of a single point. This is the tick value divided by the tick size
    /// </summary>
    /// <returns>Value of a point</returns>
    public double PointValue()
    {
      return FxFuture.PointValue;
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
      return price / FxFuture.TickSize * FxFuture.TickValue;
    }

    /// <inheritdoc cref="ContractMarginValue(double)" />
    public double ContractMarginValue()
    {
      return ContractMarginValue(QuotedPrice);
    }

    /// <summary>
    /// Margin value as a percentage of notional
    /// </summary>
    /// <param name="price">Quote</param>
    /// <returns>Margin value as a percenage of notional</returns>
    public double PercentageMarginValue(double price)
    {
      return ContractMarginValue(price) / FxFuture.ContractSize;
    }

    /// <inheritdoc cref="PercentageMarginValue(double)" />
    public double PercentageMarginValue()
    {
      return PercentageMarginValue(QuotedPrice);
    }

    #endregion Margin Calculations

    /// <summary>
    /// Generate payment schedule
    /// </summary>
    /// <param name="ps">payment schedule</param>
    /// <param name="from">Start date for generation of payments</param>
    /// <returns>Payment schedule for the ed future</returns>
    /// <remarks>Because of the marking to the market mechanism the cashflows of a future are really daily settlements reflecting the 
    /// changes in the market rate/price. The generated payment schedule is an equivalent one time interest payment that pays the value of the 
    /// futures at maturity, i.e. the expected value of its payoff under the risk neutral measure.
    ///   
    /// </remarks>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from)
    {
      if (ps == null)
        ps = new PaymentSchedule();
      else
        ps.Clear();
      var future = FxFuture;
      if (from > future.LastDeliveryDate)
        return ps;
      var p = new FxFuturesSettlement(future.LastDeliveryDate, future.LastDeliveryDate, ValuationCurrency, future.ReceiveCcy, future.PayCcy, FxCurve,
                                      RateModelParameters);
      ps.AddPayment(p);
      return ps;
    }

    #endregion Methods

    #region Data

    private readonly Currency _valuationCurrency;

    #endregion
  }
}
