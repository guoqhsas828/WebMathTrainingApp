/*
 *  -2013. All rights reserved.
 * Partial implementation of the scenario sensitivity functions
 */

using System;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Shared.Dynamic;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Sensitivity
{
  ///
  /// <summary>
  ///   
  /// </summary>
  //Methods for calculating generalized sensitivity measures
  public static partial class Sensitivities
  {

    #region Obsolete

    /// <summary>
    ///   Calculate the changes of prices in a scenario
    /// </summary>
    /// 
    /// <remarks>
    ///  <para>This function calcuates the changes in price meansures due to any or a combination
    ///   of changes in credit spreads, recovery rates, interest rates and correlations.</para>
    /// 
    ///  <para>The use must provide <paramref name="survBumps">survival surves</paramref>
    ///  for any bump of credit spreads, recovery rates, and interest rates with recalibration.
    ///  It can be empty if only correlations are bumped or if interest rates are bumped without
    ///  recalibration.</para>
    /// 
    ///  <para>The parameter <paramref name="survBumps"/> specifies how the credit curves are
    ///  modfied, the elements of which can be either the word "defaulted" (to mark the curve
    ///  as defaulted), basis points (if <paramref name="survBumpRelative"/> is false)
    ///  or percentage (if <paramref name="survBumpRelative"/> is true).</para>
    /// </remarks>
    /// 
    /// <param name="pricers">Array of pricers</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="survCurves">Array of N original survival curves.</param>
    /// <param name="survBumps">Array of N curve shift factors or "default" to indicate credit curve default"</param>
    /// <param name="survBumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="forceFit">Set to true to help fit curves that break due to bumping</param>
    /// <param name="recoveryBumps">Array of N recovery shifts in absolute terms</param>
    /// <param name="corrs">Array of correlations to bump or null for all underlying correlations</param>
    /// <param name="corrBumps">Array of correlation bump sizes or null for no bump</param>
    /// <param name="irCurves">Array of M DiscountCurves to bump or null for all underlying discount curves</param>
    /// <param name="irBumps">Array of M bump sizes for discount curves or null for no bump</param>
    /// <param name="irBumpRelative">If true, discount bump sizes are relative instead of absolute</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="rescaleStrikes">Rescale strikes or not</param>
    /// <param name="idxBump">Array of M bump sizes for index spreads</param>
    /// <param name="idxTenor">Th tenor of the index to be bumped</param>
    /// <param name="indexScalingCalibrator">Scaling calibrator</param>
    /// 
    /// <returns>Array of changes in prices</returns>
    /// 
    [Obsolete]
    public static double[] Scenario(
      IPricer[] pricers,
      string measure,
      SurvivalCurve[] survCurves,
      object[] survBumps,
      bool survBumpRelative,
      bool forceFit,
      double[] recoveryBumps,
      CorrelationObject[] corrs,
      object corrBumps,
      DiscountCurve[] irCurves,
      double[] irBumps,
      bool irBumpRelative,
      bool recalibrate,
      bool[] rescaleStrikes,
      double idxBump,
      string idxTenor,
      IndexScalingCalibrator indexScalingCalibrator
      )
    {
      return Scenario(CreateAdapters(pricers, measure, true, true), survCurves, survBumps, survBumpRelative,
        forceFit, recoveryBumps, corrs, corrBumps, irCurves, irBumps, irBumpRelative, recalibrate,
        null, null, null, false, rescaleStrikes, idxBump, idxTenor, indexScalingCalibrator);
    }

    /// <summary>
    ///   Calculate the changes of prices in a scenario
    /// </summary>
    /// <param name="evaluators">Array of pricer evaluators</param>
    /// <param name="survCurves">Array of N original survival curves.</param>
    /// <param name="survBumps">Array of N curve shift factors or "default" to indicate credit curve default"</param>
    /// <param name="survBumpRelative">Bump sizes are relative rather than absloute</param>
    /// <param name="forceFit">Set to true to help fit curves that break due to bumping</param>
    /// <param name="recoveryBumps">Array of N recovery shifts in absolute terms</param>
    /// <param name="corrs">Array of correlations to bump or null for all underlying correlations</param>
    /// <param name="corrBumps">Array of correlation bump sizes or null for no bumps</param>
    /// <param name="irCurves">Array of M DiscountCurves to bump or null for all underlying discount curves</param>
    /// <param name="irBumps">Array of M bump sizes for discount curves or null for no bumps</param>
    /// <param name="irBumpRelative">If true, discount bump sizes are relative instead of absolute</param>
    /// <param name="recalibrate">Recalibrate survival curves after changing rate curves</param>
    /// <param name="fxCurves">Array of K FxCurves to bump</param>
    /// <param name="spotFxBumps">Array of K spot Fx bumps</param>
    /// <param name="basisCurveBumps">Array of K basis quote bumps </param>
    /// <param name="fxBumpRelative">If true, fx and basis curve bumps are relative instead of absolute</param>
    /// <param name="rescaleStrikes">Rescale strikes or not</param>
    /// <param name="idxBump">Bump size for index spreads</param>
    /// <param name="idxTenor">Bump tenor for index spreads</param>
    /// <param name="indexScalingCalibrator">Calibrator</param>
    /// <returns>Array of changes in prices</returns>
    [Obsolete]
    public static double[] Scenario(
      PricerEvaluator[] evaluators,
      SurvivalCurve[] survCurves,
      object[] survBumps,
      bool survBumpRelative,
      bool forceFit,
      double[] recoveryBumps,
      CorrelationObject[] corrs,
      object corrBumps,
      DiscountCurve[] irCurves,
      double[] irBumps,
      bool irBumpRelative,
      bool recalibrate,
      FxCurve[] fxCurves,
      double[] spotFxBumps,
      double[] basisCurveBumps,
      bool fxBumpRelative,
      bool[] rescaleStrikes,
      double idxBump,
      string idxTenor,
      IndexScalingCalibrator indexScalingCalibrator
      )
    {
      CalibratedCurve[] savedIrCurves = null;
      Tuple<CalibratedCurve, double>[] savedFxCurves = null;
      bool defaultChanged = false;
      bool recoveryBumped = false;
      CalibratedCurve[] savedSurvCurves = null;
      bool correlationBumped = false;
      CorrelationObject[] savedCorrs = null;
      double[] results;
      bool[] rescaleStrikesSaved = ResetRescaleStrikes(evaluators, rescaleStrikes);
      MarketQuote[] savedQuotes = null;
      double[] savedSpread = new double[evaluators.Length];
      double[] savedScalingFactors = null;
      var fixers = PricerLockStrikes(evaluators);
      try
      {
        // calculate the base values
        results = new double[evaluators.Length];
        for (int i = 0; i < evaluators.Length; ++i)
        {
          if (idxBump == 0 || indexScalingCalibrator == null)
            results[i] = evaluators[i].Evaluate();
          else
          {
            results[i] = ((ICreditIndexOptionPricer)evaluators[i].Pricer).MarketValue();
            savedSpread[i] = ((ICreditIndexOptionPricer)evaluators[i].Pricer).GetIndexSpread();
          }
        }
        // Bump IR curves
        if (irCurves != null && irCurves.Length > 0)
        {
          savedIrCurves = CloneUtil.Clone(irCurves);
          BumpIrCurves(irCurves, irBumps, irBumpRelative);
        }
        else
          recalibrate = false;

        // Handle FxCurves
        if (fxCurves != null && fxCurves.Length > 0)
        {
          savedFxCurves =
            fxCurves.Select(
              c =>
                new Tuple<CalibratedCurve, double>(
                  c.IsSupplied ? CloneUtil.Clone(c.GetComponentCurves<FxForwardCurve>(null).FirstOrDefault()) : CloneUtil.Clone(c.BasisCurve), c.SpotRate)).
              ToArray();
          BumpFxCurves(fxCurves, spotFxBumps, basisCurveBumps, fxBumpRelative);
        }

        // Bump survival curves and recoveries
        if (survCurves != null && survCurves.Length > 0)
        {
          recoveryBumped = (recoveryBumps != null && ((recoveryBumps.Length == 1 && recoveryBumps[0] != 0) || recoveryBumps.Length > 1));
          if (recoveryBumped)
            savedSurvCurves = CurveUtil.CurveCloneWithRecovery(survCurves);
          else
            savedSurvCurves = CloneUtil.Clone(survCurves);

          bool scaleCurveRelative = survBumpRelative;
          // bump index level
          if (idxBump != 0 && indexScalingCalibrator != null)
          {
            savedQuotes = CloneUtil.Clone(indexScalingCalibrator.Quotes);
            savedScalingFactors = CloneUtil.Clone(indexScalingCalibrator.GetScalingFactors());
            var scalingFactors = indexScalingCalibrator.GetScalingFactors();
            for (int i = 0; i < indexScalingCalibrator.Quotes.Length; ++i)
            {
              if (indexScalingCalibrator.TenorNames[i] == idxTenor)
              {
                if (survBumpRelative)
                  indexScalingCalibrator.Quotes[i].Value *= (1 + idxBump);
                else
                  indexScalingCalibrator.Quotes[i].Value += idxBump;
                indexScalingCalibrator.ReScale(survCurves);
                if (survBumps == null || survBumps.Length == 0)
                  survBumps = new object[survCurves.Length];
                for (int j = 0; j < survBumps.Length; ++j)
                  survBumps[j] = scalingFactors[i] - savedScalingFactors[i];
              }
            }
            scaleCurveRelative = (indexScalingCalibrator.ScalingType == BumpMethod.Relative) ? true : false;
          }
          // bump individual names
          Dt settle = survBumps != null ? LastSettle(evaluators) : Dt.Empty;
          defaultChanged = BumpSurvivalCurves(settle, null, survCurves, survBumps, scaleCurveRelative, forceFit, recoveryBumps, recalibrate);
        }

        // Bump the correlations
        if (corrs != null && corrs.Length > 0 && corrBumps != null)
        {
          savedCorrs = CorrelationClone(corrs);
          BumpCorrelation(corrs, corrBumps);
          correlationBumped = corrs[0].Modified;
        }
        if (correlationBumped)
        {
          // Act as if rescaleStrikes = true
          PricerUnlockStrikes(fixers);
          fixers = null;
        }

        // Reset price 
        PricerResetOriginalBasket(evaluators,
          defaultChanged, recoveryBumped, correlationBumped);

        // Calculate deltas
        for (int i = 0; i < evaluators.Length; ++i)
        {
          var icdxoPricer = evaluators[i].Pricer as ICreditIndexOptionPricer;
          if (icdxoPricer != null)
          {
            if (idxBump != 0)
            {
              var newSpread = survBumpRelative
                ? ((1 + idxBump) * savedSpread[i])
                : (savedSpread[i] + idxBump);
              var cdxoPricer = icdxoPricer as CDXOptionPricer;
              if (cdxoPricer != null)
                cdxoPricer.Spread = newSpread;
              else
                icdxoPricer = icdxoPricer.Update(new MarketQuote(
                  newSpread, QuotingConvention.CreditSpread));
              results[i] = icdxoPricer.MarketValue() - results[i];
            }
            else
            {
              results[i] = evaluators[i].Evaluate() - results[i];
            }
          }
          else
          {
            results[i] = evaluators[i].Evaluate() - results[i];
          }
        }
      }
      finally
      {
        // Restore IR curves
        if (savedIrCurves != null)
        {
          CurveUtil.CurveSet(irCurves, savedIrCurves);
        }

        // Restore FxCurves
        if (savedFxCurves != null)
        {
          for (int i = 0; i < savedFxCurves.Length; ++i)
          {
            fxCurves[i].SpotFxRate.Rate = savedFxCurves[i].Item2;
            if (savedFxCurves[i].Item1 == null)
              continue;
            if (fxCurves[i].IsSupplied)
            {
              var fxFwdCurve = fxCurves[i].GetComponentCurves<FxForwardCurve>(null).First();
              new[] { fxFwdCurve }.CurveSet(new[] { savedFxCurves[i].Item1 });
            }
            else
            {
              var basisCurve = fxCurves[i].BasisCurve;
              new[] { basisCurve }.CurveSet(new[] { savedFxCurves[i].Item1 });
            }
          }
        }
        // Restore survival curves
        if (savedSurvCurves != null)
        {
          CurveUtil.CurveRestoreWithRecovery(survCurves, savedSurvCurves);
        }
        // Restore correlation
        if (savedCorrs != null)
        {
          RestoreCorrelation(corrs, savedCorrs);
        }
        if (indexScalingCalibrator != null && savedQuotes != null)
        {
          indexScalingCalibrator.Quotes = savedQuotes;
          indexScalingCalibrator.SetScalingFactors(savedScalingFactors);
          for (int i = 0; i < evaluators.Length; ++i)
          {
            var cdxoPricer = evaluators[i].Pricer as CDXOptionPricer;
            if (cdxoPricer != null) cdxoPricer.Spread = savedSpread[i];
          }
        }

        // Reset price
        PricerResetOriginalBasket(evaluators,
          defaultChanged, recoveryBumped, correlationBumped);

        PricerUnlockStrikes(fixers);
        ResetRescaleStrikes(evaluators, rescaleStrikesSaved);
      }

      // return the results
      return results;
    }

    /// <summary>
    /// 
    /// </summary>
    private static IDisposable[] PricerLockStrikes(PricerEvaluator[] pricers)
    {
      var fixers = new IDisposable[pricers.Length];
      for (int i = 0; i < pricers.Length; i++)
      {
        var basket = pricers[i].Basket as BaseCorrelationBasketPricer;
        if (basket == null || basket.RescaleStrike) continue;
        fixers[i] = basket.LockCorrection();
      }
      return fixers;
    }

    /// <summary>
    /// 
    /// </summary>
    private static void PricerUnlockStrikes(IDisposable[] fixers)
    {
      if (fixers == null) return;
      for (int i = 0; i < fixers.Length; i++)
      {
        if (fixers[i] != null) fixers[i].Dispose();
      }
    }

    /// <summary>
    /// 
    /// </summary>
    private static void PricerResetOriginalBasket(
      PricerEvaluator[] evaluators,
      bool defaultChanged,
      bool recoveryModified,
      bool correlationModified)
    {
      if (evaluators == null)
        return;
      for (int i = 0; i < evaluators.Length; ++i)
        if (evaluators[i] != null)
        {
          var saved = evaluators[i].SensitivityFlags;
          if (defaultChanged)
            evaluators[i].SensitivityFlags |= PricerEvaluator.DefaultChangedFlag;
          evaluators[i].Reset(recoveryModified, correlationModified);
          evaluators[i].SensitivityFlags = saved;
        }
      return;
    }

    private static CorrelationObject[] CorrelationClone(
      CorrelationObject[] corr)
    {
      return CloneUtil.Clone(corr);
    }

    private static void BumpCorrelation(CorrelationObject[] corrs, object bumps)
    {
      if (bumps is double[])
      {
        BumpCorrelation(corrs, (double[])bumps);
        return;
      }

      if (bumps is CorrelationObject[])
      {
        BumpCorrelation(corrs, (CorrelationObject[])bumps);
        return;
      }

      throw new ArgumentException("Correlation bumps must either be an array of numbers or an array of base correlation objects");
    }

    private static void BumpCorrelation(CorrelationObject[] corrs, double[] bumps)
    {
      for (int i = 0; i < corrs.Length; ++i)
        if (corrs[i] != null)
        {
          double bump = (bumps.Length == 1 ? bumps[0] : bumps[i]);
          if (bump != 0)
          {
            corrs[i].BumpCorrelations(bump, false, false);
            corrs[i].Modified = true;
          }
        }
      return;
    }

    private static void BumpCorrelation(CorrelationObject[] corrs, CorrelationObject[] bumps)
    {
      if (bumps.Length != corrs.Length)
        throw new ArgumentException("The length of corrs and bumps not match");
      for (int i = 0; i < corrs.Length; ++i)
        if (corrs[i] != null)
        {
          corrs[i].SetCorrelations(bumps[i]);
          corrs[i].Modified = true;
        }
      return;
    }

    private static void RestoreCorrelation(
      CorrelationObject[] corrs, CorrelationObject[] savedCorrs)
    {
      for (int i = 0; i < corrs.Length; ++i)
        if (corrs[i] != null)
        {
          corrs[i].SetCorrelations(savedCorrs[i]);
          corrs[i].Modified = savedCorrs[i].Modified;
        }
      return;
    }

    /// <summary>
    /// </summary>
    public static bool BumpSurvivalCurves(
      Dt settle,
      string tenor,
      SurvivalCurve[] survCurves,
      object[] survBumps,
      bool bumpRelative,
      bool forceFit,
      double[] recoveryBumps,
      bool alwayRefit)
    {
      bool defaultChanged = false;
      for (int i = 0; i < survCurves.Length; ++i)
        if (survCurves[i] != null)
        {
          SurvivalCurve curve = survCurves[i];
          SurvivalCalibrator calibrator = curve.SurvivalCalibrator;
          if (calibrator == null)
            throw new ArgumentException(String.Format("The curve '{0}' is not a calibrated curve", curve.Name));

          bool isAlive = curve.Defaulted == Defaulted.NotDefaulted;
          bool needRefit = alwayRefit;
          if (recoveryBumps != null && recoveryBumps.Length > 0)
          {
            double rBump = (recoveryBumps.Length == 1 ? recoveryBumps[0] : recoveryBumps[i]);
            RecoveryCurve rc = calibrator.RecoveryCurve;
            if (rc != null)
            {
              // Bump recovery rate ONLY when one of the following holds:
              //  (1) It is alive;
              //  (2) It will default in the future (as in hypothetical scenario analysis);
              //  (3) It is explicitly marked that the default will settle in the future.
              // Note: point (3) excludes the case where the Default Settle Date is empty.
              if (rBump != 0 && (isAlive || curve.Defaulted == Defaulted.WillDefault
                || rc.Recovered == Recovered.WillRecover))
              {
                rc.Spread += rBump;
                needRefit = isAlive; // Refit only when the curve is alive.
              }
            }
          }

          // Bump the curve or set it defaulted ONLY when it is alive.
          if (survBumps != null && survBumps.Length > 0 && isAlive)
          {
            object sBump = (survBumps.Length == 1 ? survBumps[0] : survBumps[i]);
            if (sBump is string && String.Compare((string)sBump, "defaulted", true) == 0)
            {
              curve.DefaultDate = settle;
              curve.Defaulted = Defaulted.WillDefault;
              defaultChanged = true;
            }
            else
            {
              double bump = NumericUtils.ToDouble(sBump, 0.0, true);
              // bump the curve
              if (bump != 0.0)
                CurveUtil.CurveBump(curve, tenor, bump, true, bumpRelative, true);
            }

            // Indicate that we have already refitted the survival.
            needRefit = false;
          }

          // If we bumped recovery but not the survival spreads,
          // here is the place to do a refit of survival curve
          // with the bumped recoveries.
          if (needRefit)
            curve.ReFit(0);
        }

      return defaultChanged;
    }

    private static void BumpIrCurves(
      DiscountCurve[] irCurves,
      double[] irBumps,
      bool bumpRelative)
    {
      for (int i = 0; i < irCurves.Length; ++i)
        if (irCurves[i] != null)
        {
          double bump = (irBumps.Length == 1 ? irBumps[0] : irBumps[i]);
          DiscountCurve curve = irCurves[i];
          CurveUtil.CurveBump(curve, null, bump, true, bumpRelative, true);
        }

      return;
    }


    private static void BumpFxCurves(
      FxCurve[] fxCurves,
      double[] spotFxBump,
      double[] basisBumps,
      bool bumpRelative)
    {
      for (int i = 0; i < fxCurves.Length; ++i)
      {
        if (fxCurves[i] != null)
        {
          double spotBump = (spotFxBump.Length == 1) ? spotFxBump[0] : spotFxBump[i];
          var fxCurve = fxCurves[i];
          CurveUtil.CurveBump(fxCurve, fxCurve.Tenors.Where(t => t.Product is FxForward).OrderBy(t => t.Maturity).First().Name, spotBump, true, bumpRelative,
            true);
          if (fxCurve.BasisCurve != null)
          {
            double basisBump = (basisBumps.Length == 1) ? basisBumps[0] : basisBumps[i];
            CurveUtil.CurveBump(fxCurve.BasisCurve, null, basisBump, true, bumpRelative, true);
          }
        }
      }
    }

    /// <summary>
    /// </summary>
    public static double Valuation(
      IPricer pricer,
      string measure
      )
    {
      PricerEvaluator pricerEvaluator = new PricerEvaluator(pricer, measure, true, true);
      return pricerEvaluator.Evaluate();
    }

    /// <summary>
    /// </summary>
    public static double Valuation(
      IPricer pricer,
      string measure,
      ScenarioShiftGreek shiftGreek
     )
    {
      PricerEvaluator pricerEvaluator = CreateCDXOptionEvaluator(pricer, measure, shiftGreek);
      return pricerEvaluator.Evaluate();
    }

    private readonly static string[] GreekMeasures = { "Delta", "Gamma", "Theta", "Vega" };
    private static bool IsGreekMesure(string measure)
    {
      for (int i = 0; i < GreekMeasures.Length; ++i)
      {
        if (GreekMeasures[i] == measure)
          return true;
      }
      return false;
    }

    /// <summary>
    /// </summary>
    public static PricerEvaluator CreateCDXOptionEvaluator(IPricer pricer, string measure, ScenarioShiftGreek shiftGreek)
    {
      if (!IsGreekMesure(measure))
        return new PricerEvaluator(pricer, measure, true, true);

      var cdxoPricer = pricer as ICreditIndexOptionPricer;
      if (cdxoPricer == null)
        throw new Exception();

      if (shiftGreek == null)
        shiftGreek = new ScenarioShiftGreek(1, 0, true, false);
      Double_Pricer_Fn fn = null;
      if (measure.Equals("Delta"))
      {
        fn = (p) =>
        {
          var deltaShift = shiftGreek.UpShift > 0.01 ? shiftGreek.UpShift / 10000.0 : shiftGreek.UpShift;
          return ((ICreditIndexOptionPricer)p).MarketDelta(deltaShift, shiftGreek.UsePremium);
        };
      }
      else if (measure.Equals("Gamma"))
      {
        fn = (p) =>
        {
          var oneSideGamma = shiftGreek.OneSide;
          var gammaShift = shiftGreek.UpShift;
          double upBump, downBump;
          if (oneSideGamma)
          {
            upBump = gammaShift > 0 ? gammaShift : 0;
            downBump = gammaShift < 0 ? -gammaShift : 0;
          }
          else
          {
            upBump = downBump = gammaShift;
          }
          if (Math.Abs(upBump) > 0.1)
            upBump /= 10000;
          if (Math.Abs(downBump) > 0.1)
            downBump /= 10000;
          return ((ICreditIndexOptionPricer)p).MarketGamma(upBump, downBump, false, shiftGreek.UsePremium);
        };
      }
      if (measure.Equals("Theta"))
      {
        fn = (p) =>
        {
          var thetaShift = shiftGreek.UpShift;
          return ((ICreditIndexOptionPricer)p).MarketTheta(Dt.Add(((ICreditIndexOptionPricer)p).AsOf, (int)thetaShift, TimeUnit.Days),
            Dt.Add(((ICreditIndexOptionPricer)p).Settle, (int)thetaShift, TimeUnit.Days));
        };
      }
      if (measure.Equals("Vega"))
      {
        fn = (p) =>
        {
          var vegaShift = shiftGreek.UpShift;
          return ((ICreditIndexOptionPricer)p).Vega(vegaShift);
        };
      }
      return new PricerEvaluator(pricer, fn);
    }

    #endregion Obsolete

  }

  #region Obsolete Types

  /// <exclude />
  [Obsolete]
  public enum ScenarioResultType
  {
    /// <summary>
    /// Individual
    /// </summary>
    Individual = 0,
    /// <summary>
    /// Sum
    /// </summary>
    Sum = 1,
  }

  /// <summary>
  /// Shift the terms (properties) of a product or pricer
  /// </summary>
  /// <exclude />
  [Obsolete]
  public class ScenarioShiftTradeTerms : IScenarioShift
  {
    /// <summary>
    /// Create a shift to the terms of a trade (product)
    /// </summary>
    /// <param name="terms">List of terms to shift</param>
    /// <param name="values">New values for terms</param>
    public ScenarioShiftTradeTerms(string[] terms, object[] values)
    {
      Terms = terms;
      Values = values;
    }

    /// <summary>
    /// Trade term names
    /// </summary>
    public string[] Terms { get; private set; }

    /// <summary>
    /// Trade term values
    /// </summary>
    public object[] Values { get; private set; }

    /// <summary>
    /// Validate
    /// </summary>
    public void Validate()
    {
      if (!Terms.IsNullOrEmpty() && (Values.IsNullOrEmpty() || Terms.Length != Values.Length))
        throw new ArgumentException(String.Format("Number of trade terms ({0}) does not match number of values ({1})",
          Terms.Length, Values == null ? 0 : Values.Length));
    }

    /// <summary>
    /// Save state
    /// </summary>
    public void SaveState(PricerEvaluator[] evaluators)
    { }

    /// <summary>
    /// Restore state
    /// </summary>
    public void RestoreState(PricerEvaluator[] evaluators)
    { }

    /// <summary>
    /// Perform scenario shift
    /// </summary>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      foreach (PricerEvaluator t in evaluators)
      {
        var pricer = t.Pricer;
        for (int k = 0; k < Terms.Length; ++k)
          if (!String.IsNullOrEmpty(Terms[k]))
          {
            var name = Terms[k];
            var value = Values[k];
            // Special cases for handly shifts
            if ((name == "Rfr") && pricer.HasPropertyOrField("DiscountCurve") && (value is double))
            {
              pricer.SetValue("DiscountCurve", new DiscountCurve(pricer.AsOf, (double)value));
            }
            else if ((name == "Vol") && pricer.HasPropertyOrField("VolatilitySurface") && (value is double))
            {
              pricer.SetValue("VolatilitySurface",
                CalibratedVolatilitySurface.FromFlatVolatility(pricer.AsOf, (double)value));
            }
            else if ((name == "UnderlyingPrice") && pricer.HasPropertyOrField("StockCurve") && (value is double))
            {
              var vs = pricer.GetValue<StockCurve>("StockCurve");
              if (vs != null)
                vs.SpotPrice = (double)value;
            }
            else if ((name == "Time") && pricer.Product.HasPropertyOrField("Expiration") && (value is double))
            {
              Dt exp = Dt.Add(pricer.Product.Effective, (int)(((double)value) * 365.25));
              pricer.Product.SetValue("Expiration", exp);
            }
            else if ((name == "Days") && pricer.Product.HasPropertyOrField("Expiration") && (value is double))
            {
              Dt exp = Dt.Add(pricer.Product.Effective, (int)(((double)value)));
              pricer.Product.SetValue("Expiration", exp);
            }
            else if (pricer.HasPropertyOrField(name))
              pricer.SetValue(name, value);
            else if (pricer.Product.HasPropertyOrField(name))
              pricer.Product.SetValue(name, value);
          }
      }
    }

    /// <summary>
    /// Refit all calibrated objects effected by the shift
    /// </summary>
    /// <param name="evaluators">Pricer evaluators to shift</param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    {}
  }

  /// <exclude />
  [Obsolete]
  public class ScenarioShiftResultOption : IScenarioShift
  {
    /// <summary>
    /// Constructor
    /// </summary>
    public ScenarioShiftResultOption(ScenarioResultType resultType, int resultIndex)
    {
      _resultType = resultType;
      _resultIndex = resultIndex;
    }

    /// <summary>
    /// The result type
    /// </summary>
    public ScenarioResultType ResultType
    {
      get { return _resultType; }
    }

    /// <summary>
    /// The result index
    /// </summary>
    public int ResultIndex
    {
      get { return _resultIndex; }
    }

    private readonly ScenarioResultType _resultType;
    private readonly int _resultIndex;

    #region IScenarioShift Members

    /// <summary>
    ///  Validate
    /// </summary>
    public void Validate()
    {
    }

    /// <summary>
    /// Save state
    /// </summary>
    /// <param name="evaluators"></param>
    public void SaveState(PricerEvaluator[] evaluators)
    {
    }

    /// <summary>
    /// Restore state
    /// </summary>
    /// <param name="evaluators"></param>
    public void RestoreState(PricerEvaluator[] evaluators)
    {
    }

    /// <summary>
    /// Perform shift
    /// </summary>
    /// <param name="evaluators"></param>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      // no shift, do nothing
    }

    /// <summary>
    /// Refit
    /// </summary>
    /// <param name="evaluators"></param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    { }

    #endregion
  }

  /// <exclude />
  [Obsolete]
  public class ScenarioShiftCreditSpreads : IScenarioShift
  {
    /// <summary>
    /// Constructor
    /// </summary>
    public ScenarioShiftCreditSpreads(SurvivalCurve[] creditCurves, object[] shiftValues, bool relative, bool forceFit)
    {
      _creditCurves = creditCurves;
      _shiftValues = shiftValues;
      _relative = relative;
      _forceFit = forceFit;
    }

    /// <summary>
    /// credit curves
    /// </summary>
    public SurvivalCurve[] CreditCurves
    {
      get { return _creditCurves; }
    }
    /// <summary>
    /// shift values
    /// </summary>
    public object[] ShiftValues
    {
      get { return _shiftValues; }
    }

    /// <summary>
    /// Type of shift - relative or basis
    /// </summary>
    public bool RelativeShift
    {
      get { return _relative; }
    }

    /// <summary>
    /// whether to force fit after bump the spreads
    /// </summary>
    public bool ForceFit
    {
      get { return _forceFit; }
    }

    #region IScenarioShift Members

    /// <summary>
    /// Validate
    /// </summary>
    public void Validate()
    {
    }

    /// <summary>
    /// Save state
    /// </summary>
    /// <param name="evaluators"></param>
    public void SaveState(PricerEvaluator[] evaluators)
    {
    }

    /// <summary>
    /// Restore state
    /// </summary>
    /// <param name="evaluators"></param>
    public void RestoreState(PricerEvaluator[] evaluators)
    {
    }

    /// <summary>
    /// Perform shift
    /// </summary>
    /// <param name="evaluators"></param>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      // should newver be called
      throw new NotImplementedException();
    }

    /// <summary>
    /// Refit
    /// </summary>
    /// <param name="evaluators"></param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    { }

    #endregion

    private readonly SurvivalCurve[] _creditCurves;
    private readonly object[] _shiftValues;
    private readonly bool _relative;
    private readonly bool _forceFit;
  }

  /// <exclude />
  [Obsolete]
  public class ScenarioShiftRates : IScenarioShift
  {
    /// <summary>
    /// Constructor
    /// </summary>
    public ScenarioShiftRates(DiscountCurve[] rateCurves, double[] rateShifts, bool relative, bool recalibrateSpreads)
    {
      _recalibrateSpreads = recalibrateSpreads;
      _relative = relative;
      _rateShifts = rateShifts;
      _rateCurves = rateCurves;
    }

    /// <summary>
    /// Re-Calibrate the spreads or not
    /// </summary>
    public bool RecalibrateSpreads
    {
      get { return _recalibrateSpreads; }
    }
    /// <summary>
    /// Type of shift - relative or basis
    /// </summary>
    public bool RelativeShift
    {
      get { return _relative; }
    }

    /// <summary>
    /// Rate curves 
    /// </summary>
    public DiscountCurve[] RateCurves
    {
      get { return _rateCurves; }
    }

    /// <summary>
    /// Rate shifts 
    /// </summary>
    public double[] RateShifts
    {
      get { return _rateShifts; }
    }

    #region IScenarioShift Members

    /// <summary>
    /// Validate
    /// </summary>
    public void Validate()
    {
    }

    /// <summary>
    /// Save state
    /// </summary>
    /// <param name="evaluators"></param>
    public void SaveState(PricerEvaluator[] evaluators)
    {
    }

    /// <summary>
    /// Restore state
    /// </summary>
    /// <param name="evaluators"></param>
    public void RestoreState(PricerEvaluator[] evaluators)
    {
    }

    /// <summary>
    /// Perform shift
    /// </summary>
    /// <param name="evaluators"></param>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      // should newver be called
      throw new NotImplementedException();
    }

    /// <summary>
    /// Refit
    /// </summary>
    /// <param name="evaluators"></param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    { }

    #endregion

    private readonly double[] _rateShifts;
    private readonly DiscountCurve[] _rateCurves;
    private readonly bool _recalibrateSpreads;
    private readonly bool _relative;
  }

  /// <exclude />
  [Obsolete]
  public class ScenarioShiftRescaleSrikes : IScenarioShift
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="rescaleStrikes"></param>
    public ScenarioShiftRescaleSrikes(bool[] rescaleStrikes)
    {
      _rescaleStrikes = rescaleStrikes;
    }

    /// <summary>
    /// Re-scale the strike
    /// </summary>
    public bool[] RescaleStrikes
    {
      get { return _rescaleStrikes; }
    }

    #region IScenarioShift Members

    /// <summary>
    /// Validate
    /// </summary>
    public void Validate()
    {
    }

    /// <summary>
    /// Save state
    /// </summary>
    /// <param name="evaluators"></param>
    public void SaveState(PricerEvaluator[] evaluators)
    {
    }

    /// <summary>
    /// Restore state
    /// </summary>
    /// <param name="evaluators"></param>
    public void RestoreState(PricerEvaluator[] evaluators)
    {
    }

    /// <summary>
    /// Perform shift
    /// </summary>
    /// <param name="evaluators"></param>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      // should newver be called
      throw new NotImplementedException();
    }

    /// <summary>
    /// Refit
    /// </summary>
    /// <param name="evaluators"></param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    { }

    #endregion

    private readonly bool[] _rescaleStrikes;
  }


  /// <exclude />
  [Obsolete]
  public class ScenarioShiftRecovery : IScenarioShift
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="recoveryShifts"></param>
    public ScenarioShiftRecovery(double[] recoveryShifts)
    {
      _recoveryShifts = recoveryShifts;
    }

    /// <summary>
    /// Recovery shift 
    /// </summary>
    public double[] RecoveryShifts
    {
      get { return _recoveryShifts; }
    }


    #region IScenarioShift Members

    /// <summary>
    /// Validate
    /// </summary>
    public void Validate()
    {
    }

    /// <summary>
    /// Save state
    /// </summary>
    /// <param name="evaluators"></param>
    public void SaveState(PricerEvaluator[] evaluators)
    {
    }

    /// <summary>
    /// Restore state
    /// </summary>
    /// <param name="evaluators"></param>
    public void RestoreState(PricerEvaluator[] evaluators)
    {
    }

    /// <summary>
    /// Perform shift
    /// </summary>
    /// <param name="evaluators"></param>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      // should newver be called
      throw new NotImplementedException();
    }

    /// <summary>
    /// Refit
    /// </summary>
    /// <param name="evaluators"></param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    { }

    #endregion

    private readonly double[] _recoveryShifts;
  }

  /// <exclude />
  [Obsolete]
  public class ScenarioShiftIndex : IScenarioShift
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="shiftValue"></param>
    /// <param name="shiftTenor"></param>
    /// <param name="idxScalingCalibrator"></param>
    public ScenarioShiftIndex(double shiftValue, string shiftTenor, IndexScalingCalibrator idxScalingCalibrator)
    {
      _shiftValue = shiftValue;
      _shiftTenor = shiftTenor;
      _idxScalingCalibrator = idxScalingCalibrator;
    }

    /// <summary>
    /// Bump the index spread
    /// </summary>
    public double ShiftSpreads
    {
      get { return _shiftValue; }
    }
    /// <summary>
    /// Tenor of shift
    /// </summary>
    public string ShiftTenor
    {
      get { return _shiftTenor; }
    }
    /// <summary>
    /// The scaling calibrator
    /// </summary>
    public IndexScalingCalibrator ScalingCalibrator
    {
      get { return _idxScalingCalibrator; }
    }

    #region IScenarioShift Members

    /// <summary>
    /// Validate
    /// </summary>
    public void Validate()
    {
    }

    /// <summary>
    /// Save state
    /// </summary>
    /// <param name="evaluators"></param>
    public void SaveState(PricerEvaluator[] evaluators)
    {
    }

    /// <summary>
    /// Restore state
    /// </summary>
    /// <param name="evaluators"></param>
    public void RestoreState(PricerEvaluator[] evaluators)
    {
    }

    /// <summary>
    /// Perform shift
    /// </summary>
    /// <param name="evaluators"></param>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      // should newver be called
      throw new NotImplementedException();
    }

    /// <summary>
    /// Refit
    /// </summary>
    /// <param name="evaluators"></param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    { }

    #endregion

    private readonly double _shiftValue;
    private readonly string _shiftTenor;
    private readonly IndexScalingCalibrator _idxScalingCalibrator;
  }

  /// <exclude />
  public class ScenarioShiftSpread01 : IScenarioShift
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="upShift"></param>
    /// <param name="downShift"></param>
    /// <param name="measure"></param>
    /// <param name="rescaleStrikes"></param>
    /// <param name="quoteType"></param>
    public ScenarioShiftSpread01(double upShift, double downShift, string measure, bool[] rescaleStrikes, CDSQuoteType quoteType)
    {
      _upShift = upShift;
      _downShift = downShift;
      _measure = measure;
      _rescaleStrikes = rescaleStrikes;
      _quoteType = quoteType;
    }

    /// <summary>
    /// Upside shidt size
    /// </summary>
    public double UpShift
    {
      get { return _upShift; }
    }
    /// <summary>
    /// Downside shift size
    /// </summary>
    public double DownShift
    {
      get { return _downShift; }
    }

    /// <summary>
    /// The measure
    /// </summary>
    public string Measure
    {
      get { return _measure; }
    }

    /// <summary>
    /// The flags for rescaling the strikes
    /// </summary>
    public bool[] RescaleStrikes
    {
      get { return _rescaleStrikes; }
    }

    /// <summary>
    /// The CDS quote type
    /// </summary>
    public CDSQuoteType QuoteType
    {
      get { return _quoteType; }
    }

    #region IScenarioShift Members

    /// <summary>
    /// Validate
    /// </summary>
    public void Validate()
    {
    }

    /// <summary>
    /// Save state
    /// </summary>
    /// <param name="evaluators"></param>
    public void SaveState(PricerEvaluator[] evaluators)
    {
    }

    /// <summary>
    /// Restore state
    /// </summary>
    /// <param name="evaluators"></param>
    public void RestoreState(PricerEvaluator[] evaluators)
    {
    }

    /// <summary>
    /// Shift
    /// </summary>
    /// <param name="evaluators"></param>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      // should newver be called
      throw new NotImplementedException();
    }

    /// <summary>
    /// Refit
    /// </summary>
    /// <param name="evaluators"></param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    { }

    #endregion

    private readonly double _upShift;
    private readonly double _downShift;
    private readonly string _measure;
    private readonly bool[] _rescaleStrikes;
    private readonly CDSQuoteType _quoteType;
  }

  /// <exclude />
  public class ScenarioShiftGreek : IScenarioShift
  {
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="upShift"></param>
    /// <param name="downShift"></param>
    /// <param name="oneSide"></param>
    /// <param name="usePremium"></param>
    public ScenarioShiftGreek(double upShift, double downShift, bool oneSide, bool usePremium)
    {
      _upShift = upShift;
      _downShift = downShift;
      _oneSide = oneSide;
      _usePremium = usePremium;
    }

    /// <summary>
    /// Upside shidt size
    /// </summary>
    public double UpShift
    {
      get { return _upShift; }
    }
    /// <summary>
    /// Downside shift size
    /// </summary>
    public double DownShift
    {
      get { return _downShift; }
    }

    /// <summary>
    /// The flags for one side or two side bump
    /// </summary>
    public bool OneSide
    {
      get { return _oneSide; }
    }

    /// <summary>
    /// The flags for use premium
    /// </summary>
    public bool UsePremium
    {
      get { return _usePremium; }
    }

    #region IScenarioShift Members

    /// <summary>
    /// Validate
    /// </summary>
    public void Validate()
    {
    }

    /// <summary>
    /// Save state
    /// </summary>
    /// <param name="evaluators"></param>
    public void SaveState(PricerEvaluator[] evaluators)
    {
    }

    /// <summary>
    /// Restore state
    /// </summary>
    /// <param name="evaluators"></param>
    public void RestoreState(PricerEvaluator[] evaluators)
    {
    }

    /// <summary>
    /// Perform shift
    /// </summary>
    /// <param name="evaluators"></param>
    public void PerformShift(PricerEvaluator[] evaluators)
    {
      // should newver be called
      throw new NotImplementedException();
    }

    /// <summary>
    /// refit
    /// </summary>
    /// <param name="evaluators"></param>
    public void PerformRefit(PricerEvaluator[] evaluators)
    { }

    #endregion

    private readonly double _upShift;
    private readonly double _downShift;
    private readonly bool _oneSide;
    private readonly bool _usePremium;
  }

  #endregion

}
