using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util.Collections;


namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  /// Theta Scenario shift changing as-of and settle days of curves and pricers
  /// </summary>
  /// <remarks>
  /// <para>Shift the as-of and settle dates of the curves and calibrators
  ///  to target as-of and target settle days</para>
  /// <para>Rebuild the curve tenors and refit the curves with the target days based on the theta flag</para>
  /// <para>Also shift the as-of and settles dates of the pricers to the target as-of days and target settles</para>
  /// </remarks>
  public class ScenarioShiftTime : IScenarioShift
  {

    #region Constructors

    /// <summary>
    /// Create a Theta Scenario, we should provide targetAsOf, targetSettle and Thetaflag
    /// </summary>
    /// <param name="targetAsOf">target as-of</param>
    /// <param name="targetSettle">target settle</param>
    /// <param name="flag">None, RefitRates,Recalibrate, IncludeDefaultPayment. Not support Clean flag here.
    /// if want to calculate clean Pv, set the measure of qScenarioCalculate as CleanPv.</param>
    public ScenarioShiftTime(Dt targetAsOf, Dt targetSettle, ThetaFlags flag)
    {
      if (flag.HasFlag(ThetaFlags.Clean))
        throw new ArgumentException(string.Format(
          "Not support Clean flag here. Set the measure of qScenarioCalculate as CleanPv to calculate clean pv"));

      TargetAsOf = targetAsOf;
      TargetSettle = targetSettle;
      ThetaFlag = flag;
    }

    #endregion Constructors


    #region Properties

    /// <summary>
    /// target as-of
    /// </summary>
    public Dt TargetAsOf { get; set; }

    /// <summary>
    /// target settle
    /// </summary>
    public Dt TargetSettle { get; set; }

    /// <summary>
    /// Theta flag
    /// </summary>
    public ThetaFlags ThetaFlag { get; set; }

    /// <summary>
    /// all curves got from pricers
    /// </summary>
    private CalibratedCurve[] AllCurves { get; set; }

    /// <summary>
    /// saved all curves
    /// </summary>
    private CalibratedCurve[] SavedAllCurves { get; set; }

    /// <summary>
    /// Saved as-of days of all curves
    /// </summary>
    private Dt[] SavedCurveAsOf { get; set; }

    /// <summary>
    /// Saved settle days of all curves
    /// </summary>
    private Dt[] SavedCurveSettle { get; set; }

    /// <summary>
    /// Saved pricer as-of days
    /// </summary>
    private Dt[] SavedPricerAsOf { get; set; }

    /// <summary>
    /// Saved pricer settle days
    /// </summary>
    private Dt[] SavedPricerSettle { get; set; }

    /// <summary>
    /// Saved Pricer flags, such as NoDefaults, SensitivityToAllRateTenors.
    /// </summary>
    private PricerFlags[] SavedPricerFlags { get; set; }

    #endregion Properties

    /// <summary>
    /// Validate
    /// </summary>
    public void Validate()
    {
      if (!TargetAsOf.IsEmpty()) TargetAsOf.Validate();
      if (!TargetSettle.IsEmpty()) TargetSettle.Validate();
      if (Dt.Cmp(TargetSettle, TargetAsOf) < 0)
        throw new ArgumentException(String.Format(
          "Target settle date {0} should be on or after target as-of date {1}", 
          TargetSettle, TargetAsOf));
    }

    /// <summary>
    /// Save states
    /// </summary>
    /// <param name="evaluators"></param>
    public void SaveState(PricerEvaluator[] evaluators)
    {
      if (evaluators == null || evaluators.Length == 0)
      {
        SavedAllCurves = null;
        SavedCurveAsOf = null;
        SavedCurveSettle = null;
        SavedPricerAsOf = null;
        SavedPricerSettle = null;
        SavedPricerFlags = null;
        return;
      }

      AllCurves = evaluators.GetPrerequisiteCurves().ToArray();
      SavedAllCurves = IsNullOrEmpty(AllCurves)
        ? null
        : CurveUtil.CurveCloneWithRecovery(AllCurves);

      SavedCurveAsOf = IsNullOrEmpty(AllCurves)
        ? null
        : AllCurves.Select(curve => curve.Calibrator.AsOf).ToArray();

      SavedCurveSettle = IsNullOrEmpty(AllCurves)
        ? null
        : AllCurves.Select(curve => curve.Calibrator.Settle).ToArray();

      SavedPricerAsOf = new Dt[evaluators.Length];
      SavedPricerSettle = new Dt[evaluators.Length];
      SavedPricerFlags = new PricerFlags[evaluators.Length];
      for (int i = 0, n = evaluators.Length; i < n; ++i)
      {
        var pricer = evaluators[i].Pricer;
        if (pricer == null) continue;
        SavedPricerAsOf[i] = pricer.AsOf;
        SavedPricerSettle[i] = pricer.Settle;
        SavedPricerFlags[i] = ((PricerBase) pricer).PricerFlags;
        evaluators[i] = evaluators[i].Substitute(
          pricer.LockFloatingCoupons(TargetAsOf));
      }
    }

    /// <summary>
    /// Perform shift. 
    /// </summary>
    /// <param name="evaluators">Evaluators(pricers) to shift</param>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      if (evaluators == null) return;
      foreach (var curve in AllCurves)
      {
        if (curve == null) continue;
        if (curve is DiscountCurve && ThetaFlag.HasFlag(ThetaFlags.RefitRates))
        {
          ThetaShiftUtil.ShiftDatesAndRefit((DiscountCurve) curve, TargetAsOf);
          continue;
        }

        if (curve is SurvivalCurve && ThetaFlag.HasFlag(ThetaFlags.Recalibrate)
            && ThetaFlag.HasFlag(ThetaFlags.RefitRates))
        {
          ThetaShiftUtil.ShiftDatesAndRefit((SurvivalCurve) curve, TargetAsOf);
          continue;
        }

        if (curve is SurvivalCurve)
          Sensitivities.SetSurvivalCurveAsOf(new List<SurvivalCurve> {(SurvivalCurve) curve},
            true, TargetAsOf, TargetSettle, ThetaFlag.HasFlag(ThetaFlags.Recalibrate));
        else
          curve.SetCurveAsOfDate(TargetAsOf);
      }

      for (int i = 0; i < evaluators.Length; ++i)
      {
        var pricer = evaluators[i].Pricer;
        pricer.AsOf = TargetAsOf;
        pricer.Settle = TargetSettle;
        if (ThetaFlag.HasFlag(ThetaFlags.IncludeDefaultPayment))
          ((PricerBase) pricer).PricerFlags = PricerFlags.NoDefaults;
        pricer.Reset();
      }
    }

    /// <summary>
    /// perform refit
    /// </summary>
    /// <param name="evaluators"></param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    {
    }

    /// <summary>
    /// restore states and clean the saved states
    /// </summary>
    /// <param name="evaluators"></param>
    public void RestoreState(PricerEvaluator[] evaluators)
    {
      for (var i = 0; i < AllCurves.Length; ++i)
      {
        if (AllCurves[i] == null) continue;
        if (AllCurves[i] is DiscountCurve && ThetaFlag.HasFlag(ThetaFlags.RefitRates))
        {
          ThetaShiftUtil.ShiftDatesAndRefit((DiscountCurve) AllCurves[i], SavedCurveAsOf[i]);
          continue;
        }

        if (AllCurves[i] is SurvivalCurve && ThetaFlag.HasFlag(ThetaFlags.Recalibrate) &&
            ThetaFlag.HasFlag(ThetaFlags.RefitRates))
        {
          ThetaShiftUtil.ShiftDatesAndRefit((SurvivalCurve) AllCurves[i], SavedCurveAsOf[i]);
          continue;
        }

        if (AllCurves[i] is SurvivalCurve)
        {
          Sensitivities.SetSurvivalCurveAsOf(new List<SurvivalCurve> {(SurvivalCurve) AllCurves[i]},
            true, SavedCurveAsOf[i], SavedCurveSettle[i], ThetaFlag.HasFlag(ThetaFlags.Recalibrate));
          CurveUtil.CurveRestoreWithRecovery(new[]{AllCurves[i]}, new[]{SavedAllCurves[i]});
        }
        else
          AllCurves[i].SetCurveAsOfDate(SavedCurveAsOf[i]);
      }
      SavedCurveAsOf = null;
      SavedCurveSettle = null;
      SavedAllCurves = null;

      for (int i = 0; i < evaluators.Length; ++i)
      {
        var pricer = evaluators[i].Pricer;
        pricer.AsOf = SavedPricerAsOf[i];
        pricer.Settle = SavedPricerSettle[i];
        ((PricerBase) pricer).PricerFlags = SavedPricerFlags[i];
        pricer.Reset();
      }
      SavedPricerAsOf = null;
      SavedPricerSettle = null;
      SavedPricerFlags = null;
    }

    private bool IsNullOrEmpty(CalibratedCurve[] curves)
    {
      return (curves == null || curves.Length == 0);
    }
  } //class ScenarioShiftTime
}
