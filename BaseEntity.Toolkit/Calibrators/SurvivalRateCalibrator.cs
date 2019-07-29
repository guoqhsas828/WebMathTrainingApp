/*
 * SurvivalRateCalibrator.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using log4net;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Calibrators
{
  ///<summary>
  ///  Survival curve calibration using risky zero coupon rates
  ///</summary>
  ///<remarks>
  ///  <para>Each risky zero coupon bond is assumed to pay face at maturity
  ///  if no default occurs or a percentage of face (recovery) if default
  ///  occurs before maturity.</para>
  ///  <para>The Zero coupon bond yields are expressed in terms of simple yields</para>
  ///</remarks>
  [Serializable]
  public class SurvivalRateCalibrator : SurvivalCalibrator
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (SurvivalRateCalibrator));

    #region Constructors

    ///<summary>
    ///  Constructor given as-of (pricing) date
    ///</summary>
    ///<remarks>
    ///  <para>Settlement date defaults to as-of date.</para>
    ///</remarks>
    ///<param name = "asOf">As-of (pricing) date</param>
    protected SurvivalRateCalibrator(Dt asOf) : base(asOf)
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
    public SurvivalRateCalibrator(Dt asOf, Dt settle) : base(asOf, settle)
    {
    }

    ///<summary>
    ///  Constructor given recovery and discount curves
    ///</summary>
    ///<param name = "asOf">As-of date</param>
    ///<param name = "settle">Settlement date</param>
    ///<param name = "recoveryCurve">Recovery curve</param>
    ///<param name = "discountCurve">Discount Curve</param>
    public SurvivalRateCalibrator(Dt asOf, Dt settle, RecoveryCurve recoveryCurve, DiscountCurve discountCurve)
      : base(asOf, settle, recoveryCurve, discountCurve)
    {
    }

    #endregion // Constructors

    #region Methods

    ///<summary>
    ///  Fit from a specified point assuming all initial test/etc. have been performed.
    ///</summary>
    ///<param name = "curve">Survival curve to calibrate</param>
    ///<param name = "fromIdx">Index to start fit from</param>
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      var survivalCurve = new OverlayWrapper(curve);

      // Always start from scratch as this is quick.
      survivalCurve.Clear();

      // Construct vectors for model
      int periods = survivalCurve.Tenors.Count;

      if (FitSpotRates)
      {
        FitWasForced = false;
        for (int i = 0; i < periods; i++)
        {
          var tenor = survivalCurve.Tenors[i];
          var note = tenor.Product as Note;
          if (note == null)
          {
            throw new ToolkitException(String.Format(
              "Invalid product for SurvivalRateCalibrator. Must be note"));
          }

          var recoveryRate = RecoveryCurve.RecoveryRate(tenor.Maturity);
          var df = (DiscountCurve != null)
            ? DiscountCurve.DiscountFactor(tenor.Maturity) : 1.0;
          var rdf = RateCalc.PriceFromRate(note.Coupon, AsOf, note.Maturity, note.DayCount,
            note.Freq);
          var sp = (rdf/df - recoveryRate)/(1 - recoveryRate);
          survivalCurve.Add(tenor.CurveDate, sp);

          // When sp < 0 and survivalCurve.DayCount != None, the point value is NaN.
          if (Double.IsNaN(survivalCurve.GetVal(i)))
          {
            if (ForceFit && i > 0)
            {
              survivalCurve.Shrink(i);
              survivalCurve.Add(tenor.CurveDate,
                survivalCurve.Interpolate(tenor.CurveDate));
              FitWasForced = true;
            }
            else
            {
              logger.Debug(String.Format("Unable to fit tenor {0} survival={1}",
                tenor.CurveDate, sp));
            }
          }

          logger.Debug(String.Format("Tenor {0} survival={1}", tenor.CurveDate, sp));
        }
      }
      else
      {
        var rdf = new double[periods];
        var df = new double[periods];
        var recovery = new double[periods];
        var survival = new double[periods];

        for (int i = 0; i < periods; i++)
        {
          CurveTenor tenor = survivalCurve.Tenors[i];

          if (!(tenor.Product is Note))
            throw new ToolkitException(String.Format("Invalid product for SurvivalRateCalibrator. Must be note"));

          var note = (Note) tenor.Product;
          df[i] = (DiscountCurve != null) ? DiscountCurve.DiscountFactor(tenor.Maturity) : 1.0;
          rdf[i] = RateCalc.PriceFromRate(note.Coupon, AsOf, note.Maturity, note.DayCount, note.Freq);
          recovery[i] = RecoveryCurve.RecoveryRate(tenor.Maturity);
        }

        // Do conversion
        NegSPFound = Bootstrap.RdfToSurvival(rdf, df, recovery, NegSPTreatment, survival);

        // Save survival curve
        for (int i = 0; i < periods; i++)
        {
          CurveTenor tenor = survivalCurve.Tenors[i];
          survivalCurve.Add(tenor.Maturity, survival[i]);
          logger.Debug(String.Format("Tenor {0} survival={1}", tenor.Maturity, survival[i]));
        }
      }
    }

    /// <summary>
    /// Construct a pricer matching the model(s) used for calibration.
    /// </summary>
    /// <param name = "curve">Curve to calibrate</param>
    /// <param name = "product">Product to price</param>
    /// <returns>Constructed pricer for product</returns>
    public override IPricer GetPricer(CalibratedCurve curve, IProduct product)
    {
      var survivalCurve = (SurvivalCurve)curve;

      var note = product as Note;
      if (note != null)
      {
        return new DefaultableNotePricer(note, AsOf, Settle, 1.0,
          DiscountCurve, survivalCurve,
          RecoveryCurve == null ? 0.0
            : RecoveryCurve.RecoveryRate(product.Maturity));
      }

      var pricer = CashflowPricerFactory.PricerForProduct(product);
      pricer.AsOf = AsOf;
      pricer.Settle = Settle;
      pricer.DiscountCurve = DiscountCurve;
      pricer.ReferenceCurve = ReferenceCurve;
      pricer.SurvivalCurve = survivalCurve;
      pricer.RecoveryCurve = RecoveryCurve;
      return pricer;
    }

    internal bool FitSpotRates { get; set; }
    #endregion // Methods
  }

}
