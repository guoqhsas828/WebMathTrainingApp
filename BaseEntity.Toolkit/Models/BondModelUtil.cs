//
// Partial proxy for Bond Pricer model
//  -2014. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Models
{
  /// <summary>
  /// BondModel calculations
  /// </summary>
  [ReadOnly(true)]
  public static partial class BondModelUtil
  {
    private const double MaxPrice = 5.0;
    private const string PriceRangeMsg = "Price must be between 0 and 500pc";
    private const string WorkoutRangeMsg = "Workout price must be between 0 and 500pc";

    #region New Methods

    /// <summary>
    /// calculate the accrued interest for the bond
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="BaseEntity.Toolkit.Pricers.BondPricer.AccruedInterest()" />
    /// </remarks>
    /// <seealso cref="BondModel">Standard market bond calculations</seealso>
    /// <param name="settle">Settle date</param>
    /// <param name="bond">Bond product</param>
    /// <param name="dayCount">Bond day count</param>
    /// <param name="couponRate">Coupon rate</param>
    /// <param name="cumDiv">Cum dividend flag.</param>
    /// <param name="ignoreExDiv">Ignore Ex div date in the cash flows flag</param>
    /// <returns>Bond accrued interest</returns>  
    public static double AccruedInterest(Dt settle, Bond bond,DayCount dayCount,
      double couponRate, bool cumDiv, bool ignoreExDiv)
    {
      double accrualFactor;
      int roundingDigits = -1;

      Schedule schedule = bond.Schedule;
      var schedParams = schedule as IScheduleParams;
      Dt effective = schedParams.AccrualStartDate;
      if (settle <= effective)
        return 0.0;

      int idx = schedule.GetNextCouponIndex(settle);
      // if no next coupon , i.e settle >= maturity
      if (idx < 0)
        return 0.0;

      Dt prevCoupon = schedule.GetPeriodStart(idx);
      if (settle <= prevCoupon && idx > 0)
        prevCoupon = schedule.GetPeriodStart(--idx);
      Dt nextCoupon = schedule.GetPeriodEnd(idx);
      if (settle == nextCoupon)
        return 0.0;
      var bondType = bond.BondType;
      Dt exDiv = ExDivDate(bond, nextCoupon);

      // Some special cases
      switch (bondType)
      {
        case BondType.JGB:
          {
            // QUESTION: cumDiv and ignoreExDiv have no effect?
            Dt firstCoupon = schedParams.FirstCouponDate;
            int accrualDays = AccrualDays(settle, bond, false, ignoreExDiv);
            double accruedInterest;
            if ((accrualDays == 183 || accrualDays == 184) && Dt.Cmp(settle, firstCoupon) < 0)
              accruedInterest = couponRate / 2;
            else
            {
              accrualFactor = accrualDays / 182.5;
              accruedInterest = couponRate / 2 * accrualFactor;
            }
            return RoundingUtil.Round(accruedInterest, 9);
          }
        case BondType.CADGovt:
          {
            dayCount = DayCount.Actual365Fixed;
            int accrualDays = AccrualDays(settle, bond, false, ignoreExDiv);
            if (accrualDays == 183)
              return couponRate * (1 - 1 / 182.5) / 2;
          }
          break;
        case BondType.DEMGovt:
          {
            //German bonds follow the ActualActualBond convention for Accrued Interest and 
            //have EOM rule 
            dayCount = DayCount.ActualActualBond;
            roundingDigits = -1;
          }
          break;
        case BondType.FRFGovt:
          {
            //French bonds follow the ActualActualBond convention for Accrued Interest and 
            //have EOM rule 
            dayCount = DayCount.ActualActualBond;
            roundingDigits = 5;
          }
          break;
        case BondType.ITLGovt:
          {
            dayCount = DayCount.ActualActualBond;
            roundingDigits = 7;
          }
          break;
        case BondType.AUSGovt:
          {
            roundingDigits = 5;
          }
          break;
      }

      // All other bonds
      //
      if (!cumDiv && !ignoreExDiv && exDiv <= settle)
        accrualFactor = -schedule.Fraction(settle, nextCoupon, dayCount);
      else
        accrualFactor = schedule.Fraction(prevCoupon, settle, dayCount);

      double ai = couponRate * accrualFactor;
      return RoundingUtil.Round(ai, roundingDigits);
    }

    /// <summary>
    /// Return the Discount Factor to be applied for discounting the first coupon payment
    /// </summary>
    /// <param name="settle"></param>
    /// <param name="bond"></param>
    /// <param name="dc"></param>
    /// <returns></returns>
    public static double DiscountFactor(Dt settle, Bond bond, DayCount dc)
    {
      Schedule schedule = bond.Schedule;
      Frequency freq = bond.Freq;
      var schedParams = schedule as IScheduleParams;
      Dt effective = schedParams.AccrualStartDate;
      if (settle < effective)
        return 0.0;

      int idx = schedule.GetNextCouponIndex(settle);
      // if no next coupon , i.e settle >= maturity
      if (idx < 0)
        return 0.0;

      Dt nextCoupon = schedule.GetPeriodEnd(idx);

      double df = schedule.Fraction(settle, nextCoupon, dc);
      return df * (int)freq;
    }

    /// <summary>
    /// New Accrual Days method for the customized schedule
    /// </summary>
    /// <param name="settle">Settle date</param>
    /// <param name="customizedSchedule">Customized cash flow</param>
    /// <returns></returns>
    public static int AccrualDays(Dt settle, PaymentSchedule customizedSchedule)
    {
      var interestPayments = customizedSchedule.GetPaymentsByType<InterestPayment>();
      var interestPriod = interestPayments.FirstOrDefault(ip => ip.AccrualStart <= settle && ip.AccrualEnd > settle);
      if (interestPriod == null)
        return 0;

      var accrualDays = Dt.FractionDays(interestPriod.AccrualStart, settle, interestPriod.DayCount);
      return accrualDays;
    }

    /// <summary>
    /// New Accrual Days method
    /// </summary>
    /// <param name="settle"></param>
    /// <param name="bond"></param>
    /// <param name="cumDiv"></param>
    /// <param name="ignoreExDivDate"></param>
    /// <returns></returns>
    public static int AccrualDays(Dt settle, Bond bond, bool cumDiv, bool ignoreExDivDate)
    {
      if (bond.CustomPaymentSchedule != null)
        return AccrualDays(settle, bond.CustomPaymentSchedule);
      Schedule schedule = bond.Schedule;
      if (schedule.Count == 0)
        throw new ToolkitException("Schedule is empty. Please check the dates.");

      int accrualDays;

      var schedParams = schedule as IScheduleParams;
      var dayCount = bond.AccrualDayCount;
      var bondType = bond.BondType;
      switch (bondType)
      {
      case BondType.JGB:
      {
        Dt effective = schedParams.AccrualStartDate;
        Dt firstCoupon = schedParams.FirstCouponDate;
        Dt referenceIssueDate = new Dt(1, 3, 2001);
        accrualDays = schedule.AccrualDays(settle, dayCount);

        // If settlement date is in the first coupon period (unless on effective date),
        // then both the effective date and the settlement date are included in the
        // accrual calculation (Krgin, p. 52).

        if (settle < firstCoupon && settle > effective &&
          effective < referenceIssueDate)
          accrualDays++;
        return accrualDays;
      }
      case BondType.DEMGovt:
        //The DayCount is ActualActualBond for DEMGovt bonds 
        dayCount = DayCount.ActualActualBond; //<== why we need this?
        break;
      case BondType.FRFGovt:
        dayCount = DayCount.ActualActualBond; //<== why we need this?
        break;
      }

      accrualDays = schedule.AccrualDays(settle, dayCount); 
      if (!cumDiv && !ignoreExDivDate)
      {
        int idx = schedule.GetNextCouponIndex(settle);
        if (idx < 0)
        {
          // This happens if settle >= maturity.
          // We simply use the last coupon period.
          idx = schedule.Count - 1;
        }

        // Find the next coupon date.
        Dt nextCoupon;
        {
          Dt pStart = schedule.GetPeriodStart(idx);
          nextCoupon = settle <= pStart ? pStart
            : schedule.GetPeriodEnd(idx);
        }

        // See if we need to do ex-div.
        Dt exDiv = ExDivDate(bond, nextCoupon);
        if (exDiv <= settle)
          accrualDays = - schedule.AccrualDays(settle, nextCoupon, dayCount);
      }

      return accrualDays;
    }

    /// <summary>
    /// New Price from yield method
    /// </summary>
    /// <param name="settle"></param>
    /// <param name="nextCouponDate"></param>
    /// <param name="previousCycle"></param>
    /// <param name="N"></param>
    /// <param name="couponRate"></param>
    /// <param name="bond"></param>
    /// <param name="principal"></param>
    /// <param name="quotedYield"></param>
    /// <param name="accruedInterest"></param>
    /// <param name="recoveryRate"></param>
    /// <param name="cumDiv"></param>
    /// <param name="ignoreExDivDate"></param>
    /// <returns></returns>
    public static double PriceFromYield(Dt settle, Dt nextCouponDate, Dt previousCycle, int N, double couponRate, Bond bond, double principal,
      double quotedYield, double accruedInterest, double recoveryRate, bool cumDiv, bool ignoreExDivDate)
    {
      var bondType = bond.BondType;
      var effective = bond.Effective;
      var maturity = bond.Maturity;
      var dayCount = bond.AccrualDayCount;
      var bdc = bond.BDConvention;
      var firstCouponDate = bond.FirstCoupon;
      var lastCouponDate = bond.LastCoupon;
      bool periodAdjustment = bond.PeriodAdjustment;
      var cal = bond.Calendar;
      var freq = bond.Freq;
      bool eomRule = bond.EomRule;
      var ccy = bond.Ccy;

      if (!IsActive(settle, effective, maturity))
        return 0.0;
      if (settle == maturity)
        return 1.0;

      ValidateQuotedYield(quotedYield);
      double price;
      Dt exDivDate = ExDivDate(bond, nextCouponDate);
      Dt prevCouponDate = previousCycle;
      if (periodAdjustment)
        prevCouponDate = Dt.Roll(prevCouponDate, bdc, cal);
      // First and last coupon periods
      double firstPer = ((settle < exDivDate) || ignoreExDivDate)
                      ? FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule,
                                          dayCount, bondType)
                      : 0.0;

      double lastPer = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dayCount, bondType);
      switch (bondType)
      {
        case BondType.USGovt:
          {
            if (N == 1)
              price = BondModel.MoneyMarketPrice(accruedInterest,
                DiscountFactor(settle, bond, dayCount),
                NextCouponAmount(settle, bondType, exDivDate, effective, firstCouponDate, prevCouponDate, nextCouponDate, couponRate, dayCount/*DayCount.ActualActualBond*/, freq, eomRule),
                freq, principal, quotedYield);
            else
              price = BondModel.YtmToPrice(accruedInterest,
                DiscountFactor(settle, bond, dayCount),
                lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedYield);
          }
          break;
        case BondType.JGB:
          {
            int Tsm = JGBSettleToMaturity(settle, maturity);
            price = BondModelJGB.YtmToPrice(principal, couponRate, Tsm, quotedYield);
            price = RoundingUtil.Round(price, 5);
          }
          break;
        case BondType.USTBill:
          {
            int Tsm = Dt.Diff(settle, maturity, DayCount.Actual365Fixed);
            int Ndl = USTbillDays(effective);
            if (Tsm <= 182)
              price = BondModelTM.MoneyMarketPrice(quotedYield, principal, Tsm, 360, Ndl);
            else
            {
              if (Ndl == 266 && Tsm == 183)
                price = BondModelTM.YtmToPriceLeapYear(quotedYield);
              else
                price = BondModelTM.YtmToPrice(quotedYield, principal, Tsm, Ndl);
            }
          }
          break;

        case BondType.ITLGovt:
          {
            double AI = AccruedInterest(settle, bond, dayCount, couponRate, cumDiv, ignoreExDivDate);

            price = BondModelITL.YtmToPrice(AI, principal, quotedYield, settle, effective, firstCouponDate,
                                            lastCouponDate, maturity, ccy, couponRate, dayCount, freq, bdc, cal,
                                            recoveryRate);
          }
          break;
        case BondType.CADGovt:
          {
            // First and last coupon periods
            if (N == 1)
              price = BondModel.MoneyMarketPrice(
                AccruedInterest(settle, bond, DayCount.Actual365Fixed, couponRate, cumDiv, ignoreExDivDate),
                DiscountFactor(settle, bond, DayCount.Actual365Fixed),
                NextCouponAmount(settle, bondType, exDivDate, effective, firstCouponDate, prevCouponDate, nextCouponDate, couponRate, DayCount.Actual365Fixed, freq, eomRule),
                freq, principal, quotedYield);
            else
              price = BondModel.YtmToPrice(
                AccruedInterest(settle, bond, DayCount.ActualActualBond, couponRate, cumDiv, ignoreExDivDate),
                DiscountFactor(settle, bond, DayCount.ActualActualBond),
                lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedYield);
          }
          break;
        case BondType.DEMGovt:
          {
            //For German Bonds the following rule applies,
            //1) Simple Interest in the Last Coupon period 
            //2) Discount Factor is calculated using ActualActualBond Daycount
            //3) End of Month Rule Applies 
            // First and last coupon periods
            if (N == 1)
              price = BondModel.MoneyMarketPrice(
                AccruedInterest(settle, bond, dayCount, couponRate, cumDiv, ignoreExDivDate),
                DiscountFactor(settle, bond, dayCount),
                NextCouponAmount(settle, bondType, exDivDate, effective, firstCouponDate, prevCouponDate, nextCouponDate, couponRate, DayCount.ActualActualBond, freq, true),
                freq, principal, quotedYield);
            else
              price = BondModel.YtmToPrice(
                AccruedInterest(settle, bond, DayCount.ActualActualBond, couponRate, cumDiv, ignoreExDivDate),
                DiscountFactor(settle, bond, DayCount.ActualActualBond),
                lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedYield);

          }
          break;
        case BondType.FRFGovt:
          {
            //For french bonds the following rule applies 
            //1) Compound interest in the last coupon period 
            //2) Discount factor is calculated using the ActualActualBond daycount 
            //3) End of month rule applies 
            price =
              BondModelFRF.YtmToPrice(
                AccruedInterest(settle, bond, DayCount.ActualActualBond, couponRate, cumDiv, ignoreExDivDate),
                DiscountFactor(settle, bond, DayCount.ActualActualBond),
                lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedYield);
            price = RoundingUtil.Round(price, 5);
          }
          break;
        default:
          {
            price = BondModel.YtmToPrice(
              AccruedInterest(settle, bond, dayCount, couponRate, cumDiv, ignoreExDivDate),
              DiscountFactor(settle, bond, dayCount),
              lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedYield);
          }
          break;
      }
      return price;
    }

    /// <summary>
    /// Calculate present value of a yield 01 of bond
    /// </summary>
    /// <remarks>
    /// <para>This is the change in price of a $1000 notional bond given a 1bp reduction in yield.</para>
    /// <formula>
    ///   PV01 = 100\times\frac{pd - pu} {2 \times yieldBump} 
    /// </formula>
    /// <para>where</para>
    /// <list type="bullet">
    ///   <item><description><formula inline="true"> PV01 </formula> Price to yield delta</description></item>
    ///   <item><description><formula inline="true"> yieldBump = 0.0001</formula> is the 1bp yield bump</description></item>
    ///   <item><description><formula inline="true"> pu = P(yield + yieldBump) </formula> is the clean price  after 1 bp upbump in yield</description></item>
    ///   <item><description><formula inline="true"> pd = P(yield - yieldBump)</formula> is the clean price after 1 bp downbump in yield</description></item>
    /// </list>
    /// <para>Reference: Stigum (p. 219).</para>
    /// </remarks>
    public static double PV01(Dt settle, Bond bond, double quotedYield, Dt previousCycle, Dt nextCouponDate, int N, double couponRate,
      double principal, double recoveryRate, bool cumDiv, bool ignoreExDivDate)
    {
      var bondType = bond.BondType;
      var effective = bond.Effective;
      var maturity = bond.Maturity;
      var dayCount = bond.AccrualDayCount;
      var bdc = bond.BDConvention;
      var firstCouponDate = bond.FirstCoupon;
      var lastCouponDate = bond.LastCoupon;
      bool periodAdjustment = bond.PeriodAdjustment;
      var cal = bond.Calendar;
      var freq = bond.Freq;
      bool eomRule = bond.EomRule;
      var ccy = bond.Ccy;

      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;
      ValidateQuotedYield(quotedYield);
      Dt prevCouponDate = previousCycle;
      if (periodAdjustment)
        prevCouponDate = Dt.Roll(prevCouponDate, bdc, cal);
      Dt exDivDate = ExDivDate(bond, nextCouponDate);
      switch (bondType)
      {
        case BondType.CADGovt:
          {
            if (N == 1)
              dayCount = DayCount.Actual365Fixed;
            else
              dayCount = DayCount.ActualActualBond;
            break;
          }
        case BondType.DEMGovt:
          {
            // First and last coupon periods
            double fcf = FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dayCount, bondType);
            return BondModel.Pv01(
                AccruedInterest(settle, bond, dayCount, couponRate, cumDiv, ignoreExDivDate),
                DiscountFactor(settle, bond, dayCount),
                lcf, fcf, lcf, couponRate, N, freq, principal, quotedYield);
          }
        case BondType.JGB:
          {
            int Tsm = JGBSettleToMaturity(settle, maturity);
            return BondModelJGB.Pv01(principal, couponRate, Tsm, quotedYield);
          }
        case BondType.USTBill:
          {
            int tsm = Dt.Diff(settle, maturity, DayCount.Actual365Fixed);
            int Ndl = USTbillDays(effective);
            if (tsm <= 182)
              return BondModelTM.MoneyMarketPv01(quotedYield, tsm, Ndl);
            else
              return BondModelTM.Pv01(quotedYield, principal, tsm, Ndl);
          }
        case BondType.FRFGovt:
          {
            // First and last coupon periods
            double fcf = FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dayCount, bondType);
            return BondModelFRF.Pv01(
                AccruedInterest(settle, bond, dayCount, couponRate, cumDiv, ignoreExDivDate),
                DiscountFactor(settle, bond, dayCount),
                lcf, fcf, lcf, couponRate, N, freq, principal, quotedYield);
          }
        case BondType.ITLGovt:
          {
            double accruedInterest = AccruedInterest(settle, bond, dayCount, couponRate, cumDiv, ignoreExDivDate);
            return BondModelITL.Pv01(accruedInterest, principal, quotedYield, settle, effective, firstCouponDate,
                                     lastCouponDate, maturity, ccy, couponRate, dayCount, freq, bdc, cal, recoveryRate);
          }
        default:
          break;
      }

      // First and last coupon periods
      double firstPer = ((settle < exDivDate) || ignoreExDivDate)
                      ? FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule,
                                          dayCount, bondType)
                      : 0.0;
      double lastPer = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dayCount, bondType);

      return BondModel.Pv01(
        AccruedInterest(settle, bond, dayCount, couponRate, cumDiv, ignoreExDivDate),
        DiscountFactor(settle, bond, dayCount),
        lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedYield);
    }

    /// <summary>
    /// Calculates the (Macaulay) duration of the bond.
    /// </summary>
    /// <remarks>
    /// <para>Duration measures the price elasticity of the bond (i.e the ratio of a
    /// small percentage change in the bond's dirty/full price divided by a small percentage
    /// change in the bond's yield to maturity).</para>
    /// <para>Duration can be thought of as the weighted-average term to maturity of the
    /// cash flows from a bond. The weight of each cash flow is determined by dividing
    /// the present value of the cash flow by the price, and is a measure of bond price
    /// volatility with respect to interest rates.</para>
    /// <para>Duration is calculated using a closed-form solution:</para>
    /// <formula>
    ///   D_{mac} = \frac{ v^{t_{sn}} (p_1 + p_2)}{w B}
    /// </formula>
    /// <para>where</para>
    /// <formula>
    ///   p_1 = C * ((1 + y_w) / y_w) * (((1 - v^{N-1}) / y_w) - ((N - 1) * v^N))
    /// </formula>
    /// <formula>
    ///   p_2 = (t_{sn} * C * (1 - v^{N-1}) / y_w) + ((N - 1 + t_sn) * R * v^{N-1}) + (t_{sn} * C_n)
    /// </formula>
    /// <para>and</para>
    /// <list type="bullet">
    ///   <item><description><formula inline="true"> D_{mac} </formula> is the Macaulay duration</description></item>
    ///   <item><description><formula inline="true"> y_{tm} </formula> is the yield to maturity</description></item>
    ///   <item><description><formula inline="true"> w </formula> is the frequency of coupon payments</description></item>
    ///   <item><description><formula inline="true"> y_{w} = \frac{y_{tm}}{w} </formula> is the periodic yield to maturity</description></item>
    ///   <item><description><formula inline="true"> v = \frac{1}{1 + y_{w}} </formula> is the periodic discount factor;</description></item>
    ///   <item><description><formula inline="true"> R </formula> is the redemption amount;</description></item>
    ///   <item><description><formula inline="true"> C </formula> is the current coupon;</description></item>
    ///   <item><description><formula inline="true"> C_n </formula> is the next coupon;</description></item>
    ///   <item><description><formula inline="true"> B </formula> is the full price of the bond</description></item>
    ///   <item><description><formula inline="true"> AI </formula> is the accrued interest</description></item>
    ///   <item><description><formula inline="true"> t_{sn} </formula> is the period fraction (in years)</description></item>
    ///   <item><description><formula inline="true"> N </formula> is the number of remaining coupon periods</description></item>
    /// </list>
    /// <para>Or alternatively:</para>
    ///   <formula>
    ///     D_{mac} = \displaystyle{\frac{-\frac{dP}{dY}}{P_{full}}(1+\frac{Y}{F})}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><formula inline="true"> D_{mac} </formula> is the Macaulay duration</description></item>
    ///     <item><description><formula inline="true"> \frac{dP}{dY} </formula> is the first derivative of Price with respect to Yield;</description></item>
    ///     <item><description><formula inline="true"> P_{full} </formula> is Price, including accrued;</description></item>
    ///     <item><description><formula inline="true"> Y </formula> is Yield to maturity;</description></item>
    ///     <item><description><formula inline="true"> F </formula> is frequency of coupon payments per year;</description></item>
    ///   </list>
    /// <para>Reference: <i>Handbook of Global Fixed Income Calculations</i> by Dragomir Krgin</para>
    /// <note>Ignores any call features of the bond.</note>
    /// </remarks>
    /// <returns>Duration of bond</returns>
    public static double Duration(Dt settle, Bond bond, Dt previousCycle, Dt nextCouponDate, int N, double flatPrice, double quotedYield,
      double accruedInterest, double principal, double couponRate, double recoveryRate, bool ignoreExDivDate, bool cumDiv)
    {
      var bondType = bond.BondType;
      var effective = bond.Effective;
      var maturity = bond.Maturity;
      var dayCount = bond.AccrualDayCount;
      var bdc = bond.BDConvention;
      var firstCoupon = bond.FirstCoupon;
      var lastCoupon = bond.LastCoupon;
      bool periodAdjustment = bond.PeriodAdjustment;
      var cal = bond.Calendar;
      var freq = bond.Freq;
      bool eomRule = bond.EomRule;
      var ccy = bond.Ccy;

      // Validate
      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;

      if (flatPrice <= 0.0 || flatPrice > MaxPrice)
        throw new ArgumentOutOfRangeException("flatPrice", flatPrice, PriceRangeMsg);
      ValidateQuotedYield(quotedYield);
      Dt prevCoupon = previousCycle;
      if (periodAdjustment)
        prevCoupon = Dt.Roll(prevCoupon, bdc, cal);
      switch (bondType)
      {
        case BondType.CADGovt:
          {
            if (N == 1)
              dayCount = DayCount.Actual365Fixed;
            else
              dayCount = DayCount.ActualActualBond;
            break;
          }
        case BondType.DEMGovt:
          {
            dayCount = DayCount.ActualActualBond;
            eomRule = true;
            double fcf = FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);
            return BondModel.Duration(accruedInterest,
                                        DiscountFactor(settle, bond, dayCount),
                                        lcf, fcf, lcf, couponRate, N, freq, principal, flatPrice, quotedYield);
          }
        case BondType.FRFGovt:
          {
            dayCount = DayCount.ActualActualBond;
            eomRule = true;
            double fcf = FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);
            return BondModelFRF.Duration(accruedInterest,
                                        DiscountFactor(settle, bond, dayCount),
                                        lcf, fcf, lcf, couponRate, N, freq, principal, flatPrice, quotedYield);
          }
        case BondType.JGB:
          {
            int Tsm = JGBSettleToMaturity(settle, maturity);
            double AI = AccruedInterest(settle, bond, dayCount, couponRate, cumDiv, ignoreExDivDate);
            return BondModelJGB.Duration(AI, principal, couponRate, Tsm, quotedYield, N);
          }
        case BondType.USTBill:
          {
            int tsm = Dt.Diff(settle, maturity, DayCount.Actual365Fixed);
            int Ndl = USTbillDays(effective);
            if (tsm <= 182)
              return BondModelTM.MoneyMarketDuration(quotedYield, tsm, Ndl);
            else
              return BondModelTM.Duration(quotedYield, tsm, Ndl);
          }
        case BondType.ITLGovt:
          {
            double AI = AccruedInterest(settle, bond, dayCount, couponRate, cumDiv, ignoreExDivDate);
            return BondModelITL.Duration(AI, principal, quotedYield, flatPrice, settle, effective, firstCoupon,
                                         lastCoupon, maturity, ccy, couponRate, dayCount, freq, bdc, cal, recoveryRate);
          }
        default:
          break;
      }

      Dt exDivDate = ExDivDate(bond, nextCouponDate);
      // First and last coupon periods
      double firstPer = ((settle < exDivDate) || ignoreExDivDate)
                      ? FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule,
                                          dayCount, bondType)
                      : 0.0;

      double lastPer = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);

      return BondModel.Duration(accruedInterest,
        DiscountFactor(settle, bond, dayCount),
        lastPer, firstPer, lastPer, couponRate, N, freq, principal, flatPrice, quotedYield);
    }

    /// <summary>
    /// Calculates modified duration for a bond.
    /// </summary>
    /// <remarks>
    /// <para>Modified duration is defined as the Macaulay duration divided by (1 + periodic yield to maturity).</para>
    /// <formula>
    ///  D_{m} = D_{mac} / ( 1 + y_{w})
    /// </formula>
    /// <para>where</para>
    /// <list type="bullet">
    ///   <item><description><formula inline="true"> D_{m} </formula> is the modified duration</description></item>
    ///   <item><description><formula inline="true"> D_{mac} </formula> is the Macaulay duration</description></item>
    ///   <item><description><formula inline="true"> y_{tm} </formula> is the yield to maturity</description></item>
    ///   <item><description><formula inline="true"> w </formula> is the frequency of coupon payments</description></item>
    ///   <item><description><formula inline="true"> y_{w} = \frac{y_{tm}}{w} </formula> is the periodic yield to maturity</description></item>
    /// </list>
    /// <para>Reference: <i>Handbook of Global Fixed Income Calculations</i> by Dragomir Krgin</para>
    /// </remarks>
    /// <returns>Modified duration of the bond</returns>
    public static double ModDuration(Dt settle, Bond bond, Dt previousCycle, Dt nextCouponDate, int N,
      double flatPrice, double quotedYield, double accruedInterest, double principal, double couponRate,
      double recoveryRate, bool ignoreExDivDate, bool cumDiv)
    {
      var bondType = bond.BondType;
      var effective = bond.Effective;
      var maturity = bond.Maturity;
      var freq = bond.Freq;
      double duration = Duration(settle, bond, previousCycle, nextCouponDate, N, flatPrice, quotedYield, accruedInterest,
                                 principal, couponRate, recoveryRate, ignoreExDivDate, cumDiv);
      switch (bondType)
      {
        case BondType.DEMGovt:
            return BondModel.ModDuration(duration, quotedYield, freq);
        case BondType.FRFGovt:
            return BondModelFRF.ModDuration(duration, quotedYield, freq);
        case BondType.JGB:
            return BondModelJGB.ModDuration(duration, quotedYield);
        case BondType.USTBill:
          {
            int tsm = Dt.Diff(settle, maturity, DayCount.Actual365Fixed);
            int Ndl = USTbillDays(effective);
            if (tsm <= 182)
              return BondModelTM.MoneyMarketModifiedDuration(principal, quotedYield, tsm, Ndl, 360);
            else
              return BondModelTM.ModifiedDuration(principal, quotedYield, tsm, Ndl);
          }
        default:
          return BondModel.ModDuration(duration, quotedYield, freq);
      }
    }

    /// <summary>
    /// Calculates yield to maturity given a price and workout using standard bond equation.
    /// </summary>
    /// <remarks>
    /// <para>Calculates yield given a price using standard bond equation.</para>
    /// <para>All numeric values are entered in absolute terms.  For example, to
    /// specify a coupon rate of 5%, you would enter 0.05.</para>
    /// </remarks>
    public static double YieldToMaturity(Dt settle, Bond bond, Dt previousCycleDate, Dt nextCouponDate,
      Dt maturity, int N, double quotedPrice, double accruedInterest, double principal, double couponRate,
      double recoveryRate, bool ignoreExDivDate, bool cumDiv)
    {
      var bondType = bond.BondType;
      var effective = bond.Effective;
      var dayCount = bond.AccrualDayCount;
      var bdc = bond.BDConvention;
      var firstCouponDate = bond.FirstCoupon;
      var lastCouponDate = bond.LastCoupon;
      bool periodAdjustment = bond.PeriodAdjustment;
      var cal = bond.Calendar;
      var freq = bond.Freq;
      bool eomRule = bond.EomRule;
      var ccy = bond.Ccy;
      // Validate
      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;
      if (principal <= 0.0 || principal > MaxPrice)
        throw new ArgumentOutOfRangeException("principal", principal, WorkoutRangeMsg);
      if (quotedPrice <= 0.0 || quotedPrice > MaxPrice)
        throw new ArgumentOutOfRangeException("quotedPrice", quotedPrice, PriceRangeMsg);

      double yield;
      Dt prevCouponDate = previousCycleDate;
      if (periodAdjustment)
        prevCouponDate = Dt.Roll(previousCycleDate, bdc, cal);

      Dt exDivDate = ExDivDate(bond, nextCouponDate);

      //TODO: move the first and last period calculations to outside the switch-case block 
      switch (bondType)
      {
        case BondType.USGovt:
          {
            // First and last coupon periods
            double firstPer = FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule, dayCount, bondType/*DayCount.ActualActualBond*/);
            double lastPer = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dayCount, bondType/*DayCount.ActualActualBond*/);

            if (N == 1)
              yield = BondModel.MoneyMarketYield(accruedInterest,
                DiscountFactor(settle, bond, dayCount),
                NextCouponAmount(settle, bondType, exDivDate, effective, firstCouponDate, prevCouponDate, nextCouponDate, couponRate, dayCount/*DayCount.ActualActualBond*/, freq, eomRule),
                freq, principal, quotedPrice);
            else
              yield = BondModel.PriceToYtm(accruedInterest,
                DiscountFactor(settle, bond, dayCount),
                lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedPrice);

            // Round to 8 dp's
            yield = Math.Floor(yield * Math.Pow(10, 8) + 0.5) / Math.Pow(10, 8);
          }
          break;
        case BondType.USTBill:
          {
            int tsm = Dt.Diff(settle, maturity, DayCount.Actual365Fixed);
            int Ndl = USTbillDays(effective);
            if (tsm <= 182)
            {
              yield = BondModelTM.MoneyMarketYield(quotedPrice, principal, tsm, 360, Ndl);
            }
            else
            {
              if (tsm == 183 && Ndl == 366)
                yield = BondModelTM.PriceToYtmLeapYear(quotedPrice);
              else
                yield = BondModelTM.PriceToYtm(quotedPrice, principal, tsm, Ndl);
            }
          }
          break;
        case BondType.UKGilt:
          {
            // First and last coupon periods
            double firstPer = ((settle < exDivDate) || ignoreExDivDate)
                                ? FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule,
                                                    DayCount.ActualActualBond, bondType)
                                : 0;

            double lastPer = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, DayCount.ActualActualBond, bondType);

            yield = BondModel.PriceToYtm(accruedInterest,
              DiscountFactor(settle, bond, dayCount),
              lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedPrice);

            // Round to 8 dp's
            yield = Math.Floor(yield * Math.Pow(10, 8) + 0.5) / Math.Pow(10, 8);
          }
          break;
        case BondType.JGB:
          {
            int Tsm = JGBSettleToMaturity(settle, maturity);
            return BondModelJGB.PriceToYtm(principal, couponRate, Tsm, quotedPrice);
          }
        case BondType.CADGovt:
          {
            DayCount dc = (N == 1 ? DayCount.Actual365Fixed : DayCount.ActualActualBond);

            //specified 1st coupon and in 1st coupon period
            double firstPer = FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule, dc, bondType);
            double lastPer = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dc, bondType);

            if (N == 1)
              yield = BondModel.MoneyMarketYield(accruedInterest,
                DiscountFactor(settle, bond, dc),
                NextCouponAmount(settle, bondType, exDivDate, effective, firstCouponDate, prevCouponDate, nextCouponDate, couponRate, dc, freq, eomRule),
                freq, principal, quotedPrice);
            else
              yield = BondModel.PriceToYtm(accruedInterest,
                DiscountFactor(settle, bond, dc),
                lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedPrice);

            yield = Math.Floor(yield * Math.Pow(10, 8) + 0.5) / Math.Pow(10, 8);
          }
          break;
        case BondType.DEMGovt:
          {
            double fcf = FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dayCount, bondType);
            if (N == 1)
              yield = BondModel.MoneyMarketYield(accruedInterest,
                DiscountFactor(settle, bond, dayCount),
                NextCouponAmount(settle, bondType, exDivDate, effective, firstCouponDate, prevCouponDate, nextCouponDate, couponRate, dayCount, freq, eomRule),
                freq, principal, quotedPrice);
            else
              yield = BondModel.PriceToYtm(accruedInterest,
                DiscountFactor(settle, bond, dayCount),
                lcf, fcf, lcf, couponRate, N, freq, principal, quotedPrice);

          }
          break;
        case BondType.FRFGovt:
          {
            double fcf = FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dayCount, bondType);
            yield = BondModelFRF.PriceToYtm(accruedInterest,
                DiscountFactor(settle, bond, dayCount),
                lcf, fcf, lcf, couponRate, N, freq, principal, quotedPrice);

          }
          break;
        case BondType.ITLGovt:
          {
            yield = BondModelITL.PriceToYtm(accruedInterest, principal, quotedPrice, settle, effective, firstCouponDate,
                                            lastCouponDate, maturity, ccy, couponRate, dayCount, freq, bdc, cal,
                                            recoveryRate);
            //Convert semi-annual yield to effective yield 
            yield = Math.Pow(1 + (yield / 2), 2) - 1.0;

          }
          break;
        default:
          {
            // First and last coupon periods

            double firstPer = ((settle < exDivDate) || ignoreExDivDate)
                              ? FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule,
                                                  dayCount, bondType)
                              : 0;
            if (N == 1 && firstPer.Equals(1.0))
            {
              Dt nextCycleDate = Dt.Add(previousCycleDate, freq, 1, bond.CycleRule);
              if (maturity < nextCycleDate)
              {
                // If the maturity is inside the first cycle period,
                // then the firstPer must be calculated based on maturity.
                firstPer = Dt.Fraction(previousCycleDate, nextCycleDate,
                  prevCouponDate, maturity, dayCount, freq)*(int)freq;
              }
            }
            double lastPer = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dayCount, bondType);

            yield = BondModel.PriceToYtm(accruedInterest,
              DiscountFactor(settle, bond, dayCount),
              lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedPrice);

            // Round to 8 dp's
            yield = Math.Floor(yield * Math.Pow(10, 8) + 0.5) / Math.Pow(10, 8);
          }
          break;
      }
      return yield;
    }

    /// <summary>
    /// Calculate bond yield to the next put date.
    /// </summary>
    /// <remarks>
    /// <para>Calculates the yield to maturity of the bond to the next put date
    /// using standard bond equation.</para>
    /// <para>If the settlement date is in a put period, the yield is calculated to
    /// the first call date of the next put period.</para>
    /// </remarks>
    /// <returns>Calculate yield-to-call</returns>
    public static double YieldToPut(Dt settle, Bond bond, Dt previousCycleDate, Dt nextCouponDate, Dt maturity,
      int N, double quotedPrice, double accruedInterest, double principal, double couponRate,
      double recoveryRate, bool ignoreExDiv, bool cumDiv)
    {
      var effective = bond.Effective;
      var putSchedule = bond.PutSchedule;

      // Validate
      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;
      if (principal <= 0.0 || principal > MaxPrice)
        throw new ArgumentOutOfRangeException("principal", principal, WorkoutRangeMsg);
      if (quotedPrice <= 0.0 || quotedPrice > MaxPrice)
        throw new ArgumentOutOfRangeException("quotedPrice", quotedPrice, PriceRangeMsg);


      if (putSchedule != null)
      {
        foreach (PutPeriod p in putSchedule)
        {
          if (p.StartDate > settle)
          {
            // calculate yield to start of call period
            return YieldToEarlyRedemption(settle, p.StartDate, p.PutPrice, bond, previousCycleDate, nextCouponDate, quotedPrice, accruedInterest, couponRate,
                                          recoveryRate, ignoreExDiv, cumDiv);
          }
        }
      }
      return YieldToMaturity(settle, bond, previousCycleDate, nextCouponDate, bond.Maturity, N, quotedPrice,
                             accruedInterest, principal, couponRate, recoveryRate, ignoreExDiv, cumDiv);

    }

    /// <summary>
    /// Calculate bond yield to the next call date.
    /// </summary>
    /// <remarks>
    /// <para>Calculates the yield to maturity of the bond to the next call date
    /// using standard bond equation.</para>
    /// <para>If the settlement date is in a call period, the yield is calculated to
    /// the first call date of the next call period.</para>
    /// </remarks> 
    /// <returns>Calculate yield-to-call</returns>
    public static double YieldToCall(Dt settle, Bond bond, Dt previousCycleDate, Dt nextCouponDate,
      Dt maturity, int N, double quotedPrice, double accruedInterest, double principal,
      double couponRate, double recoveryRate, bool ignoreExDiv, bool cumDiv)
    {
      var effective = bond.Effective;
      var callSchedule = bond.CallSchedule.GetActivePeriodsBackwardCompatible(
        settle, bond.GetNotificationDays());
      // Validate
      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;
      if (principal <= 0.0 || principal > MaxPrice)
        throw new ArgumentOutOfRangeException("principal", principal, WorkoutRangeMsg);
      if (quotedPrice <= 0.0 || quotedPrice > MaxPrice)
        throw new ArgumentOutOfRangeException("quotedPrice", quotedPrice, PriceRangeMsg);

      if (callSchedule != null)
      {
        foreach (CallPeriod c in callSchedule)
        {
          if (c.StartDate > settle)
          {
            // calculate yield to start of call period
            return YieldToEarlyRedemption(settle, c.StartDate, c.CallPrice, bond, previousCycleDate,
              nextCouponDate, quotedPrice, accruedInterest, couponRate,
              recoveryRate, ignoreExDiv, cumDiv);
          }
        }
      }
      return YieldToMaturity(settle, bond, previousCycleDate, nextCouponDate, bond.Maturity, N,
        quotedPrice, accruedInterest, principal, couponRate,
        recoveryRate, ignoreExDiv, cumDiv);
    }

    /// <summary>
    /// Calculate bond yield to worst call date
    /// </summary>
    /// <remarks>
    /// <para>Calculates yield to worst call date using standard bond equation.</para>
    /// <para>The Workout Date is set equal to the date for which Yield is the lowest. 
    /// The dates considered are the call dates(beginning and end of call periods) and maturity.</para>
    /// </remarks>    
    /// <returns>Calculate yield-to-worst</returns>
    public static double YieldToWorst(Dt settle,Bond bond, Dt previousCycleDate, Dt nextCouponDate,
      Dt maturity, int N, double quotedPrice, double accruedInterest, double principal, double couponRate,
      double recoveryRate, bool ignoreExDiv, bool cumDiv)
    {
      var effective = bond.Effective;
      var callSchedule = bond.CallSchedule.GetActivePeriodsBackwardCompatible(
        settle, bond.GetNotificationDays());
      
      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;
      if (principal <= 0.0 || principal > MaxPrice)
        throw new ArgumentOutOfRangeException("principal", principal, WorkoutRangeMsg);
      if (quotedPrice <= 0.0 || quotedPrice > MaxPrice)
        throw new ArgumentOutOfRangeException("quotedPrice", quotedPrice, PriceRangeMsg);

      double yieldToWorst = YieldToMaturity(settle, bond, previousCycleDate, nextCouponDate, maturity, N, quotedPrice,
                                            accruedInterest, principal, couponRate, recoveryRate, ignoreExDiv, cumDiv);

      if (callSchedule != null)
      {
        foreach (CallPeriod c in callSchedule)
        {
          if (c.StartDate > settle)
          {
            // calculate yield to start of call period
            var yieldToStart = YieldToEarlyRedemption(settle, c.StartDate, c.CallPrice, bond, previousCycleDate, nextCouponDate, quotedPrice, accruedInterest,
                                                      couponRate, recoveryRate, ignoreExDiv, cumDiv);
            // calculate yield to end of call period
            var yieldToEnd = YieldToEarlyRedemption(settle, c.EndDate, c.CallPrice, bond, previousCycleDate, nextCouponDate, quotedPrice, accruedInterest,
                                                    couponRate, recoveryRate, ignoreExDiv, cumDiv);

            yieldToWorst = Math.Min(yieldToWorst, yieldToStart);
            yieldToWorst = Math.Min(yieldToWorst, yieldToEnd);
          }
        }
      }

      return yieldToWorst;
    }

    /// <summary>
    /// Gets the active option periods which can be exercised
    ///  after the as-of date.
    /// </summary>
    /// <param name="periods">The periods.</param>
    /// <param name="asOf">As of.</param>
    /// <param name="notificationDays">The notification days.</param>
    /// <returns>IEnumerable&lt;T&gt;.</returns>
    public static IEnumerable<CallPeriod> GetActivePeriods(
      this IEnumerable<CallPeriod> periods,
      Dt asOf, BusinessDays notificationDays)
    {
      return periods == null ? null
        : BuildActivePeriods(periods, asOf, notificationDays);
    }

    // The backward compatible version:
    // With 0 notification days, it may return the call periods before the as-of date.
    private static IEnumerable<CallPeriod> GetActivePeriodsBackwardCompatible(
      this IEnumerable<CallPeriod> periods,
      Dt asOf, BusinessDays notificationDays)
    {
      return periods == null || notificationDays.Count == 0
        ? periods
        : BuildActivePeriods(periods, asOf, notificationDays);
    }

    private static IEnumerable<CallPeriod> BuildActivePeriods(
      IEnumerable<CallPeriod> periods,
      Dt asOf, BusinessDays notificationDays)
    {
      // The active period should count from the END of the trade date,
      // equivalent to the begin of (T + 1) in calendar days.
      var activePeriodStart = (asOf + 1) + notificationDays;
      foreach (var cs in periods)
      {
        Debug.Assert(cs.EndDate >= cs.StartDate);
        var end = cs.EndDate;
        if (end < activePeriodStart)
        {
          continue; // ignore, for it is gone completely
        }
        yield return (cs.StartDate >= activePeriodStart) ? cs :
          new CallPeriod(activePeriodStart, end, cs.CallPrice,
            cs.TriggerPrice, cs.Style, cs.Grace);
      }
    }

    public static BusinessDays GetNotificationDays(this Bond bond)
    {
      return new BusinessDays(bond.NotificationDays, bond.Calendar);
    }

    /// <summary>
    /// Calculates yield to worst using standard bond equation.
    /// </summary>
    /// <remarks>
    /// <para>Calculates yield to worst using standard bond equation.</para>
    /// <para>The worst date is set equal to the date for which Yield is the lowest. 
    /// The dates considered are the put/call dates(beginning and end of put/call periods) and maturity.</para>
    /// </remarks>
    /// <returns>Calculate yield-to-worst</returns>
    public static double YieldToWorstConvertible(Dt settle, Bond bond, Dt previousCycleDate, Dt nextCouponDate,
      Dt maturity, int N, double quotedPrice, double accruedInterest, double principal, double couponRate,
      double recoveryRate, bool ignoreExDiv, bool cumDiv)
    {
      var yieldToWorst = YieldToMaturity(settle, bond, previousCycleDate, nextCouponDate, maturity, N, quotedPrice, accruedInterest, principal, couponRate, recoveryRate, ignoreExDiv, cumDiv);
      yieldToWorst = Math.Min(yieldToWorst, YieldToCall(settle, bond, previousCycleDate, nextCouponDate, maturity, N, quotedPrice,
                                                        accruedInterest, principal, couponRate, recoveryRate, ignoreExDiv, cumDiv));
      yieldToWorst = Math.Min(yieldToWorst, YieldToPut(settle, bond, previousCycleDate, nextCouponDate, maturity, N, quotedPrice,
                                                       accruedInterest, principal, couponRate, recoveryRate, ignoreExDiv, cumDiv));
      return yieldToWorst;
    }

    private static double YieldToEarlyRedemption(Dt settle, Dt earlyRedemptionDate, double redemptionPrice, Bond bond, Dt previousCycleDate, Dt nextCouponDate,
                                                 double quotedPrice, double accruedInterest, double couponRate, double recoveryRate, bool ignoreExDiv,
                                                 bool cumDiv)
    {
      var nextFirstCoupon = bond.FirstCoupon <= earlyRedemptionDate ? bond.FirstCoupon : earlyRedemptionDate;
      var nextLastCoupon = bond.Schedule.GetPrevCouponDate(Dt.Add(earlyRedemptionDate, -1)); // do this to make sure date cycling is the same
      var bondToCall = new Bond(bond.Effective, earlyRedemptionDate, bond.Ccy, bond.BondType, bond.Coupon, bond.DayCount, bond.CycleRule, bond.Freq,
                                bond.BDConvention, bond.Calendar) {FirstCoupon = nextFirstCoupon, LastCoupon = nextLastCoupon};
      var yieldToStart = YieldToMaturity(settle, bondToCall, previousCycleDate, nextCouponDate, earlyRedemptionDate,
                                         bondToCall.Schedule.NumberOfCouponsRemaining(settle), quotedPrice,
                                         accruedInterest, redemptionPrice, couponRate, recoveryRate, ignoreExDiv, cumDiv);
      return yieldToStart;
    }

    /// <summary>
    /// Calculates the approximate first derivative of Price with respect to Yield using the standard bond equation.
    /// </summary>
    /// <remarks>
    /// <formula>
    ///   \displaystyle{\frac{dP}{dY} = \frac{pd - pu}{2.0 \times yieldBump} / 100 }
    /// </formula>
    /// <para>where</para>
    /// <list type="bullet">
    ///   <item><description><formula inline="true"> dp/dY </formula> Derivative of Price with respect to Yield</description></item>
    ///   <item><description><formula inline="true"> yieldBump = 0.0001</formula> is the 1bp yield bump</description></item>
    ///   <item><description><formula inline="true"> pd = P(yield + yieldBump) </formula> is the clean price  after 1 bp upbump in yield</description></item>
    ///   <item><description><formula inline="true"> pd = P(yield - yieldBump)</formula> is the clean price after 1 bp downbump in yield</description></item>
    /// </list>
    /// <para>Reference: Stigum (p. 219).</para>
    /// </remarks>
    /// <returns>The first derivative </returns>
    public static double dPdY(Dt settle, Bond bond, Dt previousCycle, Dt nextCouponDate, int N, double quotedYield,
      double accruedInterest, double principal, double couponRate, double recoveryRate, bool ignoreExDivDate, bool cumDiv)
    {
      var bondType = bond.BondType;
      var effective = bond.Effective;
      var maturity = bond.Maturity;
      var dayCount = bond.AccrualDayCount;
      var bdc = bond.BDConvention;
      var firstCoupon = bond.FirstCoupon;
      var lastCoupon = bond.LastCoupon;
      bool periodAdjustment = bond.PeriodAdjustment;
      var cal = bond.Calendar;
      var freq = bond.Freq;
      bool eomRule = bond.EomRule;
      var ccy = bond.Ccy;
      // Validate
      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;
      ValidateQuotedYield(quotedYield);

      Dt prevCouponDate = previousCycle;
      if (periodAdjustment)
        prevCouponDate = Dt.Roll(previousCycle, bdc, cal);

      Dt exDivDate = ExDivDate(bond, nextCouponDate);

      switch (bondType)
      {
        case BondType.CADGovt:
          {
            if (N == 1)
              dayCount = DayCount.Actual365Fixed;
            else
              dayCount = DayCount.ActualActualBond;
            break;
          }
        case BondType.DEMGovt:
          {
            double fcf = FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);
            return BondModel.dPdY(accruedInterest,
                                    DiscountFactor(settle, bond, dayCount),
                                    lcf, fcf, lcf, couponRate, N, freq, principal, quotedYield);
          }
        case BondType.FRFGovt:
          {
            double fcf = FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);
            return BondModelFRF.dPdY(accruedInterest,
                                    DiscountFactor(settle, bond, dayCount),
                                    lcf, fcf, lcf, couponRate, N, freq, principal, quotedYield);
          }
        case BondType.JGB:
          {
            int Tsm = JGBSettleToMaturity(settle, maturity);
            return BondModelJGB.dPdY(principal, couponRate, Tsm, quotedYield);
          }
        case BondType.USTBill:
          {
            int tsm = Dt.Diff(settle, maturity, DayCount.Actual365Fixed);
            int Ndl = USTbillDays(effective);
            if (tsm <= 182)
              return BondModelTM.MoneyMarketdPdY(quotedYield, tsm, Ndl);
            else
              return BondModelTM.dPdY(quotedYield, principal, tsm, Ndl);
          }
        case BondType.ITLGovt:
          {
            return BondModelITL.dPdY(accruedInterest, principal, quotedYield, settle, effective, firstCoupon, lastCoupon,
                                     maturity, ccy, couponRate, dayCount, freq, bdc, cal, recoveryRate);

          }
        default:
          break;
      }

      //specified 1st coupon and in 1st coupon period
      double firstPer = ((settle < exDivDate) || ignoreExDivDate)
                    ? FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule,
                                        dayCount, bondType)
                    : 0.0;
      double lastPer = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);

      return BondModel.dPdY(accruedInterest,
        DiscountFactor(settle, bond, dayCount),
        lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedYield);
    }

    /// <summary>
    /// Calculate approximately the 2nd derivative of Price with respect to Yield using the standard bond equation.
    /// </summary>
    /// <remarks>
    /// <formula>
    ///   dP^2/dY^2 =  ( pd + pu - 2 * pm ) / ( yieldBump * yieldbump )
    /// </formula>
    /// <para>where</para>
    /// <list type="bullet">
    ///   <item><description><formula inline="true"> dP^2/dY^2 </formula> 2nd Derivative of Pricer with respect to Yield</description></item>
    ///   <item><description><formula inline="true"> yieldBump = 0.0001</formula> is the 1bp yield bump</description></item>
    ///   <item><description><formula inline="true"> pu = P(yield + yieldBump) </formula> is the clean price after 1 bp upbump in yield;</description></item>
    ///   <item><description><formula inline="true"> pd = P(yield - yieldBump)</formula> is the clean price after 1 bp downbump in yield;</description></item>
    ///   <item><description><formula inline="true"> pm = P(yield)</formula> is the clean price at current yield;</description></item>
    /// </list>
    /// </remarks>
    /// <returns>Convexity of the bond</returns>
    /// <exclude/>
    public static double dP2dY2(Dt settle, Bond bond, Dt previousCycleDate, Dt nextCouponDate, int N,
      double flatPrice, double quotedYield, double accruedInterest, double principal, double couponRate,
      double recoveryRate, bool ignoreExDivDate, bool cumDiv)
    {
      var bondType = bond.BondType;
      var effective = bond.Effective;
      var maturity = bond.Maturity;
      var dayCount = bond.AccrualDayCount;
      var bdc = bond.BDConvention;
      var firstCoupon = bond.FirstCoupon;
      var lastCoupon = bond.LastCoupon;
      bool periodAdjustment = bond.PeriodAdjustment;
      var cal = bond.Calendar;
      var freq = bond.Freq;
      bool eomRule = bond.EomRule;
      var ccy = bond.Ccy;

      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;
      if (flatPrice <= 0.0 || flatPrice > MaxPrice)
        throw new ArgumentOutOfRangeException("flatPrice", flatPrice, PriceRangeMsg);
      ValidateQuotedYield(quotedYield);


      Dt prevCouponDate = previousCycleDate;
      if (periodAdjustment)
        prevCouponDate = Dt.Roll(previousCycleDate, bdc, cal);
      switch (bondType)
      {
        case BondType.CADGovt:
          {
            if (N == 1)
              dayCount = DayCount.Actual365Fixed;
            else
              dayCount = DayCount.ActualActualBond;
            break;
          }
        case BondType.DEMGovt:
          {
            double fcf = FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);
            return BondModel.dP2dY2(accruedInterest,
                                      DiscountFactor(settle, bond, dayCount),
                                      lcf, fcf, lcf, couponRate, N, freq, principal, flatPrice, quotedYield);
          }
        case BondType.FRFGovt:
          {
            double fcf = FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);
            return BondModelFRF.dP2dY2(accruedInterest,
                                      DiscountFactor(settle, bond, dayCount),
                                      lcf, fcf, lcf, couponRate, N, freq, principal, flatPrice, quotedYield);
          }
        case BondType.JGB:
          {
            int Tsm = JGBSettleToMaturity(settle, maturity);
            return BondModelJGB.dP2dY2(principal, couponRate, Tsm, quotedYield);
          }
        case BondType.USTBill:
          {
            int tsm = Dt.Diff(settle, maturity, DayCount.Actual365Fixed);
            int Ndl = USTbillDays(effective);
            if (tsm <= 182)
              return BondModelTM.MoneyMarketdP2dY2(quotedYield, tsm, Ndl);
            else
              return BondModelTM.dP2dY2(quotedYield, principal, tsm, Ndl);
          }
        case BondType.ITLGovt:
          {
            return BondModelITL.dP2dY2(accruedInterest, principal, quotedYield, flatPrice, settle, effective,
                                       firstCoupon, lastCoupon, maturity, ccy, couponRate, dayCount, freq, bdc, cal,
                                       recoveryRate);
          }
        default:
          break;
      }
      Dt exDivDate = ExDivDate(bond, nextCouponDate);
      //specified 1st coupon and in 1st coupon period
      double firstPer = ((settle < exDivDate) || ignoreExDivDate)
                    ? FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule,
                                        dayCount, bondType)
                    : 0.0;
      double lastPer = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);

      return BondModel.dP2dY2(accruedInterest,
        DiscountFactor(settle, bond, dayCount),
        lastPer, firstPer, lastPer, couponRate, N, freq, principal, flatPrice, quotedYield);
    }

    /// <summary>
    /// Calculate convexity of bond
    /// </summary>
    /// <remarks>
    /// <para>This is the change in Pv01 of a $1000 notional bond given a 1bp reduction in yield.</para>
    /// <para>It is a volatility measure for bonds used in conjunction with modified duration in order to 
    /// measure how the bond's price will change as interest rates change. It is equal to 
    /// the negative of the second derivative of the bond's price relative to its yield, 
    /// divided by its price.</para>
    /// <formula>
    ///   Convexity = \displaystyle{\frac{d^2P}{dY^2}/P_{full}}
    /// </formula>
    /// <para>where</para>
    /// <list type="bullet">
    ///   <item><description><formula inline="true"> \frac{d^2P}{dY^2} </formula> is the 2nd derivative of Price with respect to Yield (twice)</description></item>
    ///   <item><description><formula inline="true"> P_{full} </formula> is the Price including accrued</description></item>
    /// </list>
    /// <para>or alternatively</para>
    /// <formula>
    ///   Convexity =  ( P_{d} + P_{u} - 2 * P ) / ( B_{y} * B_{y} * P )
    /// </formula>
    /// <para>where</para>
    /// <list type="bullet">
    ///   <item><description><formula inline="true"> B_{y} </formula> (1bp) is the yield bump</description></item>
    ///   <item><description><formula inline="true"> P </formula> is the clean price after 1 bp upbump in yield;</description></item>
    ///   <item><description><formula inline="true"> P_{d} </formula> is the clean price after 1 bp downbump in yield;</description></item>
    ///   <item><description><formula inline="true"> P_{u} </formula> is the clean price at current yield;</description></item>
    /// </list>
    /// <para>This uses the standard bond equations ignoring any call provisions, amortizations, or coupon schedules.</para>
    /// <para>Reference: <i>Handbook of Global Fixed Income Calculations</i> by Dragomir Krgin</para>
    /// </remarks>
    /// <returns>Convexity of the bond</returns>
    public static double Convexity(Dt settle, Bond bond, Dt previousCycleDate, Dt nextCouponDate, int N, double flatPrice, double quotedYield,
      double accruedInterest, double principal, double couponRate, double recoveryRate, bool ignoreExDivDate, bool cumDiv)
    {
      var bondType = bond.BondType;
      var effective = bond.Effective;
      var maturity = bond.Maturity;
      var dayCount = bond.AccrualDayCount;
      var bdc = bond.BDConvention;
      var firstCoupon = bond.FirstCoupon;
      var lastCoupon = bond.LastCoupon;
      bool periodAdjustment = bond.PeriodAdjustment;
      var cal = bond.Calendar;
      var freq = bond.Freq;
      bool eomRule = bond.EomRule;
      var ccy = bond.Ccy;

      // Validate
      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;
      if (flatPrice <= 0.0 || flatPrice > MaxPrice)
        throw new ArgumentOutOfRangeException("flatPrice", flatPrice, PriceRangeMsg);
      ValidateQuotedYield(quotedYield);

      Dt exDivDate = ExDivDate(bond, nextCouponDate);
      Dt prevCouponDate = previousCycleDate;
      if (periodAdjustment)
        prevCouponDate = Dt.Roll(prevCouponDate, bdc, cal);

      switch (bondType)
      {
        case BondType.CADGovt:
          {
            if (N == 1)
              dayCount = DayCount.Actual365Fixed;
            else
              dayCount = DayCount.ActualActualBond;
            break;
          }
        case BondType.DEMGovt:
          {
            dayCount = DayCount.ActualActualBond;
            eomRule = true;
            double fcf = FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);

            return BondModel.Convexity(accruedInterest, DiscountFactor(settle, bond, dayCount),
              lcf, fcf, lcf, couponRate, N, freq, principal, flatPrice, quotedYield);
          }
        case BondType.FRFGovt:
          {
            dayCount = DayCount.ActualActualBond;
            eomRule = true;
            double fcf = FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);

            return BondModelFRF.Convexity(accruedInterest, DiscountFactor(settle, bond, dayCount),
              lcf, fcf, lcf, couponRate, N, freq, principal, flatPrice, quotedYield);

          }
        case BondType.ITLGovt:
          {
            dayCount = DayCount.ActualActualBond;
            eomRule = true;
            return BondModelITL.Convexity(accruedInterest, principal, quotedYield, flatPrice, settle, effective,
              firstCoupon, lastCoupon, maturity, ccy, couponRate, dayCount, freq, bdc, cal, recoveryRate);
          }
        case BondType.JGB:
          {
            int Tsm = JGBSettleToMaturity(settle, maturity);
            return BondModelJGB.Convexity(accruedInterest, principal, couponRate, Tsm, quotedYield);
          }
        case BondType.USTBill:
          {
            int tsm = Dt.Diff(settle, maturity, DayCount.Actual365Fixed);
            int Ndl = USTbillDays(effective);
            if (tsm <= 182)
              return BondModelTM.MoneyMarketConvexity(principal, quotedYield, tsm, Ndl, 360);
            else
              return BondModelTM.Convexity(quotedYield, principal, tsm, Ndl);
          }
        default:
          break;
      }

      // First and last coupon periods

      double firstPer = ((settle < exDivDate) || ignoreExDivDate)
                          ? FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule,
                                              dayCount, bondType)
                          : 0.0;
      double lastPer = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);

      return BondModel.Convexity(accruedInterest,
        DiscountFactor(settle, bond, dayCount),
        lastPer, firstPer, lastPer, couponRate, N, freq, principal, flatPrice, quotedYield);
    }
    
    #endregion New Methods

    #region Methods

    /// <summary>
    /// Calculates the factor applied to the first coupon period for discounting and accrued interest.
    /// </summary>
    public static double FirstCouponFactor(Dt settleDate, Dt issueDate, Dt firstCouponDate, Frequency freq, bool eomRule, DayCount dayCount, BondType bondType)
    {
      if (firstCouponDate.IsEmpty() || issueDate > settleDate || settleDate >= firstCouponDate)
        return 1.0;
      else
      {
        switch (bondType)
        {
          case BondType.DEMGovt:
            {
              //For German Government bonds the EOM Rule is always in effect and the daycount conventions are 
              //Actual/Actual(in period) for everything
              Dt normalIssue = Dt.Add(firstCouponDate, freq, -1, true);
              return (double)freq * Dt.Fraction(normalIssue, firstCouponDate, issueDate, firstCouponDate, DayCount.ActualActualBond, freq);
            }
          case BondType.ITLGovt:
            {
              //For German Government bonds the EOM Rule is always in effect and the daycount conventions are 
              //Actual/Actual(in period) for everything
              Dt normalIssue = Dt.Add(firstCouponDate, freq, -1, true);
              return (double)freq * Dt.Fraction(normalIssue, firstCouponDate, issueDate, firstCouponDate, DayCount.ActualActualBond, freq);
            }
          case BondType.USTBill:
            {
              Dt normalIssue = Dt.Add(firstCouponDate, freq, -1, true);
              return (double)freq * Dt.Fraction(normalIssue, firstCouponDate, issueDate, firstCouponDate, DayCount.ActualActualBond, freq);
            }
          default:
            {
              //find the fractional period
              Dt normalIssue = Dt.Add(firstCouponDate, freq, -1, eomRule);
              return (double)freq * Dt.Fraction(normalIssue, firstCouponDate, issueDate, firstCouponDate, dayCount, freq);
            }
        }

      }
    }

    /// <summary>
    /// Calculates the factor applied to the last coupon period for discounting and accrued interest.
    /// </summary>
    public static double LastCouponFactor(Dt lastCouponDate, Dt maturityDate, Frequency freq, bool eomRule, DayCount dayCount, BondType bondType)
    {
      if (lastCouponDate.IsEmpty() || (lastCouponDate == maturityDate))
        return 1.0;
      else
      {
        switch (bondType)
        {
          case BondType.DEMGovt:
            {
              //For German Government bonds the EOM Rule is always in effect and the daycount conventions are 
              //Actual/Actual(in period) for everything
              Dt normalMaturity = Dt.Add(lastCouponDate, freq, 1, true);
              return (double)freq * Dt.Fraction(lastCouponDate, normalMaturity, lastCouponDate, maturityDate, DayCount.ActualActualBond, freq);
            }
          case BondType.USTBill:
            {
              //For US T Bills the EOM Rule is always in effect and the daycount conventions are 
              //Actual/Actual(in period) for everything
              Dt normalMaturity = Dt.Add(lastCouponDate, freq, 1, true);
              return (double)freq * Dt.Fraction(lastCouponDate, normalMaturity, lastCouponDate, maturityDate, DayCount.ActualActualBond, freq);
            }
          default:
            {
              //find the fractional period
              Dt normalMaturity = Dt.Add(lastCouponDate, freq, 1, eomRule);
              return (double)freq * Dt.Fraction(lastCouponDate, normalMaturity, lastCouponDate, maturityDate, dayCount, freq);
            }
        }
      }
    }

    #endregion Methods

    #region Utils


    /// <summary>
    /// calculates the Accrual days for th ebond
    /// </summary>
    /// <param name="settle">Settle </param>
    /// <param name="bondType">Bond type</param>
    /// <param name="effective">Efffective date </param>
    /// <param name="firstCoupon">First coupon date </param>
    /// <param name="lastCoupon">Last coupon date</param>
    /// <param name="previousCycle">previous cycle date</param>
    /// <param name="nextCycle">Next cycle date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="dayCount">daycount</param>
    /// <param name="periodAdjustment">period adjustment</param>
    /// <param name="bdc">Bd convention</param>
    /// <param name="cal">calendar</param>
    /// <param name="eomRule">eom rule</param>
    /// <param name="cumDiv">cum div flag</param>
    /// <param name="ignoreExDivDate">ignore ex div date in cashflows flag</param>
    /// <returns>the number of accrual days</returns>
    [Obsolete]
    public static int AccrualDays(Dt settle, BondType bondType, Dt effective, Dt firstCoupon,Dt lastCoupon,Dt previousCycle,
      Dt nextCycle, Dt maturity, DayCount dayCount,bool periodAdjustment, BDConvention bdc, Calendar cal,bool eomRule,bool cumDiv, bool ignoreExDivDate)
    {
      int accrualDays;
      int result;

      Dt nextCoupon = nextCycle;
      Dt prevCouponDate = previousCycle;

      //adjust prior coupon date to handle roll
      //check for FRN and roll then
      if (periodAdjustment)
      {
        prevCouponDate = Dt.Roll(prevCouponDate, bdc, cal);
        nextCoupon = Dt.Roll(nextCoupon, bdc, cal);
        firstCoupon = Dt.Roll(firstCoupon, bdc, cal);
        lastCoupon = Dt.Roll(lastCoupon, bdc, cal);
      }

      switch (bondType)
      {
        case BondType.JGB:
          {
            Dt referenceIssueDate = new Dt(1, 3, 2001);
            dayCount = DayCount.Actual365Fixed;
            result = Dt.Cmp(prevCouponDate, effective);
            if (result < 0)
            {
              // This is the first coupon period, and is short
              accrualDays = Dt.Diff(effective, settle, dayCount);
            }
            else if ((result > 0) && (Dt.Cmp(nextCoupon, firstCoupon) == 0))
            {
              if (Dt.Cmp(settle, prevCouponDate) < 0)
                // Settlement date is in the short part of the long stub
                accrualDays = Dt.Diff(effective, settle, dayCount);
              else
                // Settlement date is in the regular part of the long stub
                accrualDays = Dt.Diff(effective, prevCouponDate, dayCount) +
                              Dt.Diff(prevCouponDate, settle, dayCount);
            }
            else
            {
              if (Dt.Cmp(lastCoupon, settle) < 0 && Dt.Cmp(settle, maturity) < 0)
              {
                accrualDays = Dt.Diff(lastCoupon, settle, dayCount);

              }
              else
              {
                // This is a regular coupon period.
                accrualDays = Dt.Diff(prevCouponDate, settle, dayCount);
              }
            }


            // If settlement date is in the first coupon period (unless on effective date),
            // then both the effective date and the settlement date are included in the
            // accrual calculation (Krgin, p. 52).

            if (Dt.Cmp(settle, firstCoupon) < 0 && Dt.Cmp(settle, effective) > 0 && Dt.Cmp(effective, referenceIssueDate) < 0)
              accrualDays++;
            return accrualDays;
          }
        case BondType.DEMGovt:
          {
            //The DayCount is ActualActualBond for DEMGovt bonds 
            dayCount = DayCount.ActualActualBond;
            eomRule = true;
          }
          break;
        case BondType.FRFGovt:
          {
            dayCount = DayCount.ActualActualBond;
            eomRule = true;
          }
          break;
      }



      Dt exDiv = ExDivDate(bondType, nextCoupon);

      result = Dt.Cmp(prevCouponDate, effective);
      if (result < 0)
      {
        // This is the first coupon period, and is short
        if (!cumDiv && !ignoreExDivDate && Dt.Cmp(exDiv, settle) <= 0)
          // Ex-div
          accrualDays = 0 - Dt.Diff(settle, nextCoupon, dayCount);
        else
          // Cum-div
          accrualDays = Dt.Diff(effective, settle, dayCount);
      }
      else if ((result > 0) && (nextCoupon == firstCoupon))
      {
        if (settle < prevCouponDate)
        {
          // Settlement date is in the short part of the long stub
          if (!cumDiv && !ignoreExDivDate && exDiv <= settle)
            // Ex-div
            accrualDays = 0 -
                          (Dt.Diff(settle, prevCouponDate, dayCount) +
                           Dt.SignedDiff(prevCouponDate, nextCoupon, dayCount));
          else
            // Cum-div
            accrualDays = Dt.Diff(effective, settle, dayCount);
        }
        else
        {
          // Settlement date is in the regular part of the long stub
          if (!cumDiv && !ignoreExDivDate && exDiv <= settle)
            // Ex-div
            accrualDays = 0 - Dt.Diff(settle, nextCoupon, dayCount);
          else
            // Cum-div
            accrualDays = Dt.Diff(effective, prevCouponDate, dayCount) + Dt.Diff(prevCouponDate, settle, dayCount);
        }
      }
      else
      {
        //This means that the settle date is in the long last coupon period 
        if (Dt.Cmp(lastCoupon, settle) < 0 && Dt.Cmp(settle, maturity) < 0)
        {
          accrualDays = Dt.Diff(lastCoupon, settle, dayCount);

        }
        else
        {
          // This is a a regular coupon period.
          if (!cumDiv && !ignoreExDivDate && exDiv <= settle)
            // Ex-div
            accrualDays = 0 - Dt.Diff(settle, nextCoupon, dayCount);
          else
            // Cum-div
            accrualDays = Dt.Diff(prevCouponDate, settle, dayCount);
        }
      }


      return accrualDays;
    }

    /// <summary>
    ///   Get ex dividend date by bond type, this is replaced by ExDivDate(Bond bond, Dt nextCouponDate)
    /// </summary>
    ///
    /// <returns>Ex dividend date</returns>
    ///
    [Obsolete]
    public static Dt ExDivDate(BondType bondType, Dt nextCouponDate)
    {
      switch (bondType)
      {
        case BondType.AUSGovt:
          return Dt.Add(nextCouponDate, -7, TimeUnit.Days);
        case BondType.UKGilt:
          return Dt.AddDays(nextCouponDate, -6, Calendar.LNB);
        default:
          return nextCouponDate;
      }
    }
    /// <summary>
    ///   Get ex dividend date
    /// </summary>
    ///
    /// <returns>Ex dividend date</returns>
    ///
    public static Dt ExDivDate(Bond bond, Dt nextCouponDate)
    {
      if (bond.BondExDivRule != null)
      {
        return ExDivDate(bond.BondExDivRule, bond.Calendar, nextCouponDate);
      }
      else
      {
        switch (bond.BondType)
        {
          case BondType.AUSGovt:
            return Dt.Add(nextCouponDate, -7, TimeUnit.Days);
          case BondType.UKGilt:
            return Dt.AddDays(nextCouponDate, -6, Calendar.LNB);
          default:
            return nextCouponDate;
        }
      }
    }

    public static Dt ExDivDate(ExDivRule rule, Calendar calendar, Dt nextCouponDate)
    {
      if (rule.ExDivDays == 0)
        return nextCouponDate;
      else if (rule.ExDivBusinessFlag)
        return Dt.AddDays(nextCouponDate, -rule.ExDivDays, calendar);
      else
        return Dt.Add(nextCouponDate, -rule.ExDivDays, TimeUnit.Days);
    }

    /// <summary>
   /// Calculates the Accrued Interest for the bond
   /// </summary>
    /// <remarks>
    /// <inheritdoc cref="BaseEntity.Toolkit.Pricers.BondPricer.AccruedInterest()" />
    /// </remarks>
    /// <seealso cref="BondModel">Standard market bond calculations</seealso>
   /// <param name="settle">Settle date </param>
   /// <param name="bondType">Bond type</param>
   /// <param name="effective">Effective date for the bodn</param>
   /// <param name="firstCoupon">First coupon date for the bond</param>
   /// <param name="lastCoupon">Last coupn date for the bodn</param>
   /// <param name="previousCycle">Previous cycle date for the bond</param>
   /// <param name="nextCycle">Next cycle date for the bond</param>
   /// <param name="maturity">Maturity date </param>
   /// <param name="couponRate">Coupon rate </param>
   /// <param name="dayCount">Bond daycount</param>
   /// <param name="freq">Bond Frequency</param>
   /// <param name="eomRule">Eom Rule</param>
   /// <param name="periodAdjustment">Period Adjustment</param>
   /// <param name="bdc">BdConvention for the bond</param>
   /// <param name="cal">Calendar</param>
   /// <param name="ignoreExDiv">Ignore Ex div date in the cashflows flag </param>
   /// <returns>the accrued interest</returns>
   [Obsolete]
   public static double AccruedInterest(Dt settle, BondType bondType, Dt effective, Dt firstCoupon, Dt lastCoupon,Dt previousCycle,
      Dt nextCycle, Dt maturity, double couponRate, DayCount dayCount, Frequency freq,
      bool eomRule,bool periodAdjustment, BDConvention bdc, Calendar cal,bool ignoreExDiv)
  {
    double accrualFactor;
    int result;
    int roundingDigits = -1;

    Dt nextCoupon = nextCycle;
    if (periodAdjustment)
    {
      nextCoupon = Dt.Roll(nextCoupon, bdc, cal);
      firstCoupon = Dt.Roll(firstCoupon, bdc, cal);
      lastCoupon = Dt.Roll(lastCoupon, bdc, cal);
    }
    Dt exDiv = ExDivDate(bondType, nextCoupon);

    // if no next coupon , i.e settle >= maturity
    if (nextCoupon.IsEmpty())
      return 0.0;


    // Some special cases
    switch (bondType)
    {
      case BondType.JGB:
        {
          int accrualDays = AccrualDays(settle, bondType, effective, firstCoupon, lastCoupon,previousCycle,
            nextCycle, maturity, dayCount, periodAdjustment, bdc, cal,
            eomRule, false, ignoreExDiv);
          double accruedInterest;
          if ((accrualDays == 183 || accrualDays == 184) && Dt.Cmp(settle, firstCoupon) < 0)
            accruedInterest = couponRate / 2;
          else
          {
            accrualFactor = accrualDays / 182.5;
            accruedInterest = couponRate / 2 * accrualFactor;
          }
          return RoundingUtil.Round(accruedInterest, 9);

        }
      case BondType.CADGovt:
        {
          dayCount = DayCount.Actual365Fixed;
          int accrualDays = AccrualDays(settle, bondType, effective, firstCoupon, lastCoupon,previousCycle,
            nextCycle, maturity, dayCount, periodAdjustment, bdc, cal,
            eomRule, false, ignoreExDiv);
          if (accrualDays == 183)
            return couponRate * (1 - 1 / 182.5) / 2;
        }
        break;
      case BondType.DEMGovt:
        {
          //German bonds follow the ActualActualBond convention for Accrued Interest and 
          //have EOM rule 
          dayCount = DayCount.ActualActualBond;
          eomRule = true;
          roundingDigits = -1;
        }
        break;
      case BondType.FRFGovt:
        {

          //French bonds follow the ActualActualBond convention for Accrued Interest and 
          //have EOM rule 
          dayCount = DayCount.ActualActualBond;
          eomRule = true;
          roundingDigits = 5;

        }
        break;
      case BondType.ITLGovt:
        {
          dayCount = DayCount.ActualActualBond;
          eomRule = true;
          roundingDigits = 7;
        }
        break;

      case BondType.AUSGovt:
        {
          roundingDigits = 5;
        }
        break;
    }

    // All other bonds
    //

    //find the previous coupon date
    Dt prevCouponDate = previousCycle;

    //check for FRN and roll then
    if (periodAdjustment)
      prevCouponDate = Dt.Roll(prevCouponDate, bdc, cal);

    result = Dt.Cmp(prevCouponDate, effective);

    //Checks if we are in short first coupon period 
    if (result < 0)
    {
      // This is the first coupon period, and is short
      if ((Dt.Cmp(exDiv, settle) <= 0) && !ignoreExDiv)
        accrualFactor = 0 - Dt.Fraction(prevCouponDate, nextCoupon, settle, nextCoupon, dayCount, freq);
      else
        accrualFactor = Dt.Fraction(prevCouponDate, nextCoupon, effective, settle, dayCount, freq);
    }
    else if ((result > 0) && (Dt.Cmp(nextCoupon, firstCoupon) == 0))
    {
      //If the nextCoupon == firstCoupon , then we have the settlement date in the first coupon period 
      //    Accrual is calculated as the sum of the accruals for the "regular"
      // and the "short" parts of the long stub.
      // ref: [1] (bond. 79)
      //The PrevPrevCoupon date is the Pseudo Issue date 
      Dt prevPrevCouponDate = Dt.Subtract(prevCouponDate, freq, eomRule);

      //This means that the settle is inbetween Dated date and NI date 
      if (Dt.Cmp(settle, prevCouponDate) < 0)
      {
        // Settlement date is in the short part of the long stub
        if ((Dt.Cmp(exDiv, settle) <= 0) && !ignoreExDiv)
          accrualFactor = 0 -
                          Dt.Fraction(prevPrevCouponDate, prevCouponDate, settle, prevCouponDate, dayCount, freq) -
                          Dt.Fraction(prevCouponDate, nextCoupon, prevCouponDate, nextCoupon, dayCount, freq);
        else
          accrualFactor = Dt.Fraction(prevPrevCouponDate, prevCouponDate, effective, settle, dayCount, freq);
      }
      else
      {
        // Settlement date is in the regular part of the long stub
        //This means , the settlement date is in between NI date and First Coupon date 
        if ((Dt.Cmp(exDiv, settle) <= 0) && !ignoreExDiv)
          accrualFactor = 0 - Dt.Fraction(prevCouponDate, nextCoupon, settle, nextCoupon, dayCount, freq);
        else
          accrualFactor = Dt.Fraction(prevPrevCouponDate, prevCouponDate, effective, prevCouponDate, dayCount, freq) +
            Dt.Fraction(prevCouponDate, nextCoupon, prevCouponDate, settle, dayCount, freq);
      }
    }
    else
    {
      //This means that the settle date is in the last coupon period 
      if ((Dt.Cmp(lastCoupon, settle) < 0) && (Dt.Cmp(settle, maturity) < 0))
      {
        Dt normalMaturity = Dt.Add(lastCoupon, freq, eomRule);
        accrualFactor = Dt.Fraction(lastCoupon, normalMaturity, lastCoupon, settle, dayCount, freq);
      }
      else
      {
        // This is a a regular coupon period.
        if ((exDiv <= settle) && !ignoreExDiv)
          accrualFactor = 0 - Dt.Fraction(prevCouponDate, nextCoupon, settle, nextCoupon, dayCount, freq);
        else
          accrualFactor = Dt.Fraction(prevCouponDate, nextCoupon, prevCouponDate, settle, dayCount, freq);
      }
    }

    double ai = couponRate * accrualFactor;
    return RoundingUtil.Round(ai, roundingDigits);
  }

    /// <summary>
    ///   Get next coupon amount
    /// </summary>
    ///
    /// <param name="settle">Settlement date</param>
    /// <param name="bondType">Bond type</param>
    /// <param name="exDiv">Ex-div date</param>
    /// <param name="effective">Bond effective date</param>
    /// <param name="firstCoupon">Bond first coupon date</param>
    /// <param name="prevCouponDate">The Previous Coupon Date </param>
    /// <param name="nextCoupon">Next bond coupon date after settlement</param>
    /// <param name="couponRate">Bond coupon rate</param>
    /// <param name="freq">Bond coupon payment frequency</param>
    /// <param name="dayCount">Bond coupon daycount</param>
    /// <param name="eomRule">Bond EOM convention</param>
    ///
    /// <returns>next coupon amount</returns>
    ///
    private static double NextCouponAmount(
      Dt settle, BondType bondType, Dt exDiv,
      Dt effective, Dt firstCoupon, Dt prevCouponDate,Dt nextCoupon,
      double couponRate, DayCount dayCount, Frequency freq, bool eomRule
      )
    {
      double nextCouponFactor;

      switch (bondType)
      {
        case BondType.DEMGovt:
          {
            dayCount = DayCount.ActualActualBond;
            eomRule = true;
          }
          break;
      }

      if (exDiv <= settle)
        return 0.0;

      int result = Dt.Cmp(prevCouponDate, effective);
      if (result < 0)
      {
        // This is the first coupon period, and is short
        nextCouponFactor = Dt.Fraction(prevCouponDate, nextCoupon, effective, nextCoupon, dayCount, freq);
      }
      else if ((result > 0) && (nextCoupon == firstCoupon))
      {
        // This is the first coupon period and is long
        Dt prevPrevCouponDate = Dt.Subtract(prevCouponDate, freq, eomRule);
        nextCouponFactor =
          Dt.Fraction(prevPrevCouponDate, prevCouponDate, effective, prevCouponDate, dayCount, freq) +
          Dt.Fraction(prevCouponDate, nextCoupon, prevCouponDate, nextCoupon, dayCount, freq);
      }
      else
      {
        // This is a a regular coupon period.
        nextCouponFactor = 1.0 / (double)freq;
      }

      return couponRate * nextCouponFactor;
    }

    /// <summary>
    /// Helper method used to calculate the Tsm (Actual number of days between settle and maturity)
    /// for JGB bonds  
    /// </summary>
    /// <param name="settle">settledate </param>
    /// <param name="maturity">maturity date </param>
    /// <returns>Tsm </returns>
    public static int JGBSettleToMaturity(Dt settle, Dt maturity)
    {
      int numDays = Dt.Diff(settle, maturity);
      int settleYear = settle.Year;
      int maturityYear = maturity.Year;
      int numLeapYears = 0;
      int startYear = settleYear + 1;
      int endYear = maturityYear - 1;
      if (Dt.IsLeapYear(settleYear))
      {
        Dt settleLeapDate = new Dt(29, 2, settleYear);
        if (Dt.Cmp(settle, settleLeapDate) < 0)
          startYear = settleYear;
      }

      if (Dt.IsLeapYear(maturityYear))
      {
        Dt maturityLeapdate = new Dt(29, 2, maturityYear);
        if (Dt.Cmp(maturity, maturityLeapdate) > 0)
          endYear = maturityYear;
      }

      for (int i = startYear; i <= endYear; i++)
      {
        numLeapYears = (Dt.IsLeapYear(i)) ? (numLeapYears + 1) : numLeapYears;
      }

      if (numDays <= 365)
      {
        return numDays;
      }
      else
      {
        return numDays - numLeapYears;
      }
    }

    private static int USTbillDays(Dt issueDate)
    {
      int year = issueDate.Year;
      Dt startDate = new Dt(28, 2, year);
      Dt endDate = new Dt(1, 3, year + 1);
      if (Dt.IsLeapYear(year))
      {
        if (Dt.Cmp(issueDate, startDate) > 0 && Dt.Cmp(issueDate, endDate) < 0)
        {
          return 366;
        }
        else
        {
          return 365;
        }
      }
      else
      {
        return 365;
      }


    }


    /// <summary>
    ///   True if product is active
    /// </summary>
    ///
    /// <remarks>
    ///   <para>A product is active if the pricing AsOf date is before the product maturity date.</para>
    /// </remarks>
    ///
    /// <returns>true if product is active</returns>
    /// 
    static private bool IsActive(Dt asOf, Dt effective, Dt maturity)
    {
      return asOf <= maturity;
    }
    
    private static void ValidateQuotedYield(double quotedYield)
    {
      if (quotedYield <= -1.0 || quotedYield > 4.0)
        throw new ArgumentOutOfRangeException("quotedYield", quotedYield, "Quoted yield must be between -100pct and 400pct");

    }

    #endregion Utils

    #region obsolete methods

    /// <summary>
    ///   Get discount factor for next coupon payment
    /// </summary>
    /// <param name="settle">Settlement date</param>
    /// <param name="bondType">Bond type</param>
    /// <param name="effective">Bond effective date</param>
    /// <param name="firstCoupon">Bond first coupon date</param>
    /// <param name="lastCoupon">Bond next to last coupon date</param>
    /// <param name="prevCouponDate">The previous coupon date </param>
    /// <param name="nextCoupon">Next bond coupon date after settlement</param>
    /// <param name="freq">Bond coupon payment frequency</param>
    /// <param name="maturity">Bond maturity date</param>
    /// <param name="dayCount">Bond coupon daycount</param>
    /// <param name="eomRule">Bond EOM convention</param>
    /// <returns>discount factor for next coupon payment</returns>
    [Obsolete]
    public static double DiscountFactor(
      Dt settle, BondType bondType, Dt effective, Dt firstCoupon, Dt lastCoupon, Dt prevCouponDate,
      Dt nextCoupon, Dt maturity, DayCount dayCount, Frequency freq, bool eomRule)
    {
      double discountFactor;
      int result;

      switch (bondType)
      {
        case BondType.DEMGovt:
          {
            //German Bonds have Actual/Actual Daycount convention 
            //and eomrule is set to true
            dayCount = DayCount.ActualActualBond;
            eomRule = true;
          }
          break;
        case BondType.FRFGovt:
          {
            dayCount = DayCount.ActualActualBond;
            eomRule = true;
          }
          break;
      }

      result = Dt.Diff(nextCoupon, settle);
      if (result == 0)
        // Accrual on a coupon payment date is always 0.0
        return 0.0;

      result = Dt.Cmp(prevCouponDate, effective);
      if (result < 0)
      {
        // This is the first coupon period, and is short
        discountFactor = Dt.Fraction(prevCouponDate, nextCoupon, settle, nextCoupon, dayCount, freq);
      }
      else if ((result > 0) && (Dt.Cmp(nextCoupon, firstCoupon) == 0))
      {
        // This is the first coupon period, and is long
        Dt prevPrevCouponDate = Dt.Subtract(prevCouponDate, freq, eomRule);
        if (Dt.Cmp(settle, prevCouponDate) < 0)
          // Settlement date is in the short part of the long stub
          discountFactor =
            Dt.Fraction(prevPrevCouponDate, prevCouponDate, settle, prevCouponDate, dayCount, freq) +
            //Dt.Fraction(prevCouponDate, nextCoupon, prevPrevCouponDate, nextCoupon, dayCount);
            Dt.Fraction(prevCouponDate, nextCoupon, prevCouponDate, nextCoupon, dayCount, freq);
        else
          // Settlement date is in the regular part of the long stub
          discountFactor = Dt.Fraction(prevCouponDate, nextCoupon, settle, nextCoupon, dayCount, freq);
      }
      else
      {
        if ((Dt.Cmp(lastCoupon, settle) < 0) && (Dt.Cmp(settle, maturity) < 0))
        {
          //This means that we are in the last coupon period 
          Dt normalMaturity = Dt.Add(lastCoupon, freq, eomRule);
          Dt pseudoMaturity = Dt.Add(normalMaturity, freq, eomRule);
          if ((Dt.Cmp(settle, normalMaturity) < 0) && (Dt.Cmp(normalMaturity, maturity) < 0))
          {
            //This means that the last coupon period is long and we are inbetween the last coupon date and normal maturity date
            discountFactor = Dt.Fraction(lastCoupon, normalMaturity, settle, normalMaturity, dayCount, freq) +
                             Dt.Fraction(normalMaturity, pseudoMaturity, normalMaturity, maturity, dayCount, freq);
          }
          else
          {
            if ((Dt.Cmp(maturity, normalMaturity) < 0) && (Dt.Cmp(settle, normalMaturity) < 0))
              //This means that the last coupon period is short and we are inbetween the last coupon date and maturity date 
              discountFactor = Dt.Fraction(lastCoupon, maturity, settle, maturity, dayCount, freq);
            else
              discountFactor = Dt.Fraction(normalMaturity, pseudoMaturity, settle, maturity, dayCount, freq);
          }
        }
        else
          // This is a a regular coupon period.
          discountFactor = Dt.Fraction(prevCouponDate, nextCoupon, settle, nextCoupon, dayCount, freq);
      }

      // Convert from fractional year to fractional period
      return discountFactor * (int)freq;
    }


    /// <summary>
    /// obsolete method to be retired 
    /// </summary>
    /// <param name="settle"></param>
    /// <param name="bondType"></param>
    /// <param name="effective"></param>
    /// <param name="maturity"></param>
    /// <param name="principal"></param>
    /// <param name="firstCouponDate"></param>
    /// <param name="lastCouponDate"></param>
    /// <param name="nextCouponDate"></param>
    /// <param name="previousCycle"></param>
    /// <param name="N"></param>
    /// <param name="couponRate"></param>
    /// <param name="freq"></param>
    /// <param name="dayCount"></param>
    /// <param name="eomRule"></param>
    /// <param name="periodAdjustment"></param>
    /// <param name="bdc"></param>
    /// <param name="cal"></param>
    /// <param name="quotedYield"></param>
    /// <param name="accruedInterest"></param>
    /// <param name="recoveryRate"></param>
    /// <param name="ccy"></param>
    /// <param name="ignoreExDivDate"></param>
    /// <returns></returns>
    [Obsolete]
    public static double PriceFromYield(
      Dt settle, BondType bondType, Dt effective, Dt maturity, double principal,
      Dt firstCouponDate, Dt lastCouponDate, Dt nextCouponDate, Dt previousCycle, int N,
      double couponRate, Frequency freq, DayCount dayCount, bool eomRule, bool periodAdjustment,
      BDConvention bdc, Calendar cal, double quotedYield, double accruedInterest, double recoveryRate,
      Currency ccy, bool ignoreExDivDate)
    {
      // Validate
      if (!IsActive(settle, effective, maturity))
        return 0.0;
      if (settle == maturity)
        return 1.0;

      ValidateQuotedYield(quotedYield);

      double price;
      Dt exDivDate = ExDivDate(bondType, nextCouponDate);
      Dt prevCouponDate = previousCycle;
      if (periodAdjustment)
        prevCouponDate = Dt.Roll(prevCouponDate, bdc, cal);
      // First and last coupon periods
      double firstPer = ((settle < exDivDate) || ignoreExDivDate)
                      ? FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule,
                                          dayCount, bondType)
                      : 0.0;

      double lastPer = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dayCount, bondType);
      switch (bondType)
      {
        case BondType.USGovt:
          {
            if (N == 1)
              price = BondModel.MoneyMarketPrice(accruedInterest,
                DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, dayCount/*DayCount.ActualActualBond*/, freq, eomRule),
                NextCouponAmount(settle, bondType, exDivDate, effective, firstCouponDate, prevCouponDate, nextCouponDate, couponRate, dayCount/*DayCount.ActualActualBond*/, freq, eomRule),
                freq, principal, quotedYield);
            else
              price = BondModel.YtmToPrice(accruedInterest,
                DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, dayCount/*DayCount.ActualActualBond*/, freq, eomRule),
                lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedYield);
          }
          break;
        case BondType.JGB:
          {
            int Tsm = JGBSettleToMaturity(settle, maturity);
            price = BondModelJGB.YtmToPrice(principal, couponRate, Tsm, quotedYield);
            price = RoundingUtil.Round(price, 5);
          }
          break;
        case BondType.USTBill:
          {
            int Tsm = Dt.Diff(settle, maturity, DayCount.Actual365Fixed);
            int Ndl = USTbillDays(effective);
            if (Tsm <= 182)
              price = BondModelTM.MoneyMarketPrice(quotedYield, principal, Tsm, 360, Ndl);
            else
            {
              if (Ndl == 266 && Tsm == 183)
                price = BondModelTM.YtmToPriceLeapYear(quotedYield);
              else
                price = BondModelTM.YtmToPrice(quotedYield, principal, Tsm, Ndl);
            }
          }
          break;

        case BondType.ITLGovt:
          {
            double AI = AccruedInterest(settle, bondType, effective, firstCouponDate, lastCouponDate, previousCycle,
                                        nextCouponDate, maturity, couponRate, dayCount, freq, true, periodAdjustment,
                                        bdc, cal, ignoreExDivDate);
            /*double AI = AccruedInterest(settle, bondType, effective, firstCouponDate, lastCouponDate,
                                                     nextCouponDate, maturity,
                                                     couponRate, dayCount, freq, true, periodAdjustment, bdc, cal,ignoreExDivDate);*/
            price = BondModelITL.YtmToPrice(AI, principal, quotedYield, settle, effective, firstCouponDate,
                                            lastCouponDate, maturity, ccy, couponRate, dayCount, freq, bdc, cal,
                                            recoveryRate);
          }
          break;
        case BondType.CADGovt:
          {
            // First and last coupon periods
            if (N == 1)
              price = BondModel.MoneyMarketPrice(
                AccruedInterest(settle, bondType, effective, firstCouponDate, lastCouponDate, previousCycle, nextCouponDate, maturity, couponRate, DayCount.Actual365Fixed, freq, eomRule, periodAdjustment, bdc, cal, ignoreExDivDate),
                DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, DayCount.Actual365Fixed, freq, eomRule),
                NextCouponAmount(settle, bondType, exDivDate, effective, firstCouponDate, prevCouponDate, nextCouponDate, couponRate, DayCount.Actual365Fixed, freq, eomRule),
                freq, principal, quotedYield);
            else
              price = BondModel.YtmToPrice(
                AccruedInterest(settle, bondType, effective, firstCouponDate, lastCouponDate, previousCycle, nextCouponDate, maturity, couponRate, DayCount.ActualActualBond, freq, eomRule, periodAdjustment, bdc, cal, ignoreExDivDate),
                DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, DayCount.ActualActualBond, freq, eomRule),
                lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedYield);
          }
          break;
        case BondType.DEMGovt:
          {
            //For German Bonds the following rule applies,
            //1) Simple Interest in the Last Coupon period 
            //2) Discount Factor is calculated using ActualActualBond Daycount
            //3) End of Month Rule Applies 
            // First and last coupon periods
            if (N == 1)
              price = BondModel.MoneyMarketPrice(
                AccruedInterest(settle, bondType, effective, firstCouponDate, lastCouponDate, previousCycle, nextCouponDate, maturity, couponRate, dayCount, freq, eomRule, periodAdjustment, bdc, cal, ignoreExDivDate),
                DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, dayCount, freq, eomRule),
                NextCouponAmount(settle, bondType, exDivDate, effective, firstCouponDate, prevCouponDate, nextCouponDate, couponRate, DayCount.ActualActualBond, freq, true),
                freq, principal, quotedYield);
            else
              price = BondModel.YtmToPrice(
                AccruedInterest(settle, bondType, effective, firstCouponDate, lastCouponDate, previousCycle, nextCouponDate, maturity, couponRate, DayCount.ActualActualBond, freq, true, periodAdjustment, bdc, cal, ignoreExDivDate),
                DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, DayCount.ActualActualBond, freq, true),
                lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedYield);
          }
          break;
        case BondType.FRFGovt:
          {
            //For french bonds the following rule applies 
            //1) Compound interest in the last coupon period 
            //2) Discount factor is calculated using the ActualActualBond daycount 
            //3) End of month rule applies 
            price = BondModelFRF.YtmToPrice(
                AccruedInterest(settle, bondType, effective, firstCouponDate, lastCouponDate, previousCycle, nextCouponDate, maturity,
                                couponRate, DayCount.ActualActualBond, freq, true, periodAdjustment, bdc, cal, ignoreExDivDate),
                DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity,
                               DayCount.ActualActualBond, freq, true),
                lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedYield);
            price = RoundingUtil.Round(price, 5);
          }
          break;
        default:
          {
            price = BondModel.YtmToPrice(
              AccruedInterest(settle, bondType, effective, firstCouponDate, lastCouponDate, previousCycle, nextCouponDate, maturity, couponRate, dayCount, freq, eomRule, periodAdjustment, bdc, cal, ignoreExDivDate),
              DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, dayCount, freq, eomRule),
              lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedYield);
          }
          break;
      }
      return price;
    }

    /// <summary>
    /// old convexity method to be retired soon 
    /// </summary>
    /// <param name="settle"></param>
    /// <param name="bondType"></param>
    /// <param name="effective"></param>
    /// <param name="maturity"></param>
    /// <param name="principal"></param>
    /// <param name="firstCoupon"></param>
    /// <param name="lastCoupon"></param>
    /// <param name="previousCycleDate"></param>
    /// <param name="nextCouponDate"></param>
    /// <param name="N"></param>
    /// <param name="couponRate"></param>
    /// <param name="freq"></param>
    /// <param name="dayCount"></param>
    /// <param name="eomRule"></param>
    /// <param name="periodAdjustment"></param>
    /// <param name="bdc"></param>
    /// <param name="cal"></param>
    /// <param name="flatPrice"></param>
    /// <param name="quotedYield"></param>
    /// <param name="accruedInterest"></param>
    /// <param name="ccy"></param>
    /// <param name="recoveryRate"></param>
    /// <param name="ignoreExDivDate"></param>
    /// <returns></returns>
    [Obsolete]
    public static double Convexity(
      Dt settle, BondType bondType, Dt effective, Dt maturity, double principal,
      Dt firstCoupon, Dt lastCoupon, Dt previousCycleDate, Dt nextCouponDate, int N,
      double couponRate, Frequency freq, DayCount dayCount, bool eomRule, bool periodAdjustment,
      BDConvention bdc, Calendar cal, double flatPrice, double quotedYield, double accruedInterest, Currency ccy, double recoveryRate, bool ignoreExDivDate
      )
    {
      // Validate
      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;
      if (flatPrice <= 0.0 || flatPrice > MaxPrice)
        throw new ArgumentOutOfRangeException("flatPrice", flatPrice, PriceRangeMsg);
      ValidateQuotedYield(quotedYield);

      Dt exDivDate = ExDivDate(bondType, nextCouponDate);
      Dt prevCouponDate = previousCycleDate;
      if (periodAdjustment)
        prevCouponDate = Dt.Roll(prevCouponDate, bdc, cal);

      switch (bondType)
      {
        case BondType.CADGovt:
          {
            if (N == 1)
              dayCount = DayCount.Actual365Fixed;
            else
              dayCount = DayCount.ActualActualBond;
            break;
          }
        case BondType.DEMGovt:
          {
            dayCount = DayCount.ActualActualBond;
            eomRule = true;
            double fcf = FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);
            return BondModel.Convexity(accruedInterest,
                                        DiscountFactor(settle, bondType, effective, firstCoupon, lastCoupon, prevCouponDate, nextCouponDate, maturity, dayCount, freq, eomRule),
                                        lcf, fcf, lcf, couponRate, N, freq, principal, flatPrice, quotedYield);
          }
        case BondType.FRFGovt:
          {
            dayCount = DayCount.ActualActualBond;
            eomRule = true;
            double fcf = FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);
            return BondModelFRF.Convexity(accruedInterest,
                                        DiscountFactor(settle, bondType, effective, firstCoupon, lastCoupon, prevCouponDate, nextCouponDate, maturity, dayCount, freq, eomRule),
                                        lcf, fcf, lcf, couponRate, N, freq, principal, flatPrice, quotedYield);
          }
        case BondType.ITLGovt:
          {
            dayCount = DayCount.ActualActualBond;
            eomRule = true;
            return BondModelITL.Convexity(accruedInterest, principal, quotedYield, flatPrice, settle, effective,
                                          firstCoupon, lastCoupon, maturity, ccy, couponRate, dayCount, freq,
                                          bdc, cal, recoveryRate);
          }
        case BondType.JGB:
          {
            int Tsm = JGBSettleToMaturity(settle, maturity);
            return BondModelJGB.Convexity(accruedInterest, principal, couponRate, Tsm, quotedYield);
          }
        case BondType.USTBill:
          {
            int tsm = Dt.Diff(settle, maturity, DayCount.Actual365Fixed);
            int Ndl = USTbillDays(effective);
            if (tsm <= 182)
              return BondModelTM.MoneyMarketConvexity(principal, quotedYield, tsm, Ndl, 360);
            else
              return BondModelTM.Convexity(quotedYield, principal, tsm, Ndl);
          }
        default:
          break;
      }

      // First and last coupon periods
      double firstPer = ((settle < exDivDate) || ignoreExDivDate)
                          ? FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule,
                                              dayCount, bondType)
                          : 0.0;
      double lastPer = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);
      return BondModel.Convexity(accruedInterest,
        DiscountFactor(settle, bondType, effective, firstCoupon, lastCoupon, prevCouponDate, nextCouponDate, maturity, dayCount, freq, eomRule),
        lastPer, firstPer, lastPer, couponRate, N, freq, principal, flatPrice, quotedYield);
    }

    /// <summary>
    /// old dpdy calculation to be retired 
    /// </summary>
    /// <param name="settle"></param>
    /// <param name="bondType"></param>
    /// <param name="effective"></param>
    /// <param name="maturity"></param>
    /// <param name="principal"></param>
    /// <param name="firstCoupon"></param>
    /// <param name="lastCoupon"></param>
    /// <param name="previousCycle"></param>
    /// <param name="nextCouponDate"></param>
    /// <param name="N"></param>
    /// <param name="couponRate"></param>
    /// <param name="freq"></param>
    /// <param name="dayCount"></param>
    /// <param name="eomRule"></param>
    /// <param name="periodAdjustment"></param>
    /// <param name="bdc"></param>
    /// <param name="cal"></param>
    /// <param name="quotedYield"></param>
    /// <param name="accruedInterest"></param>
    /// <param name="ccy"></param>
    /// <param name="recoveryRate"></param>
    /// <param name="ignoreExDivDate"></param>
    /// <returns></returns>
    [Obsolete]
    public static double dPdY(Dt settle, BondType bondType, Dt effective, Dt maturity, double principal,
      Dt firstCoupon, Dt lastCoupon, Dt previousCycle, Dt nextCouponDate, int N,
      double couponRate, Frequency freq, DayCount dayCount, bool eomRule, bool periodAdjustment,
      BDConvention bdc, Calendar cal, double quotedYield, double accruedInterest, Currency ccy, double recoveryRate, bool ignoreExDivDate
      )
    {
      // Validate
      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;
      ValidateQuotedYield(quotedYield);

      Dt prevCouponDate = previousCycle;
      if (periodAdjustment)
        prevCouponDate = Dt.Roll(previousCycle, bdc, cal);

      Dt exDivDate = ExDivDate(bondType, nextCouponDate);

      switch (bondType)
      {
        case BondType.CADGovt:
          {
            if (N == 1)
              dayCount = DayCount.Actual365Fixed;
            else
              dayCount = DayCount.ActualActualBond;
            break;
          }
        case BondType.DEMGovt:
          {
            double fcf = FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);
            return BondModel.dPdY(accruedInterest,
                                    DiscountFactor(settle, bondType, effective, firstCoupon, lastCoupon, prevCouponDate, nextCouponDate, maturity, dayCount,
                                                   freq, eomRule),
                                    lcf, fcf, lcf, couponRate, N, freq, principal, quotedYield);
          }
        case BondType.FRFGovt:
          {
            double fcf = FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);
            return BondModelFRF.dPdY(accruedInterest,
                                    DiscountFactor(settle, bondType, effective, firstCoupon, lastCoupon, prevCouponDate, nextCouponDate, maturity, dayCount,
                                                   freq, eomRule),
                                    lcf, fcf, lcf, couponRate, N, freq, principal, quotedYield);
          }
        case BondType.JGB:
          {
            int Tsm = JGBSettleToMaturity(settle, maturity);
            return BondModelJGB.dPdY(principal, couponRate, Tsm, quotedYield);
          }
        case BondType.USTBill:
          {
            int tsm = Dt.Diff(settle, maturity, DayCount.Actual365Fixed);
            int Ndl = USTbillDays(effective);
            if (tsm <= 182)
              return BondModelTM.MoneyMarketdPdY(quotedYield, tsm, Ndl);
            else
              return BondModelTM.dPdY(quotedYield, principal, tsm, Ndl);
          }
        case BondType.ITLGovt:
          {
            return BondModelITL.dPdY(accruedInterest, principal, quotedYield, settle, effective, firstCoupon, lastCoupon,
                                     maturity, ccy, couponRate, dayCount, freq, bdc, cal, recoveryRate);
          }
        default:
          break;
      }

      //specified 1st coupon and in 1st coupon period
      double firstPer = ((settle < exDivDate) || ignoreExDivDate)
                    ? FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule,
                                        dayCount, bondType)
                    : 0.0;
      double lastPer = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);
      return BondModel.dPdY(accruedInterest,
        DiscountFactor(settle, bondType, effective, firstCoupon, lastCoupon, prevCouponDate, nextCouponDate, maturity, dayCount, freq, eomRule),
        lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedYield);
    }

    /// <summary>
    /// old dp2dy2 calculation to be retired
    /// </summary>
    /// <param name="settle"></param>
    /// <param name="bondType"></param>
    /// <param name="effective"></param>
    /// <param name="maturity"></param>
    /// <param name="principal"></param>
    /// <param name="firstCoupon"></param>
    /// <param name="lastCoupon"></param>
    /// <param name="previousCycleDate"></param>
    /// <param name="nextCouponDate"></param>
    /// <param name="N"></param>
    /// <param name="couponRate"></param>
    /// <param name="freq"></param>
    /// <param name="dayCount"></param>
    /// <param name="eomRule"></param>
    /// <param name="periodAdjustment"></param>
    /// <param name="bdc"></param>
    /// <param name="cal"></param>
    /// <param name="flatPrice"></param>
    /// <param name="quotedYield"></param>
    /// <param name="accruedInterest"></param>
    /// <param name="ccy"></param>
    /// <param name="recoveryRate"></param>
    /// <param name="ignoreExDivDate"></param>
    /// <returns></returns>
    [Obsolete]
    public static double dP2dY2(
     Dt settle, BondType bondType, Dt effective, Dt maturity, double principal,
     Dt firstCoupon, Dt lastCoupon, Dt previousCycleDate, Dt nextCouponDate, int N,
     double couponRate, Frequency freq, DayCount dayCount, bool eomRule, bool periodAdjustment,
     BDConvention bdc, Calendar cal, double flatPrice, double quotedYield, double accruedInterest, Currency ccy, double recoveryRate, bool ignoreExDivDate
     )
    {
      // Validate
      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;
      if (flatPrice <= 0.0 || flatPrice > MaxPrice)
        throw new ArgumentOutOfRangeException("flatPrice", flatPrice, PriceRangeMsg);
      ValidateQuotedYield(quotedYield);
      Dt prevCouponDate = previousCycleDate;
      if (periodAdjustment)
        prevCouponDate = Dt.Roll(previousCycleDate, bdc, cal);
      switch (bondType)
      {
        case BondType.CADGovt:
          {
            if (N == 1)
              dayCount = DayCount.Actual365Fixed;
            else
              dayCount = DayCount.ActualActualBond;
            break;
          }
        case BondType.DEMGovt:
          {
            double fcf = FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);
            return BondModel.dP2dY2(accruedInterest,
                                      DiscountFactor(settle, bondType, effective, firstCoupon, lastCoupon, prevCouponDate, nextCouponDate, maturity, dayCount,
                                                     freq, eomRule),
                                      lcf, fcf, lcf, couponRate, N, freq, principal, flatPrice, quotedYield);
          }
        case BondType.FRFGovt:
          {
            double fcf = FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);
            return BondModelFRF.dP2dY2(accruedInterest,
                                      DiscountFactor(settle, bondType, effective, firstCoupon, lastCoupon, prevCouponDate, nextCouponDate, maturity, dayCount,
                                                     freq, eomRule),
                                      lcf, fcf, lcf, couponRate, N, freq, principal, flatPrice, quotedYield);
          }
        case BondType.JGB:
          {
            int Tsm = JGBSettleToMaturity(settle, maturity);
            return BondModelJGB.dP2dY2(principal, couponRate, Tsm, quotedYield);
          }
        case BondType.USTBill:
          {
            int tsm = Dt.Diff(settle, maturity, DayCount.Actual365Fixed);
            int Ndl = USTbillDays(effective);
            if (tsm <= 182)
              return BondModelTM.MoneyMarketdP2dY2(quotedYield, tsm, Ndl);
            else
              return BondModelTM.dP2dY2(quotedYield, principal, tsm, Ndl);
          }
        case BondType.ITLGovt:
          {
            return BondModelITL.dP2dY2(accruedInterest, principal, quotedYield, flatPrice, settle, effective,
                                       firstCoupon, lastCoupon, maturity, ccy, couponRate, dayCount, freq, bdc, cal,
                                       recoveryRate);
          }
        default:
          break;
      }
      Dt exDivDate = ExDivDate(bondType, nextCouponDate);
      //specified 1st coupon and in 1st coupon period
      double firstPer = ((settle < exDivDate) || ignoreExDivDate)
                    ? FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule,
                                        dayCount, bondType)
                    : 0.0;
      double lastPer = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);

      return BondModel.dP2dY2(accruedInterest,
        DiscountFactor(settle, bondType, effective, firstCoupon, lastCoupon, prevCouponDate, nextCouponDate, maturity, dayCount, freq, eomRule),
        lastPer, firstPer, lastPer, couponRate, N, freq, principal, flatPrice, quotedYield);
    }

    /// <summary>
    /// Old pv01 calculation to be retured 
    /// </summary>
    /// <param name="settle"></param>
    /// <param name="bondType"></param>
    /// <param name="effective"></param>
    /// <param name="maturity"></param>
    /// <param name="principal"></param>
    /// <param name="firstCouponDate"></param>
    /// <param name="lastCouponDate"></param>
    /// <param name="previousCycle"></param>
    /// <param name="nextCouponDate"></param>
    /// <param name="N"></param>
    /// <param name="couponRate"></param>
    /// <param name="freq"></param>
    /// <param name="dayCount"></param>
    /// <param name="eomRule"></param>
    /// <param name="periodAdjustment"></param>
    /// <param name="bdc"></param>
    /// <param name="cal"></param>
    /// <param name="quotedYield"></param>
    /// <param name="ccy"></param>
    /// <param name="recoveryRate"></param>
    /// <param name="ignoreExDivDate"></param>
    /// <returns></returns>
    [Obsolete]
    public static double PV01(
      Dt settle, BondType bondType, Dt effective, Dt maturity, double principal,
      Dt firstCouponDate, Dt lastCouponDate, Dt previousCycle, Dt nextCouponDate, int N,
      double couponRate, Frequency freq, DayCount dayCount, bool eomRule, bool periodAdjustment,
      BDConvention bdc, Calendar cal, double quotedYield, Currency ccy, double recoveryRate, bool ignoreExDivDate)
    {
      // Validate
      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;
      ValidateQuotedYield(quotedYield);

      Dt prevCouponDate = previousCycle;
      if (periodAdjustment)
        prevCouponDate = Dt.Roll(prevCouponDate, bdc, cal);

      Dt exDivDate = ExDivDate(bondType, nextCouponDate);

      switch (bondType)
      {
        case BondType.CADGovt:
          {
            if (N == 1)
              dayCount = DayCount.Actual365Fixed;
            else
              dayCount = DayCount.ActualActualBond;
            break;
          }
        case BondType.DEMGovt:
          {
            // First and last coupon periods
            double fcf = FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dayCount, bondType);
            return BondModel.Pv01(
                AccruedInterest(settle, bondType, effective, firstCouponDate, lastCouponDate, previousCycle, nextCouponDate, maturity, couponRate, dayCount, freq,
                                eomRule, periodAdjustment, bdc, cal, ignoreExDivDate),
                DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, dayCount, freq, eomRule),
                lcf, fcf, lcf, couponRate, N, freq, principal, quotedYield);
          }
        case BondType.JGB:
          {
            int Tsm = JGBSettleToMaturity(settle, maturity);
            return BondModelJGB.Pv01(principal, couponRate, Tsm, quotedYield);
          }
        case BondType.USTBill:
          {
            int tsm = Dt.Diff(settle, maturity, DayCount.Actual365Fixed);
            int Ndl = USTbillDays(effective);
            if (tsm <= 182)
              return BondModelTM.MoneyMarketPv01(quotedYield, tsm, Ndl);
            else
              return BondModelTM.Pv01(quotedYield, principal, tsm, Ndl);
          }
        case BondType.FRFGovt:
          {
            // First and last coupon periods
            double fcf = FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dayCount, bondType);
            return BondModelFRF.Pv01(
                AccruedInterest(settle, bondType, effective, firstCouponDate, lastCouponDate, previousCycle, nextCouponDate, maturity, couponRate, dayCount, freq,
                                eomRule, periodAdjustment, bdc, cal, ignoreExDivDate),
                DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, dayCount, freq, eomRule),
                lcf, fcf, lcf, couponRate, N, freq, principal, quotedYield);
          }
        case BondType.ITLGovt:
          {
            double accruedInterest = AccruedInterest(settle, bondType, effective, firstCouponDate, lastCouponDate, previousCycle,
                                                     nextCouponDate, maturity, couponRate, dayCount, freq,
                                                     eomRule, periodAdjustment, bdc, cal, ignoreExDivDate);
            return BondModelITL.Pv01(accruedInterest, principal, quotedYield, settle, effective, firstCouponDate,
                                     lastCouponDate, maturity, ccy, couponRate, dayCount, freq, bdc, cal, recoveryRate);
          }
        default:
          break;
      }

      // First and last coupon periods
      double firstPer = ((settle < exDivDate) || ignoreExDivDate)
                      ? FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule,
                                          dayCount, bondType)
                      : 0.0;
      double lastPer = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dayCount, bondType);

      return BondModel.Pv01(
        AccruedInterest(settle, bondType, effective, firstCouponDate, lastCouponDate, previousCycle, nextCouponDate, maturity, couponRate, dayCount, freq, eomRule, periodAdjustment, bdc, cal, ignoreExDivDate),
        DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, dayCount, freq, eomRule),
        lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedYield);
    }

    /// <summary>
    /// Old Ytm calculation to be retired soon  
    /// </summary>
    /// <param name="settle"></param>
    /// <param name="bondType"></param>
    /// <param name="effective"></param>
    /// <param name="maturity"></param>
    /// <param name="principal"></param>
    /// <param name="firstCouponDate"></param>
    /// <param name="lastCouponDate"></param>
    /// <param name="previousCycleDate"></param>
    /// <param name="nextCouponDate"></param>
    /// <param name="N"></param>
    /// <param name="couponRate"></param>
    /// <param name="freq"></param>
    /// <param name="dayCount"></param>
    /// <param name="eomRule"></param>
    /// <param name="periodAdjustment"></param>
    /// <param name="bdc"></param>
    /// <param name="cal"></param>
    /// <param name="quotedPrice"></param>
    /// <param name="accruedInterest"></param>
    /// <param name="ccy"></param>
    /// <param name="recoveryRate"></param>
    /// <param name="ignoreExDivDate"></param>
    /// <returns></returns>
    [Obsolete]
    public static double YieldToMaturity(
      Dt settle, BondType bondType, Dt effective, Dt maturity, double principal,
      Dt firstCouponDate, Dt lastCouponDate, Dt previousCycleDate, Dt nextCouponDate, int N, double couponRate,
      Frequency freq, DayCount dayCount, bool eomRule, bool periodAdjustment,
      BDConvention bdc, Calendar cal, double quotedPrice, double accruedInterest, Currency ccy, double recoveryRate, bool ignoreExDivDate)
    {
      // Validate
      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;
      if (principal <= 0.0 || principal > MaxPrice)
        throw new ArgumentOutOfRangeException("principal", principal, WorkoutRangeMsg);
      if (quotedPrice <= 0.0 || quotedPrice > MaxPrice)
        throw new ArgumentOutOfRangeException("quotedPrice", quotedPrice, PriceRangeMsg);

      double yield;
      Dt prevCouponDate = previousCycleDate;
      if (periodAdjustment)
        prevCouponDate = Dt.Roll(previousCycleDate, bdc, cal);

      Dt exDivDate = ExDivDate(bondType, nextCouponDate);

      //TODO: move the first and last period calculations to outside the switch-case block 
      switch (bondType)
      {
        case BondType.USGovt:
          {
            // First and last coupon periods
            double firstPer = FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule, dayCount, bondType/*DayCount.ActualActualBond*/);
            double lastPer = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dayCount, bondType/*DayCount.ActualActualBond*/);

            if (N == 1)
              yield = BondModel.MoneyMarketYield(accruedInterest,
                DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, dayCount/*DayCount.ActualActualBond*/, freq, eomRule),
                NextCouponAmount(settle, bondType, exDivDate, effective, firstCouponDate, prevCouponDate, nextCouponDate, couponRate, dayCount/*DayCount.ActualActualBond*/, freq, eomRule),
                freq, principal, quotedPrice);
            else
              yield = BondModel.PriceToYtm(accruedInterest,
                DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, dayCount/*DayCount.ActualActualBond*/, freq, eomRule),
                lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedPrice);

            // Round to 8 dp's
            yield = Math.Floor(yield * Math.Pow(10, 8) + 0.5) / Math.Pow(10, 8);
          }
          break;
        case BondType.USTBill:
          {
            int tsm = Dt.Diff(settle, maturity, DayCount.Actual365Fixed);
            int Ndl = USTbillDays(effective);
            if (tsm <= 182)
            {
              yield = BondModelTM.MoneyMarketYield(quotedPrice, principal, tsm, 360, Ndl);
            }
            else
            {
              if (tsm == 183 && Ndl == 366)
                yield = BondModelTM.PriceToYtmLeapYear(quotedPrice);
              else
                yield = BondModelTM.PriceToYtm(quotedPrice, principal, tsm, Ndl);
            }
          }
          break;
        case BondType.UKGilt:
          {
            // First and last coupon periods
            double firstPer = ((settle < exDivDate) || ignoreExDivDate)
                                ? FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule,
                                                    DayCount.ActualActualBond, bondType)
                                : 0;

            double lastPer = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, DayCount.ActualActualBond, bondType);

            yield = BondModel.PriceToYtm(accruedInterest,
              DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, DayCount.ActualActualBond, freq, eomRule),
              lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedPrice);

            // Round to 8 dp's
            yield = Math.Floor(yield * Math.Pow(10, 8) + 0.5) / Math.Pow(10, 8);
          }
          break;
        case BondType.JGB:
          {
            int Tsm = JGBSettleToMaturity(settle, maturity);
            return BondModelJGB.PriceToYtm(principal, couponRate, Tsm, quotedPrice);
          }
        case BondType.CADGovt:
          {
            DayCount dc = (N == 1 ? DayCount.Actual365Fixed : DayCount.ActualActualBond);

            //specified 1st coupon and in 1st coupon period
            double firstPer = FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule, dc, bondType);
            double lastPer = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dc, bondType);

            if (N == 1)
              yield = BondModel.MoneyMarketYield(accruedInterest,
                DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, dc, freq, eomRule),
                NextCouponAmount(settle, bondType, exDivDate, effective, firstCouponDate, prevCouponDate, nextCouponDate, couponRate, dc, freq, eomRule),
                freq, principal, quotedPrice);
            else
              yield = BondModel.PriceToYtm(accruedInterest,
                DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, dc, freq, eomRule),
                lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedPrice);

            yield = Math.Floor(yield * Math.Pow(10, 8) + 0.5) / Math.Pow(10, 8);
          }
          break;
        case BondType.DEMGovt:
          {
            double fcf = FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dayCount, bondType);
            if (N == 1)
              yield = BondModel.MoneyMarketYield(accruedInterest,
                DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, dayCount, freq, eomRule),
                NextCouponAmount(settle, bondType, exDivDate, effective, firstCouponDate, prevCouponDate, nextCouponDate, couponRate, dayCount, freq, eomRule),
                freq, principal, quotedPrice);
            else
              yield = BondModel.PriceToYtm(accruedInterest,
                DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, dayCount, freq, eomRule),
                lcf, fcf, lcf, couponRate, N, freq, principal, quotedPrice);
          }
          break;
        case BondType.FRFGovt:
          {
            double fcf = FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dayCount, bondType);
            yield = BondModelFRF.PriceToYtm(accruedInterest,
                DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, dayCount, freq, eomRule),
                lcf, fcf, lcf, couponRate, N, freq, principal, quotedPrice);
          }
          break;
        case BondType.ITLGovt:
          {
            yield = BondModelITL.PriceToYtm(accruedInterest, principal, quotedPrice, settle, effective, firstCouponDate,
                                            lastCouponDate, maturity, ccy, couponRate, dayCount, freq, bdc, cal,
                                            recoveryRate);
            //Convert semi-annual yield to effective yield 
            yield = Math.Pow(1 + (yield / 2), 2) - 1.0;

          }
          break;
        default:
          {
            // First and last coupon periods

            double firstPer = ((settle < exDivDate) || ignoreExDivDate)
                              ? FirstCouponFactor(settle, effective, firstCouponDate, freq, eomRule,
                                                  dayCount, bondType)
                              : 0;
            double lastPer = LastCouponFactor(lastCouponDate, maturity, freq, eomRule, dayCount, bondType);

            yield = BondModel.PriceToYtm(accruedInterest,
              DiscountFactor(settle, bondType, effective, firstCouponDate, lastCouponDate, prevCouponDate, nextCouponDate, maturity, dayCount, freq, eomRule),
              lastPer, firstPer, lastPer, couponRate, N, freq, principal, quotedPrice);

            // Round to 8 dp's
            yield = Math.Floor(yield * Math.Pow(10, 8) + 0.5) / Math.Pow(10, 8);
          }
          break;
      }
      return yield;
    }
    
    /// <summary>
    /// Obsolete method to be retired 
    /// </summary>
    /// <param name="settle"></param>
    /// <param name="bondType"></param>
    /// <param name="effective"></param>
    /// <param name="maturity"></param>
    /// <param name="principal"></param>
    /// <param name="firstCoupon"></param>
    /// <param name="lastCoupon"></param>
    /// <param name="previousCycle"></param>
    /// <param name="nextCouponDate"></param>
    /// <param name="N"></param>
    /// <param name="couponRate"></param>
    /// <param name="freq"></param>
    /// <param name="dayCount"></param>
    /// <param name="eomRule"></param>
    /// <param name="periodAdjustment"></param>
    /// <param name="bdc"></param>
    /// <param name="cal"></param>
    /// <param name="flatPrice"></param>
    /// <param name="quotedYield"></param>
    /// <param name="accruedInterest"></param>
    /// <param name="ccy"></param>
    /// <param name="recoveryRate"></param>
    /// <param name="ignoreExDivDate"></param>
    /// <returns></returns>
    [Obsolete]
    public static double Duration(
      Dt settle, BondType bondType, Dt effective, Dt maturity, double principal,
      Dt firstCoupon, Dt lastCoupon, Dt previousCycle, Dt nextCouponDate, int N,
      double couponRate, Frequency freq, DayCount dayCount, bool eomRule, bool periodAdjustment,
      BDConvention bdc, Calendar cal, double flatPrice, double quotedYield, double accruedInterest, Currency ccy, double recoveryRate, bool ignoreExDivDate)
    {
      // Validate
      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;

      if (flatPrice <= 0.0 || flatPrice > MaxPrice)
        throw new ArgumentOutOfRangeException("flatPrice", flatPrice, PriceRangeMsg);
      ValidateQuotedYield(quotedYield);

      Dt prevCoupon = previousCycle;
      if (periodAdjustment)
        prevCoupon = Dt.Roll(prevCoupon, bdc, cal);

      switch (bondType)
      {
        case BondType.CADGovt:
          {
            if (N == 1)
              dayCount = DayCount.Actual365Fixed;
            else
              dayCount = DayCount.ActualActualBond;
            break;
          }
        case BondType.DEMGovt:
          {
            dayCount = DayCount.ActualActualBond;
            eomRule = true;
            double fcf = FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);
            return BondModel.Duration(accruedInterest,
                                        DiscountFactor(settle, bondType, effective, firstCoupon, lastCoupon, prevCoupon, nextCouponDate, maturity,
                                                       dayCount, freq, eomRule),
                                        lcf, fcf, lcf, couponRate, N, freq, principal, flatPrice, quotedYield);
          }
        case BondType.FRFGovt:
          {
            dayCount = DayCount.ActualActualBond;
            eomRule = true;
            double fcf = FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule, dayCount, bondType);
            double lcf = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);
            return BondModelFRF.Duration(accruedInterest,
                                        DiscountFactor(settle, bondType, effective, firstCoupon, lastCoupon, prevCoupon, nextCouponDate, maturity,
                                                       dayCount, freq, eomRule),
                                        lcf, fcf, lcf, couponRate, N, freq, principal, flatPrice, quotedYield);
          }
        case BondType.JGB:
          {
            int Tsm = JGBSettleToMaturity(settle, maturity);
            double AI = AccruedInterest(settle, bondType, effective, firstCoupon, lastCoupon, previousCycle, nextCouponDate, maturity,
                                        couponRate, dayCount, freq, eomRule, periodAdjustment, bdc, cal, ignoreExDivDate);
            return BondModelJGB.Duration(AI, principal, couponRate, Tsm, quotedYield, N);
          }
        case BondType.USTBill:
          {
            int tsm = Dt.Diff(settle, maturity, DayCount.Actual365Fixed);
            int Ndl = USTbillDays(effective);
            if (tsm <= 182)
              return BondModelTM.MoneyMarketDuration(quotedYield, tsm, Ndl);
            else
              return BondModelTM.Duration(quotedYield, tsm, Ndl);
          }
        case BondType.ITLGovt:
          {
            double AI = AccruedInterest(settle, bondType, effective, firstCoupon, lastCoupon, previousCycle, nextCouponDate, maturity,
                                        couponRate, dayCount, freq, eomRule, periodAdjustment, bdc, cal, ignoreExDivDate);
            return BondModelITL.Duration(AI, principal, quotedYield, flatPrice, settle, effective, firstCoupon,
                                         lastCoupon, maturity, ccy, couponRate, dayCount, freq, bdc, cal, recoveryRate);
          }
        default:
          break;
      }


      Dt exDivDate = ExDivDate(bondType, nextCouponDate);
      // First and last coupon periods
      double firstPer = ((settle < exDivDate) || ignoreExDivDate)
                      ? FirstCouponFactor(settle, effective, firstCoupon, freq, eomRule,
                                          dayCount, bondType)
                      : 0.0;

      double lastPer = LastCouponFactor(lastCoupon, maturity, freq, eomRule, dayCount, bondType);

      return BondModel.Duration(accruedInterest,
        DiscountFactor(settle, bondType, effective, firstCoupon, lastCoupon, prevCoupon, nextCouponDate, maturity, dayCount, freq, eomRule),
        lastPer, firstPer, lastPer, couponRate, N, freq, principal, flatPrice, quotedYield);
    }

    /// <summary>
    /// old mod durationmethod to be retired
    /// </summary>
    /// <param name="settle"></param>
    /// <param name="bondType"></param>
    /// <param name="effective"></param>
    /// <param name="maturity"></param>
    /// <param name="principal"></param>
    /// <param name="firstCoupon"></param>
    /// <param name="lastCoupon"></param>
    /// <param name="previousCycle"></param>
    /// <param name="nextCouponDate"></param>
    /// <param name="N"></param>
    /// <param name="couponRate"></param>
    /// <param name="freq"></param>
    /// <param name="dayCount"></param>
    /// <param name="eomRule"></param>
    /// <param name="periodAdjustment"></param>
    /// <param name="bdc"></param>
    /// <param name="cal"></param>
    /// <param name="flatPrice"></param>
    /// <param name="quotedYield"></param>
    /// <param name="accruedInterest"></param>
    /// <param name="ccy"></param>
    /// <param name="recoveryRate"></param>
    /// <param name="ignoreExDivDate"></param>
    /// <returns></returns>
    [Obsolete]
    public static double ModDuration(
      Dt settle, BondType bondType, Dt effective, Dt maturity, double principal,
      Dt firstCoupon, Dt lastCoupon, Dt previousCycle, Dt nextCouponDate, int N, double couponRate, Frequency freq, DayCount dayCount, bool eomRule, bool periodAdjustment,
      BDConvention bdc, Calendar cal, double flatPrice, double quotedYield, double accruedInterest, Currency ccy, double recoveryRate, bool ignoreExDivDate
      )
    {
      double duration = Duration(settle, bondType, effective, maturity, principal,
                                 firstCoupon, lastCoupon, previousCycle, nextCouponDate, N,
                                 couponRate, freq, dayCount, eomRule, periodAdjustment,
                                 bdc, cal, flatPrice, quotedYield, accruedInterest, ccy, recoveryRate, ignoreExDivDate);
      switch (bondType)
      {
        case BondType.DEMGovt:
            return BondModel.ModDuration(duration, quotedYield, freq);
        case BondType.FRFGovt:
            return BondModelFRF.ModDuration(duration, quotedYield, freq);
        case BondType.JGB:
            return BondModelJGB.ModDuration(duration, quotedYield);
        case BondType.USTBill:
          {
            int tsm = Dt.Diff(settle, maturity, DayCount.Actual365Fixed);
            int Ndl = USTbillDays(effective);
            if (tsm <= 182)
              return BondModelTM.MoneyMarketModifiedDuration(principal, quotedYield, tsm, Ndl, 360);
            else
              return BondModelTM.ModifiedDuration(principal, quotedYield, tsm, Ndl);
          }
        default:
          return BondModel.ModDuration(duration, quotedYield, freq);
      }
    }

    /// <exclude />
    [Obsolete]
    public static double YieldToPut(
      Dt settle, BondType bondType, Dt effective, Dt maturity, double principal,
      Dt firstCoupon, Dt lastCoupon, Dt previousCycleDate, Dt nextCouponDate, int N, double couponRate,
      Frequency freq, DayCount dayCount, bool eomRule, bool periodAdjustment,
      BDConvention bdc, Calendar cal, double quotedPrice, List<PutPeriod> putSchedule, double accruedInterest, Currency ccy, double recoveryRate, bool ignoreExDiv)
    {
      // Validate
      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;
      if (principal <= 0.0 || principal > MaxPrice)
        throw new ArgumentOutOfRangeException("principal", principal, WorkoutRangeMsg);
      if (quotedPrice <= 0.0 || quotedPrice > MaxPrice)
        throw new ArgumentOutOfRangeException("quotedPrice", quotedPrice, PriceRangeMsg);

      if (putSchedule != null)
      {
        foreach (PutPeriod c in putSchedule)
        {
          if (c.StartDate > settle)
            return YieldToMaturity(settle, bondType, effective, c.StartDate, c.PutPrice,
              firstCoupon, lastCoupon, previousCycleDate, nextCouponDate, N, couponRate,
              freq, dayCount, eomRule, periodAdjustment, bdc, cal, quotedPrice, accruedInterest, ccy, recoveryRate, ignoreExDiv);
        }
      }
      return YieldToMaturity(settle, bondType, effective, maturity, principal,
        firstCoupon, lastCoupon, previousCycleDate, nextCouponDate, N, couponRate,
        freq, dayCount, eomRule, periodAdjustment, bdc, cal, quotedPrice, accruedInterest, ccy, recoveryRate, ignoreExDiv);
    }

    /// <summary>
    /// </summary>
    [Obsolete]
    public static double YieldToCall(
      Dt settle, BondType bondType, Dt effective, Dt maturity, double principal,
      Dt firstCoupon, Dt lastCoupon, Dt previousCycleDate, Dt nextCouponDate, int N, double couponRate,
      Frequency freq, DayCount dayCount, bool eomRule, bool periodAdjustment,
      BDConvention bdc, Calendar cal, double quotedPrice, List<CallPeriod> callSchedule, double accruedInterest, Currency ccy, double recoveryRate, bool ignoreExDiv)
    {
      // Validate
      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;
      if (principal <= 0.0 || principal > MaxPrice)
        throw new ArgumentOutOfRangeException("principal", principal, WorkoutRangeMsg);
      if (quotedPrice <= 0.0 || quotedPrice > MaxPrice)
        throw new ArgumentOutOfRangeException("quotedPrice", quotedPrice, PriceRangeMsg);

      if (callSchedule != null)
      {
        foreach (CallPeriod c in callSchedule)
        {
          if (c.StartDate > settle)
            return YieldToMaturity(settle, bondType, effective, c.StartDate, c.CallPrice,
              firstCoupon, lastCoupon, previousCycleDate, nextCouponDate, N, couponRate,
              freq, dayCount, eomRule, periodAdjustment, bdc, cal, quotedPrice, accruedInterest, ccy, recoveryRate, ignoreExDiv);
        }
      }
      return YieldToMaturity(settle, bondType, effective, maturity, principal,
        firstCoupon, lastCoupon, previousCycleDate, nextCouponDate, N, couponRate,
        freq, dayCount, eomRule, periodAdjustment, bdc, cal, quotedPrice, accruedInterest, ccy, recoveryRate, ignoreExDiv);
    }

    /// <exclude />
    [Obsolete]
    public static double YieldToWorst(
      Dt settle, BondType bondType, Dt effective, Dt maturity, double principal,
      Dt firstCoupon, Dt lastCoupon, Dt previousCycleDate, Dt nextCouponDate, int N, double couponRate,
      Frequency freq, DayCount dayCount, bool eomRule, bool periodAdjustment,
      BDConvention bdc, Calendar cal, double quotedPrice, double accruedInterest,
      List<CallPeriod> callSchedule, List<PutPeriod> putSchedule, Currency ccy, double recoveryRate, bool ignoreExDiv)
    {
      // Validate
      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;
      if (principal <= 0.0 || principal > MaxPrice)
        throw new ArgumentOutOfRangeException("principal", principal, WorkoutRangeMsg);
      if (quotedPrice <= 0.0 || quotedPrice > MaxPrice)
        throw new ArgumentOutOfRangeException("quotedPrice", quotedPrice, PriceRangeMsg);

      double yieldToWorst = YieldToMaturity(settle, bondType, effective, maturity, principal,
        firstCoupon, lastCoupon, previousCycleDate, nextCouponDate, N, couponRate,
        freq, dayCount, eomRule, periodAdjustment, bdc, cal, quotedPrice, accruedInterest, ccy, recoveryRate, ignoreExDiv);

      if (callSchedule != null)
      {
        foreach (CallPeriod c in callSchedule)
        {
          if (Dt.Cmp(c.StartDate, settle) > 0)
          {
            // calculate yield to start of call period
            yieldToWorst = Math.Min(
              yieldToWorst,
              YieldToMaturity(settle, bondType, effective, c.StartDate,
                              c.CallPrice, firstCoupon, lastCoupon, previousCycleDate, nextCouponDate, N, couponRate,
                              freq, dayCount, eomRule, periodAdjustment, bdc, cal, quotedPrice, accruedInterest, ccy, recoveryRate, ignoreExDiv)
              );
            // calculate yield to end of call period
            yieldToWorst = Math.Min(
              yieldToWorst,
              YieldToMaturity(settle, bondType, effective, c.EndDate, c.CallPrice,
                              firstCoupon, lastCoupon, previousCycleDate, nextCouponDate, N, couponRate,
                              freq, dayCount, eomRule, periodAdjustment, bdc, cal, quotedPrice, accruedInterest, ccy, recoveryRate, ignoreExDiv)
              );
          }
        }
      }
      if (putSchedule != null)
      {
        foreach (PutPeriod p in putSchedule)
        {
          if (Dt.Cmp(p.StartDate, settle) > 0)
          {
            // calculate yield to start of put period
            yieldToWorst = Math.Min(
              yieldToWorst,
              YieldToMaturity(settle, bondType, effective, p.StartDate,
                              p.PutPrice, firstCoupon, lastCoupon, previousCycleDate, nextCouponDate, N, couponRate,
                              freq, dayCount, eomRule, periodAdjustment, bdc, cal, quotedPrice, accruedInterest, ccy, recoveryRate, ignoreExDiv)
              );

            // calculate yield to end of put period
            yieldToWorst = Math.Min(
              yieldToWorst,
              YieldToMaturity(settle, bondType, effective, p.EndDate,
                              p.PutPrice, firstCoupon, lastCoupon, previousCycleDate, nextCouponDate, N, couponRate,
                              freq, dayCount, eomRule, periodAdjustment, bdc, cal, quotedPrice, accruedInterest, ccy, recoveryRate, ignoreExDiv)
              );
          }
        }
      }
      return yieldToWorst;
    }
   
    ///
    [Obsolete]
    public static double YieldToWorst(
      Dt settle, BondType bondType, Dt effective, Dt maturity, double principal,
      Dt firstCoupon, Dt lastCoupon, Dt previousCycleDate, Dt nextCouponDate, int N, double couponRate,
      Frequency freq, DayCount dayCount, bool eomRule, bool periodAdjustment,
      BDConvention bdc, Calendar cal, double quotedPrice, double accruedInterest, List<CallPeriod> callSchedule, Currency ccy, double recoveryRate, bool ignoreExDiv
      )
    {
      // Validate
      if (!IsActive(settle, effective, maturity) || (settle == maturity))
        return 0.0;
      if (principal <= 0.0 || principal > MaxPrice)
        throw new ArgumentOutOfRangeException("principal", principal, WorkoutRangeMsg);
      if (quotedPrice <= 0.0 || quotedPrice > MaxPrice)
        throw new ArgumentOutOfRangeException("quotedPrice", quotedPrice, PriceRangeMsg);

      double yieldToWorst = YieldToMaturity(settle, bondType, effective, maturity, principal,
        firstCoupon, lastCoupon, previousCycleDate, nextCouponDate, N, couponRate,
        freq, dayCount, eomRule, periodAdjustment, bdc, cal, quotedPrice, accruedInterest, ccy, recoveryRate, ignoreExDiv);

      if (callSchedule == null) return yieldToWorst;
      foreach (var c in callSchedule)
      {
        if (Dt.Cmp(c.StartDate, settle) <= 0) continue;
        // calculate yield to start of call period
        yieldToWorst = Math.Min(
          yieldToWorst,
          YieldToMaturity(settle, bondType, effective, c.StartDate,
            c.CallPrice, firstCoupon, lastCoupon, previousCycleDate, nextCouponDate, N, couponRate,
            freq, dayCount, eomRule, periodAdjustment, bdc, cal, quotedPrice, accruedInterest, ccy, recoveryRate, ignoreExDiv)
          );
        // calculate yield to end of call period
        yieldToWorst = Math.Min(
          yieldToWorst,
          YieldToMaturity(settle, bondType, effective, c.EndDate, c.CallPrice,
            firstCoupon, lastCoupon, previousCycleDate, nextCouponDate, N, couponRate,
            freq, dayCount, eomRule, periodAdjustment, bdc, cal, quotedPrice, accruedInterest, ccy, recoveryRate, ignoreExDiv)
          );
      }

      return yieldToWorst;
    }
    
    #endregion 

  }
}
