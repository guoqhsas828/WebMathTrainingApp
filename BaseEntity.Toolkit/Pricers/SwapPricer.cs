//
//  -2012. All rights reserved.
//
using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.Simulations;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Pricers.BGM;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Swap pricer for contracts composed of two swap legs. Could be both floating, fixed or any combination thereof. 
  /// Some methods, like ParCpn() are supported only for standard floating to fixed swap contracts
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.Swap" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Models.CashflowModel" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.Swap"/>
  /// <seealso cref="SwapLegPricer"/>
  /// <seealso cref="BaseEntity.Toolkit.Models.CashflowModel"/>
  [Serializable]
  public class SwapPricer : PricerBase, IPricer, ICashflowNodesGenerator, IAmericanMonteCarloAdapter, IReadOnlyCollection<SwapLegPricer>
  {
    #region ParCouponFn

    private class ParCouponFn : SolverFn
    {
      private readonly SwapLegPricer pricer_;
      private readonly PaymentSchedule schedule_;
      private readonly double tgt_;

      /// <exclude></exclude>
      public ParCouponFn(SwapLegPricer pricer, double tgt)
      {
        pricer_ = pricer;
        schedule_ = pricer.GetPaymentSchedule(null, pricer.AsOf);
        tgt_ = tgt;
      }

      private static double Pv(SwapLegPricer pricer, PaymentSchedule ps)
      {
        var swapNotional = pricer.SwapLeg.Notional;
        return ps.Pv(pricer.AsOf, pricer.Settle, pricer.DiscountCurve, null,
            pricer.IncludePaymentOnSettle, pricer.DiscountingAccrued)
          *pricer.CurrentNotional/(swapNotional.AlmostEquals(0.0) ? 1.0 : swapNotional);
      }

      /// <summary>
      /// Evaluate func
      /// </summary>
      /// <param name="x"></param>
      /// <returns></returns>
      public override double evaluate(double x)
      {
        foreach (var coupon in schedule_)
        {
          var ip = coupon as InterestPayment;
          if (ip != null)
          {
            ip.FixedCoupon = x;
            ip.ResetAmountOverride();
          }
        }
        double pv = Pv(pricer_, schedule_);
        return pv + tgt_;
      }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor from a swap
    /// </summary>
    /// <param name="swap">Interest rate swap</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="notional"> Notional amount of the deal</param>
    /// <param name="discountCurve">Discount curve to discount cashflows</param>
    /// <param name="referenceCurve">Reference curve used for computation of the floating rate</param>
    /// <param name="rateResets">Historical resets</param>
    public SwapPricer(Swap swap, Dt asOf, Dt settle, double notional, DiscountCurve discountCurve,
                      CalibratedCurve referenceCurve, RateResets rateResets)
      : this(
        new SwapLegPricer(swap.ReceiverLeg, asOf, settle, notional, discountCurve, null, referenceCurve, rateResets, null, null),
        new SwapLegPricer(swap.PayerLeg, asOf, settle, notional, discountCurve, null, referenceCurve, rateResets, null, null),
        null
        )
    {}

    /// <summary>
    /// Constructor from a swap
    /// </summary>
    /// <param name="swap">Swap contract</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="notional"> Notional amount of the deal</param>
    /// <param name="discountCurve">Discount curve to discount cashflows</param>
    /// <param name="referenceIndex">Reference index</param>
    /// <param name="referenceCurve">Reference curve used for computation of the floating rate</param>
    /// <param name="rateResets">Historical resets</param>
    /// <param name="fwdModelParams">Forward rate model parameters</param>
    /// <param name="fxCurve">fx rate curve</param>
    public SwapPricer(Swap swap, Dt asOf, Dt settle, double notional, DiscountCurve discountCurve, ReferenceIndex referenceIndex,
                      CalibratedCurve referenceCurve, RateResets rateResets, RateModelParameters fwdModelParams, FxCurve fxCurve)
      : this(
        new SwapLegPricer(swap.ReceiverLeg, asOf, settle, notional, discountCurve, referenceIndex, referenceCurve, rateResets, fwdModelParams, fxCurve),
        new SwapLegPricer(swap.PayerLeg, asOf, settle, notional, discountCurve, null, referenceCurve, rateResets, fwdModelParams, fxCurve),
        null
        )
    {}

    /// <summary>
    /// Constructor from a swap
    /// </summary>
    /// <param name="swap">Swap contract</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="notional"> Notional amount of the deal</param>
    /// <param name="discountCurve">Discount curve to discount cashflows</param>
    /// <param name="referenceCurve">Reference curves used for computation of the floating rate for receiver and payer legs</param>
    /// <param name="rateResets">Historical resets for receiver and payer legs</param>
    /// <param name="fwdModelParams">Forward rate model parameters</param>
    /// <param name="fxCurve">fx rate curve</param>
    public SwapPricer(Swap swap, Dt asOf, Dt settle, double notional, DiscountCurve discountCurve,
                      CalibratedCurve[] referenceCurve, RateResets[] rateResets, RateModelParameters fwdModelParams, FxCurve fxCurve)
      : this(
        new SwapLegPricer(swap.ReceiverLeg, asOf, settle, notional, discountCurve, swap.ReceiverLeg.ReferenceIndex, referenceCurve[0], rateResets[0], fwdModelParams, fxCurve),
        new SwapLegPricer(swap.PayerLeg, asOf, settle, -notional, discountCurve, swap.PayerLeg.ReferenceIndex, referenceCurve[1], rateResets[1], fwdModelParams, fxCurve)
        )
    {}

    /// <summary>
    /// Constructor of a swap pricer from two swap leg pricers
    /// </summary>
    ///<param name="receiverSwapPricer">LegSwapPricer object to price the receiver leg of the contract</param>
    /// <param name="payerSwapPricer">LegSwapPricer object to price the payer side of the contract</param>
    public SwapPricer(SwapLegPricer receiverSwapPricer, SwapLegPricer payerSwapPricer)
      : this(receiverSwapPricer, payerSwapPricer, null)
    {}

    /// <summary>
    /// Constructor of a swap pricer from two swap leg pricers
    /// </summary>
    ///<param name="receiverSwapPricer">LegSwapPricer object to price the receiver leg of the contract</param>
    /// <param name="payerSwapPricer">LegSwapPricer object to price the payer side of the contract</param>
    ///<param name="swaptionPricer">Swaption pricer</param>
    public SwapPricer(SwapLegPricer receiverSwapPricer, SwapLegPricer payerSwapPricer, IPricer swaptionPricer)
      : base(new Swap((SwapLeg)receiverSwapPricer.Product, (SwapLeg)payerSwapPricer.Product))
    {
      ReceiverSwapPricer = receiverSwapPricer;
      PayerSwapPricer = payerSwapPricer;
      PricerFlags = PricerFlags.SensitivityToAllRateTenors;
      SwaptionPricer = swaptionPricer;
      Notional = Math.Abs(ReceiverSwapPricer.ValuationCcyNotional);
      if (SwaptionPricer is SwapBermudanBgmTreePricer)
      {
        Swap.ExerciseSchedule = CollectionUtil.ConvertAll(((Swap)SwaptionPricer.Product).ExerciseSchedule, t => (IOptionPeriod)t.Clone());
        Swap.NotificationDays = ((Swap)SwaptionPricer.Product).NotificationDays;
      }
      else if (SwaptionPricer is SwaptionBlackPricer)
      {
        Swap.ExerciseSchedule = new List<IOptionPeriod>();
        if (((SwaptionBlackPricer)SwaptionPricer).Swaption.OptionType == OptionType.Put) //Todo this needs to be revisited!
        {
          Swap.ExerciseSchedule.Add(new PutPeriod(SwaptionPricer.Product.Maturity, SwaptionPricer.Product.Maturity, 1.0, OptionStyle.European));
        }
        else
        {
          Swap.ExerciseSchedule.Add(
            new CallPeriod(SwaptionPricer.Product.Maturity, SwaptionPricer.Product.Maturity, 1.0, 0.0, OptionStyle.European, 0));
        }
        Swap.NotificationDays = ((Swaption)SwaptionPricer.Product).NotificationDays;
      }
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Get payment schedule
    /// </summary>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule paymentSchedule, Dt from)
    {
      var payNotional = Math.Sign(PayerSwapPricer.Notional) *
                        Math.Abs(PayerSwapPricer.SwapLeg.Notional * PayerSwapPricer.Notional /
                                 PayerSwapPricer.ValuationCcyNotional);
      var receiveNotional = Math.Sign(ReceiverSwapPricer.Notional) *
                            Math.Abs(ReceiverSwapPricer.SwapLeg.Notional * ReceiverSwapPricer.Notional /
                                     ReceiverSwapPricer.ValuationCcyNotional);
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
      return ReceiverSwapPricer.Accrued() + PayerSwapPricer.Accrued() + (SwaptionPricer == null ? 0.0 : SwaptionPricer.Accrued());
    }

    /// <summary>
    /// Deep copy 
    /// </summary>
    /// <returns>A new swap pricer object.</returns>
    public override object Clone()
    {
      return (SwaptionPricer == null)
               ? new SwapPricer((SwapLegPricer)ReceiverSwapPricer.Clone(), (SwapLegPricer)PayerSwapPricer.Clone())
               : new SwapPricer((SwapLegPricer)ReceiverSwapPricer.Clone(), (SwapLegPricer)PayerSwapPricer.Clone(),
                                (IPricer)SwaptionPricer.Clone());
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
      if (Math.Abs(ReceiverSwapPricer.ValuationCcyNotional + PayerSwapPricer.ValuationCcyNotional) > 1e-8)
        InvalidValue.AddError(errors, this, "Notional",
                              "PayerSwapLegPricer and ReceiverSwapLegPricer must have same domestic currency notional");
    }

    /// <summary>
    /// Present value of a swap composed of two legs, i.e. the difference of the the present value of the two legs.
    /// </summary>
    /// <returns>Present value of the swap leg</returns>
    public override double ProductPv()
    {
      return ReceiverSwapPricer.ProductPv() + PayerSwapPricer.ProductPv() + (SwaptionPricer != null ? SwaptionPricer.Pv() : 0.0);
    }

    ///<summary>
    /// Present value including pv of any additional payment
    ///</summary>
    ///<returns></returns>
    public override double Pv()
    {
      return ReceiverSwapPricer.Pv() + PayerSwapPricer.Pv() + (SwaptionPricer != null ? SwaptionPricer.Pv() : 0.0);
    }

    /// <summary>
    /// Difference in PV based on a shift in the coupon rate of the fixed leg (divided by the bump in coupon).
    /// This difference (abs value) is then divided by the bump size and by Notional.
    /// This is only applicable to a swap that has one fixed and one floating leg.
    /// </summary>
    public double Coupon01()
    {
      if (!IsCoupon01Applicable())
        throw new ToolkitException("The Coupon01 measure is only applicable to a swap with a fixed and a floating leg.");
      SwapLegPricer fixedPricer = (ReceiverSwapPricer.SwapLeg.Floating ? PayerSwapPricer : ReceiverSwapPricer);
      SwapLeg fixedLeg = fixedPricer.SwapLeg;
      // First compute the original PV. NOTE: we only need the PV of the fixed leg since the floating leg does not change.
      double origPV = fixedPricer.ProductPv();
      if (SwaptionPricer != null)
        origPV += SwaptionPricer.Pv(); //NOTE: the previous assumption that only fixed leg is needed is not correct anymore for cancellable swap
      // Now bump the fixed coupon by the default amount (1 BP) and re-compute PV.
      // NOTE: besides the case of the simple coupon, we need to consider the cases of step-up coupon and custom cash flow schedule.
      // First, save the original values for all 3 cases.
      double origCoupon = fixedLeg.Coupon;
      var origCouponSchedule = new List<CouponPeriod>(fixedLeg.CouponSchedule);
      PaymentSchedule origPaymentSchedule = fixedLeg.CustomPaymentSchedule;
      // Now bump and re-compute PV
      double pvAfterBump;
      BumpFixedCoupon(fixedLeg);
      try
      {
        pvAfterBump = fixedPricer.ProductPv();
        if (SwaptionPricer != null)
          pvAfterBump += SwaptionPricer.Pv();
      }
      catch (Exception)
      {
        RestoreFixedCoupon(fixedLeg, origCoupon, origCouponSchedule, origPaymentSchedule);
        throw;
      }
      double res = Math.Abs((pvAfterBump - origPV) / (Coupon01CouponBump * fixedPricer.CurrentNotional));
      RestoreFixedCoupon(fixedLeg, origCoupon, origCouponSchedule, origPaymentSchedule);
      return res;
    }

    /// <summary>
    /// Check if the Coupon01 measure is applicable to the underlying swap. It is applicable only if one leg is fixed and the other is floating.
    /// </summary>
    private bool IsCoupon01Applicable()
    {
      var underlying = (Swap)Product;
      return underlying.IsFixedAndFloating;
    }

    /// <summary>
    /// Bump the coupon (or the coupons in the coupon schedule) of the fixed leg for the purpose of Coupon01 computation.
    /// </summary>
    private void BumpFixedCoupon(SwapLeg fixedLeg)
    {
      // Case 1: we have a custom payment schedule
      if (fixedLeg.CustomPaymentSchedule != null && fixedLeg.CustomPaymentSchedule.Count > 0)
      {
        PaymentSchedule ps = PaymentScheduleUtils.CopyPaymentScheduleForBumpingCoupon(fixedLeg.CustomPaymentSchedule);
        // create a "partial" copy for the lack of Clone() method
        foreach (Dt dt in ps.GetPaymentDates())
        {
          foreach (Payment pmt in ps.GetPaymentsOnDate(dt))
          {
            var iPmt = pmt as FixedInterestPayment;
            if (iPmt != null)
              iPmt.FixedCoupon += Coupon01CouponBump;
          }
        }
        fixedLeg.CustomPaymentSchedule = ps; // Replace with a bumped copy - to be restored later.
      }
        // Case 2: we have a coupon schedule
      else if (fixedLeg.CouponSchedule.Count > 0)
      {
        var originalCouponSchedule = new List<CouponPeriod>(fixedLeg.CouponSchedule);
        fixedLeg.CouponSchedule.Clear();
        originalCouponSchedule.ForEach(
          c => fixedLeg.CouponSchedule.Add(new CouponPeriod(c.Date, c.Coupon + Coupon01CouponBump)));
      }
      else // Case 3: just a simple coupon
      {
        fixedLeg.Coupon += Coupon01CouponBump;
      }
    }

    /// <summary>
    /// Restore the coupon (or the coupon schedule) of the fixed leg after the Coupon01 computation.
    /// </summary>
    private void RestoreFixedCoupon(SwapLeg fixedLeg, double origCoupon, List<CouponPeriod> origCouponSchedule,
                                    PaymentSchedule origPaymentSchedule)
    {
      fixedLeg.Coupon = origCoupon;
      fixedLeg.CouponSchedule.Clear();
      origCouponSchedule.ForEach(c => fixedLeg.CouponSchedule.Add(c));
      fixedLeg.CustomPaymentSchedule = origPaymentSchedule;
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

      var swap = (Swap)Product;
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

    private double SolveForPar(SwapLegPricer p1, SwapLegPricer p2)
    {
      var fn = new ParCouponFn(p1, p2.Pv());
      var solver = new Brent2();
      double multiplier = (p1.Notional * p2.Notional < 0) ? 1.0 : -1.0;
      double res = solver.solve(fn, 0.0);
      return res * multiplier;
    }

    /// <summary>
    /// Par coupon for a vanilla swap
    /// </summary>
    /// <param name="p1">Fixed leg</param>
    /// <param name="p2">Floating leg</param>
    /// <param name="level">Overwritten by annuity</param>
    /// <returns>Par coupon</returns>
    internal static double ParCoupon(SwapLegPricer p1, SwapLegPricer p2, out double level)
    {
      p1 = (SwapLegPricer)p1.ShallowCopy();
      p1.Product = (SwapLeg)p1.SwapLeg.Clone();
      p1.SwapLeg.CouponSchedule.Clear();
      p1.SwapLeg.Coupon = 1.0;
      p1.SwapLeg.Index = "";
      level = p1.ProductPv() / p2.CurrentNotional;
      double floatPv = p2.ProductPv() / p2.CurrentNotional;
      return floatPv / level;
    }

    private static IEnumerable<Dt> EnumerateSchedule(Schedule schedule)
    {
      for (int i = 0; i < schedule.Count; ++i)
        yield return schedule.GetPaymentDate(i);
    }

    private IEnumerable<Dt> GetCancellationSchedule(IList<Dt> cancellationDates, IList<IOptionPeriod> optionPeriods, Schedule schedule)
    {
      Dt settle = Settle;
      Dt maturity = schedule.GetPaymentDate(schedule.Count - 1);
      if ((cancellationDates != null) && cancellationDates.Any())
        return cancellationDates.Distinct().OrderBy(dt => dt);
      return EnumerateSchedule(schedule).Where(dt => (dt < maturity) && (dt > settle) && (optionPeriods.IndexOf(dt) >= 0)).Concat(
        optionPeriods.Where(p => p.StartDate == p.EndDate && p.StartDate > settle && p.StartDate < maturity).Select(p => p.StartDate)).Distinct().OrderBy(
          dt => dt);
    }

    private bool HasOptionRight()
    {
      if (Swap.HasOptionRight.HasValue)
        return Swap.HasOptionRight.Value;
      //else by default, fixed payer has right to cancel
      if ((Swap.IsPayerFixed && Swap.Callable) || (Swap.IsReceiverFixed && Swap.Puttable))
        return true;
      return false;
    }

    #endregion Method

    #region Properties

    /// <summary>
    /// As of date
    /// </summary>
    public override Dt AsOf
    {
      get { return Dt.Min(ReceiverSwapPricer.AsOf, PayerSwapPricer.AsOf); }
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
    public Swap Swap
    {
      get { return Product as Swap; }
    }

    /// <summary>
    /// Accessor for pricer of the reciver leg
    /// </summary>
    public SwapLegPricer ReceiverSwapPricer { get; private set; }

    /// <summary>
    /// Accessor for swap pricer of the payer leg
    /// </summary>
    public SwapLegPricer PayerSwapPricer { get; private set; }

    ///<summary>
    /// Accessor for swaption pricer if the product is cancellable
    ///</summary>
    public IPricer SwaptionPricer { get; private set; }

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
    /// Swap FxCurves
    /// </summary>
    public FxCurve[] FxCurves
    {
      get
      {
        FxCurve rFx = ReceiverSwapPricer.FxCurve;
        FxCurve pFx = PayerSwapPricer.FxCurve;
        if (rFx == null)
        {
          if (pFx == null) return new FxCurve[0];
          return new[] {pFx};
        }
        return rFx == pFx || pFx == null ? new[] {rFx} : new[] {rFx, pFx};
      }
    }

    /// <summary>
    /// FxCurve
    /// </summary>
    public FxCurve FxCurve
    {
      get
      {
        FxCurve rFx = ReceiverSwapPricer.FxCurve;
        FxCurve pFx = PayerSwapPricer.FxCurve;
        if (pFx != null && rFx != null && !Equals(rFx, pFx))
          throw new ToolkitException("Payer and receiver legs have different FX curves, call is ambiguous");
        if (pFx != null)
          return pFx;
        if (rFx != null)
          return rFx;
        return null;
      }
    }


    /// <summary>
    /// Reference curves
    /// </summary>
    public CalibratedCurve[] ReferenceCurves
    {
      get
      {
        CalibratedCurve payerRef = PayerSwapPricer.ReferenceCurve;
        CalibratedCurve receiverRef = ReceiverSwapPricer.ReferenceCurve;
        if (payerRef == receiverRef)
          return new[] {payerRef};
        return new[] {receiverRef, payerRef};
      }
    }


    /// <summary>
    /// Swap reference curve
    /// </summary>
    public CalibratedCurve ReferenceCurve
    {
      get
      {
        CalibratedCurve payerRef = PayerSwapPricer.ReferenceCurve;
        CalibratedCurve receiverRef = ReceiverSwapPricer.ReferenceCurve;
        if (payerRef != null && receiverRef != null && (!Equals(payerRef, receiverRef)))
          throw new ToolkitException("Payer and receiver legs have different reference curves, call is ambiguous");
        if (payerRef != null)
          return payerRef;
        if (receiverRef != null)
          return receiverRef;
        return null;
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

    #region IAmericanMonteCarloAdapter Members

    /// <summary>
    /// Underlying funding curves
    /// </summary>
    IEnumerable<DiscountCurve> IAmericanMonteCarloAdapter.DiscountCurves
    {
      get
      {
        var retVal = new List<CalibratedCurve>();
        if (ReceiverSwapPricer.DiscountCurve != null)
          retVal.Add(ReceiverSwapPricer.DiscountCurve);
        if (ReceiverSwapPricer.FxCurve != null)
          ReceiverSwapPricer.FxCurve.GetComponentCurves<DiscountCurve>(retVal);
        if (PayerSwapPricer.DiscountCurve != null)
          retVal.Add(PayerSwapPricer.DiscountCurve);
        if (PayerSwapPricer.FxCurve != null)
          PayerSwapPricer.FxCurve.GetComponentCurves<DiscountCurve>(retVal);
        return retVal.Where(cc => cc != null).Distinct().Cast<DiscountCurve>();
      }
    }

    /// <summary>
    /// Underlying reference curves
    /// </summary>
    IEnumerable<CalibratedCurve> IAmericanMonteCarloAdapter.ReferenceCurves
    {
      get
      {
        if (ReceiverSwapPricer.ReferenceCurve != null)
        {
          if (ReceiverSwapPricer.ReferenceCurve != ReceiverSwapPricer.DiscountCurve)
            yield return ReceiverSwapPricer.ReferenceCurve;
        }
        if (PayerSwapPricer.ReferenceCurve != null)
        {
          if ((PayerSwapPricer.ReferenceCurve != PayerSwapPricer.DiscountCurve) && (PayerSwapPricer.ReferenceCurve != ReceiverSwapPricer.ReferenceCurve) &&
              (PayerSwapPricer.ReferenceCurve != ReceiverSwapPricer.DiscountCurve))
            yield return PayerSwapPricer.ReferenceCurve;
        }
      }
    }

    /// <summary>
    /// Underlying survival curves 
    /// </summary>
    IEnumerable<SurvivalCurve> IAmericanMonteCarloAdapter.SurvivalCurves
    {
      get { yield break; }
    }

    /// <summary>
    /// Underlying fx rates
    /// </summary>
    IEnumerable<FxRate> IAmericanMonteCarloAdapter.FxRates
    {
      get
      {
        if ((ReceiverSwapPricer.FxCurve != null) && ReceiverSwapPricer.FxCurve.WithDiscountCurve)
          yield return ReceiverSwapPricer.FxCurve.SpotFxRate;
        if ((PayerSwapPricer.FxCurve != null) && (PayerSwapPricer.FxCurve != ReceiverSwapPricer.FxCurve) &&
            PayerSwapPricer.FxCurve.WithDiscountCurve)
          yield return PayerSwapPricer.FxCurve.SpotFxRate;
      }
    }

    /// <summary>
    /// Cashflow of the underlier paid out until the first of call/put date 
    /// </summary>
    IList<ICashflowNode> IAmericanMonteCarloAdapter.Cashflow
    {
      get { return ((ICashflowNodesGenerator)this).Cashflow; }
    }

    /// <summary>
    /// Call price process
    /// </summary>
    ExerciseEvaluator IAmericanMonteCarloAdapter.CallEvaluator
    {
      get
      {
        if (!Swap.Cancelable || HasOptionRight())
          return null;
        return new CancellableSwapExercise(GetCancellationSchedule(Swap.ExerciseDates, Swap.ExerciseSchedule, ReceiverSwapPricer.SwapLeg.Schedule),
                                           Swap.ExerciseSchedule);
      }
    }

    /// <summary>
    /// Put price process 
    /// </summary>
    ExerciseEvaluator IAmericanMonteCarloAdapter.PutEvaluator
    {
      get
      {

        if (!Swap.Cancelable || !HasOptionRight())
          return null;
        return new CancellableSwapExercise(GetCancellationSchedule(Swap.ExerciseDates, Swap.ExerciseSchedule, PayerSwapPricer.SwapLeg.Schedule),
                                           Swap.ExerciseSchedule);
      }
    }

    /// <summary>
    /// Trade notional
    /// </summary>
    double IAmericanMonteCarloAdapter.Notional
    {
      get { return Notional; }
    }


    /// <summary>
    /// Explanatory variables for regression algorithm. 
    ///TODO For the time being rely on default implementation. 
    /// </summary>
    BasisFunctions IAmericanMonteCarloAdapter.Basis
    {
      get { return null; }
    }

    /// <summary>
    /// Check if product if vanilla (i.e. does not require AMC for valuation) 
    /// </summary>
    bool IAmericanMonteCarloAdapter.Exotic
    {
      get { return Swap.Cancelable; }
    }

    /// <summary>
    /// Exposure dates
    /// </summary>
		public Dt[] ExposureDates
		{
			get
			{
				if (_exposureDts == null)
					_exposureDts = InitExposureDates(null);
				return _exposureDts;
			}
			set { _exposureDts = InitExposureDates(value); }
		}

	  private Dt[] InitExposureDates(Dt[] inputDates)
	  {
      Dt rolledMaturity = Dt.Roll(Swap.Maturity, Swap.PayerLeg.BDConvention, Swap.PayerLeg.Calendar);
	    Dt max = rolledMaturity;
		  var dates = new UniqueSequence<Dt>();
		  if(inputDates != null && inputDates.Any(dt => dt <= max))
		  {
		    dates.Add(inputDates.Where(dt => dt <= max).ToArray());
		    var lastDt = dates.Max();
        if (lastDt < max && inputDates.Any(dt => dt > max))
		      dates.Add(inputDates.First(dt => dt > max)); 
			  max = Dt.Earlier(inputDates.First(), max);
		  }
			var iDeal = this as IAmericanMonteCarloAdapter;
			if (iDeal.CallEvaluator != null)
			{
				foreach (var exDt in iDeal.CallEvaluator.ExerciseDates)
				{
					if (exDt > max)
						break;
          var beforeDt = Dt.Add(exDt, -1);
					if (beforeDt > AsOf)
						dates.Add(beforeDt);
					dates.Add(exDt);
				}
				if (iDeal.CallEvaluator.EventDates != null)
				{
					foreach (var eventDate in iDeal.CallEvaluator.EventDates)
					{
						if (eventDate > max)
							break;
            var beforeDt = Dt.Add(eventDate, -1);
						if (beforeDt > AsOf)
							dates.Add(beforeDt);
						dates.Add(eventDate);
					}
				}
			}
			if (iDeal.PutEvaluator != null)
			{
				foreach (var exDt in iDeal.PutEvaluator.ExerciseDates)
				{
					if (exDt > max)
						break;
          var beforeDt = Dt.Add(exDt, -1);
					if (beforeDt > AsOf)
						dates.Add(beforeDt);
					dates.Add(exDt);
				}
				if (iDeal.PutEvaluator.EventDates != null)
				{
					foreach (var eventDate in iDeal.PutEvaluator.EventDates)
					{
						if (eventDate > max)
							break;
            var beforeDt = Dt.Add(eventDate, -1);
						if (beforeDt > AsOf)
							dates.Add(beforeDt);
						dates.Add(eventDate);
					}
				}
			}
			dates.Add(AsOf);
      if (rolledMaturity < max)
        dates.Add(rolledMaturity); 
				
		  return dates.ToArray();
	  }

	  private Dt[] _exposureDts; 

	  #endregion

    #region Data

    private const double Coupon01CouponBump = 0.0001;
    // Bump coupon by this amount (1 BP) and re-compute the PV of the fixed leg.

    #endregion

    #region ICashflowNodesGenerator Members

    /// <summary>
    /// Generate cashflow for simulation
    /// </summary>
    IList<ICashflowNode> ICashflowNodesGenerator.Cashflow
    {
      get
      {
        double notional = Notional;
        var retVal = ReceiverSwapPricer.GetPaymentSchedule(null, AsOf).ToCashflowNodeList(Math.Abs(ReceiverSwapPricer.SwapLeg.Notional),
                                                                                          ReceiverSwapPricer.Notional / notional,
                                                                                          ReceiverSwapPricer.DiscountCurve, null, null);
        return PayerSwapPricer.GetPaymentSchedule(null, AsOf).ToCashflowNodeList(Math.Abs(PayerSwapPricer.SwapLeg.Notional),
                                                                                 PayerSwapPricer.Notional / notional, PayerSwapPricer.DiscountCurve, null,
                                                                                 retVal);
      }
    }

    #endregion

    #region ExerciseEvaluator

    [Serializable]
    private class CancellableSwapExercise : ExerciseEvaluator
    {
      #region Data

      private readonly IList<IOptionPeriod> _optionPeriods;

      #endregion

      #region Constructor

      internal CancellableSwapExercise(IEnumerable<Dt> exerciseSchedule, IList<IOptionPeriod> optionPeriods)
        : base(exerciseSchedule, true, Dt.Empty)
      {
        _optionPeriods = optionPeriods;
      }

      #endregion

      #region Methods

      public override double Value(Dt date)
      {
        return 0.0;
      }

      public override double Price(Dt date)
      {
        if (_optionPeriods != null)
          return _optionPeriods.ExercisePriceByDate(date) - 1.0;
        return 0.0;
      }

      #endregion
    }
    #endregion
 
    #region IReadOnlyCollection<SwapLegPricer> members

    /// <summary>
    /// Count
    /// </summary>
    public int Count
    {
      get { return 2; }
    }

    /// <summary>
    /// GetEnumerator
    /// </summary>
    /// <returns></returns>
    public IEnumerator<SwapLegPricer> GetEnumerator()
    {
      yield return ReceiverSwapPricer;
      yield return PayerSwapPricer;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }

    #endregion
  }
}