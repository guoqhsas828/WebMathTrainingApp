/*
 * TimeDependentBarrierOption.PartialProxy.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  ///   Barrier option pricing model allowing time dependent volatility and rates.
  /// </summary>
  public abstract class TimeDependentBarrierOption: Native.TimeDependentBarrierOption
  {
    internal static double P(
      bool isRegular,
      OptionType optionType, double S, double K,
      OptionBarrierType barrierType, double H,
      Dt settle, Dt windowEnd, Dt maturity, Curve volCurve,
      DiscountCurve domesticCurve, FxCurve fxCurve, int flags)
    {
      const DiscountCurve foreignCurve = null, basisCurve = null;
      if (domesticCurve.Ccy != fxCurve.SpotFxRate.ToCcy)
        flags |= InvertFxCurve;

      double pv;
      if (isRegular || IsTouchOption(flags))
      {
        pv = P(optionType, S, K, barrierType, H, settle, windowEnd, maturity,
          volCurve, domesticCurve, foreignCurve, basisCurve, fxCurve,
          flags);
      }
      else
      {
        var bumpSize = 1e-8;
        var pu = P(optionType, S, K + bumpSize, barrierType, H, settle,
          windowEnd, maturity, volCurve, domesticCurve, foreignCurve, basisCurve,
          fxCurve, flags);
        var pd = P(optionType, S, K - bumpSize, barrierType, H, settle,
          windowEnd, maturity, volCurve, domesticCurve, foreignCurve, basisCurve,
          fxCurve, flags);

        pv = ((pu - pd)/(2*bumpSize))*
          (optionType == OptionType.Call ? -1.0 : 1.0);
      }

      // Done
      return pv;
    }
    private static bool IsTouchOption(int flags)
    {
      return (flags & (int)(OptionBarrierFlag.NoTouch
        | OptionBarrierFlag.OneTouch)) != 0;
    }

    internal static double CalculateTouchProbability(
      double S, double H, bool down,
      double sigma, Dt settle, Dt maturity,
      DiscountCurve domesticCurve,
      FxCurve fxCurve, bool symmetric)
    {
      var T = (maturity - settle)/365.0;
      var domesticDf = domesticCurve.DiscountFactor(settle, maturity);
      var fxFactor = fxCurve.Interpolate(settle, maturity);
      double foreignDf;
      if (domesticCurve.Ccy != fxCurve.SpotFxRate.ToCcy)
      {
        foreignDf = domesticDf/fxFactor;
        S = 1/S;
        H = 1/H;
        down = !down;
      }
      else
      {
        foreignDf = domesticDf*fxFactor;
      }
      var rf = -Math.Log(foreignDf)/T;
      var rd = -Math.Log(domesticDf)/T;
      var p0 = down
        ? CalculateDownInProbability(S, H, T, sigma, rd, rf)
        : CalculateUpInProbability(S, H, T, sigma, rd, rf);
      if(!symmetric) return p0;
      var p1 = down
        ? CalculateUpInProbability(1/S, 1/H, T, sigma, rf, rd)
        : CalculateDownInProbability(1/S, 1/H, T, sigma, rf, rd);
      return (p0 + p1)/2;
    }

    /// <summary>
    /// Calculate implied volatility
    /// </summary>
    /// <param name="type"></param>
    /// <param name="barrierType"></param>
    /// <param name="T">Time to expiration</param>
    /// <param name="S">Underlying asset price</param>
    /// <param name="K">Strike</param>
    /// <param name="H"></param>
    /// <param name="rebate"></param>
    /// <param name="rd"></param>
    /// <param name="rf"></param>
    /// <param name="flags"></param>
    /// <param name="price">Option price to imply volatility from</param>
    /// <returns>Implied volatility</returns>
    public static double ImpliedVolatility(
      OptionType type,
      OptionBarrierType barrierType,
      double T,
      double S,
      double K,
      double H,
      double rebate,
      double rd,
      double rf,
      int flags,
      double price
      )
    {
      double vol;
      // Target function
      Double_Double_Fn fn = delegate(double v, out string msg)
                              {
                                msg = null;
                                double val = double.NaN;
                                try
                                {
                                  val = Price(type, barrierType, T, S, K, H, rebate, rd, rf, v, flags);
                                }
                                catch (Exception ex)
                                {
                                  msg = ex.ToString();
                                }
                                return val;
                              };

      // Setup solver
      SolverFn f = new DelegateSolverFn(fn, null);
      Brent2 solver = new Brent2();
      solver.setLowerBounds(1E-15);
      solver.setLowerBracket(0.2);
      solver.setUpperBracket(0.8);

      try
      {
        // Solve
        vol = solver.solve(f, price, 0.5);
      }
      catch (SolverException)
      {
        vol = double.NaN;
      }

      // Done
      return vol;
    }
  }

  /// <summary>
  ///  Extension methods
  /// </summary>
  public static class TimeDependentBarrierOptionExtensions
  {
    /// <summary>
    /// Calculates the average volatility.
    /// </summary>
    /// <param name="blackVolCurve">The black vol curve.</param>
    /// <param name="start">The start.</param>
    /// <param name="end">The end.</param>
    /// <returns></returns>
    public static double CalculateAverageVolatility(this Curve blackVolCurve, Dt start, Dt end)
    {
      if (blackVolCurve == null)
      {
        throw new ToolkitException("No volatility curve provided.");
      }
      double v1 = blackVolCurve.Interpolate(start);
      if (start == end)
      {
        return v1;
      }
      Dt asOf = blackVolCurve.AsOf;
      double t1 = Dt.FractDiff(asOf, start);
      double t2 = Dt.FractDiff(asOf, end);
      double v2 = blackVolCurve.Interpolate(end);
      double v = (t2 * v2 * v2 - t1 * v1 * v1) / (t2 - t1);
      return Math.Sqrt(v);
    }

  }
}
