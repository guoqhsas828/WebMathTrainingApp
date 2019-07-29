//
// ReinvestmentInterestPayment.cs
// 
//

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows
{
  #region Reinvestment Interest Payment for Sell/Buy Backs with a fixed repo rate

  /// <summary>
  ///  Reinvestment payment interest, required for Sell/Buy backs
  /// </summary>
  [Serializable]
  public class ReinvestmentFixedInterestPayment : FixedInterestPayment
  {
    #region Constructors

    /// <summary>
    /// Create a reinvestment interest payment
    /// </summary>
    /// <param name="prevPayDt">Previous Payment Date or Accrual Start</param>
    /// <param name="payDt">Payment Date</param>
    /// <param name="ccy">Currency of payment</param>
    /// <param name="cycleStart">Start of payment cycle</param>
    /// <param name="cycleEnd">End of payment cycle</param>
    /// <param name="periodStart">Start of accrual period</param>
    /// <param name="periodEnd">End of accrual period</param>
    /// <param name="exDivDt">Ex dividend date</param>
    /// <param name="notional">notional for this payment</param>
    /// <param name="coupon">fixed coupon for this payment</param>
    /// <param name="dc">Daycount convention</param>
    /// <param name="compoundingFreq">Compounding frequency</param>
    public ReinvestmentFixedInterestPayment(Dt prevPayDt, Dt payDt, Currency ccy, Dt cycleStart, Dt cycleEnd, Dt periodStart, Dt periodEnd, Dt exDivDt,
      double notional, double coupon, DayCount dc, Frequency compoundingFreq)
      : base(prevPayDt, payDt, ccy, cycleStart, cycleEnd, periodStart, periodEnd, exDivDt, notional, coupon, dc, compoundingFreq) { }

    #endregion
  }

  #endregion

  #region Reinvestment Interest Payment for Sell/Buy Backs with a floating repo rate

  /// <summary>
  ///  Reinvestment payment interest, required for Sell/Buy backs
  /// </summary>
  [Serializable]
  public class ReinvestmentFloatingInterestPayment : FloatingInterestPayment
  {
    #region Constructors

    /// <summary>
    ///  Reinvestment interest payment with a floating rate
    /// </summary>
    /// <param name="prevPayDt"></param>
    /// <param name="payDt"></param>
    /// <param name="ccy"></param>
    /// <param name="cycleStart"></param>
    /// <param name="cycleEnd"></param>
    /// <param name="periodStart"></param>
    /// <param name="periodEnd"></param>
    /// <param name="exDivDt"></param>
    /// <param name="notional"></param>
    /// <param name="spread"></param>
    /// <param name="dc"></param>
    /// <param name="compoundingFreq"></param>
    /// <param name="compoundingConvention"></param>
    /// <param name="rateProjector"></param>
    /// <param name="forwardAdjustment"></param>
    /// <param name="cycleRule"></param>
    /// <param name="indexMultiplier"></param>
    public ReinvestmentFloatingInterestPayment(Dt prevPayDt, Dt payDt, Currency ccy, Dt cycleStart, Dt cycleEnd, Dt periodStart,
      Dt periodEnd, Dt exDivDt, double notional, double spread, DayCount dc, Frequency compoundingFreq,
      CompoundingConvention compoundingConvention, IRateProjector rateProjector,
      IForwardAdjustment forwardAdjustment, CycleRule cycleRule, double indexMultiplier)
      : base(prevPayDt, payDt, ccy, cycleStart, cycleEnd, periodStart,
      periodEnd, exDivDt, notional, spread, dc, compoundingFreq,
      compoundingConvention, rateProjector,
      forwardAdjustment, cycleRule, indexMultiplier)
      { }

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
      PaymentScheduleUtils.FloatingInterestPaymentGenerator generator = (prevPay, paymentDate, cycleStart, cycleEnd, periodStart, periodEnd, notional, cpn, fraction,
                                            includeEndDateInAccrual, accrueOnCycle, indexMultiplier) =>
        new ReinvestmentFloatingInterestPayment(prevPay > from ? prevPay : from, paymentDate, ccy, cycleStart > from ? cycleStart : from, cycleEnd, periodStart > from ? periodStart : from, 
                                    periodEnd, exDivCalculator != null ? exDivCalculator.Calc(periodEnd) : Dt.Empty, notional, cpn,
                                    dc, projectionParams.CompoundingFrequency, projectionParams.CompoundingConvention,
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
      return sched.FloatingInterestPaymentScheduleFactory(from, to, flags, coupon, coupons, initialNotional, amortizations, intermediateExchange, dc, generator,
                                                  includeTradeSettleCf, defaultDt, defaultSettleDt, paymentLag, resets, rateMultiplierSchedule);
    }

    #endregion
  }

  #endregion

}
