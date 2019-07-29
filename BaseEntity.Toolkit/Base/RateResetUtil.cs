/*
 * RateResetUtil.cs
 *
 *   2008. All rights reserved.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base
{

  #region RateResetUtil

  /// <summary>
  ///   Utility methods for the <see cref="RateReset"/> class
  /// </summary>
  ///
  /// <seealso cref="RateReset"/>
  ///
  public static class RateResetUtil
  {
    #region Methods

    /// <summary>
    /// Return the reset date prior to reset
    /// </summary>
    /// <param name="reset">Reset date </param>
    /// <param name="start">Beginning of accrual period</param>
    /// <param name="periodTenor"></param>
    /// <param name="resetLag">Reset lag</param>
    /// <param name="rateResets"></param>
    /// <returns>The reset date prior to reset </returns>
    /// TODO: this is not correct, need full schedule info
    public static Dt GetPreviousReset(Dt reset, Dt start, Tenor periodTenor, Tenor resetLag, RateResets rateResets)
    {
      Dt prev = Dt.Empty;
      foreach (Dt dt in rateResets.AllResets.Keys)
      {
        if (dt < reset && dt > prev)
          prev = dt;
      }

      if (prev.IsEmpty())
      {
        prev = Dt.Add(start, -Math.Abs(periodTenor.N), periodTenor.Units);
        prev = Dt.Add(prev, -Math.Abs(resetLag.N), resetLag.Units);
      }

      return prev;
    }


    /// <summary>
    ///   Fill an array of dates and rates from a RateReset schedule
    /// </summary>
    ///
    /// <param name="schedule">Rate reset schedule</param>
    /// <param name="dates">Returned array of effective dates</param>
    /// <param name="rates">Returned array of rates</param>
    ///
    public static void FromSchedule(IList<RateReset> schedule, out Dt[] dates, out double[] rates)
    {
      if (schedule != null && schedule.Count > 0)
      {
#if ONE_WAY
        Schedule sched = new Schedule(effective, effective, firstCpn, maturity, freq, roll, cal);
        int numResets = rateResets.Count;
        resetDates = new Dt[numResets];
        resetRates = new double[numResets];
        int i = 0; // counter through resetDates/Rates
        int j = 0; // counter through schedule

        foreach (DictionaryEntry de in rateResets)
        {
          Dt resetDate = (Dt)de.Key;

          // if reset was captured for a rolled period start then pass to the cashflow model
          // as the unadjusted period start; 
          // FillFloat only looks for most recent resets, <= period start
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
          resetRates[i++] = (double)de.Value;
        }
#else
        // Have a schedule
        int num = schedule.Count;
        dates = new Dt[num];
        rates = new double[num];
        for (int i = 0; i < num; i++)
        {
          dates[i] = schedule[i].Date;
          rates[i] = schedule[i].Rate;
        }
#endif
      }
      else
      {
        // No schedule
        dates = new Dt[0];
        rates = new double[0];
      }
    }

    /// <summary>
    ///   Fill an RateReset schedule from an array of dates and coupons
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Ignores invalid dates and zero rates.</para>
    /// </remarks>
    ///
    /// <param name="dates">Array of rate effective dates</param>
    /// <param name="rates">Array of rates</param>
    /// <param name="schedule">Rate reset schedule filled</param>
    ///
    public static void ToSchedule(Dt[] dates, double[] rates, IList<RateReset> schedule)
    {
      schedule.Clear();
      if (dates != null && dates.Length > 0)
        for (int i = 0; i < dates.Length; i++)
          if (dates[i].IsValid() && !rates[i].AlmostEquals(0.0))
            schedule.Add(new RateReset(dates[i], rates[i]));
    }

    /// <summary>
    ///   Calculate rate at a specified date
    /// </summary>
    ///
    /// <param name="schedule">Schedule of HistoricalResets</param>
    /// <param name="date">Date rate required for</param>
    ///
    /// <returns>Rate effective on the specified date</returns>
    ///
    public static double ResetAt(IList<RateReset> schedule, Dt date)
    {
      var rrlist = schedule as RateResetsList;
      if (rrlist != null)
      {
        return rrlist.ResetAt(date);
      }
      double rate = 0.0;
      if (schedule != null)
      {
        foreach (RateReset r in schedule)
        {
          if (r.Date <= date)
            rate = r.Rate;
          else
            break;
        }
      }
      return rate;
    }

    /// <summary>
    ///   Validate RateReset schedule
    /// </summary>
    ///
    /// <param name="schedule">RateReset schedule</param>
    /// <param name="errors">List of errors to add to</param>
    ///
    public static void Validate(IList<RateReset> schedule, ArrayList errors)
    {
      if (schedule != null)
      {
        for (int i = 0; i < schedule.Count; i++)
        {
          // Validate individual schedule item
          schedule[i].Validate(errors);
          // Make sure schedule is in order
          if (i > 0 && (schedule[i].Date <= schedule[i - 1].Date))
            InvalidValue.AddError(errors, schedule, String.Format("RateReset schedule [{0}] is out of date order", i));
        }
      }
    }


    /// <summary>
    /// Find Rate on Reset Date
    /// </summary>
    /// <param name="reset">Reset Date</param>
    /// <param name="asOf">As of</param>
    /// <param name="rateResets">Historical Rate Resets</param>
    /// <param name="resetState"></param>
    /// <returns>Fixing reset</returns>
    public static double FindRate(Dt reset, Dt asOf, RateResets rateResets, out RateResetState resetState)
    {
      return FindRate(reset, asOf, rateResets, false, out resetState);
    }


    /// <summary>
    /// Find Rate on Reset Date
    /// </summary>
    /// <param name="reset">Reset Date</param>
    /// <param name="asOf">As of</param>
    /// <param name="rateResets">Historical Rate Resets</param>
    /// <param name="useAsOfResets">turn this on to use rate resets that are set precisely on the asOf date</param>
    /// <param name="resetState">Reset state</param>
    /// <returns></returns>
    public static double FindRate(Dt reset, Dt asOf, RateResets rateResets, bool useAsOfResets, out RateResetState resetState)
    {
      if (reset > asOf)
      {
        resetState = RateResetState.IsProjected;
        return 0.0;
      }
      if (reset == asOf)
      {
        if (!useAsOfResets || rateResets == null)
        {
          resetState = RateResetState.IsProjected;
          return 0.0;
        }
        bool found;
        double rate = rateResets.GetRate(reset, out found);
        if (found)
        {
          resetState = RateResetState.ObservationFound;
          return rate;
        }
        resetState = RateResetState.Missing;
        return 0.0;
      }
      else
      {
        if (rateResets == null)
        {
          resetState = RateResetState.Missing;
          return 0.0;
        }
        bool found;
        double rate = rateResets.GetRate(reset, out found);
        if (found)
        {
          resetState = RateResetState.ObservationFound;
          return rate;
        }
        resetState = RateResetState.Missing;
        return 0.0;
      }
    }

    private static Dt ResetDtFromLag(Dt fixingDt, ReferenceIndex index, Tenor resetLag)
    {
      if (resetLag == Tenor.Empty)
        return Dt.AddDays(fixingDt, -index.SettlementDays, index.Calendar);
      if (resetLag.N == 0)
        return Dt.Roll(fixingDt, index.Roll, index.Calendar);
      return (resetLag.Units == TimeUnit.Days)
               ? Dt.AddDays(fixingDt, -resetLag.N, index.Calendar)
               : Dt.Roll(Dt.Add(fixingDt, -resetLag.N, resetLag.Units), index.Roll, index.Calendar);
    }

    /// <summary>
    /// Calc reset date
    /// </summary>
    /// <param name="fixingDt">fixing date</param>
    /// <param name="index">reference index</param>
    /// <param name="resetLag">Reset lag: if empty the settlement days of the index are used</param>
    ///<returns>Reset date</returns>
    /// <remarks>For the time being the following rules are supported:
    /// resetDt = fixingDt - index.DaysToSpot business days.
    /// resetDt = fixingDt - resetLag (unadjusted)
    /// resetDt = fixed day of week for weekly observations , i.e. Monday of each week, Tuesday of each week etc..
    /// resetDt = fixed day of month for monthly observations, i.e. First Day of the each month etc
    /// resetLag is empty and index is not null with resetDate
    /// If the fixingDt falls exactly on the reset date it is assumed that the observation is available on that day
    /// (TODO allow for user to select whether the observation is available or not)</remarks>
    public static Dt ResetDate(Dt fixingDt, ReferenceIndex index, Tenor resetLag)
    {
      if (index == null && resetLag.IsEmpty)
        return fixingDt;
      if (index == null)
        return Dt.Add(fixingDt, -resetLag.N, resetLag.Units);
      if (index.ResetDateRule == CycleRule.None)
        return ResetDtFromLag(fixingDt, index, resetLag);
      Dt retVal;
      switch (index.PublicationFrequency)
      {
        case Frequency.Monthly:
          int rule = (int)index.ResetDateRule + 1 - (int)CycleRule.First;
          bool after = fixingDt.Day >= rule;
          retVal = after
                     ? new Dt(rule, fixingDt.Month, fixingDt.Year)
                     : Dt.Add(fixingDt, Frequency.Monthly, -1, index.ResetDateRule);
          break;
        case Frequency.Weekly:
          int days = (int)fixingDt.DayOfWeek() - ((int)index.ResetDateRule - (int)CycleRule.Monday);
          retVal = (days >= 0)
                     ? Dt.Add(fixingDt, -days)
                     : Dt.Add(fixingDt, -7 - days);
          break;
        default:
          retVal = fixingDt;
          break;
      }
      return retVal;
    }


    /// <summary>
    /// Find Rate on Reset Date
    /// </summary>
    /// <param name="reset">Reset Date</param>
    /// <param name="asOf">As of</param>
    /// <param name="rateResets">Historical Rate Resets</param>
    /// <param name="state">state of reset</param>
    /// <returns></returns>
    public static double FindRateAndReportState(Dt reset, Dt asOf, RateResets rateResets, out RateResetState state)
    {
      if (reset <= asOf)
      {
        state = RateResetState.Missing;
        if (rateResets.AllResets != null && rateResets.AllResets.ContainsKey(reset))
        {
          state = RateResetState.ObservationFound;
          return rateResets.AllResets[reset];
        }
        if (rateResets.HasCurrentReset)
        {
          state = RateResetState.ObservationFound;
          return rateResets.CurrentReset;
        }
        if (reset == asOf)
          state = RateResetState.IsProjected;
        return 0;
      }
      state = RateResetState.IsProjected;
      return 0;
    }

    ///<summary>
    /// Check if on special dates a missing rate reset may be replaced by rate projection.
    ///</summary>
    ///<param name="resetDt">Rate reset date</param>
    ///<param name="asOf">Pricing date</param>
    ///<param name="start">Payment start date</param>
    ///<returns>True if projected rate can be used to replace missing rate reset</returns>
    public static bool ProjectMissingRateReset(Dt resetDt, Dt asOf, Dt start)
    {
      return (resetDt <= asOf && start >= asOf);
    }

    #endregion

    /// <summary>
    /// Determine rate fixing based on historic observations in one or more interest rate index.
    /// If in a first/last long/short stub period and more than one interest rate index is supplied, then use linear interpolation to 
    /// determine a weighted average rate from index tenors bracketing the tenor of the requested period.
    /// </summary>
    /// <param name="indices"></param>
    /// <param name="leg"></param>
    /// <param name="asOf">If resetDate is empty, determine fixing for current period based on asOf date</param>
    /// <param name="resetDate">If non-empty, determine fixing for precisely this reset date</param>
    /// <returns></returns>
    public static double FixingOnReset(InterestRateIndex[] indices,
                                       SwapLeg leg,
                                       Dt asOf,
                                       Dt resetDate)
    {
      if (indices.Length < 1)
        throw new ArgumentException("Need at least one InterestRateIndex");
      if (!leg.Floating)
        return 0.0;

      // First get reset info for all periods using one of the indices
      var dummy = new RateResets();
      var resetInfos = leg.GetResetInfo(asOf, dummy, indices[0]);
      var date = resetDate.IsEmpty() ? resetInfos.Keys.LastOrDefault(k => k <= asOf) : resetDate;
      var resetInfo = resetInfos[date];
      var periodNumber = resetInfos.Keys.ToList().IndexOf(date);
      var nPeriods = resetInfos.Keys.Count;

      if (resetInfo == null)
        throw new ToolkitException(String.Format("Can't find reset info for {0}", date));

      // If just one index presented, return the resetInfo's effective rate
      if (indices.Length == 1)
      {
        if (!resetInfo.EffectiveRate.HasValue)
          throw new ToolkitException(String.Format("Reset info for {0} has no rate", date));

        return resetInfo.EffectiveRate.Value;
      }

      // If regular period (not a stub)
      if (periodNumber > 0 && periodNumber < nPeriods - 1)
      {
        // use correct rate index
        var rateIndex = indices.FirstOrDefault(i => i.IndexName == leg.Index);
        if (rateIndex == null)
          throw new ToolkitException(String.Format("No index matches leg index {0}", leg.Index));

        var ri = leg.GetResetInfo(asOf, dummy, rateIndex);
        var rr = ri[date].EffectiveRate;
        if (!rr.HasValue)
          throw new ToolkitException(String.Format("Reset info for index {0} on {1} has no rate", rateIndex.IndexName, date));
        return rr.Value;
      }

      // Otherwise, determine candidate rates and the end dates for their corresponding loan periods
      var indexPeriods = indices
        .Select(index => new
        {
          Date = Dt.Roll(Dt.Add(resetInfo.AccrualStart, index.IndexTenor), index.Roll, index.Calendar),
          Index = index
        })
        .OrderBy(d => d.Date)
        .ToList();
      var priorIndexPeriod = indexPeriods.LastOrDefault(d => d.Date <= resetInfo.AccrualEnd);
      if (priorIndexPeriod == null)
        throw new ToolkitException(String.Format("Can't find index period shorter than requested period"));

      // Test to see if this is actually a regular period
      var ri1 = leg.GetResetInfo(asOf, dummy, priorIndexPeriod.Index);
      var r1 = ri1[date].EffectiveRate;
      if (!r1.HasValue)
        throw new ToolkitException(String.Format("Reset info for index {0} on {1} has no rate", priorIndexPeriod.Index.IndexName, date));

      if (priorIndexPeriod.Date == resetInfo.AccrualEnd)
      {
        return r1.Value;
      }

      // Otherwise linearly interpolate from the index values for periods straddling the accrual period
      var nextIndexPeriod = indexPeriods.FirstOrDefault(d => d.Date > resetInfo.AccrualEnd);
      if (nextIndexPeriod == null)
        throw new ToolkitException(String.Format("Can't find index period longer than requested period"));

      // Once we've determined the period dates using the index conventions (roll, calendar), we use simple interpolation without day count
      var days1 = priorIndexPeriod.Date - resetInfo.AccrualStart;
      var days2 = nextIndexPeriod.Date - resetInfo.AccrualStart;
      var days = resetInfo.AccrualEnd - resetInfo.AccrualStart;

      var ri2 = leg.GetResetInfo(asOf, dummy, nextIndexPeriod.Index);
      var r2 = ri2[date].EffectiveRate;
      if (!r2.HasValue)
        throw new ToolkitException(String.Format("Reset info for index {0} on {1} has no rate", nextIndexPeriod.Index.IndexName, date));

      var r = r1 + (r2 - r1) * (days - days1) / (days2 - days1);

      return r.Value;
    }
  }

  #endregion// class RateResetUtil
}