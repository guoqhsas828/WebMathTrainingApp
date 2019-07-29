//
// Sensitivities.Theta.cs
//  -2008. All rights reserved.
//  Partial implementation of the theta sensitivity functions
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Pricers.BGM;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Collections;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Sensitivity
{
  /// <summary>
  ///  Theta flags
  /// </summary>
  [Flags]
  public enum ThetaFlags
  {
    /// <summary> If set, do nothing </summary>
    None = 0x0000,

    /// <summary>If set, calculate on clean rather than full price</summary>
    Clean = 0x0001,

    /// <summary>If set, recalibrate survival curves</summary>
    Recalibrate = 0x0002,

    /// <summary>If set, refit the rate curves with the shifted market environment</summary>
    RefitRates = 0x0004,

    /// <summary>
    /// If set, will not exclude the default payment from model valuation
    /// </summary>
    IncludeDefaultPayment = 0x0008,

  }

  /// <summary>
  /// Used to define HasFlag method (should be removed when we migrate to .NET 4)
  /// </summary>
  public static class ThetaFlagsExtension
  {
    /// <summary>
    /// Return true if the bit(s) specified by the flag argument are set in the flags enum
    /// </summary>
    /// <param name="flags"></param>
    /// <param name="flag"></param>
    /// <returns></returns>
    public static bool HasFlag(this ThetaFlags flags, ThetaFlags flag)
    {
      return (flags & flag) != 0;
    }
  }

  ///
  /// <summary>
  ///   
  /// </summary>
  //Methods for calculating generalized sensitivity measures
  public static partial class Sensitivities
  {
    #region SummaryRiskMethods

    /// <summary>
    ///   Calculate the theta
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The theta is calculated as the difference between the current
    ///   price, and the price at the specified future pricing date
    ///   <paramref name="toAsOf"/> and future settlement date
    ///   <paramref name="toSettle"/>.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on survival curves.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of IPricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="toAsOf">Pricing as-of date for future pricing</param>
    /// <param name="toSettle">Settlement date for future pricing</param>
    /// <param name="flags">A ThetaFlags indicating whether Clean, Recalibrate, and RescaleStrikes are applied</param>
    /// <param name="rs">Array of enum values SensitivityRescaleStrikes</param>
    /// <returns>Theta</returns>
    ///
    public static double[] Theta(IPricer[] pricers, string measure, Dt toAsOf, Dt toSettle, ThetaFlags flags, SensitivityRescaleStrikes[] rs)
    {
      bool[] rescaleStrikes = new bool[pricers.Length];
      for (int i = 0; i < rescaleStrikes.Length; i++)
      {
        switch(rs[i])
        {
          case SensitivityRescaleStrikes.Yes:
            rescaleStrikes[i] = true;
            break;
          case SensitivityRescaleStrikes.No:
            rescaleStrikes[i] = false;
            break;
          case SensitivityRescaleStrikes.UsePricerSetting:
            if (pricers[i] is SyntheticCDOPricer && ((SyntheticCDOPricer)pricers[i]).Basket !=null)
            {
              if(((SyntheticCDOPricer)pricers[i]).Basket is BaseCorrelationBasketPricer)
                rescaleStrikes[i] = ((BaseCorrelationBasketPricer)((SyntheticCDOPricer) pricers[i]).Basket).RescaleStrike;
            }
            else rescaleStrikes[i] = false;
            break;
        }        
      }
      bool clean = flags.HasFlag(ThetaFlags.Clean);
      bool recalibrate = flags.HasFlag(ThetaFlags.Recalibrate);
      return Theta(CreateAdapters(pricers.LockFloatingCoupons(toAsOf), measure), toAsOf, 
        toSettle, clean, rescaleStrikes, recalibrate, flags.HasFlag(ThetaFlags.RefitRates), 
        flags.HasFlag(ThetaFlags.IncludeDefaultPayment));
    }

    /// <summary>
    /// Calculate the theta (time sensitivity)
    /// </summary>
    /// <remarks>
    ///   <para>The theta is calculated as the difference between the current
    ///   price, and the price at the specified future pricing date and future
    ///   settlement date.</para>
    ///   <math>\Theta = \frac{\partial V}{\partial \tau}</math>
    ///   <para>Note also when calculating at the specified future pricing date, the
    ///   term structure of interest rates and survival probabilities are held
    ///   constant. Ie. the 3 month survival probability at todays date is used
    ///   for the 3 month survival probability at the future pricing date and the
    ///   3 month discount factor at todays date is used for the 3 month discount
    ///   factor at the future pricing date.</para>
    /// </remarks>
    /// <param name="pricer">IPricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="toAsOf">Pricing as-of date for future pricing (or pricing as-of + 1D if unset)</param>
    /// <param name="toSettle">Settlement date for future pricing (or pricing settle + 1D if unset)</param>
    /// <param name="flags">A ThetaFlags indicating whether Clean, Recalibrate, and RescaleStrikes are applied</param>
    /// <param name="rs">Array of enum values SensitivityRescaleStrikes</param>
    /// <returns>Theta</returns>
    public static double Theta(IPricer pricer, string measure, Dt toAsOf, Dt toSettle, 
      ThetaFlags flags, SensitivityRescaleStrikes rs)
    {
      // Default asOf and settle dates if not specified
      if (toAsOf.IsEmpty())
        toAsOf = Dt.Add(pricer.AsOf, 1);
      if (toSettle.IsEmpty())
        toSettle = Dt.Add(pricer.Settle, 1);
      bool rescaleStrike = false;
      if(rs == SensitivityRescaleStrikes.No)
        rescaleStrike = false;
      else if (rs == SensitivityRescaleStrikes.Yes)
        rescaleStrike = true;
      else
      {
        if (pricer is SyntheticCDOPricer && ((SyntheticCDOPricer)pricer).Basket != null)
        {
          if (((SyntheticCDOPricer)pricer).Basket is BaseCorrelationBasketPricer)
            rescaleStrike = ((BaseCorrelationBasketPricer)((SyntheticCDOPricer)pricer).Basket).RescaleStrike;
        }
      }
      pricer = pricer.LockFloatingCoupons(toAsOf);
      return Theta(measure == null
        ? new PricerEvaluator(pricer)
        : new PricerEvaluator(pricer, measure), toAsOf,toSettle,(flags & ThetaFlags.Clean) != 0, 
        (flags & ThetaFlags.Recalibrate) != 0,rescaleStrike, flags.HasFlag(ThetaFlags.RefitRates), 
        flags.HasFlag(ThetaFlags.IncludeDefaultPayment));
    }
    
    /// <summary>
    ///   Calculate the theta
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The theta is calculated as the difference between the current
    ///   price, and the price at the specified future pricing date
    ///   <paramref name="toAsOf"/> and future settlement date
    ///   <paramref name="toSettle"/>.</para>
    ///
    ///   <para>This routines relies on the IPricer interface and as such can be used with any pricer
    ///   that depends on survival curves.</para>
    /// </remarks>
    ///
    /// <param name="evaluator">Pricer evaluator</param>
    /// <param name="toAsOf">Pricing as-of date for future pricing</param>
    /// <param name="toSettle">Settlement date for future pricing</param>
    /// <param name="clean">Calculate on clean rather than full price</param>
    /// <param name="recalibrate">Recalibrate curve or not</param>
    /// <param name="refitRates">Refit discount curve or not</param>
    /// <param name="rescaleStrikes">Boolean indicating rescale strikes or not for CDO pricer</param>
    /// <param name="includeDefaultPmts">Do not intentionally exclude default payment from model valuation</param> 
    /// <returns>Theta</returns>
    ///
    private static double Theta( PricerEvaluator evaluator, Dt toAsOf, Dt toSettle, 
      bool clean, bool recalibrate, bool rescaleStrikes, bool refitRates, bool includeDefaultPmts)
    {
      double[] res = Theta(new PricerEvaluator[] { evaluator }, toAsOf, toSettle, 
        clean, new bool[]{rescaleStrikes}, recalibrate, refitRates, includeDefaultPmts);

      return res[0];
    }

    /// <summary>
    /// Computes rollover
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Computes the difference in Mark-to-Market after pricing product 1 day later.</para>
    ///
    ///   <para>Equivalent to <see cref="Rolldown(IPricer,string,bool)">
    ///   Rolldown(pricer, null, clean)</see></para>
    /// </remarks>
    /// 
    /// <param name="pricer">Pricer</param>
    /// <param name="clean">Flag to include accrued in calculation</param>
    ///
    /// <returns>Rolldown value</returns>
    /// 
    public static double Rolldown(IPricer pricer, bool clean)
    {
      //default to 1 day roll
      Dt rollAsOf = Dt.Add(pricer.AsOf, 1);
      Dt rollSettle = Dt.Add(pricer.Settle, 1);
      return Rolldown(pricer, rollAsOf, rollSettle, clean);
    }

    /// <summary>
    /// Computes rollover
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Computes the difference in Mark-to-Market after pricing product 1 day later.</para>
    ///   <para>The function "roll down" the as of date and settle date of the Pricer, but
    ///   unlike qTheta does not change the as of and settle dates of any Curves
    ///   (e.g. DiscountCurve, SurvivalCurve, etc.)</para>
    /// </remarks>
    /// 
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="clean">Flag to include accrued in calculation</param>
    /// 
    /// <returns>Rolldown value</returns>
    /// 
    public static double Rolldown(IPricer pricer, string measure, bool clean)
    {
      //default to 1 day roll
      Dt rollAsOf = Dt.Add(pricer.AsOf, 1);
      Dt rollSettle = Dt.Add(pricer.Settle, 1);
      return Rolldown(pricer, measure, rollAsOf, rollSettle, clean);
    }

    /// <summary>
    ///   Computes rollover
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Computes the difference in Mark-to-Market after pricing product 1 day later.</para>
    ///
    ///   <para>Equivalent to <see cref="Rolldown(IPricer,string,Dt,Dt,bool)">
    ///   Rolldown(pricer, null, roolAsOf, rollSettle, clean)</see></para>
    /// </remarks>
    /// 
    /// <param name="pricer">Pricer</param>
    /// <param name="rollAsOf"></param>
    /// <param name="rollSettle"></param>
    /// <param name="clean">Flag to include accrued in calculation</param>
    /// 
    /// <returns>Rolldown value</returns>
    /// 
    public static double
    Rolldown( IPricer pricer, Dt rollAsOf, Dt rollSettle, bool clean )
    {
      return Rolldown(new PricerEvaluator(pricer.LockFloatingCoupons(rollAsOf)), rollAsOf, rollSettle, clean);
    }

    /// <summary>
    ///   Computes rollover
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Computes the difference in Mark-to-Market after pricing product 1 day later.</para>
    ///
    ///   <para>The function "roll down" the as of date and settle date of the Pricer, but
    ///   unlike qTheta does not change the as of and settle dates of any Curves
    ///   (e.g. DiscountCurve, SurvivalCurve, etc.)</para>
    /// </remarks>
    /// 
    /// <param name="pricer">Pricer</param>
    /// <param name="measure">Target value to evaluate</param>
    /// <param name="rollAsOf"></param>
    /// <param name="rollSettle"></param>
    /// <param name="clean">Flag to include accrued in calculation</param>
    /// 
    /// <returns>Rolldown value</returns>
    /// 
    public static double
    Rolldown( IPricer pricer, string measure, Dt rollAsOf, Dt rollSettle, bool clean )
    {
      return Rolldown(new PricerEvaluator(pricer.LockFloatingCoupons(rollAsOf), measure), rollAsOf, rollSettle, clean);
    }

    /// <summary>
    /// Computes rollover
    /// </summary>
    /// 
    /// <remarks>
    ///   <para>Computes the difference in Mark-to-Market after pricing product 1 day later.</para>
    ///   <para>The function "roll down" the as of date and settle date of the Pricer, but
    ///   unlike qTheta does not change the as of and settle dates of any Curves
    ///   (e.g. DiscountCurve, SurvivalCurve, etc.)</para>
    /// </remarks>
    /// 
    /// <param name="pricer">Pricer Object</param>
    /// <param name="rollAsOf"></param>
    /// <param name="rollSettle"></param>
    /// <param name="clean">Flag to include accrued in calculation</param>
    /// 
    /// <returns>Rolldown value</returns>
    /// 
    private static double
    Rolldown( PricerEvaluator pricer, Dt rollAsOf, Dt rollSettle, bool clean )
    {
      Timer timer = new Timer();
      timer.start();

      logger.Debug("Calculating rolldown...");
      double rolldown = 0;

      // Validation
      if (pricer == null)
        throw new ArgumentException("Must specify products to price");

      Dt asOf = pricer.AsOf;
      Dt settle = pricer.Settle;
      PricerEvaluator shallowPricer = pricer.Substitute((IPricer)((PricerBase)pricer.Pricer).ShallowCopy());

      if (Dt.Cmp(rollAsOf, shallowPricer.AsOf) < 0)
        throw new ArgumentException(String.Format(
          "Future pricing as-of date {0} must be on or after pricing as-of date {1}", 
          rollAsOf, shallowPricer.AsOf));
      if (Dt.Cmp(rollSettle, shallowPricer.Settle) < 0)
        throw new ArgumentException(String.Format(
          "Future settlement date {0} must be on or after settlement date {1}", 
          rollSettle, shallowPricer.Settle));
      
      CallOnceOnBaskets(new PricerEvaluator[]{shallowPricer}, (b) => b.SetNullDefaultInfo());
      // Compute base case
      shallowPricer.PricerFlags = PricerFlags.NoDefaults; // do not consider defaults in rolldown calc
      shallowPricer.Reset();
      double pv = shallowPricer.Evaluate();
      if (clean)
        pv -= shallowPricer.Accrued();
      rolldown = -pv;
      logger.DebugFormat("Base pricing for pricer #{0} is {1}", 1, pv);

      PricerEvaluator[] pricers = new PricerEvaluator[] { shallowPricer };
      IList<SurvivalCurve> survivalCurves = PricerEvaluatorUtil.GetSurvivalCurves(pricers, false);
      var savedCurves = CurveUtil.CurveCloneWithRecovery(survivalCurves.ToArray());
      try
      {
        //todo: may be ,we dont need to pass the survival curves to this function , since we are not changing them 
        PricerSetDates(pricers, false, false, false, asOf, settle, rollAsOf, rollSettle, null, survivalCurves, null,null);
          
        // Reprice
        shallowPricer.PricerFlags = PricerFlags.NoDefaults; 
        shallowPricer.Reset();
        pv = shallowPricer.Evaluate();
        if (clean)
          pv -= shallowPricer.Accrued();
        logger.DebugFormat("Forward pricing for pricer #{0} is {1}", 1, pv);
        rolldown += pv;
      }
      finally
      {
        CallOnceOnBaskets(new PricerEvaluator[] { shallowPricer }, (b) => b.SetBackDefaultInfo());
        // Restore all modified dates
        PricerSetDates(pricers, true, false, false, asOf, settle, rollAsOf, rollSettle, null, survivalCurves, null,null);
        CurveUtil.CurveRestoreWithRecovery(survivalCurves.ToArray(), savedCurves);
        shallowPricer.Reset();
      }

      timer.stop();
      logger.InfoFormat("Completed rolldown in {0}s", timer.getElapsed());

      return rolldown;
    }

    #endregion SummaryRiskMethods

    #region Theta_Sensitivity

    /// <summary>
    ///   Calculate the theta for a series of products
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Returns an array containing the theta for each product.</para>
    ///
    ///   <para>The theta is calculated as the difference between the current
    ///   price, and the price at the specified future pricing date
    ///   <paramref name="toAsOf"/> and future settlement date
    ///   <paramref name="toSettle"/>.</para>
    ///
    ///   <para>The option is provided to either use the full price which includes
    ///   the accrued interest or the clean pricer.</para>
    ///
    ///   <para>Note also when calculating at the specified future pricing date, the
    ///   term structure of interest rates and survival probabilities are held
    ///   constant. Ie. the 3 month survival probability at todays date is used
    ///   for the 3 month survival probability at the future pricing date and the
    ///   3 month discount factor at todays date is used for the 3 month discount
    ///   factor at the future pricing date.</para>
    /// </remarks>
    ///
    /// <param name="pricers">Array of pricing adaptors (elements may be null)</param>
    /// <param name="toAsOf">Pricing as-of date for future pricing</param>
    /// <param name="toSettle">Settlement date for future pricing</param>
    /// <param name="clean">Calculate on clean rather than full price</param>
    /// <param name="recalibrate">Recalibrate curve or not</param>
    /// <param name="refitRates">Refit discount curves</param>
    /// <param name="includeDefaultPmts">Do not intentionally exclude default payment from model valuation</param> 
    /// <returns>An array of thetas for each product</returns>
    ///
    private static double[]
    Theta(PricerEvaluator[] pricers, Dt toAsOf, Dt toSettle, bool clean,
      bool recalibrate, bool refitRates, bool includeDefaultPmts)
    {
      Timer timer = new Timer();
      timer.start();

      logger.DebugFormat("Calculating theta to as-of={0}, settle={1}", toAsOf, toSettle);

      // Validation
      if (pricers == null || pricers.Length < 1)
        throw new ArgumentException("Must specify products to price");

      Dt asOf = Dt.Empty;
      Dt settle = Dt.Empty;
      PricerEvaluator[] shallowPricers = new PricerEvaluator[pricers.Length];

      for (int i = 0; i < pricers.Length; i++)
      {
        if (pricers[i] != null)
        {
          if (asOf.IsValid())
          {
            if (Dt.Cmp(pricers[i].AsOf, asOf) != 0)
              throw new ArgumentException(String.Format(
                "pricer #{0} pricing as-of date {1} does not match first pricer as-of date {2}", 
                i + 1, pricers[i].AsOf, asOf));
            if (settle.IsValid() && Dt.Cmp(pricers[i].Settle, settle) != 0)
              throw new ArgumentException(String.Format(
                "pricer #{0} settlement date {1} does not match first pricer settlement {2}", 
                i + 1, pricers[i].Settle, settle));
          }
          else
          {
            asOf = pricers[i].AsOf;
            settle = pricers[i].Settle;
          }
          shallowPricers[i] = pricers[i].Substitute((IPricer)((PricerBase)pricers[i].Pricer).ShallowCopy());
        }
      }

      if (logger.IsDebugEnabled)
      {
        for (int i = 0; i < shallowPricers.Length; i++)
        {
          if (shallowPricers[i] != null)
          {
            if (Dt.Cmp(toAsOf, shallowPricers[i].AsOf) < 0)
              logger.Debug(String.Format("Future pricing as-of date {0} must be on or after pricer #{1} pricing as-of date {2}", toAsOf, i + 1,
                shallowPricers[i].AsOf));
            if (Dt.Cmp(toSettle, shallowPricers[i].Settle) < 0)
              logger.Debug(String.Format("Future settlement date {0} must be on or after pricer #{1} settlement date {2}", toSettle, i + 1,
                shallowPricers[i].Settle));
          }
        }
      }

      IList<CalibratedCurve> discountCurves = PricerEvaluatorUtil.GetRateCurves(shallowPricers, false);
      for (int i = 0; i < discountCurves.Count; i++)
        discountCurves[i].CheckCurveAsOfDate(asOf);
      IList<SurvivalCurve> survivalCurves = PricerEvaluatorUtil.GetSurvivalCurves(shallowPricers, false);

      for (int i = 0; i < survivalCurves.Count; i++)
        survivalCurves[i].CheckCurveAsOfDate(asOf);
      IList<RecoveryCurve> recoveryCurves = PricerEvaluatorUtil.GetRecoveryCurves(shallowPricers, false);
      for (int i = 0; i < recoveryCurves.Count; i++)
        if (Dt.Cmp(recoveryCurves[i].AsOf, asOf) != 0)
          throw new ToolkitException(String.Format(
            "Recovery curve {0} has inconsistent pricing as-of date {1}. Should be {2}", 
            recoveryCurves[i].Name, recoveryCurves[i].AsOf, asOf));

      IList<RateVolatilityCube> fwdVolatilityCubes = PricerEvaluatorUtil.GetRateVolatilityCubes(shallowPricers, false);


      // Compute base case, for now redo this on the shallow clones
      PricerReset(shallowPricers);

      //The particular case we ran into was between default date and an as-yet unknown default 
      //settlement date. To include the default settlement in the MTM we set the default 
      //settlement date on the curve to T+2 (i.e. Pricer.Settle + 1), but when the Theta calc 
      //bumps T->T’ = T+1, T+2 is now equal to Pricer.Settle and it drops out of the calc. In 
      //this case the logical thing to do is bump the default settlement date to the new T’+2 
      //(i.e. T + 3) during the Theta calc, but since this gives exactly the same contribution 
      //to Theta as not calculating it in the first place then current approach is fine.
      if (!includeDefaultPmts)
      {
        CallOnceOnBaskets(shallowPricers, (b) => b.SetNullDefaultInfo());
      }

      double[] theta = new double[shallowPricers.Length];
      for (int i = 0; i < shallowPricers.Length; i++)
      {
        if (shallowPricers[i] != null)
        {
          if (!includeDefaultPmts)
          {
            shallowPricers[i].PricerFlags = PricerFlags.NoDefaults;
          }
          double pv = shallowPricers[i].Evaluate();
          if (clean)
            pv -= shallowPricers[i].Accrued();
          theta[i] = -pv;
          logger.DebugFormat("Base pricing for pricer #{0} is {1}", i + 1, pv);
        }
      }

      var savedCurves = CurveUtil.CurveCloneWithRecovery(survivalCurves.ToArray());
      try
      {
        // Set all dates forward
        PricerSetDates(shallowPricers, false, true, asOf, settle, toAsOf, toSettle, 
          discountCurves, refitRates, survivalCurves, recalibrate, recoveryCurves, fwdVolatilityCubes);

        // Reprice
        PricerReset(shallowPricers);
        for (int i = 0; i < shallowPricers.Length; i++)
        {
          if (shallowPricers[i] != null)
          {
            if (!includeDefaultPmts)
            {
              shallowPricers[i].PricerFlags = PricerFlags.NoDefaults;
            }
            double pv = shallowPricers[i].Evaluate();
            if (clean)
              pv -= shallowPricers[i].Accrued();
            logger.DebugFormat("Forward pricing for pricer #{0} is {1}", i + 1, pv);
            theta[i] += pv;
          }
        }
      }
      finally
      {
        if (!includeDefaultPmts)
        {
          CallOnceOnBaskets(shallowPricers, (b) => b.SetBackDefaultInfo());
        }
        // Restore all modified dates
        PricerSetDates(shallowPricers, true, true, asOf, settle, toAsOf, toSettle, 
          discountCurves, refitRates, survivalCurves, recalibrate, recoveryCurves, fwdVolatilityCubes);
        CurveUtil.CurveRestoreWithRecovery(survivalCurves.ToArray(), savedCurves);
        PricerReset(shallowPricers, false, true);
      }

      timer.stop();
      logger.InfoFormat("Completed theta in {0}s", timer.getElapsed());

      return theta;
    }

    private static void CheckCurveAsOfDate(this CalibratedCurve curve, Dt asOf)
    {
      Dt expect = Dt.AddDays(asOf, curve.SpotDays, curve.SpotCalendar);
      if (curve.AsOf != expect)
        throw new ToolkitException(String.Format(
          "{0} {1} has inconsistent pricing as-of date {2}. Should be {3}",
          curve.GetType().Name, curve.Name, curve.AsOf, expect));
    }

    /// <summary>
    /// Call a deleagte on each of the baskets in pricers
    /// and take care to call it only once on the same baskets.
    /// </summary>
    /// <param name="pricers">The pricers.</param>
    /// <param name="fn">Action to perform on baskets.</param>
    private static void CallOnceOnBaskets(
      PricerEvaluator[] pricers, Action<BasketPricer> fn)
    {
      bool[] called = new bool[pricers.Length];
      for (int i = 0; i < pricers.Length; ++i)
        if (!called[i] && pricers[i] != null && pricers[i].Basket != null)
        {
          var basket = pricers[i].Basket;
          fn(basket);
          called[i] = true;
          for (int j = i + 1; j < pricers.Length; ++j)
          {
            if (!called[j] && (pricers[j] == null || pricers[j].Basket == basket))
              called[j] = true;
          }
        }
      return;
    }
    #endregion // Theta Senstivity

    #region Shift dates with refit
    private static void PricerSetDates(PricerEvaluator[] pricers,
      bool reset, bool setCurveAsOfs, Dt asOf, Dt settle, Dt toAsOf, Dt toSettle,
      IList<CalibratedCurve> discountCurves, bool refitDiscountCurves,
      IList<SurvivalCurve> survivalCurves, bool recalibrateSurvival,
      IList<RecoveryCurve> recoveryCurves, IList<RateVolatilityCube> fwdVolCubes)
    {
      if (refitDiscountCurves && setCurveAsOfs && discountCurves != null)
      {
        Dt targetAsOf = reset ? asOf : toAsOf;

        var curves = pricers.GetDependentCurves(discountCurves);
        foreach (var curve in curves.OfType<DiscountCurve>())
          ThetaShiftUtil.ShiftDatesAndRefit(curve, targetAsOf);

        // Mark that we are done with the discount curve.
        discountCurves = null;

        if (recalibrateSurvival && survivalCurves != null)
        {
          for (int i = 0, n = survivalCurves.Count; i < n; i++)
            ThetaShiftUtil.ShiftDatesAndRefit(survivalCurves[i], targetAsOf);

          // Mark that we are done with the survival curve.
          survivalCurves = null;
        }
      }

      // Old routine to handle the rest.
      PricerSetDates(pricers, reset, setCurveAsOfs, recalibrateSurvival, asOf, settle, 
        toAsOf, toSettle,discountCurves, survivalCurves, recoveryCurves, fwdVolCubes);
    }
    #endregion

    #region Wrapers
    /// <summary>
    ///  Wrapper method between Theta(IPricer,...) and Theta(PricerEvaluator,...)
    ///  Its function is to set and restore rescale strikes flag for CDO pricer
    /// </summary>
    /// <param name="pricers"></param>
    /// <param name="toAsOf"></param>
    /// <param name="toSettle"></param>
    /// <param name="clean"></param>
    /// <param name="rescaleStrikes"></param>
    /// <param name="recalibrate">Recalibrate curve or not</param>
    /// <param name="refitRates">Refit rates</param>
    /// <param name="includeDefaultPmts">Do not intentionally exclude default payment from model valuation</param> 
    /// <returns>Array of theta values</returns>
    private static double[] Theta(PricerEvaluator[] pricers, Dt toAsOf, Dt toSettle, bool clean, 
      bool[] rescaleStrikes, bool recalibrate, bool refitRates, bool includeDefaultPmts)
    {
      // Some of the pricers settle > their products maturity, 0 the results for them
      bool[] rescaleStrikesSaved = Sensitivities.ResetRescaleStrikes(pricers, rescaleStrikes);
      List<double> res_SettleAfterMaturity = new List<double>();
      List<PricerEvaluator> goodPricers = new List<PricerEvaluator>();
      double[] res = null;
      for (int i = 0; i < pricers.Length; i++)
      {
        if (!IsThetaApplicable(pricers[i]))
          res_SettleAfterMaturity.Add(0.0);
        else
          goodPricers.Add(pricers[i]);
      }
      try
      {
        if(goodPricers.Count > 0)
          res = Theta(goodPricers.ToArray(), toAsOf, toSettle, clean, 
            recalibrate, refitRates, includeDefaultPmts);
      }
      finally
      {
        Sensitivities.ResetRescaleStrikes(pricers, rescaleStrikesSaved);
      }
      int len = (res_SettleAfterMaturity.Count) + (res == null ? 0 : res.Length);
      double[] res_return = new double[len];
      for (int i = 0, j = 0, k = 0; i < len; i++)
      {
        if (!IsThetaApplicable(pricers[i]))
          res_return[i] = res_SettleAfterMaturity[k++];
        else
          res_return[i] = (res == null || res.Length == 0) ? 0.0 : res[j++];
      }
      return res_return;
    }

    private static bool IsThetaApplicable(PricerEvaluator pEval)
    {
      if (pEval.Pricer is BondPricer)
      {
        var bp = pEval.Pricer as BondPricer;
        if (bp.IsDefaulted(pEval.Settle))
        {
          if (!bp.DefaultPaymentDate.IsValid() || pEval.Settle <= bp.DefaultPaymentDate)
            return true;
          return false;
        }
      }
      return pEval.Settle <= pEval.Product.EffectiveMaturity;
    }
    #endregion Wrapers

    #region Lock coupon projections
    private static IPricer[] LockFloatingCoupons(this IPricer[] pricers, Dt toAsOf)
    {
      pricers = (IPricer[])pricers.Clone();
      for (int i = 0; i < pricers.Length; ++i)
        pricers[i] = pricers[i].LockFloatingCoupons(toAsOf);
      return pricers;
    }

    internal static IPricer LockFloatingCoupons(this IPricer pricer, Dt toAsOf)
    {
      if (pricer is ILockedRatesPricerProvider)
        return ((ILockedRatesPricerProvider)pricer).LockRatesAt(toAsOf);
      if (pricer is IRatesLockable)
        return ((IRatesLockable)pricer).DoLockFloatingCoupons(toAsOf);
      if (pricer is SwapPricer)
        return ((SwapPricer)pricer).DoLockFloatingCoupons(toAsOf);
      return pricer;
    }

    private static SwapPricer DoLockFloatingCoupons(this SwapPricer pricer, Dt toAsOf)
    {
      var pPricer = pricer.PayerSwapPricer.DoLockFloatingCoupons(toAsOf)
        as SwapLegPricer;
      var rPricer = pricer.ReceiverSwapPricer.DoLockFloatingCoupons(toAsOf)
        as SwapLegPricer;
      IPricer swaptionPricer = null;
      if (pricer.SwaptionPricer != null && pricer.SwaptionPricer is SwaptionBlackPricer)
      {
        swaptionPricer = ((SwaptionBlackPricer) pricer.SwaptionPricer).DoLockFloatingCoupons(toAsOf);
      }
      else if (pricer.SwaptionPricer != null && pricer.SwaptionPricer is SwapBermudanBgmTreePricer)
      {
        swaptionPricer = ((SwapBermudanBgmTreePricer)pricer.SwaptionPricer).DoLockFloatingCoupons(toAsOf);
      }
      else if (pricer.SwaptionPricer != null)
      {
        throw new ToolkitException("Unsupported swaption pricer type in swap pricer DoLockFloatingCoupons process");
      }

      if (pPricer != pricer.PayerSwapPricer || rPricer != pricer.PayerSwapPricer || swaptionPricer != pricer.SwaptionPricer)
      {
        pricer = pricer.SwaptionPricer == null ? new SwapPricer(rPricer, pPricer) : new SwapPricer(rPricer, pPricer, swaptionPricer);
      }
      return pricer;
    }

    private static IPricer DoLockFloatingCoupons(this ILockedRatesPricerProvider pricer, Dt toAsOf)
    {
      return pricer.LockRatesAt(toAsOf);
    }

    private static IPricer DoLockFloatingCoupons(this IRatesLockable pricer, Dt toAsOf)
    {
      var ps = pricer.ProjectedRates;
      if (ps == null) return (IPricer)pricer;
      var rateResets = pricer.LockedRates;
      if (rateResets == null)
      {
        rateResets = new RateResets();
      }
      else
      {
        // Shallow clone
        rateResets = (RateResets)rateResets.ShallowCopy();
      }
      // We clone the AllResets object before locking rates.
      var resets = rateResets.AllResets =
        new SortedDictionary<Dt, double>(rateResets.AllResets);
      var count = resets.Count;
      foreach (var rr in ps)
      {
       if (rr.Date >= toAsOf || resets.ContainsKey(rr.Date))
        {
          continue;
        }
        resets.Add(rr.Date, rr.Rate);
      }
      if (resets.Count != count)
      {
        if (pricer is BaseEntityObject)
          pricer = (IRatesLockable) ((BaseEntityObject) pricer).ShallowCopy();
        pricer.LockedRates = rateResets;
      }
      return (IPricer) pricer;
    }
    #endregion
  } // class Sensitivities.Theta


  #region ThetaShiftUtil

  /// <summary>
  /// 
  /// </summary>
  public static class ThetaShiftUtil
  {
    internal static void ShiftDatesAndRefit(
      this DiscountCurve curve, Dt toAsOf)
    {
      if (curve == null) return;
      if (curve.Calibrator is FxBasisFitCalibrator
          || curve.Calibrator is FxBasisInverseFitCalibrator)
      {
        curve.Fit();
        return;
      }

      curve.SetCurveAsOfDate(toAsOf);

      var calibrator = curve.Calibrator as DiscountCalibrator;
      var tenors = curve.Tenors;
      if (calibrator == null || calibrator is DiscountBootstrapCalibrator || calibrator is FxBasisFitCalibrator 
        || calibrator is FxBasisInverseFitCalibrator || tenors.IsNullOrEmpty())
        return;

      if (tenors is ThetaShiftedTenors)
      {
        // Restore the original tenors and dates
        var saved = tenors as ThetaShiftedTenors;
        tenors = curve.Tenors = saved.OriginalTenors;
        calibrator.Settle = saved.OriginalSettle;
        calibrator.AsOf = saved.OriginalAsOf;
        if (saved.OriginalAsOf == toAsOf)
        {
          curve.Fit();
          return;
        }
      }

      curve.Tenors = new ThetaShiftedTenors(tenors,
        calibrator.AsOf, calibrator.Settle);

      Dt origAsOf = calibrator.AsOf;
      calibrator.AsOf = calibrator.Settle = toAsOf;
      for (int i = 0, n = tenors.Count; i < n; ++i)
      {
        var tenor = tenors[i];
        var tn = tenor.GetTenorString();
        if (tn == null)
        {
          throw new ToolkitException(String.Format(
            "Incomplete information in tenor {0}", i));
        }

        var note = tenor.Product as Note;
        if (note != null)
        {
          var cal = note.Calendar;
          var days = GetBusinessDaysInBetween(origAsOf, note.Effective, cal);
          var settle = Dt.AddDays(toAsOf, days, cal);
          var maturity = DiscountCurveCalibrationUtils.GetMaturity(
            InstrumentType.MM, settle, tn, cal, note.BDConvention);
          curve.AddMoneyMarket(tenor.Name, tenor.Weight, settle, maturity,
            note.Coupon, note.DayCount, note.Freq, note.BDConvention, cal);
          continue;
        }

        var fut = tenor.Product as StirFuture;
        if (fut != null)
        {
          var cal = fut.ReferenceIndex.Calendar;
          var days = fut.Effective.IsEmpty()
            ? 0
            : GetBusinessDaysInBetween(origAsOf, fut.Effective, cal);
          var settle = Dt.AddDays(toAsOf, days, cal);
          var maturity = (fut.RateFutureType == RateFutureType.ASXBankBill)
            ? Dt.ImmDate(settle, tn, CycleRule.IMMAUD)
            : DiscountCurveCalibrationUtils.GetMaturity(InstrumentType.FUT,
              settle, tn, cal, fut.ReferenceIndex.Roll);
          fut = (StirFuture)fut.ShallowCopy();
          fut.Effective = settle;
          fut.Maturity = maturity;
          curve.Add(fut, tenor.MarketPv, 0, 0, tenor.Weight);
          continue;
        }

        var fra = tenor.Product as FRA;
        if (fra != null)
        {
          var cal = fra.Calendar;
          var days = GetBusinessDaysInBetween(origAsOf, fra.Effective, cal);
          var settle = Dt.AddDays(toAsOf, days, cal);
          var maturity = DiscountCurveCalibrationUtils.GetMaturity(
            InstrumentType.FRA, settle, tn, cal, fra.BDConvention);
          curve.AddFRA(tn, tenor.Weight, settle, maturity,
            tenor.MarketPv, fra.ReferenceIndex);
          continue;
        }

        var bond = tenor.Product as Bond;
        if (bond != null)
        {
          // Bond effective and maturity need not to changed at all.
          curve.Add(bond, tenor.MarketPv, tenor.Coupon, tenor.FinSpread, tenor.Weight);
          continue;
        }

        var swap = tenor.Product as Swap;
        if (swap != null)
        {
          var cal = swap.ReceiverLeg.Floating ? swap.PayerLeg.Calendar : swap.ReceiverLeg.Calendar;
          var receiver = ShiftDates(swap.ReceiverLeg, tn, origAsOf, toAsOf, cal);
          var payer = ShiftDates(swap.PayerLeg, tn, origAsOf, toAsOf, cal);
          curve.Add(new Swap(receiver, payer) { Description = swap.Description },
            tenor.MarketPv, tenor.Coupon, tenor.FinSpread, tenor.Weight);
          continue;
        }

        var leg = tenor.Product as SwapLeg;
        if (leg != null)
        {
          curve.Add(ShiftDates(leg, tn, origAsOf, toAsOf, leg.Calendar),
            tenor.MarketPv, tenor.Coupon, tenor.FinSpread, tenor.Weight);
          continue;
        }

        throw new ApplicationException(String.Format(
          "Cannot handle tenor {0} in date shifts", tenor.Name));
      }
      curve.Fit();
    }

    internal static void ShiftDatesAndRefit(
      this SurvivalCurve curve, Dt targetAsOf)
    {
      if (curve == null) return;

      // Here we do everything relative to as-of date.
      curve.SetCurveAsOfDate(targetAsOf);
      if (curve.Calibrator == null || curve.Tenors.IsNullOrEmpty())
        return;

      var calibrator = curve.Calibrator;
      Dt originalAsOf = calibrator.AsOf;
      Dt targetSettle = targetAsOf;
      if (originalAsOf != calibrator.Settle)
        targetSettle = targetAsOf + Dt.Diff(originalAsOf, calibrator.Settle);
      calibrator.AsOf = targetAsOf;
      calibrator.Settle = targetSettle;
      var sc = calibrator as SurvivalFitCalibrator;
      if (sc != null && !sc.ValueDate.IsEmpty())
        sc.ValueDate = targetAsOf + Dt.Diff(originalAsOf, sc.ValueDate);

      CurveUtil.ResetCdsTenorDatesRecalibrate(curve, targetAsOf, targetSettle, ToolkitConfigurator.Settings.ThetaSensitivity.RecalibrateWithNewCdsEffective, ToolkitConfigurator.Settings.ThetaSensitivity.RecalibrateWithRolledCdsMaturity);
    }

    internal static void SetCurveAsOfDate(this CalibratedCurve curve, Dt asOf)
    {
      curve.AsOf = Dt.AddDays(asOf, curve.SpotDays, curve.SpotCalendar);
    }

    private static string GetTenorString(this CurveTenor tenor)
    {
      var s = tenor.Name ??
        (tenor.Product != null ? tenor.Product.Description : null);
      if (String.IsNullOrEmpty(s))
        return null;
      var m = Regex.Match(s,
        @"(?:\d+\s*(?:d(?:ays?)?|w(?:eeks?)?|m(?:onths?)?|y(?:ears?|r)?)|[FGHJKMNQUVXZ]\d|[a-z]{3,3}\d{1,2}|\d+d?\s*[xX]\s*\d+d?|O/N|T/N)$",
        RegexOptions.IgnoreCase);
      return m.Success ? m.Value : null;
    }

    private static int GetBusinessDaysInBetween(Dt begin, Dt end, Calendar calendar)
    {
      if (begin.IsEmpty() || end.IsEmpty())
        return 0;
      if (begin > end)
      {
        var t = begin;
        begin = end;
        end = t;
      }
      int days = 0;
      for (begin = Dt.Add(begin, 1); begin <= end; begin = Dt.Add(begin, 1))
      {
        if (CalendarCalc.IsValidSettlement(calendar,
          begin.Day, begin.Month, begin.Year)) ++days;
      }
      return days;
    }

    class ThetaShiftedTenors : CurveTenorCollection
    {
      internal ThetaShiftedTenors(CurveTenorCollection originalTenors,
        Dt originalAsOf, Dt originalSettle)
      {
        OriginalTenors = originalTenors;
        OriginalAsOf = originalAsOf;
        OriginalSettle = originalSettle;
      }
      internal readonly Dt OriginalAsOf, OriginalSettle;
      internal readonly CurveTenorCollection OriginalTenors;
    }

    private static SwapLeg ShiftDates(
      SwapLeg leg, string tn, Dt origAsOf, Dt toAsOf, Calendar cal)
    {
      var days = GetBusinessDaysInBetween(origAsOf, leg.Effective, cal);
      var settle = Dt.AddDays(toAsOf, days, cal);
      var maturity = DiscountCurveCalibrationUtils.GetMaturity(
        InstrumentType.Swap, settle, tn, cal, leg.BDConvention);
      var cloned = (SwapLeg)leg.ShallowCopy();
      cloned.Effective = settle;
      cloned.Maturity = maturity;
      cloned.FirstCoupon = cloned.LastCoupon = Dt.Empty;
      cloned.CycleRule = CycleRule.None;
      var sp = (IScheduleParams)cloned.Schedule;
      cloned.CycleRule = sp.CycleRule;
      cloned.Maturity = sp.Maturity;
      return cloned;
    }
  }//class ThetaShiftUtil

  #endregion ThetaShiftUtil
}
