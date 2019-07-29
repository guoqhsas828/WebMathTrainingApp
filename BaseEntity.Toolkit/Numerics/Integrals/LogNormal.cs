/*
 * LogNormal.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using BaseEntity.Shared;

namespace BaseEntity.Toolkit.Numerics.Integrals
{
  /// <summary>
  ///   Implement left, right and overall integral for the functions
  ///   of log-normal variables.
  ///   <preliminary/>
  /// </summary>
  /// 
  /// <remarks>
  ///   This class provides a simple quadrature rule to evaluate the following integrals:
  ///   <formula>
  ///    \int_{0}^{K} f(x) p(x) d x \approx w_0\, f(x_0) + w_1\, f(x_1)
  ///   </formula>
  ///   <formula>
  ///    \int_{K}^{\infty} f(x) p(x) d x  \approx  w_2\, f(x_2) + w_3\, f(x_3)
  ///   </formula>
  ///   <formula>
  ///    \int_{0}^{\infty} f(x) p(x) d x  \approx \sum_{i=0}^3 w_i\, f(x_i)
  ///   </formula>
  ///   where <fomula inline="true">p(x)</fomula> is the log-normal density function.
  /// </remarks>
  /// 
  /// <exclude />
  public class LogNormal
  {
    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="d">The boundary</param>
    /// <param name="sigma">The standard deviation</param>
    public LogNormal(double d, double sigma)
    {
      Initialize(d, sigma);
    }

    /// <summary>
    ///   Evaluate the left integral
    /// </summary>
    /// <param name="fn">Function to integrate</param>
    /// <returns>Left integral</returns>
    public double LeftIntegral(Func<double,double> fn)
    {
      double result = Weighted(w_[0], fn, x_[0]) + Weighted(w_[1], fn, x_[1]);
      return result;
    }

    /// <summary>
    ///   Evaluate the left integral
    /// </summary>
    /// <param name="fn">Function to integrate</param>
    /// <returns>Left integral</returns>
    public double LeftIntegral(IUnivariateFunction fn)
    {
      return LeftIntegral(fn.evaluate);
    }

    /// <summary>
    ///   Evaluate the right integral
    /// </summary>
    /// <param name="fn">Function to integrate</param>
    /// <returns>Right integral</returns>
    public double RightIntegral(Func<double,double> fn)
    {
      double result = Weighted(w_[2], fn, x_[2]) + Weighted(w_[3], fn, x_[3]);
      return result;
    }

    /// <summary>
    ///   Evaluate the right integral
    /// </summary>
    /// <param name="fn">Function to integrate</param>
    /// <returns>Right integral</returns>
    public double RightIntegral(IUnivariateFunction fn)
    {
      return RightIntegral(fn.evaluate);
    }

    /// <summary>
    ///   Evaluate the overall integral
    /// </summary>
    /// <param name="fn">Function to integrate</param>
    /// <returns>Overall integral</returns>
    public double Integral(Func<double,double> fn)
    {
      double result = Weighted(w_[0], fn, x_[0]) + Weighted(w_[1], fn, x_[1])
        + Weighted(w_[2], fn, x_[2]) + Weighted(w_[3], fn, x_[3]);
      return result;
    }

    /// <summary>
    ///   Evaluate the overall integral
    /// </summary>
    /// <param name="fn">Function to integrate</param>
    /// <returns>Overall integral</returns>
    public double Integral(IUnivariateFunction fn)
    {
      return Integral(fn.evaluate);
    }

    /// <summary>
    ///   Initialize the points and weights
    /// </summary>
    /// <param name="d">The boundary</param>
    /// <param name="sigma">The standard deviation</param>
    public void Initialize(double d, double sigma)
    {
      x_ = new double[4];
      w_ = new double[4];
      double sigma2 = sigma * sigma;
      double e1 = Math.Exp(sigma2 / 2);
      double e2 = Math.Exp(2 * sigma2);
      double e3 = Math.Exp(9 * sigma2 / 2);

      double P0 = Normal.cumulative(d, 0, 1);
      double P1 = Normal.cumulative(d - sigma, 0, 1);
      if (P1 < 1E-12)
      {
        x_[0] = x_[1] = (P0 < 1E-12 ? Math.Exp(sigma*(d - 0.5*sigma)) : (P1*e1/P0));
        w_[0] = 0.0; w_[1] = P0;
        if (!calculate(1 - P0, (1 - P1)*e1, e2, e3, 2))
        {
          w_[2] = 1 - P0; w_[3] = 0;
          x_[2] = x_[3] = (P0 > 1 - 1E-12 ? Math.Exp(sigma*(d + 0.5*sigma)) : ((1 - P1)*e1/(1 - P0)));
        }
        return;
      }
      else if (P1 > 1 - 1E-10)
      {
        if (!calculate(P0, P1*e1, e2, e3, 0))
        {
          x_[0] = x_[1] = (P0 < 1E-12 ? Math.Exp(sigma*(d - 0.5*sigma)) : (P1*e1/P0));
          w_[0] = 0; w_[1] = P0;
        }
        x_[2] = x_[3] = (P0 > 1 - 1E-12 ? Math.Exp(sigma*(d + 0.5*sigma)) : ((1 - P1)*e1/(1 - P0)));
        w_[2] = 1 - P0; w_[3] = 0.0;
        return;
      }

      double P2 = Normal.cumulative(d - 2 * sigma, 0, 1);
      double P3 = Normal.cumulative(d - 3 * sigma, 0, 1);
      calculate(P0, P1 * e1, P2 * e2, P3 * e3,  0);
      calculate(1 - P0, (1 - P1) * e1, (1 - P2) * e2, (1 - P3) * e3, 2);
      //vandermonde(x_, wa_);
      return;
    }

    private bool calculate(double E0, double E1, double E2, double E3,
      int pos)
    {
      double B = E1 * E1 - E0 * E2;
      if (B.AlmostEquals(0.0)) return false;
      double A = (E1 * E2 - E0 * E3) / 2 / B;
      B = (E2 * E2 - E1 * E3) / B;
      B = Math.Sqrt(A * A - B);
      x_[pos] = A - B;
      x_[pos + 1] = A + B;
      w_[pos] = (E0 * x_[pos + 1] - E1) / 2 / B;
      w_[pos+1] = E0 - w_[pos];
      return true;
    }

    private static double Weighted(double w, Func<double, double> f, double x)
    {
      return w.AlmostEquals(0.0) ? 0.0 : (w*f(x));
    }

    private static void vandermonde(double[] x, double[] w)
    {
      int nm1 = x.Length - 1;
      for (int k = 0; k < nm1; ++k)
      {
        double xk = x[k];
        for (int j = nm1; j > k; --j)
          w[j] -= xk * w[j - 1];
      }
      for (int k = nm1 - 1; k >= 0; --k)
      {
        for (int j = k + 1; j <= nm1; ++j)
          w[j] /= x[j] - x[j - k - 1];
        for (int j = k; j < nm1; ++j)
          w[j] -= w[j + 1];
      }
      return;
    }

    private double[] x_, w_;
  }

  class LogNormalHermite
  {
    static LogNormalHermite()
    {
      _points = new double[_n];
      _weights = new double[_n];
      Quadrature.Normal(0, 1, true, _points, _weights);
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="d">The boundary</param>
    /// <param name="sigma">The standard deviation</param>
    public LogNormalHermite(double d, double sigma)
    {
      var x = _x = new double[_n];
      var w = _w = new double[_n];
      int bound = -1;
      for (int i = 0; i < x.Length; ++i)
      {
        w[i] = _weights[i];
        var xi = _points[i];
        if (bound < 0 && xi > d)
          bound = i;
        x[i] = Math.Exp(xi*sigma);
      }
      _bound = bound;
    }

    /// <summary>
    ///   Evaluate the left integral
    /// </summary>
    /// <param name="fn">Function to integrate</param>
    /// <returns>Left integral</returns>
    public double LeftIntegral(Func<double, double> fn)
    {
      var w = _w;
      var x = _x;
      double sum = 0.0;
      for (int i = 0, n = _bound; i < n; ++i)
        sum += w[i] * fn(x[i]);
      return sum;
    }

    /// <summary>
    ///   Evaluate the right integral
    /// </summary>
    /// <param name="fn">Function to integrate</param>
    /// <returns>Right integral</returns>
    public double RightIntegral(Func<double, double> fn)
    {
      var w = _w;
      var x = _x;
      double sum = 0.0;
      for (int i = _bound < 0 ? 0 : _bound, n = x.Length; i < n; ++i)
        sum += w[i] * fn(x[i]);
      return sum;
    }

    /// <summary>
    ///   Evaluate the overall integral
    /// </summary>
    /// <param name="fn">Function to integrate</param>
    /// <returns>Overall integral</returns>
    public double Integral(Func<double, double> fn)
    {
      var w = _w;
      var x = _x;
      double sum = 0.0;
      for (int i = 0, n = x.Length; i < n; ++i)
        sum += w[i] * fn(x[i]);
      return sum;
    }

    private const int _n = 300;
    private static readonly double[] _points, _weights;
    private readonly int _bound;
    private double[] _x, _w;
  }
}
