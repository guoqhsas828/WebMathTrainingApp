/*
 *   2012. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using QMath = BaseEntity.Toolkit.Numerics.SpecialFunctions;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  /// <summary>
  ///   Utilities to build volatility smiles and surfaces based on Black-Scholes model.
  /// </summary>
  /// <remarks>
  /// </remarks>
  public static class BlackScholesCalibration
  {
    #region Delta sticky volatilities
    /// <summary>
    ///   Gets the at-the-money strike.
    /// </summary>
    /// <param name="parameter">The parameter.</param>
    /// <param name="atmKind">The ATM specification.</param>
    /// <param name="premiumIncluded">True for premium included delta.</param>
    /// <param name="atmVolatility">The ATM volatility.</param>
    /// <returns></returns>
    /// <remarks>
    /// </remarks>
    public static double GetAtmStrike(
      this IBlackScholesParameterData parameter,
      AtmKind atmKind, bool premiumIncluded, double atmVolatility)
    {
      if (atmKind == AtmKind.Spot) return parameter.Spot;
      if (atmKind == AtmKind.Forward) return parameter.GetForward();
      // Now we need to find the delta neutral strike.
      return parameter.GetForward() * Math.Exp((premiumIncluded ? -1 : 1)
        * (0.5 * atmVolatility * atmVolatility * parameter.Time));
    }

    /// <summary>
    /// Gets the at-the-money strike and volatility from a smile.
    /// </summary>
    /// <param name="parameter">The parameter.</param>
    /// <param name="smileInLogMoneyness">The smile in moneyness.</param>
    /// <param name="atmKind">The ATM specification.</param>
    /// <param name="premiumIncluded">True for premium included delta.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="volatility">The volatility.</param>
    /// <remarks></remarks>
    public static void GetAtmStrikeAndVolatility(
      this IBlackScholesParameterData parameter,
      Func<double,double> smileInLogMoneyness,
      AtmKind atmKind, bool premiumIncluded,
      out double strike, out double volatility)
    {
      double forward = parameter.GetForward();
      if (atmKind == AtmKind.Spot)
      {
        strike = parameter.Spot;
        volatility = smileInLogMoneyness(Math.Log(strike / forward));
        return ;
      }
      if (atmKind == AtmKind.Forward)
      {
        strike = forward;
        volatility = smileInLogMoneyness(0.0);
        return;
      }
      // Now we need to find the delta neutral strike.
      // Use a solver.
      double time = parameter.Time;
      var solver = new Brent2();
      solver.setToleranceX(1E-12);
      solver.setToleranceF(1E-12);
      volatility = solver.solve(v =>
      {
        double m = ((premiumIncluded ? -1 : 1) * (0.5 * v * v * time));
        return smileInLogMoneyness(m) - v;
      }, null, 0.0, 0.1, 0.7);
      strike = forward * Math.Exp((premiumIncluded ? -1 : 1)
        * (0.5 * volatility * volatility * time));
    }

    /// <summary>
    ///   Calculates the strike for the specified delta/volatility pair.
    /// </summary>
    /// <param name="parameter">The Black-Scholes parameter.</param>
    /// <param name="optionType">Type of the option (call or put).</param>
    /// <param name="deltaStyle">The delta style.</param>
    /// <param name="delta">The delta size.</param>
    /// <param name="volatility">The volatility value at the specified delta.</param>
    /// <returns>The strike corresponding to the specified delta/volatility pair.</returns>
    /// <remarks>
    /// <para>The regular simple delta is given by <math>
    ///     \Delta = \omega D_{\!f}\, \Phi\!\left(\omega \frac{\log(F/K) + \sigma^2 T/2}{\sigma \sqrt{T}}\right)
    ///   </math>where <m>F = S e^{(r_2 - r_1)T}</m> is the forward, <m>\omega = 1</m> for call option, <m>\omega = -1</m> for put option, and<math>
    ///     D_{\!f} = \begin{cases}
    ///     e^{-r_1 T} &amp; \text{for spot delta } \displaystyle \frac{\partial P}{\partial S}
    ///     \\ \\ 1 &amp; \text{for forward delta } \displaystyle \frac{\partial P}{\partial\left(e^{-r_2 T} F\right)}
    ///     \end{cases}
    ///   </math>
    ///   Hence the strike is given by <math>
    ///     K = F \exp\left(-\omega \sigma\sqrt{T}\,\Phi^{\!-1}\!(|\Delta|/D_{\!f})+0.5 \sigma^2 T \right)
    ///   </math></para>
    /// <para>For premium included delta, we have<math>
    ///     \Delta^{pi} =\omega \frac{K}{F} D_{\!f}\, \Phi\!\left(\omega \frac{\log(F/K) - \sigma^2 T/2}{\sigma \sqrt{T}}\right)
    /// </math>To find the strike, we have to solve the above equation.
    /// </para>
    /// </remarks>
    public static double GetDeltaStrike(
      this IBlackScholesParameterData parameter,
      OptionType optionType, DeltaStyle deltaStyle,
      double delta, double volatility)
    {
      Debug.Assert(optionType != OptionType.None);
      double v = volatility * Math.Sqrt(parameter.Time);

      // Convert spot delta to forward delta
      if ((deltaStyle & DeltaStyle.Forward) == 0)
      {
        delta = parameter.ConvertSpotToForwardDelta(delta);
      }

      // Indicate call/put
      var w = optionType == OptionType.Call ? 1 : -1;

      // For simple delta, it is easy to calculate the strike
      if ((deltaStyle & DeltaStyle.PremiumIncluded) == 0)
      {
        return parameter.GetForward() * Math.Exp(
          v * (0.5 * v - w * QMath.NormalInverseCdf(delta)));
      }

      // Now we need to find the strike for premium included delta.
      var solver = new Brent2();
      solver.setToleranceF(1E-13);
      solver.setToleranceX(1E-11);
      var moneyness = solver.solve(m => 
        m*QMath.NormalCdf(-w*(Math.Log(m)/v + 0.5*v)),
        null, delta, 0.8, 1.2);
      return moneyness * parameter.GetForward();
    }
    #endregion

    #region Utilities providing Black-Scholes parameter data for various options

    /// <summary>
    ///   Gets the parameter data of the Black-Scholes model for FX options.
    /// </summary>
    /// <param name="today">Today.</param>
    /// <param name="expiry">The option expiry date.</param>
    /// <param name="fractionFn">The function to calculate the time in years between two dates.</param>
    /// <param name="domesticDiscountCurve">The domestic discount curve.</param>
    /// <param name="fxCurve">The FX curve.</param>
    /// <returns>An instance of <see cref="IBlackScholesParameterData" />.</returns>
    /// <remarks>
    /// </remarks>
    public static IBlackScholesParameterData GetParameters(
      Dt today, Dt expiry,
      Func<Dt, Dt, double> fractionFn,
      DiscountCurve domesticDiscountCurve,
      FxCurve fxCurve)
    {
      double spot = fxCurve.SpotRate;
      double fwd = fxCurve.FxRate(expiry);
      double pd = domesticDiscountCurve.DiscountFactor(today, expiry);
      double pf = fwd * pd / spot;
      double time = fractionFn(today, expiry);
      return new BlackScholesParameterData(time, spot,
        -Math.Log(pf) / time, -Math.Log(pd) / time);
    }
    #endregion

    #region Volatility interpolation
    /// <summary>
    /// Interpolates the volatility from the specified surface.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="expiry">The expiry.</param>
    /// <param name="atmForward">The atm forward.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility</returns>
    /// <remarks></remarks>
    public static double Interpolate(
      this IVolatilitySurface surface,
      Dt expiry, double atmForward, double strike)
    {
      var cs = surface as VolatilitySurface;
      if (cs != null)
      {
        switch (cs.SmileInputKind)
        {
        case SmileInputKind.Strike:
          return cs.Interpolate(expiry, strike);
        case SmileInputKind.Moneyness:
          return cs.Interpolate(expiry, strike / atmForward);
        case SmileInputKind.LogMoneyness:
          return cs.Interpolate(expiry, Math.Log(strike / atmForward));
        }
      }
      return surface.Interpolate(expiry, strike);
    }

    #endregion
  }
}
