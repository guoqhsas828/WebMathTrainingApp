//
// Copyright (c)    2002-2018. All rights reserved.
//
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Numerics.Integrals;

using BaseEntity.Toolkit.Base;

// ReSharper disable once CheckNamespace
namespace BaseEntity.Toolkit.Tests.Numerics
{
  using NUnit.Framework;

  [TestFixture]
  public class TestLogNormalIntegral
  {
    /// <summary>
    ///  <math>\begin{align}
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
    /// </summary>
    private static IEnumerable<double[]> TwoPointQuadratureParameters
    {
      get
      {
        yield return new[] {1.0, 0.0, 1.0, 0.0};
        for (int i = -20; i <= 20; ++i)
        {
          double b = i/20.0,
            pdf = SpecialFunctions.NormalPdf(b),
            cdf = SpecialFunctions.NormalCdf(-b);
          yield return new[]
          {
            cdf, pdf, b*pdf + cdf, (b*b + 2)*pdf
          };
        }
      }
    }


    /// <summary>
    ///  Test integral against Black-Scholes formula for call option
    ///  <m>(S - k)^+</m> where <m>S</m> is a log-normal variable
    ///  with mean <m>s</m> and volatility <m>\sigma</m>.
    /// </summary>
    [TestCase(1.2, 1, 0.4)]
    [TestCase(1.0, 1, 0.4)]
    [TestCase(0.8, 1, 0.4)]
    public static void TestBlackScholes(double s, double k, double sigma)
    {
      //! Let <m>X</m> be a standard log-normal variable with volatility
      //! <m>\sigma</m>, as defined by<math>
      //!   X = \exp(\sigma Z)
      //!   ,\qquad Z \sim N(0,1)
      //! </math>
      //! Since <m>S</m> is log-normal with mean <m>s</m>, we have<math>\begin{align}
      //!  S &amp;= s \exp\!\left(\sigma Z - \frac{1}{2}\sigma^2\right)
      //!   = s e^{-\frac{1}{2}\sigma^2} X = c\,X
      //!   \\ &amp;\text{where}\quad c \equiv s e^{-\frac{1}{2}\sigma^2}
      //! \end{align}</math>
      //! In addition, <m>S > K</m> implies<math>
      //!  z > \frac{\sigma}{2} + \frac{\log(K/S)}{\sigma} \equiv d
      //! </math>
      //! Hence we have the following calculation.<p></p>
      var d = sigma/2 + Math.Log(k/s)/sigma;
      var c = s*Math.Exp(-sigma*sigma/2);
      var quad = new LogNormal(d, sigma);
      var actual = quad.RightIntegral(x =>
      {
        var S = c*x;
        return (S - k);
      });
      var expect = SpecialFunctions.Black(s, k, sigma);
      NUnit.Framework.Assert.AreEqual(expect, actual, 1E-15);
    }

    [TestCase(0.025, 0.02, 0.4)]
    [TestCase(0.025, 0.025, 0.4)]
    [TestCase(0.025, 0.015, 0.4)]
    public static void CashSwaption(
      double swapRate, double strike, double sigma)
    {
      const Frequency freq = Frequency.Annual;
      var market = CashAnnuity(swapRate, freq)*SpecialFunctions.Black(
        swapRate, strike, sigma);

      var d = sigma/2 + Math.Log(strike/swapRate)/sigma;
      var c = swapRate*Math.Exp(-sigma*sigma/2);
      var quad = new LogNormal(d, sigma);
      var model = Annuity(swapRate, freq)*quad.RightIntegral(x =>
      {
        var s = c*x;
        return (CashAnnuity(s, freq)/Annuity(s, freq))*(s - strike);
      });
      NUnit.Framework.Assert.AreEqual(market, model, 1E-6);

      CalculateLinearTsr(swapRate, freq, out var alpha0, out var alpha1);
      var tsr = Annuity(swapRate, freq)*quad.RightIntegral(x =>
      {
        var s = c*x;
        return (alpha0*s + alpha1)*CashAnnuity(s, freq)*(s - strike);
      });
      NUnit.Framework.Assert.AreEqual(market, tsr, 1E-6);
    }

    private static double CashAnnuity(double rate, Frequency freq)
    {
      var frac = 1.0/(int)freq;
      return frac/(1 + frac*rate);
    }

    private static double Annuity(double rate, Frequency freq)
    {
      var effective = Dt.Add(Dt.Today(), "1Y");
      var maturity = Dt.Add(effective, freq, 1, true);
      var frac = Dt.Fraction(effective, maturity, DayCount.Actual365Fixed);
      return frac / (1 + frac * rate);
    }

    private static void CalculateLinearTsr(
      double rate, Frequency freq,
      out double alpha0, out double alpha1)
    {
      var effective = Dt.Add(Dt.Today(), "1Y");
      var maturity = Dt.Add(effective, freq, 1, true);
      var frac = Dt.Fraction(effective, maturity, DayCount.Actual365Fixed);
      var A0 = frac/(1 + frac*rate);
      alpha1 = 1/frac;
      alpha0 = (1/rate)*(1/A0 - alpha1);
    }

    //TODO: It does not work with sigma >= 500%
    [TestCase(-1E8, 2.5)]
    [TestCase(1E8, 2.5)]
    [TestCase(-1E8, 1E-10)]
    [TestCase(1E8, 1E-8)]
    [TestCase(-1E8, 1)]
    [TestCase(1E8, 1)]
    [TestCase(0.6, 1.0)]
    [TestCase(0.48, 0.8)]
    [TestCase(0.3, 0.5)]
    public static void TestMoment(double d, double sigma)
    {
      const double tol = 0.02;
      var quad = new LogNormal(d, sigma);
      var one = quad.Integral(x => 1);
      Assert.AreEqual(1.0, one, tol);

      // integral of f(x) = x
      {
        var coef = Math.Exp(-LogMoment(sigma));
        var mean = quad.Integral(x => x) * coef;
        Assert.AreEqual(1.0, mean, tol);

        var left = quad.LeftIntegral(x => x) * coef;
        var leftf = LeftFactor(d, sigma);
        Assert.AreEqual(leftf, left, tol);

        var right = quad.RightIntegral(x => x) * coef;
        var rightf = RightFactor(d, sigma);
        Assert.AreEqual(rightf, right, tol);

        Assert.AreEqual(1.0, left + right, tol);
      }

      // integral of f(x) = x^2
      {
        var coef = Math.Exp(-LogMoment(2 * sigma));
        var mean = quad.Integral(x => x * x) * coef;
        Assert.AreEqual(1.0, mean, tol);

        var left = quad.LeftIntegral(x => x * x) * coef;
        var leftf = LeftFactor(d, 2 * sigma);
        Assert.AreEqual(leftf, left, tol);

        var right = quad.RightIntegral(x => x * x) * coef;
        var rightf = RightFactor(d, 2 * sigma);
        Assert.AreEqual(rightf, right, tol);

        Assert.AreEqual(1.0, left + right, tol);
      }

      // integral of f(x) = x^3
      {
        var coef = Math.Exp(-LogMoment(3 * sigma));
        var mean = quad.Integral(x => x * x * x) * coef;
        Assert.AreEqual(1.0, mean, tol);

        var left = quad.LeftIntegral(x => x * x * x) * coef;
        var leftf = LeftFactor(d, 3 * sigma);
        Assert.AreEqual(leftf, left, tol);

        var right = quad.RightIntegral(x => x * x * x) * coef;
        var rightf = RightFactor(d, 3 * sigma);
        Assert.AreEqual(rightf, right, tol);

        Assert.AreEqual(1.0, left + right, tol);
      }

      return;
    }

    static double LogMoment(double sigma)
    {
      return 0.5 * sigma * sigma;
    }

    /// <summary>
    ///  Let <math>
    ///    X = e^{\sigma Z - \frac{\sigma^2}{2}}</math>
    ///  this function calculates<math>
    ///   E[I_{\{Z \lt b\}} X]
    ///   = \int_{-\infty}^b e^{\sigma z - \frac{\sigma^2}{2}}\frac{1}{\sqrt{2\pi}}e^{-\frac{z^2}{2}}
    ///   = \int_{-\infty}^b \frac{1}{\sqrt{2\pi}\sigma}e^{-\frac{(z-\sigma)^2}{2}}
    ///  </math>
    /// </summary>
    /// <param name="b"></param>
    /// <param name="sigma"></param>
    /// <returns></returns>
    static double LeftFactor(double b, double sigma)
    {
      return SpecialFunctions.NormalCdf(b - sigma);
    }

    static double RightFactor(double b, double sigma)
    {
      return SpecialFunctions.NormalCdf(sigma - b);
    }
  }
}
