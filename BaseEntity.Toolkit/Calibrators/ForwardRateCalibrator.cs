/*
 * ForwardRateCalibrator.cs
 *
 *  -2008. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Calibrators
{
  ///
  /// <summary>
  ///   Interest rate curve calibration using zero coupon rates
  /// </summary>
  ///
  ///
  /// <remarks>
  ///   <para>Each zero coupon bond is assumed to pay face at maturity.</para>
  ///
  ///   <para>The Zero coupon bond yields are expressed in terms of simple yields</para>
  /// </remarks>
  ///
  [Serializable]
  public class ForwardRateCalibrator : DiscountCalibrator
  {
    #region Constructors

    /// <summary>
    ///   Constructor given as-of (pricing) date
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Settlement date defaults to as-of date.</para>
    /// </remarks>
    ///
    /// <param name="asOf">As-of (pricing) date</param>
    ///
    public ForwardRateCalibrator(Dt asOf)
      : base(asOf, asOf)
    { }

    #endregion // Constructors

    #region Methods

    ///
    /// <summary>
    ///   Fit from a specified point assuming all initial test/etc. have been performed.
    /// </summary>
    ///
    /// <param name="curve">Discount curve to calibrate</param>
    /// <param name="fromIdx">Index to start fit from</param>
    ///
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      DiscountCurve discountCurve = (DiscountCurve)curve;

      // Start from scratch each time as this is fast
      discountCurve.Clear();

      // Do fit
      var tenors = discountCurve.Tenors;
      int count = tenors.Count;
      double df = 1;
      for (int i = 0; i < count; ++i)
      {
        var qh = tenors[i].QuoteHandler
          as CurveTenorQuoteHandlers.ForwardRateHandler;
        if (qh == null)
        {
          throw new Exception(String.Format("Invalid quote handler for"
            + " ForwardRateCalibrator. Must be ForwardRateQuoteHandler"));
        }
        tenors[i].MarketPv = tenors[i].ModelPv = df;
        df /= 1 + qh.Rate * Dt.Fraction(qh.Reset, qh.Maturity, qh.Reset, qh.Maturity, qh.DayCount, Frequency.None);
        discountCurve.Add(qh.Maturity, df);
      }
      return;
    } // Fit()

    /// <summary>
    /// Creates a discount curve from a set of consecutive forward rates.
    /// </summary>
    /// <param name="asOf">As-of date.</param>
    /// <param name="maturities">An array of maturity dates by rates.</param>
    /// <param name="dayCount">The day count.</param>
    /// <param name="rates">An array of the forward rates.</param>
    /// <returns>A discount curve.</returns>
    public static DiscountCurve CreateForwardRateCurve(
      Dt asOf,
      DayCount dayCount,
      IList<Dt> maturities,
      IList<double> rates)
    {
      var calibrator = new ForwardRateCalibrator(asOf);
      var curve = new DiscountCurve(calibrator);
      int last = rates.Count - 1;
      for (int i = 0; i <= last; ++i)
      {
        Dt maturity = maturities[i];
        Dt reset = i == 0 ? asOf : maturities[i - 1];
        Note note = new Note(reset, maturity,
          Currency.None, rates[i], dayCount,
          Frequency.None, BDConvention.None, Calendar.None)
        {
          Description = Utils.ToTenorName(
            asOf, maturity, TenorNameRound.Month)
        };
        curve.Add(note, 0.0);
        curve.Tenors[i].QuoteHandler = new CurveTenorQuoteHandlers
          .ForwardRateHandler(rates[i], reset, maturity, dayCount);
      }
      curve.Fit();
      return curve;
    }

    #endregion // Methods

  } // ForwardRateCalibrator

} 
