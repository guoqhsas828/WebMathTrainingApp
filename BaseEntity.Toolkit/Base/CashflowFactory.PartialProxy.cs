/*
 * CashflowFactor.PartialProxy.cs
 *
 *   2007-2008. All rights reserved.
 * 
 */

using System.Collections.Generic;
using System.ComponentModel;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;

namespace BaseEntity.Toolkit.Cashflows
{
  [ReadOnly(true)]
  public static class CashflowFactory
  {
    #region Methods

    /// <summary>
    /// Fills the fixed rate cashflow.
    /// </summary>
    /// <param name="cf">The cashflow to fill.</param>
    /// <param name="asOf">As-of date.</param>
    /// <param name="schedParams">The schedule parameters.</param>
    /// <param name="ccy">The currency.</param>
    /// <param name="dayCount">The day count.</param>
    /// <param name="coupon">The coupon.</param>
    /// <param name="cpnSchedule">The coupon schedule.</param>
    /// <param name="principal">The principal.</param>
    /// <param name="amortSchedule">The amort schedule.</param>
    /// <param name="defaultAmount">The default amount.</param>
    /// <param name="defaultCcy">The default ccy.</param>
    /// <param name="defaultDate">The default date.</param>
    /// <param name="fee">The fee.</param>
    /// <param name="feeSettle">The fee settle.</param>
    /// <returns></returns>
    public static Cashflow FillFixed(Cashflow cf, Dt asOf,
                                     IScheduleParams schedParams,
                                     Currency ccy, DayCount dayCount,
                                     double coupon, IList<CouponPeriod> cpnSchedule,
                                     double principal, IList<Amortization> amortSchedule,
                                     double defaultAmount, Currency defaultCcy, Dt defaultDate,
                                     double fee, Dt feeSettle)
    {
      var schedule = Schedule.CreateScheduleForCashflowFactory(schedParams);
      return FillFixed(cf, asOf, schedule, schedParams, ccy, dayCount, coupon, cpnSchedule, principal, amortSchedule,
                       defaultAmount, defaultCcy, defaultDate, fee, feeSettle);
    }

    /// <summary>
    /// Fills the fixed rate cashflow.
    /// </summary>
    /// <param name="cf">The cashflow to fill.</param>
    /// <param name="asOf">As-of date.</param>
    /// <param name="schedule">Schedule</param>
    /// <param name="schedParams">The schedule parameters.</param>
    /// <param name="ccy">The currency.</param>
    /// <param name="dayCount">The day count.</param>
    /// <param name="coupon">The coupon.</param>
    /// <param name="cpnSchedule">The CPN schedule.</param>
    /// <param name="principal">The principal.</param>
    /// <param name="amortSchedule">The amort schedule.</param>
    /// <param name="defaultAmount">The default amount.</param>
    /// <param name="defaultCcy">The default ccy.</param>
    /// <param name="defaultDate">The default date.</param>
    /// <param name="fee">The fee.</param>
    /// <param name="feeSettle">The fee settle.</param>
    /// <returns></returns>
    public static Cashflow FillFixed(Cashflow cf, Dt asOf,
                                     Schedule schedule,
                                     IScheduleParams schedParams,
                                     Currency ccy, DayCount dayCount,
                                     double coupon, IList<CouponPeriod> cpnSchedule,
                                     double principal, IList<Amortization> amortSchedule,
                                     double defaultAmount, Currency defaultCcy, Dt defaultDate,
                                     double fee, Dt feeSettle)
    {
      Dt[] cpnDates;
      double[] cpnAmounts;

      Dt nextCouponDate = schedule != null ? schedule.GetNextCouponDate(asOf) : Schedule.NextCouponDate(schedParams, asOf);
      CouponPeriodUtil.FromSchedule(cpnSchedule, nextCouponDate, schedParams.Maturity, coupon, out cpnDates, out cpnAmounts);

      Dt[] amortDates;
      double[] amortAmounts;
      AmortizationUtil.FromSchedule(amortSchedule, out amortDates, out amortAmounts);

      if (cf == null)
        cf = new Cashflow(asOf);

      Native.CashflowFactory.FillFixed(cf, schedule, asOf,
                schedParams.AccrualStartDate, schedParams.FirstCouponDate, schedParams.NextToLastCouponDate, schedParams.Maturity,
                ccy, fee, feeSettle, cpnDates, cpnAmounts, dayCount, schedParams.Frequency, schedParams.Roll, schedParams.Calendar,
                principal, amortDates, amortAmounts, defaultAmount, defaultCcy, defaultDate,
                schedParams.CycleRule, schedParams.CashflowFlag);

      return cf;
    }


    /// <summary>
    /// Fills the float rate cashflow.
    /// </summary>
    /// <param name="cf">The cashflow to fill</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="schedParams">The schedule parameters</param>
    /// <param name="ccy">The currency</param>
    /// <param name="dayCount">The day count</param>
    /// <param name="coupon">The coupon</param>
    /// <param name="cpnSchedule">The CPN schedule</param>
    /// <param name="principal">The principal</param>
    /// <param name="amortSchedule">The amort schedule</param>
    /// <param name="referenceCurve">The reference curve</param>
    /// <param name="rateResets">The rate resets</param>
    /// <param name="defaultAmount">The default amount</param>
    /// <param name="defaultCcy">The default ccy</param>
    /// <param name="defaultDate">The default date</param>
    /// <param name="fee">The fee</param>
    /// <param name="feeSettle">The fee settle</param>
    /// <returns>Generated cashflow</returns>
    public static Cashflow FillFloat(Cashflow cf, Dt asOf,
                                     IScheduleParams schedParams,
                                     Currency ccy, DayCount dayCount,
                                     double coupon, IList<CouponPeriod> cpnSchedule,
                                     double principal, IList<Amortization> amortSchedule,
                                     Curve referenceCurve, IList<RateReset> rateResets,
                                     double defaultAmount, Currency defaultCcy, Dt defaultDate,
                                     double fee, Dt feeSettle)
    {
      var schedule = Schedule.CreateScheduleForCashflowFactory(schedParams);
      return FillFloat(cf, asOf, schedule, schedParams, ccy, dayCount, coupon, cpnSchedule, principal, amortSchedule,
                       referenceCurve, rateResets, defaultAmount, defaultCcy, defaultDate, fee, feeSettle);
    }

    /// <summary>
    /// Fills the float rate cashflow.
    /// </summary>
    /// <param name="cf">The cashflow to fill</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="schedule">Schedule</param>
    /// <param name="schedParams">The schedule parameters</param>
    /// <param name="ccy">The currency</param>
    /// <param name="dayCount">The day count</param>
    /// <param name="coupon">The coupon</param>
    /// <param name="cpnSchedule">The CPN schedule</param>
    /// <param name="principal">The principal</param>
    /// <param name="amortSchedule">The amort schedule</param>
    /// <param name="referenceCurve">The reference curve</param>
    /// <param name="rateResets">The rate resets</param>
    /// <param name="defaultAmount">The default amount</param>
    /// <param name="defaultCcy">The default ccy</param>
    /// <param name="defaultDate">The default date</param>
    /// <param name="fee">The fee</param>
    /// <param name="feeSettle">The fee settle</param>
    /// <returns>Generated cashflow</returns>
    public static Cashflow FillFloat(Cashflow cf, Dt asOf,
                                     Schedule schedule, IScheduleParams schedParams,
                                     Currency ccy, DayCount dayCount,
                                     double coupon, IList<CouponPeriod> cpnSchedule,
                                     double principal, IList<Amortization> amortSchedule,
                                     Curve referenceCurve, IList<RateReset> rateResets,
                                     double defaultAmount, Currency defaultCcy, Dt defaultDate,
                                     double fee, Dt feeSettle)
    {
      Dt[] cpnDates;
      double[] cpnAmounts;

      Dt nextCouponDate = schedule != null ? schedule.GetNextCouponDate(asOf) : Schedule.NextCouponDate(schedParams, asOf);
      CouponPeriodUtil.FromSchedule(cpnSchedule, nextCouponDate, schedParams.Maturity, coupon, out cpnDates, out cpnAmounts);

      Dt[] amortDates;
      double[] amortAmounts;
      AmortizationUtil.FromSchedule(amortSchedule, out amortDates, out amortAmounts);

      Dt[] resetDates;
      double[] resetRates;
#if USE_SCHEDULE
      RateResetUtil.FromSchedule(rateResets, out resetDates, out resetRates);
#else
      if (rateResets != null && rateResets.Count > 0)
      {
        Schedule sched = (schedule != null) ? schedule : Schedule.CreateScheduleForCashflowFactory(schedParams);
        int numResets = rateResets.Count;
        resetDates = new Dt[numResets];
        resetRates = new double[numResets];
        int i = 0; // counter through resetDates/Rates
        int j = 0; // counter through schedule

        foreach (RateReset r in rateResets)
        {
          Dt resetDate = r.Date;

          // if reset was captured for a rolled period start then pass to the cashflow model
          // as the unadjusted period start; FillFloat only looks for most recent resets, <= period start
          for (; j < sched.Count; j++)
          {
            Dt periodStart = sched.GetPeriodStart(j);
            Dt adjPeriodStart = Dt.Roll(periodStart, schedParams.Roll, schedParams.Calendar);
            if (Dt.Cmp(resetDate, adjPeriodStart) == 0)
            {
              resetDate = periodStart;
              ++j; // start at next period for next rate reset
              break;
            }
            else if (Dt.Cmp(adjPeriodStart, resetDate) > 0)
            {
              break;
            }
          }

          resetDates[i] = resetDate;
          resetRates[i++] = r.Rate;
        }
      }
      else
      {
        resetDates = new Dt[0];
        resetRates = new double[0];
      }
#endif

      if (cf == null)
        cf = new Cashflow(asOf);
      Native.CashflowFactory.FillFloat(cf, schedule, asOf,
                schedParams.AccrualStartDate, schedParams.FirstCouponDate, schedParams.NextToLastCouponDate,
                schedParams.Maturity, ccy, fee, feeSettle, cpnDates, cpnAmounts, dayCount, schedParams.Frequency,
                schedParams.Roll, schedParams.Calendar, referenceCurve, resetDates, resetRates,
                principal, amortDates, amortAmounts, defaultAmount, defaultCcy, defaultDate,
                schedParams.CycleRule, schedParams.CashflowFlag);

      return cf;
    }

    #region Backward Compatible

#if OBSOLETE

    /// <summary>
    ///   Fill a set of regular fixed cashflows.
    /// </summary>
    ///
    /// <param name="cf">Cashflow stream to fill</param>
    /// <param name="asOf">As-of date. Cashflows are generated from this date</param>
    /// <param name="effective">Effective date</param>
    /// <param name="firstCpn">First coupon payment date. Coupons cycle from this date</param>
    /// <param name="lastCpn">Last coupon payment date before maturity</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency of coupon</param>
    /// <param name="fee">Fee paid</param>
    /// <param name="feeSettle">Payment date for fee</param>
    /// <param name="coupon">Original coupon</param>
    /// <param name="cpnSchedule">Coupon schedule</param>
    /// <param name="dc">Daycount of coupon accrual</param>
    /// <param name="freq">Frequency of coupons payments (per year)</param>
    /// <param name="roll">Business day roll convention for date adjustments</param>
    /// <param name="cal">Calendar for payment date adjustments</param>
    /// <param name="principal">Principal in percent</param>
    /// <param name="amortSchedule">Amortization schedule</param>
    /// <param name="accruedPaid">True if accrued coupon paid on default</param>
    /// <param name="includeDfltDate">True if default date included in accrued recived on default</param>
    /// <param name="defaultAmount">Cashflow (one-off) paid on default</param>
    /// <param name="defaultCcy">Currency of default payment</param>
    /// <param name="accrueOnCycle">Accrue on cycle rather than payment dates</param>
    /// <param name="adjustNext">Is true if the next period start is adjusted to the payment date</param>
    /// <param name="adjustLast">Is true if the last period end date is rolled</param>
    /// <param name="eomRule">If true, moves payment date to the end of the month if the starting date was the end of the month.</param>
    /// <param name="defaultDate">Date of default or empty date</param>
    /// <param name="includeMaturityAccrual">Include maturity date in accrual</param>
    ///
    public static Cashflow FillFixed(
      Cashflow cf, Dt asOf, Dt effective, Dt firstCpn, Dt lastCpn, Dt maturity, Currency ccy, double fee,
      Dt feeSettle, double coupon, IList<CouponPeriod> cpnSchedule, DayCount dc, Frequency freq, BDConvention roll,
      Calendar cal, double principal, IList<Amortization> amortSchedule, bool accruedPaid,
      bool includeDfltDate, double defaultAmount, Currency defaultCcy, bool accrueOnCycle,
      bool adjustNext, bool adjustLast, bool eomRule, Dt defaultDate, bool includeMaturityAccrual
      )
    {
      Dt[] cpnDates;
      double[] cpnAmounts;

      Dt nextCouponDate = ScheduleUtil.NextCouponDate(asOf, effective, firstCpn, freq, eomRule);
      CouponPeriodUtil.FromSchedule(cpnSchedule, nextCouponDate, maturity, coupon, out cpnDates, out cpnAmounts);

      Dt[] amortDates;
      double[] amortAmounts;
      AmortizationUtil.FromSchedule(amortSchedule, out amortDates, out amortAmounts);

      if (cf == null)
        cf = new Cashflow(asOf);

      FillFixed(cf, asOf, effective, firstCpn, lastCpn, maturity, ccy, fee, feeSettle, cpnDates, cpnAmounts,
                dc, freq, roll, cal, principal, amortDates, amortAmounts, accruedPaid, includeDfltDate,
                defaultAmount, defaultCcy, accrueOnCycle, adjustNext, adjustLast, eomRule, defaultDate,
                includeMaturityAccrual);

      return cf;
    }

    
    /// <summary>
    ///   Fill a set of regular fixed cashflows.
    /// </summary>
    ///
    /// <param name="cf">Cashflow stream to fill</param>
    /// <param name="asOf">As-of date. Cashflows are generated from this date</param>
    /// <param name="effective">Effective date</param>
    /// <param name="firstCpn">First coupon payment date. Coupons cycle from this date</param>
    /// <param name="lastCpn">Last coupon payment date before maturity</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency of coupon</param>
    /// <param name="fee">Fee paid</param>
    /// <param name="feeSettle">Payment date for fee</param>
    /// <param name="cpnDates">Coupon schedule dates</param>
    /// <param name="cpns">Coupons in percent</param>
    /// <param name="dc">Daycount of coupon accrual</param>
    /// <param name="freq">Frequency of coupons payments (per year)</param>
    /// <param name="roll">Business day roll convention for date adjustments</param>
    /// <param name="cal">Calendar for payment date adjustments</param>
    /// <param name="principal">Principal in percent</param>
    /// <param name="amortDates">Amortization schedule dates</param>
    /// <param name="amorts">Amortization amounts as a percentage of principal</param>
    /// <param name="accruedPaid">True if accrued coupon paid on default</param>
    /// <param name="includeDfltDate">True if default date included in accrued recived on default</param>
    /// <param name="defaultAmount">Cashflow (one-off) paid on default</param>
    /// <param name="defaultCcy">Currency of default payment</param>
    /// <param name="accrueOnCycle">Accrue on cycle rather than payment dates</param>
    /// <param name="adjustNext">Is true if the next period start is adjusted to the payment date</param>
    /// <param name="adjustLast">Is true if the last period end date is rolled</param>
    /// <param name="eomRule">If true, moves payment date to the end of the month if the starting date was the end of the month.</param>
    /// <param name="defaultDate">Date of default or empty date</param>
    /// <param name="includeMaturityAccrual">Include maturity date in accrual</param>
    public static void FillFixed(Cashflow cf, Dt asOf, Dt effective, Dt firstCpn, Dt lastCpn, Dt maturity,
      Currency ccy, double fee, Dt feeSettle,
      Dt[] cpnDates, double[] cpns,
      DayCount dc, Frequency freq, BDConvention roll, Calendar cal,
      double principal, Dt[] amortDates, double[] amorts,
      bool accruedPaid, bool includeDfltDate,
      double defaultAmount, Currency defaultCcy, bool accrueOnCycle,
      bool adjustNext, bool adjustLast, bool eomRule, Dt defaultDate, bool includeMaturityAccrual)
  {
    var cc = MakeRule(accruedPaid, includeDfltDate,
      accrueOnCycle, adjustNext, adjustLast, eomRule, includeMaturityAccrual, false);
    FillFixed(cf, null, asOf, effective, firstCpn, lastCpn, maturity,
      ccy, fee, feeSettle, cpnDates, cpns, dc, freq, roll, cal,
      principal, amortDates, amorts, defaultAmount, defaultCcy, defaultDate,
      cc.Key, cc.Value);
  }


    /// <summary>
    ///   Fill a set of regular fixed cashflows.
    /// </summary>
    ///
    /// <param name="cf">Cashflow stream to fill</param>
    /// <param name="asOf">As-of date. Cashflows are generated from this date</param>
    /// <param name="effective">Effective date</param>
    /// <param name="firstCpn">First coupon payment date. Coupons cycle from this date</param>
    /// <param name="lastCpn">Last coupon date before maturity</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency of coupon</param>
    /// <param name="fee">Fee paid</param>
    /// <param name="feeSettle">Payment date for fee</param>
    /// <param name="cpn">Percentage coupon received</param>
    /// <param name="dc">Daycount of coupon accrual</param>
    /// <param name="freq">Frequency of coupons payments (per year)</param>
    /// <param name="roll">Business day roll convention for date adjustments</param>
    /// <param name="cal">Calendar for payment date adjustments</param>
    /// <param name="principal">%Payment at maturity received</param>
    /// <param name="accruedPaid">True if accrued coupon paid on default</param>
    /// <param name="includeDfltDate">True if default date included in accrued recived on default</param>
    /// <param name="defaultAmount">Cashflow (one-off) paid on default</param>
    /// <param name="defaultCcy">Currency of default payment</param>
    /// <param name="accrueOnCycle">Accrue on cycle rather than payment dates</param>
    /// <param name="adjustNext">Is true if the next period start is adjusted to the payment date</param>
    /// <param name="adjustLast">Is true if the last period end date is rolled</param>
    /// <param name="eomRule">If true, moves payment date to the end of the month if
    ///   the starting date was the end of the month.</param>
    /// <param name="includeMaturityAccrual">Include maturity date in accrual</param>
    ///
    /// <example>
    ///   Generate a cashflow schedule for a regular fixed coupon bond
    ///   with an assumed recovery rate of 40% of face and accrued.
    ///   <code>
    ///     Cashflow cf = new Cashflow();
    ///     CashflowFactory.FillFixed(cf,              // Cashflow stream to fill
    ///                               asOfDate,        // Date to generate cashflows as-of
    ///                               effectiveDate,   // Effective date
    ///                               firstCpnDate,    // First coupon date
    ///                               lastCpnDate,     // Last coupon date before maturity
    ///                               maturityDate,    // Maturity date
    ///                               currency,        // Currency of coupon
    ///                               fee,             // Upfront fee
    ///                               feeSettle,       // Fee settlement (payment) date
    ///                               coupon,          // Coupon rate
    ///                               dayCount,        // Coupon daycount
    ///                               cpnFrequency,    // Coupon frequency (per year)
    ///                               roll,            // Business day roll convention for coupon payments
    ///                               calendar,        // Calendar for coupon payments
    ///                               principal,       // Principal amount
    ///                               true,            // Accrued coupon is paid under default
    ///                               true,            // Include default date in default accrued
    ///                               0.4,             // Recovery payment
    ///                               currency         // Currency of recovery payment
    ///                               adjustNext,      // Adjust cycle date based on next payment date
    ///                               adjustLast,      // Adjust the last payment date based on the roll convention
    ///                               eomRule          // Keep payments on end of month
    ///                               );
    ///   </code>
    /// </example>
    public static void FillFixed(Cashflow cf, Dt asOf, Dt effective, Dt firstCpn, Dt lastCpn, Dt maturity,
                          Currency ccy, double fee, Dt feeSettle, double cpn, DayCount dc,
                          Frequency freq, BDConvention roll, Calendar cal,
                          double principal, bool accruedPaid, bool includeDfltDate,
                          double defaultAmount, Currency defaultCcy, bool accrueOnCycle,
                          bool adjustNext, bool adjustLast, bool eomRule, bool includeMaturityAccrual)
    {
      Dt[] cpnDates = new Dt[]{ maturity};
      double[] cpns = new double[]{ cpn };
      Dt[] amortDates = new Dt[0];
      double[] amorts = new double[0];
      Dt defaultDate = Dt.Empty;
      FillFixed(cf, asOf, effective, firstCpn, lastCpn, maturity, ccy, fee, feeSettle,
        cpnDates, cpns, dc, freq, roll, cal, principal, amortDates, amorts, accruedPaid,
        includeDfltDate, defaultAmount, defaultCcy, accrueOnCycle, adjustNext, adjustLast,
        eomRule, defaultDate, includeMaturityAccrual);
    }

    /// <summary>
    ///   Fill a set of regular fixed cashflows.
    /// </summary>
    ///
    /// <param name="cf">Cashflow stream to fill</param>
    /// <param name="asOf">As-of date. Cashflows are generated from this date</param>
    /// <param name="effective">Effective date</param>
    /// <param name="firstCpn">First coupon payment date. Coupons cycle from this date</param>
    /// <param name="lastCpn">Last coupon date before maturity</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency of coupon</param>
    /// <param name="fee">Fee paid</param>
    /// <param name="feeSettle">Payment date for fee</param>
    /// <param name="cpn">Percentage coupon received</param>
    /// <param name="dc">Daycount of coupon accrual</param>
    /// <param name="freq">Frequency of coupons payments (per year)</param>
    /// <param name="roll">Business day roll convention for date adjustments</param>
    /// <param name="cal">Calendar for payment date adjustments</param>
    /// <param name="principal">%Payment at maturity received</param>
    /// <param name="accruedPaid">True if accrued coupon paid on default</param>
    /// <param name="includeDfltDate">True if default date included in accrued recived on default</param>
    /// <param name="defaultAmount">Cashflow (one-off) paid on default</param>
    /// <param name="defaultCcy">Currency of default payment</param>
    /// <param name="adjustNext">Is true if the next period start is adjusted to the payment date</param>
    /// <param name="eomRule">If true, moves payment date to the end of the month if
    ///   the starting date was the end of the month.</param>
    /// <param name="includeMaturityAccrual">Include maturity date in accrual</param>
    ///
    /// <example>
    ///   Generate a cashflow schedule for a regular fixed coupon bond
    ///   with an assumed recovery rate of 40% of face and accrued.
    ///   <code>
    ///     Cashflow cf = new Cashflow();
    ///     CashflowFactory.FillFixed(cf,              // Cashflow stream to fill
    ///                               asOfDate,        // Date to generate cashflows as-of
    ///                               effectiveDate,   // Effective date
    ///                               firstCpnDate,    // First coupon date
    ///                               lastCpnDate,     // Last coupon date before maturity
    ///                               maturityDate,    // Maturity date
    ///                               currency,        // Currency of coupon
    ///                               fee,             // Upfront fee
    ///                               feeSettle,       // Fee settlement (payment) date
    ///                               coupon,          // Coupon rate
    ///                               dayCount,        // Coupon daycount
    ///                               cpnFrequency,    // Coupon frequency (per year)
    ///                               roll,            // Business day roll convention for coupon payments
    ///                               calendar,        // Calendar for coupon payments
    ///                               principal,       // Principal amount
    ///                               true,            // Accrued coupon is paid under default
    ///                               true,            // Include default date in default accrued
    ///                               0.4,             // Recovery payment
    ///                               currency         // Currency of recovery payment
    ///                               adjustNext,      // Adjust cycle date based on next payment date
    ///                               eomRule          // Keep payments on end of month
    ///                               );
    ///   </code>
    /// </example>
    public static void FillFixed(Cashflow cf, Dt asOf, Dt effective, Dt firstCpn, Dt lastCpn, Dt maturity,
                          Currency ccy, double fee, Dt feeSettle, double cpn, DayCount dc,
                          Frequency freq, BDConvention roll, Calendar cal,
                          double principal, bool accruedPaid, bool includeDfltDate,
                          double defaultAmount, Currency defaultCcy, bool adjustNext, bool eomRule,
                          bool includeMaturityAccrual)
    {
      Dt[] cpnDates = new Dt[] { maturity };
      double[] cpns = new double[] { cpn };
      Dt[] amortDates = new Dt[0];
      double[] amorts = new double[0];
      Dt defaultDate = Dt.Empty;
      bool useCycleDateForAccruals = ToolkitConfigurator.Settings.CDSCashflowPricer.UseCycleDateForAccruals;
      FillFixed(cf, asOf, effective, firstCpn, lastCpn, maturity, ccy, fee, feeSettle, cpnDates, cpns, dc, freq, roll, cal,
        principal, amortDates, amorts, accruedPaid, includeDfltDate, defaultAmount, defaultCcy,
        useCycleDateForAccruals, adjustNext, false, eomRule, defaultDate, includeMaturityAccrual);
    }

    /// <summary>
    ///   Fill a set of regular fixed cashflows.
    /// </summary>
    ///
    /// <param name="cf">Cashflow stream to fill</param>
    /// <param name="asOf">As-of date. Cashflows are generated from this date</param>
    /// <param name="effective">Effective date</param>
    /// <param name="firstCpn">First coupon payment date. Coupons cycle from this date</param>
    /// <param name="lastCpn">Last coupon date before maturity</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency of coupon</param>
    /// <param name="fee">Fee paid</param>
    /// <param name="feeSettle">Payment date for fee</param>
    /// <param name="cpn">Percentage coupon received</param>
    /// <param name="dc">Daycount of coupon accrual</param>
    /// <param name="freq">Frequency of coupons payments (per year)</param>
    /// <param name="roll">Business day roll convention for date adjustments</param>
    /// <param name="cal">Calendar for payment date adjustments</param>
    /// <param name="principal">%Payment at maturity received</param>
    /// <param name="accruedPaid">True if accrued coupon paid on default</param>
    /// <param name="includeDfltDate">True if default date included in accrued recived on default</param>
    /// <param name="defaultAmount">Cashflow (one-off) paid on default</param>
    /// <param name="defaultCcy">Currency of default payment</param>
    /// <param name="includeMaturityAccrual">Include maturity date in accrual</param>
    ///
    /// <example>
    ///   Generate a cashflow schedule for a regular fixed coupon bond
    ///   with an assumed recovery rate of 40% of face and accrued.
    ///   <code>
    ///     Cashflow cf = new Cashflow();
    ///     CashflowFactory.FillFixed(cf,              // Cashflow stream to fill
    ///                               asOfDate,        // Date to generate cashflows as-of
    ///                               effectiveDate,   // Effective date
    ///                               firstCpnDate,    // First coupon date
    ///                               lastCpnDate,     // Last coupon date before maturity
    ///                               maturityDate,    // Maturity date
    ///                               currency,        // Currency of coupon
    ///                               fee,             // Upfront fee
    ///                               feeSettle,       // Fee settlement (payment) date
    ///                               coupon,          // Coupon rate
    ///                               dayCount,        // Coupon daycount
    ///                               cpnFrequency,    // Coupon frequency (per year)
    ///                               roll,            // Business day roll convention for coupon payments
    ///                               calendar,        // Calendar for coupon payments
    ///                               principal,       // Principal amount
    ///                               true,            // Accrued coupon is paid under default
    ///                               true,            // Include default date in default accrued
    ///                               0.4,             // Recovery payment
    ///                               currency         // Currency of recovery payment
    ///                               );
    ///   </code>
    /// </example>
    public static void FillFixed(Cashflow cf, Dt asOf, Dt effective, Dt firstCpn, Dt lastCpn, Dt maturity,
                          Currency ccy, double fee, Dt feeSettle, double cpn, DayCount dc,
                          Frequency freq, BDConvention roll, Calendar cal,
                          double principal, bool accruedPaid, bool includeDfltDate,
                          double defaultAmount, Currency defaultCcy, bool includeMaturityAccrual)
    {
      Dt[] cpnDates = new Dt[] { maturity };
      double[] cpns = new double[] { cpn };
      Dt[] amortDates = new Dt[0];
      double[] amorts = new double[0];
      Dt defaultDate = Dt.Empty;
      bool useCycleDateForAccruals = ToolkitConfigurator.Settings.CDSCashflowPricer.UseCycleDateForAccruals;
      FillFixed(cf, asOf, effective, firstCpn, lastCpn, maturity, ccy, fee, feeSettle, cpnDates, cpns, dc, freq, roll, cal,
        principal, amortDates, amorts, accruedPaid, includeDfltDate, defaultAmount, defaultCcy,
        useCycleDateForAccruals, false, false, false, defaultDate, includeMaturityAccrual);
    }


    /// <summary>
    ///   Fill a set of regular fixed cashflows.
    /// </summary>
    ///
    /// <param name="cf">Cashflow stream to fill</param>
    /// <param name="asOf">As-of date. Cashflows are generated from this date</param>
    /// <param name="effective">Effective date</param>
    /// <param name="firstCpn">First coupon payment date. Coupons cycle from this date</param>
    /// <param name="lastCpn">Last coupon date before maturity</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency of coupon</param>
    /// <param name="cpn">Percentage coupon received</param>
    /// <param name="dc">Daycount of coupon accrual</param>
    /// <param name="freq">Frequency of coupons payments (per year)</param>
    /// <param name="roll">Business day roll convention for date adjustments</param>
    /// <param name="cal">Calendar for payment date adjustments</param>
    /// <param name="principal">%Payment at maturity received</param>
    /// <param name="accruedPaid">True if accrued coupon paid on default</param>
    /// <param name="includeDfltDate">True if default date included in accrued recived on default</param>
    /// <param name="defaultAmount">Cashflow (one-off) paid on default</param>
    /// <param name="defaultCcy">Currency of default payment</param>
    /// <param name="includeMaturityAccrual">Include maturity date in accrual</param>
    ///
    /// <example>
    ///   Generate a cashflow schedule for a regular fixed coupon bond
    ///   with an assumed recovery rate of 40% of face and accrued.
    ///   <code>
    ///     Cashflow cf = new Cashflow();
    ///     CashflowFactory.FillFixed(cf,              // Cashflow stream to fill
    ///                               asOfDate,        // Date to generate cashflows as-of
    ///                               effectiveDate,   // Effective date
    ///                               firstCpnDate,    // First coupon date
    ///                               lastCpnDate,     // Last coupon date before maturity
    ///                               maturityDate,    // Maturity date
    ///                               currency,        // Currency of coupon
    ///                               coupon,          // Coupon rate
    ///                               dayCount,        // Coupon daycount
    ///                               cpnFrequency,    // Coupon frequency (per year)
    ///                               roll,            // Business day roll convention for coupon payments
    ///                               calendar,        // Calendar for coupon payments
    ///                               principal,       // Principal amount
    ///                               true,            // Accrued coupon is paid under default
    ///                               true,            // Include default date in default accrued
    ///                               0.4,             // Recovery payment
    ///                               currency         // Currency of recovery payment
    ///                               );
    ///   </code>
    /// </example>
    public static void FillFixed(Cashflow cf, Dt asOf, Dt effective, Dt firstCpn, Dt lastCpn, Dt maturity,
                          Currency ccy, double cpn, DayCount dc,
                          Frequency freq, BDConvention roll, Calendar cal,
                          double principal, bool accruedPaid, bool includeDfltDate,
                          double defaultAmount, Currency defaultCcy, bool includeMaturityAccrual)
    {
      Dt[] cpnDates = new Dt[] { maturity };
      double[] cpns = new double[] { cpn };
      Dt[] amortDates = new Dt[0];
      double[] amorts = new double[0];
      Dt defaultDate = Dt.Empty;
      bool useCycleDateForAccruals = ToolkitConfigurator.Settings.CDSCashflowPricer.UseCycleDateForAccruals;
      FillFixed(cf, asOf, effective, firstCpn, lastCpn, maturity, ccy, 0.0, effective, cpnDates, cpns, dc, freq, roll, cal,
        principal, amortDates, amorts, accruedPaid, includeDfltDate, defaultAmount, defaultCcy,
        useCycleDateForAccruals, false, false, false, defaultDate, includeMaturityAccrual);
    }


    /// <summary>
    ///   Fill a set of regular floating cashflows.
    /// </summary>
    ///
    /// <param name="cf">Cashflow stream to fill</param>
    /// <param name="asOf">As-of date. Cashflows are generated from this date</param>
    /// <param name="effective">Effective date</param>
    /// <param name="firstCpn">First coupon payment date. Coupons cycle from this date</param>
    /// <param name="lastCpn">Last coupon payment date before maturity</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency of coupon</param>
    /// <param name="fee">Fee paid</param>
    /// <param name="feeSettle">Payment date for fee</param>
    /// <param name="coupon">Original coupon</param>
    /// <param name="cpnSchedule">Coupon schedule</param>
    /// <param name="dc">Daycount of coupon accrual</param>
    /// <param name="freq">Frequency of coupons payments (per year)</param>
    /// <param name="roll">Business day roll convention for date adjustments</param>
    /// <param name="cal">Calendar for payment date adjustments</param>
    /// <param name="referenceCurve">Discount curve of reference rate</param>
    /// <param name="rateResets">Historical reset rate schedule</param>
    /// <param name="principal">%Payment at maturity received</param>
    /// <param name="amortSchedule">Amortization schedule</param>
    /// <param name="accruedPaid">True if accrued coupon paid on default</param>
    /// <param name="includeDfltDate">True if default date included in accrued recived on default</param>
    /// <param name="defaultAmount">Cashflow (one-off) paid on default</param>
    /// <param name="defaultCcy">Currency of default payment</param>
    /// <param name="accrueOnCycle">Accrue on cycle rather than payment dates</param>
    /// <param name="adjustNext">Is true if the next period start is adjusted to the payment date</param>
    /// <param name="adjustLast">Is true if the last period end date is rolled </param>
    /// <param name="eomRule">If true, moves payment date to the end of the month if
    ///   the starting date was the end of the month.</param>
    /// <param name="defaultDate">Date of default or empty date.</param>
    /// <param name="includeMaturityAccrual">Include maturity date in accrual</param>
    ///
    public static Cashflow FillFloat(
      Cashflow cf, Dt asOf, Dt effective, Dt firstCpn, Dt lastCpn, Dt maturity, Currency ccy, double fee,
      Dt feeSettle, double coupon, IList<CouponPeriod> cpnSchedule, DayCount dc, Frequency freq, BDConvention roll,
      Calendar cal, Curve referenceCurve, IList<RateReset> rateResets, double principal,
      IList<Amortization> amortSchedule, bool accruedPaid, bool includeDfltDate, double defaultAmount,
      Currency defaultCcy, bool accrueOnCycle, bool adjustNext, bool adjustLast, bool eomRule,
      Dt defaultDate, bool includeMaturityAccrual
      )
    {
      Dt[] cpnDates;
      double[] cpnAmounts;

      Dt nextCouponDate = ScheduleUtil.NextCouponDate(asOf, effective, firstCpn, freq, eomRule);
      CouponPeriodUtil.FromSchedule(cpnSchedule, nextCouponDate, maturity, coupon, out cpnDates, out cpnAmounts);

      Dt[] amortDates;
      double[] amortAmounts;
      AmortizationUtil.FromSchedule(amortSchedule, out amortDates, out amortAmounts);

      Dt[] resetDates;
      double[] resetRates;
#if USE_SCHEDULE
      RateResetUtil.FromSchedule(rateResets, out resetDates, out resetRates);
#else
      if (rateResets != null && rateResets.Count > 0)
      {
        Schedule sched = new Schedule(effective, effective, firstCpn, maturity, freq, roll, cal);
        int numResets = rateResets.Count;
        resetDates = new Dt[numResets];
        resetRates = new double[numResets];
        int i = 0; // counter through resetDates/Rates
        int j = 0; // counter through schedule

        foreach (RateReset r in rateResets)
        {
          Dt resetDate = r.Date;

          // if reset was captured for a rolled period start then pass to the cashflow model
          // as the unadjusted period start; FillFloat only looks for most recent resets, <= period start
          for (; j < sched.Count; j++)
          {
            Dt periodStart = sched.GetPeriodStart(j);
            Dt adjPeriodStart = Dt.Roll(periodStart, roll, cal);
            if (Dt.Cmp(resetDate, adjPeriodStart) == 0)
            {
              resetDate = periodStart;
              ++j; // start at next period for next rate reset
              break;
            }
            else if (Dt.Cmp(adjPeriodStart, resetDate) > 0)
            {
              break;
            }
          }

          resetDates[i] = resetDate;
          resetRates[i++] = r.Rate;
        }
      }
      else
      {
        resetDates = new Dt[0];
        resetRates = new double[0];
      }
#endif

      if (cf == null)
        cf = new Cashflow(asOf);
      FillFloat(cf, asOf, effective, firstCpn, lastCpn, maturity, ccy, fee, feeSettle, cpnDates, cpnAmounts,
        dc, freq, roll, cal, referenceCurve, resetDates, resetRates, principal, amortDates, amortAmounts,
        accruedPaid, includeDfltDate, defaultAmount, defaultCcy, accrueOnCycle, adjustNext,
        adjustLast, eomRule, defaultDate, includeMaturityAccrual);

      return cf;
    }


    /// <summary>
    ///   Fill a set of regular floating cashflows.
    /// </summary>
    ///
    /// <param name="cf">Cashflow stream to fill</param>
    /// <param name="asOf">As-of date. Cashflows are generated from this date</param>
    /// <param name="effective">Effective date</param>
    /// <param name="firstCpn">First coupon payment date. Coupons cycle from this date</param>
    /// <param name="lastCpn">Last coupon date before maturity</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency of coupon</param>
    /// <param name="fee">Fee paid</param>
    /// <param name="feeSettle">Payment date for fee</param>
    /// <param name="cpnDates">Coupon schedule dates</param>
    /// <param name="cpns">Coupon spreads over reference rate</param>
    /// <param name="dc">Daycount of coupon accrual</param>
    /// <param name="freq">Frequency of coupons payments (per year)</param>
    /// <param name="roll">Business day roll convention for date adjustments</param>
    /// <param name="cal">Calendar for payment date adjustments</param>
    /// <param name="referenceCurve">Discount curve of reference rate</param>
    /// <param name="resetDates">Historical reset rate dates</param>
    /// <param name="resets">Historical coupon resets</param>
    /// <param name="principal">%Payment at maturity received</param>
    /// <param name="amortDates">Amortization schedule dates</param>
    /// <param name="amorts">Amortization amounts as a percentage of principal</param>
    /// <param name="accruedPaid">True if accrued coupon paid on default</param>
    /// <param name="includeDfltDate">True if default date included in accrued recived on default</param>
    /// <param name="defaultAmount">Cashflow (one-off) paid on default</param>
    /// <param name="defaultCcy">Currency of default payment</param>
    /// <param name="accrueOnCycle">Accrue on cycle rather than payment dates</param>
    /// <param name="adjustNext">Is true if the next period start is adjusted to the payment date</param>
    /// <param name="adjustLast">Is true if the last period end date is rolled</param>
    /// <param name="eomRule">If true, moves payment date to the end of the month if
    ///   the starting date was the end of the month.</param>
    /// <param name="defaultDate">Date of default or empty date.</param>
    /// <param name="includeMaturityAccrual">Include maturity date in accrual</param>
    public static void FillFloat(Cashflow cf, Dt asOf, Dt effective, Dt firstCpn, Dt lastCpn, Dt maturity,
                          Currency ccy, double fee, Dt feeSettle,
                          Dt[] cpnDates, double[] cpns,
                          DayCount dc, Frequency freq, BDConvention roll, Calendar cal,
                          Curve referenceCurve, Dt[] resetDates, double[] resets,
                          double principal, Dt[] amortDates, double[] amorts,
                          bool accruedPaid, bool includeDfltDate,
                          double defaultAmount, Currency defaultCcy,
                          bool accrueOnCycle, bool adjustNext, bool adjustLast, bool eomRule, Dt defaultDate,
                          bool includeMaturityAccrual)
  {
    var cc = MakeRule(accruedPaid, includeDfltDate,
      accrueOnCycle, adjustNext, adjustLast, eomRule, includeMaturityAccrual, false);
    FillFloat(cf, null, asOf, effective, firstCpn, lastCpn, maturity, ccy,
      fee, feeSettle, cpnDates, cpns, dc, freq, roll, cal, referenceCurve,
      resetDates, resets, principal, amortDates, amorts,
      defaultAmount, defaultCcy, defaultDate, cc.Key, cc.Value);
  }

    /// <summary>
    ///   Fill a set of regular floating cashflows.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Create a stream of regular floating cashflows with a step-up coupon and amortization
    ///   schedule but no historical reset schedule.</para>
    /// </remarks>
    ///
    /// <param name="cf">Cashflow stream to fill</param>
    /// <param name="asOf">As-of date. Cashflows are generated from this date</param>
    /// <param name="effective">Effective date</param>
    /// <param name="firstCpn">First coupon payment date. Coupons cycle from this date</param>
    /// <param name="lastCpn">Last coupon payment date before maturity</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency of coupon</param>
    /// <param name="fee">Fee paid</param>
    /// <param name="feeSettle">Payment date for fee</param>
    /// <param name="cpn">Current coupon received</param>
    /// <param name="cpnDates">Coupon schedule dates</param>
    /// <param name="cpns">Coupon spreads over reference rate</param>
    /// <param name="dc">Daycount of coupon accrual</param>
    /// <param name="freq">Frequency of coupons payments (per year)</param>
    /// <param name="roll">Business day roll convention for date adjustments</param>
    /// <param name="cal">Calendar for payment date adjustments</param>
    /// <param name="referenceCurve">Discount curve of reference rate</param>
    /// <param name="principal">%Payment at maturity received</param>
    /// <param name="amortDates">Amortization schedule dates</param>
    /// <param name="amorts">Amortization amounts as a percentage of principal</param>
    /// <param name="accruedPaid">True if accrued coupon paid on default</param>
    /// <param name="includeDfltDate">True if default date included in accrued recived on default</param>
    /// <param name="defaultAmount">Cashflow (one-off) paid on default</param>
    /// <param name="defaultCcy">Currency of default payment</param>
    /// <param name="accrueOnCycle">Accrue on cycle rather than payment dates</param>
    /// <param name="adjustNext">Is true if the next period start is adjusted to the payment date</param>
    /// <param name="adjustLast">Is true if the last period end date is rolled</param>
    /// <param name="eomRule">If true, moves payment date to the end of the month if
    ///   the starting date was the end of the month.</param>
    /// <param name="defaultDate">Date of default or empty date.</param>
    /// <param name="includeMaturityAccrual">Include maturity date in accrual</param>
    ///
    /// <example>
    ///   Generate a cashflow schedule for a regular floating coupon bond
    ///   with an assumed recovery rate of 40% of face and accrued.
    ///   <code>
    ///     Dt [] cpnDates = new Dt[1]; cpnDates[0] = maturity;
    ///     double [] cpns[1] = new double[1]; cpns[0] = spread;
    ///     Dt [] amortDates = new Dt[1];
    ///     double [] amorts = new double[];
    ///     Cashflow cf = new Cashflow();
    ///     CashflowFactory.FillFloat(cf,              // Cashflow stream to fill
    ///                               asOfDate,        // Date to generate cashflows as-of
    ///                               effectiveDate,   // Effective date
    ///                               firstCpnDate,    // First coupon date
    ///                               lastCpnDate,     // Last coupon payment date before maturity
    ///                               maturityDate,    // Maturity date
    ///                               currency,        // Currency of coupon
    ///                               coupon,          // Current coupon rate
    ///                               cpnDates,        // Step up coupon schedule dates
    ///                               cpns,            // Step up coupon schduled coupons
    ///                               dayCount,        // Coupon daycount
    ///                               cpnFrequency,    // Coupon frequency (per year)
    ///                               roll,            // Business day roll convention for coupon payments
    ///                               calendar,        // Calendar for coupon payments
    ///                               liborCurve,      // Floating rate reference curve
    ///                               principal,       // Principal amount
    ///                               amortDates,      // Amortization schedule dates
    ///                               amorts,          // Amortization schedule amounts
    ///                               true,            // Accrued coupon is paid on default
    ///                               true,            // Include default date in default accrued
    ///                               0.4,             // Recovery payment
    ///                               currency         // Currency of recovery payment
    ///                               accrueOnCycle,   // Accrue based on cycle date rather than schedule date
    ///                               adjustNext,      // Adjust cycle date based on next payment date
    ///                               adjustLast,      // Adjust the last period end date based on the roll convention
    ///                               eomRule          // Keep payments on end of month
    ///                               );
    ///   </code>
    /// </example>
    public static void FillFloat(Cashflow cf, Dt asOf, Dt effective, Dt firstCpn, Dt lastCpn, Dt maturity,
                          Currency ccy, double fee, Dt feeSettle, double cpn,
                          Dt[] cpnDates, double[] cpns,
                          DayCount dc, Frequency freq, BDConvention roll, Calendar cal,
                          Curve referenceCurve, double principal,
                          Dt[] amortDates, double[] amorts,
                          bool accruedPaid, bool includeDfltDate,
                          double defaultAmount, Currency defaultCcy,
                          bool accrueOnCycle, bool adjustNext, bool adjustLast, bool eomRule, Dt defaultDate,
                          bool includeMaturityAccrual)
    {
      Dt[] resetDates = new Dt[] {effective};
      double[] resets = new double[] {cpn};
      FillFloat(cf, asOf, effective, firstCpn, lastCpn, maturity, ccy, fee, feeSettle,
        cpnDates, cpns, dc, freq, roll, cal, referenceCurve, resetDates, resets,
        principal, amortDates, amorts, accruedPaid, includeDfltDate,
        defaultAmount, defaultCcy, accrueOnCycle, adjustNext, adjustLast,
        eomRule, defaultDate, includeMaturityAccrual);
    }

    /// <summary>
    ///   Fill a set of regular floating cashflows.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Create a stream of regular floating cashflows with a fixed spread and now
    ///   amortization, no step-up coupon or no historical resets.</para>
    /// </remarks>
    ///
    /// <param name="cf">Cashflow stream to fill</param>
    /// <param name="asOf">As-of date. Cashflows are generated from this date</param>
    /// <param name="effective">Effective date</param>
    /// <param name="firstCpn">First coupon payment date. Coupons cycle from this date</param>
    /// <param name="lastCpn">Last coupon payment date before maturity</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency of coupon</param>
    /// <param name="cpn">Current coupon received</param>
    /// <param name="spread">Coupon spread over reference rate for future coupons</param>
    /// <param name="dc">Daycount of coupon accrual</param>
    /// <param name="freq">Frequency of coupons payments (per year)</param>
    /// <param name="roll">Business day roll convention for date adjustments</param>
    /// <param name="cal">Calendar for payment date adjustments</param>
    /// <param name="referenceCurve">Discount curve of reference rate</param>
    /// <param name="principal">%Payment at maturity received</param>
    /// <param name="accruedPaid">True if accrued coupon paid on default</param>
    /// <param name="includeDfltDate">True if default date included in accrued recived on default</param>
    /// <param name="defaultAmount">Cashflow (one-off) paid on default</param>
    /// <param name="defaultCcy">Currency of default payment</param>
    /// <param name="accrueOnCycle">Accrue on cycle rather than payment dates</param>
    /// <param name="adjustNext">Is true if the next period start is adjusted to the payment date</param>
    /// <param name="adjustLast">Is true if the last period end date is rolled</param>
    /// <param name="eomRule">If true, moves payment date to the end of the month if
    ///   the starting date was the end of the month.</param>
    /// <param name="defaultDate">Date of default or empty date.</param>
    /// <param name="includeMaturityAccrual">Include maturity date in accrual</param>
    ///
    /// <example>
    ///   Generate a cashflow schedule for a regular floating coupon bond
    ///   with an assumed recovery rate of 40% of face and accrued.
    ///   <code>
    ///     Cashflow cf = new Cashflow();
    ///     CashflowFactory.FillFloat(cf,              // Cashflow stream to fill
    ///                               asOfDate,        // Date to generate cashflows as-of
    ///                               effectiveDate,   // Effective date
    ///                               firstCpnDate,    // First coupon date
    ///                               lastCpnDate,     // Last coupon date before maturity
    ///                               maturityDate,    // Maturity date
    ///                               currency,        // Currency of coupon
    ///                               coupon,          // Current coupon rate
    ///                               0.005,           // 50bp spread over libor for forward coupons
    ///                               dayCount,        // Coupon daycount
    ///                               cpnFrequency,    // Coupon frequency (per year)
    ///                               roll,            // Business day roll convention for coupon payments
    ///                               calendar,        // Calendar for coupon payments
    ///                               liborCurve,      // Floating rate reference curve
    ///                               principal,       // Principal amount
    ///                               true,            // Accrued coupon is paid on default
    ///                               true,            // Include default date in default accrued
    ///                               0.4,             // Recovery payment
    ///                               currency         // Currency of recovery payment
    ///                               accrueOnCycle,   // Accrue based on cycle date rather than schedule date
    ///                               adjustNext,      // Adjust cycle date based on next payment date
    ///                               adjustLast,      // Adjust the last period end date based on the roll convention
    ///                               eomRule          // Keep payments on end of month
    ///                               );
    ///   </code>
    /// </example>
    public static void FillFloat(Cashflow cf, Dt asOf, Dt effective, Dt firstCpn, Dt lastCpn, Dt maturity,
                          Currency ccy, double cpn, double spread, DayCount dc,
                          Frequency freq, BDConvention roll, Calendar cal,
                          Curve referenceCurve, double principal,
                          bool accruedPaid, bool includeDfltDate,
                          double defaultAmount, Currency defaultCcy,
                          bool accrueOnCycle, bool adjustNext, bool adjustLast, bool eomRule, Dt defaultDate,
                          bool includeMaturityAccrual)
    {
      Dt[] cpnDates = new Dt[] {maturity};
      double[] cpns = new double[] {spread};
      Dt[] resetDates = new Dt[] {effective};
      double[] resets = new double[] {cpn};
      Dt[] amortDates = new Dt[0];
      double[] amorts = new double[0];
      FillFloat(cf, asOf, effective, firstCpn, lastCpn, maturity, ccy, 0.0, asOf,
        cpnDates, cpns, dc, freq, roll, cal, referenceCurve, resetDates, resets,
        principal, amortDates, amorts, accruedPaid, includeDfltDate,
        defaultAmount, defaultCcy,
        accrueOnCycle, adjustNext, adjustLast, eomRule, defaultDate,
        includeMaturityAccrual);
    }

    private static Pair<CycleRule,CashflowFlag> MakeRule(
      bool accruedPaid, bool includeDfltDate,
      bool accrueOnCycle, bool adjustNext, bool adjustLast, bool eomRule,
      bool includeMaturityAccrual, bool simpleProjection)
  {
    CycleRule rule = adjustNext ? CycleRule.FRN : (eomRule ? CycleRule.EOM : CycleRule.None);

    CashflowFlag flags = 0;
    if(accruedPaid) flags |= CashflowFlag.AccruedPaidOnDefault;
    if(includeDfltDate) flags |= CashflowFlag.IncludeDefaultDate;
    if(accrueOnCycle) flags |= CashflowFlag.AccrueOnCycle;
    if(adjustLast) flags |= CashflowFlag.AdjustLast;
    if(includeMaturityAccrual) flags |= CashflowFlag.IncludeMaturityAccrual;
    if(simpleProjection) flags |= CashflowFlag.SimpleProjection;
    if (ToolkitConfigurator.Settings.CashflowPricer.RollLastPaymentDate)
      flags |= CashflowFlag.RollLastPaymentDate;
    return new Pair<CycleRule, CashflowFlag>(rule, flags);
  }
#endif

    #endregion

    #endregion Methods
  } // class CashflowFactory
}
