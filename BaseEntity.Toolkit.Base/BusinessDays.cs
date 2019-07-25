/*
 * Copyright (c)    2002-2013. All rights reserved.
 */

using System;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///  A number of business days based on the specified calendar.
  /// </summary>
  [Serializable]
  public struct BusinessDays
  {
    /// <summary>
    /// The number of business days.
    /// </summary>
    public readonly int Count;

    /// <summary>
    /// The calendar.
    /// </summary>
    public readonly Calendar Calendar;

    /// <summary>
    /// Construct a business days object
    /// </summary>
    /// <param name="count">Number of business days</param>
    /// <param name="calendar">Weekend/holiday calendar</param>
    public BusinessDays(int count, Calendar calendar)
    {
      Count = count;
      Calendar = calendar;
    }

    /// <summary>Subtracts the specified number of business days from the specified date.</summary>
    /// <remarks>This operator only counts weekends as non-business days.</remarks>
    public static Dt operator -(Dt d1, BusinessDays bdays) { return Dt.AddDays(d1, -bdays.Count, bdays.Calendar); }

    /// <summary>Adds the specified number of business days to the specified date.</summary>
    /// <remarks>This operator only counts weekends as non-business days.</remarks>
    public static Dt operator +(Dt d1, BusinessDays bdays) { return Dt.AddDays(d1, bdays.Count, bdays.Calendar); }
  }

  /// <summary>
  ///  Utility methods related to business days.
  /// </summary>
  internal static class DaysUtility
  {
    /// <summary>
    ///  Creates a number of business days based on the calendar with only the weekends as holidays.
    /// </summary>
    /// <param name="count">The number of days.</param>
    /// <returns>BusinessDays.</returns>
    /// <remarks>>This allows the expression <c>2.BusinessDays("NYB+LNB")</c>.</remarks>
    public static BusinessDays BusinessDays(this int count)
    {
      return new BusinessDays(count, Calendar.None);
    }

    /// <summary>
    ///  Creates a number of business days based on the specified calendar.
    /// </summary>
    /// <param name="count">The number of days.</param>
    /// <param name="calendar">The calendar.</param>
    /// <returns>BusinessDays.</returns>
    /// <remarks>>This allows the expression <c>2.BusinessDays("NYB+LNB")</c>.</remarks>
    public static BusinessDays BusinessDays(this int count, string calendar)
    {
      return new BusinessDays(count, Calendar.Parse(calendar));
    }

    /// <summary>
    ///  Creates a number of business days based on the specified calendar.
    /// </summary>
    /// <param name="count">The number of days.</param>
    /// <param name="calendar">The calendar.</param>
    /// <returns>BusinessDays.</returns>
    public static BusinessDays BusinessDays(this int count, Calendar calendar)
    {
      return new BusinessDays(count, calendar);
    }
  }
}
