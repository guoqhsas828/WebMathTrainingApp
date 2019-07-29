/*
 * ScheduleUtil.cs
 *
 *  -2011. All rights reserved.
 *
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Utility functions to deal with schedule dates
  ///   <prelimninary/>
  /// </summary>
	public static class ScheduleUtil
  {
    #region Date utility methods
    /// <summary>
    /// Validate that a list of dates are in ascending order.
    /// </summary>
    /// <typeparam name="T">Any type implementing IPeriod interface.</typeparam>
    /// <param name="schedule">Schedule of periods</param>
    /// <param name="errors">List of errors to add to</param>
    /// <param name="prefix">The text to identify the schedule (can be null or empty).</param>
    /// <param name="allowDuplication">if set to <c>true</c> allow duplicated dates in the sequence.</param>
    /// <remarks>
    /// This function checks that in the list:
    /// (1) Each element is valid by itself;
    /// (2) All the dates are in ascending order.
    /// </remarks>
    public static void Validate<T>(this IList<T> schedule,
      ArrayList errors, string prefix, bool allowDuplication) where T : IDate
    {
      if (schedule == null || schedule.Count == 0) return;

      int n = schedule.Count;
      for (int i = 0; i < n; ++i)
      {
        // Validate individual CallPeriod
        var v = schedule[i] as IValidatable;
        if (v != null) v.Validate(errors);
        // Make sure schedule is in order
        if (i > 0 && (allowDuplication
          ? schedule[i - 1].Date > schedule[i].Date
          : schedule[i - 1].Date >= schedule[i].Date))
        {
          InvalidValue.AddError(errors, schedule, String.Format(
            "{0} [{1}] is out of date order",
            String.IsNullOrEmpty(prefix) ? "Period" : prefix,
            i));
        }
      }
      return;
    }

    /// <summary>
    /// Validates that the schedule dates are in order and throw a ToolkitException
    /// if validation fails.
    /// </summary>
    /// <typeparam name="T">Any type implementing IPeriod interface.</typeparam>
    /// <param name="schedule">The schedule.</param>
    /// <param name="prefix">The text to identify the schedule (can be null or empty).</param>
    /// <param name="allowDuplication">if set to <c>true</c>, allow duplication in dates.</param>
    public static void Validate<T>(this IList<T> schedule,
      string prefix, bool allowDuplication) where T : IDate
    {
      var errors = new ArrayList();
      schedule.Validate(errors, prefix, allowDuplication);
      var msg = BaseEntityObject.CollectValidationErrors(errors);
      if (!String.IsNullOrEmpty(msg)) throw new ToolkitException(msg);
    }
    #endregion

    #region Period utility methods

    /// <summary>
    /// Validate a list of periods.
    /// </summary>
    /// <remarks>
    /// This function checks that in the list:
    ///   (1) Each element is valid by itself;
    ///   (2) All the periods are in ascending order by the start dates;
    ///   (3) There is no overlap in the periods.
    /// </remarks>
    /// <typeparam name="T">Any type implementing IPeriod interface.</typeparam>
    /// <param name="schedule">Schedule of periods</param>
    /// <param name="errors">List of errors to add to</param>
    /// <param name="prefix">The text to identify the schedule (can be null or empty).</param>
    public static void Validate<T>(this IList<T> schedule,
      ArrayList errors, string prefix) where T : IPeriod
    {
      if (schedule == null) return;

      for (int i = 0; i < schedule.Count; i++)
      {
        // Validate individual CallPeriod
        var v = schedule[i] as IValidatable;
        if (v != null) v.Validate(errors);
        // Make sure schedule is in order
        if (i > 0 && (schedule[i - 1].ExclusiveEnd
          ? schedule[i].StartDate < schedule[i - 1].EndDate
          : schedule[i].StartDate <= schedule[i - 1].EndDate))
        {
          InvalidValue.AddError(errors, schedule, String.Format(
            "{0} [{1}] is out of date order",
            String.IsNullOrEmpty(prefix) ? "Period" : prefix,
            i));
        }
      }
      return;
    }

    /// <summary>
    /// Validates the schedule periods and throw a ToolkitException
    ///  if validation fails.
    /// </summary>
    /// <typeparam name="T">Any type implementing IPeriod interface.</typeparam>
    /// <param name="schedule">The schedule.</param>
    /// <param name="prefix">The text to identify the schedule (can be null or empty).</param>
    public static void Validate<T>(this IList<T> schedule,
      string prefix) where T : IPeriod
    {
      var errors = new ArrayList();
      schedule.Validate(errors, prefix);
      var msg = BaseEntityObject.CollectValidationErrors(errors);
      if (!String.IsNullOrEmpty(msg)) throw new ToolkitException(msg);
    }

    /// <summary>
    /// Compares two periods by the start dates.
    /// </summary>
    /// <param name="p1">The first period.</param>
    /// <param name="p2">The second period.</param>
    /// <returns>0 if the start dates are equal;
    ///   less than 0 if the first period starts before the second;
    ///   greater than 0 if the first period starts after the second.</returns>
    public static int Compare(IPeriod p1, IPeriod p2)
    {
      return Dt.Cmp(
        p1 != null ? p1.StartDate : Dt.Empty,
        p2 != null ? p2.StartDate : Dt.Empty);
    }

    /// <summary>
    ///  Find an index of a period in the list which encloses the specified date.
    /// </summary>
    /// <remarks>The schedule must be validated before call this fucntions.</remarks>
    /// <typeparam name="T">Any type implementing IPeriod interface.</typeparam>
    /// <param name="schedule">The ordered list of periods.</param>
    /// <param name="date">The date.</param>
    /// <returns>Index of a period enclosing the date, or -1 if no such period exists.</returns>
    public static int IndexOf<T>(this IList<T> schedule, Dt date) where T : IPeriod
    {
      if (schedule == null || schedule.Count == 0) return -1;
      schedule.DebugValidate();
      int n = schedule.Count;
      for (int i = 0; i < n; ++i)
      {
        var p = schedule[i];
        if (date < p.StartDate) return -1;
        if (p.ExclusiveEnd ? date < p.EndDate : date <= p.EndDate)
          return i;
      }
      return -1;
    }

    /// <summary>
    ///  The debug version of validate function, only active in the debug build,
    ///   used for internal checks.
    /// </summary>
    /// <typeparam name="T">Any type implementing IPeriod interface.</typeparam>
    /// <param name="schedule">The schedule.</param>
    [Conditional("DEBUG")]
    internal static void DebugValidate<T>(this IList<T> schedule) where T : IPeriod
    {
      schedule.Validate(null);
    }

    /// <summary>
    ///  The debug version of validate function, only active in the debug build,
    ///   used for internal checks.
    /// </summary>
    /// <remarks>This can be used in the internal functions to catch errors
    ///  which are expected to be caught by other public functions.  Such errors
    ///  are mistakes in the internal logic.  They should be caught in the debug
    ///  phase.</remarks>
    /// 
    [Conditional("DEBUG")]
    internal static void DebugValidate(this IBaseEntityObject obj)
    {
      obj.Validate();
    }
    #endregion

    #region Option period utilities
    /// <summary>
    ///   Merge overlaped periods and return a list of disjoint periods.
    /// </summary>
    /// <param name="periods">Input periods, assumed sorted by start dates</param>
    /// <returns></returns>
    public static IEnumerable<IOptionPeriod> MergeOverlapPeriods(
      this IEnumerable<IOptionPeriod> periods)
    {
      var otype = OptionType.None;
      var ostyle = OptionStyle.None;
      Dt start = Dt.Empty, end = Dt.Empty;
      foreach (var period in periods)
      {
        var periodEnd = period.ExclusiveEnd
          ? period.EndDate
          : (period.EndDate + 1);
        if (period.StartDate > end || (period.StartDate == end &&
          (period.Type != otype || period.Style != ostyle)))
        {
          if (!end.IsEmpty())
            yield return CreatePeriod(otype, ostyle, start, end);

          start = period.StartDate;
          end = periodEnd;
          otype = period.Type;
          ostyle = period.Style;
          continue;
        }

        if (period.Type != otype || period.Style != ostyle)
        {
          throw new ToolkitException(
            "Inconsistent option type/style in overlapped period");
        }

        if (periodEnd > end)
        {
          end = periodEnd;
        }
      } // end for loop

      // After loop
      if (!end.IsEmpty())
        yield return CreatePeriod(otype, ostyle, start, end);
    }

    /// <summary>
    /// If the peroid start or end day are not on the payment days, find the last payment day before the period
    /// start day and the first payment day after the peroid end day.
    /// </summary>
    /// <param name="period">The input peroid</param>
    /// <param name="dates">the payment days</param>
    /// <returns></returns>
    public static IOptionPeriod ExpendPeriod(
      this IOptionPeriod period, Dt[] dates)
    {
      Dt start = Dt.Empty, end = Dt.Empty;
      var idx = Array.BinarySearch(dates, period.StartDate);
      if (idx < 0)
      {
        idx = ~idx;
        start = dates[idx > 0 ? (idx - 1) : 0];
      }
      idx = Array.BinarySearch(dates, period.EndDate);
      if (idx < 0)
      {
        idx = ~idx;
        if (idx <= dates.Length) end = dates[idx];
      }
      if (start.IsEmpty() && end.IsEmpty())
        return period;
      return CreatePeriod(period.Type, period.Style,
        start.IsEmpty() ? period.StartDate : start,
        end.IsEmpty() ? period.EndDate : end);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="otype">Option type</param>
    /// <param name="ostyle">Option style</param>
    /// <param name="startDate">Begin date of the period (inclusive)</param>
    /// <param name="endDate">End date of the perid (exclusive)</param>
    /// <returns></returns>
    private static IOptionPeriod CreatePeriod(OptionType otype, OptionStyle ostyle, Dt startDate, Dt endDate)
    {
      Debug.Assert(endDate - startDate >= 1);
      var period = otype == OptionType.Call
        ? (IOptionPeriod)new CallPeriod(startDate, endDate - 1, 1, 0, ostyle, 0)
        : (IOptionPeriod)new PutPeriod(startDate, endDate - 1, 1, ostyle);
      return period;
    }

    /// <summary>
    ///   Get exercise price by date from a list of option exercise periods.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Returns zero if no call is active.</para>
    /// </remarks>
    ///
    /// <param name="schedule">Call schedule</param>
    /// <param name="date">Date at which call price is requested.</param>
    ///
    public static double ExercisePriceByDate<T>(
      this IList<T> schedule, Dt date) where T : IOptionPeriod
    {
      if (schedule != null)
      {
        foreach (var c in schedule)
        {
          if ((c.StartDate <= date) && (c.EndDate >= date))
            return c.ExercisePrice;
        }
      }
      return 0.0;
    }

    internal static bool TryGetExercisePriceByDate<T>(
      this IList<T> schedule, Dt date, out double result) where T : IOptionPeriod
    {
      if (schedule != null)
      {
        foreach (var c in schedule)
        {
          if ((c.StartDate <= date) && (c.EndDate >= date))
          {
            result = c.ExercisePrice;
            return true;
          }
        }
      }
      result = 0;
      return false;
    }

    /// <summary>
    /// Get the option exercise price by the specified date.
    /// </summary>
    /// <param name="callable">The callable.</param>
    /// <param name="date">Date at which call price is requested.</param>
    /// <returns>
    /// The call price, or <c>NaN</c> is no option is active.
    /// </returns>
    /// <remarks><para>Returns <c>NaN</c> if no option is active.
    /// The caller may use the function <c>Double.IsNaN(value)</c>
    /// to check the return value.</para>
    /// 
    /// <para>The exercise scedule must be validated.</para>
    /// </remarks>
    internal static double GetExercisePriceByDate(this ICallable callable, Dt date)
    {
      int idx;
      var sched = callable.ExerciseSchedule;
      if (sched == null|| 0 >(idx = sched.IndexOf(date)))
        return Double.NaN;
      return sched[idx].ExercisePrice;
    }

    #endregion

    #region Cashflow utilities

    /// <summary>
    /// Enumerates the payment dates in a cash flow.
    /// </summary>
    /// <param name="cf">The cash flow.</param>
    /// <param name="includeEffective">if set to <c>true</c>, the first accrual start date.</param>
    /// <returns>Enumerable of Dates.</returns>
    internal static IEnumerable<Dt> EnumerateDates(
      this CashflowAdapter cf, bool includeEffective)
    {
      if (cf != null)
      {
        int n = cf.Count;
        yield return cf.Effective;
        for (int i = 0; i < n; ++i)
          yield return cf.GetDt(i);
      }
    }

    #endregion

    #region CouponPeriod utilities

    internal static Dt GetAccrualBegin(
      this IList<Schedule.CouponPeriod> periods,
      int index)
    {
      Debug.Assert(index >= 0 && index < periods.Count);
      return periods[index].AccrualBegin;
    }
    internal static Dt GetAccrualEnd(
      this IList<Schedule.CouponPeriod> periods,
      int index)
    {
      Debug.Assert(index >= 0 && index < periods.Count);
      return periods[index].AccrualEnd;
    }
    internal static Dt GetCycleBegin(
      this IList<Schedule.CouponPeriod> periods,
      int index)
    {
      Debug.Assert(index >= 0 && index < periods.Count);
      return periods[index].CycleBegin;
    }
    internal static Dt GetCycleEnd(
      this IList<Schedule.CouponPeriod> period,
      int index)
    {
      Debug.Assert(index >= 0 && index < period.Count);
      return period[index].CycleEnd;
    }
    internal static Dt GetPaymentDate(
      this IList<Schedule.CouponPeriod> periods,
      int index)
    {
      Debug.Assert(index >= 0 && index < periods.Count);
      return periods[index].Payment;
    }

    #endregion

    #region Miscellaneous
    /// <summary>
    ///   Get the next coupon payment date AFTER value date, using no Roll convention.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Assumptions: Loan pays annually, semiannually, quarterly, or monthly; and 
    ///   effectiveDate &lt;= asOf &lt;= maturityDate</para>
    ///   
    ///   <para>NOTE: The coupon payment date found is not guaranteed to actually be a valid settlement date.</para>
    /// </remarks>
    ///
    /// <returns>Next coupon date after pricing as-of date</returns>
    ///
    public static Dt NextCouponDate(Dt settle, Dt issue, Dt firstCoupon, Frequency freq, bool eomRule)
    {
      Dt nextCouponDate = (firstCoupon == Dt.Empty ? issue : firstCoupon);
      while (nextCouponDate <= settle)
      {
        nextCouponDate = Dt.Add(nextCouponDate, freq, eomRule);
      }
      return nextCouponDate;
    }
    #endregion

  } // class ScheduleUtil
}
