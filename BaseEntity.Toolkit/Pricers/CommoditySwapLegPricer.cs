// 
//  -2017. All rights reserved.
// 

using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using log4net;
using System.Linq;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Price a <see cref="CommoditySwapLeg"/>.
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.CommoditySwapLeg" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.CommoditySwapLeg">Commodity Swap Leg</seealso>
  /// <seealso cref="CommoditySwapPricer">Commodity Swap Pricer</seealso>
  [Serializable]
  public partial class CommoditySwapLegPricer : PricerBase, IPricer, ILockedRatesPricerProvider
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(CommoditySwapLegPricer));

    #region Constructors

    /// <summary>
    /// Default constructor
    /// </summary>
    public CommoditySwapLegPricer()
      : base(null)
    {
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="swap">Swap contract</param>
    /// <param name="asOf">As of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="notional"> Notional amount of the deal</param>
    /// <param name="discountCurve">Discount curve to discount cashflows</param>
    /// <param name="referenceIndex">Reference index</param>
    /// <param name="referenceCurve">Reference curve used for computation of the floating rate</param>
    /// <param name="rateResets">Historical resets</param>
    public CommoditySwapLegPricer(CommoditySwapLeg swap, Dt asOf, Dt settle, double notional, DiscountCurve discountCurve,
                                  ReferenceIndex referenceIndex, CalibratedCurve referenceCurve,
                                  RateResets rateResets)
      : base(swap, asOf, settle)
    {
      if (swap.Floating && swap.ReferenceIndex == null && referenceIndex == null)
        throw new ToolkitException("A non null reference index should be provided for floating swap legs");
      ReferenceIndex = referenceIndex ?? swap.ReferenceIndex;
      if (ReferenceIndex != null && !Array.Exists(ReferenceIndex.ProjectionTypes, pt => pt == SwapLeg.ProjectionType))
        SwapLeg.ProjectionType = ReferenceIndex.ProjectionTypes[0];
      ReferenceCurve = referenceCurve;
      DiscountCurve = discountCurve;
      Notional = notional;
      if (ReferenceIndex != null && rateResets != null)
      {
        ReferenceIndex.HistoricalObservations = rateResets;
        RateResets = rateResets;
      }
      else
      {
        RateResets = ReferenceIndex?.HistoricalObservations;
      }
    }

    #endregion

    #region Methods

    #region Initialization 

    /// <summary>
    /// Gets the rate projector.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="swapLeg">The swap leg.</param>
    /// <param name="referenceCurve">The reference curve.</param>
    /// <param name="approximate">Approximate price observation schedule</param>
    /// <returns></returns>
    public IRateProjector GetRateProjector(Dt asOf, CommoditySwapLeg swapLeg, CommodityCurve referenceCurve, bool approximate)
    {
      return new CommodityAveragePriceCalculator(asOf, ReferenceIndex, referenceCurve, 
        swapLeg.ObservationRule, swapLeg.Observations, swapLeg.RollExpiry, swapLeg.Weighted, approximate);
    }

    #endregion

    /// <summary>
    /// Shallow copy 
    /// </summary>
    /// <returns>A new swap leg pricer object.</returns>
    public override object Clone()
    {
      return new CommoditySwapLegPricer((CommoditySwapLeg)Product, AsOf, Settle, Notional, DiscountCurve, ReferenceIndex, ReferenceCurve,
                                        RateResets);
    }

    /// <summary>
    ///   Total accrued interest for product to settlement date given pricing arguments
    /// </summary>
    /// <returns>Total accrued interest</returns>
    public override double Accrued()
    {
      return 0.0;
    }

    private PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from, Dt to)
    {
      if (from > SwapLeg.EffectiveMaturity)
        return null;
      if (SwapLeg.CustomPaymentSchedule != null && SwapLeg.CustomPaymentSchedule.Count > 0)
      {
        if (ps == null)
          ps = new PaymentSchedule();
        else
          ps.Clear();
        if (SwapLeg.Effective > from) from = SwapLeg.Effective;
        if (to.IsValid() && from > to) return null;
        foreach (var d in SwapLeg.CustomPaymentSchedule.GetPaymentDates())
        {
          if (d >= from)
          {
            var paymentsOnDate = CloneUtil.CloneToGenericList(SwapLeg.CustomPaymentSchedule.GetPaymentsOnDate(d).ToList());
            
            // Update rate resets in floating interest payment objects at this point, taking into account the pricing date:
            if (SwapLeg.Floating)
            {
              //var projParams = GetProjectionParams();
              var rateProjector = GetRateProjector(AsOf, SwapLeg, ReferenceCurve as CommodityCurve, ApproximateForFastCalculation);              
              foreach (var pay in paymentsOnDate)
              {
                if (!pay.VolatilityStartDt.IsEmpty())
                {
                  pay.VolatilityStartDt = AsOf;
                }
                var flp = pay as CommodityFloatingPricePayment;
                if (flp != null)
                {
                  flp.RateProjector = rateProjector;
                  RateResets.UpdateResetsInCustomCashflowPayments(flp,
                    flp.ResetDate < AsOf && flp.PeriodEndDate > AsOf, false);
                }
              }
            }
            ps.AddPayments(paymentsOnDate);
          }
          if (to.IsValid() && d > to)
            break;
        }
        return ps;
      }
      if (!SwapLeg.Floating)
      {
        PaymentScheduleUtils.CommodityPaymentGenerator generator =
          (effective, date, start, end, notional, price) => new CommodityFixedPricePayment(date, SwapLeg.Ccy, effective == start ? start : start + 1, end, notional, price);

        ps = SwapLeg.Schedule.CommodityPaymentScheduleFactory(SwapLeg.Effective, from, to, CashflowFlag.None, SwapLeg.Price, SwapLeg.PriceSchedule, SwapLeg.Notional,
                                                                SwapLeg.AmortizationSchedule, SwapLeg.Amortizes,
                                                                generator, Dt.Empty, Dt.Empty, SwapLeg.PayLagRule, null);
      }
      else
      {
        var rateProjector = GetRateProjector(AsOf, SwapLeg, ReferenceCurve as CommodityCurve, ApproximateForFastCalculation);
        PaymentScheduleUtils.CommodityPaymentGenerator generator =
          (effective, date, start, end, notional, spread) =>
          new CommodityFloatingPricePayment(date, SwapLeg.Ccy, effective == start ? start : start + 1, end, notional, spread, rateProjector, null);

        ps = SwapLeg.Schedule.CommodityPaymentScheduleFactory(SwapLeg.Effective, from, to, CashflowFlag.None, SwapLeg.Price, SwapLeg.PriceSchedule, SwapLeg.Notional,
                                                                SwapLeg.AmortizationSchedule, SwapLeg.Amortizes,
                                                                generator, Dt.Empty, Dt.Empty, SwapLeg.PayLagRule, null);
      }
      return ps;
    }

    /// <summary>
    /// Get the payment schedule: a detailed representation of payments
    /// </summary>
    /// <param name="paymentSchedule">Payment schedule</param>
    /// <param name="from">Include payments starting from from date</param>
    /// <returns>payments associated to the swap</returns>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule paymentSchedule, Dt from)
    {
      return GetPaymentSchedule(paymentSchedule, from, Dt.Empty);
    }

    /// <summary>
    /// Present value of a swap leg at pricing as-of date given pricing arguments
    /// </summary>
    /// <returns>Present value of the swap leg</returns>
    public override double ProductPv()
    {
      if (Settle >= Product.EffectiveMaturity)
        return 0.0;

      var unitNotional = SwapLeg.Notional > 0 ? SwapLeg.Notional : 1.0;
      var ps = GetPaymentSchedule(null, AsOf);
      return ps.Pv(AsOf, Settle, DiscountCurve, null, IncludePaymentOnSettle, true) / unitNotional * CurrentNotional;
    }

    ///<summary>
    /// Find the known (fixed or reset) rate for current coupon period.
    ///</summary>
    ///<param name="includeProjection">For floating coupons validly not yet reset, if true return current projection</param>
    ///<returns>The current coupon rate on pricer settlement date</returns>
    /// <remarks>
    /// If floating and not yet reset (e.g. pricing date is reset date and not yet reset, or leg resets in arrears) then result is null.
    /// </remarks>
    public double? CurrentCoupon(bool includeProjection)
    {
      if (SwapLeg == null)
        return null;

      return !SwapLeg.Floating
               ? this.FixedCouponAt(Settle, SwapLeg.Price, SwapLeg.PriceSchedule)
               : this.FloatingCouponAt(AsOf, Settle, RateResets, includeProjection);
    }

    /// <summary>
    /// Gets the Notional Factor for amortizing swap leg or swap leg with a customized schedule.
    /// </summary>
    /// <remarks>Here we return current notional as a fraction of Original notional.</remarks>
    public double NotionalFactorAt(Dt asOf)
    {
      // This code has been adopted from the similar function in the BondPricer.
      var ps = SwapLeg.CustomPaymentSchedule;
      var not = 1.0;
      if (ps != null && ps.Count > 0)
      {
        not = PaymentScheduleUtils.GetEffectiveNotionalFromCustomizedSchedule(ps, asOf) / SwapLeg.Notional;
      }
      else
      {
        var amort = SwapLeg.AmortizationSchedule;
        if (amort != null && amort.Count > 0)
        {
          not = amort.PrincipalAt(1.0, asOf);
        }
      }
      return not;
    }

    /// <summary>
    ///   Validate pricer inputs
    /// </summary>
    /// <param name="errors">Error list </param>
    /// <remarks>
    ///   This tests only relationships between fields of the pricer that
    ///   cannot be validated in the property methods.
    /// </remarks>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if (DiscountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", "Invalid discount curve. Cannot be null");
    }

    /// <summary>
    ///   Reset the pricer
    ///   <preliminary/>
    /// </summary>
    ///
    /// <remarks>
    ///   
    /// </remarks>
    /// 
    /// <param name="what">The flags indicating what attributes to reset</param>
    /// <exclude/>
    public override void Reset(ResetAction what)
    {
      if (what == ResetSettle && IncludePaymentOnSettle && Product.Effective == ProductSettle)
        IncludePaymentOnSettle = false;
      base.Reset(what);
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   Swap leg Product
    /// </summary>
    public CommoditySwapLeg SwapLeg => (CommoditySwapLeg)Product;

    /// <summary>
    /// Historical Rate Resets
    /// </summary>
    public RateResets RateResets { get; internal set; }

    /// <summary>
    /// Accessor for the discount curve. 
    /// </summary>
    public DiscountCurve DiscountCurve { get; private set; }

    /// <summary>
    /// Reference index 
    /// </summary>
    public ReferenceIndex ReferenceIndex { get; private set; }

    /// <summary>
    /// Reference curve associated to the index. 
    /// </summary>
    public CalibratedCurve ReferenceCurve { get; private set; }

    /// <summary>
    /// Natural number of settle days for the product
    /// </summary>
    public int SpotDays { get; set; }

    ///<summary>
    /// The natural "Settle" date for the pricer
    ///</summary>
    /// <remarks>
    /// Will be the same as Settle except for forward pricing cases
    /// </remarks>
    public Dt ProductSettle => Dt.AddDays(AsOf, SpotDays, SwapLeg.Calendar);

    /// <summary>
    /// Include payment on trade settlement date
    /// </summary>
    public bool IncludePaymentOnSettle { get; set; }

    /// <summary>
    /// Build payment pricer
    /// </summary>
    public override IPricer BuildPaymentPricer(Payment payment, DiscountCurve discountCurve)
    {
      if (payment == null || payment.PayDt <= ProductSettle) return null;
      var oneTimeFee = new OneTimeFee(payment.Ccy, payment.Amount, payment.PayDt, "");
      var pricer = new SimpleCashflowPricer(oneTimeFee, AsOf, ProductSettle, discountCurve, null);
      pricer.Add(payment.PayDt, payment.Amount, 0.0, 0.0, 0.0, 0.0, 0.0);
      return pricer;
    }

    /// <summary>
    /// Payment pricer
    /// </summary>
    public override IPricer PaymentPricer
    {
      get
      {
        if (Payment == null) return paymentPricer_;
        return paymentPricer_ ?? (paymentPricer_ = BuildPaymentPricer(Payment, DiscountCurve));
      }
    }

    ///<summary>
    /// Present value of any additional payment associated with the pricer.
    ///</summary>
    ///<returns></returns>
    public override double PaymentPv()
    {
      return PaymentPricer != null && Payment.PayDt > ProductSettle ? PaymentPricer.Pv() : 0.0;
    }

    /// <summary>
    /// Valuation currency
    /// </summary>
    public override Currency ValuationCurrency => DiscountCurve.Ccy;

    #endregion Properties

    #region ILockedRatesPricerProvider Members

    /// <summary>
    ///   Get a pricer in which all the rate fixings with the reset dates on
    ///   or before the anchor date are fixed at the current projected values.
    /// </summary>
    /// <param name="anchorDate">The anchor date.</param>
    /// <returns>The original pricer instance if no rate locked;
    ///   Otherwise, the cloned pricer with the rates locked.</returns>
    /// <remarks>This method never modifies the original pricer,
    ///  whose states and behaviors remain exactly the same before
    ///  and after calling this method.</remarks>
    IPricer ILockedRatesPricerProvider.LockRatesAt(Dt anchorDate)
    {
      var swapleg = SwapLeg;
      if (!swapleg.Floating)
        return this;

      var ps = GetPaymentSchedule(null, AsOf);

      // For non-compounding coupon and custom schedule,
      // we just need to lock the coupons.
      var modified = GetModifiedRateResets(RateResets,
        ps.EnumerateProjectedRates(), anchorDate);
      if (modified == null) return this;

      // We need return a modified pricer.
      var pricer = (CommoditySwapLegPricer)ShallowCopy();
      pricer.RateResets = modified;
      return pricer;
    }

    IPricer ILockedRatesPricerProvider.LockRateAt(Dt asOf, IPricer otherPricer)
    {
      var swapleg = SwapLeg;
      if (!swapleg.Floating)
        return this;

      var ps = GetPaymentSchedule(null, asOf);
      var pmt = ps.GetPaymentsByType<CommodityFloatingPricePayment>().FirstOrDefault(fip => fip.ResetDate == asOf);
      if (pmt == null) //there is no rate reset on the pricing date
        return this;

      // For non-compounding coupon and custom schedule,
      // we just need to lock the coupons.
      var modified = GetModifiedRateResets(RateResets,
        ProjectAllFixings(asOf, ps, true), asOf);
      if (modified == null) return this;

      // We need return a modfied pricer.
      var pricer = (CommoditySwapLegPricer)ShallowCopy();
      pricer.RateResets = modified;
      return pricer;
    }

    private static RateResets GetModifiedRateResets(RateResets oldRateResets, 
      IEnumerable<RateReset> projectedRates, Dt anchorDate)
    {
      var resets = new SortedDictionary<Dt, double>();

      if (oldRateResets != null && oldRateResets.HasAllResets)
      {
        foreach (var rr in oldRateResets.AllResets)
        {
          if (rr.Key >= anchorDate || resets.ContainsKey(rr.Key))
            continue;

          resets.Add(rr.Key, rr.Value);
        }
      }

      int origCount = resets.Count;
      foreach (var rr in projectedRates)
      {
        if (rr.Date > anchorDate)
          continue;

        if (resets.ContainsKey(rr.Date))
        {
          if (rr.Date == anchorDate)
          {
            resets[rr.Date] = rr.Rate;
          }

          continue;
        }

        resets.Add(rr.Date, rr.Rate);
      }
      var retVal = resets.Count == origCount ? null : new RateResets(resets);
      if (oldRateResets != null && oldRateResets.HasCurrentReset && retVal != null)
      {
        retVal.CurrentReset = oldRateResets.CurrentReset;
      }

      return retVal;
    }

    // Enumerate the projected fixings instead of the coupon rates.
    // With compounding, a single coupon may consists of several fixings.
    private static IEnumerable<RateReset> ProjectAllFixings(Dt asOf,
      PaymentSchedule ps, bool withSpread)
    {
      if (ps == null) yield break;
      foreach (var d in ps.GetPaymentDates())
      {
        foreach (var p in ps.GetPaymentsOnDate(d))
        {
          var fip = p as CommodityFloatingPricePayment;
          if (fip == null || (!fip.IsProjected && !RateResetUtil.ProjectMissingRateReset(fip.ResetDate, asOf, fip.PeriodStartDate))) continue;
          foreach (var rr in EnumerateProjectedFixings(fip, withSpread))
            yield return rr;
        }
      }
    }

    // Find all the rate fixings used in the FloatingInterestPayment.
    private static IEnumerable<RateReset> EnumerateProjectedFixings(
      CommodityFloatingPricePayment fip, bool withSpread)
    {
      RateReset resets;
      var projector = fip.RateProjector;
      var schedule = fip.FixingSchedule;
      if (schedule.ObservationDates.Count > 1)
      {
        resets = Project(projector, schedule, withSpread ? fip.Spread : 0.0);
        if (resets != null) yield return resets;
      }
      resets = Project(projector, schedule, fip.Spread);
      if (resets != null) yield return resets;
    }

    // Find the projected rate for a single fixing.
    private static RateReset Project(IRateProjector projector, FixingSchedule fs, double spread)
    {
      var fixing = projector.Fixing(fs);
      return fixing == null || fixing.RateResetState != RateResetState.IsProjected
        ? null
        : new RateReset(fs.ResetDate, fixing.Forward + spread);
    }

    #endregion

  }

}