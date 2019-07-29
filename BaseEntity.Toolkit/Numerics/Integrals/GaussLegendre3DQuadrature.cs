//
//   2015. All rights reserved.
//
using System;
using Parallel = BaseEntity.Toolkit.Concurrency.Algorithms;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  /// Multidimensional quadrature on rectangular regions
  /// </summary>
  public interface MultiDimensionalQuadrature
  {
    /// <summary>
    /// Integration rule
    /// </summary>
    int Rule { get; }

    /// <summary>
    /// One dimensional numerical integration of func(x) in [a,b].
    /// </summary>
    /// <param name="func">Function object.</param>
    /// <param name="a">Lower limit of integration</param>
    /// <param name="b">Upper limit of integration</param>
    /// <returns>Definite integral of 1 dfunction</returns>
    double Integrate1D(Func<double, double> func, double a, double b);

    /// <summary>
    /// 2 dimensional numerical integration of func(x,y) in [a0,b0]x[a1,b1]
    /// </summary>
    /// <param name="func">Function object</param>
    /// <param name="a0">Lower limit of integration for first variable</param>
    /// <param name="b0">Upper limit of integration of first variable</param>
    /// <param name="a1">Lower limit of integration of second variable</param>
    /// <param name="b1">Upper limit of integration of second variable</param>
    /// <returns>Definite integral of 2 d function</returns>
   double Integrate2D(Func<double, double, double> func, double a0, double b0, double a1, double b1);

    /// <summary>
    /// 3 dimensional numerical integration of func(x,y,z) in [a0,b0]x[a1,b1]x[a2,b2]
    /// </summary>
    /// <param name="func">Function object</param>
    /// <param name="a0">Lower limit of integration for first variable</param>
    /// <param name="b0">Upper limit of integration of first variable</param>
    /// <param name="a1">Lower limit of integration of second variable</param>
    /// <param name="b1">Upper limit of integration of second variable</param>
    /// <param name="a2">Lower limit of integration of third variable</param>
    /// <param name="b2">Upper limit of integration of third variable</param>
    /// <returns>Definite integral of 3 d function</returns>
    double Integrate3D(Func<double, double, double, double> func, double a0, double b0, double a1, double b1,
                              double a2, double b2);
  }


  #region GaussLegendre3D

  /// <summary>
  /// Integration in one two and three dimensions using a 
  /// </summary>
  public class MultiDimensionalGaussLegendre : MultiDimensionalQuadrature
  {
    private double[] pts_;
    private double[] wts_;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="n">Number of quadrature points</param>
    public MultiDimensionalGaussLegendre(int n)
    {
      pts_ = new double[n];
      wts_ = new double[n];
      Quadrature.GaussLegendre(true, true, pts_, wts_);
    }

    /// <summary>
    /// Quadrature rule
    /// </summary>
    public int Rule { get { return pts_.Length; } }

    /// <summary>
    /// One dimensional numerical integration of func(x) in [a,b].
    /// </summary>
    /// <param name="func">Function object.</param>
    /// <param name="a">Lower limit of integration</param>
    /// <param name="b">Upper limit of integration</param>
    /// <returns>Definite integral of 1 dfunction</returns>
    public double Integrate1D(Func<double, double> func, double a, double b)
    {
      double xl = 0.5 * (b - a);
      double xm = 0.5 * (b + a);
      double retVal = 0.0;
      unchecked
      {
        for (int i = 0; i < pts_.Length; ++i)
        {
          double w = wts_[i];
          double x = xm + xl * pts_[i];
          double y = func(x);
          retVal += w * y;
        }
      }
      retVal *= xl;
      return retVal;
    }

    /// <summary>
    /// 2 dimensional numerical integration of func(x,y) in [a0,b0]x[a1,b1]
    /// </summary>
    /// <param name="func">Function object</param>
    /// <param name="a0">Lower limit of integration for first variable</param>
    /// <param name="b0">Upper limit of integration of first variable</param>
    /// <param name="a1">Lower limit of integration of second variable</param>
    /// <param name="b1">Upper limit of integration of second variable</param>
    /// <returns>Definite integral of 2 d function</returns>
    public double Integrate2D(Func<double, double, double> func, double a0, double b0, double a1, double b1)
    {
      double xl = 0.5 * (b1 - a1);
      double xm = 0.5 * (b1 + a1);
      int n = pts_.Length;
      double retVal = Parallel.Sum(0, n - 1, (i) =>
      {
        double w = wts_[i];
        double x = xm + xl * pts_[i];
        double y = Integrate1D((z) => func(z, x), a0, b0);
        return w * y;
      });
      retVal *= xl;
      return retVal;
    }

    /// <summary>
    /// 3 dimensional numerical integration of func(x,y,z) in [a0,b0]x[a1,b1]x[a2,b2]
    /// </summary>
    /// <param name="func">Function object</param>
    /// <param name="a0">Lower limit of integration for first variable</param>
    /// <param name="b0">Upper limit of integration of first variable</param>
    /// <param name="a1">Lower limit of integration of second variable</param>
    /// <param name="b1">Upper limit of integration of second variable</param>
    /// <param name="a2">Lower limit of integration of third variable</param>
    /// <param name="b2">Upper limit of integration of third variable</param>
    /// <returns>Definite integral of 3 d function</returns>
    public double Integrate3D(Func<double, double, double, double> func, double a0, double b0, double a1, double b1,
                              double a2, double b2)
    {
      double xl = 0.5 * (b2 - a2);
      double xm = 0.5 * (b2 + a2);
      int n = pts_.Length;
      double retVal = Parallel.Sum(0, n - 1, (i) =>
      {
        double w = wts_[i];
        double x = xm + xl * pts_[i];
        double y = Integrate2D((u, z) => func(u, z, x), a0, b0, a1, b1);
        return w * y;
      });
      retVal *= xl;
      return retVal;
    }

    /// <summary>
    /// Number of points in the quadrature
    /// </summary>
    internal int NumberPoints
    {
      get { return pts_.Length;} 
      set 
      { pts_ = new double[value];
        wts_ = new double[value];
        Quadrature.GaussLegendre(false, false, pts_, wts_);
      }
    }
  }

  #endregion
}
