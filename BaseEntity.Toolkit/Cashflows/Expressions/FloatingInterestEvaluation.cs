/*
 * 
 */

using System;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Util;
using CompoundingPeriod = System.Tuple<BaseEntity.Toolkit.Base.Dt,
  BaseEntity.Toolkit.Base.Dt, BaseEntity.Toolkit.Cashflows.FixingSchedule>;
using E = BaseEntity.Toolkit.Cashflows.Expressions.Evaluable;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  /// <summary>
  ///   Utilities to evaluate floating interest rate coupon.
  /// </summary>
  static class FloatingInterestEvaluation
  {
    internal static Evaluable CalculateAccrual(
      FloatingInterestPayment payment,
      bool useDiscountRateForCompounding,
      ForwardRateCalculator fwd, double indexMultiplier =1.0)
    {
      Evaluable rate;
      var coupon = payment.FixedCoupon;
      bool multiplicative = (payment.SpreadType == SpreadType.Multiplicative
        && !coupon.AlmostEquals(0.0));
      var compoundingPeriods = payment.CompoundingPeriods;
      if (compoundingPeriods.Count > 1)
        //optionality not supported for compounded payments
      {
        switch (payment.CompoundingConvention)
        {
        case CompoundingConvention.ISDA:
        case CompoundingConvention.FlatISDA:
          // don't do approximate daily compounding yet
          rate = CalculateCompoundRate(fwd, compoundingPeriods,
            multiplicative ? 0.0 : coupon, useDiscountRateForCompounding,
            payment.CompoundingConvention == CompoundingConvention.FlatISDA, 
            true, indexMultiplier);
          break;
        case CompoundingConvention.Simple:
          rate = CalculateCompoundRate(fwd, compoundingPeriods,
            multiplicative ? 0.0 : coupon, useDiscountRateForCompounding, 
            false, false, indexMultiplier);
          break;
        default:
          throw new ArgumentException("Compounding convention not supported");
        }
        if (multiplicative && !coupon.AlmostEquals(0.0))
        {
          rate = coupon*rate;
        }
      }
      else
      {
        var sched = (ForwardRateFixingSchedule) compoundingPeriods[0].Item3;
        rate = Evaluable.ForwardRate(fwd.ReferenceCurve,
          sched.StartDate, sched.EndDate, fwd.ReferenceIndex.DayCount)*indexMultiplier;
        if (!coupon.AlmostEquals(0.0))
        {
          rate = payment.SpreadType == SpreadType.Multiplicative
            ? (coupon*rate) : (coupon + rate);
        }
        if (payment.ForwardAdjustment != null)
        {
          if (payment.Cap.HasValue)
          {
            rate = Evaluable.Min(payment.Cap.Value, rate);
          }
          if (payment.Floor.HasValue)
          {
            rate = Evaluable.Max(payment.Floor.Value, rate);
          }
        }
      }
      return payment.AccrualFactor*rate;
    }

    private static Evaluable CalculateCompoundRate(
      CouponCalculator fwd, IList<CompoundingPeriod> periods,
      double coupon, bool useDiscountRate, bool flat,
      bool isCompound, double indexMultiplier)
    {
      var dc = fwd.ReferenceIndex.DayCount;
      var curve = fwd.ReferenceCurve;
      double periodFrac = Dt.Fraction(periods[0].Item1,
        periods[periods.Count - 1].Item2, dc);
      if (periodFrac <= 0.0)
        return Evaluable.Constant(0.0);

      var cmpnFwd = Evaluable.Constant(0.0);
      foreach (var p in periods)
      {
        var s = (ForwardRateFixingSchedule) p.Item3;
        RateResetState state;
        var rate = FindHistoricalRate(fwd, s, out state);
        Evaluable fixingForward;
        switch (state)
        {
        case RateResetState.Missing:
          throw new MissingFixingException(string.Format(
            "Fixing resetting on date {0} is missing.", s.ResetDate));
        case RateResetState.IsProjected:
          fixingForward = Evaluable.ForwardRate(curve, s.StartDate, s.EndDate, dc);
          break;
        default:
          fixingForward = Evaluable.Constant(rate);
          break;
        }
        var cmpnRate = useDiscountRate && state == RateResetState.IsProjected
          ? Evaluable.ForwardRate(fwd.DiscountCurve, p.Item1, p.Item2, dc)
          : fixingForward;
        double frac = Dt.Fraction(p.Item1, p.Item2, dc);
        var term1 = cmpnFwd + (fixingForward*indexMultiplier + coupon)*frac;
        if (isCompound)
        {
          cmpnFwd = term1 + cmpnFwd*(cmpnRate*indexMultiplier + (flat ? 0.0 : coupon))*frac;
        }
        else
          cmpnFwd = term1;
      }
      cmpnFwd = cmpnFwd/periodFrac;
      return cmpnFwd;
    }

    private static double FindHistoricalRate(
      CouponCalculator calculator, ForwardRateFixingSchedule fs,
      out RateResetState state)
    {
      var rate = RateResetUtil.FindRate(fs.ResetDate, calculator.AsOf,
        calculator.HistoricalObservations, calculator.UseAsOfResets,
        out state);
      if (state == RateResetState.Missing && RateResetUtil.
        ProjectMissingRateReset(fs.ResetDate, calculator.AsOf, fs.StartDate))
      {
        state = RateResetState.IsProjected;
      }
      return rate;
    }

  }
}
