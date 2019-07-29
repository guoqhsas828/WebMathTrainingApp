using System;
using System.Collections.Generic;
using System.Text;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Cashflows.Utils;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Pricers.Baskets
{
  /// <summary>
  ///   For internal use only
  ///   <preliminary/>
  /// </summary>
  /// <exclude/>
  public static partial class PriceCalc
  {
    internal delegate double ExpectedLossesFn(Dt date);
    internal delegate double ExpectedBalanceFn(Dt date);
    internal delegate void ExpectedLossesDerFn(Dt date, double[] retVal);
    internal delegate void ExpectedBalanceDerFn(Dt date, double[] retVal);

    /// <summary>
    ///   Calculate price based on a cashflow stream.
    ///   For internal use only.
    ///   <preliminary/>
    /// </summary>
    /// <exclude/>
    internal static double Price(
      CashflowAdapter cfa, Dt settle,
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
      if (stopIdx < 0 || stopIdx > cfa.Count)
        throw new ArgumentException(String.Format("Invalid index {0} for cashflowstream pv", stopIdx));
      if (stopIdx == 0)
        return 0.0;

      // Find the first cahsflow date after the settle
      int firstIdx;
      for (firstIdx = 0; firstIdx < stopIdx; firstIdx++)
      {
        if (Dt.Cmp(cfa.GetDt(firstIdx), settle) > 0 || (includeSettle && Dt.Cmp(cfa.GetDt(firstIdx), settle) == 0))
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
        Dt nextDate = cfa.GetDt(i);        // Date for this cashflow

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

          double avgDf = ((step == 1) && (stepUnit == TimeUnit.Days)) ? df : ((1.0 - defaultTiming) * prevDf + defaultTiming * df);

          if (includeProtection)
          {
            // lossThisPeriod is already a negative value
            double lossThisPeriod = prevLosses - losses;
            double protEPV = avgDf * lossThisPeriod * cfa.GetPrincipalAt(i);
            // the protection is included only when counterparty survival
            if (counterpartyCurve != null)
              protEPV *= counterpartyCurve.SurvivalProb(settle, date);
            pv += protEPV;
          }
        } // while

        if (includeFees)
        {
          double prevBalance = balance;
          double accrual = cfa.GetAccrued(i);
          if ((i == firstIdx))
          {
            Dt accrualStart = (firstIdx > 0 ? cfa.GetDt(firstIdx - 1) : cfa.Effective);
            DayCount dayCount = cfa.GetDayCount(i);
            Frequency freq = cfa.GetFrequency(i);
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

          double feeEPV = feeNotional * (accrual + cfa.GetAmount(i)) * df;
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
        CashflowAdapter cashflow, Dt settle,
        DiscountCurve discountCurve,
        ExpectedLossesDerFn expectedLossDerivatives, ExpectedBalanceDerFn expectedBalanceDerivatives,
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
        if (Dt.Cmp(cashflow.GetDt(firstIdx), settle) > 0 || (includeSettle && Dt.Cmp(cashflow.GetDt(firstIdx), settle) == 0))
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
        Dt nextDate = cashflow.GetDt(i);       // Date for this cashflow
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
          double avgDf = ((step == 1) && (stepUnit == TimeUnit.Days)) ? df : ((1.0 - defaultTiming) * prevDf + defaultTiming * df);
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
            DayCount dayCount = cashflow.GetDayCount(i);
            Frequency freq = cashflow.GetFrequency(i);
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

    // Find when the contract terminates
    internal static Dt FindTerminateDate(
      SurvivalCurve counterpartyCurve,
      Dt settle,
      Dt maturity)
    {
      // If the counterparty has a default date set and the default date
      // is before the maturity, than the contract terminates on the default date.
      if (counterpartyCurve != null && counterpartyCurve.DefaultDate.IsValid())
      {
        Dt counterpartyDfltDate = counterpartyCurve.DefaultDate;
        // If counterparty default before settle, nothing
        if (Dt.Cmp(counterpartyDfltDate, settle) <= 0)
          return settle;
        if (Dt.Cmp(counterpartyDfltDate, maturity) < 0)
          return counterpartyDfltDate;
      }
      return maturity;
    }

    internal static PaymentSchedule GeneratePsForFee(
      Dt settle, double coupon,
      Dt effective, Dt firstPrem, Dt maturity, //double fee, Dt feeSettle,
      Currency ccy, DayCount dayCount, Frequency freq, BDConvention roll, Calendar cal,
      SurvivalCurve counterpartyCurve,
      bool floating, bool funded, DiscountCurve referenceCurve, List<RateReset> rateResets,
      DiscountCurve discountCurve)
    {
      var terminateDate = FindTerminateDate(counterpartyCurve, settle, maturity);

      CashflowFlag flags = CashflowFlag.IncludeMaturityAccrual;
      if (!ToolkitConfigurator.Settings.CashflowPricer.BackwardCompatibleSchedule)
        flags |= CashflowFlag.RespectLastCoupon;

      var schedParams = new ScheduleParams(effective, firstPrem,
        Dt.Empty, terminateDate, freq, roll, cal, CycleRule.None, flags);
      var schedule = Schedule.CreateScheduleForCashflowFactory(schedParams);
      return GeneratePsForFee(settle, Dt.Empty, discountCurve,
        referenceCurve, counterpartyCurve, schedule,
        coupon, ccy, dayCount,
        floating, false/*funded*/, rateResets);
    }

    internal static PaymentSchedule GeneratePsForFee(
      Dt from, Dt to, DiscountCurve discountCurve, DiscountCurve referenceCurve,
      SurvivalCurve counterpartyCurve, Schedule schedule, double coupon, Currency ccy, 
      DayCount dayCount, bool floating, bool funded, List<RateReset> rateResets)
    {
      // Find the termination date
      double principal = funded ? 1.0 : 0.0;

      // Generate cashflow
      var ps = new PaymentSchedule();
      var flag = schedule.CashflowFlag | CashflowFlag.AccrueOnCycle;
      // if funded call FillFloat; if Libor flag turned on use Libor curve as reference curve 
      // so you don't need to add a reference curve to the cdo pricer.
      if (floating)
      {
        ps.AddPayments(LegacyCashflowCompatible.GetRegularPayments(from,
          to, schedule, ccy, dayCount, 
          new PaymentGenerationFlag(flag, false, false),
          coupon, null, principal, null, referenceCurve, discountCurve, rateResets));
      }
      else
      {
        ps.AddPayments(LegacyCashflowCompatible.GetRegularPayments(from, to,
          schedule, ccy, dayCount, 
          new PaymentGenerationFlag(flag, false, false),
          coupon, null, principal, null, referenceCurve, discountCurve, rateResets, true));
      }
      return ps;
    }

    // Generate simple cashflow
    internal static PaymentSchedule GeneratePsForProtection(
      Dt settle, Dt maturity, Currency ccy, SurvivalCurve counterpartyCurve)
    {
      
      var ps = new PaymentSchedule();
      //we need to add in one empty fixed payment to make CashflowAdapter properties happy
      ps.AddPayment(new FixedInterestPayment(settle, maturity, ccy, Dt.Empty, 
        Dt.Empty, settle, maturity, Dt.Empty, 1.0, 0.0, 
        DayCount.Actual365Fixed, Frequency.None));

      ps.AddPayment(new RecoveryPayment(settle, maturity, 1.0, ccy)
        {IsFunded = true, Notional = 1.0});

      return ps;
    }
  } // class PriceCalc
}//namespace
