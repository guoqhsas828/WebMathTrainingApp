
using System;
using System.Diagnostics;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Numerics.Integrals;

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  ///   Evaluate European options where the forward value is
  ///   an non-linear function of a log-normal variable.
  /// </summary>
  /// <remarks>
  ///  <para>In this model, the option payoff at the expiration time <m>T</m>
  ///    is given by<math>
  ///      V_T \equiv \left[\xi \left(f(X) - K \right)\right]^+
  ///       ,\quad X = e^{\mu + \sigma Z}
  ///   </math>where <m>f(\cdot)</m> is a increasing function,
  ///     <m>X</m> is a log-normal variable with center <m>\mu</m>
  ///       and variance <m>\sigma^2</m>,
  ///     <m>Z</m> is standard normal,
  ///     <m>K</m> is the strike value,
  ///     and <m>\xi</m> is either 1 (call option) or -1 (put option).
  ///  </para>
  ///   <para>
  ///     This class evaluate the expectation <m>E V_T</m> by numerical integration.
  ///  </para>
  /// </remarks>
  public static class NonLinearLogNormalOption
  {
    /// <summary>
    ///   Calculate the option value <m>E V_T</m>
    /// </summary> 
    /// <param name="valueFn">The value function <m>f(\cdot)</m></param>
    /// <param name="foward">The forward value <m>F</m></param>
    /// <param name="strike">The strike value <m>K</m></param>
    /// <param name="isCall">True for call option; otherwise, put option</param>
    /// <param name="sigma">The standard deviation <m>\sigma</m></param>
    /// <param name="initialGuessOfMean">Optional initial guess
    ///   to solve for <m>\mu</m> such that <m>\mathrm{E}[f(X)] = F</m></param>
    /// <returns>The option value</returns>
    /// <remarks>
    ///  <para>This function evaluates the expectation<math>
    ///      \pi \equiv \mathrm{E}\left[\xi \left(f(X) - K \right)\right]^+
    ///       ,\quad X = e^{\mu + \sigma Z}
    ///   </math>where <m>f(\cdot)</m> is a increasing function,
    ///     <m>Z</m> is standard normal,
    ///     <m>K</m> is the strike value,
    ///     <m>\xi</m> is either 1 (call option) or -1 (put option),
    ///     and <m>\mu</m> is calibrated to satisfy <m>\mathrm{E}[f(X)] = F</m>
    ///     with <m>F</m> being the forward value.
    ///  </para>
    /// </remarks>
    public static double CalculateValue(
      Func<double, double> valueFn,
      double foward, double strike,
      bool isCall, double sigma,
      double initialGuessOfMean = Double.NaN)
    {
      return CalculateExpectation(valueFn, foward, strike,
        strike, isCall, sigma, initialGuessOfMean);
    }

    /// <summary>
    ///   Calculate the option exercise probability
    /// </summary> 
    /// <param name="valueFn">The value function <m>f(\cdot)</m></param>
    /// <param name="foward">The forward value <m>F</m></param>
    /// <param name="strike">The strike value <m>K</m></param>
    /// <param name="isCall">True for call option; otherwise, put option</param>
    /// <param name="sigma">The standard deviation <m>\sigma</m></param>
    /// <param name="initialGuessOfMean">Optional initial guess
    ///   to solve for <m>\mu</m> such that <m>\mathrm{E}[f(X)] = F</m></param>
    /// <returns>The option value</returns>
    /// <remarks>
    ///  <para>This function calculates the probability<math>
    ///      p \equiv \Pr \left(\xi \left(f(X) - K \right) > 0\right)
    ///       ,\quad X = e^{\mu + \sigma Z}
    ///   </math>where <m>f(\cdot)</m> is a increasing function,
    ///     <m>Z</m> is standard normal,
    ///     <m>K</m> is the strike value,
    ///     <m>\xi</m> is either 1 (call option) or -1 (put option),
    ///     and <m>\mu</m> is calibrated to satisfy <m>\mathrm{E}[f(X)] = F</m>
    ///     with <m>F</m> being the forward value.
    ///  </para>
    /// </remarks>
    public static double CalculateProbability(
      Func<double, double> valueFn,
      double foward, double strike,
      bool isCall, double sigma,
      double initialGuessOfMean = Double.NaN)
    {
      if (sigma < 1E-12)
      {
        return Math.Max(0,
          (isCall ? 1 : -1)*(foward > strike ? 1 : -1));
      }

      var b = SolveForX(strike, valueFn, initialGuessOfMean);
      var mu = CalibrateCenter(foward, b, sigma, valueFn, initialGuessOfMean);

      Func<double, double> fn = x => 1;
      var quad = GetQuadrature(mu, b, sigma);
      return isCall ? quad.RightIntegral(fn) : quad.LeftIntegral(fn);
    }


    /// <summary>
    ///   Calculates the expectation of the payoff
    ///   <m>V(X) \equiv \xi\left(f(X) - K\right)\,I_{\{L &lt; f(X) \le U\}}</m>,
    ///   where <m>X</m> is log-normal variable, and <m>\xi</m> is either 1 or -1.
    /// </summary>
    /// <param name="valueFn">The function <m>f(\cdot)</m>, which must be increasing</param>
    /// <param name="foward">The mean <m>F = \mathrm{E}[f(X)]</m></param>
    /// <param name="strike">The strike <m>K</m></param>
    /// <param name="lower">The lower barrier <m>L</m></param>
    /// <param name="upper">The upper barrier <m>U</m></param>
    /// <param name="isCall">If true, <m>\xi = 1</m>; otherwise, <m>\xi = -1</m></param>
    /// <param name="sigma">The volatility of <m>X</m></param>
    /// <param name="initialGuessOfMean">The initial values to solve for the inverse <m>b = f^{-1}(B)</m></param>
    /// <returns>The option value</returns>
    public static double CalculateValue(
      Func<double, double> valueFn,
      double foward, double strike,
      double lower, double upper,
      bool isCall, double sigma,
      double initialGuessOfMean = Double.NaN)
    {
      // Normalize the upper/lower bounds.
      // The following conditions work when either lower/upper are NaN.
      if (isCall)
        lower = !(lower >= strike) ? strike : lower;
      else
        upper = !(upper <= strike) ? strike : upper;

      // Return 0 if the condition is not feasible.
      if (!(upper > lower)) return 0.0;

      //- Calculate the barrier option value.
      var v1 = CalculateExpectation(valueFn, foward, strike,
        isCall ? lower : upper, isCall, sigma, initialGuessOfMean);
      var v2 = CalculateExpectation(valueFn, foward, strike,
        isCall ? upper : lower, isCall, sigma, initialGuessOfMean);

      return v1 - v2;
    }

    /// <summary>
    ///   Calculates the expectation of the generic payoff
    ///   <m>V(X) \equiv \xi\left(f(X) - K\right)\,I_{\{\xi f(X) &gt; \xi B\}}</m>,
    ///   where <m>X</m> is log-normal variable,  <m>\xi</m> is either 1 or -1,
    ///   the strike <m>K</m> and barrier <m>B</m> satisfy the condition
    ///   <m>\xi B \ge \xi K</m>.
    /// </summary>
    /// <param name="valueFn">The function <m>f(\cdot)</m>, which must be increasing</param>
    /// <param name="foward">The mean <m>F = \mathrm{E}[f(X)]</m></param>
    /// <param name="strike">The strike <m>K</m></param>
    /// <param name="barrier">The barrier <m>B</m></param>
    /// <param name="isCall">If true, <m>\xi = 1</m>; otherwise, <m>\xi = -1</m></param>
    /// <param name="sigma">The volatility of <m>X</m></param>
    /// <param name="initialGuessOfMean">The initial values to solve for the inverse <m>b = f^{-1}(B)</m></param>
    /// <returns>The expectation value</returns>
    private static double CalculateExpectation(
      Func<double, double> valueFn,
      double foward, double strike, double barrier,
      bool isCall, double sigma,
      double initialGuessOfMean = Double.NaN)
    {
      var a = isCall ? 1 : -1;
      Debug.Assert(a*(barrier - strike) >= 0);

      if (Double.IsPositiveInfinity(a*barrier))
        return 0.0;

      if (sigma < 1E-12)
      {
        return isCall
          ? (foward > barrier ? (foward - strike) : 0.0)
          : (barrier > foward ? (strike - foward) : 0.0);
      }

      var b = SolveForX(barrier, valueFn, initialGuessOfMean);
      var mu = CalibrateCenter(foward, b, sigma, valueFn, initialGuessOfMean);

      var s0 = Math.Exp(mu);
      Func<double, double> fn = x => Math.Max(0.0, a*(valueFn(s0*x) - strike));
      var quad = GetQuadrature(mu, b, sigma);
      return isCall ? quad.RightIntegral(fn) : quad.LeftIntegral(fn);
    }

    internal static double CalibrateCenter(
      double value, double bound, double stddev,
      Func<double, double> valueFn,
      double initialGuess)
    {
      var initX = SolveForX(value, valueFn, initialGuess);
      var mu0 = Math.Log(initX) - 0.5*stddev*stddev;
      return Solve(
        mu => CalculateExpectation(mu, bound, stddev, valueFn),
        value, mu0 - 0.05, mu0 + 0.05);
    }

    internal static double SolveForX(
      double value,
      Func<double, double> valueFn,
      double initialGuess)
    {
      var x0 = initialGuess;
      if (!(x0 > 0)) x0 = 0.01;
      return Solve(valueFn, value, 0.5*x0, 1.5*x0);
    }

    internal static double Solve(
       Func<double, double> fn,
       double y,
       double x0Lower, double x0Upper)
    {
      var rf = new Brent2();
      rf.setLowerBounds(-MaxLog);
      rf.setUpperBounds(MaxLog);
      rf.setToleranceX(1E-12);
      rf.setToleranceF(1E-12);
      rf.setMaxIterations(1000);
      rf.setMaxEvaluations(5000);

      return rf.solve(fn, null, y, x0Lower, x0Upper);
    }

    internal static double CalculateExpectation(
      double mu, double bound, double stddev,
      Func<double, double> valueFn)
    {
      var s0 = Math.Exp(mu);
      return GetQuadrature(mu, bound, stddev)
        .Integral(x => valueFn(s0*x));
    }

    private static LogNormal GetQuadrature(
      double mu, double bound, double stddev)
    {
      var d = (Math.Log(bound) - mu)/stddev;
      return new LogNormal(d, stddev);
    }

    private static readonly double MaxLog = Math.Log(Double.MaxValue);
  }
}
