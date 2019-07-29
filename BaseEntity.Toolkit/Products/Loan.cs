/*
 * Loan.cs
 *
 *   2008. All rights reserved.
 *
 * Created by rsmulktis on 1/30/2008 12:28:26 PM
 *
 */

using System;
using System.Collections;
using System.ComponentModel;
using System.Collections.Generic;
using System.Xml.Serialization;

using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Cashflows.Utils;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Syndicated Cash Loan product.
  /// </summary>
  /// <remarks>
  /// <para>
  /// The syndicated loan market consists of two main categories: leveraged (or high yield) and investment grade. 
  /// The leveraged loan market is comparable to the high-yield bond market from an issuer credit rating perspective 
  /// and leverage perspective. A loan can be classified as leveraged if it generally meets any of the following 
  /// criteria: (1) debt ratings of below Baa3/BBB- from Moody’s and SP, respectively; (2) debt/EBITDA ratio of 3.0 
  /// times or greater; or (3) pricing of at least 125bp over LIBOR at issue. Leveraged loans are further broken down 
  /// into leveraged and highly leveraged deals.
  /// </para>
  /// <para>Terms of a Loan</para>
  /// <list type="bullet">
  ///   <item><description>Leveraged loans have a floating interest rate</description></item>
  ///   <item><description>They generally mature in five to eight years</description></item>
  ///   <item><description>These loans are generally callable</description></item>
  ///   <item><description>Leveraged loans have covenant packages that allow lenders to have some control over a credit</description></item>
  ///   <item><description>They are usually rated by Moody’s and SP</description></item>
  ///   <item><description>Leveraged loans are usually secured by the assets of the issuer</description></item>
  /// </list>
  /// <para><b>Floating-Rate Coupon</b></para>
  /// <para>
  /// Leveraged Loans Pay a Floating Interest Rate, Usually Set at a Spread above LIBOR. Leveraged loans pay interest 
  /// on a floating-rate basis, so interest payments on loans increase as market interest rates rise. This floating-rate 
  /// structure is accomplished by setting the interest rate of a leveraged loan at a spread above a benchmark market 
  /// floating interest rate. The most commonly used benchmark in the leveraged loan market is LIBOR. So a loan paying 
  /// 3.0% above LIBOR, or L+300bp, would temporarily yield 8.4% annually if LIBOR were at 5.4%. As LIBOR moves, the 
  /// interest payments of a leveraged loan move with it. In a comparison of leveraged loans to high-yield bonds, one of 
  /// the most significant differences is the interest rate. High-yield bonds pay a fixed interest rate using a US 
  /// Treasury bond benchmark that leaves investors exposed to movements in interest rates. The risk is that if market 
  /// interest rates rise, the fixed-rate high-yield bond will continue to pay the same lower interest rate. Although 
  /// derivatives can be used to hedge away this risk, this can only be done at a cost that cuts into expected returns.
  /// </para>
  /// <para><b>Maturity</b></para>
  /// <para>
  /// Term loans generally mature in five to eight years from the time of issue, which is less than the typical high yield 
  /// bond maturity of seven to ten years.
  /// </para>
  /// <para><b>Callability</b></para>
  /// <para>
  /// Loans Are Typically Callable and Can Be Repaid by the Issuer at Any Time. Loans are generally callable at par, meaning 
  /// that the issuer can repay its loans partially or in total at any time. This differs from comparable high-yield bonds, 
  /// which are usually structured with a noncall period of three to five years. Occasionally, loans will have non-call periods 
  /// or call protection that requires the issuer to pay a penalty premium for prepaying loans. These features are usually only 
  /// added to loans in the primary market when weak investor demand requires additional incentives to attract sufficient buyers.
  /// </para>
  /// <para><b>Financial Covenants</b></para>
  /// <para>
  /// Loan facilities are structured with financial covenant tests that limit a borrower’s ability to increase credit risk 
  /// beyond certain specific parameters. Financial covenants are outlined in the legal credit agreement of a loan facility 
  /// that is executed at the time that a loan is issued. Generally, financial covenants are tested every quarter and results 
  /// are sent to all of the members of the bank group. Financial covenant tests provide lenders with a more detailed view of 
  /// the credit health of a borrower and allow lenders to take action in the event a borrower gets into credit trouble. When a 
  /// borrower breaches a financial covenant, the loan is required to be repaid unless the lenders agree to amend the covenants 
  /// to keep the borrower in compliance with the credit agreement. A credit agreement for a leveraged loan may generally have 
  /// between two and six financial covenants depending on the credit risk of the borrower and market conditions.
  /// </para>
  /// <para>Some commonly used financial covenants include:</para>
  /// <list type="bullet">
  ///   <item><description>Minimum EBITDA</description></item>
  ///   <item><description>Total leverage debt/EBITDA</description></item>
  ///   <item><description>Senior leverage senior debt/EBITDA</description></item>
  ///   <item><description>Minimum net worth</description></item>
  ///   <item><description>Maximum capital expenditures</description></item>
  ///   <item><description>Minimum interest coverage EBITDA/interest</description></item>
  /// </list>
  /// <para><b>Ratings</b></para>
  /// <para>
  /// The major debt rating agencies have dramatically increased the number of leveraged loan issuers that they rate during the 
  /// last decade in response to strong investor demand. Moody’s, SP, and Fitch are all actively rating and monitoring loan 
  /// deals. The number of rated loans has soared to the point where now more than 80% of all new issues receive a rating from 
  /// at least one agency. Although methodologies used to determine the ratings differ somewhat from agency to agency, significant 
  /// progress in refining the methodology has occurred across all agencies, meaning that investors are receiving more accurate 
  /// information on a wider number of loans, which should allow for more reliable pricing.
  /// </para>
  /// <para>
  /// This is especially important given the material rise in secondary trading and the corresponding entry into the market of a 
  /// large number of investors who demand ratings for their fund structures and have to carry out regular mark-to-market portfolio 
  /// pricing. This phenomenon has influenced the leveraged loan market so that a rating change by any one of the agencies can 
  /// cause a significant change in the value of a loan in the secondary market.
  /// </para>
  /// <para>The ratings given to loans are primarily based on two factors:</para>
  /// <list type="bullet">
  ///   <item><description>Probability of default</description></item>
  ///   <item><description>Expected recovery rate</description></item>
  /// </list>
  /// <para>
  /// The probability of default for a bank loan is approximately equal to that of a bond. Despite this fact, a loan is 
  /// frequently given a higher rating than a bond of similar size and duration. The reason for this disparity lies in the fact 
  /// that default rates do not capture a critical, value-adding component of a loan — its higher status in the capital structure 
  /// of a firm relative to a bond. Because a bank loan is generally a senior secured debt obligation, the average recovery rate 
  /// for loans is significantly higher than the average recovery rate for bonds, which, at best, tend to be senior unsecured debt 
  /// obligations.
  /// </para>
  /// <para>
  /// The widespread rating of loans is a relatively recent phenomenon that did not take off until the mid-1990s. In the past, 
  /// when loans were not rated, market participants generally estimated that the loan should be one notch up from the most 
  /// senior unsecured bond. While this rule is sometimes accurate, it has some serious shortcomings when used to evaluate pricing 
  /// for individual credits. As a matter of fact, according to a 1998 study carried out by Moody’s when loan rating methodologies 
  /// were being determined, only 37% of loans were actually rated exactly one notch higher than the senior unsecured bond. This 
  /// means that an investor exclusively using this “one-notch” rule to price the premium paid on a loan’s higher recovery rate 
  /// would have mispriced the loan more than 60% of the time.
  /// </para>
  /// <para>
  /// This is not to say that the one-notch rule does not have practical uses. It is still useful to use as a benchmark from 
  /// which to start one’s credit analysis. As a tool to price loans, however, it is clearly inadequate. Instead, investors need 
  /// to follow the lead of the rating agencies and look very carefully at the individual credit’s attributes to determine how 
  /// factors such as industry, corporate structure, legal subordination, underlying collateral quality, and a host of others will 
  /// impact recovery rates in cases of default, because these factors can cause the recovery rates of seemingly similar loans to 
  /// differ significantly.
  /// </para>
  /// <para><b>Security</b></para>
  /// <para>
  /// Leveraged loans are generally structured with a lien against the assets of the borrower. These asset claims are also known 
  /// as the security of the loan. Secured loans have a number of advantages over unsecured parts of a company’s capital structure. 
  /// In the event of a default, the lenders can take possession of the borrower’s assets they have a claim to and sell them or 
  /// operate them for cash. Recovery rates on defaulted loans are consistently higher than recoveries on unsecured parts of the 
  /// capital structure.
  /// </para>
  /// <para><b>Fees</b></para>
  /// <para>
  /// The fees for leveraged loans are generally comprised of “up-front fees” and “commitment fees” that vary with market conditions.
  /// </para>
  /// <para><b>Up-Front Fees</b></para>
  /// <para>
  /// In the leveraged loan market, issuers pay one-time up-front fees at the time of issuance to attract banks and institutional 
  /// investors to invest in their loans. Up-front fees for pro rata loans tend to be higher than up-front fees on institutional 
  /// term loans because of their lower coupons and less favorable supply/demand conditions.
  /// </para>
  /// <para><b>Commitment Fees</b></para>
  /// <para>
  /// Also called unused fees, commitment fees are assessed on an ongoing basis on the committed but undrawn component of a 
  /// revolving credit facility. Commitment fees accrue daily on the undrawn balance of a loan at an annualized interest rate 
  /// specified in the credit agreement. As banks evolve from lending based on relationships to lending based on returns, the 
  /// average commitment fee has broached the 50bp level, a benchmark that was considered as a ceiling on these fees. This trend 
  /// is expected to continue in the future, as retail bank investors continue to raise the bar on returns for the pro rata 
  /// tranches of leveraged loans.
  /// </para>
  /// </remarks>
  [Serializable]
  public partial class Loan : Product
  {
    #region Constructor

    /// <summary>
    /// Default Constructor.
    /// </summary>
    public Loan() : base()
    {
    }

    /// <summary>
    /// Copy Constructor.
    /// </summary>
    /// 
    /// <param name="other">The Loan to copy.</param>
    /// 
    public Loan(Loan other) : base()
    {
      this.BDConvention = other.BDConvention;
      this.Calendar = other.Calendar;
      this.Ccy = other.Ccy;
      this.CommitmentFee = other.CommitmentFee;
      this.DayCount = other.DayCount;
      this.Description = other.Description;
      this.Effective = other.Effective;
      this.CycleRule = other.CycleRule;
      this.FirstCoupon = other.FirstCoupon;
      this.Frequency = other.Frequency;
      this.Index = other.Index;
      this.LastCoupon = other.LastCoupon;
      this.LastDraw = other.LastDraw;
      this.LoanType = other.LoanType;
      this.Maturity = other.Maturity;
      this.Notional = other.Notional;
      this.PeriodAdjustment = other.PeriodAdjustment;

      CollectionUtil.Add(this.AmortizationSchedule, other.AmortizationSchedule);
      CollectionUtil.Add(this.PricingGrid, other.PricingGrid);
    }

    #endregion Constructor

    #region Methods
 
    private bool IsResetAvailable(Dt dt, IList<RateReset> rateResets)
    {
      if (rateResets == null || rateResets.Count == 0) return false;
      foreach (RateReset rr in rateResets)
      {
        if (rr.Date == dt) return true;
      }
      return false;
    }


    internal ScheduleParams GetScheduleParamsToCall(Dt callDate)
    {
      if (!callDate.IsValid() || callDate <= Effective)
        throw new ToolkitException("Invalid call date");
      ScheduleParams sc = GetScheduleParams();
      if (callDate >= Maturity) return sc;

      // Even if the PeriodAdjustment flag is set, still create a schedule 
      //WITHOUT period adjustment, so that we can set an un-adjusted
      // next to last coupon date, to count the other coupon dates back from it. 
      //This way we will recover the same coupon dates as in the original schedule.
      CashflowFlag flags = CashflowFlag.AccrueOnCycle;
      sc = new ScheduleParams(Effective, FirstCoupon, LastCoupon, Maturity, 
        Frequency, BDConvention, Calendar, CycleRule, flags);

      Schedule schedule = Schedule.CreateScheduleForCashflowFactory(sc);
      var scheduleParams = (ScheduleParams)sc.Clone();
      scheduleParams.CycleRule = schedule.CycleRule;
      Dt nextToLastCouponDate = schedule.GetPrevCouponDate(callDate);
      if (nextToLastCouponDate >= callDate)
        nextToLastCouponDate = schedule.GetPrevCouponDate(callDate - 1);
      if (nextToLastCouponDate > Effective)
        scheduleParams.NextToLastCouponDate = nextToLastCouponDate;
      scheduleParams.Maturity = callDate;
      if (Dt.Cmp(scheduleParams.NextToLastCouponDate, scheduleParams.Maturity) > 0)
        scheduleParams.NextToLastCouponDate = Dt.Empty;
      // Now put the period adjustment flag back to get the adusted dates in the "reduced" schedule.
      if (PeriodAdjustment)
        scheduleParams.CashflowFlag = CashflowFlag.AdjustLast;  
      return scheduleParams;
    }

    /// <summary>
    /// Get all rate resets (from the effective date) for a floating Term loan, from the interest periods.
    /// </summary>
    internal List<RateReset> GetAllRateResets(Dt asOf, Dt settle, Schedule schedule, 
      IList<InterestPeriod> interestPeriods)
    {
      bool periodAdjustment = this.PeriodAdjustment;
      var rateResets = new List<RateReset>();
      if (schedule == null || schedule.Count == 0 || interestPeriods == null 
        || interestPeriods.Count == 0)
        return rateResets; // empty
      for (int i = 0; i < schedule.Count; i++)
      {
        Dt perStart = schedule.GetPeriodStart(i);
        Dt perStartAdjusted = perStart;
        if (!periodAdjustment)
          perStartAdjusted = (i == 0 ? perStart : schedule.GetPaymentDate(i - 1));
        Dt resetDate = Dt.AddDays(perStart, -RateResetDays, Calendar);
        if (resetDate > asOf) break;
        bool foundReset = false;
        foreach (InterestPeriod ip in interestPeriods)
        {
          // When period adjustment flag of the Loan product is set to false, 
          //we will allow the users enter the dates of interest periods either 
          //as business adjusted or un-adjusted coupon dates.
          if (ip.StartDate == perStart || ip.StartDate == perStartAdjusted)
          {
            rateResets.Add(new RateReset(perStart, ip.AnnualizedCoupon));
            foundReset = true;
            break;
          }
        }
        // This is to generate an error when computing inception-to-date P/L from the past cash flows ...
        if (!foundReset && asOf > resetDate)
          rateResets.Add(new RateReset(perStart, double.NaN));  
      }
      return rateResets;
    }

    /// <summary>
    /// Find all reset dates for a term loan
    /// </summary>
    /// <remarks>Calculates all the interest periods and determines a reset date n 
    /// business days prior to the start of each period
    /// based on RateResetDays and Calendar properties
    /// </remarks>
    public List<Dt> GetRateResetDatesForTermLoan()
    {
      var ret = new List<Dt>();
      if (this.LoanType != LoanType.Term || !this.IsFloating) return ret;
      Schedule schedule = Schedule;

      for (int i = 0; i < schedule.Count; i++)
      {
        Dt perStart = schedule.GetPeriodStart(i);
        Dt resetDate = Dt.AddDays(perStart, -RateResetDays, Calendar);
        ret.Add(resetDate);
      }
      ret.Sort();  // Probably redundant, but just in case ...
      return ret;
    }

    /// <summary>
    /// Get the current rate reset from the interest periods for a Term loan 
    /// (known on asof date, in effect at the pricer settle date)
    /// </summary>
    internal RateReset GetCurrentReset(Dt asOf, Dt settle, Schedule schedule, 
      IList<InterestPeriod> interestPeriods)
    {
      bool periodAdjustment = this.PeriodAdjustment;
      var rateResets = new List<RateReset>();
      if (schedule == null || schedule.Count == 0 || interestPeriods == null 
        || interestPeriods.Count == 0)
        return null;
      for (int i = 0; i < schedule.Count; i++)
      {
        Dt perStart = schedule.GetPeriodStart(i);
        Dt perEnd = schedule.GetPeriodEnd(i);
        if (perStart <= settle && settle < perEnd) // This is the "current" coupon period
        {
          // Now find the corresponding Interest Period
          Dt resetDate = Dt.AddDays(perStart, -RateResetDays, Calendar);
          if (resetDate > asOf) return null;
          Dt perStartAdjusted = perStart;
          if (!periodAdjustment)
            perStartAdjusted = (i == 0 ? perStart : schedule.GetPaymentDate(i - 1));
          foreach (InterestPeriod ip in interestPeriods)
          {
            // When period adjustment flag of the Loan product is set to false, 
            //we will allow the users enter the dates of interest periods either 
            //as business adjusted or un-adjusted coupon dates.
            if (ip.StartDate == perStart || ip.StartDate == perStartAdjusted)
            {
              return(new RateReset(perStart, ip.AnnualizedCoupon));
            }
          }
          return null; // Matching interest period not found
        }
      }
      return null;
    }
 
    private ProjectionParams GetProjectionParams()
    {
      var flags = ProjectionFlag.None;
      var par = new ProjectionParams
      {
        ProjectionType = ProjectionType.SimpleProjection,
        ProjectionFlags = flags,
        ResetLag = Tenor.Empty
      };
    
      return par;
    }

    private IRateProjector GetRateProjector(
      DiscountCurve discountCurve,
      CalibratedCurve referenceCurve,
      ProjectionParams projectionParams)
    {
      //Note: we don't assume cashflow start is the trade date (today).
      // Instead we use the referenceCurve.AsOf as today to determine
      // whether to project rates or to look at the resets.
      var rateProjector = CouponCalculator.Get(
        referenceCurve == null ? Dt.Empty : referenceCurve.AsOf,
        GetReferenceIndex(),
        referenceCurve, discountCurve,
        projectionParams);
   
      return rateProjector;
    }

    private ReferenceIndex GetReferenceIndex()
    {
      //Note: This can be removed once the ReferenceIndex property is added to the bond.
      var index = Index;
      if (index == null) return null;
      return new InterestRateIndex(index, Frequency, Ccy, DayCount, Calendar, 0);
    }

    
    internal PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from, Dt to,
      DiscountCurve discountCurve, DiscountCurve referenceCurve, Schedule schedule,
      RateResets rateResets, string currentLevel, IList<InterestPeriod> interestPeriods,
      double[] floorValues, double drawn, Dt defaultDate, Dt dfltSettle,
      double recoveryRate, double fee = 0.0)
    {
      var loan = this;
      if (ps == null)
        ps = new PaymentSchedule();
      if (from > loan.EffectiveMaturity &&
          !(dfltSettle.IsValid() && from < dfltSettle || !dfltSettle.IsValid()
            && defaultDate.IsValid()))
        return ps;

      if (loan.CustomPaymentSchedule != null && loan.CustomPaymentSchedule.Count > 0)
      {
        foreach (Dt d in loan.CustomPaymentSchedule.GetPaymentDates())
        {
          if (d >= from)
          {
            if (to.IsValid() && d > to)
              break;
            IEnumerable<Payment> paymentsOnDate =
              loan.CustomPaymentSchedule.GetPaymentsOnDate(d);
            // Not all the items with pay date >= from will be taken - see below.
            var usedPaymentsOnDate = new List<Payment>();
            foreach (Payment pmt in paymentsOnDate)
            {
              if (pmt is InterestPayment)
              {
                if (((InterestPayment)pmt).PeriodEndDate >= from)
                  usedPaymentsOnDate.Add((InterestPayment)pmt.Clone());
              }
              else if (pmt is PrincipalExchange)
              {
                var bpmt = (PrincipalExchange)pmt;
                if (!bpmt.CutoffDate.IsEmpty())
                {
                  if (bpmt.CutoffDate >= from)
                    usedPaymentsOnDate.Add((PrincipalExchange)pmt.Clone());
                }
                else
                {
                  if (bpmt.PayDt >= from)
                    usedPaymentsOnDate.Add((PrincipalExchange)pmt.Clone());
                }
              }
            }
            ps.AddPayments(usedPaymentsOnDate);
          }
        }
        // Update rate resets in floating interest payment objects at this point, 
        //taking into account the pricing date:
        if (loan.IsFloating && rateResets != null)
        {
          ProjectionParams projParams = loan.GetProjectionParams();
          IRateProjector rateProjector = loan.GetRateProjector(discountCurve, referenceCurve, projParams);
          var projector = (CouponCalculator)rateProjector;
          var arrFlt = ps.ToArray<FloatingInterestPayment>(null); // These will be sorted by the pay date.
          if (arrFlt != null && arrFlt.Length > 0)
          {
            for (int i = 0; i < arrFlt.Length; i++)
            {
              FloatingInterestPayment flp = arrFlt[i];
              flp.RateProjector = rateProjector;
              bool isCurrent = false;
              if (!rateResets.HasAllResets && rateResets.HasCurrentReset)
              {
                if (flp.ResetDate <= projector.AsOf && (i >= arrFlt.Length - 1
                                                        || arrFlt[i + 1].ResetDate > projector.AsOf))
                  isCurrent = true;
              }
              rateResets.UpdateResetsInCustomCashflowPayments(flp, isCurrent, false);
            }
          }
        }
        return ps;
      }

      var resetList = new List<RateReset>();
      foreach (var rateReset in rateResets)
      {
        resetList.Add(rateReset);
      }
      var amorts = loan.AmortizationSchedule;
      var cpn = loan.PricingGrid[currentLevel];
      if (!loan.IsFloating)
      {
        ps.AddPayments(LegacyCashflowCompatible.GetRegularPayments(from, to, schedule,
          loan.Ccy, loan.DayCount, new PaymentGenerationFlag(CashflowFlags, false, false),
          cpn, null, loan.Notional, amorts, referenceCurve, discountCurve, resetList, true));
      }
      else
      {
        ps.AddPayments(LegacyCashflowCompatible.GetRegularPayments(from, to, schedule,
          loan.Ccy, loan.DayCount, new PaymentGenerationFlag(CashflowFlags, false, false),
          cpn, null, loan.Notional, amorts, referenceCurve, discountCurve, resetList));

        //now we need to deal with the accrued of the first payment.
        if (LoanType != LoanType.Term && interestPeriods?.Count > 0
            && rateResets.Count > 0)
        {
          int idx = schedule.GetPrevCouponIndex(from);
          double newAccrued = LoanModel.FirstCouponValue(from,
            schedule.GetPeriodStart(idx + 1), schedule.GetPeriodEnd(idx + 1),
            Calendar, BDConvention, DayCount, IsFloating,
            (DiscountCurve)referenceCurve, PricingGrid[currentLevel],
            fee, drawn, interestPeriods, false);
          if (ps.Count > 0)
            ps.First().Amount = newAccrued;
        }

        if (floorValues?.Length > 0 && ps.Count > 0)
        {
          int j = 0;
          foreach (var p in ps)
          {
            var ip = p as InterestPayment;
            if (ip == null || ip.PayDt < from)
              continue;

            var startDt = ip.AccrualStart;
            if (!(LoanType == LoanType.Term && IsResetAvailable(startDt, resetList))
                && (j < floorValues.Length) && floorValues[j] > 0.0)
            {
              ip.EffectiveRate += floorValues[j];
            }
            j++;
          }
        }
      }
      if (!defaultDate.IsEmpty() && (to.IsEmpty() || defaultDate <= to))
      {
        // If defaulted, include the default settlement payment.
        if (dfltSettle.IsEmpty() || dfltSettle >= from)
        {
          double notionalAtDefault = amorts.PrincipalAt(loan.Notional, defaultDate);
          var defSettlement = new DefaultSettlement(defaultDate,
            dfltSettle, loan.Ccy, notionalAtDefault, recoveryRate);
          defSettlement.IsFunded = true;
          ps.AddPayment(defSettlement);
        }
      }
      return ps;
    }



    /// <summary>
    ///   Validate product
    /// </summary>
    ///
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    ///   
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      // Validate Loan
      //TODO: Validate the Loan

      // Validate schedules
      AmortizationUtil.Validate(amortSchedule_, errors);

      return;
    }

    /// <summary>
    /// Determines whether the Loan has a floating coupon.
    /// </summary>
    public bool IsFloating
    {
      get
      {
        return StringUtil.HasValue(this.Index);
      }
    }

    /// <summary>
    /// Determines whether the Loan principal amortizes or is a bullet payment at maturity.
    /// </summary>
    public bool IsAmortizing
    {
      get
      {
        return this.AmortizationSchedule.Count > 0;
      }
    }

    /// <summary>
    /// Determines whether the Loan is priced using performance coupons or a fixed spread.
    /// </summary>
    public bool HasPricingGrid
    {
      get { return (this.pricingGrid_ != null && this.pricingGrid_.Count > 1); }
    }

    /// <summary>
    /// Clones the Loan product.
    /// </summary>
    /// 
    /// <returns>A Loan.</returns>
    /// 
    public override object Clone()
    {
      return new Loan(this);
    }

    /// <summary>
    /// Return a Schedule instance for this product.
    /// </summary>
    public Schedule Schedule
    {
      get
      {
        ScheduleParams scParams = GetScheduleParams();
        return Schedule.CreateScheduleForCashflowFactory(scParams);
      }
    }

    #endregion Methods

    #region Properties
    /// <summary>
    /// The performance level to use if no performance (or only one) levels exist.
    /// </summary>
    public const string DefaultPerformanceLevel = "I";

    /// <summary>
    /// The type of loan.
    /// </summary>
    [Category("Base")]
    public LoanType LoanType
    {
      get { return loanType_; }
      set { loanType_ = value; }
    }

    /// <summary>
    /// The fee (in bps) paid on the undrawn portion of a revolving Loan.
    /// </summary>
    [Category("Base")]
    public double CommitmentFee
    {
      get { return commitmentFee_; }
      set { commitmentFee_ = value; }
    }

    /// <summary>
    /// The name of the reference interest rate index that floating coupons are calculated off.
    /// </summary>
    [Category("Base")]
    public string Index
    {
      get { return index_; }
      set { index_ = value; }
    }

    /// <summary>
    /// Whether to adjust the coupon period for rolled payments or simply adjust the payment dates.
    /// </summary>
    [Category("Base")]
    public bool PeriodAdjustment
    {
      get { return periodAdjustment_; }
      set { periodAdjustment_ = value; }
    }

    /// <summary>
    /// Cycle rule.
    /// </summary>
    [Category("Base")]
    public CycleRule CycleRule { get; set; }

    /// <summary>
    /// The number of days before the start of the next interest period that the coupon rate(s) is fixed.
    /// </summary>
    [Category("Base")]
    public int RateResetDays { get; set; }

    /// <summary>
    /// The calendar for determing payment dates.
    /// </summary>
    [Category("Base")]
    public Calendar Calendar
    {
      get { return calendar_; }
      set { calendar_ = value; }
    }

    /// <summary>
    /// The frequency of interest payments.
    /// </summary>
    [Category("Base")]
    public Frequency Frequency
    {
      get { return frequency_; }
      set { frequency_ = value; }
    }

    /// <summary>
    /// The roll methodology for payments occuring on weekends and/or holidays.
    /// </summary>
    [Category("Base")]
    public BDConvention BDConvention
    {
      get { return roll_; }
      set { roll_ = value; }
    }

    /// <summary>
    /// The method to use for day counting.
    /// </summary>
    [Category("Base")]
    public DayCount DayCount
    {
      get { return dayCount_; }
      set { dayCount_ = value; }
    }

    /// <summary>
    /// The date of the last full coupon payment.
    /// </summary>
    [Category("Base")]
    public Dt LastCoupon
    {
      get { return (!lastCoupon_.IsEmpty()) ? lastCoupon_ 
          : Schedule.DefaultLastCouponDate(FirstCoupon, Frequency, Maturity, false); }
      set { lastCoupon_ = value; }
    }

    /// <summary>
    /// The date of the first full coupon payment.
    /// </summary>
    [Category("Base")]
    public Dt FirstCoupon
    {
      get { return (!firstCoupon_.IsEmpty()) ? firstCoupon_ 
          : Schedule.DefaultFirstCouponDate(Effective, Frequency, Maturity, false); }
      set { firstCoupon_ = value; }
    }

    /// <summary>
    /// The date of the last draw that is allowed.
    /// </summary>
    [Category("Base")]
    public Dt LastDraw
    {
      get { return lastDraw_; }
      set { lastDraw_ = value; }
    }

    /// <summary>
    /// The performance levels for the Loan in order of likeliest 
    /// to default, where the first level is farther from default and the 
    /// last level is closest to default.
    /// </summary>
    [Category("Base")]
    public string[] PerformanceLevels
    {
      get { return levels_; }
      set { levels_ = value; }
    }

    /// <summary>
    /// The coupon schedule of the loan for each corresponding rating.
    /// </summary>
    [XmlIgnore]
    [Category("Base")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public IDictionary<string, double> PricingGrid
    {
      get
      {
        if (pricingGrid_ == null)
          pricingGrid_ = new Dictionary<string, double>();
        return pricingGrid_;
      }
    }

    /// <summary>
    /// The list of amortizations that define how the principal is paid down.
    /// </summary>
    [XmlIgnore]
    [Category("Base")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public IList<Amortization> AmortizationSchedule
    {
      get
      {
        if (amortSchedule_ == null)
          amortSchedule_ = new List<Amortization>();
        return amortSchedule_;
      }
    }

    /// <summary>
    /// The Loan ID, typically a LoanX ID or LIN from the Loan Identification Corporation.
    /// </summary>
    [Category("Base")]
    public string LoanId
    {
      get { return loanid_; }
      set { loanid_ = value; }
    }

    /// <summary>
    /// Drawn amount
    /// </summary>
    [Category("Base")]
    public double Drawn
    {
      get { return drawn_; }
      set { drawn_ = value; }
    }

    /// <summary>
    /// The loan floor.
    /// </summary>
    [Category("Base")]
    public double? LoanFloor
    {
      get { return floor_; }
      set { floor_ = value; }
    }

    /// <summary>
    /// Schedule terms.
    /// </summary>
    public ScheduleParams GetScheduleParams()
    {
      CashflowFlag flags = CashflowFlag.None;
      if (PeriodAdjustment)
        flags |= CashflowFlag.AdjustLast;
      else
        flags |= CashflowFlag.AccrueOnCycle;

      return new ScheduleParams(Effective, FirstCoupon, LastCoupon, 
        Maturity, Frequency, BDConvention, Calendar, CycleRule, flags);
    }

    // A private property for internal use.
    // This should only be set in Unit tests through reflection.
    private bool EnableNewCashflow
    {
      get { return enableNewCashflow_; }
    }

    /// <summary>
    /// Customized exercise schedule
    /// </summary>
    public List<CallPeriod> CallSchedule
    {
      get { return _callSchedule; }
      set { _callSchedule = value; }
    }

    internal CashflowFlag CashflowFlags
    {
      get
      {
        CashflowFlag flags = CashflowFlag.None;
        if (PeriodAdjustment)
          flags |= CashflowFlag.AdjustLast;
        else
          flags |= CashflowFlag.AccrueOnCycle;
        return flags;
      }
    }

    #endregion Properties

    #region Data

    private List<Amortization> amortSchedule_;
    private List<CallPeriod> _callSchedule = null; 
    private IDictionary<string, double> pricingGrid_;
    private Dt firstCoupon_ = Dt.Empty;
    private Dt lastCoupon_ = Dt.Empty;
    private Dt lastDraw_ = Dt.Empty;
    private DayCount dayCount_ = DayCount.Actual360;
    private BDConvention roll_ = BDConvention.None;
    private Frequency frequency_ = Frequency.None;
    private Calendar calendar_ = Calendar.None;
    private bool periodAdjustment_ = false;
    private string index_ = "";
    private LoanType loanType_;
    private double commitmentFee_;
    private string loanid_;
    private string[] levels_;
    private double  drawn_;
    private double? floor_;
    private readonly bool enableNewCashflow_ = 
      !ToolkitConfigurator.Settings.LoanModel.UseBackwardCompatibleCashflowForCLO;

    [Mutable]
    private Schedule schedule_;

    #endregion Data
  } // class Loan
}
