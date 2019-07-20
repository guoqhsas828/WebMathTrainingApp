using System;

namespace BaseEntity.Metadata
{
  /// <summary>
  ///     Extension methods for DateTime
  /// </summary>
  public static class DateTimeExtensions
  {
    /// <summary>
    /// Value to represent a psuedo null DateTime in the database.
    /// This is the value for a DateTime that has not been given a value but is saved to the db.
    /// </summary>
    private static readonly DateTime SqlMinDateTime = new DateTime(1753, 1, 1);

    /// <summary>
    /// Value to represent a psuedo null UTC DateTime in the database.
    /// This is the value for a UTC DateTime that has not been given a value but is saved to the db.
    /// </summary>
    private static readonly DateTime SqlMinDateTimeUtc = new DateTime(1753, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dt"></param>
    /// <returns></returns>
    public static bool IsEmpty(this DateTime dt)
    {
      return dt == DateTime.MinValue || dt == SqlMinDateTime || dt == SqlMinDateTimeUtc;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dt"></param>
    /// <returns></returns>
    public static DateTime ToDateTime(this DateTime dt)
    {
      return dt;
    }

    /// <summary>
    /// Convert to int using same logic as Dt
    /// </summary>
    /// <param name="dt"></param>
    /// <returns></returns>
    public static int ToInt(this DateTime dt)
    {
      return dt.Year * 10000 + dt.Month * 100 + dt.Day;
    }

    /// <summary>
    /// Tries the validate.
    /// </summary>
    /// <param name="dateTime">The date time.</param>
    /// <param name="errorMsg">The error MSG.</param>
    /// <returns></returns>
    public static bool TryValidate(this DateTime dateTime, out string errorMsg)
    {
      errorMsg = String.Empty;
      if (dateTime != DateTime.MinValue && dateTime.Kind != DateTimeKind.Utc)
      {
        errorMsg = "DateTime value is not UTC!";
        return false;
      }
      if (dateTime < SqlMinDateTimeUtc)
      {
        errorMsg = String.Format("DateTime value [{0}] < MinValue [{1}]", dateTime.ToLocalTime(), SqlMinDateTimeUtc.ToLocalTime());
        return false;
      }
      return true;
    }

    /// <summary>
    ///  Checks for equality ignoring microseconds.
    /// </summary>
    /// <param name="dateTime"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsSame(this DateTime dateTime, DateTime value)
    {
      return dateTime.Kind == value.Kind && dateTime.Date == value.Date && ((int)dateTime.TimeOfDay.TotalSeconds) == ((int)value.TimeOfDay.TotalSeconds);
    }

    /// <summary>
    ///   Removes milliseconds from the datetime
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static DateTime TrimMilliSeconds(this DateTime dateTime)
    {
      return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Kind);
    }
  }
}
