/*
 * DiscountRateCalibrator.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using log4net;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators
{
  ///<summary>
  ///  Interest rate curve calibration using zero coupon rates
  ///</summary>
  ///<remarks>
  ///  <para>Each zero coupon bond is assumed to pay face at maturity.</para>
  ///
  ///  <para>The Zero coupon bond yields are expressed in terms of simple yields</para>
  ///</remarks>
  [Serializable]
  public class DiscountRateCalibrator : DiscountCalibrator
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (DiscountRateCalibrator));

    #region Constructors

    ///<summary>
    ///  Constructor given as-of (pricing) date
    ///</summary>
    ///<remarks>
    ///  <para>Settlement date defaults to as-of date.</para>
    ///</remarks>
    ///<param name = "asOf">As-of (pricing) date</param>
    protected DiscountRateCalibrator(Dt asOf) : base(asOf)
    {
    }

    ///<summary>
    ///  Constructor given as-of and settlement dates
    ///</summary>
    ///<remarks>
    ///  <para>Settlement date defaults to as-of date.</para>
    ///</remarks>
    ///<param name = "asOf">As-of (pricing) date</param>
    ///<param name = "settle">Settlement date</param>
    public DiscountRateCalibrator(Dt asOf, Dt settle) : base(asOf, settle)
    {
    }

    #endregion // Constructors

    #region Methods

    ///<summary>
    ///  Fit from a specified point assuming all initial test/etc. have been performed.
    ///</summary>
    ///<param name = "curve">Discount curve to calibrate</param>
    ///<param name = "fromIdx">Index to start fit from</param>
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      var discountCurve = new OverlayWrapper(curve);

      // Start from scratch each time as this is fast
      discountCurve.Clear();

      // Do fit
      foreach (CurveTenor tenor in discountCurve.Tenors)
      {
        if (!(tenor.Product is Note))
          throw new ToolkitException(String.Format("Invalid product for DiscountRateCalibrator. Must be note"));

        var note = (Note) tenor.Product;

        double df = RateCalc.PriceFromRate(note.Coupon,
          AsOf, note.Maturity, note.DayCount, note.Freq);
        logger.DebugFormat("Tenor {0} price={1}", note.Maturity, df);

        discountCurve.Add(tenor.Maturity, Inverse ? (1/df) : df);
      } // foreach

      return;
    }


    /// <summary>
    /// Construct a pricer matching the model(s) used for calibration.
    /// </summary>
    /// <param name="curve">Curve to calibrate</param>
    /// <param name="product">Product to price</param>
    /// <returns>Constructed pricer for product</returns>
    public override IPricer GetPricer(CalibratedCurve curve, IProduct product)
    {
      var note = product as Note;
      if (note != null)
      {
        return new NotePricer(note, AsOf, AsOf, 1.0, (DiscountCurve)curve);
      }

      if (product != null)
      {
        throw new NotImplementedException(string.Format(
          "DiscountRateCalibrator: unable to get pricer for {0}",
          product.GetType().Name));
      }
      return null;
    }

    #endregion // Methods

    #region Properties
    public bool Inverse { get; set; }
    #endregion
  }
}