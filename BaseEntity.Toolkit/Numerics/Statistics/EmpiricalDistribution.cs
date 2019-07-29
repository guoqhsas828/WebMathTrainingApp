/*
 * EmpiricalDistribution.cs
 *
 *  -2008. All rights reserved.    
 *
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  ///   Empirical distribution
  /// </summary>
  [Serializable]
  public class EmpiricalDistribution
  {
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="x">Sample values (ordered ascending)</param>
    /// <param name="p">Cumulative probability of values</param>
    public EmpiricalDistribution(double[] x, double[] p)
    {
      // Check array consistency.
      if (x == null)
        throw new ArgumentNullException("x");
      if (p == null)
        throw new ArgumentNullException("p");
      if (x.Length != p.Length)
        throw new ArgumentException(String.Format(
          "Size of x ({0}) and p ({1}) not match", x.Length, p.Length));
      // If any element is NaN, then the array cannot be properly sorted
      // and binary search may end with some index out of range.
      if (x.Any(Double.IsNaN))
        throw new ArgumentException("x contains NaN");
      if (p.Any(Double.IsNaN))
        throw new ArgumentException("p contains NaN");

      x_ = x; p_ = p;
    }

    /// <summary>
    ///   Cumulative probability at a given level
    /// </summary>
    /// <param name="x">level</param>
    /// <returns>cumulative probability</returns>
    public double Cumulative(double x)
    {
      if (x > x_[x_.Length - 1] - tolerance)
        return p_[p_.Length - 1];
      if (x < x_[0] + tolerance)
        return p_[0];
      int idx = Array.BinarySearch(x_, x);
      if (idx < 0) idx = ~idx;
      Debug.Assert(idx >= 0, "Index is negative");
      Debug.Assert(idx < x_.Length, "Index out of range");
      double dx = x_[idx] - x_[idx - 1];
      if (dx < tolerance)
        return p_[idx];
      double p = p_[idx-1] + (p_[idx] - p_[idx-1])
        * ((x - x_[idx-1]) / dx);
      return p;
    }

    /// <summary>
    ///   Quantile corresponding to a cumulative probability level
    /// </summary>
    /// <param name="p">cumulative probability</param>
    /// <returns>quantile</returns>
    public double Quantile(double p)
    {
      if (p > p_[p_.Length - 1] - tolerance)
        return x_[x_.Length - 1];
      if (p < p_[0] + tolerance)
        return x_[0];
      int idx = Array.BinarySearch(p_, p);
      if (idx < 0) idx = ~idx;
      Debug.Assert(idx >= 0);
      Debug.Assert(idx < p_.Length);
      double dp = p_[idx] - p_[idx - 1];
      if (dp < tolerance)
        return x_[idx];
      double x = x_[idx-1] + (x_[idx] - x_[idx-1])
        * ((p - p_[idx-1]) / dp);
      return x;
    }

    /// <summary>
    ///   Tolerance level.
    /// </summary>
    public static double Tolerance
    {
      get { return tolerance; }
    }

    private const double tolerance = 1E-15;
    private readonly double[] x_;
    private readonly double[] p_;
  }

}
