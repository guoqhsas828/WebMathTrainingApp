// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  ///   Implementation of a general Heston stochastic volatility model.
  /// </summary>
  /// <remarks>
  ///   <para>The Heston model is a well known stochastic volatility model named after
  ///   Steven Heston. See <see href="http://www.jstor.org/pss/2962057" target="_self">A
  ///   Closed-Form Solution for Options with Stochastic Volatility with Applications
  ///   to Bond and Currency Options", by Steven L. Heston, ''The Review of Financial Studies''
  ///   1993 Volume 6, number 2, pp. 327-343</see>.</para>
  ///   <para>The Heston model assumes both the underlying asset price as well as the asset volatility are
  ///   stochastic with the underlying asset following a lognormal process and the volatility following
  ///   a CIR process.</para>
  ///   <para><b>Basic Heston</b></para>
  ///   <para>This model covers processes with dynamics described by:</para>
  ///   <math>
  ///     dX_t = \mu S_t dt + \sqrt{v_t} S_t dW^{X}_{t}
  ///   </math><math>
  ///     dv_t = k\left ( \theta - v_t \right ) dt + \xi \sqrt{v_t} dW^v_{t}
  ///   </math>
  ///   <para>where:</para>
  ///   <list type="bullet">
  ///     <item><span><m>k</m> is the volatilities rate of mean reversion</span></item>
  ///     <item><span><m>\theta</m> is the mean level of the invariant distribution of <m>v_t</m>, and</span></item>
  ///     <item><span><m>\xi</m> is the volatility of the volatility</span></item>
  ///     <item><span><m>dW^X</m> and <m>dV^v</m> are Weiner process with correlation <m>p</m></span></item>
  ///   </list>
  ///   <para><b>Variations</b></para>
  ///   <para>In order to accurately fit the observed market, a number of common extensions have been
  ///   developed for the Heston model.</para>
  ///   <para>A straightforward extension to the standard Heston model is to allow the parameters to be time dependent.
  ///   Here the dynamics are described by:</para>
  ///   <math>
  ///     dX_t=\mu S_t dt + \sqrt{v_t}S_t dW^X_{t}
  ///   </math><math>
  ///     dv_t = k_t\left ( \theta_t - v_t \right ) dt + \xi_t \sqrt{v_t} dW^v_{t}
  ///   </math>
  ///   <para>where <m>k_t</m>, <m>\theta_t</m> and <m>\xi_t</m> are time dependent.</para>
  ///   <para>A second approach is to add another volatility process, independent of the first. Here:</para>
  ///   <math>
  ///     dX_t=\mu S_t dt + \sqrt{v_t}S_t dW^X_{t}
  ///   </math><math>
  ///     dv^1_t = k^1\left ( \theta^1 - v^1_t \right ) dt + \xi^1 \sqrt{v^1_t} dW^{v^1}_{t}
  ///   </math><math>
  ///     dv^2_t = k^2\left ( \theta^2 - v^2_t \right ) dt + \xi^2 \sqrt{v^2_t} dW^{v^2}_{t}
  ///   </math>
  /// </remarks>
  public class Heston
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="Heston"/> class.
    /// </summary>
    /// <param name="payoffFn">Payoff function of option</param>
    /// <param name="simulations">Numer of simulation paths</param>
    /// <param name="steps">Number of time steps</param>
    /// <param name="T">Time (years) to expiration.</param>
    /// <param name="s0">Underlying price</param>
    /// <param name="r">Risk free rate (continuous comp, eg. 0.06)</param>
    /// <param name="v0">Volatiliy</param>
    /// <param name="kappav">Is the mean reversion rate of volatility</param>
    /// <param name="thetav">Is the long term volatility</param>
    /// <param name="sigmav">Volatility of volatility</param>
    /// <param name="rhov">correlation between underlying price and volatility</param>
    public Heston(Func<double[], double> payoffFn, int simulations, int steps, double T, double s0, double r,
                  double v0, double kappav, double thetav, double sigmav, double rhov)
    {
      _pathCount = simulations;
      _kappa = kappav;
      _theta = thetav;
      _sigmav = sigmav;
      _rho = rhov;
      _rate = r;
      var pathParams = new StochasticPathGenerator.PathParameters(
        // Two independent random shocks
        2,
        // The initial values of the state variables.
        new[] {s0, v0},
        // We only need to evaluate the payoff function once at the end.
        // So, each path is generated with a single stage, within which
        //  there are STEPS steps and the step size is (T/STEPS).
        new[] {T / steps}, new[] {steps}
        );

      // The second parameter is null, for we use the default random number generator.
      _pathGenerator = new StochasticPathGenerator(pathParams, null);

      // Remember the payoff function.
      _payoffFn = payoffFn;
    }

    /// <summary>
    ///   Calculates fair value of an option given the payoff function using Monte Carlo.
    /// </summary>
    /// <returns>fair value of option.</returns>
    public double Price()
    {
      var expectPayoff = new ExpectationBuilder();
      var pathCount = _pathCount;
      for (int i = 0; i < pathCount; ++i)
      {
        expectPayoff.Add(1.0, EvaluateSinglePath());
      }
      return expectPayoff.Mean;
    }

    /// <summary>
    ///  This function intends to be thread safe, so it can be used with a
    ///   thread safe random number generator to produce and eveluate paths
    ///   in parallel.
    /// </summary>
    private double EvaluateSinglePath()
    {
      double payoff = 0;
      var stockPrices = new List<double>();
      _pathGenerator.DrawPath((t, startX, weiner, dt, endX) =>
                              {
                                Debug.Assert(startX != null && startX.Length == 2);
                                Debug.Assert(endX != null && endX.Length == 2);
                                Debug.Assert(weiner != null && weiner.Length == 2);
                                double z1 = weiner[0], z2 = weiner[1], sdt = Math.Sqrt(dt);
                                double s0 = startX[0], v0 = startX[1];
                                double v1 =
                                  endX[1] =
                                  v0 + _kappa * (_theta - v0) * dt + _sigmav * Math.Sqrt(Math.Abs(v0)) * (_rho * z1 + Math.Sqrt(1.0 - (_rho * _rho)) * z2) * sdt;
                                double s1 = endX[0] = s0 + s0 * (_rate * dt + Math.Sqrt(Math.Abs(v1)) * z1 * sdt);
                                stockPrices.Add(s1);
                              },
                              (i, x) =>
                              {
                                payoff = _payoffFn(stockPrices.ToArray());
                                return false; // Signal the end of this path. Redundant in this case.
                              });
      return payoff;
    }

    private readonly int _pathCount;
    private readonly double _kappa, _theta, _sigmav, _rho, _rate;
    private readonly Func<double[], double> _payoffFn;
    private readonly StochasticPathGenerator _pathGenerator;
  }
}