/*
 * SwaptionVolatilityBaseCalibrator
 *
 *  -2011. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using System.Collections;
using BaseEntity.Toolkit.Calibrators.Volatilities;

namespace BaseEntity.Toolkit.Calibrators
{
  ///<summary>
  /// Base class for all swaption volatility related calibrators
  ///</summary>
  [Serializable]
  public class SwaptionVolatilityBaseCalibrator : RateVolatilityCalibrator
  {
    ///<summary>
    ///</summary>
    ///<param name="asOf">Pricing date</param>
    ///<param name="rateIndex">Interest rate index</param>
    ///<param name="discountCurve">Discount curve</param>
    ///<param name="referenceCurve">Reference curve</param>
    ///<param name="volType">Volatility type</param>
    public SwaptionVolatilityBaseCalibrator(Dt asOf, InterestRateIndex rateIndex, DiscountCurve discountCurve,
      DiscountCurve referenceCurve, VolatilityType volType) :
      base(asOf, discountCurve, referenceCurve, rateIndex, volType, null, null, null)
    {
    }

    /// <summary>
    /// Fit the cube
    /// </summary>
    /// <param name = "surface">The cube.</param>
    /// <param name = "fromIdx">From idx.</param>
    public sealed override void FitFrom(CalibratedVolatilitySurface surface, int fromIdx)
    {
      var cube = surface as SwaptionVolatilityCube;
      if (cube != null)
      {
        cube.Skew.Fit();
        var cs = cube.VolatilitySurface;
        if (cs != null) cs.Fit();
        return;
      }

      var spline = surface as SwaptionVolatilitySpline;
      if (spline == null)
        throw new ArgumentException(
          "RateVolatilitySwaptionMarketCalibrator is supporting SwaptionVolatilityCube only");

      spline.TenorAxis = new List<double>();
      spline.ExpiryAxis = new List<double>();
      foreach (RateVolatilityTenor tenor in spline.Tenors)
      {
        var expiry = Dt.Diff(AsOf, tenor.Maturity)/365.0;
        if (!spline.ExpiryAxis.Contains(expiry))
          spline.ExpiryAxis.Add(expiry);

        var maturity = SwaptionVolatilityCube.ConvertForwardTenor(tenor.ForwardTenor);
        if (!spline.TenorAxis.Contains(maturity))
          spline.TenorAxis.Add(maturity);

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
    public sealed override double Interpolate(CalibratedVolatilitySurface surface, Dt expiryDate, double forwardRate, double strike)
    {
      throw new NotImplementedException();
    }

    /// <summary>
    /// Validate
    /// </summary>
    /// <param name="errors">Errors</param>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if (RateIndex == null)
        InvalidValue.AddError(errors, this, "RateIndex", "RateIndex can not be null for successful calibration");
    }

    /// <summary>
    /// Interpolate
    /// </summary>
    /// <param name="surface"></param>
    /// <param name="expiryDate"></param>
    /// <param name="duration"></param>
    /// <param name="strike"></param>
    /// <returns></returns>
    public double Evaluate(RateVolatilitySurface surface, Dt expiryDate, double duration, double strike)
    {
      var cube = surface as SwaptionVolatilityCube;
      var spline = cube != null ? cube.Skew : (SwaptionVolatilitySpline)surface;
      var interp = (ISwapVolatilitySurfaceInterpolator)spline.RateVolatilityInterpolator;
      var calibrator = (SwaptionVolatilityBaseCalibrator)spline.RateVolatilityCalibrator;
      var swapEffective = Dt.AddDays(expiryDate, NotificationDays, NotifyCalendar);
      var atmStrike = CurveUtil.DiscountForwardSwapRate(calibrator.RateProjectionCurve, swapEffective,
                                                        Dt.Add(swapEffective, Convert.ToInt32(365 * duration /12.0)),
                                                        calibrator.SwapDayCount, calibrator.SwapFreq,
                                                        calibrator.SwapRoll, calibrator.RateIndex.Calendar);
      return interp.Interpolate(surface, expiryDate, strike - atmStrike, duration);
    }

    #region Properties
    /// <summary>
    /// Swap fixed leg day-count
    /// </summary>
    public DayCount SwapDayCount { get; set; }

    /// <summary>
    /// Swap fixed leg payment frequency
    /// </summary>
    public Frequency SwapFreq { get; set; }

    /// <summary>
    /// Swap roll convention
    /// </summary>
    public BDConvention SwapRoll { get; set; }

    /// <summary>
    /// Swap calendar
    /// </summary>
    public Calendar SwapCal { get; set; }

    ///<summary>
    /// List of expiry tenors
    ///</summary>
    public IList<Tenor> ExpiryTenors { get; set; }
    ///<summary>
    /// List of forward tenors
    ///</summary>
    public IList<Tenor> ForwardTenors { get; set; }

    ///<summary>
    /// Swaption notification days
    ///</summary>
    public int NotificationDays { get; set; }

    /// <summary>
    /// Notification business day calendar
    /// </summary>
    public Calendar NotifyCalendar { get; set; }
    #endregion
  }
}
