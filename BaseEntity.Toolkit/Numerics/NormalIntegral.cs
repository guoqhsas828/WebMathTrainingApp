/*
 * NormalIntegral.cs
 * 
 * Evaluate the integrals of the functions of standard normal variable
 *
 *
 */

using System;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  ///   Evaluate the integrals of the functions of standard normal variable
  /// </summary>
  /// <exclude />
  [Serializable]
  class NormalIntegral
  {
    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="numPoints">Number of quadrature points</param>
    /// <exclude />
    public NormalIntegral(int numPoints)
    {
      points_ = new double[numPoints];
      weights_ = new double[numPoints];
      Quadrature.Gamma(0.5, false, true, points_, weights_);
    }

    /// <summary>
    ///   Evaluate the right integral
    /// </summary>
    /// 
    /// <remarks>
    /// This method evaluates the integral
    /// <formula>
    ///   \int_{a}^{\infty} f(x,a) \frac{1}{\sqrt{2\pi}} e^{-\frac{1}{2}x^2} dx
    /// </formula>
    /// It uses an algorithm which preserves the differentiablity of the integral with respect to variable <c>a</c>.
    /// </remarks>
    /// 
    /// <param name="f">integrand function</param>
    /// <param name="a">lower bound</param>
    /// <returns>integral</returns>
    /// <exclude />
    public double Right(IBivariateFunction f, double a)
    {
      return (a >= 0.0 ? integral_0(f, a, 1) : integral_1(f, a, -1));
    }

    /// <summary>
    ///   Evaluate the left integral
    /// </summary>
    /// 
    /// <remarks>
    /// This method evaluates the integral
    /// <formula>
    ///   \int_{-\infty}^{b} f(x,b) \frac{1}{\sqrt{2\pi}} e^{-\frac{1}{2}x^2} dx
    /// </formula>
    /// It use an algorithm which preserves the differentiablity of the integral with respect to variable <c>b</c>.
    /// </remarks>
    /// 
    /// <param name="f">integrand function</param>
    /// <param name="b">upper bound</param>
    /// <returns>integral</returns>
    /// <exclude />
    public double Left(IBivariateFunction f, double b)
    {
      return (b >= 0.0 ? integral_1(f, b, 1) : integral_0(f, b, -1));
    }

    /// <summary>
    ///   Evaluate the complete integral
    /// </summary>
    /// 
    /// <remarks>
    /// This method evaluates the integral
    /// <formula>
    ///   \int_{-\infty}^{\infty} f(x) \frac{1}{\sqrt{2\pi}} e^{-\frac{1}{2}x^2} dx
    /// </formula>
    /// </remarks>
    /// 
    /// <param name="f">integrand function</param>
    /// <returns>integral</returns>
    /// <exclude />
    public double Complete(IUnivariateFunction f)
    {
      double sum = 0;
      for (int i = 0; i < points_.Length; ++i)
      {
        double twox = 2 * points_[i];
        double sqrt2x = Math.Sqrt(twox);
        double fv = f.evaluate(sqrt2x) + f.evaluate(-sqrt2x);
        sum += fv * weights_[i];
      }
      return 0.5 * sum;
    }

    private double integral_0(IBivariateFunction f, double a, int sign)
    {
      double a2 = a * a;
      double sum = 0;
      for (int i = 0; i < points_.Length; ++i)
      {
        double sqrt2xa2, sqrtratio;
        double twox = 2 * points_[i];
        if (twox < a2)
        {
          double twoxa = twox / a2;
          sqrt2xa2 = a * Math.Sqrt(1 + twoxa);
          sqrtratio = Math.Sqrt(twoxa / (1 + twoxa));
        }
        else
        {
          double atwox = a2 / twox;
          double sqrt1atwox = Math.Sqrt(1 + atwox);
          sqrt2xa2 = Math.Sqrt(twox) * sqrt1atwox;
          sqrtratio = 1 / sqrt1atwox;
        }
        sum += f.evaluate(sign * sqrt2xa2, a) * sqrtratio * weights_[i];
      }
      return 0.5 * sum * Math.Exp(-a2 / 2);
    }

    private double integral_1(IBivariateFunction f, double a, int sign)
    {
      double a2 = a * a;
      double sum = 0;
      for (int i = 0; i < points_.Length; ++i)
      {
        double twox = 2 * points_[i];
        double sqrt2x = Math.Sqrt(twox);
        double fv = f.evaluate(sqrt2x, a) + f.evaluate(-sqrt2x, a);
        if (a2 < 80)
        {
          double sqrt2xa2, sqrtratio;
          if (twox < a2)
          {
            double twoxa = twox / a2;
            sqrt2xa2 = a * Math.Sqrt(1 + twoxa);
            sqrtratio = Math.Sqrt(twoxa / (1 + twoxa));
          }
          else
          {
            double atwox = a2 / twox;
            double sqrt1atwox = Math.Sqrt(1 + atwox);
            sqrt2xa2 = Math.Sqrt(twox) * sqrt1atwox;
            sqrtratio = 1 / sqrt1atwox;
          }
          fv -= f.evaluate(sign * sqrt2xa2, a) * sqrtratio * Math.Exp(-a2 / 2);
        }
        sum += fv * weights_[i];
      }
      return 0.5 * sum;
    }

    private double[] points_;
    private double[] weights_;
  }
}
