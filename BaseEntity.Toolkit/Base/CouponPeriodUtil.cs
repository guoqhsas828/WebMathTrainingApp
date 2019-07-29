/*
 * CouponPeriodUtil.cs
 *
 *   2008. All rights reserved.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Utility methods for the <see cref="CouponPeriod"/> class
  /// </summary>
  ///
  /// <seealso cref="CouponPeriod"/>
  ///
  public static class CouponPeriodUtil
  {
    /// <summary>
    ///   Fill an array of dates and coupons from a CouponPeriod schedule
    /// </summary>
    ///
    /// <param name="schedule">Coupon Schedule</param>
    /// <param name="maturity">Maturity date (if no schedule)</param>
    /// <param name="coupon">Coupon (if no schedule)</param>
    /// <param name="dates">Returned array of coupon dates</param>
    /// <param name="amounts">Returned array of coupons</param>
    ///
    public static void FromSchedule(
      IList<CouponPeriod> schedule, Dt maturity, double coupon, out Dt[] dates, out double[] amounts
      )
    {
      if (schedule != null && schedule.Count > 0)
      {
        // Have a schedule
        int num = schedule.Count; 
        dates = new Dt[num];
        amounts = new double[num];
        for (int i = 0; i < num; i++)
        {
          dates[i] = schedule[i].Date;
          amounts[i] = schedule[i].Coupon;
        }
      }
      else
      {
        // No schedule so just the maturity and current coupon
        dates = new Dt[1]; dates[0] = maturity;
        amounts = new double[1]; amounts[0] = coupon;
      }

      return;
    }

    /// <summary>
    ///   Fill an array of dates and coupons from a CouponPeriod schedule
    /// </summary>
    ///
    /// <param name="schedule">Coupon Schedule</param>
    /// <param name="nextCouponDate">AsOf date</param>
    /// <param name="maturity">Maturity date (if no schedule)</param>
    /// <param name="coupon">Coupon (if no schedule)</param>
    /// <param name="dates">Returned array of coupon dates</param>
    /// <param name="amounts">Returned array of coupons</param>
    ///
    public static void FromSchedule(
      IList<CouponPeriod> schedule, Dt nextCouponDate, Dt maturity, double coupon, out Dt[] dates, out double[] amounts
      )
    {
      if (schedule != null && schedule.Count > 0)
      {
        // Have a schedule
        int num = schedule.Count;
        
        if (schedule[0].Date > nextCouponDate) 
          dates = new Dt[num + 1];
        else
          dates = new Dt[num];

        amounts = new double[dates.Length];
        if (dates.Length == num + 1)
        {
          dates[0] = nextCouponDate;
          amounts[0] = coupon;
          for (int i = 1; i < dates.Length; i++)
          {
            dates[i] = schedule[i - 1].Date;
            amounts[i] = schedule[i - 1].Coupon;
          }
        }
        else
        {
          for (int i = 0; i < dates.Length; i++)
          {
            dates[i] = schedule[i].Date;
            amounts[i] = schedule[i].Coupon;
          }
        }
      }
      else
      {
        // No schedule so just the maturity and current coupon
        dates = new Dt[1]; dates[0] = maturity;
        amounts = new double[1]; amounts[0] = coupon;
      }

      return;
    }


    /// <summary>
    ///   Fill an CouponPeriod schedule from an array of dates and coupons
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Ignores invalid dates.</para>
    /// </remarks>
    ///
    /// <param name="dates">Array of coupon dates</param>
    /// <param name="amounts">Array of coupons</param>
    /// <param name="schedule">Coupon Schedule filled</param>
    ///
    public static void ToSchedule(Dt[] dates, double[] amounts, IList<CouponPeriod> schedule)
    {
      schedule.Clear();
      if (dates != null && dates.Length > 0)
        for (int i = 0; i < dates.Length; i++)
          if (dates[i].IsValid())
            schedule.Add(new CouponPeriod(dates[i], amounts[i]));

      return;
    }

    /// <summary>
    ///   Calculate coupon effective at settlement date
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Calculates the annualised coupon rate effective on the
    ///   specified settlement date.</para>
    ///   <para>If no reference curve is specified - a fixed rate
    ///   is assumed and the coupon implied by the initial coupon
    ///   and coupon schedule are used.</para>
    ///   <para>If the reference curve is specified - a floating rate
    ///   is assumed and historical rates are </para>
    /// </remarks>
    ///
    /// <param name="settle">Settlement date</param>
    /// <param name="prevCouponDate">Previous coupon cycle date</param>
    /// <param name="coupon">Initial coupon rate or spread</param>
    /// <param name="couponSchedule">Coupon schedule</param>
    /// <param name="rateResets">Historical rate resets</param>
    /// <param name="referenceCurve">Floating rate reference curve</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="dayCount">Coupon daycount</param>
    /// <param name="freq">Coupon payment frequency</param>
    /// <param name="eomRule">EOM rule</param>
    ///
    /// <returns>Coupon effective at specified settlement date</returns>
    ///
    public static double CouponAt(
      Dt settle, Dt prevCouponDate, double coupon,
      IList<CouponPeriod> couponSchedule, IList<RateReset> rateResets, DiscountCurve referenceCurve,
      Dt maturity, DayCount dayCount, Frequency freq, bool eomRule )
    {
      if( referenceCurve == null )
        // Fixed rate coupon
        return CouponAt(couponSchedule, coupon, settle);
      else
      {
        if( !prevCouponDate.IsEmpty() && prevCouponDate > referenceCurve.AsOf )
        {
          // Floating rate that resets after curve generation date, so imply from curve
          double cpn = CouponAt(couponSchedule, coupon, settle);
          return cpn + referenceCurve.F(prevCouponDate, Dt.Add(prevCouponDate, freq, eomRule), dayCount, freq);
        }
        else
          // Historical floating rate coupon
          return RateResetUtil.ResetAt(rateResets, settle);
      }
    }

		/// <summary>
		///   Calculate coupon effective at the given (future) date.
		/// </summary>
		/// 
		/// <remarks>
		///   <para>Calculates the annualised coupon rate effective on the
		///   specified date.</para>
		///   <para>If the reset rate is less than or equal to zero - a fixed rate
		///   is assumed and the coupon implied by the initial coupon
		///   and coupon schedule are used.</para>
		///   <para>If the reset rate is greater than 0 - a floating rate
		///   is assumed.</para>
		/// </remarks>
		///
		/// <param name="date">Date</param>
		/// <param name="coupon">Initial coupon rate or spread</param>
		/// <param name="couponSchedule">Coupon schedule</param>
		/// <param name="resetRate">The last rate reset</param>
		///
		/// <returns>Coupon effective at specified date</returns>
		///
		public static double CouponAt(Dt date, double coupon, IList<CouponPeriod> couponSchedule, double resetRate)
		{
			if (resetRate <= 0.0)
				// Fixed rate coupon
				return CouponAt(couponSchedule, coupon, date);
			else
			{
				// Floating rate that resets after curve generation date, so imply from curve
				double cpn = CouponAt(couponSchedule, coupon, date);
				return cpn + resetRate;
			}
		}

    /// <summary>
    ///   Calculate coupon at a specified date
    /// </summary>
    ///
    /// <param name="schedule">Coupon Schedule</param>
    /// <param name="coupon">Initial coupon</param>
    /// <param name="date">Date coupon required for</param>
    /// <returns>Coupon effective on the specified date</returns>
    ///
    public static double CouponAt(IList<CouponPeriod> schedule, double coupon, Dt date)
    {
      double premium = coupon;
      if (schedule != null && schedule.Count > 0)
      {
        foreach (CouponPeriod c in schedule)
        {
          if (c.Date <= date)
            premium = c.Coupon;
          else
            break;
        }
      }

      return premium;
    }

    /// <summary>
    ///   Validate CouponPeriod schedule
    /// </summary>
    ///
    /// <param name="schedule">Coupon Schedule</param>
    /// <param name="errors">List of errors to add to</param>
    /// <param name="validateCoupon">Validate the coupon amount (if rate)</param>
    public static void Validate(IList<CouponPeriod> schedule, ArrayList errors, bool validateCoupon = true)
    {
      if (schedule != null)
        for (int i = 0; i < schedule.Count; i++)
        {
          // Validate individual Amortization
          schedule[i].Validate(errors, validateCoupon);
          // Make sure schedule is in order
          if (i > 0 && (schedule[i].Date <= schedule[i - 1].Date))
            InvalidValue.AddError(errors, schedule, String.Format("CouponPeriod schedule [{0}] is out of date order", i));
        }

      return;
    }

    /// <summary>
    /// Calculate avarage coupon between two days with coupon schedule
    /// </summary>
    /// <param name="begin">Begin date</param>
    /// <param name="end">End date</param>
    /// <param name="dayCount">day count</param>
    /// <param name="schedule">The full coupon schedule</param>
    /// <returns></returns>
    internal static double CalculateAverageCoupon(Dt begin, Dt end,
      DayCount dayCount, IList<CouponPeriod> schedule)
    {
      if (begin >= end)
        return 0.0;
      return CalculateAccrual(begin, end, dayCount, schedule)/
        Dt.Fraction(begin, end, dayCount);
    }


    internal static double CalculateAccrual(Dt begin, Dt end,
      DayCount dayCount, IList<CouponPeriod> schedule)
    {
      if (schedule == null || schedule.Count == 0)
        return 0.0;

      var currentCpn = CouponPeriodUtil.CouponAt(schedule, 0.0, begin);
      
      var accrual = 0.0;
      var start = begin;
      foreach (var p in schedule)
      {
        if(start >= p.Date)
          continue;

        if (p.Date > end)
          break;

        accrual += currentCpn*Dt.Fraction(start, p.Date, start, 
          p.Date,dayCount, Frequency.None);
        start = p.Date;
        currentCpn = p.Coupon;
      }
      accrual += currentCpn*Dt.Fraction(start, end, start,
        end, dayCount, Frequency.None);
      return accrual;
    }
  } // class CouponPeriodUtil
}
