using System;
using BaseEntity.Toolkit.Base;
using static BaseEntity.Toolkit.Numerics.SpecialFunctions;

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  /// One factor model of correlated short rates and credit spreads
  ///  with Hull-White dynamics.
  /// </summary>
  /// 
  /// <remarks>
  ///  <para>The short rate <m>r_t</m> and the hazard rate <m>\lambda_t</m>
  ///   are described by the following Hull-White stochastic equations
  ///   <math>\begin{align}
  ///        d r_t &amp;= (\theta_{1t} - a_1 r_t)\,dt + \sigma_{1}\,dW_{1t}
  ///     \\ d \lambda_t &amp;= (\theta_{2t} - a_2 \lambda_t)\,dt + \sigma_{2}\,dW_{2t}
  ///     \\ \rho\,dt &amp;= dW_{1t}\,dW_{2t}
  ///   \end{align}</math>where we assume both the mean reversion and volatility are
  ///   constant, while the long term means <m>\theta_{it}</m> are deterministic, but
  ///   they can be time varying, making it possible to calibrate to the initial term
  ///   structures of the interest rates and credit spreads.
  ///  </para>
  /// 
  ///  <para>Let <m>P(t,T)</m> and <m>\tilde{P}(t,T)</m> be the prices at time <m>t</m>
  ///   of the riskless and defaultable zero coupon bonds maturing at time <m>T</m>,
  ///   respectively.
  ///   <math>\begin{align}
  ///     P(t,T) &amp;\equiv \mathrm{E}\left[
  ///       e^{-\int_t^T r_s ds} \mid \mathcal{F}_t
  ///     \right]
  ///    \\ \tilde{P}(t,T) &amp;\equiv \mathrm{E}\left[
  ///       e^{-\int_t^T \left(r_s + \lambda_s\right) ds} \mid \mathcal{F}_t
  ///     \right]
  ///   \end{align}</math></para>
  /// 
  ///  <para>There are analytical formula for both prices.
  ///   <math>\begin{align}
  ///    P(t,T) &amp;= \exp\!\left(
  ///      - r_t\,B(a_1, T-t)
  ///      - \gamma_{1}(t, T)
  ///      + \frac{\sigma_1^2}{2}\,C(a_1, a_1, T-t)
  ///   \right)
  ///  \end{align}</math><math>\begin{align}
  ///    \frac{\tilde{P}(t,T)}{P(t,T)} &amp;= \exp\!\left(      
  ///      - \lambda_t\,B(a_2, T-t)
  ///      - \gamma_{2}(t, T)
  ///      \right.
  ///   \\ &amp; \qquad\left.
  ///     +\; \frac{\sigma_2^2}{2}\,C(a_2, a_2, T-t)
  ///     + \rho \sigma_1 \sigma_2\,C(a_1, a_2, T-t)
  ///   \right)
  ///  \end{align}</math>where<math>\begin{align}
  ///    B(a, t) &amp;\equiv \frac{1 - e^{-a t}}{a}
  ///   \\ \gamma_{i}(t, T) &amp;\equiv
  ///      \int_t^T \theta_{is}\,B(a_i, T-s)\,ds
  ///      ,\quad i = 1, 2
  ///   \\ C(a, b, t) &amp;\equiv \frac{
  ///      t - B(a, t) - B(b, t) + B(a + b, t)
  ///     }{a b}
  ///  \end{align}</math>
  ///  </para>
  /// 
  ///  <para>In the case of the constant <m>\theta_{it} = \theta_i</m>,
  ///  we have<math>
  ///    \gamma_i(t,T) = \theta_i\frac{T - t - B(a, T-t)}{a}
  ///  </math></para>
  /// </remarks>
  public static class CreditShortRateHybridModel
  {
    /// <summary>
    ///  Calculate the risk-less zero price.
    /// </summary>
    /// <param name="date">The maturity date of the zero bond</param>
    /// <param name="asOf">Today</param>
    /// <param name="r0">The initial short rate level</param>
    /// <param name="a">The mean reversion parameter</param>
    /// <param name="theta">The drift parameter</param>
    /// <param name="sigma">The volatility parameter</param>
    /// <returns>System.Double.</returns>
    public static double ZeroPrice(
      Dt date, Dt asOf,
      double r0, double a, double theta, double sigma)
    {
      double logP = LogPrice((date - asOf)/365.0, r0, a, theta, sigma);
      return Math.Exp(logP);
    }

    /// <summary>
    ///  Calculate the defaultable zero price.
    /// </summary>
    /// <param name="date">The maturity date of the zero bond</param>
    /// <param name="asOf">Today</param>
    /// <param name="r0">The initial short rate level</param>
    /// <param name="lambda0">The initial hazard rate level</param>
    /// <param name="rho">The rho.</param>
    /// <param name="aR">The mean reversion of short rate</param>
    /// <param name="thetaR">The drift parameter of short rate</param>
    /// <param name="sigmaR">The volatility of short rate</param>
    /// <param name="aL">The mean reversion of hazard rate</param>
    /// <param name="thetaL">The drift parameter of hazard rate</param>
    /// <param name="sigmaL">The volatility of hazard rate</param>
    /// <returns>System.Double.</returns>
    public static double RiskyZeroPrice(
      Dt date, Dt asOf,
      double r0, double lambda0, double rho,
      double aR, double thetaR, double sigmaR,
      double aL, double thetaL, double sigmaL)
    {
      double time = (date - asOf)/365.0;
      double logP = LogPrice(time, r0, aR, thetaR, sigmaR);
      double logQ = LogPrice(time, lambda0, aL, thetaL, sigmaL);
      double logR = rho*sigmaR*sigmaL*CalculateB2(aR, aL, time);
      return Math.Exp(logP + logQ + logR);
    }

    /// <summary>
    ///  Calculate the logarithm of zero price<math>
    ///   \log P(0,t) = - r_0\,B(a, t)
    ///      - \gamma(0, t)
    ///      + \frac{\sigma_1^2}{2}\,C(a,a,t)
    ///  </math>
    /// </summary>
    /// <param name="time">The time to maturity</param>
    /// <param name="initialRate">The initial rate level</param>
    /// <param name="a">The mean reversion parameter</param>
    /// <param name="theta">The drift parameter</param>
    /// <param name="sigma">The volatility parameter</param>
    /// <returns>System.Double.</returns>
    public static double LogPrice(
      double time, double initialRate,
      double a, double theta, double sigma)
    {
      return -initialRate*CalculateB(a, time)
        - theta*CalculateDelta(a, time)
        + 0.5*sigma*sigma*CalculateC(2*a, time);
    }

    /// <summary>
    /// Calculates <m>\displaystyle B(a,t)
    ///  \equiv \frac{1 - e^{-at}}{a}
    ///  = t\,\frac{e^{-at} - 1}{{-a}t}</m>.
    /// </summary>
    public static double CalculateB(double a, double t)
    {
      return t*Expd1(-a*t);
    }

    /// <summary>
    /// Calculates <m>\displaystyle \delta(a,t)
    ///   \equiv \frac{t - \delta(a,t)}{a}
    ///   = t^2\,\frac{at - 1 + e^{-at}}{a^2t^2}</m>.
    /// </summary>
    public static double CalculateDelta(double a, double t)
    {
      return t*t*Expd2(-a*t);
    }

    /// <summary>
    /// Calculates <m>\displaystyle C(a,t)
    ///   \equiv \frac{t - 2 B(a,t) + B(2 a, t)}{a^2}
    /// </m>
    /// </summary>
    public static double CalculateC(double a, double t)
    {
      return t*t*t*Expd2(-a*t, -a*t);
    }

    /// <summary>
    /// Calculates <m>\displaystyle C(a_1, a_2, t)
    ///   \equiv \frac{t - B(a_1,t) - B(a_2, t)
    ///    + B(a_1+a_2,t)}{a_1 a_2}</m>
    /// </summary>
    public static double CalculateB2(double a1, double a2, double t)
    {
      return t*t*t*Expd2(-a1*t, -a2*t);
    }

  }
}
