/*
 * DiscountCalibrator.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;
using Process = BaseEntity.Toolkit.Models.RateModelParameters.Process;
using Parameter = BaseEntity.Toolkit.Models.RateModelParameters.Param;

namespace BaseEntity.Toolkit.Calibrators
{
  #region ForwardPriceCalibrator
  /// <summary>
  ///   Abstract base class for all AssetForward calibrators.
  /// </summary>
  [Serializable]
  public abstract class ForwardPriceCalibrator : Calibrator, IHasCashflowCalibrator
  {
    #region Constructors
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="asOf">As of date</param>
    /// <param name="settle">Settle date</param>
    /// <param name="discountCurve">discount curve</param>
    protected ForwardPriceCalibrator(Dt asOf, Dt settle, DiscountCurve discountCurve)
      : base(asOf, settle)
    {
      DiscountCurve = discountCurve;
      CashflowCalibratorSettings = new CashflowCalibrator.CashflowCalibratorSettings();
    }
    #endregion Constructors

    #region Properties
    
    /// <summary>
    /// Gets the fitting error code.
    /// </summary>
    /// <value>The fitting error code.</value>
    public CashflowCalibrator.OptimizerStatus FittingErrorCode { get; protected set; }

    /// <summary>
    /// Settings for cashflow calibrator
    /// </summary>
    public CashflowCalibrator.CashflowCalibratorSettings CashflowCalibratorSettings { get; protected set; }

    /// <summary>
    /// Calibration settings
    /// </summary>
    public CalibratorSettings CurveFitSettings { get; protected set; }

    /// <summary>
    ///   Discount curve 
    /// </summary>
    public DiscountCurve DiscountCurve { get; private set; }
    #endregion Properties

    #region Methods
    /// <summary>
    /// Add a calibration product
    /// </summary>
    /// <param name="tenor"></param>
    /// <param name="calibrator">calibrator</param>
    /// <param name="targetCurve">targetCurve</param>
    protected abstract void AddData(CurveTenor tenor, CashflowCalibrator calibrator, CalibratedCurve targetCurve);
    /// <summary>
    /// Set curve dates on tenors
    /// </summary>
    /// <param name="targetCurve"></param>
    protected abstract void SetCurveDates(CalibratedCurve targetCurve);

    /// <summary>
    /// Get the pricer for calibration
    /// </summary>
    /// <param name="curve">Curve</param>
    /// <param name="product">Product</param>
    /// <returns>Pricer</returns>
    protected abstract IPricer GetSpecializedPricer(ForwardPriceCurve curve, IProduct product);
    /// <summary>
    ///   General purpose bootstrapping algorithm to calibrate curve whose interpolation
    ///   method is local or global.
    /// </summary>
    /// <param name = "curve">Curve to be calibrated</param>
    /// <param name = "fromIdx">Fit from index</param>
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      var afc = curve as ForwardPriceCurve;
      if (afc == null)
        throw new ArgumentException("Curve of type AssetForwardCurve expected");
      var curveFitSettings = CurveFitSettings ?? new CalibratorSettings {CurveAsOf = curve.AsOf};
      var cashflowCalibratorSettings = CashflowCalibratorSettings ?? new CashflowCalibrator.CashflowCalibratorSettings();
      SetCurveDates(curve);
      curve.Tenors.Sort();
      var calibrator = FillData(afc);
      if (calibrator.Count == 0)
        return;
      IModelParameter vol = null;
      if (curveFitSettings.Method == CurveFitMethod.SmoothFutures && curveFitSettings.FwdModelParameters != null)
        curveFitSettings.FwdModelParameters.TryGetValue(Process.Projection, Parameter.Custom, out vol);
      if (curveFitSettings.MaximumIterations >= 0)
      {
        cashflowCalibratorSettings.MaximumOptimizerIterations =
          cashflowCalibratorSettings.MaximumSolverIterations = curveFitSettings.MaximumIterations;
      }
      double[] priceErrors;
      FittingErrorCode = calibrator.Calibrate(curveFitSettings.Method, afc.TargetCurve,
                                              curveFitSettings.SlopeWeightCurve,
                                              curveFitSettings.CurvatureWeightCurve, vol, out priceErrors,
                                              cashflowCalibratorSettings);
      if (String.IsNullOrEmpty(afc.Name) && afc.Spot != null)
        afc.Name = String.Format("{0}.{1}", afc.GetType(), afc.Spot.Name);
      afc.Ccy = DiscountCurve.Ccy;
      SetDependentCurves(curve, DiscountCurve);
    }

    /// <summary>
    ///   Create a pricer for calibration
    /// </summary>
    /// <param name = "curve">Calibrated curve</param>
    /// <param name = "product">product</param>
    /// <returns>Pricer</returns>
    public override IPricer GetPricer(CalibratedCurve curve, IProduct product)
    {
      var p = product as SpotAsset;
      if (p != null)
        return GetAssetSpotPricer(curve as ForwardPriceCurve, p);
      return GetSpecializedPricer(curve as ForwardPriceCurve, product);
    }

    private CashflowCalibrator FillData(ForwardPriceCurve curve)
    {
      curve.Clear();
      var calibrator = new CashflowCalibrator(curve.AsOf);
      foreach (CurveTenor tenor in curve.Tenors)
      {
        if (tenor.Product is SpotAsset)
        {
          curve.Spot.Value = tenor.MarketPv;
          curve.Add(tenor.Maturity, tenor.MarketPv);

          // Enable this temporarily before we resolve the related
          // risk regression differences.
          if (curve is CommodityCurve)
          {
            curve.SpotPriceCurve?.Fit();
          }
        }
        else
          AddData(tenor, calibrator, curve);
      }
      return calibrator;
    }

    /// <summary>
    /// Validate
    /// </summary>
    /// <param name="errors"></param>
    public override void Validate(System.Collections.ArrayList errors)
    {
      if(DiscountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", "Non null DiscountCurve required");
      base.Validate(errors);
    }

    /// <summary>
    /// Get AssetSpotPricer for first point in the curve
    /// </summary>
    /// <param name="curve">calibrated curve</param>
    /// <param name="product">product</param>
    /// <returns>AssetSpotPricer</returns>
    private IPricer GetAssetSpotPricer(ForwardPriceCurve curve, SpotAsset product)
    {
      var retVal = new SpotAssetPricer(product, Settle, 1.0, curve);
      retVal.Validate();
      return retVal;
    }

    /// <summary>
    /// Parent curves list
    /// </summary>
    /// <returns>Parent curves</returns>
    public override IEnumerable<CalibratedCurve> EnumerateParentCurves()
    {
      yield return DiscountCurve;
    }
    #endregion
  }
  #endregion
}
