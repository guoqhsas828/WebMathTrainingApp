/*
 * RateVolatilityForwardForwardCalibrator.cs
 *
 *   2005-2011. All rights reserved.
 * 
 */

using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///   Calibrates a forward volatility cube from a set of forward-forward volatilities.
  /// </summary>
  [Serializable]
  public class RateVolatilityForwardForwardCalibrator : RateVolatilityCalibrator, IVolatilityTenorsProvider
  {
    #region Constructors

    /// <summary>
    /// Construct volatility cube from forward forward volatilities
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="rateIndex">Libor index</param>
    /// <param name="volatilityType">Volatility type (normal/lognormal)</param>
    /// <param name="dates">Running time</param>
    /// <param name="expiries">Caplet expiries</param>
    /// <param name="strikes">Caplet strikes</param>
    /// <param name="vols">Forward forward volatilities</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="projectionCurve">Projection curve</param>
    public RateVolatilityForwardForwardCalibrator(
      Dt asOf,
      InterestRateIndex rateIndex,
      VolatilityType volatilityType,
      Dt[] dates,
      Dt[] expiries,
      double[] strikes,
      double[,,] vols,
      DiscountCurve discountCurve,
      DiscountCurve projectionCurve
      )
      : base(asOf, discountCurve, projectionCurve, rateIndex, volatilityType, dates, expiries, strikes)
    {
      FillMissingPoints(vols);
      volatilities_ = vols;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Fit the cube
    /// </summary>
    /// <param name="surface">The cube.</param>
    /// <param name="fromIdx">From idx.</param>
    public override void FitFrom(CalibratedVolatilitySurface surface, int fromIdx)
    {
      //Clear out cube
      var fwdVols = ((RateVolatilityCube)surface).FwdVols;
      fwdVols.Clear();
      // Add volatilities
      for (int strike = 0; strike < Strikes.Length; strike++)
      {
        for (int expiry = 0; expiry < Expiries.Length; expiry++)
        {
          for (int date = 0; date < Dates.Length; date++)
          {
            fwdVols.AddVolatility(date, expiry, strike, volatilities_[strike, expiry, date]);
          }
        }
      }
    }

    /// <summary>
    /// Interpolates a volatility at given date and strike based on the specified cube.
    /// </summary>
    /// <param name="surface">The volatility cube.</param>
    /// <param name="expiryDate">The expiry date.</param>
    /// <param name="forwardRate">The forward rate.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    public override double Interpolate(CalibratedVolatilitySurface surface,
                                       Dt expiryDate, double forwardRate, double strike)
    {
      return CalcCapletVolatilityFromCube(surface, expiryDate, strike);
    }

    #endregion

    #region Factory Methods

    /// <summary>
    ///   Creates a calibrator from a table of forward-forward volatility quotes.
    /// </summary>
    /// <param name = "asOf">As of.</param>
    /// <param name = "rateIndex">Interest rate index.</param>
    /// <param name = "volType">Type of the vol.</param>
    /// <param name = "dateColumn">The forward dates.</param>
    /// <param name = "expiryColumn">The expiries.</param>
    /// <param name = "strikeColumn">The strikes.</param>
    /// <param name = "volatilityColumn">The volatilities.</param>
    /// <param name="discountCurve"></param>
    /// <param name="projectionCurve"></param>
    /// <returns>RateVolatilityForwardForwardCalibrator</returns>
    public static RateVolatilityForwardForwardCalibrator CreateFromForwardForwardVolatilityTable(Dt asOf,
      InterestRateIndex
        rateIndex,
      VolatilityType volType,
      Dt[] dateColumn,
      Dt[] expiryColumn,
      double[] strikeColumn,
      double[] volatilityColumn,
      DiscountCurve discountCurve,
      DiscountCurve projectionCurve)
    {
      var expiries = new List<Dt>();
      var dates = new List<Dt>();
      var strikes = new List<double>();
      for (int i = 0; i < dateColumn.Length; ++i)
      {
        // Add to lists
        if (!expiries.Contains(expiryColumn[i]))
          expiries.Add(expiryColumn[i]);
        if (!dates.Contains(dateColumn[i]))
          dates.Add(dateColumn[i]);
        if (!strikes.Contains(strikeColumn[i]))
          strikes.Add(strikeColumn[i]);
      }
      // Sort
      expiries.Sort();
      dates.Sort();
      strikes.Sort();

      // Fill out vols
      var vols = new double[strikes.Count, expiries.Count, dates.Count];
      for (int i = 0; i < dateColumn.Length; ++i)
      {
        // Get index
        int e = expiries.IndexOf(expiryColumn[i]);
        int d = dates.IndexOf(dateColumn[i]);
        int s = strikes.IndexOf(strikeColumn[i]);
        vols[s, e, d] = volatilityColumn[i];
      }

      // Create Calibrator
      var calibrator = new RateVolatilityForwardForwardCalibrator(asOf, rateIndex, volType, dates.ToArray(), expiries.ToArray(), strikes.ToArray(), vols,
        discountCurve, projectionCurve);
      // Done
      return calibrator;
    }

    private static void FillMissingPoints(double[,,] vols)
    {
      int ni = vols.GetLength(0),
        nj = vols.GetLength(1),
        nk = vols.GetLength(2);
      for (int i = 0; i < ni; ++i)
      {
        for (int j = 0; j < nj; ++j)
          for (int k = 0; k < nk; ++k)
          {
            if (vols[i, j, k] > 0) continue;
            FillMissingPoint(vols, i, j, k);
          }
      }
    }

    private static bool FillMissingPoint(double[,,] vols, int i, int j, int k)
    {
      for (int h = k; --h >= 0; )
      {
        if (!(vols[i, j, h] > 0)) continue;
          vols[i, j, k] = vols[i, j, h];
        return true;
      }
      for (int h = k, n = vols.GetLength(2); ++h < n; )
      {
        if (!(vols[i, j, h] > 0)) continue;
        vols[i, j, k] = vols[i, j, h];
        return true;
      }
      return false;
    }

    #endregion

    #region Data

    // Readonly data
    private readonly double[,,] volatilities_;

    #endregion

    #region IVolatilityTenorsProvider Members

    IEnumerable<IVolatilityTenor> IVolatilityTenorsProvider.EnumerateTenors()
    {
      for (int dIndex = 0; dIndex < Expiries.Length; ++dIndex)
      {
        int d = dIndex;
        var date = Dates[dIndex];
        for (int eIndex = 0; eIndex < Expiries.Length; ++eIndex)
        {
          int e = eIndex;
          var rate = Expiries[eIndex];
          if (rate < date) continue;
          yield return new BasicVolatilityTenor(
            String.Format("{0}/{1}", date, rate),
            date, new VolatilityList(this, d, e));
        }
      }
    }

    [Serializable]
    private class VolatilityList : FixedSizeList<double>
    {
      private readonly RateVolatilityForwardForwardCalibrator _cal;
      private readonly int _dateIdx, _expiryIdx;

      public VolatilityList(RateVolatilityForwardForwardCalibrator cal,
        int dateIndex, int expiryIndex)
      {
        _cal = cal;
        _dateIdx = dateIndex;
        _expiryIdx = expiryIndex;
      }

      public override double this[int strikeIndex]
      {
        get { return _cal.volatilities_[strikeIndex, _expiryIdx, _dateIdx]; }
        set { _cal.volatilities_[strikeIndex, _expiryIdx, _dateIdx] = value; }
      }

      public override int Count
      {
        get { return _cal.Strikes.Length; }
      }
    }

    #endregion
  }
}
