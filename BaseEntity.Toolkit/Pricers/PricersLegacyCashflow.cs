

/*
 * PricerLegacyCashflow.cs
 * Copyright(c)   2002-2018. All rights reserved.
*/


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Pricers.BasketForNtdPricers;
using BaseEntity.Toolkit.Pricers.BasketPricers;
using BaseEntity.Toolkit.Pricers.Baskets;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;

namespace BaseEntity.Toolkit.Pricers
{
  #region BondPricer
  public partial class BondPricer
  {

    /// <summary>
    ///   Generate cashflow from product
    /// </summary>
    /// <param name="cashflow">Cashflow to fill</param>
    /// <param name="asOf">As of date to fill from</param>
    /// <returns>Generated cashflows</returns>
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt asOf)
    {
      Dt defaultDate, dfltSettle;
      GetDefaultDates(out defaultDate, out dfltSettle);
      cashflow = WorkBond.GenerateCashflow(cashflow, asOf, asOf, this.DiscountCurve,
        this.ReferenceCurve, this.RateResets, this.RecoveryRate,
        defaultDate, dfltSettle, IgnoreExDivDateInCashflow);
      return cashflow;
    }

    private void LogPvDiffs(string prefix, double pv, Action<string> log, bool testTradeCf)
    {
      var cf = testTradeCf ? TradeCashflow : Cashflow;
      double cfPv = new CashflowAdapter(cf).CalculatePvWithOptions(AsOf, ProtectionStart,
        discountCurve_, survivalCurve_, null, 0.0, OptionType.Put,
        ExerciseSchedule, NotificationDays, VolatilityObject, CashflowModelFlags,
        StepSize, StepUnit, BgmTreeOptions);
      if (Math.Abs(pv - cfPv) > 1E-15)
        log($"{prefix}: ps = {pv}, cf = {cfPv}, diff = {pv - cfPv}");
    }

    [Obsolete("For backward compatible tests only")]
    public double CashflowPv()
    {
      if (!(IsDefaulted(Settle) && BeforeDefaultRecoverySettle(Settle) || Settle < Product.Maturity))
        return 0;
      if (this.Bond.Convertible && this.ConvertibleBondTreeModel != null)
        return (ConvertibleBondTreeModel.Pv() / 1000.0 + ModelBasis) * Notional;
      if (RequiresHullWhiteCallableModel)
        return (HullWhiteTreeModelPv(true) + ModelBasis) * Notional;
      double pv = BondCashflowAdapter.CalculatePvWithOptions(AsOf, ProtectionStart,
        discountCurve_, survivalCurve_, null, 0.0, OptionType.Put,
        ExerciseSchedule, NotificationDays, VolatilityObject, CashflowModelFlags,
        StepSize, StepUnit, BgmTreeOptions);
      return (pv + ModelBasis) * Notional;
    }

    private void CfReset()
    {
      cashflow_ = null;
    }

    private void TradeCfReset()
    {
      cumDivCashflow_ = null;
    }

    private void SpotCfReset()
    {
      spotSettleCashflow_ = null;
    }


    /// <summary>
    ///   All future cashflows for this product the buyer is entitled
    /// </summary>
    /// <remarks>
    ///   <para>Returns the cashflows for this product from the pricing asOf date.</para>
    /// </remarks>
    public Cashflow Cashflow
    {
      get
      {
        if (cashflow_ == null)
        {
          Dt fromDate = Settle;
          Dt defaultDate, dfltSettle;
          GetDefaultDates(out defaultDate, out dfltSettle);
          Cashflow cf = WorkBond.GenerateCashflow(null, Settle, fromDate, this.DiscountCurve,
            this.ReferenceCurve, this.RateResets, this.RecoveryRate,
            defaultDate, dfltSettle, IgnoreExDivDateInCashflow);
          cf.AsOf = AsOf;
          cashflow_ = cf;
        }
        return cashflow_;
      }
    }


    /// <summary>
    /// All the future Cashflow the buyer is entitled to
    /// </summary>
    /// <remarks>Only relevant when a cum-div trade is being priced in an ex-div period 
    /// or has unsettled lagging payment(s)
    /// </remarks>
    public Cashflow TradeCashflow
    {
      get
      {
        if (cumDivCashflow_ == null)
        {
          if (!TradeSettle.IsEmpty())
          {
            Dt from = GetCashflowsFromDate(true, Settle);
            Dt defaultDate, dfltSettle;
            GetDefaultDates(out defaultDate, out dfltSettle);
            Cashflow cf = WorkBond.GenerateCashflow(null, TradeSettle, from,
              DiscountCurve, ReferenceCurve, RateResets, RecoveryRate,
              defaultDate, dfltSettle, IgnoreExDivDateInCashflow);
            cf.AsOf = AsOf;
            cumDivCashflow_ = cf;
          }
          else
          {
            var cf = GenerateCashflow(null, AsOf);
            cumDivCashflow_ = cf;
          }
        }
        return cumDivCashflow_;
      }
    }

    /// <summary>
    ///   All future cashflows for this product after spot settle date, in the case of forward-settle trade, this includes the portion of cash flow 
    /// the buyer not entitled to (between trade date and trade settlement date), which is needed for repo rate calculation
    /// </summary>
    /// <remarks>
    ///   <para>Returns the cashflows for this product from the pricing asOf date.</para>
    /// </remarks>
    public Cashflow SpotSettleCashflow
    {
      get
      {
        var bond = WorkBond;
        if (spotSettleCashflow_ == null)
        {
          Dt nextCoupon = NextCouponDate(AsOf);
          Dt defaultDate, dfltSettle;
          GetDefaultDates(out defaultDate, out dfltSettle);
          Dt asOf = (!defaultDate.IsValid() && !bond.CumDiv(ProductSettle, ProductSettle) && !IgnoreExDivDateInCashflow && nextCoupon.IsValid()) ? Dt.Add(nextCoupon, 1) : AsOf;
          if (bond.PaymentLagRule != null)
          {
            asOf = GenerateFromDate(ProductSettle, ProductSettle);
          }

          Cashflow cf = bond.GenerateCashflow(null, asOf, asOf, this.DiscountCurve,
            this.ReferenceCurve, this.RateResets, this.RecoveryRate,
            defaultDate, dfltSettle, IgnoreExDivDateInCashflow);
          cf.AsOf = AsOf;

          spotSettleCashflow_ = cf;
        }
        return spotSettleCashflow_;
      }
    }

    private void CfFixPaymentAmounts()
    {
      cashflow_ = GenerateCashflow(null, AsOf);
      _cfAdapter = null; //Reset cash flow adapter.
    }

    private DefaultSettlement GetDfltPytFromCashflow(CashflowAdapter cfa)
    {
      if (cfa == null)
        return null;
      var cf = cfa.Data as Cashflow;
      if (cf?.DefaultPayment == null)
        return null;

      var cfdp = cf.DefaultPayment;
      return new DefaultSettlement(cfdp.Date, cfdp.Date,
        Currency.None, 1.0, cfdp.Amount, cfdp.Accrual, true);
    }


    #region Data
    private Cashflow cashflow_; // Cashflow matching product
    private Cashflow cumDivCashflow_;
    private Cashflow spotSettleCashflow_; // Cashflow starting from spot settle date
    #endregion


  }

  #endregion BondPricer

  #region BasketCdsLinkedNotePricer

  public partial class BasketCdsLinkedNotePricer
  {
    private partial class BasketCdsDynamicPricer
    {
      private static Cashflow GenerateCashflowForFee(
        BasketCDS basketCds, Dt settle)
      {
        return PriceCalc.GenerateCashflowForFee(settle, basketCds.Premium,
          basketCds.Effective, basketCds.FirstPrem,
          basketCds.Maturity, basketCds.Ccy, basketCds.DayCount, basketCds.Freq,
          basketCds.BDConvention, basketCds.Calendar, null, false, null, null);
      }

      private static Cashflow GenerateCashflowForProtection(
        BasketCDS basketCds, Dt settle)
      {
        return PriceCalc.GenerateCashflowForProtection(settle,
          basketCds.Maturity, basketCds.Ccy, null);
      }

      private double ProtectionPv(Dt settle, Cashflow cf, BasketCDS basketCds,
        BasketPricer basketPricer, DiscountCurve discountCurve,
        double defaultTiming, double accruedFractionOnDefault)
      {
        Dt maturity = basketCds.Maturity;
        if (settle >= maturity)
          return 0.0;
        double pv = PriceCalc.Price(cf, settle, discountCurve, LossAt,
          SurvivalAt, null, false, true, false,
          defaultTiming, accruedFractionOnDefault, basketPricer.StepSize,
          basketPricer.StepUnit, cf.Count);
        pv += basketPricer.DefaultSettlementPv(settle, basketCds.Maturity,
          discountCurve, 0.0, 1.0, true, false);
        return pv;
      }

      private double FeePv(Dt settle, Cashflow cf, BasketCDS basketCds,
        BasketPricer basketPricer,
        DiscountCurve discountCurve, double defaultTiming,
        double accruedFractionOnDefault)
      {
        if (settle > basketCds.Maturity)
          return 0.0;
        return PriceCalc.Price(cf, settle, discountCurve, LossAt,
          SurvivalAt, null, true, false, false, defaultTiming,
          accruedFractionOnDefault, basketPricer.StepSize,
          basketPricer.StepUnit, cf.Count);
      }

      private double CfPv(Dt dt)
      {
        double ppv = ProtectionPv(dt, protectionCf_, basketCds_,
          basketPricer_, discount_, defaultTiming_,
          accruedFractionAtDefault_);
        double fpv = FeePv(dt, feeCf_, basketCds_, basketPricer_,
          discount_, defaultTiming_, accruedFractionAtDefault_);
        return ppv + fpv;
      }

      #region Data
      private readonly Cashflow feeCf_;
      private readonly Cashflow protectionCf_;
      #endregion Data

    }
  }
  #endregion BasketCdsLinkedNotePricer

  #region BasketCDSPricer

  public partial class BasketCDSPricer
  {

    /// <summary>
    ///   All future cashflows for this product.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Returns the cashflows for this product from the pricing asOf date.</para>
    /// </remarks>
    ///
    public Cashflow Cashflow
    {
      get { return GenerateCashflow(cashflow_, AsOf); }
    }

    /// <summary>
    ///   Helper function to generate cashflows for CDX
    /// </summary>
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt from)
    {
      BasketCDS note = BasketCDS;
      DiscountCurve referenceCurve = (ReferenceCurve != null) ? ReferenceCurve : DiscountCurve;
      return note.GenerateCashflow(cashflow, from, Settle, referenceCurve, RateResets, SurvivalCurves, RecoveryCurves);
    }

    private Cashflow cashflow_;                     // Cashflow matching product
  }

  #endregion BasketCDSPricer

  #region BillPricer

  public partial class BillPricer
  {
    /// Only risk calls this function. To be deleted.
    /// <summary>
    /// Get cashflows for this product from the specified date
    /// </summary>
    /// <param name="cashflow">Cashflow to fill. May be null to generate a new Cashflow.</param>
    /// <param name="from">Date to generate cashflows from</param>
    /// <returns>Cashflow from the specified date</returns>
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt from)
    {
      var paymentSchedule = GetPaymentSchedule(null, from);
      var recovery = (RecoveryCurve != null) ? RecoveryCurve.RecoveryRate(Product.Maturity) : 0.0;
      return PaymentScheduleUtils.FillCashflow(cashflow, paymentSchedule, from, Math.Abs(Notional), recovery);
    }
  }

  #endregion BillPricer

  #region CapFloorPricer

  public abstract partial class CapFloorPricerBase
  {
    /// <summary>
    /// Generates the cashflows for the cap/floor.
    /// </summary>
    /// <param name="cashflow"></param>
    /// <param name="from"></param>
    /// <returns></returns>
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt from)
    {
      return Cap.GenerateCashflow(cashflow, from, new RateResets(Resets), AsOf);
    }
  }

  #endregion CapFloorPricer

  #region CDPricer

  public partial class CDPricer
  {
    /// Only risk calls this function. To be deleted
    /// <summary>
    /// Get cashflows for this product from the specified date
    /// </summary>
    /// <param name="cashflow">Cashflow to fill. May be null to generate a new Cashflow.</param>
    /// <param name="from">Date to generate cashflows from</param>
    /// <returns>Cashflow from the specified date</returns>
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt from)
    {
      var paymentSchedule = GetPaymentSchedule(null, from);
      var recovery = (RecoveryCurve != null) ? RecoveryCurve.RecoveryRate(Product.Maturity) : 0.0;
      return PaymentScheduleUtils.FillCashflow(cashflow, paymentSchedule, from, Math.Abs(Notional), recovery);
    }
  }


  #endregion CDPricer

  #region CashflowPricer

  public partial class CashflowPricer
  {

    /// <summary>
    /// All future cashflows for this product.
    /// </summary>
    /// <remarks>
    /// <para>Returns the cashflows for this product from the pricing asOf date.</para>
    /// </remarks>
    public Cashflow Cashflow
    {
      get
      {
        if (cashflow_ == null)
          cashflow_ = GenerateCashflow(null, AsOf);
        return cashflow_;
      }
    }

    private void CfReset()
    {
      cashflow_ = null;
      base.Reset();
    }

    private object CfClone()
    {
      CashflowPricer obj = (CashflowPricer)base.Clone();
      obj.cashflow_ = (cashflow_ != null) ? cashflow_.clone() : null;
      obj.discountCurve_ = (discountCurve_ != null) ? (DiscountCurve)discountCurve_.Clone() : null;
      // Careful with clone of reference curve as this may be shared with discount curve.
      if (referenceCurve_ == discountCurve_)
        obj.referenceCurve_ = obj.discountCurve_;
      else
        obj.referenceCurve_ = (referenceCurve_ != null) ? (DiscountCurve)referenceCurve_.Clone() : null;
      obj.rateResets_ = CloneUtil.Clone(rateResets_);
      obj.survivalCurve_ = (survivalCurve_ != null) ? (SurvivalCurve)survivalCurve_.Clone() : null;
      obj.counterpartyCurve_ = (counterpartyCurve_ != null) ? (SurvivalCurve)counterpartyCurve_.Clone() : null;
      obj.recoveryCurve_ = (recoveryCurve_ != null) ? (RecoveryCurve)recoveryCurve_.Clone() : null;

      return obj;
    }

    private void CfConstruct(DiscountCurve discountCurve,
      DiscountCurve referenceCurve, SurvivalCurve survivalCurve,
      SurvivalCurve counterpartyCurve, double correlation,
      int stepSize, TimeUnit stepUnit, RecoveryCurve recoveryCurve)
    {
      this.cashflow_ = null;
      this.discountCurve_ = discountCurve;
      this.referenceCurve_ = referenceCurve;
      this.survivalCurve_ = survivalCurve;
      this.recoveryCurve_ = recoveryCurve;
      this.stepSize_ = stepSize;
      this.stepUnit_ = stepUnit;
      this.counterpartyCurve_ = counterpartyCurve;
      this.correlation_ = correlation;
      this.discountingAccrued_ = settings_.CashflowPricer.DiscountingAccrued;
    }

    private double CfProductPv()
    {
      Cashflow cf = Cashflow;
      Dt asOf = AsOf;
      double pv = CashflowModel.Pv(cf, asOf, ProtectionStart,
                    discountCurve_, survivalCurve_, counterpartyCurve_, correlation_,
                    includeSettlePayments_, includeMaturityProtection_, DiscountingAccrued,
                    StepSize, StepUnit) * CurrentNotional;
      if (cf.DefaultPayment != null)
        pv += DefaultPv(cf.DefaultPayment, asOf, ProtectionStart,
                discountCurve_, counterpartyCurve_) * Notional;
      return pv;
    }


    private double CfProtectionPv()
    {
      Cashflow cf = Cashflow;
      Dt asOf = AsOf;

      double pv = CashflowModel.ProtectionPv(cf, asOf, ProtectionStart,
                    discountCurve_, survivalCurve_, counterpartyCurve_, correlation_,
                    includeSettlePayments_, includeMaturityProtection_, DiscountingAccrued,
                    StepSize, StepUnit) * CurrentNotional;

      if (cf.DefaultPayment != null)
        pv += DefaultLossPv(cf.DefaultPayment, asOf, Settle, discountCurve_, counterpartyCurve_) * Notional;
      return pv;
    }

    private double CfFullFeePv()
    {
      Cashflow cf = Cashflow;
      Dt asOf = AsOf;
      double pv = CashflowModel.FeePv(cf, asOf, ProtectionStart,
                    discountCurve_, survivalCurve_, counterpartyCurve_, correlation_,
                    includeSettlePayments_, includeMaturityProtection_, DiscountingAccrued,
                    StepSize, StepUnit) * CurrentNotional;
      if (cf.DefaultPayment != null)
        pv += DefaultFeePv(cf.DefaultPayment, asOf, ProtectionStart,
                discountCurve_, counterpartyCurve_) * Notional;
      return pv;
    }

    private double CfIrr(double price, DayCount daycount, Frequency freq)
    {
      Dt settle = ProtectionStart;
      return CashflowModel.Irr(Cashflow, settle, settle,
        survivalCurve_, counterpartyCurve_, correlation_,
        includeSettlePayments_, includeMaturityProtection_, DiscountingAccrued,
        StepSize, StepUnit, price, daycount, freq);
    }


    private double CfImpliedDiscountSpread(double price)
    {
      Dt settle = ProtectionStart;
      return CashflowModel.ImpDiscountSpread(Cashflow, settle, settle,
        discountCurve_, survivalCurve_, counterpartyCurve_, correlation_,
        includeSettlePayments_, includeMaturityProtection_, DiscountingAccrued,
        StepSize, StepUnit, price);
    }


    private double CfImpliedHazardRateSpread(double price)
    {
      Dt settle = ProtectionStart;
      return CashflowModel.ImpSurvivalSpread(Cashflow, settle, settle,
        discountCurve_, survivalCurve_, counterpartyCurve_, correlation_,
        includeSettlePayments_, includeMaturityProtection_, DiscountingAccrued,
        StepSize, StepUnit, price);
    }

    /// <summary>
    /// Implied flag cds curve
    /// </summary>
    /// <param name="price">price</param>
    /// <param name="recoveryRate">recovery rate</param>
    /// <returns></returns>
    public SurvivalCurve ImpliedFlatCDSCurve(double price, double recoveryRate)
    {
      return CashflowModel.ImpliedCDSCurve(this.AsOf, this.Settle, this.Cashflow, this.DiscountCurve,
        this.CounterpartyCurve, this.Correlation,
        this.IncludeSettlePayments, this.DiscountingAccrued, false,
        this.StepSize, this.StepUnit, price, recoveryRate, false);
    }

    public static double DefaultPv(
      Cashflow.ScheduleInfo defaultPayment,
      Dt asOf, Dt settle, DiscountCurve dc,
      SurvivalCurve counterparty)
    {
      if (defaultPayment == null) return 0;
      if (counterparty != null && !counterparty.DefaultDate.IsEmpty()
          && counterparty.DefaultDate <= defaultPayment.Date)
      {
        return 0;
      }
      if (defaultPayment.Date <= settle)
      {
        return 0;
      }
      return defaultPayment.Accrual + (defaultPayment.Amount + defaultPayment.Loss)
             * dc.DiscountFactor(asOf, defaultPayment.Date);
    }

    /// <summary>
    /// Calculates the fee pv upon default.
    /// </summary>
    /// <param name="defaultPayment">The default payment.</param>
    /// <param name="asOf">As of date.</param>
    /// <param name="settle">Settle date.</param>
    /// <param name="dc">Discount curve.</param>
    /// <param name="counterparty">Counterparty survival curve.</param>
    /// <returns>Fee Pv under default</returns>
    public static double DefaultFeePv(
      Cashflow.ScheduleInfo defaultPayment,
      Dt asOf, Dt settle, DiscountCurve dc,
      SurvivalCurve counterparty)
    {
      if (defaultPayment == null) return 0;
      if (counterparty != null && !counterparty.DefaultDate.IsEmpty()
          && counterparty.DefaultDate <= defaultPayment.Date)
      {
        return 0;
      }
      if (defaultPayment.Date <= settle)
      {
        return 0;
      }
      return defaultPayment.Accrual + defaultPayment.Amount
             * dc.DiscountFactor(asOf, defaultPayment.Date);
    }

    /// <summary>
    /// PV of any default payment
    /// </summary>
    /// <param name="defaultPayment"></param>
    /// <param name="asOf"></param>
    /// <param name="settle"></param>
    /// <param name="dc"></param>
    /// <param name="counterparty"></param>
    /// <returns>PV of default payment</returns>
    public static double DefaultLossPv(
      Cashflow.ScheduleInfo defaultPayment,
      Dt asOf, Dt settle, DiscountCurve dc,
      SurvivalCurve counterparty)
    {
      if (defaultPayment == null) return 0;
      if (counterparty != null && !counterparty.DefaultDate.IsEmpty()
          && counterparty.DefaultDate <= defaultPayment.Date)
      {
        return 0;
      }

      if (defaultPayment.Date > settle)
        return defaultPayment.Loss * dc.DiscountFactor(asOf, defaultPayment.Date);
      return 0;
    }

    //this function is currently only used in a test.
    /// <summary>
    ///   Calculates present value of all cashflows received on or before
    ///   <paramref name="toDate"/>.
    /// </summary>
    /// <remarks>
    ///   <para>Cashflows after the settlement date and on or before
    ///   <paramref name="toDate"/> are present valued back to the pricing
    ///   as-of date.</para>
    /// </remarks>
    /// <param name="toDate">date of last cashflow to pv</param>
    /// <returns>Pv of cashflows</returns>
    public double Pv(Dt toDate)
    {
      Cashflow cf = Cashflow;
      Dt asOf = AsOf;
      double pv = CashflowModel.PvTo(cf, asOf, ProtectionStart, toDate,
        discountCurve_, survivalCurve_, counterpartyCurve_, correlation_,
        includeSettlePayments_, includeMaturityProtection_, DiscountingAccrued,
        StepSize, StepUnit) * CurrentNotional;
      if (cf.DefaultPayment != null && cf.DefaultPayment.Date <= toDate)
        pv += DefaultPv(cf.DefaultPayment, asOf, ProtectionStart, discountCurve_, counterpartyCurve_) * Notional;
      return pv;
    }

    private Cashflow cashflow_;               // Cashflow matching product
  }

  #endregion CashflowPricer

  #region CDSCashflowPricer
  partial class CDSCashflowPricer
  {
    #region Pv calculations

    public double CfFwdPremium01(Dt forwardSettle)
    {
      // Test forward settlement on or after product effective date
      if (Product != null && (Product.Effective > forwardSettle))
        forwardSettle = Product.Effective;
      // Price stream of 1bp premiums
      double fv01;
      if (CDS.CdsType == CdsType.Unfunded)
      {
        Cashflow cashflow = new Cashflow();
        GenerateCashflow01(cashflow, AsOf, forwardSettle, true);
        fv01 = CashflowModel.FeePv(cashflow, forwardSettle, forwardSettle, DiscountCurve,
                                   DomesticSurvivalCurve, CounterpartyCurve, Correlation,
                                   false, IncludeMaturityProtection, DiscountingAccrued, StepSize, StepUnit) *
               CurrentNotional;
        return fv01;
      }
      else // funded case
      {
        // base case
        double origPremium = CDS.Premium;
        try
        {
          var cf = GenerateCashflow(null, forwardSettle);
          double basePv = CashflowModel.Pv(cf, forwardSettle, forwardSettle, DiscountCurve,
                                           DomesticSurvivalCurve, CounterpartyCurve, Correlation,
                                           false, IncludeMaturityProtection, DiscountingAccrued, StepSize, StepUnit) *
                          CurrentNotional;

          CDS.Premium = CDS.Premium + 0.0001;

          cf = GenerateCashflow(null, forwardSettle);
          double bumpedPv = CashflowModel.Pv(cf, forwardSettle, forwardSettle, DiscountCurve,
                                             DomesticSurvivalCurve, CounterpartyCurve, Correlation,
                                             false, IncludeMaturityProtection, DiscountingAccrued, StepSize, StepUnit) *
                            CurrentNotional;

          fv01 = bumpedPv - basePv;
        }
        finally
        {
          CDS.Premium = origPremium;
        }
        return fv01;
      }
    }

    public double CfRiskyDuration()
    {
#if NEW_WAY // RTD
      return RiskyDuration(this.ProtectionStart);
#else
      if (Dt.Cmp(Settle, CDS.Maturity) > 0)
        return 0.0;
      // Price premium of 1.0 pv
      if (CDS.CdsType == CdsType.Unfunded)
      {
        Cashflow cashflow = new Cashflow();
        Dt settle = ProtectionStart;
        GenerateCashflow01(cashflow, AsOf, settle, false);
        return CashflowModel.FeePv(cashflow, AsOf, settle, DiscountCurve,
                                   DomesticSurvivalCurve, CounterpartyCurve, Correlation,
                                   false, IncludeMaturityProtection, DiscountingAccrued, StepSize, StepUnit) * 10000.0;
      }
      // funded CDS: 
      {
        return CfFwdPremium01(Settle) / Notional * 10000;
      }
#endif
    }

    public double CfRiskyDuration(Dt forwardSettle)
    {
#if NEW_WAY // RTD
      if (Dt.Cmp(forwardSettle, CDS.Maturity) > 0)
        return 0.0;
      else if (CDS.CdsType == CdsType.Unfunded)
      {
        // Price stream of 1bp premiums matching CDS
        Cashflow cashflow = new Cashflow();
        GenerateCashflow01(cashflow, AsOf, false);
        return CashflowModel.FeePv(cashflow, AsOf, forwardSettle, DiscountCurve,
          DomesticSurvivalCurve, CounterpartyCurve, Correlation,
          false, IncludeMaturityProtection, DiscountingAccrued, StepSize, StepUnit) * 10000.0;
      }
      else // Funded CDS
        return FwdPremium01(forwardSettle)/this.Notional * 10000.0;
#else
      if (CDS.CdsType == CdsType.Unfunded)
      {
        // Price stream of 1bp premiums matching CDS
        Cashflow cashflow = new Cashflow();
        GenerateCashflow01(cashflow, AsOf, forwardSettle, false);
        return CashflowModel.FeePv(cashflow, AsOf, forwardSettle, DiscountCurve,
          DomesticSurvivalCurve, CounterpartyCurve, Correlation,
          false, IncludeMaturityProtection, DiscountingAccrued, StepSize, StepUnit) * 10000.0;
      }
      // Funded CDS
      return CfFwdPremium01(forwardSettle) / Notional * 10000;
#endif
    }


    public double CfPv()
    {
      // CDS.PayRecoveryAtMaturity is a flag to indicate all loss in protection leg 
      // should be defered to the CDS maturity. Since this only affects protection
      // but not fee leg, it's must easier to handle it in C# without getting into 
      // cashflow model in C++, over there a discount curve is required to price both
      // protection and fee, making it more involved to handle pay recovery at maturity
      if (CDS.PayRecoveryAtMaturity)
      {
        return FullFeePv() + ProtectionPv();
      }
      Dt asOf = AsOf;
      Dt settle = ProtectionStart;
      Cashflow cf = GenerateCashflow(null, asOf, CDS, 0.0);
      double pv = 0.0;
      pv += (CashflowModel.Pv(cf, asOf, settle, DiscountCurve, DomesticSurvivalCurve, CounterpartyCurve, Correlation,
        IncludeSettlePayments, IncludeMaturityProtection, DiscountingAccrued,
        StepSize, StepUnit) + QuantoCapAdjustment) * CurrentNotional;
      // Add Upfront Fee PV
      pv += UpfrontFeePv(asOf, settle);
      if (IsDefaultIncluded(PricerFlags))
      {
        if (cf.DefaultPayment != null)
          pv += DefaultPv(cf.DefaultPayment, asOf, settle, DiscountCurve, CounterpartyCurve) * Notional;
      }

      if (logger.IsDebugEnabled) LogDiffs(pv, PsPv, "Pv");

      return pv;
    }

    public double CfFeePv()
    {
      // Price without fee
      Dt asOf = AsOf;
      Dt settle = ProtectionStart;
      Cashflow cf = GenerateCashflow(null, asOf, CDS, 0.0);
      double pv = CashflowModel.FeePv(cf, asOf, settle,
        DiscountCurve, DomesticSurvivalCurve, CounterpartyCurve, Correlation,
        IncludeSettlePayments, IncludeMaturityProtection, DiscountingAccrued,
        StepSize, StepUnit) * CurrentNotional;
      // Add Upfront Fee PV
      pv += UpfrontFeePv(asOf, settle);
      if (cf.DefaultPayment != null)
        pv += DefaultFeePv(cf.DefaultPayment, asOf, settle, DiscountCurve, CounterpartyCurve) * Notional;
      // Since the cashflow has the optional bonus added, there's no need to add it 
      // to feepv here like: BulletCouponUtil.GetDiscountedBonus(this)
      if (CDS.Funded)
        pv += CfProtectionPv();

      if (logger.IsDebugEnabled)
        LogDiffs(pv, PsFeePv, "Fee");

      return pv;
    }

    /// <summary>
    ///  Calculate the pv of losses/Recoveries.
    /// </summary>
    public double CfProtectionPv()
    {
      Dt asOf = AsOf;
      Cashflow cf = GenerateCashflow(null, asOf, this.CDS, 0.0);
      bool payRecovAtMaturity = Product is CDS && ((CDS)Product).PayRecoveryAtMaturity;
      var discCurve = payRecovAtMaturity ? GetFlatDiscountCurve(DiscountCurve, false) : DiscountCurve;
      double pv = CashflowModel.ProtectionPv(cf, asOf, ProtectionStart,
        discCurve, DomesticSurvivalCurve,
        CounterpartyCurve, Correlation,
        IncludeSettlePayments, IncludeMaturityProtection,
        DiscountingAccrued,
        StepSize, StepUnit);
      pv = (pv + QuantoCapAdjustment) * CurrentNotional;
      if (payRecovAtMaturity)
        pv *= DiscountCurve.DiscountFactor(asOf, Product.Maturity);
      if (IsDefaultIncluded(PricerFlags) && (cf.DefaultPayment != null))
        pv += DefaultLossPv(cf.DefaultPayment, asOf, Settle, DiscountCurve, CounterpartyCurve) * Notional;

      if (logger.IsDebugEnabled)
        LogDiffs(pv, PsProtectionPv, "Protection");

      return pv;
    }

    #endregion

    #region Break even premium

    // Helper method to calculate the BEP
    public double CfBreakEvenPremium(Dt forwardSettle, bool replicatingSpread)
    {
      double bep = Double.NaN;

      // Test of we are settling after Maturity
      if (forwardSettle > CDS.Maturity)
        return 0.0;

      // Test forward settlement on or after product effective date
      if (Product != null && (Product.Effective > forwardSettle))
        forwardSettle = Product.Effective;

      // Store the original bonus
      var bullet = CDS.Bullet;

      try
      {
        CDS.Bullet = null;

        // Price stream of 1bp premiums matching CDS
        Cashflow cashflow = new Cashflow();
        GenerateCashflow01(cashflow, AsOf, forwardSettle, false);
        double fv01 = CashflowModel.FeePv(cashflow, forwardSettle, forwardSettle, DiscountCurve,
                                          DomesticSurvivalCurve, CounterpartyCurve, Correlation,
                                          false, IncludeMaturityProtection, DiscountingAccrued, StepSize, StepUnit);
        double fwdProtection = (QuantoCapAdjustment + CashflowModel.ProtectionPv(cashflow, forwardSettle, forwardSettle,
                                                                                 DiscountCurve,
                                                                                 DomesticSurvivalCurve,
                                                                                 CounterpartyCurve, Correlation,
                                                                                 false, IncludeMaturityProtection,
                                                                                 DiscountingAccrued, StepSize,
                                                                                 StepUnit));
        const double tiny = Double.Epsilon;
        if (Math.Abs(fv01) < tiny && Math.Abs(fwdProtection) < tiny)
          return 0.0; // this may happen when the curve is defaulted.
        double upFrontFee = replicatingSpread ? 0.0 : UpfrontFeePv(forwardSettle, forwardSettle) / Notional;
        bep = ((-fwdProtection - upFrontFee) / fv01) / 10000;
      }
      finally
      {
        CDS.Bullet = bullet;
      }
      return bep;
    }

    #endregion

    #region Generate cash flows

    /// <summary>
    ///  Generate cashflow from product
    /// </summary>
    /// <param name="cashflow">Cashflow to fill</param>
    /// <param name="asOf">As of date to fill from</param>
    /// <returns></returns>
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt asOf)
    {
      return GenerateCashflow(null, asOf, this.CDS, this.Fee());
    }

    // This is the core to pop up the DefaultAmount in cashflow
    private void FillRecoveryFundingCosts(Cashflow cashflow)
    {
      // Loop over the cashflow index i and get basic properties
      // And set() the properties (with the DefaultAmount updated)

      if (cashflow.Count <= 1)
        return;
      Dt prevDate = cashflow.AsOf;
      double matPv;
      IPricer bond_swap_pricer_1 = CreateRecoveryFundingPricer(prevDate, out matPv);
      IPricer bond_swap_pricer_2;
      double d1 = Dt.Cmp(prevDate, ReferenceCurve.AsOf) > 0 ? ReferenceCurve.DiscountFactor(prevDate) : 1.0,
             d2 = 1.0;
      double pv_1 = 0, pv_2 = 0;

      pv_1 = bond_swap_pricer_1.Pv() + matPv;

      for (int i = 0; i < cashflow.Count; ++i)
      {
        // Get the ith accrual amount paid if no default 
        double accrued = cashflow.GetAccrued(i);
        // Get the ith payment amount paid under no default 
        double amount = cashflow.GetAmount(i);
        // Get the ith coupon
        // double coupon = cashflow.GetCoupon(i);
        // Get the ith default payment amount paid on default 
        double defaultAmount = cashflow.GetDefaultAmount(i);

        Dt date = cashflow.GetDt(i);

        // Without considering the treasury, the desk (protection buyer) pays the investor 
        // recovery rate R. With the treasury, the desk borrows R from treasry and pays it to 
        // the investor, and pays R*(interest + face) back to treasury. This is equivalent
        // for desk to pay out a new default amount = R*(interest + face) = R*Pv(bond)

        bond_swap_pricer_2 = CreateRecoveryFundingPricer(date, out matPv);

        // Calculate the present value for recovery fund. 0 for the very last. 
        pv_2 = date >= CDS.Maturity ? 0 : bond_swap_pricer_2.Pv() + matPv;

        // Calculate current discount factor
        d2 = Dt.Cmp(date, ReferenceCurve.AsOf) > 0 ? ReferenceCurve.DiscountFactor(date) : 1.0;

        // Calculate the updated defaultAmount between previous and 
        // current dates. This is approximated by: 
        // [Pv(ForwardSwap(Date1) + Pv(ForwardSwap(Date2)] / (D1+D2)
        // where D1 and D2 are discount factors at Date1 and Date2
        if (pv_1 == 0)
          defaultAmount = defaultAmount * pv_2 / d2;
        else if (pv_2 == 0)
          defaultAmount = defaultAmount * pv_1 / d1;
        else
          defaultAmount = defaultAmount * (pv_1 + pv_2) / (d1 + d2);

        // Set the new default amount back to cashflow
        // cashflow.Set(i, amount, accrued, coupon, defaultAmount);
        cashflow.Set(i, amount, accrued, defaultAmount); // temp hack, don't care about coupon

        // Set the previous discount factor and pv.
        d1 = d2;
        pv_1 = pv_2;
      }
      return;
    }

    // Create either a bond pricer of swap pricer
    private IPricer CreateRecoveryFundingPricer(Dt date, out double matDatePv)
    {
      matDatePv = 0.0;

      // Get the floating bond or swapleg
      Bond fundCostBond = null;
      SwapLeg fundCostSwapLeg = null;
      BondPricer pricer_bond = null;
      SwapLegPricer pricer_swap = null;
      double currentRate = 0;
      if (Dt.Cmp(date, this.ReferenceCurve.AsOf) < 0)
      {
        // Need to approximate the current coupon
        if (this.CurrentRate > 0)
          currentRate = this.CurrentRate;
        else
        {
          Frequency freq = Bond != null ? Bond.Freq : SwapLeg.Freq;
          DayCount dayCount = Bond != null ? Bond.DayCount : SwapLeg.DayCount;
          Dt next = Dt.Add(this.ReferenceCurve.AsOf, freq, true);
          Dt asof = this.ReferenceCurve.AsOf;
          currentRate = (1 / this.ReferenceCurve.DiscountFactor(next) - 1) / Dt.Fraction(asof, next, dayCount);
        }
      }
      if (Bond != null)
      {
        fundCostBond = (Bond)Bond.Clone();
        Dt firstCouponDate = Bond.FirstCoupon.IsEmpty()
                               ? Dt.Empty
                               : Schedule.NextPaymentDate(Bond, date);
        if (firstCouponDate > Bond.LastCoupon)
        {
          firstCouponDate = Dt.Empty;
        }

        fundCostBond.Effective = date;
        fundCostBond.FirstCoupon = firstCouponDate;
        fundCostBond.Maturity = CDS.Maturity;
        bool ignoreCall = true;

        pricer_bond = new BondPricer(fundCostBond, this.AsOf, this.Settle,
          DiscountCurve, null, 0, TimeUnit.None, 1, 0, 0, ignoreCall);
        pricer_bond.Notional = 1;
        pricer_bond.ReferenceCurve = (DiscountCurve)this.ReferenceCurve.Clone();

        pricer_bond.CurrentRate = currentRate;
        pricer_bond.QuotingConvention = QuotingConvention.None;
      }
      else if (SwapLeg != null)
      {
        fundCostSwapLeg = (SwapLeg)SwapLeg.Clone();
        Dt firstCouponDate = SwapLeg.FirstCoupon.IsEmpty()
                               ? Dt.Empty
                               : Schedule.NextPaymentDate(Bond, date);
        if (firstCouponDate > SwapLeg.LastCoupon)
        {
          firstCouponDate = Dt.Empty;
        }

        fundCostSwapLeg.Effective = date;
        fundCostSwapLeg.FirstCoupon = firstCouponDate;
        fundCostSwapLeg.Maturity = CDS.Maturity;

        var index = SwapLeg.ReferenceIndex ?? new InterestRateIndex(
          SwapLeg.Index, SwapLeg.Freq, SwapLeg.Ccy, SwapLeg.DayCount,
          SwapLeg.Calendar, 2);

        pricer_swap =
          new SwapLegPricer(fundCostSwapLeg, this.AsOf, this.Settle, 1.0,
          DiscountCurve, index, this.ReferenceCurve, new RateResets(AsOf, currentRate),
          null, null);

        // unlike the bond, we're using a swap without principal exchange at maturity
        // so the pricing will need to manually adjust for this. Can't use a swap with principal
        // exchange because this will pv the initial principal exchange as well.
        matDatePv = ReferenceCurve.DiscountFactor(CDS.Maturity);
      }
      else
        throw new ArgumentException("Recovery Funding must be bond or swapleg");

      if (fundCostBond != null)
        return pricer_bond;
      return pricer_swap;
    }

    /// <summary>
    /// Generate cashflow from product
    /// </summary>
    /// <param name="cashflow">Cashflow to fill or null for new Cashflow</param>
    /// <param name="asOf">As of date to fill from</param>
    /// <param name="cds">CDS product with specified feature</param>
    /// <param name="fee">The upfront fee.</param>
    /// <returns>Cashflow</returns>
    public Cashflow GenerateCashflow(Cashflow cashflow, Dt asOf, CDS cds, double fee)
    {
      double recovery;
      if (IsQuantoStructure && cds.RecoveryCcy != DiscountCurve.Ccy)
        recovery = cds.QuantoNotionalCap.HasValue ? 1 - cds.QuantoNotionalCap.Value : 0.0;
      else
        recovery = cds.FixedRecovery ? cds.FixedRecoveryRate : RecoveryRate;
      Currency defaultCcy = cds.Ccy;
      Dt defaultDate = (SurvivalCurve != null) ? SurvivalCurve.DefaultDate : Dt.Empty;
      DiscountCurve referenceCurve = ReferenceCurve ?? DiscountCurve;

      if (cds.CdsType == CdsType.FundedFloating)
      {
        const double principal = 1.0;
        double defaultAmount = recovery;
        cashflow = CashflowFactory.FillFloat(cashflow, asOf, cds.CashflowFactorySchedule, cds.ScheduleParams, cds.Ccy, cds.DayCount,
          cds.Premium, cds.PremiumSchedule,
          principal, cds.AmortizationSchedule,
          referenceCurve, this.RateResets,
          defaultAmount, defaultCcy, defaultDate,
          fee, FeeSettle);
      }
      else
      {
        double principal = (cds.Funded) ? 1.0 : 0.0;
        double defaultAmount = (cds.Funded) ? recovery : recovery - 1.0;
        cashflow = CashflowFactory.FillFixed(cashflow, asOf, cds.CashflowFactorySchedule, cds.ScheduleParams, cds.Ccy, cds.DayCount,
          cds.Premium, cds.PremiumSchedule,
          principal, cds.AmortizationSchedule,
          defaultAmount, defaultCcy, defaultDate,
          fee, this.FeeSettle);
      }

      // Note: there are 4 cases regarding how to treat the funding cashflow
      // [1] PayRecoveryAtMaturity = FALSE, SwapLeg (Bond) != null, do     FillRecoveryFundingCosts()
      // [2] PayRecoveryAtMaturity = FALSE, SwapLeg (Bond) == null, do NOT FillRecoveryFundingCosts()
      // [3] PayRecoveryAtMaturity = TRUE,  SwapLeg (Bond) != null, do NOT FillRecoveryFundingCosts()
      // [4] PayRecoveryAtMaturity = TRUE,  SwapLeg (Bond) == null, do NOT FillRecoveryFundingCosts()
      if (cds.PayRecoveryAtMaturity == false && (RecoveryFunded || SwapLeg != null || Bond != null) && AsOf <= cds.Maturity)
        FillRecoveryFundingCosts(cashflow);

      Dt cashflowStart = asOf < ProtectionStart ? ProtectionStart : asOf;
      if (defaultDate.IsEmpty())
      {
        if (cds.Bullet != null)
          cashflow.AddMaturityPayment(cds.Bullet.CouponRate, 0, 0, 0);
        return cashflow;
      }
      if (RecoveryCurve == null
        || (!SupportAccrualRebateAfterDefault && (RecoveryCurve.DefaultSettlementDate.IsValid() && RecoveryCurve.DefaultSettlementDate < cashflowStart))
        || (defaultDate.IsValid() && defaultDate >= cashflowStart))
      {
        // Note: To avoid double counting, we do not construct a default settlement
        //   object for the default that settles before the cashflow start date,
        //   nor the default that happens on or after the cashflow start date.
        //   In the later case the default is handled by the cashflow model itself.
        return cashflow;
      }

      // Setup default payment info for the default happened before as-of date.

      // For safety, we let the cashflow factory to handle any amortization schedules
      // by letting it fills a cashflow from the cds effective date.
      Cashflow work = new Cashflow();
      if (cds.CdsType == CdsType.FundedFloating)
      {
        Dt from = cds.Effective;
        const double principal = 1.0;
        double defaultAmount = recovery;
        CashflowFactory.FillFloat(work, from, cds.CashflowFactorySchedule, cds.ScheduleParams, cds.Ccy, cds.DayCount,
          cds.Premium, cds.PremiumSchedule,
          principal, cds.AmortizationSchedule,
          referenceCurve, this.RateResets,
          defaultAmount, defaultCcy, defaultDate,
          fee, this.FeeSettle);
      }
      else
      {
        Dt from = cds.Effective;
        double principal = (cds.Funded) ? 1.0 : 0.0;
        double defaultAmount = (cds.Funded) ? recovery : recovery - 1.0;
        CashflowFactory.FillFixed(work, from, cds.CashflowFactorySchedule, cds.ScheduleParams, cds.Ccy, cds.DayCount,
          cds.Premium, cds.PremiumSchedule,
          principal, cds.AmortizationSchedule,
          defaultAmount, defaultCcy, defaultDate,
          fee, this.FeeSettle);
      }

      int last = work.Count - 1;
      if (last < 0 || (defaultDate.IsValid() && work.GetDt(last) != defaultDate))
      {
        // the unlikely case that the credit defaults before effective
        return cashflow;
      }

      if (RecoveryCurve.JumpDate.IsValid())
      {
        var defaultPaymentDate = RecoveryCurve.JumpDate;
        var dpay = new Cashflow.ScheduleInfo
        {
          Date = defaultPaymentDate,
          Accrual = work.GetAccrued(last)
        };
        if (cds.Funded)
          dpay.Amount = work.GetDefaultAmount(last);
        else
          dpay.Loss = work.GetDefaultAmount(last);

        if (SupportAccrualRebateAfterDefault)
        {
          Cashflow fullCashflow = new Cashflow();
          if (cds.CdsType == CdsType.FundedFloating)
          {
            Dt from = cds.Effective;
            const double principal = 1.0;
            CashflowFactory.FillFloat(fullCashflow, from, cds.CashflowFactorySchedule, cds.ScheduleParams, cds.Ccy, cds.DayCount,
              cds.Premium, cds.PremiumSchedule,
              principal, cds.AmortizationSchedule,
              referenceCurve, this.RateResets,
              0.0, defaultCcy, Dt.Empty,
              fee, this.FeeSettle);
          }
          else
          {
            Dt from = cds.Effective;
            double principal = (cds.Funded) ? 1.0 : 0.0;
            CashflowFactory.FillFixed(fullCashflow, from, cds.CashflowFactorySchedule, cds.ScheduleParams, cds.Ccy, cds.DayCount,
              cds.Premium, cds.PremiumSchedule,
              principal, cds.AmortizationSchedule,
              0.0, defaultCcy, Dt.Empty,
              fee, this.FeeSettle);
          }

          int rebateIdx = -1;
          for (int i = 0; i < fullCashflow.Count; ++i)
          {
            if (defaultDate <= fullCashflow.GetDt(i) && defaultPaymentDate >= fullCashflow.GetDt(i))
            {
              rebateIdx = i;
              break;
            }
          }

          if (rebateIdx >= 0)
          {
            dpay.Accrual -= fullCashflow.GetAccrued(rebateIdx);
            var payDt = fullCashflow.GetDt(rebateIdx);
            if (payDt >= asOf)
            {
              cashflow.Add(fullCashflow.GetStartDt(rebateIdx), fullCashflow.GetEndDt(rebateIdx), payDt,
                fullCashflow.GetPeriodFraction(rebateIdx), 0.0, 0.0, fullCashflow.GetAccrued(rebateIdx), 0.0, 0.0);
            }
          }
        }
        cashflow.DefaultPayment = dpay;
      }

      if (cds.Bullet != null)
      {
        cashflow.AddMaturityPayment(cds.Bullet.CouponRate, 0, 0, 0);
      }
      return cashflow;
    }

    /// <summary>
    ///   Helper function to generate cashflows, 1bp premium cashflow for CDS
    /// </summary>
    ///
    private void GenerateCashflow01(Cashflow cashflow, Dt asOf, Dt settle, bool includeAccrued)
    {
      CDS cds = CDS;
      double recovery;
      if (IsQuantoStructure && cds.RecoveryCcy != DiscountCurve.Ccy)
        recovery = cds.QuantoNotionalCap.HasValue ? 1 - cds.QuantoNotionalCap.Value : 0.0;
      else
        recovery = cds.FixedRecovery ? cds.FixedRecoveryRate : RecoveryRate;
      Dt effective = cds.Effective;
      Dt firstPrem = cds.FirstPrem;
      Dt lastPrem = cds.LastPrem;
      // If we don't want accrued, adjust effective and firstPrem to force 0 accrued
      if (!includeAccrued && Dt.Cmp(settle, effective) > 0)
      {
        // RTD: I think this is incorrect - it should be previous coupon.
        effective = settle;
        firstPrem = cds.GetNextPremiumDate(effective);
        /*
          8/9/2007    9/9/2007            3/23/2009  6/8/2009
         ----|----------|---------------------|---------|
         Effective  FirstPrem              AsOf      Maturity
        
         In some situations like above, the risky duration calculation
         sets the new effective to be settle (say 3/24/2009) based on
         which a new first premimuim date is computed, if it passes the
         CDS maturity, it is set to  be maturity. As a result the last
         premium date is before first premium date, cause an error.
         The solution (as MF pointed to be appropriate fix) is to set
         the last premium to be same as first premium.
         */
        if (!lastPrem.IsEmpty() && (firstPrem > lastPrem))
          lastPrem = firstPrem;
      }

      var cycleRule = cds.CycleRule;

      CashflowFlag flags = CashflowFlag.IncludeDefaultDate;
      if (cds.AccruedOnDefault)
        flags |= CashflowFlag.AccruedPaidOnDefault;
      if (IncludeMaturityAccrual)
        flags |= CashflowFlag.IncludeMaturityAccrual;

      var schedParams = new ScheduleParams(effective, firstPrem, lastPrem, cds.Maturity, cds.Freq, cds.BDConvention,
                                           cds.Calendar, cycleRule, flags);

      const double fee = 0.0;
      Dt feeSettle = Dt.Empty;
      const double principal = 0.0;
      const double premium = 0.0001;

      Currency defaultCcy = cds.Ccy;
      double defaultAmount = recovery - 1.0;
      Dt defaultDate = (SurvivalCurve != null) ? SurvivalCurve.DefaultDate : Dt.Empty;

      // Generate all cashflow on or after settlement date excluding any upfront fee
      CashflowFactory.FillFixed(cashflow, asOf, schedParams, cds.Ccy, cds.DayCount,
                                premium, null, principal, cds.AmortizationSchedule,
                                defaultAmount, defaultCcy, defaultDate,
                                fee, feeSettle);
    }

    #endregion

    #region Payment schedule to cash flow conversion

    /// <summary>
    ///  Generate the legacy cash flows through payment schedule.
    ///  Mainly used to check the full backward compatibility.
    /// </summary>
    public Cashflow PaymentScheduleToCashflow(Dt asOf = new Dt())
    {
      var cds = CDS;
      var recovery = GetRecoveryRate();
      var defaultAmount = (cds.Funded) ? recovery : recovery - 1.0;

      if (asOf.IsEmpty()) asOf = AsOf;
      var ps = GeneratePayments(asOf, cds, Fee());
      var cf = PaymentScheduleUtils.FillCashflow(null,
        ps, asOf, 1.0, defaultAmount);
      cf.AsOf = asOf;
      cf.Frequency = cds.Freq;
      cf.AccruedFractionOnDefault = 0.5;
      cf.DefaultTiming = 0.5;
      cf.AccruedPaidOnDefault = cds.AccruedOnDefault;
      cf.AccruedIncludingDefaultDate = (cds.CashflowFlag & CashflowFlag.IncludeDefaultDate) != 0;

      var dpay = cf.DefaultPayment;
      if (dpay != null)
      {
        var amount = dpay.Loss + dpay.Amount;
        dpay.Loss = dpay.Amount = 0;
        if (cds.Funded) dpay.Amount = amount;
        else dpay.Loss = amount;
      }
      return cf;
    }

    #endregion

    #region Consistency checks

    private void LogDiffs(double cfPv, Func<double> psPvFn, string what)
    {
      LogDiffs(() => cfPv, psPvFn, what);
    }

    private void LogDiffs(Func<double> cfPvFn, double psPv, string what)
    {
      LogDiffs(cfPvFn, () => psPv, what);
    }

    private void LogDiffs(Func<double> cfPvFn, Func<double> psPvFn, string what)
    {
      if (_insideLogger) return;
      _insideLogger = true;
      try
      {
        double cfPv = cfPvFn(), psPv = psPvFn();
        if (!(Math.Abs(psPv - cfPv) <= 5E-14 * Math.Abs(Notional)))
          logger.Debug($"{what}, psPv = {psPv}, cfPv = {cfPv}, diff = {psPv - cfPv}");
      }
      finally
      {
        _insideLogger = false;
      }
    }

    [ThreadStatic]
    private static bool _insideLogger;

    // For consistency check
    public static bool IsLegacyCashflowEnabled { get; set; }

    #endregion
  }


  #endregion CDSCashflowPricer

  #region CdsLinkedNotePricer

  public partial class CdsLinkedNotePricer
  {
    private partial class CdsDynamicPricer
    {
      private static Cashflow GenerateCashflowForFee(CDS cds, Dt settle)
      {
        return PriceCalc.GenerateCashflowForFee(settle, cds.Premium,
          cds.Effective, cds.FirstPrem, cds.Maturity,
          cds.Ccy, cds.DayCount, cds.Freq, cds.BDConvention, cds.Calendar,
          null, false, null, null);
      }

      // Generate simple cashflow stream
      private static Cashflow GenerateCashflowForProtection(CDS cds, Dt settle)
      {
        return PriceCalc.GenerateCashflowForProtection(settle,
          cds.Maturity, cds.Ccy, null);
      }

      private double ProtectionPv(Dt settle, Cashflow cf, CDS cds,
        DiscountCurve discountCurve, double defaultTiming,
        double accruedFractionOnDefault)
      {
        Dt maturity = cds.Maturity;
        if (settle >= maturity)
          return 0.0;
        double pv = PriceCalc.Price(cf, settle, discountCurve,
          LossAt, SurvivalAt, null, false, true, false,
          defaultTiming, accruedFractionOnDefault,
          dynamicSurvival_.StepSize, dynamicSurvival_.StepUnit, cf.Count);
        return pv;
      }

      private double FeePv(Dt settle, Cashflow cf, CDS cds,
        DiscountCurve discountCurve, double defaultTiming,
        double accruedFractionOnDefault)
      {
        if (settle > cds.Maturity)
          return 0.0;
        return PriceCalc.Price(cf, settle, discountCurve,
          LossAt, SurvivalAt, null, true, false, false, defaultTiming,
          accruedFractionOnDefault, dynamicSurvival_.StepSize,
          dynamicSurvival_.StepUnit, cf.Count);
      }

      private double CfPv(Dt dt)
      {
        double ppv = ProtectionPv(dt, protectionCf_, cds_,
          discount_, defaultTiming_, accruedFractionAtDefault_);
        double fpv = FeePv(dt, feeCf_, cds_, discount_,
          defaultTiming_, accruedFractionAtDefault_);
        return ppv + fpv;
      }

      #region Data
      private readonly Cashflow feeCf_;
      private readonly Cashflow protectionCf_;
      #endregion Data

    }
  }
  #endregion CdsLinkedNotePricer

  #region CdoLinkedNotePricer
  public partial class CdoLinkedNotePricer
  {
    private partial class CdoDynamicPricer
    {
      private static Cashflow GenerateCashflowForFee(SyntheticCDO cdo, Dt settle)
      {
        return PriceCalc.GenerateCashflowForFee(settle, cdo.Premium,
          cdo.Effective, cdo.FirstPrem, cdo.Maturity,
          cdo.Ccy, cdo.DayCount, cdo.Freq, cdo.BDConvention, cdo.Calendar,
          null, false, null, null);
      }

      // Generate simple cashflow stream
      private static Cashflow GenerateCashflowForProtection(SyntheticCDO cdo, Dt settle)
      {
        return PriceCalc.GenerateCashflowForProtection(settle,
          cdo.Maturity, cdo.Ccy, null);
      }


      private double ProtectionPv(Dt settle, Cashflow cf,
        SyntheticCDO cdo, BasketPricer basketPricer,
        DiscountCurve discountCurve, double defaultTiming,
        double accruedFractionOnDefault)
      {
        Dt maturity = cdo.Maturity;
        if (settle >= maturity)
          return 0.0;
        double pv = PriceCalc.Price(cf, settle, discountCurve,
          LossAt, SurvivalAt, null, false, true, false,
          defaultTiming, accruedFractionOnDefault, basketPricer.StepSize,
          basketPricer.StepUnit, cf.Count);
        pv += basketPricer.DefaultSettlementPv(settle, cdo.Maturity,
          discountCurve, cdo.Attachment, cdo.Detachment,
          true, false);
        return pv;
      }

      private double FeePv(Dt settle, Cashflow cf, SyntheticCDO cdo,
        BasketPricer basketPricer,
        DiscountCurve discountCurve, double defaultTiming,
        double accruedFractionOnDefault)
      {
        if (settle > cdo.Maturity)
          return 0.0;
        return PriceCalc.Price(cf, settle, discountCurve, LossAt,
          SurvivalAt, null, true, false, false, defaultTiming,
          accruedFractionOnDefault, basketPricer.StepSize,
          basketPricer.StepUnit, cf.Count);
      }

      private double CfPv(Dt dt)
      {
        double ppv = ProtectionPv(dt, protectionCf_, cdo_,
          basketPricer_, discount_, defaultTiming_,
          accruedFractionAtDefault_);
        double fpv = FeePv(dt, feeCf_, cdo_, basketPricer_,
          discount_, defaultTiming_, accruedFractionAtDefault_);
        return ppv + fpv;
      }

      #region Data
      private readonly Cashflow feeCf_;
      private readonly Cashflow protectionCf_;
      #endregion Data

    }
  }

  #endregion CdoLinkedNotePricer

  #region CDXPricer

  public partial class CDXPricer
  {
    /// <summary>
    ///   All future cashflows for this product.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Returns the cashflows for this product from the pricing asOf date.</para>
    /// </remarks>
    ///
    public Cashflow Cashflow
    {
      get { return GenerateCashflow(cashflow_, AsOf); }
    }

    // only risk calls this function
    /// <summary>
    ///   Helper function to generate cashflows for CDX
    /// </summary>
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt from)
    {
      DiscountCurve referenceCurve = (ReferenceCurve != null) ? ReferenceCurve : DiscountCurve;
      return CDXPricerUtil.GenerateCashflow(cashflow, from, Settle,
        CDX, MarketRecoveryRate, referenceCurve, RateResets, SurvivalCurves, RecoveryCurves);
    }

    private Cashflow cashflow_;                     // Cashflow matching product
  }

  #endregion CDXPricer

  #region CommoditySwaplegPricer

  public partial class CommoditySwapLegPricer
  {
    /// <summary>
    /// Generate cashflows
    /// </summary>
    /// <param name="cf">A cashflow object</param>
    /// <param name="from">Cashflow start date</param>
    /// <returns>Cashflow associated to the swap</returns>
    public override Cashflow GenerateCashflow(Cashflow cf, Dt from)
    {
      var paymentSchedule = GetPaymentSchedule(null, from);
      cf = PaymentScheduleUtils.FillCashflow(cf, paymentSchedule, from, SwapLeg.Notional, 0.0);
      return cf;
    }
  }

  #endregion CommoditySwaplegPricer

  #region DefaultdAssetPricer

  public partial class DefaultedAssetPricer
  {
    /// <summary>
    /// Get cashflows for this product from the specified date
    /// </summary>
    /// <param name="cashflow">Cashflow to fill. May be null to generate a new Cashflow.</param>
    /// <param name="from">Date to generate cashflows from</param>
    /// <returns>Cashflow from the specified date</returns>
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt from)
    {
      if (RecoveryCurve == null)
        return new Cashflow(AsOf);
      PaymentSchedule ps = GetPaymentSchedule(null, from);
      double recoveryRate = RecoveryCurve.RecoveryRate(Product.Maturity);
      cashflow = PaymentScheduleUtils.FillCashflow(cashflow, ps, from, Notional, recoveryRate);
      cashflow.DefaultCurrency = Product.Ccy;
      return cashflow;
    }
  }

  #endregion DefaultedAssetPricer

  #region FraPricer

  public partial class FRAPricer
  {
    /// Only risk calls this function
    /// <summary>
    ///   Get cashflows for this product from the specified date
    /// </summary>
    /// <remarks>
    ///   <para>Returns the cashflows for this product from the specified date.</para>
    ///   <para>Derived pricers may implement this, otherwise a NotImplementedException is thrown.</para>
    /// </remarks>
    /// <param name="cashflow">Cashflow to fill. May be null to generate a new Cashflow.</param>
    /// <param name="from">Date to generate cashflows from</param>
    /// <returns>Cashflow from the specified date or null if not supported</returns>
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt from)
    {
      var paymentSchedule = GetPaymentSchedule(null, from);
      return PaymentScheduleUtils.FillCashflow(cashflow, paymentSchedule, from, Product.Notional, 0.0);
    }
  }


  #endregion FraPricer

  #region FTDPricer

  public partial class FTDPricer
  {
    /// <summary>
    ///   Helper function to generate cashflows for NTD
    /// </summary>
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt from)
    {
      FTD ftd = this.FTD;

      // Find the termination date
      Dt maturity = ftd.Maturity;

      // TBD: Cashflow schedule is currently only filled in for premium side
      if (cashflow == null)
        cashflow = new Cashflow(from);

      double principal = (ftd.NtdType == NTDType.FundedFixed || ftd.NtdType == NTDType.FundedFloating) ? 1.0 : 0.0;

      CycleRule cycleRule = ftd.CycleRule;
      CashflowFlag flags = CashflowFlag.IncludeMaturityAccrual;

      // Floating
      if (ftd.NtdType == NTDType.FundedFloating || ftd.NtdType == NTDType.IOFundedFloating)
      {
        DiscountCurve referenceCurve = discountCurve_;

        // Fill in reset dates and reset rates
        Dt[] resetDates;
        double[] resetCpns;
        RateResetUtil.FromSchedule(RateResets, out resetDates, out resetCpns);

        Currency defaultCcy = ftd.Ccy;
        const double defaultAmount = -1.0;
        Dt defaultDate = Dt.Empty;

        var schedParams = new ScheduleParams(ftd.Effective, ftd.FirstPrem, ftd.LastPrem, ftd.Maturity, ftd.Freq,
                                             ftd.BDConvention, ftd.Calendar, cycleRule, flags);

        CashflowFactory.FillFloat(cashflow, from, schedParams, ftd.Ccy, ftd.DayCount,
                                  ftd.Premium, null,
                                  principal, null,
                                  referenceCurve, RateResets,
                                  defaultAmount, defaultCcy, defaultDate,
                                  ftd.Fee, ftd.FeeSettle);
      }
      else // Unfunded/FundedFixed
      {
        Currency defaultCcy = ftd.Ccy;
        const double defaultAmount = 0.0;
        Dt defaultDate = Dt.Empty;

        flags |= CashflowFlag.IncludeDefaultDate;

        var schedParams = new ScheduleParams(ftd.Effective, ftd.FirstPrem, ftd.LastPrem, ftd.Maturity, ftd.Freq,
                                             ftd.BDConvention, ftd.Calendar, cycleRule, flags);

        CashflowFactory.FillFixed(cashflow, from, schedParams, ftd.Ccy, ftd.DayCount,
                                  ftd.Premium, null,
                                  principal, null,
                                  defaultAmount, defaultCcy, defaultDate,
                                  ftd.Fee, ftd.FeeSettle);

        if (FTD.NtdType == NTDType.PO)
        {
          cashflow.Add(maturity, maturity, maturity, 0.0, 1.0, 0.0, 0.0, -1.0);
        }
      }

      if (ftd.Bullet != null)
      {
        cashflow.AddMaturityPayment(ftd.Bullet.CouponRate, 0, 0, 0);
      }

      return cashflow;
    }



    private double CfAccrued(FTD ftd, Dt settle, double premium)
    {
      if (Dt.Cmp(settle, ftd.Maturity) > 0 || FTD.NtdType == NTDType.PO)
        return 0;
      if (Math.Abs(premium) < 1E-15)
        return 0;

      // If no periodic frequency or no premium
      if (((int)ftd.Freq) < 1)
        return 0.0;

      // Generate cashflow stream
      Cashflow cf = PriceCalc.GenerateCashflowForFee(
        settle, premium, ftd.Effective, ftd.FirstPrem, ftd.Maturity,
        ftd.Ccy, ftd.DayCount, ftd.Freq, ftd.BDConvention, ftd.Calendar,
        null, ftd.NtdType == NTDType.FundedFloating
        || ftd.NtdType == NTDType.IOFundedFloating, this.DiscountCurve,
        this.RateResets);

      // Find first cashflow on or after settlement (depending on includeSettle flag)
      int N = cf.Count;
      int firstIdx;
      for (firstIdx = 0; firstIdx < N; firstIdx++)
      {
        if (Dt.Cmp(cf.GetDt(firstIdx), settle) > 0)
          break;
      }
      if (firstIdx >= N)
        return 0.0; // This may happen when the forward date is after maturity, for example.

      //TODO: revist this to consider start and end dates.
      Dt accrualStart = (firstIdx > 0) ? cf.GetDt(firstIdx - 1) : cf.Effective;
      if (Dt.Cmp(accrualStart, settle) > 0)
        return 0.0; // this may occur for forward starting CDO

      Dt nextDate = cf.GetDt(firstIdx);
      double paymentPeriod = Dt.Fraction(accrualStart, nextDate,
        accrualStart, nextDate, ftd.DayCount, ftd.Freq);
      if (paymentPeriod < 1E-10)
        return 0.0; // this may happen if maturity on settle, for example

      double accrued = Basket.AccrualFraction(accrualStart,
                         settle, ftd.DayCount, ftd.Freq,
                         ftd.First, ftd.NumberCovered,
                         ftd.AccruedOnDefault) / paymentPeriod * cf.GetAccrued(firstIdx);
      return accrued;
    }


    private double CfProtectionPv(int nth, Dt settle)
    {
      FTD ftd = this.FTD;
      if (Dt.Cmp(GetProtectionStart(), ftd.Maturity) > 0)
        return 0.0;
      Curve lossCurve = Basket.NthLossCurve(nth);
      Dt dfltDate = lossCurve.JumpDate;
      if (!dfltDate.IsEmpty() && dfltDate <= settle)
        return 0.0;
      Cashflow cashflow = PriceCalc.GenerateCashflowForProtection(
        settle, ftd.Maturity, ftd.Ccy, null);

      // Use 0-flat discount curve to hornor PayRecoveryAtMaturity = true
      DiscountCurve flatDiscCurve = new DiscountCurve(AsOf, 0);
      double pv = PriceCalc.Price(cashflow, settle,
        FTD.PayRecoveryAtMaturity ? flatDiscCurve : DiscountCurveForContingentLeg,
        lossCurve.Interpolate,
        (dt) => 1.0, // not used
        null, false, true, false,
        DefaultTiming, AccruedFractionOnDefault,
        StepSize, StepUnit, cashflow.Count);
      if (FTD.PayRecoveryAtMaturity)
        pv *= DiscountCurveForContingentLeg.DiscountFactor(settle, FTD.Maturity);
      return pv * Notional / ftd.NumberCovered;
    }

    private double CfRecoveryPv(int nth, Dt settle, Curve nthSurvival)
    {
      FTD ftd = FTD;
      if (Dt.Cmp(GetProtectionStart(), ftd.Maturity) > 0)
        return 0.0;
      Curve nthLoss = Basket.NthLossCurve(nth);
      Dt dfltDate = nthLoss.JumpDate;
      if (!dfltDate.IsEmpty() && dfltDate <= settle)
        return 0.0;
      Cashflow cashflow = PriceCalc.GenerateCashflowForProtection(
        settle, ftd.Maturity, ftd.Ccy, null);

      // Use 0-flat discount curve to hornor PayRecoveryAtMaturity = true
      DiscountCurve flatDiscCurve = new DiscountCurve(AsOf, 0);
      if (IsQuantoStructure)
      {
        //the survival curve is under domestic measure, 
        //while the loss curve is under the foreign measure, 
        //so we need to compute separately
        double fpv = PriceCalc.Price(cashflow, settle,
          ftd.PayRecoveryAtMaturity ? flatDiscCurve : DiscountCurve,
          (dt) => 1 - nthSurvival.Interpolate(dt),
          (dt) => 1.0, // not used
          null, false, true, false,
          DefaultTiming, AccruedFractionOnDefault,
          StepSize, StepUnit, cashflow.Count);
        if (ftd.PayRecoveryAtMaturity)
          fpv *= DiscountCurve.DiscountFactor(settle, ftd.Maturity);
        double ppv = PriceCalc.Price(cashflow, settle,
          ftd.PayRecoveryAtMaturity ? flatDiscCurve : DiscountCurveForContingentLeg,
          nthLoss.Interpolate, (dt) => 1.0, // not used
          null, false, true, false,
          DefaultTiming, AccruedFractionOnDefault,
          StepSize, StepUnit, cashflow.Count);
        if (ftd.PayRecoveryAtMaturity)
          ppv *= DiscountCurveForContingentLeg.DiscountFactor(settle, ftd.Maturity);
        return -(fpv - ppv); // reverse the sign since this is recovery, not loss.
      }
      double pv = PriceCalc.Price(cashflow, settle,
        ftd.PayRecoveryAtMaturity ? flatDiscCurve : DiscountCurve,
        (dt) => 1 - nthSurvival.Interpolate(dt) - nthLoss.Interpolate(dt),
        (dt) => 1.0, // not used
        null, false, true, false,
        DefaultTiming, AccruedFractionOnDefault,
        StepSize, StepUnit, cashflow.Count);
      if (ftd.PayRecoveryAtMaturity)
        pv *= DiscountCurve.DiscountFactor(settle, ftd.Maturity);
      return -pv; // reverse the sign since this is recovery, not loss.
    }


    private double CfFeePv(int nth, Dt settle, double premium,
      DayCount dayCount, Frequency freq, DiscountCurve discountCurve)
    {
      FTD ftd = this.FTD;
      if (Dt.Cmp(settle, ftd.Maturity) > 0)
        return 0;

      // Get the survival curve
      BasketForNtdPricer basket = Basket;
      SurvivalCurve curve = basket.NthSurvivalCurve(nth);
      Dt dfltDate = curve.JumpDate;
      if (!dfltDate.IsEmpty() && dfltDate <= settle)
      {
        // The default settlement payment is handled in other places.
        // Here we need to add the accrued.
        FTD ftdOne = (FTD)ftd.Clone();
        ftdOne.First = nth;
        ftdOne.NumberCovered = 1;
        ftdOne.DayCount = dayCount;
        ftd.Freq = freq;
        double accrued = Accrued(ftdOne, settle, premium);
        return accrued * Notional / ftd.NumberCovered
               / discountCurve.DiscountFactor(AsOf, settle);
      }
      var referenceCurve = ReferenceCurve ?? DiscountCurve;
      // We cannot simply call PriceCalcObsolete.GenerateCashflowForFee()
      // with the parameter floating to be false, because we MUST handle
      // floating rate in the cashflow generator.
      Cashflow cashflow = PriceCalc.GenerateCashflowForFee(
        settle, premium, ftd.Effective, ftd.FirstPrem, ftd.Maturity,
        ftd.Ccy, dayCount, freq, ftd.BDConvention, ftd.Calendar,
        null, ftd.NtdType == NTDType.FundedFloating || ftd.NtdType == NTDType.IOFundedFloating,
        referenceCurve/*as reference curve*/, RateResets);

      int stepSize = 0; TimeUnit stepUnit = TimeUnit.None;

      // This mimics the Funded CDO: add back the credit-contingent maturity fee 
      double fundPaidBack = 0;
      if (settle < Maturity && (ftd.NtdType == NTDType.FundedFixed
                                || ftd.NtdType == NTDType.FundedFloating || ftd.NtdType == NTDType.PO))
      {
        // Fund payback is the recovery payback plus the remaining notional at maturity.
        fundPaidBack = RecoveryPv(nth, settle, curve)
                       + curve.Interpolate(ftd.Maturity)
                       * discountCurve.DiscountFactor(settle, ftd.Maturity);
      }
      if (ftd.NtdType == NTDType.PO)
        return fundPaidBack * Notional / ftd.NumberCovered;

      double pv = PriceCalc.Price(
        cashflow, settle, discountCurve,
        (dt) => 0.0,
        curve.Interpolate,
        null, true, false, false,
        DefaultTiming, AccruedFractionOnDefault,
        stepSize, stepUnit, cashflow.Count);
      pv += fundPaidBack;

      return pv * Notional / ftd.NumberCovered;
    }

    private double CfCarry()
    {
      FTD ftd = FTD;

      // Floating coupon case
      if (ftd.NtdType == NTDType.FundedFloating || ftd.NtdType == NTDType.IOFundedFloating)
      {
        Dt settle = GetProtectionStart();

        // If settle date is later than NTD maturity, nothing to carry
        if (settle >= ftd.Maturity)
          return 0.0;

        // If no periodic frequency or no premium
        if (((int)ftd.Freq) < 1 || Math.Abs(ftd.Premium) < 1E-14)
          return 0.0;

        //- Generate a cahsflow, which has taken care of using the
        //- correct floating coupons when neccessary.        
        Cashflow cf = PriceCalc.GenerateCashflowForFee(
          settle, ftd.Premium, ftd.Effective, ftd.FirstPrem, ftd.Maturity,
          ftd.Ccy, ftd.DayCount, ftd.Freq, ftd.BDConvention, ftd.Calendar,
          null, true, this.DiscountCurve, this.RateResets);

        //- Find the latest cashflow on or before settlement
        int N = cf.Count;
        int firstIdx;
        for (firstIdx = 0; firstIdx < N; firstIdx++)
        {
          if (Dt.Cmp(cf.GetDt(firstIdx), settle) > 0)
            break;
        }
        //TODO: Revisit this to use start and end dates.
        Dt accrualStart = (firstIdx > 0) ? cf.GetDt(firstIdx - 1) : cf.Effective;
        if (Dt.Cmp(accrualStart, settle) > 0)
          return 0.0;

        Dt nextDate = cf.GetDt(firstIdx);
        double paymentPeriod = Dt.Fraction(accrualStart, nextDate, accrualStart, nextDate, ftd.DayCount, ftd.Freq);
        if (paymentPeriod < 1E-10)
          return 0.0;

        //- find the effective coupon on the settle date
        double coupon = cf.GetAccrued(firstIdx) / paymentPeriod;
        return coupon / 360 * this.CurrentNotional;
      }
      return ftd.Premium / 360 * this.CurrentNotional;
    }


    /// <summary>
    ///   Cashflow schedule of payments for NTD
    /// </summary>
    ///
    public Cashflow Cashflow
    {
      get { return GenerateCashflow(null, AsOf); }
    }
  }

  #endregion FTDPricer

  #region InflationBondPricer

  public partial class InflationBondPricer
  {
    /// <summary>
    /// Generate inflation bond cashflows
    /// </summary>
    /// <param name="cf">A cashflow object</param>
    /// <param name="asOf">As of date</param>
    /// <returns>Cashflow associated to the inflation bond</returns>
    public override Cashflow GenerateCashflow(Cashflow cf, Dt asOf)
    {
      var paymentSchedule = GetPaymentSchedule(null, asOf);
      cf = PaymentScheduleUtils.FillCashflow(cf, paymentSchedule,
        asOf, InflationBond.Notional, RecoveryRate);
      cf.DayCount = InflationBond.DayCount;
      return cf;
    }

  }

  #endregion InflationBondPricer

  #region LCDXPricer

  public partial class LCDXPricer
  {
    /// <summary>
    ///   All future cashflows for this product.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Returns the cashflows for this product from the pricing asOf date.</para>
    /// </remarks>
    ///
    public Cashflow Cashflow
    {
      get { return GenerateCashflow(cashflow_, AsOf); }
    }

    // only risk calls this function
    /// <summary>
    ///   Helper function to generate cashflows for CDX
    /// </summary>
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt from)
    {
      DiscountCurve referenceCurve = (ReferenceCurve != null) ? ReferenceCurve : DiscountCurve;
      return CDXPricerUtil.GenerateCashflow(cashflow, from, Settle,
        LCDX, MarketRecoveryRate, referenceCurve, RateResets, SurvivalCurves, RecoveryCurves);
    }

    private Cashflow cashflow_;                        // Cashflow matching product
  }

  #endregion LCDXPricer

  #region LoanPricer

  public partial class LoanPricer
  {
    public double YieldToMaturityCf(Dt asOf, Dt settle,
      Frequency cmpdFrequency)
    {
      var schedule = Loan.CallSchedule;
      var maturity = Loan.Maturity;
      if (schedule != null && schedule.Count > 0)
      {
        var callDates = schedule.Select(s => s.StartDate).ToArray();
        var idx = GetIndex(callDates, maturity);
        if (idx >= 0)
          return YieldToCall(asOf, settle, cmpdFrequency,
            schedule[idx].StartDate, schedule[idx].CallPrice);
      }
      Cashflow cf = GenerateCashflow(null, settle);
      cf.AsOf = asOf;
      int flags = (int)(CashflowModelFlags.IncludeFees
                        | CashflowModelFlags.IncludeProtection);
      int stepSize = 0;
      double settlementCash = MarketFullPrice();
      DayCount daycount = (Loan.DayCount == DayCount.ActualActualBond
        ? DayCount.Actual365Fixed : Loan.DayCount);
      double yield = CashflowModel.Irr(cf, asOf, settle, null, null, 0.0,
        flags, stepSize, TimeUnit.None,
        settlementCash, daycount, cmpdFrequency);
      return yield;
    }

    public double YieldToCallCf(Dt asOf, Dt settle, Frequency cmpdfrequency,
      Dt callDate, double callStrike = Double.NaN)
    {
      if (Loan.LoanType != LoanType.Term)
        throw new ToolkitException("Yield to call is currently " +
                                   "supported only for Term loans.");
      Cashflow cf = GenerateCashflowToCall(asOf, settle, settle, callDate);
      int flags = (int) (CashflowModelFlags.IncludeFees
        | CashflowModelFlags.IncludeProtection);

      if (!Double.IsNaN(callStrike))
      {
        var last = cf.Count - 1;
        Debug.Assert(cf.GetPrincipalAt(last).AlmostEquals(cf.GetAmount(last)));
        cf.Set(last, cf.GetAmount(last) * callStrike, cf.GetAccrued(last),
          cf.GetDefaultAmount(last));
      }
      int stepSize = 0;
      double settlementCash = MarketFullPrice();
      DayCount daycount = (Loan.DayCount == DayCount.ActualActualBond
        ? DayCount.Actual365Fixed : Loan.DayCount);
      double yield = CashflowModel.Irr(cf, asOf, settle, null, null,
        0.0, flags, stepSize, TimeUnit.None,
        settlementCash, daycount, cmpdfrequency);
      return yield;
    }

    private double PriceFromYieldCf(double yield, Dt callDate,
      double callStrike = double.NaN)
    {
      Cashflow cf = callDate == Loan.Maturity
        ? GenerateCashflow(null, Settle)
        : GenerateCashflowToCall(AsOf, Settle, Settle, callDate);
      CashflowModelFlags flags = CashflowModelFlags.IncludeFees
                                 | CashflowModelFlags.IncludeProtection;
      DayCount dayCount = (Loan.DayCount == DayCount.ActualActualBond
        ? DayCount.Actual365Fixed
        : Loan.DayCount);

      if (!Double.IsNaN(callStrike))
      {
        var last = cf.Count - 1;
        Debug.Assert(cf.GetPrincipalAt(last).AlmostEquals(cf.GetAmount(last)));
        cf.Set(last, cf.GetAmount(last) * callStrike, cf.GetAccrued(last),
          cf.GetDefaultAmount(last));
      }

      var dc = DiscountCurveFromYield(yield, AsOf,
        Enumerable.Range(0, cf.Count).Select(i => cf.GetDt(i)),
        dayCount, Loan.Frequency);
      return CashflowModel.Price(cf, AsOf, Settle, dc, null, null,
        0.0, (int)flags, 0, TimeUnit.None, cf.Count);
    }

    #region Cashflow Generation
    /// <summary>
    /// Generates cash flows for a Loan using the current 
    /// level in the pricing grid.
    /// </summary>
    /// 
    /// <param name="cashflow">Cashflow to fill</param>
    /// <param name="from">The date to fill from</param>
    /// 
    /// <remarks>
    /// <para>Assumes that the performance level does not 
    /// change and thus that the spread over the 
    /// floating rate index does not change. </para>
    /// </remarks>
    /// 
    /// <returns>Cashflow</returns>
    /// 
    public CashflowStream GenerateCashflowStream(
      CashflowStream cashflow, Dt from)
    {
      return GenerateCashflowStream(Loan, cashflow, from,
        CurrentLevel, InterestPeriods, ReferenceCurve);
    }

    /// <summary>
    /// Generates cash flows for a Loan using the current level in the pricing grid.
    /// </summary>
    /// 
    /// <param name="cashflow">Cashflow to fill</param>
    /// <param name="from">The date to fill from</param>
    /// 
    /// <remarks>
    /// <para>Assumes that the performance level does not change and thus that the spread over the 
    /// floating rate index does not change. </para>
    /// </remarks>
    /// 
    /// <returns>Cashflow</returns>
    /// 
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt from)
    {
      return Loan.GenerateCashflow(cashflow, AsOf, Settle, from, CurrentLevel,
        DrawnCommitment / TotalCommitment,
        InterestPeriods, ReferenceCurve, null, FloorValues);
    }

    /// <summary>
    /// Generates cash flows for a defaulted loan to a default date.
    /// </summary>
    public Cashflow GenerateCashflow(Cashflow cashflow, Dt from, Dt defaultDate)
    {
      ScheduleParams scParams = Loan.GetScheduleParams();
      return Loan.GenerateCashflow(cashflow, AsOf, Settle, from, CurrentLevel,
        DrawnCommitment / TotalCommitment,
        InterestPeriods, ReferenceCurve, null, FloorValues, scParams, defaultDate);
    }

    /// <summary>
    /// Generates cash flows for a Loan using the current level in the pricing grid.
    /// </summary>
    /// 
    /// <param name="cashflow">Cashflow to fill</param>
    /// <param name="from">The date to fill from</param>
    /// 
    /// <returns>Cashflow</returns>
    /// 
    public Cashflow GenerateCashflowFromLastReset(Cashflow cashflow, Dt from)
    {
      return Loan.GenerateCashflow(cashflow, AsOf, Settle, from, CurrentLevel,
        DrawnCommitment / TotalCommitment,
        InterestPeriods, ReferenceCurve, Schedule, FloorValues);
    }

    public Cashflow GenerateCashflowToCall(Dt asOf, Dt settle, Dt from, Dt callDate)
    {
      Cashflow cf = Loan.GenerateCashflowToCall(from, asOf, settle, CurrentLevel,
        DrawnCommitment / TotalCommitment,
        InterestPeriods, ReferenceCurve, null, FloorValues, callDate);
      cf.AsOf = asOf;
      return cf;
    }

    /// <summary>
    /// Generates cash flows for a Loan using the current level in the pricing grid.
    /// </summary>
    /// 
    /// <param name="cashflow">Cashflow to fill</param>
    /// <param name="from">The date to fill from</param>
    /// <param name="curLevel">Current performance level</param>
    /// <param name="interestPeriods">Current and (optionally) historical interest periods</param>
    /// <param name="referenceCurve">Interest rate curve for projecting forward rates</param>
    /// 
    /// <remarks>
    /// <para>Assumes that the performance level does not change and thus that the spread over the 
    /// floating rate index does not change. </para>
    /// </remarks>
    /// 
    /// <returns>Cashflow</returns>
    /// 
    private CashflowStream GenerateCashflowStream(Loan loan, CashflowStream cashflow,
      Dt from, string curLevel, IList<InterestPeriod> interestPeriods,
      DiscountCurve referenceCurve)
    {
      CashflowStream result = (cashflow ?? new CashflowStream(from));

      if (loan.IsFloating)
      {
        IList<RateReset> rateResets = InterestPeriodUtil.TransformToRateReset(from, loan.Effective, interestPeriods);
        CashflowStreamFactory.FillFloat(result, from, loan.Effective, loan.FirstCoupon, loan.Maturity, loan.Ccy,
          0, Dt.Empty, loan.PricingGrid[curLevel], loan.DayCount, loan.Frequency,
          loan.BDConvention, loan.Calendar,
          referenceCurve, rateResets, null, 1.0,
          loan.AmortizationSchedule, true, false, false, !loan.PeriodAdjustment,
          loan.PeriodAdjustment, false, loan.CycleRule == CycleRule.EOM, false);
      }
      else
        CashflowStreamFactory.FillFixed(result, from, loan.Effective, loan.FirstCoupon, loan.Maturity, loan.Ccy,
          0, Dt.Empty, loan.PricingGrid[curLevel], loan.DayCount, loan.Frequency,
          loan.BDConvention, loan.Calendar,
          null, 1.0, loan.AmortizationSchedule, true, false,
          false, !loan.PeriodAdjustment, loan.PeriodAdjustment,
          false, loan.CycleRule == CycleRule.EOM, false);

      // Done
      return result;
    }



    #endregion

    /// <summary>
    /// The distribution of the Loan's performance across time and performance level. 
    /// </summary>
    /// 
    /// <remarks>
    /// <para>Here, the first dimension is time and the second dimension is performance 
    /// level with prepayment at index 0, in order according to the Loan's performance levels, 
    /// and with default at the final index.</para></remarks>
    /// 
    /// <returns>Distribution array.</returns>
    /// 
    public double[,] ExpectedCashflows(bool allowPrepayment)
    {
      Cashflow cf = GenerateCashflow(null, Settle);
      // Validate
      if (!IsActive(Settle, Loan.Effective, Loan.Maturity) || (Settle == Loan.Maturity))
        return new double[0, cf.Count];

      return LoanModel.ExpectedCashflows(
        this.AsOf, this.Settle, Loan.GetScheduleParams(), Loan.DayCount, Loan.IsFloating,
        CurrentLevel, currentLevelIdx_, this.Loan.PerformanceLevels, Loan.PricingGrid,
        Loan.CommitmentFee, this.Usage, this.PerformanceLevelDistribution,
        this.DiscountCurve, this.ReferenceCurve, GetSurvivalCurve(), this.RecoveryCurve,
        this.VolatilityCurve, this.FullDrawOnDefault, allowPrepayment,
        this.PrepaymentCurve, this.RefinancingCost, AdjustedAmortization,
        this.InterestPeriods, CalibrationType, marketSpread_, this.UseDrawnNotional,
        FloorValues, CurrentFloatingCoupon());
    }
  }

  #endregion LoanPricer

  #region MMDRateLockPricer

  public partial class MmdRateLockPricer
  {

    // only risk calls this function.
    /// <summary>
    ///   Get cashflows for this product from the specified date
    /// </summary>
    /// <remarks>
    ///   <para>Returns the cashflows for this product from the specified date.</para>
    ///   <para>Derived pricers may implement this, otherwise a NotImplementedException is thrown.</para>
    /// </remarks>
    /// <param name="cashflow">Cashflow to fill. May be null to generate a new Cashflow.</param>
    /// <param name="from">Date to generate cashflows from</param>
    /// <returns>Cashflow from the specified date or null if not supported</returns>
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt from)
    {
      var paymentSchedule = GetPaymentSchedule(null, from);
      return PaymentScheduleUtils.FillCashflow(cashflow, paymentSchedule, from, Product.Notional, 0.0);
    }
  }

  #endregion MMDRateLockPricer

  #region NtdLinkedNotePricer
  public partial class NtdLinkedNotePricer
  {
    private partial class NtdDynamicPricer
    {
      private static Cashflow GenerateCashflowForFee(FTD ntd, Dt settle)
      {
        return PriceCalc.GenerateCashflowForFee(settle, ntd.Premium,
          ntd.Effective, ntd.FirstPrem, ntd.Maturity,
          ntd.Ccy, ntd.DayCount, ntd.Freq, ntd.BDConvention,
          ntd.Calendar, null, false, null, null);
      }

      // Generate simple cashflow stream
      private static Cashflow GenerateCashflowForProtection(FTD ntd, Dt settle)
      {
        return PriceCalc.GenerateCashflowForProtection(settle,
          ntd.Maturity, ntd.Ccy, null);
      }

      private double ProtectionPv(Dt settle, Cashflow cf,
        FTD ntd, BasketForNtdPricer basketPricer,
        DiscountCurve discountCurve, double defaultTiming,
        double accruedFractionOnDefault)
      {
        Dt maturity = ntd.Maturity;
        if (settle >= maturity)
          return 0.0;
        double pv = PriceCalc.Price(cf, settle, discountCurve,
          LossAt, SurvivalAt, null, false, true, false,
          defaultTiming, accruedFractionOnDefault, basketPricer.StepSize,
          basketPricer.StepUnit, cf.Count);
        pv += basketPricer.DefaultSettlementPv(settle,
          ntd.Maturity, discountCurve, ntd.First, 1, true, false);
        return pv;
      }

      private double FeePv(Dt settle, Cashflow cf, FTD ntd,
        BasketForNtdPricer basketPricer,
        DiscountCurve discountCurve, double defaultTiming,
        double accruedFractionOnDefault)
      {
        if (settle > ntd.Maturity)
          return 0.0;
        return PriceCalc.Price(cf, settle, discountCurve, LossAt,
          SurvivalAt, null, true, false, false, defaultTiming,
          accruedFractionOnDefault, basketPricer.StepSize,
          basketPricer.StepUnit, cf.Count);
      }

      private double CfPv(Dt dt)
      {
        double ppv = ProtectionPv(dt, protectionCf_, ntd_,
          basketPricer_, discount_, defaultTiming_,
          accruedFractionAtDefault_);
        double fpv = FeePv(dt, feeCf_, ntd_, basketPricer_,
          discount_, defaultTiming_, accruedFractionAtDefault_);
        return ppv + fpv;
      }

      #region Data
      private readonly Cashflow feeCf_;
      private readonly Cashflow protectionCf_;
      #endregion Data


    }
  }
  #endregion NtdLinkedNotePricer

  #region NoteCashflowPricer

  /// <summary>
  /// Price a <see cref="BaseEntity.Toolkit.Products.Note">Note</see> using the
  /// <see cref="BaseEntity.Toolkit.Models.CashflowModel">General Cashflow Model</see>.
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.Note" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="CashflowPricer" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.Note">Note Product</seealso>
  /// <seealso cref="BaseEntity.Toolkit.Models.CashflowModel">Cashflow pricing model</seealso>
  [Serializable]
  public class NoteCashflowPricer : CashflowPricer
  {
    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">SwapLeg to price</param>
    ///
    public
    NoteCashflowPricer(Note product)
      : base(product)
    { }


    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">SwapLeg to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurve">Survival Curve for pricing</param>
    /// <param name="stepSize">Step size for pricing grid</param>
    /// <param name="stepUnit">Units for step size</param>
    /// <param name="recoveryRate">Recovery rate</param>
    ///
    public
    NoteCashflowPricer(Note product,
                       Dt asOf,
                       Dt settle,
                       DiscountCurve discountCurve,
                       SurvivalCurve survivalCurve,
                       int stepSize,
                       TimeUnit stepUnit,
                       double recoveryRate)
      : base(product, asOf, settle,
             discountCurve, discountCurve, survivalCurve,
             stepSize, stepUnit, new RecoveryCurve(asOf, recoveryRate))
    { }


    /// <summary>
    ///   Constructor with counterparty risks
    /// </summary>
    ///
    /// <param name="product">Note to price</param>
    /// <param name="asOf">As-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurve">Survival Curve of underlying credit</param>
    /// <param name="counterpartySurvivalCurve">Survival Curve of counterparty</param>
    /// <param name="correlation">Correlation between credit and conterparty defaults</param>
    /// <param name="stepSize">Step size for pricing grid</param>
    /// <param name="stepUnit">Units for step size</param>
    /// <param name="recoveryRate">Recovery rate</param>
    ///
    public
    NoteCashflowPricer(Note product,
                       Dt asOf,
                       Dt settle,
                       DiscountCurve discountCurve,
                       SurvivalCurve survivalCurve,
                       SurvivalCurve counterpartySurvivalCurve,
                       double correlation,
                       int stepSize,
                       TimeUnit stepUnit,
                       double recoveryRate)
      : base(product, asOf, settle,
             discountCurve, discountCurve,
             survivalCurve, counterpartySurvivalCurve, correlation,
             stepSize, stepUnit, new RecoveryCurve(asOf, recoveryRate))
    { }

    #endregion // Constructorts

    #region Methods

    /// <summary>
    ///   Generate cashflow from product
    /// </summary>
    ///
    /// <param name="cashflow">Cashflow to fill</param>
    /// <param name="asOf">As of date to fill from</param>
    ///
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt asOf)
    {
      Note note = Note;

      CycleRule cycleRule = (note.BDConvention == BDConvention.FRN)
                              ? CycleRule.FRN
                              : CycleRule.None;

      const CashflowFlag flags = CashflowFlag.None;

      var schedParams = new ScheduleParams(note.Effective, note.FirstCoupon, Dt.Empty, note.Maturity, note.Freq, note.BDConvention,
                                           note.Calendar, cycleRule, flags);

      Dt from = asOf;

      Currency ccy = note.Ccy;
      DayCount dayCount = note.DayCount;
      const double fee = 0.0;
      Dt feeSettle = Dt.Empty;
      double coupon = note.Coupon;
      const double principal = 1.0;
      IList<CouponPeriod> couponSchedule = note.CouponSchedule;
      IList<Amortization> amortSchedule = note.AmortizationSchedule;

      Currency defaultCcy = note.Ccy;
      double defaultAmount = RecoveryRate;
      Dt defaultDate = Dt.Empty;

      if (cashflow == null)
        cashflow = new Cashflow(asOf);
      CashflowFactory.FillFixed(cashflow, from, schedParams, ccy, dayCount,
                                coupon, couponSchedule,
                                principal, amortSchedule,
                                defaultAmount, defaultCcy, defaultDate,
                                fee, feeSettle);
      return cashflow;
    }

    #endregion // Methods

    #region Properties

    /// <summary>
    ///   Product
    /// </summary>
    public Note Note
    {
      get { return (Note)Product; }
    }

    #endregion // Properties

  } // class NoteCashflowPricer


  #endregion NoteCashflowPricer

  #region QuantoCDSPricer

  public partial class QuantoCDSPricer
  {
    private double CfProductPv()
    {
      double[] X0 = new double[] { spotFx_, lambda0_ };
      QuantoCDS qcds = (QuantoCDS)Product;
      Cashflow cf = this.Cashflow;
      double pv = QuantoCredit.PremiumPv(AsOf, cf,
        discountCurve_,
        discountCurveR_,
        18, X0, qcds.RecoveryFx, recovery_,
        quantoFactor_, fxVolatility_,
        kappa_, theta_, sigma_);
      pv -= QuantoCredit.ProtectionPv(AsOf, cf,
        discountCurve_,
        discountCurveR_,
        18, X0, qcds.RecoveryFx, recovery_,
        quantoFactor_, fxVolatility_,
        kappa_, theta_, sigma_);
      return pv * Notional;
    }

    private double CfFeePv()
    {
      double[] X0 = new double[] { spotFx_, lambda0_ };

      QuantoCDS qcds = (QuantoCDS)Product;
      Cashflow cf = this.Cashflow;
      double pv = QuantoCredit.PremiumPv(AsOf, cf,
        discountCurve_,
        discountCurveR_,
        18, X0, qcds.RecoveryFx, recovery_,
        quantoFactor_, fxVolatility_,
        kappa_, theta_, sigma_);
      return pv * Notional;

    }

    private double CfProtectionPv()
    {
      double[] X0 = new double[] { spotFx_, lambda0_ };

      QuantoCDS qcds = (QuantoCDS)Product;
      Cashflow cf = this.Cashflow;
      double pv = QuantoCredit.ProtectionPv(AsOf, cf,
        discountCurve_,
        discountCurveR_,
        18, X0, qcds.RecoveryFx, recovery_,
        quantoFactor_, fxVolatility_,
        kappa_, theta_, sigma_);
      return pv * Notional;
    }

    /// <summary>
    /// Generate cashflow as of specified date
    /// </summary>
    /// <param name="cashflow"></param>
    /// <param name="asOf"></param>
    /// <returns>Cashflow</returns>
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt asOf)
    {
      return QuantoCDSPricer.GenerateCashflow(cashflow, this.QuantoCDS, asOf, this.RecoveryRate);
    }

    /// <summary>
    ///   Helper function to generate cashflows for quanto CDS
    /// </summary>
    public static Cashflow GenerateCashflow(Cashflow cashflow, QuantoCDS qcds, Dt asOf, double recovery)
    {
      if (cashflow == null)
        cashflow = new Cashflow(asOf);


      const CycleRule cycleRule = CycleRule.None;

      CashflowFlag flags = CashflowFlag.IncludeDefaultDate;
      if (qcds.AccruedOnDefault)
        flags |= CashflowFlag.AccruedPaidOnDefault;

      var schedParams = new ScheduleParams(qcds.Effective, qcds.FirstPrem, Dt.Empty, qcds.Maturity, qcds.Freq,
        qcds.BDConvention, qcds.Calendar, cycleRule, flags);

      const double fee = 0.0;
      Dt feeSettle = Dt.Empty;
      double premium = qcds.Premium;
      const double principal = 0.0;

      Currency defaultCcy = qcds.RecoveryCcy;
      double defaultAmount = recovery;
      Dt defaultDate = Dt.Empty;

      CashflowFactory.FillFixed(cashflow, asOf, schedParams, qcds.Ccy, qcds.DayCount,
        premium, null,
        principal, null,
        defaultAmount, defaultCcy, defaultDate,
        fee, feeSettle);

      return cashflow;
    }

    /// <summary>
    ///   Cashflow from product
    /// </summary>
    public Cashflow Cashflow
    {
      get { return GenerateCashflow(cashflow_, AsOf); }
    }

    private Cashflow cashflow_ = null;             // Cashflow matching product

  }

  #endregion QuantoCDSPricer

  #region RepoPricer

  public partial class RepoLoanPricer
  {
    /// <summary>
    /// Generate repoLoan cashflows
    /// </summary>
    /// <param name="cf">A cashflow object</param>
    /// <param name="from">Cashflow start date</param>
    /// <returns>Cashflow associated to the repoLoan</returns>
    /// <remarks>If there are no payments between from and to this method will return the payment/payments on the payment date immediately following from</remarks>
    public override Cashflow GenerateCashflow(Cashflow cf, Dt from)
    {
      var paymentSchedule = GetPaymentSchedule(null, from);
      cf = PaymentScheduleUtils.FillCashflow(cf, paymentSchedule, from, RepoLoan.Notional, 0.0);
      cf.DayCount = RepoLoan.DayCount;
      //Need to tackle the item on repo end date
      var repoEndPaymentAmt = paymentSchedule.GetPaymentsOnDate(RepoLoan.Maturity).Sum(p => p.Amount);
      for (int idx = 0; idx < cf.Count; ++idx)
      {
        var payDt = cf.GetDt(idx);
        if (payDt != RepoLoan.Maturity)
          continue;

        cf.Set(idx, repoEndPaymentAmt, 0.0, 0.0);
      }
      return cf;
    }

  }

  #endregion RepoPricer

  #region RecoverySwapPricer

  public partial class RecoverySwapPricer
  {
    // only one XL function qRecoverySwapCashflowTable calls this. 
    /// <summary>
    ///   Generate cashflow for CDS
    /// </summary>
    ///
    /// <param name="cashflow">Cashflow to fill</param>
    /// <param name="asOf">As of date to fill from</param>
    ///
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt asOf)
    {
      if (cashflow == null)
        cashflow = new Cashflow(asOf);
      if (SurvivalCurve.DefaultDate.IsValid() && (asOf >= SurvivalCurve.DefaultDate || SurvivalCurve.Defaulted == Defaulted.WillDefault))
      {
        // Setup Cashflow
        cashflow.Clear();
        cashflow.AsOf = asOf;
        cashflow.Effective = asOf;
        cashflow.Currency = RecoverySwap.Ccy;
        cashflow.DefaultCurrency = RecoverySwap.Ccy;

        Dt defaultDate, dfltSettle;
        double recoverRate;

        SurvivalCurve.GetDefaultDates(null, out defaultDate, out dfltSettle, out recoverRate);
        if (!dfltSettle.IsValid())
        {
          // If we do not have the default settlement date specified yet, estimate it as the next business day ...
          dfltSettle = Dt.AddDays(Settle, 1, RecoverySwap.Calendar);
        }

        double defaultAmount = RecoveryRate - RecoverySwap.RecoveryRate;
        cashflow.DefaultPayment = new Cashflow.ScheduleInfo();
        cashflow.DefaultPayment.Date = dfltSettle;
        cashflow.DefaultPayment.Loss = defaultAmount;
      }
      else
      {
        // Follow this logic if we have not defaulted yet:
        RecoverySwapPricer.GenerateCashflow(cashflow, RecoverySwap, asOf, RecoveryRate);
      }

      return cashflow;
    }

    /// <summary>
    ///   Helper function to generate cashflows for CDS
    /// </summary>
    public static void
    GenerateCashflow(Cashflow cf, RecoverySwap swap, Dt asOf, double recoveryRate)
    {
      // If the fee settle is not set, use the asOf date
      Dt feeSettle = swap.FeeSettle;
      if (feeSettle.Month <= 0)
        feeSettle = asOf;

      // Setup Cashflow
      cf.Clear();
      cf.AsOf = asOf;
      cf.Effective = asOf;
      cf.Currency = swap.Ccy;
      cf.DefaultCurrency = swap.Ccy;

      // what you receive in default
      double defaultAmount = recoveryRate - swap.RecoveryRate;
      if (swap.Fee != 0.0)
        cf.Add(feeSettle, feeSettle, feeSettle, 0.0, swap.Fee, 0.0, 0.0, defaultAmount);
      cf.Add(swap.Maturity, swap.Maturity, swap.Maturity, 0.0, 0.0, 0.0, 0.0, defaultAmount);
    }
  }

  #endregion RecoverySwapPricer

  #region SyntheticCDOPricer
  /// <summary>
  /// The part of the class SyntheticCDOPricer is for the
  /// Legacy cash flow. 
  /// </summary>
  public partial class SyntheticCDOPricer
  {
    /// <summary>
    ///   Get cashflows for this product from the specified date
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Returns the cashflows for this product from the specified date.</para>
    ///   <para>Can return null if not supported.</para>
    /// </remarks>
    /// 
    /// <param name="cashflow">Cashflow to fill. May be null to generate a new Cashflow.</param>
    /// <param name="from">Date to generate cashflows from</param>
    ///
    /// <returns>Cashflow from the specified date or null if not supported</returns>
    ///
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt from)
    {
      SyntheticCDO cdo = CDO;

      // Find the termination date
      Dt maturity = cdo.Maturity;

      // Generate cashflow
      if (cashflow == null) cashflow = new Cashflow(from);
      // if funded call FillFloat; if Libor flag turned on use Libor curve as reference curve 
      // so you don't need to add a reference curve to the cdo pricer.

      double bulletFundedPayment = 0.0;
      if (cdo.CdoType == CdoType.FundedFixed || cdo.CdoType == CdoType.FundedFloating)
      {
        bulletFundedPayment = 1.0;
      }

      if (cdo.CdoType == CdoType.FundedFloating || cdo.CdoType == CdoType.IoFundedFloating)
      {
        // Floating

        DiscountCurve referenceCurve = referenceCurve_ ?? discountCurve_;

        CycleRule cycleRule = cdo.CycleRule;
        const CashflowFlag flags = CashflowFlag.IncludeMaturityAccrual;

        var schedParams = new ScheduleParams(cdo.Effective, cdo.FirstPrem, cdo.LastPrem, cdo.Maturity, cdo.Freq,
          cdo.BDConvention, cdo.Calendar, cycleRule, flags);

        double fee = cdo.Fee;
        Dt feeSettle = cdo.FeeSettle;
        double principal = bulletFundedPayment;
        double premium = cdo.Premium;

        Currency defaultCcy = cdo.Ccy;
        const double defaultAmount = -1.0;
        Dt defaultDate = Dt.Empty;

        CashflowFactory.FillFloat(cashflow, from, schedParams, cdo.Ccy, cdo.DayCount,
          premium, null,
          principal, null,
          referenceCurve, this.RateResets,
          defaultAmount, defaultCcy, defaultDate,
          fee, feeSettle);
      }
      else
      {
        CycleRule cycleRule = cdo.CycleRule;
        const CashflowFlag flags = CashflowFlag.IncludeMaturityAccrual;

        var schedParams = new ScheduleParams(cdo.Effective, cdo.FirstPrem, cdo.LastPrem, cdo.Maturity, cdo.Freq,
          cdo.BDConvention, cdo.Calendar, cycleRule, flags);

        double fee = cdo.Fee;
        Dt feeSettle = cdo.FeeSettle;
        double principal = bulletFundedPayment;
        double premium = cdo.Premium;

        Currency defaultCcy = cdo.Ccy;
        const double defaultAmount = -1.0;
        Dt defaultDate = Dt.Empty;

        CashflowFactory.FillFixed(cashflow, from, schedParams, cdo.Ccy, cdo.DayCount,
          premium, null,
          principal, null,
          defaultAmount, defaultCcy, defaultDate,
          fee, feeSettle);

        // Handle PO maturity payment
        if (cdo.CdoType == CdoType.Po)
        {
          cashflow.Add(maturity, maturity, maturity, 0.0, 0.0, 1.0, 0.0, 0.0, -1.0);
        }
      }
      if (CDO.Bullet != null)
      {
        cashflow.AddMaturityPayment(CDO.Bullet.CouponRate, 0, 0, 0);
      }
      return cashflow;
    }

    public Cashflow GenerateCashflowForFee(Dt t, double coupon)
    {
      SyntheticCDO cdo = CDO;
      DiscountCurve referenceCurve = ReferenceCurve ?? DiscountCurve;
      return PriceCalc.GenerateCashflowForFee(t, coupon,
        cdo.Effective, cdo.FirstPrem, cdo.Maturity,
        cdo.Ccy, cdo.DayCount, cdo.Freq, cdo.BDConvention, cdo.Calendar,
        this.CounterpartySurvivalCurve,
        cdo.CdoType == CdoType.FundedFloating || cdo.CdoType == CdoType.IoFundedFloating,
        referenceCurve, this.RateResets);
    }

    private Cashflow GenerateCashflowForProtection(Dt settle)
    {
      SyntheticCDO cdo = CDO;
      return PriceCalc.GenerateCashflowForProtection(
        settle, cdo.Maturity, cdo.Ccy, this.CounterpartySurvivalCurve);
    }


    private double CfProtectionPv(Dt forwardDate)
    {
      Dt t = forwardDate;
      SyntheticCDO cdo = CDO;
      BasketPricer basket = Basket; // this also update the public state

      Dt maturity = cdo.Maturity;

      // Funded notes(including Po's) don't have a protection leg (the bullet payment to the protection
      // seller at maturity is added to the fee leg). Io's also have no protectionleg but there's no bullet
      // payment at maturity 
      if (cdo.CdoType == CdoType.FundedFixed
          || cdo.CdoType == CdoType.FundedFloating
          || cdo.CdoType == CdoType.Po
          || cdo.CdoType == CdoType.IoFundedFloating
          || cdo.CdoType == CdoType.IoFundedFixed)
      {
        return 0.0;
      }

      // include unsettled default payment
      double pv = Basket.DefaultSettlementPv(t, maturity,
        discountCurve_, cdo.Attachment, cdo.Detachment, true, false);

      if (Dt.Cmp(GetProtectionStart(), maturity) <= 0)
      {
        // Note: this is a bit INCONSISTENCY with the CDS pricer
        // because the later rolls if the maturity date is on sunday.
        // This may cause several hundreds bucks differences in FeePv
        Cashflow cashflow = cashflow_ ?? GenerateCashflowForProtection(t);
        pv += price(cashflow, t, discountCurve_,
          basket, cdo.Attachment, cdo.Detachment,
          NeedAmortization(cdo, basket), this.CounterpartySurvivalCurve,
          false, true, false,
          this.DefaultTiming, this.AccruedFractionOnDefault,
          basket.StepSize, basket.StepUnit, cashflow.Count);
      }
      return pv * this.Notional;
    }


    private double CfUnsettledDefaultAccrualAdjustment(Dt forwardDate, double premium)
    {
      if (!paysAccrualAfterDefault_) { return 0; }

      Dt settle = forwardDate;
      SyntheticCDO cdo = CDO;
      if (cdo.CdoType == CdoType.Po || Dt.Cmp(settle, cdo.Maturity) > 0)
      {
        return 0.0;
      }

      // If the user supplied a cash flow, we simply use it.
      // Otherwise, we generate a cahs flow, which has taken care of using the
      // correct floating coupons when necessary.
      Cashflow cf = cashflow_ ?? GenerateCashflowForFee(settle, premium);

      // Find first cash flow on or after settlement (depending on includeSettle flag)
      int N = cf.Count;
      int firstIdx;
      for (firstIdx = 0; firstIdx < N; firstIdx++)
      {
        if (Dt.Cmp(cf.GetDt(firstIdx), settle) > 0)
          break;
      }
      if (firstIdx >= N)
        return 0.0; // This may happen when the forward date is after maturity, for example.

      //TODO: revisit and consider using start and end dates.
      Dt accrualStart = (firstIdx > 0) ? cf.GetStartDt(firstIdx - 1) : cf.Effective;
      if (Dt.Cmp(accrualStart, settle) > 0)
        return 0.0; // this may occur for forward starting CDO

      Dt accrualEnd = cf.GetEndDt(firstIdx), payDate = cf.GetDt(firstIdx);
      double paymentPeriod = Dt.Fraction(accrualStart, accrualEnd, cdo.DayCount);
      if (paymentPeriod < 1E-10)
        return 0.0; // this may happen if maturity on settle, for example

      return Basket.UnsettledDefaultAccrualAdjustment(
               payDate, accrualStart, accrualEnd, cdo.DayCount,
               CDO.Attachment, CDO.Detachment, DiscountCurve)
             * premium * Notional / DiscountCurve.DiscountFactor(forwardDate);
    }

    private double CfFeePvNoAccruedAdjustment(Dt forwardDate, double premium)
    {
      Dt t = forwardDate;
      SyntheticCDO cdo = CDO;
      if (Dt.Cmp(t, cdo.Maturity) > 0)
        return 0;

      double pv = 0.0;
      double bulletFundedPayment = 0.0;
      BasketPricer basket = Basket; // this alsoe update the public state

      if (cdo.CdoType == CdoType.FundedFixed || cdo.CdoType == CdoType.FundedFloating
          || cdo.CdoType == CdoType.Po)
      {
        // Only the maturity distributions count
        double survivalPrincipal = cdo.Detachment - cdo.Attachment;
        survivalPrincipal -= basket.AccumulatedLoss(cdo.Maturity, cdo.Attachment, cdo.Detachment);
#       if EXCLUDE_AMORTIZE
        if (cdo.AmortizePremium)
          survivalPrincipal -= basket.AmortizedAmount(cdo.Maturity, cdo.Attachment, cdo.Detachment);
#       endif
        survivalPrincipal *= DiscountCurve.DiscountFactor(t, cdo.Maturity);
        bulletFundedPayment = survivalPrincipal * TotalPrincipal;

        // include the unsettled recoveries
        bulletFundedPayment += Basket.DefaultSettlementPv(t, cdo.Maturity,
                                 DiscountCurve, cdo.Attachment, cdo.Detachment, false, true) * Notional;
      }

      if (cdo.CdoType == CdoType.Po)
      {
        // no running fee for zero coupon (PO tranche). Only bullet payment (remaining notional) at maturity
        return bulletFundedPayment;
      }

      double trancheWidth = cdo.Detachment - cdo.Attachment;
      if (cdo.FeeGuaranteed)
      {
        var cycleRule = cdo.CycleRule;

        CashflowFlag flags = CashflowFlag.IncludeMaturityAccrual;
        if (cdo.AccruedOnDefault)
          flags |= CashflowFlag.AccruedPaidOnDefault;

        var schedParams = new ScheduleParams(cdo.Effective, cdo.FirstPrem, cdo.LastPrem, cdo.Maturity, cdo.Freq,
          cdo.BDConvention, cdo.Calendar, cycleRule, flags);

        const double fee = 0.0;
        Dt feeSettle = Dt.Empty;
        const double principal = 0.0;

        Currency defaultCcy = cdo.Ccy;
        const double defaultAmount = 0.0;
        Dt defaultDate = Dt.Empty;

        // Use hazard rate constructor with 0 hazard to get riskfree survival curve.
        SurvivalCurve riskfree = new SurvivalCurve(basket.AsOf, 0.0);
        Cashflow cashflow = new Cashflow();
        CashflowFactory.FillFixed(cashflow, basket.AsOf, schedParams, cdo.Ccy, cdo.DayCount,
          premium, null,
          principal, null,
          defaultAmount, defaultCcy, defaultDate,
          fee, feeSettle);
        pv = CashflowModel.FeePv(cashflow, t, t, DiscountCurve,
          riskfree, null, 0.0, false, false, false,
          basket.StepSize, basket.StepUnit);
        pv *= trancheWidth;
        // add bullet payment at maturity if funded note
        return pv * this.TotalPrincipal + bulletFundedPayment;
      }
      else
      {
        // check if Libor flag is turned on
        Cashflow cashflow = cashflow_ ?? GenerateCashflowForFee(t, premium);
        int stepSize = 0; TimeUnit stepUnit = TimeUnit.None;
        pv = price(cashflow, t, discountCurve_,
          basket, cdo.Attachment, cdo.Detachment,
          NeedAmortization(cdo, basket), this.CounterpartySurvivalCurve,
          true, false, false,
          this.DefaultTiming, this.AccruedFractionOnDefault,
          stepSize, stepUnit, cashflow.Count);
        pv *= this.Notional;
        // add bullet payment at maturity if funded note
        return pv + bulletFundedPayment;
      }
    }


    private int CfAccrualDays(Dt settle)
    {
      // Leverages code from Accrued(date, premium) method, simply calls FractionDays(...) instead of Fraction(...)
      SyntheticCDO cdo = CDO;
      if (cdo.CdoType == CdoType.Po || Dt.Cmp(settle, cdo.Maturity) > 0)
      {
        return 0;
      }
      else
      {
        // If the user suppiled a cashflow, we simply use it.
        // Otherwise, we generate a cahsflow, which has taken care of using the
        // correct floating coupons when neccessary.
        Cashflow cf = cashflow_ ?? GenerateCashflowForFee(settle, 1.0);

        // Find first cashflow on or after settlement (depending on includeSettle flag)
        int N = cf.Count;
        int firstIdx;
        for (firstIdx = 0; firstIdx < N; firstIdx++)
        {
          if (Dt.Cmp(cf.GetDt(firstIdx), settle) > 0)
            break;
        }
        if (firstIdx >= N)
          return 0;  // This may happen when the forward date is after maturity, for example.

        //TODO: revisit and consider using start and end dates.
        Dt accrualStart = (firstIdx > 0) ? cf.GetDt(firstIdx - 1) : cf.Effective;
        if (Dt.Cmp(accrualStart, settle) > 0)
          return 0; // this may occur for forward starting CDO

        // Dt.Diff(...) is the numerator for *most* daycounts.  OneOne and Monthly might be off here; it's too complex to worry about now.
        return Dt.Diff(accrualStart, settle);
      }
      // end of the AccrualDays
    }


    private double CfAccrued(Dt forwardDate, double premium)
    {
      Dt settle = forwardDate;
      SyntheticCDO cdo = CDO;
      if (cdo.CdoType == CdoType.Po || Dt.Cmp(settle, cdo.Maturity) > 0)
      {
        return 0.0;
      }
      else
      {
        // If the user suppiled a cashflow, we simply use it.
        // Otherwise, we generate a cahsflow, which has taken care of using the
        // correct floating coupons when neccessary.
        Cashflow cf = cashflow_ ?? GenerateCashflowForFee(settle, premium);

        // Find first cashflow on or after settlement (depending on includeSettle flag)
        int N = cf.Count;
        int firstIdx;
        for (firstIdx = 0; firstIdx < N; firstIdx++)
        {
          if (Dt.Cmp(cf.GetDt(firstIdx), settle) > 0)
            break;
        }
        if (firstIdx >= N)
          return 0.0;  // This may happen when the forward date is after maturity, for example.

        //TODO: revisit and consider using start and end dates.
        Dt accrualStart = (firstIdx > 0) ? cf.GetDt(firstIdx - 1) : cf.Effective;
        if (Dt.Cmp(accrualStart, settle) > 0)
          return 0.0; // this may occur for forward starting CDO

        Dt nextDate = cf.GetDt(firstIdx);
        double paymentPeriod = Dt.Fraction(accrualStart, nextDate, cdo.DayCount);
        if (paymentPeriod < 1E-10)
          return 0.0; // this may happen if maturity on settle, for example

        double accrued = (UseOriginalNotionalForFee ? Dt.Fraction(accrualStart,
                           settle, cdo.DayCount) : Basket.AccrualFraction(accrualStart,
                           settle, cdo.DayCount, CDO.Attachment, CDO.Detachment))
                         / paymentPeriod * cf.GetAccrued(firstIdx);
        return accrued;
      }
      // end of the Accrued
    }


    private double CfAccruedPerUnitNotional(Dt forwardDate, double premium)
    {
      Dt settle = forwardDate;
      SyntheticCDO cdo = CDO;
      if (cdo.CdoType == CdoType.Po || Dt.Cmp(settle, cdo.Maturity) > 0)
      {
        return 0.0;
      }
      else
      {
        if (IgnoreAccruedSetting)
        {
          // Oldway and wrong way
          Schedule sched = new Schedule(settle, cdo.Effective, cdo.FirstPrem, cdo.Maturity, cdo.Maturity,
            cdo.Freq, cdo.BDConvention, cdo.Calendar, false, true);

          // Calculate accrued to settlement.
          Dt start = sched.GetPeriodStart(0);
          Dt end = sched.GetPeriodEnd(0);
          // Note schedule currently includes last date in schedule period. This may get changed in
          // the future so to handle this we test if we are on a coupon date. RTD. Jan05
          if (Dt.Cmp(settle, start) == 0 || Dt.Cmp(settle, end) == 0)
            return 0.0;
          else
            return (Dt.Fraction(start, settle, cdo.DayCount) * premium);
        }
        else
        {
          // new way and right way

          // If the user suppiled a cashflow, we simply use it.
          // Otherwise, we generate a cahsflow, which has taken care of using the
          // correct floating coupons when neccessary.
          Cashflow cf = cashflow_ ?? GenerateCashflowForFee(settle, premium);

          // Find first cashflow on or after settlement (depending on includeSettle flag)
          int N = cf.Count;
          int firstIdx;
          for (firstIdx = 0; firstIdx < N; firstIdx++)
          {
            if (Dt.Cmp(cf.GetDt(firstIdx), settle) > 0)
              break;
          }
          if (firstIdx >= N)
            return 0.0;  // This may happen when the forward date is after maturity, for example.

          //TODO: revisit and consider using start and end dates.
          Dt accrualStart = (firstIdx > 0) ? cf.GetDt(firstIdx - 1) : cf.Effective;
          if (Dt.Cmp(accrualStart, settle) > 0)
            return 0.0; // this may occur for forward starting CDO

          Dt nextDate = cf.GetDt(firstIdx);
          double paymentPeriod = Dt.Fraction(accrualStart, nextDate, cdo.DayCount);
          if (paymentPeriod < 1E-10)
            return 0.0; // this may happen if maturity on settle, for example

          double accrued = Dt.Fraction(accrualStart, settle, cdo.DayCount)
                           / paymentPeriod * cf.GetAccrued(firstIdx);
          return accrued;
        }
      }
      // end of the Accrued
    }


    private double CfCarry()
    {
      SyntheticCDO cdo = CDO;

      if (cdo.CdoType == CdoType.FundedFloating || cdo.CdoType == CdoType.IoFundedFloating)
      {
        Dt settle = GetProtectionStart();
        if (settle >= cdo.Maturity)
          return 0.0;

        //- If the user suppiled a cashflow, we simply use it.
        //- Otherwise, we generate a cahsflow, which has taken care of using the
        //- correct floating coupons when neccessary.
        Cashflow cf = cashflow_ ?? GenerateCashflowForFee(settle, cdo.Premium);

        //- Find the latest cashflow on or before settlement
        int N = cf.Count;
        int firstIdx;
        for (firstIdx = 0; firstIdx < N; firstIdx++)
        {
          if (Dt.Cmp(cf.GetDt(firstIdx), settle) > 0)
            break;
        }
        //TODO: revisit and consider using start and end dates.
        Dt accrualStart = (firstIdx > 0) ? cf.GetDt(firstIdx - 1) : cf.Effective;
        if (Dt.Cmp(accrualStart, settle) > 0)
          return 0.0; //- can this occur?

        Dt nextDate = cf.GetDt(firstIdx);
        double paymentPeriod = Dt.Fraction(accrualStart, nextDate, cdo.DayCount);
        if (paymentPeriod < 1E-10)
          return 0.0; // can this happen?

        //- find the effective coupon on the settle date
        double coupon = cf.GetAccrued(firstIdx) / paymentPeriod;
        return coupon / 360 * CurrentNotional;
      }

      return cdo.Premium / 360 * CurrentNotional;
    }


    private void CfProtectionPvDerivatives(Dt forwardDate, double[] retVal)
    {
      Dt t = forwardDate;
      SyntheticCDO cdo = CDO;
      BasketPricer basket = Basket; // this also update the public state
      Dt maturity = cdo.Maturity;
      for (int i = 0; i < retVal.Length; i++)
        retVal[i] = 0.0;

      // Funded notes(including Po's) don't have a protection leg (the bullet payment to the protection
      // seller at maturity is added to the fee leg). Io's also have no protectionleg but theere's no bullet
      // payment at maturity 
      if (cdo.CdoType == CdoType.FundedFixed || cdo.CdoType == CdoType.FundedFloating
          || cdo.CdoType == CdoType.Po || cdo.CdoType == CdoType.IoFundedFloating || cdo.CdoType == CdoType.IoFundedFixed
          || Dt.Cmp(GetProtectionStart(), maturity) > 0)
      {
        return;
      }
      Cashflow cashflow = cashflow_ ?? GenerateCashflowForProtection(t);
      greeks(cashflow, t, discountCurve_, basket, cdo.Attachment, cdo.Detachment, NeedAmortization(cdo, basket), this.CounterpartySurvivalCurve,
        false, true, false, this.DefaultTiming, this.AccruedFractionOnDefault, basket.StepSize, basket.StepUnit, cashflow.Count, retVal);

    }


    private void CfFeePvDerivatives(Dt forwardDate, double premium, double[] retVal)
    {

      for (int i = 0; i < retVal.Length; i++)
        retVal[i] = 0.0;
      //double[] survPrincipalDers = new double[retVal.Length];
      Dt t = forwardDate;
      SyntheticCDO cdo = CDO;
      if (Dt.Cmp(t, cdo.Maturity) > 0)
        return;
      BasketPricer basket = Basket; // this also updates the public state
      if (cdo.CdoType == CdoType.FundedFixed || cdo.CdoType == CdoType.FundedFloating
          || cdo.CdoType == CdoType.Po)
      {
        basket.AccumulatedLossDerivatives(cdo.Maturity, cdo.Attachment, cdo.Detachment, retVal);
        for (int k = 0; k < retVal.Length; k++)
          retVal[k] = -retVal[k];
#      if EXCLUDE_AMORTIZE
                double[] amort = new double[retVal.Length];
                if (cdo.AmortizePremium)
                basket.AmortizedAmountDerivatives(cdo.Maturity, cdo.Attachment, cdo.Detachment, amort);
                for (int k = 0; k < retVal.Length; k++)
                    retVal[k] -= amort[k];
#       endif
        double mult = DiscountCurve.DiscountFactor(t, cdo.Maturity) / (this.CDO.Detachment - this.CDO.Attachment);
        for (int k = 0; k < retVal.Length; k++)
          retVal[k] *= mult;
      }
      if (cdo.CdoType == CdoType.Po || cdo.FeeGuaranteed)
        return;
      double trancheWidth = cdo.Detachment - cdo.Attachment;
      double[] temp = new double[retVal.Length];
      Cashflow cashflow = cashflow_ ?? GenerateCashflowForFee(t, premium);
      int stepSize = 0; TimeUnit stepUnit = TimeUnit.None;
      greeks(cashflow, t, discountCurve_, basket, cdo.Attachment, cdo.Detachment,
        NeedAmortization(cdo, basket), this.CounterpartySurvivalCurve,
        true, false, false, this.DefaultTiming, this.AccruedFractionOnDefault,
        stepSize, stepUnit, cashflow.Count, temp);
      for (int k = 0; k < retVal.Length; k++)
        retVal[k] += temp[k];//*this.Notional;
    }


    private double price(
      Cashflow cashflow, Dt settle, DiscountCurve discountCurve,
      BasketPricer basket, double attachment, double detachment, bool withAmortize,
      SurvivalCurve counterpartyCurve,
      bool includeFees, bool includeProtection, bool includeSettle,
      double defaultTiming, double accruedFractionOnDefault,
      int step, TimeUnit stepUnit, int stopIdx)
    {
      // If settle is after maturity, simply return 0
      if (Dt.Cmp(settle, basket.Maturity) > 0)
        return 0;

      double trancheWidth = detachment - attachment;
      if (trancheWidth < 1E-9) return 0.0;

      return PriceCalc.Price(cashflow, settle, discountCurve,
        delegate (Dt date)
        {
          double loss = basket.AccumulatedLoss(date,
                          attachment, detachment) / trancheWidth;
          return Math.Min(1.0, loss);
        },
        delegate (Dt date)
        {
          double loss = basket.AccumulatedLoss(date, attachment, detachment);
          if (withAmortize)
            loss += basket.AmortizedAmount(date, attachment, detachment);
          // For a CDOSquare pricer, we nned to adjust the maximum loss level
          double maxLoss = 1.0;
          if (basket is CDOSquaredBasketPricer)
          {
            double totalPrincipal = ((CDOSquaredBasketPricer)basket).TotalPrincipal;
            maxLoss = ((CDOSquaredBasketPricer)basket).CurrentTotalPrincipal(date) / totalPrincipal;
          }
          return Math.Max(0.0, maxLoss - loss / trancheWidth);
        },
        counterpartyCurve, includeFees, includeProtection, includeSettle,
        defaultTiming, accruedFractionOnDefault, step, stepUnit, stopIdx);
    }

    /// <summary>
    ///   Calculate price based on a cashflow stream
    /// </summary>
    private void greeks(
      Cashflow cashflow, Dt settle, DiscountCurve discountCurve,
      BasketPricer basket, double attachment, double detachment, bool withAmortize,
      SurvivalCurve counterpartyCurve,
      bool includeFees, bool includeProtection, bool includeSettle,
      double defaultTiming, double accruedFractionOnDefault,
      int step, TimeUnit stepUnit, int stopIdx, double[] retVal)
    {
      for (int i = 0; i < retVal.Length; i++)
        retVal[i] = 0.0;
      // If settle is after maturity, simply return 0
      if (Dt.Cmp(settle, basket.Maturity) > 0)
        return;
      double trancheWidth = detachment - attachment;
      if (trancheWidth < 1E-9) return;
      PriceCalc.Greeks(cashflow, settle, discountCurve,
        delegate (Dt date, double[] res)
        {
          basket.AccumulatedLossDerivatives(date, attachment, detachment, res);
          for (int k = 0; k < res.Length; k++)
            res[k] /= trancheWidth;
        },
        delegate (Dt date, double[] res)
        {
          double[] temp = new double[res.Length];
          basket.AccumulatedLossDerivatives(date, attachment, detachment, res);
          if (withAmortize)
          {
            basket.AmortizedAmountDerivatives(date, attachment, detachment, temp);
          }
          for (int k = 0; k < res.Length; k++)
            res[k] = -(res[k] + temp[k]) / trancheWidth;
        }, counterpartyCurve, includeFees, includeProtection, includeSettle,
        defaultTiming, accruedFractionOnDefault, step, stepUnit, stopIdx, retVal);
    }


    private Cashflow cashflow_ = null;
    private CashflowStream cashflowStream_ = null;
  }
  #endregion SyntheticCDOPricer

  #region StockRatchetOptionPricer

  public partial class StockRatchetOptionPricer
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="from"></param>
    /// <param name="resetCutoff"> </param>
    /// <returns></returns>
    public Cashflow GenerateStrikeResettingPayoutCashflow(Dt from, Dt resetCutoff)
    {
      var strikeFixingDates = StrikeResets.OrderBy(sf => sf.Date).Select(sf => sf.Date).ToArray();
      var strikeFixingStrikes = StrikeResets.OrderBy(sf => sf.Date).Select(sf => sf.Rate).ToArray();
      var cf = new Cashflow();

      for (int idx = 1; idx < strikeFixingDates.Length; idx++)
      {
        var resetDate = strikeFixingDates[idx];
        if (!resetCutoff.IsEmpty() && resetDate > resetCutoff)
          continue;
        var payDate = StockRatchetOption.PayoutOnResetDate ? resetDate : StockRatchetOption.Maturity;
        if (!from.IsEmpty() && payDate <= from)
          continue;
        var sign = StockRatchetOption.Type == OptionType.Call ? 1.0 : -1.0;

        cf.Add(resetDate, payDate, payDate, 1.0,
          Math.Max(sign * (strikeFixingStrikes[idx] - strikeFixingStrikes[idx - 1]), 0.0),
          0.0, 0.0, 0.0);
      }
      return cf;
    }
  }

  #endregion StockRatchetOptionPricer

  #region StockCliquetOptionPricer

  public partial class StockCliquetOptionPricer
  {
    /// <summary>
    /// 
    /// </summary>
    /// <param name="from"></param>
    /// <param name="resetCutoff"> </param>
    /// <returns></returns>
    public Cashflow GenerateStrikeResettingPayoutCashflow(Dt from, Dt resetCutoff)
    {
      var strikeFixingDates = HistoricalPrices.OrderBy(sf => sf.Date).Select(sf => sf.Date).ToArray();
      var strikeFixingStrikes = HistoricalPrices.OrderBy(sf => sf.Date).Select(sf => sf.Rate).ToArray();
      var cf = new Cashflow();

      for (int idx = 1; idx < strikeFixingDates.Length; idx++)
      {
        var resetDate = strikeFixingDates[idx];
        if (!resetCutoff.IsEmpty() && resetDate > resetCutoff)
          continue;
        var payDate = StockCliquetOption.Maturity;
        if (!from.IsEmpty() && payDate <= from)
          continue;
        var sign = 1.0;

        cf.Add(resetDate, payDate, payDate, 1.0,
          Math.Max(sign * (strikeFixingStrikes[idx] - strikeFixingStrikes[idx - 1]), 0.0),
          0.0, 0.0, 0.0);
      }
      return cf;
    }
  }

  #endregion StockCliquetOptionPricer

  #region SwaplegCashflowPricer

  public partial class SwapLegCashflowPricer
  {
    // XL function qSwapLegCashflowTable/qSwapLegCashflows call this function
    /// <summary>
    ///   Generate cashflow from product
    /// </summary>
    ///
    /// <param name="cashflow">Cashflow to fill</param>
    /// <param name="asOf">As of date to fill from</param>
    ///
    public override Cashflow GenerateCashflow(Cashflow cashflow, Dt asOf)
    {
      SwapLeg swapLeg = SwapLeg;

      double principal = (swapLeg.InitialExchange) ? -1 : 0;
      double fee = principal;
      Dt feeSettle = swapLeg.Effective;
      double defaultAmount = this.RecoveryRate;
      Currency defaultCcy = swapLeg.Ccy;
      Dt defaultDate = Dt.Empty;

      if (swapLeg.Floating)
      {
        var schedParams = new ScheduleParams(swapLeg.Effective, swapLeg.FirstCoupon, swapLeg.LastCoupon,
                                             swapLeg.Maturity, swapLeg.Freq, swapLeg.BDConvention, swapLeg.Calendar,
                                             CycleRule.None, flags_);

        cashflow = CashflowFactory.FillFloat(cashflow, asOf, schedParams, swapLeg.Ccy, swapLeg.DayCount,
                                             swapLeg.Coupon, swapLeg.CouponSchedule,
                                             principal, swapLeg.AmortizationSchedule,
                                             this.ReferenceCurve, this.RateResets,
                                             defaultAmount, defaultCcy, defaultDate,
                                             fee, feeSettle);
      }
      else
      {
        var schedParams = new ScheduleParams(swapLeg.Effective, swapLeg.FirstCoupon, swapLeg.LastCoupon,
                                             swapLeg.Maturity, swapLeg.Freq, swapLeg.BDConvention, swapLeg.Calendar,
                                             CycleRule.None, CashflowFlag.None);

        cashflow = CashflowFactory.FillFixed(cashflow, asOf, schedParams,
                                             swapLeg.Ccy, swapLeg.DayCount,
                                             swapLeg.Coupon, swapLeg.CouponSchedule,
                                             principal, swapLeg.AmortizationSchedule,
                                             defaultAmount, defaultCcy, defaultDate,
                                             fee, feeSettle);
        /*
                cashflow = CashflowFactory.FillFixed(cashflow, asOf, swapLeg.Effective, swapLeg.FirstCoupon, swapLeg.LastCoupon, swapLeg.Maturity, swapLeg.Ccy, 
                                                     swapLeg.InitialExchange ? -1 : 0, swapLeg.Effective,
                                                     swapLeg.Coupon, swapLeg.CouponSchedule,
                                                     swapLeg.DayCount, swapLeg.Freq, swapLeg.BDConvention, swapLeg.Calendar,
                                                     swapLeg.InitialExchange ? 1 : 0, swapLeg.AmortizationSchedule, 
                                                     false, false, 
                                                     RecoveryRate, swapLeg.Ccy, 
                                                     false, false, false, false, Dt.Empty, 
                                                     false);
        */
      }

      return cashflow;
    }

  }

  #endregion SwaplegCashflowPricer

  #region SwaplegPricer

  public partial class SwapLegPricer
  {
    /// <summary>
    /// Generate swap cashflows
    /// </summary>
    /// <param name="cf">A cashflow object</param>
    /// <param name="from">Cashflow start date</param>
    /// <returns>Cashflow associated to the swap</returns>
    /// <remarks>If there are no payments between from and to this method will return the payment/payments on the payment date immediately following from</remarks>
    public override Cashflow GenerateCashflow(Cashflow cf, Dt from)
    {
      PaymentSchedule paymentSchedule = GetPaymentSchedule(null, from);
      cf = PaymentScheduleUtils.FillCashflow(cf, paymentSchedule, from, SwapLeg.Notional, 0.0);
      cf.DayCount = SwapLeg.DayCount;
      return cf;
    }


    public double CfPv() //TODO remove intermediate step of converting to old cashflow 
    {
      Cashflow cf = GenerateCashflow(null, Settle);
      cf.AsOf = AsOf;
      var flags = CashflowModelFlags.IncludeFees |
        (SwapLeg.CustomPaymentSchedule != null ? CashflowModelFlags.CreditRiskToPaymentDate : 0) |
        (Product.EffectiveMaturity > Product.Maturity ? CashflowModelFlags.CreditRiskToPaymentDate : 0) |
        (DiscountingAccrued ? CashflowModelFlags.FullFirstCoupon : 0) |
        CashflowSettings.IgnoreAccruedInProtection |
        (IncludePaymentOnSettle ? CashflowModelFlags.IncludeSettlePayments : 0);
      return CashflowModel.Price(cf, AsOf, Settle, DiscountCurve,
        null, null, 0, (int)flags, 0, TimeUnit.None, cf.Count) * CurrentNotional;
    }

  }

  #endregion SwaplegPricer

  #region VarianceSwapPricer
  public partial class VarianceSwapPricer
  {
    /// <summary>
    /// Generate cashflows
    /// </summary>
    /// <param name="cf">A cashflow object</param>
    /// <param name="from">Cashflow start date</param>
    /// <returns>Cashflow associated to the swap</returns>
    public override Cashflow GenerateCashflow(Cashflow cf, Dt from)
    {
      var paymentSchedule = GetPaymentSchedule(null, from);
      cf = PaymentScheduleUtils.FillCashflow(cf, paymentSchedule, from, VarianceSwap.Notional, 0.0);
      return cf;
    }
  }

  #endregion VarianceSwapPricer

} // namespace




