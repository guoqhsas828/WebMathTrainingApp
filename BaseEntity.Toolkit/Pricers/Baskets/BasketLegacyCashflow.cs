

/*
 * BasketLegacyCashflow.cs
 * Copyright(c)   2002-2018. All rights reserved.
*/


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Pricers.Baskets
{

  public static partial class PriceCalc
  {
    /// <summary>
    ///   Calculate price based on a cashflow stream.
    ///   For internal use only.
    ///   <preliminary/>
    /// </summary>
    /// <exclude/>
    internal static double Price(
      Cashflow cashflow, Dt settle,
      DiscountCurve discountCurve,
      ExpectedLossesFn expectedLosses, ExpectedBalanceFn expectedBalance,
      SurvivalCurve counterpartyCurve,
      bool includeFees, bool includeProtection, bool includeSettle,
      double defaultTiming, double accruedFractionOnDefault,
      int step, TimeUnit stepUnit, int stopIdx)
    {
      // Find counterparty default date if any
      Dt counterpartyDfltDate = Dt.Empty;
      if (counterpartyCurve != null && counterpartyCurve.DefaultDate.IsValid())
      {
        counterpartyDfltDate = counterpartyCurve.DefaultDate;
        // If counterparty default before settle, nothing
        if (Dt.Cmp(counterpartyDfltDate, settle) <= 0)
          return 0.0;
      }

      // Check stop index
      if (stopIdx < 0 || stopIdx > cashflow.Count)
        throw new ArgumentException(String.Format("Invalid index {0} for cashflowstream pv", stopIdx));
      if (stopIdx == 0)
        return 0.0;

      // Find the first cahsflow date after the settle
      int firstIdx;
      for (firstIdx = 0; firstIdx < stopIdx; firstIdx++)
      {
        if (Dt.Cmp(cashflow.GetDt(firstIdx), settle) > 0
          || (includeSettle && Dt.Cmp(cashflow.GetDt(firstIdx), settle) == 0))
          break;
      }
      if (firstIdx >= stopIdx)
        return 0.0;

      // This is the accrued before settle
      double guaranteedAccrued = 0;
      double startDf = discountCurve.DiscountFactor(settle);
      double startLosses = expectedLosses(settle);
      double startBalance = expectedBalance(settle);
      if (startBalance < 1E-9)
        return 0;

      double pv = 0.0;
      double losses = startLosses;
      double balance = startBalance;
      double df = startDf;
      Dt prevDate = settle;
      for (int i = firstIdx; i < stopIdx; i++)
      {
        Dt nextDate = cashflow.GetDt(i); // Date for this cashflow

        Dt date = prevDate;
        while (Dt.Cmp(date, nextDate) < 0)
        {
          if (step > 0)
          {
            date = Dt.Add(date, step, stepUnit);
            if (Dt.Cmp(date, nextDate) > 0)
              date = nextDate;
          }
          else
            date = nextDate;

          double prevDf = df;
          df = discountCurve.Interpolate(date);

          double prevLosses = losses;
          losses = expectedLosses(date);

          double avgDf = ((step == 1) && (stepUnit == TimeUnit.Days))
            ? df : ((1.0 - defaultTiming) * prevDf + defaultTiming * df);

          if (includeProtection)
          {
            // lossThisPeriod is already a negative value
            double lossThisPeriod = prevLosses - losses;
            double protEPV = avgDf * lossThisPeriod * cashflow.GetPrincipalAt(i);
            // the protection is included only when counterparty survival
            if (counterpartyCurve != null)
              protEPV *= counterpartyCurve.SurvivalProb(settle, date);
            pv += protEPV;
          }
        } // while

        if (includeFees)
        {
          double prevBalance = balance;
          double accrual = cashflow.GetAccrued(i);
          if ((i == firstIdx))
          {
            Dt accrualStart = (firstIdx > 0 ? cashflow.GetDt(firstIdx - 1) : cashflow.Effective);
            DayCount dayCount = cashflow.DayCount;
            Frequency freq = cashflow.Frequency;
            double accrued = accrual *
                             (Dt.Fraction(accrualStart, nextDate, accrualStart, settle, dayCount, freq)
                              / Dt.Fraction(accrualStart, nextDate, accrualStart, nextDate, dayCount, freq));
            accrual -= accrued;
            guaranteedAccrued += accrued * prevBalance;
            if (includeSettle && (Dt.Cmp(settle, nextDate) == 0))
              df = discountCurve.Interpolate(settle);
          }
          balance = expectedBalance(date);

          // Be careful here:
          //   accruedFractionOnDefault is 0 ==> no accrued paid ==> use the current balance;
          //   accruedFractionOnDefault is 1 ==> full accrued paid ==> use the previous balance;
          double feeNotional = (1 - accruedFractionOnDefault) * balance + accruedFractionOnDefault * prevBalance;

          double feeEPV = feeNotional * (accrual + cashflow.GetAmount(i)) * df;
          // the protection is included only when counterparty survival
          if (counterpartyCurve != null)
            feeEPV *= counterpartyCurve.SurvivalProb(settle, date);
          pv += feeEPV;
        }

        prevDate = nextDate;
      } // for

      return pv / startDf + guaranteedAccrued;
    }

    /// <summary>
    ///   Calculate greeks of price based on a cashflow stream.
    ///   For internal use only.
    ///   <preliminary/>
    /// </summary>
    /// <exclude/>
    internal static void Greeks(
      Cashflow cashflow, Dt settle,
      DiscountCurve discountCurve,
      ExpectedLossesDerFn expectedLossDerivatives,
      ExpectedBalanceDerFn expectedBalanceDerivatives,
      SurvivalCurve counterpartyCurve,
      bool includeFees, bool includeProtection, bool includeSettle,
      double defaultTiming, double accruedFractionOnDefault,
      int step, TimeUnit stepUnit, int stopIdx, double[] retVal)
    {
      for (int i = 0; i < retVal.Length; i++)
        retVal[i] = 0.0;
      // Find counterparty default date if any
      Dt counterpartyDfltDate = Dt.Empty;
      if (counterpartyCurve != null && counterpartyCurve.DefaultDate.IsValid())
      {
        counterpartyDfltDate = counterpartyCurve.DefaultDate;
        // If counterparty default before settle, nothing
        if (Dt.Cmp(counterpartyDfltDate, settle) <= 0)
          return;
      }
      // Check stop index
      if (stopIdx < 0 || stopIdx > cashflow.Count)
        throw new ArgumentException(String.Format("Invalid index {0} for cashflowstream pv", stopIdx));
      if (stopIdx == 0)
        return;
      // Find the first cashflow date after the settle
      int firstIdx;
      for (firstIdx = 0; firstIdx < stopIdx; firstIdx++)
      {
        if (Dt.Cmp(cashflow.GetDt(firstIdx), settle) > 0
          || (includeSettle && Dt.Cmp(cashflow.GetDt(firstIdx), settle) == 0))
          break;
      }
      if (firstIdx >= stopIdx)
        return;
      double[] lossDerPrev = new double[retVal.Length];
      double[] lossDerNext = new double[retVal.Length];
      double[] ePV = new double[retVal.Length];
      int length = includeFees ? retVal.Length : 0;
      double[] balanceDerNext = new double[length];
      double[] balanceDerPrev = new double[length];
      double[] accruedDer = new double[length];
      double startDf = discountCurve.DiscountFactor(settle);
      expectedLossDerivatives(settle, lossDerNext);
      if (includeFees)
      {
        expectedBalanceDerivatives(settle, balanceDerNext);
      }
      double df = startDf;
      Dt prevDate = settle;
      for (int i = firstIdx; i < stopIdx; i++)
      {
        Dt nextDate = cashflow.GetDt(i); // Date for this cashflow
        Dt date = prevDate;
        while (Dt.Cmp(date, nextDate) < 0)
        {
          if (step > 0)
          {
            date = Dt.Add(date, step, stepUnit);
            if (Dt.Cmp(date, nextDate) > 0)
              date = nextDate;
          }
          else
            date = nextDate;
          double prevDf = df;
          df = discountCurve.Interpolate(date);
          lossDerNext.CopyTo(lossDerPrev, 0);
          expectedLossDerivatives(date, lossDerNext);
          double avgDf = ((step == 1) && (stepUnit == TimeUnit.Days))
            ? df : ((1.0 - defaultTiming) * prevDf + defaultTiming * df);
          if (includeProtection)
          {
            double not = cashflow.GetPrincipalAt(i);
            // lossThisPeriod is already a negative value
            for (int k = 0; k < retVal.Length; k++)
              ePV[k] = avgDf * (lossDerPrev[k] - lossDerNext[k]) * not;
            // the protection is included only when counterparty survival
            if (counterpartyCurve != null)
            {
              double ctpSurv = counterpartyCurve.SurvivalProb(settle, date);
              for (int k = 0; k < retVal.Length; k++)
                retVal[k] += ePV[k] * ctpSurv;
            }
            else
            {
              for (int k = 0; k < retVal.Length; k++)
                retVal[k] += ePV[k];
            }
          }
        } // while
        if (includeFees)
        {
          balanceDerNext.CopyTo(balanceDerPrev, 0);
          double accrual = cashflow.GetAccrued(i);
          if ((i == firstIdx))
          {
            Dt accrualStart = (firstIdx > 0 ? cashflow.GetDt(firstIdx - 1) : cashflow.Effective);
            DayCount dayCount = cashflow.DayCount;
            Frequency freq = cashflow.Frequency;
            double accrued = accrual *
                             (Dt.Fraction(accrualStart, nextDate, accrualStart, settle, dayCount, freq)
                              / Dt.Fraction(accrualStart, nextDate, accrualStart, nextDate, dayCount, freq));
            accrual -= accrued;
            for (int k = 0; k < retVal.Length; k++)
            {
              accruedDer[k] = accrued * balanceDerPrev[k];
            }
            if (includeSettle && (Dt.Cmp(settle, nextDate) == 0))
              df = discountCurve.Interpolate(settle);
          }
          expectedBalanceDerivatives(date, balanceDerNext);
          double mult = df * (cashflow.GetAmount(i) + accrual);
          double delta = accruedFractionOnDefault;
          double deltaNot = 1.0 - delta;
          // Be careful here:
          //accruedFractionOnDefault is 0 ==> no accrued paid ==> use the current balance;
          //accruedFractionOnDefault is 1 ==> full accrued paid ==> use the previous balance;
          for (int k = 0; k < retVal.Length; k++)
            ePV[k] = mult * (deltaNot * balanceDerNext[k] + delta * balanceDerPrev[k]);
          if (counterpartyCurve != null)
          {
            double ctpSurv = counterpartyCurve.SurvivalProb(settle, date);
            for (int k = 0; k < retVal.Length; k++)
              retVal[k] += ePV[k] * ctpSurv;
          }
          else
          {
            for (int k = 0; k < retVal.Length; k++)
              retVal[k] += ePV[k];
          }
        }
        prevDate = nextDate;
      }
      if (includeFees)
      {
        for (int k = 0; k < retVal.Length; k++)
        {
          retVal[k] = retVal[k] / startDf + accruedDer[k];
        }
      }
      else
      {
        for (int k = 0; k < retVal.Length; k++)
        {
          retVal[k] /= startDf;
        }
      }
    }

    // Generate simple cashflow stream
    internal static Cashflow GenerateCashflowForFee(
      Dt settle, double coupon,
      Dt effective, Dt firstPrem, Dt maturity, //double fee, Dt feeSettle,
      Currency ccy, DayCount dayCount, Frequency freq, BDConvention roll, Calendar cal,
      SurvivalCurve counterpartyCurve,
      bool floating, DiscountCurve referenceCurve, List<RateReset> rateResets)
    {
      return GenerateCashflowForFee(settle, coupon, effective, firstPrem,
        maturity, ccy, dayCount, freq, roll, cal, counterpartyCurve,
        floating, false, referenceCurve, rateResets);
    }

    // Generate simple cashflow stream
    internal static Cashflow GenerateCashflowForFee(
      Dt settle, double coupon,
      Dt effective, Dt firstPrem, Dt maturity, //double fee, Dt feeSettle,
      Currency ccy, DayCount dayCount, Frequency freq, BDConvention roll, Calendar cal,
      SurvivalCurve counterpartyCurve,
      bool floating, bool funded, DiscountCurve referenceCurve, List<RateReset> rateResets)
    {
      // Find the termination date
      maturity = FindTerminateDate(counterpartyCurve, settle, maturity);

      CashflowFlag flags = CashflowFlag.IncludeMaturityAccrual;
      if (!ToolkitConfigurator.Settings.CashflowPricer.BackwardCompatibleSchedule)
        flags |= CashflowFlag.RespectLastCoupon;

      var schedParams = new ScheduleParams(effective, firstPrem,
        Dt.Empty, maturity, freq, roll, cal, CycleRule.None, flags);
      double principal = funded ? 1.0 : 0.0;

      // Generate cashflow
      Cashflow cf = new Cashflow(settle);
      // if funded call FillFloat; if Libor flag turned on use Libor curve as reference curve 
      // so you don't need to add a reference curve to the cdo pricer.
      if (floating)
      {
        // Floating
        CashflowFactory.FillFloat(cf, settle, schedParams, ccy,
          dayCount, coupon, //freq, roll, cal,
          null/*couponSched*/, principal, null/*amortSched*/,
          referenceCurve, /*reference curve*/
          rateResets, /*reset Rates*/
          0.0, Currency.None, Dt.Empty, // default amount, currency and date
          0.0, Dt.Empty // no fee, for fee is calculated separately
        );
      }
      else
      {
        CashflowFactory.FillFixed(cf, settle, schedParams, ccy, dayCount, coupon,
          null /*couponSched*/, principal, null /*amortSched*/,
          0.0, Currency.None, Dt.Empty, // default amount, currency and date
          0.0, Dt.Empty // no fee, for fee is calculated separately
        );
      }
      return cf;
    }

    // Generate simple cashflow
    internal static Cashflow GenerateCashflowForProtection(
      Dt settle, Dt maturity, Currency ccy, SurvivalCurve counterpartyCurve)
    {
      // Find the termination date
      maturity = FindTerminateDate(counterpartyCurve, settle, maturity);

      // Generate cashflow
      Cashflow cf = new Cashflow(settle);
      cf.Currency = ccy;
      cf.AccruedPaidOnDefault = false; // not used?
      cf.AccruedIncludingDefaultDate = false; // not used?

      cf.Add(settle, maturity, maturity, 0.0, 0.0, 0.0, 0.0, 1.0);

      return cf;
    }

  } // pricecalc


  public static partial class CDXPricerUtil
  {
    /// <summary>
    ///   Helper function to generate cashflows for CDX/LCDX
    /// </summary>
    /// <param name="cashflow">Cashflow to fill</param>
    /// <param name="from">The date from which teh cashflow is filled</param>
    /// <param name="settle">Pricer settle date</param>
    /// <param name="note">CDX or LCDX note</param>
    /// <param name="marketRecoveryRate">Market recovery rate</param>
    /// <param name="referenceCurve">Reference curve for floating note</param>
    /// <param name="rateResets">Rate resets for floating note</param>
    /// <param name="survivalCurves">Survival curves</param>
    /// <param name="recoveryCurves">Recovery curves</param>
    /// <returns>Generated cashflow</returns>
    internal static Cashflow GenerateCashflow(Cashflow cashflow,
      Dt from, Dt settle, CDX note, double marketRecoveryRate,
      DiscountCurve referenceCurve, IList<RateReset> rateResets,
      SurvivalCurve[] survivalCurves, RecoveryCurve[] recoveryCurves)
    {
      return GenerateCashflow(cashflow, from, settle, note.Effective, note.FirstPrem, note.Maturity, note.AnnexDate,
        note.Ccy, note.Premium, note.DayCount, note.Freq, CycleRule.None, note.BDConvention, note.Calendar,
        note.CdxType != CdxType.Unfunded, marketRecoveryRate, referenceCurve,
        note.CdxType == CdxType.FundedFloating, rateResets, survivalCurves, note.Weights,
        recoveryCurves);
    }


    /// <summary>
    ///   Helper function to generate cashflows for simple CreditBasket (eg. CDX/LCDX)
    /// </summary>
    /// <param name="cashflow">Cashflow to fill</param>
    /// <param name="from">The date from which teh cashflow is filled</param>
    /// <param name="settle">Pricer settle date</param>
    /// <param name="effective">effective date for the basket product</param>
    /// <param name="firstPrem">First premium date for the basket product</param>
    /// <param name="maturity">maturity for the basket product</param>
    /// <param name="annexDate">Date the version of the index was issued</param>
    /// <param name="ccy">Currency</param>
    /// <param name="premium"> Deal or original issue premium of index as a number (100bp = 0.01).
    ///                        For a funded floating note this is the spread.</param>
    /// <param name="dayCount"></param>
    /// <param name="freq"></param>
    /// <param name="cycleRule"></param>
    /// <param name="roll"></param>
    /// <param name="cal"></param>
    /// <param name="funded">Is the note funded or unfunded?</param>
    /// <param name="marketRecoveryRate">Market recovery rate</param>
    /// <param name="referenceCurve">Reference curve for floating note</param>
    /// <param name="floating">Floating or Fixed rate cashflow?</param>
    /// <param name="rateResets">Rate resets for floating note</param>
    /// <param name="survivalCurves">Survival curves</param>
    /// <param name="weights">percentage contribution of each name to basket notional</param>
    /// <param name="recoveryCurves">Recovery curves</param>
    /// <returns>Generated cashflow</returns>
    internal static Cashflow GenerateCashflow(Cashflow cashflow,
      Dt from, Dt settle, Dt effective, Dt firstPrem, Dt maturity, Dt annexDate,
      Currency ccy, double premium, DayCount dayCount, Frequency freq, CycleRule cycleRule,
      BDConvention roll, Calendar cal, bool funded, double marketRecoveryRate,
      DiscountCurve referenceCurve, bool floating, IList<RateReset> rateResets,
      SurvivalCurve[] survivalCurves, double[] weights, RecoveryCurve[] recoveryCurves)
    {
      if (cashflow == null)
        cashflow = new Cashflow(from);

      cashflow.RecoveryScheduleInfo = new Cashflow.DefaultRecoveryScheduleInfo();
      const double fee = 0.0;
      Dt feeSettle = Dt.Empty;
      double principal = (funded) ? 1.0 : 0.0;
      double defaultAmount = (funded) ? marketRecoveryRate : marketRecoveryRate - 1.0;
      Currency defaultCcy = ccy;
      Dt defaultDate = Dt.Empty;

      var config = ToolkitConfigurator.Settings.CDSCashflowPricer;

      CashflowFlag flags =
        CashflowFlag.IncludeDefaultDate |
        CashflowFlag.AccruedPaidOnDefault |
        ((config.IncludeMaturityAccrual) ? CashflowFlag.IncludeMaturityAccrual : CashflowFlag.None);

      var schedParams = new ScheduleParams(effective, firstPrem, Dt.Empty, maturity, freq, roll, cal, cycleRule, flags);

      if (!floating)
      {
        CashflowFactory.FillFixed(cashflow, from, schedParams, ccy, dayCount,
                                  premium, null,
                                  principal, null,
                                  defaultAmount, defaultCcy, defaultDate,
                                  fee, feeSettle);
      }

      Dt[] resetDates;
      double[] resetRates;
      RateResetUtil.FromSchedule(rateResets, out resetDates, out resetRates);

      if (floating)
      {
        CashflowFactory.FillFloat(cashflow, from, schedParams, ccy, dayCount,
                                  premium, null,
                                  principal, null,
                                  referenceCurve, rateResets,
                                  defaultAmount, defaultCcy, defaultDate,
                                  fee, feeSettle);
      }

      // Calculate the default payment info
      if (survivalCurves == null || recoveryCurves == null)
        return cashflow;

      if (settle < from)
        settle = from;
      Cashflow.ScheduleInfo dpay = null;
      for (int i = 0; i < survivalCurves.Length; ++i)
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
          defaultAmount = recoveryCurves[i].RecoveryRate(maturity);
          Cashflow work = new Cashflow();
          if (floating)
          {
            CashflowFactory.FillFloat(work, effective, schedParams, ccy, dayCount,
                                      premium, null,
                                      principal, null,
                                      referenceCurve, rateResets,
                                      defaultAmount, defaultCcy, Dt.Empty,
                                      fee, feeSettle);
          }
          else
          {
            CashflowFactory.FillFixed(work, effective, schedParams, ccy, dayCount,
                                      premium, null,
                                      principal, null,
                                      defaultAmount, defaultCcy, Dt.Empty,
                                      fee, feeSettle);
          }

          int last = work.Count - 1;
          if (last < 0 || defaultDate < effective)
          {
            //TODO: this could be a bug, we should continue, not return
            //TODO: because we just handle one survival curve
            // the unlikely case that the credit defaults before effective
            return cashflow;
          }

          if (dpay == null)
          {
            dpay = new Cashflow.ScheduleInfo();
            dpay.Date = settle;
          }
          double w = weights != null ? weights[i] : (1.0 / survivalCurves.Length);
          //Need to manually compute the accrual up to the default date
          double dftAccrual = 0.0;
          double dftAmount = 0.0;
          double dftPeriodFullCoupon = 0.0;
          int rebateIdx = -1;
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
                && dpay.Date >= work.GetDt(workScheduleIdx))
              {
                rebateIdx = workScheduleIdx;
              }

              break;
            }
          }

          dpay.Accrual += dftAccrual * w;
          cashflow.RecoveryScheduleInfo.UpdateRecoveryInfo(defaultDate,
            dftAccrual * w, survivalCurves[i].Name);

          if (rebateIdx >= 0)
          {
            var fullCoupon = dftPeriodFullCoupon * w;
            dpay.Accrual -= fullCoupon;
            cashflow.RecoveryScheduleInfo.UpdateRecoveryInfo(defaultDate,
              -fullCoupon, survivalCurves[i].Name);
          }

          if (funded)
            dpay.Amount += dftAmount * w;
          else
            dpay.Loss += dftAmount * w;
        }

      cashflow.DefaultPayment = dpay;

      return cashflow;
    }


  }

}
