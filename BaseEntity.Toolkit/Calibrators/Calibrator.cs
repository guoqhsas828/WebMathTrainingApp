/*
 * Calibrator.cs
 *
 *  -2011. All rights reserved.
 *
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using log4net;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.TenorQuoteHandlers;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Calibrators
{
  /// <summary>
  /// Abstract base class for all calibrators.
  /// </summary>
  /// <remarks>
  /// Calibrators are used to calibrate a model or curve to market data.
  /// </remarks>
  [Serializable]
  public abstract class Calibrator : BaseEntityObject
  {
    // Logger
    private static readonly ILog logger = LogManager.GetLogger(typeof(Calibrator));

    #region Data

    private Dt asOf_;
    private Dt settle_;
    [Mutable]
    private double calibrationTime_; //in seconds
    #endregion Data

    #region Constructors

    ///<summary>
    ///  Constructor
    ///</summary>
    ///<remarks>
    ///  The settlement date defaults to the as-of date.
    ///</remarks>
    ///<param name = "asOf">As-of (pricing) date</param>
    protected Calibrator(Dt asOf)
    {
      AsOf = asOf;
      Settle = asOf;
      ParentCurves = new List<long>();
    }

    ///<summary>
    ///  Constructor
    ///</summary>
    ///<param name = "asOf">As-of (pricing) date</param>
    ///<param name = "settle">Settlement date</param>
    protected Calibrator(Dt asOf, Dt settle)
    {
      AsOf = asOf;
      Settle = settle;
      ParentCurves = new List<long>();
    }

    /// <summary>
    ///   Clone object
    /// </summary>
    public override object Clone()
    {
      Calibrator obj = (Calibrator)base.Clone();
      return obj;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Fit (calibrate) a curve to the specified market data.
    /// </summary>
    /// <remarks>
    ///   <para>Fits whole curve from scratch. After the curve has
    ///   been fitted, Refit may be called with care to improve
    ///   performance.</para>
    ///   <para>Clear the curves and calls FitFrom(curve, 0).</para>
    /// </remarks>
    /// <param name="curve">Curve to calibrate</param>
    public void Fit(CalibratedCurve curve)
    {
      // Start timer
      var stopwatch = new Stopwatch();
      stopwatch.Start();

      //logger.DebugFormat("Starting fit for {0}", curve.Name);

      //Note: We allow fitting parameter curves which may not have tenor on its own.
      //      So this check should be disabled.
      //if (curve.Tenors.Count > 0)
      //{
        Dt jumpDate = curve.JumpDate;

        // Clear curve to fit.
        curve.Clear();

        // Perform fit
        curve.Tenors.UpdateProducts(AsOf);
        PerformFitAction(curve, 0);

        // Set default date property
        // we do it after refit in case we use conditional probability before default,
        // i.e., the case when curve.Deterministic is false.
        if (jumpDate.IsValid())
        {
          var sc = curve as SurvivalCurve;
          if (sc == null) curve.JumpDate = jumpDate;
          else sc.SetDefaulted(jumpDate, sc.Deterministic);
        }
     // }
      //else if (curve.JumpDate.IsEmpty())
      //{
      //  // Error: no tenor and no jump date
      //  throw new ArgumentException("Must set up tenors before fit attempted");
      //}

      // Stop timer
      stopwatch.Stop();
      TimeSpan diff = stopwatch.Elapsed;
      calibrationTime_ = diff.TotalSeconds;
      logger.DebugFormat("Completed fit for {0} in {1} seconds", curve.Name, diff.TotalSeconds);

      return;
    }

    ///<summary>
    ///  Refit individual curve from the specified tenor point
    ///</summary>
    ///<remarks>
    ///  <para>Care should be taken when using this method as it assumes
    ///  that the calibration has previously been run and may take
    ///  liberties and assumptions of pre-calculated values for speed.</para>
    ///  <para>Assumes nothing in the calibration has changed. If things
    ///  like settlement date, etc. have changed, Fit() should be
    ///  called.</para>
    ///  <para>Does some validation and housekeeping then calls FitFrom(curve, fromIdx).</para>
    ///</remarks>
    ///<param name = "curve">Curve to calibrate</param>
    ///<param name = "fromIdx">Index to start fit from</param>
    public void ReFit(CalibratedCurve curve, int fromIdx)
    {
      // Start timer
      var stopwatch = new Stopwatch();
      stopwatch.Start();

      //logger.DebugFormat( "Starting refit for {0} from {1}", curve.Name, fromIdx );

      //Note: We allow fitting parameter curves which may not have tenor on its own.
      //      So this check should be disabled.
      //if (curve.Tenors.Count > 0)
     // {
        Dt jumpDate = curve.JumpDate;

        // Perform fit
        curve.Tenors.UpdateProducts(AsOf);
        PerformFitAction(curve, 0);

        // Set default date property
        // we do it after refit in case we use conditional propability before default,
        // i.e., the case when curve.Deterministic is false.
        if (jumpDate.IsValid())
        {
          var sc = curve as SurvivalCurve;
          if (sc == null) curve.JumpDate = jumpDate;
          else sc.SetDefaulted(jumpDate, sc.Deterministic);
        }
      //}
      //else if (curve.JumpDate.IsEmpty())
      //{
      //  // Error: no tenor and no jump date
      //  throw new ToolkitException("Must set up tenors before fit attempted");
      //}

      // Stop timer
      stopwatch.Stop();
      TimeSpan diff = stopwatch.Elapsed;
      calibrationTime_ = diff.TotalSeconds;
      if (logger.IsDebugEnabled)
        logger.DebugFormat("Completed refit for {0} with {1} points in {2} seconds", curve.Name, RefitPointCount(curve),
                           diff.TotalSeconds);
    }

    private void PerformFitAction(CalibratedCurve curve, int fromIdx)
    {
      var action = CurveFitAction;
      var state = action != null ? action.PreProcess(curve) : null;
      FitFrom(curve, fromIdx);
      if (action!= null) action.PostProcess(state, curve);
    }

    internal static int RefitPointCount(CalibratedCurve curve)
    {
      return curve.ShiftOverlay != null ? curve.ShiftOverlay.Count : curve.Count;
    }

    ///<summary>
    ///  Fit a curve from the specified tenor point
    ///</summary>
    ///<remarks>
    ///  <para>Derived calibrated curves implement this to do the work of the
    ///  fitting</para>
    ///  <para>Called by Fit() and Refit(). Child calibrators can assume
    ///  that the tenors have been validated and the data curve has
    ///  been cleared for a full refit (fromIdx = 0).</para>
    ///</remarks>
    ///<param name = "curve">Curve to calibrate</param>
    ///<param name = "fromIdx">Index to start fit from</param>
    protected abstract void FitFrom(CalibratedCurve curve, int fromIdx);

    ///<summary>
    ///  Construct a pricer matching the model(s) used for calibration.
    ///</summary>
    ///<param name = "curve">Curve to calibrate</param>
    ///<param name = "product">Product to price</param>
    ///<returns>Constructed pricer for product</returns>
    public virtual IPricer GetPricer(CalibratedCurve curve, IProduct product)
    {
      throw new ToolkitException(String.Format(
        "Pricer not implemented for {0}", GetType().Name));
    }

    ///<summary>
    ///  Construct a pricer matching the model(s) used for calibration.
    ///</summary>
    ///<param name="curve">Curve to calibrate</param>
    ///<param name="tenor">Tenor to work with</param>
    ///<returns>Constructed pricer for product</returns>
    public IPricer GetPricer(CalibratedCurve curve, CurveTenor tenor)
    {
      return GetPricer(curve, GetProduct(tenor));
    }

    /// <summary>
    ///   Get tenor instrument as a product 
    /// </summary>
    /// <param name="tenor">The tenor</param>
    /// <returns>The product</returns>
    public IProduct GetProduct(CurveTenor tenor)
    {
      if (tenor == null) return null;
      tenor.UpdateProduct(AsOf);
      return tenor.Product;
    }

    /// <summary>
    ///   Convert to string
    /// </summary>
    public override string ToString()
    {
      return "Type = " + GetType().Name + "; " + "AsOf = " + asOf_ + "; " + "Settle = " + settle_ + "; " +
             "CalibrationTime = " + calibrationTime_;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   As-of (pricing) date
    /// </summary>
    public Dt AsOf
    {
      get { return asOf_; }
      set { asOf_ = value; }
    }

    /// <summary>
    ///   Settlement date
    /// </summary>
    public Dt Settle
    {
      get { return settle_; }
      set { settle_ = value; }
    }

    /// <summary>
    ///   calibration time
    /// </summary>
    public double CalibrationTime
    {
      get { return calibrationTime_; }
    }

    /// <summary>
    /// Configuration settings
    /// </summary>
    protected ToolkitConfigSettings Settings => ToolkitConfigurator.Settings;

    /// <summary>
    /// Parent curve list
    /// </summary>
    internal List<long> ParentCurves { get; set; }


    /// <exclude> </exclude> 
    internal protected static void SetParentCurves(List<long> parentSet, params CalibratedCurve[] parents)
    {
      if (parents == null || parents.Length == 0)
        return;
      for (int i = 0; i < parents.Length; ++i)
      {
        var pi = parents[i];
        if (pi == null)
          continue;
        bool add = true;
        for (int j = 0; j < parents.Length; ++j)
        {
          if (i == j)
            continue;
          var pj = parents[j];
          if (pj == null || pj.Calibrator == null)
            continue;
          if (pj.Calibrator.ParentCurves.Contains(pi.Id))
          {
            add = false;
            break;
          }
        }
        if (add && !parentSet.Contains(pi.Id))
          parentSet.Add(pi.Id);
      }
    }

    /// <exclude> </exclude> 
    internal protected static void SetDependentCurves(CalibratedCurve curve, params CalibratedCurve[] parents)
    {
      if (curve.ShiftOverlay != null) return;
      if (curve.Calibrator == null || parents == null || parents.Length == 0)
        return;
      foreach (var parent in parents)
      {
        if (parent == null)
          continue;
        if(parent.Id == 0)
        {
          throw new ToolkitException("Null native curve used as parents");
        }
        if (curve.Calibrator.ParentCurves.Contains(parent.Id))
        {
          parent.DependentCurves[curve.Id] = curve;
        }
      }
    }

    /// <summary>
    /// Parent curves
    /// </summary>
    /// <returns></returns>
    public virtual IEnumerable<CalibratedCurve> EnumerateParentCurves()
    {
      yield break;
    }

    internal ICurveFitAction CurveFitAction { get; set; }
    #endregion Properties
  }
}