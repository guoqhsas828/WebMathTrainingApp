/*
 * CDXProtPvFactorCalc.cs
 *
 *   2008. All rights reserved.
 *
 * $Id$
 *
 */

using System;
using System.Collections.Generic;
using System.Text;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers.Baskets
{
  /// <summary>
  ///   Protection Pv CDX scaling factor calculator
  /// </summary>
  internal class CDXProtPvFactorCalc : SolverFn
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CDXProtPvFactorCalc));

    /// <summary>
    ///   Constructor
    /// </summary>
    public CDXProtPvFactorCalc(
      SurvivalCurve[] survivalCurves,
      bool relativeScaling,
      string[] tenorNamesScaled,
      double[] scalingFactors,
      string[] tenorNamesToScale,
      double[] scalingWeights,
      CDXPricer pricer)
    {
      checkWeights(scalingWeights);

      // Construct a set of curves to bump
      // Leave the survivalCurves untouched
      SurvivalCurve[] bumpedSurvivalCurves = new SurvivalCurve[survivalCurves.Length];
      for (int i = 0; i < survivalCurves.Length; i++)
        bumpedSurvivalCurves[i] = (SurvivalCurve)survivalCurves[i].Clone();

      // Scale the curves for the tenors we already know the factors
      if ((scalingFactors != null) && (scalingFactors.Length > 0))
      {
        CurveUtil.CurveBump(bumpedSurvivalCurves, tenorNamesScaled, scalingFactors, 
                            true, relativeScaling, true, scalingWeights);
        savedCurves_ = CloneUtil.Clone(bumpedSurvivalCurves);
        survivalCurves_ = bumpedSurvivalCurves;
      }
      else
      {
        savedCurves_ = survivalCurves;
        survivalCurves_ = bumpedSurvivalCurves;
      }

      // Set the data members
      relative_ = relativeScaling;
      tenorNamesToScale_ = tenorNamesToScale;
      scalingWeights_ = scalingWeights;
      pricer_ = pricer;
    }

    /// <summary>
    ///   Solve for a scaling factor
    /// </summary>
    /// <returns>evaluated objective function f(x)</returns>
    public static double Solve(
      CDXPricer pricer,
      SurvivalCurve[] survivalCurves,
      bool relativeScaling,
      string[] tenorNamesScaled,
      double[] scalingFactors,
      string[] tenorNamesToScale,
      double marketValue,
      double[] scalingWeights)
    {
      checkWeights(scalingWeights);

      // Set up root finder
      Brent2 rf = new Brent2();
      rf.setToleranceX(ToleranceX);
      rf.setToleranceF(ToleranceF);
      if (MaxIterations > 0)
        rf.setMaxIterations(MaxIterations);

      // remember original curves
      SurvivalCurve[] origCurves = pricer.SurvivalCurves;

      // Solve
      try
      {
        CDXProtPvFactorCalc fn = 
          new CDXProtPvFactorCalc(survivalCurves, relativeScaling, tenorNamesScaled, 
                                  scalingFactors, tenorNamesToScale, scalingWeights, pricer);
        pricer.SurvivalCurves = fn.survivalCurves_;
        double res = rf.solve(fn, marketValue, -0.2, 0.2);
        return res;
      }
      finally
      {
        pricer.SurvivalCurves = origCurves;
      }
    }

    /// <summary>
    ///  Compute protection pv of the CDX (sum of all survival curves) with x as the scaling factor
    /// </summary>
    /// <returns>evaluated objective function f(x)</returns>
    public override double evaluate(double x)
    {
      logger.DebugFormat("Trying factor {0}", x);

      CDXPricer pricer = pricer_;
      CalibratedCurve[] survivalCurves = survivalCurves_;
      string[] tenorsToScale = tenorNamesToScale_;
      double[] scalingWeights = scalingWeights_;

      // Bump up curves
      CurveUtil.CurveBump(survivalCurves, tenorsToScale, new double[] { x }, true, relative_, true, scalingWeights);

      // Calculate the protection pv of CDX
      // The protection pv of CDX is the weighted protection pv
      // of CDS pricer taking each individual survival surve
      //double protPv = pricer.ProtectionPv();
      double protPv = pricer.ExpectedLoss();
            
      // Restore curves
      CurveUtil.CurveRestoreQuotes(survivalCurves_, savedCurves_);

      // Return results scaled to percent of notional
      logger.DebugFormat("Returning index fair value {0} for factor {1}", protPv, x);

      return protPv;
    }

    private static void checkWeights(double[] scalingWeights)
    {
      double sumWeights = 0.0;
      for (int i = 0; i < scalingWeights.Length; i++)
        sumWeights += scalingWeights[i];

      if (Math.Abs(sumWeights) < 10.0 * Double.Epsilon)
        throw new ArgumentException("Scaling weights must not sum to zero.");
    }

    private SurvivalCurve[] survivalCurves_;
    private CalibratedCurve[] savedCurves_;
    private string[] tenorNamesToScale_;
    private double[] scalingWeights_;
    private CDXPricer pricer_;
    private bool relative_;

    /// <summary>
    ///   Tolerance of factor error
    /// </summary>
    public static double ToleranceX = 1E-6;

    /// <summary>
    ///   Tolerance of spread error
    /// </summary>
    public static double ToleranceF = 1E-8;

    /// <summary>
    ///   Maximum iterations allowed
    /// </summary>
    public static int MaxIterations = 30;
  }; // class CDXProtPvFactorCalc
}