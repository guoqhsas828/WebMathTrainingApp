/*
 * RateVolatilityUtil.cs
 *
 *   2010. All rights reserved.
 *
 */
using System;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Curves.Volatilities;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using Cashflow = BaseEntity.Toolkit.Cashflows.CashflowAdapter;

namespace BaseEntity.Toolkit.Calibrators.Volatilities
{
  ///<summary>
  /// Utility class for rate volatility related functions
  ///</summary>
  public static class RateVolatilityUtil
  {
    #region Utility functions related to caplet volatility

    /// <summary>
    ///  Calculate the caplet (regular or CMS) volatility based on the rate index.
    /// </summary>
    /// <param name="volatilityObject">The volatility object</param>
    /// <param name="expiry">The expiry date</param>
    /// <param name="rate">The forward rate or swap rate</param>
    /// <param name="strike">The strike</param>
    /// <param name="index">The rate index</param>
    /// <returns>System.Double.</returns>
    /// <exception cref="System.NotImplementedException"></exception>
    public static double CapletVolatility(
      this IVolatilityObject volatilityObject,
      Dt expiry, double rate, double strike, ReferenceIndex index)
    {
      var cube = volatilityObject as RateVolatilityCube;
      if (cube != null && index is InterestRateIndex)
      {
        return cube.CapletVolatility(expiry, rate, strike, index);
      }
      var swapIndex = index as SwapRateIndex;
      if (swapIndex != null)
      {
        return SwaptionVolatilityFactory.EvaluateVolatility(
          volatilityObject, expiry, rate, strike, swapIndex);
      }
      var rateInterp = volatilityObject as IModelParameter;
      if (rateInterp != null)
      {
        return rateInterp.Interpolate(expiry, strike, index);
      }
      throw new NotImplementedException();
    }

    /// <exclude />
    public static Tenor[] GetRateExpiryTenors(
      this IVolatilityObject volatilityObject)
    {
      var svol = volatilityObject as SwaptionVolatilityCube;
      if (svol != null)
      {
        volatilityObject = svol.AtmVolatilityObject;
      }
      var cube = volatilityObject as RateVolatilityCube;
      if (cube != null)
      {
        return cube.ExpiryTenors ?? cube.Dates.Select(
          d => Tenor.FromDateInterval(cube.AsOf, d)).ToArray();
      }
      var bgm = volatilityObject as BgmForwardVolatilitySurface;
      if (bgm != null)
      {
        var resets = bgm.CalibratedVolatilities.ResetDates;
        var count = resets.Length - 1;
        if (count <= 0) return EmptyArray<Tenor>.Instance;
        return resets.Take(count).Select(
          d => Tenor.FromDateInterval(bgm.AsOf, d)).ToArray();
      }
      var surface = volatilityObject as VolatilitySurface;
      if (surface != null)
      {
        Dt asOf = surface.AsOf;
        return surface.GetTenorDates().Distinct().OrderBy(d=>d)
          .Select(d => Tenor.FromDateInterval(asOf, d)).ToArray();
      }
      throw new NotImplementedException();
    }

    /// <exclude />
    public static double[] GetRateStrikes(
      this IVolatilityObject volatilityObject)
    {
      var svol = volatilityObject as SwaptionVolatilityCube;
      if (svol != null)
      {
        var skew = svol.Skew;
        if (skew != null)
          return skew.Strikes ?? new[] {0.0};
        volatilityObject = svol.AtmVolatilityObject;
      }
      var flat = volatilityObject as FlatVolatility;
      if (flat != null) return new[] { 0.0 };
      var cube = volatilityObject as RateVolatilityCube;
      if (cube != null) return cube.Strikes;
      var bgm = volatilityObject as BgmForwardVolatilitySurface;
      if (bgm != null)
      {
        var dc = bgm.DiscountCurve;
        var resets = bgm.CalibratedVolatilities.ResetDates;
        int count = resets.Length - 1;
        if (count <= 0) return EmptyArray<double>.Instance;
        var rates = new double[count];
        var dt0 = resets[0];
        var df0 = dc.DiscountFactor(dt0);
        for (int i = 0; i < count; ++i)
        {
          var dt = resets[i + 1];
          var df = dc.DiscountFactor(dt);
          rates[i] = (df0 / df - 1) / ((dt - dt0) / 365.0);
          df0 = df;
          dt0 = dt;
        }
        return rates;
      }
      throw new NotImplementedException();
    }

    #endregion

    ///<summary>
    /// Utility function to compute cap vol from the rate volatility cube
    ///</summary>
    ///<param name="surface">Rate volatility cube</param>
    ///<param name="discountCurve">Discount curve</param>
    ///<param name="lastPmt">Expiry</param>
    ///<param name="strike">Strike</param>
    ///<param name="rateIndex">Rate index</param>
    ///<returns>Rate volatility</returns>
    public static double CapVolatility(
      VolatilitySurface surface,
      DiscountCurve discountCurve,
      Dt lastPmt,
      double strike,
      InterestRateIndex rateIndex)
    {
      var cube = surface as RateVolatilitySurface;
      if (cube == null)
      {
        //TODO: deal with other volatility types
        throw new ArgumentException("Expect rate volatility surface");
      }

      // Factor
      double factor = (cube.VolatilityType == VolatilityType.Normal ? 10000.0 : 1.0);

      // Setup default rateIndex if nec.
      if (rateIndex == null)
      {
        rateIndex = cube.RateVolatilityCalibrator.RateIndex;
      }

      // Setup Cap
      Dt tradeSettle = Dt.AddDays(cube.AsOf, 2, rateIndex.Calendar);
      Dt effective = Dt.Roll(Dt.Add(tradeSettle, rateIndex.IndexTenor), rateIndex.Roll,
                             rateIndex.Calendar);
      var cap = new Cap(effective, lastPmt, Currency.None, CapFloorType.Cap, strike, rateIndex.DayCount,
                        rateIndex.IndexTenor.ToFrequency(), rateIndex.Roll, rateIndex.Calendar)
                  {
                    CycleRule = CycleRule.None,
                    AccrueOnCycle = true
                  };

      // Calc
      return cube.CapVolatility(discountCurve, cap)*factor;
    }

    ///<summary>
    /// Calculate the swaption standard settlement date
    ///</summary>
    ///<param name="asOf">Pricing date</param>
    ///<param name="rateIndex">Floating interest rate index</param>
    ///<returns>Swaption settlement</returns>
    public static Dt SwaptionStandardSettle(Dt asOf, InterestRateIndex rateIndex)
    {
      return Dt.AddDays(asOf, rateIndex.SettlementDays, rateIndex.Calendar);
    }

    ///<summary>
    /// Calculate the date swaption expiration
    ///</summary>
    ///<param name="asOf">Pricing date</param>
    ///<param name="rateIndex">Floating interest rate index</param>
    ///<param name="expiryTenor">Expiry tenor</param>
    ///<returns>Swaption expiry</returns>
    public static Dt SwaptionStandardExpiry(Dt asOf, InterestRateIndex rateIndex, Tenor expiryTenor)
    {
      var settle = SwaptionStandardSettle(asOf, rateIndex);
      return  Dt.AddDays(Dt.Roll(Dt.Add(settle, expiryTenor), rateIndex.Roll, rateIndex.Calendar),-rateIndex.SettlementDays, rateIndex.Calendar);
    }

    ///<summary>
    /// Calculation the swap effective date for swaption settlement 
    ///</summary>
    ///<param name="expiry">Expiration</param>
    ///<param name="notificationDays">Numer of days to notify</param>
    ///<param name="notificationCal">Notification calendar</param>
    ///<returns>Swap effective date</returns>
    public static Dt SwaptionStandardForwardSwapEffective(Dt expiry, int notificationDays, Calendar notificationCal)
    {
      return Dt.AddDays(expiry, notificationDays, notificationCal);
    }


    ///<summary>
    /// Create a matching forward cap based on swaption information
    ///</summary>
    ///<param name="swaptionPricer">Swaption black pricer</param>
    ///<returns>Cap</returns>
    public static Cap CreateEquivalentCap(SwaptionBlackPricer swaptionPricer)
    {
      var swaption = swaptionPricer.Swaption;
      var maturity = Dt.Add(swaption.Maturity,
                            (int)
                            (365.0 * EffectiveSwaptionDuration(swaptionPricer).Value/12.0));

      double fwdRate = CurveUtil.DiscountForwardSwapRate(swaptionPricer.ReferenceCurve,
                                                          swaption.Maturity, maturity,
                                                               swaption.UnderlyingFixedLeg.DayCount,
                                                               swaption.UnderlyingFixedLeg.Freq,
                                                               swaption.UnderlyingFixedLeg.BDConvention,
                                                               swaption.UnderlyingFixedLeg.Calendar);
      double swaptionEffectiveStrike =
        SwaptionVolatilityFactory.EffectiveStrike(swaptionPricer);
      double strikeAdjust = swaptionEffectiveStrike -fwdRate;

      var strikeSchedule = new List<CouponPeriod>();
      var amortizationSchedule = new List<Amortization>();
      foreach (Amortization amort in swaptionPricer.Swaption.UnderlyingFloatLeg.AmortizationSchedule)
      {
        amortizationSchedule.Add((Amortization)amort.Clone());
      }
      foreach (var schedule in swaptionPricer.Swaption.UnderlyingFloatLeg.CouponSchedule)
      {

        if (schedule.Date > swaptionPricer.Swaption.Maturity && schedule.Date <= maturity)
        {
          var capMaturity = Dt.Add(schedule.Date, swaption.UnderlyingFloatLeg.IndexTenor);
          if (capMaturity >= maturity)
            capMaturity = maturity;
          fwdRate = swaptionPricer.ReferenceCurve.F( schedule.Date ,
                                                           capMaturity,
                                                           swaption.UnderlyingFloatLeg.DayCount,
                                                           Frequency.None);

          strikeSchedule.Add(new CouponPeriod(schedule.Date, strikeAdjust + fwdRate));
        }
      }
      var cap = new Cap(swaption.Maturity, maturity, swaption.Ccy,
                        swaption.Type == PayerReceiver.Receiver ? CapFloorType.Floor : CapFloorType.Cap,
                        swaptionEffectiveStrike, swaption.UnderlyingFloatLeg.DayCount, swaption.UnderlyingFloatLeg.Freq,
                        swaption.UnderlyingFloatLeg.BDConvention, swaption.UnderlyingFloatLeg.Calendar) 
                        {StrikeSchedule = strikeSchedule, AmortizationSchedule = amortizationSchedule};
      return cap;

    }

    ///<summary>
    /// Convert a swaption black pricer into equivalent cap/floor pricer for sensitivity calculation
    ///</summary>
    ///<param name="swaptionPricer">Swaption pricer</param>
    ///<returns>Equivalent cap/floor pricer</returns>
    public static CapFloorPricer CreateEquivalentCapFloorPricer(SwaptionBlackPricer swaptionPricer)
    {
      if (swaptionPricer.VolatilityObject is BgmForwardVolatilitySurface || 
        !(((SwaptionVolatilityCube) swaptionPricer.VolatilityObject).RateVolatilityCalibrator is RateVolatilityCapFloorBasisAdjustCalibrator ))
        return null;
      var cap = CreateEquivalentCap(swaptionPricer);
      var capfloorPricer = new CapFloorPricer(cap, swaptionPricer.AsOf, swaptionPricer.Settle,
                                              swaptionPricer.ReferenceCurve,
                                              swaptionPricer.DiscountCurve,
                                              ((SwaptionVolatilityCube) swaptionPricer.VolatilityObject).
                                                AtmVolatilityObject) {Notional = swaptionPricer.Notional};
      return capfloorPricer;
    }

    /// <summary>
    /// Utility method to calculate the effective strike of swaption with spread or coupon/spread schedule
    /// </summary>
    /// <param name="swaption">Swaption</param>
    /// <param name="asOf">The pricing date</param>
    /// <param name="settle">The settle date.</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="referenceCurve">Reference curve</param>
    /// <param name="rateResets">The rate resets.</param>
    /// <param name="excludeNotionalPay">if set to <c>true</c>, notional payments are not included in cashflows.</param>
    /// <param name="effectiveRate">The computeed effective swap rate.</param>
    /// <param name="level">The computed swap level.</param>
    /// <returns>The computed swaption strike.</returns>
    public static double EffectiveSwaptionStrike(this Swaption swaption,
      Dt asOf, Dt settle,
      DiscountCurve discountCurve, CalibratedCurve referenceCurve,
      RateResets rateResets, bool excludeNotionalPay,
      out double effectiveRate, out double level)
    {
      Cashflow unitFixedCf, fixedCf, unitFloatCf, floatCf;
      swaption.GenerateCashflows(asOf, settle, discountCurve,
        referenceCurve, rateResets, excludeNotionalPay,
        out unitFixedCf, out fixedCf, out unitFloatCf, out floatCf);
      double strike;
      // Swaption.Maturity is is forward start date of the underlying swap.
      CalculateLevelRateStrike(settle, swaption.Maturity,
        unitFixedCf, fixedCf, unitFloatCf, floatCf, discountCurve, swaption.UnderlyingFixedLeg.Freq,
        out level, out effectiveRate, out strike);
      return strike;
    }

    /// <summary>
    /// Generates the cashflows.
    /// </summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="asOf">The as-of date</param>
    /// <param name="settle">The settle date.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="referenceCurve">The reference curve.</param>
    /// <param name="rateResets">The rate resets.</param>
    /// <param name="excludeNotionalPay">if set to <c>true</c>, notional payments are not included in cashflows.</param>
    /// <param name="unitFixedCf">The generated cashflow for the fixed leg with unit coupon (coupon = 1).</param>
    /// <param name="fixedCf">The generated cashflow for the fixed leg with actual coupons which may be time-varying.</param>
    /// <param name="unitFloatCf">The generated cashflow for the floating leg without any spread (spread = 0).</param>
    /// <param name="floatCf">The generated cashflow for the floating leg with the actual spreads which may be time-varying.</param>
    internal static void GenerateCashflows(
      this Swaption swaption,
      Dt asOf, Dt settle, DiscountCurve discountCurve,
      CalibratedCurve referenceCurve, RateResets rateResets, bool excludeNotionalPay,
      out Cashflow unitFixedCf, out Cashflow fixedCf,
      out Cashflow unitFloatCf, out Cashflow floatCf)
    {
      GenerateCashflows(swaption.UnderlyingFixedLeg, swaption.UnderlyingFloatLeg,
        asOf, settle, discountCurve, referenceCurve, rateResets, excludeNotionalPay,
        out unitFixedCf, out fixedCf, out unitFloatCf, out floatCf);
    }

    /// <summary>
    /// Generates the cashflows.
    /// </summary>
    /// <param name="fixedLeg">The fixed leg.</param>
    /// <param name="floatLeg">The float leg.</param>
    /// <param name="asOf">The as-of date</param>
    /// <param name="settle">The settle date.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="referenceCurve">The reference curve.</param>
    /// <param name="rateResets">The rate resets.</param>
    /// <param name="excludeNotionalPay">if set to <c>true</c>, notional payments are not included in cashflows.</param>
    /// <param name="unitFixedCf">The generated cashflow for the fixed leg with unit coupon (coupon = 1).</param>
    /// <param name="fixedCf">The generated cashflow for the fixed leg with actual coupons which may be time-varying.</param>
    /// <param name="unitFloatCf">The generated cashflow for the floating leg without any spread (spread = 0).</param>
    /// <param name="floatCf">The generated cashflow for the floating leg with the actual spreads which may be time-varying.</param>
    internal static void GenerateCashflows(
      SwapLeg fixedLeg, SwapLeg floatLeg,
      Dt asOf, Dt settle, DiscountCurve discountCurve,
      CalibratedCurve referenceCurve, RateResets rateResets, bool excludeNotionalPay,
      out Cashflow unitFixedCf, out Cashflow fixedCf,
      out Cashflow unitFloatCf, out Cashflow floatCf)
    {
      if (_usePaymentScheduleForCashflow)
      {
        GenerateCashflowsFromPaymentSchedules(fixedLeg, floatLeg, asOf, settle,
          discountCurve, referenceCurve, rateResets, excludeNotionalPay,
          out unitFixedCf, out fixedCf, out unitFloatCf, out floatCf);
        return;
      }

      // we won't go to this part for payment schedule. Keep it for backward compatible.
      GenerateLegacyCashflow(fixedLeg, floatLeg, asOf, settle, discountCurve,
        referenceCurve, rateResets, excludeNotionalPay, out unitFixedCf,
        out fixedCf, out unitFloatCf, out floatCf);
    }

    [Obsolete]
    private static void GenerateLegacyCashflow(SwapLeg fixedLeg, SwapLeg floatLeg,
      Dt asOf, Dt settle, DiscountCurve discountCurve,
      CalibratedCurve referenceCurve, RateResets rateResets, bool excludeNotionalPay,
      out Cashflow unitFixedCf, out Cashflow fixedCf,
      out Cashflow unitFloatCf, out Cashflow floatCf)
    {
      fixedCf = new SwapLegPricer(fixedLeg, asOf, settle, 1.0, discountCurve,
        null, null, null, null, null).GenerateCashflow(null, settle);
      fixedCf.AsOf = asOf;
      if (excludeNotionalPay)
        fixedCf.ClearNotionalPayment();

      var unitLeg = (SwapLeg)fixedLeg.Clone();
      unitLeg.CouponSchedule.Clear();
      unitLeg.Index = "";
      unitLeg.Coupon = UnitCouponForSwaptionAnnuity;
      unitFixedCf = new SwapLegPricer(unitLeg, asOf, settle, 1.0, discountCurve,
        null, null, null, null, null).GenerateCashflow(null, settle);
      unitFixedCf.AsOf = asOf;
      if (excludeNotionalPay)
        unitFixedCf.ClearNotionalPayment();

      var unitFloatLeg = (SwapLeg)floatLeg.Clone();
      unitFloatLeg.CouponSchedule.Clear();
      unitFloatLeg.Coupon = 0.0;
      unitFloatCf = new SwapLegPricer(unitFloatLeg, asOf, settle, 1.0,
        discountCurve, unitFloatLeg.ReferenceIndex, referenceCurve,
        rateResets, null, null).GenerateCashflow(null, settle);
      unitFloatCf.AsOf = asOf;
      if (excludeNotionalPay)
        unitFloatCf.ClearNotionalPayment();
      if (floatLeg.Coupon != 0.0 || floatLeg.CouponSchedule.Count > 0)
      {
        floatCf = new SwapLegPricer(floatLeg, asOf, settle, 1.0, discountCurve,
            floatLeg.ReferenceIndex, referenceCurve, rateResets, null, null).
          GenerateCashflow(null, settle);
        floatCf.AsOf = asOf;
        if (excludeNotionalPay)
          floatCf.ClearNotionalPayment();
      }
      else
      {
        floatCf = null;
      }
      return;
    }

    internal static void GenerateCashflowsFromPaymentSchedules(
      SwapLeg fixedLeg, SwapLeg floatLeg,
      Dt asOf, Dt settle, DiscountCurve discountCurve,
      CalibratedCurve referenceCurve, RateResets rateResets, bool excludeNotionalPay,
      out Cashflow unitFixedCf, out Cashflow fixedCf,
      out Cashflow unitFloatCf, out Cashflow floatCf)
    {
      var fixedPs = new SwapLegPricer(fixedLeg, asOf, settle, 1.0, discountCurve,
        null, null, null, null, null).GetPaymentSchedule(null, settle);
      fixedCf = GetPsCashflowAdapter(fixedPs, excludeNotionalPay, fixedLeg.Notional);

      var unitFixedPs = new SwapLegPricer(fixedLeg, asOf, settle, 1.0, discountCurve,
        null, null, null, null, null).GetPaymentSchedule(null, settle);
      foreach (var payment in unitFixedPs.OfType<FixedInterestPayment>())
      {
        payment.FixedCoupon = UnitCouponForSwaptionAnnuity;
      }
      unitFixedCf = GetPsCashflowAdapter(unitFixedPs, excludeNotionalPay,
        fixedLeg.Notional);

      var floatPs = new SwapLegPricer(floatLeg, asOf, settle, 1.0,
        discountCurve, floatLeg.ReferenceIndex, referenceCurve, rateResets,
        null, null).GetPaymentSchedule(null, settle);
      floatCf = GetPsCashflowAdapter(floatPs, excludeNotionalPay, floatLeg.Notional);
        
      var noSpread = floatPs.OfType<FloatingInterestPayment>()
        .All(p => p.FixedCoupon.AlmostEquals(0.0));
      if (noSpread)
      {
        unitFloatCf = floatCf;
        floatCf = null;
        return;
      }

      var unitFloatPs = new SwapLegPricer(floatLeg, asOf, settle, 1.0,
        discountCurve, floatLeg.ReferenceIndex, referenceCurve, rateResets,
        null, null).GetPaymentSchedule(null, settle);
      foreach (var payment in unitFloatPs.OfType<FloatingInterestPayment>())
      {
        payment.FixedCoupon = 0.0;
      }
      
      unitFloatCf = GetPsCashflowAdapter(unitFloatPs, excludeNotionalPay, floatLeg.Notional);
    }

    internal static Cashflow GetPsCashflowAdapter(PaymentSchedule ps,
      bool excludeNotionalPay, double notional)
    {
      var cfa = new Cashflow(ps, notional);
      if (excludeNotionalPay)
        cfa.ClearNotionalPayment();
      return cfa;
    }
   
    internal const double UnitCouponForSwaptionAnnuity = 0.0001;
    static bool _usePaymentScheduleForCashflow = true;

    /// <summary>
    /// Calculate the effectives the swaption level, rate and strike.
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="fwdStart">The forward start date of the underlying swap.</param>
    /// <param name="unitFixedCf">The cashflow for the fixed leg with unit coupon (coupon = 1).</param>
    /// <param name="fixedCf">The cashflow for the fixed leg with actual coupons, which may vary over time.</param>
    /// <param name="unitFloatCf">The cashflow for the floating leg without any spread (spread = 0).</param>
    /// <param name="floatCf">The cashflow for the floating leg with the actual spreads, which may vary over time.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="freq">Fixed leg payment frequency</param>
    /// <param name="level">The computed swap level.</param>
    /// <param name="effectiveRate">The computeed effective swap rate.</param>
    /// <param name="effectiveStrike">The computed swaption strike.</param>
    internal static void CalculateLevelRateStrike(Dt asOf, Dt fwdStart,
      Cashflow unitFixedCf, Cashflow fixedCf, Cashflow unitFloatCf, Cashflow floatCf,
      DiscountCurve discountCurve, Frequency freq,
      out double level, out double effectiveRate, out double effectiveStrike)
    {
      level = CalculateCashflowPv(asOf, fwdStart, unitFixedCf, discountCurve, freq)/UnitCouponForSwaptionAnnuity;
      if(level < -Double.Epsilon)
      {
        throw new ToolkitException("Got negative swap annuity.");
      }
      if (level < Double.Epsilon) // abosolute value of level is close zero.
      {
        level = effectiveRate = effectiveStrike = 0;
        return;
      }

      var floatingUnitPremium = CalculateCashflowPv(
        asOf, fwdStart, unitFloatCf, discountCurve, freq);
      effectiveRate = floatingUnitPremium / level;

      var fixedPv = CalculateCashflowPv(asOf, fwdStart, fixedCf, discountCurve, freq);
      if (floatCf != null)
      {
        var floatingPremium = CalculateCashflowPv(asOf, fwdStart, floatCf, discountCurve, freq);
        fixedPv += floatingUnitPremium - floatingPremium;
      }
      effectiveStrike = fixedPv / level;
    }

    private static double CalculateCashflowPv(Dt asOf, Dt settle, Cashflow cf, DiscountCurve discountCurve, Frequency freq)
    {
      //NOTE: need to exclude the accrued portion for consideration of cancelling a swap in the middle of coupon period
      int idx;
      for (idx = 0; idx < cf.Count; idx++)
      {
        if (cf.GetEndDt(idx) > settle && cf.GetDt(idx) > settle)
          break;
      }
      var accrualStart = idx > 0 ? cf.GetEndDt(idx - 1) : cf.Effective;
      var periodEnd = cf.GetEndDt(idx);
      
      var accrued = Dt.Cmp(settle, accrualStart) <= 0 ? 0.0 : cf.GetAccrued(idx) * (Dt.Fraction(accrualStart, periodEnd, accrualStart, settle, cf.GetDayCount(idx), freq)) / cf.GetPeriodFraction(idx);
      return Modelpv(asOf, settle, cf, discountCurve)
        - accrued*discountCurve.Interpolate(asOf, cf.GetDt(idx));
    }

    private static double Modelpv(Dt asOf, Dt settle, Cashflow cf, DiscountCurve discountCurve)
    {
      var ps = cf.Ps;
      if (ps != null)
      {
        return ps.GroupByCutoffDate().CalculatePv(asOf, settle, discountCurve,
          null as DefaultRiskCalculator, false, true)/cf.OriginalPrincipal;
      }
      return cf.BackwardCompatiblePv(asOf, settle, discountCurve, null, null, 0.0,
        0, TimeUnit.None, AdapterUtil.CreateFlags(false, false, true));
    }

    private static double CalcForwardSwapDuration(this SwaptionBlackPricer swaptionPricer, double maturity)
    {
      var swapLeg = swaptionPricer.Swaption.UnderlyingFixedLeg;
      var fixedLeg = new SwapLeg(swaptionPricer.Swaption.Maturity, Dt.Add(swaptionPricer.Swaption.Maturity, (int)(maturity*365.0)),
   Currency.None, 0.01, swapLeg.DayCount, swapLeg.Freq, swapLeg.BDConvention, swapLeg.Calendar, swapLeg.AccrueOnCycle);
      var fixedLegPricer = new SwapLegPricer(fixedLeg, swaptionPricer.AsOf, swaptionPricer.Settle, 1.0, swaptionPricer.DiscountCurve, null, null, null, null, null);
      return fixedLegPricer.ProductPv() / 0.01;

    }

    private static double SolveEffectiveSwaptionMaturity(double duration,
      SwaptionBlackPricer swaptionPricer, bool backward)
    {
      var rf = new Brent2();
      rf.setToleranceX(1e-6);
      rf.setToleranceF(1e-8);
      rf.setLowerBounds(1E-10);

      // We do a memberwise clone and leave the original pricer intact.
      var pricer = (SwaptionBlackPricer) swaptionPricer.ShallowCopy();

      Func<double, double> fn = pricer.CalcForwardSwapDuration;

      var initialMaturity = Dt.Diff(swaptionPricer.Swaption.Maturity,
        swaptionPricer.Swaption.UnderlyingFixedLeg.Maturity)/365.0;
      if (backward)
      {
        // We need to calculate the duration by moving the swap start date
        // forward while keeping its maturity fixed.
        var fullDuration = pricer.CalcForwardSwapDuration(initialMaturity);
        if (fullDuration < duration + 1E-4)
        {
          if (fullDuration < duration)
          {
            log4net.LogManager.GetLogger(typeof (RateAveragingUtils)).WarnFormat(
              "Target duration {0} greater than full duration {1}, ignored",
              duration, fullDuration);
          }
          return (initialMaturity*365.0);
        }
        fn = start => fullDuration - pricer.CalcForwardSwapDuration(start);
      }
 
      //Solve in years to improve accuracy.
      double res = rf.solve(fn, null, duration, 1.0/365, initialMaturity + 1);
      return (int) (res*365.0);

    }

    ///<summary>
    /// Calculate the swap effective duration 
    ///</summary>
    ///<param name="swaptionPricer">Swaption pricer</param>
    ///<returns>Effective swap duration</returns>
    ///<exception cref="ToolkitException"></exception>
    public static DateAndValue<double> EffectiveSwaptionDuration(SwaptionBlackPricer swaptionPricer)
    {
      var swaption = swaptionPricer.Swaption;
      var asOf = swaptionPricer.AsOf;
      var settle = swaptionPricer.Settle;
      var discountCurve = swaptionPricer.DiscountCurve;
      var fixLeg = swaption.UnderlyingFixedLeg;
      double fwdTenorMeasure = SwaptionVolatilityCube.ConvertForwardTenor(swaption.Maturity,
        swaption.UnderlyingFixedLeg.Maturity);
      Dt effectiveStart = swaption.Maturity;

      if (fixLeg.AmortizationSchedule.Count > 0)
      {
        if (fixLeg.Coupon.AlmostEquals(0.0))
        {
          fixLeg = fixLeg.CloneObjectGraph();
          fixLeg.Coupon = 1.0;
        }
        var pricer = new SwapLegPricer(fixLeg, asOf, settle, 1.0, discountCurve, null, null, null, null, null);

        var maxNotional = FindMaximumNotional(GetPsCashflowAdapter(
          pricer.GetPaymentSchedule(null, settle), false, fixLeg.Notional));
        var swapFwdDuration = pricer.ProductPv()/fixLeg.Coupon/maxNotional.Value;

        try
        {
          var backward = maxNotional.Date >= fixLeg.Maturity;
          var solvedMaturity = SolveEffectiveSwaptionMaturity(
            swapFwdDuration, swaptionPricer, backward);
          fwdTenorMeasure = solvedMaturity*12.0/365.0;

          if (backward)
          {
            effectiveStart = Dt.AddMonth(effectiveStart,
              (int) Math.Floor(fwdTenorMeasure), true);
          }
          else if (maxNotional.Date > effectiveStart)
          {
            var months = fwdTenorMeasure;
            Dt begin = effectiveStart, end = fixLeg.Maturity,
              date = maxNotional.Date;
            if (date >= end)
            {
              date = end;
            }
            else
            {
              // shorten duration proportionally w.r.t. the maximum notional date 
              months *= (date - begin)/(end - begin);
            }
            var start = Dt.AddMonth(date, -(int) Math.Ceiling(months), true);
            if (start > effectiveStart)
              effectiveStart = start;
          }
        }
        catch (Exception ex)
        {
          throw new ToolkitException("Can not solve equivalent vanilla maturity for amortizing swaption because of " +
            ex.Message);
        }
      }

      return DateAndValue.Create(effectiveStart, fwdTenorMeasure);
    }

    private static DateAndValue<double> FindMaximumNotional(Cashflow cf)
    {
      if (cf == null)
      {
        throw new ArgumentException("Cashflow cannot be null");
      }
      var onceDecreased = false;
      var anchorDate = cf.Effective;
      int n = cf.Count - 1;
      var notional = n >= 0 ? cf.GetPrincipalAt(0) : 0.0;
      for (int i = 1; i <= n; ++i)
      {
        var prin = cf.GetPrincipalAt(i);
        if (prin > notional)
        {
          notional = prin;
          anchorDate = cf.GetStartDt(i);
        }
        else if (!onceDecreased)
        {
          onceDecreased = (prin < notional);
        }
      }
      if (anchorDate > cf.Effective && !onceDecreased)
      {
        // If notional is monotonically increasing,
        // we anchored at the maturity and move the start date.
        Dt end = cf.GetEndDt(n), pay = cf.GetDt(n);
        anchorDate = end > pay ? end : pay;
      }
      if (notional <= 1)
      {
        notional = 1;
      }
      return DateAndValue.Create(anchorDate, notional);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="edfCount"></param>
    /// <param name="capCount"></param>
    /// <param name="volType"></param>
    /// <param name="useSabr"></param>
    /// <param name="lambdaEdf"></param>
    /// <param name="lambdaCap"></param>
    public static void MapFitSettings(RateVolatilityFitSettings settings, int edfCount, int capCount,
                                   VolatilityType volType,
                                   bool useSabr, out double[] lambdaEdf, out double[] lambdaCap)
    {
      double logNormalFitVal = 0.05 + 0.35 * settings.FitToMarket;
      double normalFitVal = 0.05 + 0.15 * settings.FitToMarket;

      if (volType == VolatilityType.LogNormal)
      {
        lambdaEdf = ArrayUtil.Generate(edfCount, (i) => logNormalFitVal);
        lambdaCap = ArrayUtil.Generate(capCount, (i) => logNormalFitVal);
      }
      else
      {
        if (useSabr)
        {
          lambdaEdf = ArrayUtil.Generate(edfCount, (i) => logNormalFitVal);
          lambdaCap = ArrayUtil.Generate(capCount, (i) => logNormalFitVal);
        }
        else
        {
          lambdaEdf = ArrayUtil.Generate(edfCount, (i) => normalFitVal);
          lambdaCap = ArrayUtil.Generate(capCount, (i) => normalFitVal);
        }
      }
    }
  }
}
