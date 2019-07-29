/*
 * BulletCouponUtil.cs
 *
 *   2008. All rights reserved.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Pricers.BasketForNtdPricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Base
{
  /// <summary>
  ///   Utility method to calculate discounted Bonus payment.
  /// </summary>
  ///
  public class BulletCouponUtil
  {
    /// <summary>
    ///  Compute NPV of bonus for unit notional
    /// </summary>
    /// <param name="bullet">BulletConvention object</param>
    /// <param name="discountFactor">discount factor</param>
    /// <param name="survivalProb">survival probability</param>
    /// <returns>NPV of bonus for unit notional</returns>
    public static double GetDiscountedBonus(
      BulletConvention bullet, double discountFactor, double survivalProb)
    {
      if (bullet == null)
        return 0;
      return bullet.CouponRate * discountFactor * survivalProb;
    }

    /// <summary>
    ///  Compute NPV of bonus
    /// </summary>
    /// <param name="bullet">BulletConvention object</param>
    /// <param name="discountFactor">discount factor</param>
    /// <param name="survivalProb">survival probability</param>
    /// <param name="notional">Notional amount</param>
    /// <returns>NPV of bonus</returns>
    public static double GetDiscountedBonus(
      BulletConvention bullet, double discountFactor, double survivalProb, double notional)
    {
      if (bullet == null)
        return 0;
      return bullet.CouponRate * discountFactor * survivalProb * notional;
    }

    /// <summary>
    ///  Compute NPV of bonus
    /// </summary>
    /// <param name="pricer">IPricer</param>
    /// <returns>NPV of bonus</returns>
    public static double GetDiscountedBonus(IPricer pricer)
    {
      double discountFactor = 0, expectedSurvival = 0, notional = 0;
      Dt discountStart = Dt.Empty, discountEnd = Dt.Empty;
      Dt survProbStart = Dt.Empty, survProbEnd = Dt.Empty;
      DiscountCurve discountCurve = null;
      BulletConvention bullet = null;
      
      if (pricer is FTDPricer)
      {
        FTDPricer ftdPricer = (FTDPricer)pricer;
        bullet = ftdPricer.FTD.Bullet;
        discountCurve = ftdPricer.DiscountCurve;
        discountStart = ftdPricer.Settle;
        discountEnd = ftdPricer.FTD.Maturity;
        survProbStart = ftdPricer.AsOf;
        survProbEnd = ftdPricer.FTD.Maturity;
        notional = ftdPricer.Notional;
      }
      else if (pricer is BasketCDSPricer)
      {
        BasketCDSPricer basketCDSPricer = (BasketCDSPricer)pricer;
        bullet = basketCDSPricer.BasketCDS.Bullet;
        discountCurve = basketCDSPricer.DiscountCurve;
        discountStart = pricer.AsOf;
        discountEnd = pricer.Product.Maturity;
        survProbStart = pricer.Settle;
        survProbEnd = ToolkitConfigurator.Settings.CDSCashflowPricer.IncludeMaturityProtection ? Dt.Add(discountEnd, 1) : discountEnd;          
        notional = basketCDSPricer.Notional;
      }
      else if (pricer is CDSCashflowPricer)
      {
        CDSCashflowPricer cdsPricer = (CDSCashflowPricer)pricer;
        bullet = cdsPricer.CDS.Bullet;
        discountCurve = cdsPricer.DiscountCurve;
        discountStart = pricer.AsOf;
        discountEnd = cdsPricer.CDS.Maturity;
        survProbStart = pricer.Settle;
        survProbEnd = ToolkitConfigurator.Settings.CDSCashflowPricer.IncludeMaturityProtection ? Dt.Add(discountEnd, 1) : discountEnd;
        notional = cdsPricer.Notional;
      }
      else if (pricer is SyntheticCDOPricer)
      {
        SyntheticCDOPricer cdoPricer = (SyntheticCDOPricer)pricer;
        bullet = cdoPricer.CDO.Bullet;
        discountCurve = cdoPricer.DiscountCurve;
        discountStart = pricer.AsOf;
        discountEnd = cdoPricer.CDO.Maturity;
        survProbStart = discountStart;
        survProbEnd = cdoPricer.CDO.Maturity;
        notional = cdoPricer.Notional;
      }
      else
      {
        throw new ArgumentException(pricer.ToString() + " does not support Bullet property.");
      }

      discountFactor = discountCurve.DiscountFactor(discountStart, discountEnd);
      expectedSurvival = ComputeExpectedSurvival(pricer, survProbStart, survProbEnd);
      return GetDiscountedBonus(bullet, discountFactor, expectedSurvival, notional);
    }

    private static double ComputeExpectedSurvival(IPricer pricer, Dt start, Dt end)
    {
      if (pricer is CDSCashflowPricer)
      {
        CDSCashflowPricer CDSPricer = (CDSCashflowPricer)pricer;
        return CDSPricer.SurvivalCurve.SurvivalProb(start, end);
      }
      else if (pricer is BasketCDSPricer)
      {
        BasketCDSPricer basketCDSPricer = (BasketCDSPricer)pricer;
        SurvivalCurve[] survCurves = basketCDSPricer.SurvivalCurves;
        double[] weights = basketCDSPricer.BasketCDS.Weights;
        if (survCurves.Length == 1)
          weights = null;
        double totProb = 0.0, pv = 0;
        for (int i = 0; i < survCurves.Length; i++)
        {
          pv = survCurves[i].SurvivalProb(start, end);
          totProb += pv * ((weights != null) ? weights[i] : (1.0 / survCurves.Length));
        }
        return totProb;
      }
      else if (pricer is FTDPricer)
      {
        FTDPricer ftdPricer = (FTDPricer)pricer;
        BasketForNtdPricer basket = ftdPricer.Basket;
        FTD ftd = ftdPricer.FTD;
        int first = ftd.First, covered = ftd.NumberCovered;
        double totProb = 0;
        for (int i = 0; i < covered; ++i)
        {      
          SurvivalCurve curve = basket.NthSurvivalCurve(first + i);
          double survivalProbability = curve.SurvivalProb(start, end);
          totProb += survivalProbability;
        }
        return totProb / covered;     
      }
      else if (pricer is SyntheticCDOPricer)
      {
        SyntheticCDOPricer cdoPricer = (SyntheticCDOPricer)pricer;
        BasketPricer basket = cdoPricer.Basket;
        double attach = cdoPricer.CDO.Attachment, detach = cdoPricer.CDO.Detachment;
        double tranche = Math.Max(0, detach - attach);
        if (tranche < 1E-9)
          return 0;
        double loss = Math.Min(1.0, basket.AccumulatedLoss(end, attach, detach) / tranche);
        return Math.Max(1 - loss, 0);
      }
      else
      {
        throw new ArgumentException("Bullet coupon is not supported for " + pricer.ToString());
      }
    }
  } // class BulletCouponUtil.cs
}