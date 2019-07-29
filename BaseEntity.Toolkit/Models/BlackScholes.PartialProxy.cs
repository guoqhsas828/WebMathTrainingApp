/*
 *  -2012. All rights reserved.
 */

using System;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using QMath = BaseEntity.Toolkit.Numerics.SpecialFunctions;

namespace BaseEntity.Toolkit.Models
{
  public static class BlackScholesExtensions
  {
    #region Black-Scholes data manipulation

    /// <summary>
    /// Gets the forward price.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns>The forward price</returns>
    public static double GetForward(this IBlackScholesParameterData data)
    {
      return data.Spot * Math.Exp(data.Time * (data.Rate2 - data.Rate1));
    }

    /// <summary>
    /// Converts the spot delta to forward delta.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="spotDelta">The spot delta.</param>
    /// <returns>The forward delta.</returns>
    public static double ConvertSpotToForwardDelta(
      this IBlackScholesParameterData data, double spotDelta)
    {
      return spotDelta * Math.Exp(data.Rate1 * data.Time);
    }

    /// <summary>
    /// Converts the forward delta to spot delta.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="forwardDelta">The forward delta.</param>
    /// <returns>The forward delta.</returns>
    public static double ConvertForwardToSpotDelta(
      this IBlackScholesParameterData data, double forwardDelta)
    {
      return forwardDelta * Math.Exp(-data.Rate1 * data.Time);
    }

    public static double GetForwardDelta(double moneyness,
      double sigmaT, int sign, bool premiumIncluded)
    {
      return premiumIncluded
        ? Math.Exp(moneyness) * QMath.NormalCdf(
          -sign * (0.5 * sigmaT + moneyness / sigmaT))
        : QMath.NormalCdf(sign * (0.5 * sigmaT - moneyness / sigmaT));
    }

    #endregion
  }

  public abstract class BlackScholes : Native.BlackScholes
  {
    #region Black-Scholes data manipulation

    /// <summary>
    /// Gets the log-moneyness from delta.
    /// </summary>
    /// <param name="forwardDelta">The forward delta.</param>
    /// <param name="sigmaT">The total volatility (sigma times the square root of time).</param>
    /// <param name="callPut">The indicator with value 1 for call and -1 for put.</param>
    /// <param name="premiumIncluded">if set to <c>true</c>, premium included delta.</param>
    /// <returns>The log-moneyness</returns>
    /// <remarks></remarks>
    public static double GetLogMoneynessFromDelta(
      double forwardDelta, double sigmaT, int callPut, bool premiumIncluded)
    {
      forwardDelta = CheckForwardDelta(forwardDelta);
      callPut = -callPut;
      double alpha = QMath.NormalInverseCdf(forwardDelta);
      if (!premiumIncluded)
      {
        return sigmaT * (callPut * alpha + 0.5 * sigmaT);
      }
      var solver = new Brent2();
      solver.setToleranceX(1E-12);
      solver.setToleranceF(1E-12);
      var result = solver.solve(
        x => Math.Exp(x) * QMath.NormalCdf(callPut * (0.5 * sigmaT + x / sigmaT)),
        null, forwardDelta, 0);
      return result;
    }

    /// <summary>
    /// Gets the volatility from the forward delta.
    /// </summary>
    /// <param name="smileInLogMoneyness">The smile as the function of log-moneyness.</param>
    /// <param name="forwardDelta">The forward delta.</param>
    /// <param name="sqrtTime">The square root of the time to expiry.</param>
    /// <param name="callPut">The indicator which is <c>1</c> for call and <c>-1</c> for put.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public static double GetLogMoneynessFromDelta(
      Func<double, double> smileInLogMoneyness, double forwardDelta,
      double sqrtTime, int callPut)
    {
      if (forwardDelta < 0) forwardDelta = -forwardDelta;
      if (!(forwardDelta > 0 && forwardDelta <= 1))
        throw new ToolkitException(String.Format("Delta {0} is out of range", forwardDelta));
      if (!(sqrtTime > DoubleNumberComparison.MachineEpsilon))
        throw new ToolkitException(String.Format("Time to expiry {0} is not strictly positive", sqrtTime));
      var solver = new Brent2();
      solver.setToleranceX(1E-12);
      solver.setToleranceF(1E-12);
      var result = solver.solve(x =>
      {
        var sigmaT = smileInLogMoneyness(x) * sqrtTime;
        return QMath.NormalCdf(callPut * (0.5 * sigmaT * sigmaT - x) / sigmaT);
      }, null, forwardDelta, -1.0, 1.0);
      return result;
    }

    public static double ImplyAverageVolatility(double forwardDelta,
      double vwbT, double rrT, bool premiumIncluded, bool ccy2Strangle)
    {
      forwardDelta = CheckForwardDelta(forwardDelta);
      var price = GetOneVolStranglePrice(forwardDelta,
        vwbT, premiumIncluded, ccy2Strangle);
      var solver = new Brent2();
      solver.setToleranceX(1E-12);
      solver.setToleranceF(1E-12);
      var result = solver.solve(x =>
      {
        double sigPut = x - 0.5 * rrT, sigCall = x + 0.5 * rrT;
        double mPut = GetLogMoneynessFromDelta(forwardDelta,
          sigPut, -1, premiumIncluded);
        double mCall = GetLogMoneynessFromDelta(forwardDelta,
          sigCall, 1, premiumIncluded);
        return ForwardNormalizedOptionPrice(mCall, sigCall, 1)
          + ForwardNormalizedOptionPrice(mPut, sigPut, -1);
      }, null, price, vwbT);
      return result;
    }

    public static double ImplyStrangleVolatility(double forwardDelta,
      bool premiumIncluded, bool ccy2Strangle, double fnPrice, double sigma0)
    {
      forwardDelta = CheckForwardDelta(forwardDelta);
      var solver = new Brent2();
      solver.setToleranceX(1E-12);
      solver.setToleranceF(1E-12);
      var result = solver.solve(x => GetOneVolStranglePrice(
        forwardDelta, x, premiumIncluded, ccy2Strangle),
        null, fnPrice, sigma0);
      return result;
    }

    private static double CheckForwardDelta(double forwardDelta)
    {
      if (forwardDelta < 0) forwardDelta = -forwardDelta;
      if (!(forwardDelta > 0 && forwardDelta < 1))
      {
        throw new ToolkitException(String.Format(
          "Delta {0} is out of range", forwardDelta));
      }
      return forwardDelta;
    }

    private static double GetOneVolStranglePrice(double forwardDelta,
      double sigmaT,bool premiumIncluded, bool ccy2Strangle)
    {
      var mCall = GetLogMoneynessFromDelta(forwardDelta,
        sigmaT, 1, premiumIncluded);
      var mPut = GetLogMoneynessFromDelta(forwardDelta,
        sigmaT, -1, premiumIncluded);
      if (ccy2Strangle)
      {
        double kc = Math.Exp(mCall), kp = Math.Exp(mPut);
        return ForwardNormalizedOptionPrice(mCall, sigmaT, 1) / kc
          + ForwardNormalizedOptionPrice(mPut, sigmaT, -1) / kp;
      }
      return ForwardNormalizedOptionPrice(mCall, sigmaT, 1)
        + ForwardNormalizedOptionPrice(mPut, sigmaT, -1);
    }

    public static double ForwardNormalizedOptionPrice(
      double moneyness, double sigmaT, int sign)
    {
      double d1 = -moneyness / sigmaT + sigmaT / 2;
      double d2 = d1 - sigmaT;
      return sign * (QMath.NormalCdf(sign * d1)
        - Math.Exp(moneyness) * QMath.NormalCdf(sign * d2));
    }

    #endregion

    #region Forward start and cliquet options

    /// <summary>
    ///  Calculates the fair value of the specified forward starting option
    /// </summary>
    /// <param name="type">The type</param>
    /// <param name="start">The time to the forward start date</param>
    /// <param name="expiry">The time to the option expiry date</param>
    /// <param name="spot">The spot price</param>
    /// <param name="alpha">The strike if the option starts at 0,
    ///   or strike factor if it starts at t &gt; 0, in which case
    ///   alpha times the forward price at time t gives the strike</param>
    /// <param name="rfr">The risk free interest rate</param>
    /// <param name="qrate">The dividend yield or foreign interest rate</param>
    /// <param name="sigma">The volatility</param>
    /// <param name="divs">The dividend schedule</param>
    /// <param name="delta">The delta</param>
    /// <param name="gamma">The gamma</param>
    /// <param name="theta">The theta</param>
    /// <param name="vega">The vega</param>
    /// <param name="rho">The rho</param>
    /// <returns>The fair value of the option</returns>
    /// <remarks>
    ///   Instead of reseting the original values, this function adds the greeks to the original
    ///   values of the variables delta, gamma, theta, vega and rho, allowing them to accumulate
    ///   over a sequence of forward starting options.
    /// </remarks>
    public static double ForwardStartingOptionValue(
      OptionType type, double start, double expiry, double spot, double alpha,
      double rfr, double qrate, double sigma, DividendSchedule divs,
      ref double delta, ref double gamma, ref double theta,
      ref double vega, ref double rho)
    {
      double d = 0.0, g = 0.0, t = 0.0, v = 0.0, r = 0.0, la=0.0, ge=0.0, kge=0.0, va = 0.0, ch = 0.0, sp = 0.0, zo = 0.0, co = 0.0, vo = 0.0, dd = 0.0, dg = 0.0;
      double fv;
      if (start <= DoubleNumberComparison.MachineEpsilon)
      {
        // If the option starts at now, it's a regular option.
        fv = P(OptionStyle.European, type, expiry, spot, alpha, rfr, qrate, divs, sigma, 
          ref d, ref g, ref t, ref v, ref r, ref la, ref ge, ref kge, ref va, ref ch, ref sp, ref zo, ref co, ref vo, ref dd, ref dg);
        delta += d;
        gamma += g;
        theta += t;
        vega += v;
        rho += rho;
        return fv;
      }

      // Now calculate the value of the forward starting option.
      // First the discounted forward price at the starting date.
      double fwd = spot *  Math.Exp(-qrate * start);

      // Then we call Black-Sholes model for the forward option part.
      fv = fwd * P(OptionStyle.European, type, expiry - start, 1.0, alpha, rfr, qrate, divs, sigma, 
        ref d, ref g, ref t, ref v, ref r, ref la, ref ge, ref kge, ref va, ref ch, ref sp, ref zo, ref co, ref vo, ref dd, ref dg);

      // We also need to adjust the greeks
      delta += fv/spot; // gamma is zero.
      theta += -qrate * fv;
      vega += fwd * v;
      rho += fwd * r;
      return fv;
    }

    public static double RatchetOptionValue(
      OptionType type, IEnumerable<double> resets, double expiry,
      double spot, double strike, double rfr, double qrate,
      double sigma, DividendSchedule divs,
      ref double delta, ref double gamma, ref double theta,
      ref double vega, ref double rho)
    {
      double fv = 0.0;
      double alpha = strike, lastReset = 0.0;
      foreach (var reset in resets)
      {
        fv += ForwardStartingOptionValue(type, lastReset, reset,
          spot, alpha, rfr, qrate, sigma, divs,
          ref delta, ref gamma, ref theta, ref vega, ref rho);
        alpha = 1;
        lastReset = reset;
      }
      if (expiry > lastReset)
      {
        fv += ForwardStartingOptionValue(type, lastReset, expiry,
          spot, alpha, rfr, qrate, sigma, divs,
          ref delta, ref gamma, ref theta, ref vega, ref rho);
      }
      return fv;
    }

    #endregion
  }
}
