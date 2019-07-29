//
// CDXPricerUtil.cs
//  -2008. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows.Utils;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Pricers.Baskets
{
  /// <summary>
  /// Credit index pricing helper functions
  /// </summary>
  public static partial class CDXPricerUtil
  {
    #region PaymentSchedule

    // This function doesn't handle the property cashflow.RecoveryScheduleInfo.
    // Its analogous function GenerateCashflow is only used in the risk, and the
    // toolkit only uses the default payment part.
    internal static PaymentSchedule GetPaymentSchedule(PaymentSchedule ps,
      Dt from, Dt settle, Dt effective, Dt firstPrem, Dt maturity, Dt annexDate,
      Currency ccy, double premium, DayCount dayCount, Frequency freq, CycleRule cycleRule,
      BDConvention roll, Calendar cal, bool funded, double marketRecoveryRate,
      DiscountCurve discountCurve, DiscountCurve referenceCurve, bool floating, 
      IList<RateReset> rateResets, SurvivalCurve[] survivalCurves, double[] weights, 
      RecoveryCurve[] recoveryCurves)
    {
      if(ps == null)
        ps = new PaymentSchedule(); 
      else
        ps.Clear();

      double principal = (funded) ? 1.0 : 0.0;
      Dt defaultDate = Dt.Empty;

      var config = ToolkitConfigurator.Settings.CDSCashflowPricer;

      CashflowFlag flags = CashflowFlag.IncludeDefaultDate
        | CashflowFlag.AccruedPaidOnDefault
        | ((config.IncludeMaturityAccrual) 
        ? CashflowFlag.IncludeMaturityAccrual : CashflowFlag.None);

      var schedParams = new ScheduleParams(effective, firstPrem,
        Dt.Empty, maturity, freq, roll, cal, cycleRule, flags);
      var schedule = Schedule.CreateScheduleForCashflowFactory(schedParams);
      if (floating)
      {
        ps.AddPayments(LegacyCashflowCompatible.GetRegularPayments(from,
          Dt.Empty, schedule, ccy, dayCount, 
          new PaymentGenerationFlag(flags, false, false),
          premium, null, principal, null, referenceCurve, discountCurve, rateResets));
      }
      else
      {
        ps.AddPayments(LegacyCashflowCompatible.GetRegularPayments(from,
          Dt.Empty, schedule, ccy, dayCount, 
          new PaymentGenerationFlag(flags, false, false),
          premium, null, principal, null, referenceCurve, 
          discountCurve, rateResets, true));
      }

      // add recovery payment
      ps.GetRecoveryPayments(settle, false, dt => marketRecoveryRate, funded, flags);

      // there is no default payment, return the ps we have.
      if (survivalCurves == null || referenceCurve == null)
      {
        return ps;
      }

      // Deal with the default payment
      if (settle < from)
        settle = from;
      
      double defaultAccrual = 0.0;
      double recoveryRate = 0.0;
      for (int i = 0; i < survivalCurves.Length; ++i)
      {
        if (
          // we don't have rights to this recovery because this version of index was issued after the default
          annexDate >= survivalCurves[i].DefaultDate ||
          // we're valuing as-of before the default occurred
          survivalCurves[i].DefaultDate >= settle ||
          recoveryCurves[i] == null || recoveryCurves[i].JumpDate <= settle)
        {
          continue;
        }
        else
        {
          defaultDate = survivalCurves[i].DefaultDate;
          var workps = new PaymentSchedule();

          if (floating)
          {
            workps.AddPayments(LegacyCashflowCompatible.GetRegularPayments(effective,
              Dt.Empty, schedule, ccy, dayCount,
              new PaymentGenerationFlag(flags, false, false),
              premium, null, principal, null, referenceCurve, discountCurve, rateResets));
          }
          else
          {
            workps.AddPayments(LegacyCashflowCompatible.GetRegularPayments(effective,
              Dt.Empty, schedule, ccy, dayCount,
              new PaymentGenerationFlag(flags, false, false),
              premium, null, principal, null, referenceCurve, discountCurve, rateResets, true));
          }

          double w = weights?[i] ?? (1.0 / survivalCurves.Length);
          //Need to manually compute the accrual up to the default date
          double dftAccrual = 0.0;
          double dftAmount = 0.0;
          double dftPeriodFullCoupon = 0.0;
          int rebateIdx = -1;

          var work = new CashflowAdapter(workps);

          int last = work.Count - 1;
          if (last < 0 || defaultDate < effective)
          {
            continue;
          }

          for (int workScheduleIdx = 0; workScheduleIdx < work.Count; ++workScheduleIdx)
          {
            if (defaultDate < work.GetDt(workScheduleIdx)
              && defaultDate >= work.GetStartDt(workScheduleIdx))
            {
              bool accrueOnCycle = (flags & CashflowFlag.AccrueOnCycle) != 0;
              bool includeDfltDate = (flags & CashflowFlag.IncludeDefaultDate) != 0;
              Dt pstart = !accrueOnCycle && workScheduleIdx > 0
                ? work.GetDt(workScheduleIdx - 1)
                : work.GetStartDt(workScheduleIdx);
              Dt pend = work.GetEndDt(workScheduleIdx);
              Dt start = pstart;
              Dt end = includeDfltDate ? Dt.Add(defaultDate, 1) : defaultDate;
              double accrualPeriod = Dt.Fraction(pstart, pend, start, end, dayCount, Frequency.None)
                / work.GetPeriodFraction(workScheduleIdx);
              dftPeriodFullCoupon = work.GetAccrued(workScheduleIdx);
              dftAccrual = accrualPeriod * work.GetAccrued(workScheduleIdx);
              dftAmount = recoveryCurves[i].RecoveryRate(maturity) - (funded ? 0.0 : 1.0);

              if (config.SupportAccrualRebateAfterDefault
                && defaultDate < work.GetDt(workScheduleIdx)
                && settle >= work.GetDt(workScheduleIdx))
              {
                rebateIdx = workScheduleIdx;
              }

              break;
            }
          }

          defaultAccrual += dftAccrual * w;

          if (rebateIdx >= 0)
          {
            var fullCoupon = dftPeriodFullCoupon * w;
            defaultAccrual -= fullCoupon;
          }

          if (funded)
            recoveryRate += dftAmount * w;
          else
            recoveryRate += (dftAmount - 1) * w;
        }
      }

      // if both default accrual and amount equal to zero, we don't need to add in
      if (!defaultAccrual.AlmostEquals(0.0) || !recoveryRate.AlmostEquals(0.0))
      {
        ps.AddPayment(new DefaultSettlement(settle, settle, ccy, principal,
          recoveryRate, defaultAccrual, funded));
      }
      return ps;
    }


    #endregion PaymentSchedule

    #region Effective Notional
    /// <summary>
    ///   The proportion of the effective names in the total original notional.
    /// </summary>
    /// <remarks>
    ///   The effective names are the credits not defaulted
    ///   or defaulted but not settled by the settle date.
    /// </remarks>
    /// <param name="settle">Settle date</param>
    /// <param name="survivalCurves">Array of survival curves</param>
    /// <param name="weights">
    ///   Weights by names or null.  If not null, it must have the same length
    ///   as the survival curves and the weights must sum up to one.
    ///   Both preconditions are NOT checked.
    /// </param>
    /// <param name="annexDate">start date for this version of the index, important for excluding events that only pertain to previous versions</param>
    /// <returns>Effective factor</returns>
    internal static double EffectiveFactor(
      Dt settle, SurvivalCurve[] survivalCurves, double[] weights, Dt annexDate
      )
    {
      if (survivalCurves == null)
        return 1.0;
      else if (weights == null)
      {
        double weight = 0.0;
        for (int i = 0; i < survivalCurves.Length; i++)
        {
          if (IsIncluded(survivalCurves[i], settle, annexDate))
            weight += 1.0;
        }
        return weight / survivalCurves.Length;
      }
      else
      {
        double sumWeight = 0.0;
        double weight = 0.0;
        for (int i = 0; i < survivalCurves.Length; i++)
        {
          if (IsIncluded(survivalCurves[i], settle, annexDate))
            weight += weights[i];
          sumWeight += weights[i];
        }
        return weight / sumWeight;
      }
    }

    /// <summary>
    ///   The proportion of the effective names in the total original notional.
    /// </summary>
    /// <remarks>
    ///   The effective names are the credits not defaulted
    ///   or defaulted but not settled by the settle date.
    /// </remarks>
    /// <param name="settle">Settle date</param>
    /// <param name="survivalCurves">Array of survival curves</param>
    /// <param name="weights">
    ///   Weights by names or null.  If not null, it must have the same length
    ///   as the survival curves and the weights must sum up to one.
    ///   Both preconditions are NOT checked.
    /// </param>
    /// <param name="annexDate">start date for this version of the index, important for excluding events that only pertain to previous versions</param>
    /// <returns>Effective factor</returns>
    internal static double MarkToMarketFactor(
      Dt settle, SurvivalCurve[] survivalCurves, double[] weights, Dt annexDate
      )
    {
      if (survivalCurves == null)
        return 1.0;
      else if (weights == null)
      {
        double weight = 0.0;
        for (int i = 0; i < survivalCurves.Length; i++)
        {
          if (!IsDefaultedAndRecoverySettled(survivalCurves[i], settle, annexDate))
            weight += 1.0;
        }
        return weight / survivalCurves.Length;
      }
      else
      {
        double sumWeight = 0.0;
        double weight = 0.0;
        for (int i = 0; i < survivalCurves.Length; i++)
        {
          if (!IsDefaultedAndRecoverySettled(survivalCurves[i], settle, annexDate))
            weight += weights[i];
          sumWeight += weights[i];
        }
        return weight / sumWeight;
      }
    }

    /// <summary>
    ///   The proportion of the survived names in the total original notional.
    /// </summary>
    /// <remarks>
    ///   The survived names are the credits not defaulted.
    ///   All the defaulted names, no mater settled or not, are excluded.
    /// </remarks>
    /// <param name="settle">Settle date</param>
    /// <param name="survivalCurves">Array of survival curves</param>
    /// <param name="weights">
    ///   Weights by names or null.  If not null, it must have the same length
    ///   as the survival curves and the weights must sum up to one.
    ///   Both preconditions are NOT checked.
    /// </param>
    /// <returns>Current factor</returns>
    public static double CurrentFactor(
      Dt settle, SurvivalCurve[] survivalCurves, double[] weights
      )
    {
      if (survivalCurves == null)
        return 1.0;
      else if (weights == null)
      {
        double weight = 0.0;
        for (int i = 0; i < survivalCurves.Length; i++)
        {
          if (NotDefaulted(survivalCurves[i], settle))
            weight += 1.0;
        }
        return weight / survivalCurves.Length;
      }
      else
      {
        double sumWeight = 0.0;
        double weight = 0.0;
        for (int i = 0; i < survivalCurves.Length; i++)
        {
          if (NotDefaulted(survivalCurves[i], settle))
            weight += weights[i];
          sumWeight += weights[i];
        }
        return weight / sumWeight;
      }
    }

    /// <summary>
    ///   Whether a curve is included in current notional
    /// </summary>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="settle">Settle date</param>
    /// <param name="annexDate">Annex date</param>
    /// <returns>True or false</returns>
    private static bool IsIncluded(SurvivalCurve survivalCurve, Dt settle, Dt annexDate)
    {
      return (survivalCurve.DefaultDate.IsEmpty()
        || survivalCurve.DefaultDate > settle
        || (survivalCurve.SurvivalCalibrator != null
           && survivalCurve.SurvivalCalibrator.RecoveryCurve != null
           && survivalCurve.SurvivalCalibrator.RecoveryCurve.DefaultSettlementDate > settle
           && annexDate < survivalCurve.DefaultDate));
    }

    /// <summary>
    ///   Whether a curve is included in effective notional
    /// </summary>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="settle">Settle date</param>
    /// <param name="annexDate">Annex date</param>
    /// <returns>True or false</returns>
    public static bool IsDefaultedAndRecoverySettled(SurvivalCurve survivalCurve, Dt settle, Dt annexDate)
    {
      if (NotDefaulted(survivalCurve, settle))
        return false;

      if (survivalCurve.SurvivalCalibrator == null || survivalCurve.SurvivalCalibrator.RecoveryCurve == null)
        return false;

      return survivalCurve.SurvivalCalibrator.RecoveryCurve.DefaultSettlementDate <= settle
           || annexDate >= survivalCurve.SurvivalCalibrator.RecoveryCurve.DefaultSettlementDate;
    }

    /// <summary>
    ///   Whether a curve is not defaulted on the settle
    /// </summary>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="settle">Settle date</param>
    /// <returns>True or false</returns>
    private static bool NotDefaulted(SurvivalCurve survivalCurve, Dt settle)
    {
      return (survivalCurve.DefaultDate.IsEmpty()
        || survivalCurve.DefaultDate > settle);
    }
    #endregion Effective Notional

    #region Market Value and Accrued
    /// <summary>
    ///   Calculates the market value for a given quote.
    /// </summary>
    /// <param name="pricer">The CDX/LCDX pricer.</param>
    /// <param name="marketQuote">The market quote.</param>
    /// <param name="quotingConvention">The quoting convention.</param>
    /// <param name="getEquivalentCDSPricer">The delegate to create
    ///   an equivalent CDS pricer based on the market quote.</param>
    /// <returns>Market value.</returns>
    internal static double MarketValue(ICDXPricer pricer,
      double marketQuote, QuotingConvention quotingConvention,
      Func<CDSCashflowPricer> getEquivalentCDSPricer)
    {
      if (quotingConvention == QuotingConvention.FlatPrice)
      {
        CDX cdx = (CDX)pricer.Product;
        double flatPv = cdx.CdxType == CdxType.FundedFloating
          || cdx.CdxType == CdxType.FundedFixed ? marketQuote : (marketQuote - 1);
        double pv = flatPv * pricer.EffectiveNotional + pricer.Accrued();
        return pv;
      }
      else
      {
        double pv = 0;
        double currentNotional = pricer.CurrentNotional;
        if (Math.Abs(currentNotional) >= 1E-15)
        {
          CDSCashflowPricer cdsPricer = getEquivalentCDSPricer();
          pv += cdsPricer.ProductPv() - (double.IsNaN(pricer.MarkToMarketFactor)
                  ? cdsPricer.Accrued() * (1 - (currentNotional / cdsPricer.Notional))
                  : 0.0);
        }

        if (double.IsNaN(pricer.MarkToMarketFactor))
        {
          var ds = pricer.GeneratePayments(null, pricer.Settle)
            .GetPaymentsByType<DefaultSettlement>().FirstOrDefault();

          if (ds != null)
            pv += ds.Accrual * pricer.Notional;
        }
        return pv;
      }
    }

    /// <summary>
    ///   Calculates the accrued for a CDX/LCDX pricer.
    /// </summary>
    /// <param name="pricer">The CDX/LCDX pricer.</param>
    /// <param name="getEquivalentCDSPricer">The delegate to get
    ///   an equivalent CDS pricer based on the CDX/LCDX product.</param>
    /// <returns>Accrued.</returns>
    internal static double Accrued(ICDXPricer pricer,
      Func<CDSCashflowPricer> getEquivalentCDSPricer)
    {
      double accrued = 0;
      double currentNotional = pricer.CurrentNotional;
      if (Math.Abs(currentNotional) >= 1E-15)
      {
        // The accrued from the names not defaulted (those included
        // in currentNotional).
        CDSCashflowPricer cdsPricer = getEquivalentCDSPricer();
        accrued += cdsPricer.Accrued() * (double.IsNaN(pricer.MarkToMarketFactor)
                     ? currentNotional / cdsPricer.Notional
                     : 1.0);
      }

      if (double.IsNaN(pricer.MarkToMarketFactor))
      {
        var ds = pricer.GeneratePayments(null, pricer.Settle)
          .GetPaymentsByType<DefaultSettlement>().FirstOrDefault();

        if (ds != null)
          accrued += ds.Accrual * pricer.Notional;
      }

      return accrued;
    }
    #endregion Market Value and Accrued

    #region CreatePricers
    /// <summary>
    ///   Create a CDX pricer based on the product type
    /// </summary>
    /// <param name="cdx"></param>
    /// <param name="asOf"></param>
    /// <param name="settle"></param>
    /// <param name="discountCurve"></param>
    /// <param name="survivalCurves"></param>
    /// <returns></returns>
    internal static ICDXPricer CreateCdxPricer(
      CDX cdx, Dt asOf, Dt settle, DiscountCurve discountCurve, SurvivalCurve[] survivalCurves)
    {
      ICDXPricer pricer;
      if (cdx is LCDX)
      {
        pricer = new LCDXPricer((LCDX)cdx, asOf, settle, discountCurve, survivalCurves);
        pricer.MarketQuote = 1.0; // dummy quote
      }
      else
      {
        pricer = new CDXPricer(cdx, asOf, settle, discountCurve, survivalCurves);
        pricer.MarketQuote = cdx.Premium; // dummy quote, CDXPricer.Accrued() requires this to be set.
      }
      return pricer;
    }

    #endregion CreatePricers

    #region Common

    /// <summary>
    /// Create a market standard flat survival curve for a CDX note
    /// </summary>
    internal static SurvivalCurve FitFlatSurvivalCurve(Dt asOf, Dt settle, CDX cdx, double marketPremium, double marketRecoveryRate, DiscountCurve discountCurve, IList<RateReset> rateResets)
    {
      SurvivalCurve survivalCurve;
      if (double.IsNaN(marketPremium))
        throw new ArgumentException("Cannot fit invalid market premium");
      else if (marketPremium < 0.0)
        throw new ArgumentOutOfRangeException("marketPremium",
          "CDX market premium cannot be negative: " + marketPremium);

      if (ToolkitConfigurator.Settings.SurvivalCalibrator.UseNaturalSettlement)
      {
        // Manually find the first premium date after the settle.
        // Note: Cannot rely on SurvivalCurve.AddCDS() to determine the first premium date.
        //       For example, when effective is 27/3/2007 and settle is 22/5/2007,
        //       SurvivalCurve.AddCDS() gives the first premium on 20/9/2007 instead of 20/6/2007.  HJ 21May07
        Dt firstPrem = cdx.FirstPrem;
        Dt maturity = cdx.Maturity;

        // Calibration settle is actually the protection start date.
        // For forward cdx, protection start with the effective
        if (Dt.Cmp(settle, cdx.Effective) <= 0)
        {
          settle = cdx.Effective;
        }
        else
        {
          while (Dt.Cmp(firstPrem, settle) <= 0)
            firstPrem = Dt.CDSRoll(firstPrem, false);
          if (Dt.Cmp(firstPrem, maturity) > 0)
            firstPrem = maturity;
        }

        // Create a market level curve using the standard 40pc recovery
        SurvivalCalibrator calibrator = new SurvivalFitCalibrator(asOf, settle,
          marketRecoveryRate, discountCurve);
        survivalCurve = new SurvivalCurve(calibrator);
        survivalCurve.Ccy = cdx.Ccy;
        survivalCurve.AddCDS("None", cdx.Maturity, 0.0 /*note.Fee*/, firstPrem,
          marketPremium, cdx.DayCount,
          cdx.Freq, cdx.BDConvention, cdx.Calendar);
        CDS cds = (CDS) survivalCurve.Tenors[0].Product;
        if (cdx.LastCoupon > firstPrem) cds.LastCoupon = cdx.LastCoupon;
        cds.CycleRule = cdx.CycleRule;
        cds.CashflowFlag = cdx.CashflowFlag;
      }
      else
      {
        // Backward compatible branch
        //
        // Create a market level curve using the standard 40pc recovery
        if (Dt.Cmp(settle, cdx.Effective) <= 0)
        {
          // For forward cdx, protection start with the effective
          settle = cdx.Effective;
        }
        SurvivalCalibrator calibrator = new SurvivalFitCalibrator(asOf, settle,
          marketRecoveryRate, discountCurve);
        survivalCurve = new SurvivalCurve(calibrator);
        survivalCurve.AddCDS("None", cdx.Maturity, marketPremium, cdx.DayCount,
          cdx.Freq, cdx.BDConvention, cdx.Calendar);
      }

      if (cdx.CdxType != CdxType.Unfunded)
      {
        CDS cds = (CDS) survivalCurve.Tenors[0].Product;
        if (cdx.CdxType == CdxType.FundedFloating)
        {
          cds.CdsType = CdsType.FundedFloating;
          ((SurvivalFitCalibrator) survivalCurve.Calibrator).RateResets =
            CloneUtil.CloneToGenericList(rateResets);
        }
        else
          cds.CdsType = CdsType.FundedFixed;
        survivalCurve.Tenors[0].MarketPv = 1;
      }

      // In the modified Black model of CDX option, the solver and integral routines may occasionally
      // call this function with 0 market premium.  In theory, 0 market premium is a perfectly possible
      // case in which the default probability is zero.  We construct such a curve by simply using setting
      // zero hazard rate without going to calibrator.fit() rootine.  This is much faster and more
      // accurate approach.
      if (marketPremium == 0.0)
        survivalCurve.Set(new SurvivalCurve(asOf, 0.0));
      else
        survivalCurve.Fit();

      return survivalCurve;
    }

    /// <summary>
    /// Convert a CDX Market Price to an equivalent spread
    /// </summary>
    internal static double CDXPriceToSpread(Dt asOf, Dt settle, CDX cdx, double price, DiscountCurve discountCurve, SurvivalCurve[] survivalCurves, double marketRecoveryRate, double currentRate)
    {
      CDXPricer localPricer = new CDXPricer(cdx, asOf, settle, discountCurve, survivalCurves, 0.01);
      if (cdx.CdxType == CdxType.FundedFloating)
        localPricer.CurrentRate = currentRate;
      localPricer.MarketRecoveryRate = marketRecoveryRate;
      double spread = localPricer.PriceToSpread(price);
      return spread;
    }

    /// <summary>
    /// Convert a CDX Market premium to a price
    /// </summary>
    internal static double CDXSpreadToPrice(Dt asOf, Dt settle, CDX cdx, double spread, DiscountCurve discountCurve, SurvivalCurve[] survivalCurves, double marketRecoveryRate, double currentRate)
    {
      CDXPricer localPricer = new CDXPricer(cdx, asOf, settle, discountCurve, survivalCurves, 0.01);
      if (cdx.CdxType == CdxType.FundedFloating)
        localPricer.CurrentRate = currentRate;
      localPricer.MarketRecoveryRate = marketRecoveryRate;
      double price = localPricer.SpreadToPrice(spread);
      return price;
    }


    #endregion Common
  } // class CDXPricerUtil
}
