/*
 * RateVolatilitySwaptionMarketCalibrator
 *
 *  -2011. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  /// Calibrator used to construct rate volatility cube from a set of swaption market quotes
  /// </summary>
  [Serializable]
  public class RateVolatilitySwaptionMarketCalibrator : SwaptionVolatilityBaseCalibrator
  {
    /// <summary>
    ///   Constructor for single curve calibrator
    /// </summary>
    /// <param name = "asOf">as of date</param>
    /// <param name = "discountCurve">discount curve</param>
    /// <param name = "fwdTenors">Forward tenors</param>
    /// <param name = "rateIndex">Interest rate index</param>
    /// <param name = "volatilityType">volatility Type</param>
    /// <param name="expiries">Swaption expiration dates</param>
    /// <param name="swapDc">Swap day count</param>
    /// <param name="swapRoll">Swap BD convention</param>
    /// <param name="swapFreq">Swap fix leg payment frequency</param>
    /// <param name="notifyCal">Swap calendar</param>
    /// <param name="notifyDays">Swaption notification days</param>
    internal RateVolatilitySwaptionMarketCalibrator(Dt asOf, int notifyDays, DiscountCurve discountCurve, IList<Tenor> expiries,
                                          IList<Tenor> fwdTenors, InterestRateIndex rateIndex, VolatilityType volatilityType,
                                          DayCount swapDc, BDConvention swapRoll, Frequency swapFreq, Calendar notifyCal)
      : base(asOf, rateIndex, discountCurve, null, volatilityType)
    {
      NotificationDays = notifyDays;
      ForwardTenors = fwdTenors;
      ExpiryTenors = expiries;
      Strikes = new double[] {0};
      Dates = CollectionUtil.ConvertAll(expiries, t => RateVolatilityUtil.SwaptionStandardExpiry(asOf, rateIndex, t));
      Expiries = CollectionUtil.ConvertAll(fwdTenors, t => Dt.Add(Dates[0], t));
      SwapDayCount = swapDc;
      SwapRoll = swapRoll;
      SwapFreq = swapFreq;
      SwapCal = rateIndex.Calendar;
      NotifyCalendar = notifyCal;
      Validate();
    }

    #region Methods

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// <remarks>
    ///   By default validation is metadata-driven.  Entities that enforce
    ///   additional constraints can override this method.  Methods that do
    ///   override must first call Validate() on the base class.
    /// </remarks>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(System.Collections.ArrayList errors)
    {
      base.Validate(errors);
      if (ForwardTenors.Count == 0)
        InvalidValue.AddError(errors, this, "ForwardTenors", "Forward tenor list can not be empty");

      if (ExpiryTenors.Count == 0)
        InvalidValue.AddError(errors, this, "ExpiryTenors", "ExpiryTernos can not be empty");
    }

    #endregion
  }
}
