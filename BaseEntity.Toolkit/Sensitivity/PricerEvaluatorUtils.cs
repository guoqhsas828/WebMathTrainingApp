/*
 * PricerEvaluatorUtils.cs
 *
 *   2008. All rights reserved. 
 *
 * $Id $
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Numerics;

namespace BaseEntity.Toolkit.Sensitivity
{
  public interface IRecoverySensitivityCurvesGetter
  {
    /// <summary>
    /// Gets the curves consisting of both survival curves and recovery curves.
    /// </summary>
    /// <remarks>
    ///   This if the recovery curve is associated with a survival curve,
    ///   the later should be returned for the recalibration to work correctly.
    /// </remarks>
    /// <returns>IList{Curve}.</returns>
    IList<Curve> GetCurves(); 
  }

  public interface ISpreadSensitivityCurvesGetter
  {
    IList<SurvivalCurve> GetCurves();
  }

  public interface IDefaultSensitivityCurvesGetter
  {
    IList<SurvivalCurve> GetCurves();
  }

  
  /// <summary>
  ///   Utility methods for the PricerEvaluator class
  /// </summary>
  public static class PricerEvaluatorUtil
  {
    #region Utilities

    /// <summary>
    ///   Retrieve a list of discount curves from pricers 
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="mustExist">If true, throw exceptions when the properties are not found</param>
    /// <returns>List of DiscountCurves</returns>
    public static List<CalibratedCurve> GetRateCurves(PricerEvaluator[] pricers, bool mustExist)
    {
      var list = new List<CalibratedCurve>();
      foreach (var pricer in pricers)
      {
        var dc = pricer.RateCurves;
        if (dc != null)
        {
          foreach (var d in dc.Where(d => !list.Contains(d) && d is DiscountCurve))
            list.Add(d);
        }
        else if (mustExist)
          throw new ArgumentException(String.Format(
            "Unsupported pricer {0} - Does not have Reference Curve, DiscountCurve or DiscountCurves property", pricer.PricerType));
      }
      return list;
    }

    /// <summary>
    /// Retrieve a list of forward volatility cubes from pricers
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="mustExist">if true, throw exceptions when the properties are not found</param>
    /// <returns>List of Forward Volatility Cubes </returns>
    public static List<RateVolatilityCube> GetRateVolatilityCubes(PricerEvaluator[] pricers,bool mustExist)
    {
      var list = new List<RateVolatilityCube>();
      foreach(var pricer in pricers)
      {
        var fwdCubes = pricer.RateVolatilityCubes;
        if(fwdCubes!=null)
        {
          foreach (var cube in fwdCubes.Where(cube => !list.Contains(cube)))
            list.Add(cube);
        }
        else if(mustExist)
          throw new ArgumentException(String.Format("Unsupported pricer {0} - Does not have VolatilityCube property", pricer.PricerType));
      }
      return list;
    }

    /// <summary>
    ///   Retrieve a list of discount curves from pricers 
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="mustExist">If true, throw exceptions when the properties are not found</param>
    /// <returns>List of DiscountCurves</returns>
    public static List<DiscountCurve> GetFundingCurves(PricerEvaluator[] pricers, bool mustExist)
    {
      var list = new List<DiscountCurve>();
      foreach (var pricer in pricers)
      {
        var d = pricer.DiscountCurves;
        if (d != null)
          list.AddRange(d.Where(dc => !list.Contains(dc)));
        else if (mustExist)
          throw new ArgumentException(String.Format("Unsupported pricer {0} - Does not have DiscountCurve property",
                                                    pricer.PricerType));
        var fx = pricer.FxCurve;
        if (fx != null)
          list.AddRange(fx.Cast<FxCurve>().SelectMany(c => new[] {c.Ccy1DiscountCurve, c.Ccy2DiscountCurve}).Where(c => c != null && !list.Contains(c)));
      }
      return list;
    }

    /// <summary>
    ///   Retrieve a list of discount curves from pricers 
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="mustExist">If true, throw exceptions when the properties are not found</param>
    /// <returns>List of DiscountCurves</returns>
    public static List<DiscountCurve> GetDiscountCurves(PricerEvaluator[] pricers, bool mustExist)
    {
      var list = new List<DiscountCurve>();
      foreach (var pricer in pricers)
      {
        var d = pricer.DiscountCurve;
        if (d != null)
        {
          if (!list.Contains(d))
            list.Add(d);
        }
        else if (mustExist)
          throw new ArgumentException(String.Format(
            "Unsupported pricer {0} - Does not have DiscountCurve or DiscountCurves property", pricer.PricerType));
      }
      return list;
    }

    ///<summary>
    ///   Retrieve a list of reference curves from pricers 
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="mustExist">If true, throw exceptions when the properties are not found</param>
    /// <returns>List of DiscountCurves</returns>
    public static List<CalibratedCurve> GetReferenceCurves(PricerEvaluator[] pricers, bool mustExist)
    {
      var list = new List<CalibratedCurve>();
      foreach (var pricer in pricers)
      {
        var dc = pricer.RateCurves;
        if (dc != null)
        {
          foreach (var d in dc.Where(d => !list.Contains(d)))
            list.Add(d);
        }
        else if (mustExist)
          throw new ArgumentException(String.Format(
            "Unsupported pricer {0} - Does not have RateCurves property", pricer.PricerType));
      }
      return list;
    }

    /// <summary>
    /// Retrieve a list of overlay zero-rate curve
    /// </summary>
    /// <param name="pricers">Pricer list</param>
    /// <param name="throwIfException">Throw exception on error if true</param>
    /// <returns>List of overlay curves</returns>
    public static List<CalibratedCurve> GetZeroCurves(PricerEvaluator[] pricers, bool throwIfException)
    {
      var list = new List<CalibratedCurve>();
      foreach (PricerEvaluator pricer in pricers)
      {
        CalibratedCurve[] dc = pricer.RateCurves;
        if (dc != null)
        {
          foreach (CalibratedCurve d in dc)
          {
            if (d.Calibrator is OverlayCalibrator)
            {
              if (!list.Contains(d))
                list.Add(d);
            }
            else if (d.CustomInterpolator != null && d.CustomInterpolator is MultiplicativeOverlay)
            {
              var overlay = (CalibratedCurve)((MultiplicativeOverlay)d.CustomInterpolator).OverlayCurve;
              if (!list.Contains(overlay))
                list.Add(overlay);
            }
          }
        }
      }
      return list;
    }

    private static List<CalibratedCurve> FilterPricerRateCurves(IPricer[] pricers, string curveType)
    {
      var origCurves = new List<CalibratedCurve>();
      if (string.IsNullOrEmpty(curveType))
      {
        origCurves.AddRange(IPricerUtil.PricerDiscountCurves(pricers));
      }
      else
      {
        foreach (var curves in CollectionUtil.ConvertAll(pricers, p => PropertyGetBuilder
                .CreateGetter<CalibratedCurve>(p, curveType)(p)))
        {
          foreach (var curve in curves)
          {
            if (!origCurves.Contains((DiscountCurve)curve))
              origCurves.Add((DiscountCurve)curve);
          }
        }
      }
      return origCurves;
    }

    public static IList<CalibratedCurve> GetRateAndInflationCurves(this IPricer[] pricers, string[] curveNames = null)
    {
      var origCurves = new List<CalibratedCurve>();
      bool noCurveSelection = curveNames.IsNullOrEmpty();
      origCurves.AddRange(IPricerUtil.PricerDiscountCurves(pricers).Where(dc => noCurveSelection || (curveNames != null && curveNames.Contains(dc.Name))));
      foreach (var icurve in pricers
        .SelectMany(p => new PricerEvaluator(p).ReferenceCurves
          ?? EmptyArray<CalibratedCurve>.Instance)
        .OfType<InflationCurve>())
      {
        var curve = icurve.TargetCurve;
        if (curve != null && !origCurves.Contains(curve) && noCurveSelection || (curveNames != null &&curveNames.Contains(curve.Name)))
          origCurves.Add(curve);
      }
      return origCurves;
    }

    public class ZeroCurveBumpHelper
    {
      public ZeroCurveBumpHelper(string curveType)
      {
        curveType_ = curveType;
      }

      public CalibratedCurve[] GetZeroCurves(IPricer pricer)
      {
        var list = new List<CalibratedCurve>();
        foreach (var d in FilterPricerRateCurves(new IPricer[]{pricer}, curveType_))
        {
          if (d.Calibrator is OverlayCalibrator)
          {
            var overlay = ((OverlayCalibrator)d.Calibrator).OverlayCurve;
            if (!list.Contains(overlay))
              list.Add(overlay);
          }
        }
        return list.ToArray();
      }

      private static string curveType_;
    }

    /// <summary>
    ///   Create a get function to retrieve a reference curve from a pricer
    /// </summary>
    /// <returns>Property getter object</returns>
    public static Func<IPricer, CalibratedCurve[]> ZeroCurveGetter(string curveType) 
    {
      return (new ZeroCurveBumpHelper(curveType)).GetZeroCurves;
    }

    /// <summary>
    ///   Retrieve fx curve from pricers 
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="mustExist">If true, throw exceptions when the properties are not found</param>
    /// <returns>List of FXCurves</returns>
    public static List<CalibratedCurve> GetFxCurve(PricerEvaluator[] pricers, bool mustExist)
    {
      var list = new List<CalibratedCurve>();
      foreach (var pricer in pricers)
      {
        var fx = pricer.FxCurve;
        if (fx != null)
        {
          foreach (var f in fx.Where(f => !list.Contains(f)))
            list.Add(f);
        }
        else if (mustExist)
          throw new ArgumentException(String.Format(
            "Unsupported pricer {0} - Does not have FXCurve or FXCurves property", pricer.PricerType));
      }
      return list;
    }

    /// <summary>
    /// Gets the fx forward curves.
    /// </summary>
    /// <param name="pricers">The pricers.</param>
    /// <param name="mustExist">if set to <c>true</c> [must exist].</param>
    /// <returns>List of fx forward curves</returns>
    public static List<CalibratedCurve> GetFxForwardCurves(this PricerEvaluator[] pricers, bool mustExist)
    {
      var checkedFxCurves = new List<FxCurve>();
      var list = new List<CalibratedCurve>();
      foreach (var pricer in pricers)
      {
        var curves = pricer.FxCurve;
        if (curves != null)
        {
          foreach (var curve in curves)
          {
            var fxCurve = curve as FxCurve;
            // Not an FxCurve derived class
            if (fxCurve == null)
            {
              if (curve != null && !list.Contains(curve))
                list.Add(curve);
              continue;
            }
            if(checkedFxCurves.Contains(fxCurve)) continue;
            checkedFxCurves.Add(fxCurve);
            fxCurve.GetComponentCurves(c=>c.Tenors.Any(t=>t.Product is FxForward), false, list);
          }
        }
        else if (mustExist)
          throw new ToolkitException(String.Format(
            "Unsupported pricer {0} - Does not have FXCurve or FXCurves property", pricer.PricerType));
      }
      return list;
    }

    /// <summary>
    /// Retrieve the set of Basis adjustment curves from the pricers 
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="mustExist">If true, throw exceptions when the properties are not found</param>
    /// <returns>List of Basis Adjustment curves</returns>
    public static List<CalibratedCurve> GetBasisAdjustmentCurve(this PricerEvaluator[]pricers, bool mustExist)
    {
      var list = new List<CalibratedCurve>();
      foreach (var pricer in pricers)
      {
        var basisAdjustmentCurves = pricer.BasisAdjustmentCurves;
        if (basisAdjustmentCurves != null)
        {
          foreach (var f in basisAdjustmentCurves.Where(f => f != null && !list.Contains(f)))
            list.Add(f);
        }
        else if (mustExist)
          throw new ArgumentException(String.Format(
            "Unsupported pricer {0} - Does not have FXCurve or FXCurves property", pricer.PricerType));
      }
      return list;
    }

    /// <summary>
    ///   Retrieve a list of survival curves from pricers 
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="mustExist">If true, throw exceptions when the properties are not found</param>
    /// <returns>List of SurvivalCurves</returns>
    public static List<SurvivalCurve> GetSurvivalCurves(PricerEvaluator[] pricers, bool mustExist)
    {
      var list = new List<SurvivalCurve>();
      foreach (var pricer in pricers)
      {
        var sc = pricer.SurvivalCurves;
        if (sc != null)
        {
          foreach (var s in sc.Where(s => !list.Contains(s)))
            list.Add(s);
        }
        else if (mustExist)
          throw new System.ArgumentException(String.Format(
            "Unsupported pricer {0} - Does not have SurvivalCurve or SurvivalCurves property", pricer.PricerType));
      }
      return list;
    }

    /// <summary>
    ///   Retrieve a list of recovery curves from pricers 
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="mustExist">If true, throw exceptions when the properties are not found</param>
    /// <returns>List of RecoveryCurves</returns>
    public static List<RecoveryCurve> GetRecoveryCurves(PricerEvaluator[] pricers, bool mustExist)
    {
      var list = new List<RecoveryCurve>();
      foreach (var pricer in pricers)
      {
        var rc = pricer.RecoveryCurves;
        if (rc != null)
        {
          foreach (var r in rc.Where(r => !list.Contains(r)))
            list.Add(r);
        }
        else if (mustExist)
          throw new ArgumentException(String.Format(
            "Unsupported pricer {0} - Does not have RecoveryCurve or RecoveryCurves property", pricer.PricerType));
      }
      return list;
    }

    /// <summary>
    ///   Retrieve a list of inflation curves from pricers 
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="mustExist">If true, throw exceptions when the properties are not found</param>
    /// <returns>List of InflationCurves</returns>
    public static List<InflationCurve> GetInflationCurves(PricerEvaluator[] pricers, bool mustExist)
    {
      var list = new List<InflationCurve>();
      foreach (var pricer in pricers)
      {
        var rc = pricer.InflationCurves;
        if (rc != null)
        {
          foreach (var r in rc.Where(r => !list.Contains(r)))
            list.Add(r);
        }
        else if (mustExist)
          throw new ArgumentException(String.Format(
            "Unsupported pricer {0} - Does not have InflationCurve or InflationCurves property", pricer.PricerType));
      }
      return list;
    }

    /// <summary>
    ///   Retrieve a list of commodity curves from pricers 
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="mustExist">If true, throw exceptions when the properties are not found</param>
    /// <returns>List of CommodityCurves</returns>
    public static List<CommodityCurve> GetCommodityCurves(PricerEvaluator[] pricers, bool mustExist)
    {
      var list = new List<CommodityCurve>();
      foreach (var pricer in pricers)
      {
        var rc = pricer.CommodityCurves;
        if (rc != null)
        {
          foreach (var r in rc.Where(r => !list.Contains(r)))
            list.Add(r);
        }
        else if (mustExist)
          throw new ArgumentException(String.Format(
            "Unsupported pricer {0} - Does not have CommodityCurve or CommodityCurves property", pricer.PricerType));
      }
      return list;
    }

    /// <summary>
    ///   Retrieve a list of stock curves from pricers 
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="mustExist">If true, throw exceptions when the properties are not found</param>
    /// <returns>List of StockCurves</returns>
    public static List<StockCurve> GetStockCurves(PricerEvaluator[] pricers, bool mustExist)
    {
      var list = new List<StockCurve>();
      foreach (var pricer in pricers)
      {
        var rc = pricer.StockCurves;
        if (rc != null)
        {
          foreach (var r in rc.Where(r => !list.Contains(r)))
            list.Add(r);
        }
        else if (mustExist)
          throw new ArgumentException(String.Format(
            "Unsupported pricer {0} - Does not have StockCurve or StockCurves property", pricer.PricerType));
      }
      return list;
    }

    /// <summary>
    ///   Retrieve a list of correlations from pricers 
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <returns>List of correlation objects</returns>
    public static List<CorrelationObject> GetCorrelations(PricerEvaluator[] pricers)
    {
      var list = new List<CorrelationObject>();
      foreach (var pricer in pricers)
      {
        CorrelationObject[] corrs = pricer.Correlations;
        if (corrs != null)
          foreach (CorrelationObject c in corrs)
            if (!list.Contains(c))
              list.Add(c);
      }
      return list;
    }

    /// <summary>
    /// Get the curve for recovery sensitivity calculation
    /// </summary>
    /// <param name="pricers">The pricers</param>
    /// <param name="mustExist">if set to <c>true</c>, then an exception is thrown if no curve is retrieved</param>
    /// <returns>Curves</returns>
    public static List<Curve> GetRecoverySensitivityCurves(
      this PricerEvaluator[] pricers, bool mustExist)
    {
      var list = new List<Curve>();
      foreach (var pricer in pricers)
      {
        var g = pricer.Pricer as IRecoverySensitivityCurvesGetter;
        Curve[] sc = g != null
          ? g.GetCurves().ToArray() : pricer.SurvivalCurves;
        pricer.SetDependentCurves(sc);
        if (sc != null && sc.Length != 0)
        {
          foreach (var s in sc)
            if (s != null && !list.Contains(s))
              list.Add(s);
        }
        else if (mustExist)
          throw new System.ArgumentException(String.Format(
            "Unsupported pricer {0} - Does not have SurvivalCurve or RecoveryCurve",
            pricer.PricerType));
      }
      return list;
    }

    public static IEnumerable<SurvivalCurve> GetBasketUnsettledDefaults(
      this PricerEvaluator[] pricers)
    {
      return pricers.SelectMany(e => GetUnsettledDefaults(e.Basket));
    }

    public static SurvivalCurve[] GetSurvivalCurves(
      this BaseEntity.Toolkit.Pricers.BasketPricers.BasketPricer basket,
      bool withRecoverySensitivity)
    {
      var sc = basket.SurvivalCurves;
      if (withRecoverySensitivity && basket.GetUnsettledDefaults().Any())
      {
        // Order is important: Regular curves followed by unsettled defaults.
        sc = sc.Concat(basket.GetUnsettledDefaults()).ToArray();
      }
      return sc;
    }

    public static int GetSurvivalBumpCount(
      this BaseEntity.Toolkit.Pricers.BasketPricers.BasketPricer basket,
      bool withRecoverySensitivity)
    {
      return basket.SurvivalCurves.Length + (withRecoverySensitivity
        ? basket.GetUnsettledDefaults().Count() : 0);
    }

    private static IEnumerable<SurvivalCurve> GetUnsettledDefaults(
      this BaseEntity.Toolkit.Pricers.BasketPricers.BasketPricer basket)
    {
      return basket?.OriginalBasket.SurvivalCurves.Where(HasUnsettledRecovery)
        ?? Enumerable.Empty<SurvivalCurve>();
    }

    public static bool HasUnsettledRecovery(this SurvivalCurve sc)
    {
      if (sc.Defaulted != Defaulted.HasDefaulted) return false;
      var rc = sc.SurvivalCalibrator?.RecoveryCurve;
      return (rc != null && rc.Recovered == Recovered.WillRecover);
    }

    /// <summary>
    /// Gets curves for spread sensitivity calculation
    /// </summary>
    /// <param name="pricers">The pricers</param>
    /// <param name="mustExist">if set to <c>true</c>, then an exception is thrown if no curve is retrieved</param>
    /// <returns>Survival Curves</returns>
    public static List<SurvivalCurve> GetSpreadSensitivityCurves( this PricerEvaluator[] pricers, bool mustExist)
    {
      var list = new List<SurvivalCurve>();
      foreach (var pricer in pricers)
      {
        var g = pricer.Pricer as ISpreadSensitivityCurvesGetter;
        SurvivalCurve[] sc = g != null
          ? g.GetCurves().ToArray() : pricer.SurvivalCurves;
        pricer.SetDependentCurves(sc);
        if (sc != null && sc.Length != 0)
        {
          foreach (var s in sc)
            if (s != null && !list.Contains(s))
              list.Add(s);
        }
        else if (mustExist)
          throw new System.ArgumentException(String.Format(
            "Unsupported pricer {0} - Does not have SurvivalCurve or SurvivalCurves property",
            pricer.PricerType));
      }
      return list;
    }

    /// <summary>
    /// Gets curves for default sensitivity calculation
    /// </summary>
    /// <param name="pricers">The pricers.</param>
    /// <param name="mustExist">if set to <c>true</c>, then an exception is thrown if no curve is retrieved.</param>
    /// <returns>Survival Curves</returns>
    public static List<SurvivalCurve> GetDefaultSensitivityCurves( this PricerEvaluator[] pricers, bool mustExist)
    {
      var list = new List<SurvivalCurve>();
      foreach (var pricer in pricers)
      {
        var g = pricer.Pricer as IDefaultSensitivityCurvesGetter;
        SurvivalCurve[] sc = g != null
          ? g.GetCurves().ToArray() : pricer.SurvivalCurves;
        pricer.SetDependentCurves(sc);
        if (sc != null && sc.Length != 0)
        {
          foreach (var s in sc)
            if (s != null && !list.Contains(s))
              list.Add(s);
        }
        else if (mustExist)
          throw new ArgumentException(String.Format(
            "Unsupported pricer {0} - Does not have SurvivalCurve or SurvivalCurves property",
            pricer.PricerType));
      }
      return list;
    }

    private static T[] ToArray<T>(this IList<T> list)
    {
      if (list == null || list.Count == 0)
        return null;
      if (list is T[])
        return (T[])list;
      if (list is List<T>)
        return ((List<T>)list).ToArray();
      int count = list.Count;
      var result = new T[list.Count];
      for (int i = 0; i < count; ++i)
        result[i] = list[i];
      return result;
    }

    #region Zero-coupon based Sensitivity Helpers

    /// <summary>
    ///   Invoke a zero rate sensitivity function based on the type of curve specified to bump. 
    ///   This method adds a zero-rate instrument based overlay curve to the specified pricer rate curve,
    ///   then remove the overlay curve after zero rate sensitivity calculation is done 
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="curveType">Specify curve type to be bumped, if null, select all rate curves</param>
    /// <param name="interp">Zero curve interpolation scheme</param>
    /// <param name="compoundingFreq">Zero-rate compounding frequency</param>
    /// <param name="calculate">Action to calculate the sensitivity</param>
    /// <param name="bumpTenors">Zero rate tenors for bumping</param>
    public static void InvokeZeroRateSensitivityCalc(
      IPricer[] pricers,
      string[] bumpTenors,
      string curveType,
      Interp interp,
      Frequency compoundingFreq,
      Action<Func<PricerEvaluator[], bool, IList<CalibratedCurve>>> calculate)
    {
      InvokeZeroRateSensitivityCalc(
       FilterPricerRateCurves(pricers, curveType),
       bumpTenors, interp, compoundingFreq, calculate);
    }

    public static void InvokeZeroRateSensitivityCalc(
      this IList<CalibratedCurve> origCurves,  
      string[] bumpTenors,
      Interp interp,
      Frequency compoundingFreq,
      Action<Func<PricerEvaluator[], bool, IList<CalibratedCurve>>> calculate)
    {
      var usedCurves = new List<CalibratedCurve>();
      var zeroCurves = new List<CalibratedCurve>();
      try
      {
        for (int i = 0, n = origCurves.Count; i < n; ++i)
        {
          if (usedCurves.Contains(origCurves[i]))
            continue;

          var curve = origCurves[i];
          var roll = BDConvention.None;
          var cal = Calendar.None;
          BaseEntity.Toolkit.Base.ReferenceIndices.ReferenceIndex targetIndex = null;
          if (curve.Calibrator != null && curve.Calibrator is DiscountCurveFitCalibrator)
          {
            targetIndex = ((DiscountCurveFitCalibrator) curve.Calibrator).ReferenceIndex;
          }
          else if (curve.Calibrator != null && curve.Calibrator is ProjectionCurveFitCalibrator)
          {
            targetIndex = ((ProjectionCurveFitCalibrator)curve.Calibrator).ReferenceIndex;
          }

          if (targetIndex != null)
          {
            roll = targetIndex.Roll;
            cal = targetIndex.Calendar;
          }

          Dt asOf = curve.AsOf;
          string[] zeroCurveTenors;
          Dt[] maturities;
          if (bumpTenors.IsNullOrEmpty() || bumpTenors[0] == "all")
          {
            maturities = curve.Tenors.Select(t => t.Maturity)
              .Where(x => x > asOf).Distinct().ToArray();
            zeroCurveTenors = maturities
              .Select(d => Tenor.FromDateInterval(asOf, d).ToString("S", null))
              .ToArray();
          }
          else
          {
            zeroCurveTenors = bumpTenors;
            maturities = CollectionUtil.ConvertAll(zeroCurveTenors, t => Dt.Roll(Dt.Add(curve.AsOf, t), roll, cal));  
          }

          var dc = CurveUtil.ConstructZeroCurve(curve, zeroCurveTenors, maturities, DayCount.Actual365Fixed,
                                                roll, cal, compoundingFreq, interp);
          curve.AddOverlay(dc);
          usedCurves.Add(curve);
          zeroCurves.Add(dc);
        }

        calculate((p,b)=>zeroCurves);
      }
      finally
      {
        for (int i = 0; i < usedCurves.Count; ++i)
        {
          // restore origDiscCurves
          if (usedCurves[i] != null)
            usedCurves[i].RemoveOverlay();
        }
      }
    }

    #endregion Zero-coupon based Sensitivity

    #endregion Utilities

  }
}
