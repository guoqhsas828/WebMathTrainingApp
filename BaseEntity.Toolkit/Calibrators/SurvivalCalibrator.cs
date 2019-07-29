/*
 * SurvivalCalibrator.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using log4net;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  /// Abstract base class for all SurvivalCurve calibrators.
  /// </summary>
  [Serializable]
  public abstract class SurvivalCalibrator : Calibrator
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (SurvivalCalibrator));

    #region Config

    private readonly bool useCdsSpecificSolver_; // hard coded, retire later

    /// <summary>
    ///   Use T+1 for settle date
    /// </summary>
    /// <exclude />
    public bool UseNaturalSettlement
    {
      get { return Settings.SurvivalCalibrator.UseNaturalSettlement; }
    }

    /// <summary>
    ///   Use 8.6.1 solver
    /// </summary>
    /// <exclude />
    public bool UseCdsSpecificSolver
    {
      get { return useCdsSpecificSolver_; }
    }

    /// <summary>
    ///   Tolerance of curve fitting for variables
    /// </summary>
    /// <exclude />
    public double ToleranceX
    {
      get
      {
        return Double.IsNaN(_toleranceX)
          ? Settings.SurvivalCalibrator.ToleranceX
          : _toleranceX;
      }
      set { _toleranceX = value; }
    }

    /// <summary>
    ///   Tolerance of curve fitting for function values
    /// </summary>
    /// <exclude />
    public double ToleranceF
    {
      get
      {
        return Double.IsNaN(_toleranceF)
          ? Settings.SurvivalCalibrator.ToleranceF
          : _toleranceF;
      }
      set { _toleranceF = value; }
    }

    #endregion Config

    #region Constructors

    /// <summary>
    ///   Constructor given as-of (pricing) date
    /// </summary>
    /// <remarks>
    ///   <para>Settlement date defaults to as-of date.</para>
    /// </remarks>
    /// <param name = "asOf">As-of (pricing) date</param>
    protected SurvivalCalibrator(Dt asOf) : base(asOf)
    {
      discountCurve_ = null;
      referenceCurve_ = null;
      recoveryCurve_ = null;
      negSPTreatment_ = NegSPTreatment.Allow;
      negSPFound_ = false;
    }

    /// <summary>
    ///   Constructor given as-of and settlement dates
    /// </summary>
    /// <remarks>
    ///   <para>Settlement date defaults to as-of date.</para>
    /// </remarks>
    /// <param name = "asOf">As-of (pricing) date</param>
    /// <param name = "settle">Settlement date</param>
    public SurvivalCalibrator(Dt asOf, Dt settle) : base(asOf, settle)
    {
      discountCurve_ = null;
      referenceCurve_ = null;
      recoveryCurve_ = null;
      negSPTreatment_ = NegSPTreatment.Allow;
      negSPFound_ = false;
    }

    /// <summary>
    ///   Constructor given recovery curve and discount curves
    /// </summary>
    /// <param name = "asOf">As-of date</param>
    /// <param name = "settle">Settlement date</param>
    /// <param name = "recoveryCurve">Recovery curve</param>
    /// <param name = "discountCurve">Discount Curve</param>
    public SurvivalCalibrator(Dt asOf, Dt settle, RecoveryCurve recoveryCurve, DiscountCurve discountCurve)
      : base(asOf, settle)
    {
      discountCurve_ = discountCurve;
      referenceCurve_ = null;
      recoveryCurve_ = recoveryCurve;
      negSPTreatment_ = NegSPTreatment.Allow;
      negSPFound_ = false;
    }

    /// <summary>
    ///   Constructor give recovery rate and discount curves
    /// </summary>
    /// <param name = "asOf">As-of date</param>
    /// <param name = "settle">Settlement date</param>
    /// <param name = "recoveryRate">Recovery rate</param>
    /// <param name = "discountCurve">Discount Curve</param>
    public SurvivalCalibrator(Dt asOf, Dt settle, double recoveryRate, DiscountCurve discountCurve) : base(asOf, settle)
    {
      discountCurve_ = discountCurve;
      referenceCurve_ = null;
      recoveryCurve_ = new RecoveryCurve(asOf, recoveryRate);
      negSPTreatment_ = NegSPTreatment.Allow;
      negSPFound_ = false;
    }

    /// <summary>
    ///   Clone object
    /// </summary>
    public override object Clone()
    {
      var obj = (SurvivalCalibrator) base.Clone();

      obj.discountCurve_ = CloneUtil.Clone(discountCurve_);
      obj.referenceCurve_ = CloneUtil.Clone(referenceCurve_);
      obj.recoveryCurve_ = CloneUtil.Clone(recoveryCurve_);
      obj.counterpartyCurve_ = CloneUtil.Clone(counterpartyCurve_);

      return obj;
    }

    #endregion Constructors

    #region Properties

    /// <summary>
    ///   Recovery Curve
    /// </summary>
    public RecoveryCurve RecoveryCurve
    {
      get { return recoveryCurve_; }
      set
      {
        if (value == null) throw new ArgumentException(String.Format("Invalid recovery curve. Cannot be null"));
        recoveryCurve_ = value;
      }
    }

    /// <summary>
    ///   Recovery rate type
    /// </summary>
    public RecoveryType RecoveryType
    {
      get { return recoveryCurve_.RecoveryType; }
      set { recoveryCurve_.RecoveryType = value; }
    }

    /// <summary>
    ///   Recovery dispersion
    /// </summary>
    public double RecoveryDispersion
    {
      get { return recoveryCurve_.RecoveryDispersion; }
      set { recoveryCurve_.RecoveryDispersion = value; }
    }

    /// <summary>
    ///   Discount Curve used for pricing
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return discountCurve_; }
      set
      {
        if (value == null) throw new ArgumentException(String.Format("Invalid discount curve. Cannot be null"));
        discountCurve_ = value;
      }
    }

    /// <summary>
    ///   Reference Curve used for pricing of floating-rate cashflows
    /// </summary>
    public DiscountCurve ReferenceCurve
    {
      get { return referenceCurve_; }
      set { referenceCurve_ = value; }
    }

    /// <summary>
    ///   Counterparty Curve (refinancing curve)
    /// </summary>
    public SurvivalCurve CounterpartyCurve
    {
      get { return counterpartyCurve_; }
      set { counterpartyCurve_ = value; }
    }

    /// <summary>
    ///   Correlation between counterparty curve and survival curve
    /// </summary>
    public double CounterpartyCorrelation
    {
      get { return corr_; }
      set { corr_ = value; }
    }

    /// <summary>
    ///   Treatment of negative survival probabilities
    /// </summary>
    public NegSPTreatment NegSPTreatment
    {
      get { return negSPTreatment_; }
      set { negSPTreatment_ = value; }
    }

    /// <summary>
    ///   True if negative survival probabilities found during the fit
    /// </summary>
    /// <remarks>
    ///   Depending on how NegSPTreatment is set, the fit may complete 
    ///   even if negative forward probabilites are found and this flag
    ///   is used to indicate this situation to the caller.
    /// </remarks>
    public bool NegSPFound
    {
      get { return negSPFound_; }
      set { negSPFound_ = value; }
    }

    /// <summary>
    /// Allow manipulation of tenor quotes to force curve to fit
    /// </summary>
    /// <remarks>
    ///   This value defaults to false. This flag will most commonly be used in scenario
    ///   analysis, when modified quotes are more likely to result in curves that fail to fit.
    /// </remarks>
    [Category("Base")]
    public bool ForceFit
    {
      set { forceFit_ = value; }
      get { return forceFit_; }
    }

    /// <summary>
    ///   True if the fit was forced by adjusting tenor quotes
    /// </summary>
    /// <remarks>
    ///   Depending on how ForceFit is set, the fit may complete 
    ///   even if some tenors are out of line with the others.
    ///   This is currently pertinent to only the SurvivalFitCalibrator.
    /// </remarks>
    public bool FitWasForced
    {
      get { return fitWasForced_; }
      set { fitWasForced_ = value; }
    }

    #endregion // Properties

    #region Methods

    /// <summary>
    /// </summary>
    /// <returns></returns>
    public override IEnumerable<CalibratedCurve> EnumerateParentCurves()
    {
      if (discountCurve_ != null)
        yield return discountCurve_;
      if (referenceCurve_ != null)
        yield return referenceCurve_;
      if (counterpartyCurve_ != null)
        yield return counterpartyCurve_;
    }

    /// <summary>
    ///   Convert to string
    /// </summary>
    public override string ToString()
    {
      return base.ToString() + "; " + "NegSPTreatment = " + NegSPTreatment + "; " + "NegSPFound = " + NegSPFound + "; " +
             "DiscountCurve = " + DiscountCurve + "; " + "ReferenceCurve = " + ReferenceCurve + "; " +
             "CounterpartyCurve = " + CounterpartyCurve + "; " + "Corr = " + CounterpartyCorrelation + "; " +
             "RecoveryCurve = " + RecoveryCurve;
    }

    #endregion Methods

    #region Data

    private double corr_;
    private SurvivalCurve counterpartyCurve_;
    private DiscountCurve discountCurve_;
    private bool negSPFound_;
    private NegSPTreatment negSPTreatment_;
    private RecoveryCurve recoveryCurve_;
    private DiscountCurve referenceCurve_;
    private double _toleranceX = Double.NaN,
      _toleranceF = double.NaN;
    private bool fitWasForced_;
    private bool forceFit_; // Flag for manipulating quotes to force a fit

    #endregion Data
  }


  /// <exclude />
  [Serializable]
  public class SurvivalCalibratorConfig
  {
    /// <exclude />
    [ToolkitConfig("Error tolerance in the Value value.")] public readonly double ToleranceF = 1E-6;

    /// <exclude />
    [ToolkitConfig("Error tolerance in survival probability.")] public readonly double ToleranceX = 1E-6;

    /// <exclude />
    [ToolkitConfig("Whether to use T+1 as settlement date.")] public readonly bool UseNaturalSettlement = true;

    // <exclude />
    //[Util.Configuration.ToolkitConfig("Pricing step size (0 means to use the payment periods).")]
    //public readonly uint StepSize = 0;

    // <exclude />
    //[Util.Configuration.ToolkitConfig("Pricing step unit (None means to use the payment periods).")]
    //public readonly TimeUnit StepUnit =  TimeUnit.None;
  }
}