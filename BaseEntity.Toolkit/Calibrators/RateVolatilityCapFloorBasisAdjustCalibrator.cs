/*
 * RateVolatilityCapFloorBasisAdjustCalibrator
 *
 *  -2011. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators.Volatilities;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  /// Rate volatility calibrator that puts the basis adjustments on top of rate volatility surface to creates a forward volatility cube
  /// </summary>
  [Serializable]
  public class RateVolatilityCapFloorBasisAdjustCalibrator : SwaptionVolatilityBaseCalibrator
  {
    #region Constructors

    /// <summary>
    ///   Constructor for dual curve calibrator
    /// </summary>
    /// <param name = "asOf">as of date</param>
    /// <param name = "discountCurve">discount curve</param>
    /// <param name = "fwdTenors">Forward tenors</param>
    /// <param name = "rateIndex">Interest rate index</param>
    /// <param name = "volatilityType">Volatility Type</param>
    /// <param name="projectionCurve">Rate projection curve</param>
    /// <param name="expiries">Swaption expiration dates</param>
    /// <param name="swapDc">Swap day count</param>
    /// <param name="swapRoll">Swap BD convention</param>
    /// <param name="swapFreq">Swap fix leg payment frequency</param>
    /// <param name="notifyCal">Swap calendar</param>
    /// <param name="notifyDays">Swaption notification days</param>
    /// <param name="strikeShifts">Strike shifts</param>
    internal RateVolatilityCapFloorBasisAdjustCalibrator(Dt asOf, int notifyDays, DiscountCurve discountCurve, DiscountCurve projectionCurve,
                                             IList<Tenor> expiries, IList<Tenor> fwdTenors, double[] strikeShifts, InterestRateIndex rateIndex,
                                             VolatilityType volatilityType, DayCount swapDc, BDConvention swapRoll, Frequency swapFreq, Calendar notifyCal)
      : base(asOf, rateIndex, discountCurve, projectionCurve, volatilityType)
    {
      NotificationDays = notifyDays;
      Dates = CollectionUtil.ConvertAll(expiries, t => RateVolatilityUtil.SwaptionStandardExpiry(asOf, rateIndex, t));
      Strikes = strikeShifts;
      ForwardTenors = fwdTenors;
      ExpiryTenors = expiries;
      Expiries = CollectionUtil.ConvertAll(fwdTenors, t => Dt.Add(Dates[0], t));
      SwapDayCount = swapDc;
      SwapRoll = swapRoll;
      SwapFreq = swapFreq;
      SwapCal = rateIndex.Calendar;
      NotifyCalendar = notifyCal;
      Validate();
    }

    #endregion Constructors

  }
}
