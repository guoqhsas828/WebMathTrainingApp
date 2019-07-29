using System;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;

// ReSharper disable once CheckNamespace
namespace BaseEntity.Toolkit.Numerics.Integrals
{
  static class HalfOpenIntegration
  {
    #region Calculate half-open normal integrals

    /// <summary>
    ///  Calculate <m>\mathrm{E}\!\left[I_{\{S \gt K\}}\,f(S)\right]</m>
    ///  where <m>S \sim N(\mu, \sigma^2)</m> is normal variable.
    /// </summary>
    /// <param name="mu">The mean od <m>S</m></param>
    /// <param name="k">The strike value</param>
    /// <param name="sigma">The volatility</param>
    /// <param name="fn">The function taking <m>S</m> as the single argument</param>
    /// <returns></returns>
    internal static double NormalRightIntegral(
      double mu, double k, double sigma,
      Func<double, double> fn)
    {
      var scale = ScaleValues(ref mu, ref k, ref sigma);

      if (sigma < -1E-15)
      {
        throw new ToolkitException("Volatility cannot be nagative");
      }

      if (sigma < 1E-15)
      {
        return fn(scale*mu);
      }

      //! Note that <m>
      //!   S = \sigma X + \mu
      //! </m>,
      //! Hence <m>S > k</m> implies<math>
      //!   X \gt \frac{k - \mu}{\sigma}
      //!   \equiv b
      //! </math>
      var b = (k - mu)/sigma;
      CalculateNormalRightMoments(b, out var m0, out var m1, out var m2, out var m3);
      CalculateQuadrature(m0, m1, m2, m3, out var w1, out var w2, out var x1, out var x2);
      return w1*fn(scale*(sigma*x1 + mu)) + w2*fn(scale*(sigma*x2 + mu));
    }


    /// <summary>
    ///  Calculate <m>\mathrm{E}\!\left[I_{\{S \lt K\}}\,f(S)\right]</m>
    ///  where <m>S \sim N(\mu, \sigma^2)</m> is normal variable.
    /// </summary>
    /// <param name="mu"></param>
    /// <param name="k"></param>
    /// <param name="sigma"></param>
    /// <param name="fn"></param>
    /// <returns></returns>
    internal static double NormalLeftIntegral(
      double mu, double k, double sigma,
      Func<double, double> fn)
    {
      var scale = ScaleValues(ref mu, ref k, ref sigma);

      if (sigma < -1E-15)
      {
        throw new ToolkitException("Volatility cannot be nagative");
      }

      if (sigma < 1E-15)
      {
        return fn(scale*mu);
      }

      //! Note that <m>
      //!   S = \sigma X + \mu
      //! </m>,
      //! Hence <m>S > k</m> implies<math>
      //!   X \gt \frac{k - \mu}{\sigma}
      //!   \equiv b
      //! </math>
      var b = (k - mu)/sigma;
      CalculateNormalLeftMoments(b, out var m0, out var m1, out var m2, out var m3);
      CalculateQuadrature(m0, m1, m2, m3, out var w1, out var w2, out var x1, out var x2);
      return w1*fn(scale*(sigma*x1 + mu)) + w2*fn(scale*(sigma*x2 + mu));
    }

    /// <summary>
    ///  Scales parameters such that the largest pf <c>forward</c> and <c>strike</c>
    ///  is normalized to be 1.
    /// </summary>
    /// <param name="forward"></param>
    /// <param name="strike"></param>
    /// <param name="sigma"></param>
    /// <returns></returns>
    private static double ScaleValues(
      ref double forward, ref double strike, ref double sigma)
    {
      double scale = 1;
      if (Math.Abs(forward) > Math.Abs(strike))
      {
        scale = Math.Abs(forward);
        forward /= scale;
        strike /= scale;
        sigma /= scale;
      }
      else if (Math.Abs(strike) > 0)
      {
        scale = Math.Abs(forward);
        forward /= scale;
        strike /= scale;
        sigma /= scale;
      }

      return scale;
    }

    #endregion

    #region Calculate half-open normal moments

    /// <summary>
    ///  Calculate the moments <m>E\left[I_{\{Z \gt b\}}Z^n\right]</m>
    ///  for <m>n = 0, 1, 2, 3</m>.
    /// </summary>
    /// <remarks>
    ///  For right integrals<math>\begin{align}
    ///   m_0 &amp;= \frac{1}{\sqrt{2\pi}}\int_b^\infty e^{-x^2/2} dx = \Phi(-b)
    ///   %
    ///   \\ m_1 &amp;= \frac{1}{\sqrt{2\pi}}\int_b^\infty x e^{-x^2/2} dx
    ///   \\  &amp;= -\frac{1}{\sqrt{2\pi}}\int_b^\infty  d\left(e^{-x^2/2}\right)
    ///   \\  &amp;= \frac{1}{\sqrt{2\pi}}e^{-b^2/2} = \phi(b)
    ///   %
    ///   \\ m_2 &amp;= \frac{1}{\sqrt{2\pi}}\int_b^\infty x^2 e^{-x^2/2} dx
    ///   \\  &amp;= -\frac{1}{\sqrt{2\pi}}\int_b^\infty x\,d\left(e^{-x^2/2}\right)
    ///   \\  &amp;= \frac{b}{\sqrt{2\pi}}e^{-b^2/2}
    ///     + \frac{1}{\sqrt{2\pi}}\int_b^\infty e^{-x^2/2} dx
    ///   \\  &amp;= b\,\phi(b) + \Phi(-b)
    ///   %
    ///   \\ m_3 &amp;= \frac{1}{\sqrt{2\pi}}\int_b^\infty x^3 e^{-x^2/2} dx
    ///   \\  &amp;= -\frac{1}{\sqrt{2\pi}}\int_b^\infty x^2\,d\left(e^{-x^2/2}\right)
    ///   \\  &amp;= \frac{b^2}{\sqrt{2\pi}}e^{-b^2/2}
    ///     + \frac{2}{\sqrt{2\pi}}\int_b^\infty x\,e^{-x^2/2} dx
    ///   \\  &amp;= (b^2 + 2)\phi(b)
    /// \end{align}</math>
    /// </remarks>
    internal static void CalculateNormalRightMoments(double b,
      out double m0, out double m1, out double m2, out double m3)
    {
      var pdf = SpecialFunctions.NormalPdf(b);
      var cdf = m0 = SpecialFunctions.NormalCdf(-b);
      m1 = pdf;
      m2 = b*pdf + cdf;
      m3 = (b*b + 2)*pdf;
    }

    /// <summary>
    ///  Calculate the moments <m>E\left[I_{\{Z \lt b\}}Z^n\right]</m>
    ///  for <m>n = 0, 1, 2, 3</m>.
    /// </summary>
    /// <remarks>
    /// Similarly, for left integrals<math>\begin{align}
    ///   m_0 &amp;= \frac{1}{\sqrt{2\pi}}\int_{-\infty}^b e^{-x^2/2} dx = \Phi(b)
    ///   %
    ///   \\ m_1 &amp;= \frac{1}{\sqrt{2\pi}}\int_{-\infty}^b x e^{-x^2/2} dx = -\phi(b)
    ///   %
    ///   \\ m_2 &amp;= \frac{1}{\sqrt{2\pi}}\int_b^\infty x^2 e^{-x^2/2} dx
    ///          = \Phi(b) - b\,\phi(b)
    ///   %
    ///   \\ m_3 &amp;= \frac{1}{\sqrt{2\pi}}\int_b^\infty x^3 e^{-x^2/2} dx
    ///     = -(b^2 + 2)\phi(b)
    /// \end{align}</math>
    /// </remarks>
    internal static void CalculateNormalLeftMoments(double b,
      out double m0, out double m1, out double m2, out double m3)
    {
      var pdf = SpecialFunctions.NormalPdf(b);
      var cdf = m0 = SpecialFunctions.NormalCdf(b);
      m1 = -pdf;
      m2 = cdf - b*pdf;
      m3 = -(b*b + 2)*pdf;
    }

    #endregion

    #region Calculate quadrature weights and points

    /// <summary>
    ///  Two points quadrature
    /// </summary>
    /// <remarks>
    /// Two points quadrature is obtained by solving the equation system
    /// <math>\begin{align}
    ///     w_1 + w_2 &amp;= m_0
    ///  \\ w_1 x_1 + w_2 x_2 &amp;= m_1
    ///  \\ w_1 x_1^2 + w_2 x_2^2 &amp;= m_2
    ///  \\ w_1 x_1^3 + w_2 x_2^3 &amp;= m_3
    /// \end{align}</math>
    /// which transforms into
    /// <math>\begin{align}
    ///     w_1 + w_2 &amp;= m_0
    ///  \\ w_1 (x_2 - x_1) &amp;= m_0 x_2 - m_1
    ///  \\ w_1 (x_2 - x_1) x_1 &amp;= m_1 x_2 - m_2
    ///  \\ w_1 (x_2 - x_1) x_1^2 &amp;= m_2 x_2 - m_3
    /// \end{align}</math>
    /// which in turn yields
    /// <math>
    ///   x_1 = \frac{m_1 x_2 - m_2}{m_0 x_2 - m_1}
    ///       = \frac{m_2 x_2 - m_3}{m_1 x_2 - m_2}
    /// </math>
    /// <math>
    ///   m_1^2x_2^2 - 2 m_1 m_2 x_2 + m_2^2
    ///    = m_0 m_2 x_2^2 - (m_0 m_3 + m_1 m_2)x_2 + m_1 m_3
    /// </math>
    /// This reduce to <m>ax^2 + bx + c =0</m>, where
    /// <math>\begin{align}
    ///   a &amp;= m_1^2 - m_0 m_2
    /// \\ b &amp;= m_0 m_3 - m_1 m_2
    /// \\ c &amp;= m_2^2 - m_1 m_3
    /// \end{align}</math>
    /// When <m>a \neq 0</m>, the solution is<math>
    ///  x_{1,2} = \frac{-b \pm \sqrt{b^2 - 4ac}}{2a}
    /// </math><math>
    ///  w_1 = \frac{m_0 x_2 - m_1}{x_2 - x_1}
    /// </math>
    /// When <m>a = 0</m>, we pick the solution<math>
    ///  x_1 = x_2 = -c/b
    ///  ,\quad w_1 = m_0
    ///  ,\quad w_2 = 0
    /// </math>
    /// </remarks>
    internal static void CalculateQuadrature(
      double m0, double m1, double m2, double m3,
      out double w1, out double w2, out double x1, out double x2)
    {
      var c = m2*m2 - m1*m3;
      var b = m0*m3 - m1*m2;
      var a = m1*m1 - m0*m2;
      if (Math.Abs(a) > Math.Abs(b))
      {
        b /= a;
        c /= a;
        a = 1;
      }
      else // the case |a| <= |b|
      {
        a /= b;
        if (a.AlmostEquals(0.0))
        {
          x1 = x2 = -c/b;
          w1 = m0;
          w2 = 0;
          return;
        }

        c /= b;
        b = 1;
      }


      var d = b*b - 4*a*c;
      if (d < -1E-15)
      {
        throw new SolverException(
          "No real solution found in " + nameof(CalculateQuadrature));
      }

      if (d < 1E-15)
      {
        x1 = x2 = -b/a/2;
        w1 = m0;
        w2 = 0;
        return;
      }

      d = Math.Sqrt(d);
      x1 = (-b - d)/a/2;
      x2 = (-b + d)/a/2;
      w1 = (m0*x2 - m1)*a/d;
      w2 = m0 - w1;
      return;
    }

    #endregion
  }
}
