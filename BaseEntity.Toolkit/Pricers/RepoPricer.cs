//
// RepoLoanPricer.cs
// 
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Ccr;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Pricers
{
  #region RepoLoanPricer

  /// <summary>
  ///  Repo Loan pricer
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.RepoLoan" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.RepoLoan"/>
  [Serializable]
 	public partial class RepoLoanPricer : PricerBase, IPricer, ILockedRatesPricerProvider
  {
    #region Constructors

    /// <summary>
    ///   Fixed rate repo loan pricer
    /// </summary>
    /// <param name="product">Product to price</param>
    /// <param name="assetPricer">Collateral asset pricer</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="notional">Notional</param>
    /// <param name="discountCurve">Discount curve for pricing</param>
    /// <param name="survivalCurve">Survival curve for pricing</param>
    public RepoLoanPricer(RepoLoan product, IRepoAssetPricer assetPricer, Dt asOf, Dt settle, double notional, DiscountCurve discountCurve, SurvivalCurve survivalCurve)
      : this(product, assetPricer, asOf, settle, notional, discountCurve, null, null, null, survivalCurve) { }

    /// <summary>
    ///   Floating rate repo loan pricer
    /// </summary>
    /// <param name="product">Product to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="notional">Notional</param>
    /// <param name="rateIndex">Rate Index</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Floating rate reference curve</param>
    /// <param name="rateResets">Floating rate resets</param>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="assetPricer">Asset pricer</param>
    public RepoLoanPricer(RepoLoan product, IRepoAssetPricer assetPricer, Dt asOf, Dt settle, double notional, DiscountCurve discountCurve, 
      ReferenceIndex rateIndex, CalibratedCurve referenceCurve, RateResets rateResets, SurvivalCurve survivalCurve)
      : base(product, asOf, settle)
    {
      Notional = notional;
      DiscountCurve = discountCurve;
      SurvivalCurve = survivalCurve;
      ReferenceCurve = referenceCurve;
      ReferenceIsDiscount = DiscountCurve == ReferenceCurve;
      ReferenceIndex = rateIndex;
      RateResets = rateResets;
      DiscountingAccrued = settings_.SwapLegPricer.DiscountingAccrued;
      AssetPricer = assetPricer as PricerBase;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Shallow copy 
    /// </summary>
    /// <returns>A new swap leg pricer object.</returns>
    public override object Clone()
    {
      return new RepoLoanPricer(RepoLoan, AssetPricer as IRepoAssetPricer, 
        AsOf, Settle, Notional, DiscountCurve, ReferenceIndex, 
        ReferenceCurve, RateResets, SurvivalCurve);
    }

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;

      base.Validate(errors);

      if (DiscountCurve == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", "Invalid discount curve. Cannot be null");

      if (RepoLoan.Floating && ReferenceCurve == null)
        InvalidValue.AddError(errors, this, "ReferenceCurve", 
          string.Format("Invalid reference curve. Cannot be null for floating rate repo"));

      if (AssetPricer != null)
      { 
        (AssetPricer as PricerBase)?.Validate();
        if (ValuationCurrency != AssetPricer.ValuationCurrency)
          InvalidValue.AddError(errors, this, "ValuationCurrency",
            "RepoPricer and AssetPricer must have same valuation currency");

        // Check for correct notionals
        if (Math.Sign(Notional) * Math.Sign(AssetPricer.Notional) >= 0)
          InvalidValue.AddError(errors, this, "Notional",
            "Notional and asset pricer notional must have opposite sign");
      }
      if (RepoLoan.RepoType == RepoType.Repo || RepoLoan.RepoType == RepoType.SellBuyBack 
        || RepoLoan.RepoType == RepoType.SecurityLending)
      {
        if (Notional >= 0.0)
          InvalidValue.AddError(errors, this, "Notional",
            "Negative notional required for Repo, Sell Buy Back and Security Lending");
        if (AssetPricer!=null && AssetPricer.Notional <= 0.0)
          InvalidValue.AddError(errors, this, "AssetPricer.Notional",
            "Positive asset pricer notional required for Repo, Sell Buy Back and Security Lending");
      }
      else
      {
        if (Notional <= 0.0)
          InvalidValue.AddError(errors, this, "Notional",
            "Positive notional required for Reverse Repo, Buy Sell Back and Security Borrowing");
        if (AssetPricer != null && AssetPricer.Notional >= 0.0)
          InvalidValue.AddError(errors, this, "AssetPricer.Notional",
            "Negative asset pricer notional required for Reverse Repo, Buy Sell Back and Security Borrowing");
      }
    }

    /// <summary>
    ///   Reset the pricer
    ///   <preliminary/>
    /// </summary>
    ///
    /// <remarks>
    ///   
    /// </remarks>
    /// 
    /// <param name="what">The flags indicating what attributes to reset</param>
    /// <exclude/>
    public override void Reset(ResetAction what)
    {
      base.Reset(what);
    }

    /// <summary>
    /// Present value of a repoLoan at pricing as-of date given pricing arguments.
    /// For the open repo, the product pv includes the accrued 
    /// and the notional payments, there is no discouting.
    /// </summary>
    /// <returns>Present value of the repoLoan</returns>
    public override double ProductPv()
    {
      var repo = RepoLoan;
      if (!repo.Maturity.IsEmpty() && Settle >= repo.Maturity)
        return 0.0;

      if (repo.IsOpen)
        return Accrued() + Notional;

      var ps = GetPaymentSchedule(null, Settle);
      return ps.Pv(AsOf, Settle, DiscountCurve, SurvivalCurve, 
        IncludePaymentOnSettle, DiscountingAccrued) * Notional;
    }

    /// <summary>
    /// Gets the payments of the underlying asset and the 
    /// interest payments on those delayed asset income 
    /// payments reinvested at the fixed/floating repo rate.
    /// </summary>
    /// <param name="begin">The begin.</param>
    /// <param name="end">The end.</param>
    /// <returns>IEnumerable&lt;Payment&gt;.</returns>
    public IEnumerable<Payment> GetReinvestedAssetPayments(Dt begin, Dt end)
    {
      var ps = new PaymentSchedule();
      ps = AssetPricer.GetPaymentSchedule(ps, begin); // Not AsOf since we need all payments!
      foreach (var date in ps.GetPaymentDates())
      {
        if (date > end) continue; // Get reinvested payment on end date
        var payments = ps.GetPaymentsOnDate(date);

        // Scale payments by asset notional / notional because the 
        // payments are scaled by the notional in the calculations
        var unitAssetNotional = AssetNotional / Notional;
        foreach (var p in payments)
        {
          if (!RepoLoan.Floating)
          {
            // Fixed payments 
            yield return new ReinvestmentFixedInterestPayment(p.PayDt, RepoLoan.Maturity, 
              ValuationCurrency, p.PayDt, RepoLoan.Maturity,
              p.PayDt, RepoLoan.Maturity, Dt.Empty, p.Amount * unitAssetNotional, 
              RepoLoan.RepoRate, RepoLoan.DayCount, Frequency.None);
          }
          else
          {
            // Floating payments
            var projParams = GetProjectionParams();
            var rateProjector = GetRateProjector(projParams);
            var forwardAdjustment = GetForwardAdjustment(projParams);
            ps = ReinvestmentFloatingInterestPayment.FloatingRatePaymentSchedule(
              p.PayDt, RepoLoan.Maturity,
              RepoLoan.Ccy,
              rateProjector, forwardAdjustment, RateResets,
              RepoLoan.Schedule,
              RepoLoan.CashflowFlag,
              RepoLoan.RepoRate,
              null, // Initial notional
              p.Amount * unitAssetNotional,
              null, // Amortisations
              false, // Intermediate exchange
              RepoLoan.DayCount, null, // FxCurve
              projParams,
              null, // Cap
              null, // Floor
              null, // Rate multiplier schedule
              false, // Include trade settle cashflow
              Dt.Empty, Dt.Empty, // Default date and default settle dates
              null, // Payment lag
              null // Ex-dividend rule
              );

            foreach (var rp in ps)
            {
              yield return rp;
            }
          }
          yield return new DelayedPayment(p.PayDt, RepoLoan.Maturity, 
            p.Amount * unitAssetNotional, ValuationCurrency);
        }
      }
    }

    /// <summary>
    ///   Total accrued interest for product to settlement date given pricing arguments
    /// </summary>
    /// <returns>Total accrued interest</returns>
    public override double Accrued()
    {
      var repo = RepoLoan;
      var settle = Settle;
      var effective = repo.Effective;
      if (settle <= repo.Effective
        || (!repo.Maturity.IsEmpty() && settle >= repo.Maturity))
        return 0.0;

      if (!repo.Floating)
      {
        return CouponPeriodUtil.CalculateAccrual(effective,
            settle, repo.DayCount, repo.RateSchedule)*Notional
          + GetReInvestAccrued();
      }

      //floating
      return GetPsAccrued(GetPaymentSchedule(null, settle));
    }

    private double GetReInvestAccrued()
    {
      var repo = RepoLoan;
      var ps = new PaymentSchedule();
      if (repo.RepoType == RepoType.BuySellBack
        || repo.RepoType == RepoType.SellBuyBack)
      {
        var reinvestPayments = GetReinvestedAssetPayments(
          repo.Effective, repo.Maturity);
        if (reinvestPayments != null)
          ps.AddPayments(reinvestPayments);
      }
      return GetPsAccrued(ps);
    }

    private double GetPsAccrued(PaymentSchedule ps)
    {
      var repo = RepoLoan;
      var from = repo.IsOpen ? repo.Effective : Settle;
      var fip = ps.GetPaymentsByType<InterestPayment>()
        .Where(p => p.PayDt > from).ToArray();
      if (!fip.Any())
        return 0.0;
      var accrued = 0.0;
      foreach (var fi in fip)
      {
        double accrual;
        accrued += fi.Accrued(Settle, out accrual);
      }
      return accrued * Notional;
    }

    /// <summary>
    /// Cash value paid by the seller of assets to the buyer on 
    /// the repurchase date, equal to the purchase price 
    /// plus a return on the use of the cash over the term of the repo.
    /// </summary>
    /// <returns></returns>
    public double RepaymentAmount()
	  {
	    return Notional + RepoInterest() + AssetProceeds();
	  }

	  /// <summary>
	  ///  RepoLoan interest
	  /// </summary>
	  /// <returns></returns>
	  public double RepoInterest()
	  {
	    var repo = RepoLoan;
	    if (!repo.IsOpen && Settle > Product.EffectiveMaturity)
        return 0.0;
	    var paymentSchedule = GetPaymentSchedule(null, Settle);
	    var sum = paymentSchedule.GetPaymentsByType<InterestPayment>()
	      .Where(o => !(o is ReinvestmentFixedInterestPayment 
        || o is ReinvestmentFloatingInterestPayment))
	      .Sum(o => o.Amount);
      return sum * Notional;
	  }

    /// <summary>
    ///  Asset coupons and reinvestment interest for buy/sell backs
    /// </summary>
    /// <returns></returns>
    public double AssetProceeds()
    {
      return AssetIncome() + ReinvestmentInterest();
    }

    /// <summary>
    ///  Asset income for buy/sell backs
    /// </summary>
    /// <returns></returns>
    public double AssetIncome()
    {
      var repo = RepoLoan;
      var closeDate = repo.Maturity;
      if (!closeDate.IsEmpty() && Settle > closeDate)
        return 0.0;
      var paymentSchedule =GetPaymentSchedule(null, Settle);
      var sum = paymentSchedule.GetPaymentsByType<DelayedPayment>().Sum(o => o.Amount);
      return sum * Notional;
    }

    /// <summary>
    ///  Reinvestment Interest on any asset income payments for buy/sell backs
    /// </summary>
    /// <returns></returns>
    public double ReinvestmentInterest()
    {
      var repo = RepoLoan;
      var closeDate = repo.Maturity;
      if (!closeDate.IsEmpty() && Settle > closeDate)
        return 0.0;
      var paymentSchedule =GetPaymentSchedule(null, Settle);
      var sum = paymentSchedule.GetPaymentsByType<ReinvestmentFixedInterestPayment>()
        .Sum(o => o.Amount)
        + paymentSchedule.GetPaymentsByType<ReinvestmentFloatingInterestPayment>()
        .Sum(o => o.Amount);
      return sum * Notional;
    }
    
    /// <summary>
    /// Get the payment schedule: a detailed representation of payments
    /// </summary>
    /// <param name="paymentSchedule">Payment schedule</param>
    /// <param name="from">Include payments starting from from date</param>
    /// <returns>payments associated to the repoLoan</returns>
    /// <remarks>If there are no payments between from and to this method will return the payment/payments on the payment date immediately following from</remarks>
    public override PaymentSchedule GetPaymentSchedule(
      PaymentSchedule paymentSchedule, Dt from)
    {
      return GetPaymentSchedule(paymentSchedule, from, Dt.Empty);
    }

    private PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from, Dt to)
    {
      var repo = RepoLoan;
      if (ps == null)
        ps = new PaymentSchedule();
      else
        ps.Clear();

      //check the open repo is still active or not. If the repo is open,
      // and maturity is empty, that means the open repo is active, we 
      //don't produce specific payment schedule for it; but if the maturity
      // is not empty, that indicates the open repo has been closed out,
      // and it becomes a term repo, then produces the term repo payment schedule.
      if (repo.IsOpen && repo.Maturity.IsEmpty())
      {
        ps.AddPayment(new PrincipalExchange(repo.Effective, -repo.Notional, repo.Ccy));
        return ps;
      }
      
      var paySched = repo.Schedule;
      var accrueOnCycle = (repo.CashflowFlag & CashflowFlag.AccrueOnCycle) != 0;
      int firstIdx = (!to.IsEmpty() && to < from) // first index for schedule
        ? GetFirstPeriod(to, paySched, accrueOnCycle)
        : GetFirstPeriod(from, paySched, accrueOnCycle);

      var paymentFn = GetCreateInterestFn(paySched, firstIdx);

      double origPrincipal = (repo.Notional > 0.0) ? repo.Notional : 1.0;

      //handle with the step schedule
      double currentCpn = repo.RepoRate;
      var rateSched = repo.RateSchedule;
      if (rateSched != null && rateSched.Count > 0)
        currentCpn = CouponPeriodUtil.CouponAt(rateSched, currentCpn, from);

      int lastSchedIndex = paySched.Count - 1;
      for (int i = firstIdx; i <= lastSchedIndex; i++)
      {
        var ip = paymentFn(i, origPrincipal, currentCpn);
        ip.RateSchedule = rateSched;
        ps.AddPayment(ip);
        // Check if we stop here
        if (!to.IsEmpty() && to <= paySched.GetPeriodEnd(i))
        {
          break;
      }
      }

      // Buy/Sell Back only - include interim asset payments 
      //and reinvest at the agreed repo rate
      if (repo.RepoType == RepoType.BuySellBack 
        || repo.RepoType == RepoType.SellBuyBack)
      {
        var reinvestPayments = GetReinvestedAssetPayments(
          repo.Effective, repo.Maturity);
        if (reinvestPayments != null)
          ps.AddPayments(reinvestPayments);
      }

      ps.AddPayment(new PrincipalExchange(repo.Effective, -repo.Notional, repo.Ccy));

      Dt maturity = paySched.GetPaymentDate(paySched.Count - 1);
      if (!to.IsValid() || maturity <= to)
        ps.AddPayment(new PrincipalExchange(maturity, repo.Notional, repo.Ccy));

      return ps;
    }

    private ProjectionParams GetProjectionParams()
    {
      var flags = ProjectionFlag.None;
      if (RepoLoan.InArrears)
        flags |= ProjectionFlag.ResetInArrears;
      if (RepoLoan.IsZeroCoupon)
        flags |= ProjectionFlag.ZeroCoupon;
      if (RepoLoan.WithDelay)
        flags |= ProjectionFlag.ResetWithDelay;
      if (ApproximateForFastCalculation)
        flags |= ProjectionFlag.ApproximateProjection;
      var retVal = new ProjectionParams
      {
        ProjectionType = RepoLoan.ProjectionType,
        CompoundingFrequency = RepoLoan.CompoundingFrequency,
        CompoundingConvention = RepoLoan.CompoundingConvention,
        ResetLag = RepoLoan.ResetLag,
        YoYRateTenor = RepoLoan.IndexTenor,
        ProjectionFlags = flags
      };
      return retVal;
    }

    private IRateProjector GetRateProjector(ProjectionParams projectionParams)
    {
      var rateIndex = ReferenceIndex.Clone() as ReferenceIndex;
      if (rateIndex != null) 
        rateIndex.HistoricalObservations = ReferenceIndex.HistoricalObservations ?? RateResets; 
      return CouponCalculator.Get(AsOf, rateIndex, ReferenceCurve, DiscountCurve, projectionParams);
    }

    private IForwardAdjustment GetForwardAdjustment(ProjectionParams projectionParams)
    {
      return ForwardAdjustment.Get(AsOf, DiscountCurve, FwdRateModelParameters, projectionParams);
    }

    /// <summary>
    ///  Repo current exposure based on the collateral 
    ///  value movement and the repo interest accrued
    /// </summary>
    /// <returns></returns>
    public double CurrentExposure()
    {
      var repo = RepoLoan;
      var closeDate = repo.Maturity;
      if (!closeDate.IsEmpty() && Dt.Cmp(closeDate, Settle) < 0)
        return 0.0;

      var sign = Notional < 0.0 ? 1.0 : -1.0;

      var haircut = RepoLoan.Haircut;
      var securityPrice = ((IRepoAssetPricer)AssetPricer).SecurityMarketValue() * Math.Abs(AssetPricer.EffectiveNotional);
      var securityPrice0 = Math.Abs(EffectiveNotional);// / (1 - haircut);

      return sign * ((1.0 - haircut) * securityPrice - securityPrice0) + Accrued();
    }

    /// <summary>
    /// The current floating coupon rate
    /// </summary>
    /// <param name="includingProjection">Indicate whether to project the rate when rate reset is missing</param>
    /// <returns></returns>
    public double? CurrentFloatingCoupon(bool includingProjection)
    {
      return this.FloatingCouponAt(AsOf, Settle, RateResets, includingProjection);
    }


    private Func<int, double, double, InterestPayment>
    GetCreateInterestFn(Schedule schedule, int firstPeriod)
    {
      Func<int, double, double, InterestPayment> createInterestPaymentFn;
      var wr = RepoLoan;
      if (!wr.Floating)
      {
        createInterestPaymentFn = (i, notional, cpn) =>
        {
          var periodEnd = schedule.GetPeriodEnd(i);
          var ip = new FixedInterestPayment(
            GetPreviousPayDate(schedule, i), schedule.GetPaymentDate(i),
            wr.Ccy, schedule.GetCycleStart(i), schedule.GetCycleEnd(i),
            schedule.GetPeriodStart(i), periodEnd,
            Dt.Empty, notional, cpn, wr.DayCount, Frequency.None)
          {
            AccrueOnCycle = (wr.CashflowFlag & CashflowFlag.AccrueOnCycle) != 0,
            IncludeEndDateInAccrual = (wr.CashflowFlag & CashflowFlag.IncludeMaturityAccrual) != 0
              && i == schedule.Count - 1
          };
          return ip;
        };
      }
      else
      {
        var projParams = GetProjectionParams();
        var rateProjector = (CouponCalculator)GetRateProjector(projParams);
        var forwardAdjustment = GetForwardAdjustment(projParams);

        var resets = RateResets;
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
            GetPreviousPayDate(schedule, i), schedule.GetPaymentDate(i),
            wr.Ccy, schedule.GetCycleStart(i), schedule.GetCycleEnd(i),
            schedule.GetPeriodStart(i), periodEnd,
            Dt.Empty, notional, cpn, wr.DayCount,
            projParams.CompoundingFrequency, projParams.CompoundingConvention,
            rateProjector, forwardAdjustment, schedule.CycleRule, 1.0)
          {
            SpreadType = projParams.SpreadType,
            AccrueOnCycle = (wr.CashflowFlag & CashflowFlag.AccrueOnCycle) != 0,
            IncludeEndDateInAccrual
              = (wr.CashflowFlag & CashflowFlag.IncludeMaturityAccrual) != 0
              && i == schedule.Count - 1 // the last peroid
          };

          if (resets != null && ip.ResetDate < resetEnd)
          {
            resets.HandleResets(i == firstPeriod, i == firstPeriod + 1, ip);
            if (ip.EffectiveRateOverride != null &&
            !double.IsNaN(resets.CurrentReset)) ip.FixedCoupon = 0;
          }
          return ip;
        };
      }
      return createInterestPaymentFn;
    }

    private static Dt GetPreviousPayDate(Schedule schedule, int i)
    {
      return i <= 0 ? schedule.GetPeriodStart(0)
        : schedule.GetPaymentDate(i - 1);
    }

    private static int GetFirstPeriod(Dt asOf,
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

    #endregion Methods

    #region Properties

    /// <summary>
    ///  Repo asset pricer
    /// </summary>
    public PricerBase AssetPricer { get; }

    /// <summary>
    ///   Product
    /// </summary>
    public RepoLoan RepoLoan => (RepoLoan)Product;

    /// <summary>
	  ///   Discount Curve used for pricing
	  /// </summary>
	  public DiscountCurve DiscountCurve { get; private set; }

    /// <summary>
    /// Reference index 
    /// </summary>
    public ReferenceIndex ReferenceIndex { get; set; }

    /// <summary>
    /// Reference curve associated to the index. 
    /// </summary>
    public CalibratedCurve ReferenceCurve { get; set; }

    /// <summary>
    /// Survival curve in case payments are credit contingent
    /// </summary>
    public SurvivalCurve SurvivalCurve { get; set; }

    /// <summary>
    ///   Value of initial collateral
    /// </summary>
    public double CollateralNotional => -CurrentNotional / (1.0 - RepoLoan.Haircut);

    /// <summary>
    ///   Asset Notional
    /// </summary>
    public double AssetNotional => AssetPricer?.EffectiveNotional ?? 0.0;

    /// <summary>
    /// Accessor for the Discounting Accrued field for the SwapLegPricer
    /// </summary>
    public bool DiscountingAccrued { get; private set; }

    /// <summary>
    /// Historical Rate Resets
    /// </summary>
    public RateResets RateResets { get; internal set; }

    /// <summary>
    /// Set to true if the discount curve and the reference curve of the reference index are the same. By default it is true. 
    /// </summary>
    public bool ReferenceIsDiscount { get; private set; }

    /// <summary>
    /// Accessor for model parameters used for convexity adjustments
    /// </summary>
    public RateModelParameters FwdRateModelParameters { get; internal set; }

    /// <summary>
    /// Repo discount curves
    /// </summary>
    public DiscountCurve[] DiscountCurves
    {
      get
      {
        var list = new List<DiscountCurve>();
        var curve = ReferenceCurve as DiscountCurve;
        if (curve != null) list.Add(curve);
        curve = AssetPricer.GetReferenceCurve();
        if (curve != null && !list.Contains(curve)) list.Add(curve);
        curve = AssetPricer.GetDiscountCurve();
        if (curve != null && !list.Contains(curve)) list.Add(curve);
        curve = DiscountCurve;
        if (curve != null && !list.Contains(curve)) list.Add(curve);
        return list.ToArray();
      }
    }

    /// <summary>
    /// Reference curves
    /// </summary>
    public CalibratedCurve[] ReferenceCurves
    {
      get
      {
        CalibratedCurve ref1 = ReferenceCurve;
        CalibratedCurve ref2 = AssetPricer.GetReferenceCurve();
        return ref1 == ref2 ? new[] { ref1 } : new[] { ref1, ref2 };
      }
    }

    /// <summary>
    /// Survival curves
    /// </summary>
    public CalibratedCurve[] SurvivalCurves
    {
      get
      {
        CalibratedCurve sc1 = SurvivalCurve;
        CalibratedCurve sc2 = AssetPricer.GetSurvivalCurve();
        return sc1 == sc2 ? new[] { sc1 } : new[] { sc1, sc2 };
      }
    }
    
    /// <summary>
    /// Gets the valuation currency.  Only single currency repos are allowed at the moment.
    /// </summary>
    /// <value>The valuation currency.</value>
    public override Currency ValuationCurrency
    {
      get
      {
        if (RepoLoan.Ccy != AssetPricer.ValuationCurrency)
          throw new ArgumentException("Cash and Asset in Repo Pricer should be denominated in the same currency");
        return RepoLoan.Ccy;
      }
    }

    /// <summary>
    /// Since the maturity of open repo is pricer settle date,
    /// should include payment on settle.
    /// </summary>
    private bool IncludePaymentOnSettle => false;

    #endregion Properties

    #region ILockedRatesPricerProvider Members

    /// <summary>
    ///   Get a pricer in which all the rate fixings with the reset dates on
    ///   or before the anchor date are fixed at the current projected values.
    /// </summary>
    /// <param name="anchorDate">The anchor date.</param>
    /// <returns>The original pricer instance if no rate locked;
    ///   Otherwise, the cloned pricer with the rates locked.</returns>
    /// <remarks>This method never modifies the original pricer,
    ///  whose states and behaviors remain exactly the same before
    ///  and after calling this method.</remarks>
    IPricer ILockedRatesPricerProvider.LockRatesAt(Dt anchorDate)
    {
      var repoLoan = RepoLoan;
      if (!repoLoan.Floating)
        return this;

      RateResets modified;
      var ps = GetPaymentSchedule(null, AsOf);

      if (repoLoan.CompoundingConvention == CompoundingConvention.None ||
        (repoLoan.CustomPaymentSchedule != null &&
        repoLoan.CustomPaymentSchedule.Count > 0))
      {
        // For non-compounding coupon and custom schedule,
        // we just need to lock the coupons.
        modified = GetModifiedRateResets(RateResets,
          ps.EnumerateProjectedRates(), anchorDate);
        if (modified == null) return this;

        // We need return a modified pricer.
        var pricer = (RepoLoanPricer)ShallowCopy();
        pricer.RateResets = modified;
        return pricer;
      }
      else
      {
        // For regular case we need lock the rate fixings and
        // put them in ReferenceIndex.HistoricalObservations.
        modified = GetModifiedRateResets(ReferenceIndex.HistoricalObservations,
          EnumerateProjectedFixings(ps, false), anchorDate);
        if (modified == null) return this;

        // We need to modify both pricer and reference index,
        // so make both shallow copies.
        var pricer = (RepoLoanPricer)ShallowCopy();
        var index = pricer.ReferenceIndex =
          (ReferenceIndex)pricer.ReferenceIndex.ShallowCopy();
        index.HistoricalObservations = modified;
        return pricer;
      }
    }

    IPricer ILockedRatesPricerProvider.LockRateAt(Dt asOf, IPricer otherPricer)
    {
      var repoLoan = RepoLoan;
      if (!repoLoan.Floating)
        return this;

      RateResets modified;
      var ps = GetPaymentSchedule(null, asOf);

      var pmt = ps.GetPaymentsByType<FloatingInterestPayment>().FirstOrDefault(fip => fip.ResetDate == asOf);
      if (pmt == null) //there is no rate reset on the pricing date
        return this;


      if (repoLoan.CompoundingConvention == CompoundingConvention.None ||
        (repoLoan.CustomPaymentSchedule != null &&
        repoLoan.CustomPaymentSchedule.Count > 0))
      {
        // For non-compounding coupon and custom schedule,
        // we just need to lock the coupons.
        modified = GetModifiedRateResets(RateResets,
          ProjectAllFixings(asOf, ps, true), asOf);
        if (modified == null) return this;

        // We need return a modfied pricer.
        var pricer = (RepoLoanPricer)ShallowCopy();
        pricer.RateResets = modified;
        return pricer;
      }
      else
      {
        modified = GetModifiedRateResets(ReferenceIndex.HistoricalObservations,
          ProjectAllFixings(asOf, ps, false), asOf);
        if (modified == null) return this;

        var pricer = (RepoLoanPricer)ShallowCopy();
        var index = pricer.ReferenceIndex =
          (ReferenceIndex)pricer.ReferenceIndex.ShallowCopy();
        index.HistoricalObservations = modified;
        return pricer;
      }
    }

    //
    // The following are general utilities and maybe refactory to a separate class?
    //

    private static RateResets GetModifiedRateResets(RateResets oldRateResets,
      IEnumerable<RateReset> projectedRates, Dt anchorDate)
    {
      var resets = new SortedDictionary<Dt, double>();

      if (oldRateResets != null && oldRateResets.HasAllResets)
      {
        foreach (var rr in oldRateResets.AllResets)
        {
          if (rr.Key >= anchorDate || resets.ContainsKey(rr.Key))
            continue;

          resets.Add(rr.Key, rr.Value);
        }
      }

      int origCount = resets.Count;
      foreach (var rr in projectedRates)
      {
        if (rr.Date > anchorDate)
          continue;

        if (resets.ContainsKey(rr.Date))
        {
          if (rr.Date == anchorDate)
          {
            resets[rr.Date] = rr.Rate;
          }

          continue;
        }

        resets.Add(rr.Date, rr.Rate);
      }
      var retVal = resets.Count == origCount ? null : new RateResets(resets);
      if (oldRateResets != null && oldRateResets.HasCurrentReset && retVal != null)
      {
        retVal.CurrentReset = oldRateResets.CurrentReset;
      }

      return retVal;
    }

    // Enumerate the projected fixings instead of the coupon rates.
    // With compounding, a single coupon may consists of several fixings.
    private static IEnumerable<RateReset> ProjectAllFixings(Dt asOf,
      PaymentSchedule ps, bool withSpread)
    {
      if (ps == null) yield break;
      foreach (Dt d in ps.GetPaymentDates())
      {
        foreach (Payment p in ps.GetPaymentsOnDate(d))
        {
          var fip = p as FloatingInterestPayment;
          if (fip == null || (!fip.IsProjected && !RateResetUtil.ProjectMissingRateReset(fip.ResetDate, asOf, fip.PeriodStartDate))) continue;
          foreach (var rr in EnumerateProjectedFixings(fip, withSpread))
            yield return rr;
        }
      }
    }

    // Enumerate the projected fixings instead of the coupon rates.
    // With compounding, a single coupon may consists of several fixings.
    private static IEnumerable<RateReset> EnumerateProjectedFixings(
      PaymentSchedule ps, bool withSpread)
    {
      if (ps == null) yield break;
      foreach (Dt d in ps.GetPaymentDates())
      {
        foreach (Payment p in ps.GetPaymentsOnDate(d))
        {
          var fip = p as FloatingInterestPayment;
          if (fip == null || !fip.IsProjected) continue;
          foreach (var rr in EnumerateProjectedFixings(fip, withSpread))
            yield return rr;
        }
      }
    }

    // Find all the rate fixings used in the FloatingInterestPayment.
    private static IEnumerable<RateReset> EnumerateProjectedFixings(
      FloatingInterestPayment fip, bool withSpread)
    {
      RateReset reset;
      var projector = fip.RateProjector;
      var compoundingPeriods = fip.CompoundingPeriods;
      if (compoundingPeriods.Count > 1)
      {
        switch (fip.CompoundingConvention)
        {
          case CompoundingConvention.ISDA:
          case CompoundingConvention.FlatISDA:
          case CompoundingConvention.Simple:
            foreach (var period in compoundingPeriods)
            {
              reset = Project(projector, period.Item3, withSpread ? fip.FixedCoupon : 0.0);
              if (reset != null) yield return reset;
            }
            yield break;
          default:
            throw new ArgumentException("Compounding convention not supported");
        }
      }
      reset = Project(projector, compoundingPeriods[0].Item3, fip.FixedCoupon);
      if (reset != null) yield return reset;
    }

    // Find the projected rate for a single fixing.
    private static RateReset Project(IRateProjector projector, FixingSchedule fs, double spread)
    {
      var fixing = projector.Fixing(fs);
      return fixing == null || fixing.RateResetState != RateResetState.IsProjected
        ? null
        : new RateReset(fs.ResetDate, fixing.Forward + spread);
    }

    #endregion

  } // class RepoLoanPricer

  #endregion

}



