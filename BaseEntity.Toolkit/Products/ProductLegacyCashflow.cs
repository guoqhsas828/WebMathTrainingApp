using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Products
{
  #region BasketCDS
  public partial class BasketCDS
  {
    /// <summary>
    /// to be deleted
    /// </summary>
    /// <param name="asOf"></param>
    /// <param name="rateResets"></param>
    /// <returns></returns>
    public IDictionary<Dt, RateResets.ResetInfo> GetResetInfo(Dt asOf, RateResets rateResets)
    {
      IDictionary<Dt, RateResets.ResetInfo> allInfo = new Dictionary<Dt, RateResets.ResetInfo>();
      Cashflow cf = GenerateCashflow(null, Effective, Effective, new DiscountCurve(Effective, 0.0), rateResets.ToList(), null, null);

      for (int i = 0; i < cf.Count; i++)
      {
        RateResetState state;
        Dt reset = cf.GetStartDt(i);
        double rate = RateResetUtil.FindRateAndReportState(reset, asOf, rateResets, out state);
        RateResets.ResetInfo rri = new RateResets.ResetInfo(reset, rate, state);
        allInfo[reset] = rri;
      }
      return allInfo;
    }

    /// <summary>
    ///   Helper function to generate cashflows for CDX
    /// </summary>
    public Cashflow GenerateCashflow(Cashflow cashflow, Dt from, Dt settle, DiscountCurve referenceCurve, IList<RateReset> rateResets, SurvivalCurve[] survivalCurves,
      RecoveryCurve[] recoveryCurves)
    {
      cashflow = CDXPricerUtil.GenerateCashflow(cashflow, from, settle, Effective, FirstPrem, Maturity, Dt.Empty, Ccy,
        Premium, DayCount, Freq, CycleRule, BDConvention, Calendar, BasketCdsType != BasketCDSType.Unfunded,
        1.0, referenceCurve, BasketCdsType == BasketCDSType.FundedFloating, rateResets, survivalCurves, Weights, recoveryCurves);
      if (Bullet != null)
      {
        cashflow.AddMaturityPayment(Bullet.CouponRate, 0, 0, 0);
      }
      return cashflow;
    }
  }

  #endregion BasketCDS

  #region Bond
  public partial class Bond
  {
    /// <summary>
    /// Helper method for generating cashflows from raw data elements.
    /// </summary>
    /// <param name="cashflow">The cashflow.</param>
    /// <param name="settle">Cashflow cut off</param>
    /// <param name="from">From date for cashflow generation</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="referenceCurve">The reference curve.</param>
    /// <param name="rateResets">The rate resets.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="defaultDate">The default date.</param>
    /// <param name="dfltSettle">The default settlement date (should be typically after the default date)</param>
    /// <param name="ignoreExDivs">Ignore ex-div dates in cashflow</param>
    /// <returns>Generated cashflow</returns>
    public Cashflow GenerateCashflow(Cashflow cashflow, Dt settle, Dt from,
      DiscountCurve discountCurve, DiscountCurve referenceCurve,
      RateResets rateResets, double recoveryRate, Dt defaultDate, Dt dfltSettle, bool ignoreExDivs)
    {
      const double fee = 0.0;
      Dt feeSettle = Dt.Empty;
      const double principal = 1.0;
      if (EnableNewCashflow)
      {
        if (discountCurve == null) discountCurve = new DiscountCurve(from, 0.0);
        cashflow = GenerateNewCashflow(this, cashflow, settle, from, discountCurve,
          referenceCurve, rateResets, recoveryRate, defaultDate, dfltSettle, ignoreExDivs);
      }
      else if (Floating)
      {
        cashflow = CashflowFactory.FillFloat(cashflow, from, this, Ccy, DayCount,
                                             Coupon, CouponSchedule,
                                             principal, AmortizationSchedule,
                                             referenceCurve, rateResets.ToBackwardCompatibleList(Effective),
                                             recoveryRate, Ccy, defaultDate,
                                             fee, feeSettle);
      }
      else
      {
        cashflow = CashflowFactory.FillFixed(cashflow, from, this, Ccy, DayCount,
                                             Coupon, CouponSchedule,
                                             principal, AmortizationSchedule,
                                             recoveryRate, Ccy, defaultDate,
                                             fee, feeSettle);
      }
      var finalRedemption = FinalRedemptionValue;
      if (!finalRedemption.AlmostEquals(1.0) && cashflow.Count > 0)
      {
        var last = cashflow.Count - 1;
        if (cashflow.GetDt(last) >= Maturity)
        {
          cashflow.Set(last, finalRedemption * principal,
            cashflow.GetAccrued(last), cashflow.GetDefaultAmount(last));
        }
      }
      return cashflow;
    }

    private Cashflow GenerateNewCashflow(Bond bond,
      Cashflow cf, Dt settle, Dt from,
      DiscountCurve discountCurve,
      DiscountCurve referenceCurve, RateResets rateResets,
      double recoveryRate, Dt defaultDate, Dt dfltSettle, bool ignoreExDivs)
    {
      PaymentSchedule paymentSchedule = bond.GetPaymentSchedule(
        null, @from, Dt.Empty, discountCurve, referenceCurve,
        rateResets, defaultDate, dfltSettle, recoveryRate, ignoreExDivs);
      cf = PaymentScheduleUtils.FillCashflow(cf, paymentSchedule, settle, bond.Notional, recoveryRate);
      cf.DayCount = bond.DayCount;
      cf.Frequency = bond.Freq;
      cf.Currency = bond.Ccy;
      cf.DefaultCurrency = bond.Ccy;
      return cf;
    }
  }
  #endregion Bond

  #region CapBase

  public partial class CapBase
  {
    /// <summary>
    ///   Helper method for generating cashflows from raw data elements.
    /// </summary>
    ///
    /// <param name="cashflow"></param>
    /// <param name="from">generate cashflows from this date forward</param>
    /// <param name="rateResets"></param>
    /// <param name="asOf">pricing date (used to judge state of rate resets)</param>
    ///
    /// <returns>Generated cashflow</returns>
    ///
    public Cashflow GenerateCashflow(Cashflow cashflow, Dt from, RateResets rateResets, Dt asOf)
    {
      var caplets = GetPaymentSchedule(asOf, rateResets);
      Cashflow cf = new Cashflow(from);

      // Go through the caplets and add payments
      foreach (CapletPayment caplet in caplets)
      {
        if (caplet.PayDt >= from)
        {
          // Force resets
          if (caplet.RateResetState == RateResetState.Missing)
            throw new ToolkitException(String.Format("Missing Rate Reset for {0}", caplet.Expiry));

          // Add CF
          cf.Add(caplet.RateFixing, caplet.TenorDate, caplet.PayDt, caplet.PeriodFraction, caplet.Notional,
            caplet.Amount, 0, 0, 0);
        }
      }

      // Done 
      return cf;
    }
  }

  #endregion CapBase

  #region Loan

  public partial class Loan
  {
    /// <summary>
    /// Generates cashflows for a Loan.
    /// </summary>
    /// <param name="cashflow">Cashflow to fill.</param>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Pricing settlement date</param>
    /// <param name="from">Date to start filling from</param>
    /// <param name="curLevel">Current performance level</param>
    /// <param name="drawn">Current drawn percentage</param>
    /// <param name="interestPeriods">Current and (optionally) historical interest periods</param>
    /// <param name="referenceCurve">Interest rate curve for projecting forward rates</param>
    /// <returns>Cashflow</returns>
    public Cashflow GenerateCashflow(Cashflow cashflow, Dt asOf, Dt settle, Dt from, string curLevel, double drawn, IList<InterestPeriod> interestPeriods, CalibratedCurve referenceCurve)
    {
      return GenerateCashflow(cashflow, asOf, settle, from, curLevel, drawn, interestPeriods, referenceCurve, null, null);

    }

    /// <summary>
    /// Generates cashflows for a Loan.
    /// </summary>
    /// <param name="cashflow">Cashflow to fill.</param>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Pricing settlement date</param>
    /// <param name="from">Date to start filling from</param>
    /// <param name="curLevel">Current performance level</param>
    /// <param name="drawn">Current drawn percentage</param>
    /// <param name="interestPeriods">Current and (optionally) historical interest periods</param>
    /// <param name="referenceCurve">Interest rate curve for projecting forward rates</param>
    /// <param name="loanSchedule">Loan schedule</param>
    /// <param name="floorValues">Floor values</param>
    /// <returns>Generated cashflow</returns>
    public Cashflow GenerateCashflow(Cashflow cashflow, Dt asOf, Dt settle, Dt from, string curLevel, double drawn, IList<InterestPeriod> interestPeriods,
      CalibratedCurve referenceCurve, Schedule loanSchedule, double[] floorValues)
    {
      ScheduleParams scParams = GetScheduleParams();
      Cashflow cf = GenerateCashflow(cashflow, asOf, settle, from, curLevel, drawn, interestPeriods, referenceCurve, loanSchedule, floorValues, scParams);
      return cf;
    }

    /// <summary>
    /// Generates cashflows for a Loan.
    /// </summary>
    /// <param name="cashflow">Cashflow to fill.</param>
    /// <param name="from">Date to start filling from</param>
    /// <param name="curLevel">Current performance level</param>
    /// <param name="drawn">Current drawn percentage</param>
    /// <param name="interestPeriods">Current and (optionally) historical interest periods</param>
    /// <param name="referenceCurve">Interest rate curve for projecting forward rates</param>
    /// <param name="loanSchedule">Loan schedule</param>
    /// <param name="floorValues">Floor values</param>
    /// <param name="scParams">Schedule parameters (to maturity or to a call date)</param>
    /// <returns>Generated cashflow</returns>
    internal Cashflow GenerateCashflow(Cashflow cashflow, Dt asOf, Dt settle, Dt from,
      string curLevel, double drawn, IList<InterestPeriod> interestPeriods,
      CalibratedCurve referenceCurve, Schedule loanSchedule, double[] floorValues,
      ScheduleParams scParams)
    {
      Dt defaultDate = Dt.Empty;
      Cashflow cf = GenerateCashflow(cashflow, asOf, settle, from, curLevel, drawn,
        interestPeriods, referenceCurve, loanSchedule, floorValues, scParams,
        defaultDate);
      return cf;
    }

    internal Cashflow GenerateCashflow(Cashflow cashflow, Dt asOf, Dt settle,
      Dt from, string curLevel, double drawn, IList<InterestPeriod> interestPeriods,
      CalibratedCurve referenceCurve, Schedule loanSchedule,
      double[] floorValues, ScheduleParams scParams, Dt defaultDate)
    {
      Currency ccy = Ccy;
      DayCount dayCount = DayCount;
      const double fee = 0.0;
      Dt feeSettle = Dt.Empty;
      double coupon = PricingGrid[curLevel];
      IList<CouponPeriod> couponSchedule = null;
      const double principal = 1.0;
      IList<Amortization> amortSchedule = AmortizationSchedule;
      Currency defaultCcy = Ccy;
      const double defaultAmount = 0.0;

      if (IsFloating)
      {
        Schedule schedule = loanSchedule ?? Schedule.CreateScheduleForCashflowFactory(scParams);
        //DataTable dtbl = schedule.ToDataTable(); // Just to test...
        IList<RateReset> rateResets = (LoanType == LoanType.Term ?
          GetAllRateResets(asOf, settle, schedule, interestPeriods) :
          InterestPeriodUtil.TransformToRateReset(from, Effective, interestPeriods));
        if (loanSchedule == null)
        {
          cashflow = CashflowFactory.FillFloat(cashflow, from,
            scParams, ccy, dayCount,
            coupon, couponSchedule,
            principal, amortSchedule,
            referenceCurve, rateResets,
            defaultAmount, defaultCcy, defaultDate,
            fee, feeSettle);
        }
        else
        {
          cashflow = CashflowFactory.FillFloat(cashflow, from,
            loanSchedule, scParams, ccy, dayCount,
            coupon, couponSchedule,
            principal, amortSchedule,
            referenceCurve, rateResets,
            defaultAmount, defaultCcy, defaultDate,
            fee, feeSettle);
        }


        if (LoanType != LoanType.Term && interestPeriods != null && interestPeriods.Count > 0
          && rateResets != null && rateResets.Count > 0)
        {
          int idx = schedule.GetPrevCouponIndex(from);
          double newAccrued = LoanModel.FirstCouponValue(from,
            schedule.GetPeriodStart(idx + 1),
            schedule.GetPeriodEnd(idx + 1), Calendar, BDConvention, DayCount, IsFloating,
            (DiscountCurve)referenceCurve, PricingGrid[curLevel],
            fee, drawn, interestPeriods, false);
          if (cashflow != null && cashflow.Count > 0)
            cashflow.Set(0, cashflow.GetAmount(0), newAccrued, defaultAmount);
        }

        if (floorValues != null && floorValues.Length > 0 && cashflow != null)
        {
          // floorValues have been generated from price.Settle onwards (inclusive), but cashflow is generated in this method from "from" onwards
          // so need to identify within cashflow the first interest payment that might need to be adjusted by floorValues
          int j = 0;
          for (; j < cashflow.Count; ++j)
          {
            var pay = cashflow.GetDt(j);
            if (pay >= settle) // LoanPricer.Schedule includes payment on settle
              break;
          }

          for (int i = 0; i < floorValues.Length && j < cashflow.Count; ++i, ++j)
          {
            Dt startDt = cashflow.GetStartDt(j);
            if (!(LoanType == LoanType.Term && IsResetAvailable(startDt, rateResets)) && floorValues[i] > 0.0)
            {
              cashflow.Set(j,
                cashflow.GetAmount(j),
                cashflow.GetAccrued(j) * (cashflow.GetCoupon(j) + floorValues[i]) / cashflow.GetCoupon(j),
                cashflow.GetCoupon(j) + floorValues[i],
                defaultAmount);
            }
          }
        }
      }
      else
      {
        cashflow = CashflowFactory.FillFixed(cashflow, from,
          scParams, ccy, dayCount,
          coupon, couponSchedule,
          principal, amortSchedule,
          defaultAmount, defaultCcy, defaultDate,
          fee, feeSettle);
      }
      return cashflow;
    }

    internal Cashflow GenerateCashflowToCall(Dt from, Dt asOf, Dt settle,
      string curLevel, double drawn,
      IList<InterestPeriod> interestPeriods, CalibratedCurve referenceCurve,
      Schedule loanSchedule, double[] floorValues, Dt callDate)
    {
      ScheduleParams scParams = GetScheduleParamsToCall(callDate);
      Cashflow cf = GenerateCashflow(null, asOf, settle, from, curLevel, drawn,
        interestPeriods, referenceCurve, loanSchedule, floorValues, scParams);
      return cf;
    }

    /// <summary>
    ///   Helper method for generating cashflows from raw data elements.
    /// </summary>
    ///
    /// <param name="cashflow">The cashflow.</param>
    /// <param name="asOf">From.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="referenceCurve">The reference curve.</param>
    /// <param name="rateResets">The rate resets.</param>
    /// <param name="recoveryRate">The recovery rate.</param>
    /// <param name="defaultDate">The default date.</param>
    /// <param name="dfltSettle">The default settlement date (should be typically after the default date)</param>
    /// <returns>Generated cashflow</returns>
    public Cashflow GenerateCashflow(Cashflow cashflow, Dt asOf,
      DiscountCurve discountCurve, DiscountCurve referenceCurve,
      IList<RateReset> rateResets, double recoveryRate, Dt defaultDate, Dt dfltSettle)
    {
      const double fee = 0.0;
      Dt feeSettle = Dt.Empty;
      const double principal = 1.0;
      if (!EnableNewCashflow && LoanFloor.HasValue)
      {
        throw new ToolkitException("Loan floor feature " +
                                   "is not supported when " +
                                   "UseBackwardCompatibleCashflowForCLO " +
                                   "is set to be true in toolkit configuration settings");
      }

      if (EnableNewCashflow)
      {
        if (discountCurve == null) discountCurve = new DiscountCurve(asOf, 0.0);
        cashflow = this.GenerateNewCashflow(cashflow, asOf, discountCurve,
          referenceCurve, new RateResets(rateResets), recoveryRate, defaultDate, dfltSettle);
      }
      else if (IsFloating)
      {
        cashflow = CashflowFactory.FillFloat(cashflow, asOf, GetScheduleParams(), Ccy, DayCount,
                                              PricingGrid[DefaultPerformanceLevel], null,
                                              principal, AmortizationSchedule,
                                              referenceCurve, rateResets,
                                              0.0, Ccy, defaultDate,
                                              fee, feeSettle);
      }
      else
      {
        cashflow = CashflowFactory.FillFixed(cashflow, asOf, GetScheduleParams(), Ccy, DayCount,
                                             PricingGrid[DefaultPerformanceLevel], null,
                                             principal, AmortizationSchedule,
                                             0.0, Ccy, defaultDate,
                                             fee, feeSettle);
      }
      return cashflow;
    }

    private Cashflow GenerateNewCashflow(Cashflow cf, Dt from,
      DiscountCurve discountCurve, DiscountCurve referenceCurve, RateResets rateResets,
      double recoveryRate, Dt defaultDate, Dt dfltSettle)
    {
      var loan = this;
      var paymentSchedule = this.GetPaymentSchedule(
        null, from, Dt.Empty, discountCurve, referenceCurve, Schedule, rateResets,
        DefaultPerformanceLevel, null, null, 0.0, defaultDate, dfltSettle, recoveryRate);
      cf = PaymentScheduleUtils.FillCashflow(cf, paymentSchedule, from, loan.Notional, recoveryRate);
      cf.DayCount = loan.DayCount;
      cf.Frequency = loan.Frequency;
      cf.Currency = loan.Ccy;
      cf.DefaultCurrency = loan.Ccy;
      return cf;
    }
  }

  #endregion Loan
}
