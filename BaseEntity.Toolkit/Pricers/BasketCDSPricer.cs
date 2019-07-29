/*
 * BasketCDSPricer.cs
 *
 *  -2008. All rights reserved.
 *
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Toolkit.Util.Configuration;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  ///  BasketCDSPricer class
  /// </summary>
  [Serializable]
  public partial class BasketCDSPricer : PricerBase, IPricer, IAnalyticDerivativesProvider
  {
    // Logger
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BasketCDSPricer));

    #region Config
    #endregion Config

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>For this constructor, the recovery rates are taken from
    ///   the survival curves.</para>
    /// </remarks>
    ///
    /// <param name="product">BasketCDS to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurves">Survival curves matching credits of Index</param>
    public BasketCDSPricer(BasketCDS product, Dt asOf, Dt settle,
      DiscountCurve discountCurve, SurvivalCurve[] survivalCurves)
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      SurvivalCurves = (survivalCurves != null && survivalCurves.Length == 0 ?
                        null : survivalCurves);
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>For this constructor, the recovery rates are taken from
    ///   the survival curves.</para>
    /// </remarks>
    ///
    /// <param name="product">BasketCDS to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    ///<param name="referenceCurve">Reference Curve for floating payments forecast</param>
    /// <param name="survivalCurves">Survival curves matching credits of Index</param>
    public BasketCDSPricer(BasketCDS product, Dt asOf, Dt settle,
      DiscountCurve discountCurve, DiscountCurve referenceCurve, SurvivalCurve[] survivalCurves)
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      ReferenceCurve = referenceCurve;
      SurvivalCurves = (survivalCurves != null && survivalCurves.Length == 0 ?
                        null : survivalCurves);
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This is a constructor for pricing BasketCDS based on market quote
    ///   only.  No link is made to undelying portfolio.</para>
    /// </remarks>
    ///
    /// <param name="product">BasketCDS to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    public BasketCDSPricer(BasketCDS product, Dt asOf, Dt settle, DiscountCurve discountCurve)
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      SurvivalCurves = null;
      cashflow_ = null;
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This is a constructor for pricing BasketCDS based on market quote
    ///   only.  No link is made to undelying portfolio.</para>
    /// </remarks>
    ///
    /// <param name="product">BasketCDS to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Reference Curve for floating payments forecast</param>
    public BasketCDSPricer(BasketCDS product, Dt asOf, Dt settle, DiscountCurve discountCurve, DiscountCurve referenceCurve)
      : base(product, asOf, settle)
    {
      DiscountCurve = discountCurve;
      ReferenceCurve = referenceCurve;
      SurvivalCurves = null;
      cashflow_ = null;
    }

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      BasketCDSPricer obj = (BasketCDSPricer)base.Clone();

      obj.survivalCurves_ = CloneUtil.Clone<SurvivalCurve>(survivalCurves_);
      obj.discountCurve_ = CloneUtil.Clone(discountCurve_);
      obj.referenceCurve_ = CloneUtil.Clone(referenceCurve_);
      obj.cashflow_ = null;
      obj.rateResets_ = CloneUtil.Clone(rateResets_);
      return obj;
    }

    /// <summary>
    ///  Validate BasketCDSPricer by validating bases iteratively
    /// </summary>
    /// <param name="errors">Error list of validating</param>
    public override void Validate(ArrayList errors)
    {
      if (!IsActive())
        return;

      base.Validate(errors);

      if (this.discountCurve_ == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));

      // Validate schedules
      RateResetUtil.Validate(rateResets_, errors);

      // market methods are enabled by setting MarketQuote
      if (!marketQuote_.Equals(Double.NaN))
      {
        if (marketQuote_ < 0.0 || marketQuote_ > 200.0)
          InvalidValue.AddError(errors, this, "MarketQuote", String.Format("Invalid market quote. Must be between 0 and 200, Not {0}", marketQuote_));

        if (!(quotingConvention_ == QuotingConvention.CreditSpread || quotingConvention_ == QuotingConvention.FlatPrice))
          InvalidValue.AddError(errors, this, "QuotingConvention", String.Format("Invalid quoting convention. Must be either creditspread or flatprice", quotingConvention_));
      }
      return;
    }

    /// <summary>
    /// Payment Pricer
    /// </summary>
    public override IPricer PaymentPricer
    {
      get
      {
        if (Payment != null)
        {
          if (paymentPricer_ == null)
            paymentPricer_ = BuildPaymentPricer(Payment, discountCurve_);
        }
        return paymentPricer_;
      }
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ps"></param>
    /// <param name="from"></param>
    /// <returns></returns>
    public PaymentSchedule GeneratePayments(PaymentSchedule ps, Dt from)
    {
      BasketCDS note = BasketCDS;
      DiscountCurve referenceCurve = ReferenceCurve ?? DiscountCurve;

      ps = CDXPricerUtil.GetPaymentSchedule(ps, from, 
        Settle, note.Effective, note.FirstPrem, note.Maturity, Dt.Empty, 
        note.Ccy, note.Premium, note.DayCount, note.Freq, note.CycleRule, 
        note.BDConvention, note.Calendar, 
        note.BasketCdsType != BasketCDSType.Unfunded,
        1.0, DiscountCurve, referenceCurve, 
        note.BasketCdsType == BasketCDSType.FundedFloating, 
        RateResets, SurvivalCurves, note.Weights, RecoveryCurves);
      if (note.Bullet != null)
      {
        // do we need to find the last payment date from ps?
        ps.AddPayment(new PrincipalExchange(note.Maturity, 
          note.Bullet.CouponRate, note.Ccy));
      }

      return ps;
    }


    /// <summary>
    ///   Calculate the protection pv of the BasketCDS product
    ///   By definition, ProtectionPv = Sum(ProtectionPv for individual underlying)
    /// </summary>
    ///
    /// <returns>Protection leg of the BasketCDS</returns>
    /// <exclude/>
    public double ProtectionPv()
    {
      logger.Debug("Calculating the protection pv of BasketCDS...");

      // In getting CDS pricer, we set a 0-rate flat discount curve
      // for bullet payment since it's on maturity. The pricer sets
      // regular reference discount curve.
      CDSCashflowPricer pricer = GetCDSPricer();
      pricer.Notional = this.Notional;

      // The individual CDS pricer handles the Funded/Unfunded cases
      // EvaluateAdditive sets each survial curve to pricer, then sum.
      double totalProtPv = EvaluateAdditive(pricer,
        delegate(CDSCashflowPricer p) { return p.ProtectionPv(); });

      logger.DebugFormat("Returning index protection pv {0}", totalProtPv);
      return totalProtPv;
    }

    /// <summary>
    ///  Calculate the present value of fee leg with accrual
    /// </summary>
    /// <returns>FeePv</returns>
    public double FeePv()
    {
      logger.Debug("Calculating the fee pv of BasketCDS...");

      CDSCashflowPricer pricer = GetCDSPricer();
      pricer.Notional = this.Notional;

      // The individual CDS pricer handles the Funded/Unfunded cases
      // EvaluateAdditive sets each survial curve to pricer, then sum.
      double totalFeePv = EvaluateAdditive(pricer,
        delegate(CDSCashflowPricer p) { return p.FeePv() - p.Accrued(); });

      // bonus handled in cds cashflow, no need to 
      // add separately as below:
      // totalFeePv += BulletCouponUtil.GetDiscountedBonus(this);

      totalFeePv += Accrued();

      logger.DebugFormat("Returning index protection pv {0}", totalFeePv);
      return totalFeePv;
    }

    /// <summary>
    ///  Calculate the full Pv
    /// </summary>
    /// <returns>Full Pv</returns>
    public override double ProductPv()
    {
      return ProtectionPv() + FeePv();
    }

    /// <summary>
    ///  Compute the number of accrual days
    /// </summary>
    /// <returns>Number of accrual days</returns>
    public int AccrualDays()
    {
      return GetCDSPricer().AccrualDays();
    }

    /// <summary>
    ///  Compute the amount of the accrued
    /// </summary>
    /// <returns>The amount of accrued</returns>
    public override double Accrued()
    {
      logger.Debug("Calculating the accrual of BasketCDS...");

      ICDSPricer pricer = GetCDSPricer();
      pricer.Notional = this.Notional;

      if (survivalCurves_ == null)
        throw new ArgumentException("BasketCDS Relative value calcs require SurvivalCurves to be set");

      double[] weights = BasketCDS.Weights;
      if (survivalCurves_.Length == 1)
        weights = null;

      double totalAccrual = 0;
      for (int i = 0; i < survivalCurves_.Length; i++)
      {
        pricer.SurvivalCurve = survivalCurves_[i];
        if (survivalCurves_[i].SurvivalCalibrator != null)
          pricer.RecoveryCurve = survivalCurves_[i].SurvivalCalibrator.RecoveryCurve;
        else
          pricer.RecoveryCurve = new RecoveryCurve(SurvivalCurves[i].AsOf, 0.4);

        double accrual = pricer.Accrued();

        totalAccrual += accrual * ((weights != null) ? weights[i] : (1.0 / survivalCurves_.Length));
      }
      return totalAccrual;
    }

    /// <summary>
    ///  Compute the risky duration for BasketCDS
    /// </summary>
    /// <returns>Risky duration</returns>
    public double RiskyDuration()
    {
      logger.Debug("Calculating risky duration of BasketCDS ...");

      CDSCashflowPricer pricer = GetCDSPricer();

      double totRiskyDuration = EvaluateAdditive(pricer,
        delegate(CDSCashflowPricer p) { return p.RiskyDuration(); });

      logger.DebugFormat("Returning intrinsic risky duration {0}", totRiskyDuration);
      return totRiskyDuration;
    }

    /// <summary>
    ///  Compute the carry defined as: Carry = (1-day premium) * EffectiveNotional = premium / 360 * EffectiveNotional
    /// </summary>
    /// <returns>Carry</returns>
    public double Carry()
    {
      if(BasketCDS.BasketCdsType != BasketCDSType.FundedFloating)
        return BasketCDS.Premium / 360 * CurrentNotional;
      logger.Debug("Calculating the accrual of BasketCDS...");
      ICDSPricer pricer = GetCDSPricer();
      pricer.Notional = this.Notional;
      if (survivalCurves_ == null)
        throw new ArgumentException("BasketCDS Relative value calcs require SurvivalCurves to be set");
      double[] weights = BasketCDS.Weights;
      if (survivalCurves_.Length == 1)
        weights = null;
      double totalCarry = 0;
      for (int i = 0; i < survivalCurves_.Length; i++)
      {
        pricer.SurvivalCurve = survivalCurves_[i];
        if (survivalCurves_[i].SurvivalCalibrator != null)
          pricer.RecoveryCurve = survivalCurves_[i].SurvivalCalibrator.RecoveryCurve;
        else
          pricer.RecoveryCurve = new RecoveryCurve(SurvivalCurves[i].AsOf, 0.4);

        double carry = pricer.Carry();
        totalCarry += carry * ((weights != null) ? weights[i] : (1.0 / survivalCurves_.Length));
      }
      return totalCarry;
    }

    /// <summary>
    ///  Compute the mark-to-market carry: MTMCarry = Break Even Premium / 360 * EffectiveNotional
    /// </summary>
    /// <returns></returns>
    public double MTMCarry()
    {
      return BreakEvenPremium() / 360 * CurrentNotional;
    }

    /// <summary>
    ///  Compute break even premium for BasketCDS
    /// </summary>
    /// <returns>Break even premium</returns>
    public double BreakEvenPremium()
    {
      if (BasketCDS.BasketCdsType == BasketCDSType.Unfunded)
        return BreakEvenPremiumUnfunded();
      else
      {
        double bep = Double.NaN;
        BasketCDSType savedType = BasketCDS.BasketCdsType;
        try
        {
          BasketCDS.BasketCdsType = BasketCDSType.Unfunded;
          this.Reset();
          bep = BreakEvenPremiumUnfunded();
        }
        catch (Exception)
        { }
        finally
        {
          BasketCDS.BasketCdsType = savedType;
          this.Reset();
          if (Double.IsNaN(bep))
            throw new ToolkitException("Cannot bracket a solution");
        }
        return bep;
      }
    }

    /// <summary>
    ///  Calculate breakeven fee for BasketCDS
    /// </summary>
    /// <param name="runningPremium">Running premium</param>
    /// <returns>Breakeven fee</returns>
    public double BreakevenFee(double runningPremium)
    {
      if (BasketCDS.BasketCdsType == BasketCDSType.Unfunded)
        return BreakevenFeeUnfunded(runningPremium);
      else
      {
        double bef = Double.NaN;
        BasketCDSType savedType = BasketCDS.BasketCdsType;
        try
        {
          BasketCDS.BasketCdsType = BasketCDSType.Unfunded;
          this.Reset();
          bef = BreakevenFeeUnfunded(runningPremium);
        }
        catch (Exception)
        { }
        finally
        {
          BasketCDS.BasketCdsType = savedType;
          this.Reset();
        }
        return bef;
      }
    }

    // Helper method to calculate breakeven fee for Unfunded BasketCDS
    private double BreakevenFeeUnfunded(double runningPremium)
    {
      if (BasketCDS.BasketCdsType == BasketCDSType.Unfunded)
      {
        double feeCopy = BasketCDS.Fee;
        // If FeeSettle is empty or ealier, the BEF should be one for a 
        // new contract with FeeSettle set on 3 days after asof date.
        if (BasketCDS.FeeSettle.IsEmpty() || Dt.Cmp(AsOf, BasketCDS.FeeSettle) < 0)
          BasketCDS.FeeSettle = Dt.Add(AsOf, 3, TimeUnit.Days);
        double premiumCopy = BasketCDS.Premium;
        this.BasketCDS.Fee = 0;
        if (!Double.IsNaN(runningPremium))
          BasketCDS.Premium = runningPremium;
        this.Reset();
        double feeSettleDiscount = DiscountCurve.DiscountFactor(AsOf, BasketCDS.FeeSettle);
        double bef = Double.NaN;
        try
        {
          bef = (ProtectionPv() + FeePv() - Accrued()) / (Notional * feeSettleDiscount);
        }
        catch (Exception ex)
        {
          throw new ToolkitException("BreakevenFee calculation failed due to " + ex.ToString());
        }
        finally
        {
          // Restore the original upfront fee
          BasketCDS.Fee = feeCopy;
          if (!Double.IsNaN(runningPremium))
            BasketCDS.Premium = premiumCopy;
          this.Reset();
        }
        return -bef;
      }
      else
        throw new ArgumentException("BasketCDS type must be Unfunded");
    }

    // Helper method to calculate breakeven premium for Unfunded BasketCDS
    private double BreakEvenPremiumUnfunded()
    {
      if (BasketCDS.BasketCdsType != BasketCDSType.Unfunded)
        throw new ArgumentException("BasketCDS type must be Unfunded");

      logger.Debug("Calculating breakeven premium of BasketCDS...");

      CDSCashflowPricer pricer = GetCDSPricer();
      double be = 0.0;
      double protectPv = -EvaluateAdditive(pricer,
        delegate(CDSCashflowPricer p)
        {
          // ignore bonus for BE
          p.CDS.Bullet = null;
          return p.ProtectionPv();
        });
      double duration = EvaluateAdditive(pricer,
        delegate(CDSCashflowPricer p)
        {
          // ignore bonus for BE
          p.CDS.Bullet = null;
          return p.RiskyDuration();
        });
      be = protectPv / duration;

      logger.DebugFormat("Returning index intrinsic break even premium {0}", be);
      return be;
    }

    /// <summary>
    ///  Compute the forward premium of BasketCDS. This is different from CDX forward premium
    ///  which is computed by equivalent CDS pricer (using flat curve). This forward premium
    ///  uses each underlying cds curve (non-flat in general).
    /// </summary>
    /// <param name="forwardSettle">Forward settle date</param>
    /// <returns>Forward premium</returns>
    public double FwdPremium(Dt forwardSettle)
    {
      if (SurvivalCurves == null || SurvivalCurves.Length == 0)
        throw new ArgumentException("Portfolio of BasketCDS has no survival curves set up");
      // Test forward settlement on or after product effective date
      if (this.Product != null && (this.Product.Effective > forwardSettle))
        forwardSettle = this.Product.Effective;

      double[] weights = BasketCDS.Weights;
      if (SurvivalCurves.Length == 1)
        weights = null;

      if (BasketCDS.BasketCdsType == BasketCDSType.Unfunded) // for Unfunded case
      {
        double totFv01 = 0.0, totFwdProt = 0.0;
        double totfwdPremium = 0.0;
        var n = SurvivalCurves.Length;
        for (int i = 0; i < n; ++i)
        {
          CDSCashflowPricer pricer = GetCDSPricer(SurvivalCurves[i]);
          var cds = pricer.CDS;
          var bullet = cds.Bullet;
          var weight = weights?[i] ?? (1.0 / n);
          try
          {
            cds.Bullet = null;

            // Price stream of 1bp premiums matching CDS
            var ps = pricer.GeneratePayments01(AsOf, forwardSettle,
              SurvivalCurves[i], false);

            var recovery = pricer.GetRecoveryRate();
            var defaultRisk = pricer.GetDefaultRiskCalculator(forwardSettle, forwardSettle);
            totFv01 += weight * CDSCashflowPricer.RegularPv(ps, forwardSettle,
                         forwardSettle, DiscountCurve, defaultRisk,
                         recovery, false, cds.CashflowFlag, true, false,
                         false, pricer.DiscountingAccrued);

            totFwdProt += weight * (pricer.QuantoCapAdjustment + CDSCashflowPricer.RegularPv(ps,
                                      forwardSettle, forwardSettle, DiscountCurve, defaultRisk,
                                      recovery, false, cds.CashflowFlag, false, true,
                                      false, pricer.DiscountingAccrued));

            const double tiny = double.Epsilon;
            if (Math.Abs(totFv01) < tiny && Math.Abs(totFwdProt) < tiny)
              return 0.0; // this may happen when the curve is defaulted.
            totfwdPremium = (-totFwdProt / totFv01) / 10000;
          }
          finally
          {
            cds.Bullet = bullet;
          }

        }
        return totfwdPremium;
      }
      else  //funded case
      {
        // search for premium which sets the Clean Price = Pv - Accrued == 1
        Brent rf = new Brent();
        rf.setToleranceX(1e-6);
        rf.setToleranceF(1e-10);
        rf.setLowerBounds(1E-10);

        Double_Double_Fn fn_ = new Double_Double_Fn(this.EvaluateFlatPriceForBEPremium);
        DelegateSolverFn solveFn_ = new DelegateSolverFn(fn_, null);

        // Solve
        double res;
        double origPremium = BasketCDS.Premium;
        Dt origAsOf = this.AsOf;
        Dt origSettle = this.Settle;
        this.AsOf = forwardSettle;
        this.Settle = forwardSettle;
        try
        {
          res = rf.solve(solveFn_, 1.0, 1e-6, BasketCDS.Premium * 100.0);
        }
        finally
        {
          // Tidy up transient data
          fn_ = null;
          solveFn_ = null;
          // restore orig premium, asof, settle
          BasketCDS.Premium = origPremium;
          this.AsOf = origAsOf;
          this.Settle = origSettle;
        }
        return res;
      }
    }

    /// <summary>
    ///  Compute the forward premium 01
    /// </summary>
    /// <param name="forwardSettle">Forward date</param>
    /// <returns>Forward premium 01</returns>
    public double FwdPremium01(Dt forwardSettle)
    {
      if (SurvivalCurves == null || SurvivalCurves.Length == 0)
        throw new ArgumentException("Portfolio of BasketCDS has no survival curves set up");

      // Test forward settlement on or after product effective date
      if (this.Product != null && (this.Product.Effective > forwardSettle))
        forwardSettle = this.Product.Effective;

      double[] weights = BasketCDS.Weights;
      if (SurvivalCurves.Length == 1)
        weights = null;

      double totFv01 = 0;
      double originalPremium = BasketCDS.Premium;
      var length = SurvivalCurves.Length;
      for (int i = 0; i < length; ++i)
      {
        try
        {
          BasketCDS.Premium = BasketCDS.Premium + 0.0001;
          CDSCashflowPricer pricer = GetCDSPricer(SurvivalCurves[i]);
          double pcn = pricer.CurrentNotional;
          var fv01 = pcn.AlmostEquals(0.0)
            ? 0.0 : pricer.PsFwdPremium01(forwardSettle) * (EffectiveNotional / pcn);
          totFv01 += fv01 * (weights?[i] ?? (1.0 / length));
        }
        finally
        {
          BasketCDS.Premium = originalPremium;
        }
      }

      return totFv01;
    }

    /// <inheritdoc cref="Sensitivities.Recovery01(IPricer, string, double, double, bool, bool[])" />
    /// <param name="measure">String-type measure such as "Pv"</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="recalibrate">Boolean recalibrate</param>
    public double Recovery01(string measure, double upBump, double downBump, bool recalibrate)
    {
      BasketCDSPricer pricer = (BasketCDSPricer)CloneUtil.Clone(this);
      return Sensitivities.Recovery01(pricer, measure, upBump, downBump, recalibrate);
    }

    /// <summary>
    ///  Compute the interest rate sensitivity
    /// </summary>
    /// <param name="measure">String-typed measure such as "Pv"</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="recalibrate">Boolean recalibrate</param>
    /// <returns></returns>
    public double Rate01(string measure, double upBump, double downBump, bool recalibrate)
    {
      BasketCDSPricer pricer = (BasketCDSPricer)CloneUtil.Clone(this);
      return Sensitivities.IR01(pricer, measure, upBump, downBump, recalibrate);
    }

    /// <inheritdoc cref="Sensitivities.Spread01(IPricer, string, double, double, bool[])" />
    /// <param name="measure">Target measure to evaluate (FeePv, ProtectionPv, BreakEvenPremium, etc.)</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpFlags">Bump flags, such as BumpRelative, BumpDown, BumpInPlace etc.</param>
    public double Spread01(string measure, double upBump, double downBump, BumpFlags bumpFlags)
    {
      BasketCDSPricer pricer = (BasketCDSPricer)CloneUtil.Clone(this);
      return Sensitivities2.Spread01(pricer, measure, upBump, downBump, bumpFlags);
    }



    /// <summary>
    /// Calculate the Spread Gamma for BasketCDS pricer's specific measure
    /// </summary>
    /// <param name="measure">Target measure to evaluate (FeePv, ProtectionPv, BreakEvenPremium, etc.)</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpFlags">bump flags (BumpInPlace, RemapCorrelation, BumpRelative,etc)</param>
    /// <returns>Spread Gamma</returns>
    public double SpreadGamma(string measure, double upBump, double downBump, BumpFlags bumpFlags)
    {
      BasketCDSPricer pricer = (BasketCDSPricer)CloneUtil.Clone(this);
      return Sensitivities2.SpreadGamma(pricer, measure, upBump, downBump, bumpFlags);
    }



    /// <summary>
    ///   Calculate the Notional of the sum of the CDS for each underlying credit 
    /// </summary>
    /// <param name="measure">Target measure to evaluate (FeePv, ProtectionPv, BreakEvenPremium, etc.)</param>
    /// <param name="hedgeTenor">Tenor name to hedge</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <returns></returns>
    public double SpreadHedge(string measure, string hedgeTenor, double upBump, double downBump)
    {
      BasketCDSPricer pricer = (BasketCDSPricer)CloneUtil.Clone(this);
      return Sensitivities2.SpreadHedge(pricer, measure, hedgeTenor, upBump, downBump, BumpFlags.BumpInPlace);
    }

    /// <summary>
    /// Calculate the Notional of the sum of the CDS for each underlying credit (has bumpflag parameter)
    /// </summary>
    /// <param name="measure">Target measure to evaluate (FeePv, ProtectionPv, BreakEvenPremium, etc.)</param>
    /// <param name="hedgeTenor">Tenor name to hedge</param>
    /// <param name="upBump">Up bump size</param>
    /// <param name="downBump">Down bump size</param>
    /// <param name="bumpFlags">bump flags (BumpInPlace, RemapCorrelation, BumpRelative,etc)</param>
    /// <returns>Calculated spread hedge notional</returns>
    public double SpreadHedge(string measure, string hedgeTenor, double upBump, double downBump, BumpFlags bumpFlags)
    {
      BasketCDSPricer pricer = (BasketCDSPricer)CloneUtil.Clone(this);
      return Sensitivities2.SpreadHedge(pricer, measure, hedgeTenor, upBump, downBump, bumpFlags);
    }



    /// <summary>
    /// 
    /// </summary>
    /// <param name="measure">Target measure to evaluate (FeePv, ProtectionPv, BreakEvenPremium, etc.)</param>
    /// <param name="toAsOf">Pricing as-of date for future pricing</param>
    /// <param name="toSettle">Pricing settle date for future pricing</param>
    /// <param name="clean">Calculate on clean rather than full price</param>
    /// <returns></returns>
    public double Theta(string measure, Dt toAsOf, Dt toSettle, bool clean)
    {
      BasketCDSPricer pricer = (BasketCDSPricer)CloneUtil.Clone(this);
      return Sensitivities.Theta(pricer, measure, toAsOf, toSettle, clean 
        ? ThetaFlags.Clean : ThetaFlags.None, SensitivityRescaleStrikes.No);
    }

    /// <summary>
    ///   Compute the default (event) sensitivity.
    /// </summary>
    /// <remarks>
    ///   <details>
    ///   <para>The Default or VOD sensitivity is the change in MTM given an instintaneous
    ///   default of the underlying credit curve with the lowest 5Yr survival probability
    ///   (highest spread).</para>
    ///   </details>
    /// </remarks>
    /// <param name="measure">Target measure to evaluate</param>
    /// <returns>Default sensitivity</returns>
    public double VOD(string measure)
    {
      BasketCDSPricer pricer = (BasketCDSPricer)CloneUtil.Clone(this);
      return Sensitivities.VOD(pricer, measure);
    }

    /// <summary>
    ///   Expected survival of notional up to a forward date
    /// </summary>
    /// <param name="date">The forward date</param>
    /// <returns>Expected survival of notional</returns>
    public double SurvivalToDate(Dt date)
    {
      if (date < this.AsOf)
        throw new ArgumentException("Date " + date.ToStr("%D") +
          " for survival cannot be earlier than " + this.AsOf.ToStr("%D"));
      if (date == this.AsOf)
        return 1.0;
      CDSCashflowPricer pricer = GetCDSPricer();
      double survival = EvaluateAdditive(pricer,
        delegate(CDSCashflowPricer p) { return p.SurvivalProbability(date); });
      return survival;
    }

    /// <summary>
    ///   Expected cumulative losses up to a forward date
    /// </summary>
    /// <param name="date">The forward date</param>
    /// <returns>Expected cumulative losses</returns>
    public double LossToDate(Dt date)
    {
      if (date < this.AsOf)
        throw new ArgumentException("Date " + date.ToStr("%D") +
          " for loss cannot be earlier than pricing date " + this.AsOf.ToStr("%D"));
      if (date == this.AsOf)
        return 0;
      CDSCashflowPricer pricer = GetCDSPricer();
      double loss = EvaluateAdditive(pricer,
        delegate(CDSCashflowPricer p)
        {
          return (1 - p.SurvivalProbability(date)) * (1 - p.RecoveryRate);
        });
      return loss * Notional;
    }

    #endregion Methods

    #region MarketMethods

    /// <summary>
    ///   Calculate the market value of the BasketCDS given a 
    ///   quoted level for a CDX on the same basket
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The market value is the settlement value in dollars (or other currency).</para>
    ///
    ///   <para>The calculation of the market value is based on market
    ///   convention. If the market quote is quoted as a price the dollar value is easily calculated.
    ///   For spread quotes, first, construct a single name CDS with the same characteristics as
    ///   the index.  In particular the CDS premium is the index deal spread.
    ///   The convention is to then price this product using a flat credit curve at the market quote spread
    ///   and a standard (default 40<formula inline="true">\%</formula>) recovery.</para>
    ///
    ///   <para>The Market Value includes the accrued</para>
    /// </remarks>
    /// <returns>Value of the BasketCDS note at current market quoted level</returns>
    public double
    MarketValue()
    {
      return MarketValue(MarketQuote);
    }

    /// <summary>
    ///   Calculate the market value of the BasketCDS given a 
    ///   quoted level for a CDX on the same basket
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The market value is the settlement value in dollars (or other currency).</para>
    ///
    ///   <para>The calculation of the market value is based on market
    ///   convention. If the market quote is quoted as a price the dollar value is easily calculated.
    ///   For spread quotes, first, construct a single name CDS with the same characteristics as
    ///   the index.  In particular the CDS premium is the index deal spread.
    ///   The convention is to then price this product using a flat credit curve at the market quote spread
    ///   and a standard (default 40<formula inline="true">\%</formula>) recovery.</para>
    ///
    ///   <para>The Market Value includes the accrued</para>
    /// </remarks>
    ///
    /// <param name="marketQuote">Current market quote. If QuotingConvention is CreditSpread 100bp = 0.0100, if FlatPrice par = 1.0</param>
    ///
    /// <returns>Value of the BasketCDS note at current market quoted level</returns>
    ///
    public double
    MarketValue(double marketQuote)
    {
      double marketPrem = marketQuote;
      if (QuotingConvention == QuotingConvention.FlatPrice)
      {
        marketPrem = CDXPricerUtil.CDXPriceToSpread(AsOf, Settle, BasketCDS.MarketCDX, marketQuote, DiscountCurve,
                                                    SurvivalCurves, MarketRecoveryRate, CurrentRate);
      }
      double pv = 0;
      double currentNotional = CurrentNotional;
      if (Math.Abs(currentNotional) >= 1E-15)
      {
        CDSCashflowPricer pricer = GetEquivalentCDSPricer(marketPrem);
        pv += pricer.ProductPv() - pricer.Accrued() * (1 - (currentNotional / pricer.Notional));
      }
      var ds = GeneratePayments(null, Settle)
        .GetPaymentsByType<DefaultSettlement>().FirstOrDefault();
      // TODO: This is a quick hack. Need to restore the commented logic.  see FB case 12804.
      if (ds != null)
        pv += ds.Accrual * Notional;
      //if (cf.DefaultPayment != null)
      //  pv += (cf.DefaultPayment.Accrual + cf.DefaultPayment.Amount
      //    + cf.DefaultPayment.Loss) * Notional;

      return pv;

    }

    /// <summary>
    ///   Calculate the clean price (as a percentage of notional) based on the Market Quote of the CDX on the same basket
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The market price is the settlement price as a percentage
    ///   of the remaining notional of the index.</para>
    ///
    ///   <para>The calculation of the market price is based on market
    ///   convention. First, construct a single name CDS with the same characteristics as
    ///   the index.  In particular the premium is the index deal spread.
    ///   The convention is to then price this product using a flat credit curve at the market quote spread
    ///   and a 40<formula inline="true">\%</formula> recovery.</para>
    /// </remarks>
    /// <returns>Price of the BasketCDS at the quoted CDX spread</returns>
    ///
    public double
    MarketPrice()
    {
      return MarketPrice(MarketPremium);
    }

    /// <summary>
    ///   Calculate the clean price (as a percentage of notional) based on the Market Quote of the CDX on the same basket
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The market price is the settlement price as a percentage
    ///   of the remaining notional of the index.</para>
    ///
    ///   <para>The calculation of the market price is based on market
    ///   convention. First, construct a single name CDS with the same characteristics as
    ///   the index.  In particular the premium is the index deal spread.
    ///   The convention is to then price this product using a flat credit curve at the market quote spread
    ///   and a 40<formula inline="true">\%</formula> recovery.</para>
    /// </remarks>
    ///
    /// <param name="marketSpread">Current quoted market premium as a number (100bp = 0.01).</param>
    ///
    /// <returns>Price of the BasketCDS at the quoted CDX spread</returns>
    ///
    public double MarketPrice(double marketSpread)
    {
      if (marketSpread < 0.0)
        throw new ArgumentOutOfRangeException("marketSpread", "marketSpread must be positive.");
      if (marketSpread.AlmostEquals(0.0)) return 0;

      double effectiveNotional = EffectiveNotional;
      // If effective notional is zero, price is zero
      if (Math.Abs(effectiveNotional) < 1E-15)
        return 0.0;

      // Note: 
      //   (1) CashflowPricer.FlatPrice() is simple clean Pv per unit notional.
      //       Since GetEquivalentCDSPricer returns a pricer with its notional
      //       set to the CurrentNotional, we are OK.
      //   (2) In addition, the price should include unsettled default loss,
      //       as MarkIt does.
      double price = GetEquivalentCDSPricer(marketSpread).FlatPrice();

      // TODO: This is a quick hack. Need to restore the commented logic.  see FB case 12804.
      // Cashflow cf = GenerateCashflow(null, Settle);
      //if (cf.DefaultPayment != null)
      //  price += (cf.DefaultPayment.Amount + cf.DefaultPayment.Loss)
      //    * Notional / effectiveNotional;

      return BasketCDS.BasketCdsType == BasketCDSType.FundedFloating
        || BasketCDS.BasketCdsType == BasketCDSType.FundedFixed ? price : (price + 1);
    }

    /// <summary>
    ///   Calculate the impact of a 1bp increase in the market spread of the CDX on the same basket
    /// </summary>
    ///
    /// <remarks>
    ///   <para>calculates the change in total value for a one basis point increase in
    ///   the index quoted market spread</para>
    /// </remarks>
    public double MarketSpread01()
    {
      return MarketSpread01(MarketQuote);
    }

    /// <summary>
    ///   Calculate the impact of a 1bp increase in the market spread of the CDX on the same basket
    /// </summary>
    ///
    /// <remarks>
    ///   <para>calculates the change in total value for a one basis point increase in
    ///   the index quoted market spread</para>
    /// </remarks>
    ///
    /// <param name="marketQuote">Current quoted market premium/price as a number (100bp = 0.01; 95 = 0.95).</param>
    public double
    MarketSpread01(double marketQuote)
    {
      if (QuotingConvention == QuotingConvention.FlatPrice)
      {
        double marketPremium = CDXPricerUtil.CDXPriceToSpread(AsOf, Settle, BasketCDS.MarketCDX, marketQuote,
                                                              DiscountCurve, SurvivalCurves, MarketRecoveryRate,
                                                              CurrentRate);

        double newMarketQuote = CDXPricerUtil.CDXSpreadToPrice(AsOf, Settle, BasketCDS.MarketCDX, marketPremium + .0001,
                                                               DiscountCurve, SurvivalCurves, MarketRecoveryRate,
                                                               CurrentRate);
        return (MarketValue(newMarketQuote) - MarketValue(marketQuote));
      }

      return (MarketValue(marketQuote + 0.0001) - MarketValue(marketQuote));
    }

    #endregion Market Methods

    #region private helper methods
    /// <summary>
    ///   Create a CDS for accrual based calculations
    /// </summary>
    /// <returns></returns>
    private CDS GetCDS(ref DiscountCurve discountCurve)
    {
      BasketCDS basketCDS = BasketCDS;
      CDS cds = new CDS(basketCDS.Effective, basketCDS.Maturity, basketCDS.Ccy, basketCDS.FirstPrem,
        basketCDS.Premium, basketCDS.DayCount, basketCDS.Freq, basketCDS.BDConvention, basketCDS.Calendar);

      cds.AccruedOnDefault = basketCDS.AccruedOnDefault;
      cds.LastPrem = basketCDS.LastPrem;
      cds.CycleRule = basketCDS.CycleRule;
      
      // Note that we need to use a speparate reference curve,
      // which is different that the discount curve above in the bullet case.
      if (basketCDS.BasketCdsType == BasketCDSType.FundedFloating)
      {
        cds.CdsType = CdsType.FundedFloating;
        DiscountCurve referenceCurve = (ReferenceCurve != null) ? ReferenceCurve : DiscountCurve;
        if (referenceCurve == null || basketCDS.Premium <= 0.0)
          throw new ArgumentException("Must specify the reference curve and current coupon for a floating rate cds");
      }
      else if (basketCDS.BasketCdsType == BasketCDSType.FundedFixed)
        cds.CdsType = CdsType.FundedFixed;
      else
        cds.CdsType = CdsType.Unfunded;
      // Set the PayRecoveryAtMaturity property to that of BasketCDS
      cds.PayRecoveryAtMaturity = BasketCDS.PayRecoveryAtMaturity;
      cds.Bullet = this.BasketCDS.Bullet;
      return cds;
    }

    /// <summary>
    ///   Get a CDS pricer for calculating the additive values
    /// </summary>
    private CDSCashflowPricer GetCDSPricer(params SurvivalCurve[] survCurve)
    {
      DiscountCurve discountCurve = DiscountCurve;
      CDS cds = GetCDS(ref discountCurve);

      CDSCashflowPricer pricer = new CDSCashflowPricer(
        cds, AsOf, Settle, discountCurve, ReferenceCurve, null, 0, TimeUnit.None);
      pricer.ReferenceCurve = (ReferenceCurve != null) ? ReferenceCurve : DiscountCurve;
      if (survCurve != null && survCurve.Length > 0)
      {
        pricer.SurvivalCurve = survCurve[0];
        if (survCurve[0].SurvivalCalibrator != null)
          pricer.RecoveryCurve = survCurve[0].SurvivalCalibrator.RecoveryCurve;
        else
          pricer.RecoveryCurve = new RecoveryCurve(survCurve[0].AsOf, 0.4);
      }

      // Set the SwapLeg (or Bond) passed in to BasketCDSPricer to CDS pricer
      pricer.SwapLeg = SwapLeg;
      pricer.Bond = Bond;
      pricer.PricerFlags = PricerFlags;

      foreach (RateReset r in RateResets)
        pricer.RateResets.Add((RateReset)r.Clone());

      return pricer;
    }

    /// <summary>
    ///   Create equivalent CDS Pricer for CDX market standard calculations
    /// </summary>
    ///
    /// <param name="marketPremium">Market quote for CDX</param>
    ///
    /// <returns>CDSCashflowPricer for MarketCDX</returns>
    private CDSCashflowPricer GetEquivalentCDSPricer(double marketPremium)
    {
      CDS cds = null;

      // use flat survival curve fitted to market CDX quote
      SurvivalCurve survivalCurve = CDXPricerUtil.FitFlatSurvivalCurve(AsOf, Settle, BasketCDS.MarketCDX, marketPremium,
                                                                       MarketRecoveryRate, DiscountCurve, RateResets);
      // Create equivalent CDS from BasketCDS 
      cds = new CDS(BasketCDS.Effective, BasketCDS.Maturity, BasketCDS.Ccy, BasketCDS.FirstPrem, BasketCDS.Premium,
                       BasketCDS.DayCount, BasketCDS.Freq, BasketCDS.BDConvention, BasketCDS.Calendar);

      if (BasketCDS.BasketCdsType == BasketCDSType.FundedFloating)
      {
        cds.CdsType = CdsType.FundedFloating;
        DiscountCurve referenceCurve = (ReferenceCurve != null) ? ReferenceCurve : DiscountCurve;
        if (referenceCurve == null || marketPremium <= 0.0)
          throw new ArgumentException("Must specify the reference curve and current coupon for a floating rate index");
      }
      else if (BasketCDS.BasketCdsType == BasketCDSType.FundedFixed)
        cds.CdsType = CdsType.FundedFixed;
      else
        cds.CdsType = CdsType.Unfunded;

      // add bonus premium if present
      cds.Bullet = BasketCDS.Bullet;

      CDSCashflowPricer pricer = new CDSCashflowPricer(cds, AsOf, 
        Settle, DiscountCurve, ReferenceCurve, survivalCurve, 
        0, TimeUnit.None);
      pricer.Notional = EffectiveNotional; // TODO: change to CurrentNotional.  See case 12804.
      foreach (RateReset r in RateResets)
        pricer.RateResets.Add(r);
      return pricer;
    }

    private delegate double PriceEvaluator(CDSCashflowPricer pricer);

    /// <summary>
    ///  Sum over additive quantities
    /// </summary>
    /// <param name="pricer">ICDS Pricer</param>
    /// <param name="cdsEvalFn">Cds evaluation function</param>
    /// <returns>Weighted evaluation result</returns>
    private double EvaluateAdditive(CDSCashflowPricer pricer, PriceEvaluator cdsEvalFn)
    {
      if (survivalCurves_ == null)
        throw new ArgumentException("BasketCDS Relative value calcs require SurvivalCurves to be set");

      double[] weights = BasketCDS.Weights;
      if (survivalCurves_.Length == 1)
        weights = null;

      double totPv = 0.0;
      // The bullet or not does not affect the protection pv
      // so we should use original discount curve (not 0-flat)
      bool calculateProtectionPv = cdsEvalFn.Method.Name.Contains("ProtectionPv");
      DiscountCurve curve = (DiscountCurve)pricer.DiscountCurve.Clone();
      if (calculateProtectionPv)
        pricer.DiscountCurve = this.DiscountCurve;
      for (int i = 0; i < survivalCurves_.Length; i++)
      {
        if (!BasketCDS.Fee.AlmostEquals(0.0) && BasketCDS.FeeSettle.IsValid() &&
          BasketCDS.FeeSettle < BasketCDS.Maturity && BasketCDS.FeeSettle > pricer.AsOf)
        {
          pricer.CDS.Fee = BasketCDS.Fee;
          pricer.CDS.FeeSettle = BasketCDS.FeeSettle;
        }
        pricer.SurvivalCurve = survivalCurves_[i];
        if (survivalCurves_[i].SurvivalCalibrator != null)
          pricer.RecoveryCurve = survivalCurves_[i].SurvivalCalibrator.RecoveryCurve;
        else
          pricer.RecoveryCurve = new RecoveryCurve(SurvivalCurves[i].AsOf, 0.4);

        double pv = cdsEvalFn(pricer);

        logger.DebugFormat("Calculated index component {0} value = {1}", survivalCurves_[i].Name, pv);
        totPv += pv * ((weights != null) ? weights[i] : (1.0 / survivalCurves_.Length));
      }
      pricer.DiscountCurve = curve;
      return totPv;
    }

    /// <summary>
    ///  Calculate the flat price for BasketCDS at given premium x
    /// </summary>
    /// <param name="x">Current trial premium</param>
    /// <param name="exceptDesc">Exception message</param>
    /// <returns>Weighted flat price</returns>
    private double EvaluateFlatPriceForBEPremium(double x, out string exceptDesc)
    {
      double flatPrice = 0.0;
      exceptDesc = null;

      // Get CDS pricer shared by all underlying curves
      CDSCashflowPricer pricer = GetCDSPricer();

      logger.DebugFormat("Trying premium {0}", x);
      try
      {
        pricer.CDS.Premium = x;
        pricer.Reset();
        // Compute weighted flat price, which is targeted in finding breakeven premium
        flatPrice = EvaluateAdditive(pricer,
          delegate(CDSCashflowPricer p) { return p.FlatPrice(); });
        logger.DebugFormat("Returning clean index intrinsic value {0} for quote {1}", flatPrice, x);
      }
      catch (Exception ex)
      {
        exceptDesc = ex.Message;
      }
      return flatPrice;
    }


    #endregion private helper methods

    #region SemiAnalyticSensitivitiesMethods
    /// <summary>
    /// Tests whether pricer supports semi-analytic derivatives
    /// </summary>
    /// <returns>True is semi-analytic derivatives are supported</returns>
    bool IAnalyticDerivativesProvider.HasAnalyticDerivatives
    {
      get { return true; }
    }

    /// <summary>
    /// Returns the collection of semi-analytic derivatives w.r.t each underlying reference curve
    /// </summary>
    /// <returns>IDerivativeCollection object</returns>
    IDerivativeCollection IAnalyticDerivativesProvider.GetDerivativesWrtOrdinates()
    {
      if (survivalCurves_ == null)
        throw new ArgumentException("BasketCDSPricer Relative value calcs require SurvivalCurves to be set");
      double[] weights = BasketCDS.Weights;
      if (survivalCurves_.Length == 1)
        weights = null;
      DerivativeCollection retVal = new DerivativeCollection(survivalCurves_.Length);
      CDSCashflowPricer pricer = GetCDSPricer();
      pricer.PricerFlags = PricerFlags;
      for (int i = 0; i < survivalCurves_.Length; i++)
      {
        pricer.SurvivalCurve = survivalCurves_[i];
        if (survivalCurves_[i].SurvivalCalibrator != null)
          pricer.RecoveryCurve = survivalCurves_[i].SurvivalCalibrator.RecoveryCurve;
        else
          pricer.RecoveryCurve = new RecoveryCurve(SurvivalCurves[i].AsOf, 0.4);
        double w = (weights != null) ? weights[i] : (1.0 / survivalCurves_.Length);
        var p = pricer as IAnalyticDerivativesProvider;
        DerivativeCollection cdsSens = (DerivativeCollection) p.GetDerivativesWrtOrdinates();
        retVal.Add(cdsSens.GetDerivatives(0));
        {
          int kk = 0;
          for (int j = 0; j < retVal.GetDerivatives(i).Gradient.Length; j++)
          {
            retVal.GetDerivatives(i).Gradient[j] *= w;
            for (int k = 0; k <= j; k++)
            {
              retVal.GetDerivatives(i).Hessian[kk] *= w;
              kk++;
            }
          }
          retVal.GetDerivatives(i).RecoveryDelta *= w;
          retVal.GetDerivatives(i).Vod *= w;
        }
      }
      return retVal;
    }
    #endregion

    #region Properties

    /// <summary>
    ///   Product to price
    /// </summary>
    public BasketCDS BasketCDS
    {
      get { return (BasketCDS)Product; }
    }

    /// <summary>
    ///   Discount Curve used for pricing
    /// </summary>
    public DiscountCurve DiscountCurve
    {
      get { return discountCurve_; }
      set
      {
        discountCurve_ = value;
        Reset();
      }
    }

    /// <summary>
    ///   Survival curves
    /// </summary>
    public SurvivalCurve[] SurvivalCurves
    {
      get { return survivalCurves_; }
      set
      {
        // Survival curves may be null
        survivalCurves_ = (value != null && value.Length == 0 ?
                            null : value);
        Reset();
      }
    }

    /// <summary>
    ///  Recovery curves from curves
    /// </summary>
    public RecoveryCurve[] RecoveryCurves
    {
      get
      {
        // Check null SurvivalCurves
        if (survivalCurves_ == null)
          return null;
        RecoveryCurve[] recoveryCurves = new RecoveryCurve[survivalCurves_.Length];
        for (int i = 0; i < survivalCurves_.Length; i++)
        {
          if (survivalCurves_[i] != null && survivalCurves_[i].Calibrator != null)
            recoveryCurves[i] = survivalCurves_[i].SurvivalCalibrator.RecoveryCurve;
          else
            throw new ArgumentException(String.Format("Must specify recoveries as curve {0} does not have recoveries from calibration", survivalCurves_[i] == null ? null : survivalCurves_[i].Name));
        }
        return recoveryCurves;
      }
    }

    /// <summary>
    ///   The effective outstanding notional, including both
    ///   the names not defaulted and the names defaulted
    ///   but not settled.
    /// </summary>
    public override double EffectiveNotional
    {
      get
      {
        //no annex date for basket cds, use effective date
        return CDXPricerUtil.EffectiveFactor(Settle,
          survivalCurves_, BasketCDS.Weights, BasketCDS.Effective) * Notional;
      }
    }

    /// <summary>
    ///   Remaining notional (not defaulted)
    /// </summary>
    public override double CurrentNotional
    {
      get
      {
        return CDXPricerUtil.CurrentFactor(Settle,
          survivalCurves_, BasketCDS.Weights) * Notional;
      }
    }
   
    /// <summary>
    ///   Historical rate fixings (only for funded note)
    /// </summary>
    public IList<RateReset> RateResets
    {
      get
      {
        if (rateResets_ == null)
          rateResets_ = new List<RateReset>();
        return rateResets_;
      }
      set { rateResets_ = (List<RateReset>)value; Reset(); }
    }

    /// <summary>
    ///   Current floating rate
    /// </summary>
    public double CurrentRate
    {
      get { return RateResetUtil.ResetAt(rateResets_, AsOf); }
      set
      {
        // Set the RateResets to support returning the current coupon
        rateResets_ = new List<RateReset>();
        rateResets_.Add(new RateReset(Product.Effective, value));
        Reset();
      }
    }

    /// <summary>
    ///  Boolean indicating recovery will be funded
    /// </summary>
    public bool RecoveryFunded
    {
      get { return recoveryFunded_; }
      set { recoveryFunded_ = value; }
    }

    /// <summary>
    ///   Quoting convention for associated market CDX
    /// </summary>
    public QuotingConvention QuotingConvention
    {
      get
      {
        if (quotingConvention_ != QuotingConvention.FlatPrice)
          quotingConvention_ = QuotingConvention.CreditSpread;
        return quotingConvention_;
      }
      set { quotingConvention_ = value; }
    }

    /// <summary>
    ///   Current market quote  for associated CDX
    /// </summary>
    /// 
    /// <details>
    ///   <para>CreditSpread and FlatPrice  quoting types are supported
    ///   and are set by <see cref="QuotingConvention"/>. The default
    ///   quoting convention is CreditSpread.</para>
    /// </details>
    public double MarketQuote
    {
      get { return marketQuote_; }
      set
      {
        marketQuote_ = value;
      }
    }

    /// <summary>
    ///   Current associated CDX market premium as a number (100bp = 0.01).
    /// </summary>
    public double MarketPremium
    {
      get
      {
        if (QuotingConvention == BaseEntity.Toolkit.Base.QuotingConvention.CreditSpread)
          return MarketQuote;
        else
          return CDXPricerUtil.CDXPriceToSpread(AsOf, Settle, BasketCDS.MarketCDX, MarketQuote, DiscountCurve,
                                                SurvivalCurves, MarketRecoveryRate, CurrentRate);
      }
    }

    /// <summary>
    ///   CDX Recovery rate for market standard calculations
    /// </summary>
    public double MarketRecoveryRate
    {
      get { return marketRecoveryRate_; }
      set { marketRecoveryRate_ = value; }
    }

    /// <summary>
    ///  Bond product for recovery funding
    /// </summary>
    public Bond Bond
    {
      get { return bond_; }
      set { bond_ = value; }
    }

    /// <summary>
    ///  Swap product for recovery funding
    /// </summary>
    public SwapLeg SwapLeg
    {
      get { return swapLeg_; }
      set { swapLeg_ = value; }
    }

    /// <summary>
    /// Reference Curve used for floating payments forecast
    /// </summary>
    public DiscountCurve ReferenceCurve
    {
      get { return referenceCurve_; }
      set { referenceCurve_ = value; }
    }

    #endregion Properties

    #region Data

    private DiscountCurve discountCurve_;
    private DiscountCurve referenceCurve_;
    private SurvivalCurve[] survivalCurves_;    
    private QuotingConvention quotingConvention_ = QuotingConvention.CreditSpread;
    private List<RateReset> rateResets_ = null;
    private bool recoveryFunded_ = false;
    private Bond bond_ = null;
    private SwapLeg swapLeg_ = null;
    private double marketRecoveryRate_ = 0.40;      // Market standard recovery rate
    private double marketQuote_ = Double.NaN;

    #endregion Data

  } // class BasketCDSPricer
}
