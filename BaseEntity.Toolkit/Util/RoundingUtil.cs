using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Toolkit.Util
{
  //TODO: Rename this class to NumberUtility for number related small functions.

  /// <summary>
  /// Class that helps 
  /// </summary>
  public static class RoundingUtil
  {
    /// <summary>
    /// Gets the machine epsilon.
    /// </summary>
    /// <remarks></remarks>
    public static double MachineEpsilon
    {
      get { return machEps_; }
    }

    /// <summary>
    /// Gets the default value of the epsilon for comparison of two double values.
    /// </summary>
    /// <remarks></remarks>
    public static double DefaultComparisonPrecision
    {
      get { return compareEps_; }
    }

    /// <summary>
    ///  Check if x approximately equals to y.
    /// </summary>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static bool ApproximatelyEqualsTo(this double x, double y)
    {
      return Math.Abs(x - y) < DefaultComparisonPrecision;
    }

    /// <summary>
    /// Check if x approximately equals to y within the specified epsilon.
    /// </summary>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <param name="epsilon">The epsilon.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static bool ApproximatelyEqualsTo(this double x, double y, double epsilon)
    {
      return Math.Abs(x - y) < epsilon;
    }

    /// <summary>
    ///  Round by a number of digits
    /// </summary>
    /// <param name="number"></param>
    /// <param name="digits"></param>
    /// <returns></returns>
    public static double Round(double number, int digits)
    {
      return (digits!= -1)?Math.Floor(number*Math.Pow(10, digits) + 0.5)/Math.Pow(10, digits):number;
    }

    internal static uint SetBitIf(this uint flags, bool condition, uint bit)
    {
      return condition ? (flags | bit) : (flags & ~bit);
    }

    private static double machEps_;
    private static double compareEps_;
    static RoundingUtil()
    {
      double machEps = 1.0d;
      do
      {
        machEps /= 2.0d;
      }
      while ((double)(1.0 + machEps) != 1.0);
      machEps_ = machEps;
      compareEps_ = 2*machEps;
    }
  }
}
