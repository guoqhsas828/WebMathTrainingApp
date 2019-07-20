/*
 * Copyright (c) WebMathTraining 2012. All rights reserved.
 * 
 * First version by Hehui Jin Sept 17, 2012. 
 */

using System;
using System.Runtime.InteropServices;

namespace BaseEntity.Shared
{
  /// <summary>
  ///   Utilities for comparing two double precision floating numbers.
  /// </summary>
  public static class DoubleNumberComparison
  {
    #region Static data

    private const int DoubleBitCount = 64;
    private const UInt64 DoubleSignBitMask = 1UL << (DoubleBitCount - 1);

    /// <summary>
    ///  The machine epsilon of double precision floating number.
    /// </summary>
    public static readonly double MachineEpsilon = FindDoubleEpsilon();

    /// <summary>
    ///   The default number of ULP's (Units in the Last Place) to tolerate when
    ///   comparing two floating point numbers.
    /// </summary>
    /// <remarks>
    ///   See the article <a href="http://randomascii.wordpress.com/2012/02/25/comparing-floating-point-numbers-2012-edition/">
    ///   Comparing floating point numbers</a> by Bruce Dawson for more details on ULP.
    /// </remarks>
    public static readonly UInt64 DefaultUlpTolerance = 4;

    #endregion

    #region Methods

    /// <summary>
    ///  Converts double number to unsigned 64 bit interger which preserves
    ///  the relative order among the floating numbers.
    /// </summary>
    /// <param name="number">The number to convert.</param>
    /// <returns>The unsigned integer representation.</returns>
    /// <remarks>
    ///  <para>This function actually converts the binary representation of 
    ///   the floating point number from the the sign-and-magnitude form to the biased form.</para>
    ///  <para>More precisely, let <m>N = 2^{63}</m> and <m>x</m> be a signed long interger.
    ///  Then the biased representation of <m>x</m> is <m>x+N</m>.</para>
    /// <para>In this way, the most negative long integer, <m>1-N</m>, becomes <c>1</c>;
    /// 0 becomes <m>N</m>; and the most positive long integer, <m>N-1</m>, becomes <m>2N-1</m>.</para>
    /// </remarks>
    public static UInt64 ToBiasedBits(this double number)
    {
      ulong sam = new Rep(number).Bits;
      if ((DoubleSignBitMask & sam) != 0)
      {
        // sam represents a negative number.
        return ~sam + 1;
      }
      // sam represents a positive number.
      return DoubleSignBitMask | sam;
    }

    /// <summary>
    ///  Get the distance in ULPs of two numbers x and y.
    /// </summary>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <returns>The distance in ULPs.</returns>
    public static UInt64 Distance(double x, double y)
    {
      ulong bx = ToBiasedBits(x);
      ulong by = ToBiasedBits(y);
      return (bx >= by) ? (bx - by) : (by - bx);
    }

    /// <summary>
    /// Determines whether the number x and y have the same binary representation.
    /// </summary>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    public static bool IsSameAs(this double x, double y)
    {
      return new Rep(x).Bits == new Rep(y).Bits;
    }

    /// <summary>
    /// Determines whether the number x and y are almost the same.
    /// </summary>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <returns><c>true</c> if they are almost the same; otherwise, <c>false</c>.</returns>
    /// <remarks>
    ///  This differs from <see cref="AlmostEquals(double,double)"/> only in the comparision of NaN.
    ///  If both x and y are NaN, this function return <c>true</c> (they are almost the same),
    ///  while the function <see cref="AlmostEquals(double,double)"/> return <c>false</c> (NaN does not
    ///  equal to any number, including itself).
    /// </remarks>
    public static bool IsAlmostSameAs(this double x, double y)
    {
      return Distance(x, y) <= DefaultUlpTolerance;
    }

    /// <summary>
    /// Determines whether the number x and y are almost the same within the specified ULP tolerance limits.
    /// </summary>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <param name="ulpTolerance">The ULP tolerance.</param>
    /// <returns><c>true</c> if they are almost the same; otherwise, <c>false</c>.</returns>
    /// <remarks>This differs from <see cref="AlmostEquals(double,double,ulong)"/> only in the comparision of NaN.
    /// If both x and y are NaN, this function return <c>true</c> (they are almost the same),
    /// while the function <see cref="AlmostEquals(double,double,ulong)"/> return <c>false</c> (NaN does not
    /// equal to any number, including itself).</remarks>
    public static bool IsAlmostSameAs(this double x,
      double y, ulong ulpTolerance)
    {
      return Distance(x, y) <= ulpTolerance;
    }

    /// <summary>
    /// Determines whether the number x and y are almost equal.
    /// </summary>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <returns><c>true</c> if  they are almost equal; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    public static bool AlmostEquals(this double x, double y)
    {
      if (Double.IsNaN(x) && Double.IsNaN(y)) return false;
      return Distance(x, y) <= DefaultUlpTolerance;
    }

    /// <summary>
    /// Determines whether the number x and y are almost equal within the specified ULP tolerance limits.
    /// </summary>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <param name="ulpTolerance">The ULP tolerance.</param>
    /// <returns><c>true</c> if  they are almost equal; otherwise, <c>false</c>.</returns>
    /// <remarks></remarks>
    public static bool AlmostEquals(this double x, double y, ulong ulpTolerance)
    {
      if (Double.IsNaN(x) && Double.IsNaN(y)) return false;
      return Distance(x, y) <= ulpTolerance;
    }

    private static double FindDoubleEpsilon()
    {
      double e = 1.0;
      // ReSharper disable CompareOfFloatsByEqualityOperator
      while (1.0 + e != 1.0) e /= 2;
      // ReSharper restore CompareOfFloatsByEqualityOperator
      return e;
    }

    #endregion

    #region Nested type: Rep

    /// <summary>
    ///   Dual representation of the double type 
    ///   as a number and binary bits.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct Rep
    {
      public Rep(double d)
      {
        Bits = 0; // to please the compiler.
        Number = d;
      }

      [FieldOffset(0)] private readonly double Number;
      [FieldOffset(0)] public readonly UInt64 Bits;
    }

    #endregion
  }
}