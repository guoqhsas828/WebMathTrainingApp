/*
 * PricerTask.cs 
 *
 */

using System;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;


namespace BaseEntity.Toolkit.Sensitivity
{

  /// <exclude />
  [Serializable]
  public class CurveTaskState
  {
    /// <exclude />
    public CurveTaskState(PricerEvaluator[] evaluators,
      CalibratedCurve[] curves,
      CalibratedCurve[] dependentCurves,
      double[] initialBump,
      double[] upBump,
      double[] downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      bool calcHedge,
      string hedgeTenor,
      QuotingConvention targetQuoteType,
      bool evalAllTenors)
    {
      Evaluators = evaluators;
      Curves = curves;
      DependentCurves = dependentCurves;
      InitialBump = initialBump;
      UpBump = upBump;
      DownBump = downBump;
      BumpRelative = bumpRelative;
      ScaledDelta = scaledDelta;
      BumpType = bumpType;
      BumpTenors = bumpTenors;
      CalcGamma = calcGamma;
      CalcHedge = calcHedge;
      HedgeTenor = hedgeTenor;
      TargetQuoteType = targetQuoteType;
      EvalAllTenors = evalAllTenors;
    }

    /// <exclude />
    public CurveTaskState(PricerEvaluator[] evaluators,
      CalibratedCurve[] curves,
      CalibratedCurve[] dependentCurves,
      double[] initialBump,
      double[] upBump,
      double[] downBump,
      bool bumpRelative,
      bool scaledDelta,
      BumpType bumpType,
      string[] bumpTenors,
      bool calcGamma,
      bool calcHedge,
      string hedgeTenor,
      QuotingConvention targetQuoteType)
      : this(evaluators, curves, dependentCurves, initialBump, upBump, downBump, bumpRelative, scaledDelta, bumpType, bumpTenors, calcGamma, calcHedge,
      hedgeTenor, targetQuoteType, false)
    {}

    /// <exclude />
    public PricerEvaluator[] Evaluators;
    /// <exclude />
    public CalibratedCurve[] Curves;
    /// <exclude />
    public CalibratedCurve[] DependentCurves;
    /// <exclude />
    public double[] InitialBump;
    /// <exclude />
    public double[] UpBump;
    /// <exclude />
    public double[] DownBump;
    /// <exclude />
    public bool BumpRelative;
    /// <exclude />
    public bool ScaledDelta;
    /// <exclude />
    public BumpType BumpType;
    /// <exclude />
    public string[] BumpTenors;
    /// <exclude />
    public bool CalcGamma;
    /// <exclude />
    public bool CalcHedge;
    /// <exclude />
    public string HedgeTenor;
    /// <exclude />
    public QuotingConvention TargetQuoteType;
    /// <exclude />
    public bool EvalAllTenors;
  }

}

