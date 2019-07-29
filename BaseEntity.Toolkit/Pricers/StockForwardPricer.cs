// 
// StockForwardPricer.cs
//   2014. All rights reserved.
// 

using System;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Pricer for a <see cref="BaseEntity.Toolkit.Products.StockForward">Stock Forward</see>
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.StockForward" />
  /// </remarks>
  public sealed class StockForwardPricer : PricerBase, IPricer
  {
    #region StockForwardSettlement
    [Serializable]
    private class StockForwardSettlement : Payment
    {
      public StockForwardSettlement(Dt resetDt, Dt deliveryDt, double deliveryPrice, double modelBasis, StockCurve curve)
        : base(deliveryDt, curve.Ccy)
      {
        ResetDt = resetDt;
        ModelBasis = modelBasis;
        DeliveryPrice = deliveryPrice;
        StockCurve = curve;
      }

      public override bool IsProjected
      {
        get { return !AmountOverride.HasValue; }
      }

      private Dt ResetDt { get; set; }

      private double ModelBasis { get; set; }

      private double DeliveryPrice { get; set; }

      private StockCurve StockCurve { get; set; }

      protected override double ComputeAmount()
      {
        return StockCurve.Interpolate(ResetDt) - ModelBasis - DeliveryPrice;
      }
    }

    #endregion

    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="product">Stock forward to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="rfr">Risk free rate</param>
    /// <param name="divYield">Dividend yield</param>
    /// <param name="spotPrice">Spot price of underlying commodity</param>
    public StockForwardPricer(StockForward product, Dt asOf, Dt settle, double spotPrice, double rfr, double divYield)
      : base(product, asOf, settle)
    {
      DiscountCurve = new DiscountCurve(asOf, rfr);
      StockCurve = new StockCurve(asOf, spotPrice, DiscountCurve, divYield, null);
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="product">Stock forward</param>
    /// <param name="asOf">As of date</param>
    /// <param name="settle">Settle</param>
    /// <param name="notional">Notional</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="stockCurve">Term structure of stock forward prices</param>
    public StockForwardPricer(StockForward product, Dt asOf, Dt settle, double notional, DiscountCurve discountCurve, StockCurve stockCurve)
      : base(product, asOf, settle)
    {
      Notional = notional;
      StockCurve = stockCurve;
      DiscountCurve = discountCurve;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Validate
    /// </summary>
    /// <param name="errors"></param>
    public override void Validate(System.Collections.ArrayList errors)
    {
      base.Validate(errors);
      if (StockCurve == null)
        InvalidValue.AddError(errors, this, "StockCurve", String.Format("Invalid reference curve. Cannot be null"));
      if (DiscountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));
    }

    /// <summary>
    /// Clear cached calculation results
    /// </summary>
    public override void Reset()
    {
      base.Reset();
      _quotedPrice = null;
    }

    #endregion
    
    #region Properties

    /// <summary>
    /// Market quoted price of forward
    /// </summary>
    public double QuotedPrice
    {
      get { return _quotedPrice.HasValue ? _quotedPrice.Value : ModelPrice(); }
      set { _quotedPrice = value; }
    }

    /// <summary>
    /// Basis adjustment to use for model pricing. Used to match model price to market price
    /// </summary>
    /// <remarks>
    /// <para>This is the basis used in the pricing of the forward contract for sensitivity calculations.
    /// This must be set explicitly. There are methods for calculating this implied basis.</para>
    /// <seealso cref="ImpliedModelBasis"/>
    /// </remarks>
    public double ModelBasis { get; set; }
    
    /// <summary>
    /// Term structure of forward stock prices
    /// </summary>
    public StockCurve StockCurve { get; private set; }

    /// <summary>
    /// Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve { get; private set; }

    /// <summary>
    /// Product
    /// </summary>
    public StockForward StockForward { get { return (StockForward)Product; } }

    /// <summary>
    /// Overridden resets
    /// </summary>
    public RateResets SettlementResets { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// Present Value
    /// </summary>
    /// <returns>Pv</returns>
    public override double ProductPv()
    {
      return Notional * GetPaymentSchedule(null, AsOf).Pv(AsOf, Settle, StockCurve.DiscountCurve, null, false, false);
    }

    /// <summary>
    /// Model implied price of forward
    /// </summary>
    /// <remarks>
    ///   <para>The model price of the forward contract is the forward price of the stock.</para>
    ///   <para>The forward price of the deliverable is calculated from the spot price of the stock, the
    ///   funding cost, and the dividend yield.</para>
    ///   <para>The model forward price is:</para>
    ///   <formula>
    ///     F_T^m = S e^{(r-\delta)T}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">F_T^m</formula> is the model forward price</description></item>
    ///			<item><description><formula inline="true">S</formula> is the spot price of the deliverable commodity</description></item>
    ///     <item><description><formula inline="true">r</formula> is the funding rate</description></item>
    ///     <item><description><formula inline="true">\delta</formula> is the dividend yield</description></item>
    ///     <item><description><formula inline="true">T</formula> is the years till expiration</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Model forward price</returns>
    public double ModelPrice()
    {
      return StockCurve.Interpolate(StockForward.FixingDate.IsEmpty() ? StockForward.DeliveryDate : StockForward.FixingDate);
    }

    /// <summary>
    /// Basis between model and quoted forward price
    /// </summary>
    /// <remarks>
    ///   <para>The model basis is the difference between the model forward price
    ///   and the quoted futures price.</para>
    ///   <formula>
    ///     Basis = F^m - QuotedPrice
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">F^m</formula> is the implied forward price</description></item>
    ///     <item><description><formula inline="true">QuotedPrice</formula> is the quoted forward price</description></item>
    ///   </list>
    ///   <para>This method calculates the implied basis. The actual basis used during pricing must be explicitly set. The
    ///   default for the basis used in pricing is 0.</para>
    ///   <seealso cref="ModelBasis"/>
    /// </remarks>
    /// <returns>Model basis</returns>
    public double ImpliedModelBasis()
    {
      return _quotedPrice.HasValue ? ModelPrice() - _quotedPrice.Value : 0.0;
    }

    /// <summary>
    /// Dividend yield implied from quoted forward price
    /// </summary>
    /// <remarks>
    ///   <para>The dividend yield is the difference between the stock funding rate and the expected
    ///   growth rate of the stock.</para>
    ///   <para>The dividend yield is related to the commodity spot price and quoted forward price by:</para>
    ///   <formula>
    ///     F = S e^{(r-\delta)T}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">F</formula> is the quoted forward price</description></item>
    ///			<item><description><formula inline="true">S</formula> is the stock spot price</description></item>
    ///     <item><description><formula inline="true">r</formula> is the funding rate</description></item>
    ///     <item><description><formula inline="true">\delta</formula> is the dividend yield</description></item>
    ///     <item><description><formula inline="true">T</formula> is the years till expiration</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Implied lease rate</returns>
    public double ImpliedDividendYield()
    {
      Dt dt = StockForward.FixingDate.IsEmpty() ? StockForward.DeliveryDate : StockForward.FixingDate;
      if (StockCurve.Spot.Spot >= dt)
        return 0.0;
      var fwdPrice = QuotedPrice;
      var spotPrice = StockCurve.Spot.Value;
      var df = DiscountCurve.DiscountFactor(AsOf, dt);
      double T = Dt.FractDiff(AsOf, dt) / 365.0;
      return -Math.Log(fwdPrice / spotPrice * df) / T;
    }

    /// <summary>
    /// Get payment schedule for pricer
    /// </summary>
    /// <param name="paymentSchedule">Payment schedule to add on to</param>
    /// <param name="from">Add payments paid on or after from</param>
    /// <returns></returns>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule paymentSchedule, Dt from)
    {
      if (paymentSchedule == null)
        paymentSchedule = new PaymentSchedule();
      else
        paymentSchedule.Clear();
      var forward = StockForward;
      var maturity = Dt.Roll(forward.DeliveryDate, forward.Roll, forward.Calendar);
      if (from > maturity)
        return paymentSchedule;
      var payment = new StockForwardSettlement(forward.FixingDate.IsEmpty() ? forward.DeliveryDate : forward.FixingDate, maturity,
                                               forward.DeliveryPrice, ModelBasis, StockCurve);
      if ((SettlementResets != null) && SettlementResets.HasCurrentReset)
        payment.Amount = SettlementResets.CurrentReset;
      paymentSchedule.AddPayment(payment);
      return paymentSchedule;
    }

    #endregion

    #region Data

    private double? _quotedPrice;

    #endregion
  }
}
