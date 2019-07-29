/*
 * InterestPeriodUtil.cs
 *
 *   2008. All rights reserved.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Utility methods for the <see cref="InterestPeriod"/> class
  /// </summary>
  ///
  /// <seealso cref="InterestPeriod"/>
  ///
  public static class InterestPeriodUtil
  {
    /// <summary>
    /// Transforms a set of Loan InterestPeriods into a single RateReset for a given date.
    /// </summary>
    /// 
    /// <param name="settle">The date</param>
    /// <param name="effective">The effective date of the RateReset period</param>
    /// <param name="interestPeriods">List of all InterestPeriods (not just applicable ones)</param>
    /// 
    /// <returns>List of HistoricalResets</returns>
    /// 
    public static List<RateReset> TransformToRateReset(Dt settle, Dt effective, IList<InterestPeriod> interestPeriods)
    {
      List<RateReset> rateResets = new List<RateReset>();
      List<InterestPeriod> currentPeriods = InterestPeriodsForDate(settle, interestPeriods);
      double avgCpn = 0;
      double totNot = 0;

      // Validate
      if (currentPeriods.Count == 0)
        return rateResets;

      // Get the avg coupon
      for (int i = 0; i < currentPeriods.Count; i++)
      {
        avgCpn += currentPeriods[i].AnnualizedCoupon * currentPeriods[i].PercentageNotional;
        totNot += currentPeriods[i].PercentageNotional;
      }

      // Weight by notional
      avgCpn /= totNot;

      // Setup reset
      rateResets.Add(new RateReset(effective, avgCpn));

      // Done 
      return rateResets;
    }

    /// <summary>
    ///   Return all active/current interest periods for a given date. <formula inline="true">(i.e startDt \lt date \leq endDt)</formula>
    /// </summary>
    ///
    /// <param name="date">Date which falls within interest period</param>
    /// <param name="rateResets">List of interest periods for all IR streams</param>
    ///
    public static List<InterestPeriod> InterestPeriodsForDate(Dt date, IList<InterestPeriod> rateResets)
    {
      List<InterestPeriod> interestPeriodsForDate = new List<InterestPeriod>();
      for (int i = 0; i < rateResets.Count; ++i)
      {
        if (rateResets[i].StartDate <= date && date < rateResets[i].EndDate)
        {
          interestPeriodsForDate.Add(rateResets[i]);
        }
      }

      return interestPeriodsForDate;
    }

    /// <summary>
    ///   Return (aggregate) cashflow amount for all interest periods paid on a given date.
    /// </summary>
    ///
    /// <param name="date">Payment date</param>
    /// <param name="rateResets">List of interest periods for all IR streams</param>
    ///
    public static double CashflowAt(Dt date, IList<InterestPeriod> rateResets)
    {
      double cashflowAmount = 0.0;
      for (int i = 0; i < rateResets.Count; ++i)
      {
        if (date == rateResets[i].EndDate)
        {
          DayCount dc = rateResets[i].DayCount;
          Dt startDate = rateResets[i].StartDate;
          Dt endDate = rateResets[i].EndDate;
          double coupon = rateResets[i].AnnualizedCoupon;
          double periodFraction = Dt.Fraction(startDate, endDate, startDate, date, dc, Frequency.None);
          double percentageNotional = rateResets[i].PercentageNotional;
          cashflowAmount += periodFraction * coupon * percentageNotional;
        }
      }
      return cashflowAmount;
    }

    /// <summary>
    ///   Return accrued amount for all interest periods in effect on a given date.
    /// </summary>
    /// <param name="date">date</param>
    /// <param name="rateResets">List of interest periods for all IR streams</param>
    public static double CashflowAccruedAt(Dt date, IList<InterestPeriod> rateResets)
    {
      double cashflowAmount = 0.0;
      for (int i = 0; i < rateResets.Count; ++i)
      {
        if (rateResets[i].StartDate <= date && date <= rateResets[i].EndDate)
        {
          DayCount dc = rateResets[i].DayCount;
          Dt startDate = rateResets[i].StartDate;
          Dt endDate = rateResets[i].EndDate;
          double coupon = rateResets[i].AnnualizedCoupon;
          double periodFraction = ((double)Dt.Diff(startDate, date, dc)) / (double)Dt.Diff(startDate, endDate, dc);
          double period = (double)Dt.Fraction(startDate, endDate, startDate, endDate, dc, Frequency.None);
          double percentageNotional = rateResets[i].PercentageNotional;
          cashflowAmount += periodFraction * period * coupon * percentageNotional;
        }
      }
      return cashflowAmount;
    }

    /// <summary>
    /// Generates the current InterestPeriod for a Loan using the standard schedule defined by the Loan.
    /// </summary>
    /// 
    /// <param name="settle">The pricing settlement date</param>
    /// <param name="loan_">The Loan</param>
    /// <param name="lastReset">The last index rate reset</param>
    /// <param name="drawn">The percentage of the total commitment that is drawn</param>
    /// 
    /// <returns>InterestPeriod</returns>
    /// 
    public static InterestPeriod DefaultInterestPeriod(Dt settle, Loan loan_, double lastReset, double drawn)
    {
      return DefaultInterestPeriod(settle, loan_.GetScheduleParams(), loan_.DayCount, lastReset, drawn);
    }

    /// <summary>
    /// Generates the current InterestPeriod for a Loan using the standard schedule terms given.
    /// </summary>
    /// 
    /// <param name="settle">The pricing settlement date</param>
    /// <param name="scheduleParams">Schedule parameters</param>
    /// <param name="dayCount">The day count method</param>
    /// <param name="lastReset">The last index rate reset</param>
    /// <param name="drawn">The percentage of total commitment drawn</param>
    /// 
    /// <returns>InterestPeriod</returns>
    /// 
    public static InterestPeriod DefaultInterestPeriod(Dt settle, ScheduleParams scheduleParams, DayCount dayCount, double lastReset, double drawn)
    {
      Schedule sched = Schedule.CreateScheduleForCashflowFactory(scheduleParams);
      Dt start = Dt.Empty, finish = Dt.Empty;

      if (sched.Count == 0)
      {
        start = scheduleParams.AccrualStartDate;
        finish = scheduleParams.Maturity;
      }
      else
      {
        int idx = sched.GetNextCouponIndex(settle);
        if (idx < 0) return null;
        start = sched.GetPeriodStart(idx);
        finish = sched.GetPeriodEnd(idx);
      }

      return new InterestPeriod(start, finish, lastReset, drawn, scheduleParams.Frequency, dayCount, scheduleParams.Roll, scheduleParams.Calendar);
    }
  } // class InterestPeriodUtil
}
