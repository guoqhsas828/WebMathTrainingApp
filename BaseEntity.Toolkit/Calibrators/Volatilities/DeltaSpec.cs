/*
 *   2012. All rights reserved.
 */

using System;
using System.Text.RegularExpressions;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///   Delta specification.
  /// </summary>
  /// <remarks></remarks>
  [Serializable]
  public struct DeltaSpec : IComparable<DeltaSpec>
  {
    #region Data
    /// <summary>Specification of the at the money strike.</summary>
    public static readonly DeltaSpec Atm = new DeltaSpec(Double.NaN);

    /// <summary>An empty specification.</summary>
    public static readonly DeltaSpec Empty = new DeltaSpec(0.0); // use zero for it's the default value.

    private static readonly Regex _regexInput = new Regex(
      @"^(\d+)\s*d?\s*(?:(Call|c)|(Put|p))$|^(ATM)$",
      RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly double _size;
    #endregion

    #region Instance methods
    /// <summary>
    /// Prevents a default instance of the <see cref="DeltaSpec"/> struct from being created.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <remarks></remarks>
    private DeltaSpec(double size)
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
          : (IsCall
            ? String.Format("{0}C", (int)(_size * 100))
            : String.Format("{0}P", (int)(-_size * 100))));
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
    public bool IsCall
    {
      get { return _size > 0; }
    }

    /// <summary>
    /// Gets a value indicating whether this instance is a delta with put option.
    /// </summary>
    /// <remarks></remarks>
    public bool IsPut
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

    #region IComparable<FxDeltaSpec> Members

    /// <summary>
    /// Compare
    /// </summary>
    /// <param name="other">Other DeltaSpec to compare to</param>
    /// <returns>Comparison</returns>
    public int CompareTo(DeltaSpec other)
    {
      double s1 = IsAtm ? 0 : _size;
      double s2 = other.IsAtm ? 0 : other._size;
      return s1 < s2 ? 1 : (s1 > s2 ? -1 : 0);
    }

    #endregion

    #region Static constructors
    /// <summary>
    ///  Create an instance of the <see cref="DeltaSpec"/> struct with call option.
    /// </summary>
    /// <param name="deltaSize">Size of the delta.</param>
    /// <returns>An instance of the <see cref="DeltaSpec"/> struct</returns>
    public static DeltaSpec Call(double deltaSize)
    {
      if (!(deltaSize > 0 && deltaSize < 1))
        throw new ToolkitException(String.Format("Invalid delta size: {0}", deltaSize));
      return new DeltaSpec(deltaSize);
    }

    /// <summary>
    ///  Create an instance of the <see cref="DeltaSpec"/> struct with put option.
    /// </summary>
    /// <param name="deltaSize">Size of the delta.</param>
    /// <returns>An instance of the <see cref="DeltaSpec"/> struct</returns>
    public static DeltaSpec Put(double deltaSize)
    {
      if (!(deltaSize > 0 && deltaSize < 1))
        throw new ToolkitException(String.Format("Invalid delta size: {0}", deltaSize));
      return new DeltaSpec(-deltaSize);
    }

    /// <summary>
    /// Parses the specified string into an instance of the <see cref="DeltaSpec"/> struct.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns>An instance of the <see cref="DeltaSpec"/> struct</returns>
    public static DeltaSpec Parse(string input)
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
      return String.IsNullOrEmpty(match.Groups[2].Value) ? Put(size) : Call(size);
    }
    #endregion
  }
}