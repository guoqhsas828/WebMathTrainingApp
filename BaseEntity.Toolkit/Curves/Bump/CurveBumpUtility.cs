using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;

using log4net;

namespace BaseEntity.Toolkit.Curves.Bump
{
  public static class CurveBumpUtility
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(CurveBumpUtility));

    #region Generic Curve Bumps
    public static double BumpTenors(
      this DependencyGraph<CalibratedCurve> curves,
      ICurveTenorSelection selection, double bumpSize, BumpFlags bumpFlags,
      List<CalibratedCurve> affectedCurves)
    {
      if (curves == null || !curves.Any() || selection == null)
      {
        return 0;
      }
      IList<CurveTenor> tenors = selection.Tenors;
      var quotes = tenors.Select(t => t.CurrentQuote).ToList();
      try
      {
        var size = tenors.Select(t => t.QuoteHandler.BumpQuote(t, bumpSize, bumpFlags)).Average();
        var handler = selection.GetBumpHandler(bumpFlags, bumpSize);
        var isInPlaceBump = (bumpFlags & BumpFlags.BumpInPlace) != 0;
        affectedCurves.Clear();
        curves.ForEach(c => handler.ShiftSingleCurve(c, isInPlaceBump, affectedCurves));
        return size;
      }
      finally
      {
        for (int i = 0, n = tenors.Count; i < n; ++i)
          tenors[i].SetQuote(quotes[i].Type, quotes[i].Value);
      }
    }

    private static void ShiftSingleCurve(this ICurveBumpHandler handler,
      CalibratedCurve curve, bool inPlaceBump, List<CalibratedCurve> affectedCurves)
    {
      // Do we need to refit this curve?
      if (curve == null || curve.Calibrator == null
        || !curve.JumpDate.IsEmpty()
        || !handler.HasAffected(curve.GetCurveShifts()))
      {
        return;
      }
      //the affectedCurve should be the targetcurve
      var targeCurve = GetTartget(curve);
      affectedCurves.Add(targeCurve);

      // Find the shift overlay curve.
      var shift = inPlaceBump ? targeCurve : targeCurve.GetShiftOverlay();

      // Can we use the existing shifts for this curve?
      var vals = handler.GetShiftValues(targeCurve.GetCurveShifts());
      if (vals != null && vals.Length == shift.Count)
      {
        SetCurvePoints(shift, vals);
        return;
      }

      // Refit this curve, calculate the shifts and save them...
      if (inPlaceBump) shift.ClearCache();
      else if (curve is SurvivalCurve) shift.Initialize(1.0);
      else shift.Clear();
      curve.Calibrator.ReFit(curve, 0);
      if (shift.Count == 0) { SetNoShift(shift, targeCurve); }
      vals = GetCurvePoints(shift);
      handler.SetShiftValues(targeCurve.CurveShifts, vals);
    }

    /// <summary>
    /// Restores the bumped curves and all their parents to base states.
    /// </summary>
    /// <param name="curves">The curves.</param>
    /// <remarks></remarks>
    public static void RestoreBaseCurves(
      this IEnumerable<CalibratedCurve> curves)
    {
      foreach (var curve in curves)
      {
        RestoreBaseCurve(curve);
      }
    }

    public static void InitializeCurveBump(
      this IList<CalibratedCurve> curves, BumpFlags bumpFlags)
    {
      if ((bumpFlags & BumpFlags.BumpInPlace) == 0)
        return; // no need to save the original points for overlay bumps.

      // For in place bumps, we always save the original curve points.
      var zeroBump = CurveBumpScenario.ZeroBumpScenario;
      foreach (var curve in curves)
      {
        var targetCurve = GetTartget(curve);
        var shifts = targetCurve.GetCurveShifts();
        var vals = GetCurvePoints(targetCurve, true);
        shifts[zeroBump] = vals;
      }
    }
    #endregion

    #region Helpers

    private static CalibratedCurve GetTartget(CalibratedCurve curve)
    {
      return (curve as ICalibratedCurveContainer)?.TargetCurve ?? curve;
    } 

    private static CurveShifts GetCurveShifts(this CalibratedCurve curve)
    {
      if (curve.CurveShifts == null) SetUpCurveShifts(curve);
      return curve.CurveShifts;
    }

    private static void SetUpCurveShifts(this CalibratedCurve targetCurve)
    {
      var tenors = targetCurve.Tenors as IEnumerable<CurveTenor>
        ?? EmptyArray<CurveTenor>.Instance ;
      if (targetCurve.Calibrator != null)
      {
        tenors = targetCurve.EnumeratePrerequisiteCurves()
          .Aggregate(tenors, (current, curve) =>
            current.Concat(curve.GetCurveShifts().AllTenors));
      }
      targetCurve.CurveShifts = new CurveShifts(tenors);
    }

    private static Curve GetShiftOverlay(this CalibratedCurve curve)
    {
      if (curve.ShiftOverlay == null) SetUpShiftOverlay(curve);
      return curve.ShiftOverlay;
    }

    private static void SetUpShiftOverlay(this CalibratedCurve targetCurve)
    {
      var curve = new Curve(targetCurve.AsOf, Frequency.Continuous)
      {
        Interp = targetCurve.GetCurveShifts().OverlayInterp,
        Name = targetCurve.Name + "_overlay",
        DayCount = targetCurve.DayCount,
      };
      int n = targetCurve.Count;
      for (int i = 0; i < n; ++i)
      {
        curve.Add(targetCurve.GetDt(i), 1.0);
      }
      bool notCreditCurve = !(targetCurve is SurvivalCurve);
      targetCurve.AddOverlay(targetCurve.ShiftOverlay = curve, notCreditCurve);
      if (notCreditCurve) return;

      // For credit curve, we want to preserve the original dates.
      for (int i = 0; i < n; ++i)
      {
        targetCurve.Add(curve.GetDt(i), 1.0);
      }
    }

    private static void RestoreBaseCurve(this CalibratedCurve curve)
    {
      if (curve == null) return;
      if (curve.ShiftOverlay == null)
      {
        var shifts = curve.GetCurveShifts();
        if (shifts == null) return;
        var vals = shifts[CurveBumpScenario.ZeroBumpScenario];
        if (vals.IsNullOrEmpty()) return;
        SetCurvePoints(curve, vals);
        return;
      }
      curve.RemoveOverlay();
      curve.ShiftOverlay = null;
    }

    private static void Initialize(this Curve curve, double value)
    {
      if (curve == null) return;
      for (int i = 0, n = curve.Count; i < n; ++i)
      {
        curve.SetVal(i, value);
      }
    }

    private static void SetNoShift(Curve shift, Curve original)
    {
      if (original == null || shift == null) return;
      for (int i = 0, n = original.Count; i < n; ++i)
      {
        shift.Add(original.GetDt(i), 1.0);
      }

    }

    //
    // Get and set curve points.
    // Note: 
    //  (1) It is more efficient and numerically more accurate
    //      to get/set the rate (abscissa y) directly;
    //  (2) This is the only way to get/set the rate at the
    //      curve as-of date, when curve.GetVal(0) always returns
    //      1 regardless of the underlying rate. 

    private static double[] GetCurvePoints(Curve curve, bool withSpread = false)
    {
      int count = curve.Count;
      if (count == 0)
        return EmptyArray<double>.Instance;

      var native = curve.NativeCurve;
      var results = new double[count + (withSpread ? 1 : 0)];
      for (int i = 0; i < count; ++i)
      {
        results[i] = native.GetY(i);
      }
      if (withSpread) results[count] = native.GetSpread();
      return results;
    }

    private static void SetCurvePoints(Curve curve, double[] points)
    {
      if (points == null || points.Length == 0) return;

      int count = curve.Count;
      if (count == 0) return;
      if (points.Length < count)
      {
        count = points.Length;
      }
      else if (points.Length > count)
      {
        curve.Spread = points[count];
      }

      for (int i = 0; i < count; ++i)
      {
        curve.SetRate(i, points[i]);
      }
    }

    #endregion
  }
}
