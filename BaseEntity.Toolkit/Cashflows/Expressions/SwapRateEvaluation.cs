/*
 *   2005-2016. All rights reserved.
 */

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;
using E = BaseEntity.Toolkit.Cashflows.Expressions.Evaluable;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  static class SwapRateEvaluation
  {
    #region Swap rate 

    internal static Evaluable CalculateSwapRate(SwapRateFixingSchedule sched,
      Dt asOf, RateResets historicalObservations, bool useAsOfResets,
      DiscountCurve discountCurve, Curve referenceCurve,
      SwapRateIndex referenceIndex,
      out RateResetState state, out Evaluable annuity)
    {
      var forwardRateIndex = referenceIndex.ForwardRateIndex;
      Evaluable rate = RateResetUtil.FindRate(sched.ResetDate,
        asOf, historicalObservations, useAsOfResets, out state);
      annuity = 0.0;
      if ((state == RateResetState.Missing) && ((sched.ResetDate <= asOf) && (sched.StartDate >= asOf)))
        state = RateResetState.IsProjected;
      if (state == RateResetState.IsProjected)
      {
        var floatingLegPv = GetFloatingLegPv(
          sched.FloatingLegSchedule,
          discountCurve ?? referenceCurve, referenceCurve, forwardRateIndex.DayCount);
        annuity = GetAnnuityPv(sched.FixedLegSchedule, discountCurve, referenceIndex.DayCount);
        return floatingLegPv/annuity;
      }
      if (state == RateResetState.Missing)
      {
        throw new MissingFixingException(string.Format(
          "Fixing resetting on date {0} is missing.", sched.ResetDate));
      }
      return rate;
    }

    private static Evaluable GetAnnuityPv(Schedule sc, Curve discount, DayCount dayCount)
    {
      Evaluable retVal = 0.0;
      for (int i = 0; i < sc.Count; ++i)
        retVal += Evaluable.Interpolate(discount, sc.GetPaymentDate(i))*sc.Fraction(i, dayCount);
      return retVal;
    }

    private static Evaluable GetFloatingLegPv(
      Schedule sc,
      Curve discount,
      Curve referenceCurve,
      DayCount dayCount)
    {
      Evaluable retVal = 0.0;
      for (int i = 0; i < sc.Count; ++i)
      {
        retVal += Evaluable.Interpolate(discount, sc.GetPaymentDate(i))*
          Evaluable.ForwardRate(referenceCurve, sc.GetPeriodStart(i), sc.GetPeriodEnd(i),
            dayCount)*sc.Fraction(i, dayCount);
      }
      return retVal;
    }

    #endregion

    #region Convexity adjustment

    internal static Evaluable ConvexityAdjustment(
      Dt asOf, Dt payDt,
      FixingSchedule fixingSchedule,
      RateModelParameters rateModelParameters,
      Evaluable forwardRate)
    {
      if (fixingSchedule.ResetDate <= asOf)
        return 0.0;
      if (rateModelParameters.ModelName(RateModelParameters.Process.Projection) == RateModelParameters.Model.Custom)
      {
        return Evaluable.Call(true, f => rateModelParameters.Interpolate(
          payDt, f, RateModelParameters.Param.Custom,
          RateModelParameters.Process.Projection), forwardRate);
      }
      var swapFixingSchedule = (SwapRateFixingSchedule) fixingSchedule;
      var schedule = swapFixingSchedule.FixedLegSchedule;
      double delta = Dt.FractDiff(swapFixingSchedule.ResetDate, payDt)/365.0;
      double n = schedule.Count;
      double rateTen = Dt.FractDiff(schedule.GetPeriodStart(0), schedule.GetPeriodEnd(schedule.Count - 1))/365.0;
      double tau = rateTen/n; //avg fraction
      return Evaluable.Call(true, f =>
      {
        double taufrw0 = tau*f;
        double constant = 1 - taufrw0/(1 + taufrw0)*(delta/tau + n/(Math.Pow(1 + taufrw0, n) - 1.0));
        Dt volStart = CmsCapletConvexityFromExposureDate ? PricingDate.Value : asOf;
        double var = rateModelParameters.SecondMoment(
          RateModelParameters.Process.Projection, volStart, f,
          swapFixingSchedule.ResetDate, swapFixingSchedule.ResetDate);
        return (f > 0) ? f*constant*(var/(f*f) - 1.0) : 0.0;
      }, forwardRate);
    }

    private static bool CmsCapletConvexityFromExposureDate =>
      ToolkitConfigurator.Settings.CcrPricer.CmsCapletConvexityFromExposureDate
      && !PricingDate.Value.IsEmpty();

    #endregion
  }
}
