/*
 *  -2012. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;
using QMath=BaseEntity.Toolkit.Numerics.SpecialFunctions;

namespace BaseEntity.Toolkit.Curves.Volatilities
{
  /// <summary>
  ///   The smile interpolator based on quadratic regression model.
  /// </summary>
  /// <remarks>
  /// </remarks>
  [Serializable]
  public class QuadraticRegressionSmile
  {
    private readonly double _a, _b, _c, _logFwd, _refSigmaT, _sqrtTime;
    private readonly Func<double,double> _errorCorrector;

    /// <summary>
    ///   Initializes a new instance of the <see cref="QuadraticRegressionSmile" /> class.
    /// </summary>
    /// <param name="a">The regression coefficient a.</param>
    /// <param name="b">The regression coefficient b.</param>
    /// <param name="c">The regression coefficient c.</param>
    /// <param name="logFwd">The logarithm of the forward price.</param>
    /// <param name="refSigmaT">The reference volatility times the square root of time.</param>
    /// <param name="sqrtTime">The square root of the time to expiry.</param>
    /// <param name="errorCorrector">The error corrector as the function of log moneyness.</param>
    /// <remarks>
    /// </remarks>
    public QuadraticRegressionSmile(
      double a, double b, double c,
      double logFwd, double refSigmaT, double sqrtTime,
      Func<double, double> errorCorrector)
    {
      _a = a;
      _b = b;
      _c = c;
      _logFwd = logFwd;
      _refSigmaT = refSigmaT;
      _sqrtTime = sqrtTime;
      _errorCorrector = errorCorrector;
    }

    /// <summary>
    /// Calculate the volatility at the specified input level.
    /// </summary>
    /// <param name="x">The input value.</param>
    /// <param name="xkind">The input kind.</param>
    /// <returns>System.Double.</returns>
    /// <exception cref="ToolkitException"></exception>
    public double Evaluate(double x, SmileInputKind xkind)
    {
      switch(xkind)
      {
      case SmileInputKind.Strike:
        return EvaluateStrike(x);
      case SmileInputKind.Moneyness:
        return EvaluateMoneyness(x);
      case SmileInputKind.LogMoneyness:
        return EvaluateLogMoneyness(x);
      }
      throw new ToolkitException(String.Format(
        "Invalid SmileInputKind {0}", xkind));
    }

    /// <summary>
    ///   Calculate the volatility at the specified strike.
    /// </summary>
    /// <param name="strike">The strike.</param>
    /// <returns></returns>
    /// <remarks>
    /// </remarks>
    public double EvaluateStrike(double strike)
    {
      double x = Math.Log(strike) - _logFwd;
      double n = QMath.NormalCdf(-x / _refSigmaT);
      return (_a * n + _b) * n + _c + _errorCorrector(x);
    }

    /// <summary>
    ///   Calculate the volatility at the specified moneyness.
    /// </summary>
    /// <param name="logMoneyness">The moneyness.</param>
    /// <returns></returns>
    /// <remarks>
    /// </remarks>
    public double EvaluateLogMoneyness(double logMoneyness)
    {
      double x = logMoneyness;
      double n = QMath.NormalCdf(-x / _refSigmaT);
      return (_a * n + _b) * n + _c + _errorCorrector(x);
    }

    /// <summary>
    ///   Calculate the volatility at the specified moneyness.
    /// </summary>
    /// <param name="moneyness">The moneyness.</param>
    /// <returns></returns>
    /// <remarks>
    /// </remarks>
    public double EvaluateMoneyness(double moneyness)
    {
      return EvaluateLogMoneyness(Math.Log(moneyness));
    }

    /// <summary>
    ///   Calculate the moneyness at the specified forward delta.
    /// </summary>
    /// <param name="delta">The forward delta.</param>
    /// <param name="sign">The sign indicating the option type, <c>1</c> for call option and <c>-1</c> for put option.</param>
    /// <returns>The moneyness associated with delta</returns>
    /// <remarks>
    ///   The delta is given by <m>\Delta = \Phi(\omega d_1(x))</m> where <m>\omega</m> is the sign and<math>
    ///     d_1(x) = \frac{\sigma(x)^2T - x}{\sigma(x)\sqrt{T}}
    ///     ,\quad x = \log(K/F)
    ///   </math>This function finds a value <m>x</m> to match the specified <m>\Delta</m>, and then it returns <m>\sigma(x)</m>. 
    /// </remarks>
    public double EvaluateDelta(double delta, int sign)
    {
      if (delta < 0) delta = -delta;
      if (!(delta > 0 && delta <= 1))
      {
        throw new ToolkitException(String.Format(
          "Delta {0} is out of range", delta));
      }
      var solver = new Brent2();
      solver.setToleranceX(1E-12);
      solver.setToleranceF(1E-12);
      var result = solver.solve(x =>
      {
        var sigmaT = EvaluateLogMoneyness(x) * _sqrtTime;
        return QMath.NormalCdf(sign * (0.5 * sigmaT * sigmaT - x) / sigmaT);
      }, null, delta, -1.0, 1.0);
      return result;
    }

    /// <summary>
    /// Implies the strangle volatility.
    /// </summary>
    /// <param name="forwardDelta">The delta.</param>
    /// <param name="premiumIncluded">if set to <c>true</c>, premium included delta.</param>
    /// <param name="ccy2Strangle">True for currency 2 based strangle.</param>
    /// <returns></returns>
    /// <remarks></remarks>
    public double ImplyStrangleVolatility(double forwardDelta,
      bool premiumIncluded, bool ccy2Strangle)
    {
      if (forwardDelta < 0) forwardDelta = -forwardDelta;
      if (!(forwardDelta > 0 && forwardDelta < 1))
      {
        throw new ToolkitException(String.Format(
          "{0}: Forward delta not between 0 and 1", forwardDelta));
      }
      double price, avgSigma;
      GetPriceAndAverageVolatylity(forwardDelta, out price, out avgSigma);
      return BlackScholes.ImplyStrangleVolatility(forwardDelta,
        premiumIncluded, ccy2Strangle, price, avgSigma * _sqrtTime)
          / _sqrtTime;
    }

    private void GetPriceAndAverageVolatylity(
      double delta, out double price, out double avgSigma)
    {
      var mPut = EvaluateDelta(delta, -1);
      var sigPut = EvaluateLogMoneyness(mPut);
      var mCall = EvaluateDelta(delta, 1);
      var sigCall = EvaluateLogMoneyness(mCall);
      price = BlackScholes.ForwardNormalizedOptionPrice(
        mCall, sigCall * _sqrtTime, 1)
          + BlackScholes.ForwardNormalizedOptionPrice(
            mPut, sigPut * _sqrtTime, -1);
      avgSigma = (sigCall + sigPut) / 2;
    }

  }
}



