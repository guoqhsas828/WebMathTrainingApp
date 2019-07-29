/*
 * 
 */

using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Util;
using E = BaseEntity.Toolkit.Cashflows.Expressions.Evaluable;

namespace BaseEntity.Toolkit.Cashflows.Expressions
{
  static class CapletEvaluation
  {
    internal static Evaluable Amount(CapletPayment caplet)
    {
      Dt asOf = caplet.VolatilityStartDt, settle = asOf;
      var capletRate = CapletRate(caplet, asOf, settle, false);
      var timeToExpiry = (caplet.Expiry - PricingDate.AsVariable)/365.0;
      var volatilityType = caplet.GetVolatilityType();
      var vollatility = caplet.CapletVolatility(capletRate.Evaluate());

      capletRate *= caplet.IndexMultiplier;

      var dt = caplet.PeriodFraction*caplet.Notional;
      var eval = new CapletEvaluator(caplet);
      if (caplet.OptionDigitalType != OptionDigitalType.None)
      {
        if (volatilityType == DistributionType.Normal)
        {
          return dt*Evaluable.Call(true, eval.EvaluateDigitalNormal,
            capletRate, vollatility, timeToExpiry);
        }
        if (volatilityType == DistributionType.LogNormal)
        {
          return dt*Evaluable.Call(true, eval.EvaluateDigitalLogNormal,
            capletRate, vollatility, timeToExpiry);
        }
      }
      else
      {
        if (volatilityType == DistributionType.Normal)
        {
          return dt*Evaluable.Call(true, eval.EvaluateNormal,
            capletRate, vollatility, timeToExpiry);
        }
        if (volatilityType == DistributionType.LogNormal)
        {
          return dt *Evaluable.Call(true, eval.EvaluateLogNormal,
            capletRate, vollatility, timeToExpiry);
        }
      }
      throw new ApplicationException(string.Format(
        "Distribution type {0} not supported", volatilityType));
    }

    private static Evaluable CapletRate(
      CapletPayment caplet,
      Dt asOf, Dt settle,
      bool checkResetInForwardRate)
    {
      if (caplet.Expiry <= settle)
      {
        if (caplet.RateResetState == RateResetState.Missing &&
          !RateResetUtil.ProjectMissingRateReset(caplet.Expiry, asOf, caplet.RateFixing))
        {
          throw new ToolkitException(string.Format("Missing Rate Reset for {0}", caplet.Expiry));
        }
        if (caplet.RateResetState == RateResetState.ObservationFound
          || !checkResetInForwardRate)
        {
          return Evaluable.Constant(caplet.Rate);
        }
      }

      var projector = caplet.RateProjector;
      var sp = projector as SwapRateCalculator;
      if (sp != null)
      {
        var fa = caplet.ForwardAdjustment as SwapRateAdjustment;
        var convexityParameters = fa == null ? null : fa.RateModelParameters;
        return ProjectSwapRate(caplet, sp, convexityParameters);
      }
      var fp = (ForwardRateCalculator) projector;
      var referenceCurve = fp.ReferenceCurve;
      var index = fp.ReferenceIndex;
      return Evaluable.ForwardRate(referenceCurve, caplet.RateFixing,
        caplet.TenorDate, index.DayCount);
    }

    private static Evaluable ProjectSwapRate(
      CapletPayment swaplet,
      SwapRateCalculator projector,
      RateModelParameters convexityParameters)
    {
      var schdule = (SwapRateFixingSchedule) projector.GetFixingSchedule(
        Dt.Empty, swaplet.RateFixing, swaplet.TenorDate, swaplet.PayDt);
      var swapRate = Fixing(projector, schdule);
      if (convexityParameters == null)
        return swapRate;

      var ca = SwapRateEvaluation.ConvexityAdjustment(
        swaplet.VolatilityStartDt, swaplet.PayDt, schdule, 
        convexityParameters, swapRate);
      return ca + swapRate;
    }

    private static Evaluable Fixing(
      SwapRateCalculator calc,
      SwapRateFixingSchedule sched)
    {
      RateResetState state;
      Evaluable annuity;
      return SwapRateEvaluation.CalculateSwapRate(
        sched, calc.AsOf,
        calc.HistoricalObservations, calc.UseAsOfResets,
        calc.DiscountCurve, calc.ReferenceCurve,
        (SwapRateIndex)calc.ReferenceIndex,
        out state, out annuity);
    }

  }

  internal class CapletEvaluator
  {
    private static readonly log4net.ILog Logger
      = log4net.LogManager.GetLogger(typeof(CapletEvaluator));

    internal CapletPayment Caplet { get; }

    internal CapletEvaluator(CapletPayment caplet)
    {
      Caplet = caplet;
    }

    internal double EvaluateDigitalNormal(double rate, double vol, double T)
    {
      var caplet = Caplet;
      return DigitalOption.NormalBlackP(
        OptionStyle.European, caplet.OptionType, caplet.OptionDigitalType,
        T < 0 ? 0 : T, rate, caplet.Strike, T <= 0 ? 0 : vol,
        caplet.DigitalFixedPayout);
    }

    internal double EvaluateDigitalLogNormal(double rate, double vol, double T)
    {
      var caplet = Caplet;
      return DigitalOption.BlackP(
        OptionStyle.European, caplet.OptionType, caplet.OptionDigitalType,
        T < 0 ? 0 : T, rate, caplet.Strike, T <= 0 ? 0 : vol,
        caplet.DigitalFixedPayout);
    }

    internal double EvaluateNormal(double rate, double vol, double T)
    {
      var caplet = Caplet;
      var p = BlackNormal.P(caplet.OptionType, T < 0 ? 0 : T, 0,
        rate, caplet.Strike, T <= 0 ? 0 : vol);
      if (Logger.IsDebugEnabled)
      {
        Logger.DebugFormat("\t{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t",
          PricingDate.Value.ToInt(), caplet.Expiry.ToInt(), rate, vol, T, p);
      }
      return p;
    }

    internal double EvaluateLogNormal(double rate, double vol, double T)
    {
      double p;
      var caplet = Caplet;
      if (T <= 0 || rate <= 0.0 || caplet.Strike <= 0)
      {
        p = Math.Max((caplet.OptionType == OptionType.Call
          ? 1.0 : -1.0) * (rate - caplet.Strike), 0.0);
      }
      else
      {
        //p = Black.P(caplet.OptionType, T, rate, caplet.Strike, vol);
        p = caplet.OptionType == OptionType.Call
          ? SpecialFunctions.Black(rate, caplet.Strike, vol * Math.Sqrt(T))
          : SpecialFunctions.Black(caplet.Strike, rate, vol * Math.Sqrt(T));
      }
      if (Logger.IsDebugEnabled)
      {
        Logger.DebugFormat("\t{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t",
          PricingDate.Value.ToInt(), caplet.Expiry.ToInt(), rate, vol, T, p);
      }

      return p;
    }
  }

}
