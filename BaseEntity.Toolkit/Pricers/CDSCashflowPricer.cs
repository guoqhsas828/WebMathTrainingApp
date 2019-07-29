/*
 * CDSCashflowPricer.cs
 *
 *  -2009. All rights reserved.
 *
 * RTD - Need to clarify use of CDS Effective and ProtectionStart. Need separate accrual start date and protection start dates.
 * Pricer has AsOf, Settle, ProtectionStart
 * Product has Effective, Maturity, FirstPrem, LastPrem
 * Product needs 1. Date accrual starts = Effective, 2. First valid settlement date = Effective?
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Cashflows.Utils;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.ISDACDSModel;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;
using BaseEntity.Toolkit.Sensitivity;

namespace BaseEntity.Toolkit.Pricers
{
  #region Config
  /// <exclude />
  [Serializable]
  public class CDSCashflowPricerConfig
  {
    /// <exclude />
    [ToolkitConfig("Whether to use the cycle dates without rolling to calclate teh accrual.  If false, payment dates which are not business dates roll to the next business dates.")]
    public readonly bool UseCycleDateForAccruals = false;

    /// <exclude />
    [ToolkitConfig("Whether to include maturity date in CDS accrual calculation.")]
    public readonly bool IncludeMaturityAccrual = true;

    /// <exclude />
    [ToolkitConfig("Whether to include maturity date in CDS protection calculation.")]
    public readonly bool IncludeMaturityProtection = true;

    /// <exclude />
    [ToolkitConfig("Whether to include settlement date payment in price")]
    public readonly bool IncludeSettlePaymentDefault = false;

    /// <exclude />
    [ToolkitConfig("Whether to make cashflow effective the last accrual start date before settle.  If false, cashflow effective is set to as-of date.")]
    public readonly bool UseConsistentCashflowEffective = true;

    /// <exclude />
    [ToolkitConfig("Whether to make full accrual payments after default and get reimbursed with recovery payment.  If false, protection buyer only pays accrual between previous IMM date and default date with recovery settlement.")]
    public readonly bool SupportAccrualRebateAfterDefault = true;
  }

  /// <exclude />
  [Serializable]
  public class CashflowPricerConfig
  {
    /// <exclude />
    [ToolkitConfig("Whether to roll the last payment date if it is not a business date.")]
    public readonly bool RollLastPaymentDate = true;

    /// <exclude />
    [ToolkitConfig("If true, date rolling is separated from date generation (not recommended).")]
    public readonly bool BackwardCompatibleSchedule = false;

    /// <exclude />
    [ToolkitConfig("If true, assume payment dates are coupon dates for credit products (not recommended).")]
    public readonly bool BackwardCompatibleModel = false;

    /// <exclude />
    [ToolkitConfig("Whether to make cashflow effective the last accrual start date before settle.  If false, cashflow effective is set to as-of date.")]
    public readonly bool UseConsistentCashflowEffective = true;

    /// <exclude />
    [ToolkitConfig("Whether the accrued is discounted.")]
    public readonly bool DiscountingAccrued = false;

    /// <exclude />
    [ToolkitConfig("Whether to include accrued paymentd when default on the settle date.")]
    public readonly bool IncludeAccruedOnDefaultAtSettle = true;

    /// <exclude />
    [ToolkitConfig("Whether to exclude accrued from recovery calculation upon default.")]
    public readonly bool IgnoreAccruedInProtection = true;
  }

  public static class CashflowSettings
  {
    public static CashflowModelFlags IgnoreAccruedInProtection
    {
      get
      {
        return ToolkitConfigurator.Settings.CashflowPricer.IgnoreAccruedInProtection
          ? CashflowModelFlags.IgnoreAccruedInProtection : 0;
      }
    }
  }
  #endregion Config

  /// <summary>
  ///   <para>Pricer for an <see cref="BaseEntity.Toolkit.Products.CDS">CDS</see> or a
  ///   <see cref="BaseEntity.Toolkit.Products.LCDS">LCDS</see> based on the
  ///   <see cref="BaseEntity.Toolkit.Models.CashflowModel">General Cashflow Model</see>.</para>
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.CDS" />
  /// <para><h2>Pricing</h2></para>
  /// <inheritdoc cref="CashflowPricer" />
  /// 
  /// <para><h2>LCDS Pricing</h2></para>
  /// <para><b>Adjustment for prepayment risk</b></para>
  /// <para>The lattice method described above may be adjusted to account for prepayment of the
  /// asset in a similar way to that used for counterparty risk. In this case
  /// the three possible states would be "no default", "reference name defaults
  /// prior to protection seller", and "asset prepaid prior to reference name defaulting".</para>
  /// <para>The analysis is per above, with the calculation of probabilities being done
  /// using a survival curve and a probability of prepayment, plus the correlation
  /// between the two. These are combined using a Gaussian factor copula which allows for
  /// direct computation of the required probabilities via quadrature routines.</para>
  /// <para>Recovery rate are specified in one of three ways,</para>
  /// <list type="bullet">
  ///   <item>If the LCDS is a fixed recovery deal, the fixed recovery will be used</item>
  ///   <item>If a recovery rate is specified, that recovery rate will be used</item>
  ///   <item>Otherwise the recovery rate used in the calibration of the survival curve will be used</item>
  /// </list>
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.CDS">CDS Product</seealso>
  /// <seealso cref="BaseEntity.Toolkit.Models.CashflowModel">Cashflow pricing model</seealso>
  [Serializable]
  public partial class CDSCashflowPricer : CashflowPricer, ICDSPricer, IAnalyticDerivativesProvider, IRatesLockable, IQuantoDefaultSwapPricer
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(CDSCashflowPricer));

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <param name="product">CDS to price</param>
    ///
    public
    CDSCashflowPricer(CDS product)
      : this(product, Dt.Empty, Dt.Empty, null, null, null, 0.0, 0, TimeUnit.None)
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The recovery rate is taken from the survival curve</para>
    /// 
    ///   <para>Standard terms include:</para>
    ///   <list type="bullet">
    ///     <item><description>Settlement is T+1</description></item>
    ///     <item><description>Pricing grid is default (premium payments)</description></item>
    ///   </list>
    /// </remarks>
    ///
    /// <param name="product">CDS to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurve">Survival Curve for pricing</param>
    ///
    public
    CDSCashflowPricer(CDS product,
                      Dt asOf,
                      DiscountCurve discountCurve,
                      SurvivalCurve survivalCurve)
      : this(product, asOf, Dt.Add(asOf, 1), discountCurve, survivalCurve, null, 0.0, 0, TimeUnit.None)
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The recovery rate is taken from the survival curve</para>
    /// 
    ///   <para>Standard terms include:</para>
    ///   <list type="bullet">
    ///     <item><description>Settlement is T+1</description></item>
    ///     <item><description>Pricing grid is default (premium payments)</description></item>
    ///   </list>
    /// </remarks>
    ///
    /// <param name="product">CDS to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    ///<param name="referenceCurve">Reference curve for floating payments projection</param>
    /// <param name="survivalCurve">Survival Curve for pricing</param>
    ///
    public
    CDSCashflowPricer(CDS product,
                      Dt asOf,
                      DiscountCurve discountCurve,
                      DiscountCurve referenceCurve,
                      SurvivalCurve survivalCurve)
      : this(product, asOf, Dt.Add(asOf, 1), discountCurve, referenceCurve, survivalCurve, null, 0.0, 0, TimeUnit.None)
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The recovery rate is taken from the survival curve</para>
    /// </remarks>
    ///
    /// <param name="product">CDS to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurve">Survival Curve for pricing</param>
    /// <param name="stepSize">Step size for pricing grid</param>
    /// <param name="stepUnit">Units for step size</param>
    ///
    public
    CDSCashflowPricer(CDS product,
                      Dt asOf,
                      Dt settle,
                      DiscountCurve discountCurve,
                      SurvivalCurve survivalCurve,
                      int stepSize,
                      TimeUnit stepUnit)
      : this(product, asOf, settle, discountCurve, survivalCurve, null, 0.0, stepSize, stepUnit)
    {
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The recovery rate is taken from the survival curve</para>
    /// </remarks>
    ///
    /// <param name="product">CDS to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Reference curve used for floating payments forecast</param>
    /// <param name="survivalCurve">Survival Curve for pricing</param>
    /// <param name="stepSize">Step size for pricing grid</param>
    /// <param name="stepUnit">Units for step size</param>
    ///
    public
    CDSCashflowPricer(CDS product,
                      Dt asOf,
                      Dt settle,
                      DiscountCurve discountCurve,
                      DiscountCurve referenceCurve,
                      SurvivalCurve survivalCurve,
                      int stepSize,
                      TimeUnit stepUnit)
      : this(product, asOf, settle, discountCurve, referenceCurve, survivalCurve, null, 0.0, stepSize, stepUnit)
    {
    }

    /// <summary>
    ///   Constructor with counterparty risks
    /// </summary>
    ///
    /// <param name="product">CDS to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurve">Survival Curve of underlying credit</param>
    /// <param name="counterpartySurvivalCurve">Survival Curve of counterparty</param>
    /// <param name="correlation">Correlation between credit and conterparty defaults</param>
    /// <param name="stepSize">Step size for pricing grid</param>
    /// <param name="stepUnit">Units for step size</param>
    ///
    public
    CDSCashflowPricer(CDS product,
                      Dt asOf,
                      Dt settle,
                      DiscountCurve discountCurve,
                      SurvivalCurve survivalCurve,
                      SurvivalCurve counterpartySurvivalCurve,
                      double correlation,
                      int stepSize,
                      TimeUnit stepUnit)
      : base(product, asOf, settle,
           discountCurve, discountCurve,
           survivalCurve, counterpartySurvivalCurve, correlation,
           stepSize, stepUnit)
    {
      var config = settings_.CDSCashflowPricer;
      IncludeMaturityAccrual = config.IncludeMaturityAccrual;
      IncludeMaturityProtection = config.IncludeMaturityProtection;
      IncludeSettlePayments = config.IncludeSettlePaymentDefault;
      SupportAccrualRebateAfterDefault = config.SupportAccrualRebateAfterDefault;
    }

    /// <summary>
    ///   Constructor with counterparty risks
    /// </summary>
    ///
    /// <param name="product">CDS to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Reference Curve for floating payments forecast</param>
    /// <param name="survivalCurve">Survival Curve of underlying credit</param>
    /// <param name="counterpartySurvivalCurve">Survival Curve of counterparty</param>
    /// <param name="correlation">Correlation between credit and conterparty defaults</param>
    /// <param name="stepSize">Step size for pricing grid</param>
    /// <param name="stepUnit">Units for step size</param>
    ///
    public
    CDSCashflowPricer(CDS product,
                      Dt asOf,
                      Dt settle,
                      DiscountCurve discountCurve,
                      DiscountCurve referenceCurve,
                      SurvivalCurve survivalCurve,
                      SurvivalCurve counterpartySurvivalCurve,
                      double correlation,
                      int stepSize,
                      TimeUnit stepUnit)
      : base(product, asOf, settle,
           discountCurve, referenceCurve,
           survivalCurve, counterpartySurvivalCurve, correlation,
           stepSize, stepUnit)
    {
      var config = settings_.CDSCashflowPricer;
      IncludeMaturityAccrual = config.IncludeMaturityAccrual;
      IncludeMaturityProtection = config.IncludeMaturityProtection;
      IncludeSettlePayments = config.IncludeSettlePaymentDefault;
      SupportAccrualRebateAfterDefault = config.SupportAccrualRebateAfterDefault;
    }

    /// <summary>
    ///   Constructor with Quanto risk
    /// </summary>
    ///
    /// <remarks>
    ///   <para>The recovery rate is taken from the survival curve unless a fixed recovery is specified in the product</para>
    /// </remarks>
    ///
    /// <param name="product">CDS to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Reference curve used for floating payments forecast</param>
    /// <param name="survivalCurve">Survival Curve for pricing</param>
    /// <param name="fxCurve">Forward FX curve </param>
    /// <param name="fxVolatility">Forward FX volatility</param>
    /// <param name="fxCorrelation">Correlation between default time Gaussian transform and forward FX 
    /// from numeraire (discountCurve) currency to survivalCurve currency under measure associated to 
    /// numeraire in survivalCurve currency</param>
    /// <param name="fxDevaluation">Jump of forward FX (from numeraire (discountCurve) currency to survivalCurve currency) at default</param>
    /// <param name="stepSize">Step size for pricing grid</param>
    /// <param name="stepUnit">Units for step size</param>
    public
    CDSCashflowPricer(CDS product,
                      Dt asOf,
                      Dt settle,
                      DiscountCurve discountCurve,
                      DiscountCurve referenceCurve,
                      SurvivalCurve survivalCurve,
                      FxCurve fxCurve,
                      VolatilityCurve fxVolatility,
                      double fxCorrelation,
                      double fxDevaluation,
                      int stepSize,
                      TimeUnit stepUnit)
      : this(product, asOf, settle, discountCurve, referenceCurve, survivalCurve, null, 0.0, stepSize, stepUnit)
    {
      FxCurve = fxCurve;
      FxVolatility = fxVolatility;
      FxCorrelation = fxCorrelation;
      FxDevaluation = fxDevaluation;
    }

    #endregion Constructors

    #region Generate Payment Schedule

    /// <summary>
    /// Generates the payment schedule.
    /// </summary>
    /// <param name="asOf">The protection start date</param>
    /// <param name="cds">The CDS</param>
    /// <param name="fee">The upfront fee</param>
    /// <returns>PaymentSchedule</returns>
    /// 
    /// <remarks>
    ///  This is the <see cref="PaymentSchedule" /> counterpart of the method
    ///  <see cref="GenerateCashflow(Cashflow, Dt, CDS, double)" />.
    ///  The results must be fully compatible.
    /// </remarks>
    public PaymentSchedule GeneratePayments(
      Dt asOf, CDS cds, double fee)
    {
      DefaultSettlement defaultSettlement;
      var ps = GeneratePayments(asOf, cds, fee, out defaultSettlement);
      if (defaultSettlement != null) ps.AddPayment(defaultSettlement);
      return ps;
    }

    /// <summary>
    /// Generates the payment schedule, with the default settlement
    ///  payment separated from the schedule.
    /// </summary>
    /// <param name="asOf">The as-of date</param>
    /// <param name="cds">The CDS product</param>
    /// <param name="fee">The fee</param>
    /// <param name="defaultSettlement">The separated default settlement, or null</param>
    /// <returns>The PaymentSchedule without the default payment</returns>
    private PaymentSchedule GeneratePayments(Dt asOf,
      CDS cds, double fee, out DefaultSettlement defaultSettlement)
    {
      defaultSettlement = null;

      Dt defaultDate = SurvivalCurve?.DefaultDate ?? Dt.Empty;
      var flag = GetPaymentGenerationFlag(asOf, cds.CashflowFlag, 
        cds.Funded, defaultDate);

      var ps = new PaymentSchedule();
      if (!defaultDate.IsEmpty() && defaultDate < cds.Effective)
        return ps; // default before effective

      ps.AddPayments(GenerateRegularPayments(asOf, cds, defaultDate, flag));

      if (defaultDate.IsEmpty())
      {
        if (cds.Bullet != null && !cds.Bullet.CouponRate.Equals(0.0))
        {
          var payDt = ps.GetPaymentDates().Max();
          var bullet = new BulletBonusPayment(payDt,
            cds.Bullet.CouponRate, cds.Ccy);
          if (flag.IncludeMaturityProtection)
          {
            var ip = ps.GetPaymentsOnDate(payDt)
              .OfType<InterestPayment>().First();
            bullet.CreditRiskEndDate = ip.PeriodEndDate + 1;
          }
          ps.AddPayment(bullet);
        }
      }
      else
      {
        // The legacy cash flow enables the default settle date only for
        // the existing defaults occurring in the past.
        Dt cashflowStart = asOf < ProtectionStart ? ProtectionStart : asOf;
        Dt defaultPaymentDate = defaultDate < cashflowStart
          ? RecoveryCurve?.JumpDate ?? Dt.Empty : Dt.Empty;
        defaultSettlement = LegacyCashflowCompatible.GetDefaultSettlement(
          ps, GetRecoveryRate(), cds.RecoveryCcy,
          defaultDate, defaultPaymentDate, flag);
      }

      var upfront = GetUpfrontFeePayment(asOf, cds, fee);
      if (upfront != null) ps.AddPayment(upfront);

      return ps;
    }

    /// <summary>
    /// Generates the regular payments up to and including
    ///  the period when the default occurs, or to the last
    ///  period on no default.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="cds">The CDS.</param>
    /// <param name="defaultDate">The default date.</param>
    /// <param name="flag">The flag.</param>
    /// <returns>IEnumerable&lt;Payment&gt;.</returns>
    private IEnumerable<Payment> GenerateRegularPayments(
      Dt asOf, CDS cds, Dt defaultDate, PaymentGenerationFlag flag)
    {
      var discountCurve = DiscountCurve;
      var referenceCurve = cds.CdsType == CdsType.FundedFloating
        ? ReferenceCurve ?? discountCurve : null;

      double principal = (cds.Funded) ? 1.0 : 0.0;
      return LegacyCashflowCompatible.GetRegularPayments(
        asOf, defaultDate, cds.Schedule,
        cds.Ccy, cds.DayCount, flag,
        cds.Premium, cds.PremiumSchedule,
        principal, cds.AmortizationSchedule,
        referenceCurve, discountCurve, this.RateResets);
    }

    /// <summary>
    ///   Helper function to generate cash flows with 1bp premium
    /// </summary>
    public PaymentSchedule GeneratePayments01(Dt asOf, Dt settle,
      SurvivalCurve survivalCurve, bool includeAccrued)
    {
      var ps = new PaymentSchedule();
      Dt defaultDate = survivalCurve?.DefaultDate ?? Dt.Empty;
      if (!defaultDate.IsEmpty() && defaultDate <= settle)
        return ps;

      CDS cds = CDS;
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

      var schedParams = new ScheduleParams(effective, firstPrem,
        lastPrem, cds.Maturity, cds.Freq, cds.BDConvention,
        cds.Calendar, cycleRule, flags);
      var schedule = Schedule.CreateScheduleForCashflowFactory(schedParams);

      const double principal = 0.0;
      const double premium = 0.0001;
      var psFlag = GetPaymentGenerationFlag(asOf, flags, false, defaultDate);

      ps.AddPayments(LegacyCashflowCompatible.GetRegularPayments(
        asOf, defaultDate, schedule,
        cds.Ccy, cds.DayCount, psFlag,
        premium, EmptyArray<CouponPeriod>.Instance,
        principal, cds.AmortizationSchedule));

      if (!defaultDate.IsEmpty() && defaultDate >= asOf)
      {
        LegacyCashflowCompatible.GetDefaultSettlement(
          ps, GetRecoveryRate(), cds.RecoveryCcy,
          defaultDate, Dt.Empty, psFlag);
      }
      return ps;
    }

    /// <summary>
    /// Gets the upfront fee payment.
    /// </summary>
    /// <param name="asOf">As of.</param>
    /// <param name="cds">The CDS.</param>
    /// <param name="fee">The fee.</param>
    /// <returns>The <see cref="UpfrontFee" />, or null for no such fee</returns>
    private UpfrontFee GetUpfrontFeePayment(Dt asOf, CDS cds, double fee)
    {
      Dt feeSettle = FeeSettle;
      if ((fee > 0 || fee < 0) && asOf <= feeSettle)
      {
        var ccy = cds.Ccy;
        return new UpfrontFee(feeSettle, fee, ccy);
      }
      return null;
    }

    public double GetRecoveryRate()
    {
      var cds = CDS;
      if (IsQuantoStructure && cds.RecoveryCcy != DiscountCurve.Ccy)
      {
        return cds.QuantoNotionalCap.HasValue
          ? (1 - cds.QuantoNotionalCap.Value) : 0.0;
      }
      return cds.FixedRecovery ? cds.FixedRecoveryRate : RecoveryRate;
    }

    private PaymentGenerationFlag GetPaymentGenerationFlag(
      Dt asOf, CashflowFlag flag, bool isFunded, Dt defaultDate)
    {
      Dt cashflowStart = asOf < ProtectionStart ? ProtectionStart : asOf;
      return new PaymentGenerationFlag(
        // Always accrual on cycle
        flag | CashflowFlag.AccrueOnCycle,
        IncludeMaturityProtection,
        defaultDate < cashflowStart && SupportAccrualRebateAfterDefault,
        isFunded);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      // Clone
      CDSCashflowPricer obj = (CDSCashflowPricer) base.Clone();
      obj.Reset();
      obj.ISDADiscountCurve = (ISDADiscountCurve != null) ? (DiscountCurve) this.ISDADiscountCurve.Clone() : null;
      obj.Bond = (Bond != null) ? (Bond) this.Bond.Clone() : null;
      obj.SwapLeg = (SwapLeg != null) ? (SwapLeg) this.SwapLeg.Clone() : null;
      obj.FxCurve = FxCurve;
      obj.FxCorrelation = FxCorrelation;
      obj.FxVolatility = FxVolatility;
      obj.FxDevaluation = FxDevaluation;
      return obj;
    }

    /// <summary>
    ///   Reset the pricer
    /// </summary>
    ///
    /// <remarks>
    ///   <para>There are some pricers which need to remember some public state
    ///   in order to skip redundant calculation steps. This method is provided
    ///   to indicate that this internate state should be cleared.</para>
    /// </remarks>
    ///
    public override void Reset()
    {
      fee_ = null;
      conventionalSpread_ = null;
      base.Reset();
    }

    /// <summary>
    ///   Reset the pricer
    ///   <preliminary/>
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Some pricers need to remember certain public states in order
    ///   to skip redundant calculation steps.
    ///   This function tells the pricer that what attributes of the products
    ///   and other data have changed and therefore give the pricer an opportunity
    ///   to selectively clear/update its public states.  When used with caution,
    ///   this method can be much more efficient than the method Reset() without argument,
    ///   since the later resets everything.</para>
    ///
    ///   <para>The default behaviour of this method is to ignore the parameter
    ///   <paramref name="what"/> and simply call Reset().  The derived pricers
    ///   may implement a more efficient version.</para>
    /// </remarks>
    /// 
    /// <param name="what">The flags indicating what attributes to reset</param>
    /// <exclude/>
    public override void Reset(ResetAction what)
    {
      if (what == QuoteChanged)
      {
        fee_ = null;
        conventionalSpread_ = null;
      }
      else
        base.Reset(what);
    }

    #endregion Utilty Methods

    #region Methods

    /// <summary>
    ///   Upfront fee
    /// </summary>
    /// <remarks>
    ///   <para>The upfront fee either specified directly or calculated from
    ///   the quoted conventional spread.</para>
    ///   <para>Use MarketQuote and QuotingConvention to specify either
    ///   the upfront fee or the conventional spread.</para>
    /// </remarks>
    /// <returns>Upfront fee as a percentage of notional</returns>
    public double Fee()
    {
      if (fee_ == null)
      {
        switch (QuotingConvention)
        {
          case QuotingConvention.CreditConventionalSpread:
            {
              if (ISDADiscountCurve == null)
                throw new ArgumentException("Unable to convert CDS conventional spread to fee as missing ISDA swap curve");
              if (ISDARecoveryRate == 0.0)
                throw new ArgumentException("Unable to convert CDS conventional spread to fee as ISDA recovery rate not set");
              if (!ISDAConverterDate.IsValid())
                throw new ArgumentException("Unable to convert CDS conventional spread to fee as ISDA trade date not set");
              var res = SNACCDSConverter.Convert(
                ISDAConverterDate, CDS.Maturity, ISDADiscountCurve, CDS.Premium * 10000.0, ISDARecoveryRate,
                Notional, MarketQuote, SNACCDSConverter.InputType.ConvSpread);
              fee_ = (100.0 - res.CleanPrice) / 100.0;
              break;
            }
          case QuotingConvention.CreditConventionalUpfront:
            fee_ = MarketQuote;
            break;
          case QuotingConvention.None:
            // Return here as calling methods could change the CDS Fee and not know to reset the pricer.
            // This will go away once the Fee has been removed from the product. RTD Sep'09
            return CDS.Fee;
          default:
            throw new ArgumentException("Unsupported quoting convention for CDS");
        }
      }

      return (double)fee_;
    }

    /// <summary>
    ///   Conventional quoted spread
    /// </summary>
    /// <remarks>
    ///   <para>The conventional spread either specified directly or
    ///   calculated from the quoted upfront fee.</para>
    ///   <para>Use MarketQuote and QuotingConvention to specify either
    ///   the upfront fee or the conventional spread.</para>
    /// </remarks>
    /// <returns>Conventional spread</returns>
    public double ConventionalSpread()
    {
      if (conventionalSpread_ == null)
      {
        switch (quotingConvention_)
        {
          case QuotingConvention.CreditConventionalSpread:
            conventionalSpread_ = MarketQuote;
            break;
          case QuotingConvention.CreditConventionalUpfront:
            {
              if (ISDADiscountCurve == null)
                throw new ArgumentException("Unable to convert CDS fee to conventional spread as missing ISDA swap curve");
              if (ISDARecoveryRate == 0.0)
                throw new ArgumentException("Unable to convert CDS fee to conventional spread as ISDA recovery rate not set");
              if (!ISDAConverterDate.IsValid())
                throw new ArgumentException("Unable to convert CDS fee to conventional spread as ISDA trade date not set");
              var res = SNACCDSConverter.Convert(
                ISDAConverterDate, CDS.Maturity, ISDADiscountCurve, CDS.Premium * 10000.0, ISDARecoveryRate,
                Notional, MarketQuote, SNACCDSConverter.InputType.UpFront);
              conventionalSpread_ = res.ConventionalSpread / 10000.0;
              break;
            }
          case QuotingConvention.None:
            {
              if (ISDADiscountCurve == null)
                throw new ArgumentException("Unable to convert CDS fee to conventional spread as missing ISDA swap curve");
              if (ISDARecoveryRate == 0.0)
                throw new ArgumentException("Unable to convert CDS fee to conventional spread as ISDA recovery rate not set");
              if (!ISDAConverterDate.IsValid())
                throw new ArgumentException("Unable to convert CDS fee to conventional spread as ISDA trade date not set");
              var res = SNACCDSConverter.Convert(
                ISDAConverterDate, CDS.Maturity, ISDADiscountCurve, CDS.Premium * 10000.0, ISDARecoveryRate,
                Notional, BreakEvenFee(), SNACCDSConverter.InputType.UpFront);
              conventionalSpread_ = res.ConventionalSpread / 10000.0;
              break;
            }
          default:
            throw new ArgumentException("Unsupported quoting convention for CDS");
        }
      }
      return (double)conventionalSpread_;
    }

    /// <summary>
    ///   Calculate the accrued premium for a Credit Default Swap
    /// </summary>
    /// <remarks>
    ///   <para>The accrued is in dollars.</para>
    /// </remarks>
    /// <returns>Accrued premium of Credit Default Swap at the settlement date</returns>
    public override double Accrued()
    {
      CDS cds = CDS;
      Dt start;
      double prem;
      Dt settle = ProtectionStart;
      if (FindAccrualStart(settle, cds, out start, out prem))
      {
        return cds.Schedule.Fraction(start, settle, cds.DayCount)*prem*CurrentNotional;
      }
      DefaultSettlement dp;
      GeneratePayments(AsOf, CDS, Fee(), out dp);
      if (dp == null) 
        return 0;
      if (((PricerFlags & PricerFlags.NoDefaults) != PricerFlags.NoDefaults) && dp.PayDt > Settle) 
        return dp.Accrual * Notional;
      return 0;
    }

    /// <summary>
    ///   Calculate the number of accrual days for a Credit Default Swap
    /// </summary>
    /// <returns>Number of days accrual for Credit Default Swap at the settlement date</returns>
    public int AccrualDays()
    {
      CDS cds = CDS;
      Dt start;
      double prem;
      Dt settle = ProtectionStart;
      if (FindAccrualStart(settle, cds, out start, out prem))
        return Dt.FractionDays(start, settle, cds.DayCount);
      return 0;
    }

    /// <summary>
    ///   Find the start day for calculating accrual
    /// </summary>
    /// <remarks>
    ///   <para>This is a duplication of code in CashflowFactory and Schedule.
    ///   After the schedule class has been revisited, this should be removed.</para>
    /// </remarks>
    /// <param name="settle">Settlement date</param>
    /// <param name="cds">CDS</param>
    /// <param name="accrualStart">Accrual starting date ((output)</param>
    /// <param name="premium">Premium for accrual period ((output)</param>
    /// <returns>default status of the name</returns>
    /// <exclude />
    public bool FindAccrualStart(Dt settle, CDS cds, out Dt accrualStart, out double premium)
    {
      // Find the effective survival
     if ((SurvivalCurve != null && SurvivalCurve.DefaultDate.IsValid() && (SurvivalCurve.DefaultDate < settle)) ||
        (CounterpartyCurve != null && CounterpartyCurve.DefaultDate.IsValid() && (CounterpartyCurve.DefaultDate < settle)))
      {
        // Credit or counterparty has defaulted before the settlement date
        accrualStart = Settle;
        premium = 0;
        return false;
      }

      // Generate out payment dates from settlement.
      Schedule sched = CDS.CashflowFactorySchedule;

      // Step over to find the period for accrual
      int idx;
      for (idx = 0; idx < sched.Count; idx++)
      {
        Dt periodStartDate = (idx > 0) ? sched.GetPaymentDate(idx - 1) : sched.GetPeriodStart(0);
        if (Dt.Cmp(periodStartDate, settle) >= 0)
          break;
      }
      if (idx > 0)
        idx--;

      // Determine accrual start/end dates
      Dt start = (idx > 0) ? sched.GetPaymentDate(idx - 1) : sched.GetPeriodStart(0);
      Dt end = sched.GetPaymentDate(idx);
      accrualStart = start;

      // Get remaining principal
      double remainingPrincipal = AmortizationUtil.PrincipalAt(cds.AmortizationSchedule, 1.0, start);

      // Get current coupon
      premium = CouponPeriodUtil.CouponAt(cds.PremiumSchedule, cds.Premium, start) * remainingPrincipal;

      // For funded floating CDS
      if (CDS.CdsType == CdsType.FundedFloating)
      {
        if (RateResets != null && RateResets.Count > 0)
        {
          // Sort the RateReset by date
          int num = RateResets.Count;
          Dt[] rateResetDates = new Dt[num];
          double[] rateResetRates = new double[num];
          for (int i = 0; i < num; i++)
          {
            rateResetDates[i] = RateResets[i].Date;
            rateResetRates[i] = RateResets[i].Rate;
          }
          for (int i = 0; i < num - 1; i++)
          {
            for (int j = i + 1; j < num; j++)
            {
              if (Dt.Cmp(RateResets[i].Date, RateResets[j].Date) > 0)
              {
                Dt tempt = rateResetDates[j];
                rateResetDates[j] = rateResetDates[i];
                rateResetDates[i] = tempt;
                double tempd = rateResetRates[j];
                rateResetRates[j] = rateResetRates[i];
                rateResetRates[i] = tempd;
              }
            }
          }
          // throw exception or use CouponPeriodUtil.CouponAt from 
          // previous step
          if (Dt.Cmp(accrualStart, rateResetDates[0]) < 0)
            throw new ArgumentException("RateReset list dates do not cover accrual start date.");
          // Need to find proper rate for premium.
          // Assume the reset dates are sorted in ascending order,
          // the following loop find the LATEST date on or before accrual start.
          for (int i = num; --i >= 0; )
            if (Dt.Cmp(accrualStart, rateResetDates[i]) >= 0)
            {
              premium = rateResetRates[i];
              break;
            }
        }
      }

      // Note schedule currently includes last date in schedule period. This may get changed in
      // the future so to handle this we test if we are on a coupon date.
      if (Dt.Cmp(settle, start) == 0 || Dt.Cmp(settle, end) == 0 || Dt.Cmp(settle, cds.Maturity) >= 0)
        return false;
      return true;
    }

    /// <summary>
    ///   Calculates replicating spread for a CDS.
    /// </summary>
    /// <remarks>
    ///   <para>The replicating spread is the premium that implies a zero MTM value assuming 0 upfront fee.</para>
    ///   <para>It can be calculated by Breakeven Premium for a 0-upfront CDS</para>
    /// </remarks>
    /// <returns>Replicating spread for CDS in bps</returns>
    public double ReplicatingSpread()
    {
      return CalcBreakEvenPremium(Settle, true);
    }

    /// <summary>
    ///   Calculate break-even premium for a Credit Default Swap
    /// </summary>
    /// <remarks>
    ///   <para>The break-even premium is the premium which would imply a zero MTM value.</para>
    ///   <para>The BreakEvenPremium is related to the Protection Leg PV, the Duration and the
    ///   Notional by <formula inline="true">\mathrm{BreakEvenPremium = \displaystyle{\frac{ProtectionLegPv}{Duration \times Notional}}}</formula></para>
    ///   <para>For consistency with the Duration, the break-even premium ignores accrued and
    ///   is effectively the break-even premium for a newly issued CDS where the effective date
    ///   is set to the settlement date.</para>
    /// </remarks>
    /// <returns>Break-even premium for Credit Default Swap in percent</returns>
    public double BreakEvenPremium()
    {
      return CalcBreakEvenPremium(Settle, false);
    }

    /// <summary>
    ///  Calculate breakeven fee for a CDS
    /// </summary>
    /// <remarks>
    ///   <para>The breakeven fee satisfies: UpFront(BEF) + CleanFee(Premium) = ProtectionLeg</para>
    /// </remarks>
    /// <returns>Breakeven Fee</returns>
    public double BreakEvenFee()
    {
      return CalcBreakEvenFee(Double.NaN);
    }

    /// <summary>
    ///  Calculate breakeven fee for a CDS given a specified premium
    /// </summary>
    /// <remarks>
    ///   <para>The breakeven fee satisfies: UpFront(BEF) + CleanFee(Premium) = ProtectionLeg</para>
    /// </remarks>
    /// <param name="runningPremium">Running premium of CDS</param>
    /// <returns>Breakeven Fee</returns>
    public double BreakEvenFee(double runningPremium)
    {
      return CalcBreakEvenFee(runningPremium);
    }

    /// <summary>
    ///   Calculate the forward break-even premium for a Credit Default Swap
    /// </summary>
    /// <remarks>
    ///   <para>The forward break-even premium is the premium which would imply a
    ///   zero MTM value on the specified forward settlement date.</para>
    ///   <para>The BreakEvenPremium is related to the Protection Leg PV, the Duration and the
    ///   Notional by <formula inline="true">\mathrm{BreakEvenPremium = \displaystyle{\frac{ProtectionLegPv}{Duration \times Notional}}}</formula></para>
    ///   <para>For consistency with the Duration, the break-even premium ignores accrued and
    ///   is effectively the break-even premium for a newly issued CDS where the effective date
    ///   is set to the settlement date.</para>
    ///   <para>Note: Currently does not support CDS with up-front fees</para>
    /// </remarks>
    /// <param name="forwardSettle">Forward Settlement date</param>
    /// <returns>The forward break-even premium for the Credit Default Swap in percent</returns>
    public double FwdPremium(Dt forwardSettle)
    {
      return CalcBreakEvenPremium(forwardSettle, false);
    }

    /// <summary>
    ///   Calculate the forward break-even premium for a Credit Default Swap
    /// </summary>
    /// <remarks>
    ///   <para>The forward break-even premium is the premium which would imply a
    ///   zero MTM value on the specified forward settlement date.</para>
    ///   <para>The BreakEvenPremium is related to the Protection Leg PV, the Duration and the
    ///   Notional by <formula inline="true">\mathrm{BreakEvenPremium = \displaystyle{\frac{ProtectionLegPv}{Duration \times Notional}}}</formula></para>
    ///   <para>For consistency with the Duration, the break-even premium ignores accrued and
    ///   is effectively the break-even premium for a newly issued CDS where the effective date
    ///   is set to the settlement date.</para>
    ///   <para>Note: Currently does not support CDS with up-front fees</para>
    /// </remarks>
    /// <param name="forwardSettle">Forward Settlement date</param>
    /// <param name="replicatingSpread">True means CDS assumes 0 upfront fee</param>
    /// <returns>The forward break-even premium for the Credit Default Swap in percent</returns>
    public double FwdPremium(Dt forwardSettle, bool replicatingSpread)
    {
      return CalcBreakEvenPremium(forwardSettle, replicatingSpread);
    }

    /// <summary>
    ///   Calculate the Premium 01 of Credit Default Swap
    /// </summary>
    /// <remarks>
    ///   <para>The Premium 01 is the change in PV (MTM) for a Credit
    ///   Default Swap if the premium is increased by one basis point.</para>
    ///   <para>The Premium 01 is calculated by calculating the PV (MTM) of the
    ///   Credit Default Swap then bumping up the premium by one basis point
    ///   and re-calculating the PV and returning the difference in value.</para>
    ///   <para>The Premium 01 includes accrued.</para>
    /// </remarks>
    /// <returns>Premium 01 of the Credit Default Swap</returns>
    public double Premium01()
    {
      return FwdPremium01(Settle);
    }

    /// <summary>
    ///   Calculate the Forward Premium 01 of Credit Default Swap
    /// </summary>
    /// <remarks>
    ///   <para>The Forward Premium 01 is the change in PV (MTM) at a
    ///   future settlement date for a Credit Default Swap if
    ///   the premium is increased by one basis point.</para>
    ///   <para>The Forward Premium 01 is calculated by calculating
    ///   the PV (MTM) of the Credit Default Swap at a specified
    ///   future settlement date and then bumping up the premium
    ///   by one basis point and re-calculating the MTM at the
    ///   same forward settlement date and returning the difference
    ///   in value.</para>
    ///   <para>The Foward Premium 01 includes accrued.</para>
    /// </remarks>
    /// <param name="forwardSettle">Forward settlement date</param>
    /// <returns>Forward Premium 01 of the Credit Default Swap</returns>
    public double FwdPremium01(Dt forwardSettle)
    {
      return IsLegacyCashflowEnabled
        ? CfFwdPremium01(forwardSettle)
        : PsFwdPremium01(forwardSettle);
    }

    public double PsFwdPremium01(Dt fwdSettle)
    {
      // Test forward settlement on or after product effective date
      if (Product != null && (Product.Effective > fwdSettle))
        fwdSettle = Product.Effective;

      var cds = CDS;
      var asOf = AsOf;
      var defaultRisk = GetDefaultRiskCalculator(fwdSettle, fwdSettle);

      // Price stream of 1bp premiums
      if (cds.CdsType == CdsType.Unfunded)
      {
        // Price stream of 1bp premiums matching CDS
        return RegularPv(
          GeneratePayments01(asOf, fwdSettle, SurvivalCurve, true),
          fwdSettle, fwdSettle, DiscountCurve, defaultRisk,
          GetRecoveryRate(), false, cds.CashflowFlag, true, false,
          false, DiscountingAccrued) * CurrentNotional;
      }

      // funded case
      DefaultSettlement ds;
      var fee = Fee();
      var ps = GeneratePayments(fwdSettle, cds, fee, out ds);

      var basePv = RegularPv(ps,
        fwdSettle, fwdSettle, DiscountCurve, defaultRisk,
        GetRecoveryRate(), cds.Funded, cds.CashflowFlag, true, true,
        false, DiscountingAccrued) * CurrentNotional;

      if (UniformBump)
      {
        foreach (var ip in ps.OfType<InterestPayment>())
          ip.FixedCoupon += 0.0001;
      }
      else
      {
        var savedPremium = cds.Premium;
        try
        {
          cds.Premium += 0.0001;
          ps = GeneratePayments(fwdSettle, cds, fee, out ds);
        }
        finally
        {
          cds.Premium = savedPremium;
        }
      }

      var bumpedPv = RegularPv(ps,
        fwdSettle, fwdSettle, DiscountCurve, defaultRisk,
        GetRecoveryRate(), cds.Funded, cds.CashflowFlag, true, true,
        false, DiscountingAccrued) * CurrentNotional;

      return bumpedPv - basePv;
    }


    /// <summary>
    ///   Calculate the risky duration of the Credit Default Swap
    /// </summary>
    /// <remarks>
    ///   <para>The risky duration is the fee pv of a CDS with a premium of 1 (10,000bps) and
    ///    a notional of 1.0.</para>
    ///   <para>The risky duration is based on the remaining premium that is uncertain and
    ///   as such does not include any accrued.</para>
    ///   <para>The Duration is related to the Protection Leg PV, the Break Even Premium and the
    ///   Notional by <formula inline="true">Duration = ProtectionLegPv / { BreakEvenPremium * Notional }</formula></para>
    ///   <para>The risky duration is discounted back to the settle date or
    ///   the pricing date (as-of date), depending on the runtime configuration in xml
    ///   By default, it discounts back to as-of date.</para>
    /// </remarks>
    /// <returns>Risky duration of the Credit Default Swap</returns>
    public double RiskyDuration()
    {
      return IsLegacyCashflowEnabled ? CfRiskyDuration() : PsRiskyDuration();
    }

    public double PsRiskyDuration()
    {
      if (Dt.Cmp(Settle, CDS.Maturity) > 0)
        return 0.0;

      // Price premium of 1.0 pv
      return (CDS.CdsType == CdsType.Unfunded)
        ? PsUnfundedRiskyDuration(ProtectionStart)
        : (PsFwdPremium01(Settle) / Notional * 10000);
    }

    private double PsUnfundedRiskyDuration(Dt forwardSettle)
    {
      var asOf = AsOf;

      // Price stream of 1bp premiums matching CDS
      var ps = GeneratePayments01(asOf, forwardSettle, SurvivalCurve, false);

      var defaultRisk = GetDefaultRiskCalculator(asOf, forwardSettle);
      return RegularPv(ps, asOf, forwardSettle, DiscountCurve, defaultRisk,
        GetRecoveryRate(), false, CDS.CashflowFlag, true, false,
        false, DiscountingAccrued) * 10000.0;
    }


    /// <summary>
    ///   Calculate the risky duration of the Credit Default Swap
    /// </summary>
    /// <remarks>
    ///   <para>The risky duration is the pv of a premium of 1.</para>
    ///   <para>The risky duration is based on the remaining premium that is uncertain and
    ///   as such does not include any accrued.</para>
    ///   <para>The Duration is related to the Protection Leg PV, the Break Even Premium and the
    ///   Notional by <formula inline="true">\mathrm{Duration = \displaystyle{\frac{ProtectionLegPv}{BreakEvenPremium \times Notional}}}</formula></para>
    ///   <para>The forward risky duration is discounted back to the forward settle date instead of
    ///   the pricing date (as-of date).</para>
    /// </remarks>
    /// <param name="forwardSettle">Forward settlement date</param>
    /// <returns>Risky duration of the Credit Default Swap</returns>
    public double RiskyDuration(Dt forwardSettle)
    {
      return IsLegacyCashflowEnabled
        ? CfRiskyDuration(forwardSettle)
        : PsRiskyDuration(forwardSettle);
    }

    public double PsRiskyDuration(Dt forwardSettle)
    {
      return CDS.CdsType == CdsType.Unfunded
        ? PsUnfundedRiskyDuration(forwardSettle)
        : (PsFwdPremium01(forwardSettle) / Notional * 10000);
    }

    /// <summary>
    ///   Calculate survival probability
    /// </summary>
    /// <remarks>
    ///   <para>This method handles the correlation between the credit and counterpaty defaults.</para>
    /// </remarks>
    /// <returns>Survival probability at the maturity date</returns>
    public double SurvivalProbability()
    {
      return settings_.CDSCashflowPricer.IncludeMaturityProtection ?
        SurvivalProbability(Settle, Dt.Add(CDS.Maturity, 1)) :
        SurvivalProbability(Settle, CDS.Maturity);
    }

    /// <summary>
    ///   Calculate survival probability
    /// </summary>
    /// <remarks>
    ///   <para>This method handles the correlation between the credit and counterpaty defaults.</para>
    /// </remarks>
    /// <returns>Survival probability at the maturity date</returns>
    public double SurvivalProbability(Dt end)
    {
      return SurvivalProbability(Settle, end);
    }

    /// <summary>
    ///   Calculate the carry of the Credit Default Swap
    /// </summary>
    /// <remarks>
    ///   <para>The carry is daily income from premium and is simply
    ///   the premium divided by 360 times the notional, <formula inline="true">\mathrm{Carry=\displaystyle{\frac{Premium}{360}}\times Notional}</formula>.</para>
    /// </remarks>
    /// <returns>Carry of the Credit Default Swap</returns>
    public double Carry()
    {
      CDS cds = CDS;
      Dt settle = Settle;
      if (Dt.Cmp(settle, cds.Maturity) < 0)
      {
        // Find the proper premium at the settle date.
        Dt start;
        double prem;
        FindAccrualStart(settle, cds, out start, out prem);
        return prem / 360 * CurrentNotional;
      }
      return 0;
    }

    /// <summary>
    ///   Calculate the MTM Carry of the Credit Default Swap
    /// </summary>
    /// <remarks>
    ///   <para>The MTM carry is the daily income of the MTM level
    ///   of the credit default swap. It is the Break Even Premium
    ///   divided by 360 times the notional, <formula inline="true">\mathrm{Carry=\displaystyle{\frac{BreakEvenPremium}{360}}\times Notional}</formula></para>
    /// </remarks>
    /// <returns>MTM Carry of Credit Default Swap</returns>
    public double MTMCarry()
    {
      return Dt.Cmp(Settle, CDS.Maturity) > 0 ? 0.0 : BreakEvenPremium() / 360 * CurrentNotional;
    }

    /// <summary>
    ///   Calculate the implied flat(annual) counterparty curve given a duration quote 
    /// </summary>
    /// <remarks>
    ///   <para>The function searches for the flat(annual) counterparty rate that matches a given duration quote
    ///   The duration quote has to be less than the CDS maturity</para>
    /// </remarks>
    /// <returns>Implied flat(annual) counterparty curve </returns>
    public SurvivalCurve ImpliedCounterpartyCurve(double duration)
    {
      CDS cds = CDS;
      Dt durationDate = new Dt(AsOf, duration);
      if (Dt.Cmp(durationDate, cds.Maturity) > 0)
        throw new System.ArgumentException("The duration CDS maturity");

      // solve for counterparty rate
      logger.Debug("Calculating implied quoted spread for CDS Index...");

      // Set up root finder
      //
      Brent rf = new Brent();
      rf.setToleranceX(1e-6);
      rf.setToleranceF(1e-10);
      rf.setLowerBounds(1E-10);

      Double_Double_Fn fn_ = new Double_Double_Fn(this.EvaluateQuotedDuration);
      DelegateSolverFn solveFn_ = new DelegateSolverFn(fn_, null);

      // Solve
      double res;
      try
      {
        res = rf.solve(solveFn_, duration, 0.00000001, 0.3);
      }
      finally
      {
        // Tidy up transient data
        fn_ = null;
        solveFn_ = null;
      }

      // build flat refi curve w single annual refinancing probability
      Dt[] tenorDates = new Dt[] { Dt.Add(AsOf, 1, TimeUnit.Years) };
      string[] tenorNames = new string[] { "1Y" };
      double[] nonRefiProbs = new double[] { 1.0 - res };
      SurvivalCurve refinancingCurve = SurvivalCurve.FromProbabilitiesWithBond(AsOf, Currency.None, null, InterpMethod.Weighted,
        ExtrapMethod.Const, tenorDates, nonRefiProbs, tenorNames, null, null, null, 0);

      return refinancingCurve;
    }

    /// <summary>
    ///   Calculate the present value <formula inline="true">Pv = Full Price \times Notional</formula> of the cash flow stream
    /// </summary>
    /// <remarks>
    ///   <para>Cashflows after the settlement date are present valued back to the pricing
    ///   as-of date.</para>
    /// </remarks>
    /// <returns>Present value to the pricing as-of date of the cashflow stream</returns>
    public override double ProductPv()
    {
      return IsLegacyCashflowEnabled ? CfPv() : PsPv();
    }

    public double PsPv()
    {
      if (CDS.PayRecoveryAtMaturity)
      {
        return FullFeePv() + ProtectionPv();
      }
      var pv = PsCalculatePv(AsOf, ProtectionStart, DiscountCurve, true, true);

      if (logger.IsDebugEnabled) LogDiffs(CfPv, pv, "Pv");

      return pv;
    }

    /// <summary>
    ///   Calculate the present value of the fee part (including accrued) of the cashflow stream
    /// </summary>
    /// <remarks>
    ///   <para>Fee cashflows after the settlement date are present valued
    ///   back to the pricing as-of date.</para>
    /// </remarks>
    /// <returns>Present value of the fee part of the cashflow stream</returns>
    public override double FullFeePv()
    {
      return IsLegacyCashflowEnabled ? CfFeePv() : PsFeePv();
    }

    public double PsFeePv()
    {
      var feePv = PsCalculatePv(AsOf, ProtectionStart,
        DiscountCurve, true, false);
      if (CDS.Funded)
        feePv += PsProtectionPv();

      if (logger.IsDebugEnabled)
        LogDiffs(CfFeePv, feePv, "Fee");

      return feePv;
    }

    /// <summary>
    /// Calculate the present value of the protection part of a cashflow stream
    /// </summary>
    /// <remarks>
    ///   <para>Protection cashflows after the settlement date are present valued
    ///   back to the pricing as-of date.</para>
    /// </remarks>
    /// <returns>Present value of the protection part of the cashflow stream</returns>
    public override double ProtectionPv()
    {
      if (this.CDS.Funded)
        return 0;
      return IsLegacyCashflowEnabled ? CfProtectionPv() : PsProtectionPv();
    }

    public double PsProtectionPv()
    {
      Dt asOf = AsOf, settle = ProtectionStart;
      bool payRecovAtMaturity = CDS.PayRecoveryAtMaturity;
      var discountCurve = payRecovAtMaturity
        ? GetFlatDiscountCurve(DiscountCurve, false)
        : DiscountCurve;
      var pv = PsCalculatePv(asOf, settle, discountCurve, false, true);
      if (payRecovAtMaturity)
        pv *= DiscountCurve.DiscountFactor(asOf, Product.Maturity);

      if (logger.IsDebugEnabled)
        LogDiffs(CfProtectionPv, pv, "Protection");

      return pv;
    }

    public double PsCalculatePv(Dt asOf, Dt settle,
   DiscountCurve discountCurve,
   bool includeFees, bool includeProtection)
    {
      var cds = CDS;
      DefaultSettlement ds;
      var ps = GeneratePayments(asOf, cds, 0.0, out ds);

      double pv = 0;

      if (!IsDefaulted)
      {
        var defaultRisk = GetDefaultRiskCalculator(asOf, settle);
        var recoveryRate = GetRecoveryRate();
        if (LegacyCashflowCompatible.TryGetValueOfWillDefault(
          ps, asOf, settle, discountCurve, defaultRisk,
          cds.Funded ? recoveryRate : (recoveryRate - 1),
          includeFees, includeProtection, DiscountingAccrued,
          out pv))
        {
          ds = null;
        }
        else
        {
          pv = RegularPv(ps, asOf, settle, discountCurve, defaultRisk,
            recoveryRate, cds.Funded, cds.CashflowFlag,
            includeFees, includeProtection && ds == null,
            IncludeSettlePayments, DiscountingAccrued);
        }
        if (includeProtection)
          pv += QuantoCapAdjustment;
        pv = pv * CurrentNotional;
      }

      // Add Upfront Fee PV
      if (includeFees)
      {
        pv += UpfrontFeePv(asOf, settle);
      }

      // Add default settlement
      if (ds != null && IsDefaultIncluded(
        ds.PayDt, settle, includeProtection))
      {
        double accrued = 0, amount = 0;
        if (includeFees)
        {
          if (ds.IsFunded)
            amount += ds.AccrualAmount;
          else
            accrued += ds.AccrualAmount;
        }
        if (includeProtection)
        {
          amount += ds.RecoveryAmount;
          if (IsDefaulted)
            accrued += QuantoCapAdjustment;
        }
        pv += Notional * (accrued +
          amount * DiscountCurve.Interpolate(asOf, ds.PayDt));
      }

      return pv;
    }

    public static double RegularPv(
      PaymentSchedule ps, Dt asOf, Dt settle,
      DiscountCurve discountCurve,
      DefaultRiskCalculator defaultRisk,
      double recoveryRate, bool funded, CashflowFlag flags,
      bool includeFees, bool includeProtection,
      bool includeSettlePayments, bool discountingAccrued)
    {
      const bool creditRiskToPaymentDate = false;
      var recoveryPayments = includeProtection
        ? ps.GetRecoveryPayments(settle, creditRiskToPaymentDate,
          dt => recoveryRate, funded, flags)
        : null;
      if (!includeFees)
      {
        // remove all the regular fee payments
        ps = new PaymentSchedule();
      }
      if (recoveryPayments != null && recoveryPayments.Count > 0)
        ps.AddPayments(recoveryPayments);
      if (!creditRiskToPaymentDate)
      {
        return ps.GroupByAccrualEnd().CalculatePv(asOf, settle, discountCurve,
          defaultRisk, includeSettlePayments, discountingAccrued, false);
      }
      return ps.CalculatePv(asOf, settle, discountCurve, defaultRisk,
        includeSettlePayments, discountingAccrued, false);
    }

    private bool IsDefaultIncluded(Dt payDate, Dt settle, bool withProtection)
    {
      if (withProtection && (PricerFlags & PricerFlags.NoDefaults) != 0)
        return false;

      if (IsDefaulted)
      {
        int cmp = Dt.Cmp(payDate, settle);
        return cmp > 0 || (cmp == 0 && IncludeSettlePayments);
      }
      Dt defaultDate = SurvivalCurve?.DefaultDate ?? Dt.Empty;
      return defaultDate >= settle;
    }

    private static bool IsDefaultIncluded(PricerFlags flags)
    {
      return (flags & PricerFlags.NoDefaults) != PricerFlags.NoDefaults;
    }

    public DefaultRiskCalculator GetDefaultRiskCalculator(
      Dt asOf, Dt settle)
    {
      var sc = DomesticSurvivalCurve;
      if (sc == null) return null;

      Debug.Assert(sc.CustomInterpolator == null
        || sc.DayCount == DayCount.None);
      var cds = CDS;
      Debug.Assert(cds != null);

      Dt maturity = cds.Maturity;
      if (IncludeMaturityProtection)
        maturity = maturity + 1;
      return new DefaultRiskCalculator(asOf, settle, maturity,
        sc, CounterpartyCurve, Correlation,
        cds.AccruedOnDefault,
        (cds.CashflowFlag & CashflowFlag.IncludeDefaultDate) != 0,
        sc.Stressed,
        StepSize, StepUnit);
    }



    #endregion

    #region Local Methods

    /// <summary>
    ///  Calculate breakeven fee for a CDS
    /// </summary>
    /// <param name="runningPremium">Running premium of CDS or NaN</param>
    /// <returns>Breakeven Fee</returns>
    private double CalcBreakEvenFee(double runningPremium)
    {
      CDS cds = CDS;
      CdsType cdsTypeCopy = cds.CdsType;
      double premiumCopy = cds.Premium;
      double bef;
      try
      {
        // Since UpFront(BEF) + CleanFee(Premium) = ProtectionLeg
        // We have: BEF = [(ProtectionLeg - CleanFee(Premium)]/Notional
        cds.CdsType = CdsType.Unfunded;
        if (!Double.IsNaN(runningPremium))
          cds.Premium = runningPremium;
        if (!premiumCopy.AlmostEquals(cds.Premium) || cdsTypeCopy != cds.CdsType)
          this.Reset();

        // Breakeven fee is calculated as if dealing with a new contract 
        // and for NA CDS, the fee settle should be 3 days later from asof
        double feeSettleDiscount = this.DiscountCurve.DiscountFactor(AsOf, Dt.AddDays(AsOf, 3, CDS.Calendar));
        bef = (ProtectionPv() + FlatFeePv() - UpfrontFeePv(AsOf, Settle)) / (Notional * feeSettleDiscount);
      }
      catch (Exception ex)
      {
        throw new ToolkitException("BreakevenFee calculation failed due to " + ex.ToString());
      }
      finally
      {
        // Restore the original CDS details if we need to
        if (!premiumCopy.AlmostEquals(cds.Premium) || cdsTypeCopy != cds.CdsType)
        {
          cds.CdsType = cdsTypeCopy;
          cds.Premium = premiumCopy;
          Reset();
        }
      }
      return -bef;
    }

    // Helper method to calculate the BEP
    private double CalcBreakEvenPremium(Dt forwardSettle, bool replicatingSpread)
    {
      return IsLegacyCashflowEnabled
        ? CfBreakEvenPremium(forwardSettle, replicatingSpread)
        : PsBreakEvenPremium(forwardSettle, replicatingSpread);
    }

    public double PsBreakEvenPremium(Dt forwardSettle, bool replicatingSpread)
    {
      double bep = Double.NaN;

      var cds = CDS;

      // Test of we are settling after Maturity
      if (forwardSettle > cds.Maturity)
        return 0.0;

      // Test forward settlement on or after product effective date
      if (cds.Effective > forwardSettle)
        forwardSettle = cds.Effective;

      // Store the original bonus
      var bullet = cds.Bullet;

      try
      {
        cds.Bullet = null;

        // Price stream of 1bp premiums matching CDS
        var ps = GeneratePayments01(AsOf, forwardSettle, SurvivalCurve, false);
        var defaultRisk = GetDefaultRiskCalculator(forwardSettle, forwardSettle);
        double fv01 = RegularPv(ps, forwardSettle, forwardSettle, DiscountCurve, defaultRisk,
          GetRecoveryRate(), false, CDS.CashflowFlag, true, false,
          false, DiscountingAccrued);

        double fwdProtection = (QuantoCapAdjustment + RegularPv(ps,
          forwardSettle, forwardSettle, DiscountCurve, defaultRisk,
          GetRecoveryRate(), false, CDS.CashflowFlag, false, true,
          false, DiscountingAccrued));
        const double tiny = double.Epsilon;
        if (Math.Abs(fv01) < tiny && Math.Abs(fwdProtection) < tiny)
          return 0.0; // this may happen when the curve is defaulted.
        double upFrontFee = replicatingSpread ? 0.0
          : UpfrontFeePv(forwardSettle, forwardSettle) / Notional;
        bep = ((-fwdProtection - upFrontFee) / fv01) / 10000;
      }
      finally
      {
        cds.Bullet = bullet;
      }
      return bep;
    }

    //
    // Called by ImpliedCounterpartyCurve root finder to find the flat annual
    // counterparty rate given a quoted duration.
    //
    private double EvaluateQuotedDuration(double x, out string exceptDesc)
    {
      double fv = 0.0;
      exceptDesc = null;

      logger.DebugFormat("Trying refi rate {0}", x);

      // build flat refi curve
      // Single annual refinancing probability
      Dt[] tenorDates = new[] { Dt.Add(AsOf, 1, TimeUnit.Years) };
      string[] tenorNames = new[] { "1Y" };
      double[] nonRefiProbs = new[] { 1.0 - x };
      SurvivalCurve refinancingCurve = SurvivalCurve.FromProbabilitiesWithBond(AsOf, Currency.None, null, InterpMethod.Weighted,
        ExtrapMethod.Const, tenorDates, nonRefiProbs, tenorNames, null, null, null, 0);
      SurvivalCurve.SurvivalCalibrator.CounterpartyCurve = refinancingCurve;
      SurvivalCurve.Fit();
      CounterpartyCurve = refinancingCurve;

      try
      {
        fv = RiskyDuration();
        logger.DebugFormat("Returning celan index market value {0} for quote {1}", fv, x);
      }
      catch (Exception ex)
      {
        exceptDesc = ex.Message;
      }

      return fv;
    }

    /// <summary>
    ///   Calculate the present value to asOf of any upfront fee settling on or after
    ///   settle date.
    /// </summary>
    public double UpfrontFeePv(Dt asOf, Dt settle)
    {
      Dt feeSettle = FeeSettle;
      if (Dt.Cmp(feeSettle, Settle) > 0 &&
        (!(SurvivalCurve.DefaultDate.IsValid()) ||
        (SurvivalCurve.DefaultDate.IsValid() && Dt.Cmp(SurvivalCurve.DefaultDate, feeSettle) <= 0)))
      {
        // Fee settles on or after settle and on or before any default
        return Fee() * Notional * DiscountCurve.DiscountFactor(asOf, feeSettle);
      }
      {
        return 0.0;
      }
    }

    #endregion Local Methods

    #region Properties

    /// <summary>
    /// Product to price
    /// </summary>
    public CDS CDS
    {
      get { return (CDS)Product; }
    }

    /// <summary>
    /// Fee settlement date
    /// </summary>
    /// <remarks>This will be retuired after a rework of pricer and product dates. RTD Sep09</remarks>
    public Dt FeeSettle
    {
      get { return feeSettle_.IsEmpty() ? CDS.FeeSettle : this.feeSettle_; }
      set { feeSettle_ = value; }
    }

    /// <summary>
    /// Boolean indicating recovery will be funded
    /// </summary>
    public bool RecoveryFunded
    {
      get { return recoveryFunded_; }
      set { recoveryFunded_ = value; Reset(); }
    }

    /// <summary>
    /// Bond product for recovery funding
    /// </summary>
    public Bond Bond
    {
      get { return bond_; }
      set { bond_ = value; Reset(); }
    }

    /// <summary>
    /// Swap product for recovery funding
    /// </summary>
    public SwapLeg SwapLeg
    {
      get { return swapLeg_; }
      set { swapLeg_ = value; Reset(); }
    }

    /// <summary>
    /// Market quote for CDS
    /// </summary>
    /// <details>
    ///   <para>CDS are quoted in terms of conventional spreads or
    ///   upfront fees.</para>
    /// </details>
    public double MarketQuote
    {
      get { return marketQuote_ == null ? 0 : (double)marketQuote_; }
      set { marketQuote_ = value; Reset(); }
    }

    /// <summary>
    ///   Quoting convention for CDS
    /// </summary>
    public QuotingConvention QuotingConvention
    {
      get { return quotingConvention_; }
      set { quotingConvention_ = value; Reset(); }
    }

    /// <summary>
    /// ISDA Converter AsOf date
    /// </summary>
    /// <remarks>
    /// <para>Used as input to ISDA Standard CDS converter to convert between Conventional Spread and Upfront Fee</para>
    /// <para>Typically this is the same as pricer AsOf, however the pricer AsOf is bumped when doing a Theta calculation
    /// and we need this date to remain fixed.</para>
    /// </remarks>
    public Dt ISDAConverterDate
    {
      get { return isdaConverterDate_; }
      set { isdaConverterDate_ = value; Reset(); }
    }

    /// <summary>
    /// ISDA discount curve used for settlement calculations
    /// </summary>
    public DiscountCurve ISDADiscountCurve
    {
      get { return isdaDiscountCurve_; }
      set { isdaDiscountCurve_ = value; Reset(); }
    }

    /// <summary>
    /// ISDA recovery rate used for settlement calculations
    /// </summary>
    public double ISDARecoveryRate
    {
      get { return isdaRecoveryRate_; }
      set { isdaRecoveryRate_ = value; Reset(); }
    }

    private bool UniformBump => _uniformBump;

    #endregion Properties

    #region QuantoProperties
    /// <summary>
    /// Check whether product is quanto
    /// </summary>
    private bool IsQuantoStructure
    {
      get { return  (FxCurve != null) && (FxVolatility != null) && (SurvivalCurve.Ccy != DiscountCurve.Ccy); }
    }

    /// <summary>
    /// Fx curve for Quanto product 
    /// </summary>
    public FxCurve FxCurve
    {
      get;
      private set;
    }

    /// <summary>
    /// Black volatility of forward FX
    /// </summary>
    public VolatilityCurve FxVolatility
    {
      get;
      private set;
    }

    /// <summary>
    /// <m>\rho</m> is correlation between the forward FX (from SurvivalCurve.Ccy to DiscountCurve.Ccy) and the default time </summary>
    public double FxCorrelation
    {
      get;
      set;
    }

    /// <summary>
    /// <m>\theta FX_{\tau-} = FX_{\tau - FX_{\tau-}}</m> is the jump of the FX (from SurvivalCurve.Ccy to DiscountCurve.Ccy) at default 
    /// </summary>
    public double FxDevaluation
    {
      get; 
      set;
    }

    /// <summary>
    /// Survival curve under the measure associated to domestic zero bond as numeraire asset
    /// </summary>
    private SurvivalCurve DomesticSurvivalCurve
    {
      get
      {
        if (IsQuantoStructure)
          return SurvivalCurve.ToDomesticForwardMeasure(CDS.Maturity,
            DiscountCurve, FxVolatility, FxCorrelation, FxDevaluation,
            StepSize, StepUnit);
        return SurvivalCurve;
      }
    }

    /// <summary>
    /// Quanto Cap adjustment to contingent leg
    /// </summary>
    public double QuantoCapAdjustment
    {
      get
      {
        if (IsQuantoStructure && (CDS.RecoveryCcy == SurvivalCurve.Ccy))
        {
          if(CDS.PayRecoveryAtMaturity)
            throw new ArgumentException("PayRecoveryAtMaturity flag not supported in Quanto structures");
          return
            SurvivalCurve.QuantoCapValue(DomesticSurvivalCurve,
            AsOf, ProtectionStart, GetPricingTimeGrid(), IncludeMaturityProtection,
            FxCurve, CDS.FixedRecovery ? CDS.FixedRecoveryRate : RecoveryRate,
            CDS.QuantoNotionalCap, CDS.FxAtInception, FxVolatility,
            FxCorrelation, FxDevaluation); //protection seller
        }
        return 0.0;
      }
    }

    public IList<Dt> GetPricingTimeGrid()
    {
      Dt asOf = AsOf, maturity = CDS.Maturity;
      return GenerateCashflow(null, asOf, CDS, 0.0)
        .Schedules.Select(s => s.Date).Where(d => d > asOf && d < maturity)
        .MergeTimeGrid(ProtectionStart, maturity, StepSize, StepUnit,
          StepUnit == TimeUnit.Days
            ? 1
            : (StepUnit == TimeUnit.Weeks ? 3 : 10));
    }

    #endregion

    #region IAnalyticDerivativesProvider methods

    /// <summary>
    /// Derivatives of the PV value w.r.t ordinates of the underlying curve 
    /// </summary>
    /// <param name="grad">grad is filled with the gradient of the present value wrt survival curve ordinates,
    /// <param name="hess">hess is filled with the hessian of the present value wrt survival curve ordinates</param>
    /// retVal[K..K + K*(K+1)/2-1] is the hessian wrt curve ordinates</param>
    public void PvDerivatives(double[] grad, double[] hess)
    {
      if (IsQuantoStructure)
        throw new NotImplementedException("Semianalytic derivatives not implemented for Quanto structures");
      PvDerivatives(ProtectionStart, grad, hess);
    }

    /// <summary>
    /// Derivatives of the fair spread  w.r.t ordinates of the underlying curve 
    /// </summary>
    /// <param name="grad">grad is filled with the gradient of the fair spread wrt survival curve ordinates,
    /// <param name="hess">hess is filled with the hessian of the fair spread wrt survival curve ordinates</param>
    /// retVal[K..K + K*(K+1)/2-1] is the hessian wrt curve ordinates</param>
    public void ParCouponDerivatives(double[] grad, double[] hess)
    {
      int n = grad.Length;
      var gradP = new double[n];
      var hessP = new double[n * (n + 1) / 2];
      var gradRd = new double[n];
      var hessRd = new double[n * (n + 1) / 2];
      var p = ProtectionPv() / Notional;
      var d = RiskyDuration() / Notional;
      ProtectionPvDerivatives(gradP, hessP);
      RiskyDurationDerivatives(gradRd, hessRd);
      CashflowModelDerivatives.RatioDerivatives(p, d, gradP, hessP, gradRd, hessRd, grad, hess);
      CashflowModelDerivatives.MultiplyByScalar(-1, grad, hess, grad, hess);
    }

    /// <summary>
    /// Derivatives of the PV value w.r.t ordinates of the underlying curve 
    /// </summary>
    ///<param name="forwardSettle">Forward start date</param>
    /// <param name="grad">grad is filled with the gradient of the present value wrt survival curve ordinates,
    /// <param name="hess">hess is filled with the hessian of the present value wrt survival curve ordinates</param>
    /// retVal[K..K + K*(K+1)/2-1] is the hessian wrt curve ordinates</param>
    public void PvDerivatives(Dt forwardSettle, double[] grad, double[] hess)
    {
      Cashflow cf = GenerateCashflow(null, AsOf, this.CDS, 0.0);
      CashflowModelDerivatives.PriceDerivatives(cf, AsOf, forwardSettle,
      DiscountCurve, SurvivalCurve, CounterpartyCurve, Correlation, true, true,
      IncludeSettlePayments, IncludeMaturityProtection, DiscountingAccrued,
      StepSize, StepUnit, cf.Count, grad, hess);
      return;
    }




    /// <summary>
    /// Derivatives of the risky duration value w.r.t ordinates of the underlying curve 
    /// </summary>
    /// <param name="grad">grad is filled with the gradient of the risky duration value wrt survival curve ordinates,
    /// <param name="hess">hess is filled with the hessian of the risky duration value wrt survival curve ordinates</param>
    /// retVal[K..K + K*(K+1)/2-1] is the hessian wrt curve ordinates</param>
    public void RiskyDurationDerivatives(double[] grad, double[] hess)
    {
      RiskyDurationDerivatives(ProtectionStart, grad, hess);
    }

    /// <summary>
    /// Derivatives of the risky duration value w.r.t ordinates of the underlying curve 
    /// </summary>
    ///<param name="forwardSettle">Forward start date</param>
    /// <param name="grad">grad is filled with the gradient of the risky duration value wrt survival curve ordinates,
    /// <param name="hess">hess is filled with the hessian of the risky duration value wrt survival curve ordinates</param>
    /// retVal[K..K + K*(K+1)/2-1] is the hessian wrt curve ordinates</param>
    public void RiskyDurationDerivatives(Dt forwardSettle, double[] grad, double[] hess)
    {
      Cashflow cashflow = new Cashflow();
      GenerateCashflow01(cashflow, AsOf, forwardSettle, false);
      int size = SurvivalCurve.Count;
      CashflowModelDerivatives.PriceDerivatives(cashflow, AsOf, forwardSettle, DiscountCurve,
            SurvivalCurve, CounterpartyCurve, Correlation, true,
            false, IncludeSettlePayments, IncludeMaturityProtection, DiscountingAccrued, StepSize, StepUnit, cashflow.Count, grad, hess);
      CashflowModelDerivatives.MultiplyByScalar(1e4, grad, hess, grad, hess);

    }


    /// <summary>
    /// Derivatives of the protection leg value w.r.t ordinates of the underlying curve 
    /// </summary>
    /// <param name="grad">grad is filled with the gradient of the protection value wrt survival curve ordinates,
    /// <param name="hess">hess is filled with the hessian of the protection value wrt survival curve ordinates</param>
    /// retVal[K..K + K*(K+1)/2-1] is the hessian wrt curve ordinates</param>
    public void ProtectionPvDerivatives(double[] grad, double[] hess)
    {
      ProtectionPvDerivatives(ProtectionStart, grad, hess);
    }


    /// <summary>
    /// Derivatives of the protection leg value w.r.t ordinates of the underlying curve 
    /// </summary>
    ///<param name="forwardSettle">Forward start date</param>
    /// <param name="grad">grad is filled with the gradient of the protection value wrt survival curve ordinates,
    /// <param name="hess">hess is filled with the hessian of the protection value wrt survival curve ordinates</param>
    /// retVal[K..K + K*(K+1)/2-1] is the hessian wrt curve ordinates</param>
    public void ProtectionPvDerivatives(Dt forwardSettle, double[] grad, double[] hess)
    {
      Dt asOf = AsOf;
      Cashflow cf = GenerateCashflow(null, asOf, this.CDS, 0.0);
      DiscountCurve discCurve = null;
      bool payRecovAtMaturity = this.Product is CDS && ((CDS)Product).PayRecoveryAtMaturity;
      if (payRecovAtMaturity)
        discCurve = GetFlatDiscountCurve(DiscountCurve, false);
      else
        discCurve = DiscountCurve;
      CashflowModelDerivatives.PriceDerivatives(cf, asOf, forwardSettle, discCurve, SurvivalCurve, CounterpartyCurve, Correlation, false, true, IncludeSettlePayments, IncludeMaturityProtection,
          DiscountingAccrued, StepSize, StepUnit, cf.Count, grad, hess);
      if (payRecovAtMaturity)
        CashflowModelDerivatives.MultiplyByScalar(DiscountCurve.DiscountFactor(Product.Maturity), grad, hess, grad, hess);
    }


    /// <summary>
    /// Tests whether pricer supports semi-analytic derivatives
    /// </summary>
    /// <returns>True is semi-analytic derivatives are supported</returns>
    bool IAnalyticDerivativesProvider.HasAnalyticDerivatives
    {
      get { return true; }
    }

    /// <summary>
    /// Returns the collection of semi-analytic derivatives w.r.t underlying reference curve
    /// </summary>
    /// <returns>IDerivativeCollection object</returns>
    IDerivativeCollection IAnalyticDerivativesProvider.GetDerivativesWrtOrdinates()
    {
      DerivativeCollection retVal = new DerivativeCollection(1);
      DerivativesWrtCurve der = new DerivativesWrtCurve(this.SurvivalCurve);
      retVal.Name = this.Product.Description;
      int k = this.SurvivalCurve.Count;
      double[] grad = new double[k];
      double[] hess = new double[k * (k + 1) / 2];
      bool alive = !this.IsDefaulted;
      if (alive)
        PvDerivatives(grad, hess);
      der.Gradient = grad;
      der.Hessian = hess;
      double delta = CDS.FixedRecovery ? CDS.FixedRecoveryRate : RecoveryRate;
      //vod
      der.Vod = alive ? AccruedOnSettleDefault() : 0;
      der.RecoveryDelta = 0.0;
      if (alive)
      {
        bool payRecovAtMaturity = this.CDS.PayRecoveryAtMaturity;
        if (!payRecovAtMaturity)
          der.Vod += UpfrontFeePv(AsOf, ProtectionStart) / Notional;
        Dt protectionSettle = payRecovAtMaturity ? Product.Maturity : Settle;
        double discountFac = DiscountCurve.DiscountFactor(AsOf, protectionSettle);
        double protectionPayment = 0.0;
        if (Dt.Cmp(Dt.Add(Settle, 1), ProtectionStart) >= 0)
          protectionPayment += (CDS.Funded ? -delta : (1 - delta)) * discountFac;
        der.Vod -= (protectionPayment + ProductPv() / Notional);
        double num = this.CfProtectionPv() / CurrentNotional;
        double den = CDS.Funded ? delta : delta - 1.0;
        der.RecoveryDelta = num / den;
      }
      else
      {
        if (!IsSettled)
        {
          double df = DiscountCurve.Interpolate(RecoveryCurve.DefaultSettlementDate) / DiscountCurve.Interpolate(AsOf);
          der.RecoveryDelta = df;
        }
      }
      retVal.Add(der);
      return retVal;
    }


    /// <summary>
    ///  Calculate the accrued when the name defaults on settle date,
    ///  which may differ from the normal accrued in that it can include
    ///  one day carry when paying accrual on default date is true.
    /// </summary>
    /// <remarks>
    ///   We go through cashflow to find the appropriate accrued, leaving 
    ///   to the cashflow generator all the complexities of coupon
    ///   calculations (fixed, floating, and possibly others).
    /// </remarks>
    /// <returns>The accrued on default.</returns>
    public double AccruedOnSettleDefault()
    {
      if (!settings_.CashflowPricer.IncludeAccruedOnDefaultAtSettle) return 0;

      Dt defaultDate = Settle;

      var pricer = (CDSCashflowPricer)ShallowCopy();
      pricer.Reset();

      var curve = pricer.SurvivalCurve = (SurvivalCurve)SurvivalCurve.Clone();
      curve.DefaultDate = defaultDate;
      curve.Defaulted = Defaulted.WillDefault;

      var cf = pricer.GenerateCashflow(null, AsOf, CDS, 0.0);
      int idx = cf.Count - 1;
      if(idx < 0)
      {
        logger.DebugFormat("No cashflow when default on Settle");
      }
      else if(defaultDate > cf.GetStartDt(idx))
      {
        return cf.GetAccrued(idx);
      }
      return 0;
    }
    #endregion

    #region ResetAction

    /// <summary>Quote (price or yield) changed</summary>
    public static readonly ResetAction QuoteChanged = new ResetAction();

    #endregion ResetAction

    #region Data

    // Note: If you add any properties here make sure the Clone() is updated as necessary.
    //
    private Dt feeSettle_ = Dt.Empty;
    private object marketQuote_;
    private QuotingConvention quotingConvention_ = QuotingConvention.None;
    private bool recoveryFunded_;
    private Dt isdaConverterDate_;
    private DiscountCurve isdaDiscountCurve_;
    private double isdaRecoveryRate_;
    private Bond bond_;
    private SwapLeg swapLeg_;
    private bool _uniformBump = false;


    // Results cache
    // Note: If you add any properties here make sure Reset() clears them.
    //
    private object fee_;
    private object conventionalSpread_;
    #endregion Data

    #region IRateResetsUpdater Members

    RateResets IRatesLockable.LockedRates
    {
      get { return new RateResets(RateResets); }
      set { RateResets.Initialize(value); }
    }

    IEnumerable<RateReset> IRatesLockable.ProjectedRates
    {
      get
      {
        if (CDS.CdsType != CdsType.FundedFloating)
          return null;
        DefaultSettlement ds;
        return GeneratePayments(AsOf, CDS, Fee(), out ds)
          .EnumerateProjectedRates();
      }
    }

    #endregion
  }
}
