// 
//  -2013. All rights reserved.
// 

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BGM;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Sensitivity
{
  #region Config

  /// <exclude />
  [Serializable]
  public class ThetaSensitivityConfig
  {
    /// <exclude />
    [ToolkitConfig("Whether to roll the credit-default-swap maturities when re-calibrating the survival curves in theta sensitivity on the roll date")]
    public readonly bool RecalibrateWithRolledCdsMaturity = false;

    /// <exclude />
    [ToolkitConfig("Whether to roll the credit-default-swap effective dates when re-calibrating the survival curves in theta sensitivity on the roll date")]
    public readonly bool RecalibrateWithNewCdsEffective = true;
  }

  #endregion Config

  /// <summary>
  /// General sensitivity calculators
  /// </summary>
  /// <remarks>
  /// <para>Methods for calculating a wide range of standard sensitivity measures. These methods provide a convenient
  /// and powerful method of calculating unified sensitivities across all <see cref="IPricer">Pricers</see>
  /// supported by the Toolkit.</para>
  /// <para>Most sensitivity methods provide a wide range of options re how the sensitivities are calculated and come in two
  /// forms. Summary sensitivity functions are provided for convenience that return a single result for a single
  /// <see cref="IPricer">Pricer</see>. More comprehensive sensitivity functions operate on a portfolio of
  /// <see cref="IPricer">Pricers</see> and return a <see cref="DataSet"/> containing results.</para>
  /// </remarks>
  public static partial class Sensitivities
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(Sensitivities));

    /// <summary>Default date format for result dates</summary>
    public static readonly string DefaultDateFormat = "dd-MMM-yy";

    #region CurveTask

    //   Worker function for Spread and Rate sensitivity functions. Not for public use.
    //   This is for a group of pricers that might have different maturities
    /// <exclude />
    private static object CurveTask(object stateData)
    {
      CurveTaskState taskState = (CurveTaskState)stateData;

      PricerEvaluator[] evaluators = taskState.Evaluators;
      CalibratedCurve[] curves = taskState.Curves;
      CalibratedCurve[] dependentCurves = taskState.DependentCurves;
      double[] initialBumps = taskState.InitialBump;
      double[] upBumps = taskState.UpBump;
      double[] downBumps = taskState.DownBump;
      bool bumpRelative = taskState.BumpRelative;
      bool scaledDelta = taskState.ScaledDelta;
      BumpType bumpType = taskState.BumpType;
      string[] bumpTenors = taskState.BumpTenors;
      bool calcGamma = taskState.CalcGamma;
      bool calcHedge = taskState.CalcHedge;
      string hedgeTenor = taskState.HedgeTenor;
      QuotingConvention targetQuoteType = taskState.TargetQuoteType;

      // hedgeTenor could be:
      //  [0] Blank, which is defaulted to "matching" to bumped tenors
      //  [1] Tenor name (5y, 7Y, ABCDS...)
      //  [2] Tenor date
      //  [3] "maturity" which means maturity of individual product (say CDO)

      // Create a list of tenors to bump if this is not specified and we need it.
      if (bumpType == BumpType.ByTenor && (bumpTenors == null || bumpTenors.Length == 0))
      {
        Dt lastMaturity = LastMaturity(evaluators);
        bumpTenors = CurveUtil.CurveTenors(curves, taskState.EvalAllTenors ? Dt.Empty : lastMaturity);
      }

      // Find the maturity date for each pricer evaluator
      Dt[] hedgeMaturities = null;
      bool[] hedgeMaturitiesOrNot = null;
      if (calcHedge && (String.Compare(hedgeTenor != null ? hedgeTenor.ToLower() : hedgeTenor, "maturity") == 0))
      {
        hedgeMaturities = new Dt[evaluators.Length];
        hedgeMaturitiesOrNot = new bool[evaluators.Length];
        for (int i = 0; i < evaluators.Length; ++i)
        {
          hedgeMaturities[i] = evaluators[i].Product.EffectiveMaturity;
          if (evaluators[i].Product is CDSOption)
          {
            hedgeMaturities[i] = ((CDSOption)evaluators[i].Product).Underlying.Maturity;
          }
          hedgeMaturitiesOrNot[i] = false;
        }
      }

      // Convert the string to Dt if hedgeTenor passed in is an excel date
      Dt hedgeDate = new Dt();
      if (calcHedge && String.Compare(hedgeTenor, "matching") != 0)
      {
        if (String.Compare(hedgeTenor != null ? hedgeTenor.ToLower() : hedgeTenor, "maturity") != 0
            && (hedgeTenor != null ? hedgeTenor.Length : 0) >= 5 && StringIsNum(hedgeTenor))
          hedgeDate = Dt.FromExcelDate(Double.Parse(hedgeTenor));
      }

      // Set buffer for maximum number of results
      int rowCount = evaluators.Length;
      if (bumpType != BumpType.Uniform)
      {
        rowCount *= curves.Length;
        if (bumpType == BumpType.ByTenor)
          rowCount *= bumpTenors.Length;
      }
      object[][] rows = new object[rowCount][];
      int colCount = 9;
      int currentRow = 0;

      // This is a quick hack for the new dependency structure in the rate calibration
      bool withNewDependency = false;
      if (dependentCurves == null)
      {
        dependentCurves = curves.GetDependentCurves();
        withNewDependency = dependentCurves != null;
      }

      // Save a copy of the initial curves to restore later
      logger.Debug("Saving copy of curves before bumping");
      CalibratedCurve[] origCurves = CloneUtil.Clone(curves);
      CalibratedCurve[] origDependentCurves = dependentCurves != null ? CloneUtil.Clone(dependentCurves) : null;

      BumpFlags flags = bumpRelative ? BumpFlags.BumpRelative : 0;
      

      // Perform any initial bump
      if (initialBumps.Any(x => x != 0.0))
      {
        logger.DebugFormat("Performing initial bump {0}", initialBumps);

        // Note we save the fitting for the try block to give additional security re the
        // integrity of the curves.
        curves.BumpQuotes(targetQuoteType, initialBumps, flags);
      }

      // Compute sensitivities
      // Any errors are trapped and the curves restored.
      try
      {
        // Get a copy of the curves to be bumped including any initial bump
        CalibratedCurve[] savedCurves = origCurves;
        CalibratedCurve[] savedDependentCurves = origDependentCurves;
        if (initialBumps.Any(x => x != 0.0))
        {
          logger.Debug("Fitting to initial bump");
          CurveUtil.CurveFit(curves);
          savedCurves = CloneUtil.Clone(curves);
          if (dependentCurves != null)
          {
            CurveUtil.CurveFit(dependentCurves);
            savedDependentCurves = CloneUtil.Clone(dependentCurves);
          }
        }
        // savedCurves now contains a copy of the curve state before bumping
        // origCurves contains a copy of the curve state before sensitivities were started

        double[,] upTable = null, downTable = null;
        double[] upHedge = null, downHedge = null;
        double[][] upHedge2 = null, downHedge2 = null;
        double[] mtm = null;
        double[] avgUpBumps = null, avgDownBumps = null;

        // Create a copy of the curves to bump
        logger.Debug("Copying curves for bumping");
        CalibratedCurve[] bumpedCurves;
        if (withNewDependency)
        {
          var cloned = CloneUtil.CloneObjectGraph(curves, dependentCurves);
          bumpedCurves = cloned.Item1;
          dependentCurves.CreateIndiretions(cloned.Item2);
        }
        else
        {
          bumpedCurves = CloneUtil.CloneObjectGraph(curves);
        }

        // Start bumping
        logger.Debug("Starting bump by tenor");

        if (hedgeMaturities != null && hedgeMaturities.Length > 0)
        {
          upHedge2 = new double[hedgeMaturities.Length][];
          downHedge2 = new double[hedgeMaturities.Length][];
        }
        // Do bumps
        bool calcTenorHedge = false;
        switch (bumpType)
        {
          #region ByTenor

          case BumpType.ByTenor:
            if (calcHedge && String.Compare(hedgeTenor, "maturity") == 0)
              throw new ArgumentException(String.Format("{0} is not allowed for ByTenor hedging", "maturity"));
            for (int k = 0; k < bumpTenors.Length; k++)
            {
              if (bumpedCurves.Length == 1 && (bumpedCurves[0] is FxForwardCurve
                                               || bumpedCurves[0].Calibrator is FxBasisFitCalibrator)
                  && bumpedCurves[0].Tenors.Index(bumpTenors[k]) < 0)
              {
                continue;
              }

              upTable = downTable = null;
              upHedge = downHedge = null;
              avgUpBumps = avgDownBumps = null;

              // Figure out if we need to calculate a hedge for this specific tenor
              // Hedge if calculating matching hedge or if this tenor is the one to hedge.
              calcTenorHedge = (calcHedge && (String.Compare(hedgeTenor, "matching") == 0 ||
                                              String.Compare(hedgeTenor, bumpTenors[k]) == 0));
              if (Dt.Cmp(hedgeDate, Dt.Empty) != 0)
              {
                CurveTenor curveTenor = CurveUtil.FindClosestTenor(curves, hedgeDate);
                if (curveTenor != null)
                  calcTenorHedge = calcTenorHedge || ((String.Compare(curveTenor.Name, bumpTenors[k]) == 0));
                else
                  calcTenorHedge = calcTenorHedge || false;
              }

              if (hedgeMaturities != null && hedgeMaturities.Length > 0)
              {
                for (int l = 0; l < hedgeMaturities.Length; ++l)
                {
                  CurveTenor curveTenor = CurveUtil.FindClosestTenor(curves, hedgeMaturities[l]);
                  if (curveTenor != null)
                  {
                    hedgeMaturitiesOrNot[l] = hedgeMaturitiesOrNot[l] ||
                                              (String.Compare(curveTenor.Name, bumpTenors[k]) == 0);
                    calcTenorHedge = calcTenorHedge || hedgeMaturitiesOrNot[l];
                  }
                  else
                    calcTenorHedge = calcTenorHedge || false;
                }
              }
              // Bump up
              if (upBumps.Any(x => x != 0.0))
              {
                // Bump up curves, calculate, then restore curves
                logger.DebugFormat("Tenor {0} - bumping up", bumpTenors[k]);

                double upBump = upBumps.Count() == 1 ? upBumps[0] : upBumps[k];
                avgUpBumps = bumpedCurves.BumpQuotes(bumpTenors[k], targetQuoteType, upBump,
                    flags | BumpFlags.RefitCurve);
                
                upTable = CalcIndividualBumpedPvs(evaluators, curves, bumpedCurves, savedCurves, dependentCurves, savedDependentCurves, mtm);
                if (calcTenorHedge)
                {
                  if (Dt.Cmp(Dt.Empty, hedgeDate) == 0 && (hedgeMaturities == null || hedgeMaturities.Length == 0))
                    upHedge = CurveUtil.CurveHedge(curves, bumpedCurves, bumpTenors[k]);
                  else if (Dt.Cmp(Dt.Empty, hedgeDate) != 0)
                    upHedge = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeDate);
                  else
                  {
                    for (int l = 0; l < hedgeMaturities.Length; ++l)
                      upHedge2[l] = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeMaturities[l]);
                  }
                }
                CurveUtil.CurveRestoreQuotes(bumpedCurves, savedCurves);

                // Remember base mtm for future bumps
                // Note: we do this here because basket bumping is faster for including base calculation.
                mtm = new double[evaluators.Length];
                for (int j = 0; j < evaluators.Length; j++)
                  mtm[j] = upTable[0, j];
              }

              // Bump down
              if (downBumps.Any(x => x != 0.0))
              {
                // Bump down curves, calculate, then restore curves
                logger.DebugFormat("Tenor {0} - bumping down", bumpTenors[k]);
                double downBump = downBumps.Count() == 1 ? downBumps[0] : downBumps[k];
                avgDownBumps = bumpedCurves.BumpQuotes(bumpTenors[k], targetQuoteType, downBump,
                                                       flags | BumpFlags.BumpDown | BumpFlags.RefitCurve);
                downTable = CalcIndividualBumpedPvs(evaluators, curves, bumpedCurves, savedCurves, dependentCurves, savedDependentCurves, mtm);
                if (calcTenorHedge)
                {
                  if (Dt.Cmp(Dt.Empty, hedgeDate) == 0 && (hedgeMaturities == null || hedgeMaturities.Length == 0))
                    downHedge = CurveUtil.CurveHedge(curves, bumpedCurves, bumpTenors[k]);
                  else if (Dt.Cmp(Dt.Empty, hedgeDate) != 0)
                    downHedge = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeDate);
                  else
                  {
                    for (int l = 0; l < hedgeMaturities.Length; ++l)
                      downHedge2[l] = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeMaturities[l]);
                  }
                }
                CurveUtil.CurveRestoreQuotes(bumpedCurves, savedCurves);
              }

              // Save results
              logger.DebugFormat("Tenor {0} - saving results", bumpTenors[k]);

              for (int i = 0; i < bumpedCurves.Length; i++)
              {
                // Indicating the curves is bumped or not
                var bumpResult = GetBumpResult(i, upTable, downTable, avgUpBumps, avgDownBumps, false);

                for (int j = 0; j < evaluators.Length; j++)
                {
                  if (evaluators[j].DependsOn(curves[i]))
                  {
                    object[] row = new object[colCount];

                    if (bumpResult == BumpResultType.Bumped)
                    {
                      // The curve is bumped by non-zero units....
                      logger.DebugFormat(" Curve {0}, tenor {1}, trade {2}, up = {3}, mid = {4}, down = {5}",
                                         bumpedCurves[i].Name, bumpTenors[k], evaluators[j].Product.Description,
                                         (upTable != null) ? upTable[i + 1, j] : 0.0,
                                         (upTable != null) ? upTable[0, j] : downTable[0, j],
                                         (downTable != null) ? downTable[i + 1, j] : 0.0);

                      row[0] = bumpedCurves[i].Category;
                      row[1] = bumpedCurves[i].Name;
                      row[2] = bumpTenors[k];
                      row[3] = evaluators[j].Product.Description;

                      double delta = CalcDelta(i, j, upTable, downTable, scaledDelta, avgUpBumps, avgDownBumps, false);
                      row[4] = delta;

                      if (calcGamma)
                        row[5] = CalcGamma(i, j, upTable, downTable, scaledDelta, avgUpBumps, avgDownBumps, false);

                      if (calcTenorHedge)
                      {
                        double hedgeDelta;
                        if (hedgeMaturities != null && hedgeMaturities.Length > 0 && hedgeMaturitiesOrNot[j])
                        {
                          hedgeDelta = CalcHedge(i, upHedge2[j], downHedge2[j], scaledDelta, avgUpBumps, avgDownBumps, false);
                          row[6] = (new DateTime(1899, 12, 30).Add(TimeSpan.FromDays(Dt.ToExcelDate(hedgeMaturities[j])))).ToString(DefaultDateFormat);
                        }
                        else if (hedgeMaturities != null && hedgeMaturities.Length > 0 && !hedgeMaturitiesOrNot[j])
                        {
                          hedgeDelta = 0;
                          row[6] = (new DateTime(1899, 12, 30).Add(TimeSpan.FromDays(Dt.ToExcelDate(hedgeMaturities[j])))).ToString(DefaultDateFormat);
                        }
                        else if (String.Compare(hedgeTenor, "matching") == 0)
                        {
                          hedgeDelta = CalcHedge(i, upHedge, downHedge, scaledDelta, avgUpBumps, avgDownBumps, false);
                          row[6] = bumpTenors[k];
                        }
                        else
                        {
                          if (Dt.Cmp(Dt.Empty, hedgeDate) != 0)
                          {
                            hedgeDelta = CalcHedge(i, upHedge, downHedge, scaledDelta, avgUpBumps, avgDownBumps, false);
                            row[6] = (new DateTime(1899, 12, 30).Add(TimeSpan.FromDays(Dt.ToExcelDate(hedgeDate)))).ToString(DefaultDateFormat);
                          }
                          else
                          {
                            hedgeDelta = CalcHedge(i, upHedge, downHedge, scaledDelta, avgUpBumps, avgDownBumps, false);
                            row[6] = bumpTenors[k];
                          }
                        }
                        row[7] = 1000000 * hedgeDelta;
                        row[8] = (hedgeDelta != 0.0) ? delta / hedgeDelta : 0.0;
                      }
                      else if (calcHedge)
                      {
                        row[6] = bumpTenors[k];
                        row[7] = row[8] = 0;
                      }
                    }
                    else
                    {
                      // The curve is not bumped.  There are two cases:
                      //  BumpResultType.Skipped - because the required tenors not exist;
                      //  BumpResultType.Failed - because the curve fails to refit.
                      logger.DebugFormat(" Curve {0}, tenor {1}, trade {2}, up = {3}, mid = {4}, down = {5}",
                                         bumpedCurves[i].Name, bumpTenors[k], evaluators[j].Product.Description,
                                         0.0, 0.0, 0.0);

                      row[0] = bumpedCurves[i].Category;
                      row[1] = bumpedCurves[i].Name;
                      row[2] = bumpTenors[k];
                      row[3] = evaluators[j].Product.Description;
                      row[4] = bumpResult == BumpResultType.Skipped ? 0.0 : Double.NaN; // delta;

                      if (calcGamma)
                        row[5] = bumpResult == BumpResultType.Skipped ? 0.0 : Double.NaN; // gamma

                      if (calcTenorHedge)
                      {
                        if (hedgeMaturities != null && hedgeMaturities.Length > 0 && hedgeMaturitiesOrNot[j])
                          row[6] = (new DateTime(1899, 12, 30).Add(TimeSpan.FromDays(Dt.ToExcelDate(hedgeMaturities[j])))).ToString(DefaultDateFormat);
                        else if (hedgeMaturities != null && hedgeMaturities.Length > 0 && !hedgeMaturitiesOrNot[j])
                          row[6] = (new DateTime(1899, 12, 30).Add(TimeSpan.FromDays(Dt.ToExcelDate(hedgeMaturities[j])))).ToString(DefaultDateFormat);
                        else
                        {
                          if (Dt.Cmp(Dt.Empty, hedgeDate) != 0)
                            row[6] = (new DateTime(1899, 12, 30).Add(TimeSpan.FromDays(Dt.ToExcelDate(hedgeDate)))).ToString(DefaultDateFormat);
                          else
                            row[6] = bumpTenors[k];
                        }
                        //row[7] = row[8] = System.DBNull.Value; Excel will show blank
                        row[7] = row[8] = bumpResult == BumpResultType.Skipped ? 0.0 : Double.NaN;
                      }
                      else if (calcHedge)
                      {
                        row[6] = bumpTenors[k];
                        row[7] = row[8] = bumpResult == BumpResultType.Skipped ? 0.0 : Double.NaN;
                      }
                    }
                    rows[currentRow++] = row;
                  } // tranches...
                }
              } // curves...
            } // tenors...

            break;

            #endregion ByTenor

            #region Parallel

          case BumpType.Parallel:
            // Make sure we use the right hedge. "matching" doesn't really make sense here.
            if (calcHedge && String.Compare(hedgeTenor, "matching") == 0)
              throw new ArgumentException(
                "Hedge calculation for a parallel bump requires specification of the hedge security if the 'hedgeTenor' parameter is 'matching'");

            calcTenorHedge = calcHedge;
            if (Dt.Cmp(hedgeDate, Dt.Empty) != 0)
              calcTenorHedge = calcTenorHedge && (CurveUtil.FindClosestTenor(curves, hedgeDate) != null);

            // Bump up
            if (upBumps.Any(x => x != 0.0))
            {
              // Bump up curves
              logger.Debug("Bumping all curves up");

              avgUpBumps = bumpedCurves.BumpQuotes(bumpTenors, targetQuoteType, upBumps,
                  flags | BumpFlags.RefitCurve, null);
              
              upTable = CalcIndividualBumpedPvs(evaluators, curves, bumpedCurves, savedCurves, dependentCurves, savedDependentCurves, mtm);
              if (calcTenorHedge)
              {
                if (Dt.Cmp(Dt.Empty, hedgeDate) == 0 && (hedgeMaturities == null || hedgeMaturities.Length == 0))
                  upHedge = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeTenor);
                else if (Dt.Cmp(Dt.Empty, hedgeDate) != 0)
                  upHedge = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeDate);
                else
                  for (int l = 0; l < hedgeMaturities.Length; ++l)
                    upHedge2[l] = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeMaturities[l]);
              }

              CurveUtil.CurveRestoreQuotes(bumpedCurves, savedCurves);

              // Remember base mtm for future bumps
              // Note: we do this here because basket bumping is faster for including base calculation.
              mtm = new double[evaluators.Length];
              for (int j = 0; j < evaluators.Length; j++)
                mtm[j] = upTable[0, j];
            }

            // Bump down
            if (downBumps.Any(x => x != 0.0))
            {
              logger.Debug("Bumping all curves down");
              avgDownBumps = bumpedCurves.BumpQuotes(bumpTenors, targetQuoteType, downBumps,
                flags | BumpFlags.BumpDown | BumpFlags.RefitCurve, null);
              downTable = CalcIndividualBumpedPvs(evaluators, curves, bumpedCurves, savedCurves, dependentCurves, savedDependentCurves, mtm);
              if (calcTenorHedge)
              {
                if (Dt.Cmp(Dt.Empty, hedgeDate) == 0 && (hedgeMaturities == null || hedgeMaturities.Length == 0))
                  downHedge = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeTenor);
                else if (Dt.Cmp(Dt.Empty, hedgeDate) != 0)
                  downHedge = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeDate);
                else
                  for (int l = 0; l < hedgeMaturities.Length; ++l)
                    downHedge2[l] = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeMaturities[l]);
              }
              CurveUtil.CurveRestoreQuotes(bumpedCurves, savedCurves);
            }
            
            // Save results
            logger.Debug("Saving results");
            for (int i = 0; i < bumpedCurves.Length; i++)
            {
              // skip the curves not bumped
              var bumpResult = GetBumpResult(i, upTable, downTable, avgUpBumps, avgDownBumps, false);

              for (int j = 0; j < evaluators.Length; j++)
              {
                object[] row = new object[colCount];

                if (evaluators[j].DependsOn(curves[i]))
                {
                  if (bumpResult == BumpResultType.Bumped)
                  {
                    // The curve is bumped normally...
                    logger.DebugFormat(" Curve {0}, tenor all, trade {1}, up = {2}, mid = {3}, down = {4}",
                                       bumpedCurves[i].Name, evaluators[j].Product.Description,
                                       (upTable != null) ? upTable[i + 1, j] : 0.0,
                                       (upTable != null) ? upTable[0, j] : downTable[0, j],
                                       (downTable != null) ? downTable[i + 1, j] : 0.0);

                    row[0] = bumpedCurves[i].Category;
                    row[1] = bumpedCurves[i].Name;
                    row[2] = "all";
                    row[3] = evaluators[j].Product.Description;

                    double delta;
                    delta = CalcDelta(i, j, upTable, downTable, scaledDelta, avgUpBumps, avgDownBumps, false);
                    row[4] = delta;

                    if (calcGamma)
                      row[5] = CalcGamma(i, j, upTable, downTable, scaledDelta, avgUpBumps, avgDownBumps, false);

                    if (calcTenorHedge)
                    {
                      double hedgeDelta;
                      if (hedgeMaturities != null && hedgeMaturities.Length > 0)
                      {
                        hedgeDelta = CalcHedge(i, upHedge2[j], downHedge2[j], scaledDelta, avgUpBumps, avgDownBumps, false);
                        row[6] = (new DateTime(1899, 12, 30).Add(TimeSpan.FromDays(Dt.ToExcelDate(hedgeMaturities[j])))).ToString(DefaultDateFormat);
                      }
                      else
                      {
                        if (Dt.Cmp(Dt.Empty, hedgeDate) != 0)
                        {
                          hedgeDelta = CalcHedge(i, upHedge, downHedge, scaledDelta, avgUpBumps, avgDownBumps, false);
                          row[6] = (new DateTime(1899, 12, 30).Add(TimeSpan.FromDays(Dt.ToExcelDate(hedgeDate)))).ToString(DefaultDateFormat);
                        }
                        else
                        {
                          hedgeDelta = CalcHedge(i, upHedge, downHedge, scaledDelta, avgUpBumps, avgDownBumps, false);
                          row[6] = hedgeTenor;
                        }
                      }
                      row[7] = 1000000 * hedgeDelta;
                      row[8] = (hedgeDelta != 0.0) ? delta / hedgeDelta : 0.0;
                    }
                    else
                    {
                      if (calcHedge)
                      {
                        //row[6] = System.DBNull.Value;
                        row[7] = row[8] = 0;
                      }
                    }
                  }
                  else
                  {
                    // The curve is not bumped.  There are two cases:
                    //  BumpResultType.Skipped - because the required tenors not exist;
                    //  BumpResultType.Failed - because the curve fails to refit.
                    logger.DebugFormat(" Curve {0}, tenor all, trade {1}, up = {2}, mid = {3}, down = {4}",
                                       bumpedCurves[i].Name, evaluators[j].Product.Description,
                                       0.0, 0.0, 0.0);

                    row[0] = bumpedCurves[i].Category;
                    row[1] = bumpedCurves[i].Name;
                    row[2] = "all";
                    row[3] = evaluators[j].Product.Description;
                    row[4] = bumpResult == BumpResultType.Skipped ? 0.0 : Double.NaN; // delta

                    if (calcGamma)
                      row[5] = bumpResult == BumpResultType.Skipped ? 0.0 : Double.NaN; // gamma

                    if (calcHedge)
                    {
                      if (hedgeMaturities != null && hedgeMaturities.Length > 0 && hedgeMaturitiesOrNot[j])
                        row[6] = (new DateTime(1899, 12, 30).Add(TimeSpan.FromDays(Dt.ToExcelDate(hedgeMaturities[j])))).ToString(DefaultDateFormat);
                      else if (hedgeMaturities != null && hedgeMaturities.Length > 0 && !hedgeMaturitiesOrNot[j])
                        row[6] = (new DateTime(1899, 12, 30).Add(TimeSpan.FromDays(Dt.ToExcelDate(hedgeMaturities[j])))).ToString(DefaultDateFormat);
                      else
                      {
                        if (Dt.Cmp(Dt.Empty, hedgeDate) != 0)
                          row[6] = (new DateTime(1899, 12, 30).Add(TimeSpan.FromDays(Dt.ToExcelDate(hedgeDate)))).ToString(DefaultDateFormat);
                        else
                          row[6] = hedgeTenor;
                      }
                      row[7] = bumpResult == BumpResultType.Skipped ? 0.0 : Double.NaN; // hedgeDelta;
                      row[8] = bumpResult == BumpResultType.Skipped ? 0.0 : Double.NaN; // hedge notional
                    }
                  }

                  rows[currentRow++] = row;
                } // tranches...
              }
            } // curves...

            break;

            #endregion Parallel

            #region Uniform

          case BumpType.Uniform:
            // Make sure we use the right hedge. "matching" doesn't really make sense here.
            if (calcHedge && String.Compare(hedgeTenor, "matching") == 0)
              throw new ArgumentException("Hedge calculation for a uniform bump requires specification of the hedge security, 'matching' not allows");
            calcTenorHedge = calcHedge;
            if (Dt.Cmp(hedgeDate, Dt.Empty) != 0)
              calcTenorHedge = calcTenorHedge && (CurveUtil.FindClosestTenor(curves, hedgeDate) != null);

            // Bump up curves, recalibrate and reprice
            if (upBumps.Any(x => x != 0.0))
            {
              logger.Debug("Bumping all curves up");
              avgUpBumps = bumpedCurves.BumpQuotes(bumpTenors, targetQuoteType, upBumps,
                                                 flags | BumpFlags.RefitCurve, null);
              
              upTable = CalcBumpedPvs(evaluators, curves, bumpedCurves, savedCurves, dependentCurves, savedDependentCurves, mtm);
              if (calcTenorHedge)
              {
                if (Dt.Cmp(Dt.Empty, hedgeDate) == 0 && (hedgeMaturities == null || hedgeMaturities.Length == 0))
                  upHedge = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeTenor);
                else if (Dt.Cmp(Dt.Empty, hedgeDate) != 0)
                  upHedge = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeDate);
                else
                  for (int l = 0; l < hedgeMaturities.Length; ++l)
                    upHedge2[l] = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeMaturities[l]);
              }
              CurveUtil.CurveRestoreQuotes(bumpedCurves, savedCurves);

              // Remember base mtm for future bumps
              // Note: we do this here because basket bumping is faster for including base calculation.
              mtm = new double[evaluators.Length];
              for (int j = 0; j < evaluators.Length; j++)
                mtm[j] = upTable[0, j];
            }

            // Bump up curves, recalibrate and reprice
            if (downBumps.Any(x => x != 0.0))
            {
              logger.Debug("Bumping all curves down");
              avgDownBumps = bumpedCurves.BumpQuotes(bumpTenors, targetQuoteType, downBumps,
                flags | BumpFlags.BumpDown | BumpFlags.RefitCurve, null);
              downTable = CalcBumpedPvs(evaluators, curves, bumpedCurves, savedCurves, dependentCurves, savedDependentCurves, mtm);
              if (calcTenorHedge)
              {
                if (Dt.Cmp(Dt.Empty, hedgeDate) == 0 && (hedgeMaturities == null || hedgeMaturities.Length == 0))
                  downHedge = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeTenor);
                else if (Dt.Cmp(Dt.Empty, hedgeDate) != 0)
                  downHedge = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeDate);
                else
                  for (int l = 0; l < hedgeMaturities.Length; ++l)
                    downHedge2[l] = CurveUtil.CurveHedge(curves, bumpedCurves, hedgeMaturities[l]);
              }
              CurveUtil.CurveRestoreQuotes(bumpedCurves, savedCurves);
            }

            // Save results
            logger.Debug("Saving results");
            for (int j = 0; j < evaluators.Length; j++)
            {
              logger.DebugFormat(" Curve all, trade {0}, base = {1}, up = {2}, down = {3}",
                                 evaluators[j].Product.Description,
                                 (upTable != null) ? upTable[0, j] : 0.0,
                                 (upTable != null) ? upTable[1, j] : 0.0,
                                 (downTable != null) ? downTable[1, j] : 0.0);

              object[] row = new object[colCount];

              row[0] = "all";
              row[1] = "all";
              row[2] = "all";
              row[3] = evaluators[j].Product.Description;

              double delta = CalcDelta(0, j, upTable, downTable, scaledDelta, avgUpBumps, avgDownBumps, true);
              row[4] = delta;

              if (calcGamma)
                row[5] = CalcGamma(0, j, upTable, downTable, scaledDelta, avgUpBumps, avgDownBumps, true);

              if (calcTenorHedge)
              {
                double hedgeDelta = 0.0;
                if (hedgeMaturities != null && hedgeMaturities.Length > 0)
                {
                  for (int i = 0; i < curves.Length; ++i)
                    hedgeDelta += CalcHedge(i, upHedge2[j], downHedge2[j], scaledDelta, avgUpBumps, avgDownBumps, false);
                  row[6] = (new DateTime(1899, 12, 30).Add(TimeSpan.FromDays(Dt.ToExcelDate(hedgeMaturities[j])))).ToString(DefaultDateFormat);
                }
                else
                {
                  if (Dt.Cmp(Dt.Empty, hedgeDate) != 0)
                  {
                    for (int i = 0; i < curves.Length; ++i)
                      hedgeDelta += CalcHedge(i, upHedge, downHedge, scaledDelta, avgUpBumps, avgDownBumps, false);
                    row[6] = (new DateTime(1899, 12, 30).Add(TimeSpan.FromDays(Dt.ToExcelDate(hedgeDate)))).ToString(DefaultDateFormat);
                  }
                  else
                  {
                    bool hasTenor = CurveUtil.CurveHasTenors(curves, new string[] {hedgeTenor});
                    if (!hasTenor)
                      throw new ToolkitException(String.Format("Tenor {0} not found", hedgeTenor));
                    row[6] = hedgeTenor;
                    for (int i = 0; i < curves.Length; ++i)
                      hedgeDelta += CalcHedge(i, upHedge, downHedge, scaledDelta, avgUpBumps, avgDownBumps, false);
                  }
                }

                hedgeDelta /= (double)curves.Length;
                row[7] = 1000000 * hedgeDelta;
                row[8] = (hedgeDelta != 0.0) ? delta / hedgeDelta : 0.0;
              }
              else
              {
                if (calcHedge)
                {
                  //row[6] = System.DBNull.Value;
                  row[7] = row[8] = 0;
                }
              }

              rows[currentRow++] = row;
            } // pricers...

            break;

            #endregion Uniform

          default:
            throw new ArgumentException(String.Format("This type of bump ({0}) is not yet supported ", bumpType));
        }
      }
      finally
      {
        // Restore what we may have changed
        CurveUtil.CurveSet(curves, origCurves);
        if (dependentCurves != null)
          CurveUtil.CurveSet(dependentCurves, origDependentCurves);

        // Mark pricers as needing recalculation
        PricerReset(evaluators);
      }

      return rows;
    }

    internal static bool StringIsNum(string hedgeTenor)
    {
      if (hedgeTenor == null || hedgeTenor.Length == 0)
        return false;
      bool isNum;
      double retNum;
      isNum = Double.TryParse(hedgeTenor, System.Globalization.NumberStyles.Any,
                              System.Globalization.NumberFormatInfo.InvariantInfo, out retNum);
      return isNum;
    }

    #endregion CurveTask

    #region Local_Helpers

    /// <summary>
    ///   Calculate MTM values for a pricer using each bumped curve in sequence.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Recalculation is avoided if the original and bumped curves are the same.</para>
    ///
    ///   <para>Does no argument validation</para>
    ///
    ///   <para>Does not maintain the state of the pricers</para>
    ///
    ///   <para>Does not maintain integrity of the original curve if an exception is
    ///   thrown. Must be called within code which saves the original curves then restores
    ///   under exception</para>
    /// </remarks>
    ///
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="curves">Array of original curves</param>
    /// <param name="bumpedCurves">Array of bumped curves</param>
    /// <param name="savedCurves">Array of copies of the original curves (to save us re-copying them) or null</param>
    /// <param name="dependentCurves">Array of dependent curves or null</param>
    /// <param name="savedDependentCurves">Array of copies of the original dependent curves or null</param>
    /// <param name="mtm">Array of original (unbumped) price values (or null if calculate)</param>
    ///
    /// <returns>
    ///    A table of price values represented by a two dimensional array.
    ///    Each column indentifies a pricer, while row 0 contains the base or original values
    ///    and row i (i &gt; 0) contains the values when the curve i-1 is replaced
    ///    by its alternative.
    /// </returns>
    ///
    public static double[,]
      CalcIndividualBumpedPvs(
      IPricer pricer,
      string measure,
      CalibratedCurve[] curves,
      CalibratedCurve[] bumpedCurves,
      CalibratedCurve[] savedCurves,
      CalibratedCurve[] dependentCurves,
      CalibratedCurve[] savedDependentCurves,
      double[] mtm
      )
    {
      return CalcIndividualBumpedPvs(CreateAdapters(pricer, measure),
                                     curves, bumpedCurves, savedCurves, dependentCurves, savedDependentCurves, mtm);
    }

    private static double[,]
      CalcIndividualBumpedPvs(
      PricerEvaluator[] evaluators,
      Curve[] curves,
      Curve[] bumpedCurves,
      Curve[] savedCurves,
      Curve[] dependentCurves,
      Curve[] savedDependentCurves,
      double[] mtm
      )
    {
      bool[] ignorePricers = null;
      if (evaluators != null && evaluators.Length > 0)
        ignorePricers = new bool[evaluators.Length];  // default all elements to false
      double[,] res =
        CalcIndividualBumpedPvs(evaluators, curves, bumpedCurves, savedCurves, dependentCurves, savedDependentCurves, mtm, ignorePricers);
      return res;
    }

    /// <summary>
    ///   Calculate MTM values for a series of prices using each bumped curve in
    ///   sequence.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Recalculation is avoided if the original and bumped curves are the same.</para>
    ///
    ///   <para>Does no argument validation</para>
    ///
    ///   <para>Does not maintain the state of the pricers</para>
    ///
    ///   <para>Does not maintain integrity of the original curve if an exception is
    ///   thrown. Must be called within code which saves the original curves then restores
    ///   under exception</para>
    /// </remarks>
    ///
    /// <param name="evaluators">Array of pricer evaluators</param>
    /// <param name="curves">Array of original curves</param>
    /// <param name="bumpedCurves">Array of bumped curves</param>
    /// <param name="savedCurves">Array of copies of the original curves (to save us re-copying them) or null</param>
    /// <param name="dependentCurves">Array of dependent curves or null</param>
    /// <param name="savedDependentCurves">Array of copies of the original dependent curves or null</param>
    /// <param name="mtm">Array of original (unbumped) price values (or null if calculate)</param>
    /// <param name="ignorePricers">The elements of this array correspond to the element of evaluators array. 
    /// Will set bumped pv and sensitivity to 0 for those elements of this array which are set to true.</param>
    ///
    /// <returns>
    ///    A table of price values represented by a two dimensional array.
    ///    Each column indentifies a pricer, while row 0 contains the base or original values
    ///    and row i (i &gt; 0) contains the values when the curve i-1 is replaced
    ///    by its alternative.
    /// </returns>
    ///
    private static double[,]
      CalcIndividualBumpedPvs(
      PricerEvaluator[] evaluators,
      Curve[] curves,
      Curve[] bumpedCurves,
      Curve[] savedCurves,
      Curve[] dependentCurves,
      Curve[] savedDependentCurves,
      double[] mtm,
      bool[] ignorePricers
      )
    {
#if DEBUG
      // Validation
      if (evaluators == null || evaluators.Length == 0)
        throw new ArgumentOutOfRangeException("evaluators", "Must specify pricers for calculation");
      if (ignorePricers == null || ignorePricers.Length != evaluators.Length)
        throw new ArgumentException("The size of ignorePricers array must match the size of evaluators array");
      if (curves == null || curves.Length == 0)
        throw new ArgumentOutOfRangeException("curves", "Must specify curves to bump or null for all");
      if (bumpedCurves == null || bumpedCurves.Length != curves.Length)
        throw new ArgumentOutOfRangeException("bumpedCurves", "bumpedCurves and curves must of the sample length");
      if (savedCurves != null && savedCurves.Length != curves.Length)
        throw new ArgumentOutOfRangeException("savedCurves", "savedCurves and curves must of the sample length");
      if (dependentCurves != null && savedDependentCurves != null && dependentCurves.Length != savedDependentCurves.Length)
        throw new ArgumentOutOfRangeException("dependentCurves", "dependentCurves and savedDependentCurves must of the sample length");
#endif

      double[,] results = null;
      Timer timer = new Timer();
      timer.start();

      logger.Debug("Repricing bumped PVs...");

      // For efficiency,
      logger.Debug("Directly calculating sensitivities...");
      results = new double[curves.Length + 1, evaluators.Length];
      bool[] calculated = new bool[evaluators.Length];

      // Save curves before bumping
      if (savedCurves == null)
      {
        savedCurves = CloneUtil.Clone(curves);
      }
      if (savedDependentCurves == null && dependentCurves != null)
      {
        savedDependentCurves = new Curve[dependentCurves.Length];
        CurveUtil.CurveSet(savedDependentCurves, dependentCurves);
      }

      // Price remaining products
      for (int j = 0; j < evaluators.Length; j++)
      {
        if (ignorePricers[j])
        {
          for (int i = 0; i < curves.Length + 1; i++)
            results[i, j] = 0.0;
        }
        else
        {
          if (!calculated[j])
          {
            evaluators[j].SurvivalBumpedEval.Calculate(
              j, evaluators, curves, bumpedCurves, savedCurves,
              dependentCurves, savedDependentCurves,
              calculated, mtm, results);
          }
        }
      }

      timer.stop();
      logger.InfoFormat("Completed repricing in {0}s", timer.getElapsed());

      return results;
    }

    /// <summary>
    ///   Calculate MTM values for a series of prices using all bumped curve together.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Does no argument validation</para>
    ///
    ///   <para>Does not maintain the state of the pricers</para>
    ///
    ///   <para>Maintains the state of the original curve unless an exception
    ///   occurs. If the state needs to be guaranteed, the calling function must save
    ///   the curves state and restore it under exception.</para>
    /// </remarks>
    ///
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="curves">Array of original curves</param>
    /// <param name="bumpedCurves">Array of bumped curves</param>
    /// <param name="savedCurves">Array of copies of the original curves (to save us re-copying them) or null</param>
    /// <param name="dependentCurves">Array of dependent curves or null</param>
    /// <param name="savedDependentCurves">Array of copies of the original dependent curves or null</param>
    /// <param name="mtm">Array of original (unbumped) price values (or null if calculate)</param>
    ///
    /// <returns>
    ///    A table of MTM values represented by a two dimensional array.
    ///    Each column indentifies a pricer, while row 0 contains the base or original values
    ///    and row 1 contains the values when all curves are replaced by their alternative.
    /// </returns>
    ///
    public static double[,]
      CalcBumpedPvs(
      IPricer pricer,
      string measure,
      CalibratedCurve[] curves,
      CalibratedCurve[] bumpedCurves,
      CalibratedCurve[] savedCurves,
      CalibratedCurve[] dependentCurves,
      CalibratedCurve[] savedDependentCurves,
      double[] mtm
      )
    {
      return CalcBumpedPvs(CreateAdapters(pricer, measure),
                           curves, bumpedCurves, savedCurves, dependentCurves, savedDependentCurves, mtm);
    }

    private static double[,]
      CalcBumpedPvs(
      PricerEvaluator[] pricers,
      CalibratedCurve[] curves,
      CalibratedCurve[] bumpedCurves,
      CalibratedCurve[] savedCurves,
      CalibratedCurve[] dependentCurves,
      CalibratedCurve[] savedDependentCurves,
      double[] mtm
      )
    {
      bool[] ignorePricers = null;
      if (pricers != null && pricers.Length > 0)
        ignorePricers = new bool[pricers.Length];  // default all elements to false
      double[,] res =
        CalcBumpedPvs(pricers, curves, bumpedCurves, savedCurves, dependentCurves, savedDependentCurves, mtm, ignorePricers);
      return res;
    }

    /// <summary>
    ///   Calculate MTM values for a series of prices using all bumped curve together.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Does no argument validation</para>
    ///
    ///   <para>Does not maintain the state of the pricers</para>
    ///
    ///   <para>Maintains the state of the original curve unless an exception
    ///   occurs. If the state needs to be guaranteed, the calling function must save
    ///   the curves state and restore it under exception.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricers</param>
    /// <param name="curves">Array of original curves</param>
    /// <param name="bumpedCurves">Array of bumped curves</param>
    /// <param name="savedCurves">Array of copies of the original curves (to save us re-copying them) or null</param>
    /// <param name="dependentCurves">Array of dependent curves or null</param>
    /// <param name="savedDependentCurves">Array of copies of the original dependent curves or null</param>
    /// <param name="mtm">Array of original (unbumped) MTMs (or null if calculate)</param>
    /// <param name="ignorePricers">The elements of this array correspond to the element of evaluators array. 
    /// Will set bumped pv and sensitivity to 0 for those elements of this array which are set to true.</param>
    ///
    /// <returns>
    ///    A table of MTM values represented by a two dimensional array.
    ///    Each column indentifies a pricer, while row 0 contains the base or original values
    ///    and row 1 contains the values when all curves are replaced by their alternative.
    /// </returns>
    ///
    private static double[,]
      CalcBumpedPvs(
      PricerEvaluator[] pricers,
      CalibratedCurve[] curves,
      CalibratedCurve[] bumpedCurves,
      CalibratedCurve[] savedCurves,
      CalibratedCurve[] dependentCurves,
      CalibratedCurve[] savedDependentCurves,
      double[] mtm,
      bool[] ignorePricers
      )
    {
#if DEBUG
  // Validation
      if (pricers == null || pricers.Length == 0)
        throw new ArgumentException("Must specify pricers for calculation");
      if (ignorePricers == null || ignorePricers.Length != pricers.Length)
        throw new ArgumentException("The size of ignorePricers array must match the size of pricers array");
      if (curves == null || curves.Length == 0)
        throw new ArgumentException("Must specify curves to bump or null for all");
      if (bumpedCurves == null || bumpedCurves.Length != curves.Length)
        throw new ArgumentException("bumpedCurves and curves must of the sample length");
      if (savedCurves != null && savedCurves.Length != curves.Length)
        throw new ArgumentException("savedCurves and curves must of the sample length");
      if (dependentCurves != null && savedDependentCurves != null && dependentCurves.Length != savedDependentCurves.Length)
        throw new ArgumentException("dependentCurves and savedDependentCurves must of the sample length");
#endif

      Timer timer = new Timer();
      timer.start();

      logger.Debug("Repricing...");

      // compute the base case
      double[,] results = new double[2,pricers.Length];
      PricerReset(pricers);
      for (int j = 0; j < pricers.Length; j++)
      {
        if (ignorePricers[j])
          results[0, j] = 0.0;
        else
          results[0, j] = (mtm == null) ? pricers[j].Evaluate() : mtm[j];
      }

      // Save curves before bumping
      if (savedCurves == null)
      {
        savedCurves = new CalibratedCurve[curves.Length];
        CurveUtil.CurveSet(savedCurves, curves);
      }
      if (savedDependentCurves == null && dependentCurves != null)
      {
        savedDependentCurves = new CalibratedCurve[dependentCurves.Length];
        CurveUtil.CurveSet(savedDependentCurves, dependentCurves);
      }

      // Set bump curves
      CurveUtil.CurveSet(curves, bumpedCurves);

      // Recalibrate dependent curves if necessary
      if (dependentCurves != null)
        CurveUtil.CurveFit(dependentCurves);

      // Reprice
      PricerReset(pricers);
      for (int j = 0; j < pricers.Length; j++)
      {
        results[1, j] = ignorePricers[j] ? 0.0 : pricers[j].Evaluate();
      }

      // Restore curves
      CurveUtil.CurveSet(curves, savedCurves);
      if (savedDependentCurves != null)
        CurveUtil.CurveSet(dependentCurves, savedDependentCurves);

      timer.stop();
      logger.InfoFormat("Completed repricing in {0}s", timer.getElapsed());

      return results;
    }

    /// <summary>
    ///   Local utility function to calc Delta for curve i, pricer j
    /// </summary>
    /// <param name="i">Curve index</param>
    /// <param name="j">Pricer index</param>
    /// <param name="upTable">Up bumped table</param>
    /// <param name="downTable">Down bumped table</param>
    /// <param name="scaled">Whether to scale the result</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <returns>Delta</returns>
    public static double
      CalcDelta(int i, int j, double[,] upTable, double[,] downTable,
                bool scaled, double upBump, double downBump)
    {
      double delta = 0.0;

      if (null != upTable)
        delta += (upTable[i + 1, j] - upTable[0, j]);
      if (null != downTable)
        delta -= (downTable[i + 1, j] - downTable[0, j]);
      if (scaled)
        delta /= (upBump + downBump);

      return delta;
    }

    /// <summary>
    ///   Local utility function to calc Delta for curve i, pricer j
    /// </summary>
    /// <param name="i">Curve index</param>
    /// <param name="j">Pricer index</param>
    /// <param name="upTable">Up bumped table</param>
    /// <param name="downTable">Down bumped table</param>
    /// <param name="scaled">Whether to scale the result</param>
    /// <param name="upBumps">Up bump sizes</param>
    /// <param name="downBumps">Down bump sizes</param>
    /// <param name="uniform">Whether the bump is uniform</param>
    /// <returns>Delta</returns>
    private static double
      CalcDelta(int i, int j, double[,] upTable, double[,] downTable,
                bool scaled, double[] upBumps, double[] downBumps, bool uniform)
    {
      double upBump = 0.0, downBump = 0.0;

      if (scaled)
      {
        upBump = (null != upTable) ? (uniform ? Average(upBumps) : upBumps[i]) : 0.0;
        downBump = (null != downTable) ? (uniform ? Average(downBumps) : downBumps[i]) : 0.0;
      }

      return CalcDelta(i, j, upTable, downTable, scaled, upBump, downBump);
    }

    /// <summary>
    ///   Local utility function to check if curve i is bumped by nonzero amount
    /// </summary>
    /// <param name="i">Curve index</param>
    /// <param name="upTable">Up bumped table</param>
    /// <param name="downTable">Down bumped table</param>
    /// <param name="upBumps">Up bump sizes</param>
    /// <param name="downBumps">Down bump sizes</param>
    /// <param name="uniform">Whether the bump is uniform</param>
    /// <returns>Delta</returns>
    private static BumpResultType GetBumpResult(int i, double[,] upTable, double[,] downTable,
                                                double[] upBumps, double[] downBumps, bool uniform)
    {
      double upBump = (null != upTable) ? (uniform ? Average(upBumps) : upBumps[i]) : 0.0;
      if (Double.IsNaN(upBump)) return BumpResultType.Failed;
      double downBump = (null != downTable) ? (uniform ? Average(downBumps) : downBumps[i]) : 0.0;
      if (Double.IsNaN(downBump)) return BumpResultType.Failed;
      return Math.Abs(upBump + downBump) > 0.0 ? BumpResultType.Bumped : BumpResultType.Skipped;
    }

    private enum BumpResultType
    {
      Bumped,
      Skipped,
      Failed
    }

    /// <summary>
    ///   Local utility function to calc Gamma for curve i, pricer j
    /// </summary>
    /// <param name="i">Curve index</param>
    /// <param name="j">Pricer index</param>
    /// <param name="upTable">Up bumped table</param>
    /// <param name="downTable">Down bumped table</param>
    /// <param name="scaled">Whether to scale the result</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <returns>Gamma</returns>
    public static double
      CalcGamma(int i, int j, double[,] upTable, double[,] downTable,
                bool scaled, double upBump, double downBump)
    {
      double gamma = 0.0;

      if (null != upTable && null != downTable && scaled)
      {
        // NOTE!!! If upBump != downBump you're really getting a guess at the gamma not
        // at x, but at x + 0.5*(upBump-downBump).  This is unlikely to be a huge deal,
        // but if you have a highly convex function it might be an issue.

        // Find change in deltas. First term approximates delta at (x + 0.5*upBump) and
        // the second term approximates (minus) delta at (x - 0.5*downBump)
        gamma = (upTable[i + 1, j] - upTable[0, j]) / upBump + (downTable[i + 1, j] - downTable[0, j]) / downBump;
        // Now scale by the change in x
        gamma /= ((upBump + downBump) / 2);
      }

      return gamma;
    }

    // Local utility function to calc Gamma for curve i, pricer j
    private static double
      CalcGamma(int i, int j, double[,] upTable, double[,] downTable,
                bool scaled, double[] upBumps, double[] downBumps, bool uniform)
    {
      double upBump = 0.0, downBump = 0.0;

      if (null != upTable && null != downTable && scaled)
      {
        upBump = (uniform ? Average(upBumps) : upBumps[i]);
        downBump = (uniform ? Average(downBumps) : downBumps[i]);
      }

      return CalcGamma(i, j, upTable, downTable, scaled, upBump, downBump);
    }

    /// <summary>
    ///   Local utility function to calc hedge for curve i
    /// </summary>
    /// <param name="i">Curve index</param>
    /// <param name="upHedge">Array of up hedges</param>
    /// <param name="downHedge">Array of down hedges</param>
    /// <param name="scaled">Whether to scale the result</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <returns>Hedge</returns>
    public static double
      CalcHedge(int i, double[] upHedge, double[] downHedge,
                bool scaled, double upBump, double downBump)
    {
      double hedge = 0.0;

      if (null != upHedge)
        hedge += upHedge[i];
      if (null != downHedge)
        hedge -= downHedge[i];
      if (scaled)
      {
        //ensure denominator is non-zero; 
        //upBump on downBump may be zeroed out during the sensitivity calc if the curve is defaulted
        if (upBump + downBump == 0)
          return 0.0;
        hedge /= (upBump + downBump);
      }

      return hedge;
    }

    // Local utility function to calc hedge for curve i
    private static double
      CalcHedge(int i, double[] upHedge, double[] downHedge,
                bool scaled, double[] upBumps, double[] downBumps, bool uniform)
    {
      double upBump = 0.0, downBump = 0.0;

      if (scaled)
      {
        upBump = (null != upHedge) ? (uniform ? Average(upBumps) : upBumps[i]) : 0.0;
        downBump = (null != downHedge) ? (uniform ? Average(downBumps) : downBumps[i]) : 0.0;
      }

      return CalcHedge(i, upHedge, downHedge, scaled, upBump, downBump);
    }

    // utility: calculate average of a double array.
    // To avoid NaN, use index j as divisor in stead of index i.
    // Only the non-zero elements are averaged.
    private static double Average(double[] a)
    {
      double result = 0;
      if (null != a)
      {
        for (int i = 0, j = 0; i < a.Length; ++i)
        {
          if (Math.Abs(a[i]) > 0)
          {
            result += (a[i] - result) / (j + 1);
            j++;
          }
        }
      }
      return result;
    }

    #endregion // Local_Helpers

    #region Helpers

    /// <summary>
    /// Check pricers and target value
    /// </summary>
    /// <param name="pricer">Pricer</param>
    /// <param name="targetValue">Target value to evaluate (delegate or pricer method name)</param>
    /// <returns>Valid pricer array</returns>
    public static PricerEvaluator[] CreateAdapters(IPricer pricer, object targetValue)
    {
      return CreateAdapters(new [] {pricer}, targetValue, false, true);
    }

    /// <summary>
    /// Check pricers and target value
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="targetValue">Target value to evaluate (delegate or pricer method name)</param>
    /// <returns>Valid pricer array</returns>
    public static PricerEvaluator[] CreateAdapters(this IPricer[] pricers, object targetValue)
    {
      return CreateAdapters(pricers, targetValue, false, true);
    }

    /// <summary>
    /// Check pricers and target value
    /// </summary>
    /// <param name="pricers">Array of pricers</param>
    /// <param name="targetValue">Target value to evaluate (delegate or method name)</param>
    /// <param name="allowMissing">Allow missing method. In this case return 0 when evaluated</param>
    /// <param name="isAdditive">Whether the pricer measure is additive</param>
    /// <returns>Valid pricer array</returns>
    public static PricerEvaluator[] CreateAdapters(this IPricer[] pricers, object targetValue, bool allowMissing, bool isAdditive)
    {
      if (pricers == null) return null;
      var adapters = new PricerEvaluator[pricers.Length];
      if ((targetValue is string) && String.Compare((string)targetValue, "CleanPv", StringComparison.OrdinalIgnoreCase) == 0)
      {
        // Special case of clean pv. RD Apr'15. Moved from Scenarios.cs. Should pricers that support accrued all suport this directly?
        Func<IPricer, double> f = p => p.Pv() - p.Accrued();
        targetValue = f;
      }
      var del = targetValue as Delegate;
      if (del != null)
      {
        // targetValue is a delegate
        var fn = del.ToDoublePricerFn();
        for (var i = 0; i < pricers.Length; ++i)
          adapters[i] = new PricerEvaluator(pricers[i], fn);
      }
      else
      {
        // targetValue is a method name
        var measure = (targetValue != null) ? (string)targetValue : null;
        for (var i = 0; i < pricers.Length; ++i)
          adapters[i] = new PricerEvaluator(pricers[i], measure, allowMissing, isAdditive);
      }
      return adapters;
    }

    /// <summary>
    ///   Reset pricer evaluators and selective update baskets for modified recovery rates
    /// </summary>
    /// <param name="evaluators">Pricer evaluators</param>
    private static void PricerResetRecoveryRates(PricerEvaluator[] evaluators)
    {
      if (evaluators == null)
        return;
      foreach (PricerEvaluator t in evaluators)
        if (t != null)
          t.Reset(true, false);
    }

    /// <summary>
    ///   Reset pricer evaluators and selective update baskets for modified correlations
    /// </summary>
    /// <param name="evaluators">Pricer evaluators</param>
    private static void PricerResetCorrelations(PricerEvaluator[] evaluators)
    {
      if (evaluators == null)
        return;
      foreach (PricerEvaluator t in evaluators)
        if (t != null)
          t.Reset(false, true);
    }

    /// <summary>
    ///   Reset pricer evaluators and selective update baskets
    /// </summary>
    /// <param name="evaluators">Pricer evaluators</param>
    /// <param name="recoveryModified">Whether recovery curves need to reset</param>
    /// <param name="correlationModified">Whether correlation need reset</param>
    private static void PricerReset(
      PricerEvaluator[] evaluators,
      bool recoveryModified,
      bool correlationModified)
    {
      if (evaluators == null)
        return;
      foreach (PricerEvaluator t in evaluators)
        if (t != null)
          t.Reset(recoveryModified, correlationModified);
    }

    // Reset a set of pricers to require recalculation
    private static void PricerReset(PricerEvaluator[] evaluators)
    {
      if (evaluators == null)
        return;
      foreach (PricerEvaluator t in evaluators)
        if (t != null)
          t.Reset();
    }

    // Set pricer dates.
    private static void PricerSetDates(PricerEvaluator[] pricers, bool reset, bool setCurveAsOfs, bool recalibrate, Dt asOf, Dt settle, Dt toAsOf, Dt toSettle,
                                       IList<CalibratedCurve> discountCurves, IList<SurvivalCurve> survivalCurves, IList<RecoveryCurve> recoveryCurves,
                                       IList<RateVolatilityCube> fwdVolCubes)
    {
      Dt targetAsOf = reset ? asOf : toAsOf;
      Dt targetSettle = reset ? settle : toSettle;

      if (pricers != null)
        for (int i = 0; i < pricers.Length; i++)
          if (pricers[i] != null)
          {
            if (pricers[i].Pricer is LoanPricer)
            {
              if (reset)
                ((LoanPricer)pricers[i].Pricer).ResetAfterTheta();
              else
                ((LoanPricer)pricers[i].Pricer).SetInterestPeriodsForTheta(toAsOf, toSettle);
            }
            pricers[i].AsOf = targetAsOf;
            pricers[i].Settle = targetSettle;
          }

      if (discountCurves != null && setCurveAsOfs)
        for (int i = 0; i < discountCurves.Count; i++)
          discountCurves[i].SetCurveAsOfDate(targetAsOf);

      if (recoveryCurves != null && setCurveAsOfs)
        for (int i = 0; i < recoveryCurves.Count; i++)
          recoveryCurves[i].AsOf = targetAsOf;

      
      SetSurvivalCurveAsOf(survivalCurves, setCurveAsOfs, targetAsOf, targetSettle, recalibrate);

      if (fwdVolCubes != null && setCurveAsOfs)
      {
        for (int i = 0; i < fwdVolCubes.Count; i++)
        {
          fwdVolCubes[i].AsOf = targetAsOf;
          fwdVolCubes[i].RateVolatilityCalibrator.AsOf = targetAsOf;
        }
      }
      return;
    }


    
    internal static void SetSurvivalCurveAsOf(IList<SurvivalCurve> survivalCurves,
      bool setCurveAsOfs, Dt targetAsOf, Dt targetSettle, bool recalibrate)
    {
      if (survivalCurves != null)
      {
        for (int i = 0; i < survivalCurves.Count; i++)
        {
          if (setCurveAsOfs)
            survivalCurves[i].SetCurveAsOfDate(targetAsOf);
          if (recalibrate)
          {
            if (survivalCurves[i].Calibrator != null)
            {
              // Empirically, need to set calibrator dates in this case as well.  See case 18409.
              survivalCurves[i].Calibrator.AsOf = targetAsOf;
              survivalCurves[i].Calibrator.Settle = targetSettle;
              if (survivalCurves[i].Calibrator is SurvivalFitCalibrator)
                ((SurvivalFitCalibrator)survivalCurves[i].Calibrator).ValueDate = targetSettle;              
            }

            CurveUtil.ResetCdsTenorDatesRecalibrate(survivalCurves[i], targetAsOf, targetSettle, ToolkitConfigurator.Settings.ThetaSensitivity.RecalibrateWithNewCdsEffective, ToolkitConfigurator.Settings.ThetaSensitivity.RecalibrateWithRolledCdsMaturity);
          }
        }
      }
    }

    /// <summary>
    /// Given a pricer (evaluator), find the last date for which market data will affect
    /// pricing. 
    /// </summary>
    /// <param name="pricers"></param>
    /// <returns></returns>
    public static Dt LastMaturity(PricerEvaluator[] pricers)
    {
      if (pricers == null || pricers.Length == 0)
        return Dt.Empty;
      Dt last = pricers[0].Pricer.GetProductForLastMaturity().LastMaturity();

      for (int i = 1; i < pricers.Length; i++)
      {
        var newMaturity = pricers[i].Pricer.GetProductForLastMaturity().LastMaturity();
        if (Dt.Cmp(newMaturity, last) > 0)
          last = newMaturity;
      }
      return last;
    }

    private static IProduct GetProductForLastMaturity(this IPricer pricer)
    {
      if (pricer == null) return null;
      var bondFuturePricer = pricer as BondFuturePricer;
      return bondFuturePricer != null ? bondFuturePricer.CtdBond : pricer.Product;
    }

    private static Dt LastMaturity(this IProduct product)
    {
      if (product == null) return Dt.Empty;
      var fut = product as StirFuture;
      if (fut != null) return fut.DepositMaturity;
      var fra = product as FRA;
      if (fra != null) return fra.ContractMaturity;
      var option = product as IOptionProduct;
      return option != null && option.Underlying != null
        ? LastMaturity(option.Underlying)
        : product.Maturity;
    }

    // Utility to find the latest settlement date
    private static Dt LastSettle(PricerEvaluator[] pricers)
    {
      if (pricers == null || pricers.Length == 0)
        return Dt.Empty;
      Dt settle = pricers[0].Settle;
      for (int i = 1; i < pricers.Length; ++i)
        if (Dt.Cmp(settle, pricers[i].Settle) < 0)
          settle = pricers[i].Settle;
      return settle;
    }

    /// <summary>
    /// Save and set new rescaleStrikes flags
    /// </summary>
    internal static bool[] ResetRescaleStrikes(PricerEvaluator[] pricers, bool[] rescaleStrikes)
    {
      if (rescaleStrikes == null || rescaleStrikes.Length == 0)
        return null;
      bool[] rescaleStrikesSaved = new bool[pricers.Length];
      for (int i = 0; i < pricers.Length; i++)
      {
        var basket = pricers[i].Basket as BaseCorrelationBasketPricer;
        if (basket == null) continue;
        rescaleStrikesSaved[i] = basket.RescaleStrike;
        basket.RescaleStrike
          = rescaleStrikes.Length == 1 ? rescaleStrikes[0] : rescaleStrikes[i];
      }

      return rescaleStrikesSaved;
    }

    // Save and set new rescaleStrikes flags
    internal static bool[] ResetRescaleStrikes(SyntheticCDOPricer[] pricers, bool[] rescaleStrikes)
    {
      if (rescaleStrikes == null || rescaleStrikes.Length == 0)
        return null;
      bool[] rescaleStrikesSaved = new bool[pricers.Length];
      for (int i = 0; i < pricers.Length; i++)
      {
        BaseCorrelationBasketPricer basket = pricers[i].Basket as BaseCorrelationBasketPricer;
        if (basket == null) continue;
        rescaleStrikesSaved[i] = basket.RescaleStrike;
        basket.RescaleStrike
          = rescaleStrikes.Length == 1 ? rescaleStrikes[0] : rescaleStrikes[i];
      }
      return rescaleStrikesSaved;
    }

    #endregion // Helpers
  }

  // class Sensitivities
}