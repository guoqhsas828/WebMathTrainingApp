/*
 * DoubleBarrierOptionPricer.PartialProxy.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models.BGM
{
  public abstract class DoubleBarrierOptionPricer : Native.DoubleBarrierOptionPricer
  {
    public static double P(FxOption option,
      Barrier lowerBarrier, Barrier upperBarrier,
      double S, Dt settle, Dt maturity, Curve volCurve,
      FxCurveSet fcs, int flags)
    {
      double pv;
      if (option.IsRegular)
      {
        return P(option.Type, S, option.Strike,
          lowerBarrier.BarrierType, lowerBarrier.Value,
          upperBarrier.BarrierType, upperBarrier.Value,
          settle, maturity, volCurve,
          fcs.DomesticDiscountCurve, fcs.ForeignDiscountCurve,
          fcs.BasisCurve, fcs.FxFactorCurve,
          flags);
      }

      const double bumpSize = 1e-8;
      double pu = P(option.Type, S, option.Strike + bumpSize,
        lowerBarrier.BarrierType, lowerBarrier.Value,
        upperBarrier.BarrierType, upperBarrier.Value,
        settle, maturity, volCurve,
        fcs.DomesticDiscountCurve, fcs.ForeignDiscountCurve,
        fcs.BasisCurve, fcs.FxFactorCurve,
        flags);
      double pd = P(option.Type, S, option.Strike - bumpSize,
        lowerBarrier.BarrierType, lowerBarrier.Value,
        upperBarrier.BarrierType, upperBarrier.Value,
        settle, maturity, volCurve,
        fcs.DomesticDiscountCurve, fcs.ForeignDiscountCurve,
        fcs.BasisCurve, fcs.FxFactorCurve,
        flags);
      pv = ((pu - pd)/(2*bumpSize))*(option.Type == OptionType.Call ? -1.0 : 1.0);
      Debug.Assert(pv > -bumpSize*500);
      return Math.Max(pv, 0.0);
    }

    public static double NoTouchProbability(Dt settle, Dt maturity, double S, double L, double U,
      Curve volCurve, FxCurveSet fcs, int flags)
    {
      if (U == L)
      {
        return 0.0;
      }
      else
      {
        if((flags&TimeDependentBarrierOption.UseOldModel) != 0)
        {
          volCurve = Curve.CreateForwardVolatilityCurve(volCurve,null);
        }
        double dkoCall = P(OptionType.Call, S, L,
          OptionBarrierType.DownOut, L, OptionBarrierType.UpOut, U,
          settle, maturity, volCurve,
          fcs.DomesticDiscountCurve, fcs.ForeignDiscountCurve,
          fcs.BasisCurve, fcs.FxFactorCurve,
          flags);
        double dkoPut = P(OptionType.Put, S, U,
          OptionBarrierType.DownOut, L, OptionBarrierType.UpOut, U,
          settle, maturity, volCurve,
          fcs.DomesticDiscountCurve, fcs.ForeignDiscountCurve,
          fcs.BasisCurve, fcs.FxFactorCurve,
          flags);
        var p = (dkoCall + dkoPut) / (U - L);
        return p / fcs.DomesticDiscountCurve.DiscountFactor(settle, maturity);
      }
    }

    public static double TouchOptionPrice(bool isNoTouch, bool payAtHit,
      double time, double S, double r1, double r2,
      double L, double U, double sigma)
    {
      if (isNoTouch)
      {
        return NoTouchProbability(time, S, r1, r2, L, U, sigma, true);
      }

      if (!payAtHit)
      {
        return (1-NoTouchProbability(time, S, r1, r2, L, U, sigma))
          *Math.Exp(-r1*time);
      }

      return OneSideTouchPayAtHit(time, S, r1, r2, L, U, sigma)
        + OneSideTouchPayAtHit(time, S, r1, r2, U, L, sigma);
    }

    /// <summary>
    /// Calculates the probability that neither of the upper/lower barriers
    /// is hit during the lifetime of the option.
    /// </summary>
    /// <param name="time">The time to expiry</param>
    /// <param name="S">The spot rate</param>
    /// <param name="r1">The value of rate 1</param>
    /// <param name="r2">The value of rate 2</param>
    /// <param name="L">The lower barrier</param>
    /// <param name="U">The upper barrier</param>
    /// <param name="sigma">The volatility</param>
    /// <param name="discounted">if set to <c>true</c> [discounted].</param>
    /// <returns>System.Double.</returns>
    public static double NoTouchProbability(
      double time, double S, double r1, double r2,
      double L, double U, double sigma,
      bool discounted = false)
    {
      if (!(U > S && S > L)) return 0;

      const double pi = Math.PI, epsilon = 2E-16;

      double Z = Math.Log(U/L),
        s2 = sigma*sigma,
        halfS2T = s2*time/2,
        alpha = 0.5 + (r2 - r1)/s2,
        alpha2 = alpha*alpha,
        beta = -alpha2 - 2*r1/s2;
      double sL = Math.Pow(S/L, alpha),
        sU = Math.Pow(S/U, alpha),
        sLsUsum = sL + sU,
        slsUdif = (sL - sU)/sLsUsum,
        piZ = pi/Z, piZ2 = piZ*piZ,
        sinTerm = piZ*Math.Log(S/L);
      bool isOne = true;
      double sum = 0;
      for (int i = 1; i < 500; ++i)
      {
        double ipz2 = i*piZ2, iipz2 = i*ipz2;
        double e = ipz2/(alpha2 + iipz2)*Math.Exp(-iipz2*halfS2T);
        sum += (isOne ? 1.0 : slsUdif)*Math.Sin(i*sinTerm)*e;
        if (e <= epsilon)
          break;
        isOne = !isOne;
      }
      return sum*(2/pi)*sLsUsum*Math.Exp(
        beta*halfS2T + (discounted ? 0 : r1*time));
    }


    /// <summary>
    ///  Calculate the value of the double barrier option
    ///   which pays 1 if a specific barrier is hit first,
    ///   and it pays nothing otherwise.  The payment is made
    ///   at the hit time.
    /// </summary>
    /// <param name="time">The time to expiry</param>
    /// <param name="S">The spot rate</param>
    /// <param name="r1">The value of rate 1</param>
    /// <param name="r2">The value of rate 2</param>
    /// <param name="barrierPay">The barrier which, when hit first, 
    ///   pays a unit amount immediately</param>
    /// <param name="barrierOut">The barrier which, when hit first,
    ///   makes the option void</param>
    /// <param name="sigma">The volatility</param>
    /// <returns>System.Double.</returns>
    public static double OneSideTouchPayAtHit(
      double time, double S, double r1, double r2,
      double barrierPay, double barrierOut, double sigma)
    {
      const double pi = Math.PI,
        halfPi = pi/2,
        epsilon = 2E-16;

      double Z = Math.Log(barrierOut/barrierPay),
        s2 = sigma*sigma,
        halfS2T = s2*time/2,
        alpha = 0.5 + (r2 - r1)/s2,
        alpha2 = alpha*alpha,
        beta = -alpha2 - 2*r1/s2,
        sL = Math.Pow(S/barrierPay, alpha),
        piZ = pi/Z, piZ2 = piZ*piZ,
        logSoL = Math.Log(S/barrierPay), sinTerm = piZ*logSoL;
      double sum = 0;
      for (int i = 1; i < 500; ++i)
      {
        double iipz2 = i*i*piZ2,
          betaTerm = iipz2 - beta,
          e = (beta - iipz2*Math.Exp(-betaTerm*halfS2T))/betaTerm/(i*halfPi);
        sum += Math.Sin(i*sinTerm)*e;
        if (Math.Abs(e) <= epsilon)
          break;
      }
      return (sum + 1 - logSoL/Z)*sL;
    }

    public static double NoTouchIndicator(
      double time, double S, double r, double d,
      double L, double U)
    {
      Debug.Assert(U > S);
      Debug.Assert(S > L);
      double sT = S + (r - d) * time;
      return (sT >= U || sT <= L) ? 1.0 : 0.0;
    }
  }
}
