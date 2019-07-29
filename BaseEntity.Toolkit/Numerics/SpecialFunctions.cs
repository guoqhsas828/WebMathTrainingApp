/*
 * SpecialFunctions.cs
 *
 *  -2010. All rights reserved.
 *
 */
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BaseEntity.Toolkit.Numerics
{
  /// <summary>
  ///  Special mathematical functions.
  /// </summary>
  public static class SpecialFunctions
  {
    #region Simple utilities

    /// <summary>
    /// Calculate the function
    ///  <m>\displaystyle\mathrm{expd1}(x) = \frac{e^x - 1}{x}</m>
    /// </summary>
    /// <param name="x">The x value</param>
    /// <returns>System.Double.</returns>
    public static double Expd1(double x)
    {
      if (Math.Abs(x) < 1E-3)
        return 1 + x*(1 + x*(1 + x*(1 + x/5)/4)/3)/2;
      return (Math.Exp(x) - 1)/x;
    }

    /// <summary>
    /// Calculate the function
    /// <m>\displaystyle\mathrm{powd1}(x) = \frac{(1+x)^\beta - 1}{x}</m>
    /// for <m>x \geq 0</m>.
    /// </summary>
    /// <param name="x">The x value</param>
    /// <param name="beta">The beta.</param>
    /// <returns>System.Double.</returns>
    public static double Powd1(double x, double beta)
    {
      if (Math.Abs(x) < 1E-3)
        return beta*(1 + (beta - 1)*x*(1 + (beta - 2)*x*(1 + (beta - 3)*x*(1 + (beta - 4)*x/5)/4)/3)/2);
      return (Math.Pow(1 + x, beta) - 1)/x;
    }

    /// <summary>
    /// Calculate the function
    ///  <m>\displaystyle\mathrm{expd2}(x) = \frac{e^x - 1 - x}{x^2}</m>
    /// </summary>
    /// <param name="x">The x value</param>
    /// <returns>System.Double.</returns>
    public static double Expd2(double x)
    {
      if (Math.Abs(x) < 1E-3)
      {
        //! Use the expansion <math>
        //!  e^x = 1 + x + \frac{x^2}{2!} + \frac{x^3}{3!} + \frac{x^4}{4!}
        //!    + \frac{x^5}{5!} + \frac{x^6}{6!}
        //! </math>
        return (1 + x*(1 + x*(1 + x*(1 + x/6)/5)/4)/3)/2;
      }
      return (Math.Exp(x) - 1 - x)/(x*x);
    }

    /// <summary>
    /// Calculate the function
    ///  <math>\displaystyle\mathrm{d}_2(x_1, x_2) = \frac{
    ///    1 - \mathrm{d}_1(x_1) - \mathrm{d}_1(x_2) 
    ///    + \mathrm{d}_1(x_1 + x_2)}{x_1 x_2}
    ///  </math>
    /// where <m>\displaystyle\mathrm{d}_1(x) = \frac{x^x - 1}{x}</m>
    /// </summary>
    /// <param name="x1">The x1 value</param>
    /// <param name="x2">The x2 value</param>
    /// <returns>System.Double.</returns>
    public static double Expd2(double x1, double x2)
    {
      var x12 = x1*x2;
      if (Math.Abs(x12) < 1E-9)
      {
        return (1.0/3 + (x1 + x2)/8);
      }
      return (1 - Expd1(x1) - Expd1(x2) + Expd1(x1 + x2))/x12;
    }

    #endregion

    #region Gamma, beta and related functions

    /// <summary>
    ///  Calculate the hypergeometric probability.
    /// </summary>
    /// <remarks>
    ///  The function is given by <math>
    ///  H(n_1,k_1,n_2,k_2) = \frac{\displaystyle\binom{n_1}{k_1}\binom{n_2}{k_2}}
    ///  {\displaystyle\binom{n_1+n_2}{k_1+k_2}}</math>
    /// </remarks>
    /// <param name="n1">The n1.</param>
    /// <param name="k1">The k1.</param>
    /// <param name="n2">The n2.</param>
    /// <param name="k2">The k2.</param>
    /// <returns>The hypergeometric probability.</returns>
    public static double HypergeometricPdf(
      uint n1, uint k1, uint n2, uint k2)
    {
      // make sure n1 <= n2
      if (n2 < n1)
      {
        // swap
        uint tmp = n1;
        n1 = n2;
        n2 = tmp;
        tmp = k1;
        k1 = k2;
        k2 = tmp;
      }
      if (n1 == 1)
      {
        if (k1 > 1) return 0;
        return (k1 == 0 ? (n2 + 1 - k2) : k2)
               / (n2 + 1.0);
      }
      return Math.Exp(LogBinom(n1, k1)
                      + LogBinom(n2, k2)
                      - LogBinom(n1 + n2, k1 + k2));
    }

    private static double LogBinom(uint n, uint k)
    {
      if (k > n)
        return Double.NaN;
      if (k == 0 || k == n)
        return 0;
      int sign;
      return Math.Log(((double)n) / k / (n - k))
             - LogBeta(k, n - k, out sign);
    }

    /// <summary>
    ///  Calculate the combinations picking <c>k</c> items
    ///  from <c>n</c>.
    /// </summary>
    /// <remarks>
    ///  The function is given by <math>
    ///  \binom{n}{k} = \frac{n!}{k!(n-k)!}</math>
    /// </remarks>
    /// <param name="n">The n.</param>
    /// <param name="k">The k.</param>
    /// <returns>Combinations.</returns>
    public static double Binom(uint n, uint k)
    {
      if (k > n)
        return Double.NaN;
      if (k == 0 || k == n)
        return 1;
      return ((double)n) / k / (n - k) / Beta(k, n - k);
    }


    /// <summary>
    ///  Calculate the cumulative probability of binomial distribution.
    /// </summary>
    /// <remarks>
    ///   The function is given by <math>
    ///   \displaystyle P(k;n,p)=\sum_{j=0}^k \binom{n}{j}\,p^j\,(1-p)^{n-j}</math>
    /// </remarks>
    /// <param name="k">The k.</param>
    /// <param name="n">The n.</param>
    /// <param name="p">The p.</param>
    /// <returns>The cumulative probability.</returns>
    [DllImport("MagnoliaIGNative", EntryPoint = "binomial_cdf", CallingConvention = CallingConvention.Cdecl)]
    public static extern double BinomialCdf(uint k, uint n, double p);

    /// <summary>
    ///  Calculate the binomial probability.
    /// </summary>
    /// <remarks>
    ///   The function is given by <math>
    ///   \displaystyle P(k;n,p)=\sum_{j=0}^k \binom{n}{j}\,p^j\,(1-p)^{n-j}</math>
    /// </remarks>
    /// <param name="k">The k.</param>
    /// <param name="n">The n.</param>
    /// <param name="p">The p.</param>
    /// <returns>The probability.</returns>
    [DllImport("MagnoliaIGNative", EntryPoint = "binomial_pdf", CallingConvention = CallingConvention.Cdecl)]
    public static extern double BinomialPdf(uint k, uint n, double p);

    /// <summary>
    ///  Caculate the beta function.
    /// </summary>
    /// <remarks>
    ///   The function is given by <math>
    ///   \displaystyle B(x, y) = \int_{0}^{1}\,t^{x-1}\,(1-t)^{y-1}\,dt</math>
    /// </remarks>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <returns>THe beta function value.</returns>
    [DllImport("MagnoliaIGNative", EntryPoint = "beta", CallingConvention = CallingConvention.Cdecl)]
    public static extern double Beta(double x, double y);

    /// <summary>
    ///  Caculate the logarithm of beta function.
    /// </summary>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <param name="sign">An integer receive the sign.</param>
    /// <returns>THe beta function value.</returns>
    [DllImport("MagnoliaIGNative", EntryPoint = "log_beta", CallingConvention = CallingConvention.Cdecl)]
    public static extern double LogBeta(double x, double y, out int sign);

    /// <summary>
    ///  Caculate the value gamma function, <m>\displaystyle\Gamma(x)=\int_0^\infty t^{x-1}e^{-t}\,dt</m>.
    /// </summary>
    /// <param name="x">The x.</param>
    /// <returns>THe gamma function value.</returns>
    [DllImport("MagnoliaIGNative", EntryPoint = "gamma", CallingConvention = CallingConvention.Cdecl)]
    public static extern double Gamma(double x);

    /// <summary>
    ///  Caculate the logarithm of gamma function, <m>\log|\Gamma(x)|</m>.
    /// </summary>
    /// <param name="x">The x.</param>
    /// <param name="sign">An integer receive the sign.</param>
    /// <returns>THe gamma function value.</returns>
    [DllImport("MagnoliaIGNative", EntryPoint = "log_gamma", CallingConvention = CallingConvention.Cdecl)]
    public static extern double LogGamma(double x, out int sign);

    /// <summary>
    /// Incomplete gamma integral
    ///  <m>\displaystyle\Gamma(a, x) = \frac{1}{\Gamma(a)}
    ///   \int_0^{x}\,e^{-t}\,t^{a-1}\,dt</m>.
    /// </summary>
    /// <param name="a">The a.</param>
    /// <param name="x">The x.</param>
    /// <returns>THe gamma function value.</returns>
    [DllImport("MagnoliaIGNative", EntryPoint = "gamma_incomplete", CallingConvention = CallingConvention.Cdecl)]
    public static extern double GammaCdf(double a, double x);

    /// <summary>
    /// Complemented incomplete gamma integral
    ///  <m>\displaystyle\Gamma^c(a, x) = \frac{1}{\Gamma(a)}
    ///   \int_{x}^{\infty}\,e^{-t}\,t^{a-1}\,dt</m>.
    /// </summary>
    /// <param name="a">The a.</param>
    /// <param name="x">The x.</param>
    /// <returns>THe gamma function value.</returns>
    [DllImport("MagnoliaIGNative", EntryPoint = "gamma_incomplete_c", CallingConvention = CallingConvention.Cdecl)]
    public static extern double GammaTail(double a, double x);

    #endregion

    #region Black-Scholes and related

    /// <summary>
    ///  Calculate the call option value based on
    ///  the Black-Scholes model with log-normal distribution.
    /// </summary>
    /// <param name="s">The forward value</param>
    /// <param name="k">The strike value</param>
    /// <param name="sigma">The volatility</param>
    /// <returns>System.Double.</returns>
    public static double Black(double s, double k, double sigma)
    {
      if (!(s > 0))
      {
        if (s.Equals(0.0))
        {
          return k >= 0 ? 0.0 : -k;
        }
        if (!(s < 0))
        {
          return double.NaN;
        }
        if (k >= 0) return 0.0;
        var tmp = -s;
        s = -k;
        k = tmp;
      }
      if (!(k > 0))
      {
        return k <= 0 ? (s - k) : double.NaN;
      }
      Debug.Assert(s > 0 && k > 0);

      if (sigma.Equals(0))
        return s > k ? (s - k) : 0;
      Debug.Assert(sigma > 0);

      //return s*bs_call(k/s, sigma);
      double d = Math.Log(s/k)/sigma + sigma/2;
      double result = s*NormalCdf(d) - k*NormalCdf(d - sigma);
      return result;
    }

    /// <summary>
    ///  Calculate the call option value based on
    ///  the Black-Scholes model with normal distribution.
    /// </summary>
    /// <param name="s">The forward value</param>
    /// <param name="k">The strike value</param>
    /// <param name="sigma">The volatility</param>
    /// <returns>System.Double.</returns>
    internal static double BlackNormal(double s, double k, double sigma)
    {
      return bs_normal_call(k - s, sigma);
    }

    /// <summary>
    ///  Calculate the implied volatility based on
    ///  the Blacks model with normal distribution.
    /// </summary>
    /// <param name="price">The option value</param>
    /// <param name="s">The forward value</param>
    /// <param name="k">The strike value</param>
    /// <returns>The implied volatility</returns>
    internal static double BlackNormalImpliedVolatility(
      double price, double s, double k)
    {
      return bs_normal_implied_volatility(price, k - s);
    }

    /// <summary>
    /// Calculate the Black-Scholes implied volatility.
    /// </summary>
    /// <param name="isCall">if set to <c>true</c> [is call].</param>
    /// <param name="price">The normalized call option price (price divided by the ATM forward).</param>
    /// <param name="moneyness">The moneyness (strike divided by the ATM forward).</param>
    /// <param name="tolerance">The tolerance.</param>
    /// <returns>The implied total volatility.</returns>
    /// <remarks>Given the call price <m>c</m>, the moneyness <m>m</m>, and the tolerance <m>\epsilon</m>
    /// this function finds a volatility <m>v</m> such that<math>
    /// |\Phi(-\log(m)/v + v/2)-m\Phi(-\log(m)/v - v/2) - c| &lt; \epsilon
    /// </math>
    /// <para>For a regular Black-Scholes model with expiry <m>T</m>, spot <m>S</m>, strike <m>K</m>,
    /// interest rate <m>r</m>, divident yield <m>d</m>, and call price <m>{\cal C}</m>,
    /// the conversion is given by<math>
    /// m = Ke^{-rT}/(Se^{-dT}),\quad c = {\cal C}/(Se^{-dT}),\quad v = \sigma\sqrt{T}
    /// </math>
    /// This function is based on closed-form approximation, suplemented by iterative improvements
    /// to achieve the desired accuracy.  The implementation is about 3 times faster than pure iterative
    /// procedures such as <c>Newton-Raphson</c> or <c>Brent</c> solvers to achieve the the same accuracy.</para>
    /// <para>If the parameter <c>tolerance</c> is zero, then the machine epsilon is used as the desired accuracy.</para></remarks>
    public static double BlackScholesImpliedVolatility(bool isCall, double price, double moneyness, double tolerance)
    {
      double c = isCall ? price : (1 - moneyness + price);
      return c < -tolerance ? Double.NaN : bs_implied_volatility(c, moneyness, tolerance, IntPtr.Zero);
    }


    [DllImport("MagnoliaIGNative", EntryPoint = "bs_call", CallingConvention = CallingConvention.Cdecl)]
    private static extern double bs_call(double m, double sigma);

    [DllImport("MagnoliaIGNative", CallingConvention = CallingConvention.Cdecl)]
    private static extern double bs_implied_volatility(double c, double m, double tolerance, IntPtr iter);

    [DllImport("MagnoliaIGNative", CallingConvention = CallingConvention.Cdecl)]
    private static extern double bs_normal_implied_volatility(double price, double m);


    [DllImport("MagnoliaIGNative", CallingConvention = CallingConvention.Cdecl)]
    private static extern double bs_normal_call(double m, double sigma);

    #endregion

    /// <summary>
    ///   Cumulative probability of a non-central student t variable
    /// </summary>
    /// <param name="t">The level at which to calcculate the cumulative probability.</param>
    /// <param name="df">The degrees of freedom.</param>
    /// <param name="delta">The noncentrality parameter.</param>
    /// <returns>The cumulative probability at the given level</returns>
    public static double NonCentralStudentTCdf(double t, double df, double delta)
    {
      return noncentral_student_t_cdf_ex(t, df, delta, 1E-12, 50000);
    }

    /// <summary>
    ///   Cumulative probability of a non-central student t variable
    /// </summary>
    /// <param name="t">The level at which to calcculate the cumulative probability.</param>
    /// <param name="df">The degrees of freedom.</param>
    /// <param name="delta">The noncentrality parameter.</param>
    /// <param name="errmax">The maximum error allowed.</param>
    /// <param name="itermax">The maximum iterations allowed.</param>
    /// <returns>The cumulative probability at the given level</returns>
    [DllImport("MagnoliaIGNative", CallingConvention = CallingConvention.Cdecl)]
    internal static extern double noncentral_student_t_cdf_ex(
      double t, double df, double delta, double errmax, int itermax);

    /// <summary>
    /// Caculate the standard normal distribution function.
    /// </summary>
    /// <param name="x">The level.</param>
    /// <returns>The probability that <formula inline="true">\Pr[X &lt; x]</formula>.</returns>
    public static double NormalCdf(double x)
    {
      return Double.IsNaN(x) ? x : NativeNormalCdf(x);
    }

    [DllImport("MagnoliaIGNative", EntryPoint = "normal_cdf", CallingConvention = CallingConvention.Cdecl)]
    private static extern double NativeNormalCdf(double x);

    /// <summary>
    /// Caculate the inverse of the standard normal distribution function.
    /// </summary>
    /// <param name="p">The probability level.</param>
    /// <returns>The level that <formula inline="true">\Pr[X &lt; x]=p</formula>.</returns>
    [DllImport("MagnoliaIGNative", EntryPoint = "normal_inverse_cdf", CallingConvention = CallingConvention.Cdecl)]
    public static extern double NormalInverseCdf(double p);


    /// <summary>
    ///  The normal probability densitity function.
    /// </summary>
    /// <param name="x">The x.</param>
    public static double NormalPdf(double x)
    {
      return Math.Exp(-x*x/2)/Sqrt2Pi;
    }
    private static readonly double Sqrt2Pi = Math.Sqrt(2 * Math.PI);

    /// <summary>
    /// Bivariate normal tail probability function.
    /// </summary>
    /// <param name="u">The first variable.</param>
    /// <param name="v">The second variable.</param>
    /// <param name="r">The correlation between two variables.</param>
    /// <returns>The tail probability.</returns>
    /// <remarks>
    ///  The tail probability of the standard bivariate normal distribution.
    ///  <math> P(u, v, r)= \frac{1}{2\pi\sqrt{1-r^2}}
    ///   \int_{u}^{\infty} \int_{v}^{\infty}
    ///    \exp\left(-\frac{x^2+y^2-2rxy}{2(1-r^2)}\right)
    ///  dx\,dy</math>
    /// </remarks>
    [DllImport("MagnoliaIGNative", EntryPoint = "bivariate_normal_tail", CallingConvention = CallingConvention.Cdecl)]
    public static extern double BivariateNormalTail(double u, double v, double r);

    /// <summary>
    /// Bivariates normal cumulative distribution function.
    /// </summary>
    /// <param name="u">The first variable.</param>
    /// <param name="v">The second variable.</param>
    /// <param name="r">The correlation between two variables.</param>
    /// <returns>The cumulative probability.</returns>
    /// <remarks></remarks>
    public static double BivariateNormalCdf(double u, double v, double r)
    {
      return BivariateNormalTail(-u, -v, r);
    }

    /// <summary>
    ///  Calculate the multivariate Normal probability, <m>\Pr(\mathbf{b} \leq \mathbf{X} \leq \mathbf{u})</m>,
    ///  where <m>\mathbf{X}</m> follows multivariate Normal distribution with variance 1
    ///  and correlation matrix <m>\mathbf{R}</m>.
    /// </summary>
    /// <param name="n">The dimension.</param>
    /// <param name="lower">The lower bounds.</param>
    /// <param name="upper">The upper bounds.</param>
    /// <param name="correl">The correlation matrix.</param>
    /// <param name="maxpoints">The max points.</param>
    /// <param name="abseps">The absolute accuracy.</param>
    /// <param name="releps">The relative accuracy.</param>
    /// <param name="error">The estimated error (output).</param>
    /// <param name="numevls">The number of function evaluations (output).</param>
    /// <param name="info">The infomation code (output).</param>
    /// <returns></returns>
    /// <remarks></remarks>
    [DllImport("MagnoliaIGNative", EntryPoint = "multiple_normal_probability", CallingConvention = CallingConvention.Cdecl)]
    public static extern double MultipleNormalProbability(
      int n, [In] double[] lower, [In] double[] upper, [In] double[] correl,
      int maxpoints, double abseps, double releps,
      ref double error, ref int numevls, ref int info);
  }
}
