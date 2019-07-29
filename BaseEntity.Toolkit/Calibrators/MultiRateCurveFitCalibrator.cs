// 
//  -2012. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using log4net;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base.ReferenceRates;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;

namespace BaseEntity.Toolkit.Calibrators
{
  using OptimizerStatus = CashflowCalibrator.OptimizerStatus;
  using CashflowFitSettings = CashflowCalibrator.CashflowCalibratorSettings;
  using CurveFitMethod = CashflowCalibrator.CurveFittingMethod;
  using Parameter = RateModelParameters.Param;
  using Process = RateModelParameters.Process;

  #region MultiRateCurveFitCalibrator

  /// <summary>
  ///   Discount fit calibrator
  /// </summary>
  [Serializable]
  public class MultiRateCurveFitCalibrator : DiscountCalibrator,
    IHasCashflowCalibrator, IRateCurveCalibrator
  {

     /*
     * Some design considerations:
     * 
     * 1. The data member _prerequisites contains the curves required
     *    to build (calibrate) the target curve;
     *    
     * 2. The property MultiRateCurveSet contains all the curves required
     *    to build the hedge pricers for the curve tenors. 
     * 
     * 1 and 2 can be very different sets.  Example: OIS discount curve
     * does not require projection curves to calibrate, but it needs the
     * projection curves to build the hedge pricers for the swap/basis swap
     * tenors.  Hence the prerequisite curve set is empty, but the multi-rate
     * curve set must contain all the relevant projections curves for the
     * hedge calculation to work.
     * 
     * The design is to have the related curves to share the same multi-rate
     * curve set, and add itself to this set once calibrated, without erasing
     * the curves already exist in the sets. 
     * 
     * Currently we compare curves by reference, not by reference index or name
     * ID.  A projection curve may appear in the set twice if we build it twice
     * with two different instances.  This is not good.  An alternative is to
     * compare by reference index and replace the existing curve instance with
     * the new one if both share the same reference index.  Not sure whether
     * this is a breaking change or not.
     * 
     * Anyway, the best practice is to CALIBRATE ALL THE RELATED CURVES EXACTLY
     * ONCE IN THE DEPENDENCY ORDER.
     */

    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof (MultiRateCurveFitCalibrator));

    /// <summary>
    ///   Fit interest rate curve
    /// </summary>
    /// <param name="curveName">The name of the curve to fit</param>
    /// <param name="category">The curve category</param>
    /// <param name="tradeDate">The as-of date</param>
    /// <param name="targetRate">The target interest rate of the curve</param>
    /// <param name="targetIndexTenor">The target interest index tenor of the curve</param>
    /// <param name="basisRate">Basis reference rate</param>
    /// <param name="basisIndexTenor">Basis index tenor</param>
    /// <param name="longTargetIndexTenor">The long target interest index tenor of the curve</param>
    /// <param name="longbasisIndexTenor">The long basis swap tenor</param>
    /// <param name="futuresTenor">Futures tenor if different from the index tenor</param>
    /// <param name="tenorQuotes">The collection of market quotes used to fit the curve</param>
    /// <param name="fitSettings">The calibration settings</param>
    /// <param name="referenceCurves">The known reference curves (discount curve or projection 
    /// curves) required to evaluate the instrument</param>
    /// <returns></returns>
    public static DiscountCurve Fit(
      string curveName,
      string category,
      Dt tradeDate,
      InterestReferenceRate targetRate,
      Tenor targetIndexTenor,
      Tenor longTargetIndexTenor,
      InterestReferenceRate basisRate,
      Tenor basisIndexTenor,
      Tenor longbasisIndexTenor,
      Tenor futuresTenor,
      IEnumerable<CurveTenor> tenorQuotes,
      CalibratorSettings fitSettings,
      IList<CalibratedCurve> referenceCurves)
    {
      if (targetRate == null)
        throw new NullReferenceException("Target interest rate cannot be empty");
      var targetIndex = new InterestRateIndex(targetRate, targetIndexTenor);
      var targetIndexLong = (longTargetIndexTenor != Tenor.Empty) ? new InterestRateIndex(targetRate, longTargetIndexTenor) : null;
      var basisIndex = (basisRate != null) ? new InterestRateIndex(basisRate, basisIndexTenor) : null;
      var basisIndexLong = (longTargetIndexTenor != Tenor.Empty) ? new InterestRateIndex(targetRate, longbasisIndexTenor) : null;
      var targetIndexFutures = (futuresTenor != Tenor.Empty) ? new InterestRateIndex(targetRate, futuresTenor) : targetIndex;

      if (fitSettings == null)
        fitSettings = new CalibratorSettings();
      var mc = GetMultiRateCurveSet(referenceCurves);
      var discountCurve = mc.DiscountCurve;
      var calibrator = new MultiRateCurveFitCalibrator(tradeDate,  mc, targetIndex, targetIndexLong, referenceCurves, fitSettings);
      DiscountCurve curve;
      if (fitSettings.CreateAsBasis)
      {
        var basisSettings = new CalibratorSettings(fitSettings)
        {
          OverlayCurve = discountCurve,
          FwdModelParameters = fitSettings.FwdModelParameters
        };
        curve = DiscountCurveCalibrationUtils.CreateTargetDiscountCurve(calibrator, basisSettings, targetIndex, category, curveName);
      }
      else
        curve = DiscountCurveCalibrationUtils.CreateTargetDiscountCurve(calibrator, fitSettings, targetIndex, category, curveName);
      
      if (discountCurve == null)
      {
        // If no discount curve supplied, we are fitting the discount curve.
        Debug.Assert(mc.DiscountCurve == null);
        mc.DiscountCurve = curve;
      }
      else
      {
        // With the discount curve given, we are fitting a projection curve.
        mc.AddProjectionCurve(curve);
      }

      var tenors = curve.Tenors = new CurveTenorCollection();
      foreach (var tenor in tenorQuotes)
      {
        if (tenor == null) continue;
        tenor.UpdateProduct(tradeDate);
        if (!MatchIndex(tenor, targetIndex, targetIndexLong, basisIndex, basisIndexLong, targetIndexFutures, fitSettings.AllSwaps))
          continue;
        tenors.Add(tenor);
      }
      var overlap = new OverlapTreatment(fitSettings.OverlapTreatmentOrder);
      // This calibrator relies on the fact that there exists only
      // a single instance for each distinct quote/tenor, no mater
      // how many times the tenor appears in different curves.
      curve.ResolveOverlap(overlap, cloneIndividualTenors: false);
      curve.Fit();
      return curve;
    }

    private static bool MatchIndex(CurveTenor tenor,
      ReferenceIndex targetIndex, ReferenceIndex targetIndexLong,
      ReferenceIndex basisIndex, ReferenceIndex basisIndexLong, 
      ReferenceIndex futuresIndex, bool includeAllSwaps)
    {
      if (tenor.Product == null || tenor.Product.Ccy != targetIndex.Currency)
        return false;
      var index = tenor.ReferenceIndex;
      if (index == null)
        return true; // Empty means match any
      if (tenor.Product is StirFuture)
        return futuresIndex.IsEqual(futuresIndex);
      var swap = tenor.Product as Swap;
      if (swap != null)
      {
        // Include all swaps for given currency
        if (includeAllSwaps && swap.Ccy == targetIndex.Currency)
          return true;
        if (swap.IsFixedAndFloating)
          return swap.ReferenceIndices.Any(o => o.IsEqual(targetIndex) 
          || (targetIndexLong != null && o.IsEqual(targetIndexLong))
          || o.IsEqual(basisIndex) 
          || (basisIndexLong != null && o.IsEqual(basisIndexLong)));
        if (basisIndex != null && swap.IsBasisSwap)
          return swap.ReferenceIndices.Any(o => o.IsEqual(targetIndex) 
          || (targetIndexLong != null && o.IsEqual(targetIndexLong)))
          && swap.ReferenceIndices.Any(o => o.IsEqual(basisIndex) 
          || (basisIndexLong != null && o.IsEqual(basisIndexLong)));
      }
      return (targetIndex.IsEqual(index) 
        || (targetIndexLong != null && targetIndexLong.IsEqual(index)));
    }

    private static MultiRateCurveSet GetMultiRateCurveSet(
      IList<CalibratedCurve> curves)
    {
      var mc = FindMultiRateCurveSet(curves) ?? new MultiRateCurveSet();
      if (curves.IsNullOrEmpty()) return mc;

      var discountCurve = mc.DiscountCurve;
      foreach (var curve in curves)
      {
        if (ReferenceEquals(curve, discountCurve)) continue;
        mc.AddProjectionCurve(curve);
      }
      return mc;
    }

    private static MultiRateCurveSet FindMultiRateCurveSet(
      IList<CalibratedCurve> curves)
    {
      if (curves == null) return null;
      for (int i = 0, n = curves.Count; i < n; ++i)
      {
        var curve = curves[i];
        var calibrator = curve.Calibrator as MultiRateCurveFitCalibrator;
        if (calibrator == null) continue;
        var mc = calibrator.MultiRateCurveSet;
        if (mc == null) continue;
        if (mc.DiscountCurve == null)
          throw new NullReferenceException("Discount curve cannot be null");
        return mc;
      }
      return null;
    }

    #region Methods

    #region Constructor

    private MultiRateCurveFitCalibrator(
      Dt asOf,
      MultiRateCurveSet multiCurveSet,
      ReferenceIndex referenceIndex,
      ReferenceIndex referenceIndexLong,
      IList<CalibratedCurve> projectionCurves,
      CalibratorSettings curveFitSettings)
      : base(asOf, asOf)
    {
      if (referenceIndex == null)
        throw new ToolkitException("Target reference index cannot be null");
      ReferenceIndex = referenceIndex;
      ReferenceIndexLong = referenceIndexLong;

      if (curveFitSettings == null)
        CurveFitSettings = new CalibratorSettings {CurveAsOf = AsOf};
      else
      {
        CurveFitSettings = curveFitSettings;
        if (CurveFitSettings.CurveAsOf.IsEmpty())
          CurveFitSettings.CurveAsOf = AsOf;
      }
      CashflowCalibratorSettings = new CashflowFitSettings()
      {
        SolverTolerance = CurveFitSettings.Tolerance
      };

      // Initialize the multi-curve set field
      if (multiCurveSet == null)
        throw new ToolkitException("Multi-curve set cannot be null");
      MultiRateCurveSet = multiCurveSet;

      // Record prerequisite curves
      _prerequisites = new List<CalibratedCurve>();
      var discountCurve = multiCurveSet.DiscountCurve;
      if (discountCurve != null)
      {
        _prerequisites.Add(discountCurve);
      }
      if (projectionCurves != null)
      {
        foreach (var projectionCurve in projectionCurves)
        {
          if (_prerequisites.Contains(projectionCurve))
            continue;
          if (!multiCurveSet.Contains(projectionCurve))
          {
            throw new ToolkitException(String.Format(
              "Prerequisite curve {0} not in the specified multi-curve set",
              projectionCurve.Name));
          }
          if (!_prerequisites.Contains(projectionCurve))
            _prerequisites.Add(projectionCurve);
        }
      }

      //TODO: do we really need this?
      SetParentCurves(ParentCurves, discountCurve);
      if (projectionCurves != null)
        foreach (var projectionCurve in projectionCurves)
          SetParentCurves(ParentCurves, projectionCurve);
    }

    #endregion


    #region Calibration

    /// <summary>
    ///   Fit a curve from the specified tenor point
    /// </summary>
    /// <param name = "curve">Curve to calibrate</param>
    /// <param name = "fromIdx">Index to start fit from</param>
    /// <remarks>
    ///   <para>Derived calibrated curves implement this to do the work of the
    ///     fitting</para>
    ///   <para>Called by Fit() and Refit(). Child calibrators can assume
    ///     that the tenorNames have been validated and the data curve has
    ///     been cleared for a full refit (fromIdx = 0).</para>
    /// </remarks>
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      curve.ReferenceIndex = ReferenceIndex;
      if (string.IsNullOrEmpty(curve.Name))
        curve.Name = string.Concat(ReferenceIndex.IndexName, "_Curve");

      if (logger.IsDebugEnabled)
      {
        logger.Debug(string.Format("Begin fit curve {0}", curve.Name));
      }

      bool chainedSwaps = CurveFitSettings.ChainedSwapApproach;
      var rateIndices = GetKnownIndices(ReferenceIndex, ReferenceIndexLong);

      // Chained swap approach
      var calibrationTenors = curve.Tenors.ComposeSwapChain(
        ReferenceIndex, ReferenceIndexLong, rateIndices, 
        CurveFitSettings.ChainedSwapApproach);
      var calibrator = FillData(curve, calibrationTenors);
      calibrator.Lower = 1e-8;
      calibrator.Upper = 1.0;
      IModelParameter vol = null;
      if (CurveFitSettings.Method == CurveFitMethod.SmoothFutures &&
        CurveFitSettings.FwdModelParameters != null)
        CurveFitSettings.FwdModelParameters.TryGetValue(Process.Projection,
          Parameter.Custom, out vol);
      if (CurveFitSettings.MaximumIterations >= 0)
      {
        CashflowCalibratorSettings.MaximumOptimizerIterations =
          CashflowCalibratorSettings.MaximumSolverIterations =
            CurveFitSettings.MaximumIterations;
      }
      double[] pricerErrors;
      FittingErrorCode =
        calibrator.Calibrate(CurveFitSettings.Method, curve,
          CurveFitSettings.SlopeWeightCurve,
          CurveFitSettings.CurvatureWeightCurve, vol, out pricerErrors,
          CashflowCalibratorSettings);
      if (logger.IsDebugEnabled)
      {
        logger.Debug(string.Format(
          "End fit curve {0}, FittingErrorCode = {1}, PricerErrors = {2}",
          curve.Name, FittingErrorCode,
          pricerErrors.Aggregate("", (s, d) => String.IsNullOrEmpty(s)
            ? d.ToString("R") : s + '\t' + d.ToString("R"))));
      }

      //TODO: do we really need this?
      SetDependentCurves(curve, DiscountCurve);
      if (ProjectionCurves != null)
        foreach (var parent in ProjectionCurves)
          SetDependentCurves(curve, parent);
    }

    /// <summary>
    /// List of parent curves
    /// </summary>
    /// <returns>List of parent curves</returns>
    public override IEnumerable<CalibratedCurve> EnumerateParentCurves()
    {
      return _prerequisites.AsReadOnly();
    }

    /// <summary>
    ///   Create a pricer equal to the one used for the basis curve calibration
    /// </summary>
    /// <param name = "curve">Calibrated curve</param>
    /// <param name = "product">Interest rate product</param>
    /// <returns>Instantiated pricer</returns>
    public override IPricer GetPricer(CalibratedCurve curve, IProduct product)
    {
      var note = product as Note;
      if (note != null)
      {
        Dt settle = note.Effective;
        var pricer = new NotePricer(note, AsOf, settle, 1.0, (DiscountCurve) curve);
        return pricer;
      }

      var discountCurve = DiscountCurve ?? (DiscountCurve) curve;
      var future = product as StirFuture;
      if (future != null)
      {
        var pricer = new StirFuturePricer(future, AsOf, Settle,
          1.0/future.ContractSize, discountCurve, (DiscountCurve) curve)
        {RateModelParameters = CurveFitSettings.FwdModelParameters};
        pricer.Validate();
        return pricer;
      }
      var swap = product as Swap;
      if (swap != null)
      {
        var receiverPricer = new SwapLegPricer(swap.ReceiverLeg, AsOf,
          swap.Effective, 1.0, discountCurve, swap.ReceiverLeg.ReferenceIndex,
          GetProjectionCurve(curve, MultiRateCurveSet, swap.ReceiverLeg.ReferenceIndex),
          new RateResets(0.0, 0.0), CurveFitSettings.FwdModelParameters, null)
        {
          ApproximateForFastCalculation = CurveFitSettings.ApproximateRateProjection
        };
        var payerPricer = new SwapLegPricer(swap.PayerLeg, AsOf,
          swap.Effective, -1.0, discountCurve, swap.PayerLeg.ReferenceIndex,
          GetProjectionCurve(curve, MultiRateCurveSet, swap.PayerLeg.ReferenceIndex),
          new RateResets(0.0, 0.0), CurveFitSettings.FwdModelParameters, null)
        {
          ApproximateForFastCalculation = CurveFitSettings.ApproximateRateProjection
        };
        var pricer = new SwapPricer(receiverPricer, payerPricer);
        pricer.Validate();
        return pricer;
      }
      var fra = product as FRA;
      if (fra != null)
      {
        var pricer = new FRAPricer(fra, AsOf, fra.Effective, DiscountCurve,
          (DiscountCurve) curve, 1);
        pricer.Validate();
        return pricer;
      }
      throw new ToolkitException("Product not supported");
    }

    private CashflowCalibrator FillData(CalibratedCurve curve, 
      CurveTenorCollection tenors)
    {
      DiscountCurveCalibrationUtils.SetCurveDates(tenors);
      tenors.Sort();

      var discountCurve = DiscountCurve ?? (DiscountCurve) curve;
      IList<CalibratedCurve> projectionCurves = null;
      var calibrator = new CashflowCalibrator(curve.AsOf);
      foreach (CurveTenor tenor in tenors)
      {
        //the new branch to add SwapChain product
        if (tenor.Product is SwapChain)
        {
          var swaps = (SwapChain) tenor.Product;
          var payments = swaps.Chain.GetSwapChainPayments(swaps.Count,
            curve.ReferenceIndex, discountCurve,
            projectionCurves ?? (projectionCurves = MergeProjectionCurves(curve)),
            CurveFitSettings);
          calibrator.Add(0.0, payments[0].ToArray(), payments[1].ToArray(),
            swaps.Effective, discountCurve, tenor.CurveDate, 1.0,
            NeedsParallel(CurveFitSettings, swaps.ReceiverLeg.ProjectionType),
            NeedsParallel(CurveFitSettings, swaps.PayerLeg.ProjectionType));
        }
        else if (tenor.Product is CD)
        {
          var cd = tenor.Product as CD;
          Dt settle = cd.Effective;
          var q = tenor.CurrentQuote;
          var pricer = new CDPricer(cd, AsOf, settle, q.Type, q.Value,
            (DiscountCurve) curve, null, null, 1.0);
          calibrator.Add(1.0, pricer.GetPaymentSchedule(null, curve.AsOf), pricer.Settle,
            (DiscountCurve) curve, tenor.CurveDate, tenor.Weight, true);
        }
        else if (tenor.Product is Note)
        {
          var pricer = (NotePricer) GetPricer(curve, tenor.Product);
          calibrator.Add(1.0, pricer.GetPaymentSchedule(null, curve.AsOf), pricer.Settle,
            (DiscountCurve) curve,
            tenor.CurveDate, tenor.Weight, true);
        }
        else if (tenor.Product is StirFuture)
        {
          var pricer = (StirFuturePricer) curve.Calibrator.GetPricer(curve, tenor.Product);
          var ps = pricer.GetPaymentSchedule(null, curve.AsOf);
          double frac = 0.0;
          foreach (var payment in ps)
          {
            var ip = (FloatingInterestPayment) payment;
            frac += ip.AccrualFactor;
          }
          calibrator.Add((1 - tenor.MarketPv)*frac, ps,
            pricer.StirFuture.DepositSettlement, null,
            tenor.CurveDate, tenor.Weight, true);
        }
        else if (tenor.Product is Swap)
        {
          AddSwapCashflow(tenor, curve, discountCurve, calibrator);
        }
        else if (tenor.Product is FRA)
        {
          var pricer = (FRAPricer) GetPricer(curve, tenor.Product);
          calibrator.Add(0.0, pricer.GetPaymentSchedule(null, curve.AsOf), pricer.Settle,
            null, tenor.CurveDate,
            tenor.Weight, true);
        }
        else
        {
          throw new ToolkitException(
            String.Format("Calibration to products of type {0} not handled",
              tenor.Product.GetType()));
        }
      }
      return calibrator;
    }

    private void AddSwapCashflow(
      CurveTenor tenor,
      CalibratedCurve curve,
      DiscountCurve discountCurve,
      CashflowCalibrator calibrator)
    {
      var pricer = (SwapPricer)GetPricer(curve, tenor.Product);
      if (discountCurve != curve)
      {
        if (pricer.PayerSwapPricer.ReferenceCurve != curve &&
          pricer.ReceiverSwapPricer.ReferenceCurve == curve)
        {
          calibrator.Add(-pricer.PayerSwapPricer.Pv() /
            pricer.PayerSwapPricer.DiscountCurve.Interpolate(
              pricer.PayerSwapPricer.AsOf, pricer.PayerSwapPricer.Settle),
            pricer.ReceiverSwapPricer.GetPaymentSchedule(null, curve.AsOf),
            pricer.ReceiverSwapPricer.Settle,
            discountCurve, tenor.CurveDate, tenor.Weight, true,
            NeedsParallel(CurveFitSettings,
              pricer.ReceiverSwapPricer.SwapLeg.ProjectionType));
          return;
        }

        if (pricer.ReceiverSwapPricer.ReferenceCurve != curve &&
          pricer.PayerSwapPricer.ReferenceCurve == curve)
        {
          calibrator.Add(pricer.ReceiverSwapPricer.Pv() /
            pricer.ReceiverSwapPricer.DiscountCurve.Interpolate(
              pricer.ReceiverSwapPricer.AsOf, pricer.ReceiverSwapPricer.Settle),
            pricer.PayerSwapPricer.GetPaymentSchedule(null, curve.AsOf),
            pricer.PayerSwapPricer.Settle,
            discountCurve, tenor.CurveDate, tenor.Weight, true,
            NeedsParallel(CurveFitSettings,
              pricer.PayerSwapPricer.SwapLeg.ProjectionType));
          return;
        }
      }
      // The default case
      calibrator.Add(0.0,
        pricer.ReceiverSwapPricer.GetPaymentSchedule(null, curve.AsOf),
        pricer.PayerSwapPricer.GetPaymentSchedule(null, curve.AsOf),
        pricer.Settle, discountCurve, tenor.CurveDate, tenor.Weight, true,
        NeedsParallel(CurveFitSettings,
          pricer.ReceiverSwapPricer.SwapLeg.ProjectionType),
        NeedsParallel(CurveFitSettings,
          pricer.PayerSwapPricer.SwapLeg.ProjectionType));
    }

    private static bool AreEqual(ReferenceIndex referenceIndex, 
      ReferenceIndex otherIndex)
    {
      if (referenceIndex == null || otherIndex == null)
        return false;
      if (referenceIndex == otherIndex ||
        (referenceIndex.IndexTenor == otherIndex.IndexTenor &&
          referenceIndex.IndexName == otherIndex.IndexName))
      {
        return true;
      }
      return false;
    }

    private static bool NeedsParallel(CurveFitSettings settings, ProjectionType type)
    {
      return !settings.ApproximateRateProjection &&
        ((type == ProjectionType.ArithmeticAverageRate) ||
          (type == ProjectionType.GeometricAverageRate));
    }

    private static CalibratedCurve GetProjectionCurve(CalibratedCurve curve,
      MultiRateCurveSet curveSet, ReferenceIndex projectionIndex)
    {
      if (projectionIndex == null)
        return null;
      if (AreEqual(curve.ReferenceIndex, projectionIndex))
        return curve;
      if (AreEqual(curveSet.DiscountCurve?.ReferenceIndex, projectionIndex))
        return curveSet.DiscountCurve;
      var projectionCurves = curveSet.ProjectionCurves;
      if (projectionCurves == null)
        throw new NullReferenceException(
          "Cannot select projection curve for projection index from null ProjectionCurves");
      if (projectionCurves.Count == 1)
        return projectionCurves.First(); //should we do this or let error occur?
      var retVal =
        projectionCurves.Where(c => AreEqual(c.ReferenceIndex, projectionIndex)).ToArray();
      if (retVal.Length == 0)
        throw new ArgumentException(
          String.Format("Cannot find projection curve corresponding to index {0}",
            projectionIndex.IndexName));
      if (retVal.Length > 1)
        throw new ArgumentException(
          String.Format(
            "Two or more curves corresponding to index {0} were found among given projection curves.",
            projectionIndex.IndexName));
      return retVal[0];
    }

    private IList<ReferenceIndex> GetKnownIndices(
      ReferenceIndex targetIndex1, ReferenceIndex targetIndex2)
    {
      var knowns = _prerequisites;
      if (knowns == null || knowns.Count == 0)
        return null;
      var list = new List<ReferenceIndex>();
      foreach (var curve in knowns)
      {
        var index = GetCurveIndex(curve);
        if (index != null && !index.IsEqual(targetIndex1) 
          || (targetIndex2 != null && !index.IsEqual(targetIndex2))
          && !list.Any(i => i.IsEqual(index)))
        {
          list.Add(index);
        }
      }
      return list;
    }

    private static ReferenceIndex GetCurveIndex(CalibratedCurve curve)
    {
      return curve == null ? null : curve.ReferenceIndex;
    }

    private IList<CalibratedCurve> MergeProjectionCurves(CalibratedCurve target)
    {
      var list = new List<CalibratedCurve> {target};
      if (ProjectionCurves == null) return list;
      foreach (var projectionCurve in ProjectionCurves)
      {
        if (projectionCurve != null && !list.Contains(projectionCurve))
          list.Add(projectionCurve);
      }
      return list;
    }

    #endregion Calibration

    #endregion

    #region Properties

    /// <summary>
    ///   the Discount Curve
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return MultiRateCurveSet.DiscountCurve; }
    }

    /// <summary>
    ///   The projection curve
    /// </summary>
    public IList<CalibratedCurve> ProjectionCurves
    {
      get { return MultiRateCurveSet.ProjectionCurves; }
    }

    /// <summary>
    ///   The target index
    /// </summary>
    public ReferenceIndex ReferenceIndex { get; private set; }

    /// <summary>
    ///   The target index
    /// </summary>
    public ReferenceIndex ReferenceIndexLong { get; private set; }

    /// <summary>
    ///   Gets the fitting error code.
    /// </summary>
    /// <value>The fitting error code.</value>
    public OptimizerStatus FittingErrorCode { get; private set; }

    /// <summary>
    /// Settings for cashflow calibrator
    /// </summary>
    public CashflowFitSettings CashflowCalibratorSettings { get; private set; }

    /// <summary>
    ///   Gets or sets the overlay curve.
    /// </summary>
    /// <value>The overlay curve.</value>
    public Curve OverlayCurve { get; set; }

    /// <summary>
    ///   Gets or sets the indicator that the overlay should apply after calibration,
    ///   i.e., on the top of the calibrated curve.
    /// </summary>
    /// <value>The overlay curve.</value>
    public bool OverlayAfterCalibration { get; set; }

    internal MultiRateCurveSet MultiRateCurveSet { get; private set; }

    private readonly List<CalibratedCurve> _prerequisites;

    #endregion Properties
  } // class MultiRateCurveFitCalibrator

  #endregion MultiRateCurveFitCalibrator

  #region MultiRateCurveSet

  /// <summary>
  ///   A collection of all the rate curves related to the same discount curve
  /// </summary>
  internal class MultiRateCurveSet
  {
    /// <summary>
    ///   The Discount Curve
    /// </summary>
    internal DiscountCurve DiscountCurve { get; set; }

    /// <summary>
    ///   The projection curve
    /// </summary>
    internal IList<CalibratedCurve> ProjectionCurves { get; private set; }

    internal bool Contains(CalibratedCurve curve)
    {
      return curve == DiscountCurve ||
        (ProjectionCurves != null && ProjectionCurves.Contains(curve));
    }

    internal void AddProjectionCurve(CalibratedCurve curve)
    {
      var curves = ProjectionCurves;
      if (curves == null)
        curves = ProjectionCurves = new List<CalibratedCurve>();
      if (!curves.Contains(curve)) curves.Add(curve);
    }
  }

  #endregion

  #region Interface IRateCurveCalibrator

  /// <summary>
  ///  Common members implemented by interest rate curve calibrators
  /// </summary>
  /// <exclude>For internal use only.</exclude>
  public interface IRateCurveCalibrator
  {
    /// <summary>
    ///   The discount curve used to calibrate projection curves.
    /// </summary>
    DiscountCurve DiscountCurve { get; }

    /// <summary>
    ///   The target index
    /// </summary>
    ReferenceIndex ReferenceIndex { get; }
  }

  #endregion
}
