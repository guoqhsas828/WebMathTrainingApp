//
//  -2012. All rights reserved.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using log4net;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Pricers
{

  #region config

  /// <summary>
  /// Config settings class for the Swap Leg pricer 
  /// </summary>
  [Serializable]
  public class SwapLegPricerConfig
  {
    /// <exclude />
    [ToolkitConfig("backward compatibility flag , determines whether we need to discount the accrual or not")] public readonly bool DiscountingAccrued = true;
  }

  #endregion

  /// <summary>
  /// Pricer for interest rate swap legs
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.SwapLeg" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="CashflowPricer" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.SwapLeg"/>
  /// <seealso cref="SwapPricer"/>
  /// <seealso cref="BaseEntity.Toolkit.Models.CashflowModel"/>
  [Serializable]
  public partial class SwapLegPricer : PricerBase, IPricer, ILockedRatesPricerProvider
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof (SwapLegPricer));

    #region Constructors

    /// <summary>
    /// Default constructor
    /// </summary>
    public SwapLegPricer()
      : base(null)
    {
      PricerFlags = PricerFlags.SensitivityToAllRateTenors;
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
    /// <param name="fwdModelParams">Forward rate model parameters</param>
    /// <param name="fxCurve">fx rate curve</param>
    public SwapLegPricer(SwapLeg swap, Dt asOf, Dt settle, double notional, DiscountCurve discountCurve,
                         ReferenceIndex referenceIndex, CalibratedCurve referenceCurve,
                         RateResets rateResets, RateModelParameters fwdModelParams,
                         FxCurve fxCurve)
      : base(swap, asOf, settle)
    {
      PricerFlags = PricerFlags.SensitivityToAllRateTenors;
      if (swap.Floating && swap.ReferenceIndex == null && referenceIndex == null)
        throw new ToolkitException("A non null reference index should be provided for floating swap legs");
      ReferenceIndex = referenceIndex ?? swap.ReferenceIndex;
      if (ReferenceIndex != null && !Array.Exists(ReferenceIndex.ProjectionTypes, pt => pt == SwapLeg.ProjectionType))
        SwapLeg.ProjectionType = ReferenceIndex.ProjectionTypes[0];
      ReferenceCurve = referenceCurve;
      DiscountCurve = discountCurve;
      Notional = notional;
      DiscountingAccrued = settings_.SwapLegPricer.DiscountingAccrued;
      IncludePaymentOnSettle = false;
      if (ReferenceIndex != null)
        RateResets = rateResets;
      ReferenceIsDiscount = false;
      if (ReferenceCurve != null)
        ReferenceIsDiscount = Equals(ReferenceCurve, DiscountCurve) ||
                              String.Equals(ReferenceCurve.Name, DiscountCurve.Name);
      FxCurve = fxCurve;
      FwdRateModelParameters = fwdModelParams;
      ApproximateForFastCalculation = false;
    }

    #endregion

    #region Methods

    #region Initialization 

    private ProjectionParams GetProjectionParams()
    {
      ProjectionFlag flags = ProjectionFlag.None;
      if (SwapLeg.InArrears)
        flags |= ProjectionFlag.ResetInArrears;
      if (SwapLeg.IsZeroCoupon)
        flags |= ProjectionFlag.ZeroCoupon;
      if (SwapLeg.WithDelay)
        flags |= ProjectionFlag.ResetWithDelay;
      if (ApproximateForFastCalculation)
        flags |= ProjectionFlag.ApproximateProjection;
      var retVal = new ProjectionParams
                     {
                       ProjectionType = SwapLeg.ProjectionType,
                       CompoundingFrequency = SwapLeg.CompoundingFrequency,
                       CompoundingConvention = SwapLeg.CompoundingConvention,
                       ResetLag = SwapLeg.ResetLag,
                       YoYRateTenor = SwapLeg.IndexTenor,
                       ProjectionFlags = flags
                     };
      var inflationSwap = SwapLeg as InflationSwapLeg;
      if (inflationSwap != null)
        retVal.IndexationMethod = inflationSwap.IndexationMethod;
      return retVal;
    }

    private IRateProjector GetRateProjector(ProjectionParams projectionParams)
    {
      var calculator = CouponCalculator.Get(AsOf, ReferenceIndex, ReferenceCurve, DiscountCurve, projectionParams);
      calculator.UseAsOfResets = UseAsOfResets;
      return calculator;
    }

    private void SetRateProjector(IRateProjector rateProjector)
    {
      var couponCalculator = rateProjector as CouponCalculator;
      if (couponCalculator != null)
      {
        couponCalculator.ReferenceCurve = ReferenceCurve;
        if (couponCalculator.DiscountCurve != null)
          couponCalculator.DiscountCurve = DiscountCurve;
      }
    }

    private IForwardAdjustment GetForwardAdjustment(ProjectionParams projectionParams)
    {
      return ForwardAdjustment.Get(AsOf, DiscountCurve, FwdRateModelParameters, projectionParams);
    }

    #endregion

    
 
    /// <summary>
    /// Shallow copy 
    /// </summary>
    /// <returns>A new swap leg pricer object.</returns>
    public override object Clone()
    {
      return new SwapLegPricer((SwapLeg) Product, AsOf, Settle, Notional, DiscountCurve, ReferenceIndex, ReferenceCurve,
                               RateResets, FwdRateModelParameters, FxCurve);
    }

    /// <summary>
    ///   Total accrued interest for product to settlement date given pricing arguments
    /// </summary>
    ///
    /// <returns>Total accrued interest</returns>
    ///
    public override double Accrued()
    {
      if (Settle <= Product.Effective || Settle >= Product.EffectiveMaturity)
        return 0.0;
      var swap = (SwapLeg)Product;
      var paymentSchedule = GetPaymentSchedule(null, Settle, Settle);

      var fip = paymentSchedule.GetPaymentsByType<InterestPayment>().FirstOrDefault(p => p.AccrualStart < Settle && p.PayDt > Settle);
      if (fip == null)
      {
        double previousCouponAdj = 0.0;
        if (HasUnSettledLagPayment(Settle, TradeSettle))
        {
          var tradeCfa = new CashflowAdapter(GetPaymentSchedule(null, TradeSettle), swap.Notional);
          for (int i = 0, n = tradeCfa.Count; i < n; i++)
          {
            var payDt = tradeCfa.GetDt(i);
            if (payDt > TradeSettle)
            {
              if (payDt > Settle && GetBackwardCompatibleEndDate(tradeCfa, i) <= Settle)
                previousCouponAdj += tradeCfa.GetAccrued(i);
              else if (payDt > Settle)
                break;
            }
          }
        }
        return previousCouponAdj * Notional;
      }

      if (swap.CustomPaymentSchedule != null)
      {
        var accrualFraction = Dt.Fraction(fip.CycleStartDate, fip.CycleEndDate, fip.AccrualStart, Settle, swap.DayCount, swap.Freq) /
                              Dt.Fraction(fip.CycleStartDate, fip.CycleEndDate, fip.AccrualStart, fip.AccrualEnd, swap.DayCount, swap.Freq);
        return (accrualFraction > 0)
          ? (fip.Amount / swap.Notional) * Notional * accrualFraction *
            (fip.FXCurve == null ? 1.0 : fip.FXCurve.FxRate(Settle, swap.Ccy, ValuationCurrency))
          : 0.0;
      }
      else
      {
        var accrualFactor = swap.Schedule.Fraction(fip.AccrualStart, Settle, fip.DayCount);
        double coupon = fip.EffectiveRate;
        double previousCouponAdj = 0.0;
        if (HasUnSettledLagPayment(Settle, TradeSettle))
        {
          var tradeCfa = new CashflowAdapter(GetPaymentSchedule(null, TradeSettle), swap.Notional);
          for (int i = 0, n = tradeCfa.Count; i < n; i++)
          {
            Dt payDt = tradeCfa.GetDt(i);
            if (payDt > TradeSettle)
            {
              if (payDt > Settle && GetBackwardCompatibleEndDate(tradeCfa, i) <= Settle)
                previousCouponAdj += tradeCfa.GetAccrued(i);
              else if (payDt > Settle)
                break;
            }
          }
        }

        return ((accrualFactor > 0
                 ? (fip.Notional / swap.Notional) * coupon * accrualFactor *
                   (fip.FXCurve == null ? 1.0 : fip.FXCurve.FxRate(Settle, swap.Ccy, ValuationCurrency))
                 : 0.0) + previousCouponAdj) * Notional;
      }
    }

    //This function is specifically for the swap leg accrued calculations.
    //In the legacy cashflow, when flag IncludeEndDateInAccrual is true,
    // it still used the peroid end date to calculate the accrual, but
    // we should add one day. The payment schedule method performs the 
    // right way. Here to get the backward compatible, we substract one day.
    // Currently this only affect serveral IBoxxTRS trades.
    private Dt GetBackwardCompatibleEndDate(CashflowAdapter cfa, int idx)
    {
      var ips = cfa.Data as List<InterestPayment>;
      if (ips != null && ips[idx].IncludeEndDateInAccrual)
      {
        return cfa.GetEndDt(idx) - 1;
      }
      return cfa.GetEndDt(idx);
    }


    /// <summary>
    /// Check if pricing a trade settled in prior period with unsettled coupon/principal payment delayed as of pricing settle date
    /// </summary>
    /// <param name="settle">Pricing settle date</param>
    /// <param name="tradeSettle">Trade settle date</param>
    /// <returns>True if pricing a trade settled in prior period with unsettled coupon/principal payment delayed as of pricing settle date</returns>
    private bool HasUnSettledLagPayment(Dt settle, Dt tradeSettle)
    {
      if (SwapLeg.PaymentLagRule == null || tradeSettle.IsEmpty())
        return false;

      Dt prevPeriodEnd = SwapLeg.Schedule.GetPrevCouponDate(settle);
      Dt prevPayDate = prevPeriodEnd.IsEmpty() ? Product.Effective 
        : PayLagRule.CalcPaymentDate(prevPeriodEnd, 
        SwapLeg.PaymentLagRule.PaymentLagDays, 
        SwapLeg.PaymentLagRule.PaymentLagBusinessFlag, 
        SwapLeg.BDConvention, SwapLeg.Calendar);

      bool retVal = !tradeSettle.IsEmpty() && !prevPeriodEnd.IsEmpty() 
        && (tradeSettle < prevPeriodEnd && settle < prevPayDate);
      return retVal;
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
        foreach (Dt d in SwapLeg.CustomPaymentSchedule.GetPaymentDates())
        {
          if (d >= from)
          {
            var paymentsOnDate = CloneUtil.CloneToGenericList(SwapLeg.CustomPaymentSchedule.GetPaymentsOnDate(d).ToList());
            if (FxCurve != null)
            {
              foreach (Payment pay in paymentsOnDate)
                pay.FXCurve = FxCurve;
            }
            // Update rate resets in floating interest payment objects at this point, taking into account the pricing date:
            if (SwapLeg.Floating)
            {
              var projParams = GetProjectionParams();
              var rateProjector = GetRateProjector(projParams);
              var forwardAdjustment = GetForwardAdjustment(projParams);
              foreach (Payment pay in paymentsOnDate)
              {
                if (!pay.VolatilityStartDt.IsEmpty())
                {
                  pay.VolatilityStartDt = AsOf;
                }
                var flp = pay as FloatingInterestPayment;
                if (flp != null)
                {
                  if (flp.RateProjector == null) flp.RateProjector = rateProjector;
                  if (flp.ForwardAdjustment == null) flp.ForwardAdjustment = forwardAdjustment;
                  RateResets.UpdateResetsInCustomCashflowPayments(flp,
                    flp.ResetDate < AsOf && flp.PeriodEndDate > AsOf, false);
                  SetRateProjector(flp.RateProjector);
                }
              }
            }
            ps.AddPayments(paymentsOnDate);
          }
          if (to.IsValid() && d > to)
            break;
        }
        ps.OfType<InterestPayment>().EnableInterestsCompounding(
          SwapLeg.CompoundingConvention);
        return ps;
      }
      if (!SwapLeg.Floating)
      {
        ps = PaymentScheduleUtils.FixedRatePaymentSchedule(from, to, SwapLeg.Ccy, SwapLeg.Schedule, SwapLeg.CashflowFlag,
                                                           SwapLeg.Coupon, SwapLeg.CouponSchedule, SwapLeg.Notional,
                                                           SwapLeg.AmortizationSchedule, (SwapLeg.Amortizes && SwapLeg.IntermediateExchange), 
                                                           SwapLeg.DayCount, SwapLeg.CompoundingFrequency, FxCurve, false, Dt.Empty, Dt.Empty, SwapLeg.PaymentLagRule, null);
      }
      else
      {
        ProjectionParams projParams = GetProjectionParams();
        IRateProjector rateProjector = GetRateProjector(projParams);
        IForwardAdjustment forwardAdjustment = GetForwardAdjustment(projParams);
        ps = PaymentScheduleUtils.FloatingRatePaymentSchedule(
          from, to, 
          SwapLeg.Ccy, 
          rateProjector, forwardAdjustment, RateResets, 
          SwapLeg.Schedule, 
          SwapLeg.CashflowFlag,
          SwapLeg.Coupon, 
          SwapLeg.CouponSchedule,
          SwapLeg.Notional, 
          SwapLeg.AmortizationSchedule, 
          (SwapLeg.Amortizes && SwapLeg.IntermediateExchange),
          SwapLeg.DayCount, FxCurve, projParams, 
          SwapLeg.Cap, SwapLeg.Floor, 
          SwapLeg.IndexMultiplierSchedule, false, Dt.Empty, Dt.Empty, SwapLeg.PaymentLagRule, null);
      }
      if (from <= SwapLeg.Effective && SwapLeg.InitialExchange)
        ps.AddPayment(new PrincipalExchange(SwapLeg.Effective, 
          -SwapLeg.AmortizationSchedule.PrincipalAt(SwapLeg.Notional, 
          SwapLeg.Effective), SwapLeg.Ccy)
                      {FXCurve = FxCurve});
      if (SwapLeg.FinalExchange)
      {
        Dt maturity = SwapLeg.Schedule.GetPaymentDate(SwapLeg.Schedule.Count - 1);
        if (!to.IsValid() || maturity <= to)
          ps.AddPayment(new PrincipalExchange(maturity, SwapLeg.AmortizationSchedule.PrincipalAt(SwapLeg.Notional, maturity), SwapLeg.Ccy) {FXCurve = FxCurve});
      }
      return ps;
    }

    /// <summary>
    /// Get the payment schedule: a detailed representation of payments
    /// </summary>
    /// <param name="paymentSchedule">Payment schedule</param>
    /// <param name="from">Include payments starting from from date</param>
    /// <returns>payments associated to the swap</returns>
    /// <remarks>If there are no payments between from and to this method will return the payment/payments on the payment date immediately following from</remarks>
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
      var cashflowFromDate = GetCashflowsFromDate(Settle);
      var ps = GetPaymentSchedule(null, cashflowFromDate);
      return ps.Pv(AsOf, Settle, DiscountCurve, null, IncludePaymentOnSettle,
        DiscountingAccrued) / unitNotional * CurrentNotional;
    }

    private Dt GetCashflowsFromDate(Dt settle)
    {
      var sl = SwapLeg;
      // In this case we use the logic from the Cashflow function.
      var cashflowsFromDate = settle;
      if (sl.PaymentLagRule != null && sl.PaymentLagRule.PaymentLagDays > 0)
      {
        cashflowsFromDate = GenerateFromDate(settle, Dt.Empty);
      }

      return cashflowsFromDate;
    }

    private Dt GenerateFromDate(Dt settle, Dt tradeSettle)
    {
      var swapLeg = SwapLeg;
      for (int idx = 0; idx < swapLeg.Schedule.Count; idx++)
      {
        if (settle == tradeSettle && settle == swapLeg.Schedule.GetPeriodEnd(idx))
          return settle;
        if (swapLeg.Schedule.GetPeriodEnd(idx) > tradeSettle)
        {
          var payDt = swapLeg.PaymentLagRule.PaymentLagBusinessFlag
                         ? Dt.AddDays(swapLeg.Schedule.GetPeriodEnd(idx), swapLeg.PaymentLagRule.PaymentLagDays, swapLeg.Calendar)
                         : Dt.Roll(Dt.Add(swapLeg.Schedule.GetPeriodEnd(idx), swapLeg.PaymentLagRule.PaymentLagDays, TimeUnit.Days), swapLeg.Schedule.BdConvention,
                         swapLeg.Schedule.Calendar);
          if (payDt > tradeSettle && payDt > settle)
            return Dt.Add(swapLeg.Schedule.GetPeriodEnd(idx), -1); //Historical accrual or principal entitled to the trade but not paid yet
        }
      }
      return tradeSettle;
    }

    // The old cash flow based PV.
 
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
               ? this.FixedCouponAt(Settle, SwapLeg.Coupon, SwapLeg.CouponSchedule)
               : this.FloatingCouponAt(AsOf, Settle, RateResets, includeProjection);
    }

    /// <summary>
    /// Gets the Notional Factor for amortizing swap leg or swap leg with a customized schedule.
    /// </summary>
    /// <remarks>Here we return current notional as a fraction of Original notional.</remarks>
    public double NotionalFactorAt(Dt asOf)
    {
      // This code has been adopted from the similar function in the BondPricer.
      PaymentSchedule ps = SwapLeg.CustomPaymentSchedule;
      double not = 1.0;
      if (ps != null && ps.Count > 0)
      {
        double scale = SwapLeg.Notional;
        not = PaymentScheduleUtils.GetEffectiveNotionalFromCustomizedSchedule(ps, asOf) / scale;
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
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));
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
      if (what == ResetSettle)
      {
        if (IncludePaymentOnSettle)
        {
          if (Product.Effective == ProductSettle)
          {
            IncludePaymentOnSettle = false;
          }
        }
      }
      base.Reset(what);
    }

    #endregion Methods

    #region Properties
    /// <summary>
    ///   Swap leg Product
    /// </summary>
    public SwapLeg SwapLeg
    {
      get { return (SwapLeg) Product; }
    }

    /// <summary>
    /// Accessor for model parameters used for convexity adjustments
    /// </summary>
    public RateModelParameters FwdRateModelParameters { get;  set; }

    /// <summary>
    /// Historical Rate Resets
    /// </summary>
    public RateResets RateResets { get;  set; }

    /// <summary>
    /// Set to true if the discount curve and the reference curve of the reference index are the same. By default it is true. 
    /// </summary>
    public bool ReferenceIsDiscount { get; private set; }


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
    /// Survival curve in case payments are credit contingent
    /// </summary>
    public SurvivalCurve SurvivalCurve { get; set; }

    /// <summary>
    /// FxCurve 
    /// </summary>
    public FxCurve FxCurve { get; private set; }

    /// <summary>
    /// Accessor for the fx basis curve. This is only used for internal purposes to compute sensitivities
    /// </summary>
    public CalibratedCurve BasisAdjustment
    {
      get
      {
        if (FxCurve != null)
        {
          if (FxCurve.IsSupplied)
            return null;
          return FxCurve.BasisCurve;
        }
        return null;
      }
      set
      {
        if (FxCurve != null && FxCurve.BasisCurve != null)
          FxCurve.BasisCurve.Set(value);
      }
    }

    /// <summary>
    /// Accessor for the Discounting Accrued field for the SwapLegPricer
    /// </summary>
    public bool DiscountingAccrued { get; set; }

    /// <summary>
    /// Include payment on trade settlement date
    /// </summary>
    public bool IncludePaymentOnSettle { get; set; }

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
    public Dt ProductSettle
    {
      get
      {
        return Dt.AddDays(AsOf, SpotDays, SwapLeg.Calendar);
      }
    }

    /// <summary>
    ///  Use As-Of Resets
    /// </summary>
    /// <value>The valuation currency.</value>
    public bool UseAsOfResets 
    {
      get { return _useAsOfResets; }
      set { _useAsOfResets = value; }
    }

    /// <summary>
    ///  Set to true by default
    /// </summary>
    private bool _useAsOfResets = true;

    /// <summary>
    /// Build payment pricer
    /// </summary>
    public override IPricer BuildPaymentPricer(Payment payment, DiscountCurve discountCurve)
    {
      if (payment != null)
      {
        if (payment.PayDt > ProductSettle) // strictly greater than
        {
          var oneTimeFee = new OneTimeFee(payment.Ccy, payment.Amount, payment.PayDt, "");
          var pricer = new SimpleCashflowPricer(oneTimeFee, AsOf, ProductSettle, discountCurve, null);
          pricer.Add(payment.PayDt, payment.Amount, 0.0, 0.0, 0.0, 0.0, 0.0);
          return pricer;
        }
      }
      return null;
    }

    /// <summary>
    /// Payment pricer
    /// </summary>
    public override IPricer PaymentPricer
    {
      get
      {
        if (Payment != null)
        {
          if (paymentPricer_ == null)
            paymentPricer_ = BuildPaymentPricer(Payment, DiscountCurve);
        }
        return paymentPricer_;
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
    public override Currency ValuationCurrency
    {
      get
      {
        return DiscountCurve.Ccy;
      }
    }

    /// <summary>
    /// Notional expressed in valuation currency
    /// </summary>
    public double ValuationCcyNotional
    {
      get
      {
        if (DiscountCurve == null || FxCurve == null)
          return Notional;
        return (SwapLeg.Ccy == ValuationCurrency) ? Notional : Notional * FxCurve.SpotFxRate.GetRate(SwapLeg.Ccy, ValuationCurrency);
      }
    }

    /// <summary>
    /// Trade Settle
    /// </summary>
    public Dt TradeSettle { get; set; }

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

      RateResets modified;
      var ps = GetPaymentSchedule(null, AsOf);

      if (swapleg.CompoundingConvention == CompoundingConvention.None ||
        (swapleg.CustomPaymentSchedule != null &&
        swapleg.CustomPaymentSchedule.Count > 0))
      {
        // For non-compounding coupon and custom schedule,
        // we just need to lock the coupons.
        modified = GetModifiedRateResets(RateResets,
          ps.EnumerateProjectedRates(), anchorDate);
        if (modified == null) return this;

        // We need return a modified pricer.
        var pricer = (SwapLegPricer)ShallowCopy();
        pricer.RateResets = modified;
        return pricer;
      }
      else
      {
        // For regular case we need lock the rate fixings and
        // put them in ReferenceIndex.HistoricalObservations.
        modified = GetModifiedRateResets(ReferenceIndex.HistoricalObservations,
          EnumerateProjectedFixings(ps, false), anchorDate);
        if (modified == null) return this;

        // We need to modify both pricer and reference index,
        // so make both shallow copies.
        var pricer = (SwapLegPricer)ShallowCopy();
        var index = pricer.ReferenceIndex =
          (ReferenceIndex)pricer.ReferenceIndex.ShallowCopy();
        index.HistoricalObservations = modified;
        return pricer;
      }
    }

    IPricer ILockedRatesPricerProvider.LockRateAt(Dt asOf, IPricer otherPricer)
    {
      var swapleg = SwapLeg;
      if (!swapleg.Floating)
        return this;

      RateResets modified;
      var ps = GetPaymentSchedule(null, asOf);

      var pmt = ps.GetPaymentsByType<FloatingInterestPayment>().FirstOrDefault(fip => fip.ResetDate == asOf);
      if (pmt == null) //there is no rate reset on the pricing date
        return this;


      if (swapleg.CompoundingConvention == CompoundingConvention.None ||
        (swapleg.CustomPaymentSchedule != null &&
        swapleg.CustomPaymentSchedule.Count > 0))
      {
        // For non-compounding coupon and custom schedule,
        // we just need to lock the coupons.
        modified = GetModifiedRateResets(RateResets,
          ProjectAllFixings(asOf, ps, true), asOf);
        if (modified == null) return this;

        // We need return a modfied pricer.
        var pricer = (SwapLegPricer)ShallowCopy();
        pricer.RateResets = modified;
        return pricer;
      }
      else
      {
        modified = GetModifiedRateResets(ReferenceIndex.HistoricalObservations,
          ProjectAllFixings(asOf, ps, false), asOf);
        if (modified == null) return this;

        var pricer = (SwapLegPricer)ShallowCopy();
        var index = pricer.ReferenceIndex =
          (ReferenceIndex)pricer.ReferenceIndex.ShallowCopy();
        index.HistoricalObservations = modified;
        return pricer;
      }
    }

    //
    // The following are general utilities and maybe refactory to a separate class?
    //

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
      foreach (Dt d in ps.GetPaymentDates())
      {
        foreach (Payment p in ps.GetPaymentsOnDate(d))
        {
          var fip = p as FloatingInterestPayment;
          if (fip == null || (!fip.IsProjected && !RateResetUtil.ProjectMissingRateReset(fip.ResetDate, asOf, fip.PeriodStartDate))) continue;
          foreach (var rr in EnumerateProjectedFixings(fip, withSpread))
            yield return rr;
        }
      }
    }

    // Enumerate the projected fixings instead of the coupon rates.
    // With compounding, a single coupon may consists of several fixings.
    private static IEnumerable<RateReset> EnumerateProjectedFixings(
      PaymentSchedule ps, bool withSpread)
    {
      if (ps == null) yield break;
      foreach (Dt d in ps.GetPaymentDates())
      {
        foreach (Payment p in ps.GetPaymentsOnDate(d))
        {
          var fip = p as FloatingInterestPayment;
          if (fip == null || !fip.IsProjected) continue;
          foreach (var rr in EnumerateProjectedFixings(fip, withSpread))
            yield return rr;
        }
      }
    }

    // Find all the rate fixings used in the FloatingInterestPayment.
    private static IEnumerable<RateReset> EnumerateProjectedFixings(
      FloatingInterestPayment fip, bool withSpread)
    {
      RateReset reset;
      var projector = fip.RateProjector;
      var compoundingPeriods = fip.CompoundingPeriods;
      if (compoundingPeriods.Count > 1)
      {
        switch (fip.CompoundingConvention)
        {
          case CompoundingConvention.ISDA:
          case CompoundingConvention.FlatISDA:
          case CompoundingConvention.Simple:
            foreach (var period in compoundingPeriods)
            {
              reset = Project(projector, period.Item3, withSpread ? fip.FixedCoupon : 0.0);
              if (reset != null) yield return reset;
            }
            yield break;
          default:
            throw new ArgumentException("Compounding convention not supported");
        }
      }
      reset = Project(projector, compoundingPeriods[0].Item3, fip.FixedCoupon);
      if (reset != null) yield return reset;
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