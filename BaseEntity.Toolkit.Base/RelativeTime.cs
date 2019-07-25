/*
 * Copyright (c)    2014. All rights reserved.
 * Relative time
 */

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  /// Represent the relative time between two dates.
  /// </summary>
  public struct RelativeTime
  {
    /// <summary>
    /// The average days per year (365.25).
    /// </summary>
    public const double DaysPerYear = 365.25;

    /// <summary>
    /// One day represented in relaive time.
    /// </summary>
    public static readonly RelativeTime OneDay = new RelativeTime(1.0/DaysPerYear);

    /// <summary>
    /// The relative time in years, based on the convention of 365.25 days per year.
    /// </summary>
    public readonly double Value;

    /// <summary>
    ///   Initializes a new instance of the <see cref="RelativeTime" /> struct.
    /// </summary>
    /// <param name="time">The time.</param>
    private RelativeTime(double time)
    {
      Value = time;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RelativeTime" /> struct.
    /// </summary>
    /// <param name="begin">The begin.</param>
    /// <param name="end">The end.</param>
    public RelativeTime(Dt begin, Dt end)
    {
      Value = (end - begin)/DaysPerYear;
    }

    /// <summary>
    /// Gets the days.
    /// </summary>
    /// <value>The days.</value>
    public double Days
    {
      get { return Value*DaysPerYear; }
    }

    /// <summary>
    /// Converts the number of days to relative time.
    /// </summary>
    /// <param name="days">The number of days.</param>
    /// <returns>RelativeTime.</returns>
    public static RelativeTime FromDays(double days)
    {
      return new RelativeTime(days/DaysPerYear);
    }

    /// <summary>
    /// Performs an implicit conversion from <see cref="RelativeTime" /> to <see cref="System.Double" />.
    /// </summary>
    /// <param name="time">The time.</param>
    /// <returns>The value of the relative time span.</returns>
    public static implicit operator double(RelativeTime time)
    {
      return time.Value;
    }

    /// <summary>
    /// Performs an explicit conversion from <see cref="System.Double" /> to <see cref="RelativeTime" />.
    /// </summary>
    /// <param name="time">The time.</param>
    /// <returns>The result of the conversion.</returns>
    public static explicit operator RelativeTime(double time)
    {
      return new RelativeTime(time);
    }

    /// <summary>
    /// Adds the specified relative time span to the date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="time">The time.</param>
    /// <returns>The result date.</returns>
    public static Dt operator +(Dt date, RelativeTime time)
    {
      return Dt.Add(date, time);
    }

    /// <summary>
    /// Subtracts the specified relative time span from the date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="time">The time.</param>
    /// <returns>The result date.</returns>
    public static Dt operator -(Dt date, RelativeTime time)
    {
      return Dt.Add(date, new RelativeTime(-time.Value));
    }

    /// <summary>
    /// Implements the equal operator.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>The result of the operator.</returns>
    public static bool operator ==(RelativeTime left, RelativeTime right)
    {
      return left.Value == right.Value;
    }

    /// <summary>
    /// Implements the not equal operator.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>The result of the operator.</returns>
    public static bool operator !=(RelativeTime left, RelativeTime right)
    {
      return !(left == right);
    }

    /// <summary>
    /// Determines whether the specified <see cref="System.Object" /> is equal to this instance.
    /// </summary>
    /// <param name="obj">Another object to compare to.</param>
    /// <returns><c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
    public override bool Equals(object obj)
    {
      return Value.Equals(obj);
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
    public override int GetHashCode()
    {
      return Value.GetHashCode();
    }

    /// <summary>
    /// Returns a <see cref="System.String" /> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
    public override string ToString()
    {
      return Value.ToString();
    }
  }
}
