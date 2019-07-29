using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Pricers;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Models
{

  public static partial class BondModelUtil
  {

    //this function is only used for XL function qBondCashflow

    /// <summary>
    /// Utililty method used to generate the floating rate cashflows in which each of the individual coupons is 
    /// (Current Floating Rate + Spread) .This is useful for the FRN pricing methods 
    /// </summary>
    /// <param name="settle"></param>
    /// <param name="currentFloatingRate"></param>
    /// <param name="discountCurve"></param>
    /// <param name="liborStub"></param>
    /// <param name="effective"></param>
    /// <param name="firstCoupon"></param>
    /// <param name="lastCoupon"></param>
    /// <param name="nextCoupon"></param>
    /// <param name="maturity"></param>
    /// <param name="ccy"></param>
    /// <param name="coupon"></param>
    /// <param name="couponSchedule"></param>
    /// <param name="dayCount"></param>
    /// <param name="freq"></param>
    /// <param name="bdc"></param>
    /// <param name="cal"></param>
    /// <param name="rateResets"></param>
    /// <param name="amortSchedule"></param>
    /// <param name="periodAdjustment"></param>
    /// <param name="recoveryRate"></param>
    /// <param name="eomRule"></param>
    /// <param name="defaultDate"></param>
    /// <param name="principal"></param>
    /// <returns></returns>
    public static Cashflow GenerateFrnCashflow(Dt settle, double currentFloatingRate, DiscountCurve discountCurve, double liborStub, Dt effective, Dt firstCoupon, Dt lastCoupon, Dt nextCoupon,
                                           Dt maturity, Currency ccy, double coupon, IList<CouponPeriod> couponSchedule, DayCount dayCount, Frequency freq, BDConvention bdc,
                                            Calendar cal, IList<RateReset> rateResets, IList<Amortization> amortSchedule, bool periodAdjustment, double recoveryRate, bool eomRule, Dt defaultDate, double principal)
    {
      DiscountCurve referenceCurve = new DiscountCurve((DiscountCalibrator)discountCurve.Calibrator.Clone(),
                                                       discountCurve.Interp,
                                                       (dayCount != DayCount.ActualActualBond) ? dayCount : DayCount.Actual365Fixed,
                                                       freq);
      double df1 = RateCalc.PriceFromRate(liborStub, settle, nextCoupon, dayCount, freq);
      referenceCurve.Add(nextCoupon, df1);

      string endTenor = "35 Year";// extrapolate curve to 35 years
      Dt endDate = Dt.Add(settle, endTenor);

      double df2 = df1 * RateCalc.PriceFromRate(currentFloatingRate, nextCoupon, endDate, (dayCount != DayCount.ActualActualBond) ? dayCount : DayCount.Actual365Fixed, freq);
      referenceCurve.Add(endDate, df2);

      Cashflow cf = new Cashflow(settle);

      // the schedule generation accrueOnCycle flag is opposite in polarity compare to the way 
      // we present this to the user as an option on the bond
      bool accrueOnCycle = !periodAdjustment;

      // "adjustNext" behavior means that once you've calculated a rolled payment date for period n, that
      // rolled date becomes the starting point for calculating the end date for period n+1. With this behavior
      // the period start/end dates "creep" forward until they lock in at the end of the month. 
      // This behavior is triggered by BDConvention == FRN, which is effectively ModifiedFollowing + the creeping behavior.
      // Notice that EomRule needs to be false if BDConvention == FRN, otherwise FillFixed/Float will throw an error
      var cycleRule = (bdc == BDConvention.FRN) ? CycleRule.FRN : CycleRule.None;

      // for bonds (in comparison with, say, CDS) there's nothing special about the last period's accrual, so
      // adjust/don't adjust the number of accrual days when the dates roll based on the regular PeriodAdjustment flag.
      bool adjustLast = periodAdjustment;

      CashflowFlag flags = CashflowFlag.None;
      if (accrueOnCycle)
        flags |= CashflowFlag.AccrueOnCycle;
      else
        flags |= CashflowFlag.AdjustLast;

      var schedParams = new ScheduleParams(effective, firstCoupon, lastCoupon, maturity, freq, bdc,
                                           cal, cycleRule, flags);

      const double fee = 0.0;
      Dt feeSettle = Dt.Empty;
      double defaultAmount = recoveryRate;
      Currency defaultCcy = ccy;

      CashflowFactory.FillFloat(cf, settle, schedParams, ccy, dayCount,
                                coupon, couponSchedule,
                                principal, amortSchedule,
                                referenceCurve, rateResets,
                                defaultAmount, defaultCcy, defaultDate,
                                fee, feeSettle);


#if OBSOLETE
      CashflowFactory.FillFloat(cf, settle, effective, firstCoupon, lastCoupon, maturity,
                                ccy, 0.0, effective, coupon, couponSchedule, dayCount, freq, bdc, cal,
                                referenceCurve, rateResets,
                                principal, amortSchedule, false, false, recoveryRate, ccy,
                                accrueOnCycle, adjustNext, adjustLast, eomRule, defaultDate, false);
#endif

      return cf;
    }
  }

  /// <summary>
  /// Cashflow model
  /// </summary>
  [ReadOnly(true)]
  public abstract class CashflowModel : Native.CashflowModel
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CashflowModel));

    #region Methods

    // TODO: The only one caller of this function has no usage anymore.
    // TODO: we can delete this function and its caller if needed.
    /// <summary>
    /// Calculate the CDS spread implied by full price.
    /// </summary>
    /// <remarks>
    /// <para>Calculates constant spread over survival curve spreads for
    /// cashflow to match a specified full price.</para>
    /// </remarks>
    /// <param name="asOf">pricing as-of</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="cashflow">Cashflow</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="counterpartyCurve">Couterparty survival curve</param>
    /// <param name="correlation">Correlation between default and counterparty</param>
    /// <param name="includeSettlePayments">True if payments on settlement date included</param>
    /// <param name="discountingAccrued">Whether to discount the accrued or not</param>
    /// <param name="creditRiskByPayDate">True if the credit risk is determined by payment date instead of period end</param>
    /// <param name="stepSize">Pricing grid step size</param>
    /// <param name="stepUnit">Pricing grid step time unit</param>
    /// <param name="price">Target full price (percentage of notional)</param>
    /// <returns>Spreads shift to the Survival Curve implied by price</returns>
    public static double
    ImpliedCDSSpread(
      Dt asOf, Dt settle, Cashflow cashflow, DiscountCurve discountCurve, SurvivalCurve survivalCurve,
      SurvivalCurve counterpartyCurve, double correlation, bool includeSettlePayments, bool discountingAccrued,
      bool creditRiskByPayDate, int stepSize, TimeUnit stepUnit, double price)
    {
      SurvivalCurve sc;
      double spread;
      SolveSpread(asOf, settle, cashflow, discountCurve, survivalCurve,
        counterpartyCurve, correlation, includeSettlePayments, discountingAccrued,
        creditRiskByPayDate, stepSize, stepUnit, price, out sc, out spread);
      return spread;
    }

    // TODO:The only one caller of this function has no usage anymore.
    // TODO: we can delete this function and its caller if needed.
    /// <summary>
    /// Calculate the bond-implied CDS Curve
    /// </summary>
    /// <remarks>
    /// <para>Calculates the (constant)spread that needs to be added/subtracted from CDS curve to
    /// recover full bond price. Once the shift is calculated the shifted survival curve is returned.</para>
    /// </remarks>
    /// <param name="asOf">pricing as-of</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="cashflow">Cashflow</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="counterpartyCurve">Couterparty survival curve</param>
    /// <param name="correlation">Correlation between default and counterparty</param>
    /// <param name="includeSettlePayments">True if payments on settlement date included</param>
    /// <param name="discountingAccrued">Whether to discount the accrued or not</param>
    /// <param name="creditRiskByPayDate">True if the credit risk is determined by payment date instead of period end</param>
    /// <param name="stepSize">Pricing grid step size</param>
    /// <param name="stepUnit">Pricing grid step time unit</param>
    /// <param name="price">Target full price (percentage of notional)</param>
    /// <returns>Bond-Implied Survival Curve</returns>
    public static SurvivalCurve
    ImpliedCDSCurve(
      Dt asOf, Dt settle, Cashflow cashflow, DiscountCurve discountCurve, SurvivalCurve survivalCurve,
      SurvivalCurve counterpartyCurve, double correlation, bool includeSettlePayments, bool discountingAccrued,
      bool creditRiskByPayDate, int stepSize, TimeUnit stepUnit, double price)
    {
      SurvivalCurve sc;
      double spread;
      SolveSpread(asOf, settle, cashflow, discountCurve, survivalCurve,
        counterpartyCurve, correlation, includeSettlePayments, discountingAccrued,
        creditRiskByPayDate, stepSize, stepUnit, price, out sc, out spread);
      return sc;
    }

    //TODO: only risk uses this function and its caller.
    /// <summary>
    /// Calculate the Implied flat CDS Curve from a full price
    /// </summary>
    /// <remarks>
    /// <para>Implies a flat CDS curve from the specified full price.</para>
    /// <note>Does not require an existing survival curve.</note>
    /// </remarks>
    /// <param name="asOf">pricing as-of</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="cashflow">Cashflow</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="counterpartyCurve">Couterparty survival curve</param>
    /// <param name="correlation">Correlation between default and counterparty</param>
    /// <param name="includeSettlePayments">True if payments on settlement date included</param>
    /// <param name="discountingAccrued">Whether to discount the accrued or not</param>
    /// <param name="creditRiskByPayDate">True if the credit risk is determined by payment date instead of period end</param>
    /// <param name="stepSize">Pricing grid step size</param>
    /// <param name="stepUnit">Pricing grid step time unit</param>
    /// <param name="price">Target full price (percentage of notional)</param>
    /// <param name="recoveryRate">Recovery rate for CDS to imply</param>
    /// <param name="allowNegativeCDSSpreads">Allow Negative Spreads in the Survival Curve</param>
    /// <returns>Implied Survival Curve fitted from CDS quotes</returns>
    public static SurvivalCurve
    ImpliedCDSCurve(
      Dt asOf, Dt settle, Cashflow cashflow, DiscountCurve discountCurve,
      SurvivalCurve counterpartyCurve, double correlation, bool includeSettlePayments, bool discountingAccrued,
      bool creditRiskByPayDate, int stepSize, TimeUnit stepUnit, double price, double recoveryRate, bool allowNegativeCDSSpreads)
    {
      double spread;

      // Create initial survival curve
      SurvivalFitCalibrator calibrator = new SurvivalFitCalibrator(asOf, settle, recoveryRate, discountCurve);
      calibrator.AllowNegativeCDSSpreads = allowNegativeCDSSpreads;

      SurvivalCurve survivalCurve = new SurvivalCurve(calibrator);
      survivalCurve.AddCDS(Dt.CDSMaturity(settle, "5 Year"), 0.0, DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.None);

      survivalCurve.Fit();

      SolveSpread(asOf, settle, cashflow, discountCurve, survivalCurve,
        counterpartyCurve, correlation, includeSettlePayments, discountingAccrued,
        creditRiskByPayDate, stepSize, stepUnit, price, out survivalCurve, out spread);
      return survivalCurve;
    }

    //TODO: this is internal used. Its three callers are not used in toolkit anymore
    /// <summary>
    /// Local utility function to solve for the CDS Curve from a full price
    /// </summary>
    /// <param name="asOf">pricing as-of</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="cashflow">Cashflow</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="counterpartyCurve">Couterparty survival curve</param>
    /// <param name="correlation">Correlation between default and counterparty</param>
    /// <param name="includeSettlePayments">True if payments on settlement date included</param>
    /// <param name="discountingAccrued">Whether to discount the accrued or not</param>
    /// <param name="creditRiskByPayDate"></param>
    /// <param name="stepSize">Pricing grid step size</param>
    /// <param name="stepUnit">Pricing grid step time unit</param>
    /// <param name="price">Target full price (percentage of notional)</param>
    /// <param name="fittedSpread">returned implied spread</param>
    /// <param name="fittedSurvivalCurve">returned implied credit curve</param>
    private static void SolveSpread(
      Dt asOf, Dt settle, Cashflow cashflow, DiscountCurve discountCurve, SurvivalCurve survivalCurve,
      SurvivalCurve counterpartyCurve, double correlation, bool includeSettlePayments, bool discountingAccrued,
      bool creditRiskByPayDate, int stepSize, TimeUnit stepUnit, double price,
      out SurvivalCurve fittedSurvivalCurve, out double fittedSpread
      )
    {
      // validate
      if (asOf > settle)
        throw new ArgumentOutOfRangeException("settle", String.Format("Settle date {0} must be on or after the pricing asOf {1} date", settle, asOf));
      if (discountCurve == null)
        throw new ArgumentOutOfRangeException("discountCurve", "Must specify discount curve to calculation implied CDS Spread");
      if (survivalCurve == null)
        throw new ArgumentOutOfRangeException("survivalCurve", "Must specify survival curve to calculation implied CDS Spread");
      if (correlation > 1.0 || correlation < -1.0)
        throw new ArgumentOutOfRangeException("correlation", String.Format("Invalid correlation. Must be between -1 and 1 - not {0}", correlation));
      if (price < 0.0 || price > 4.0)
        throw new ArgumentOutOfRangeException("price", String.Format("Invalid full price. Must be between 0 and 4 - not {0}", price));
      if (stepSize < 0)
        throw new ArgumentOutOfRangeException("stepSize", "Invalid step size, must be >= )");

      // Solve for fitted survival curve
      HazardFn fn = new HazardFn(cashflow, asOf, settle, discountCurve, survivalCurve,
        counterpartyCurve, correlation, includeSettlePayments, discountingAccrued, stepSize, stepUnit)
      { CreditRiskByPaymentDt = creditRiskByPayDate };

      // find smallest quote
      CurveTenorCollection tenors = survivalCurve.Tenors;
      int count = survivalCurve.Tenors.Count;
      double minQuote = CurveUtil.MarketQuote(tenors[0]);
      for (int i = 1; i < count; ++i)
      {
        double quote = CurveUtil.MarketQuote(tenors[i]);
        if (quote < minQuote)
          minQuote = quote;
      }

      // Set up root finder
      Brent2 rf = new Brent2();
      rf.setToleranceX(10e-6);
      rf.setToleranceF(10e-6);
      //rf.setLowerBounds(-minQuote + 10e-8);
      //rf.setUpperBracket(1.0);

      // Solve
      double x = rf.solve(fn, price, -minQuote + 10e-8, 0.1);
      fn.evaluate(x);
      fittedSpread = fn.Spread;
      fittedSurvivalCurve = fn.SurvivalCurve;

      return;
    }

    #endregion Methods

    #region Solvers

    //TODO: It is only used in this file and not used in toolkit anymore.
    /// <summary>
    /// Solver Fn function for implying CDS curve from price
    /// </summary>
    private class HazardFn : SolverFn
    {
      public HazardFn(Cashflow cf, Dt asOf, Dt settle,
        DiscountCurve discountCurve, SurvivalCurve survivalCurve,
        SurvivalCurve counterpartyCurve, double correlation,
        bool includeSettlePayments, bool discountingAccrued, int step, TimeUnit stepUnit)
      {
        cf_ = cf;
        asOf_ = asOf;
        settle_ = settle;
        discountCurve_ = discountCurve;
        // clone original curve and calibrator so bumping will not effect the original
        survivalCurve_ = (SurvivalCurve)survivalCurve.Clone();
        counterpartyCurve_ = counterpartyCurve;
        correlation_ = correlation;
        includeSettlePayments_ = includeSettlePayments;
        discountingAccrued_ = discountingAccrued;
        ignoreAccruedInProtection_ = ToolkitConfigurator.Settings.
          CashflowPricer.IgnoreAccruedInProtection;
        step_ = step;
        stepUnit_ = stepUnit;
        x0_ = survivalCurve.Spread;
      }

      public override double evaluate(double x)
      {
        survivalCurve_.Spread = x0_ + x;
        var flags = CashflowModelFlags.IncludeProtection | CashflowModelFlags.IncludeFees |
                    (includeSettlePayments_ ? CashflowModelFlags.IncludeSettlePayments : 0) |
                    (discountingAccrued_ ? CashflowModelFlags.FullFirstCoupon : 0) |
                    (ignoreAccruedInProtection_ ? CashflowModelFlags.IgnoreAccruedInProtection : 0) |
                    (CreditRiskByPaymentDt ? CashflowModelFlags.CreditRiskToPaymentDate : 0);
        double pv = CashflowModel.Price(cf_, asOf_, settle_, discountCurve_, survivalCurve_,
          counterpartyCurve_, correlation_, (int)flags, step_, stepUnit_, cf_.Count);
        return pv;
      }

      /// <summary>Get fitted survival curve</summary>
      public SurvivalCurve SurvivalCurve
        => PaymentScheduleUtils.SynchronizeSpreads(survivalCurve_);

      public double Spread
      {
        get
        {
          Dt maturity = cf_.GetDt(cf_.Count - 1);
          CDSCashflowPricer pricer = PaymentScheduleUtils.GetPricer(
            survivalCurve_, maturity);
          double cdsSpread = pricer.BreakEvenPremium();
          double scSpread = survivalCurve_.Spread;
          try
          {
            survivalCurve_.Spread = x0_;
            pricer.Reset();
            return cdsSpread - pricer.BreakEvenPremium();
          }
          finally
          {
            survivalCurve_.Spread = scSpread;
          }
        }
      }
      /// <summary>
      /// If true, the credit risk is determined by the payment date instead of period end
      /// </summary>
      public bool CreditRiskByPaymentDt { get; set; }
      private readonly Cashflow cf_;
      private readonly Dt asOf_;
      private readonly Dt settle_;
      private readonly DiscountCurve discountCurve_;
      private readonly SurvivalCurve survivalCurve_;
      private readonly SurvivalCurve counterpartyCurve_;
      private readonly double correlation_;
      private readonly bool includeSettlePayments_;
      private readonly bool discountingAccrued_;
      private readonly bool ignoreAccruedInProtection_;
      private readonly int step_;
      private readonly TimeUnit stepUnit_;
      private readonly double x0_;
    }

    #endregion Solvers

  } // class CashflowModel




}
