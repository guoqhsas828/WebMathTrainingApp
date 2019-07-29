/*
 * CDXSpreadFactorCalc.cs
 *
 *   2008. All rights reserved.
 *
 * $Id$
 *
 */
using System;
using System.Collections.Generic;
using System.Text;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers.Baskets
{
  /// <summary>
  ///   Spread approach scaling factor calculator
  /// </summary>
  internal class CDXSpreadFactorCalc : SolverFn
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CDXSpreadFactorCalc));

    /// <summary>
    ///   Constructor
    /// </summary>
    public CDXSpreadFactorCalc(
        SurvivalCurve[] survivalCurves,
        bool relativeScaling,
        string[] tenorNamesScaled,
        double[] scalingFactors,
        string[] tenorNamesToScale,
        double[] scalingWeights,
        CDX cdx)
    {
      checkWeights(scalingWeights);

      // construct a set of curves to bump
      CalibratedCurve[] bumpedSurvivalCurves = CloneUtil.Clone(survivalCurves);

      // scale the curves for the tenors we already know the factors
      if ((null != scalingFactors) && (scalingFactors.Length > 0))
      {
        CurveUtil.CurveBump(bumpedSurvivalCurves, tenorNamesScaled,
          scalingFactors, true, relativeScaling, true, scalingWeights);
        savedCurves_ = bumpedSurvivalCurves;
        survivalCurves_ = CloneUtil.Clone(bumpedSurvivalCurves);
      }
      else
      {
        savedCurves_ = survivalCurves;
        survivalCurves_ = bumpedSurvivalCurves;
      }

      // set the data members
      relative_ = relativeScaling;
      tenorNamesToScale_ = tenorNamesToScale;
      scalingWeights_ = scalingWeights;
      cdx_ = cdx;
    }

    /// <summary>
    ///   Solve for a scaling factor
    /// </summary>
    /// <returns>evaluated objective function f(x)</returns>
    public static double Solve(
      CDX cdx,
      SurvivalCurve[] survivalCurves,
      bool relativeScaling,
      string[] tenorNamesScaled,
      double[] scalingFactors,
      string[] tenorNamesToScale,
      double targetSpread,
      double[] scalingWeights)
    {
      checkWeights(scalingWeights);

      // Set up root finder
      //
      Brent rf = new Brent();
      rf.setToleranceX(ToleranceX);
      rf.setToleranceF(ToleranceF);
      if (MaxIterations > 0)
        rf.setMaxIterations(MaxIterations);

      // Solve
      CDXSpreadFactorCalc fn = new CDXSpreadFactorCalc(survivalCurves, relativeScaling,
        tenorNamesScaled, scalingFactors, tenorNamesToScale, scalingWeights, cdx);

      // Solve
      double res;
      try
      {
        res = rf.solve(fn, targetSpread, -0.5, 0.5);
      }
      catch (Exception e)
      {
        // When the solver fails, we check the accuracy of 8.1 solution
        // and fall back to it.

        // Need to reconstruct the function object since it might
        // be left in some weird state by the exception.
        fn = new CDXSpreadFactorCalc(survivalCurves, relativeScaling,
          tenorNamesScaled, scalingFactors, tenorNamesToScale, scalingWeights, cdx);

        // calculate the duration weighted spread based on the unscaled curves
        double spread = fn.evaluate(0.0);

        // calculate the scaling factor by our old, simple, not so consistent method
        if (targetSpread > spread)
          res = targetSpread / spread - 1.0;
        else
          res = 1.0 - spread / targetSpread;

        // calculate the duration weighted spread based on the scaled curves
        spread = fn.evaluate(res);

        // log an info message so the user can check it
        logger.InfoFormat(e.Message);

        // check the fall back tolerance
        if (Math.Abs(spread - targetSpread) >= FallBackTolerance)
          logger.InfoFormat("Using factor {0} with a duration spread {1} outside the tolerance {2}",
            res, spread, FallBackTolerance);
        else
          logger.InfoFormat("Using factor {0} with a duration spread {1} within the tolerance {2}",
            res, spread, FallBackTolerance);
      }

      return res;
    }

    /// <summary>
    ///  Compute average implied cds spread with x as the scaling factor
    /// </summary>
    /// <returns>evaluated objective function f(x)</returns>
    public override double evaluate(double x)
    {
      logger.DebugFormat("Trying factor {0}", x);

      CDX cdx = cdx_;
      CalibratedCurve[] survivalCurves = survivalCurves_;
      string[] tenorsToScale = tenorNamesToScale_;
      double[] scalingWeights = scalingWeights_;

      // Bump up curves
      CurveUtil.CurveBump(survivalCurves, tenorsToScale,
        new double[] { x }, true, relative_, true, scalingWeights);      
      
      // Calculate the implied average cds spread to match the quote index spread
      double impliedAverageSpread = ImpliedAverageCdsSpread();

      // Restore curves
      CurveUtil.CurveRestoreQuotes(survivalCurves_, savedCurves_);

      // Return results scaled to percent of notional
      logger.DebugFormat("Returning average cds implied spreads {0} for factor {1}", impliedAverageSpread, x);

      return impliedAverageSpread;
    }

    internal double ImpliedAverageCdsSpread()
    {
      double[] cdxWeights = cdx_.Weights;
      double sumTenorWeights = 0.0;
      double averageSpread = 0;
      for (int i = 0; i < survivalCurves_.Length; ++i)
      {
        double tenorWeight = (cdxWeights == null ? 1.0 : cdxWeights[i]) * scalingWeights_[i];
        double impliedCdsSpread = CurveUtil.ImpliedSpread((SurvivalCurve)survivalCurves_[i], cdx_.Maturity, cdx_.DayCount,
                                                          cdx_.Freq, cdx_.BDConvention, cdx_.Calendar);
        averageSpread += tenorWeight * impliedCdsSpread;
        sumTenorWeights += tenorWeight;
      }
      averageSpread /= sumTenorWeights;
      return averageSpread;
    }

    private static void checkWeights(double[] scalingWieghts)
    {
      double sumWeights = 0.0;
      for (int i = 0; i < scalingWieghts.Length; i++)
        sumWeights += scalingWieghts[i];

      if (Math.Abs(sumWeights) < 10.0 * Double.Epsilon)
        throw new ArgumentException("Scaling weights must not sum to zero.");
    }

    private CalibratedCurve[] survivalCurves_;
    private CalibratedCurve[] savedCurves_;
    private string[] tenorNamesToScale_;
    private double[] scalingWeights_;
    private CDX cdx_;
    private bool relative_;

    /// <summary>
    ///   Tolerance of factor error
    /// </summary>
    public static double ToleranceX = 1E-5;

    /// <summary>
    ///   Tolerance of spread error
    /// </summary>
    public static double ToleranceF = 1E-6;

    /// <summary>
    ///   Maximum iterations allowed
    /// </summary>
    public static int MaxIterations = 20;

    /// <summary>
    ///   Spread error tolerance in fall back routine
    /// </summary>
    public static double FallBackTolerance = 0.0001;

  } // class CDXDurationFactorCalc
}
