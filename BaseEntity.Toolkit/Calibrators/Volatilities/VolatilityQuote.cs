/*
 *   2012. All rights reserved.
 */

using System;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///  Represent the volatility quote as a pair of key and value.
  /// </summary>
  /// <remarks>
  ///  The main purpose of this type is to provide a way to display volatility quotes as
  ///  a read-only collection.  It is not intended to be used as the internal representation
  ///  of volatility quotes within tenors.
  /// </remarks>
  [Serializable]
  public struct VolatilityQuote
  {
    /// <summary>
    ///  The quote key.
    /// </summary>
    public readonly string Key;

    /// <summary>
    ///   The quote value.
    /// </summary>
    public readonly double Value;

    /// <summary>
    /// Initializes a new instance of the <see cref="VolatilityQuote"/> struct.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <remarks></remarks>
    public VolatilityQuote(string key, double value)
    {
      Key = key;
      Value = value;
    }

    /// <summary>
    /// Returns a <see cref="System.String"/> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="System.String"/> that represents this instance.</returns>
    /// <remarks></remarks>
    public override string ToString()
    {
      return String.Format("{0}, {1}", Key, Value);
    }
  }
}