/*
 * RateVolatilityParametricSabrCalibrator.cs
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
  ///   Calibrates a Forward Volatility Cube using the SABR model parameters directly provided.
  /// </summary>
  [Serializable]
  public class RateVolatilityParametricSabrCalibrator
    : RateVolatilitySabrCalibrator, IVolatilityTenorsProvider
  {
    #region Constructors

    /// <summary>
    /// Construct SABR interpolator directly from model parameters 
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="dc">Discount curve</param>
    /// <param name="projectionCurve">Projection curve</param>
    /// <param name="rateIndex">Projection index</param>
    /// <param name="volatilityType">Vol type</param>
    /// <param name="expiries">Caplet expiries</param>
    /// <param name="dates">Running time</param>
    /// <param name="strikes">Caplet strikes</param>
    /// <param name="alphaDates">Alpha curve abscissas</param>
    /// <param name="alphaValues">Alpha curve ordinates</param>
    /// <param name="betaDates">Beta curve abscissas</param>
    /// <param name="betaValues">Beta curve ordinates</param>
    /// <param name="rhoDates">Rho curve abscissas</param>
    /// <param name="rhoValues">Rho curve ordinates</param>
    /// <param name="nuDates">Nu curve abscissas</param>
    /// <param name="nuValues">Nu curve ordinates</param>
    public RateVolatilityParametricSabrCalibrator(
      Dt asOf,
      DiscountCurve dc,
      DiscountCurve projectionCurve,
      InterestRateIndex rateIndex,
      VolatilityType volatilityType,
      Dt[] expiries,
      Dt[] dates,
      double[] strikes,
      Dt[] alphaDates,
      double[] alphaValues,
      Dt[] betaDates,
      double[] betaValues,
      Dt[] rhoDates,
      double[] rhoValues,
      Dt[] nuDates,
      double[] nuValues)
      : base(asOf, dc, projectionCurve, rateIndex, volatilityType, expiries, dates, strikes)
    {
      alphaDates_ = alphaDates;
      alphaValues_ = alphaValues;
      betaDates_ = betaDates;
      betaValues_ = betaValues;
      rhoDates_ = rhoDates;
      rhoValues_ = rhoValues;
      nuDates_ = nuDates;
      nuValues_ = nuValues;
    }

    #endregion

    #region Properties

    #endregion

    #region Methods

    /// <summary>
    /// Fit the cube
    /// </summary>
    /// <param name="surface">The volatility cube.</param>
    /// <param name="fromIdx">From idx.</param>
    public override void FitFrom(CalibratedVolatilitySurface surface, int fromIdx)
    {
      var cube = (RateVolatilityCube)surface;
      // Build curves

      // Setup Curves
      if (alphaCurve_ == null) alphaCurve_ = new Curve(AsOf);
      alphaCurve_.Clear();
      if (betaCurve_ == null) betaCurve_ = new Curve(AsOf);
      betaCurve_.Clear();
      if (rhoCurve_ == null) rhoCurve_ = new Curve(AsOf);
      rhoCurve_.Clear();
      if (nuCurve_ == null) nuCurve_ = new Curve(AsOf);
      nuCurve_.Clear();

      // Add Points
      alphaCurve_.Add(alphaDates_, alphaValues_);
      betaCurve_.Add(betaDates_, betaValues_);
      rhoCurve_.Add(rhoDates_, rhoValues_);
      nuCurve_.Add(nuDates_, nuValues_);

      // Read bootstrapped curves into fwd-fwd vol cube
      var fwdVols = cube.FwdVols;
      for (int i = 0; i < Strikes.Length; i++)
      {
        for (int j = 0; j < Expiries.Length; j++)
        {
          Dt tenorDate = Dt.Roll(Dt.Add(Expiries[j], RateIndex.IndexTenor), RateIndex.Roll, RateIndex.Calendar);
          double forwardRate = RateProjectionCurve.F(Expiries[j], tenorDate, RateIndex.DayCount, Frequency.None);

          double vol = CapletVolatility(Expiries[j], forwardRate, Strikes[i]);

          for (int k = 0; k < Dates.Length; k++)
          {
            if (Dates[k] <= Expiries[j]) fwdVols.AddVolatility(k, j, i, vol);
            else fwdVols.AddVolatility(k, j, i, 0);
          }
        }
      }

      // Set this calibrator as the cube's interpolator
      cube.RateVolatilityInterpolator = this;
    }
    #endregion

    #region data

    private readonly Dt[] alphaDates_;
    private readonly double[] alphaValues_;
    private readonly Dt[] betaDates_;
    private readonly double[] betaValues_;
    private readonly Dt[] nuDates_;
    private readonly double[] nuValues_;
    private readonly Dt[] rhoDates_;
    private readonly double[] rhoValues_;

    #endregion

    #region IVolatilityTenorsProvider Members

    IEnumerable<IVolatilityTenor> IVolatilityTenorsProvider.EnumerateTenors()
    {
      for (int index = 0; index < alphaDates_.Length; ++index)
      {
        int di = index;
        var date = alphaDates_[di];
        yield return new BasicVolatilityTenor(date.ToString(), date,
          new VolatilityList(this, di));
      }
    }


    [Serializable]
    private class VolatilityList : FixedSizeList<double>
    {
      private readonly RateVolatilityParametricSabrCalibrator _cal;
      private readonly int _dateIndex;

      public VolatilityList(RateVolatilityParametricSabrCalibrator cal, int d)
      {
        _cal = cal;
        _dateIndex = d;
      }

      public override double this[int index]
      {
        get { return _cal.alphaValues_[_dateIndex]; }
        set { _cal.alphaValues_[_dateIndex] = value; }
      }

      public override int Count
      {
        get { return 1; }
      }
    }

    #endregion
  }
}
