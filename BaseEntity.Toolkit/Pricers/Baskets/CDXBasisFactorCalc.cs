/*
 * CDXBasisFactorCalc.cs
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
  ///   Basis factor calculator
  /// </summary>
  internal class CDXBasisFactorCalc : SolverFn
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CDXBasisFactorCalc));

    /// <summary>
    ///   Constructor
    /// </summary>
    public CDXBasisFactorCalc(
      SurvivalCurve[] survivalCurves,
      bool relativeScaling,
      string[] tenorNamesScaled,
      double[] scalingFactors,
      string[] tenorNamesToScale,
      double[] scalingWeights,
      CDXPricer pricer)
    {
      checkWeights(scalingWeights);

      // construct a set of curves to bump
      SurvivalCurve[] bumpedSurvivalCurves = new SurvivalCurve[survivalCurves.Length];
      for (int i = 0; i < survivalCurves.Length; i++)
        bumpedSurvivalCurves[i] = (SurvivalCurve)survivalCurves[i].Clone();

      // scale the curves for the tenors we already know the factors
      if ((null != scalingFactors) && (scalingFactors.Length > 0))
      {
        CurveUtil.CurveBump(bumpedSurvivalCurves, tenorNamesScaled,
                             scalingFactors, true, relativeScaling, true, scalingWeights);
        savedCurves_ = CloneUtil.Clone(bumpedSurvivalCurves);
        survivalCurves_ = bumpedSurvivalCurves;
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
      //
      Brent rf = new Brent();
      rf.setToleranceX(ToleranceX);
      rf.setToleranceF(ToleranceF);
      if (MaxIterations > 0)
        rf.setMaxIterations(MaxIterations);

      // remember original curves
      SurvivalCurve[] origCurves = pricer.SurvivalCurves;
      // Solve
      try
      {
        CDXBasisFactorCalc fn = new CDXBasisFactorCalc(survivalCurves, relativeScaling,
          tenorNamesScaled, scalingFactors, tenorNamesToScale, scalingWeights, pricer);
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
    ///  Compute duration weighted spread with x as the scaling factor
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
      // Calculate full intrinsic value
      double fv = pricer.IntrinsicValue(true);
      // Restore curves
      CurveUtil.CurveRestoreQuotes(survivalCurves_, savedCurves_);

      // Return results scaled to percent of notional
      logger.DebugFormat("Returning index fair value {0} for factor {1}", fv, x);

      return fv;
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
    public static double ToleranceX = 1E-2;

    /// <summary>
    ///   Tolerance of spread error
    /// </summary>
    public static double ToleranceF = 1E-6;

    /// <summary>
    ///   Maximum iterations allowed
    /// </summary>
    public static int MaxIterations = 0;
  }; // class CDXBasisFactorCalc

}
