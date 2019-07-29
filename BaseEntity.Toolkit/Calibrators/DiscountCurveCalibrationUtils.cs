/*
 * DiscountCurveCalibrationUtils.cs
 *
 *  -2011. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using log4net;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Cashflows;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  /// Utilities for discount curve calibration
  /// </summary>
  internal static class DiscountCurveCalibrationUtils
  {
    #region General Calibration Utilities

    /// <summary>
    ///   Utility function that checks array sizes
    /// </summary>
    /// <typeparam name = "T">Type</typeparam>
    /// <param name = "count">Array size</param>
    /// <param name = "inputs">Input array</param>
    /// <param name = "dfltValue">Default value</param>
    /// <param name = "targetName">Target name</param>
    /// <param name = "inputName">Input name</param>
    /// <returns>An array of elements of type T</returns>
    internal static T[] CheckArray<T>(int count, T[] inputs, T dfltValue, string targetName, string inputName)
    {
      if (inputs == null || inputs.Length == 0) return ArrayUtil.NewArray(count, dfltValue);
      if (inputs.Length == 1) return ArrayUtil.NewArray(count, inputs[0]);
      if (inputs.Length != count)
      {
        throw new ArgumentException(String.Format("The numbers of {0} ({1}) and {2} ({3}) not match.", targetName, count,
                                                  inputName, inputName.Length));
      }
      return inputs;
    }

    /// <summary>
    ///   Check the size of an array containing a condition for a sub group of product types
    /// </summary>
    /// <typeparam name = "T">Type</typeparam>
    /// <param name = "instrumentTypes">Array of instrument types</param>
    /// <param name = "targetType">Target type on which condition is set</param>
    /// <param name = "dfltValue">Default value</param>
    /// <param name = "input"></param>
    /// <returns></returns>
    internal static T[] CheckArrayByInstrumentType<T>(InstrumentType[] instrumentTypes, InstrumentType targetType,
                                                      T dfltValue, T[] input)
    {
      InstrumentType[] types = Array.FindAll(instrumentTypes, type => (type == targetType));
      if (input == null) return ArrayUtil.NewArray(types.Length, dfltValue);
      if (input.Length == 1) return ArrayUtil.NewArray(types.Length, input[0]);
      if (input.Length != types.Length)
        throw new ArgumentException("The number of products of the selected type and the input array do not match");
      return input;
    }


    /// <summary>
    /// Check the size of a 2D array containing a condition for a sub group of product types
    /// </summary>
    /// <typeparam name="T">Type</typeparam>
    /// <param name="n">Number of rows</param>
    /// <param name="m">Number of columns</param>
    /// <param name="inputs">Inputs</param>
    /// <param name="dfltValue">Default value</param>
    /// <param name="targetName">Target name</param>
    /// <param name="inputName">Input name</param>
    /// <returns></returns>
    internal static T[,] CheckArray<T>(int n, int m, T[,] inputs, T dfltValue, string targetName, string inputName)
    {
      if (inputs == null || inputs.GetLength(0) == 0) return ArrayUtil.CreateMatrixFromSingleValue(dfltValue, n, m);
      if (inputs.GetLength(0) == 1 && inputs.GetLength(1) == 1)
      {
        return ArrayUtil.CreateMatrixFromSingleValue(inputs[0, 0], n, m);
      }
      if (inputs.GetLength(0) != n)
      {
        throw new ArgumentException(String.Format("The numbers of {0} ({1}) and {2} ({3}) not match.", targetName, n,
                                                  inputName, inputs.GetLength(0)));
      }
      return inputs;
    }

    /// <summary>
    /// Initialize DiscountCurve
    /// </summary>
    /// <param name="calibrator">Calibrator</param>
    /// <param name="curveFitSettings">CurveFitSettings</param>
    /// <param name="referenceIndex">CurveFitSettings</param>
    /// <param name="category">Category</param>
    /// <param name="name">Curve name</param>
    /// <returns>DiscountCurve</returns>
    internal static DiscountCurve CreateTargetDiscountCurve(Calibrator calibrator, CalibratorSettings curveFitSettings, ReferenceIndex referenceIndex, string category, string name)
    {
      DiscountCurve curve;
      if (curveFitSettings.Method == CashflowCalibrator.CurveFittingMethod.NelsonSiegel)
        curve = new DiscountCurve(calibrator, new NelsonSiegelFn(null), curveFitSettings.OverlayCurve);
      else if (curveFitSettings.Method == CashflowCalibrator.CurveFittingMethod.Svensson)
        curve = new DiscountCurve(calibrator, new SvenssonFn(null), curveFitSettings.OverlayCurve);
      else
        curve = new DiscountCurve(calibrator, curveFitSettings.OverlayCurve)
                {
                  Interp = curveFitSettings.GetInterp(),
                  Ccy = referenceIndex.Currency,
                  Category = category ?? "None",
                  SpotDays = curveFitSettings.CurveSpotDays,
                  SpotCalendar = curveFitSettings.CurveSpotCalendar,
                  Name = string.IsNullOrEmpty(name) ? referenceIndex.IndexName + "_Curve" : name
                };
      return curve;
    }

    #endregion

    #region Product Utilities

    /// <summary>
    ///   Gets maturity of the security without rolling the date
    /// </summary>
    /// <param name = "type">Instrument type</param>
    /// <param name = "settle">Settle date</param>
    /// <param name = "tenor">Product description</param>
    /// <param name = "calendar">Calendar to roll in case of N-days tenor</param>
    /// <param name="roll">Roll convention</param>
    /// <returns>Maturity date</returns>
    internal static Dt GetMaturity(InstrumentType type, Dt settle, string tenor, Calendar calendar, BDConvention roll)
    {
      Dt specifiedMaturity;
      if (String.IsNullOrEmpty(tenor)) throw new ArgumentException("Tenor name is empty.");
      if (type == InstrumentType.FUT) return Dt.ImmDate(settle, tenor);
      if (type == InstrumentType.FRA)
      {
        Tenor settleTenor;
        Tenor maturityTenor;
        if (Tenor.TryParseComposite(tenor, out settleTenor, out maturityTenor))
          return Dt.Add(settle, settleTenor); //this will get rolled in the process of adding product onto curve
        if (Dt.TryFromStrComposite(tenor, "%d-%b-%Y", out settle, out specifiedMaturity))
          return settle; 
        throw new ArgumentException("Tenor is required to be in A * B composite format for FRA type instrument");
      }
      try
      {
        Tenor realTenor;
        if (tenor == "O/N" || tenor == "T/N") return Dt.AddDays(settle, 1, calendar);
        if (Tenor.TryParse(tenor, out realTenor))
        {
          if (realTenor.Units == TimeUnit.Days) return Dt.AddDays(settle, realTenor.N, calendar);

          if (type == InstrumentType.MM || type == InstrumentType.FUNDMM)
          {
            return Dt.Roll(Dt.Add(settle, tenor), roll, calendar);
          }

          return Dt.Add(settle, realTenor);
        }
        if (CurveUtil.TryGetFixedMaturityFromString(tenor, out specifiedMaturity)) return specifiedMaturity;
      }
      catch
      {
        throw new ToolkitException("Invalid Tenor");
      }
      return Dt.Add(settle, tenor);
    }

    internal static Dt GetSettlement(InstrumentType type, Dt asOf, int spotDays, Calendar calendar)
    {
      if (type == InstrumentType.FUNDMM)
      {
        return asOf;
      }
      return Dt.AddDays(asOf, spotDays, calendar);
    }
    
    /// <summary>
    ///   Sets curve dates for calibration
    /// </summary>
    /// <param name = "tenor">Tenors if different from curve tenors</param>
    internal static void SetCurveDate(CurveTenor tenor)
    {
      var swap = tenor.Product as Swap;
      if (swap != null)
      {
        Dt date1 = GetCurveDate(swap.PayerLeg);
        Dt date2 = GetCurveDate(swap.ReceiverLeg);
        tenor.CurveDate = date1 > date2 ? date1 : date2;
        return;
      }
      var fut = tenor.Product as StirFuture;
      if (fut != null)
      {
        tenor.CurveDate = fut.DepositMaturity;
        return;
      }
      var fra = tenor.Product as FRA;
      if (fra != null)
      {
        tenor.CurveDate = fra.ContractMaturity;
        return;
      }
      tenor.CurveDate = tenor.Product.Maturity;
    }

    /// <summary>
    ///   Sets curve dates for calibration
    /// </summary>
    /// <param name = "tenors">Tenors if different from curve tenors</param>
    internal static void SetCurveDates(IEnumerable<CurveTenor> tenors)
    {
      foreach (CurveTenor ten in tenors)
      {
        SetCurveDate(ten);
      }
    }


    /// <summary>
    /// Gets the curve date.
    /// </summary>
    /// <remarks>
    ///  For safety, the curve date is the latest of the following four dates:
    ///   (a) the product maturity date;
    ///   (b) the last payment date;
    ///   (c) the end of the last coupon period;
    ///   (d) the end of the last reset period, if applicable.
    /// </remarks>
    /// <param name="swap">The swap leg.</param>
    /// <returns>The curve date.</returns>
    private static Dt GetCurveDate(SwapLeg swap)
    {
      Dt curveDate = swap.Maturity;
      var sched = swap.Schedule;
      int last = sched.Count - 1;
      if (last >= 0)
      {
        Dt date = sched.GetPeriodEnd(last);
        if (date > curveDate) curveDate = date;
        date = sched.GetPaymentDate(last);
        if (date > curveDate) curveDate = date;
        if (!swap.Floating || swap.ReferenceIndex == null || 
          (last==0 && swap.Calendar == swap.ReferenceIndex.Calendar && swap.BDConvention == swap.ReferenceIndex.Roll))
          return curveDate;

        if (last ==0)
        {
          date = Dt.Roll(curveDate, swap.ReferenceIndex.Roll, swap.ReferenceIndex.Calendar);
          return date > curveDate ? date : curveDate;
        }
        if (swap.ReferenceIndex != null)
        {
          var projectionParams = new ProjectionParams
                                   {
                                     ProjectionType = swap.ProjectionType,
                                     CompoundingConvention = swap.CompoundingConvention,
                                     CompoundingFrequency = swap.CompoundingFrequency,
                                     ResetLag = swap.ResetLag
                                   };
          var projector = CouponCalculator.Get(swap.Effective, swap.ReferenceIndex, null, null, projectionParams);

          if (projector is ArithmeticAvgRateCalculator)
            ((ArithmeticAvgRateCalculator) projector).Approximate = true;
          else if (projector is GeometricAvgRateCalculator)
            ((GeometricAvgRateCalculator) projector).Approximate = true;
          var fixingSchedule =
            projector.GetFixingSchedule(
              (last > 0) ? sched.GetPaymentDate(last - 1) : sched.GetPeriodStart(last), sched.GetPeriodStart(last),
              sched.GetPeriodEnd(last), sched.GetPaymentDate(last));
          date = fixingSchedule.FixingEndDate;
          if (date > curveDate)
            curveDate = date;
        }
      }
      return curveDate;
    }
    #endregion

    #region Curve Indirection

    /// <summary>
    /// Creates the linked pairs of curves.
    /// </summary>
    /// <param name="dstCurves">The destination curves.</param>
    /// <param name="srcCurves">The source curves.</param>
    /// <remarks>
    ///   For each destination curve, this function replaces its calibrator
    ///   with a new IndirectionCalibrator such that in refitting the destination
    ///   curve simple gets the curve points from the corresponding source curve.
    /// </remarks>
    internal static void CreateIndiretions(this CalibratedCurve[] dstCurves,
      CalibratedCurve[] srcCurves)
    {
      if (dstCurves == null || srcCurves == null ||
        dstCurves.Length != srcCurves.Length)
      {
        throw new ToolkitException(
          "Length of source and destination curves not match.");
      }
      for (int i = 0; i < dstCurves.Length; ++i)
        dstCurves[i].Calibrator = new IndirectionCalibrator(
          srcCurves[i], dstCurves[i].Calibrator);
    }

    /// <summary>
    /// Gets the dependent curves, including all the nested dependents.
    /// </summary>
    /// <param name="curves">The curves.</param>
    internal static CalibratedCurve[] GetDependentCurves(this CalibratedCurve[] curves)
    {
      if (curves == null) return null;
      var list = new List<CalibratedCurve>();
      for (int i = 0; i < curves.Length; ++i)
        curves[i].GetDependentCurves(list, curves);
      return list.Count == 0 ? null : list.ToArray();
    }
    private static void GetDependentCurves(this CalibratedCurve curve,
      IList<CalibratedCurve> list, CalibratedCurve[] parents)
    {
      if (curve.DependentCurves == null || curve.DependentCurves.Count == 0)
        return;
      foreach (var c in curve.DependentCurves.Values)
      {
        if (list.Contains(c) || parents.Contains(c)) continue;
        list.Add(c);
        c.GetDependentCurves(list, parents);
      }
    }
    #endregion
  }

  /// <summary>
  ///  A calibrator to set curve points of one curve from another curve.
  /// </summary>
  class IndirectionCalibrator : Calibrator, IInverseCurveProvider
  {
    private readonly CalibratedCurve sourceCurve_;
    private readonly Calibrator calibrator_;
    internal IndirectionCalibrator(CalibratedCurve sourceCurve, Calibrator calibrator)
      : base(sourceCurve.AsOf)
    {
      sourceCurve_ = sourceCurve;
      calibrator_ = calibrator;
    }
    protected override void FitFrom(CalibratedCurve curve, int fromIdx)
    {
      if (sourceCurve_ == null || curve == null || curve == sourceCurve_)
        return;
      curve.Set(sourceCurve_);
    }
    public override IPricer GetPricer(CalibratedCurve curve, IProduct product)
    {
      if (calibrator_ != null)
        return calibrator_.GetPricer(curve, product);
      return base.GetPricer(curve, product);
    }
    DiscountCurve IInverseCurveProvider.InverseCurve
    {
      get
      {
        var fx = sourceCurve_.Calibrator as FxBasisFitCalibrator;
        if (fx == null || fx.InverseFxBasisCurve == null) return null;
        return fx.InverseFxBasisCurve;
      }
    }
  }

  #region Dependency graphs

  /// <summary>
  ///   A class to refresh, save and restore curve dependency graph.
  /// </summary>
  internal class CurveDependencyGraph : IDisposable
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(CurveDependencyGraph));

    private class CurveInfo
    {
      // These are data currently used by calibrated curves
      // to store dependency infomations.
      public Dictionary<long,CalibratedCurve> DependentCurves;
      public List<long> ParentCurves;
      public string CurveName;
      public bool InsideRecursion;
    }

    // To store the original dependency infomations.
    private Dictionary<CalibratedCurve, CurveInfo> saved_ =
      new Dictionary<CalibratedCurve, CurveInfo>();

    // In the dispose, we restore the original dependence infos.
    // This might not be neccessary.
    void IDisposable.Dispose()
    {
      if (saved_ == null) return;
      foreach (var p in saved_)
      {
        var curve = p.Key;
        var info = p.Value;
        if (!String.IsNullOrEmpty(info.CurveName))
        {
          curve.Name = info.CurveName;
        }
        curve.DependentCurves = info.DependentCurves;
        if (curve.Calibrator != null)
        {
          curve.Calibrator.ParentCurves = info.ParentCurves;
        }
      }
      saved_ = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CurveDependencyGraph"/> class.
    /// </summary>
    /// <param name="curves">The curves affecting the pricers.</param>
    public CurveDependencyGraph(IEnumerable<CalibratedCurve> curves)
    {
      if (curves != null)
      {
        foreach (var curve in curves)
          AddCurve(curve);
      }
    }

    // Recursively add curve to the dependency graph.
    private void AddCurve(CalibratedCurve curve)
    {
      CurveInfo info;

      if (curve == null) return;
      if (saved_.TryGetValue(curve, out info))
      {
        if (logger.IsDebugEnabled)
        {
          logger.DebugFormat("Checking: {0}/{1} {2}",
            curve.Name, curve.Id, curve.GetType());
        }
        if (info.InsideRecursion)
        {
          logger.DebugFormat("Circular parent: {0}/{1} {2}",
            curve.Name, curve.Id, curve.GetType());
        }
        return;
      }

      // Upon the first touch, we save the original dependency info
      // and then clear the data on the curve for a fresh new start.
      info = new CurveInfo
      {
        DependentCurves = curve.DependentCurves,
        CurveName = curve.Name,
        InsideRecursion = true
      };
      if (String.IsNullOrEmpty(info.CurveName))
      {
        // If the curve have no name, we give it a unique one based on Curve Id.
        curve.Name = String.Format("[{0}/{1}]", curve.GetType(), curve.Id);
      }
      if (logger.IsDebugEnabled)
      {
        logger.DebugFormat("Adding: {0}/{1} {2}",
          curve.Name, curve.Id, curve.GetType());
      }
      // Mark this curve as being under recursion of its parents.
      saved_.Add(curve, info);

      curve.DependentCurves = new Dictionary<long,CalibratedCurve>();
      if (curve.Calibrator != null)
      {
        // We save the original parent curve names before clear it.
        info.ParentCurves = curve.Calibrator.ParentCurves;
        curve.Calibrator.ParentCurves = new List<long>();

        // We do depth first walk to build the dependence tree.
        var parents = GetParents(curve);
        if (parents != null && parents.Length > 0)
        {
          if (logger.IsDebugEnabled)
          {
            logger.DebugFormat("Begin parents of {0}/{1}", curve.Name, curve.Id);
          }
          for (int i = 0; i < parents.Length; ++i)
          {
            AddCurve(parents[i]);
          }
          if (logger.IsDebugEnabled)
          {
            logger.DebugFormat("End parents of {0}/{1}", curve.Name, curve.Id);
          }
          //var timer = new Timer();
          //timer.start();
          // This should be done after all the parents have been added.
          Calibrator.SetParentCurves(curve.Calibrator.ParentCurves, parents);
          Calibrator.SetDependentCurves(curve, parents);
          //timer.stop();
          //logger.ErrorFormat("{0} SetDependents {1}/{2} {3}",
          //  timer.getElapsed(), curve.Name, curve.Id, curve.GetType());
        }
      }

      // Mark as done.
      info.InsideRecursion = false;
    }

    private static CalibratedCurve[] GetParents(CalibratedCurve curve)
    {
      if (curve == null || curve.Calibrator == null) return null;
      var parents = curve.Calibrator.EnumerateParentCurves();
      if (parents == null) return null;
      var a = parents as CalibratedCurve[];
      if (a != null) return a;
      return parents.ToArray();
    }
  }
  #endregion
}