/*
 * AmortizationUtil.cs
 *
 *   2008. All rights reserved.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Base
{

  /// <summary>
  ///   Utility methods for the <see cref="Amortization"/> class
  /// </summary>
  ///
  /// <seealso cref="Amortization"/>
  ///
  public static class AmortizationUtil
  {
    private struct AmortInfo
    {
      public Dt Date { get; set; }
      /// <summary> This is the difference previous notional - current notional</summary>
      public double Delta { get; set; }
      /// <summary> This is the notional currently in effect as of this date (given the initial notional) </summary>
      public double Current { get; set; }
    }

    private static IEnumerable<AmortInfo> WalkAmortizationSchedule(IEnumerable<Amortization> schedule, double initialNotional)
    {
      double current = initialNotional;
      foreach (var a in schedule)
      {
        double delta = 0.0;
        switch (a.AmortizationType)
        {
          case AmortizationType.PercentOfInitialNotional:
            delta = a.Amount * initialNotional;
            current -= delta;
            if (Math.Abs(current) < NotionalTolerance)
              current = 0.0;
            break;
          case AmortizationType.PercentOfCurrentNotional:
            delta = a.Amount * current;
            current -= delta;
            if (Math.Abs(current) < NotionalTolerance)
              current = 0.0;
            break;
          case AmortizationType.RemainingNotionalLevels:
            double previous = current;
            current = a.Amount * initialNotional;
            delta = previous - current;  // So that current = previous - delta
            break;
        }
        yield return new AmortInfo {Date = a.Date, Delta = delta, Current = current};
      }
    }

    /// <summary>
    /// Convert a list of amortization records from a (Date, Value) dictionary to a list of Toolkit.Base.Amortization items.
    /// </summary>
    public static IList<Amortization> ToListOfAmortization(this IDictionary<Dt, double> amort, AmortizationType amortType)
    {
      if (amort == null || amort.Count == 0) return null;
      return amort.OrderBy(a => a.Key)
        .Select(pair => new Amortization(pair.Key, amortType, pair.Value))
        .ToList();
    }

    /// <summary>
    /// This function iterates through an amortization schedule, until the condition is met, and then returns corresponding date.
    /// This function assumes that initial notional = 1.
    /// </summary>
    /// <param name="list">List of amortizations</param>
    /// <param name="condition">Condition</param>
    /// <param name="date">Date to set</param>
    /// <returns>true if found, date set</returns>
    public static bool TryWhere(this IList<Amortization> list, Func<double, bool> condition, ref Dt date)
    {
      if (list == null)
        return false;

      foreach (var item in WalkAmortizationSchedule(list, 1.0).Where(x => condition(x.Current)))
      {
        date = item.Date;
        return true;
      }
      return false;
    }

    /// <summary>
    ///   Fill an array of dates and amortizations from a Amortization schedule
    /// </summary>
    ///
    /// <param name="schedule">Schedule of Amortizations, assumed to be sorted by date</param>
    /// <param name="dates">Returned array of amortization dates</param>
    /// <param name="amounts">Returned array of amortizations</param>
    /// <remarks>
    /// In this version, amounts (out) array is to be interpreted as the positive amount by which the notional decreases
    /// on each corresponding date.  The initial notional is assumed to be 1.
    /// 
    /// </remarks>
    ///
    public static void FromSchedule(IList<Amortization> schedule, out Dt[] dates, out double[] amounts)
    {
      FromSchedule(schedule, 1.0, AmortizationType.PercentOfInitialNotional, out dates, out amounts);
    }

    /// <summary>
    ///   Fill an array of dates and amortizations from a Amortization schedule
    /// </summary>
    ///
    /// <param name="schedule">Schedule of Amortizations, assumed to be sorted by date</param>
    /// <param name="initialNotional">Initial notional</param>
    /// <param name="type">The type of amortization. Based on this type, the returned array is filled either by current levels,
    /// or by percent differences with respect to initial notional, or with percent differences with respect to previous
    /// notional.
    /// </param>
    /// <param name="dates">Returned array of amortization dates</param>
    /// <param name="amounts">Returned array of amortizations</param>
    /// <remarks>
    /// In this version, amounts (out) array is to be interpreted based on the Amortization Type passed.
    /// 
    /// </remarks>
    ///
    public static void FromSchedule(IList<Amortization> schedule, double initialNotional, AmortizationType type,
      out Dt[] dates, out double[] amounts)
    {
      if (schedule != null && schedule.Count > 0)
      {
        int num = schedule.Count;
        dates = new Dt[num];
        amounts = new double[num];
        int i = 0;

        foreach (var item in WalkAmortizationSchedule(schedule, initialNotional))
        {
          dates[i] = item.Date;

          switch (type)
          {
            case AmortizationType.PercentOfInitialNotional:
              amounts[i] = item.Delta / initialNotional;
              break;
            case AmortizationType.RemainingNotionalLevels:
              amounts[i] = item.Current / initialNotional;
              break;
            case AmortizationType.PercentOfCurrentNotional:
              amounts[i] = item.Delta / (item.Current + item.Delta);
              break;
          }
          i++;
        }
      }
      else
      {
        dates = new Dt[0];
        amounts = new double[0];
      }
    }

    /// <summary>
    ///   Calculate notional at a specified date
    /// </summary>
    ///
    /// <param name="schedule">Schedule of Amortizations</param>
    /// <param name="initialNotional">initial notional</param>
    /// <param name="date">Date notional required for</param>
    /// <returns>Notional effective on the specified date</returns>
    ///
    public static double PrincipalAt(this IList<Amortization> schedule, double initialNotional, Dt date)
    {
      double current = initialNotional;
      if (schedule != null && schedule.Count > 0)
      {
        foreach (var item in WalkAmortizationSchedule(schedule, initialNotional))
        {
          if (item.Date > date)
            return current;
          current = item.Current;
        }
      }

      return current;
    }
    
    /// <summary>
    ///   Fill an Amortization schedule from an array of dates and amortizations
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Ignores invalid dates and zero amortization amounts. Assumes an amortization
    ///   type of <seealso cref="AmortizationType.PercentOfInitialNotional"/>.</para>
    /// </remarks>
    ///
    /// <param name="dates">Array of amortization dates</param>
    /// <param name="amounts">Array of amortizations</param>
    /// <param name="schedule">Schedule of Amortizations filled</param>
    ///
    public static void ToSchedule(Dt[] dates, double[] amounts, IList<Amortization> schedule)
    {
      ToSchedule(dates, amounts, AmortizationType.PercentOfInitialNotional, schedule);
    }

    /// <summary>
    ///   Fill an Amortization schedule from an array of dates and amortizations
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Ignores invalid dates and zero amortization amounts.</para>
    /// </remarks>
    ///
    /// <param name="dates">Array of amortization dates</param>
    /// <param name="amounts">Array of amortizations</param>
    /// <param name="schedule">Schedule of Amortizations filled</param>
    /// <param name="type">Type of amortization</param>
    ///
    public static void ToSchedule(Dt[] dates, double[] amounts, AmortizationType type, IList<Amortization> schedule)
    {
      schedule.Clear();
      if (dates != null && dates.Length > 0)
      {
        for (int i = 0; i < dates.Length; i++)
          if (dates[i].IsValid())
            schedule.Add(new Amortization(dates[i], type, amounts[i]));
      }
    }

  

    /// <summary>
    ///   Find the amortization for the period end with a given date
    ///   and start with a given index in the amortization schedule.
    /// </summary>
    ///
    /// <remarks>
    ///   The start index is automatically adjust to the
    ///   position of the first amortization date after the given date,
    ///   or to the end of the schedule, which ever comes first.
    /// </remarks>
    ///
    /// <param name="date">The coupon period end date</param>
    /// <param name="amortSchedule">Amortization schedule</param>
    /// <param name="startIndex">Start index in the amortization schedule, which is modified appropriately
    ///   upon exit of this function.</param>
    ///
    /// <returns>Amortizaion amount</returns>
    ///
    public static double ScheduledAmortization(Dt date, IList<Amortization> amortSchedule, ref int startIndex)
    {
      if (amortSchedule == null)
        return 0.0;

      double result = 0.0;
      int count = amortSchedule.Count;
      for (int i = startIndex; i < count; ++i)
        if (amortSchedule[i].Date > date)
          return result;
        else
        {
          result += amortSchedule[i].Amount;
          startIndex = i + 1;
        }
      return result;
    }

    /// <summary>
    ///   Validate Amortization schedule
    /// </summary>
    ///
    /// <param name="schedule">Amortization schedule</param>
    /// <param name="errors">List of errors to add to</param>
    ///
    public static void Validate(IList<Amortization> schedule, ArrayList errors)
    {
      double current = 1.0;
      if (schedule != null)
      {
        Dt date = Dt.Empty;
        int i = 0;
        foreach (var item in WalkAmortizationSchedule(schedule, 1.0))
        {
          if (i > 0)
          {
            if (item.Date <= date)
            {
              InvalidValue.AddError(errors, schedule, String.Format("Amortization schedule [{0}] is out of date order", i));
            }
          }
          date = item.Date;
          ++i;
          current = item.Current; // todo: should report error if < 0.0 at this point too?
        }
      }
      
      // in 100% amortization case, the repeated subtraction of floating-point type amortized amounts 
      // could result in slightly negative remaining notional/principal. 
      // we round it using the the first 15 digits.
      const int numDigits = 15;
      if (RoundingUtil.Round(current, numDigits) < 0)
      {
        InvalidValue.AddError(errors, schedule, "Amortization schedule amortizes more than initial notional");
      }
    }

    /// <summary>
    /// When notional drops below this level by abs. value, round it to 0.
    /// </summary>
    public const double NotionalTolerance = 1.0e-10;

  } // class AmortizationUtil
}
