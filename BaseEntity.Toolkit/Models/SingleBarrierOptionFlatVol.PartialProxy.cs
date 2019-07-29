/*
 * SingleBarrierOptionFlatVol.cs
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

namespace BaseEntity.Toolkit.Models.BGM
{
  /// <summary>
  /// Single barrier option volatility
  /// </summary>
  public abstract class SingleBarrierOptionFlatVol : Native.SingleBarrierOptionFlatVol
  {
    /// <summary>
    /// Barrier option price
    /// </summary>
    /// <param name="settle">Settle date</param>
    /// <param name="maturity">Option expiry</param>
    /// <param name="S">Spot Fx rate</param>
    /// <param name="K">Strike</param>
    /// <param name="H">Barrier level</param>
    /// <param name="optionType">Option type</param>
    /// <param name="isDigital">True if digital option</param>
    /// <param name="barrierType">Barrier type</param>
    /// <param name="foreignCurve">Foreign funding curve</param>
    /// <param name="domesticCurve">Domestic funding curve</param>
    /// <param name="volCurve">Black volatility curve</param>
    /// <param name="basisCurve">Fx basis curve </param>
    /// <returns>Option price</returns>
    public static double P(Dt settle, Dt maturity, double S, double K, double H, OptionType optionType, 
                            bool isDigital, OptionBarrierType barrierType, Curve foreignCurve, 
                            Curve domesticCurve, Curve volCurve,Curve basisCurve)
    {
      double T = (maturity - settle)/365.0;
      double rd = -Math.Log(domesticCurve.Interpolate(settle, maturity))/T;
      double rf = -Math.Log(foreignCurve.Interpolate(settle, maturity))/T;
      double basis = -Math.Log(basisCurve.Interpolate(settle, maturity))/T;
      double vol = volCurve.CalculateAverageVolatility(settle, maturity);

      double pv;
      if (!isDigital)
      {
        pv = P(S, K, H, optionType, barrierType, settle, maturity, rf, rd, basis, vol,0.0);
      }
      else
      {
        var bumpSize = 1e-8;
        var pu = P(S, K + bumpSize, H, optionType, barrierType,settle,maturity,rf,rd,basis,vol,0.0);
        var pd = P(S, K - bumpSize, H, optionType, barrierType,settle,maturity,rf,rd,basis,vol,0.0);

        pv = ((pu - pd) / (2 * bumpSize)) * (optionType == OptionType.Call ? -1.0 : 1.0);
      }

      // Done
      return pv;
    }
  }
}
