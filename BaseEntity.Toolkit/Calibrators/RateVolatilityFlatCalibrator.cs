/*
 * RateVolatilityFlatCalibrator.cs
 *
 *  -2011. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///   A backward compatibility purposed rate volatility calibrator, which 
  ///   helps build a forward volatility cube directly from a set of volatility curves
  /// </summary>
  [Serializable]
  public class RateVolatilityFlatCalibrator : RateVolatilityCalibrator, IVolatilityTenorsProvider
  {
    /// <summary>
    /// constructor for the flat volatility calibrator
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="expiries">The expiries.</param>
    /// <param name="volatilityType">Type of the volatility.</param>
    /// <param name="rateIndex">Index of the rate.</param>
    /// <param name="volatilities">The volatilities.</param>
    internal RateVolatilityFlatCalibrator(Dt asOf, Dt[] expiries, VolatilityType volatilityType,
                                          InterestRateIndex rateIndex, double[] volatilities)
      : base(asOf, null, null, rateIndex, volatilityType, expiries, expiries, new[] { 0.0 })
    {
      volatilities_ = volatilities;
    }

    /// <summary>
    /// Fit method for the rate volatility cube
    /// </summary>
    /// <param name="surface">The cube.</param>
    /// <param name="fromIdx">From idx.</param>
    public override void FitFrom(CalibratedVolatilitySurface surface, int fromIdx)
    {
      var cube = (RateVolatilityCube)surface;
      var firstCapIdx = new[] {0};
      var fwdVols = cube.FwdVols;
      fwdVols.Native.SetFirstCapIdx(firstCapIdx);
      for (int j = 0; j < volatilities_.Length; j++)
      {
        for (int k = 0; k < volatilities_.Length; k++)
        {
          fwdVols.AddVolatility(k, j, 0, volatilities_[j]);
        }
      }
    }

    /// <summary>
    /// Interpolates a volatility at given date and strike based on the specified cube.
    /// </summary>
    /// <param name="surface">The volatility cube.</param>
    /// <param name="expiryDate">The expiry date.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forwardRate">The forward rate.</param>
    /// <returns>The volatility.</returns>
    public override double Interpolate(CalibratedVolatilitySurface surface,
      Dt expiryDate, double forwardRate, double strike)
    {
      return CalcCapletVolatilityFromCube(surface, expiryDate, strike);
    }

    #region Data

    private readonly double[] volatilities_;

    #endregion

    #region IVolatilityTenorsProvider Members

    IEnumerable<IVolatilityTenor> IVolatilityTenorsProvider.EnumerateTenors()
    {
      return EnumerateTenors(AsOf, volatilities_, Expiries, null);
    }

    #endregion
  }
}
