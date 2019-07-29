using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Native;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Schedule of regular interest accrual periods
  /// </summary>
  /// <remarks>
  ///   <para>The Schedule class implements a schedule of regular interest accrual periods
  ///   typically used to represent the coupon or payment schedule for a fixed income
  ///   security such as a bond or CDS.</para>
  ///   <para>All accrual periods with payment dates AFTER the pricing asOf date are included.
  ///   Usually users of the schedule such as pricing will only include payments after any
  ///   settlement date but it is the responsibility of the calling function to do this to
  ///   allow flexibility re treatment of cashflows on the settlement date.</para>
  ///   <para>The selection of which dates to include is designed to be consistent with
  ///   common accrual conventions. For example, for settlement on a bond coupon date,
  ///   the next coupon would be the one after the settlement date rather than the one
  ///   on the settlement date.</para>
  ///   <para>Short and long first and last coupon periods are also supported.</para>
  ///   <para>Conceptually the schedule generates a stream of cycle, accrual, and payment dates.</para>
  ///   <list type="bullet">
  ///     <item><description>Cycle dates are the regular coupon accrual dates used to calculate
  ///     accrued interest for odd coupon periods. For example for a short first coupon, the cycle
  ///     start date would be the pseudo coupon date before the effective date.</description></item>
  ///     <item><description>Accrual dates are the interest accrual dates</description></item>
  ///     <item><description>Payment dates are the dates the coupon/premium is paid based on the
  ///     specified calendar and business day convention</description></item>
  ///   </list>
  /// </remarks>
  /// <example>
  ///   <code>
  ///    // Set up parameters for schedule
  ///    Dt asOf = new Dt(1,1,2002);                  // As-of date
  ///    Dt effective = new Dt(1,1,2000);             // Effective (first accrual) date
  ///    Dt firstCpn = new Dt(1,7,2000);              // First coupon payment date (optional
  ///    Dt lastCpn = new Dt(1,1,2004);               // Last coupon payment date (optional)
  ///    Dt maturity = new Dt(1,1,2004);              // Maturity date
  ///    Frequency freq = Frequency.SemiAnnual;       // Semi-annual coupon payments
  ///    Calendar cal = Calendar.NYB;                 // New York Banking calendar
  ///    BDConvention roll = BDConvention.Following;  // Modified following business day convention
  ///    bool adjustNext = false;                     // Don't adjust schedule date based on payment date
  ///    bool eomRule = false;                        // Don't use eom rule
  ///
  ///    // Create a new schedule
  ///    Schedule sched = new Schedule(asOf, firstAccrual, firstCpn, lastCpn, maturity,
  ///                                  freq, cal, roll, adjustNext, eomRule);
  ///
  ///    // Print out resulting schedule
  ///    Console.WriteLine(" Start\tEnd\tPayment");
  ///    for (int i = 0; i &lt; sched.Count; i++)
  ///    {
  ///      Console.WriteLine(" {0:D8}\t{1:D8}\{2:D8}",
  ///         sched.GetPeriodStart(i), // Period (accrual) start date
  ///         sched.GetPeriodEnd(i),   // Period (accrual) end date
  ///         sched.GetCycleStart(i),  // Period cycle start date (same as period start except for short or long period)
  ///         sched.GetCycleEnd(i),    // Peroid cycle end date (same as period start except for short or long period)
  ///         sched.GetPaymentDate(i)  // Period paymend date
  ///      );
  ///    }
  ///  </code>
  /// </example>
  [Serializable]
  public sealed class Schedule : Native.Schedule
    , INativeObject, ISchedule, IScheduleParams
  {
    #region Constructors

    /// <summary>
    ///   Construct an accrual schedule based on a regular cycle.
    ///   Typically this is used for creating coupon and payment streams
    ///   for fixed income securities.
    /// </summary>
    /// <remarks>
    ///   <para>All accrual periods with payment dates AFTER the pricing asOf date are included.
    ///   Usually users of the schedule such as pricing will only include payments after any
    ///   settlement date.</para>
    ///   <para>If no first coupon date is specified, the first coupon date is determined
    ///   by stepping backward from the last coupon date (or maturity if no last coupon date is
    ///   specified).</para>
    ///   <para>If no last coupon date is specified, the last coupon date is determined by
    ///   stepping forward from the first coupon date. If no first or last coupon is specified
    ///   the last coupon date is assumed to be the maturity date.</para>
    ///   <para>If cycle dates are based on the payment dates (<paramref name="adjustNext"></paramref> is true),
    ///   the first coupon date must be specified.</para>
    /// </remarks>
    /// <param name="asOf">As-of date</param>
    /// <param name="effective">Effective (first accrual) date of security</param>
    /// <param name="firstCpnDate">First coupon payment date or empty date for default</param>
    /// <param name="lastCpnDate">Last coupon payment date or empty date for default</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="freq">Payment frequency of coupons (None for pay at maturity)</param>
    /// <param name="bdc">Business day roll convention for date adjustments</param>
    /// <param name="cal">Calendar for payment date adjustments</param>
    /// <param name="rule">Cycle rule</param>
    /// <param name="flags">Flags controlling schedule generation.</param>
    public Schedule(Dt asOf, Dt effective, Dt firstCpnDate, Dt lastCpnDate,
      Dt maturity, Frequency freq, BDConvention bdc, Calendar cal,
      CycleRule rule, CashflowFlag flags)
      : base(asOf, effective, firstCpnDate, lastCpnDate,maturity, freq,bdc,cal,rule,flags)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Schedule"/> class.
    /// </summary>
    /// <param name="scheduleParams">The schedule parameters.</param>
    public Schedule(IScheduleParams scheduleParams)
      : base(scheduleParams.AccrualStartDate, scheduleParams.AccrualStartDate,
        scheduleParams.FirstCouponDate, scheduleParams.NextToLastCouponDate,
        scheduleParams.Maturity, scheduleParams.Frequency, scheduleParams.Roll,
        scheduleParams.Calendar, scheduleParams.CycleRule, scheduleParams.CashflowFlag)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Schedule"/> class.
    /// </summary>
    /// <param name="settle">The settle date.</param>
    /// <param name="scheduleParams">The schedule parameters.</param>
    public Schedule(Dt settle, IScheduleParams scheduleParams)
      : base(settle, scheduleParams.AccrualStartDate,
        scheduleParams.FirstCouponDate, scheduleParams.NextToLastCouponDate,
        scheduleParams.Maturity, scheduleParams.Frequency, scheduleParams.Roll,
        scheduleParams.Calendar, scheduleParams.CycleRule, scheduleParams.CashflowFlag)
    {
    }

    /// <summary>
    ///   Construct a schedule based on a regular cycle.
    ///   Typically this is used for creating coupon and payment streams
    ///   for fixed income securities.
    /// </summary>
    /// <remarks>
    ///   <para>All accrual periods with payment dates AFTER the pricing asOf date are included.
    ///   Usually users of the schedule such as pricing will only include payments after any
    ///   settlement date.</para>
    ///   <para>If no first coupon date is specified, the first coupon date is determined
    ///   by stepping backward from the last coupon date (or maturity if no last coupon date is
    ///   specified).</para>
    ///   <para>If cycle dates are based on the payment dates, the first coupon date must be specified.</para>
    /// </remarks>
    /// <param name="asOf">As-of date</param>
    /// <param name="effective">Effective (first accrual) date of security</param>
    /// <param name="firstCpnDate">First coupon payment date or empty date for default</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="freq">Payment frequency of coupons (None for pay at maturity)</param>
    /// <param name="bdc">Business day roll convention for date adjustments</param>
    /// <param name="cal">Calendar for payment date adjustments</param>
    public Schedule(Dt asOf, Dt effective, Dt firstCpnDate, Dt maturity,
      Frequency freq, BDConvention bdc, Calendar cal)
      : base(asOf, effective, firstCpnDate, Dt.Empty, maturity, freq, bdc, cal,
        CycleRule.None, ToCashflowFlag(true, false, false, false, true))
    { }

    /// <summary>
    ///   Construct a accrual period schedule based on a regular cycle
    /// </summary>
    /// <remarks>
    ///   <para>All accrual periods with payment dates AFTER the pricing asOf date are included.
    ///   Usually users of the schedule such as pricing will only include payments after any
    ///   settlement date.</para>
    ///   <para>If no first coupon date is specified, the first coupon date is determined
    ///   by stepping backward from the last coupon date (or maturity if no last coupon date is
    ///   specified).</para>
    ///   <para>If no last coupon date is specified, the last coupon date is determined by
    ///   stepping forward from the first coupon date. If no first or last coupon is specified
    ///   the last coupon date is assumed to be the maturity date.</para>
    ///   <para>If cycle dates are based on the payment dates (<paramref name="adjustNext"></paramref> is true),
    ///   the first coupon date must be specified.</para>
    /// </remarks>
    /// <param name="asOf">As-of date</param>
    /// <param name="effective">Effective (first accrual) date of security</param>
    /// <param name="firstCpnDate">First coupon payment date or empty date for default</param>
    /// <param name="lastCpnDate">Last coupon payment date or empty date for default</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="freq">Payment frequency of coupons (or None for pay at maturity)</param>
    /// <param name="bdc">Business day roll convention for date adjustments</param>
    /// <param name="cal">Calendar for payment date adjustments</param>
    /// <param name="adjustNext">The period cycle dates are based of the payment dates</param>
    /// <param name="eomRule">If true, moves payment date to the end of the month if
    ///   the starting date was the end of the month.</param>
    public Schedule(Dt asOf, Dt effective, Dt firstCpnDate, Dt lastCpnDate, Dt maturity,
      Frequency freq, BDConvention bdc, Calendar cal, bool adjustNext, bool eomRule)
      : base(asOf, effective, firstCpnDate, lastCpnDate, maturity, freq, bdc, cal,
        MakeRule(adjustNext, eomRule),
        ToCashflowFlag(!adjustNext, adjustNext, adjustNext, eomRule, true))
    { }

    /// <summary>
    ///   Construct an accrual schedule based on a regular cycle.
    ///   Typically this is used for creating coupon and payment streams
    ///   for fixed income securities.
    /// </summary>
    /// <remarks>
    ///   <para>All accrual periods with payment dates AFTER the pricing asOf date are included.
    ///   Usually users of the schedule such as pricing will only include payments after any
    ///   settlement date.</para>
    ///   <para>If no first coupon date is specified, the first coupon date is determined
    ///   by stepping backward from the last coupon date (or maturity if no last coupon date is
    ///   specified).</para>
    ///   <para>If no last coupon date is specified, the last coupon date is determined by
    ///   stepping forward from the first coupon date. If no first or last coupon is specified
    ///   the last coupon date is assumed to be the maturity date.</para>
    ///   <para>If cycle dates are based on the payment dates (<paramref name="adjustNext"></paramref> is true),
    ///   the first coupon date must be specified.</para>
    /// </remarks>
    /// <param name="asOf">As-of date</param>
    /// <param name="effective">Effective (first accrual) date of security</param>
    /// <param name="firstCpnDate">First coupon payment date or empty date for default</param>
    /// <param name="lastCpnDate">Last coupon payment date or empty date for default</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="freq">Payment frequency of coupons (None for pay at maturity)</param>
    /// <param name="bdc">Business day roll convention for date adjustments</param>
    /// <param name="cal">Calendar for payment date adjustments</param>
    /// <param name="adjustNext">The period cycle dates are based off the payment dates</param>
    /// <param name="adjustLast">The last period end date is rolled</param>
    /// <param name="eomRule">If true, moves payment date to the end of the month if
    ///   the starting date was the end of the month.</param>
    public Schedule(Dt asOf, Dt effective, Dt firstCpnDate, Dt lastCpnDate, Dt maturity,
      Frequency freq, BDConvention bdc, Calendar cal,
      bool adjustNext, bool adjustLast, bool eomRule)
      : base(asOf, effective, firstCpnDate, lastCpnDate, maturity, freq, bdc, cal,
        MakeRule(adjustNext, eomRule),
        ToCashflowFlag(!adjustNext, adjustNext, adjustLast, eomRule))
    {
    }

    /// <summary>
    ///   Construct an accrual schedule based on a regular cycle.
    ///   Typically this is used for creating coupon and payment streams
    ///   for fixed income securities.
    /// </summary>
    /// <remarks>
    ///   <para>All accrual periods with payment dates AFTER the pricing asOf date are included.
    ///   Usually users of the schedule such as pricing will only include payments after any
    ///   settlement date.</para>
    ///   <para>If no first coupon date is specified, the first coupon date is determined
    ///   by stepping backward from the last coupon date (or maturity if no last coupon date is
    ///   specified).</para>
    ///   <para>If no last coupon date is specified, the last coupon date is determined by
    ///   stepping forward from the first coupon date. If no first or last coupon is specified
    ///   the last coupon date is assumed to be the maturity date.</para>
    ///   <para>If cycle dates are based on the payment dates (<paramref name="adjustNext"></paramref> is true),
    ///   the first coupon date must be specified.</para>
    /// </remarks>
    /// <param name="asOf">As-of date</param>
    /// <param name="effective">Effective (first accrual) date of security</param>
    /// <param name="firstCpnDate">First coupon payment date or empty date for default</param>
    /// <param name="lastCpnDate">Last coupon payment date or empty date for default</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="freq">Payment frequency of coupons (None for pay at maturity)</param>
    /// <param name="bdc">Business day roll convention for date adjustments</param>
    /// <param name="cal">Calendar for payment date adjustments</param>
    /// <param name="accrueOnCycle">Accrue on the cycle dates rather than the payment dates</param>
    /// <param name="adjustNext">Unused parameter.</param>
    /// <param name="adjustLast">The last period end date is rolled</param>
    /// <param name="eomRule">If true, moves payment date to the end of the month if
    ///   the starting date was the end of the month.</param>
    public Schedule(Dt asOf, Dt effective, Dt firstCpnDate, Dt lastCpnDate, Dt maturity,
      Frequency freq, BDConvention bdc, Calendar cal,
      bool accrueOnCycle, bool adjustNext, bool adjustLast, bool eomRule)
      : base(asOf, effective, firstCpnDate, lastCpnDate, maturity, freq, bdc, cal,
        MakeRule(adjustNext, eomRule),
        ToCashflowFlag(accrueOnCycle, adjustNext, adjustLast, eomRule))
    {
    }

    /// <summary>
    ///   Construct a schedule directly from user supplied dates
    /// </summary>
    /// <param name="asOf">As-of date</param>
    /// <param name="firstCycleDate">First period start date</param>
    /// <param name="firstAccrualDate">First accrual date</param>
    /// <param name="lastCycleDate">Last accrual date</param>
    /// <param name="periodDate">Array of payment period ending dates</param>
    /// <param name="paymentDate">Array of period's payment dates</param>
    public Schedule(Dt asOf, Dt firstCycleDate, Dt firstAccrualDate, Dt lastCycleDate,
      Dt[] periodDate, Dt[] paymentDate)
      : base(asOf, Frequency.None, BDConvention.None, Calendar.None,
        CycleRule.None, CashflowFlag.None, Dt.Empty, periodDate.Length)
    {
      int last = periodDate.Length - 1;
      if (last < 0) return;
      PeriodSetter set = GetPeriodSetter(HandleRef);
      if (last == 0)
      {
        set(0, firstAccrualDate, periodDate[0],
          firstCycleDate, lastCycleDate, paymentDate[0]);
      }
      else
      {
        set(0, firstAccrualDate, periodDate[0],
          firstCycleDate, periodDate[0], paymentDate[0]);
      }
      for (int i = 1; i < last; ++i)
      {
        set(i, periodDate[i - 1], periodDate[i],
          periodDate[i - 1], periodDate[i], paymentDate[i]);
      }
      set(last, periodDate[last - 1], periodDate[last],
        periodDate[last - 1], lastCycleDate, paymentDate[last]);
    }
    #endregion Constructors

    #region Methods
    /// <summary>
    ///   Gets the coupon payment date immediately after the specified date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The next coupon date, or empty if no such date..</returns>
    public Dt GetNextPaymentDate(Dt date)
    {
      int idx = GetNextPaymentIndex(date);
      return idx == -1 ? Dt.Empty : GetPeriodEnd(idx);
    }

    /// <summary>
    ///   Gets the coupon date immediately after the specified date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The next coupon date, or empty if no such date..</returns>
    public Dt GetNextCouponDate(Dt date)
    {
      int idx = GetNextCouponIndex(date);
      return idx == -1 ? Dt.Empty : GetPeriodEnd(idx);
    }

    /// <summary>
    /// Gets the coupon date immediately before the specified date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The previous coupon date, or empty of no such date found.</returns>
    public Dt GetPrevCouponDate(Dt date)
    {
      int idx = GetPrevCouponIndex(date);
      return idx == -1 ? Dt.Empty : GetPeriodEnd(idx);
    }

    /// <summary>
    ///  Gets the numbers the of coupon periods starting from the specified date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <returns>The numbers the of coupon periods remaining.</returns>
    public int NumberOfCouponsRemaining(Dt date)
    {
      int idx = GetNextCouponIndex(date);
      return idx == -1 ? 0 : (Count - idx);
    }

    /// <summary>
    /// Produces data table representation of this schedule
    /// </summary>
    public DataTable ToDataTable()
    {
      // See, for example, TestSchedule.dumpSched
      var dataTable = new DataTable("Schedule table");
      dataTable.Columns.Add(new DataColumn("Index", typeof(int)));
      dataTable.Columns.Add(new DataColumn("Period Start", typeof(Dt)));
      dataTable.Columns.Add(new DataColumn("Period End", typeof(Dt)));
      dataTable.Columns.Add(new DataColumn("Payment Date", typeof(Dt)));

      for (int i = 0; i < Count; i++)
      {
        DataRow row = dataTable.NewRow();
        row[0] = i;
        row[1] = GetPeriodStart(i);
        row[2] = GetPeriodEnd(i);
        row[3] = GetPaymentDate(i);
        dataTable.Rows.Add(row);
      }
      return dataTable;
    }

    #endregion Methods

    #region Backward compatible methods
    /// <summary>
    ///  The backward compatible function to get the default first coupon date.
    ///  In the new mode it simply returns an empty date.
    /// </summary>
    /// <param name="effective">The effective.</param>
    /// <param name="freq">The freq.</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="eomRule">if set to <c>true</c> [eom rule].</param>
    /// <returns></returns>
    [Obsolete("Use the Schedule property on the product to find the first coupon.")]
    public static Dt DefaultFirstCouponDate(Dt effective, Frequency freq, Dt maturity, bool eomRule)
    {
      if (!ToolkitConfigurator.Settings.CashflowPricer.BackwardCompatibleSchedule)
        return Dt.Empty;

      Dt firstCouponDate;
      if( freq == Frequency.None )
        firstCouponDate = maturity;
      else
      {
        // Calculate first coupon date from maturity date
        Dt nd = maturity;
        int nPeriods = 0;
        do {
          firstCouponDate = nd;
          nd = Dt.Add( maturity, freq, --nPeriods, eomRule);
        } while(nd > effective);
      }
      return firstCouponDate;
    }

    /// <summary>
    ///  The backward compatible function to get the default last coupon date.
    ///  In the new mode it simply returns an empty date.
    /// </summary>
    /// <param name="firstCouponDate">The first coupon date.</param>
    /// <param name="freq">The freq.</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="eomRule">if set to <c>true</c> [eom rule].</param>
    /// <returns></returns>
    [Obsolete("Use the Schedule property on the product to find the last coupon.")]
    public static Dt DefaultLastCouponDate(Dt firstCouponDate, Frequency freq, Dt maturity, bool eomRule)
    {
      if (!ToolkitConfigurator.Settings.CashflowPricer.BackwardCompatibleSchedule)
        return Dt.Empty;

      Dt lastCouponDate;
      if (freq == Frequency.None || firstCouponDate.IsEmpty())
        lastCouponDate = maturity;
      else
      {
        // Calculate last coupon date from first coupon date
        Dt ld = firstCouponDate;
        int nPeriods = 0;
        do
        {
          lastCouponDate = ld;
          ld = Dt.Add(firstCouponDate, freq, ++nPeriods, eomRule);
        } while (ld <= maturity);
      }
      return lastCouponDate;
    }

    private static bool NotBackwardCompatible(IScheduleParams sp)
    {
      return (sp.CashflowFlag & (CashflowFlag.StubAtEnd
        | CashflowFlag.RespectLastCoupon)) != 0;
    }

    private static bool PeriodAdjustment(IScheduleParams sp)
    {
      return (sp.CashflowFlag & CashflowFlag.AccrueOnCycle) == 0;
    }

    private static bool IsEomRule(IScheduleParams sp)
    {
      return sp.CycleRule == CycleRule.EOM;
    }

    [Obsolete]
    public static Schedule CreateSchedule(IScheduleParams sp, Dt settle, bool wantCpnDate)
    {
      if (NotBackwardCompatible(sp))
      {
        return new Schedule(settle, sp);
      }
      else if (wantCpnDate && PeriodAdjustment(sp))
      {
        // case: backward compatible, with "period adjustment" true
        // we judged there were too many inconsistent usages in the 9.3 code base so decided 
        // to use the correct, 9.4 recommended, schedule for this case. Any changes compared to
        // 9.3 should be fixes to bugs (improvements).
        // in detail: inconsistent (or just plain over-complicated) uses of !accrueOnCycle, adjustNext and adjustLast
        return new Schedule(settle, sp);
      }
      else
      {
        // case: backward compatible and either:
        // (i) don't "wantCpnDate" => are looking for the cycle dates with the old logic
        // (ii) period adjustment false => are likewise looking for cycle dates with the old logic
        return new Schedule(settle, sp.AccrualStartDate, sp.FirstCouponDate,
                            sp.NextToLastCouponDate, sp.Maturity, sp.Frequency,
                            BDConvention.None, Calendar.None, false, IsEomRule(sp));
      }
    }

    /// <summary>
    /// Create a Schedule instance for the specified ScheduleParams that can be passed to CashflowFactory
    /// </summary>
    /// <remarks>
    /// <para>If the backwards compatible setting is enabled, and if FRN convention
    /// does not apply, then this schedule will differ from the regular one.</para>
    /// <para>Currently this method assume StubAtStart (backwards generation) 
    /// so this should not be used by code that requires StubAtEnd.</para>
    /// </remarks>
    /// <param name="sp"></param>
    public static Schedule CreateScheduleForCashflowFactory(IScheduleParams sp)
    {
      var cashflowFlag = sp.CashflowFlag;
      if (ToolkitConfigurator.Settings.CashflowPricer.BackwardCompatibleSchedule)
      {
        if (sp.Roll != BDConvention.FRN)
          cashflowFlag |= CashflowFlag.AccrueOnCycle;
      }
      else
      {
        cashflowFlag |= CashflowFlag.RespectLastCoupon;
      }

      return new Schedule(sp.AccrualStartDate, sp.AccrualStartDate, sp.FirstCouponDate, sp.NextToLastCouponDate,
                          sp.Maturity, sp.Frequency == Frequency.Continuous ? Frequency.None : sp.Frequency,
                          sp.Roll, sp.Calendar, sp.CycleRule, cashflowFlag);
    }

    [Obsolete("Use the Schedule property on the product to find the next coupon.")]
    public static Dt NextCouponDate(IScheduleParams sp, Dt settle)
    {
      return CreateSchedule(sp, settle, true).GetNextCouponDate(settle);
    }

    [Obsolete("Use the Schedule property on the product to find the next cycle.")]
    public static Dt NextCycleDate(IScheduleParams sp, Dt settle)
    {
      if (NotBackwardCompatible(sp))
      {
        Schedule schedule = new Schedule(sp);
        int idx = schedule.GetNextCouponIndex(settle);
        return idx == -1 ? Dt.Empty : schedule.GetCycleEnd(idx);
      }
      return CreateSchedule(sp, settle, false).GetNextCouponDate(settle);
    }

    [Obsolete("Use the Schedule property on the product to find the previous coupon.")]
    public static Dt PreviousCouponDate(IScheduleParams sp, Dt settle)
    {
      return CreateSchedule(sp, settle, true).GetPrevCouponDate(settle);
    }

    [Obsolete("Use the Schedule property on the product to find the remaining coupons.")]
    public static int RemainingCoupons(IScheduleParams sp, Dt settle)
    {
      if (NotBackwardCompatible(sp))
        return new Schedule(sp).NumberOfCouponsRemaining(settle);

      // reproduce the old result.
      bool isFrn = sp.CycleRule == CycleRule.FRN;
      BDConvention bdc = isFrn ? sp.Roll : BDConvention.None;
      Calendar cal = isFrn ? sp.Calendar : Calendar.None;
      Dt nextCpn = NextCouponDate(sp, settle);
      Dt lastCpn = sp.NextToLastCouponDate > nextCpn || sp.NextToLastCouponDate.IsEmpty()
        ? sp.NextToLastCouponDate : nextCpn;
      Dt effective = nextCpn > settle ? settle : sp.AccrualStartDate;
      return new Schedule(settle, effective, nextCpn, lastCpn,
        sp.Maturity,sp.Frequency, bdc, cal, isFrn, IsEomRule(sp)).Count;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sp"></param>
    /// <param name="settle"></param>
    /// <returns></returns>
    [Obsolete("Use the Schedule property on the product to find the next payment.")]
    public static Dt NextPaymentDate(IScheduleParams sp, Dt settle)
    {
      Dt date = new Schedule(settle, sp).GetNextPaymentDate(settle);
      return date.IsEmpty() ? sp.Maturity : date;
    }

    #endregion Backward compatible methods

    #region Serialization

    private delegate void PeriodSetter(
      int index, Dt accrualBegin, Dt accrualEnd,
      Dt cycleBegin, Dt cycleEnd, Dt payment);

    private static unsafe PeriodSetter GetPeriodSetter(HandleRef handleRef)
    {
      CouponPeriod* ptr = null;
      int count = BaseEntityPINVOKE.Schedule_Size(handleRef);
      if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
      if (count > 0)
      {
        IntPtr handle = BaseEntityPINVOKE.Schedule_getPeriodArray(handleRef);
        if (BaseEntityPINVOKE.SWIGPendingException.Pending)
          throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
        ptr = (CouponPeriod*)handle.ToPointer();
      }
      return delegate(int index, Dt accrualBegin, Dt accrualEnd,
                      Dt cycleBegin, Dt cycleEnd, Dt payment)
      {
        Debug.Assert(index >= 0 && index < count);
        ptr[index].AccrualBegin = accrualBegin;
        ptr[index].AccrualEnd = accrualEnd;
        ptr[index].CycleBegin = cycleBegin;
        ptr[index].CycleEnd = cycleEnd;
        ptr[index].Payment = payment;
      };
    }

    ///<exclude/>
    [System.Security.Permissions.SecurityPermission(System.Security.Permissions.SecurityAction.Demand, SerializationFormatter = true)]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      if (swigCMemOwn == false)
        throw new ToolkitException("Object can not be serialized when swigCMemOwn is false.");
      info.AddValue("asOf_", AsOf);
      info.AddValue("freq_", Frequency);
      info.AddValue("bdc_", BdConvention);
      info.AddValue("cal_", Calendar);
      info.AddValue("rule_", CycleRule);
      info.AddValue("flags_", CashflowFlag);
      info.AddValue("anchor_", AnchorDate);
      info.AddValue("periods_", new PeriodList(this).ToArray());
    }

    ///<exclude/>
    public Schedule(SerializationInfo info, StreamingContext context)
      :base(CreateNative(info, context), true)
    {}

    private static IntPtr CreateNative(SerializationInfo info, StreamingContext context)
    {
      Dt asOf = (Dt)info.GetValue("asOf_", typeof(Dt));
      CycleRule rule = (CycleRule)info.GetValue("rule_", typeof(CycleRule));
      Frequency freq = (Frequency)info.GetValue("freq_", typeof(Frequency));
      BDConvention bdc = (BDConvention)info.GetValue("bdc_", typeof(BDConvention));
      Calendar cal = (Calendar)info.GetValue("cal_", typeof(Calendar));
      CashflowFlag flags = (CashflowFlag)info.GetValue("flags_", typeof(CashflowFlag));
      Dt anchor = (Dt)info.GetValue("anchor_", typeof(Dt));
      CouponPeriod[] periods = (CouponPeriod[])info.GetValue("periods_", typeof(CouponPeriod[]));
      int count = periods.Length;
      IntPtr cPtr = BaseEntityPINVOKE.new_Schedule__SWIG_1(
        asOf, (int)freq, (int)bdc, cal, (int)rule, (int)flags, anchor, count);
      if (BaseEntityPINVOKE.SWIGPendingException.Pending)
        throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
      var swigCPtr = new HandleRef(periods, cPtr);
      PeriodSetter set = GetPeriodSetter(swigCPtr);
      for (int i = 0; i < count; ++i)
      {
        CouponPeriod p = periods[i];
        set(i, p.AccrualBegin, p.AccrualEnd, p.CycleBegin, p.CycleEnd, p.Payment);
      }
      return cPtr;
    }
    #endregion Serialization

    #region Period List
    /// <summary>Number of entries in schedule</summary>
    [Category("Base")]
    public int Count
    {
      get { return Size(); }
    }


    /// <summary>
    ///   Get period (accrual) start date for payment index i.
    /// </summary>
    ///
    /// <param name="idx">Index for payment.</param>
    ///
    /// <returns>Period start date</returns>
    public Dt GetPeriodStart(int idx)
    {
      return Periods.GetAccrualBegin(idx);
    }

    /// <summary>
    ///   Get period (accrual) end date for payment index i.
    /// </summary>
    ///
    /// <param name="idx">Index for payment.</param>
    ///
    /// <returns>period end date</returns>
    public Dt GetPeriodEnd(int idx)
    {
      return Periods.GetAccrualEnd(idx);
    }

    /// <summary>
    ///   Get regular cycle start date of a period.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>For a short or long interest period, the regular cycle
    ///   start date will be the pseudo cycle date and will not match
    ///   GetPeriodStart() which will return the actual accrual start
    ///   date.</para>
    /// </remarks>
    ///
    /// <param name="idx">Index for payment.</param>
    ///
    /// <returns>Cycle start date</returns>
    public Dt GetCycleStart(int idx)
    {
      return Periods.GetCycleBegin(idx);
    }

    /// <summary>
    ///   Get regular cycle start date of a period.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>For a short or long interest period, the regular cycle
    ///   end date will be the pseudo cycle date and will not match
    ///   GetPeriodEnd() which will return the actual accrual end
    ///   date.</para>
    /// </remarks>
    ///
    /// <param name="idx">Index for payment.</param>
    ///
    /// <returns>Cycle end date</returns>
    public Dt GetCycleEnd(int idx)
    {
      return Periods.GetCycleEnd(idx);
    }

    /// <summary>
    ///   Get payment date for payment index i.
    /// </summary>
    ///
    /// <param name="idx">Index for payment.</param>
    ///
    /// <returns>payment date</returns>
    public Dt GetPaymentDate(int idx)
    {
      return Periods.GetPaymentDate(idx);
    }


    /// <summary>
    ///  Gets an array of all the coupon periods contained in this schedule.
    ///  <preliminary>Costly operation, for debug purpose only.</preliminary>
    /// </summary>
    /// <returns>An array of coupon periods.</returns>
    public CouponPeriod[] PeriodArray
    {
      get { return Periods.ToArray(); }
    }

    public IList<CouponPeriod> Periods
    {
      get
      {
        if (periods_ == null)
          periods_ = new PeriodList(this);
        return periods_;
      }
    }

    IList<CouponPeriod> ISchedule.Periods
    {
      get { return Periods; }
    }

    [Mutable] private PeriodList periods_;

 #pragma warning disable 649
    [StructLayout(LayoutKind.Sequential)]
    public struct DataMember
    {
      public Dt asOf_;
      public Frequency freq_;
      public BDConvention bdc_;
      public Calendar cal_;
      public CycleRule rule_;
      public CashflowFlag flags_;
      public Dt anchor_;
    };
#pragma warning restore 649

    /// <summary>Coupon period.</summary>
    [Serializable]
    public struct CouponPeriod : IPeriod
    {
      public Dt AccrualBegin, AccrualEnd, CycleBegin, CycleEnd, Payment;

      #region IPeriod Members

      Dt IPeriod.StartDate { get { return AccrualBegin; }}

      Dt IPeriod.EndDate { get { return AccrualEnd; } }

      bool IPeriod.ExclusiveEnd { get { return true; } }

      #endregion

      #region IComparable<IPeriod> Members

      int IComparable<IPeriod>.CompareTo(IPeriod other)
      {
        return AccrualBegin.CompareTo(other.StartDate);
      }

      #endregion
    }

    public unsafe class PeriodList : FixedSizeList<CouponPeriod>
    {
      #region Data members
      private readonly CouponPeriod* ptr_;
      private readonly int count_;
      private Schedule obj_;
      #endregion

      #region Methods and Properties
      public PeriodList(Schedule schedule)
      {
        HandleRef handleRef = schedule.HandleRef;
        count_ = BaseEntityPINVOKE.Schedule_Size(handleRef);
        if (BaseEntityPINVOKE.SWIGPendingException.Pending) throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
        if (count_ > 0)
        {
          IntPtr handle = BaseEntityPINVOKE.Schedule_getPeriodArray(handleRef);
          if (BaseEntityPINVOKE.SWIGPendingException.Pending)
            throw BaseEntityPINVOKE.SWIGPendingException.Retrieve();
          ptr_ = (CouponPeriod*)handle.ToPointer();
        }
        obj_ = schedule;
      }

      /// <summary>
      /// Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1"/>.
      /// </summary>
      /// <value></value>
      /// <returns>
      /// The number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1"/>.
      /// </returns>
      public override int Count
      {
        get { return count_; }
      }

      /// <summary>
      /// Gets or sets the <see cref="CouponPeriod"/> at the specified index.
      /// </summary>
      /// <value></value>
      public override CouponPeriod this[int index]
      {
        get
        {
          Debug.Assert(index >= 0 && index < count_);
          return ptr_[index];
        }
        set
        {
          throw new InvalidOperationException("List is readonly.");
        }
      }

      /// <summary>
      /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only.
      /// </summary>
      /// <value></value>
      /// <returns>true if the <see cref="T:System.Collections.Generic.ICollection`1"/> is read-only; otherwise, false.
      /// </returns>
      public override bool IsReadOnly
      {
        get { return true; }
      }

      #endregion
    } // class PeriodList
    #endregion Period List

    #region Data and Properties
    /// <summary>Pricing as-of date</summary>
    [Category("Base")]
    public Dt AsOf
    {
      get { return GetData().AsOf; }
    }

    /// <summary>Cycle rule</summary>
    [Category("Base")]
    public CycleRule CycleRule
    {
      get { return GetData().CycleRule; }
    }

    /// <summary>Cashflow flags</summary>
    [Category("Base")]
    public CashflowFlag CashflowFlag
    {
      get { return GetData().CashflowFlag; }
    }

    /// <summary>Frequency</summary>
    [Category("Base")]
    public Frequency Frequency
    {
      get { return GetData().Frequency; }
    }

    /// <summary>Roll convention</summary>
    [Category("Base")]
    public BDConvention BdConvention
    {
      get { return GetData().BdConvention; }
    }

    /// <summary>Calendar</summary>
    [Category("Base")]
    public Calendar Calendar
    {
      get { return GetData().Calendar; }
    }

    /// <summary>Calendar</summary>
    [Category("Base")]
    public Dt AnchorDate
    {
      get { return GetData().AnchorDate; }
    }

    private Data GetData()
    {
      if (data_ == null) data_ = new Data(HandleRef);
      return data_;
    }

    /// <summary>
    ///  Gets the native handle
    /// </summary>
    public HandleRef HandleRef => getCPtr(this);

    [Mutable] private Data data_;

    private unsafe class Data
    {
      private DataMember* ptr_;

      public Data(HandleRef handleRef)
      {
        ptr_ = (DataMember*)handleRef.Handle.ToPointer();
      }

      public Dt AsOf { get { return ptr_->asOf_; } }
      public CycleRule CycleRule {get { return ptr_->rule_; }}
      public CashflowFlag CashflowFlag{get { return ptr_->flags_; }}
      public Frequency Frequency {get { return ptr_->freq_; }}
      public BDConvention BdConvention{get { return ptr_->bdc_; }}
      public Calendar Calendar { get { return ptr_->cal_; } }
      public Dt AnchorDate { get { return ptr_->anchor_; } }
    }
    #endregion Data and Properties

    #region Utilities

    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Schedule));

    public static CycleRule MakeRule(bool adjustNext, bool eomRule)
    {
      return adjustNext ? CycleRule.FRN : (eomRule ? CycleRule.EOM : CycleRule.None);
    }

    private static CashflowFlag ToCashflowFlag(bool accrualOnCycle, bool adjustNext, bool adjustLast, bool eom, bool rollLastPayDate)
    {
      if (adjustNext && accrualOnCycle)
        throw new ToolkitException("accrualOnCycle and adjustNext (period start dates roll) cannot be both true.");
      if (adjustNext && eom)
        throw new ToolkitException("adjustNext (period start dates roll) and eomRule cannot be both true.");
      CashflowFlag flags = 0;
      if (accrualOnCycle) flags |= CashflowFlag.AccrueOnCycle;
      //if (adjustNext) flags |= CashflowFlag.AdjustNext;
      if (adjustLast) flags |= CashflowFlag.AdjustLast;
      //if (eom) flags |= CashflowFlag.EndOfMonthRule;
      if (rollLastPayDate) flags |= CashflowFlag.RollLastPaymentDate;
      return flags;
    }

    public static CashflowFlag ToCashflowFlag(bool accrualOnCycle, bool adjustNext, bool adjustLast, bool eom)
    {
      if (adjustNext && accrualOnCycle)
        throw new ToolkitException("accrualOnCycle and adjustNext (period start dates roll) cannot be both true.");
      if (adjustNext && eom)
        throw new ToolkitException("adjustNext (period start dates roll) and eomRule cannot be both true.");
      CashflowFlag flags = 0;
      if (accrualOnCycle) flags |= CashflowFlag.AccrueOnCycle;
      //if (adjustNext) flags |= CashflowFlag.AdjustNext;
      if (adjustLast) flags |= CashflowFlag.AdjustLast;
      //if (eom) flags |= CashflowFlag.EndOfMonthRule;
      if (ToolkitConfigurator.Settings.CashflowPricer.RollLastPaymentDate)
        flags |= CashflowFlag.RollLastPaymentDate;
      return flags;
    }
    #endregion Serialization

    #region IScheduleParams Members

    Dt IScheduleParams.AccrualStartDate
    {
      get { return Count > 0 ? Periods[0].AccrualBegin : Dt.Empty; }
    }

    Dt IScheduleParams.Maturity
    {
      get { return Count > 0 ? Periods[Count-1].AccrualEnd : Dt.Empty; }
    }

    Dt IScheduleParams.FirstCouponDate
    {
      get { return Count > 0 ? Periods[0].AccrualEnd : Dt.Empty; }
    }

    Dt IScheduleParams.NextToLastCouponDate
    {
      get
      {
        return Count > 1 ? Periods[Count - 2].AccrualEnd
          : (Count == 1 ? Periods[0].AccrualEnd : Dt.Empty);
      }
    }

    Frequency IScheduleParams.Frequency
    {
      get { return Frequency; }
    }

    CycleRule IScheduleParams.CycleRule
    {
      get { return CycleRule; }
    }

    CashflowFlag IScheduleParams.CashflowFlag
    {
      get { return CashflowFlag; }
    }

    BDConvention IScheduleParams.Roll
    {
      get { return BdConvention; }
    }

    Calendar IScheduleParams.Calendar
    {
      get { return Calendar; }
    }

    #endregion
  } // class Schedule
}
