//
// DiscountCalibrator.cs
//  -2014. All rights reserved.
//
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  ///   Abstract base class for all DiscountCurve calibrators.
  /// </summary>
  [Serializable]
  public abstract class DiscountCalibrator : Calibrator
  {
    #region Constructors

    /// <summary>
    ///   Constructor given as-of (pricing) date
    /// </summary>
    /// <remarks>
    ///   <para>Settlement date defaults to as-of date.</para>
    /// </remarks>
    /// <param name = "asOf">As-of (pricing) date</param>
    protected DiscountCalibrator(Dt asOf) : base(asOf)
    {
      SurvivalCurve = null;
      RecoveryCurve = null;
      VolatilityCurve = null;
    }

    /// <summary>
    ///   Constructor given as-of and settlement dates
    /// </summary>
    /// <remarks>
    ///   <para>Settlement date defaults to as-of date.</para>
    /// </remarks>
    /// <param name = "asOf">As-of (pricing) date</param>
    /// <param name = "settle">Settlement date</param>
    protected DiscountCalibrator(Dt asOf, Dt settle) : base(asOf, settle)
    {
      SurvivalCurve = null;
      RecoveryCurve = null;
      VolatilityCurve = null;
    }

    /// <summary>
    ///   Constructor given recovery and discount curves
    /// </summary>
    /// <param name = "asOf">As-of date</param>
    /// <param name = "settle">Settlement date</param>
    /// <param name = "survivalCurve">Survival Curve</param>
    /// <param name = "recoveryCurve">Recovery curve</param>
    protected DiscountCalibrator(Dt asOf, Dt settle, SurvivalCurve survivalCurve, RecoveryCurve recoveryCurve)
      : base(asOf, settle)
    {
      SurvivalCurve = survivalCurve;
      RecoveryCurve = recoveryCurve;
      VolatilityCurve = null;
    }

    /// <summary>
    ///   Clone object
    /// </summary>
    public override object Clone()
    {
      var obj = (DiscountCalibrator) base.Clone();

      obj.SurvivalCurve = (SurvivalCurve != null) ? (SurvivalCurve) SurvivalCurve.Clone() : null;
      obj.RecoveryCurve = (RecoveryCurve != null) ? (RecoveryCurve) RecoveryCurve.Clone() : null;
      obj.VolatilityCurve = (VolatilityCurve != null) ? (VolatilityCurve) VolatilityCurve.Clone() : null;
      return obj;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Survival curve used for pricing
    /// </summary>
    public SurvivalCurve SurvivalCurve { get; set; }

    /// <summary>
    ///   Recovery Curve
    /// </summary>
    public RecoveryCurve RecoveryCurve { get; set; }

    /// <summary>
    ///   Volatility Curve
    /// </summary>
    public VolatilityCurve VolatilityCurve { get; set; }

    /// <summary>
    ///   Property Curve Fit Settings
    /// </summary>
    public CalibratorSettings CurveFitSettings { get; protected set; }

    /// <summary>
    ///   Model used for computation of the ED future convexity adjustment
    /// </summary>
    public RateModelParameters ModelParameters
    {
      get ;set;
    }

    #endregion Properties
  }
}