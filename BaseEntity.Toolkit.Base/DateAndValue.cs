using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///  A pair of date and value
  /// </summary>
  /// <typeparam name="TValue">The type of the value.</typeparam>
  [Serializable]
  public struct DateAndValue<TValue>
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="DateAndValue{TValue}"/> struct.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="value">The value.</param>
    public DateAndValue(Dt date, TValue value) : this()
    {
      Date = date;
      Value = value;
    }

    /// <summary>
    /// Gets the date.
    /// </summary>
    /// <value>The date.</value>
    public Dt Date { get; private set; }

    /// <summary>
    /// Gets the value.
    /// </summary>
    /// <value>The value.</value>
    public TValue Value { get; private set; }

    /// <summary>
    /// Returns a <see cref="System.String" /> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="System.String" /> that represents this instance.</returns>
    public override string ToString()
    {
      return $"{Date}, {Value}";
    }
  }

  /// <summary>
  ///  Helpers for DateAndValue creation
  /// </summary>
  public static class DateAndValue
  {
    /// <summary>
    ///  Creates a new instance of the <see cref="DateAndValue{TValue}"/> struct.
    /// </summary>
    /// <param name="date">The date</param>
    /// <param name="value">The value</param>
    public static DateAndValue<T> Create<T>(Dt date, T value)
    {
      return new DateAndValue<T>(date, value);
    }
  }
}
