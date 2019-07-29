/*
 * SurvivalDeltaCalculator.cs
 *
 *  -2008. All rights reserved. 
 *
 * $Id $
 *
 */

using System.Collections.Generic;

using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Sensitivity
{
  #region GenericCalculator

  /// <summary>
  ///   Generic calculator of bumped pvs
  /// </summary>
  /// <remarks>
  ///   <para>For internal use only.</para>
  /// </remarks>
  /// <exclude />
  public class SurvivalDeltaCalculator
  {
    // Logger
    internal static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(SurvivalDeltaCalculator));

    #region Constructors
    static readonly bool groupReset_ = true; // Introduce 9.1.2

    // These calcultors are stateless, so we can simply return a static copy.
    // It enables us to check if two pricers share the same delta calculator
    // and use this information for optimization.
    static readonly SurvivalDeltaCalculatorGroupReset groupCalculator_;
    static readonly SurvivalDeltaCalculatorCDOPricer cdoCalculator_;
    static readonly SurvivalDeltaCalculator genericCalculator_;

    static SurvivalDeltaCalculator()
    {
      groupCalculator_ = new SurvivalDeltaCalculatorGroupReset();
      cdoCalculator_ = new SurvivalDeltaCalculatorCDOPricer();
      genericCalculator_ = new SurvivalDeltaCalculator();
    }

    /// <summary>
    ///   Create survival delta calculator based on pricer type and price mesure
    /// </summary>
    ///
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">Price measure</param>
    ///
    /// <returns>Survival delta calculator</returns>
    ///
    public static SurvivalDeltaCalculator Create(IPricer pricer, string measure)
    {
      // TODO: Make CDOOptionPricer to use the fast routine
      if (pricer is SyntheticCDOPricer)
        return cdoCalculator_;
      if (measure != null && measure.Length != 0)
      {
        SurvivalBumpedEvalFn bumpedEval = SurvivalBumpedEvalFnBuilder.CreateDelegate(pricer.GetType(), measure);
        if (bumpedEval != null)
          return new SurvivalDeltaCalculatorDelegated(bumpedEval);
      }
      if (groupReset_)
        return groupCalculator_;
      return genericCalculator_;
    }
    #endregion Constructors

    /// <summary>
    ///   Perform parallel bumped evaluations
    /// </summary>
    /// 
    /// <param name="index">Index indicating the current pricer in the evaluators array</param>
    /// <param name="evaluators">Array of the evaluators</param>
    /// <param name="curves">Array of original curves</param>
    /// <param name="bumpedCurves">Array of bumped curves</param>
    /// <param name="savedCurves">Array of copyies of the original curves (to save us re-copying them) or null</param>
    /// <param name="dependentCurves">Array of dependent curves or null</param>
    /// <param name="savedDependentCurves">Array of copies of the original dependent curves or null</param>
    /// <param name="calculated">Array of booleans indicating if a pricer has been already evaluated</param>
    /// <param name="baseValues">Array of original (unbumped) MTMs (or null if calculate)</param>
    /// <param name="results">
    ///    A table of price values represented by a two dimensional array.
    ///    Each column indentifies a pricer, while row 0 contains the base or original values
    ///    and row i (i &gt; 0) contains the values when the curve i-1 is replaced
    ///    by its alternative.
    /// </param>
    /// <remarks>
    ///   This is a general routine applicable to all pricers.  It may be slow
    ///   since it takes no adavantage of the knowledge about the internal structure
    ///   of a pricer.
    /// </remarks>
    public virtual void Calculate(
      int index,
      PricerEvaluator[] evaluators,
      Curve[] curves,
      Curve[] bumpedCurves,
      Curve[] savedCurves,
      Curve[] dependentCurves,
      Curve[] savedDependentCurves,
      bool[] calculated,
      double[] baseValues,
      double[,] results)
    {
      double[] mtm = baseValues;
      int j = index;
      PricerEvaluator eval = evaluators[j];

      // compute the base case
      eval.Reset();
      results[0, j] = (mtm == null) ? eval.Evaluate() : mtm[j];
      logger.DebugFormat("Base price is {0}", results[0, j]);

      for (int i = 0; i < curves.Length; i++)
      {
        if (curves[i] == bumpedCurves[i] || !eval.DependsOn(curves[i]))
        {
          // Don't bother recalculating if the curve is unchanged or pricer does not use curve.
          results[i + 1, j] = results[0, j];
        }
        else
        {
          // Set bumped curve
          Set(curves[i], bumpedCurves[i]);
          SurvivalCalibrator savedCalibrator = null;
          double savedRecoverySpread = 0.0;
          if (eval.IncludeRecoverySensitivity
            && curves[i] is SurvivalCurve)
          {
            var sc = curves[i] as SurvivalCurve;
            savedCalibrator = sc.SurvivalCalibrator;
            if (savedCalibrator != null)
            {
              RecoveryCurve rc = savedCalibrator.RecoveryCurve;
              savedRecoverySpread = rc.Spread;
              var bsc = bumpedCurves[i] as SurvivalCurve;
              rc.Spread = bsc.SurvivalCalibrator.RecoveryCurve.Spread;
              if (eval.Basket != null)
                eval.Basket.ResetRecoveryRates();
              sc.Calibrator = bsc.Calibrator;
            }
          }

          try
          {
            // Recalibrate dependent curves if necessary
            if (dependentCurves != null)
              CurveUtil.CurveFit(dependentCurves);

            // Reprice
            eval.Reset();
            results[i + 1, j] = eval.Evaluate();
          }
          finally
          {
            // Restore bumped curve
            Set(curves[i], savedCurves[i]);
            if (savedCalibrator != null)
            {
              savedCalibrator.RecoveryCurve.Spread
                = savedRecoverySpread;
              ((SurvivalCurve)curves[i]).Calibrator = savedCalibrator;
            }
            if (savedDependentCurves != null)
              CurveUtil.CurveSet(dependentCurves, savedDependentCurves);
          }
        }
        logger.DebugFormat("Bumped price is {0}", results[i + 1, j]);
      }

      if (eval.IncludeRecoverySensitivity && eval.Basket != null)
        eval.Basket.ResetRecoveryRates();

      calculated[j] = true;
    }

    /// <summary>
    ///   Pick up bumped curves which match those found in a pricer
    /// </summary>
    /// 
    /// <param name="pricerCurves">Array of the curves found in pricer</param>
    /// <param name="curves">Array of all the original curves</param>
    /// <param name="bumpedCurves">Array of all the bumped curves</param>
    /// <param name="curveSlots">Array of slot to fill in matched postion</param>
    /// 
    /// <returns>Array of the bumped curves which match the curves found in pricer</returns>
    internal protected static SurvivalCurve[] FindMatchedCurves(
      SurvivalCurve[] pricerCurves,
      Curve[] curves,
      Curve[] bumpedCurves,
      int[] curveSlots
      )
    {
      // Match bumped survival curves to those in the Basket
      SurvivalCurve[] survivalCurves = new SurvivalCurve[pricerCurves.Length];
      pricerCurves.CopyTo(survivalCurves, 0);
      for (int k = 0; k < curves.Length; k++)
      {
        int match = pricerCurves.Length;
        if ((pricerCurves.Length == curves.Length) && (pricerCurves[k] == curves[k]))
          // Common case of bumped curve same order and size as basket curve
          match = k;
        else
          for (int k2 = 0; k2 < pricerCurves.Length; k2++)
          {
            if (curves[k] == pricerCurves[k2])
            {
              match = k2;
              break;
            }
          }
        if (match < pricerCurves.Length)
        {
          survivalCurves[match] = (SurvivalCurve)bumpedCurves[k];
          curveSlots[k] = match + 1;
        }
      }
      return survivalCurves;
    }

    /// <summary>
    ///   Copy curve states from one curve to another,
    ///   keeping calibrator unchanged.
    /// </summary>
    /// <param name="toCurve">The curve to change.</param>
    /// <param name="srcCurve">The curve to copy states from.</param>
    /// <returns>The modified curve.</returns>
    internal static void Set(Curve toCurve, Curve srcCurve)
    {
      CalibratedCurve cc = toCurve as CalibratedCurve;
      if (cc == null)
      {
        toCurve.Set(srcCurve);
        return;
      }
      Calibrator origCalibrator = cc.Calibrator;
      cc.Copy(srcCurve);
      if (cc.Calibrator is FxBasisFitCalibrator) return;
      cc.Calibrator = origCalibrator;
      return;
    }
  } // class SurvivalDeltaCalculator

  #endregion GenericCalculator

  #region DelegatedCaculator

  /// <summary>
  ///   Specialized calculator of bumped pvs for CDO pricers
  /// </summary>
  /// <remarks>
  ///   <para>For internal use only.</para>
  /// </remarks>
  /// <exclude />
  internal class SurvivalDeltaCalculatorDelegated : SurvivalDeltaCalculator
  {
    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="survivalBumpedEvalFn">Delegate of the bumped evaluation function</param>
    public SurvivalDeltaCalculatorDelegated(
      SurvivalBumpedEvalFn survivalBumpedEvalFn)
    {
      bumpedEval_ = survivalBumpedEvalFn;
    }

    /// <summary>
    ///   Perform parallel bumped evaluations
    /// </summary>
    /// 
    /// <param name="index">Index indicating the current pricer in the evaluators array</param>
    /// <param name="evaluators">Array of the evaluators</param>
    /// <param name="curves">Array of original curves</param>
    /// <param name="bumpedCurves">Array of bumped curves</param>
    /// <param name="savedCurves">Array of copyies of the original curves (to save us re-copying them) or null</param>
    /// <param name="dependentCurves">Array of dependent curves or null</param>
    /// <param name="savedDependentCurves">Array of copies of the original dependent curves or null</param>
    /// <param name="calculated">Array of booleans indicating if a pricer has been already evaluated</param>
    /// <param name="baseValues">Array of original (unbumped) MTMs (or null if calculate)</param>
    /// <param name="results">
    ///    A table of price values represented by a two dimensional array.
    ///    Each column indentifies a pricer, while row 0 contains the base or original values
    ///    and row i (i &gt; 0) contains the values when the curve i-1 is replaced
    ///    by its alternative.
    /// </param>
    /// <remarks>
    ///   This employs the special routine defined in the pricer.
    /// </remarks>
    public override void Calculate(
      int index,
      PricerEvaluator[] evaluators,
      Curve[] curves,
      Curve[] bumpedCurves,
      Curve[] savedCurves,
      Curve[] dependentCurves,
      Curve[] savedDependentCurves,
      bool[] calculated,
      double[] baseValues,
      double[,] results)
    {
      // Call specialized routine only for survival curves with no explicit dependency
      if (curves.Length == 0 || !(curves[0] is SurvivalCurve) || dependentCurves != null)
      {
        // Fall back to generic routine
        base.Calculate(index, evaluators, curves, bumpedCurves, savedCurves,
          dependentCurves, savedDependentCurves,
          calculated, baseValues, results);
        return;
      }

      int j = index;
      PricerEvaluator eval = evaluators[j];

      // Match bumped survival curves to those in the Basket
      int[] curveSlots = new int[curves.Length];
      SurvivalCurve[] pricerCurves = eval.SurvivalCurves;
      SurvivalCurve[] survivalCurves = FindMatchedCurves(
        pricerCurves, curves, bumpedCurves, curveSlots);

      logger.Debug("Using basket model sensitivities...");
      double[] tempResults = bumpedEval_(eval.Pricer, eval.MethodName, survivalCurves);
      if (tempResults == null)
      {
        // Fall back to generic routine
        base.Calculate(index, evaluators, curves, bumpedCurves, savedCurves,
          dependentCurves, savedDependentCurves,
          calculated, baseValues, results);
        return;
      }
      else if (tempResults.Length != survivalCurves.Length + 1)
      {
        throw new System.InvalidOperationException("The length of result is invalid");
      }

      results[0, j] = tempResults[0];
      for (int k = 0; k < curves.Length; k++)
      {
        int curveSlot = curveSlots[k];
        results[k + 1, j] = tempResults[curveSlot];
      }
      calculated[j] = true;
      return;
    }

    private readonly SurvivalBumpedEvalFn bumpedEval_;
  }

  #endregion DelegatedCalculator

  #region CDOPricerCalculator

  /// <summary>
  ///   Specialized calculator of bumped pvs for CDO pricers
  /// </summary>
  /// <remarks>
  ///   <para>For internal use only.</para>
  /// </remarks>
  /// <exclude />
  internal class SurvivalDeltaCalculatorCDOPricer : SurvivalDeltaCalculator
  {
    /// <summary>
    ///   Perform parallel bumped evaluations
    /// </summary>
    /// 
    /// <param name="index">Index indicating the current pricer in the evaluators array</param>
    /// <param name="evaluators">Array of the evaluators</param>
    /// <param name="curves">Array of original curves</param>
    /// <param name="bumpedCurves">Array of bumped curves</param>
    /// <param name="savedCurves">Array of copyies of the original curves (to save us re-copying them) or null</param>
    /// <param name="dependentCurves">Array of dependent curves or null</param>
    /// <param name="savedDependentCurves">Array of copies of the original dependent curves or null</param>
    /// <param name="calculated">Array of booleans indicating if a pricer has been already evaluated</param>
    /// <param name="baseValues">Array of original (unbumped) MTMs (or null if calculate)</param>
    /// <param name="results">
    ///    A table of price values represented by a two dimensional array.
    ///    Each column indentifies a pricer, while row 0 contains the base or original values
    ///    and row i (i &gt; 0) contains the values when the curve i-1 is replaced
    ///    by its alternative.
    /// </param>
    /// <remarks>
    ///   This employs the special routine defined in the Basket pricers, with a different
    ///   convention than other pricers.
    /// </remarks>
    public override void Calculate(
      int index,
      PricerEvaluator[] evaluators,
      Curve[] curves,
      Curve[] bumpedCurves,
      Curve[] savedCurves,
      Curve[] dependentCurves,
      Curve[] savedDependentCurves,
      bool[] calculated,
      double[] baseValues,
      double[,] results)
    {
      // Call specialized routine only for survival curves with no explicit dependency
      if (curves.Length == 0 || !(curves[0] is SurvivalCurve) || dependentCurves != null)
      {
        // Fall back to generic routine
        base.Calculate(index, evaluators, curves, bumpedCurves, savedCurves,
          dependentCurves, savedDependentCurves,
          calculated, baseValues, results);
        return;
      }

      int j = index;
      PricerEvaluator eval = evaluators[j];

      // Get all pricers matching this pricers basket.
      var basket = eval.Basket;
      var discountCurve = eval.DiscountCurve;
      var matchingCDOPricers = new List<PricerEvaluator>();
      var cdoSlots = new List<int>();
      matchingCDOPricers.Add(eval);
      cdoSlots.Add(j);

      for (int j2 = j + 1; j2 < evaluators.Length; j2++)
      {
        PricerEvaluator p = evaluators[j2];
        if (p != null && p.Basket == basket && p.DiscountCurve == discountCurve)
        {
          matchingCDOPricers.Add(p);
          cdoSlots.Add(j2);
        }
      }

      var matchingPricers = matchingCDOPricers.ToArray();
      var withRecoverySensitivity = matchingPricers[0].IncludeRecoverySensitivity;

      // Match bumped survival curves to those in the Basket
      int[] curveSlots = new int[curves.Length];
      var basketCurves = basket.GetSurvivalCurves(withRecoverySensitivity);
      var survivalCurves = FindMatchedCurves(
        basketCurves, curves, bumpedCurves, curveSlots);

      logger.Debug("Using basket model sensitivities...");
      double[,] tempResults = basket.BumpedPvs(matchingPricers,
        survivalCurves, withRecoverySensitivity);
      if (tempResults == null)
      {
        // Fall back to generic routine
        base.Calculate(index, evaluators, curves, bumpedCurves, savedCurves,
          dependentCurves, savedDependentCurves, calculated, baseValues, results);
        return;
      }
      else if (tempResults.GetLength(0) != survivalCurves.Length + 1)
      {
        throw new System.InvalidOperationException("The length of result is invalid");
      }

      for (int j2 = 0; j2 < matchingPricers.Length; j2++)
      {
        int idx = cdoSlots[j2];
        calculated[idx] = true;
        results[0, idx] = tempResults[0, j2];
        for (int k = 0; k < curves.Length; k++)
          results[k + 1, idx] = tempResults[curveSlots[k], j2];
      }

      return;
    }

  } // class SurvivalDeltaCalculatorCDOPricer

  #endregion CDOPricerCalculator

  #region SurvivalDeltaCalculatorGroupReset
  /// <summary>
  ///   Group reset calculator
  /// </summary>
  /// <remarks>
  ///   <para>For internal use only.</para>
  /// 
  ///   <para>This class groups all the pricers with the same delta calculator together,
  ///    and always perform calculations and resets in group.  That is, when need recalculate,
  ///    it first invokes Reset() on all pricers, followed by the call of Evaluate() on them.
  ///    So if there is any shared calculation among the pricers within the group, it will
  ///    not be interrupted by reset.</para>
  /// 
  ///   <para>It uses the fact that all the pricers within a group share the
  ///    same delta calculator.</para>
  /// </remarks>
  internal class SurvivalDeltaCalculatorGroupReset : SurvivalDeltaCalculator
  {
    /// <summary>
    ///   Perform parallel bumped evaluations
    /// </summary>
    /// 
    /// <param name="index">Index indicating the current pricer in the evaluators array</param>
    /// <param name="evaluators">Array of the evaluators</param>
    /// <param name="curves">Array of original curves</param>
    /// <param name="bumpedCurves">Array of bumped curves</param>
    /// <param name="savedCurves">Array of copyies of the original curves (to save us re-copying them) or null</param>
    /// <param name="dependentCurves">Array of dependent curves or null</param>
    /// <param name="savedDependentCurves">Array of copies of the original dependent curves or null</param>
    /// <param name="calculated">Array of booleans indicating if a pricer has been already evaluated</param>
    /// <param name="baseValues">Array of original (unbumped) MTMs (or null if calculate)</param>
    /// <param name="results">
    ///    A table of price values represented by a two dimensional array.
    ///    Each column indentifies a pricer, while row 0 contains the base or original values
    ///    and row i (i &gt; 0) contains the values when the curve i-1 is replaced
    ///    by its alternative.
    /// </param>
    /// <remarks>
    ///   This employs the special routine defined in the Basket pricers, with a different
    ///   convention than other pricers.
    /// </remarks>
    public override void Calculate(
      int index,
      PricerEvaluator[] evaluators,
      Curve[] curves,
      Curve[] bumpedCurves,
      Curve[] savedCurves,
      Curve[] dependentCurves,
      Curve[] savedDependentCurves,
      bool[] calculated,
      double[] baseValues,
      double[,] results)
    {
      int start = index;

      // Collect all the pricers with the same delta calculator
      // and recovery sensitivity specification.
      bool[] includes = new bool[evaluators.Length];
      includes[start] = true;
      bool includeRecoverySensitivity = evaluators[start].IncludeRecoverySensitivity;
      for (int j = start + 1; j < evaluators.Length; ++j)
      {
        if ((!calculated[j]) && evaluators[j].SurvivalBumpedEval == this
          && evaluators[j].IncludeRecoverySensitivity == includeRecoverySensitivity)
        {
          includes[j] = true;
        }
      }

      // Calculate or copy the base case values
      double[] mtm = baseValues;
      if (mtm == null)
      {
        // Reset
        for (int j = start; j < evaluators.Length; ++j)
        {
          if (includes[j]) evaluators[j].Reset();
        }
        // Calculate
        for (int j = start; j < evaluators.Length; ++j)
        {
          if (includes[j])
          {
            results[0, j] = evaluators[j].Evaluate();
            logger.DebugFormat("Base price is {0}", results[0, j]);
          }
        }
      }
      else
      {
        // Copy
        for (int j = start; j < mtm.Length; ++j)
        {
          if (includes[j])
          {
            results[0, j] = mtm[j];
            logger.DebugFormat("Base price is {0}", results[0, j]);
          }
        }
      }

      // Compute the bumped case values
      for (int i = 0; i < curves.Length; i++)
      {
        // Don't bother recalculating if the curve is unchanged
        if (curves[i] == bumpedCurves[i])
        {
          for (int j = start; j < includes.Length; ++j)
          {
            if (includes[j])
              results[i + 1, j] = results[0, j];
          }
          continue;
        }

        // We need a new array of booleans
        bool[] wantcalc = new bool[includes.Length];
        for (int j = start; j < includes.Length; ++j)
          wantcalc[j] = includes[j];

        // Check the dependence of pricers on curve i
        bool all_depend = true;
        for (int j = start; j < wantcalc.Length; ++j)
        {
          if (wantcalc[j])
          {
            // If pricer j does not depends on curve i,
            // don't bother recalculating.
            if (!evaluators[j].DependsOn(curves[i]))
            {
              results[i + 1, j] = results[0, j];
              wantcalc[j] = false;
            }
            else
            {
              // Indicate that at least one pricer needs recalculate
              all_depend = false;
            }
          }
        }
        if (all_depend) continue;

        // Set bumped curve
        Set(curves[i], bumpedCurves[i]);        
        SurvivalCalibrator savedCalibrator = null;
        double savedRecoverySpread = 0.0;
        if (includeRecoverySensitivity
          && curves[i] is SurvivalCurve)
        {
          var sc = curves[i] as SurvivalCurve;
          savedCalibrator = sc.SurvivalCalibrator;
          if (savedCalibrator != null)
          {
            RecoveryCurve rc = ((SurvivalCalibrator)savedCalibrator).RecoveryCurve;
            savedRecoverySpread = rc.Spread;
            var bsc = bumpedCurves[i] as SurvivalCurve;
            rc.Spread = bsc.SurvivalCalibrator.RecoveryCurve.Spread;
            for (int j = start; j < evaluators.Length; ++j)
            {
              if (wantcalc[j] && evaluators[j].Basket != null)
              {
                evaluators[j].Basket.ResetRecoveryRates();
              }
            }
            sc.Calibrator = bsc.Calibrator;
          }
        }
        else if (curves[i] is RecoveryCurve)
        {
          savedRecoverySpread = curves[i].Spread;
          curves[i].Spread = bumpedCurves[i].Spread;
        }

        try
        {
          // Recalibrate dependent curves if necessary
          if (dependentCurves != null)
            CurveUtil.CurveFit(dependentCurves);

          // Reset
          for (int j = start; j < evaluators.Length; ++j)
          {
            if (wantcalc[j]) evaluators[j].Reset();
          }
          // Reprice
          for (int j = start; j < evaluators.Length; ++j)
          {
            if (wantcalc[j])
              results[i + 1, j] = evaluators[j].Evaluate();
          }
        }
        finally
        {
          // Restore bumped curve
           Set(curves[i], savedCurves[i]);          
          if (savedCalibrator != null)
          {
            ((SurvivalCalibrator)savedCalibrator).RecoveryCurve.Spread
              = savedRecoverySpread;
            ((SurvivalCurve)curves[i]).Calibrator = savedCalibrator;
          }
          else if (curves[i] is RecoveryCurve)
            curves[i].Spread = savedRecoverySpread;
          if (savedDependentCurves != null)
            CurveSet(dependentCurves, savedDependentCurves);
        }

        for (int j = start; j < evaluators.Length; ++j)
          logger.DebugFormat("Bumped price is {0}", results[i + 1, j]);
      }

      // Clean up
      for (int j = start; j < evaluators.Length; ++j)
      {
        if (includes[j])
        {
          // Mark claculated
          calculated[j] = true;

          // Reset recovery
          if (evaluators[j].Basket != null && includeRecoverySensitivity)
            evaluators[j].Basket.ResetRecoveryRates();
        }
      }

      return;
    }

    static void CurveSet(Curve[] curves, Curve[] srcCurves)
    {
      Timer timer = new Timer();
      timer.start();

      for (int i = 0; i < curves.Length; i++)
      {
        if (srcCurves[i] == null)
        {
          curves[i] = null;
          continue;
        }
        if (curves[i] == null)
        {
          curves[i] = new Curve(srcCurves[i]);
          continue;
        }
        var ccurve = curves[i] as CalibratedCurve;
        if(ccurve==null || ccurve.Calibrator is IndirectionCalibrator)
        {
          curves[i].Set(srcCurves[i]);
          continue;
        }
        ccurve.Copy(srcCurves[i]);
      }

      timer.stop();
      logger.DebugFormat("Set curves in {0}s", timer.getElapsed());

      return;
    }
  } // class SurvivalDeltaCalculatorGroupReset
  #endregion SurvivalDeltaCalculatorGroupReset
}
