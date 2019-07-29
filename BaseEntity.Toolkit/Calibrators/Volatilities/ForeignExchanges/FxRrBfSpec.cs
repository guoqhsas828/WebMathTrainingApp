/*
 *   2012. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.Volatilities.ForeignExchanges
{
  /// <summary>
  ///  The specification of FX delta with Risk Reversal or Butterfly.
  /// </summary>
  /// <remarks></remarks>
  [Serializable]
  public struct FxRrBfSpec : IComparable<FxRrBfSpec>
  {
    #region Data
    /// <summary>Specification of the at the money strike.</summary>
    public static readonly FxRrBfSpec Atm = new FxRrBfSpec(Double.NaN);

    /// <summary>An empty specification.</summary>
    public static readonly FxRrBfSpec Empty = new FxRrBfSpec(0.0); // use zero for it's the default value.

    private static readonly Regex _regexInput = new Regex(
      @"^(\d+)\s*d?\s*(?:(RR)|(BF))$|^(ATM)$",
      RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly double _size;
    #endregion

    #region Instance methods
    /// <summary>
    /// Prevents a default instance of the <see cref="FxRrBfSpec"/> struct from being created.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <remarks></remarks>
    private FxRrBfSpec(double size)
    {
      _size = size;
    }

    /// <summary>
    /// Returns a <see cref="System.String"/> that represents this instance.
    /// </summary>
    /// <returns>A <see cref="System.String"/> that represents this instance.</returns>
    public override string ToString()
    {
      return IsEmpty
        ? ""
        : (IsAtm
          ? "ATM"
          : (IsRiskReversal
            ? String.Format("{0}RR", (int)(_size * 100))
            : String.Format("{0}BF", (int)(-_size * 100))));
    }
    #endregion

    #region Properties
    /// <summary>Gets the size of delta.</summary>
    public double Delta
    {
      get { return _size < 0 ? -_size : _size; }
    }

    // ReSharper disable CompareOfFloatsByEqualityOperator
    /// <summary>
    /// Gets a value indicating whether this instance is empty.
    /// </summary>
    public bool IsEmpty
    {
      get { return _size == 0.0; }
    }

    // ReSharper restore CompareOfFloatsByEqualityOperator

    /// <summary>
    /// Gets a value indicating whether this instance is a delta with call option.
    /// </summary>
    /// <remarks></remarks>
    public bool IsRiskReversal
    {
      get { return _size > 0; }
    }

    /// <summary>
    /// Gets a value indicating whether this instance is a delta with put option.
    /// </summary>
    /// <remarks></remarks>
    public bool IsButterfly
    {
      get { return _size < 0; }
    }

    /// <summary>
    /// Gets a value indicating whether this instance is a delta at the money.
    /// </summary>
    /// <remarks></remarks>
    public bool IsAtm
    {
      get { return Double.IsNaN(_size); }
    }
    #endregion

    #region Static constructors
    /// <summary>
    ///  Create an instance of the <see cref="FxRrBfSpec"/> struct with call option.
    /// </summary>
    /// <param name="deltaSize">Size of the delta.</param>
    /// <returns>An instance of the <see cref="FxRrBfSpec"/> struct</returns>
    public static FxRrBfSpec RiskReversal(double deltaSize)
    {
      if (!(deltaSize > 0 && deltaSize < 1))
        throw new ToolkitException(String.Format("Invalid delta size: {0}", deltaSize));
      return new FxRrBfSpec(deltaSize);
    }

    /// <summary>
    ///  Create an instance of the <see cref="FxRrBfSpec"/> struct with put option.
    /// </summary>
    /// <param name="deltaSize">Size of the delta.</param>
    /// <returns>An instance of the <see cref="FxRrBfSpec"/> struct</returns>
    public static FxRrBfSpec Butterfly(double deltaSize)
    {
      if (!(deltaSize > 0 && deltaSize < 1))
        throw new ToolkitException(String.Format("Invalid delta size: {0}", deltaSize));
      return new FxRrBfSpec(-deltaSize);
    }

    /// <summary>
    /// Parses the specified string into an instance of the <see cref="DeltaSpec"/> struct.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>An instance of the <see cref="DeltaSpec"/> struct</returns>
    public static FxRrBfSpec Parse(string input)
    {
      if (input != null) input = input.Trim();
      if (String.IsNullOrEmpty(input))
      {
        return Empty;
      }
      Match match = _regexInput.Match(input);
      if (!match.Success)
      {
        throw new ToolkitException(String.Format(
          "Not a legitimate Delta specification: {0}", input));
      }
      if (!String.IsNullOrEmpty(match.Groups[4].Value))
      {
        return Atm;
      }
      double size = Int32.Parse(match.Groups[1].Value) / 100.0;
      return String.IsNullOrEmpty(match.Groups[2].Value)
        ? Butterfly(size)
        : RiskReversal(size);
    }
    #endregion

    #region IComparable<FxDeltaRrBfSpec> Members

    /// <summary>
    /// Comparison
    /// </summary>
    /// <param name="other">Other object to compare</param>
    /// <returns>Comparison</returns>
    public int CompareTo(FxRrBfSpec other)
    {
      double s1 = IsAtm ? 0 : _size;
      double s2 = other.IsAtm ? 0 : other._size;
      return s1 < s2 ? 1 : (s1 > s2 ? -1 : 0);
    }

    #endregion
  }
}
