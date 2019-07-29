
/*
 * PaymentScheduleUtil.cs
*/



using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;


namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// 
  /// </summary>
  public interface IExDivCalculator
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="couponDate"></param>
    /// <returns></returns>
    Dt Calc(Dt couponDate);
  }

  /// <summary>
  /// Utils for filling a standard payment schedule
  /// </summary>
  public static class PaymentScheduleUtils
  {
    #region Delegates

    /// <summary>
    /// Generate Interest Payment
    /// </summary>
    /// <param name="prevPay">Previous payment date</param>
    /// <param name="paymentDate">Payment date</param>
    /// <param name="cycleStart">Cycle start date</param>
    /// <param name="cycleEnd">Cycle end date</param>
    /// <param name="periodStart">Period start date</param>
    /// <param name="periodEnd">Period end date</param>
    /// <param name="notional">Notional</param>
    /// <param name="coupon">Coupon</param>
    /// <param name="fraction">Fraction</param>
    /// <param name="includeEndDateInAccrual">Flag</param>
    /// <param name="accrueOnCycle">Flag</param>
    /// <returns>Payment</returns>
    public delegate Payment InterestPaymentGenerator(
      Dt prevPay, Dt paymentDate, Dt cycleStart, Dt cycleEnd, Dt periodStart, Dt periodEnd,
      double notional, double coupon, double fraction,
      bool includeEndDateInAccrual, bool accrueOnCycle);

    /// <summary>
    /// Generate Floating Interest Payment
    /// </summary>
    /// <param name="prevPay">Previous payment date</param>
    /// <param name="paymentDate">Payment date</param>
    /// <param name="cycleStart">Cycle start date</param>
    /// <param name="cycleEnd">Cycle end date</param>
    /// <param name="periodStart">Period start date</param>
    /// <param name="periodEnd">Period end date</param>
    /// <param name="notional">Notional</param>
    /// <param name="coupon">Coupon</param>
    /// <param name="fraction">Fraction</param>
    /// <param name="includeEndDateInAccrual">Flag</param>
    /// <param name="accrueOnCycle">Flag</param>
    /// <param name="indexMultiplier">Index rate multiplier</param>
    /// <returns>Payment</returns>
    public delegate Payment FloatingInterestPaymentGenerator(
      Dt prevPay, Dt paymentDate, Dt cycleStart, Dt cycleEnd, Dt periodStart, Dt periodEnd,
      double notional, double coupon, double fraction,
      bool includeEndDateInAccrual, bool accrueOnCycle, double indexMultiplier);

    /// <summary>
    /// Generate Commodity Payment
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="paymentDate">Payment date</param>
    /// <param name="periodStart">Period start date</param>
    /// <param name="periodEnd">Period end date</param>
    /// <param name="notional">Notional</param>
    /// <param name="price">Spread</param>
    /// <returns>Payment</returns>
    public delegate Payment CommodityPaymentGenerator(Dt effective, Dt paymentDate, Dt periodStart, Dt periodEnd, double notional, double price);


    private delegate Payment PaymentGenerator(int schedIdx, Dt prevPayDt, Dt payDt, bool defaulted, double notional, double cpn, double indexMultiplier);

    #endregion

    #region Factory

    /// <summary>
    /// Creates the payment schedule for a product that pays fixed coupons. 
    ///  This includes all payments starting from from inclusive and prior to to
    /// </summary>
    /// <param name="from">Include all payments on or after from date</param>
    /// <param name="to">Include all payments prior to to date</param>
    /// <param name="ccy">Payment currency</param>
    /// <param name="sched">Schedule</param>
    /// <param name="flags">Cashflow flags</param>
    /// <param name="coupon">Coupon</param>
    /// <param name="coupons">Coupon schedule</param>
    /// <param name="initialNotional">Initial notional</param>
    /// <param name="amortizations">Amortization schedule </param>
    /// <param name="intermediateExchange">Principal exchanges everytime notional change</param>
    /// <param name="dc">Daycount for accrual calculation</param>
    /// <param name="compoundingFreq">Compounding frequency for zero coupon </param>
    /// <param name="fxCurve">Fx curve</param>
    /// <param name="includeTradeSettleCf">True to include cash flow at settle</param>
    /// <param name="defaultDt">Default date</param>
    /// <param name="defaultSettleDt">Default settlement date</param>
    /// <param name="paymentLag"></param>
    /// <param name="exDivCalculator"></param>
    /// <returns>A payment schedule for a standard product paying fixed coupon. 
    /// The payment for the period containing defaultDt is paid at defaultSettleDt and is accrued to defaultDt</returns>
    public static PaymentSchedule FixedRatePaymentSchedule(Dt from, Dt to, Currency ccy, Schedule sched,
                                                           CashflowFlag flags, double coupon, IList<CouponPeriod> coupons,
                                                           double initialNotional, IList<Amortization> amortizations, bool intermediateExchange,
                                                           DayCount dc, Frequency compoundingFreq, FxCurve fxCurve,
                                                           bool includeTradeSettleCf, Dt defaultDt, Dt defaultSettleDt,
                                                           PayLagRule paymentLag, IExDivCalculator exDivCalculator)
    {
      var zeroCoupon = (sched.Count == 1 && sched.Frequency == Frequency.None);
      InterestPaymentGenerator generator = (prevPay, paymentDate, cycleStart, cycleEnd, periodStart, periodEnd, notional, cpn, fraction,
                                            includeEndDateInAccrual, accrueOnCycle) =>
        new FixedInterestPayment(prevPay, paymentDate, ccy, cycleStart, cycleEnd, periodStart, periodEnd,
                                 exDivCalculator != null ? exDivCalculator.Calc(periodEnd) : Dt.Empty, notional, cpn,
                                 dc, compoundingFreq)
        {
          AccrueOnCycle = accrueOnCycle,
          IncludeEndDateInAccrual = includeEndDateInAccrual,
          AccrualFactor = fraction,
          ZeroCoupon = zeroCoupon,
          FXCurve = fxCurve
        };
      return sched.InterestPaymentScheduleFactory(@from, to, flags, coupon, coupons, initialNotional, amortizations, intermediateExchange, dc, generator,
                                                  includeTradeSettleCf,
                                                  defaultDt, defaultSettleDt, paymentLag, null);
    }

    /// <summary>
    /// Create a PaymentSchedule of FloatingInterestPayments from a standard schedule
    /// </summary>
    /// <param name="from">From date</param>
    /// <param name="to">To date</param>
    /// <param name="ccy">Payment currency</param>
    /// <param name="rateProjector">Engine for forward fixing projections</param>
    /// <param name="forwardAdjustment">Engine for convexity adjustment</param>
    /// <param name="resets">Historical resets</param>
    /// <param name="sched">Coupon schedule</param>
    /// <param name="flags">Cashflow flags</param>
    /// <param name="coupon">Spread over floating fixing</param>
    /// <param name="coupons">Coupon schedule</param>
    /// <param name="initialNotional">Initial notional</param>
    /// <param name="amortizations">Amortization schedule</param>
    /// <param name="intermediateExchange">Principal exchanges everytime notional change</param>
    /// <param name="dc">Daycount</param>
    /// <param name="fxCurve">FX curve</param>
    /// <param name="projectionParams">Projection parameters</param>
    /// <param name="cap">Cap</param>
    /// <param name="floor">Floor</param>
    /// <param name="includeTradeSettleCf">True to include cash flow at settle</param>
    /// <param name="defaultDt">Default date</param>
    /// <param name="defaultSettleDt">Default settlement date</param>
    /// <param name="paymentLag"></param>
    /// <param name="exDivCalculator"></param>
    /// <returns>Floating PaymentSchedule
    /// The payment for the period containing defaultDt is paid at defaultSettleDt and is accrued to defaultDt</returns>
    public static PaymentSchedule FloatingRatePaymentSchedule(Dt from, Dt to, Currency ccy,
      IRateProjector rateProjector,
      IForwardAdjustment forwardAdjustment, RateResets resets,
      Schedule sched, CashflowFlag flags, double coupon,
      IList<CouponPeriod> coupons, double initialNotional,
      IList<Amortization> amortizations, bool intermediateExchange, DayCount dc,
      FxCurve fxCurve, ProjectionParams projectionParams,
      double? cap, double? floor, bool includeTradeSettleCf,
      Dt defaultDt, Dt defaultSettleDt, PayLagRule paymentLag, IExDivCalculator exDivCalculator)
    {
      return FloatingRatePaymentSchedule(@from, to, ccy, rateProjector, forwardAdjustment, resets, sched, flags, coupon, coupons, initialNotional,
      amortizations, intermediateExchange, dc, fxCurve, projectionParams, cap, floor, null, includeTradeSettleCf,
      defaultDt, defaultSettleDt, paymentLag, exDivCalculator);
    }

    /// <summary>
    /// Create a PaymentSchedule of FloatingInterestPayments from a standard schedule
    /// </summary>
    /// <param name="from">From date</param>
    /// <param name="to">To date</param>
    /// <param name="ccy">Payment currency</param>
    /// <param name="rateProjector">Engine for forward fixing projections</param>
    /// <param name="forwardAdjustment">Engine for convexity adjustment</param>
    /// <param name="resets">Historical resets</param>
    /// <param name="sched">Coupon schedule</param>
    /// <param name="flags">Cashflow flags</param>
    /// <param name="coupon">Spread over floating fixing</param>
    /// <param name="coupons">Coupon schedule</param>
    /// <param name="initialNotional">Initial notional</param>
    /// <param name="amortizations">Amortization schedule</param>
    /// <param name="intermediateExchange">Principal exchanges everytime notional change</param>
    /// <param name="dc">Daycount</param>
    /// <param name="fxCurve">FX curve</param>
    /// <param name="projectionParams">Projection parameters</param>
    /// <param name="cap">Cap</param>
    /// <param name="floor">Floor</param>
    /// <param name="rateMultiplierSchedule">Rate multiplier schedule</param>
    /// <param name="includeTradeSettleCf">True to include cash flow at settle</param>
    /// <param name="defaultDt">Default date</param>
    /// <param name="defaultSettleDt">Default settlement date</param>
    /// <param name="paymentLag"></param>
    /// <param name="exDivCalculator"></param>
    /// <returns>Floating PaymentSchedule
    /// The payment for the period containing defaultDt is paid at defaultSettleDt and is accrued to defaultDt</returns>
    public static PaymentSchedule FloatingRatePaymentSchedule(Dt from, Dt to, Currency ccy,
                                                              IRateProjector rateProjector,
                                                              IForwardAdjustment forwardAdjustment, RateResets resets,
                                                              Schedule sched, CashflowFlag flags, double coupon,
                                                              IList<CouponPeriod> coupons, double initialNotional,
                                                              IList<Amortization> amortizations, bool intermediateExchange, DayCount dc,
                                                              FxCurve fxCurve, ProjectionParams projectionParams,
                                                              double? cap, double? floor, IList<CouponPeriod> rateMultiplierSchedule, bool includeTradeSettleCf,
                                                              Dt defaultDt, Dt defaultSettleDt, PayLagRule paymentLag, IExDivCalculator exDivCalculator)
    {
      FloatingInterestPaymentGenerator generator = (prevPay, paymentDate, cycleStart, cycleEnd, periodStart, periodEnd, notional, cpn, fraction,
                                            includeEndDateInAccrual, accrueOnCycle, indexMultiplier) =>
        new FloatingInterestPayment(prevPay, paymentDate, ccy, cycleStart, cycleEnd, periodStart, periodEnd,
                                    exDivCalculator != null ? exDivCalculator.Calc(periodEnd) : Dt.Empty, notional, cpn,
                                    dc,
                                    projectionParams.CompoundingFrequency, projectionParams.CompoundingConvention,
                                    rateProjector, forwardAdjustment, sched.CycleRule, indexMultiplier)
        {
          SpreadType = projectionParams.SpreadType,
          AccrueOnCycle = accrueOnCycle,
          FXCurve = fxCurve,
          AccrualFactor = fraction,
          Cap = cap,
          Floor = floor,
          IncludeEndDateInAccrual = includeEndDateInAccrual
        };
      return sched.FloatingInterestPaymentScheduleFactory(@from, to, flags, coupon, coupons, initialNotional, amortizations, intermediateExchange, dc, generator,
                                                  includeTradeSettleCf, defaultDt, defaultSettleDt, paymentLag, resets, rateMultiplierSchedule);
    }


    /// <summary>
    /// Generate PaymentSchedule of InterestPayments from standard schedule
    /// </summary>
    /// <param name="from">From date</param>
    /// <param name="to">To date</param>
    /// <param name="schedule">Coupon schedule</param>
    /// <param name="flags">Cashflow flags</param>
    /// <param name="coupon">Fixed coupon/spread</param>
    /// <param name="coupons">Schedule of fixed coupon/spread</param>
    /// <param name="initialNotional">Initial notional</param>
    /// <param name="amortizations">Amortization schedule</param>
    /// <param name="dayCount">DayCount</param>
    /// <param name="generator">Generator</param>
    /// <param name="intermediateExchange">Principal exchanges everytime notional change</param>
    /// <param name="includeTradeSettleCf">True to include cash flow at settle</param>
    /// <param name="defaultDt">Default date</param>
    /// <param name="defaultSettleDt">Default settlement date</param>
    /// <param name="paymentLag">Payment lag</param> 
    /// <param name="resets">Historical resets</param>
    /// <returns>PaymentSchedule</returns>
    public static PaymentSchedule InterestPaymentScheduleFactory(
      this Schedule schedule,
      Dt from,
      Dt to,
      CashflowFlag flags,
      double coupon,
      IList<CouponPeriod> coupons,
      double initialNotional,
      IList<Amortization> amortizations,
      bool intermediateExchange,
      DayCount dayCount,
      InterestPaymentGenerator generator,
      bool includeTradeSettleCf,
      Dt defaultDt,
      Dt defaultSettleDt,
      PayLagRule paymentLag,
      RateResets resets
      )
    {
      bool accrueOnCycle = (flags & CashflowFlag.AccrueOnCycle) != 0;
      bool adjustLast = (flags & CashflowFlag.AdjustLast) != 0;
      bool includeMaturity = (flags & CashflowFlag.IncludeMaturityAccrual) != 0;
      bool includeDefault = (flags & CashflowFlag.IncludeDefaultDate) != 0;
      PaymentGenerator wrapper = (i, prevPayDt, payDt, defaulted, not, cpn, rateMultiplier) =>
                                 {
                                   var fraction = defaulted
                                                    ? schedule.Fraction(i, defaultDt, dayCount, accrueOnCycle, includeDefault,
                                                                        ToolkitConfigurator.Settings.CDSCashflowPricer.
                                                                          UseConsistentCashflowEffective)
                                                    : schedule.Fraction(i, dayCount, accrueOnCycle, adjustLast, includeMaturity,
                                                                        ToolkitConfigurator.Settings.CashflowPricer.
                                                                          BackwardCompatibleModel);
                                   bool includeEndDateInAccrual = defaulted
                                                                    ? includeDefault
                                                                    : includeMaturity && (i == schedule.Count - 1);
                                   return generator(prevPayDt,
                                                    defaulted
                                                      ? (defaultSettleDt.IsValid() ? defaultSettleDt : defaultDt)
                                                      : payDt,
                                                    schedule.GetCycleStart(i),
                                                    defaulted ? defaultDt : schedule.GetCycleEnd(i), schedule.GetPeriodStart(i),
                                                    defaulted ? defaultDt : schedule.GetPeriodEnd(i), not, cpn, fraction,
                                                    includeEndDateInAccrual, accrueOnCycle);
                                 };
      return schedule.PaymentScheduleFactory(@from, to, flags, coupon, coupons, initialNotional, amortizations, intermediateExchange, wrapper,
                                             includeTradeSettleCf, defaultDt, paymentLag, resets);
    }

    /// <summary>
    /// Generate PaymentSchedule of InterestPayments from standard schedule
    /// </summary>
    /// <param name="from">From date</param>
    /// <param name="to">To date</param>
    /// <param name="schedule">Coupon schedule</param>
    /// <param name="flags">Cashflow flags</param>
    /// <param name="coupon">Fixed coupon/spread</param>
    /// <param name="coupons">Schedule of fixed coupon/spread</param>
    /// <param name="initialNotional">Initial notional</param>
    /// <param name="amortizations">Amortization schedule</param>
    /// <param name="dayCount">DayCount</param>
    /// <param name="generator">Generator</param>
    /// <param name="intermediateExchange">Principal exchanges everytime notional change</param>
    /// <param name="includeTradeSettleCf">True to include cash flow at settle</param>
    /// <param name="defaultDt">Default date</param>
    /// <param name="defaultSettleDt">Default settlement date</param>
    /// <param name="paymentLag">Payment lag</param> 
    /// <param name="resets">Historical resets</param>
    /// <param name="indexMultiplierSchedule">Rate multiplier schedule</param>
    /// <returns>PaymentSchedule</returns>
    public static PaymentSchedule FloatingInterestPaymentScheduleFactory(
      this Schedule schedule,
      Dt from,
      Dt to,
      CashflowFlag flags,
      double coupon,
      IList<CouponPeriod> coupons,
      double initialNotional,
      IList<Amortization> amortizations,
      bool intermediateExchange,
      DayCount dayCount,
      FloatingInterestPaymentGenerator generator,
      bool includeTradeSettleCf,
      Dt defaultDt,
      Dt defaultSettleDt,
      PayLagRule paymentLag,
      RateResets resets,
      IList<CouponPeriod> indexMultiplierSchedule = null
      )
    {
      bool accrueOnCycle = (flags & CashflowFlag.AccrueOnCycle) != 0;
      bool adjustLast = (flags & CashflowFlag.AdjustLast) != 0;
      bool includeMaturity = (flags & CashflowFlag.IncludeMaturityAccrual) != 0;
      bool includeDefault = (flags & CashflowFlag.IncludeDefaultDate) != 0;
      PaymentGenerator wrapper = (i, prevPayDt, payDt, defaulted, not, cpn, rateMultiplier) =>
      {
        var fraction = defaulted
                         ? schedule.Fraction(i, defaultDt, dayCount, accrueOnCycle, includeDefault,
                                             ToolkitConfigurator.Settings.CDSCashflowPricer.
                                               UseConsistentCashflowEffective)
                         : schedule.Fraction(i, dayCount, accrueOnCycle, adjustLast, includeMaturity,
                                             ToolkitConfigurator.Settings.CashflowPricer.
                                               BackwardCompatibleModel);
        bool includeEndDateInAccrual = defaulted
                                         ? includeDefault
                                         : includeMaturity && (i == schedule.Count - 1);
        return generator(prevPayDt,
                         defaulted
                           ? (defaultSettleDt.IsValid() ? defaultSettleDt : defaultDt)
                           : payDt,
                         schedule.GetCycleStart(i),
                         defaulted ? defaultDt : schedule.GetCycleEnd(i), schedule.GetPeriodStart(i),
                         defaulted ? defaultDt : schedule.GetPeriodEnd(i), not, cpn, fraction,
                         includeEndDateInAccrual, accrueOnCycle, rateMultiplier);
      };
      return schedule.PaymentScheduleFactory(@from, to, flags, coupon, coupons, initialNotional, amortizations, intermediateExchange, wrapper,
                                             includeTradeSettleCf, defaultDt, paymentLag, resets, indexMultiplierSchedule);
    }

    /// <summary>
    /// Generate PaymentSchedule of CommodityPayments from standard schedule
    /// </summary>
    /// <param name="effective">Effective date</param>
    /// <param name="from">From date</param>
    /// <param name="to">To date</param>
    /// <param name="schedule">Schedule</param>
    /// <param name="flags">Cashflow flags</param>
    /// <param name="spread">Spread</param>
    /// <param name="spreads">Spread schedule</param>
    /// <param name="initialNotional">Initial notional</param>
    /// <param name="amortizations">Amortization schedule</param>
    /// <param name="intermediateExchange">Principal exchanges everytime notional change</param>
    /// <param name="generator">Generator</param>
    /// <param name="defaultDt">Default date</param>
    /// <param name="defaultSettleDt">Default settlement date</param>
    /// <param name="paymentLag">Payment lag</param>
    /// <param name="resets">Historical resets</param>
    /// <returns>Payment schedule</returns>
    public static PaymentSchedule CommodityPaymentScheduleFactory(
      this Schedule schedule,
      Dt effective,
      Dt from,
      Dt to,
      CashflowFlag flags,
      double spread,
      IList<CouponPeriod> spreads,
      double initialNotional,
      IList<Amortization> amortizations,
      bool intermediateExchange,
      CommodityPaymentGenerator generator,
      Dt defaultDt,
      Dt defaultSettleDt,
      PayLagRule paymentLag,
      RateResets resets)
    {
      PaymentGenerator wrapper = (i, prevPayDt, payDt, defaulted, not, cpn, indexMultiplier) => generator(effective, defaulted
                                                                                           ? (defaultSettleDt.IsValid()
                                                                                                ? defaultSettleDt
                                                                                                : defaultDt)
                                                                                           : payDt, schedule.GetPeriodStart(i),
                                                                                         defaulted
                                                                                           ? defaultDt
                                                                                           : schedule.GetPeriodEnd(i), not, cpn);
      return schedule.PaymentScheduleFactory(@from, to, flags, spread, spreads, initialNotional, amortizations, intermediateExchange,
                                             wrapper, false, defaultDt, paymentLag, resets);
    }


    private static PaymentSchedule PaymentScheduleFactory(
      this Schedule schedule,
      Dt from,
      Dt to,
      CashflowFlag flags,
      double coupon,
      IList<CouponPeriod> coupons,
      double initialNotional,
      IList<Amortization> amortizations,
      bool intermediateExchange,
      PaymentGenerator generator,
      bool includeTradeSettleCf,
      Dt defaultDt,
      PayLagRule paymentLag,
      RateResets resets,
      IList<CouponPeriod> indexMultiplierSchedule = null
      )
    {
      var retVal = new PaymentSchedule();
      bool withPrincipalExchange = (intermediateExchange && (amortizations != null && amortizations.Any()));
      int fromIdx = FindIndex(@from, schedule, flags, includeTradeSettleCf);
      var prevPayDt = PrevPayDate(schedule, paymentLag, fromIdx);
      double prevNot = 0.0;
      Payment prevPayment = null;
      for (int i = fromIdx; i < schedule.Count; ++i)
      {
        var nonDftPayDt = PayDate(schedule, paymentLag, i);
        double nextNot = (flags & CashflowFlag.NotionalResetAtPay) != 0
                           ? amortizations.PrincipalAt(initialNotional, nonDftPayDt)
                           : amortizations.PrincipalAt(initialNotional, schedule.GetPeriodStart(i));
        double cpn = CouponPeriodUtil.CouponAt(coupons, coupon, schedule.GetPeriodStart(i));
        double floatIndexMultiplier =
          CouponPeriodUtil.CouponAt(indexMultiplierSchedule, 1.0, schedule.GetPeriodStart(i));

        bool defaulted = (defaultDt.IsValid() && defaultDt <= schedule.GetPaymentDate(i));
        if (defaulted && (flags & CashflowFlag.AccruedPaidOnDefault) == 0)
          break;
        var nextPayment = generator(i, prevPayDt, nonDftPayDt, defaulted, nextNot, cpn, floatIndexMultiplier);
        resets.HandleResets((i == fromIdx), (i == fromIdx + 1), nextPayment);
        retVal.AddPayment(nextPayment);
        if (withPrincipalExchange && prevPayment != null)
        {
          var amort = prevNot - nextNot;
          if (Math.Abs(amort) > 0)
            retVal.AddPayment(new PrincipalExchange(prevPayDt, prevNot - nextNot, prevPayment.Ccy) {FXCurve = prevPayment.FXCurve});
        }
        if (to.IsValid() && nonDftPayDt > to || defaulted)
          break;
        prevPayment = nextPayment;
        prevPayDt = nonDftPayDt;
        prevNot = nextNot;
      }
      return retVal;
    }

    #endregion

    #region Cashflow

    /// <summary>
    /// Fill a cashflow 
    /// </summary>
    /// <param name="cf">Cashflow to fill</param>
    /// <param name="paymentSchedule">Payment schedule</param>
    /// <param name="originalNotional">Original notional</param>
    /// <param name="from">Fill cashflow starting from this date</param>
    /// <param name="recoveryRate">Recovery at default</param>
    /// <returns>A cashflow object</returns>
    /// <remarks>This function is geared up to be compatible with existing Cashflow factory routines, 
    /// and does not use the full generality of the PaymentSchedule.</remarks>
    public static Cashflow FillCashflow(Cashflow cf, PaymentSchedule paymentSchedule, Dt from, double originalNotional,
                                        double recoveryRate)
    {
      originalNotional = (originalNotional > 0) ? originalNotional : 1.0;
      if (cf == null)
        cf = new Cashflow();
      else
        cf.Clear();
      cf.AsOf = @from;
      if (paymentSchedule == null) return cf;

      double prevNotional = cf.OriginalPrincipal = originalNotional;
      Dt firstAccrual = Dt.Empty;
      int idx = 0;
      foreach (Dt d in paymentSchedule.GetPaymentDates())
      {
        if (d < @from)
          continue;
        IEnumerable<Payment> payments = paymentSchedule.GetPaymentsOnDate(d);
        var proj = false;
        InterestPayment ip = null;
        DefaultSettlement dp = null;
        var hasPaymentOnTheDate = false;
        double amt = 0.0, damt = 0.0, dacc = 0.0;
        foreach (Payment p in payments)
        {
          var dsp = p as DefaultSettlement;
          if (dsp != null)
          {
            dp = dsp;
            damt += dsp.RecoveryAmount;
            dacc += dsp.AccrualAmount;
            continue;
          }
          var otp = p as OneTimePayment;
          if (otp != null && (otp.CutoffDate.IsEmpty() || @from < otp.CutoffDate))
          {
            amt += p.DomesticAmount;
            proj |= p.IsProjected;
            hasPaymentOnTheDate = true;
          }
          else
            ip = p as InterestPayment; //at most one interest payment per period supported if we convert to Cashflow
        }
        if (ip != null && (ip.ExDivDate.IsEmpty() || @from < ip.ExDivDate))
        {
            if (firstAccrual == Dt.Empty && idx == 0)
              firstAccrual = ip.AccrualStart;
            if (dp == null)
              damt = recoveryRate * ip.Notional;
            var fip = ip as FloatingInterestPayment;
            if (fip != null)
            {
              cf.Add(fip.ResetDate, fip.PeriodStartDate, fip.PeriodEndDate,
                     fip.PayDt, fip.AccrualFactor, fip.Notional, amt / originalNotional,
                     fip.DomesticAmount / originalNotional, fip.EffectiveRate,
                     damt, fip.FixedCoupon, fip.IsProjected);
            }
            else
            {
              cf.Add(ip.PayDt, ip.PeriodStartDate, ip.PeriodEndDate, ip.PayDt,
                     ip.AccrualFactor, ip.Notional, amt / originalNotional,
                     ip.DomesticAmount / originalNotional, ip.FixedCoupon,
                     damt, 0.0, false);
            }
        }
        else if (dp != null && (dp.CutoffDate.IsEmpty() || @from < dp.CutoffDate))
        {
          cf.DefaultPayment = new Cashflow.ScheduleInfo {Date = d, Accrual = dacc, Amount = damt};
          prevNotional = dp.Notional;
        }
        else if (hasPaymentOnTheDate)
        {
          cf.Add(d, d, d, d, 0.0, prevNotional, amt / originalNotional, 0.0,
                 0.0, recoveryRate * originalNotional, 0.0, proj);
        }

        if (ip != null)
        {
          prevNotional = ip.Notional;
        }

        ++idx;
      }
      cf.Effective = firstAccrual.IsValid() ? firstAccrual : @from;
      return cf;
    }

 
    /// <summary>
    /// Fill a collection of cashflows, one for each relevant currency 
    /// </summary>
    /// <param name="paymentSchedule">Payment schedule</param>
    /// <param name="from">Fill cashflow starting from this date</param>
    /// <param name="originalNotional">Original notional</param>
    /// <param name="recoveryRate">Recovery at default</param>
    ///  <returns>A cashflow object</returns>
    /// <remarks>This function is geared to be compatible with uses of Cashflow object that only look at payment date, amount (principal exchange) and accrued (interest payment).</remarks>
    public static Dictionary<Currency, Cashflow> FillCashflowByCcy(PaymentSchedule paymentSchedule, Dt from, double originalNotional,
                                                                   double recoveryRate)
    {
      originalNotional = (originalNotional > 0) ? originalNotional : 1.0;
      var ret = new Dictionary<Currency, Cashflow>();

      var amts = new Dictionary<Currency, double>();
      var accs = new Dictionary<Currency, double>();

      foreach (Dt d in paymentSchedule.GetPaymentDates())
      {
        if (d < @from)
          continue;
        IEnumerable<Payment> payments = paymentSchedule.GetPaymentsOnDate(d);
        foreach (Payment p in payments)
        {
          if (p is OneTimePayment || p is PriceReturnPayment)
          {
            if (amts.ContainsKey(p.Ccy))
              amts[p.Ccy] += p.Amount;
            else
              amts[p.Ccy] = p.Amount;
          }
          else if (p is InterestPayment)
          {
            if (accs.ContainsKey(p.Ccy))
              accs[p.Ccy] += p.Amount;
            else
              accs[p.Ccy] = p.Amount;
          }
        }

        foreach (var item in accs)
        {
          Currency ccy = item.Key;
          if (!ret.ContainsKey(ccy))
          {
            ret[ccy] = new Cashflow {AsOf = @from, Currency = ccy};
          }
          Cashflow cf = ret[ccy];
          cf.Add(d, d, d, 0.0, (amts.ContainsKey(ccy) ? amts[ccy] : 0.0) / originalNotional, item.Value / originalNotional, 0.0, 0.0);
          if (amts.ContainsKey(ccy))
          {
            amts.Remove(ccy);
          }
        }

        foreach (var item in amts)
        {
          Currency ccy = item.Key;
          if (!ret.ContainsKey(ccy))
          {
            ret[ccy] = new Cashflow {AsOf = @from, Currency = ccy};
          }
          Cashflow cf = ret[ccy];
          if (!item.Value.ApproximatelyEqualsTo(0.0) && item.Key == cf.Currency)
          {
            cf.Add(d, d, d, 0.0, item.Value / originalNotional, 0.0, 0.0, 0.0);
          }
        }

        amts.Clear();
        accs.Clear();
      }

      return ret;
    }

    /// <summary>
    /// Convert a PaymentSchedule to a list of CashflowNodes to be simulated in the LeastSquaresMonteCarloEngine.
    /// </summary>
    /// <param name="paymentSchedule">Payment schedule</param>
    /// <param name="scalingFactor">Scaling factor</param>
    /// <param name="notional">Notional</param>
    /// <param name="discountCurve">Payment schedule</param>
    /// <param name="survivalFunction">Scaling factor</param>
    /// <param name="cashflowNodes">If non null, generated nodes are appended to cashflowNodes</param>
    /// <returns>CashflowNode representation of the given payment schedule</returns>
    /// <remarks>
    /// If some or all nodes are path dependent it is assumed that only one chain of path dependent nodes is present in the PaymentSchedule. 
    /// If a payment schedule contains two or more path dependent payment chains, pass them in as separate PaymentSchedule objects.  
    /// </remarks>
    public static IList<ICashflowNode> ToCashflowNodeList(this IEnumerable<Payment> paymentSchedule,
      double scalingFactor, double notional,
      DiscountCurve discountCurve, Func<Dt, double> survivalFunction,
      IList<ICashflowNode> cashflowNodes)
    {
      if (paymentSchedule == null)
        return cashflowNodes;
      if (cashflowNodes == null)
        cashflowNodes = new List<ICashflowNode>();
      scalingFactor = Math.Abs(scalingFactor).ApproximatelyEqualsTo(0.0) ? 1.0 : 1.0 / scalingFactor;
      foreach (Payment p in paymentSchedule)
      {
        p.Scale(scalingFactor);
        var node = p.ToCashflowNode(notional, discountCurve, survivalFunction);
        cashflowNodes.Add(node);
      }
      return cashflowNodes;
    }

    #endregion

    #region Pv

    private static double Pv(this IEnumerable<Payment> payments,
      Func<Dt, double> discountFunction, Func<Dt, double> survivalFunction)
    {
      return payments.Aggregate(0.0,
        (pv, p) => pv + p.DomesticAmount * p.RiskyDiscount(discountFunction, survivalFunction));
    }

    private static double Pv(this IEnumerable<Payment> payments,
      Func<Dt, double> discountFunction, Func<Dt, double> survivalFunction,
      Dt settle, out double accrued)
    {
      double pv = 0.0;
      accrued = 0.0;
      foreach (var p in payments)
      {
        double accrual;
        accrued += p.Accrued(settle, out accrual);
        pv += accrual * p.RiskyDiscount(discountFunction, survivalFunction);
      }
      return pv;
    }

    /// <summary>
    /// Risky pv the collection of payments
    /// </summary>
    /// <param name="paymentSchedule">Payment schedule</param>
    /// <param name="asOf">As of date</param>
    /// <param name="settle">Settle date</param>
    /// <param name="discountFunction">Discount curve</param>
    /// <param name="survivalFunction">Survival function</param>
    /// <param name="includeSettleCf">True to include cashflows falling exactly on trade settlement</param>
    /// <param name="discountingAccrued">True to discount accrued payment to asOf</param>
    /// <returns>Pv</returns>
    public static double Pv(
      this IEnumerable<KeyValuePair<Dt, IList<Payment>>> paymentSchedule,
      Dt asOf, Dt settle, Func<Dt, double> discountFunction,
      Func<Dt, double> survivalFunction, bool includeSettleCf, bool discountingAccrued)
    {
      var pv = 0.0;
      var guaranteedAccrued = 0.0;
      foreach (var p in paymentSchedule)
      {
        if (p.Key < settle)
          continue;
        if (p.Key == settle && !includeSettleCf)
          continue;
        if (discountingAccrued)
          pv += p.Value.Pv(discountFunction, survivalFunction);
        else
        {
          double accrued;
          pv += p.Value.Pv(discountFunction, survivalFunction, settle, out accrued);
          guaranteedAccrued += accrued;
        }
      }
      if (survivalFunction != null)
        guaranteedAccrued *= survivalFunction(settle);
      return guaranteedAccrued + pv / discountFunction(asOf);
    }

    public static double CalculatePv(
      this IEnumerable<KeyValuePair<Dt, IList<Payment>>> paymentSchedule,
      Dt asOf, Dt settle,
      Func<Dt, double> discountFunction,
      DefaultRiskCalculator defaultRiskCalculator,
      bool includeSettleCf, bool discountingAccrued,
      bool includeDefaultOnSettle = true)
    {
      return CalculateRegularAndDefaultPv(paymentSchedule, asOf, settle,
        GetFn(discountFunction, asOf),
        defaultRiskCalculator == null
          ? (Func<Dt, double>)null
          : defaultRiskCalculator.SurvivalProbability,
        includeSettleCf, discountingAccrued, includeDefaultOnSettle);
    }

    public static double CalculatePv(
      this IEnumerable<KeyValuePair<Dt, IList<Payment>>> paymentSchedule,
      Dt asOf, Dt settle, Func<Dt, double> discountFunction,
      Func<Dt, double> survivalFunction,
      bool includeSettleCf, bool discountingAccrued,
      bool includeDefaultOnSettle = true)
    {
      return CalculateRegularAndDefaultPv(paymentSchedule, asOf, settle,
        GetFn(discountFunction, asOf), GetFn(survivalFunction, settle),
        includeSettleCf, discountingAccrued, includeDefaultOnSettle);
    }

    public static double CalculatePv(this PaymentSchedule ps,
      Dt asOf, Dt settle, DiscountCurve discountCurve,
      SurvivalCurve survivalCurve, SurvivalCurve counterpartyCurve,
      double correlation, int stepSize, TimeUnit stepUnit,
      CashflowModelFlags flags)
    {
      if (ps == null)
        return 0.0;

      bool includeDftPaymentOnSettle
        = (survivalCurve != null && survivalCurve.Defaulted == Defaulted.WillDefault);
      bool includeSettlePayments = (flags & CashflowModelFlags.IncludeSettlePayments) != 0;
      bool discountingAccrued = (flags & CashflowModelFlags.FullFirstCoupon) != 0;
      bool useLogLinear = (survivalCurve != null && survivalCurve.Stressed);

      var maturity = ps.OfType<InterestPayment>()
            .OrderBy(p => p.PayDt).LastOrDefault()?.AccrualEnd ?? Dt.Empty;

      var defaultRisk = (survivalCurve == null && counterpartyCurve == null)
        ? null : new DefaultRiskCalculator(asOf, settle, maturity, survivalCurve,
          counterpartyCurve, correlation, false, true, useLogLinear, stepSize, stepUnit);
      
      var pv = ps.SetProtectionStart(settle).CalculatePv(asOf, settle, discountCurve,
        defaultRisk, includeSettlePayments, discountingAccrued,
        includeDftPaymentOnSettle);
      return pv;
    }

    private static double CalculateRegularAndDefaultPv(
      this IEnumerable<KeyValuePair<Dt, IList<Payment>>> paymentSchedule,
      Dt asOf, Dt settle, Func<Dt, double> discountFunction,
      Func<Dt, double> survivalFunction,
      bool includeSettleCf, bool discountingAccrued,
      bool includeDefaultOnSettle)
    {
      if (paymentSchedule == null) return 0.0;
      // We separate the default settlements from the regular payments.
      var defaultSettlements = null
        as IList<KeyValuePair<Dt, IList<Payment>>>;
      var regularPayments = paymentSchedule.Select(pair =>
      {
        if (!pair.Value.OfType<DefaultSettlement>().Any()) return pair;
        if (defaultSettlements == null)
        {
          defaultSettlements = new List<KeyValuePair<Dt, IList<Payment>>>();
        }
        defaultSettlements.Add(MakePair(pair.Key, pair.Value
          .OfType<DefaultSettlement>().Cast<Payment>().ToList()));
        return MakePair(pair.Key, pair.Value
          .Where(p => !(p is DefaultSettlement)).ToList());
      });

      // Now calculate the PVs separately.
      var pv = Pv(regularPayments, asOf, settle,
        discountFunction, survivalFunction,
        includeSettleCf, discountingAccrued);
      if (defaultSettlements != null)
      {
        // For default settlements, no survival curve,
        // and include payment on settle. 
        pv += Pv(defaultSettlements, asOf, settle, 
          discountFunction, null,
          includeDefaultOnSettle, discountingAccrued);
      }
      return pv;
    }

    private static KeyValuePair<TKey, IList<TValue>> MakePair<TKey, TValue>(
      TKey key, IList<TValue> value)
    {
      return new KeyValuePair<TKey, IList<TValue>>(key, value);
    }

    /// <summary>
    /// Extends the first recovery period to cover the period
    ///  from the protection start date to the recovery period end date.
    /// </summary>
    /// <param name="ps">The payment schedule</param>
    /// <param name="protectionStart">The protection start.</param>
    /// <returns>IEnumerable&lt;KeyValuePair&lt;Dt, IList&lt;Payment&gt;&gt;&gt;.</returns>
    public static IEnumerable<KeyValuePair<Dt, IList<Payment>>> SetProtectionStart(this PaymentSchedule ps, Dt protectionStart)
    {
      if (ps == null) return null;
      var first = ps.OfType<RecoveryPayment>()
        .OrderBy(r => r.BeginDate)
        .FirstOrDefault(r => r.EndDate > protectionStart);
      if (first == null || first.BeginDate == protectionStart)
        return ps;

      var payments = (IEnumerable<KeyValuePair<Dt, IList<Payment>>>) ps;
      return payments.Select(pair =>
      {
        var idx = pair.Value.IndexOf(first);
        if (idx < 0) return pair;
        return new KeyValuePair<Dt, IList<Payment>>(pair.Key, pair.Value.Select(
          p => p != first ? p
            : new RecoveryPayment(protectionStart, first.EndDate,
              first.RecoveryRate, first.Ccy)
            {
              Notional = first.Notional,
              IsFunded = first.IsFunded,
              CutoffDate = first.CutoffDate
            }).ToList());
      });
    }

    private static Func<Dt, double> GetFn(Func<Dt, double> fn, Dt settle)
    {
      if (fn == null) return null;
      var baseValue = fn(settle);
      if (baseValue.AlmostEquals(1.0) || baseValue.AlmostEquals(0.0))
        return dt => dt < settle ? 1.0 : fn(dt);
      return dt => dt <= settle ? 1.0 : fn(dt)/baseValue;
    }


    public static Dt GetCutoffDate(this Payment p)
    {
      return p.CutoffDate.IsEmpty() ? p.PayDt : p.CutoffDate;
    }

    public static IEnumerable<KeyValuePair<Dt, IList<Payment>>> GroupByCutoff(this IEnumerable<Payment> payments)
    {
      return payments.GroupBy(p => p.GetCutoffDate())
        .Select(g => new KeyValuePair<Dt, IList<Payment>>(g.Key, g.ToList()));
    }

    public static IEnumerable<KeyValuePair<Dt, IList<Payment>>> GroupByAccrualEnd(this IEnumerable<Payment> payments)
    {

      return payments.GroupBy(p =>
      {
        var ip = p as InterestPayment;
        return ip != null
          ? (ip.AccrueOnCycle ? ip.PeriodEndDate : ip.PayDt)
          : ((p as RecoveryPayment)?.EndDate ?? p.PayDt);
      }).Select(g => new KeyValuePair<Dt, IList<Payment>>(g.Key, g.ToList()));
    }



    /// <summary>
    /// Groups the payments by the accrual end dates instead of the pay dates.
    /// </summary>
    /// <param name="payments">The payments</param>
    /// <returns>IEnumerable&lt;KeyValuePair&lt;Dt, IList&lt;Payment&gt;&gt;&gt;.</returns>
    /// <remarks>
    ///  Supply the results to the <c>CalculatePv()</c> function when the PV
    ///  needs to be based on the accrual ends as the cut-off dates.
    ///  This replicates the condition <c>CreditRiskToPaymentDate == false</c>
    ///  in the legacy cash flow model.
    /// </remarks>
    public static IEnumerable<KeyValuePair<Dt, IList<Payment>>> GroupByCutoffDate(this IEnumerable<Payment> payments)
    {
      return payments.GroupBy(p =>
      {
        var ip = p as InterestPayment;
        return ip != null
          ? Dt.Earlier(ip.AccrualEnd, ip.PayDt)
          : ((p as RecoveryPayment)?.EndDate ?? p.PayDt);
      }).Select(g => new KeyValuePair<Dt, IList<Payment>>(g.Key, g.ToList()));
    }

    public static bool Before(this Dt date1, Dt date2, bool includeEqual = false)
    {
      var cmp = Dt.Cmp(date1, date2);
      return cmp < 0 || (includeEqual && cmp == 0);
    }

    /// <summary>
    /// Updates the notional.
    /// </summary>
    /// <param name="payment">The payment.</param>
    /// <param name="notional">The notional.</param>
    /// <returns>Payment.</returns>
    public static Payment UpdateNotional(
      this Payment payment, double notional)
    {
      return new ScaledPayment(payment, notional);
    }

    #endregion

    #region SolveSpread

    /// <summary>
    /// Calculate the CDS spread implied by full price.
    /// </summary>
    /// <remarks>
    /// <para>Calculates constant spread over survival curve spreads for
    /// cashflow to match a specified full price.</para>
    /// </remarks>
    /// <param name="asOf">pricing as-of</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="cfAdapter">Cashflow</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="counterpartyCurve">Couterparty survival curve</param>
    /// <param name="correlation">Correlation between default and counterparty</param>
    /// <param name="includeSettlePayments">True if payments on settlement date included</param>
    /// <param name="discountingAccrued">Whether to discount the accrued or not</param>
    /// <param name="creditRiskByPayDate">True if the credit risk is determined 
    /// by payment date instead of period end</param>
    /// <param name="stepSize">Pricing grid step size</param>
    /// <param name="stepUnit">Pricing grid step time unit</param>
    /// <param name="price">Target full price (percentage of notional)</param>
    /// <returns>Spreads shift to the Survival Curve implied by price</returns>
    public static double ImpliedCdsSpread(Dt asOf, Dt settle,
      CashflowAdapter cfAdapter, DiscountCurve discountCurve,
      SurvivalCurve survivalCurve, SurvivalCurve counterpartyCurve,
      double correlation, bool includeSettlePayments, bool discountingAccrued,
      bool creditRiskByPayDate, int stepSize, TimeUnit stepUnit, double price)
    {
      SurvivalCurve sc;
      double spread;
      SolveSpread(asOf, settle, cfAdapter, discountCurve, survivalCurve,
        counterpartyCurve, correlation, includeSettlePayments, discountingAccrued,
        creditRiskByPayDate, stepSize, stepUnit, price, out sc, out spread);
      return spread;
    }

    /// <summary>
    /// Calculate the bond-implied CDS Curve
    /// </summary>
    /// <remarks>
    /// <para>Calculates the (constant)spread that needs to be added/subtracted from CDS curve to
    /// recover full bond price. Once the shift is calculated the shifted survival curve is returned.</para>
    /// </remarks>
    /// <param name="asOf">pricing as-of</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="cfAdapter">Cashflow</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="counterpartyCurve">Couterparty survival curve</param>
    /// <param name="correlation">Correlation between default and counterparty</param>
    /// <param name="includeSettlePayments">True if payments on settlement date included</param>
    /// <param name="discountingAccrued">Whether to discount the accrued or not</param>
    /// <param name="creditRiskByPayDate">True if the credit risk is 
    /// determined by payment date instead of period end</param>
    /// <param name="stepSize">Pricing grid step size</param>
    /// <param name="stepUnit">Pricing grid step time unit</param>
    /// <param name="price">Target full price (percentage of notional)</param>
    /// <returns>Bond-Implied Survival Curve</returns>
    public static SurvivalCurve ImpliedCdsCurve(Dt asOf, Dt settle,
      CashflowAdapter cfAdapter, DiscountCurve discountCurve,
      SurvivalCurve survivalCurve, SurvivalCurve counterpartyCurve,
      double correlation, bool includeSettlePayments, bool discountingAccrued,
      bool creditRiskByPayDate, int stepSize, TimeUnit stepUnit, double price)
    {
      SurvivalCurve sc;
      double spread;
      SolveSpread(asOf, settle, cfAdapter, discountCurve, survivalCurve,
        counterpartyCurve, correlation, includeSettlePayments, discountingAccrued,
        creditRiskByPayDate, stepSize, stepUnit, price, out sc, out spread);
      return sc;
    }

    /// <summary>
    /// Calculate the Implied flat CDS Curve from a full price
    /// </summary>
    /// <remarks>
    /// <para>Implies a flat CDS curve from the specified full price.</para>
    /// <note>Does not require an existing survival curve.</note>
    /// </remarks>
    /// <param name="asOf">pricing as-of</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="cfAdapter">Cashflow</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="counterpartyCurve">Couterparty survival curve</param>
    /// <param name="correlation">Correlation between default and counterparty</param>
    /// <param name="includeSettlePayments">True if payments on settlement date included</param>
    /// <param name="discountingAccrued">Whether to discount the accrued or not</param>
    /// <param name="creditRiskByPayDate">True if the credit risk is 
    /// determined by payment date instead of period end</param>
    /// <param name="stepSize">Pricing grid step size</param>
    /// <param name="stepUnit">Pricing grid step time unit</param>
    /// <param name="price">Target full price (percentage of notional)</param>
    /// <param name="recoveryRate">Recovery rate for CDS to imply</param>
    /// <param name="allowNegativeCdsSpreads">Allow Negative Spreads in the Survival Curve</param>
    /// <returns>Implied Survival Curve fitted from CDS quotes</returns>
    public static SurvivalCurve ImpliedCdsCurve(Dt asOf, Dt settle,
      CashflowAdapter cfAdapter, DiscountCurve discountCurve,
      SurvivalCurve counterpartyCurve, double correlation,
      bool includeSettlePayments, bool discountingAccrued,
      bool creditRiskByPayDate, int stepSize, TimeUnit stepUnit, double price,
      double recoveryRate, bool allowNegativeCdsSpreads)
    {
      double spread;

      // Create initial survival curve
      SurvivalFitCalibrator calibrator = new SurvivalFitCalibrator(asOf, settle,
        recoveryRate, discountCurve);
      calibrator.AllowNegativeCDSSpreads = allowNegativeCdsSpreads;

      SurvivalCurve survivalCurve = new SurvivalCurve(calibrator);
      survivalCurve.AddCDS(Dt.CDSMaturity(settle, "5 Year"), 0.0, DayCount.Actual360,
        Frequency.Quarterly, BDConvention.Following, Calendar.None);

      survivalCurve.Fit();

      SolveSpread(asOf, settle, cfAdapter, discountCurve, survivalCurve,
        counterpartyCurve, correlation, includeSettlePayments, discountingAccrued,
        creditRiskByPayDate, stepSize, stepUnit, price, out survivalCurve, out spread);
      return survivalCurve;
    }

    /// <summary>
    /// calculate the implied discount spread using payment schedule method
    /// </summary>
    /// <param name="cfAdapter">cash flow adapter</param>
    /// <param name="asOf">as-of date</param>
    /// <param name="settle">settle date</param>
    /// <param name="discountCurve">discount curve</param>
    /// <param name="survivalCurve">survival curve</param>
    /// <param name="counterpartyCurve">counterparty curve</param>
    /// <param name="correlation">correlation</param>
    /// <param name="step">time step</param>
    /// <param name="stepUnit">step unit</param>
    /// <param name="price">price to imply the discount spread</param>
    /// <param name="flags">CashflowModelFlags</param>
    /// <returns></returns>
    public static double ImpDiscountSpread(CashflowAdapter cfAdapter,
      Dt asOf, Dt settle, DiscountCurve discountCurve,
      SurvivalCurve survivalCurve, SurvivalCurve counterpartyCurve,
      double correlation, int step, TimeUnit stepUnit,
      double price, CashflowModelFlags flags)
    {
      var originalSpread = discountCurve.Spread;
      double spread;
      var solver = new Generic();
      var fn = new DiscountSpreadFn(cfAdapter, asOf, settle,
        discountCurve, survivalCurve, counterpartyCurve, correlation,
        step, stepUnit, flags);

      solver.setLowerBounds(-5.0);
      solver.setUpperBounds(5.0);
      solver.setInitialPoint(0.0);

      try
      {
        spread = solver.solve(fn, price, -0.05, 0.05);
      }
      catch (Exception e)
      {
        discountCurve.Spread = originalSpread;
        throw new SolverException(String.Format(
          "Cannot solve the discount spread due to {0}", e));
      }

      discountCurve.Spread = originalSpread;
      return spread;
    }


    public static double Irr(CashflowAdapter cfa, Dt asOf, Dt settle,
      DiscountCurve discountCurve, SurvivalCurve survivalCurve,
      SurvivalCurve counterpartyCurve, double correlation, CashflowModelFlags flags,
      int step, TimeUnit stepUnit, DayCount dayCount,
      Frequency freq, double price)
    {
      var solver = new Generic();
      var fn = new IrrFn(cfa, asOf, settle, discountCurve, survivalCurve,
        counterpartyCurve, correlation, step, stepUnit, dayCount, freq, flags);

      solver.setLowerBounds(RateCalc.RateLowerBound(freq));
      solver.setUpperBounds(100.0);
      solver.setInitialPoint(0.05);
      return solver.solve(fn, price, 0.01, 0.20);
    }


    /// <summary>
    /// Local utility function to solve for the CDS Curve from a full price
    /// </summary>
    /// <param name="asOf">pricing as-of</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="cfa">Cashflow</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="counterpartyCurve">Couterparty survival curve</param>
    /// <param name="correlation">Correlation between default and counterparty</param>
    /// <param name="includeSettlePayments">True if payments on settlement date included</param>
    /// <param name="discountingAccrued">Whether to discount the accrued or not</param>
    /// <param name="creditRiskByPayDate"></param>
    /// <param name="stepSize">Pricing grid step size</param>
    /// <param name="stepUnit">Pricing grid step time unit</param>
    /// <param name="price">Target full price (percentage of notional)</param>
    /// <param name="fittedSpread">returned implied spread</param>
    /// <param name="fittedSurvivalCurve">returned implied credit curve</param>
    private static void SolveSpread(
      Dt asOf, Dt settle, CashflowAdapter cfa, DiscountCurve discountCurve, SurvivalCurve survivalCurve,
      SurvivalCurve counterpartyCurve, double correlation, bool includeSettlePayments, bool discountingAccrued,
      bool creditRiskByPayDate, int stepSize, TimeUnit stepUnit, double price,
      out SurvivalCurve fittedSurvivalCurve, out double fittedSpread
    )
    {
      // validate
      if (asOf > settle)
        throw new ArgumentOutOfRangeException("settle", String.Format(
          "Settle date {0} must be on or after the pricing asOf {1} date", settle, asOf));
      if (discountCurve == null)
        throw new ArgumentOutOfRangeException(
          "discountCurve", "Must specify discount curve to calculation implied CDS Spread");
      if (survivalCurve == null)
        throw new ArgumentOutOfRangeException(
          "survivalCurve", "Must specify survival curve to calculation implied CDS Spread");
      if (correlation > 1.0 || correlation < -1.0)
        throw new ArgumentOutOfRangeException(
          "correlation", String.Format("Invalid correlation. Must be between -1 and 1 - not {0}",
          correlation));
      if (price < 0.0 || price > 4.0)
        throw new ArgumentOutOfRangeException(
          "price", String.Format("Invalid full price. Must be between 0 and 4 - not {0}", price));
      if (stepSize < 0)
        throw new ArgumentOutOfRangeException(
          "stepSize", "Invalid step size, must be >= )");

      // Solve for fitted survival curve
      var flags = CashflowModelFlags.IncludeProtection | CashflowModelFlags.IncludeFees |
                  (includeSettlePayments ? CashflowModelFlags.IncludeSettlePayments : 0) |
                  (discountingAccrued ? CashflowModelFlags.FullFirstCoupon : 0) |
                  (ToolkitConfigurator.Settings.CashflowPricer.IgnoreAccruedInProtection
                  ? CashflowModelFlags.IgnoreAccruedInProtection : 0) |
                  (creditRiskByPayDate ? CashflowModelFlags.CreditRiskToPaymentDate : 0);
      CdsSpreadFn fn = new CdsSpreadFn(cfa, asOf, settle, discountCurve, survivalCurve,
        counterpartyCurve, correlation, stepSize, stepUnit, flags);

      // find smallest quote
      CurveTenorCollection tenors = survivalCurve.Tenors;
      int count = survivalCurve.Tenors.Count;
      double minQuote = CurveUtil.MarketQuote(tenors[0]);
      for (int i = 1; i < count; ++i)
      {
        double quote = CurveUtil.MarketQuote(tenors[i]);
        if (quote < minQuote)
          minQuote = quote;
      }

      // Set up root finder
      Brent2 rf = new Brent2();
      rf.setToleranceX(10e-6);
      rf.setToleranceF(10e-6);
      //rf.setLowerBounds(-minQuote + 10e-8);
      //rf.setUpperBracket(1.0);

      // Solve
      double x = rf.solve(fn, price, -minQuote + 10e-8, 0.1);
      fn.evaluate(x);
      fittedSpread = fn.Spread;
      fittedSurvivalCurve = fn.FittedSurvivalCurve;
    }

    public static double ImpSurvivalSpread(CashflowAdapter cfa, Dt asOf, Dt settle,
      DiscountCurve discountCurve, SurvivalCurve survivalCurve, SurvivalCurve counterpartyCurve,
      double correlation, bool includeSettlePayments, bool includeMaturityProtection,
      bool fullFirstCoupon, int step, TimeUnit stepUnit, double price)
    {
      double spread;
      double origSpread = survivalCurve.Spread;
      var solver = new Generic();
      var fn = new SurvivalSpreadFn(cfa, asOf, settle, discountCurve, survivalCurve,
        counterpartyCurve, correlation, step, stepUnit, AdapterUtil.CreateFlags(
          includeSettlePayments, includeMaturityProtection, fullFirstCoupon));
      solver.setLowerBounds(-100.0);
      solver.setUpperBounds(100.0);
      solver.setInitialPoint(0.0);

      try
      {
        spread = solver.solve(fn, price, -0.05, 0.05);
      }
      catch (Exception e)
      {
        survivalCurve.Spread = origSpread;
        throw new SolverException(String.Format(
          "Cannot solve the survival spread due to {0}", e));
      }

      survivalCurve.Spread = origSpread;
      return spread;
    }

    #endregion SolveSpread

    #region Solvers

    /// <summary>
    /// Base class of payment schedule solver
    /// </summary>
    private abstract class PsSolverFn : SolverFn
    {
      public PsSolverFn(CashflowAdapter cfa, Dt asOf, Dt settle,
        DiscountCurve discountCurve, SurvivalCurve survivalCurve,
        SurvivalCurve counterpartyCurve, double correlation,
        int step, TimeUnit stepUnit, CashflowModelFlags flags)
      {
        _cfAdapter = cfa;
        _asOf = asOf;
        _settle = settle;
        _discountCurve = (DiscountCurve)discountCurve?.Clone();
        _survivalCurve = (SurvivalCurve)survivalCurve?.Clone();
        _counterpartyCurve = counterpartyCurve;
        _correlation = correlation;
        _step = step;
        _stepUnit = stepUnit;
        _flags = flags;
      }

      public CashflowAdapter CfAdapter => _cfAdapter;
      public Dt AsOf => _asOf;
      public Dt Settle => _settle;

      public DiscountCurve DiscountCurve
      {
        get { return _discountCurve; }
        set { _discountCurve = value; }
      }
      public SurvivalCurve SurvivalCurve => _survivalCurve;
      public SurvivalCurve CounterpartyCurve => _counterpartyCurve;
      public double Correlation => _correlation;
      public int Step => _step;
      public TimeUnit StepUnit => _stepUnit;
      public CashflowModelFlags Flags => _flags;

      private CashflowAdapter _cfAdapter;
      private Dt _asOf;
      private Dt _settle;
      private DiscountCurve _discountCurve;
      private SurvivalCurve _survivalCurve;
      private SurvivalCurve _counterpartyCurve;
      private double _correlation;
      private int _step;
      private TimeUnit _stepUnit;
      private CashflowModelFlags _flags;
    }


    /// <summary>
    /// Solver to solve the implied discount spread
    /// </summary>
    private class DiscountSpreadFn : PsSolverFn
    {
      public DiscountSpreadFn(CashflowAdapter cf, Dt asOf, Dt settle,
        DiscountCurve discountCurve, SurvivalCurve survivalCurve,
        SurvivalCurve counterpartyCurve, double correlation,
        int step, TimeUnit stepUnit, CashflowModelFlags flags)
        : base(cf, asOf, settle, discountCurve, survivalCurve,
          counterpartyCurve, correlation, step, stepUnit, flags)
      { }

      public override double evaluate(double x)
      {
        DiscountCurve.Spread = x;
        double pv = CfAdapter.Pv(AsOf, Settle, DiscountCurve, SurvivalCurve,
          CounterpartyCurve, Correlation, Step, StepUnit, Flags);
        return pv;
      }
    }

    /// <summary>
    /// Solver to solve the survival spread
    /// </summary>
    private class SurvivalSpreadFn : PsSolverFn
    {
      public SurvivalSpreadFn(CashflowAdapter cf, Dt asOf, Dt settle,
        DiscountCurve discountCurve, SurvivalCurve survivalCurve,
        SurvivalCurve counterpartyCurve, double correlation,
        int step, TimeUnit stepUnit, CashflowModelFlags flags)
        : base(cf, asOf, settle, discountCurve, survivalCurve,
          counterpartyCurve, correlation, step, stepUnit, flags)
      { }

      public override double evaluate(double x)
      {
        SurvivalCurve.Spread = x;
        double pv = CfAdapter.Pv(AsOf, Settle, DiscountCurve, SurvivalCurve,
          CounterpartyCurve, Correlation, Step, StepUnit, Flags);
        return pv;
      }
    }


    /// <summary>
    /// Solver Fn function for implying CDS curve from price
    /// </summary>
    private class CdsSpreadFn : PsSolverFn
    {
      public CdsSpreadFn(CashflowAdapter cf, Dt asOf, Dt settle,
        DiscountCurve discountCurve, SurvivalCurve survivalCurve,
        SurvivalCurve counterpartyCurve, double correlation,
        int step, TimeUnit stepUnit, CashflowModelFlags flags)
        : base(cf, asOf, settle, discountCurve, survivalCurve,
          counterpartyCurve, correlation, step, stepUnit, flags)
      {
        _survivalSpread = survivalCurve.Spread;
      }

      public override double evaluate(double x)
      {
        SurvivalCurve.Spread = _survivalSpread + x;
        double pv = CfAdapter.Pv(AsOf, Settle, DiscountCurve, SurvivalCurve,
          CounterpartyCurve, Correlation, Step, StepUnit, Flags);
        return pv;
      }

      public SurvivalCurve FittedSurvivalCurve => SynchronizeSpreads(SurvivalCurve);

      public double Spread
      {
        get
        {
          Dt maturity = CfAdapter.GetDt(CfAdapter.Count - 1);
          CDSCashflowPricer pricer = GetPricer(SurvivalCurve, maturity);
          double cdsSpread = pricer.BreakEvenPremium();
          double scSpread = SurvivalCurve.Spread;
          try
          {
            SurvivalCurve.Spread = _survivalSpread;
            pricer.Reset();
            return cdsSpread - pricer.BreakEvenPremium();
          }
          finally
          {
            SurvivalCurve.Spread = scSpread;
          }
        }
      }

      private readonly double _survivalSpread;
    }

    /// <summary>
    /// Solver to solve Irr
    /// </summary>
    private class IrrFn : PsSolverFn
    {
      public IrrFn(CashflowAdapter cfa, Dt asOf, Dt settle,
        DiscountCurve discountCurve, SurvivalCurve survivalCurve,
        SurvivalCurve counterpartyCurve, double correlation, int step,
        TimeUnit stepUnit, DayCount dayCount, Frequency freq, CashflowModelFlags flags)
        : base(cfa, asOf, settle, discountCurve, survivalCurve, counterpartyCurve,
          correlation, step, stepUnit, flags)
      {
        _dc = dayCount;
        _freq = freq;
        _count = cfa.Count;
        if (DiscountCurve == null)
          DiscountCurve = new DiscountCurve(asOf);
        DiscountCurve = InitIrrSolver(cfa, asOf, settle, dayCount, freq,
          discountCurve, flags, out _firstIndex);
      }

      public override double evaluate(double x)
      {
        for (int i = _firstIndex; i < _count; i++)
        {
          double df = RateCalc.PriceFromRate(x, DiscountCurve.AsOf, CfAdapter.GetDt(i), _dc, _freq);
          DiscountCurve.SetVal(i - _firstIndex, df);
        }
        double pv = CfAdapter.Pv(AsOf, Settle, DiscountCurve, SurvivalCurve,
          CounterpartyCurve, Correlation, Step, StepUnit, Flags);
        return pv;
      }

      private DayCount _dc;
      private Frequency _freq;
      private int _firstIndex;
      private int _count;
    }


    #endregion Solvers

    #region Helpers

    public static PaymentSchedule FilterPayments(
      this PaymentSchedule paymentSchedule, Dt from)
    {
      if (paymentSchedule == null) return null;

      var retVal = new PaymentSchedule();

      foreach (Dt d in paymentSchedule.GetPaymentDates())
      {
        if (d < @from)
          continue;
        var payments = paymentSchedule.GetPaymentsOnDate(d)
          .Where(p =>
          {
            if (p is DefaultSettlement) return true;
            return @from < p.GetCutoffDate();
          }).ToList();
        retVal.AddPayments(payments);
      }
      return retVal;
    }

    private static Dt PayDate(Schedule sched, PayLagRule paymentLag, int i)
    {
      return paymentLag == null
        ? sched.GetPaymentDate(i)
        : (paymentLag.PaymentLagBusinessFlag
          ? Dt.AddDays(sched.GetPeriodEnd(i),
            paymentLag.PaymentLagDays, sched.Calendar)
          : Dt.Roll(Dt.Add(sched.GetPeriodEnd(i),
              paymentLag.PaymentLagDays, TimeUnit.Days),
            sched.BdConvention, sched.Calendar));
    }

    private static Dt PrevPayDate(Schedule sched, PayLagRule paymentLag, int i)
    {
      return (i > 0)
        ? (paymentLag == null
          ? sched.GetPaymentDate(i - 1)
          : paymentLag.PaymentLagBusinessFlag
            ? Dt.AddDays(sched.GetPeriodEnd(i - 1),
            paymentLag.PaymentLagDays, sched.Calendar)
            : Dt.Roll(Dt.Add(sched.GetPeriodEnd(i - 1),
            paymentLag.PaymentLagDays), sched.BdConvention, sched.Calendar))
        : sched.GetPeriodStart(0);
    }

    private static int FindIndex(Dt dt, Schedule sched,
      CashflowFlag flags, bool includeTradeSettleCf)
    {
      if (sched.Count == 0)
        return 0;
      int firstIdx = 0;
      Dt accrualStart = sched.GetPeriodStart(0);
      bool accrueOnCycle = (flags & CashflowFlag.AccrueOnCycle) != 0;
      for (; firstIdx < sched.Count; ++firstIdx)
      {
        accrualStart = (accrueOnCycle | firstIdx <= 0)
                         ? sched.GetPeriodStart(firstIdx)
                         : sched.GetPaymentDate(firstIdx - 1);
        if (accrualStart >= dt)
          break;
      }
      if (accrualStart > dt)
      {
        if (firstIdx > 0)
          return --firstIdx;
      }
      if (firstIdx > 0 && (sched.GetPaymentDate(firstIdx - 1) > dt))
        return --firstIdx;
      if (firstIdx > 0 && (sched.GetPaymentDate(firstIdx - 1) == dt))
      {
        if (includeTradeSettleCf)
          --firstIdx;
        return firstIdx;
      }
      return firstIdx;
    }

    public static void HandleResets(this RateResets resets,
      bool isCurrent, bool isNext, Payment payment)
    {
      if (resets == null)
        return;
      var ip = payment as IFloatingPayment;
      if (ip == null)
        return;
      var projector = (CouponCalculator)ip.RateProjector;
      if (isCurrent && resets.HasCurrentReset)
      {
        ip.EffectiveRate = resets.CurrentReset; //all inclusive reset
        if (resets.HasAllResets) //this may happen with theta calculation
        {
          RateResetState resetState;
          double reset = RateResetUtil.FindRate(ip.ResetDate,
            projector.AsOf, resets, projector.UseAsOfResets, out resetState);
          if (resetState == RateResetState.ObservationFound)
            ip.EffectiveRate = reset; //all inclusive reset
        }
        return;
      }
      if (isNext && resets.HasNextReset)
      {
        ip.EffectiveRate = resets.NextReset; //all inclusive reset
        return;
      }
      if (ip.ResetDate > projector.AsOf)
        return;
      if (resets.HasAllResets)
      {
        RateResetState resetState;
        double reset = RateResetUtil.FindRate(ip.ResetDate,
          projector.AsOf, resets, projector.UseAsOfResets, out resetState);
        if (resetState == RateResetState.ObservationFound)
          ip.EffectiveRate = reset; //all inclusive reset
      }
    }

    public static void UpdateResetsInCustomCashflowPayments(
      this RateResets resets, Payment ip, bool isCurrent, bool isNext)
    {
      resets.HandleResets(isCurrent, isNext, ip);
    }

    /// <summary>
    ///  Create an "almost" deep copy of the Payment Schedule (customized cash flows schedule) for the purposes
    ///  of "bumping" the coupon for coupon01 computations.
    /// </summary>
    public static PaymentSchedule CopyPaymentScheduleForBumpingCoupon(PaymentSchedule origPaymentSchedule)
    {
      // This is not a full copy; this function can not replace a Clone() method.
      var copy = new PaymentSchedule();
      foreach (Dt dt in origPaymentSchedule.GetPaymentDates())
      {
        foreach (Payment pmt in origPaymentSchedule.GetPaymentsOnDate(dt))
        {
          var iPmt = pmt as FixedInterestPayment;
          copy.AddPayment((iPmt != null) ? CreateCopyForBumpingCoupon(iPmt) : pmt);
        }
      }
      return copy;
    }

    /// <summary>
    ///  Create an "almost" deep copy of the Payment Schedule (customized cash flows schedule) for the purposes
    ///  of "bumping" the coupon for coupon01 computations.
    ///  In this version, we are also passing day count to be set in each item of the copy.
    /// </summary>
    public static PaymentSchedule CopyPaymentScheduleForBumpingCoupon(PaymentSchedule origPaymentSchedule, DayCount dc)
    {
      // This is not a full copy; this function can not replace a Clone() method.
      var copy = new PaymentSchedule();
      foreach (Dt dt in origPaymentSchedule.GetPaymentDates())
      {
        foreach (Payment pmt in origPaymentSchedule.GetPaymentsOnDate(dt))
        {
          var iPmt = pmt as FixedInterestPayment;
          copy.AddPayment((iPmt != null) ? CreateCopyForBumpingCoupon(iPmt, dc) : pmt);
          // If it is a fixed interest payment, need a deep copy so that we could bump its coupon
        }
      }
      return copy;
    }

    /// <summary>
    ///  Create an "almost" deep copy of the Payment Schedule
    /// </summary>
    public static PaymentSchedule CreateCopy(PaymentSchedule origPaymentSchedule)
    {
      // This is not a full copy; this function can not replace a Clone() method.
      var copy = new PaymentSchedule();
      foreach (Dt dt in origPaymentSchedule.GetPaymentDates())
      {
        foreach (Payment pmt in origPaymentSchedule.GetPaymentsOnDate(dt))
        {
          copy.AddPayment((Payment)pmt.Clone());
        }
      }
      return copy;
    }

    private static FixedInterestPayment CreateCopyForBumpingCoupon(FixedInterestPayment fp, DayCount dc)
    {
      // This function is in place of a copy constructor for FixedInterestPayment
      return new FixedInterestPayment(fp.PreviousPaymentDate, fp.PayDt, fp.Ccy, fp.CycleStartDate,
                                      fp.CycleEndDate, fp.PeriodStartDate, fp.PeriodEndDate, Dt.Empty, fp.Notional,
                                      fp.FixedCoupon, dc, fp.CompoundingFrequency)
      {
        FXCurve = fp.FXCurve,
        VolatilityStartDt = fp.VolatilityStartDt,
        AccrueOnCycle = fp.AccrueOnCycle,
        IncludeEndDateInAccrual = fp.IncludeEndDateInAccrual,
        ZeroCoupon = fp.ZeroCoupon
      };
    }

    private static FixedInterestPayment CreateCopyForBumpingCoupon(FixedInterestPayment fp)
    {
      return CreateCopyForBumpingCoupon(fp, fp.DayCount);
    }

    /// <summary>
    /// Compute effective notional for date dt from a customized cash flows schedule using the pay date as a reference.
    /// </summary>
    public static double GetEffectiveNotionalFromCustomizedScheduleBasedOnPayDate(PaymentSchedule ps, Dt dt)
    {
      if (ps == null || ps.Count == 0)
        return 1.0; // This function is not applicable in this case.
      // The "original" notional is assumed to be 1, while the "effective" notional at each period is a fraction of 1.
      var fips = ps.GetPaymentsByType<InterestPayment>().ToList(); // We assume the payment items will be sorted by pay date
      if (fips.Count == 0)
        return 1.0;
      if (dt <= fips[0].AccrualStart)
        return fips[0].Notional;
      if (dt <= fips[0].PayDt)
        return fips[0].Notional;
      for (int i = 0; i < fips.Count; i++)
      {
        if (i < fips.Count - 1 && dt > fips[i].PayDt && dt <= fips[i + 1].PayDt)
          return fips[i + 1].Notional;
      }
      return fips[fips.Count - 1].Notional; // Final notional
    }

    /// <summary>
    /// Compute effective notional for date dt from a customized cash flows schedule using the accrual period start date as a reference.
    /// </summary>
    public static double GetEffectiveNotionalFromCustomizedSchedule(PaymentSchedule ps, Dt dt)
    {
      if (ps == null || ps.Count == 0)
        return 1.0; // This function is not applicable in this case.
      // The "original" notional is assumed to be 1, while the "effective" notional at each period is a fraction of 1.
      var fips = ps.GetPaymentsByType<InterestPayment>().ToList(); // We assume the payment items will be sorted by pay date
      if (fips.Count == 0)
        return 1.0;
      if (dt < fips[0].AccrualStart)
        return fips[0].Notional;
      Dt redem = GetRedemptionDateFromFromCustomizedSchedule<PrincipalExchange>(ps);
      if (!redem.IsEmpty() && dt >= redem)
        return 0.0; // All the principal has been redeemed beyond this date.
      for (int i = 0; i < fips.Count; i++)
      {
        if (dt >= fips[i].AccrualStart && (i == fips.Count - 1 || dt < fips[i + 1].AccrualStart))
          return fips[i].Notional;
      }
      return 0; // We should not get to this point ...
    }

    /// <summary>
    /// Given the custom payment schedule, find the last principal repayment date and assume it to be the "redemption" date
    /// meaning the date all remaining principal has been repaid.
    /// </summary>
    public static Dt GetRedemptionDateFromFromCustomizedSchedule<T>(PaymentSchedule ps) where T : Payment
    {
      if (ps == null || ps.Count == 0)
        return Dt.Empty;
      var lastPayments = ps.GetLastPaymentsByType<T>().ToList();
      return (lastPayments.Count > 0) ? lastPayments.Max(p => p.PayDt) : Dt.Empty;
    }

    /// <summary>
    /// Returns a by date dictionary of toolkit payments and a by date dictionary of principal amounts
    /// and a by date dictionary of discount factors
    /// all dictionaries will have the same number of items and the same keys
    /// </summary>
    /// <param name="asOf">Pricer as of date</param>
    /// <param name="settle">Pricer settle date</param>
    /// <param name="schedule">Payment schedule from a product</param>
    /// <param name="dc">Discount curve</param>
    /// <param name="payments">Output list of payments</param>
    /// <param name="principals">Output list of prioncipals</param>
    /// <param name="discountFactors">Output list of discount factors</param>
    public static void CreatePaymentsAndPrincipalsAndDiscountFactors(
      Dt asOf,
      Dt settle,
      PaymentSchedule schedule,
      DiscountCurve dc,
      out List<Payment> payments,
      out SortedDictionary<Dt, double> principals,
      out SortedDictionary<Dt, double> discountFactors)
    {
      payments = new List<Payment>();
      principals = new SortedDictionary<Dt, double>();
      var principalPayments = new SortedDictionary<Dt, Payment>();

      // first deal with principal-like payments

      foreach (var otp in schedule.GetPaymentsByType<PrincipalExchange>())
      {
        principals[otp.PayDt] = (principals.ContainsKey(otp.PayDt) ? principals[otp.PayDt] : 0.0) + otp.Amount;
        otp.Amount = 0;
        payments.Add(otp);
        principalPayments[otp.PayDt] = otp;
      }

      foreach (var prp in schedule.GetPaymentsByType<PriceReturnPayment>())
      {
        principals[prp.PayDt] = (principals.ContainsKey(prp.PayDt) ? principals[prp.PayDt] : 0.0) + prp.Amount;
        prp.Amount = 0;
        payments.Add(prp);
        principalPayments[prp.PayDt] = prp;
      }

      // second deal with interest-like payments

      foreach (var fip in schedule.GetPaymentsByType<InterestPayment>())
      {
        if (principalPayments.ContainsKey(fip.PayDt))
        {
          // note   principal payment may have already been removed
          // in case where > 1 interest payment on same date. 
          payments.Remove(principalPayments[fip.PayDt]);
          principalPayments.Remove(fip.PayDt);
        }
        payments.Add(fip);

        // If there is no principal for this date then add a 0 principal amount for this date
        if (!principals.ContainsKey(fip.PayDt))
          principals[fip.PayDt] = 0;
      }

      // TODO remove this code. Only added to handle ScaledPayment temporarily to support Trade Return Swaps 
      foreach (var sp in schedule.GetPaymentsByType<ScaledPayment>())
      {
        var fip = sp.UnderlyingPayment;
        if (principalPayments.ContainsKey(fip.PayDt))
        {
          // note   principal payment may have already been removed
          // in case where > 1 interest payment on same date. 
          payments.Remove(principalPayments[fip.PayDt]);
          principalPayments.Remove(fip.PayDt);
        }
        payments.Add(fip);

        // If there is no principal for this date then add a 0 principal amount for this date
        if (!principals.ContainsKey(fip.PayDt))
          principals[fip.PayDt] = 0;
      }

      foreach (var bp in schedule.GetPaymentsByType<BasicPayment>())
      {
        if (principalPayments.ContainsKey(bp.PayDt))
        {
          // note   principal payment may have already been removed
          // in case where > 1 interest payment on same date. 
          payments.Remove(principalPayments[bp.PayDt]);
          principalPayments.Remove(bp.PayDt);
        }
        payments.Add(bp);

        // If there is no principal for this date then add a 0 principal amount for this date
        if (!principals.ContainsKey(bp.PayDt))
          principals[bp.PayDt] = 0;
      }

      foreach (var bp in schedule.GetPaymentsByType<DelayedPayment>())
      {
        payments.Add(bp);
      }

      discountFactors = new SortedDictionary<Dt, double>();

      payments.Sort((pt1, pt2) =>
      {
        int d = Dt.Cmp(pt1.PayDt, pt2.PayDt);
        if (d == 0 && pt1 is InterestPayment && pt2 is InterestPayment)
        {
          var ip1 = pt1 as InterestPayment;
          var ip2 = pt2 as InterestPayment;
          return Dt.Cmp(ip1.AccrualStart, ip2.AccrualStart);
        }
        return d;
      });

      foreach (var p in payments)
      {
        var discountFactor = 1.0;

        if (dc != null)
          discountFactor = (p.PayDt > settle) ? dc.Interpolate(asOf, p.PayDt) : 0.0;

        if (!discountFactors.ContainsKey(p.PayDt))
          discountFactors[p.PayDt] = discountFactor;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ps"></param>
    /// <returns></returns>
    public static PaymentSchedule ScalePayments(PaymentSchedule ps)
    {
      if (ps == null)
        return ps;

      var paymentSchedule = new PaymentSchedule();
      foreach (var payDt in ps.GetPaymentDates())
      {
        foreach (var payment in ps.GetPaymentsOnDate(payDt))
        {
          var underlyingPmt = payment.GetUnderlyingPayment();
          if (underlyingPmt is PriceReturnPayment)
          {
            underlyingPmt.Amount = underlyingPmt.Amount;
          }

          var scaledPayment = payment as ScaledPayment;
          if (scaledPayment != null)
          {
            underlyingPmt.Scale(scaledPayment.Notional);
          }

          paymentSchedule.AddPayment(underlyingPmt);
        }
      }

      return paymentSchedule;
    }

    public static void EnableInterestsCompounding(
      this IEnumerable<InterestPayment> payments,
      CompoundingConvention compoundingConvention)
    {
      if (compoundingConvention == CompoundingConvention.None
          || compoundingConvention == CompoundingConvention.Simple)
      {
        return;
      }

      foreach (var group in payments.GroupBy(p => p.PayDt))
      {
        InterestPayment previousPayment = null;
        foreach (var payment in @group.OrderBy(p => p.AccrualEnd))
        {
          payment.PrincipalCalculator = GetCompoundingCalculator(
            payment, previousPayment);
          previousPayment = payment;
        }
      }
    }

    private static Func<double> GetCompoundingCalculator(
      InterestPayment payment, InterestPayment prevPayment)
    {
      if (prevPayment == null)
      {
        return null;
      }

      // If we are here, at least two interest payments
      // share the same payment date.
      return EvaluateOnce.Get(() => payment.Notional - prevPayment.Notional +
                                    prevPayment.CalculationPrincipal + prevPayment.Amount);
    }

    public static DiscountCurve InitIrrSolver(CashflowAdapter cfa,
      Dt asOf, Dt settle, DayCount dc, Frequency freq,
      DiscountCurve discountCurve, CashflowModelFlags flags,
      out int firstIndex)
    {
      bool isp = (flags & CashflowModelFlags.IncludeSettlePayments) != 0;
      bool crtpd = (flags & CashflowModelFlags.CreditRiskToPaymentDate) != 0;
      int index = 0;
      for (int i = 0; i < cfa.Count; i++)
      {
        if (Dt.Cmp(GetPeriodEndDate(cfa, i, crtpd), settle) > 0
            || (isp && Dt.Cmp(GetPeriodEndDate(cfa, i, crtpd), settle) == 0))
        {
          index = i;
          break;
        }
      }
      firstIndex = index;

      if (discountCurve == null)
        discountCurve = new DiscountCurve(asOf);
      discountCurve.Clear();
      discountCurve.AsOf = asOf;
      discountCurve.DayCount = dc;
      discountCurve.Frequency = freq;

      for (int i = index; i < cfa.Count; i++)
      {
        discountCurve.Add(cfa.GetDt(i), 0.0);
      }
      return discountCurve;
    }

    private static Dt GetPeriodEndDate(CashflowAdapter cfa, int index,
      bool creditRiskToPaymentDate)
    {
      return creditRiskToPaymentDate ? cfa.GetDt(index) : cfa.GetEndDt(index);
    }


    public static CDS GetCds(SurvivalCurve survivalCurve, Dt maturity)
    {
      CDS closestCds = null;
      foreach (CurveTenor tenor in survivalCurve.Tenors)
      {
        if (tenor != null)
        {
          CDS cds = tenor.Product as CDS;
          if (cds == null) continue;
          closestCds = cds;
          if (cds.Maturity >= maturity)
            break;
        }
      }
      if (closestCds == null)
        throw new ToolkitException("Survival curve is not a CDS curve");

      // Fix the effective and first premium dates
      closestCds = (CDS)closestCds.Clone();
      Dt date = closestCds.FirstPrem;
      if (date >= maturity)
        closestCds.FirstPrem = Dt.Empty;
      else
      {
        Dt lastPrem = GetPrevCdsDate(maturity);
        if (lastPrem > closestCds.FirstPrem)
          closestCds.LastPrem = lastPrem;
      }
      closestCds.Maturity = maturity;
      closestCds.Fee = 0;
      return closestCds;
    }

    public static Dt GetPrevCdsDate(Dt maturity)
    {
      Dt date = Dt.CDSRoll(maturity);
      while (date >= maturity)
        date = Dt.Add(date, Frequency.Quarterly, -1, CycleRule.Twentieth);
      return date;
    }

    public static CDSCashflowPricer GetPricer(SurvivalCurve survivalCurve,
      Dt maturity)
    {
      if (survivalCurve.SurvivalCalibrator == null)
        throw new ArgumentException(String.Format(
          "Survival Curve {0} is a calibrated curve",
          survivalCurve.Name));
      if (!(survivalCurve.SurvivalCalibrator is SurvivalFitCalibrator))
        throw new ArgumentException(String.Format(
          "Survival Curve {0} is not calibrated using a Fit",
          survivalCurve.Name));

      CDS cds = GetCds(survivalCurve, maturity);
      SurvivalFitCalibrator calibrator =
        (SurvivalFitCalibrator)survivalCurve.SurvivalCalibrator;
      CDSCashflowPricer pricer =
        (CDSCashflowPricer)calibrator.GetPricer(survivalCurve, cds);
      return pricer;
    }

    public static SurvivalCurve SynchronizeSpreads(SurvivalCurve curve)
    {
      // Get new spreads
      SurvivalFitCalibrator calibrator =
        (SurvivalFitCalibrator)curve.SurvivalCalibrator;
      foreach (CurveTenor tenor in curve.Tenors)
      {
        // note: we assume all the tenors are CDS products.
        // Other products are not supported yet.
        CDS cds = (CDS)tenor.Product;
        ICDSPricer pricer = (ICDSPricer)calibrator.GetPricer(curve, cds);
        cds.Premium = pricer.BreakEvenPremium();
      }
      curve.OriginalSpread = curve.Spread;
      return curve;
    }

    #endregion Helpers

    #region BondPaymentUtil

    #region Calculations

    private static double YieldUp = 0.0025;

    // it is better that here should directly use the paymentschedule as 
    // input parameter, not use the cfAdapter as parameter. 
    // In the payment schedule method, it is better that 
    // the T fraction calculation uses the period start/end. 
    // the price = sum{df * payment.DomesticAmount}.
    // need to revisit this function later on.

    public static double YtmToPrice(CashflowAdapter cfAdapter,
      Dt asOf, Dt settle, double marketQuote, double accruedAmount,
      DayCount dayCount, Frequency freq)
    {
      var cf = cfAdapter.Data as Cashflow;
      if (cf != null)
      {
        return BondModelAmortizing.YtmToPrice(cf, asOf, settle,
          marketQuote, accruedAmount, dayCount, freq);
      }

      var firstIdx = 0;
      var price = 0.0;
      var count = cfAdapter.Count;
      for (int i = 0; i < count; i++)
      {
        if (Dt.Cmp(cfAdapter.GetEndDt(i), settle) > 0)
        {
          firstIdx = i;
          break;
        }
      }

      for (int i = firstIdx; i < count; i++)
      {
        Dt pStart = cfAdapter.GetStartDt(i);
        Dt pEnd = cfAdapter.GetEndDt(i);
        double T = Dt.Fraction(pStart, pEnd, settle, pEnd, dayCount, freq);
        double df = RateCalc.PriceFromRate(marketQuote, T, freq);
        price += df * (cfAdapter.GetAccrued(i) + cfAdapter.GetAmount(i));
      }
      return price - accruedAmount;
    }

    public static double PriceToYtm(CashflowAdapter cfAdapter, Dt settle, Dt protectionStart,
      DayCount dayCount, Frequency freq, double effectiveFlatPrice, double accruedInterest)
    {
      var cf = cfAdapter.Data as Cashflow;
      if (cf != null)
      {
        return BondModelAmortizing.PriceToYtm(cf, settle, protectionStart,
          dayCount, freq, effectiveFlatPrice, accruedInterest);
      }

      var solver = new Generic();
      var fn = new PriceToYtmFn(cfAdapter, settle, protectionStart, dayCount, freq, accruedInterest);

      solver.setLowerBounds(RateCalc.RateLowerBound(freq));
      solver.setUpperBounds(100.0);
      solver.setInitialPoint(0.05);
      return solver.solve(fn, effectiveFlatPrice);
    }

    public static double PriceToTrueYield(CashflowAdapter cfAdapter, Dt asOf, Dt settle, DayCount dc,
      Frequency freq, double price, double accruedInterest)
    {
      var index = 0;
      for (int i = 0; i < cfAdapter.Count; i++)
      {
        if (Dt.Cmp(cfAdapter.GetDt(i), settle) > 0)
        {
          index = i;
          break;
        }
      }

      var solver = new Generic();
      var fn = new PriceToTrueYieldFn(cfAdapter, asOf, settle, dc, freq, accruedInterest, index);

      solver.setLowerBounds(RateCalc.RateLowerBound(freq));
      solver.setUpperBounds(100.0);
      solver.setInitialPoint(0.05);
      return solver.solve(fn, price);
    }

    public static double PV01(CashflowAdapter cfAdapter, Dt asOf, Dt settle, double yield,
      double accruedInterest, DayCount dc, Frequency freq)
    {
      return dPdY(cfAdapter, asOf, settle, yield,
        accruedInterest, dc, freq) * 0.0001;
    }

    public static double Duration(CashflowAdapter cfAdapter, Dt asOf, Dt settle, double yield,
      double accruedInterest, DayCount dc, Frequency freq, double price)
    {
      CheckFrequency(freq);
      return (-dPdYHelper(cfAdapter, asOf, settle, yield, accruedInterest,
        dc, freq) / (price + accruedInterest)) * (1.0 + (yield / (int)freq));
    }

    public static double ModDuration(CashflowAdapter cfAdapter, Dt asOf, Dt settle,
      double yield, double accruedInterest, DayCount dc, Frequency freq, double price)
    {
      CheckFrequency(freq);
      return Duration(cfAdapter, asOf, settle, yield, accruedInterest, dc, freq, price)
        / (1.0 + yield / (int)freq);
    }

    public static double Convexity(CashflowAdapter cfAdapter, Dt asOf, Dt settle, double yield,
      double accruedInterest, DayCount dc, Frequency freq, double price)
    {
      CheckFrequency(freq);
      return dP2dY2Helper(cfAdapter, asOf, settle, yield, accruedInterest, dc, freq, price)
        / (price + accruedInterest);
    }

    private static double dPdYHelper(CashflowAdapter cfAdapter, Dt asOf, Dt settle, double y,
      double accruedInterest, DayCount dc, Frequency freq)
    {
      double pu = YtmToPrice(cfAdapter, asOf, settle, y + YieldUp, accruedInterest, dc, freq);
      double pd = YtmToPrice(cfAdapter, asOf, settle, y - YieldUp, accruedInterest, dc, freq);
      return (pu - pd) / (2 *YieldUp);
    }

    public static double dPdY(CashflowAdapter cfAdapter, Dt asOf, Dt settle,
      double Y, double AI, DayCount dc, Frequency freq)
    {
      CheckFrequency(freq);
      return dPdYHelper(cfAdapter, asOf, settle, Y, AI, dc, freq);
    }

    public static double dP2dY2(CashflowAdapter cfAdapter, Dt asOf, Dt settle,
      double y, double accruedInterest, DayCount dc, Frequency freq, double price)
    {
      return dP2dY2Helper(cfAdapter, asOf, settle, y, accruedInterest, dc, freq, price);
    }

    private static double dP2dY2Helper(CashflowAdapter cfAdapter, Dt asOf, Dt settle, double Y,
      double AI, DayCount dc, Frequency freq, double P)
    {
      double pu = YtmToPrice(cfAdapter, asOf, settle, Y + YieldUp, AI, dc, freq);
      double pd = YtmToPrice(cfAdapter, asOf, settle, Y - YieldUp, AI, dc, freq);
      return (pu + pd - 2 * P) / (YieldUp *YieldUp);
    }


    // this function tries to replicate the legacy fwd value calculations of
    // cashflow method, and is currently used in the bond and bond-related pricers.
    // In the legacy cashflow method, the fwd value calculations are not quite right.
    // For example, assume there is a period like this:
    // 
    // period start-------forwardDt----------------period end
    //
    // in the legacy cash flow method, it calculates the value of (period start--forwardDt),
    // then uses price minus the value in that period (start--forwardDt).
    // However, the right way should be directly calculating the period
    // forwardDt--period end. To keep that backward compatable,
    // in the payment schedule method, we also cut the payment schedule
    // before the forwardDt and then use price - fwdValue. 
    // The reason we don't fix this issue right now since some functinos
    // in the BondFuture XL functions, like Pv01 and Price01,
    // where the calculations don't match market exercise, 
    // which should take discount curve as input,
    // bump the yield, and calculate the differents, not directly calculate the model price,
    // still uses the legacy cashflow way to calculate the fwd value. 
    // We should fix that problem first, and then fix this fwd value calculations.  

    [Obsolete]
    public static double FwdValue(CashflowAdapter cfAdapter, Dt settle,
      double price, DiscountCurve discountcurve, SurvivalCurve survivalCurve,
      SurvivalCurve counterpartyCurve, double correlation,
      bool includeSettlePayment, bool includeMaturityProtection,
      bool fullFirstCoupon, int timeStep, TimeUnit stepUnit, Dt forwardDt)
    {
      if (Dt.Cmp(forwardDt, settle) < 0)
        throw new ArgumentException("Forward date is before the settle date");
      if (counterpartyCurve != null && (correlation < -1.0 || correlation > 1.0))
        throw new ArgumentException("Invalid correction, must be between -1 and 1");
      var flags = AdapterUtil.CreateFlags(includeSettlePayment,
        includeMaturityProtection, fullFirstCoupon);
      var pv = PvTo(cfAdapter, settle, settle, discountcurve, survivalCurve,
        counterpartyCurve, correlation, flags, timeStep, stepUnit, forwardDt);
      return (price - pv) / discountcurve.Interpolate(settle, forwardDt);
    }

    [Obsolete]
    public static double PvTo(CashflowAdapter cfAdapter, Dt asOf, Dt settle,
      DiscountCurve discountcurve, SurvivalCurve survivalCurve,
      SurvivalCurve counterpartyCurve, double correlation,
      CashflowModelFlags flags, int timeStep,
      TimeUnit stepUnit, Dt forwardDt)
    {
      var pv = 0.0;
      var data = cfAdapter.Data;
      var cf = data as Cashflow;
      if (cf != null)
      {
        int idx = 0;
        if (forwardDt.IsEmpty())
          idx = cf.Count;
        else
        {
          for (int n = cf.Count; idx < n; ++idx)
          {
            if (cf.GetEndDt(idx) > forwardDt) break;
          }
        }
        pv = cf.CashflowPv(settle, settle, discountcurve, survivalCurve,
          counterpartyCurve, correlation, timeStep, stepUnit, flags, idx);
      }
      else
      {
        var ps = cfAdapter.Ps;
        var principle = cfAdapter.OriginalPrincipal;
        if (forwardDt.IsEmpty())
        {
          pv = ps.CalculatePv(settle, settle, discountcurve,
            survivalCurve, counterpartyCurve, correlation,
            timeStep, stepUnit, flags) / principle;
        }
        else
        {
          var partPs = new PaymentSchedule();
          foreach (Payment p in ps)
          {
            if (p.GetCreditRiskEndDate() <= forwardDt)
              partPs.AddPayment(p);
          }

          pv = partPs.CalculatePv(settle, settle, discountcurve,
            survivalCurve, counterpartyCurve, correlation,
            timeStep, stepUnit, flags) / principle;
        }
      }
      return pv;
    }

    #endregion Calculations

    #region Helpers

    private static void CheckFrequency(Frequency freq)
    {
      if (freq < Frequency.Annual || freq > Frequency.Monthly)
        throw new ArgumentException("Invalid coupon frequency");
      return;
    }

    #endregion Helpers

    #region Solvers

    public class PriceToYtmFn : SolverFn
    {
      public PriceToYtmFn(CashflowAdapter cfa, Dt asOf, Dt settle, DayCount dc, Frequency freq,
        double accruedInterest)
      {
        _cfa = cfa;
        _asOf = asOf;
        _settle = settle;
        _dc = dc;
        _freq = freq;
        _accruedInterest = accruedInterest;
      }
      public override double evaluate(double x)
      {
        double price = YtmToPrice(_cfa, _asOf, _settle, x, _accruedInterest, _dc, _freq);
        return price;
      }

      public CashflowAdapter Cfa => _cfa;
      public Dt AsOf => _asOf;
      public Dt Settle => _settle;
      public DayCount Dc => _dc;
      public Frequency Freq => _freq;
      public double AccruedInterest => _accruedInterest;

      private CashflowAdapter _cfa;
      private Dt _asOf, _settle;
      private DayCount _dc;
      private Frequency _freq;
      private double _accruedInterest;
    }

    private class PriceToTrueYieldFn : PriceToYtmFn
    {
      public PriceToTrueYieldFn(CashflowAdapter cfa, Dt asOf, Dt settle, DayCount dc, Frequency freq,
        double accruedInterest, int firstIndex) : base(cfa, asOf, settle, dc, freq, accruedInterest)
      {
        _firstIndex = firstIndex;
      }
      public override double evaluate(double x)
      {
        double price = 0.0;
        var cfa = Cfa;
        for (int i = _firstIndex; i < cfa.Count; i++)
        {
          Dt pStart = cfa.GetStartDt(i);
          Dt pEnd = cfa.GetEndDt(i);
          Dt payDt = cfa.GetDt(i);
          double T = Dt.Fraction(pStart, pEnd, Settle, payDt, Dc, Freq);
          var df = RateCalc.PriceFromRate(x, T, Freq);
          price += df * (cfa.GetAccrued(i) + cfa.GetAmount(i));
        }
        return price - AccruedInterest;
      }

      private int _firstIndex;
    }

    #endregion Solvers


    #endregion BondPaymentUtil
  }
}