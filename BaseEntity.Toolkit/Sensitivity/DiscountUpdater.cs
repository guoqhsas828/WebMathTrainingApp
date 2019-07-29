/*
 * DiscountUpdater.cs
 *
 *  -2010. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using CurveFitMethod = BaseEntity.Toolkit.Cashflows.CashflowCalibrator.CurveFittingMethod;
namespace BaseEntity.Toolkit.Sensitivity
{

  /// <summary>
  /// Class that is used to update the interp scheme and the curve fit settings method for curve
  /// </summary>
  public static class DiscountUpdater
  {
    private static void UpdateCalibrator(IHasCashflowCalibrator calibrator, string curveFitMethod, InterpScheme interpScheme)
    {
      if ((calibrator == null))
        return;
      calibrator.CashflowCalibratorSettings.MaximumSolverIterations = 0;
      CurveFitSettings cfSettings = calibrator.CurveFitSettings;
      if (cfSettings != null)
      {
        if (StringUtil.HasValue(curveFitMethod))
        {
          var cfMethod = (CurveFitMethod)Enum.Parse(typeof(CurveFitMethod), curveFitMethod);
          cfSettings.Method = cfMethod;
        }
        if (interpScheme != null)
          cfSettings.InterpScheme = interpScheme;
      }
    }

    /// <summary>
    /// Update discount curves calibrator and interpolation method for sensitivity calculation
    /// </summary>
    /// <param name="discountCurves">Discount curves</param>
    /// <param name="interpUpdater">The interp updater.</param>
    /// <param name="curveFitMethod">Curve fit method</param>
    internal static void UpdateDiscountCurves(IEnumerable<CalibratedCurve> discountCurves, InterpUpdater interpUpdater, string curveFitMethod)
    {
      foreach (var dc in discountCurves)
      {
        UpdateDiscountCurve(dc, interpUpdater, curveFitMethod, false);
      }
    }

    private static void UpdateDiscountCurve(CalibratedCurve discountCurve, InterpUpdater interpUpdater, string curveFitMethod, bool refit)
    {
      if (interpUpdater == null)
        return;
      var calibrator = discountCurve.Calibrator as IHasCashflowCalibrator;
      if (calibrator != null)
        UpdateCalibrator(calibrator, curveFitMethod, interpUpdater.InterpScheme);
      interpUpdater.UpdateCurve(discountCurve, refit);
    }

    /// <summary>
    /// Updates all the discount curves for a given pricer. 
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="interpUpdater">The Interpolation method</param>
    /// <param name="curveFitMethod">The curve fit method</param>
    internal static void UpdatePricerDiscountCurves(IPricer[] pricers, InterpUpdater interpUpdater, string curveFitMethod)
    {
      UpdatePricerDiscountCurves(pricers, interpUpdater, curveFitMethod, false);
    }

    /// <summary>
    /// Updates all the discount curves for a given pricer
    /// </summary>
    /// <param name="pricers">Pricers</param>
    /// <param name="interpUpdater">The interpolation method</param>
    /// <param name="curveFitMethod">The curve fit method</param>
    /// <param name="refit">True to refit dependent curves</param>
    internal static void UpdatePricerDiscountCurves(IPricer[] pricers, InterpUpdater interpUpdater, string curveFitMethod, bool refit)
    {
      var discountCurves = IPricerUtil.PricerDiscountCurves(pricers);
      foreach (var discountCurve in discountCurves)
      {
        UpdateDiscountCurve(discountCurve, interpUpdater, curveFitMethod, refit);
        if (discountCurve.DependentCurveList != null)
          foreach (var dc in discountCurve.DependentCurveList)
            UpdateDiscountCurve(dc, interpUpdater, curveFitMethod, refit);
      }
    }

    internal static List<DiscountCurve> PricerDiscountCurves(IPricer[] pricers)
    {
      var discs = new List<DiscountCurve>(pricers.Length);
      foreach (var pricer in pricers)
      {
        var dinfo = pricer.GetType().GetProperty("DiscountCurve",
                                                 BindingFlags.GetProperty | BindingFlags.Public |
                                                 BindingFlags.Instance);
        var discount = (DiscountCurve)dinfo.GetValue(pricer, null);
        var fxinfo = pricer.GetType().GetProperty("FxCurve",
                                                  BindingFlags.GetProperty | BindingFlags.Public |
                                                  BindingFlags.Instance);
        FxCurve fx = null;
        if (fxinfo != null)
          fx = (FxCurve)fxinfo.GetValue(pricer, null);
        DiscountCurve foreignDiscount = null;
        if (fx != null && !fx.IsSupplied)
          foreignDiscount = fx.Ccy1DiscountCurve;
        if (discount == null && foreignDiscount == null)
          continue;
        if (discount != null)
        {
          if (!discs.Contains(discount))
            discs.Add(discount);
        }
        if (foreignDiscount != null)
        {
          if (!discs.Contains(foreignDiscount))
            discs.Add(foreignDiscount);
        }
      }
      return discs;
    }


    /// <summary>
    /// Select tenors from a curve that satisfy a given predicate
    /// </summary>
    /// <param name="ccurves">Calibrated curve </param>
    /// <param name="match">Predicate</param>
    internal static List<CurveTenor> SelectTenors(CalibratedCurve[] ccurves, Predicate<CurveTenor> match)
    {
      var tenorsLst = new List<CurveTenor>();
      var names = new List<string>();
      foreach (CalibratedCurve ccurve in ccurves)
      {
        foreach (CurveTenor ten in ccurve.Tenors)
        {
          if (match(ten) && !tenorsLst.Contains(ten) && !names.Contains(ten.Name))
          {
            tenorsLst.Add(ten);
            names.Add(ten.Name);
          }
        }
      }
      return tenorsLst;
    }

    private static bool IsProjection(CalibratedCurve cc)
    {
      return cc.Calibrator is ProjectionCurveFitCalibrator;
    }

    private static bool ContainsRelevantReferences(this CalibratedCurve dc)
    {
      if (dc.DependentCurves.Count == 0) return false;
      var dependents = dc.DependentCurveList;
      for (int j = 0; j < dc.Tenors.Count; j++)
      {
        var swap = dc.Tenors[j].Product as Swap;
        if (swap == null) continue;
        if (
          dependents.Any(
            d => (d.ReferenceIndex != null) && (swap.ReceiverLeg.ReferenceIndex == d.ReferenceIndex || swap.PayerLeg.ReferenceIndex == d.ReferenceIndex)))
          return true;
      }
      return false;
    }

    private static void RemoveExtraTenors(DiscountCurve discountCurve)
    {
      if (!discountCurve.ContainsRelevantReferences())
        return;
      var discTenors =
        discountCurve.Tenors.Where(
          ten => discountCurve.DependentCurveList.Where(IsProjection).All(cc => cc.Tenors.Any(t => t.CurveDate == ten.CurveDate || t.Maturity == ten.Maturity))).ToList();
      discountCurve.Tenors.Clear();
      foreach (var curveTenor in discTenors)
        discountCurve.Tenors.Add(curveTenor);
      foreach (var dc in discountCurve.DependentCurveList.Where(IsProjection))
      {
        var projTenors = dc.Tenors.Where(ten => discountCurve.Tenors.Any(t => t.CurveDate == ten.CurveDate || t.Maturity == ten.Maturity)).ToList();
        dc.Tenors.Clear();
        foreach (var curveTenor in projTenors)
          dc.Tenors.Add(curveTenor);
      }
    }


    private static void GenerateQuote(CalibratedCurve ccurve, CurveTenor ten)
    {
      double quote;
      IPricer p = ccurve.Calibrator.GetPricer(ccurve, ten.Product);
      if (ten.Product is Swap)
      {
        var swap = ten.Product as Swap;
        quote = ((SwapPricer)p).ParCoupon();
        if (swap.IsBasisSwap)
        {
          if (swap.IsSpreadOnPayer)
            swap.PayerLeg.Coupon = quote;
          else
            swap.ReceiverLeg.Coupon = quote;
        }
        else
        {
          if (swap.IsPayerFixed)
            swap.PayerLeg.Coupon = quote;
          else
            swap.ReceiverLeg.Coupon = quote;
        }
      }
      if (ten.Product is SwapLeg)
      {
        quote = ((SwapPricer)p).ParCoupon();
        ((SwapLeg)ten.Product).Coupon = quote;
      }
      else if (ten.Product is Note)
      {
        quote = ((NotePricer)p).ParCoupon();
        ((Note)ten.Product).Coupon = quote;
      }
      else if (ten.Product is StirFuture)
      {
        quote = ((StirFuturePricer)p).ModelPrice();
        ten.MarketPv = quote;
      }
      else if (ten.Product is FRA)
      {
        quote = ((FRAPricer)p).ImpliedFraRate;
        ((FRA)ten.Product).Strike = quote;
      }
      else
        quote = ten.CurrentQuote.Value;
      CurveUtil.SetMarketQuote(ten, quote);
      ten.OriginalQuote = ten.CurrentQuote;
    }


    private static void SetCalibrator(DiscountCurve discount, ReferenceIndex referenceIndex)
    {
      var dcalibrator = discount.Calibrator as DiscountCurveFitCalibrator;
      if (dcalibrator != null)
        discount.Calibrator = new DiscountCurveFitCalibrator(dcalibrator.AsOf, referenceIndex, dcalibrator.CurveFitSettings);
    }

    /// <summary>
    /// This function assumes that the forward rates <m>F^p(T_i)</m> of the projection curve are given by <m>F^p(T_i) = F^d(T_i) + b_i</m> where 
    /// <m>F^d(T_i)</m> are the forwards of the discount curve and <m>b_i</m> is a forward basis. It then creates a discount curve whose tenors 
    /// are the same as those of the reference curve, with quotes adjusted to reflect the functional relationship above.
    /// The discount curve then becomes sensitive to both quotes in the discount curve and the reference curve. The basis 
    /// between forwards is mantained constant when quotes are perturbed. 
    /// </summary>
    /// <param name="pricers">An array of pricers. Assumes that all share the same discount curve</param>
    /// <param name="reference">Projection curve</param>
    internal static void ReplaceCurves(IPricer[] pricers, CalibratedCurve reference)
    {
      foreach (DiscountCurve discount in PricerDiscountCurves(pricers))
      {
        if (reference == null || reference == discount)
        {
          RemoveExtraTenors(discount);
          discount.ReFit(0);
          continue;
        }
        if (!reference.SameOrDependOn(discount)) continue;
        SetCalibrator(discount, reference.ReferenceIndex);
        discount.Tenors.Clear();
        discount.ReferenceIndex = reference.ReferenceIndex;
        var tenors = SelectTenors(new[] { reference },
                                               tenor =>
                                               tenor.CurrentQuote.Type != QuotingConvention.YieldSpread);
        foreach (var ten in tenors)
        {
          var tenorClone = (CurveTenor)ten.Clone();
          GenerateQuote(discount, tenorClone);
          discount.Add(tenorClone.Product, tenorClone.MarketPv,
                       tenorClone.Coupon, tenorClone.FinSpread,
                       tenorClone.Weight);
        }
        discount.SetTensionFactors(new double[0]);
      }
    }

    private static bool SameOrDependOn(this CalibratedCurve curve1, CalibratedCurve curve2)
    {
      if(curve1==curve2) return true;
      if(curve2.DependentCurves==null) return false;
      foreach(var curve in curve2.DependentCurves.Values)
      {
        if(curve1.SameOrDependOn(curve)) return true;
      }
      return false;
    }
  }

  internal class InterpUpdater
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(InterpUpdater));

    private Interp interp_;
    internal InterpScheme InterpScheme { get; set; }
    internal Interp Interp
    {
      get
      {
        if (interp_ == null && InterpScheme != null)
          interp_ = InterpScheme.ToInterp();
        return interp_;
      }
    }
    /// <summary>
    ///  Change the curve interp method and optionally refit the curve.
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <param name="refit">If set to <c>true</c>, refit the curve.</param>
    internal void UpdateCurve(CalibratedCurve curve, bool refit)
    {
      if(!(curve is DiscountCurve))
      {
#if DEBUG
        logger.DebugFormat("Ignored request to change Interp on curve [%0]",
          curve.Name ?? curve.GetType().Name);
#endif
        return;
      }
      var interp = Interp;
      if (interp == null)
      {
        // If the caller does not supply a interp scheme,
        // we check the curve to see if we need to modify it.
        var scheme = InterpScheme.FromInterp(curve.Interp);
        switch (scheme.Method)
        {
        case InterpMethod.Flat:
        case InterpMethod.Linear:
        case InterpMethod.LogLinear:
        case InterpMethod.Weighted:
        case InterpMethod.LogWeighted:
          // Local interp scheme, keep the old one.
          break;
        default:
          // Not local interp scheme.
          // We change it to Weighted.
          scheme.Method = InterpMethod.Weighted;
          curve.Interp = scheme.ToInterp();
          break;
        }
      }
      else
      {
        // The caller specifies an interp scheme.
        curve.Interp = interp;
      }
      if (refit) curve.ReFit(0);
    }
  }
}
