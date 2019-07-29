// 
//  -2013. All rights reserved.
// 

using System;
using System.Collections;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Sensitivity;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// <para>Price a <see cref="BaseEntity.Toolkit.Products.CommodityForward">Commodity Forward</see>.</para>
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.CommodityForward" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.CommodityForward">Commodity Forwards</seealso>
  [Serializable]
  public class CommodityForwardPricer : PricerBase, IPricer, IEvaluatorProvider
  {
    #region CommodityForwardSettlement

    private class CommodityForwardSettlement : Payment
    {
      public CommodityForwardSettlement(Dt resetDt, Dt payDt, double deliveryPrice, Currency ccy, IForwardPriceCurve referenceCurve)
        : base(payDt, ccy)
      {
        ResetDt = resetDt;
        ReferenceCurve = referenceCurve;
        DeliveryPrice = deliveryPrice;
      }

      private double DeliveryPrice { get; set; }
      private Dt ResetDt { get; set; }
      private IForwardPriceCurve ReferenceCurve { get; set; }

      protected override double ComputeAmount()
      {
        var f = ReferenceCurve.Interpolate(ResetDt);
        return f - DeliveryPrice;
      }

      public override bool IsProjected
      {
        get { return !AmountOverride.HasValue; }
      }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="product">Commodity forward to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="rfr">Risk free rate</param>
    /// <param name="leaseRate">Lease rate (continuous comp, eg. 0.05)</param>
    /// <param name="spotPrice">Spot price of underlying commodity</param>
    public CommodityForwardPricer(CommodityForward product, Dt asOf, Dt settle, double rfr, double leaseRate, double spotPrice)
      : base(product, asOf, settle)
    {
      DiscountCurve = new DiscountCurve(asOf, rfr);
      CommodityCurve = new CommodityCurve(asOf, spotPrice, DiscountCurve, leaseRate, null);
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="product">Commodity forward to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="commodityCurve">Commodity curve</param>
    public CommodityForwardPricer(CommodityForward product, Dt asOf, Dt settle, DiscountCurve discountCurve, CommodityCurve commodityCurve)
      : base(product, asOf, settle)
    {
      CommodityCurve = commodityCurve;
      DiscountCurve = discountCurve;
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
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Non null discount curve required"));
      if (CommodityCurve == null)
        InvalidValue.AddError(errors, this, "CommodityCurve", String.Format("Non null commodity curve required"));
    }

    #endregion Utility Methods

    #region Properties

    /// <summary>
    /// Flat risk free rate to futures delivery
    /// </summary>
    /// <remarks>
    /// <para>Continuously compounded risk free rate to delivery date.</para>
    /// </remarks>
    public double Rfr
    {
      get { return DiscountCurve.R(DeliveryDate); }
    }

    /// <summary>
    /// Discount Curve
    /// </summary>
    public DiscountCurve DiscountCurve { get; private set; }

    /// <summary>
    /// Reference commodity curve
    /// </summary>
    public CommodityCurve CommodityCurve { get; private set; }

    /// <summary>
    /// Commodity forward product
    /// </summary>
    public CommodityForward CommodityForward
    {
      get { return (CommodityForward)Product; }
    }

    /// <summary>
    /// Effective delivery date of forward
    /// </summary>
    private Dt DeliveryDate
    {
      get { return CommodityForward.LastFixingDate.IsEmpty() ? CommodityForward.LastDeliveryDate : CommodityForward.LastFixingDate; }
    }

    /// <summary>
    /// Overridden resets
    /// </summary>
    public RateResets SettlementResets { get; set; }

    #endregion Properties

    #region Methods

    /// <summary>
    /// Net present value of the product, excluding the value of any additional payment.
    /// </summary>
    /// <remarks>
    ///   <para>This is the model forward price minus the model basis minus the delivery price scaled by the notional.</para>
    ///   <formula>
    ///     V = (F^m - DeliveryPrice)* Notional
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">F^m</formula> is the implied forward price</description></item>
    ///     <item><description><formula inline="true">Notional</formula> is the notional</description></item>
    ///   </list>
    /// </remarks>
    /// <seealso cref="ModelPrice"/>
    /// <returns>PV of product</returns>
    public override double ProductPv()
    {
      return Notional * GetPaymentSchedule(null, AsOf).Pv(AsOf, Settle, DiscountCurve, null, false, false);
    }

    /// <summary>
    /// Model implied price of forward
    /// </summary>
    /// <remarks>
    ///   <para>The model price of the forward contract is the forward price of the deliverable commodity.</para>
    ///   <para>The forward price of the deliverable is calculated from the spot price of the commodity, the
    ///   funding cost, and the lease rate.</para>
    ///   <para>The model forward price is:</para>
    ///   <formula>
    ///     F_T^m = S e^{(r-\delta)T}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">F_T^m</formula> is the model forward price</description></item>
    ///			<item><description><formula inline="true">S</formula> is the spot price of the deliverable commodity</description></item>
    ///     <item><description><formula inline="true">r</formula> is the funding rate</description></item>
    ///     <item><description><formula inline="true">\delta</formula> is the lease rate</description></item>
    ///     <item><description><formula inline="true">T</formula> is the years till expiration</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Model forward price</returns>
    public double ModelPrice()
    {
      return CommodityCurve.Interpolate(DeliveryDate);
    }

    /// <summary>
    /// Lease rate implied from quoted forward price
    /// </summary>
    /// <remarks>
    ///   <para>The lease rate is the difference between the commodity funding rate and the expected
    ///   growth rate of the commodity. This rate may not be observable in the market unless the
    ///   underlying commodity has an active lease market.</para>
    ///   <para>The lease rate is related to the commodity spot price and quoted forward price by:</para>
    ///   <formula>
    ///     F = S e^{(r-\delta)T}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///			<item><description><formula inline="true">F</formula> is the quoted forward price</description></item>
    ///			<item><description><formula inline="true">S</formula> is the spot price of the deliverable commodity</description></item>
    ///     <item><description><formula inline="true">r</formula> is the funding rate</description></item>
    ///     <item><description><formula inline="true">\delta</formula> is the lease rate</description></item>
    ///     <item><description><formula inline="true">T</formula> is the years till expiration</description></item>
    ///   </list>
    /// </remarks>
    /// <returns>Implied lease rate</returns>
    public double ImpliedLeaseRate()
    {
#if OLD_WAY // replaced by RTD Mar'13
      if (CommodityCurve.Spot == null)
        return 0.0; //no observable spot
      Dt dt = CommodityForward.LastFixingDate.IsEmpty() ? CommodityForward.LastDeliveryDate : CommodityForward.LastFixingDate;
      if (CommodityCurve.Spot.Spot >= dt)
        return 0.0;
      var fwdPrice = ModelPrice();
      var spotPrice = CommodityCurve.Spot.Value;
      var df = DiscountCurve.DiscountFactor(AsOf, dt);
      double T = Dt.FractDiff(AsOf, dt) / 365.0;
      return Math.Log(fwdPrice / spotPrice * df) / T;
#else
      return CommodityCurve.ImpliedLeaseRate(AsOf, DeliveryDate);
#endif
    }

    /// <summary>
    /// Generate payment schedule
    /// </summary>
    /// <param name="paymentSchedule">Payment schedule</param>
    /// <param name="from">From date</param>
    /// <returns>Payment schedule</returns>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule paymentSchedule, Dt from)
    {
      if (paymentSchedule == null)
        paymentSchedule = new PaymentSchedule();
      else
        paymentSchedule.Clear();
      var forward = CommodityForward;
      var maturity = Dt.Roll(forward.LastDeliveryDate, forward.Roll, forward.Calendar);
      if (from > maturity)
        return paymentSchedule;
      var p = new CommodityForwardSettlement(forward.LastFixingDate.IsEmpty() 
        ? forward.LastDeliveryDate : forward.LastFixingDate, 
        maturity, forward.DeliveryPrice, forward.Ccy, CommodityCurve);
      if (SettlementResets != null && SettlementResets.HasCurrentReset)
        p.Amount = SettlementResets.CurrentReset;
      paymentSchedule.AddPayment(p);
      return paymentSchedule;
    }

    #endregion Methods

    #region Sensitivity.IEvaluatorProvider Members

    /// <summary>
    /// Gets the evaluation function for the specified measure.
    /// </summary>
    /// <param name="measure">The measure.</param>
    /// <returns>A delegate to calculate the specified measure.</returns>
    /// <remarks></remarks>
    Func<double> IEvaluatorProvider.GetEvaluator(string measure)
    {
      // We can return null for the measures not requiring special treatment,
      // in which case, the evaluator is created regularly in the old way.
      if (measure != "Pv") return null;

      // For pv calculation, we fix the implied basis at the current level
      // and returns the model price minus basis, so bumping curves has effects.
      return () => ModelPrice();
    }

    #endregion
  }
}
