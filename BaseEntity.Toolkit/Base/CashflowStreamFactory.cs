/*
 * CashflowFactory.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util.Configuration;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows
{
  ///
  /// <summary>
  ///   Helper factory methods for CashflowStream objects
  /// </summary>
  ///
  /// <remarks>
  ///   This class provides constructor and conversion routines for CashflowStream objects.
  /// </remarks>
  ///
  public abstract class CashflowStreamFactory
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CashflowStreamFactory));

    #region Methods
    /// <summary>
    /// 
    /// </summary>
    public delegate double Projector(Dt start, Dt end, double refCoupon);

    /// <summary>
    /// Fills the fixed.
    /// </summary>
    /// <param name="cf">The cf.</param>
    /// <param name="asOf">As of.</param>
    /// <param name="scheduleParams">The schedule params.</param>
    /// <param name="ccy">The ccy.</param>
    /// <param name="dc">The dc.</param>
    /// <param name="coupon">The coupon.</param>
    /// <param name="couponSched">The coupon sched.</param>
    /// <param name="origPrincipal">The orig principal.</param>
    /// <param name="amortSched">The amort sched.</param>
    /// <param name="exchangePrincipal">if set to <c>true</c> [exchange principal].</param>
    /// <param name="fee">The fee.</param>
    /// <param name="feeSettle">The fee settle.</param>
    public static void FillFixed(CashflowStream cf, Dt asOf,
                                 ScheduleParams scheduleParams,
                                 Currency ccy, DayCount dc,
                                 double coupon, IList<CouponPeriod> couponSched,
                                 double origPrincipal, IList<Amortization> amortSched, bool exchangePrincipal,
                                 double fee, Dt feeSettle)
    {
      // Defaults
      if (fee != 0.0 && feeSettle.IsEmpty())
        feeSettle = scheduleParams.AccrualStartDate;

      // Build schedule
      Schedule sched = MakeSchedule(asOf, scheduleParams);
      Fill(cf, sched, asOf, GetFlags(scheduleParams), ccy, dc,
           coupon, couponSched, origPrincipal,amortSched,exchangePrincipal,
           (s, e, c) => c, fee, feeSettle);
    }

    /// <summary>
    /// Fills the float.
    /// </summary>
    /// <param name="cf">The cf.</param>
    /// <param name="asOf">As of.</param>
    /// <param name="scheduleParams">The schedule params.</param>
    /// <param name="ccy">The ccy.</param>
    /// <param name="dc">The dc.</param>
    /// <param name="coupon">The coupon.</param>
    /// <param name="couponSched">The coupon sched.</param>
    /// <param name="origPrincipal">The orig principal.</param>
    /// <param name="amortSched">The amort sched.</param>
    /// <param name="exchangePrincipal">if set to <c>true</c> [exchange principal].</param>
    /// <param name="referenceCurve">The reference curve.</param>
    /// <param name="resetSched">The reset sched.</param>
    /// <param name="fee">The fee.</param>
    /// <param name="feeSettle">The fee settle.</param>
    public static void FillFloat(CashflowStream cf, Dt asOf,
                                 ScheduleParams scheduleParams,
                                 Currency ccy, DayCount dc,
                                 double coupon, IList<CouponPeriod> couponSched,
                                 double origPrincipal, IList<Amortization> amortSched, bool exchangePrincipal,
                                 Curve referenceCurve, IList<RateReset> resetSched,
                                 double fee, Dt feeSettle)
    {
      if (resetSched == null)
        throw new ArgumentOutOfRangeException("resetSched", "Must specify a historical rate reset schedule");

      // Defaults
      if (fee != 0.0 && feeSettle.IsEmpty())
        feeSettle = scheduleParams.AccrualStartDate;

      // Build schedule
      Schedule sched = MakeSchedule(asOf, scheduleParams);
      FloatRateProjector p = new FloatRateProjector(sched,
                                                    scheduleParams, dc, referenceCurve, resetSched);
      Fill(cf, sched, asOf, GetFlags(scheduleParams), ccy, dc,
           coupon, couponSched, origPrincipal, amortSched, exchangePrincipal,
           p.Project, fee, feeSettle);
    }

    /// <summary>
    /// Fills the cashflow stream.
    /// </summary>
    /// <param name="cf">The cf.</param>
    /// <param name="sched">Schedule</param>
    /// <param name="asOf">As of.</param>
    /// <param name="flags">The cashflow flags.</param>
    /// <param name="ccy">The ccy.</param>
    /// <param name="dc">The dc.</param>
    /// <param name="coupon">The coupon.</param>
    /// <param name="couponSched">The coupon sched.</param>
    /// <param name="origPrincipal">The orig principal.</param>
    /// <param name="amortSched">The amort sched.</param>
    /// <param name="exchangePrincipal">if set to <c>true</c> [exchange principal].</param>
    /// <param name="project">The project.</param>
    /// <param name="fee">The fee.</param>
    /// <param name="feeSettle">The fee settle.</param>
    private static void Fill(CashflowStream cf, Schedule sched,
                             Dt asOf, CashflowFlag flags,
                             Currency ccy, DayCount dc,
                             double coupon, IList<CouponPeriod> couponSched,
                             double origPrincipal, IList<Amortization> amortSched, bool exchangePrincipal,
                             Projector project, double fee, Dt feeSettle)
    {
      // Validate
      // Note: Validation of dates is done by Schedule.
      if (dc == DayCount.None)
        throw new ArgumentOutOfRangeException("dc", "No daycount specified, cannot be None");
      if (origPrincipal == 0.0)
        throw new ArgumentOutOfRangeException("origPrincipal", "Must specify non-zero original principal");
      if (project == null)
        project = (s, e, c) => c;
      Debug.Assert(fee == 0.0 || feeSettle.IsValid());

      bool includeMaturityAccrual = (flags & CashflowFlag.IncludeMaturityAccrual) != 0;
      bool accrueOnCycle = (flags & CashflowFlag.AccrueOnCycle) != 0;
      bool backwardCompatible = IsBackwardCompatible(flags);
      bool usePeriodDates = accrueOnCycle || !backwardCompatible;
      bool useConsistentCashflowEffective = ToolkitConfigurator.Settings.CashflowPricer.UseConsistentCashflowEffective;

      // Step over to find the period for accrual
      Dt accrualStart = asOf;
      int firstIdx = 0;
      int schedCount = sched.Count;
      if (schedCount > 0)
      {
        if (useConsistentCashflowEffective)
        {
          for (; firstIdx < schedCount; firstIdx++)
          {
            accrualStart = (accrueOnCycle || firstIdx <= 0) ? sched.GetPeriodStart(firstIdx) : sched.GetPaymentDate(firstIdx - 1);
            if (accrualStart >= asOf)
              break;
          }
          if (firstIdx > 0)
            firstIdx--;

          // Determine accrual start/end dates based on accruedOnCycle
          accrualStart = (accrueOnCycle || firstIdx <= 0) ? sched.GetPeriodStart(firstIdx) : sched.GetPaymentDate(firstIdx - 1);
        }
        else
          accrualStart = sched.GetPeriodStart(0);
      }

      // Initialise Cashflow
      cf.Clear();
      cf.Effective = accrualStart;	// Effective date is current accrual start date
      cf.Currency = ccy;
      cf.DfltCurrency = ccy;
      cf.DayCount = dc;
      cf.AccruedPaidOnDefault = (flags & CashflowFlag.AccruedPaidOnDefault) != 0;
      cf.AccruedIncludingDefaultDate = (flags & CashflowFlag.IncludeDefaultDate) != 0;

      // Get current principal
      double remainingPrincipal = origPrincipal;
      IEnumerator nextAmort = (amortSched != null) ? amortSched.GetEnumerator() : null;
      if (nextAmort != null)
        while (nextAmort.MoveNext() && (((Amortization)nextAmort.Current).Date < asOf))
          remainingPrincipal -= ((Amortization)nextAmort.Current).Amount * origPrincipal;

      // Iterator for current coupon
      IEnumerator nextCpn = (couponSched != null) ? couponSched.GetEnumerator() : null;

      // Add fee.
      if (fee != 0.0 && (asOf <= feeSettle))
      {
        // We don't handle fees after first payment for now
        if (feeSettle >= sched.GetPaymentDate(0))
          throw new ArgumentOutOfRangeException("feeSettle", "Fee Settlement after first payment date");
        cf.Add(feeSettle, fee * remainingPrincipal, 0.0, remainingPrincipal);
        cf.SetUpfrontFee(fee);
        cf.SetFeeSettle(feeSettle);
      }

      // save running coupon to restore back after adding libor for each payment date coupon
      double origCoupon = coupon;

      // Generate payments
      for (int i = firstIdx; i < sched.Count; i++)
      {
        bool isLast = (i == sched.Count - 1);
        Dt date = sched.GetPaymentDate(i);
        Dt start = sched.GetPeriodStart(i);

        // Get current coupon
        if (nextCpn != null)
          while (nextCpn.MoveNext() && (((CouponPeriod)nextCpn.Current).Date <= date))
            coupon = ((CouponPeriod)nextCpn.Current).Coupon;
        coupon = project(start, sched.GetPeriodEnd(i), coupon);

        // Accrued on next payment
        double accruedPayment = (includeMaturityAccrual && isLast ? sched.Fraction(i, dc, usePeriodDates, true)
                                   : sched.Fraction(i, dc, usePeriodDates)) * coupon * remainingPrincipal;

        // Any amortizations between this coupon and the last one
        double principalPayment = 0.0;
        if (nextAmort != null)
          while (nextAmort.MoveNext() && (((Amortization)nextAmort.Current).Date <= date))
            principalPayment += ((Amortization)nextAmort.Current).Amount * origPrincipal;

        // Final remaining principal
        if (isLast)
          principalPayment += remainingPrincipal;

        // Normal case, just add cashflow
        logger.DebugFormat("Adding cashflow date {0}, principal {0}, interest {1}, notional {2}\n", date, exchangePrincipal ? principalPayment : 0.0, accruedPayment, remainingPrincipal);
        cf.Add(date, exchangePrincipal ? principalPayment : 0.0, accruedPayment, remainingPrincipal);

        remainingPrincipal -= principalPayment;

        // reset back to original coupon
        coupon = origCoupon;
      }

      return;
    }

    private static Schedule MakeSchedule(Dt asOf, ScheduleParams scheduleParams)
    {
      bool useConsistentCashflowEffective = ToolkitConfigurator.Settings.CashflowPricer.UseConsistentCashflowEffective;
      CycleRule rule = scheduleParams.CycleRule;
      CashflowFlag flags = GetFlags(scheduleParams);
      bool backwardCompatible = IsBackwardCompatible(flags);
      Dt effective = scheduleParams.AccrualStartDate;
      Dt maturity = scheduleParams.Maturity;
      Schedule sched = (effective == maturity
                          ? new Schedule(useConsistentCashflowEffective ? effective : asOf,
                                         effective, effective, effective,
                                         new Dt[] { effective }, new Dt[] { effective })
                          : new Schedule(useConsistentCashflowEffective ? effective : asOf,
                                         effective, scheduleParams.FirstCouponDate,
                                         scheduleParams.NextToLastCouponDate, maturity,
                                         scheduleParams.Frequency, scheduleParams.Roll,
                                         scheduleParams.Calendar, rule,
                                         backwardCompatible ? FlipFlags(flags, rule) : flags)
                       );
      return sched;
    }

    private static CashflowFlag GetFlags(ScheduleParams scheduleParams)
    {
      if(!ToolkitConfigurator.Settings.CashflowPricer.BackwardCompatibleSchedule)
        return scheduleParams.CashflowFlag | CashflowFlag.RespectLastCoupon;
      return scheduleParams.CashflowFlag;
    }

    private static bool IsBackwardCompatible(CashflowFlag flags)
    {
      return (flags & (CashflowFlag.RespectLastCoupon | CashflowFlag.StubAtEnd)) == 0;
    }

    private class FloatRateProjector
    {
      private Curve referenceCurve_;
      private IList<RateReset> resetSched_;
      private DayCount dc_;
      private Frequency freq_;
      private Calendar cal_;
      private bool firstFuturePeriod_ = true;
      private Schedule schedule_;

      internal FloatRateProjector(Schedule sched, ScheduleParams scheduleParams,
                                  DayCount dc, Curve referenceCurve, IList<RateReset> resetSched)
      {
        referenceCurve_ = referenceCurve;
        resetSched_ = resetSched;
        dc_ = dc;
        freq_ = (scheduleParams.CashflowFlag & CashflowFlag.SimpleProjection)
                != 0 ? Frequency.None : scheduleParams.Frequency;
        cal_ = scheduleParams.Calendar;
        if (!IsBackwardCompatible(scheduleParams.CashflowFlag))
          schedule_ = sched;
      }

      internal double Project(Dt start, Dt end, double refCoupon)
      {
        double coupon = refCoupon;
        if (start < referenceCurve_.AsOf)
        {
          // Historical coupon so look back at coupon resets
          int j = resetSched_.Count - 1;
          while (j > 0 && (start < resetSched_[j].Date))
            j--;
          coupon = resetSched_[j].Rate;
        }
        else
        {
          bool resetFromCurve = true;

          //- If first period after last historic period, take most recent reset
          //- provided it's within 2 business days of the period start date. In 9.1 this method
          //- will receive an input set of explicit reset dates and the 2 day assumption can be 
          //- dropped.

          if (firstFuturePeriod_)
          {
            if (resetSched_.Count != 0)
            {
              int j = resetSched_.Count - 1;
              while (j > 0 && (start < resetSched_[j].Date))
                j--;

              if (Dt.BusinessDays(resetSched_[j].Date, start, cal_) <= 2)
              {
                coupon = resetSched_[j].Rate;
                resetFromCurve = false;
              }
            }
            // prevent this logic firing for subsequent periods
            firstFuturePeriod_ = false;
          }

          if (resetFromCurve)
          {
            // Forward coupon to be set assuming simple compounding for rate
            if (schedule_ != null)
            {
              // Use schedule to calculate the fraction for it has the right cycle info.
              double price = referenceCurve_.Interpolate(end)/referenceCurve_.Interpolate(start);
              double T = schedule_.Fraction(start, end, dc_);
              coupon += RateCalc.RateFromPrice(price, T, freq_);
            }
            else
            {
              // backward compatible mode.
              coupon += referenceCurve_.F(start < referenceCurve_.AsOf ? referenceCurve_.AsOf : start, end, dc_, freq_);
            }
          }
        }
        return coupon;
      }
    } // class Projector

    private static CashflowFlag FlipFlags(CashflowFlag flags, CycleRule rule)
    {
      return rule == CycleRule.FRN
               ? (flags & ~CashflowFlag.AccrueOnCycle)
               : (flags | CashflowFlag.AccrueOnCycle);
    }

    /// <summary>
    ///   Fill a set of regular fixed cashflows.
    /// </summary>
    ///
    /// <param name="cf">Cashflow stream to fill</param>
    /// <param name="asOf">As-of date. Cashflows are generated from this date</param>
    /// <param name="effective">Effective date</param>
    /// <param name="firstCpn">First coupon payment date. Coupons cycle from this date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency of coupon</param>
    /// <param name="fee">Fee paid (percent of original principal)</param>
    /// <param name="feeSettle">Payment date for fee</param>
    /// <param name="coupon">Annualized coupon rate</param>
    /// <param name="dc">Daycount of coupon accrual</param>
    /// <param name="freq">Frequency of coupons payments (per year)</param>
    /// <param name="roll">Business day roll convention for date adjustments</param>
    /// <param name="cal">Calendar for payment date adjustments</param>
    /// <param name="couponSched">Step-up coupon schedule or null</param>
    /// <param name="origPrincipal">Original principal</param>
    /// <param name="amortSched">Amortization schedule or null</param>
    /// <param name="exchangePrincipal">Exchange principal if true</param>
    /// <param name="accruedPaid">True if accrued coupon paid on default</param>
    /// <param name="includeDfltDate">True if default date included in accrued recived on default</param>
    /// <param name="accrueOnCycle">Accrue on cycle rather than payment dates</param>
    /// <param name="adjustNext">Is true if the next period start is adjusted to
    ///   the payment date (usually false)</param>
    /// <param name="adjustLast">Is true if the last payment date is rolled (usually false)</param>
    /// <param name="eomRule">If true, moves payment date to the end of the month if
    ///   the starting date was the end of the month.</param>
    ///  <param name="includeMaturityAccrual">If true, includes the maturity date in accruals.</param>
    ///
    /// <example>
    ///   Generate a cashflow schedule for a regular fixed coupon bond.
    ///   <code>
    ///     Cashflow cf = new Cashflow();
    ///     CashflowFactory.FillFixed(cf,              // Cashflow stream to fill
    ///                               asOfDate,        // Date to generate cashflows as-of
    ///                               effectiveDate,   // Effective date
    ///                               firstCpnDate,    // First coupon date
    ///                               maturityDate,    // Maturity date
    ///                               currency,        // Currency of coupon
    ///                               fee,             // Upfront fee
    ///                               feeSettle,       // Fee settlement (payment) date
    ///                               coupon,          // Coupon rate
    ///                               dayCount,        // Coupon daycount
    ///                               cpnFrequency,    // Coupon frequency (per year)
    ///                               roll,            // Business day roll convention for coupon payments
    ///                               calendar,        // Calendar for coupon payments
    ///                               null,            // No step-up coupon schedule
    ///                               principal,       // Principal amount
    ///                               null,            // No amortizations
    ///                               true,            // Exchange principals
    ///                               false,           // Accrued not paid on default
    ///                               false,           // Default date not included in accrued
    ///                               false,           // Accrue on payment dates
    ///                               false,           // Dont adjust cycle dates based on payment dates
    ///                               false            // Dont use EOM rule
    ///                               );
    ///   </code>
    /// </example>
    /// <example>
    ///   Generate a cashflow schedule for a CDS
    ///   <code>
    ///     Cashflow cf = new Cashflow();
    ///     CashflowFactory.FillFixed(cf,              // Cashflow stream to fill
    ///                               asOfDate,        // Date to generate cashflows as-of
    ///                               effectiveDate,   // Effective date
    ///                               firstCpnDate,    // First coupon date
    ///                               maturityDate,    // Maturity date
    ///                               currency,        // Currency of coupon
    ///                               fee,             // Upfront fee
    ///                               feeSettle,       // Fee settlement (payment) date
    ///                               coupon,          // Coupon rate
    ///                               dayCount,        // Coupon daycount
    ///                               cpnFrequency,    // Coupon frequency (per year)
    ///                               roll,            // Business day roll convention for coupon payments
    ///                               calendar,        // Calendar for coupon payments
    ///                               null,            // No step-up coupon schedule
    ///                               principal,       // Principal amount
    ///                               null,            // No amortizations
    ///                               false,           // Dont exchange principals
    ///                               true,            // Accrued paid on default
    ///                               true,            // Default date included in accrued
    ///                               false,           // Accrue on payment dates
    ///                               false,           // Dont adjust next accrual date based on payment date
    ///                               false,           // Dont adjust last accrual date based on payment date
    ///                               false            // Dont use EOM rule
    ///                               );
    ///   </code>
    /// </example>
    ///
    [Obsolete]
    public static void FillFixed(
      CashflowStream cf, Dt asOf, Dt effective, Dt firstCpn, Dt maturity,
      Currency ccy, double fee, Dt feeSettle,
      double coupon, DayCount dc, Frequency freq, BDConvention roll, Calendar cal,
      IList<CouponPeriod> couponSched,
      double origPrincipal,
      IList<Amortization> amortSched,
      bool exchangePrincipal, bool accruedPaid, bool includeDfltDate,
      bool accrueOnCycle, bool adjustNext, bool adjustLast, bool eomRule,
      bool includeMaturityAccrual)
    {
      CashflowFlag flags = Schedule.ToCashflowFlag(
        accrueOnCycle, adjustNext, adjustLast, eomRule);
      if (!ToolkitConfigurator.Settings.CashflowPricer.BackwardCompatibleSchedule)
        flags |= CashflowFlag.RespectLastCoupon;
      if (includeMaturityAccrual) flags |= CashflowFlag.IncludeMaturityAccrual;
      if (accruedPaid) flags |= CashflowFlag.AccruedPaidOnDefault;
      if (includeDfltDate) flags |= CashflowFlag.IncludeDefaultDate;
      CycleRule rule = Schedule.MakeRule(adjustNext, eomRule);
      ScheduleParams scheduleParams = new ScheduleParams(effective, firstCpn,
                                                         Dt.Empty, maturity, freq, roll, cal, rule, flags);
      FillFixed(cf, asOf, scheduleParams, ccy, dc, coupon, couponSched,
                origPrincipal, amortSched, exchangePrincipal, fee, feeSettle);
    }

    /// <summary>
    ///   Fill a set of regular fixed cashflows.
    /// </summary>
    ///
    /// <param name="cf">Cashflow stream to fill</param>
    /// <param name="asOf">As-of date. Cashflows are generated from this date</param>
    /// <param name="effective">Effective date</param>
    /// <param name="firstCpn">First coupon payment date. Coupons cycle from this date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency of coupon</param>
    /// <param name="fee">Fee paid (percent of original principal)</param>
    /// <param name="feeSettle">Payment date for fee</param>
    /// <param name="coupon">Annualized coupon rate</param>
    /// <param name="dc">Daycount of coupon accrual</param>
    /// <param name="freq">Frequency of coupons payments (per year)</param>
    /// <param name="roll">Business day roll convention for date adjustments</param>
    /// <param name="cal">Calendar for payment date adjustments</param>
    /// <param name="referenceCurve">Reference curve for floating leg</param>
    /// <param name="resetSched">Reset dates/rates</param>
    /// <param name="couponSched">Step-up coupon schedule or null</param>
    /// <param name="origPrincipal">Original principal</param>
    /// <param name="amortSched">Amortization schedule or null</param>
    /// <param name="exchangePrincipal">Exchange principal if true</param>
    /// <param name="accruedPaid">True if accrued coupon paid on default</param>
    /// <param name="includeDfltDate">True if default date included in accrued recived on default</param>
    /// <param name="accrueOnCycle">Accrue on cycle rather than payment dates</param>
    /// <param name="adjustNext">Is true if the next period start is adjusted to
    ///   the payment date (usually false)</param>
    /// <param name="adjustLast">Is true if the last payment date is rolled (usually false)</param>
    /// <param name="eomRule">If true, moves payment date to the end of the month if
    ///   the starting date was the end of the month.</param>
    ///  <param name="includeMaturityAccrual">If true, includes the maturity date in accruals.</param>
    ///
    [Obsolete]
    public static void FillFloat(
      CashflowStream cf, Dt asOf, Dt effective, Dt firstCpn, Dt maturity,
      Currency ccy, double fee, Dt feeSettle,
      double coupon, DayCount dc, Frequency freq, BDConvention roll, Calendar cal,
      DiscountCurve referenceCurve, IList<RateReset> resetSched, IList<CouponPeriod> couponSched,
      double origPrincipal, IList<Amortization> amortSched, bool exchangePrincipal,
      bool accruedPaid, bool includeDfltDate,
      bool accrueOnCycle, bool adjustNext, bool adjustLast, bool eomRule,
      bool includeMaturityAccrual)
    {
      CashflowFlag flags = Schedule.ToCashflowFlag(
        accrueOnCycle, adjustNext, adjustLast, eomRule);
      if (!ToolkitConfigurator.Settings.CashflowPricer.BackwardCompatibleSchedule)
        flags |= CashflowFlag.RespectLastCoupon;
      if (includeMaturityAccrual) flags |= CashflowFlag.IncludeMaturityAccrual;
      if (accruedPaid) flags |= CashflowFlag.AccruedPaidOnDefault;
      if (includeDfltDate) flags |= CashflowFlag.IncludeDefaultDate;
      CycleRule rule = Schedule.MakeRule(adjustNext, eomRule);
      ScheduleParams scheduleParams = new ScheduleParams(effective, firstCpn,
                                                         Dt.Empty, maturity, freq, roll, cal, rule, flags);
      FillFloat(cf, asOf, scheduleParams, ccy, dc, coupon, couponSched,
                origPrincipal, amortSched, exchangePrincipal,
                referenceCurve, resetSched, fee, feeSettle);
    }

    #endregion Methods

  }
}