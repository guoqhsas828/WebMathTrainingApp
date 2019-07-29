
//
//  -2012. All rights reserved.
//
using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Commodity Swap pricer composed of two swap legs. May be fixed-floating, 
  /// floating-floating (for basis swaps) or fixed-fixed. 
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.CommoditySwap" />
  /// <note>Some methods, like ParCpn() are supported only for standard floating to fixed swap contracts.</note>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.CommoditySwap">Commodity Swap</seealso>
  /// <seealso cref="CommoditySwapLegPricer">Commodity Swap</seealso>
  [Serializable]
  public class CommoditySwapPricer : PricerBase, IPricer
  {
    #region Constructors

    /// <summary>
    /// Constructor from a commodity swap
    /// </summary>
    /// <param name="swap">Interest rate swap</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="notional"> Notional amount of the deal</param>
    /// <param name="discountCurve">Discount curve to discount cashflows</param>
    /// <param name="referenceCurve">Reference curve used for computation of the floating rate</param>
    /// <param name="rateResets">Historical resets</param>
    public CommoditySwapPricer(CommoditySwap swap, Dt asOf, Dt settle, double notional, DiscountCurve discountCurve,
      CalibratedCurve referenceCurve, RateResets rateResets)
      : this(
      new CommoditySwapLegPricer(swap.ReceiverLeg, asOf, settle, notional, discountCurve, swap.ReceiverLeg.ReferenceIndex, referenceCurve, rateResets),
      new CommoditySwapLegPricer(swap.PayerLeg, asOf, settle, notional, discountCurve, swap.PayerLeg.ReferenceIndex, referenceCurve, rateResets)
      )
    {}

     /// <summary>
    /// Constructor of a commodity swap pricer from two commodity swap leg pricers
    /// </summary>
    ///<param name="receiverSwapPricer">LegSwapPricer object to price the receiver leg of the contract</param>
    /// <param name="payerSwapPricer">LegSwapPricer object to price the payer side of the contract</param>
    public CommoditySwapPricer(CommoditySwapLegPricer receiverSwapPricer, CommoditySwapLegPricer payerSwapPricer)
      : base(new CommoditySwap((CommoditySwapLeg)receiverSwapPricer.Product, (CommoditySwapLeg)payerSwapPricer.Product))
    {
      ReceiverSwapPricer = receiverSwapPricer;
      PayerSwapPricer = payerSwapPricer;
      Notional = Math.Abs(ReceiverSwapPricer.Notional);
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Get payment schedule
    /// </summary>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule paymentSchedule, Dt from)
    {
      var payNotional = Math.Sign(PayerSwapPricer.Notional)*
                        Math.Abs(PayerSwapPricer.SwapLeg.Notional*PayerSwapPricer.Notional/
                                 PayerSwapPricer.Notional);
      var receiveNotional = Math.Sign(ReceiverSwapPricer.Notional)*
                            Math.Abs(ReceiverSwapPricer.SwapLeg.Notional*ReceiverSwapPricer.Notional/
                                     ReceiverSwapPricer.Notional);
      var retVal = new PaymentSchedule();
      retVal.AddPayments(
        ReceiverSwapPricer.GetPaymentSchedule(null, from).ConvertAll<Payment>(p => p.Scale(receiveNotional)));
      retVal.AddPayments(PayerSwapPricer.GetPaymentSchedule(null, from).ConvertAll<Payment>(p => p.Scale(payNotional)));
      return retVal;
    }


    /// <summary>
    ///   Total accrued interest for product to settlement date given pricing arguments
    /// </summary>
    ///
    /// <returns>Total accrued interest</returns>
    ///
    public override double Accrued()
    {
      return 0.0;
    }

    /// <summary>
    /// Deep copy 
    /// </summary>
    /// <returns>A new swap pricer object.</returns>
    public override object Clone()
    {
      return new CommoditySwapPricer((CommoditySwapLegPricer)ReceiverSwapPricer.Clone(), (CommoditySwapLegPricer)PayerSwapPricer.Clone());
    }

    /// <summary>
    ///   Validate product inputs
    /// </summary>
    /// <param name="errors">Error list </param>
    /// <remarks>
    ///   This tests only relationships between fields of the pricer that
    ///   cannot be validated in the property methods.
    /// </remarks>
    ///
    public override void Validate(ArrayList errors)
    {
      ReceiverSwapPricer.Validate();
      PayerSwapPricer.Validate();
      if (ReceiverSwapPricer.ValuationCurrency != PayerSwapPricer.ValuationCurrency)
        InvalidValue.AddError(errors, this, "ValuationCurrency",
                              "PayerSwapLegPricer and ReceiverSwapLegPricer must have same valuation currency");
    }

    /// <summary>
    /// Present value of a commodity swap composed of two legs, i.e. the difference of the the present value of the two legs.
    /// </summary>
    /// <returns>Present value of the swap leg</returns>
    public override double ProductPv()
    {
      return ReceiverSwapPricer.ProductPv() + PayerSwapPricer.ProductPv();
    }

    ///<summary>
    /// Present value including pv of any additional payment
    ///</summary>
    ///<returns></returns>
    public override double Pv()
    {
      return ReceiverSwapPricer.Pv() + PayerSwapPricer.Pv();
    }

    /// <summary>
    /// Calculate par coupon of a fixed/floating or floating/floating swap.
    /// </summary>
    /// <remarks>
    /// <para>Par coupon of a swap composed of a floating and a fixed leg or two flaoting legs.
    /// For a fixed/floating swap, this is the ratio between the the present value of the floating
    /// leg and the fixed annuity.</para>
    /// <para>For a floating/floating swpa, this is the difference of actual pvs divided by annuity to
    /// amortize it.</para>
    /// <para>Valuation is made at pricing as-of date given pricing arguments.</para>
    /// </remarks>
    /// <returns>Par coupon of a swap composed of a floating and a fixed leg or two floating legs</returns>
    public double ParCoupon()
    {
      if (Settle >= Product.Maturity)
        return 0.0;

      var swap = (CommoditySwap)Product;
      if (swap.IsReceiverFixed)
        return SolveForPar(ReceiverSwapPricer, PayerSwapPricer);
      if (swap.IsPayerFixed)
        return SolveForPar(PayerSwapPricer, ReceiverSwapPricer);
      if (swap.IsSpreadOnReceiver)
        return SolveForPar(ReceiverSwapPricer, PayerSwapPricer);
      if (swap.IsSpreadOnPayer)
        return SolveForPar(PayerSwapPricer, ReceiverSwapPricer);
      throw new ToolkitException("Par spread calculation for a swap with zero spread on both legs not supported");
    }

    #region ParCouponFn

    private class ParCouponFn : SolverFn
    {
      private readonly CommoditySwapLegPricer _pricer;
      private readonly PaymentSchedule _schedule;
      private readonly double tgt_;

      /// <exclude></exclude>
      public ParCouponFn(CommoditySwapLegPricer pricer, double tgt)
      {
        _pricer = pricer;
        _schedule = pricer.GetPaymentSchedule(null, pricer.AsOf);
        tgt_ = tgt;
      }

      private static double Pv(CommoditySwapLegPricer pricer, PaymentSchedule ps)
      {
        return ps.Pv(pricer.AsOf, pricer.Settle, pricer.DiscountCurve, null, false, true) * pricer.CurrentNotional;
      }

      /// <summary>
      /// Evaluate func
      /// </summary>
      /// <param name="x"></param>
      /// <returns></returns>
      public override double evaluate(double x)
      {
        foreach (var coupon in _schedule)
        {
          var ip = coupon as CommodityFixedPricePayment;
          if (ip != null)
          {
            ip.FixedPrice = x;
            ip.ResetAmountOverride();
          }
          else
          {
            var ip2 = coupon as CommodityFloatingPricePayment;
            if (ip2 == null) continue;
            ip2.Spread = x;
            ip2.ResetAmountOverride();
          }
        }
        var pv = Pv(_pricer, _schedule);
        return pv + tgt_;
      }
    }

    #endregion

    private static double SolveForPar(CommoditySwapLegPricer p1, CommoditySwapLegPricer p2)
    {
      var fn = new ParCouponFn(p1, p2.Pv());
      var solver = new Brent2();
      var multiplier = (p1.Notional * p2.Notional < 0) ? 1.0 : -1.0;
      var res = solver.solve(fn, 0.0);
      return res * multiplier;
    }

    #endregion Method

    #region Properties
    /// <summary>
    /// As of date
    /// </summary>
    public override Dt  AsOf
    {
      get
      {
        return Dt.Min(ReceiverSwapPricer.AsOf, PayerSwapPricer.AsOf);
      }
      set
      {
        ReceiverSwapPricer.AsOf = value;
        PayerSwapPricer.AsOf = value;
      }
    }

    /// <summary>
    /// Settle of the trade
    /// </summary>
    public override Dt Settle
    {
      get { return Dt.Min(ReceiverSwapPricer.Settle, PayerSwapPricer.Settle); }
      set
      {
        ReceiverSwapPricer.Settle = value;
        PayerSwapPricer.Settle = value;
      }
    }

    /// <summary>
    /// Product
    /// </summary>
    public CommoditySwap CommoditySwap => Product as CommoditySwap;

    /// <summary>
    /// Accessor for pricer of the reciver leg
    /// </summary>
    public CommoditySwapLegPricer ReceiverSwapPricer { get; private set; }

    /// <summary>
    /// Accessor for swap pricer of the payer leg
    /// </summary>
    public CommoditySwapLegPricer PayerSwapPricer { get; private set; }

    /// <summary>
    /// Discount curve 
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get
      {
        if (PayerSwapPricer.DiscountCurve != ReceiverSwapPricer.DiscountCurve)
          throw new ToolkitException("Payer and receiver legs are discounted with different curves, call is ambiguous");
        return PayerSwapPricer.DiscountCurve;
      }
    }

    /// <summary>
    /// Swap discount curves
    /// </summary>
    public DiscountCurve[] DiscountCurves
    {
      get
      {
        var list = new List<DiscountCurve>();
        var curve = PayerSwapPricer.ReferenceCurve as DiscountCurve;
        if (curve != null) list.Add(curve);
        curve = ReceiverSwapPricer.ReferenceCurve as DiscountCurve;
        if (curve != null && !list.Contains(curve)) list.Add(curve);
        curve = ReceiverSwapPricer.DiscountCurve;
        if (curve != null && !list.Contains(curve)) list.Add(curve);
        curve = PayerSwapPricer.DiscountCurve;
        if (curve != null && !list.Contains(curve)) list.Add(curve);
        return list.ToArray();
      }
    }

    /// <summary>
    /// Reference curves
    /// </summary>
    public CalibratedCurve[] ReferenceCurves
    {
      get
      {
        var payerRef = PayerSwapPricer.ReferenceCurve;
        var receiverRef = ReceiverSwapPricer.ReferenceCurve;
        return payerRef == receiverRef ? new[] {payerRef} : new[] {receiverRef, payerRef};
      }
    }


    /// <summary>
    /// Swap reference curve
    /// </summary>
    public CalibratedCurve ReferenceCurve
    {
      get
      {
        var payerRef = PayerSwapPricer.ReferenceCurve;
        var receiverRef = ReceiverSwapPricer.ReferenceCurve;
        if (payerRef != null && receiverRef != null && (!Equals(payerRef, receiverRef)))
          throw new ToolkitException("Payer and receiver legs have different reference curves, call is ambiguous");
        return payerRef ?? receiverRef ?? null;
      }
    }

    /// <summary>
    /// Gets the valuation currency.
    /// </summary>
    /// <value>The valuation currency.</value>
    public override Currency ValuationCurrency
    {
      get
      {
        if (ReceiverSwapPricer.ValuationCurrency != PayerSwapPricer.ValuationCurrency)
          throw new ArgumentException("Payer and Receiver SwapLegPricer should be denominated in the same currency");
        return ReceiverSwapPricer.ValuationCurrency;
      }
    }

    #endregion
  }
}