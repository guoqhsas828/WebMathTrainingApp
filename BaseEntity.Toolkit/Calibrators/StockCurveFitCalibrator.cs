using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using log4net;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  /// Calibrate stock curve
  /// </summary>
  [Serializable]
  public class StockCurveFitCalibrator : ForwardPriceCalibrator
  {
    private static readonly ILog Logger = LogManager.GetLogger(typeof(StockCurveFitCalibrator));

    #region Constructors

    /// <summary>
    ///   Calibrate an inflation curve by iterative bootstrapping
    /// </summary>
    /// <param name = "asOf">As of date</param>
    /// <param name = "settle">Settle of underlying contracts</param>
    /// <param name = "discountCurve">Curve used for discounting</param>
    /// <param name = "fitSettings">Curve fit settings</param>
    public StockCurveFitCalibrator(Dt asOf, Dt settle, DiscountCurve discountCurve, CalibratorSettings fitSettings)
      : base(asOf, settle, discountCurve)
    {
      CashflowCalibratorSettings = new CashflowCalibrator.CashflowCalibratorSettings();
      CurveFitSettings = fitSettings ?? new CalibratorSettings {CurveAsOf = asOf};
      if (CurveFitSettings.CurveAsOf.IsEmpty())
        CurveFitSettings.CurveAsOf = asOf;
      SetParentCurves(ParentCurves, DiscountCurve);
    }

    #endregion

    #region Calibration

    /// <summary>
    /// </summary>
    /// <param name="curve"></param>
    /// <param name="product"></param>
    /// <returns></returns>
    protected override IPricer GetSpecializedPricer(ForwardPriceCurve curve, IProduct product)
    {
      var fwd = product as StockForward;
      if (fwd != null)
        return new StockForwardPricer(fwd, AsOf, Settle, 1.0, DiscountCurve, (StockCurve)curve);
      var fut = product as StockFuture;
      if (fut != null)
        return new StockFuturePricer(fut, AsOf, Settle, (StockCurve)curve, 1.0);
      throw new ArgumentException("Unsupported product type");
    }

    /// <summary>
    /// </summary>
    /// <param name="tenor"></param>
    /// <param name="calibrator"></param>
    /// <param name="curve"></param>
    protected override void AddData(CurveTenor tenor, CashflowCalibrator calibrator, CalibratedCurve curve)
    {
      var fwd = tenor.Product as StockForward;
      if (fwd != null)
      {
        var pricer = (StockForwardPricer)GetPricer(curve, fwd);
        calibrator.Add(tenor.MarketPv, pricer.GetPaymentSchedule(null, pricer.AsOf), pricer.Settle, pricer.DiscountCurve, tenor.CurveDate, tenor.Weight, true);
        return;
      }
      var fut = tenor.Product as StockFuture;
      {
        if (fut != null)
        {
          var pricer = (StockFuturePricer)GetPricer(curve, fut);
          calibrator.Add(tenor.MarketPv, pricer.GetPaymentSchedule(null, pricer.AsOf), pricer.Settle, pricer.DiscountCurve, tenor.CurveDate, tenor.Weight, true);
        }
      }
    }

    /// <summary>
    /// </summary>
    /// <param name="curve"></param>
    protected override void SetCurveDates(CalibratedCurve curve)
    {
      foreach (CurveTenor ten in curve.Tenors)
        ten.CurveDate = ten.Product.Maturity;
    }

    #endregion
  }
}
