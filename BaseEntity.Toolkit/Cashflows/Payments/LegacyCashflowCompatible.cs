
/*
 * LegacyCashflowCompatible.cs
 * Copyright(c)   2002-2018. All rights reserved.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Cashflows.Utils
{
  internal static class LegacyCashflowCompatible
  {
    #region Price evaluation

    internal static bool TryGetValueOfWillDefault(
      PaymentSchedule ps, Dt asOf, Dt settle, Curve discountCurve,
      DefaultRiskCalculator defaultRisk, double defaultRate,
      bool includeFees, bool includeProtection, bool discountAccrued,
      out double pv)
    {
      pv = 0;

      if (defaultRisk == null) return false;

      var defaultDate = defaultRisk.DefaultDate;
      if (defaultDate.IsEmpty()) return false;

      bool defaultOnSettle;
      {
        var cmp = Dt.Cmp(defaultDate, settle);
        if (cmp < 0)
          return true;

        defaultOnSettle = cmp == 0;
        if (defaultOnSettle && defaultRisk.IsPrepaid)
          return true;
      }

      var discountFn = GetFn(discountCurve, settle);
      double accrued = 0;
      foreach (var payment in ps)
      {
        var payDt = payment.PayDt;
        if (payDt > defaultDate)
          continue;

        var ip = payment as InterestPayment;
        if (ip != null)
        {
          if (ip.PeriodEndDate < settle) continue;

          var ipcmp = Dt.Cmp(ip.PeriodEndDate, defaultDate);
          // Default on settle
          if (ipcmp == 0 && defaultOnSettle)
          {
            if (includeProtection)
              pv += defaultRate*ip.Notional;

            if (includeFees && ip.AccrualStart < settle)
            {
              if (discountAccrued)
                pv += ip.DomesticAmount;
              else
                accrued += ip.DomesticAmount;
            }

            break;
          }

          if (ipcmp < 0)
          {
            if (includeFees)
            {
              var feeDf = discountFn(payDt);
              if (ip.AccrualStart < settle && discountAccrued)
              {
                double accrual;
                accrued += ip.Accrued(settle, out accrual);
                pv += accrual*feeDf;
              }
              else
              {
                pv += ip.DomesticAmount*feeDf;
              }
            }
            continue;
          }

          // Now we have ip.PeriodEndDate >= defaultDate
          if (includeFees)
          {
            var riskyDiscount = defaultRisk.RiskyDiscount(ip, discountFn);
            pv += ip.DomesticAmount*riskyDiscount;
          }
          if (includeProtection)
          {
            var riskyDiscount = defaultRisk.Protection(ip, discountFn);
            pv += defaultRate*ip.Notional*riskyDiscount;
          }

          continue;
        }

        if (includeFees)
        {
          var feeDf = discountFn(payDt);
          pv += payment.DomesticAmount*feeDf;
        }
      }

      pv = accrued + pv*discountCurve.Interpolate(asOf, settle);
      return true;
    }

    internal static Func<Dt, double> GetFn(Curve curve, Dt settle)
    {
      if (curve == null) return null;
      var baseValue = curve.Interpolate(settle);
      if (baseValue.AlmostEquals(1.0) || baseValue.AlmostEquals(0.0))
        return dt => dt < settle ? 1.0 : curve.Interpolate(dt);
      return dt => dt <= settle ? 1.0 : curve.Interpolate(dt)/baseValue;
    }

    #endregion

    #region Recoveries

    /// <summary>
    /// Adds the contingent recovery payments contingent
    /// to the specified bond payment schedule
    /// </summary>
    /// <param name="payments">The payments.</param>
    /// <param name="settle">The settlement date</param>
    /// <param name="creditRiskToPaymentDate">creditRiskToPaymentDate</param>
    /// <param name="recoveryFunction">The recovery function.</param>
    /// <param name="isFunded">if set to <c>true</c> [is funded].</param>
    /// <param name="flag">The cash flow flag.</param>
    /// <returns>PaymentSchedule.</returns>
    internal static IReadOnlyList<RecoveryPayment> GetRecoveryPayments(
      this PaymentSchedule payments, Dt settle,
      bool creditRiskToPaymentDate,
      Func<Dt, double> recoveryFunction,
      bool isFunded = false,
      CashflowFlag flag = CashflowFlag.None)
    {
      if (payments == null) return null;

      List<RecoveryPayment> list = null;
      foreach (var payment in payments)
      {
        var ip = payment as InterestPayment;
        if (ip == null) continue;

        Dt periodEnd = ip.PeriodEndDate;
        Dt begin = creditRiskToPaymentDate
          ? ip.PreviousPaymentDate : ip.AccrualStart;
        Dt end = creditRiskToPaymentDate ? ip.PayDt : periodEnd;

        // Now add the contingent recovery payment
        if (recoveryFunction == null) continue;
        var recovery = recoveryFunction(end);
        if ((isFunded && recovery.AlmostEquals(0.0)) ||
          (!isFunded && recovery.AlmostEquals(1.0)))
        {
          continue;
        }

        if (list == null) list = new List<RecoveryPayment>();
        list.Add(new RecoveryPayment(begin, end, recovery, ip.Ccy)
        {
          Notional = ip.Notional,
          IsFunded = isFunded,
          IncludeEndDateProtection = ip.IncludeEndDateProtection,
        });
      }

      return list;
    }

    #endregion

    #region Generate payment schedule

    internal static DefaultSettlement GetDefaultSettlement(
      PaymentSchedule ps,
      double recoveryRate, Currency defaultCcy,
      Dt defaultDate, Dt defaultPaymentDate,
      PaymentGenerationFlag flag)
    {
      if (ps.Count == 0 || defaultDate.IsEmpty())
        return null;

      Dt lastPayDt = ps.GetPaymentDates().Max();
      if (defaultDate > lastPayDt)
        return null;

      var lastPayments = ps.GetPaymentsOnDate(lastPayDt);
      ps.RemovePayments(lastPayDt);
      var ip = lastPayments.OfType<InterestPayment>().Single();

      DefaultSettlement defaultSettlement = null;
      foreach (var payment in GenerateDefaultPayments(ip,
        recoveryRate, defaultCcy, defaultDate, defaultPaymentDate, flag))
      {
        var ds = payment as DefaultSettlement;
        if (ds == null)
        {
          ps.AddPayment(payment);
          continue;
        }
        Debug.Assert(defaultSettlement == null);
        defaultSettlement = ds;
      }
      return defaultSettlement;
    }

    /// <summary>
    /// Generates the payments for the period where the default occurs
    /// </summary>
    /// <param name="fullInterestPaymentAtDefault">
    ///  The full interest payment of the period in which the default occurs</param>
    /// <param name="recoveryRate">The recovery rate</param>
    /// <param name="defaultCcy">The currency of the default settlements</param>
    /// <param name="defaultDate">The date of the default</param>
    /// <param name="defaultPaymentDate">The date of the settlement payment</param>
    /// <param name="flag">Payment generation flag</param>
    /// <returns>IEnumerable&lt;Payment&gt;.</returns>
    private static IEnumerable<Payment> GenerateDefaultPayments(
      InterestPayment fullInterestPaymentAtDefault,
      double recoveryRate, Currency defaultCcy,
      Dt defaultDate, Dt defaultPaymentDate,
      PaymentGenerationFlag flag)
    {
      var ip = fullInterestPaymentAtDefault;
      if (ip == null)
      {
        yield break;
      }

      double defaultAccrual = 0;

      // try include the partial interest payment till default,
      // or the full interest payment with rebate at the default settle date
      if (flag.SupportAccrualRebateAfterDefault && 
        defaultDate <= ip.PayDt && defaultPaymentDate >= ip.PayDt)
      {
        // Include the full interest payments
        yield return ip;

        // set up the rebate at the default settlement date
        double rebateAmount = 0.0;
        var fullAmount = ip.Amount;
        if (!flag.AccruedPaidOnDefault)
        {
          // full rebate
          rebateAmount = -fullAmount;
        }
        else
        {
          // partial rebate
          Dt rebateBegin = flag.IncludeDefaultDate
            ? (defaultDate + 1) : defaultDate;
          var rebateFraction = Dt.Fraction(ip.CycleStartDate, ip.CycleEndDate,
            rebateBegin, ip.AccrualEnd, ip.DayCount, ip.Frequency);
          if (rebateFraction > 0)
          {
            var fullFraction = Dt.Fraction(ip.CycleStartDate, ip.CycleEndDate,
              ip.AccrualStart, ip.AccrualEnd, ip.DayCount, ip.Frequency);
            rebateAmount = -rebateFraction/fullFraction*fullAmount;
          }
        }
        if (rebateAmount < 0 || rebateAmount > 0)
        {
          Debug.Assert(!defaultPaymentDate.IsEmpty());
          defaultAccrual = rebateAmount;
        }
      }
      else
      {
        // set up the partial interest payment until the default date
        ip.IncludeEndDateInAccrual = flag.IncludeDefaultDate;
        ip.PeriodEndDate = ip.PayDt = defaultDate;
        if (!flag.AccruedPaidOnDefault)
        {
          ip.AccrualFactor = 0;
        }
        if (defaultPaymentDate.IsEmpty())
        {
          // Include the partial interest payment at default time.
          yield return ip;
        }
        else
        {
          // The partial interest payment is part of the settlement
          defaultAccrual = ip.Amount;
        }
      }

      // Include the default settlement
      yield return new DefaultSettlement(defaultDate, defaultPaymentDate,
        defaultCcy, ip.Notional, recoveryRate, defaultAccrual, flag.Funded);
    }

    /// <summary>
    /// Gets the regular payments (interests, upfront fees, and principal exchanges).
    /// Contingent recovery payments are not included.
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="stopDate">The stop date</param>
    /// <param name="schedule">The schedule</param>
    /// <param name="ccy">The currency</param>
    /// <param name="dayCount">The day count convention</param>
    /// <param name="flag">The flag.</param>
    /// <param name="initialCoupon">The initial coupon</param>
    /// <param name="couponSchedule">The coupon schedule</param>
    /// <param name="initialNotional">The initial notional</param>
    /// <param name="amortizationSchedule">The amortization schedule</param>
    /// <param name="referenceCurve">The reference curve</param>
    /// <param name="discountCurve">The discount curve</param>
    /// <param name="rateResets">The rate resets</param>
    /// <param name="fillFixed">Boolean to indicate whether to produce fixed payments</param>
    /// <returns>IEnumerable&lt;Payment&gt;.</returns>
    internal static IEnumerable<Payment> GetRegularPayments(
      Dt asOf, Dt stopDate, Schedule schedule,
      Currency ccy, DayCount dayCount, PaymentGenerationFlag flag,
      double initialCoupon, IList<CouponPeriod> couponSchedule,
      double initialNotional, IList<Amortization> amortizationSchedule,
      CalibratedCurve referenceCurve = null,
      DiscountCurve discountCurve = null,
      IList<RateReset> rateResets = null,
      bool fillFixed = false) 
    {
      bool includeMaturityAccrual = flag.IncludeMaturityAccrual;
      bool includeMaturityProtection = flag.IncludeMaturityProtection;
      bool accrueOnCycle = flag.AccrueOnCycle;
      bool creditRiskToPaymentDate = flag.CreditRiskToPaymentDate;
      var accruedFractionOnDefault = flag.AccruedPaidOnDefault ? 0.5 : 0;

      int firstPeriod;
      if (!stopDate.IsEmpty() && stopDate < asOf)
      {
        firstPeriod = GetFirstPeriod(stopDate, schedule, accrueOnCycle);
      }
      else
      {
        firstPeriod = GetFirstPeriod(asOf, schedule, accrueOnCycle);
      }

      int lastPeriod = schedule.Count - 1;
      CreateInterestFn createInterestPaymentFn;
      // Generate the full cash flows
      if (referenceCurve == null || fillFixed)
      {
        createInterestPaymentFn = (i, notional, cpn) =>
        {
          var periodEnd = schedule.GetPeriodEnd(i);
          var ip = new FixedInterestPayment(
            schedule.GetPreviousPayDate(i), schedule.GetPaymentDate(i),
            ccy, schedule.GetCycleStart(i), schedule.GetCycleEnd(i),
            schedule.GetPeriodStart(i), periodEnd,
            Dt.Empty, notional, cpn, dayCount, Frequency.None)
          {
            AccruedFractionAtDefault = accruedFractionOnDefault,
            AccrueOnCycle = accrueOnCycle,
            IncludeEndDateInAccrual = includeMaturityAccrual && i == lastPeriod,
            IncludeEndDateProtection = includeMaturityProtection && i == lastPeriod,
          };
          if (!creditRiskToPaymentDate && periodEnd != ip.PayDt)
          {
            ip.CreditRiskEndDate = periodEnd;
          }
          return ip;
        };
      }
      else
      {
        var projParams = GetProjectionParams(schedule);
        var rateProjector = (CouponCalculator)GetRateProjector(schedule,
          ccy, dayCount, discountCurve, referenceCurve, projParams);
        var forwardAdjustment = GetForwardAdjustment(discountCurve, projParams);

        var resets = rateResets != null
          ? NormalizeRateResets(schedule, schedule, initialCoupon, dayCount,
            referenceCurve, new RateResets(rateResets), firstPeriod)
          : null;
        Dt resetEnd = Dt.AddDays(rateProjector.AsOf, 2, schedule.Calendar);
        if (resets != null && resets.HasAllResets)
        {
          var max = resets.AllResets.Keys.Max();
          if (max >= resetEnd) resetEnd = max + 1;
        }

        createInterestPaymentFn = (i, notional, cpn) =>
        {
          var periodEnd = schedule.GetPeriodEnd(i);
          var ip = new FloatingInterestPayment(
            schedule.GetPreviousPayDate(i), schedule.GetPaymentDate(i),
            ccy, schedule.GetCycleStart(i), schedule.GetCycleEnd(i),
            schedule.GetPeriodStart(i), periodEnd,
            Dt.Empty, notional, cpn, dayCount,
            projParams.CompoundingFrequency, projParams.CompoundingConvention,
            rateProjector, forwardAdjustment, schedule.CycleRule, 1.0)
          {
            SpreadType = projParams.SpreadType,
            AccrueOnCycle = accrueOnCycle,
            // FXCurve = fxCurve,
            // AccrualFactor = fraction,
            // Cap = cap,
            // Floor = floor,
            AccruedFractionAtDefault = accruedFractionOnDefault,
            IncludeEndDateInAccrual = includeMaturityAccrual && i == lastPeriod,
            IncludeEndDateProtection = includeMaturityProtection && i == lastPeriod,
          };
          if (!creditRiskToPaymentDate && periodEnd != ip.PayDt)
          {
            ip.CreditRiskEndDate = periodEnd;
          }
          if (resets != null && ip.ResetDate < resetEnd)
          {
            resets.HandleResets(i == firstPeriod, i == firstPeriod + 1, ip);
            //In the legacy cashflow method, if there is no rate reset, the 
            //cpp file will reset the coupon from the curve, but it will not 
            //reset the spread to zero. In the payment schedule, we use the function
            //GetMissingRateResets to produce a reset, but the spread will be set to
            //zero here, so in the function GetMissingRateResets I set the property
            //CurrentReset = double.NaN and here we decide to reset the spread to zero or not.
            if (ip.EffectiveRateOverride != null && 
            !double.IsNaN(resets.CurrentReset)) ip.FixedCoupon = 0;
          }
          return ip;
        };
      }

      return GenerateRegularPayments(schedule,
        firstPeriod, stopDate, ccy, flag,
        initialCoupon, couponSchedule,
        initialNotional, amortizationSchedule,
        createInterestPaymentFn);
    }

    internal static Dt GetPreviousPayDate(this Schedule schedule, int i)
    {
      return i <= 0 ? schedule.GetPeriodStart(0)
        : schedule.GetPaymentDate(i - 1);
    }

    internal static int GetFirstPeriod(Dt asOf,
      Schedule schedule, bool accrueOnCycle)
    {
      // Step over to find the period for accrual
      int firstIdx = 0, lastSchedIndex = schedule.Count - 1;
      {
        if (lastSchedIndex >= 0)
        {
          for (; firstIdx <= lastSchedIndex; firstIdx++)
          {
            Dt accrualStart = (accrueOnCycle || firstIdx <= 0)
              ? schedule.GetPeriodStart(firstIdx)
              : schedule.GetPaymentDate(firstIdx - 1);
            if (accrualStart >= asOf)
              break;
          }
          if (firstIdx > 0)
            firstIdx--;
        }
      }
      return firstIdx;
    }

    // Generates the regular payments.  The coupon schedule
    // and amortization schedule are handled in the same way
    // as in the legacy cash flows.
    private static IEnumerable<Payment> GenerateRegularPayments(
      Schedule schedule, int firstIdx, Dt stopDate, Currency ccy,
      PaymentGenerationFlag flag,
      double firstCpn, IList<CouponPeriod> cpns,
      double principal, IList<Amortization> amortizations,
      CreateInterestFn createInterestPayment)
    {
      double origPrincipal = (principal > 0.0) ? principal : 1.0;

      Dt begin = schedule.GetPeriodEnd(firstIdx);
      double remainingPrincipal = origPrincipal;
      int nextAmort = 0, amortCount = amortizations?.Count ?? -1;
      // ReSharper disable once PossibleNullReferenceException
      while (nextAmort < amortCount && amortizations[nextAmort].Date < begin)
      {
        remainingPrincipal -= amortizations[nextAmort].Amount;
        nextAmort++;
      }

      double coupon = firstCpn;
      int nextCpn = -1;
      int lastCpnIndex = cpns?.Count > 0 ? cpns.Count - 1 : -1;
      // ReSharper disable once PossibleNullReferenceException
      while (nextCpn < lastCpnIndex && cpns[nextCpn + 1].Date < begin)
      {
        coupon = cpns[++nextCpn].Coupon;
      }

      //- Generate payments.
      int lastSchedIndex = schedule.Count - 1;
      for (int i = firstIdx; i <= lastSchedIndex; i++)
      {
        // Get current coupon
        // Assume coupon steps on scheduled dates for now. TBD: Revisit this. RTD Feb'06
        Dt end = schedule.GetPeriodEnd(i);
        // ReSharper disable once PossibleNullReferenceException
        while (nextCpn < lastCpnIndex && cpns[nextCpn + 1].Date < end)
        {
          coupon = cpns[++nextCpn].Coupon;
        }

        // Any amortizations between this coupon and the last one
        Dt date = schedule.GetPaymentDate(i);
        double noDfltCf = 0.0;
        // ReSharper disable once PossibleNullReferenceException
        while (nextAmort < amortCount && amortizations[nextAmort].Date <= date)
        {
          // Include amortizations on scheduled date for now. TBD: Revisit this. RTD Feb'06
          if (remainingPrincipal > 0.0)
            noDfltCf += amortizations[nextAmort].Amount;
          nextAmort++;
        }

        // Final remaining principal
        if (i == lastSchedIndex && principal > 0.0)
          noDfltCf = remainingPrincipal;

        yield return createInterestPayment(i, remainingPrincipal, coupon);

        if (principal > 0 && (noDfltCf > 0 || noDfltCf < 0))
        {
          var pe = new PrincipalExchange(date, noDfltCf, ccy);
          if (i == lastSchedIndex && flag.IncludeMaturityProtection)
            pe.CreditRiskEndDate = end + 1;
          yield return pe;
        }

        remainingPrincipal -= noDfltCf;
        Debug.Assert(remainingPrincipal >= -1e-14);

        // Check if we stop here
        if (!stopDate.IsEmpty() && stopDate <= end)
        {
          yield break;
        }
      }
    }

    private delegate InterestPayment CreateInterestFn(
      int scheduleIndex, double notional, double coupon);

    #region Rate projector

    private static RateResets NormalizeRateResets(
      Schedule sched, IScheduleParams schedParams,
      double spread, DayCount dayCount, Curve referenceCurve, 
      RateResets rateResetsObj, int firstIndex)
    {
      Dt effective = schedParams.AccrualStartDate,  asOf = referenceCurve.AsOf;
      var resets = new RateResets();
      var all = resets.AllResets;
      var rateResets = rateResetsObj.ToBackwardCompatibleList(effective);
      int rrCount = rateResets.Count;
      //here if the rate reset is empty, we need to add one reset for the 
      //backward compatible yet it doesn't necessary mean this is the correct behavior.
      if (rrCount == 0 && effective <= asOf)
      {
        return GetMissingRateResets(sched, spread, dayCount, referenceCurve, firstIndex);
      }

      int count = sched.Count;
      int j = 0; // counter through schedule
      double rate = 0;
      int rrIndex = 0;
      foreach (RateReset r in rateResets)
      {
        ++rrIndex; // get the next rate reset of r;
        Dt resetDate = r.Date;
        rate = r.Rate;

        // if reset was captured for a rolled period start then pass to the cashflow model
        // as the unadjusted period start; FillFloat only looks for most recent resets, <= period start
        for (; j < count; j++)
        {
          Dt periodStart = sched.GetPeriodStart(j);
          Dt adjPeriodStart = Dt.Roll(periodStart, schedParams.Roll,
            schedParams.Calendar);
          if (resetDate <= adjPeriodStart
            && Dt.BusinessDays(resetDate, adjPeriodStart, schedParams.Calendar) < 2)
          {
            //very rare situation that the two rate resets only have 
            //one-day difference and the second reset date = adjPeriodStart
            if (rrIndex < rrCount && rateResets[rrIndex].Date == adjPeriodStart)
              rate = rateResets[rrIndex].Rate;
            all.Add(periodStart, rate);
            if (j == firstIndex)
              resets.CurrentReset = rate;
            if (j == firstIndex + 1)
              resets.NextReset = rate;
            ++j; // start at next period for next rate reset
            break;
          }
          if (Dt.Cmp(adjPeriodStart, resetDate) > 0)
          {
            break;
          }
          all.Add(periodStart, rate);
        }
      }

      for (; j < count; j++)
      {
        Dt periodStart = sched.GetPeriodStart(j);
        if (periodStart >= asOf) break;
        all.Add(periodStart, rate);
      }
      return resets;
    }

    private static RateResets GetMissingRateResets(
      Schedule sched,
      double spread, DayCount dayCount,
      Curve referenceCurve, int firstIndex)
    {
      var retVal = new RateResets();
      var start = sched.GetPeriodStart(firstIndex);
      var end = sched.GetPeriodEnd(firstIndex);
      double price = referenceCurve.Interpolate(start, end);
      double t = Dt.Fraction(start, end, dayCount);
      double rate = RateCalc.RateFromPrice(price, t, sched.Frequency) + spread;
      retVal.Add(new RateReset(start, rate));
      retVal.CurrentReset = double.NaN; 
      return retVal;
    }

    private static IRateProjector GetRateProjector(
      IScheduleParams schedParams,
      Currency ccy, DayCount dayCount,
      DiscountCurve discountCurve,
      CalibratedCurve referenceCurve,
      ProjectionParams projectionParams)
    {
      var rateProjector = CouponCalculator.Get(
        (referenceCurve ?? discountCurve)?.AsOf ?? Dt.Empty,
        new InterestRateIndex("FloatingIndex",
          schedParams.Frequency, ccy, dayCount, schedParams.Calendar, 0),
        referenceCurve, discountCurve, projectionParams);
      var frc = rateProjector as ForwardRateCalculator;
      if (frc != null)
      {
        // this is for the backward compatible setting only.
        ((ForwardRateCalculator)rateProjector).EndSetByIndexTenor = false;
      }
      if (rateProjector != null)
        rateProjector.CashflowFlag = schedParams.CashflowFlag;
      return rateProjector;
    }

    private static IForwardAdjustment GetForwardAdjustment(
      DiscountCurve discountCurve,
      ProjectionParams projectionParams)
    {
      Dt asOf = discountCurve.AsOf;
      return ForwardAdjustment.Get(asOf, discountCurve, null,
        projectionParams);
    }

    private static ProjectionParams GetProjectionParams(
      IScheduleParams schedParams)
    {
      ProjectionFlag flags = ProjectionFlag.None;
      var par = new ProjectionParams
      {
        ProjectionType = ProjectionType.SimpleProjection,
        ProjectionFlags = flags,
      };
      if ((schedParams.CashflowFlag & CashflowFlag.SimpleProjection) == 0)
        par.CompoundingFrequency = schedParams.Frequency;
      return par;
    }

    #endregion

    #endregion
  }

  internal struct PaymentGenerationFlag
  {
    internal PaymentGenerationFlag(
      CashflowFlag flag,
      bool includeMaturityProtection,
      bool supportAccrualRebateAfterDefault,
      bool funded = false,
      bool creditRiskToPaymentDate = false)
    {
      _flag = ((uint) flag) & CashflowFlagMask;
      if (includeMaturityProtection)
        _flag |= IncludeMaturityProtectionFlag;
      if (supportAccrualRebateAfterDefault)
        _flag |= SupportAccrualRebateFlag;
      if (funded)
        _flag |= FundedFlag;
      if (creditRiskToPaymentDate)
        _flag |= CreditRiskToPaymentDateFlag;
    }

    public bool AccrueOnCycle =>
      (CashflowFlag & CashflowFlag.AccrueOnCycle) != 0;

    public bool AccruedPaidOnDefault =>
      (CashflowFlag & CashflowFlag.AccruedPaidOnDefault) != 0;

    public bool IncludeDefaultDate =>
      (CashflowFlag & CashflowFlag.IncludeDefaultDate) != 0;

    public bool IncludeMaturityAccrual =>
      (CashflowFlag & CashflowFlag.IncludeMaturityAccrual) != 0;

    public bool IncludeMaturityProtection =>
      (_flag & IncludeMaturityProtectionFlag) != 0;

    public bool SupportAccrualRebateAfterDefault =>
      (_flag & SupportAccrualRebateFlag) != 0;

    public bool Funded => (_flag & FundedFlag) != 0;

    public bool CreditRiskToPaymentDate => 
      (_flag & CreditRiskToPaymentDateFlag) != 0;

    private CashflowFlag CashflowFlag => 
      (CashflowFlag) (_flag & CashflowFlagMask);

    private readonly uint _flag;
    private const uint CashflowFlagMask = 0xFFFF;
    private const uint IncludeMaturityProtectionFlag = 0x10000;
    private const uint SupportAccrualRebateFlag = 0x20000;
    private const uint FundedFlag = 0x40000;
    private const uint CreditRiskToPaymentDateFlag = 0x80000;
  }
}
