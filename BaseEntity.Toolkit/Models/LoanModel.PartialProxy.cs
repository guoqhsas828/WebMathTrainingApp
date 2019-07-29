using System;
using System.Collections.Generic;
using System.Diagnostics;
using BaseEntity.Toolkit.Pricers;
using log4net;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows.Utils;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Models
{
  #region Config
  /// <exclude />
  [Serializable]
  public class LoanModelConfig
  {
    /// <exclude />
    [Util.Configuration.ToolkitConfig("Use a grid based on coupon frequency.")]
    public readonly bool UseFrequencyForTimeGrid = false;

    /// <exclude />
    [Util.Configuration.ToolkitConfig("Use settle date as pricing date.")]
    public readonly bool UseSettleForPricing = false;

    /// <exclude />
    [Util.Configuration.ToolkitConfig("Include negative option values.")]
    public readonly bool AllowNegativeOptionValue = false;

    /// <exclude />
    [Util.Configuration.ToolkitConfig("Use the old-style cashflow generation for CLO pricing")]
    public readonly bool UseBackwardCompatibleCashflowForCLO = false;
  }
  #endregion Config

  #region LoanFloor
  /// <summary>
  ///   Loan floor parameters
  /// </summary>
  public class LoanPricerParams
  {
    /// <summary>
    /// Constructor
    /// </summary>
    public LoanPricerParams(double? floor, double rateVol, double gridVol)
    {
      _floor = floor;
      _rateVol = rateVol;
      _gridVol = gridVol; 
    }

    /// <summary>
    /// Floor
    /// </summary>
    public double? Floor
    {
      get { return _floor; }
    }

    /// <summary>
    /// Rate volatility
    /// </summary>
    public double RateVolatility
    {
      get { return _rateVol; }
    }

    /// <summary>
    /// Grid volatility
    /// </summary>
    public double GridVolatility
    {
      get { return _gridVol; }
    }

    private readonly double? _floor;
    private readonly double _rateVol;
    private readonly double _gridVol;
  }

  #endregion LoanFloor

  /// <summary>
  ///   Corporate Loan model.
  /// </summary>
  public static partial class LoanModel
  {
    #region Data
    private static ILog Log = LogManager.GetLogger(typeof (LoanModel));
    #endregion

    #region Coupons
    /// <summary>
    /// Calculates the accrued interest as a percentage of outstanding notional.
    /// </summary>
    /// 
    /// <param name="settle">The pricing settlement date</param>
    /// <param name="scheduleParams">Schedule parameters</param>
    /// <param name="interestPeriods">The list of all InterestPeriods</param>
    /// <param name="commFee">The fee paid on the undrawn notional</param>
    /// <param name="dayCount">The day count</param>
    /// <param name="isFloating">Whether the payments pay a spread over a floating index</param>
    /// <param name="cpn">The coupon to pay for fixed rate loans</param>
    /// 
    /// <returns>Accrued (as a percentage)</returns>
    /// 
    public static double Accrued(Dt settle, ScheduleParams scheduleParams, DayCount dayCount, 
      bool isFloating, double cpn, double commFee, IList<InterestPeriod> interestPeriods)
    {
      InterestPeriod ip = InterestPeriodUtil.DefaultInterestPeriod(settle, 
        scheduleParams, DayCount.None, 0.0, 0.0);
      if (ip == null)
      {
        return 0.0;
      }

      Dt periodStart = ip.StartDate;
      Dt nextCouponDate = ip.EndDate;
      double drawn = 0;

      // Handle fixed rate loans
      if (!isFloating)
      {
        double period = Dt.Fraction(periodStart, nextCouponDate, dayCount);
        double fraction = Dt.Diff(periodStart, settle, dayCount) / (double)Dt.Diff(periodStart, 
          nextCouponDate, dayCount);
        return cpn * period * fraction;
      }

      // Declare vars
      double accrued = 0.0;
      List<InterestPeriod> currentInterestPeriods = 
        InterestPeriodUtil.InterestPeriodsForDate(settle, interestPeriods);

      // Validate we have at least 1 interest period
      if (currentInterestPeriods.Count == 0)
        throw new ToolkitException("You must specify at least " +
                                   "1 Interest Period for a Floating Rate Loan!");

      // Calculate accrued from InterestPeriods
      for (int i = 0; i < currentInterestPeriods.Count; ++i)
      {
        InterestPeriod cp = currentInterestPeriods[i];
        double periodFraction = ((double)Dt.Diff(cp.StartDate, settle, cp.DayCount)) 
          / (double)Dt.Diff(cp.StartDate, cp.EndDate, cp.DayCount);
        double period = Dt.Fraction(cp.StartDate, cp.EndDate, 
          cp.StartDate, cp.EndDate, cp.DayCount, cp.Freq);
        accrued += periodFraction * period * cp.AnnualizedCoupon ;
        drawn += cp.PercentageNotional;
      }

      // Handle commitment fee accrued on used portion
      if (1.0 - drawn > 1e-8)
      {
        double period = Dt.Fraction(periodStart, nextCouponDate, dayCount);
        double fraction = Dt.Diff(periodStart, settle, dayCount) 
          / (double)Dt.Diff(periodStart, nextCouponDate, dayCount);
        accrued += period * fraction * (1.0 - drawn) * commFee / drawn; 
      }

      // Done
      return accrued;
    }

    /// <summary>
    /// Calculates the value of the first interest coupon at the given forward date. 
    /// </summary>
    /// 
    /// <param name="settle">The pricing settlement date</param>
    /// <param name="periodEnd">The forward date</param>
    /// <param name="periodStart">The period start date, which ends on the forward date (for fixed coupon loans only)</param>
    /// <param name="discCurve">The interest rate curve for discounting</param>
    /// <param name="interestPeriods">The list of InterestPeriods</param>
    /// <param name="cal">The calendar</param>
    /// <param name="commFee">The fee to pay on undrawn notional</param>
    /// <param name="dayCount">The day count</param>
    /// <param name="isFloating">Whether the loan pays a spread over a floating index</param>
    /// <param name="roll">The business day convention</param>
    /// <param name="cpn">The coupon to pay for a fixed rate loan. Also, for a floating rate loan, pass the current coupon rate, if specified; otherwise, pass 0.</param>
    /// <param name="drawn">The percentage of the total commitment drawn</param>
    /// <param name="isTermLoan">True for term loan</param>
    /// 
    /// <remarks>
    /// <para>Loans often have a contractual feature where they are split into multiple Interest Streams that have different reset dates, 
    /// frequencies, etc. The current Interest Streams are known at the pricing date and should be incorporated into any valuation 
    /// model. This is handled by recognizing the first coupon payment as the value of all of the known Interest Streams on the coupon 
    /// payment date. This does not result in recognizing the cash flows at the exact time they will be realized but does result in their 
    /// inclusion on a "value neutral" basis; meaning that the value calculated is the same whether they were actually recognized on 
    /// the exact cash flow date or not. </para>
    /// <para>So, Interest Streams that pay out after the pricing settlement date but before the 1st coupon date are credited interest and 
    /// pay outs after the 1st coupon date are discounted. The coupons in subsequent periods are projected based on the model, given pricing 
    /// grid, etc. </para>
    /// </remarks>
    /// 
    /// <returns>First Coupon Value</returns>
    /// 
    internal static double FirstCouponValue(Dt settle, Dt periodStart, Dt periodEnd, 
      Calendar cal, BDConvention roll, DayCount dayCount, bool isFloating, 
      DiscountCurve discCurve, double cpn, double commFee, double drawn, 
      IList<InterestPeriod> interestPeriods, bool isTermLoan)
    {
      double interest = 0.0;

      // Handle no interest periods and fixed coupon
      // Also, if a value > 0 is passed, use it and do not try to compute (this is implemented for the Term loans only)
      if (!isFloating || isTermLoan)
        interest = Dt.Fraction(periodStart, periodEnd, dayCount) * cpn;
      else
      {
        List<InterestPeriod> currentStreams = 
          InterestPeriodUtil.InterestPeriodsForDate(settle, interestPeriods);
        for (int i = 0; i < currentStreams.Count; ++i)
        {
          Dt startDate = currentStreams[i].StartDate;
          Dt endDate = currentStreams[i].EndDate;
          Dt pmtDate = Dt.Roll(endDate, roll, cal);
          DayCount dc = currentStreams[i].DayCount;
          double coupon = currentStreams[i].AnnualizedCoupon;
          double period = Dt.Fraction(startDate, endDate, startDate, endDate, dc, Frequency.None);

          if (pmtDate == periodEnd)
            interest += period  * coupon;
          else if (pmtDate > periodEnd) // discount cashflows to date = first projected cashflows date
            interest += period  * coupon * discCurve.DiscountFactor(periodEnd, endDate);
          else // endDate < date: roll cashflow forward to date = first projected cashflows date
            interest += period  * coupon / discCurve.DiscountFactor(endDate, periodEnd);
        }
      }

      // Add in commitment fee
      if (1.0 - drawn > 1e-8)
        interest += Dt.Fraction(periodStart, periodEnd, dayCount) * (1.0 - drawn) * commFee / drawn;

      // Done
      return interest;
    }
    #endregion

    #region State Prices

    /// <summary>
    ///  Calculate the coupon, use and accrual functions
    /// </summary>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="scheduleParams">Schedule parameters</param>
    /// <param name="dayCount">Daycount convention</param>
    /// <param name="isFloating">Boolean floating=true, fixed=false</param>
    /// <param name="curLevel">Current level</param>
    /// <param name="startStateIdx"></param>
    /// <param name="levels"></param>
    /// <param name="pricingGrid"></param>
    /// <param name="commFee"></param>
    /// <param name="usage"></param>
    /// <param name="discountCurve"></param>
    /// <param name="referenceCurve"></param>
    /// <param name="amortizations"></param>
    /// <param name="interestPeriods"></param>
    /// <param name="coupon">Coupon delegate to be calculated</param>
    /// <param name="use">Use delegate to be calculated</param>
    /// <param name="accrual">Accrual delegate to be calculated</param>
    /// <param name="pricingDatesList">Pricing date points</param>
    /// <param name="floorValues">Values of each period's index rate floor</param>
    /// <param name="currentCouponRate">For a floating rate loan, pass the current coupon rate; 
    /// if null passed, the model will compute it based on interestPeriods.</param>
    private static void GetCouponAccrualUse(Dt asOf, Dt settle, 
      ScheduleParams scheduleParams, DayCount dayCount, bool isFloating, 
      string curLevel, int startStateIdx, string[] levels, 
      IDictionary<string, double> pricingGrid, 
      double commFee, double[] usage, DiscountCurve discountCurve, 
      DiscountCurve referenceCurve, 
      IList<Amortization> amortizations, IList<InterestPeriod> interestPeriods, 
      out Func<int, int, double> coupon, 
      out Func<int, int, double> use, out Func<int, int, double> accrual, 
      out List<Dt> pricingDatesList, double[] floorValues, double? currentCouponRate)
    {
      #region Calculated pricing date points

      // Backwards compatibility flag
      bool useFreqForTimeGrid = ToolkitConfigurator.Settings.LoanModel.UseFrequencyForTimeGrid;
      Frequency timeGridFreq = (useFreqForTimeGrid ? scheduleParams.Frequency : Frequency.Monthly);
      // We introduce this for the case when the pricer settle date is ON one of the coupon dates; 
      // in this case, we want the schedule to START after this date, 
      // so we artificially move our settle the day after
      Dt settleForSchedule = settle; 

      // Get a denser schedule and extract the dates as pricing grids
      Schedule schedule = new Schedule(settleForSchedule, scheduleParams.AccrualStartDate,
        scheduleParams.FirstCouponDate,
                                       scheduleParams.NextToLastCouponDate,
                                       scheduleParams.Maturity,
                                       timeGridFreq, scheduleParams.Roll, scheduleParams.Calendar,
                                       scheduleParams.CycleRule, scheduleParams.CashflowFlag);
      if (schedule.Count == 0)
        throw new ToolkitException("Running the loan model after the product maturity.");
      Dt firstpricingDt = schedule.GetPaymentDate(0);
      if (firstpricingDt <= settle)
      {
        // Artificially adjust the settle date to get a schedule that 
        // does not include it as the first coupon date.
        settleForSchedule = settle + 1;
        schedule = new Schedule(settleForSchedule, scheduleParams.AccrualStartDate, 
          scheduleParams.FirstCouponDate,
                                       scheduleParams.NextToLastCouponDate,
                                       scheduleParams.Maturity,
                                       timeGridFreq, scheduleParams.Roll, scheduleParams.Calendar,
                                       scheduleParams.CycleRule, scheduleParams.CashflowFlag);
        if (schedule.Count == 0)
          throw new ToolkitException("Running the loan model after the product maturity.");
      }

      // Get payment dates
      int numOfDates = schedule.Count + 1;
      Dt[] pricingDates = new Dt[numOfDates];
      pricingDates[0] = settle;
      for (int i = 0; i < schedule.Count; i++)
        pricingDates[i + 1] = schedule.GetPaymentDate(i);

      // Calculate Schedule using the real frequency
      schedule = new Schedule(settleForSchedule, scheduleParams.AccrualStartDate, 
        scheduleParams.FirstCouponDate,
                              scheduleParams.NextToLastCouponDate,
                              scheduleParams.Maturity, scheduleParams.Frequency, scheduleParams.Roll,
                              scheduleParams.Calendar, scheduleParams.CycleRule,
                              scheduleParams.CashflowFlag);

      // Get payment dates
      numOfDates = schedule.Count + 1;
      Dt[] paymentDates = new Dt[numOfDates];
      paymentDates[0] = settle;
      for (int i = 0; i < schedule.Count; i++)
        paymentDates[i + 1] = schedule.GetPaymentDate(i);

      // we need to merge these dates to find the pricing grid 
      pricingDatesList = new List<Dt>(pricingDates);
      for (int i = 0; i < paymentDates.Length; i++)
      {
        if (!pricingDatesList.Contains(paymentDates[i]))
        {
          pricingDatesList.Add(paymentDates[i]);
        }
      }
      pricingDatesList.Sort();

      #endregion Calculated pricing date points

      #region Calculate Usage for given frequency

      // Calculate contractual notional balances
      int amortStart = 0;
      double[] notional = new double[paymentDates.Length];
      for (int t = 0; t < paymentDates.Length; t++)
      {
        if (t == 0 || amortizations == null)
          notional[t] = 1.0;
        else
          notional[t] = notional[t - 1] -
                        AmortizationUtil.ScheduledAmortization(paymentDates[t],
                          amortizations, ref amortStart);
      }

      use = (int t, int i) => (i == -1 ? notional[t] : notional[t]*usage[i]);

      #endregion

      #region Calculate Coupons for given frequency

      double[,] coupons = new double[schedule.Count,levels.Length];
      for (int t = 0; t < schedule.Count; t++)
      {
        Dt start = schedule.GetPeriodStart(t);
        Dt end = schedule.GetPeriodEnd(t);
        double frac = schedule.Fraction(t, dayCount);

        for (int i = 0; i < levels.Length; i++)
        {
          double cpn = pricingGrid[levels[i]];
          if (floorValues != null && isFloating)
            cpn += floorValues[t];
          if (!isFloating)
            coupons[t, i] = frac*(use(t, i)*cpn + (1.0 - use(t, i))*commFee);
          else if (t == 0)
          {
            // This is floating only case.
            bool isTermLoan = false;
            double currCpn =  pricingGrid[curLevel];
            if (currentCouponRate.HasValue)
            {
              isTermLoan = true;
              currCpn = currentCouponRate.Value;
            }

            coupons[t, i] = FirstCouponValue(settle, start, end,
              scheduleParams.Calendar, scheduleParams.Roll, dayCount, isFloating, discountCurve, currCpn,
              commFee, usage[startStateIdx], interestPeriods, isTermLoan);
          }
          else
          {
            // double forwRate = ForwardRateCalculator.CalculateForwardRate(referenceCurve, start, 
            // end, dayCount, scheduleParams.Frequency, scheduleParams.CashflowFlag); // This will be done later ...
            double forwRate = referenceCurve.F(start, end);
            // cpn represents the spread in this case.
            coupons[t, i] = frac * (use(t, i) * (forwRate + cpn) + (1.0 - use(t, i)) * commFee); 
            //coupons[t, i] = frac * (use(t, i) * (referenceCurve.F(start, end, dayCount, Frequency.None) + cpn) + (1.0 - use(t, i)) * commFee);
          }
        }
      }

      #endregion Calculate Coupons for given frequency

      #region Calculate new coupons for Monthly frequency

      // Need to add some zeros to coupons for those non-coupon dates
      double[,] newCoupons = new double[pricingDatesList.Count - 1,levels.Length];
      for (int t = 1; t <= newCoupons.GetLength(0); t++)
      {
        for (int j = 1; j < paymentDates.Length; j++)
        {
          if (pricingDatesList[t] == paymentDates[j])
          {
            for (int i = 0; i < levels.Length; i++)
              newCoupons[t - 1, i] = coupons[j - 1, i];
            break;
          }
        }
      }
      coupon = (int t, int i) => newCoupons[t, i];

      #endregion Calculate new coupons for Monthly frequency

      #region Calculate accrual for Monthly frequency

      // Find the accrual of coupon for dates between coupon dates
      double[,] accruals = new double[pricingDatesList.Count - 1,levels.Length];
      double[] currentCpn = new double[levels.Length];
      Dt pStart = paymentDates[0], pEnd = paymentDates[1];
      for (int l = 0; l < levels.Length; l++)
        currentCpn[l] = coupons[0, l];

      for (int i = 0, k = 1; i < accruals.GetLength(0); i++)
      {
        for (int j = 0; j < levels.Length; j++)
        {
          if (newCoupons[i, j] > 0.0)
          {
            accruals[i, j] = 0.0;
            if (j == levels.Length - 1)
            {
              for (int l = 0; l < levels.Length; l++)
                currentCpn[l] = (k == coupons.GetLength(0) ? 0.0 : coupons[k, l]);
              k++;
              pStart = pEnd;
              pEnd = (k == paymentDates.Length ? Dt.Empty : paymentDates[k]);
            }
          }
          else
          {
            if (pEnd == pStart)
              accruals[i, j] = 0.0;
            else
              accruals[i, j] = Dt.Fraction(pStart, pricingDatesList[i + 1], dayCount)
                               / Dt.Fraction(pStart, pEnd, dayCount) * currentCpn[j];
          }
        }
      }
      accrual = (int t, int i) => accruals[t, i];

      #endregion Calculate accrual for Monthly frequency

      #region Calculate use for Monthly frequency

      // Do the new use
      amortStart = 0;
      notional = new double[pricingDatesList.Count];
      for (int t = 0; t < pricingDatesList.Count; t++)
      {
        if (t == 0 || amortizations == null)
          notional[t] = 1.0;
        else
          notional[t] = notional[t - 1] -
                        AmortizationUtil.ScheduledAmortization(pricingDatesList[t], amortizations, ref amortStart);
      }

      use = (int t, int i) => (i == -1 ? notional[t] : notional[t]*usage[i]);

      #endregion

      return;
    }

    /// <summary>
    /// Calculates the forward value of the Loan at each coupon date for each performance level. 
    /// </summary>
    /// 
    /// <remarks>
    /// <para>
    /// Note, that the forward values are the expected value conditional on the Loan performing at each level 
    /// at each payment date. Also, note, no discounting is applied from the date back to the pricing date. 
    /// </para>
    /// </remarks>
    /// 
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Pricing settlement date</param>
    /// <param name="scheduleParams">Schedule parameters</param>
    /// <param name="volatilityCurve">Volatility Curve</param>
    /// <param name="discountCurve">The interest rate curve for discounting</param>
    /// <param name="referenceCurve">The interest rate curve for projecting forward rates</param>
    /// <param name="survCurve">The survival curve</param>
    /// <param name="recoveryCurve">The recovery curve</param>
    /// <apram name="volatilityCurve">Volatility curve</apram>
    /// <param name="prepaymentCurve">The prepayment curve</param>
    /// <param name="refiCost">The cost of refinancing as a rate (200 bps = 0.02)</param>
    /// <param name="includePrepayOption">Whether to include the option to refinance the loan for business or cost reasons</param>
    /// <param name="endDistribution">The probability of being in each performance level at loan maturity</param>
    /// <param name="startStateIdx">The current performance level of the Loan</param>
    /// <param name="usage">The expected usage at each performance level</param>
    /// <param name="levels">The performance levels (best to worst; ie last level is closest to default)</param>
    /// <param name="interestPeriods">The interest periods for the Loan</param>
    /// <param name="fullDrawOnDefault">Whether the Loan is assumed to be fully drawn on a default</param>
    /// <param name="commFee">The fee paid on undrawn notional</param>
    /// <param name="curLevel">The current performance level</param>
    /// <param name="dayCount">The day count</param>
    /// <param name="isFloating">Whether the coupons are paid as a spread over a floating index</param>
    /// <param name="pricingGrid">The coupons or spreads for each performance level</param>
    /// <param name="amortizations">The scheduled amortizations</param>
    /// <param name="floorValues">Values of each period's index rate floor</param>
    /// <param name="currentCouponRate">For a floating rate loan, pass the current coupon rate; 
    /// if null passed, the model will compute it based on interestPeriods.</param>
    /// <returns>Pv.</returns>
    public static double[,] ForwardValues(Dt asOf, Dt settle, ScheduleParams scheduleParams, 
      DayCount dayCount, bool isFloating,
      string curLevel, int startStateIdx, string[] levels,
      IDictionary<string, double> pricingGrid, double commFee, double[] usage, 
      double[] endDistribution,
      DiscountCurve discountCurve, DiscountCurve referenceCurve, SurvivalCurve survCurve,
      RecoveryCurve recoveryCurve, VolatilityCurve volatilityCurve, bool fullDrawOnDefault, 
      bool includePrepayOption, SurvivalCurve prepaymentCurve,
      double refiCost, IList<Amortization> amortizations, IList<InterestPeriod> interestPeriods, 
      double[] floorValues, double? currentCouponRate)
    {
      Func<int, int, double> coupon;
      Func<int, int, double> use;
      Func<int, int, double> accrual;
      double[,] V;

      // Handle a known default
      if (survCurve.DefaultDate != Dt.Empty && survCurve.DefaultDate < settle)
        return new double[0, 0];

      List<Dt> pricingDatesList;
      GetCouponAccrualUse(asOf, settle, scheduleParams, dayCount, isFloating, 
        curLevel, startStateIdx, levels,
                          pricingGrid, commFee, usage, discountCurve, referenceCurve,
        amortizations, interestPeriods, out coupon, out use, 
        out accrual, out pricingDatesList, floorValues,
        currentCouponRate);

      // Calculate the distribution across the credit quality states
      StateDistribution distribution = StateDistribution.Calculate(
        (ToolkitConfigurator.Settings.LoanModel.UseSettleForPricing ? settle : asOf),
        survCurve,
        (includePrepayOption ? prepaymentCurve : null),
        volatilityCurve,
        pricingDatesList.ToArray(),
        endDistribution,
        startStateIdx);

      // Calculate value matrix
      V = Recursive(coupon, accrual, use, discountCurve, recoveryCurve, 
        refiCost, distribution, fullDrawOnDefault, includePrepayOption);

      // Done
      return Collapse(V, distribution);
    }

    /// <summary>
    /// Calculates the present value of a Loan.
    /// </summary>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Pricing settlement date</param>
    /// <param name="scheduleParams">Schedule parameters</param>
    /// <param name="dayCount">The day count</param>
    /// <param name="isFloating">Whether the coupons are paid as a spread over a floating index</param>
    /// <param name="curLevel">The current performance level</param>
    /// <param name="startStateIdx">Start level index</param>
    /// <param name="levels">The performance levels (best to worst; ie last level is closest to default)</param>
    /// <param name="pricingGrid">The coupons or spreads for each performance level</param>
    /// <param name="commFee">The fee paid on undrawn notional</param>
    /// <param name="usage">The expected usage at each performance level</param>
    /// <param name="endDistribution">The probability of being in each performance level at loan maturity</param>
    /// <param name="discountCurve">The interest rate curve for discounting</param>
    /// <param name="referenceCurve">The interest rate curve for projecting forward rates</param>
    /// <param name="survCurve">The survival curve</param>
    /// <param name="recoveryCurve">The recovery curve</param>
    /// <param name="volatilityCurve">The volatility curve</param>
    /// <param name="fullDrawOnDefault">Whether the Loan is assumed to be fully drawn on a default</param>
    /// <param name="includePrepayOption">Whether to include the option to refinance the loan for business or cost reasons</param>
    /// <param name="prepaymentCurve">The prepayment curve</param>
    /// <param name="refiCost">The cost of refinancing as a rate (200 bps = 0.02)</param>
    /// <param name="amortizations">The list of scheduled amortizations</param>
    /// <param name="interestPeriods">The interest periods for the Loan</param>
    /// <param name="calType">Calibration type</param>
    /// <param name="calSpread">Calibration spread</param>
    /// <param name="currentCouponRate">For a floating rate term loan, pass the current coupon rate; 
    /// if null passed, the model will compute it based on interestPeriods.</param>
    /// <returns></returns>           
    public static double Pv(Dt asOf, Dt settle, ScheduleParams scheduleParams, 
      DayCount dayCount, bool isFloating,
      string curLevel, int startStateIdx, string[] levels,
      IDictionary<string, double> pricingGrid, double commFee, double[] usage, 
      double[] endDistribution,
      DiscountCurve discountCurve, DiscountCurve referenceCurve, SurvivalCurve survCurve,
      RecoveryCurve recoveryCurve, VolatilityCurve volatilityCurve, 
      bool fullDrawOnDefault, bool includePrepayOption, SurvivalCurve prepaymentCurve,
      double refiCost, IList<Amortization> amortizations, IList<InterestPeriod> interestPeriods, 
      CalibrationType calType, double calSpread, double? currentCouponRate)
    {
      return Pv(asOf, settle, scheduleParams, dayCount, isFloating,
                curLevel, startStateIdx, levels,
                pricingGrid, commFee, usage, endDistribution,
                discountCurve, referenceCurve, survCurve,
                recoveryCurve, volatilityCurve, fullDrawOnDefault, includePrepayOption, prepaymentCurve,
                refiCost, amortizations, interestPeriods,
                calType, calSpread, true, -1, null, currentCouponRate);
    }
    /// <summary>
    /// Calculates the present value of a Loan.
    /// </summary>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Pricing settlement date</param>
    /// <param name="scheduleParams">Schedule parameters</param>
    /// <param name="dayCount">The day count</param>
    /// <param name="isFloating">Whether the coupons are paid as a spread over a floating index</param>
    /// <param name="curLevel">The current performance level</param>
    /// <param name="startStateIdx">Start level index</param>
    /// <param name="levels">The performance levels (best to worst; ie last level is closest to default)</param>
    /// <param name="pricingGrid">The coupons or spreads for each performance level</param>
    /// <param name="commFee">The fee paid on undrawn notional</param>
    /// <param name="usage">The expected usage at each performance level</param>
    /// <param name="endDistribution">The probability of being in each performance level at loan maturity</param>
    /// <param name="discountCurve">The interest rate curve for discounting</param>
    /// <param name="referenceCurve">The interest rate curve for projecting forward rates</param>
    /// <param name="survCurve">The survival curve</param>
    /// <param name="recoveryCurve">The recovery curve</param>
    /// <param name="volatilityCurve">The volatility curve</param>
    /// <param name="fullDrawOnDefault">Whether the Loan is assumed to be fully drawn on a default</param>
    /// <param name="includePrepayOption">Whether to include the option to refinance the loan for business or cost reasons</param>
    /// <param name="prepaymentCurve">The prepayment curve</param>
    /// <param name="refiCost">The cost of refinancing as a rate (200 bps = 0.02)</param>
    /// <param name="amortizations">The list of scheduled amortizations</param>
    /// <param name="interestPeriods">The interest periods for the Loan</param>
    /// <param name="calType">Calibration type</param>
    /// <param name="calSpread">Calibration spread</param>
    /// <param name="scaleUsage">pv scaled by usage[startStateIdx]</param>
    /// <param name="curUsage">Initial usage level</param>
    /// <param name="floorValues">Values of each period's index rate floor</param>
    /// <param name="currentCouponRate">For a floating rate loan, pass the current coupon rate; 
    /// if null passed, the model will compute it based on interestPeriods.</param>
    /// <returns></returns>           
    public static double Pv(Dt asOf, Dt settle, ScheduleParams scheduleParams, 
      DayCount dayCount, bool isFloating,
      string curLevel, int startStateIdx, string[] levels,
      IDictionary<string, double> pricingGrid, double commFee, double[] usage, 
      double[] endDistribution,
      DiscountCurve discountCurve, DiscountCurve referenceCurve, SurvivalCurve survCurve,
      RecoveryCurve recoveryCurve, VolatilityCurve volatilityCurve, bool fullDrawOnDefault, 
      bool includePrepayOption, SurvivalCurve prepaymentCurve,
      double refiCost, IList<Amortization> amortizations, 
      IList<InterestPeriod> interestPeriods,
      CalibrationType calType, double calSpread, bool scaleUsage, 
      double curUsage, double[] floorValues, double? currentCouponRate)
    {
      Func<int, int, double> coupon;
      Func<int, int, double> use;
      Func<int, int, double> accrual;
      double[,] V;
      double origSpread = 0;
      double pv = 0;

      // Handle a known default
      if (survCurve.DefaultDate != Dt.Empty)
      {
        // Handle default before settlement date
        if (survCurve.DefaultDate < settle)
          return 0;

        // Ignore default in the future!
        if (survCurve.DefaultDate == settle)
        {
          double df = discountCurve.DiscountFactor(asOf, settle);
          double recovery = df * recoveryCurve.Interpolate(survCurve.DefaultDate);
          return recovery;
        }
      }
      
      List<Dt> pricingDatesList;
      GetCouponAccrualUse(asOf, settle, scheduleParams, dayCount, isFloating, 
        curLevel, startStateIdx, levels,
                          pricingGrid, commFee, usage, discountCurve, referenceCurve,
        amortizations, interestPeriods, out coupon, out use, out accrual, 
        out pricingDatesList, floorValues,
        currentCouponRate);

      if (calType == CalibrationType.DiscountCurve)
      {
        origSpread = discountCurve.Spread;
        discountCurve.Spread += calSpread;
      }
      else if (calType == CalibrationType.SurvivalCurve)
      {
        origSpread = survCurve.Spread;
        survCurve.Spread += calSpread;
      }

      try
      {
        // Calculate the distribution across the credit quality states
        StateDistribution distribution = StateDistribution.Calculate(
          (ToolkitConfigurator.Settings.LoanModel.UseSettleForPricing ? settle : asOf),
          survCurve,
          (includePrepayOption ? prepaymentCurve : null),
          volatilityCurve,
          pricingDatesList.ToArray(),
          endDistribution,
          startStateIdx);

        // Calculate value matrix
        if (scaleUsage)
          V = Recursive(coupon, accrual, use, discountCurve, recoveryCurve, 
            refiCost, distribution, fullDrawOnDefault, includePrepayOption);
        else
          V = RecursiveSimple(coupon, accrual, use, discountCurve, 
            recoveryCurve, refiCost, distribution, fullDrawOnDefault);

        // Collapse into performance level matrix
        V = Collapse(V, distribution);

        // adjust for the current usage
        if (!scaleUsage)
          V = AdjustForCurrentUsage(V, startStateIdx, curUsage, use);

        // Done
        if(curUsage<0)
          curUsage = usage[startStateIdx];
        double df = discountCurve.DiscountFactor(asOf, settle);
        pv = df * V[0, startStateIdx]/(scaleUsage? usage[startStateIdx] : curUsage);
      }
      finally
      {
        // Restore shifted curve
        if (calType == CalibrationType.DiscountCurve)
          discountCurve.Spread = origSpread;
        else if (calType == CalibrationType.SurvivalCurve)
          survCurve.Spread = origSpread;
      }

      // Done
      return pv;
    }

    /// <summary>
    /// Recursive calculation of state prices.
    /// </summary>
    /// <param name="coupon">A delegate.  It is required that
    ///   <c>coupon(t, r)</c> returns the full coupon for rating <c>r</c>
    ///   in the period <c>[t, t+1]</c>, due at the next date <c>t+1</c>.
    /// </param>
    /// <param name="use">A delegate.  It is required that
    ///   <c>use(t, r)</c> returns the use level for rating <c>r</c>
    ///   in the period <c>[t, t+1]</c>,
    ///   which is the amount the company needs to repay if it
    ///   decides to refinance at the next date <c>t+1</c>.
    /// </param>
    /// <param name="accrual">A delegate. It is required that <c>accrual(t, r)</c>
    ///   returns the accrual amount for rating <c>r</c> in the period <c>[t, t+1]</c> due at the 
    ///   next date <c>t+1</c>.
    /// </param>
    /// <param name="discountCurve">The discount curve for the Libor rates.</param>
    /// <param name="recoveryCurve">The recovery curve giving the recovery rate.</param>
    /// <param name="fr">Fixed refinance cost.</param>
    /// <param name="distributions">
    ///   Probability distribution of rating states, prepayments and defaults,
    ///   as well as their transitions.</param>
    /// <param name="fullDrawOnDefault">Whether the Loan is assumed to be fully drawn down on a default.</param>
    /// <param name="allowPrepayment">Whether to allow prepayment of the Loan or not.</param>
    /// <returns>A 2-D array V representing
    ///   state values by dates and states, where <c>V[t,i]</c> gives the 
    ///   expected value at date <c>t+1</c>, discounted back to <c>t</c>,
    ///   given that the credit is in state <c>i</c> at date <c>t</c>.
    /// </returns>
    /// 
    /// <remarks>
    /// <para>Let <m>V(t,i)</m> be the expected payment at date <m>t+1</m>,
    ///  discounted back to date <m>t</m>,
    /// given that the credit is in state <m>i</m> at date <m>t</m> and it keeps the loan.
    /// This class calculates the values of <m>V(t,i)</m> for all dates and states.
    /// </para>
    /// <para>
    /// Suppose the coupon and usage set for the period <m>[t,t+1]</m> are <m>C(t,i)</m> and <m>U(t,i)</m>,
    /// repectively.
    /// We consider all the possible payments at date <m>t+1</m>.
    /// </para>
    /// <ol>
    /// <li>If the credit defaults at date <m>t_d \in [t,t+1]</m>, then discounted payment is
    ///   <math>
    ///     \mathrm{DefaultPayment}_{t} = D(t,t_d) [C(t,i)A(t, t_d) + R(t_d)U(t,i)]
    ///   </math>
    ///   where <m>A(t,t_d)</m> is accrual from <m>t</m> to <m>t_d</m>,
    ///   <m>R(t_d)</m> is recovery rate and <m>D(t,t_d)</m> is the discount factor.
    /// </li>
    /// <li><para>Suppose the credit jumps to state <m>j</m> during <m>[t,t+1]</m>.  Since it survives,
    ///   it has to pay the full coupon <m>C(t,i)</m>.  For other payments, there are two
    ///   possibilities.</para>
    ///   <para><em>Case 1</em>.
    ///   It keeps the loan, draws a new usage level <m>U(t+1,j)</m>
    ///    and pays <m>V(t+1, j)</m> at the date <m>t+2</m>.  The corresponding
    ///    full payment is</para>
    ///    <math>
    ///      \mathrm{NormalPay}_{t} = C(t,i) + U(t,i) - U(t+1,j) + V(t+1,j)
    ///    </math><para>
    ///    <em>Case 2</em>.
    ///   The company prepays at date <m>t+1</m>.  The total payment (undiscounted) is
    ///   <math>
    ///     \mathrm{Prepay}_{t} = C(t,i) + U(t,i) + f_r
    ///   </math>
    ///   where <m>f_r</m> is the refinance costs.
    /// </para>
    /// </li>
    /// </ol>
    /// <para>
    ///   The company may prepay because of the business reasons other than the
    ///   cost of the loan.  On the other hand, when the comany decides to prepay
    ///   based on the costs of keeping the loan relative to refinance,
    ///   the actual payment is the smaller of both, discounted back to <m>t</m>
    ///    <math>
    ///     \mathrm{SurvivalPay}_t = D(t,t+1)\,\min\{\mathrm{Prepay}_{t}, \mathrm{NormalPay}_{t}\}
    ///    </math>
    ///    where <m>D(t,t+1)</m> is the discount factor.
    /// </para>
    /// <para>The later can be put in a simpler but equivalent way.
    /// The cost of the loan at date <m>t</m> for a company in state <m>i</m>
    /// is given by <m>V(t,i)-U(t,i)</m>.  If it is larger than the cost of refinance,
    /// <m>f_r</m>,
    /// then the company prepays at date <m>t</m>; otherwise, it keeps the loan unless
    /// there are other reasons fot it to prepay.
    /// </para>
    /// <para>
    ///   Therefore, once we have a full matrix of <m>V(t,i)</m>, it is easy to determine
    ///   when the company prepays.  This property can be employed to calculate the
    ///   measures such as risky durations and average loan lifes.
    /// </para>
    /// </remarks>
    public static double[,] Recursive(
      Func<int, int, double> coupon,
      Func<int, int, double> accrual,
      Func<int, int, double> use,
      DiscountCurve discountCurve,
      RecoveryCurve recoveryCurve,
      double fr,
      StateDistribution distributions,
      bool fullDrawOnDefault,
      bool allowPrepayment)
    {
      // Dimensions.
      //   (1) lastdate is the index of the last date;
      //   (2) nstates is the number of states excluding
      //       default and refinance.
      int lastdate = distributions.DateCount - 1;
      int nstates = distributions.StateCount;
      Debug.Assert(lastdate > 0 && nstates > 0);

      // 2-D array of the values by dates and states.
      double[,] V = new double[lastdate + 1, nstates];

      // Usages and coupons and accruals for the period [t, t+1].
      double Uc; // Contractual notional
      double[] U = new double[nstates];
      double[] C = new double[nstates];
      double[] A = new double[nstates];

      // Usages for the period [t+1, t+2].
      double[] Unext = new double[nstates];

      // For all the dates before maturity.
      Dt next = distributions.GetDate(lastdate);
      for (int t = lastdate - 1; t >= 0; --t)
      {
        // The current date.
        Dt current = distributions.GetDate(t);

        // One period forward discount factor.
        double df = discountCurve.DiscountFactor(current, next);

        // Get the usages for period [t,t+1],
        Uc = use(t, -1);
        for (int i = 0; i < nstates; ++i)
          U[i] = use(t, distributions.Level(i));

        // Get the coupons for period [t,t+1],
        for (int i = 0; i < nstates; ++i)
          C[i] = coupon(t, distributions.Level(i));

        // Get the accruals for priod [t, t+1]
        for (int i = 0; i < nstates; ++i)
          A[i] = accrual==null?0:accrual(t, distributions.Level(i));

        // If the company default in period [t, t+1]....
        Dt dfltDate = DefaultDt(current, next, 0.5);
        double recoveryRate = recoveryCurve.Interpolate(dfltDate);
        double dfltDf = discountCurve.DiscountFactor(current, dfltDate);

        // Suppose at t the company is in state i.
        for (int i = 0; i < nstates; ++i)
        {
          // Assume the Loan fully draws before default. So, in this period we drawn remainder and 
          // only receive recovery!
          double dfltDraw = (fullDrawOnDefault ? Uc - U[i] : 0.0);
          double value = (recoveryRate - dfltDraw) * distributions.JumpToDefault(t, i) * dfltDf;

          // The expected payment if the company prepays
          // for business reasons other than loan costs.
          // If allowPrepay = FALSE, then distribution should be calibrated with no prepayment curve
          // leading to 0 prepayment probabilities and correct transition probabilities
          double refiPayment = C[i] + A[i] + U[i] + fr;
          double jp = distributions.JumpToPrepay(t, i);
          if (jp > 0) value += jp * refiPayment;

          // Validate prepayment
          if (!allowPrepayment && jp > 1e-8)
            throw new ArgumentException("Prepayment " +
                                        "is turned off but the supplied distribution " +
                                        "has +ve prepayment probabilties! You must " +
                                        "recalibrate the distribution with no prepayments.");

          // Sum of all the other payments conditional on
          // transitions to other states.
          for (int j = 0; j < nstates; ++j)
          {
            // The company jumps from state i to j during [t, t+1].
            // Should it prepay after it knows the new state?
            double noRefiPayment = C[i] + (U[i] - Unext[j]) + V[t + 1, j];
            if (allowPrepayment)
              value += df * Math.Min(refiPayment, noRefiPayment) * distributions.JumpToState(t, i, j);
            else
              value += df * noRefiPayment * distributions.JumpToState(t, i, j);
          }
          V[t, i] = value;
        }

        // Prepare for the next loop.
        // Current date and usages become the next.
        next = current;
        Swap(ref U, ref Unext);
      }

      return V;
    }

    /// <summary>
    /// Recursive calculation of state prices.
    /// </summary>
    /// <param name="coupon">A delegate.  It is required that
    ///   <c>coupon(t, r)</c> returns the full coupon for rating <c>r</c>
    ///   in the period <c>[t, t+1]</c>, due at the next date <c>t+1</c>.
    /// </param>
    /// <param name="use">A delegate.  It is required that
    ///   <c>use(t, r)</c> returns the use level for rating <c>r</c>
    ///   in the period <c>[t, t+1]</c>,
    ///   which is the amount the company needs to repay if it
    ///   decides to refinance at the next date <c>t+1</c>.
    /// </param>
    /// <param name="accrual">A delegate. It is required that <c>accrual(t, r)</c>
    ///   returns the accrual amount for rating <c>r</c> in the period <c>[t, t+1]</c> due at the 
    ///   next date <c>t+1</c>.
    /// </param>
    /// <param name="discountCurve">The discount curve for the Libor rates.</param>
    /// <param name="recoveryCurve">The recovery curve giving the recovery rate.</param>
    /// <param name="fr">Fixed refinance cost.</param>
    /// <param name="distributions">
    ///   Probability distribution of rating states, prepayments and defaults,
    ///   as well as their transitions.</param>
    /// <param name="fullDrawOnDefault">Whether the Loan is assumed to be fully drawn down on a default.</param>
    /// <returns>A 2-D array V representing
    ///   state values by dates and states, where <c>V[t,i]</c> gives the 
    ///   expected value at date <c>t+1</c>, discounted back to <c>t</c>,
    ///   given that the credit is in state <c>i</c> at date <c>t</c>.
    /// </returns>
    /// 
    /// <remarks>
    /// <para>Let <m>V(t,i)</m> be the expected payment at date <m>t+1</m>,
    ///  discounted back to date <m>t</m>,
    /// given that the credit is in state <m>i</m> at date <m>t</m> and it keeps the loan.
    /// This class calculates the values of <m>V(t,i)</m> for all dates and states.
    /// </para>
    /// <para>
    /// Suppose the coupon and usage set for the period <m>[t,t+1]</m> are <m>C(t,i)</m> and <m>U(t,i)</m>,
    /// repectively.
    /// We consider all the possible payments at date <m>t+1</m>.
    /// </para>
    /// <ol>
    /// <li>If the credit defaults at date <m>t_d \in [t,t+1]</m>, then discounted payment is
    ///   <math>
    ///     \mathrm{DefaultPayment}_{t} = D(t,t_d) R(t_d)[C(t,i) + A(t, t_d) + U(t,i)]
    ///   </math>
    ///   where <m>A(t,t_d)</m> is accrual from <m>t</m> to <m>t_d</m>,
    ///   <m>R(t_d)</m> is recovery rate and <m>D(t,t_d)</m> is the discount factor.
    /// </li>
    /// <li><para>Suppose the credit jumps to state <m>j</m> during <m>[t,t+1]</m>.  Since it survives,
    ///   it has to pay the full coupon <m>C(t,i)</m>.  For other payments, there are two
    ///   possibilities.</para>
    ///   <para><em>Case 1</em>.
    ///   It keeps the loan, draws a new usage level <m>U(t+1,j)</m>
    ///    and pays <m>V(t+1, j)</m> at the date <m>t+2</m>.  The corresponding
    ///    full payment is</para>
    ///    <math>
    ///      \mathrm{NormalPay}_{t} = C(t,i) + U(t,i) - U(t+1,j) + V(t+1,j)
    ///    </math><para>
    ///    <em>Case 2</em>.
    ///   The company prepays at date <m>t+1</m>.  The total payment (undiscounted) is
    ///   <math>
    ///     \mathrm{Prepay}_{t} = C(t,i) + U(t,i) + f_r
    ///   </math>
    ///   where <m>f_r</m> is the refinance costs.
    /// </para>
    /// </li>
    /// </ol>
    /// <para>
    ///   Therefore, once we have a full matrix of <m>V(t,i)</m>, it is easy to determine
    ///   when the company prepays.  This property can be employed to calculate the
    ///   measures such as risky durations and average loan lifes.
    /// </para>
    /// </remarks>
    public static double[,] RecursiveSimple(
      Func<int, int, double> coupon,
      Func<int, int, double> accrual,
      Func<int, int, double> use,
      DiscountCurve discountCurve,
      RecoveryCurve recoveryCurve,
      double fr,
      StateDistribution distributions,
      bool fullDrawOnDefault)
    {
      // Dimensions.
      //   (1) lastdate is the index of the last date;
      //   (2) nstates is the number of states excluding
      //       default and refinance.
      int lastdate = distributions.DateCount - 1;
      int nstates = distributions.StateCount;
      Debug.Assert(lastdate > 0 && nstates > 0);

      // 2-D array of the values by dates and states.
      double[,] V = new double[lastdate + 1, nstates];

      // Usages and coupons and accruals for the period [t, t+1].
      double Uc; // Contractual notional
      double[] U = new double[nstates];
      double[] C = new double[nstates];
      double[] A = new double[nstates];

      // Usages for the period [t+1, t+2].
      double[] Unext = new double[nstates];

      // For all the dates before maturity.
      Dt next = distributions.GetDate(lastdate);
      for (int t = lastdate - 1; t >= 0; --t)
      {
        // The current date.
        Dt current = distributions.GetDate(t);

        // One period forward discount factor.
        double df = discountCurve.DiscountFactor(current, next);

        // Get the usages for period [t,t+1],
        Uc = use(t, -1);
        for (int i = 0; i < nstates; ++i)
          U[i] = use(t, distributions.Level(i));

        // Get the coupons for period [t,t+1],
        for (int i = 0; i < nstates; ++i)
          C[i] = coupon(t, distributions.Level(i));

        // Get the accruals for priod [t, t+1]
        for (int i = 0; i < nstates; ++i)
          A[i] = accrual == null ? 0 : accrual(t, distributions.Level(i));

        // If the company default in period [t, t+1]....
        Dt dfltDate = DefaultDt(current, next, 0.5);
        double recoveryRate = recoveryCurve.Interpolate(dfltDate);
        double dfltDf = discountCurve.DiscountFactor(current, dfltDate);

        // Suppose at t the company is in state i.
        for (int i = 0; i < nstates; ++i)
        {
          // when default during the period, only gets back the recovery portion of the owed cash flows and the notional
          double probDefault = distributions.JumpToDefault(t, i); // This is probability of moving to default absorbing state from being in state i at time t
          double value = recoveryRate * (C[i] + A[i] + U[i]) * probDefault * dfltDf;

          // The expected payment if the company prepays
          double refiPayment = C[i] + A[i] + U[i] + fr;
          double jp = distributions.JumpToPrepay(t, i);  // This is probability of moving to PREPAY absorbing state from being in state i at time t (event of FULL repayment)
          if (jp > 0) value += jp * refiPayment;

          // Sum of all the other payments conditional on
          // transitions to other states.
          for (int j = 0; j < nstates; ++j)
          {
            // The company jumps from state i to j during [t, t+1].
            double noRefiPayment = C[i] + (U[i] - Unext[j]) + V[t + 1, j];
            double probStateTransition = distributions.JumpToState(t, i, j); // This is the probability of moving to state j at time (t+1) from being in state i at time t
            value += df * noRefiPayment * probStateTransition;
          }
          V[t, i] = value;
        }

        // Prepare for the next loop.
        // Current date and usages become the next.
        next = current;
        Swap(ref U, ref Unext);
      }

      return V;
    }
    #endregion State Prices

    #region State Distributions
    /// <summary>
    /// Calculates the ending probabilities conditional on no default or prepayment assuming a normal distribution 
    /// across the performance levels at the given date.
    /// </summary>
    /// 
    /// <param name="states">The states</param>
    /// <param name="currentState">The current state</param>
    /// 
    /// <returns>Array of End Probabilities</returns>
    /// 
    public static double[] CalculateEndDistribution(string[] states, string currentState)
    {
      int idx = Array.IndexOf(states, currentState);
      double range = 6.0;
      double width = range / states.Length;
      double mu = ((idx + 1) + idx) / 2.0 * width - 0.5 * range;
      double[] result = new double[states.Length];
      double sumP = 0;

      // calculate weights
      for (int i = 0; i < states.Length; i++)
      {
        result[i] = Normal.cumulative((i + 1) * width - 0.5 * range, mu, 1.0);
        if (i > 0)
          result[i] -= sumP;
        sumP += result[i];
      }

      // Scale up to 100%
      for (int i = 0; i < result.Length; i++)
        result[i] /= sumP;

      // Done
      return result;
    }

    /// <summary>
    /// Calculates the unconditional probabilities of the Loan being in each performance level at 
    /// each coupon date.
    /// </summary>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Pricing settlement date</param>
    /// <param name="scheduleParams">Schedule parameters</param>
    /// <param name="survivalCurve">Survival Curve</param>
    /// <param name="prepaymentCurve">Prepayment curve</param>
    /// <param name="volatilityCurve">Volatility curve</param>
    /// <param name="currentState">Current state of loan</param>
    /// <param name="endDistribution">The probability of being in each performance level at loan maturity</param>
    /// <param name="calType">Calibration type</param>
    /// <param name="calSpread">Calibration spread</param>
    /// <returns>Distribution across time and levels</returns>
    /// 
    public static double[,] Distribution(Dt asOf, Dt settle, ScheduleParams scheduleParams, 
      SurvivalCurve survivalCurve, 
      SurvivalCurve prepaymentCurve, VolatilityCurve volatilityCurve, int currentState, 
      double[] endDistribution, CalibrationType calType, double calSpread)
    {
      double origSpread = 0;
      int n, m;
      double[,] result;

      // Get the coupon dates
      Dt[] dates = LoanModel.PaymentDates(asOf, settle, scheduleParams);

      if(calType == CalibrationType.SurvivalCurve)
      {
        origSpread = survivalCurve.Spread;
        survivalCurve.Spread += calSpread;
      }
      try
      {
      // Calculate the distribution
        LoanModel.StateDistribution dist = LoanModel.StateDistribution.Calculate(
          (ToolkitConfigurator.Settings.LoanModel.UseSettleForPricing ? settle : asOf), 
          survivalCurve, 
          prepaymentCurve,
          volatilityCurve, 
          dates,
          endDistribution, 
          currentState);

        n = dist.StateCount;
        m = dist.LevelCount;
        result = new double[dates.Length - 1,m + 2];

      // Go through dates and states
      for (int t = 1; t < dates.Length; t++)
      {
        result[t - 1, 0] = dist.DefaultProbability(t);
        for (int i = 1; i <= n; ++i)
          result[t - 1, dist.Level(i - 1) + 1] += dist.Probability(t, i - 1);
        result[t - 1, m + 1] = dist.PrepayProbability(t);
        }
      }
      finally
      {
        if(calType == CalibrationType.SurvivalCurve)
          survivalCurve.Spread = origSpread;
      }

      // Done
      return result;
    }

    /// <summary>
    /// Calculates the transition matrix for the Loan on a given date.
    /// </summary>
    /// <param name="date">The date to calculate the matrix for</param>
    /// <param name="asOf">The pricing date</param>
    /// <param name="settle">The pricing settlement date</param>
    /// <param name="scheduleParams">Schedule parameters</param>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="prepaymentCurve">Prepayment curve</param>
    /// <param name="volatilityCurve">Volatility curve</param>
    /// <param name="currentState">Current state of loan</param>
    /// <param name="endDistribution">The probability of being in each performance level at loan maturity</param>
    /// <param name="calType">Calibration type</param>
    /// <param name="calSpread">Calibration spread</param>
    /// <returns></returns>
    public static double[,] TransitionMatrix(Dt date, Dt asOf, Dt settle, 
      ScheduleParams scheduleParams, 
      SurvivalCurve survivalCurve, SurvivalCurve prepaymentCurve, 
      VolatilityCurve volatilityCurve,int currentState, double[] endDistribution, 
      CalibrationType calType, double calSpread)
    {
      double origSpread = 0;
      LoanModel.StateDistribution dist;
      double[,] result;

      // Get the coupon dates
      Dt[] dates = LoanModel.PaymentDates(asOf, settle, scheduleParams);

      if(calType == CalibrationType.SurvivalCurve)
      {
        origSpread = survivalCurve.Spread;
        survivalCurve.Spread += calSpread;
      }

      try
      {
      // Calculate the distribution
        dist = LoanModel.StateDistribution.Calculate(
        (ToolkitConfigurator.Settings.LoanModel.UseSettleForPricing ? settle : asOf), 
        survivalCurve, 
        prepaymentCurve, 
        volatilityCurve, 
        dates,
        endDistribution, 
        currentState);

      // Find the date index t such that the date is in the date range [t, t+1).
      // Also handle two special cases:
      //   (1) If the date is before the first date, then t = 0;
      //   (2) If the date is after the last date, the move it
      //       to the last period (indexed by lastIndex - 1).

      int lastIndex = dates.Length - 1;
        int t;
      if (date >= dates[lastIndex])
        t = lastIndex - 1;
      else if (date < dates[0])
        t = 0;
      else
      {
        for (t = 0; t < lastIndex; t++)
          if (dates[t] <= date && date < dates[t + 1])
            break;
      }

      int m = dist.LevelCount;
      int n = dist.StateCount;
        result = new double[m + 2,m + 2];
      double[] sumP = new double[m];

      // Default stay default.
      result[0, 0] = 1.0;

      for (int i = 0; i < n; i++)
      {
        double Px = dist.Probability(t, i);
        int li = dist.Level(i) + 1;
        sumP[li - 1] += Px;

        // Jump to default.
        result[li, 0] += Px * dist.JumpToDefault(t, i);

        // Credit Quality states.
        for (int j = 0; j < n; j++)
          result[li, dist.Level(j) + 1] += Px * dist.JumpToState(t, i, j);

        // Jump to prepaid.
        result[li, m + 1] += Px * dist.JumpToPrepay(t, i);

        double total = dist.JumpToPrepay(t, i) + dist.JumpToDefault(t, i);
        for(int j = 0; j < n; j++)
          total += dist.JumpToState(t, i, j);
      }

      // Prepaid stays prepaid.
      result[m + 1, m + 1] = 1.0;

      // Adjust by sum of probabilities of being in each level
      for (int i = 0; i < m; i++)
      {
        double sumPi = sumP[i];
        if (sumPi <= 0)
        {
          // Make this an absorb state.
          sumPi = result[i + 1, i] = 1;
        }
        for (int j = 0; j <= m + 1; j++)
          result[i + 1, j] /= sumPi;
      }
      }
      finally
      {
        if(calType == CalibrationType.SurvivalCurve)
          survivalCurve.Spread = origSpread;
      }

      // Done.
      return result;
    }

    /// <summary>
    ///   Distributions of credit states and transitions.
    /// </summary>
    public class StateDistribution
    {
      #region Compute Distributions

      /// <summary>
      ///   Compute the probability distribution over default,
      ///   prepayment and the performance levels.
      /// </summary>
      /// <param name="asOf">Pricing date.</param>
      /// <param name="survivalCurve">Survival curve.</param>
      /// <param name="prepayCurve">Prepayment curve due to reasons other than loan costs.</param>
      /// <param name="volatilityCurve">Volatility curve</param>
      /// <param name="dates">An array of payment dates or pricing grid dates.</param>
      /// <param name="endDistribution">An array representing
      ///   the probability distribution over performance levels
      ///   at maturity conditional on no default and no prepayment.</param>
      /// <param name="startStateIdx">Index of initial performance level,
      ///   which should be in the range <c>[0, N-1]</c>, where <c>N</c>
      ///   is the number of performance levels.</param>
      /// <returns>A state distribution object.</returns>
      public static StateDistribution Calculate(
        Dt asOf,
        SurvivalCurve survivalCurve,
        SurvivalCurve prepayCurve,
        VolatilityCurve volatilityCurve,
        Dt[] dates,
        double[] endDistribution,
        int startStateIdx)
      {
        return Calculate(asOf, survivalCurve, prepayCurve, volatilityCurve, dates, endDistribution, startStateIdx, 20);
      }

      /// <summary>
      ///   Compute the probability distribution over default,
      ///   prepayment and the performance levels.
      /// </summary>
      /// <param name="asOf">Pricing date.</param>
      /// <param name="survivalCurve">Survival curve.</param>
      /// <param name="prepayCurve">Prepayment curve due to reasons other than loan costs.</param>
      /// <param name="volatilityCurve">Volatility curve</param>
      /// <param name="dates">An array of payment dates or pricing grid dates.</param>
      /// <param name="endDistribution">An array representing
      ///   the probability distribution over performance levels
      ///   at maturity conditional on no default and no prepayment.</param>
      /// <param name="startStateIdx">Index of initial performance level,
      ///   which should be in the range <c>[0, N-1]</c>, where <c>N</c>
      ///   is the number of performance levels.</param>
      /// <param name="quadraturePoints">The number of quadrature points to use.</param>
      /// <returns>A state distribution object.</returns>
      public static StateDistribution Calculate(
        Dt asOf,
        SurvivalCurve survivalCurve,
        SurvivalCurve prepayCurve,
        VolatilityCurve volatilityCurve,
        Dt[] dates,
        double[] endDistribution,
        int startStateIdx,
        int quadraturePoints)
      {
        Curve2D distribution = new Curve2D(asOf);
        int[] map;
        int adjStartStateIdx;

        // Adjust the end distribution so the calculation is accurate enough
        double[] adjEndDistribution = AdjustPerformanceLevels(startStateIdx, endDistribution, 
          quadraturePoints, out map, out adjStartStateIdx);

        int nOrigLevels = endDistribution.Length;
        int nstates = adjEndDistribution.Length;
        int extra = (prepayCurve == null ? 1 : 2);
        int nlevels = nstates + extra;
        int ngroups = nstates + 1;

        int ndates = dates.Length;
        if (ndates == 1 && dates[0] <= asOf)
        {
          distribution.Initialize(ndates, nlevels, ngroups);
          distribution.SetDate(0, asOf);
        }
        else
        {
          int firstIdx = -1;
          for (int i = 0; i < ndates; ++i)
            if (dates[i] >= asOf)
            {
              firstIdx = i;
              break;
            }
          if (firstIdx < 0)
            throw new System.ArgumentException("Invalid cashflow dates");

          int count = ndates - firstIdx;
          distribution.Initialize(count, nlevels, ngroups);
          for (int i = firstIdx; i < ndates; ++i)
            distribution.SetDate(i - firstIdx, dates[i]);
        }
        for (int i = 0; i < nlevels; ++i)
          distribution.SetLevel(i, i);

        BaseEntityPINVOKE.LoanModel_ComputeDistributions(
          Curve.getCPtr(survivalCurve), Curve.getCPtr(prepayCurve), 
          Curve.getCPtr(volatilityCurve),
          adjStartStateIdx, adjEndDistribution, Curve2D.getCPtr(distribution));
        if (BaseEntityPINVOKE.SWIGPendingException.Pending)
          throw new ToolkitException("Error implying Loan Survival Curve!", 
            BaseEntityPINVOKE.SWIGPendingException.Retrieve());

        return new StateDistribution(
          nstates,
          nOrigLevels,
          extra == 1 ? 0 : nlevels - 1,
          map,
          adjStartStateIdx,
          distribution);
      }
      
      /// <exclude/> 
      private static double[] AdjustPerformanceLevels(int curLevel, 
        double[] endDistribution, int quadraturePoints, out int[] map, 
        out int adjCurLevel)
      {
        int levels = endDistribution.Length;
        int[] points = new int[levels];
        int sumP = 0;
        int quadPts = Math.Max(quadraturePoints, endDistribution.Length);

        // Determine how many quadrature points are in each level at maturity
        for (int i = 0; i < levels; i++)
        {
          points[i] = Math.Max(1, (int)Math.Floor(endDistribution[i] * quadPts));
          sumP += points[i];
        }

        // Adjust to make sure we get all the points
        if (sumP < quadPts)
          points[curLevel] += (quadPts - sumP);
        else if (sumP > quadPts)
          quadPts = sumP;

        // Make sure we have an odd number of points in the starting bucket
        if (points[curLevel] % 2 == 0)
        {
          points[curLevel]++;
          quadPts++;
        }

        // Go through each performance level
        sumP = 0;
        map = new int[quadPts];
        double[] adjEndDist = new double[quadPts];
        for (int i = 0; i < levels; i++)
        {
          // Setup for the quad pts in each level
          for (int j = 0; j < points[i]; j++, sumP++)
          {
            map[sumP] = i;
            adjEndDist[sumP] = endDistribution[i] / points[i];
          }
        }

        // Make sure our probabilities total 1.0
        NumericUtils.Scale(adjEndDist);

        // Find the new start
        adjCurLevel = (int)Math.Floor(points[curLevel] / 2.0);
        for (int i = 0; i < curLevel; i++)
          adjCurLevel += points[i];

        // Done
        return adjEndDist;
      }

      #endregion Compute Distributions

      #region Public Members

      /// <summary>
      ///  Probability of jumping from state <c>i</c>
      ///  to prepayment during period <c>[t,t+1]</c>.
      /// </summary>
      /// <param name="t">Date index,
      ///   which must be in the range <c>[0, DateCount - 2]</c>.</param>
      /// <param name="i">State jumping from,
      ///   which must be in the range <c>[0, StateCount - 1]</c>.</param>
      /// <returns>Probability of jumping to prepayment.</returns>
      public double JumpToPrepay(int t, int i)
      {
        Debug.Assert(t >= -1 && t < lastdate_);
        Debug.Assert(i >= 0 && i < nstates_);
        return refiIndex_ == 0 ? 0.0
          : dist_.GetValue(i + 1, t + 1, refiIndex_);
      }

      /// <summary>
      ///  Probability of jumping from state <c>i</c>
      ///  to default during period <c>[t,t+1]</c>.
      /// </summary>
      /// <param name="t">Date index,
      ///   which must be in the range <c>[0, DateCount - 2]</c>.</param>
      /// <param name="i">State jumping from,
      ///   which must be in the range <c>[0, StateCount - 1]</c>.</param>
      /// <returns>Probability of jumping to default</returns>
      public double JumpToDefault(int t, int i)
      {
        Debug.Assert(t >= -1 && t < lastdate_);
        Debug.Assert(i >= 0 && i < nstates_);
        return dist_.GetValue(i + 1, t + 1, 0);
      }

      /// <summary>
      ///   The probability of jumping from state <c>i</c>
      ///   to state <c>j</c> during period <c>[t,t+1]</c>.
      /// </summary>
      /// <param name="t">Date index,
      ///   which must be in the range <c>[0, DateCount - 2]</c>.
      /// </param>
      /// <param name="i">State jumping from,
      ///   which must be in the range <c>[0, StateCount - 1]</c>.</param>
      /// <param name="j">State jumping to,
      ///   which must be in the range <c>[0, StateCount - 1]</c>.</param>
      /// <returns>Transition probaility</returns>
      public double JumpToState(int t, int i, int j)
      {
        Debug.Assert(t >= 0 && t < lastdate_);
        Debug.Assert(i >= 0 && i < nstates_);
        Debug.Assert(j >= 0 && j < nstates_);
        return dist_.GetValue(i + 1, t + 1, j + 1);
      }

      /// <summary>
      ///   The performance level of point <c>i</c>. 
      /// </summary>
      /// 
      /// <param name="i">The discretization point.</param>
      /// 
      /// <returns>Index of performance level.</returns>
      /// 
      public int Level(int i)
      {
        if (levelMap_ == null || levelMap_.Length == 0)
          return i;
        else
          return levelMap_[i];
      }

      /// <summary>
      /// The grid points that fall within the given performance level.
      /// </summary>
      /// 
      /// <param name="level">The performance level</param>
      /// 
      /// <returns>Array of Grid Point indexes</returns>
      /// 
      public int[] Points(int level)
      {
        // Handle no map
        if (!ArrayUtil.HasValue(levelMap_))
          return new int[] { level };

        // Find all the points
        List<int> points = new List<int>(levelMap_.Length);
        for (int i = 0; i < levelMap_.Length; i++)
          if (level == levelMap_[i])
            points.Add(i);

        // Done
        return points.ToArray();
      }

      /// <summary>
      ///  The probability of state i at date t.
      /// </summary>
      /// <param name="t">Date index,
      ///   which must be in the range <c>[0, DateCount - 1]</c>.</param>
      /// <param name="i">State index,
      ///   which must be in the range <c>[0, StateCount - 1]</c>.</param>
      /// <returns>State probaility</returns>
      public double Probability(int t, int i)
      {
        Debug.Assert(i >= 0 && i < nstates_);
        return dist_.GetValue(t, i + 1);
      }

      /// <summary>
      ///  The probability of being defaulted at date t.
      /// </summary>
      /// <param name="t">Date index,
      ///   which must be in the range <c>[0, DateCount - 1]</c>.</param>
      /// <returns>Default probaility</returns>
      public double DefaultProbability(int t)
      {
        Debug.Assert(t >= 0 && t <= lastdate_);
        return dist_.GetValue(t, 0);
      }

      /// <summary>
      ///  The probability of being prepaid at date t.
      /// </summary>
      /// <param name="t">Date index,
      ///   which must be in the range <c>[0, DateCount - 1]</c>.</param>
      /// <returns>Prepayment probaility</returns>
      public double PrepayProbability(int t)
      {
        Debug.Assert(t >= 0 && t <= lastdate_);
        return refiIndex_ == 0 ? 0.0 :
          dist_.GetValue(t, refiIndex_);
      }

      /// <summary>
      /// Gets the date value at a date index.
      /// </summary>
      /// <param name="t">Date index,
      ///   which must be in the range <c>[0, DateCount - 1]</c>.</param>
      /// <returns>Date.</returns>
      public Dt GetDate(int t)
      {
        Debug.Assert(t >= 0 && t <= lastdate_);
        return dist_.GetDate(t);
      }

      /// <summary>
      ///   Number of rating states excluding default and refinance.
      /// </summary>
      public int StateCount
      {
        get { return nstates_; }
      }

      /// <summary>
      /// Gets the date count.
      /// </summary>
      /// <value>The date count.</value>
      public int DateCount
      {
        get { return lastdate_ + 1; }
      }

      /// <summary>
      /// The discretization point that is the starting point at time <c>t = 0</c>.
      /// </summary>
      public int StartState
      {
        get { return startStateIdx_; }
      }

      /// <summary>
      /// The number of performance levels.
      /// </summary>
      public int LevelCount
      {
        get { return nlevels_; }
      }

      #endregion Public Members

      #region Private Members

      /// <exclude/> 
      private StateDistribution(
        int stateCount, int levelCount, int refiIndex, int[] map, int startIdx, Curve2D dist)
      {
        nstates_ = stateCount;
        refiIndex_ = refiIndex;
        dist_ = dist;
        lastdate_ = dist.NumDates() - 1;
        levelMap_ = map;
        startStateIdx_ = startIdx;
        nlevels_ = levelCount;
      }

      private readonly int refiIndex_;
      private readonly int nstates_;
      private readonly int lastdate_;
      private readonly Curve2D dist_;
      private readonly int[] levelMap_;
      private readonly int startStateIdx_;
      private readonly int nlevels_;

      #endregion Private Members
    }
    #endregion State Distributions

    #region Utilities
    /// <summary>
    /// Calculates the payment dates of the loan, where the first date is the settlement date.
    /// </summary>
    /// 
    /// <param name="asOf">The pricing date</param>
    /// <param name="settle">The pricing settlement date</param>
    /// <param name="scheduleParams">Schedule terms</param>
    /// 
    /// <returns>Array of Payment Dates</returns>
    /// 
    public static Dt[] PaymentDates(Dt asOf, Dt settle, ScheduleParams scheduleParams)
    {
      // Calculate Schedule
      Schedule schedule = new Schedule(settle, scheduleParams.AccrualStartDate, scheduleParams.FirstCouponDate,
                                       scheduleParams.NextToLastCouponDate, scheduleParams.Maturity,
                                       scheduleParams.Frequency, scheduleParams.Roll,
                                       scheduleParams.Calendar, scheduleParams.CycleRule, scheduleParams.CashflowFlag);

      // Get payment dates
      int numOfDates = schedule.Count + 1;
      Dt[] paymentDates = new Dt[numOfDates];
      paymentDates[0] = settle;
      for (int i = 0; i < schedule.Count; i++)
        paymentDates[i + 1] = schedule.GetPaymentDate(i);

      // Done
      return paymentDates;
    }

    /// <summary>
    ///  Calculate the default date based on default timing.
    /// </summary>
    /// <param name="start">Start date.</param>
    /// <param name="end">End date.</param>
    /// <param name="defaultTiming">Default timing.</param>
    /// <returns>Default date.</returns>
    public static Dt DefaultDt(Dt start, Dt end, double defaultTiming)
    {
      if (defaultTiming <= 0.0)
        return start;
      if (defaultTiming >= 1.0)
        return end;
      return new Dt(start.ToDouble() * (1 - defaultTiming)
        + defaultTiming * end.ToDouble());
    }

    /// <exclude/> 
    internal static void Swap<T>(ref T a, ref T b)
    {
      T tmp = a; a = b; b = tmp;
    }
    
    /// <exclude/> 
    private static double[,] AdjustForCurrentUsage(double[,] matrix, int startStateIdx, double curUsage, Func<int, int, double> use)
    {
      // Adjust to reweight
      if(curUsage >= 0 && curUsage <= 1)
        matrix[0, startStateIdx] += curUsage - use(0, startStateIdx);

      // Done
      return matrix;
    }

    /// <exclude/> 
    private static double[,] Collapse(double[,] V, StateDistribution dist)
    {
      int T = V.GetLength(0);
      int N = V.GetLength(1);
      double[,] matrix = new double[T, dist.LevelCount];

      // Collapse each discretization point into it's performance level bucket, 
      // weighted by the probability of being at each point
      for (int t = 0; t < T; t++)
      {
        double[] sumP = new double[dist.LevelCount];
        for (int n = 0; n < N; n++)
        {
          double p = dist.Probability(t, n);
          matrix[t, dist.Level(n)] += p * V[t, n];
          sumP[dist.Level(n)] += p;
        }

        // Adjust to reweight
        for (int i = 0; i < dist.LevelCount; i++)
          matrix[t, i] /= (Math.Abs(sumP[i]) < 1e-8 ? 1.0 : sumP[i]);
      }

      // Done
      return matrix;
    }
    #endregion Utilities

    #region Misc
    /// <summary>
    /// The probabilistic weighted average life.
    /// </summary>
    /// 
    /// <param name="settle">The pricing settlement date</param>
    /// <param name="scheduleParams">Schedule terms</param>
    /// <param name="prepaymentCurve">The prepayment curve</param>
    /// <param name="drawn">The amount drawn at the settlement date</param>
    /// <param name="dayCount">The day count</param>
    /// <param name="amortizations">The amortization schedule</param>
    /// <param name="ccy">The currency that coupons and principal are paid in</param>
    /// 
    /// <returns>Expected Weighted Average Life</returns>
    /// 
    public static double ExpectedWAL(Dt settle, ScheduleParams scheduleParams, 
      Currency ccy, DayCount dayCount, double drawn, 
      IList<Amortization> amortizations, SurvivalCurve prepaymentCurve)
    {
      double wal = 0, sumq = 0;

      PaymentSchedule ps = new PaymentSchedule();
      double ntnl = 1.0;

      const double principal = 1.0;
      const double coupon = 0.0;

      var schedule = Schedule.CreateScheduleForCashflowFactory(scheduleParams);

      ps.AddPayments(BkwdCompatiblePaymentSchedule(settle,
        schedule, ccy, dayCount,
        new PaymentGenerationFlag(schedule.CashflowFlag, false, false),
        coupon, null, principal, amortizations));
      
        var cf = new CashflowAdapter(ps);

      // Calculate expected WAL
      for (int i = 0; i < cf.Count; i++)
      {
        Dt date = cf.GetDt(i);// period start
        Dt lastDate = (i == 0 ? settle : Dt.AddDays(cf.GetDt(i - 1), 1, Calendar.None));
        double t = Dt.Fraction(settle, date, dayCount); // time from settle to period start
        double P = cf.GetAmount(i); // weight of amortization
        double N = ntnl; // weight of prepayment
        double p = (prepaymentCurve == null ? 1.0 : prepaymentCurve.SurvivalProb(date)); // probability of no prepayment until period start
        double q = (prepaymentCurve == null ? 0.0 : (prepaymentCurve.DefaultProb(lastDate, date) * prepaymentCurve.SurvivalProb(lastDate)));

        // probabilistic wal
        if (i < cf.Count - 1)
          wal += t * p * P + t * q * N;
        else
          wal += (1 - sumq) * t * P;

        // Sum q
        sumq += q;

        // Adjust notional by any amortizations
        ntnl -= P;
      }

      // Done
      return wal;
    }

    //this function is specifically for the loan expect wal payment schedule.
    // for the generic payment schedule, we can actually call the function in
    // the LegacyCashflowCompatible
    private static IEnumerable<Payment> BkwdCompatiblePaymentSchedule(
      Dt asOf, Schedule schedule,
      Currency ccy, DayCount dayCount, PaymentGenerationFlag flag,
      double initialCoupon, IList<CouponPeriod> cpns,
      double initialNotional, IList<Amortization> amortizations)
    {
      bool includeMaturityAccrual = flag.IncludeMaturityAccrual;
      bool includeMaturityProtection = flag.IncludeMaturityProtection;
      bool accrueOnCycle = flag.AccrueOnCycle;
      bool creditRiskToPaymentDate = flag.CreditRiskToPaymentDate;
      var accruedFractionOnDefault = flag.AccruedPaidOnDefault ? 0.5 : 0;

      var firstPeriod = LegacyCashflowCompatible.GetFirstPeriod(asOf,
        schedule, accrueOnCycle);

      Func<int, double, double, InterestPayment>
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
            IncludeEndDateInAccrual = includeMaturityAccrual && i == schedule.Count - 1,
            IncludeEndDateProtection = includeMaturityProtection && i == schedule.Count - 1,
          };
          if (!creditRiskToPaymentDate && periodEnd != ip.PayDt)
          {
            ip.CreditRiskEndDate = periodEnd;
          }
          return ip;
        };

      double origPrincipal = (initialNotional > 0.0) ? initialNotional : 1.0;

      double remainingPrincipal = origPrincipal;
      int nextAmort = 0, amortCount = amortizations?.Count ?? -1;
      // ReSharper disable once PossibleNullReferenceException
      while (nextAmort < amortCount && amortizations[nextAmort].Date < asOf)
      {
        remainingPrincipal -= amortizations[nextAmort].Amount;
        nextAmort++;
      }

      double coupon = initialCoupon;
      int nextCpn = -1;
      int lastCpnIndex = cpns?.Count > 0 ? cpns.Count - 1 : -1;
      // ReSharper disable once PossibleNullReferenceException
      while (nextCpn < lastCpnIndex && cpns[nextCpn + 1].Date < asOf)
      {
        coupon = cpns[++nextCpn].Coupon;
      }

      //- Generate payments.
      int lastSchedIndex = schedule.Count - 1;
      for (int i = firstPeriod; i <= lastSchedIndex; i++)
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
        if (i == lastSchedIndex && initialNotional > 0.0)
          noDfltCf = remainingPrincipal;

        yield return createInterestPaymentFn(i, remainingPrincipal, coupon);

        if (initialNotional > 0 && (noDfltCf > 0 || noDfltCf < 0))
        {
          var pe = new PrincipalExchange(date, noDfltCf, ccy);
          if (i == lastSchedIndex && flag.IncludeMaturityProtection)
            pe.CreditRiskEndDate = end + 1;
          yield return pe;
        }

        remainingPrincipal -= noDfltCf;
      }
    }







    #endregion

    #region Implied Discount Spread

    /// <summary>
    /// Calculates the spread over the discount curve that causes the model price to match the given price.
    /// </summary>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Pricing settlement date</param>
    /// <param name="scheduleParams">Schedule parameters</param>
    /// <param name="dayCount">The day count</param>
    /// <param name="discountCurve">The interest rate curve for discounting</param>
    /// <param name="isFloating">Whether the coupons are paid as a spread over a floating index</param>
    /// <param name="referenceCurve">The interest rate curve for projecting forward rates</param>
    /// <param name="survCurve">Survival curve</param>
    /// <param name="recoveryCurve">Recovery curve</param>
    /// <param name="fullDrawOnDflt">Whether the Loan is assumed to be fully drawn on a default</param>
    /// <param name="includePrepayOption">Include prepayment option</param>
    /// <param name="prepaymentCurve">Prepayment curve</param>
    /// <param name="volatilityCurve">Volaility curve</param>
    /// <param name="refiCost">The cost of refinancing as a rate (200 bps = 0.02)</param>
    /// <param name="curLevel">The current performance level</param>
    /// <param name="startStateIdx">The current performance level of the Loan</param>
    /// <param name="levels">The performance levels (best to worst; ie last level is closest to default)</param>
    /// <param name="pricingGrid">The coupons or spreads for each performance level</param>
    /// <param name="commFee">The fee paid on undrawn notional</param>
    /// <param name="usage">The expected usage at each performance level</param>
    /// <param name="endDistribution">The probability of being in each performance level at loan maturity</param>
    /// <param name="amortizations">The scheduled amortizations</param>
    /// <param name="interestPeriods">The interest periods for the Loan</param>
    /// <param name="fullPrice">Full loan price</param>
    /// <param name="scaleUsage"></param>
    /// <param name="currentUsage"></param>
    /// <param name="floorValues"></param>
    /// <param name="currentCouponRate">For a floating rate loan, pass the current coupon rate; 
    /// if null passed, the model will compute it based on interestPeriods.</param>
    /// <returns>Implied Discount Spread</returns>
    public static double ImpliedDiscountSpread(Dt asOf, Dt settle, 
      ScheduleParams scheduleParams, DayCount dayCount, 
      DiscountCurve discountCurve, bool isFloating, DiscountCurve referenceCurve,
      SurvivalCurve survCurve, RecoveryCurve recoveryCurve, 
      bool fullDrawOnDflt, bool includePrepayOption,
      SurvivalCurve prepaymentCurve, VolatilityCurve volatilityCurve, 
      double refiCost, string curLevel,
      int startStateIdx, string[] levels, IDictionary<string, double> pricingGrid, 
      double commFee,
      double[] usage, double[] endDistribution, IList<Amortization> amortizations,
      IList<InterestPeriod> interestPeriods, double fullPrice, 
      bool scaleUsage, double currentUsage, double[] floorValues, double? currentCouponRate)
    {
      double spread = 0;
      Brent2 solver = new Brent2();
      ImpliedDiscountSpreadSolver f = new ImpliedDiscountSpreadSolver(asOf, settle, 
        scheduleParams, commFee, isFloating, dayCount, curLevel, pricingGrid,
        discountCurve, referenceCurve, survCurve, recoveryCurve, prepaymentCurve, 
        volatilityCurve, refiCost, includePrepayOption, endDistribution, startStateIdx, 
        usage, interestPeriods, fullDrawOnDflt, levels, amortizations, scaleUsage, 
        currentUsage, floorValues, currentCouponRate);

      try
      {
        // Solve
        spread = solver.solve(f, fullPrice, 1E-8, 0.1);
      }
      catch (Exception ex)
      {
        Log.Error("Error calculating an implied discount spread!", ex);
        spread = double.NaN;
      }

      // Done
      return spread;
    }

    /// <summary>
    /// Calculate implied discount spread
    /// </summary>
    public static double ImpliedDiscountSpread(Dt asOf, Dt settle, 
      ScheduleParams scheduleParams, DayCount dayCount,
      DiscountCurve discountCurve, bool isFloating, 
      DiscountCurve referenceCurve,
      SurvivalCurve survCurve, RecoveryCurve recoveryCurve, 
      bool fullDrawOnDflt, bool includePrepayOption,
      SurvivalCurve prepaymentCurve, VolatilityCurve volatilityCurve, 
      double refiCost, string curLevel,
      int startStateIdx, string[] levels, 
      IDictionary<string, double> pricingGrid, double commFee,
      double[] usage, double[] endDistribution, 
      IList<Amortization> amortizations,
      IList<InterestPeriod> interestPeriods, double fullPrice, 
      bool scaleUsage, double currentUsage, Func<DiscountCurve, 
        double[]> floorFunc, double? currentCouponRate)
    {
      double spread = 0;
      Brent2 solver = new Brent2();
      ImpliedDiscountSpreadSolver f = new ImpliedDiscountSpreadSolver(asOf, 
        settle, scheduleParams, commFee, isFloating, dayCount, curLevel, pricingGrid,
        discountCurve, referenceCurve, survCurve, recoveryCurve, prepaymentCurve, 
        volatilityCurve, refiCost,
        includePrepayOption, endDistribution, startStateIdx, usage, interestPeriods, 
        fullDrawOnDflt, levels, amortizations, scaleUsage, currentUsage, 
        floorFunc, currentCouponRate);

      try
      {
        // Solve
        spread = solver.solve(f, fullPrice, 1E-8, 0.1);
      }
      catch (Exception ex)
      {
        Log.Error("Error calculating an implied discount spread!", ex);
        spread = double.NaN;
      }

      // Done
      return spread;
    }

    private class ImpliedDiscountSpreadSolver : SolverFn
    {
      public ImpliedDiscountSpreadSolver(Dt asOf, Dt settle, ScheduleParams scheduleParams,
        double commFee, bool isFloating,
        DayCount dayCount, string curLevel, IDictionary<string, double> pricingGrid, 
        DiscountCurve dc, DiscountCurve rc,
        SurvivalCurve sc, RecoveryCurve rec, SurvivalCurve ppmtCurve, 
        VolatilityCurve volCurve, double refiCost,
        bool includPpmt, double[] endDist, int curLvl, double[] usage, 
        IList<InterestPeriod> intPer,
        bool fullDrawOnDflt, string[] levels, IList<Amortization> amortizations, 
        bool scaleUsage, double currentUsage, double[] floorValues, 
        double? currentCouponRate)
      {
        asOf_ = asOf;
        settle_ = settle;
        discountCurve_ = dc;
        referenceCurve_ = rc;
        survivalCurve_ = sc;
        recoveryCurve_ = rec;
        prepaymentCurve_ = ppmtCurve;
        volatilityCurve_ = volCurve;
        refinancingCost_ = refiCost;
        includePrepaymentOption_ = includPpmt;
        endDistribution_ = endDist;
        currentLevelIdx_ = curLvl;
        usage_ = usage;
        interestPeriods_ = intPer;
        fullDrawOnDefault_ = fullDrawOnDflt;
        scheduleTerms_ = scheduleParams;
        commFee_ = commFee;
        isFloating_ = isFloating;
        dayCount_ = dayCount;
        curLevel_ = curLevel;
        pricingGrid_ = pricingGrid;
        levels_ = levels;
        amortizations_ = amortizations;
        scaleUsage_ = scaleUsage;
        currentUsage_ = currentUsage;
        floorValues_ = floorValues;
        floorFunc_ = null;
        currentCouponRate_ = currentCouponRate;
      }
      public ImpliedDiscountSpreadSolver(Dt asOf, Dt settle, 
        ScheduleParams scheduleParams, double commFee, bool isFloating,
       DayCount dayCount, string curLevel, IDictionary<string, double> pricingGrid, 
       DiscountCurve dc, DiscountCurve rc, SurvivalCurve sc, RecoveryCurve rec, 
       SurvivalCurve ppmtCurve, VolatilityCurve volCurve, double refiCost,
       bool includPpmt, double[] endDist, int curLvl, double[] usage, 
       IList<InterestPeriod> intPer, bool fullDrawOnDflt, string[] levels, 
       IList<Amortization> amortizations, bool scaleUsage, double currentUsage, 
       Func<DiscountCurve, double[]> floorFunc, double? currentCouponRate)
      {
        asOf_ = asOf;
        settle_ = settle;
        discountCurve_ = dc;
        referenceCurve_ = rc;
        survivalCurve_ = sc;
        recoveryCurve_ = rec;
        prepaymentCurve_ = ppmtCurve;
        volatilityCurve_ = volCurve;
        refinancingCost_ = refiCost;
        includePrepaymentOption_ = includPpmt;
        endDistribution_ = endDist;
        currentLevelIdx_ = curLvl;
        usage_ = usage;
        interestPeriods_ = intPer;
        fullDrawOnDefault_ = fullDrawOnDflt;
        scheduleTerms_ = scheduleParams;
        commFee_ = commFee;
        isFloating_ = isFloating;
        dayCount_ = dayCount;
        curLevel_ = curLevel;
        pricingGrid_ = pricingGrid;
        levels_ = levels;
        amortizations_ = amortizations;
        scaleUsage_ = scaleUsage;
        currentUsage_ = currentUsage;
        floorValues_ = null;
        floorFunc_ = floorFunc;
        currentCouponRate_ = currentCouponRate;
      }

      public override double evaluate(double x)
      {
        double pv = 0;
        double originalSpread = discountCurve_.Spread;
        try
        {
          double df0 = discountCurve_.DiscountFactor(asOf_, settle_);

          // Set spread
          discountCurve_.Spread = originalSpread + x;
          //discountCurve_.ReFit(0);
          if (floorFunc_!=null)
            floorValues_ = floorFunc_(referenceCurve_);

          // Calculate PV
          pv = Pv(asOf_, settle_, scheduleTerms_, dayCount_, isFloating_, curLevel_,
            currentLevelIdx_, levels_, pricingGrid_,
            commFee_, usage_, endDistribution_, discountCurve_, referenceCurve_, 
            survivalCurve_, recoveryCurve_, volatilityCurve_, fullDrawOnDefault_, 
            includePrepaymentOption_, prepaymentCurve_, refinancingCost_, 
            amortizations_, interestPeriods_, CalibrationType.None, 0,
            scaleUsage_, currentUsage_, floorValues_, currentCouponRate_);

          // use the original discount between asOf_ and settle_
          pv *= df0/discountCurve_.DiscountFactor(asOf_, settle_);
        }
        finally
        {
          discountCurve_.Spread = originalSpread;
        }

        // Done
        return pv;
      }

      private readonly Dt asOf_, settle_;
      private readonly DiscountCurve discountCurve_, referenceCurve_;
      private readonly SurvivalCurve survivalCurve_, prepaymentCurve_;
      private readonly VolatilityCurve volatilityCurve_;
      private readonly double refinancingCost_, commFee_;
      private readonly RecoveryCurve recoveryCurve_;
      private readonly double[] endDistribution_, usage_;
      private readonly int currentLevelIdx_;
      private readonly bool includePrepaymentOption_, fullDrawOnDefault_, isFloating_;
      private readonly IList<InterestPeriod> interestPeriods_;
      private readonly ScheduleParams scheduleTerms_;
      private readonly DayCount dayCount_;
      private readonly IDictionary<string, double> pricingGrid_;
      private readonly string curLevel_;
      private readonly string[] levels_;
      private readonly IList<Amortization> amortizations_;
      private readonly bool scaleUsage_;
      private readonly double currentUsage_;
      private double[] floorValues_;
      private Func<DiscountCurve, double[]> floorFunc_;
      private readonly double? currentCouponRate_;
    }
    #endregion

    #region Implied Credit Spread

    /// <summary>
    /// Calculates the spread over the survival curve that causes the model price to match the given price.
    /// </summary>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Pricing settlement date</param>
    /// <param name="scheduleParams">Schedule parameters</param>
    /// <param name="dayCount">The day count</param>
    /// <param name="discountCurve">The interest rate curve for discounting</param>
    /// <param name="isFloating">Whether the coupons are paid as a spread over a floating index</param>
    /// <param name="referenceCurve">The interest rate curve for projecting forward rates</param>
    /// <param name="survCurve">Survival curve</param>
    /// <param name="recoveryCurve">Recovery curve</param>
    /// <param name="fullDrawOnDflt">Whether the Loan is assumed to be fully drawn on a default</param>
    /// <param name="includePrepayOption">Include prepayment option</param>
    /// <param name="prepaymentCurve">Prepayment curve</param>
    /// <param name="volatilityCurve">Volaility curve</param>
    /// <param name="refiCost">The cost of refinancing as a rate (200 bps = 0.02)</param>
    /// <param name="curLevel">The current performance level</param>
    /// <param name="startStateIdx">The current performance level of the Loan</param>
    /// <param name="levels">The performance levels (best to worst; ie last level is closest to default)</param>
    /// <param name="pricingGrid">The coupons or spreads for each performance level</param>
    /// <param name="commFee">The fee paid on undrawn notional</param>
    /// <param name="usage">The expected usage at each performance level</param>
    /// <param name="endDistribution">The probability of being in each performance level at loan maturity</param>
    /// <param name="amortizations">The scheduled amortizations</param>
    /// <param name="interestPeriods">The interest periods for the Loan</param>
    /// <param name="fullPrice">Full loan price</param>
    /// <param name="scaleUsage"></param>
    /// <param name="currentUsage"></param>
    /// <param name="floorValues"></param>
    /// <param name="currentCouponRate">For a floating rate loan, pass the current coupon rate; 
    /// if null passed, the model will compute it based on interestPeriods.</param>
    /// <returns>Implied Credit Spread</returns>
    public static double ImpliedCreditSpread(Dt asOf, Dt settle, 
      ScheduleParams scheduleParams, DayCount dayCount,
      DiscountCurve discountCurve, bool isFloating, DiscountCurve referenceCurve,
      SurvivalCurve survCurve, RecoveryCurve recoveryCurve, 
      bool fullDrawOnDflt, bool includePrepayOption,
      SurvivalCurve prepaymentCurve, VolatilityCurve volatilityCurve, 
      double refiCost, string curLevel,
      int startStateIdx, string[] levels, IDictionary<string, double> pricingGrid, 
      double commFee,
      double[] usage, double[] endDistribution, IList<Amortization> amortizations, 
      IList<InterestPeriod> interestPeriods, double fullPrice, bool scaleUsage, 
      double currentUsage, double[] floorValues, double? currentCouponRate)
    {
      double spread = 0;
      Brent2 solver = new Brent2();
      var f = new ImpliedCreditSpreadSolver(asOf, settle, scheduleParams, 
        commFee, isFloating, dayCount, curLevel, pricingGrid,
                                            discountCurve, referenceCurve, survCurve, recoveryCurve, prepaymentCurve,
        volatilityCurve, refiCost, includePrepayOption, endDistribution, 
        startStateIdx, usage, interestPeriods, fullDrawOnDflt, 
        levels, amortizations, scaleUsage, currentUsage, floorValues, currentCouponRate);

      // Setup solver
      solver.setToleranceF(5E-8);
      solver.setToleranceX(5E-7);

      try
      {
        // Solve
        spread = solver.solve(f, fullPrice, 1E-8, 0.1);
      }
      catch (Exception ex)
      {
        Log.Error("Error calculating an implied credit spread!", ex);
        spread = double.NaN;
      }

      // Done
      return spread;
    }

    private class ImpliedCreditSpreadSolver : SolverFn
    {
      public ImpliedCreditSpreadSolver(Dt asOf, Dt settle, 
        ScheduleParams scheduleParams, double commFee, bool isFloating,
        DayCount dayCount, string curLevel, IDictionary<string, double> pricingGrid, 
        DiscountCurve dc, DiscountCurve rc,
        SurvivalCurve sc, RecoveryCurve rec, SurvivalCurve ppmtCurve, 
        VolatilityCurve volCurve, double refiCost,
        bool includPpmt, double[] endDist, int curLvl, double[] usage, 
        IList<InterestPeriod> intPer,
        bool fullDrawOnDflt, string[] levels, IList<Amortization> amortizations, 
        bool scaleUsage, double currentUsage, double[] floorValues, double? currentCouponRate)
      {
        asOf_ = asOf;
        settle_ = settle;
        discountCurve_ = dc;
        referenceCurve_ = rc;
        survivalCurve_ = sc;
        recoveryCurve_ = rec;
        prepaymentCurve_ = ppmtCurve;
        volatilityCurve_ = volCurve;
        refinancingCost_ = refiCost;
        includePrepaymentOption_ = includPpmt;
        endDistribution_ = endDist;
        currentLevelIdx_ = curLvl;
        usage_ = usage;
        interestPeriods_ = intPer;
        fullDrawOnDefault_ = fullDrawOnDflt;
        scheduleTerms_ = scheduleParams;
        commFee_ = commFee;
        isFloating_ = isFloating;
        dayCount_ = dayCount;
        curLevel_ = curLevel;
        pricingGrid_ = pricingGrid;
        levels_ = levels;
        amortizations_ = amortizations;
        scaleUsage_ = scaleUsage;
        currentUsage_ = currentUsage;
        floorValues_ = floorValues;
        currentCouponRate_ = currentCouponRate;
      }

      public override double evaluate(double x)
      {
        double pv = 0;
        double originalSpread = survivalCurve_.Spread;

        try
        {
          // Set spread
          survivalCurve_.Spread = originalSpread + x;

          // Calculate PV
          pv = Pv(asOf_, settle_, scheduleTerms_, dayCount_,
                  isFloating_, curLevel_, currentLevelIdx_, levels_, pricingGrid_,
                  commFee_, usage_, endDistribution_, discountCurve_, referenceCurve_, survivalCurve_,
                  recoveryCurve_, volatilityCurve_, fullDrawOnDefault_, includePrepaymentOption_,
                  prepaymentCurve_,
            refinancingCost_, amortizations_, interestPeriods_, CalibrationType.None, 
            0, scaleUsage_, currentUsage_,
            floorValues_, currentCouponRate_);
        }
        finally
        {
          survivalCurve_.Spread = originalSpread;
        }

        // Done
        return pv;
      }

      private readonly Dt asOf_, settle_;
      private readonly DiscountCurve discountCurve_, referenceCurve_;
      private readonly SurvivalCurve survivalCurve_, prepaymentCurve_;
      private readonly VolatilityCurve volatilityCurve_;
      private readonly double refinancingCost_, commFee_;
      private readonly RecoveryCurve recoveryCurve_;
      private readonly double[] endDistribution_, usage_;
      private readonly int currentLevelIdx_;
      private readonly bool includePrepaymentOption_, fullDrawOnDefault_, isFloating_;
      private readonly IList<InterestPeriod> interestPeriods_;
      private readonly ScheduleParams scheduleTerms_;
      private readonly DayCount dayCount_;
      private readonly IDictionary<string, double> pricingGrid_;
      private readonly string curLevel_;
      private readonly string[] levels_;
      private readonly IList<Amortization> amortizations_;
      private readonly bool scaleUsage_;
      private readonly double currentUsage_;
      private readonly double[] floorValues_;
      private readonly double? currentCouponRate_;
    }
    #endregion

    #region Implied Credit Curve
    /// <summary>
    /// Calculates the CDS curve that causes the model price to match the given price.
    /// </summary>
    /// 
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Pricing settlement date</param>
    /// <param name="scheduleParams">Schedule parameters</param>
    /// <param name="dayCount">The day count</param>
    /// <param name="discountCurve">The interest rate curve for discounting</param>
    /// <param name="isFloating">Whether the coupons are paid as a spread over a floating index</param>
    /// <param name="referenceCurve">The interest rate curve for projecting forward rates</param>
    /// <param name="recoveryCurve">Recovery curve</param>
    /// <param name="fullDrawOnDflt">Whether the Loan is assumed to be fully drawn on a default</param>
    /// <param name="prepaymentCurve">Prepayment curve</param>
    /// <param name="volatilityCurve">Volaility curve</param>
    /// <param name="refiCost">The cost of refinancing as a rate (200 bps = 0.02)</param>
    /// <param name="curLevel">The current performance level</param>
    /// <param name="startStateIdx">The current performance level of the Loan</param>
    /// <param name="levels">The performance levels (best to worst; ie last level is closest to default)</param>
    /// <param name="pricingGrid">The coupons or spreads for each performance level</param>
    /// <param name="commFee">The fee paid on undrawn notional</param>
    /// <param name="usage">The expected usage at each performance level</param>
    /// <param name="endDistribution">The probability of being in each performance level at loan maturity</param>
    /// <param name="amortizations">The scheduled amortizations</param>
    /// <param name="interestPeriods">The interest periods for the Loan</param>
    /// <param name="fullPrice">Full loan price</param>
    /// <param name="floorValues"></param>
    /// <param name="currentCouponRate">For a floating rate loan, pass the current coupon rate; 
    /// if null passed, the model will compute it based on interestPeriods.</param>
    /// <returns>SurvivalCurve</returns>
    public static SurvivalCurve ImpliedCDSCurve(Dt asOf, Dt settle, 
      ScheduleParams scheduleParams, DayCount dayCount,
      DiscountCurve discountCurve, bool isFloating, DiscountCurve referenceCurve,
      RecoveryCurve recoveryCurve, bool fullDrawOnDflt,
      SurvivalCurve prepaymentCurve, VolatilityCurve volatilityCurve, 
      double refiCost, string curLevel, int startStateIdx, string[] levels,
      IDictionary<string, double> pricingGrid, double commFee, double[] usage,
      double[] endDistribution, IList<Amortization> amortizations,
      IList<InterestPeriod> interestPeriods, double fullPrice, 
      double[] floorValues, double? currentCouponRate)
    {
      return ImpliedCDSCurve(asOf, settle, scheduleParams, dayCount,
                                 discountCurve, isFloating, referenceCurve,
                                 recoveryCurve, fullDrawOnDflt,
                                 prepaymentCurve, volatilityCurve, refiCost, curLevel, startStateIdx, levels,
                                 pricingGrid, commFee, usage, endDistribution, amortizations,
                                 interestPeriods, fullPrice, true, -1, floorValues, currentCouponRate);
    }
    
    /// <summary>
    /// Calculates the CDS curve that causes the model price to match the given price.
    /// </summary>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Pricing settlement date</param>
    /// <param name="scheduleParams">Schedule parameters</param>
    /// <param name="dayCount">The day count</param>
    /// <param name="discountCurve">The interest rate curve for discounting</param>
    /// <param name="isFloating">Whether the coupons are paid as a spread over a floating index</param>
    /// <param name="referenceCurve">The interest rate curve for projecting forward rates</param>
    /// <param name="recoveryCurve">Recovery curve</param>
    /// <param name="fullDrawOnDflt">Whether the Loan is assumed to be fully drawn on a default</param>
    /// <param name="prepaymentCurve">Prepayment curve</param>
    /// <param name="volatilityCurve">Volaility curve</param>
    /// <param name="refiCost">The cost of refinancing as a rate (200 bps = 0.02)</param>
    /// <param name="curLevel">The current performance level</param>
    /// <param name="startStateIdx">The current performance level of the Loan</param>
    /// <param name="levels">The performance levels (best to worst; ie last level is closest to default)</param>
    /// <param name="pricingGrid">The coupons or spreads for each performance level</param>
    /// <param name="commFee">The fee paid on undrawn notional</param>
    /// <param name="usage">The expected usage at each performance level</param>
    /// <param name="endDistribution">The probability of being in each performance level at loan maturity</param>
    /// <param name="amortizations">The scheduled amortizations</param>
    /// <param name="interestPeriods">The interest periods for the Loan</param>
    /// <param name="fullPrice">Full loan price</param>
    /// <param name="useDrawnNotional"></param>
    /// <param name="curUsage"></param>
    /// <param name="floorValues"></param>
    /// <param name="currentCouponRate">For a floating rate loan, pass the current coupon rate; 
    /// if null passed, the model will compute it based on interestPeriods.</param>
    /// <returns>SurvivalCurve</returns>
    public static SurvivalCurve ImpliedCDSCurve(Dt asOf, Dt settle, 
      ScheduleParams scheduleParams, DayCount dayCount,
      DiscountCurve discountCurve, bool isFloating, DiscountCurve referenceCurve,
      RecoveryCurve recoveryCurve, bool fullDrawOnDflt,
      SurvivalCurve prepaymentCurve, VolatilityCurve volatilityCurve, 
      double refiCost, string curLevel, int startStateIdx, string[] levels,
      IDictionary<string, double> pricingGrid, double commFee, double[] usage, 
      double[] endDistribution, IList<Amortization> amortizations,
      IList<InterestPeriod> interestPeriods, double fullPrice, bool useDrawnNotional, 
      double curUsage, double[] floorValues, double? currentCouponRate)
    {
      Brent2 solver = new Brent2();

      // Create initial survival curve
      SurvivalFitCalibrator calibrator = new SurvivalFitCalibrator(asOf, 
        settle, recoveryCurve, discountCurve);
      SurvivalCurve survCurve = new SurvivalCurve(calibrator);
      survCurve.AddCDS(Dt.CDSMaturity(asOf, "5 Year"), 0.0, CurveUtil.DEFAULT_DAYCOUNT, 
        CurveUtil.DEFAULT_FREQUENCY, CurveUtil.DEFAULT_ROLL, CurveUtil.DEFAULT_CALENDAR);
      survCurve.Fit();

      // Setup target function
      ImpliedCDSCurveSolver f = new ImpliedCDSCurveSolver(asOf, settle,
                                                          discountCurve, referenceCurve, survCurve, recoveryCurve,
                                                          prepaymentCurve, volatilityCurve, refiCost,
                                                          endDistribution, startStateIdx, usage,
                                                          interestPeriods, fullDrawOnDflt,
                                                          scheduleParams,
        dayCount, commFee, isFloating, pricingGrid, curLevel, levels, 
        amortizations, useDrawnNotional, curUsage,
        floorValues, currentCouponRate);

      // Setup solver
      solver.setToleranceF(5E-8);
      solver.setToleranceX(5E-7);

      double hazardRate = 0;
      try
      {
        // Solve
        hazardRate = solver.solve(f, fullPrice, 1E-8, 0.1);
      }
      catch (Exception ex)
      {
        Log.Error("Error calibrating an implied CDS curve!", ex);
        return null;
      }

      // Synchronize the curve quote
      return f.GetFittedCurve(hazardRate);
    }

    private class ImpliedCDSCurveSolver : SolverFn
    {
      public ImpliedCDSCurveSolver(Dt asOf, Dt settle, DiscountCurve dc, DiscountCurve rc,
        SurvivalCurve sc, RecoveryCurve rec, SurvivalCurve ppmtCurve, 
        VolatilityCurve volCurve, double refiCost,
        double[] endDist, int curLvl, double[] usage, IList<InterestPeriod> intPer,
        bool fullDrawOnDflt,
        ScheduleParams scheduleParams, 
        DayCount dayCount,
        double commFee,
        bool isFloating,
        IDictionary<string, double> pricingGrid,
        string curLevel,
        string[] levels,
        IList<Amortization> amortizations,
        bool useDrawnNotional,
        double curUsage,
        double[] floorValues,
        double? currentCouponRate)
      {

        asOf_ = asOf;
        settle_ = settle;
        discountCurve_ = dc;
        referenceCurve_ = rc;
        survivalCurve_ = sc;
        recoveryCurve_ = rec;
        prepaymentCurve_ = ppmtCurve;
        volatilityCurve_ = volCurve;
        refinancingCost_ = refiCost;
        endDistribution_ = endDist;
        currentLevelIdx_ = curLvl;
        usage_ = usage;
        interestPeriods_ = intPer;
        fullDrawOnDefault_ = fullDrawOnDflt;
        scheduleTerms_ = scheduleParams;
        commFee_ = commFee;
        isFloating_ = isFloating;
        dayCount_ = dayCount;
        curLevel_ = curLevel;
        pricingGrid_ = pricingGrid;
        levels_ = levels;
        amortizations_ = amortizations;
        useDrawnNotional_ = useDrawnNotional;
        curUsage_ = curUsage;
        floorValues_ = floorValues;
        currentCouponRate_ = currentCouponRate;
      }

      public override double evaluate(double x)
      {
        // Shift survival Curve hazard rate
        survivalCurve_.Spread = x;

        // Calculate PV
        double bondPv = Pv(asOf_, settle_, scheduleTerms_, dayCount_,
          isFloating_, curLevel_, currentLevelIdx_, levels_, pricingGrid_,
          commFee_, usage_, endDistribution_, discountCurve_, referenceCurve_, survivalCurve_,
          recoveryCurve_, volatilityCurve_, fullDrawOnDefault_, false, prepaymentCurve_,
          refinancingCost_, amortizations_, interestPeriods_, CalibrationType.None, 
          0, useDrawnNotional_, curUsage_, floorValues_, currentCouponRate_);

        // Calculate PV of Option
        double loanPv;
        try
        {
          loanPv = Pv(asOf_, settle_, scheduleTerms_, dayCount_,
                                isFloating_, curLevel_, currentLevelIdx_, levels_, pricingGrid_,
                                commFee_, usage_, endDistribution_, discountCurve_, referenceCurve_, survivalCurve_,
                                recoveryCurve_, volatilityCurve_, fullDrawOnDefault_, true, prepaymentCurve_,
            refinancingCost_, amortizations_, interestPeriods_, CalibrationType.None, 0,
            useDrawnNotional_, curUsage_, floorValues_, currentCouponRate_);
        }
        catch
        {
          loanPv = 0;
        }

        // Done
        if(ToolkitConfigurator.Settings.LoanModel.AllowNegativeOptionValue)
        {
          return loanPv;
        }
        else
        {
          return bondPv - Math.Max(0, bondPv-loanPv); // Loan PV
        }
      }

      internal SurvivalCurve GetFittedCurve(double hazardSpread)
      {
        // Calculate the implied spread.
        SurvivalCurve sc = survivalCurve_;
        sc.Spread = hazardSpread;
        Dt maturity = Dt.CDSMaturity(asOf_, "5 Year");
        double impliedSpread = CurveUtil.ImpliedSpread(sc, maturity);

        // Fit a new curve with the same implied spread.
        SurvivalFitCalibrator calibrator = new SurvivalFitCalibrator(asOf_, 
          settle_, recoveryCurve_, discountCurve_) {ForceFit = true};
        SurvivalCurve survCurve = new SurvivalCurve(calibrator);
        survCurve.AddCDS(maturity, impliedSpread, CurveUtil.DEFAULT_DAYCOUNT, 
          CurveUtil.DEFAULT_FREQUENCY, CurveUtil.DEFAULT_ROLL, CurveUtil.DEFAULT_CALENDAR);
        survCurve.Fit();

        // Done
        return survCurve;
      }

      private readonly Dt asOf_, settle_;
      private readonly DiscountCurve discountCurve_, referenceCurve_;
      private readonly SurvivalCurve survivalCurve_, prepaymentCurve_;
      private readonly VolatilityCurve volatilityCurve_;
      private readonly double refinancingCost_, commFee_;
      private readonly RecoveryCurve recoveryCurve_;
      private readonly double[] endDistribution_, usage_;
      private readonly int currentLevelIdx_;
      private readonly bool fullDrawOnDefault_, isFloating_;
      private readonly IList<InterestPeriod> interestPeriods_;
      private readonly ScheduleParams scheduleTerms_;
      private readonly DayCount dayCount_;
      private readonly IDictionary<string, double> pricingGrid_;
      private readonly string curLevel_;
      private readonly string[] levels_;
      private readonly IList<Amortization> amortizations_;
      private readonly bool useDrawnNotional_;
      private readonly double curUsage_;
      private readonly double[] floorValues_;
      private readonly double? currentCouponRate_;
    }
    #endregion

    #region Implied Prepayment Curve
    /// <summary>
    /// Calculates the prepayment curve that causes the expected WAL to match a given WAL.
    /// </summary>
    /// 
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Pricing settlement date</param>
    /// <param name="scheduleParams">Schedule parameters</param>
    /// <param name="drawn">The amount drawn on the settlement date</param>
    /// <param name="targetWAL">The target WAL</param>
    /// <param name="dayCount">The day count</param>
    /// <param name="amortizations">The amortization schedule</param>
    /// <param name="ccy">The currency that coupon and principal payments will be made in</param>
    /// 
    /// <returns>SurvivalCurve</returns>
    /// 
    public static SurvivalCurve ImpliedPrepaymentCurve(Dt asOf, Dt settle, ScheduleParams scheduleParams, 
      Currency ccy, DayCount dayCount, IList<Amortization> amortizations, double drawn, double targetWAL)
    {
      double spread = 0;
      Brent2 solver = new Brent2();
      SurvivalCurve prepaymentCurve = null;

      // Setup target function
      var f = new ImpliedPrepaymentCurveSolver(settle, scheduleParams, ccy,
        dayCount, amortizations, drawn);

      // Setup solver
      solver.setLowerBounds(0);
      solver.setToleranceF(1e-8);
      solver.setToleranceX(1e-8);

      try
      {
        // Solve
        spread = solver.solve(f, targetWAL, 1E-8, 0.1);
      }
      catch (Exception ex)
      {
        Log.Error("Error calibrating an implied prepayment curve!", ex);
        spread = double.NaN;
      }

      if (!double.IsNaN(spread))
      {
        // build flat refi curve w single annual refinancing probability
        Dt[] tenorDates = new [] { Dt.Add(settle, 1, TimeUnit.Years) };
        string[] tenorNames = new [] { "1Y" };
        double[] nonRefiProbs = new [] { 1.0 - spread };
        prepaymentCurve = SurvivalCurve.FromProbabilitiesWithBond(settle, 
          Currency.None, null, InterpMethod.Weighted, ExtrapMethod.Const, tenorDates, nonRefiProbs, 
          tenorNames, null, null, null, 0);
      }

      // Done
      return prepaymentCurve;
    }

    private class ImpliedPrepaymentCurveSolver : SolverFn
    {
      public ImpliedPrepaymentCurveSolver(Dt asOf, ScheduleParams scheduleParams, Currency ccy,
        DayCount dayCount, IList<Amortization> amortizations, double drawn)
      {
        asOf_ = asOf;
        ccy_ = ccy;
        dayCount_ = dayCount;
        amortizations_ = amortizations;
        drawn_ = drawn;
        scheduleTerms_ = scheduleParams;

        // Single annual refinancing probability
        tenorDates_ = new [] { Dt.Add(asOf_, 1, TimeUnit.Years) };
        tenorNames_ = new [] { "1Y" };
      }

      public override double evaluate(double x)
      {
        double wal = 0;

        // build flat refi curve
        SurvivalCurve prepaymentCurve = SurvivalCurve.FromProbabilitiesWithBond(
          asOf_, Currency.None, null, InterpMethod.Weighted,
          ExtrapMethod.Const, tenorDates_, new [] { 1.0 - x }, tenorNames_, null, null, null, 0);

        // Calculate PV
        wal = ExpectedWAL(asOf_, scheduleTerms_, ccy_, dayCount_, drawn_, 
          amortizations_, prepaymentCurve);

        // Done
        return wal;
      }

      private readonly Dt asOf_;
      private readonly ScheduleParams scheduleTerms_;
      private readonly Currency ccy_;
      private readonly DayCount dayCount_;
      private readonly IList<Amortization> amortizations_;
      private readonly double drawn_;
      private readonly Dt[] tenorDates_;
      private readonly string[] tenorNames_;
    }
    #endregion

    #region Display

    /// <summary>
    /// </summary>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Pricing settlement date</param>
    /// <param name="scheduleParams">Schedule parameters</param>
    /// <param name="dayCount">The day count</param>
    /// <param name="isFloating">Whether the coupons are paid as a spread over a floating index</param>
    /// <param name="curLevel">The current performance level</param>
    /// <param name="startStateIdx">The current performance level of the Loan</param>
    /// <param name="pricingGrid">The coupons or spreads for each performance level</param>
    /// <param name="commFee">The fee paid on undrawn notional</param>
    /// <param name="usage">The expected usage at each performance level</param>
    /// <param name="endDistribution">The probability of being in each performance level at loan maturity</param>
    /// <param name="levels">The performance levels (best to worst; ie last level is closest to default)</param>
    /// <param name="discountCurve">The interest rate curve for discounting</param>
    /// <param name="referenceCurve">The interest rate curve for projecting forward rates</param>
    /// <param name="survCurve">Survival curve</param>
    /// <param name="recoveryCurve">Recovery curve</param>
    /// <param name="volatilityCurve">Volaility curve</param>
    /// <param name="fullDrawOnDefault">Whether the Loan is assumed to be fully drawn on a default</param>
    /// <param name="includePrepayOption">Include prepayment option</param>
    /// <param name="prepaymentCurve">Prepayment curve</param>
    /// <param name="refiCost">The cost of refinancing as a rate (200 bps = 0.02)</param>
    /// <param name="amortizations">The scheduled amortizations</param>
    /// <param name="interestPeriods">The interest periods for the Loan</param>
    /// <param name="calType">Calibration type</param>
    /// <param name="calSpread"></param>
    /// <param name="scaleUsage"></param>
    /// <param name="floorValues"></param>
    /// <param name="currentCouponRate">For a floating rate loan, pass the current coupon rate; 
    /// if null passed, the model will compute it based on interestPeriods.</param>
    /// <returns>Generated expected cashflow</returns>
    public static double[,] ExpectedCashflows(Dt asOf, Dt settle, 
      ScheduleParams scheduleParams, DayCount dayCount, bool isFloating,
      string curLevel, int startStateIdx, string[] levels,
      IDictionary<string, double> pricingGrid, double commFee, double[] usage, 
      double[] endDistribution,
      DiscountCurve discountCurve, DiscountCurve referenceCurve, SurvivalCurve survCurve,
      RecoveryCurve recoveryCurve, VolatilityCurve volatilityCurve, 
      bool fullDrawOnDefault, bool includePrepayOption, 
      SurvivalCurve prepaymentCurve,
      double refiCost, IList<Amortization> amortizations, 
      IList<InterestPeriod> interestPeriods,
      CalibrationType calType, double calSpread, bool scaleUsage, 
      double[] floorValues, double? currentCouponRate)
    {
      Func<int, int, double> coupon;
      Func<int, int, double> use;
      Func<int, int, double> accrual;
      double[,] V;
      double origSpread = 0;

      // Handle a known default
      if (survCurve.DefaultDate != Dt.Empty)
      {
        // Handle default before settlement date
        if (survCurve.DefaultDate < settle)
          return null;

        // Ignore default in the future!
        if (survCurve.DefaultDate == settle)
        {
          double df = discountCurve.DiscountFactor(asOf, settle);
          double recovery = df * recoveryCurve.Interpolate(survCurve.DefaultDate);
          return null;
        }
      }

      List<Dt> pricingDatesList;
      GetCouponAccrualUse(asOf, settle, scheduleParams, dayCount, isFloating, 
        curLevel, startStateIdx, levels, pricingGrid, commFee, usage, 
        discountCurve, referenceCurve, amortizations, interestPeriods, 
        out coupon, out use, out accrual, out pricingDatesList, floorValues, 
        currentCouponRate);

      if (calType == CalibrationType.DiscountCurve)
      {
        origSpread = discountCurve.Spread;
        discountCurve.Spread += calSpread;
      }
      else if (calType == CalibrationType.SurvivalCurve)
      {
        origSpread = survCurve.Spread;
        survCurve.Spread += calSpread;
      }

      try
      {
        // Calculate the distribution across the credit quality states
        StateDistribution distribution = StateDistribution.Calculate(
          (ToolkitConfigurator.Settings.LoanModel.UseSettleForPricing ? settle : asOf),
          survCurve,
          (includePrepayOption ? prepaymentCurve : null),
          volatilityCurve,
          pricingDatesList.ToArray(),
          endDistribution,
          startStateIdx);

        // Calculate value matrix
        V = RecursiveCashflows(coupon, accrual, use, discountCurve, 
          recoveryCurve, refiCost, distribution, fullDrawOnDefault, 
          includePrepayOption);
      }
      finally
      {
        // Restore shifted curve
        if (calType == CalibrationType.DiscountCurve)
          discountCurve.Spread = origSpread;
        else if (calType == CalibrationType.SurvivalCurve)
          survCurve.Spread = origSpread;
      }

      // Done
      return V;
    }
    /// <summary>
    /// expected cash flow
    /// </summary>
    public static double[,] RecursiveCashflows(
      Func<int, int, double> coupon,
      Func<int, int, double> accrual,
      Func<int, int, double> use,
      DiscountCurve discountCurve,
      RecoveryCurve recoveryCurve,
      double fr,
      StateDistribution distributions,
      bool fullDrawOnDefault,
      bool allowPrepayment)
    {
      
      // Dimensions.
      //   (1) lastdate is the index of the last date;
      //   (2) nstates is the number of states excluding
      //       default and refinance.
      int lastdate = distributions.DateCount - 1;
      int nstates = distributions.StateCount;
      int cfCount = 11;
      Debug.Assert(lastdate > 0 && nstates > 0);

      // 2-D array of the values by dates and states.
      double[,] V = new double[lastdate + 1, cfCount];

      // Usages and coupons and accruals for the period [t, t+1].
      double[] Uprev = new double[nstates]; // previous  notional
      double[] U = new double[nstates];
      double[] C = new double[nstates];
      double[] A = new double[nstates];

      // For all the dates before maturity.
      Dt prevDate = distributions.GetDate(0);
      for (int t = 0; t < lastdate; ++t)
      {
        for (int i = 0; i < nstates; ++i)
          U[i] = use(t, distributions.Level(i));

        // Get the coupons for period [t,t+1],
        for (int i = 0; i < nstates; ++i)
          C[i] = coupon(t, distributions.Level(i));

        // Get the accruals for priod [t, t+1]
        for (int i = 0; i < nstates; ++i)
          A[i] = accrual == null ? 0 : accrual(t, distributions.Level(i));

        // If the company default in period [t-1, t]....
        Dt current = distributions.GetDate(t);
        Dt dfltDate = DefaultDt(prevDate, current, 0.5);
        double recoveryRate = recoveryCurve.Interpolate(dfltDate);

        V[t, 0] = prevDate.ToDateTime().ToOADate();
        V[t, 1] = current.ToDateTime().ToOADate();
        V[t, 2] = current.ToDateTime().ToOADate();
        V[t, 7] = discountCurve.DiscountFactor(distributions.GetDate(0), current);
        V[t, 8] = 1 - distributions.DefaultProbability(t);

        // Suppose at t the company is in state i.
        for (int i = 0; i < nstates; ++i)
        {
          // "Coupon", "Principal", "Interest", "Notional", "Discount", "Survival", "Recovery", "Prepayment"
          //      3          4           5           6            7         8           9           10
          V[t, 3] += (C[i] + A[i]) * distributions.Probability(t , i);
          V[t, 4] += U[i] * (distributions.Probability(t, i) 
            - distributions.Probability(t+1, i));
          V[t, 5] += (C[i] + A[i]) * distributions.Probability(t, i);
          V[t, 6] += U[i] * distributions.Probability(t, i);
          V[t, 9] += recoveryRate * (C[i] + A[i] + U[i]) 
            * distributions.Probability(t, i) * distributions.JumpToDefault(t , i);
          V[t, 10] += (C[i] + A[i] + U[i] + fr) * distributions.Probability(t, i) 
            * distributions.JumpToPrepay(t , i);
        }
        prevDate = current;
      }

      return V;
    }
    #endregion
  } // class LoanModel
}
