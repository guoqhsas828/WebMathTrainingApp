/*
 * BondPricer.cs
 * Bond pricer
 *  -2014. All rights reserved.
 *
 * TBD: Expose accrued and other cached values (accrued, price, yield) as arguments to the Model. RTD Nov'07
 * TBD: Add Pv(Dt toDate) method in Callable Bond Model. RTD Nov'07
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Calibrators.Volatilities;
using BaseEntity.Toolkit.Numerics;
using BaseEntity.Toolkit.Sensitivity;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Models.BGM;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Util;
using BaseEntity.Toolkit.Util.Configuration;
using StockDividends = BaseEntity.Toolkit.Models.ConvertibleBondIntersectingTreesModel.StockCorrelatedModel.StockDividends;
using CashflowAdapter = BaseEntity.Toolkit.Cashflows.CashflowAdapter;

namespace BaseEntity.Toolkit.Pricers
{

  #region config

  /// <summary>
  /// Config settings class for the BondPricer 
  /// </summary>
  [Serializable]
  public class BondPricerConfig
  {
    /// <exclude />
    [ToolkitConfig("backward compatibility flag , determines whether we need to discount the accrual or not")] public readonly bool DiscountingAccrued = true;

    /// <exclude />
    [ToolkitConfig("Allow Negative Spreads flag, determines whether we allow negative spreads while implying a CDS Curve")] public readonly bool
      AllowNegativeCDSSpreads = true;

    /// <exclude />
    [ToolkitConfig("Use the backward compatible cashflow without any new features.")] public readonly bool BackwardCompatibleCashflows = false;

    /// <exclude />
    [ToolkitConfig("Use the backward compatible z-spread calculation (i.e., applying the volatility to rate plus z-spread).")] public readonly bool
      BackwardCompatibleCallableZSpread = false;
  }

  #endregion

  /// <summary>
  /// <para>Price a <see cref="BaseEntity.Toolkit.Products.Bond">Bond</see></para>
  /// </summary>
  /// <remarks>
  /// <para>Provides both market standard pricing as well as credit relative value analysis for bonds.</para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.Bond" />
  /// 
  /// <para><h2>Standard Bond Market Calculations</h2></para>
  /// <para>A comprehensive range of standard bond market calculations are supported. A small sample set
  /// includes:</para>
  /// <list type="bullet">
  ///   <item><description><see cref="AccruedInterest()">Accrued interest</see>.</description></item>
  ///   <item><description><see cref="FullPrice()">Full price</see>.</description></item>
  ///   <item><description><see cref="YieldToMaturity()">Yield to maturity</see>.</description></item>
  ///   <item><description><see cref="YieldToCall()">Yield to call</see>.</description></item>
  ///   <item><description><see cref="YieldToPut()">Yield to put</see>.</description></item>
  ///   <item><description><see cref="YieldToWorst()">Yield to worst</see>.</description></item>
  ///   <item><description><see cref="PV01()">Price value of a yield 01</see>.</description></item>
  ///   <item><description><see cref="Duration()">Duration</see>.</description></item>
  ///   <item><description><see cref="ModDuration()">Modified duration</see>.</description></item>
  ///   <item><description><see cref="WAL()">Weighted average life</see>.</description></item>
  ///   <item><description><see cref="Convexity()">Convexity</see>.</description></item>
  /// </list>
  /// <para>See <see cref="BondModel">market standard bond model</see> for vanilla market standard bond calculation
  /// details.</para>
  /// 
  /// <para><h2>Credit Relative Value Calculations</h2></para>
  /// <para>A wide range of relative calculations allow direct comparison between bonds and other
  /// credit products. The approaches used match those common in the credit markets.</para>
  /// <para>Some examples of the calculations supported include:</para>
  /// <list type="bullet">
  ///   <item><description><see cref="Pv()">Model implied price of bond</see>.</description></item>
  ///   <item><description><see cref="ImpliedCDSSpread()">Implied CDS spread (from bond price and credit curve)</see>.</description></item>
  ///   <item><description><see cref="ImpliedCDSLevel()">Implied CDS level (from bond price)</see>.</description></item>
  ///   <item><description><see cref="CalcRSpread(bool)">R-Spread</see>.</description></item>
  ///   <item><description><see cref="Spread01()">Spread sensitivity</see>.</description></item>
  /// </list>
  /// <para>Relative value calculations are based on the generalised contingent cashflow model. These calculations
  /// use the specified interest rate curve and credit curve. If no credit curve is specified, a flat credit curve
  /// is implied from the bond price and recovery rate. In this case the recovery rate must be specified.</para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Models.CashflowModel" />
  /// <para>See the <see cref="BaseEntity.Toolkit.Models.CashflowModel">General Cashflow Model</see> for more
  /// calculation details.</para>
  ///
  /// <para><h2>Callable Bond Calculations</h2></para>
  /// <para>For callable bonds, either a Hull-White, Black-Karasinski or a BGM model is used.
  /// Some examples of the calculations supported include:</para>
  /// <list type="bullet">
  ///   <item><description><see cref="OptionPrice()">Value of any embedded call option</see>.</description></item>
  ///   <item><description><see cref="Vega()">Sensitivity of bond price to call option volatility</see>.</description></item>
  /// </list>
  /// <inheritdoc cref="BaseEntity.Toolkit.Models.HullWhiteTreeCashflowModel" />
  /// <para>See <see cref="BaseEntity.Toolkit.Models.HullWhiteTreeCashflowModel">Callable bond trinomial tree model</see> for more
  /// details re the calculations.</para>
  /// <para>The BGM model is a cashflow model based on BGM tree.</para>
  ///
  /// <para><h2>Convertible Bond Calculations</h2></para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Models.ConvertibleBondIntersectingTreesModel" />
  /// <para>Some examples of the calculations supported include:</para>
  /// <list type="bullet">
  ///   <item><description><see cref="BondFloor()">Convertible bond floor</see>.</description></item>
  ///   <item><description><see cref="Parity()">Convertible bond parity</see>.</description></item>
  ///   <item><description><see cref="ConversionPrice()">Convertible bond conversion price</see>.</description></item>
  /// </list>
  /// <para>See <see cref="BaseEntity.Toolkit.Models.ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
  /// for more details re the calculations.</para>
  /// 
  /// <para><h2>References:</h2></para>
  /// <list type="bullet">
  ///   <item>The CSFB Guide to Yield Calculations in the International
  ///     Bond &amp; Money Markets - Credit Suisse First Boston</item>
  ///   <item>Money Market &amp; Bond Calculations - Stigum &amp; Robinson</item>
  ///   <item><see href="http://www.fin.gc.ca/invest/bondprice-eng.asp">Government of Canada Securities - Technical Guide</see></item>
  ///   <item>Handbook of Global Fixed Income Calculations - Dragomir Krgin</item>
  /// </list>
  /// </remarks>
  /// <seealso cref="Bond">Bond Product</seealso>
  /// <seealso cref="BondModel">Standard market bond calculations</seealso>
  /// <seealso cref="CashflowModel">Cashflow pricing model</seealso>
  /// <seealso cref="HullWhiteTreeCashflowModel">Callable bond trinomial tree model</seealso>
  /// <seealso cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</seealso>
  [Serializable]
  public partial class BondPricer : PricerBase, ICashflowPricer, IRatesLockable, IRepoAssetPricer
  {
    private static readonly log4net.ILog logger = log4net.LogManager.GetLogger(typeof(BondPricer));

    #region Constructors

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="bond">Bond to price</param>
    public BondPricer(Bond bond)
      : this(bond, Dt.Empty)
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="bond">Bond to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    public BondPricer(Bond bond, Dt asOf)
      : this(bond, asOf, asOf)
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="bond">Bond to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    public BondPricer(Bond bond, Dt asOf, Dt settle)
      : this(bond, asOf, settle, null, null, 0, TimeUnit.None, Double.NaN)
    {}

    /// <summary>
    ///   Constructor
    /// </summary>
    /// <param name="product">Product to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurve">Survival Curve for pricing</param>
    /// <param name="stepSize">Step size for pricing grid (0 for default)</param>
    /// <param name="stepUnit">Units for step size (None for default)</param>
    /// <param name="recoveryRate">Recovery rate (or -1 to use recovery from <paramref name="survivalCurve"/>)</param>
    public BondPricer( Bond product, Dt asOf, Dt settle, DiscountCurve discountCurve, SurvivalCurve survivalCurve,
      int stepSize, TimeUnit stepUnit, double recoveryRate )
      : this(product, asOf, settle, discountCurve, survivalCurve,
             stepSize, stepUnit, recoveryRate, 0.0, 0.0, false)
    {}

    /// <summary>
    ///   Construct a callable nond pricer, evaluating it with Hull-White model if the calls are no ignored.
    /// </summary>
    /// <param name="product">Product to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="survivalCurve">Survival Curve for pricing</param>
    /// <param name="stepSize">Step size for pricing grid (0 for default)</param>
    /// <param name="stepUnit">Units for step size (None for default)</param>
    /// <param name="recoveryRate">Recovery rate (or -1 to use recovery from <paramref name="survivalCurve"/>)</param>
    /// <param name="meanReversion">Short rate (constant) mean-reversion </param>
    /// <param name="sigma">Short rate (constant) volatility</param>
    /// <param name="ignoreCall">Ignore bond callability</param>
    public BondPricer( Bond product, Dt asOf, Dt settle, DiscountCurve discountCurve, SurvivalCurve survivalCurve,
      int stepSize, TimeUnit stepUnit, double recoveryRate, double meanReversion, double sigma, bool ignoreCall )
      : base(product, asOf, settle)
    {
      discountCurve_ = discountCurve;
      survivalCurve_ = survivalCurve;
      recoveryCurve_ = (recoveryRate >= 0.0 && !asOf.IsEmpty()) ? new RecoveryCurve(asOf, recoveryRate) : null;
      stepSize_ = stepSize;
      stepUnit_ = stepUnit;
      meanReversion_ = meanReversion;
      sigma_ = sigma;
      _callableModel = ignoreCall ? CallableBondPricingMethod.IgnoreCall : CallableBondPricingMethod.BlackKarasinski;
      discountingAccrued_ = settings_.BondPricer.DiscountingAccrued;
      allowNegativeCDSSpreads_ = settings_.BondPricer.AllowNegativeCDSSpreads;
      TradeSettle = settle;
      ProductSettle = settle;
      ForwardSettle = settle;
      return;
    }

    /// <summary>
    ///   Construct a callable bond pricer, evaluating it with LIBOR market model if the calls are not ignored.
    /// </summary>
    /// <param name="product">The product, bond</param>
    /// <param name="asOf">The pricing date</param>
    /// <param name="settle">The pricing settle date</param>
    /// <param name="discountCurve">The discount curve</param>
    /// <param name="survivalCurve">The survival curve</param>
    /// <param name="stepSize">Step size for pricing grid (0 for default)</param>
    /// <param name="stepUnit">Units for step size (None for default)</param>
    /// <param name="recoveryRate">Recovery rate (or -1 to use recovery from <paramref name="survivalCurve"/>)</param>
    /// <param name="volatilityObject">The volatility object</param>
    /// <param name="ignoreCall">Flag to indicate ignore the callable feature or not. False means suing LMM callable model</param>
    public BondPricer(Bond product, Dt asOf, Dt settle, DiscountCurve discountCurve, SurvivalCurve survivalCurve,
      int stepSize, TimeUnit stepUnit, double recoveryRate, IVolatilityObject volatilityObject, bool ignoreCall)
      : this(product, asOf, settle,discountCurve,survivalCurve, stepSize, stepUnit, recoveryRate, volatilityObject,
          ignoreCall ? CallableBondPricingMethod.IgnoreCall : CallableBondPricingMethod.LiborMarket)
    {
    }

    /// <summary>
    /// Create a callable bond pricer.
    /// </summary>
    /// <param name="product">The product</param>
    /// <param name="asOf">The pricing as-of date</param>
    /// <param name="settle">The pricing settle date</param>
    /// <param name="discountCurve">The discount curve</param>
    /// <param name="survivalCurve">The survival curve</param>
    /// <param name="stepSize">Step size for pricing grid (0 for default)</param>
    /// <param name="stepUnit">Units for step size (None for default)</param>
    /// <param name="recoveryRate">Recovery rate (or -1 to use recovery from <paramref name="survivalCurve"/>)</param>
    /// <param name="callableModel">Callable bond model. Such as IgnoreCall, 
    /// YieldToWorst, HullWhite, and LiborMarket.</param>
    public BondPricer(Bond product, Dt asOf, Dt settle, DiscountCurve discountCurve,
      SurvivalCurve survivalCurve, int stepSize, TimeUnit stepUnit, double recoveryRate,
      CallableBondPricingMethod callableModel) : this(product, asOf, settle, discountCurve, survivalCurve,
        stepSize, stepUnit, recoveryRate, null, callableModel)
    {
    }

    private BondPricer(Bond product, Dt asOf, Dt settle, DiscountCurve discountCurve, SurvivalCurve survivalCurve,
      int stepSize, TimeUnit stepUnit, double recoveryRate, IVolatilityObject volatilityObject, CallableBondPricingMethod callableModel)
      : base(product, asOf, settle)
    {
      discountCurve_ = discountCurve;
      survivalCurve_ = survivalCurve;
      recoveryCurve_ = (recoveryRate >= 0.0 && !asOf.IsEmpty()) ? new RecoveryCurve(asOf, recoveryRate) : null;
      stepSize_ = stepSize;
      stepUnit_ = stepUnit;
      volatilityObject_ = volatilityObject;
      _callableModel = callableModel;
      discountingAccrued_ = settings_.BondPricer.DiscountingAccrued;
      allowNegativeCDSSpreads_ = settings_.BondPricer.AllowNegativeCDSSpreads;
      TradeSettle = settle;
      ProductSettle = settle;
      ForwardSettle = settle;
      return;
    }

    /// <summary>
    ///  Convertible bond pricer constructor
    /// </summary>
    /// <param name="product">Convertible bond</param>
    /// <param name="asOf">Pricing date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discoutn curve</param>
    /// <param name="survivalCurve">Survival curve</param>
    /// <param name="stepSize">Step size (3, 6, etc.)</param>
    /// <param name="stepUnit">Step unit (month etc.)</param>
    /// <param name="recoveryRate">Recovery rate on bond default</param>
    /// <param name="s0">Initial stock price</param>
    /// <param name="sigmaS">Stock volatility</param>
    /// <param name="divYield">Continuous dividend yield of stock</param>
    /// <param name="rho">Correlation between stock and interest rate</param>
    /// <param name="kappa">Mean conversion speed of interest rate</param>
    /// <param name="sigmaR">Interest rate volatility</param>
    /// <param name="n">Number of time steps</param>
    /// <param name="model">The short rate model for convertible bond pricing</param>
    public BondPricer( Bond product, Dt asOf, Dt settle, DiscountCurve discountCurve, SurvivalCurve survivalCurve,
      int stepSize, TimeUnit stepUnit, double recoveryRate, double s0, double sigmaS,
      StockDividends divYield, double rho, double kappa, double sigmaR, int n,
      ShortRateModelType model = ShortRateModelType.BlackKarasinski)
      : base(product, asOf, settle)
    {
      discountCurve_ = discountCurve;
      survivalCurve_ = survivalCurve;
      recoveryCurve_ = (recoveryRate >= 0.0 && !asOf.IsEmpty()) ? new RecoveryCurve(asOf, recoveryRate) : null;
      stepSize_ = stepSize;
      stepUnit_ = stepUnit;
      s0_ = s0;
      sigmaS_ = sigmaS;
      dividends_ = divYield;
      rho_ = rho;
      kappa_ = kappa;
      sigmaR_ = sigmaR;
      //spread_ = spread;
      n_ = n;
      _callableModel = CallableBondPricingMethod.None;
      _convertibleModel = model;
      discountingAccrued_ = settings_.BondPricer.DiscountingAccrued;
      ProductSettle = settle;
      TradeSettle = settle;
      ForwardSettle = settle;
      return;
    }

    #endregion Constructors

    #region Work bond construction

    private Bond WorkBond
    {
      get
      {
        return IsYieldToWorstCallableModel
          ? (_workBond ?? (_workBond = GetWorkBond(Bond, AsOf, Settle, MarketQuote,
          QuotingConvention)))
          : Bond;
      }
    }

    public static Bond GetWorkBond(Bond bond, Dt asOf, Dt settle, double quote,
      QuotingConvention quoteConvention)
    {
      var callSchedules = bond.CallSchedule
        .GetActivePeriods(asOf, bond.GetNotificationDays());

      var call = callSchedules
        .Where(cs => cs.StartDate > settle && cs.StartDate < bond.Maturity)
        .Select(s => s).FirstOrDefault();
      if (call == null) return bond;

      if (quoteConvention == QuotingConvention.FlatPrice)
        return quote < call.CallPrice ? bond : SetWorkBond(bond, call);

      var min = CalcBondPriceFromYield(bond, asOf, settle, quote);
      var nBond = SetWorkBond(bond, call);
      var price = CalcBondPriceFromYield(nBond, asOf, settle, quote);
      if (price < min) bond = nBond;
      return bond;
    }

    private static double CalcBondPriceFromYield(Bond bond,
      Dt asOf, Dt settle, double yield)
    {
      var pricer = new BondPricer(bond, asOf, settle)
      {
        MarketQuote = yield,
        QuotingConvention = QuotingConvention.Yield
      };

      return pricer.CalculatePriceFromYield(bond);
    }

    private static Bond SetWorkBond(Bond bond, CallPeriod call)
    {
      var nb = (Bond)bond.Clone();
      ChangeMaturity(nb, call.StartDate, call.CallPrice,
        bond.FirstCoupon, bond.LastCoupon);
      return nb;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    ///   Clone
    /// </summary>
    public override object Clone()
    {
      // Clone
      BondPricer obj = (BondPricer)base.Clone();

      obj.Reset();
      obj.discountCurve_ = (discountCurve_ != null) ? (DiscountCurve)discountCurve_.Clone() : null;
      // Careful with clone of reference curve as this may be shared with discount curve.
      if (referenceCurve_ == discountCurve_)
        obj.referenceCurve_ = obj.discountCurve_;
      else
        obj.referenceCurve_ = (referenceCurve_ != null) ? (DiscountCurve)referenceCurve_.Clone() : null;
      obj.rateResetsList_ = CloneUtil.Clone(rateResetsList_);
      obj.survivalCurve_ = (survivalCurve_ != null) ? (SurvivalCurve)survivalCurve_.Clone() : null;
      obj.recoveryCurve_ = (recoveryCurve_ != null) ? (RecoveryCurve)recoveryCurve_.Clone() : null;
      obj.RepoCurve = (RepoCurve != null) ? (RateQuoteCurve)RepoCurve.Clone() : null;
      return obj;
    }

    /// <summary>
    /// The Payment pricer
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

    /// <summary>
    ///   Reset the pricer
    /// </summary>
    /// <remarks>
    ///   <para>There are some pricers which need to remember some public state
    ///   in order to skip redundant calculation steps. This method is provided
    ///   to indicate that this internate state should be cleared.</para>
    /// </remarks>
    public override void Reset()
    {
      convertModel_ = null;
      ResetCashflow();
      ResetTradeCashflow();
      ResetSpotSettleCashflow();
      currentCoupon_ = null;
      nextCouponDate_ = null;
      prevCouponDate_ = null;
      remainingCoupons_ = null;
      accrualDays_ = null;
      accruedInterest_ = null;
      fullPrice_ = null;
      yield_ = null;
      rspread_ = null;
      hspread_ = null;
      cashflowAmounts_ = null;
      cashflowPrincipals_ = null;
      cashflowPeriodFractions_ = null;
      productSettle_ = Dt.Empty;
      protectionStart_ = Dt.Empty;
      _workBond = null;
      base.Reset();
      return;
    }

    private void ResetCashflow()
    {
      CfReset(); //ccr pricer still needs to reset the cashflow.
      _paymentSchedule = null;
      _cfAdapter = null;
    }

    private void ResetTradeCashflow()
    {
      TradeCfReset();
      _tradePaymentSchedule = null;
      _tradeCfAdapter = null;
    }

    private void ResetSpotSettleCashflow()
    {
      SpotCfReset();
      _spotSettlePaymentSchedule = null;
      _spotSettleCfAdapter = null;
    }

    /// <summary>
    /// Reset the pricer based on selected results changed
    /// </summary>
    /// <remarks>
    ///   <para>Some pricers need to remember certain public states in order
    ///   to skip redundant calculation steps.
    ///   This function tells the pricer that what attributes of the products
    ///   and other data have changed and therefore give the pricer an opportunity
    ///   to selectively clear/update its public states.  When used with caution,
    ///   this method can be much more efficient than the method Reset() without argument,
    ///   since the later resets everything.</para>
    ///   <para>The default behaviour of this method is to ignore the parameter
    ///   <paramref name="what"/> and simply call Reset().  The derived pricers
    ///   may implement a more efficient version.</para>
    /// </remarks>
    /// <param name="what">The flags indicating what attributes to reset</param>
    public override void Reset(ResetAction what)
    {
      if (what == QuoteChanged)
      {
        fullPrice_ = null;
        yield_ = null;
        rspread_ = null;
        hspread_ = null;
        _workBond = null;
      }
      else
        base.Reset(what);
      return;
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
      if (discountCurve_ == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));
      if (stepSize_ < 0.0)
        InvalidValue.AddError(errors, this, "StepSize", String.Format("Invalid step size. Must be >= 0, not {0}", stepSize_));
      if (Bond.Floating && referenceCurve_ == null)
        InvalidValue.AddError(errors, this, "ReferenceCurve", "Floating bond missing reference curve");
      if (Bond.Callable && !IgnoreCall)
      {
        if (meanReversion_ < -2.0 || meanReversion_ > 2.0)
          InvalidValue.AddError(errors, this, "MeanReversion", String.Format("Invalid mean reversion. Must be between -2 and 2, not {0}", meanReversion_));
        if (sigma_ < 0.0)
          InvalidValue.AddError(errors, this, "Sigma", String.Format("Invalid sigma. Must be between +ve, not {0}", sigma_));
        if (quotingConvention_ == QuotingConvention.RSpread)
          InvalidValue.AddError(errors, this, "QuotingConvention", String.Format("R Spread Quoting Convention is not supported for Callable bonds "));
      }
      if (rateResetsList_ != null) rateResetsList_.Validate(errors);
    }

    /// <summary>
    /// True if product is active. This function checks if the bond has matured, but also takes into account if the bond has defaulted,
    /// and in that case, the bond stated maturity date is no longer relevant, and what is relevant is the assumed default settlement date.
    /// </summary>
    public bool IsProductActive()
    {
      return IsProductActive(Settle);
    }

    public bool IsProductActive(Dt settle)
    {
      if (IsDefaulted(settle))
        return (!DefaultPaymentDate.IsValid() || settle <= DefaultPaymentDate);
      return (settle <= WorkBond.Maturity);
    }

    private bool IsOnLastDate()
    {
      return IsOnLastDate(Settle);
    }

    public bool IsOnLastDate(Dt settle)
    {
      if (IsDefaulted(settle))
      {
        if (DefaultPaymentDate.IsValid() && settle == DefaultPaymentDate)
          return true;
        return false;
      }
      return (settle == WorkBond.Maturity);
    }

    /// <summary>
    /// Market standard Frequency and DayCount for the floating leg of an Asset Swap, determined by Currency. 
    /// <list type="table">
    /// <listheader><term>Ccy</term><term>Frequency</term><term>Daycount</term></listheader>
    /// <item><term>USD</term><term>Quarterly</term><term>Actual360</term></item>
    /// <item><term>EUR</term><term>SemiAnnual</term><term>Actual360</term></item>
    /// <item><term>JPY</term><term>SemiAnnual</term><term>Actual360</term></item>
    /// <item><term>GBP</term><term>SemiAnnual</term><term>Actual365Fixed</term></item>
    /// <item><term>CAD</term><term>Quarterly</term><term>Actual365Fixed</term></item>
    /// <item><term>AUD</term><term>Quarterly</term><term>Actual365Fixed</term></item>
    /// <item><term>Other</term><term>SemiAnnual</term><term>Actual365Fixed</term></item>
    /// </list>
    /// </summary>
    /// <param name="ccy"></param>
    /// <param name="freq"></param>
    /// <param name="dc"></param>
    public static void DefaultAssetSwapParams(Currency ccy, out Frequency freq, out DayCount dc)
    {
      switch (ccy)
      {
        case Currency.USD:
          dc = DayCount.Actual360;
          freq = Frequency.Quarterly;
          break;
        case Currency.EUR:
        case Currency.JPY:
          dc = DayCount.Actual360;
          freq = Frequency.SemiAnnual;
          break;
        case Currency.CAD:
        case Currency.AUD:
          dc = DayCount.Actual365Fixed;
          freq = Frequency.Quarterly;
          break;
        case Currency.GBP:
          dc = DayCount.Actual365Fixed;
          freq = Frequency.SemiAnnual;
          break;
        default:
          dc = DayCount.Actual365Fixed;
          freq = Frequency.SemiAnnual;
          break;
      }
    }

    #endregion Utilty Methods

    #region Bond Market Methods

    /// <summary>
    ///   Get the current effective coupon rate (at the settlement date)
    /// </summary>
    /// <remarks>
    ///   <inheritdoc cref="CurrentCoupon(bool, Dt)"/>
    /// </remarks>
    /// <returns>Current effective coupon rate</returns>
    public double CurrentCoupon()
    {
      var cpn = CurrentCoupon(true, Settle);
      return cpn == null ? 0.0 : cpn.Value;
    }

    /// <inheritdoc cref="CurrentCoupon(bool, Dt)"/>
    /// <param name="settle">Settlement date</param>
    public double CurrentCoupon(Dt settle)
    {
      var cpn = CurrentCoupon(true, settle);
      return cpn == null ? 0.0 : cpn.Value;
    }

    /// <summary>
    ///   Calculate the coupon rate at the specified date
    /// </summary>
    /// <remarks>
    ///   <para>Supports amortizing and floating rate bonds and bonds with customized schedules.</para>
    ///   <para>Does not support forward settlement for floating rate bonds or
    ///   historical settlement for floating rate bonds when the rate resets have
    ///   note been set.</para>
    /// </remarks>
    /// <param name="includeProjection">Whether or not to project floating rate</param>
    /// <param name="settle">settle date</param>
    /// <returns>Coupon rate at specified date</returns>
    public double? CurrentCoupon(bool includeProjection, Dt settle)
    {
      if (Bond == null)
        return null;

      if (!Bond.Floating)
      {
        return this.FixedCouponAt(settle, Bond.Coupon, Bond.CouponSchedule);
      }
      else
      {
        var cpn = this.FloatingCouponAt(AsOf, settle, RateResets, includeProjection);
        if (cpn.HasValue)
          return cpn.Value;
        else if (settle <= Bond.Effective)
          return CurrentFloatingRate + Bond.Coupon;
        else
        {
          return null;
        }
      }
    }

    /// <summary>
    ///   Get next coupon date
    /// </summary>
    /// <returns>Next coupon date or empty date if no next coupon</returns>
    public Dt NextCouponDate()
    {
      if (nextCouponDate_ == null)
        nextCouponDate_ = NextCouponDate(this.Settle);
      return (Dt)nextCouponDate_;
    }

    /// <summary>
    ///   Get next coupon date
    /// </summary>
    /// <param name="settle">Settlement date</param>
    /// <returns>Next coupon date on or after specified settlement date or empty date if no next coupon</returns>
    public Dt NextCouponDate(Dt settle)
    {
      return Schedule.NextCouponDate(WorkBond.ScheduleParams, settle);
    }

    /// <summary>
    ///   Get the next unrolled coupon date.
    /// </summary>
    /// <param name="settle">The settle.</param>
    /// <returns></returns>
    private Dt NextCycleDate(Dt settle)
    {
      return Schedule.NextCycleDate(WorkBond.ScheduleParams, settle);
    }

    /// <summary>
    ///   Get previous coupon date
    /// </summary>
    /// <returns>Previous coupon date or empty date if no previous coupon</returns>
    public Dt PreviousCouponDate()
    {
      if (prevCouponDate_ == null)
        prevCouponDate_ = PreviousCouponDate(this.Settle);
      return (Dt)prevCouponDate_;
    }

    /// <summary>
    ///   Get previous coupon date
    /// </summary>
    /// <param name="settle">Settlement date</param>
    /// <returns>Coupon date before specified settlement date or empty date if no previous coupon</returns>
    public Dt PreviousCouponDate(Dt settle)
    {
      return Schedule.PreviousCouponDate(WorkBond.ScheduleParams, settle);
    }

    /// <summary>
    ///   Get the Previous Cycle date 
    /// </summary>
    /// <param name="settle">Settlement date </param>
    /// <returns>Cycle date before the specified settlement date </returns>
    public Dt PreviousCycleDate(Dt settle)
    {
      var bond = WorkBond;
      Schedule schedule = bond.Schedule;
      Dt prevCycle = Dt.Empty;
      int previousCycleIdx = schedule.GetPrevCouponIndex(settle);
      if (previousCycleIdx < 0)
      {
        if (schedule.Periods[0].CycleEnd <= settle)
          prevCycle = schedule.Periods[0].CycleEnd;
        else if (schedule.Periods[0].CycleBegin <= settle)
          prevCycle = schedule.Periods[0].CycleBegin;
        else
        {
          Dt nextCycleDate = NextCycleDate(settle);
          if (schedule.CycleRule != CycleRule.FRN)
            prevCycle = Dt.Add(nextCycleDate, schedule.Frequency, -1, schedule.CycleRule);
          else
            prevCycle = Dt.Subtract(nextCycleDate, schedule.Frequency, bond.EomRule);
        }
      }
      else
      {
        int last = schedule.Count - 1;
        if (previousCycleIdx < last &&
            schedule.Periods[previousCycleIdx + 1].CycleEnd <= settle)
        {
          prevCycle = schedule.Periods[previousCycleIdx + 1].CycleEnd;
        }
        else
          prevCycle = schedule.Periods[previousCycleIdx].CycleEnd;
      }
      return prevCycle;
    }

    /// <summary>
    ///   Return the previous cycle date 
    /// </summary>
    /// <returns></returns>
    public Dt PreviousCycleDate()
    {
      return PreviousCycleDate(this.Settle);
    }

    /// <summary>
    ///   Get number of remaining coupons
    /// </summary>
    /// <returns>Number of remaining coupons from settlement date</returns>
    public int RemainingCoupons()
    {
      if (remainingCoupons_ == null)
        remainingCoupons_ = RemainingCoupons(this.Settle);

      return (int)remainingCoupons_;
    }

    /// <summary>
    ///   Get number of remaining coupons
    /// </summary>
    /// <param name="settle">Settlement date</param>
    /// <returns>Number of remaining coupons after specified settlement date</returns>
    public int RemainingCoupons(Dt settle)
    {
      return Schedule.RemainingCoupons(WorkBond.ScheduleParams, settle);
    }

    /// <summary>
    ///   Get number of days of accrual
    /// </summary>
    /// <returns>Number of accrual days from last coupon date to settlement</returns>
    public int AccrualDays()
    {
      if (accrualDays_ == null)
      {
        if (HasDefaulted)
          accrualDays_ = 0;
        else
        {
          accrualDays_ = BondModelUtil.AccrualDays(this.Settle, WorkBond, 
            WorkBond.CumDiv(Settle, Settle), IgnoreExDivDateInCashflow);
        }
      }
      return (int)accrualDays_;
    }

    /// <summary>
    ///   Calculate the number of days of accrual based on bond settlement date
    /// </summary>
    /// <remarks>
    /// <para>Calculates the number of accural days from the settlement date.
    /// The date calculation is based on the <see cref="DayCount"> convention
    /// for the bond.</see></para>
    /// </remarks>
    /// <returns>Number of accrual days from last coupon date to bond settlement</returns>
    public int SpotAccrualDays()
    {
      if (accrualDays_ == null)
      {
        if (HasDefaulted)
          accrualDays_ = 0;
        else
        {
          accrualDays_ = BondModelUtil.AccrualDays(ProductSettle, this.WorkBond, 
            WorkBond.CumDiv(ProductSettle, ProductSettle), IgnoreExDivDateInCashflow);
        }
      }
      return (int)accrualDays_;
    }

    /// <summary>
    /// True if bond cum-dividend
    /// </summary>
    /// <returns></returns>
    public bool CumDiv()
    {
      return WorkBond.CumDiv(this.Settle, TradeSettle);
    }

    /// <summary>
    ///   Accrued interest as a percentage of face (original Notional Amount) 
    /// </summary>
    /// <remarks>
    /// <para>Calculates accrued interest on the specified settlement date.</para>
    /// <para>Accrued interest is the interest on a bond or loan that has accumulated since the 
    /// principal investment, or since the previous coupon payment if there has been one already.</para>
    /// <para>For a vanilla bond in a regular coupon period this is:</para>
    /// <math>
    ///   AI = C * D_{a} / D_{p}
    /// </math>
    /// <para>where</para>
    /// <list type="bullet">
    ///   <item><description><m> C </m> is the current coupon rate</description></item>
    ///   <item><description><m> D_{a} </m> is the days from the last coupon date to settlement</description></item>
    ///   <item><description><m> D_{p} </m> is the days in the current coupon period</description></item>
    /// </list>
    /// <para>The number of days is calculated based on the bonds <see cref="DayCount"/>.</para>
    /// <para><b>Note:</b></para>
    /// <para>Rounding conventions differ based on market conventions for particular bonds.</para>
    /// <para>Does not support forward settlement for floating rate bonds or
    /// historical settlement for floating rate bonds when the rate resets have
    /// note been set.</para>
    /// </remarks>
    /// <returns>accrued interest as a percentage of Original Notional</returns>
    public double AccruedInterest()
    {
      if (accruedInterest_ == null)
        accruedInterest_ = AccruedInterest(this.Settle, Settle);
      return (double)accruedInterest_;
    }

    /// <summary>
    /// Calculate accrued interest on the specified settlement date as a percentage of face
    /// </summary>
    /// <remarks>
    /// <inheritdoc cref="AccruedInterest()" />
    /// </remarks>
    /// <seealso cref="BondModel">Standard market bond calculations</seealso>
    /// <param name="settle">Settlement date</param>
    /// <param name="tradeSettle">Trade Settle date</param>
    /// <returns>Accrued interest on the settlement date</returns>
    public double AccruedInterest(Dt settle, Dt tradeSettle)
    {
      if (HasDefaulted)
        return 0.0;
      var bond = WorkBond;
      if (bond.CustomPaymentSchedule != null)
      {
        var interestPayments = GetPaymentSchedule(null, settle);
        var interestPeriod = interestPayments.GetPaymentsByType<InterestPayment>().FirstOrDefault(ip => ip.AccrualStart <= settle && ip.AccrualEnd > settle);
        if (interestPeriod == null)
          return 0.0;

        var cumDiv = bond.CumDiv(settle, tradeSettle);
        var ignoreExDiv = IgnoreExDivDateInCashflow;
        var periodEnd = interestPeriod.AccrualEnd;
        Dt exDivDate = BondModelUtil.ExDivDate(bond, periodEnd);
        double accrualFraction;
        if (!cumDiv && !ignoreExDiv && exDivDate <= settle)
        {
          accrualFraction = -Dt.Fraction(interestPeriod.CycleStartDate, interestPeriod.CycleEndDate, settle, periodEnd, interestPeriod.DayCount, bond.Freq);
        }
        else
        {
          accrualFraction = Dt.Fraction(interestPeriod.CycleStartDate, interestPeriod.CycleEndDate, interestPeriod.AccrualStart, settle, interestPeriod.DayCount, bond.Freq);
        }

        accrualFraction /= Dt.Fraction(interestPeriod.CycleStartDate, interestPeriod.CycleEndDate, interestPeriod.AccrualStart, interestPeriod.AccrualEnd,
          interestPeriod.DayCount, bond.Freq);
        return interestPeriod.Amount * accrualFraction/interestPeriod.Notional;

      }
      return BondModelUtil.AccruedInterest(settle, bond, bond.AccrualDayCount, CurrentCoupon(settle),
        bond.CumDiv(settle, tradeSettle), IgnoreExDivDateInCashflow);
      }

    /// <summary>
    /// Total dollar accrued for Bond to as-of date given pricing arguments
    /// </summary>
    /// <remarks>
    ///   <para>Calculates accrued interest on the specified settlement date.</para>
    ///   <para>For a vanilla bond in a regular coupon period this is:</para>
    ///   <formula>
    ///    AI = C * D_{a} / D_{p}
    ///   </formula>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><formula inline="true"> C </formula> is the current coupon rate</description></item>
    ///     <item><description><formula inline="true"> D{a} </formula> is the days from the last coupon date to settlement</description></item>
    ///     <item><description><formula inline="true"> d{p} </formula> is the days in the current coupon period</description></item>
    ///   </list>
    ///   <para>The number of days is calculated based on the bonds <see cref="DayCount"/>.</para>
    ///   <para><b>Note:</b></para>
    ///   <para>Rounding conventions differ based on market conventions for particular bonds.</para>
    ///   <para>Does not support forward settlement for floating rate bonds or
    ///   historical settlement for floating rate bonds when the rate resets have
    ///   note been set.</para>
    /// </remarks>
    /// <returns>Total dollar accrued interest</returns>
    public override double Accrued()
    {
      double unitAccrued = UnitAccrued();
      double effectiveNotional = EffectiveNotional;
      return (unitAccrued * effectiveNotional);
    }

    private double UnitAccrued()
    {
      if (HasDefaulted)
        return 0.0;

      double previousCouponAdj = 0.0;
      if (WorkBond.HasUnSettledLagPayment(Settle, TradeSettle))
      {
        var cfAdapter = BondTradeCashflowAdapter;
        var n = cfAdapter.Count;
        for (int i = 0; i < n; i++)
        {
          var date = cfAdapter.GetDt(i);
          if (date > TradeSettle)
          {
            Dt payDt = date;

            if (payDt > Settle && BondTradeCashflowAdapter.GetEndDt(i) <= Settle)
              previousCouponAdj += cfAdapter.GetAccrued(i);
            else if (payDt > Settle)
              break;
          }
        }
      }

      return (AccruedInterest(Settle, TradeSettle) + previousCouponAdj);
    }

    /// <summary>
    ///   Flat price as a percentage of Notional
    /// </summary>
    /// <remarks>
    ///   <para>The flat price is the full price minus the accrued,
    ///   <m>P_{flat}=P_{full}-AI</m></para>
    /// </remarks>
    /// <seealso cref="BondModel">Standard market bond calculations</seealso>
    /// <returns>Flat price of bond</returns>
    public double FlatPrice()
    {
      return FullPrice() - AccruedInterest();
    }

    /// <summary>
    ///   Full price as a percentage of Notional
    /// </summary>
    /// <remarks>
    ///   <para>Bond market standard price from yield.</para>
    ///   <para>For the standard method</para>
    ///   <math>
    ///    P = \frac{{C v\frac{ v^{N-1} - 1}{v - 1} + R v^{N-1} + C_{n}} }{{1 + t_{sn} y_{w}}} - AI  
    ///   </math>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><m> P </m> is the clean bond price</description></item>
    ///     <item><description><m> y_{tm} </m> is the yield to maturity</description></item>
    ///     <item><description><m> w </m> is the frequency of coupon payments</description></item>
    ///     <item><description><m> y_{w} = \frac{y_{tm}}{w} </m> is the periodic yield to maturity</description></item>
    ///     <item><description><m> v = \frac{1}{1 + y_{w}} </m> is the periodic discount factor;</description></item>
    ///     <item><description><m> R </m> is the redemption amount;</description></item>
    ///     <item><description><m> C </m> is the current coupon;</description></item>
    ///     <item><description><m> C_n </m> is the next coupon;</description></item>
    ///     <item><description><m> AI </m> is the accrued interest</description></item>
    ///     <item><description><m> t_{sn} </m> is the period fraction (in years)</description></item>
    ///     <item><description><m> N </m> is the number of remaining coupon periods</description></item>
    ///   </list>
    ///   <para>Reference: <i>Handbook of Global Fixed Income Calculations</i> by Dragomir Krgin</para>
    ///   <note>Ignores any floating coupons, amortizations and call features of the bond.</note>
    /// </remarks>
    /// <seealso cref="BondModel">Standard market bond calculations</seealso>
    /// <returns>Full price of bond</returns>
    public double FullPrice()
    {
      return FullPrice(WorkBond);
    }

    private double FullPrice(Bond bond)
    {
      if (marketQuote_ == null)
        throw new ArgumentException("Have to provide the bond's Market Quote for this calculation");
      if (fullPrice_ == null)
      {
        if (QuotingConvention == QuotingConvention.ForwardFlatPrice)
        {
          fullPrice_ = (double)marketQuote_ + AccruedInterest(Settle, TradeSettle);
          return (double)fullPrice_;
        }
        else if (QuotingConvention == QuotingConvention.ForwardFullPrice)
        {
          fullPrice_ = (double)marketQuote_;
          return (double)fullPrice_;
        }

        if (TradeIsForwardSettle)
        {
          fullPrice_ = RepoUtil.ConvertSpotQuoteToForwardPrice(ProductSettle,
            SpotSettleCashflowAdpater, SpotFullPrice(),
            Settle, RepoRateAt(Settle), RepoCurve.RateQuoteDayCount);
          return (double)fullPrice_;
        }

        switch (quotingConvention_)
        {
          case QuotingConvention.FlatPrice:
            fullPrice_ = ((double)marketQuote_ + AccruedInterest()) / (ProductIsForwardIssue ? DiscountCurve.DiscountFactor(Settle) : 1.0);
            break;
          case QuotingConvention.FullPrice:
            fullPrice_ = (double)marketQuote_ / (ProductIsForwardIssue ? DiscountCurve.DiscountFactor(Settle) : 1.0);
            break;
          case QuotingConvention.Yield:
            fullPrice_ = CalculatePriceFromYield(bond);
            break;
          case QuotingConvention.YieldToNext:
            fullPrice_ = CalculatePriceFromYieldToNextOrWorst(bond, true);
            break;
          case QuotingConvention.YieldToWorst:
            fullPrice_ = CalculatePriceFromYieldToNextOrWorst(bond, false);
            break;
          case BaseEntity.Toolkit.Base.QuotingConvention.ZSpread:
          {
            if (!IsProductActive())
              return 0.0;
            if (this.Settle == bond.Maturity)
              return 1.0;

            var origCurve = this.DiscountCurve;
            var newCurve = CloneToBondConventions(origCurve);

            // Priced off a zspread quote
            double origSpread = origCurve.Spread;

            if (HasDefaulted)
            {
              newCurve.Spread = origSpread + (double)marketQuote_;
              fullPrice_ = GetDefaultFullPrice(newCurve) / NotionalFactor;
            }
            else
            {
              double fullPrice = 0.0;
              double cashflowPv;
              try
              {
                newCurve.Spread = origSpread + (double)marketQuote_;
                if (RequiresHullWhiteCallableModel)
                {
                  // use newCurve + marketQuote(DM) to calibrate the tree but use 
                  // only newCurve + spread to generate the coupons.
                  cashflowPv = HullWhiteTreeCashflowModel.Pv(BondCashflowAdapter,
                                                             AsOf, Settle, newCurve, null, RecoveryRate,
                                                             DiffusionProcessKind.BlackKarasinski, meanReversion_,
                                                             sigma_, bond, UnitAccrued());
                }
                else if (bond.Convertible)
                {
                  var pricer = (BondPricer)MemberwiseClone();
                  pricer.discountCurve_ = newCurve;
                  pricer.survivalCurve_ = null;
                  cashflowPv = pricer.ConvertibleBondTreeModel.Pv()/1000.0;
                }
                else
                {
                  Dt settle = ProtectionStart;
                  cashflowPv = BondCashflowAdapter.CalculatePvWithOptions(settle, settle, newCurve,
                    null, null, 0.0, OptionType.Put, ExerciseSchedule, NotificationDays, VolatilityObject,
                    CashflowModelFlags, StepSize, StepUnit, BgmTreeOptions);
                }
                fullPrice = cashflowPv / NotionalFactor;
              }
              finally
              {
                fullPrice_ = fullPrice;
              }
            }
          }
            break;
          case BaseEntity.Toolkit.Base.QuotingConvention.DiscountMargin:
          {
            if (!IsProductActive())
              return 0.0;
            if (IsOnLastDate())
              return 1.0;


            double fullPrice = 0;
            if (HasDefaulted)
              throw new ArgumentException("Discount Margin quote does not apply for defaulted bonds");

            if (bond.Floating)
            {
              try
              {
                if (this.RequiresCallableModel)
                {
                  DiscountCurve origCurve = this.DiscountCurve;
                  DiscountCurve newCurve;
                  // DiscountMargin with ActAct is a bit perverse and ends up with some error cases.
                  newCurve = new DiscountCurve((DiscountCalibrator)origCurve.Calibrator.Clone(),
                                               origCurve.Interp,
                                               (bond.AccrualDayCount != DayCount.ActualActualBond) ? bond.AccrualDayCount : DayCount.Actual365Fixed, bond.Freq);

                  // extract L_stub = Libor btw pricing date and next coupon date
                  Dt nextCouponDate = ScheduleUtil.NextCouponDate(Settle, bond.Effective, bond.FirstCoupon, bond.Freq, true);

                  double liborStub = this.StubRate;
                  double currentLibor = this.CurrentFloatingRate;
                  double df1 = RateCalc.PriceFromRate(liborStub, AsOf, nextCouponDate, bond.AccrualDayCount, bond.Freq);
                  newCurve.Add(nextCouponDate, df1);

                  // add flat Libor rate to the discount curve 
                  // (at the frequency of the frn note (e.g 3 months Libor rate))
                  string endTenor = "35 Year"; // extrapolate curve to 35 years
                  Dt endDate = Dt.Add(AsOf, endTenor);
                  double df2 = df1 * RateCalc.PriceFromRate(currentLibor, nextCouponDate, endDate, bond.AccrualDayCount, bond.Freq);
                  newCurve.Add(endDate, df2);

                  // Priced off a discount margin quote
                  double origSpread = origCurve.Spread;
                  newCurve.Spread = origSpread + (double)marketQuote_;

                  if (RequiresHullWhiteCallableModel)
                  {
                    // use newCurve + marketQuote(DM) to calibrate the tree but use 
                    // only newCurve + spread to generate the coupons.
                    fullPrice = HullWhiteTreeCashflowModel.Pv(BondCashflowAdapter,
                                                              AsOf, Settle, newCurve, null, RecoveryRate,
                                                              DiffusionProcessKind.BlackKarasinski, meanReversion_,
                                                              sigma_, bond, UnitAccrued());
                    // the pv routine calibrates the tree and discounts cashflow with the DM added. Does not change payment values
                  }
                  else if (VolatilityObject != null)
                  {
                    fullPrice = BondCashflowAdapter.CalculatePvWithOptions(AsOf, Settle, newCurve,
                                                     null, null, 0.0, OptionType.Put, ExerciseSchedule, 
                                                     NotificationDays, VolatilityObject,
                                                     this.CashflowModelFlags, StepSize, StepUnit, BgmTreeOptions);
                  }
                  else
                  {
                    throw new ToolkitException("No model specified for callable bond.");
                  }
                }
                else
                {
                  fullPrice = BondModelFRN.DiscountMarginToFullPrice(CashflowPeriodFractions, CashflowPrincipals,
                                                                     CashflowAmounts, StubRate, CurrentFloatingRate,
                                                                     bond.Coupon, AccruedFraction,
                                                                     (double)marketQuote_, CurrentRate);
                }
              }
              finally
              {
                fullPrice_ = fullPrice;
              }
            }
            else
              throw new ArgumentException("The Discount Margin quote only applies to floating rate bonds");
          }
            break;
          case QuotingConvention.ASW_Par:
          case QuotingConvention.ASW_Mkt:
          {
            if (!IsProductActive())
              return 0.0;
            if (IsOnLastDate())
              return 1.0;

            if (HasDefaulted)
              throw new ArgumentException("Asset Swap Spread quote does not apply for Defaulted bonds ");

            SurvivalCurve origSurvivalCurve = SurvivalCurve;
            double fullPrice = 0.0;
            if (!bond.Floating)
            {
              try
              {
                SurvivalCurve = null;
                DayCount dc;
                Frequency freq;
                DefaultAssetSwapParams(bond.Ccy, out freq, out dc);
                double fullModelPrice = FullModelPrice();
                double coupon01 = Coupon01(dc, freq);
                if (quotingConvention_ == QuotingConvention.ASW_Mkt)
                  fullPrice = fullModelPrice / ((double)marketQuote_ * 10000.0 * coupon01 + 1);
                else
                  fullPrice = fullModelPrice - (double)marketQuote_ * 10000.0 * coupon01;
              }
              finally
              {
                this.SurvivalCurve = origSurvivalCurve;
                fullPrice_ = fullPrice;
              }
            }
            else
              throw new ArgumentException("The Asset Swap Spread quote only applies to fixed rate bonds");
          }
            break;
          case QuotingConvention.DiscountRate:
          {
            if (bond.BondType == BondType.USTBill)
            {
              int tsm = Dt.Diff(Settle, bond.Maturity, DayCount.Actual365Fixed);
              fullPrice_ = BondModelTM.DiscountRateToPrice(this.Principal, (double)marketQuote_, tsm, 360) +
                           AccruedInterest(this.Settle, TradeSettle);

            }
            else
              throw new ArgumentException("The Discount rate applies only for US Treasury Bills");
          }
            break;
          case QuotingConvention.RSpread:
          {
            if (!IsProductActive())
              return 0.0;
            if (this.RequiresCallableModel)
              throw new ArgumentException("RSpread quote does not apply for Callable bonds ");
            if (IsOnLastDate())
              return 1.0;

            DiscountCurve origCurve = this.DiscountCurve;
            var newCurve = CloneToBondConventions(origCurve);

            // Priced off a RSpread quote
            double origSpread = origCurve.Spread;

            if (HasDefaulted)
            {
              newCurve.Spread = origSpread + (double)marketQuote_;
              fullPrice_ = GetDefaultFullPrice(newCurve) / NotionalFactor;
            }
            else
            {
              double fullPrice = 0.0;
              double cashflowPv;
              try
              {
                newCurve.Spread = origSpread + (double)marketQuote_;
                Dt settle = ProtectionStart;
                cashflowPv = BondCashflowAdapter.Pv(settle, settle,
                  newCurve, survivalCurve_, null, 0.0, StepSize, StepUnit,
                  AdapterUtil.CreateFlags(false, false, discountingAccrued_));
                fullPrice = cashflowPv / NotionalFactor;
              }
              finally
              {
                fullPrice_ = fullPrice;
              }
            }

          }
            break;
          case QuotingConvention.UseModelPrice:
            fullPrice_ = FullModelPrice();
            break;
          default:
            throw new ToolkitException("Invalid quoting convention for Bond");
        }

        // Round price

        fullPrice_ = RoundBondPrice(bond.BondType, (double)fullPrice_);
      }

      return (double)fullPrice_;
    }


    private double GetDefaultFullPrice(DiscountCurve newCurve)
    {
      var payDt = DefaultPaymentDate;
      var settle = Settle;
      if (payDt < settle || (payDt == settle
        && !IncludeSettlePayments))
      {
        return 0.0;
      }
      return BondCashflowAdapter.Pv(AsOf, Settle, newCurve,
        SurvivalCurve, CounterpartyCurve, 0.0, StepSize, StepUnit,
        CashflowModelFlags);
    }

    private double CalculatePriceFromYield(Bond bond)
    {
      if (HasDefaulted)
      {
        //If a bond is defaulted then we price it as a zero coupon bond with just one payment at the end 
        //Zero coupon bonds are priced using coninously compounded rate 
        double T = Dt.Fraction(AsOf, DefaultPaymentDate, DayCount.Actual365Fixed);
        return BondModelDefaulted.PriceFromYield((double) marketQuote_, T, RecoveryRate);
      }
      else
      {
        if (bond.Floating)
        {
          double discountMargin = (double) marketQuote_ - CurrentFloatingRate;
          return BondModelFRN.DiscountMarginToFullPrice(CashflowPeriodFractions, CashflowPrincipals, CashflowAmounts,
            StubRate, CurrentFloatingRate, bond.Coupon, AccruedFraction, discountMargin,
            CurrentRate);
        }
        else
        {
          if (bond.IsCustom)
          {
            //Handle the Amortizing Bonds and Step coupon separately 

            double accruedAmount = AccruedInterest()*NotionalFactor;
            double cashflowPv = PaymentScheduleUtils.YtmToPrice(BondCashflowAdapter, this.Settle, 
              this.ProtectionStart,
              (double) marketQuote_, accruedAmount,
              bond.DayCount, bond.Freq);
            return (cashflowPv/NotionalFactor) + AccruedInterest();
          }
          else
          {
            return BondModelUtil.PriceFromYield(this.Settle, NextCouponDate(), this.PreviousCycleDate(),
              RemainingCoupons(), CurrentRate, bond,
              bond.FinalRedemptionValue, (double) marketQuote_, AccruedInterest(),
              RecoveryRate, CumDiv(), IgnoreExDivDateInCashflow) + AccruedInterest();
          }
        }
      }
    }

    private double CalculatePriceFromYieldToNextOrWorst(Bond bond, bool nextCallFlag)
    {
       var callSchedules = bond.CallSchedule.
         GetActivePeriods(AsOf, bond.GetNotificationDays());
      var callList = callSchedules.Where(cs => cs.StartDate > Settle && 
                                          cs.StartDate < bond.Maturity).Select(s => s);
      var call = callList.FirstOrDefault();
      var min = CalcBondPriceFromYield(bond, AsOf, Settle, MarketQuote);
      //If there's no call schedule, return price from maturity
      if (call == null) return min;     
      var nBond = SetWorkBond(bond, call);
      //Return Yield To Next Call Price 
      if (nextCallFlag)return CalcBondPriceFromYield(nBond, AsOf, Settle, MarketQuote);
      //Return Yield To Worst Call Price
      foreach (var callMem in callList)
      {
         var vBond = SetWorkBond(bond, callMem);
         var price = CalcBondPriceFromYield(vBond, AsOf, Settle, MarketQuote);
         if (price<min)min = price;
      }
     return min;
    }
  

    /// <summary>
    /// Round price
    /// </summary>
    /// <remarks>
    /// <para>Canadian bond prices are rounded to the nearest cent after total value (price*Notional) is calculated.
    /// <see href="http://www.iiac.ca/original_documents/Canadian%20Conventions%20in%20FI%20Markets%20-%20Release%201.1.pdf"/></para>
    /// <para>UK Gilts <see href="http://www.dmo.gov.uk/documentview.aspx?docname=giltsmarket/formulae/yldeqns.pdf"/></para>
    /// <para>Australian bonds prices are rounded to the nearest cent after total value (price*Notional) is calculated.
    /// <see href="http://www.aofm.gov.au/content/pricing_formulae.asp"/></para>
    /// </remarks>
    /// <seealso cref="BondModel">Standard market bond calculations</seealso>
    /// <param name="bondType"></param>
    /// <param name="price"></param>
    private static double RoundBondPrice(BondType bondType, double price)
    {
      switch (bondType)
      {
        case BondType.JGB:
        case BondType.USTBill:
        case BondType.FRFGovt:
          return price;
        default:
          return Math.Floor(price * Math.Pow(10, 6) + 0.5) / Math.Pow(10, 6);
      }
    }

    #region Yield Calculations
 
    /// <summary>
    /// Calculate yield to maturity
    /// </summary>
    /// <remarks>
    /// <para>Yield to maturity (YTM) is the yield promised by the bondholder on the assumption 
    /// that the bond will be held to maturity, that all coupon and principal payments will be 
    /// made and coupon payments are reinvested at the bond's promised yield at the same rate 
    ///  as invested.</para>
    /// <para>Yield to maturity is calculated using a search routine (i.e Newton-Raphson). There's
    /// no closed form solution.</para>
    /// <para>For the standard method</para>
    /// <math>
    ///   P = \frac{{C v\frac{ v^{N-1} - 1}{v - 1} + R v^{N-1} + C_{n}} }{{1 + t_{sn} y_{w}}} - AI
    /// </math>
    /// <para>where</para>
    /// <list type="bullet">
    ///   <item><description><m> P </m> is the clean bond price</description></item>
    ///   <item><description><m> y_{tm} </m> is the yield to maturity</description></item>
    ///   <item><description><m> w </m> is the frequency of coupon payments</description></item>
    ///   <item><description><m> y_{w} = \frac{y_{tm}}{w} </m> is the periodic yield to maturity</description></item>
    ///   <item><description><m> v = \frac{1}{1 + y_{w}} </m> is the periodic discount factor;</description></item>
    ///   <item><description><m> R </m> is the redemption amount;</description></item>
    ///   <item><description><m> C </m> is the current coupon;</description></item>
    ///   <item><description><m> C_n </m> is the next coupon;</description></item>
    ///   <item><description><m> AI </m> is the accrued interest</description></item>
    ///   <item><description><m> t_{sn} </m> is the period fraction (in years)</description></item>
    ///   <item><description><m> N </m> is the number of remaining coupon periods</description></item>
    /// </list>
    /// <para>Reference: <i>Handbook of Global Fixed Income Calculations</i> by Dragomir Krgin</para>
    /// <para>Ignores any floating coupons, amortizations and call features of the bond.</para>
    /// </remarks>
    /// <seealso cref="BondModel">Standard market bond calculations</seealso>
    /// <returns>yield given price</returns>
    public double YieldToMaturity()
    {
      return YieldToMaturity(Bond);
    }

    private double YieldToMaturity(Bond bond)
    {
      if (yield_ != null) return (double)yield_;
      if (quotingConvention_ == QuotingConvention.Yield && !TradeIsForwardSettle)
        yield_ = marketQuote_;
      else
      {
        double effectiveAI = AccruedInterest() * NotionalFactor;
        double effectiveFlatPrice = FlatPrice() * NotionalFactor;

        if (HasDefaulted)
        {
          if (Settle >= DefaultPaymentDate)
            return 0.0;
          double T = Dt.Fraction(AsOf, DefaultPaymentDate, DayCount.Actual365Fixed); //todo check if Settle to be used here instead of AsOf?
          yield_ = BondModelDefaulted.YieldFromPrice(FlatPrice(), T, RecoveryRate);
        }
        else
        {
          if (!IsProductActive() || IsOnLastDate())
          {
            yield_ = 0.0;
            return (double)yield_;
          }

          if (bond.Floating)
          {
            yield_ = DiscountMargin() + CurrentFloatingRate;
          }
          else
          {
            if (bond.IsCustom)
              //For an Amortizing Bond we first normalize the Accrued and Flat price value from their 
              //respective dollar amounts to a unit notional
              yield_ = PaymentScheduleUtils.PriceToYtm(BondCashflowAdapter, this.Settle, this.ProtectionStart,
                bond.DayCount, bond.Freq, effectiveFlatPrice, effectiveAI);

            else
              yield_ = BondModelUtil.YieldToMaturity(this.Settle, bond, PreviousCycleDate(), NextCouponDate(), bond.Maturity,
                RemainingCoupons(), effectiveFlatPrice, effectiveAI,
                this.Principal, CurrentRate, RecoveryRate,
                IgnoreExDivDateInCashflow, CumDiv());
          }
        }
      }
      return (double)yield_;
    }

    /// <summary>
    ///   Calculates yield given a price and workout using standard bond equation.
    /// </summary>
    /// <remarks>
    ///   <para>Calculates yield given a current flat price and future workout date and workout
    ///   price using standard bond equation.</para>
    ///   <note>Ignores any floating coupons, amortizations and call features of the bond.</note>
    /// </remarks>
    /// <param name="workoutDate">Workout date</param>
    /// <param name="workoutPrice">Workout price (eg. 1.0)</param>
    /// <returns>Calculated workout yield</returns>
    public double WorkoutYield(Dt workoutDate, double workoutPrice)
    {
      var bond = WorkBond;
      if (HasDefaulted)
      {
        double T = Dt.Fraction(AsOf, workoutDate, DayCount.Actual365Fixed);
        return BondModelDefaulted.YieldFromPrice(workoutPrice, T, RecoveryRate);
      }

      if (bond.Floating)
      {
        Dt defaultDate, dfltSettle;
        GetDefaultDates(out defaultDate, out dfltSettle);
 
        var cfa =new CashflowAdapter(bond.GetPaymentSchedule(null, workoutDate, 
          Dt.Empty, DiscountCurve,
          ReferenceCurve, RateResets, defaultDate, dfltSettle, RecoveryRate,
          IgnoreExDivDateInCashflow));

        double[] periodFractions = ArrayUtil.Generate(cfa.Count, (i) => cfa.GetPeriodFraction(i));
        double[] principals = ArrayUtil.Generate(cfa.Count, (i) => cfa.GetPrincipalAt(i));
        double[] amounts = ArrayUtil.Generate(cfa.Count, (i) => cfa.GetAmount(i));
        double discountMargin = BondModelFRN.FullPriceToDiscountMargin(periodFractions, principals, amounts, StubRate,
                                                                       CurrentFloatingRate, bond.Coupon,
                                                                       AccruedFraction, workoutPrice + AccruedInterest(),
                                                                       CurrentRate);
        return discountMargin + CurrentFloatingRate;
      }
      else
      {
        return BondModelUtil.YieldToMaturity(this.Settle, bond, PreviousCycleDate(), NextCouponDate(), workoutDate,
                                             RemainingCoupons(), workoutPrice, AccruedInterest(), this.Principal,
                                             CurrentRate, RecoveryRate, IgnoreExDivDateInCashflow, CumDiv());
      }
    }

    /// <summary>
    ///   Calculate bond yield to the next call date.
    /// </summary>
    /// <remarks>
    ///   <para>Calculates the yield to the next call date using the standard bond equation.</para>
    ///   <para>If the settlement date is in a call period, the yield is calculated to
    ///   the first call date of the next call period.</para>
    ///   <note>Ignores any floating coupons and amortizations of the bond.</note>
    /// </remarks>
    /// <returns>Bond yield to the next call date</returns>
    public double YieldToCall()
    {
      if (HasDefaulted)
      {
        return YieldToMaturity();
      }
      return BondModelUtil.YieldToCall(this.Settle, this.Bond, PreviousCycleDate(), NextCouponDate(), this.Bond.Maturity,
                                       RemainingCoupons(), FlatPrice(), AccruedInterest(), this.Principal,
                                       CurrentRate, RecoveryRate, IgnoreExDivDateInCashflow, CumDiv());
    }

    /// <summary>
    ///   Calculate bond yield to the next put date.
    /// </summary>
    /// <remarks>
    ///   <para>Calculates the yield to the next put date using the standard bond equation.</para>
    ///   <para>If the settlement date is in a put period, the yield is calculated to
    ///   the first put date of the next put period.</para>
    ///   <note>Ignores any floating coupons and amortizations of the bond.</note>
    /// </remarks>
    /// <returns>Bond yield to the next put date</returns>
    public double YieldToPut()
    {
      if (HasDefaulted)
      {
        return YieldToMaturity();
      }
      return BondModelUtil.YieldToPut(this.Settle, this.Bond, PreviousCycleDate(), NextCouponDate(), this.Bond.Maturity,
                                      RemainingCoupons(), FlatPrice(), AccruedInterest(), this.Principal,
                                      CurrentRate, RecoveryRate, IgnoreExDivDateInCashflow, CumDiv());
    }

    /// <summary>
    ///   Calculate bond yield to worst call date
    /// </summary>
    /// <remarks>
    ///   <para>Calculates yield to worst call date using standard bond equation.</para>
    ///   <para>The Workout Date is set equal to the date for which Yield is the lowest.
    ///   The dates considered are the call dates(beginning and end of call periods) and maturity.</para>
    ///   <note>Ignores any floating coupons and amortizations of the bond.</note>
    /// </remarks>    
    /// <returns>Bond yield to worst (call) Date</returns>
    public double YieldToWorst()
    {
      if (HasDefaulted)
      {
        return YieldToMaturity();
      }

      if (this.Bond.Convertible)
      {
        return BondModelUtil.YieldToWorstConvertible(this.Settle, this.Bond, PreviousCycleDate(), NextCouponDate(),
                                                     this.Bond.Maturity, RemainingCoupons(), FlatPrice(),
                                                     AccruedInterest(), this.Principal, CurrentRate, RecoveryRate,
                                                     IgnoreExDivDateInCashflow, CumDiv());
      }
      else
      {
        return BondModelUtil.YieldToWorst(this.Settle, this.Bond, PreviousCycleDate(), NextCouponDate(),
                                          this.Bond.Maturity, RemainingCoupons(), FlatPrice(), AccruedInterest(),
                                          this.Principal, CurrentRate, RecoveryRate, IgnoreExDivDateInCashflow,
                                          CumDiv());
      }
    }

    /// <summary>
    /// Calculate the call probability weighted yield exercise or maturity.
    /// </summary>
    /// <returns>System.Double.</returns>
    public double YieldToCallModel()
    {
      var list = ProbabilitiesOfCall();
      if (list == null || (list.Count == 1 && list[0].Key >= Bond.Maturity))
      {
        return YieldToMaturity();
      }
      double yield = 0, probability = 1.0;
      for (int i = 0, n = list.Count; i < n; ++i)
      {
        var p = list[i].Value;
        if (!(p > 0)) continue;
        probability -= p;
        yield += p*YieldToExercise(list[i].Key);
      }
      if (probability > 0)
        yield += probability*YieldToMaturity();
      return yield;
    }

    /// <summary>
    /// Calculate the yield to the specified exercise date.
    /// </summary>
    /// <param name="exerciseDate">The exercise date.</param>
    /// <returns>System.Double.</returns>
    /// <exception cref="ToolkitException"></exception>
    private double YieldToExercise(Dt exerciseDate)
    {
      var es = ExerciseSchedule;
      var idx = es.IndexOf(exerciseDate);
      if (idx < 0)
      {
        throw new ToolkitException(
          $"${exerciseDate} is outside the range of exercise schedule");
      }
      return YieldToDate(exerciseDate, es[idx].ExercisePrice);
    }

    private double YieldToDate(Dt date, double redemptionPrice)
    {
      double ai = AccruedInterest() * NotionalFactor;
      double flatPrice = FlatPrice() * NotionalFactor;

#if DEBUG
      Debug.Assert(!HasDefaulted);
      Debug.Assert(IsProductActive() && !IsOnLastDate());
      Debug.Assert(!Bond.Floating);
      Debug.Assert(!Bond.IsCustom);
#endif
 
      var bond = (Bond)Bond.ShallowCopy();
      bond.Maturity = date;
      if (bond.LastCoupon >= date)
      {
        bond.LastCoupon = Dt.Empty;
      }
      if (bond.FirstCoupon >= date)
      {
        bond.FirstCoupon = Dt.Empty;
      }
      var p = (BondPricer)this.ShallowCopy();
      p.Product = bond;
      p.Reset();
      return BondModelUtil.YieldToMaturity(p.Settle, bond,
        p.PreviousCycleDate(), p.NextCouponDate(), date,
        p.RemainingCoupons(), flatPrice, ai, p.Principal * redemptionPrice,
        p.CurrentRate, p.RecoveryRate, p.IgnoreExDivDateInCashflow, p.CumDiv());
    }

    #endregion

    /// <summary>
    ///   Calculate price value of a yield 01 of bond
    /// </summary>
    /// <remarks>
    ///   <para>This is the change in price bond given a 1bp reduction in yield.</para>
    ///   <math>
    ///    PV01 = 100\times\frac{pd - pu} {2 \times yieldBump} 
    ///   </math>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><m> PV01 </m> Price to yield delta</description></item>
    ///     <item><description><m> yieldBump = 0.0001</m> is the 1bp yield bump</description></item>
    ///     <item><description><m> pu = P(yield + yieldBump) </m> is the clean price  after 1 bp upbump in yield</description></item>
    ///     <item><description><m> pd = P(yield - yieldBump)</m> is the clean price after 1 bp downbump in yield</description></item>
    ///   </list>
    ///   <para>Reference: Stigum (p. 219).</para>
    /// <para>Reference: <i>Handbook of Global Fixed Income Calculations</i> by Dragomir Krgin</para>
    /// </remarks>
    /// <returns>Calculated price value of a yield 01</returns>
    public double PV01()
    {
      return IsYieldToWorstCallableModel 
        && QuotingConvention == QuotingConvention.Yield
        ? CalculateYieldToWorstBondModelPv01(WorkBond)
        : CalculatePv01(WorkBond);
    }

    private double CalculatePv01(Bond bond)
    {
      if (HasDefaulted)
      {
        double T = Dt.Fraction(AsOf, DefaultPaymentDate, DayCount.Actual365Fixed);
        return BondModelDefaulted.Pv01(FullPrice(), T, RecoveryRate, YieldToMaturity(bond)) * (-1.0) * NotionalFactor;
      }
      else
      {
        if (!IsProductActive() || IsOnLastDate())
          return 0.0;

        if (bond.Floating)
        {

          double actualPv01 = BondModelFRN.Pv01(CashflowPeriodFractions, CashflowPrincipals, CashflowAmounts, StubRate,
                                                CurrentFloatingRate, bond.Coupon, DiscountMargin(), AccruedFraction,
                                                CurrentRate, NotionalFactor) * (-1.0);
          return actualPv01 * NotionalFactor;
        }
        else
        {
          if (bond.IsCustom)
          {
            double accruedInterestAmount = AccruedInterest() * NotionalFactor;
            return PaymentScheduleUtils.PV01(BondCashflowAdapter, this.Settle, ProtectionStart, YieldToMaturity(bond), accruedInterestAmount,
              bond.DayCount, bond.Freq) * (-1.0);
          }
          else
          {
            return BondModelUtil.PV01(this.Settle, bond, YieldToMaturity(bond), PreviousCycleDate(), NextCouponDate(),
                                      RemainingCoupons(), CurrentRate, this.Principal, RecoveryRate, CumDiv(),
                                      IgnoreExDivDateInCashflow) * (-1.0);
          }
        }
      }
    }


    private double CalculateYieldToWorstBondModelPv01(Bond bond)
    {
      var bumpedYield = Scenarios.Bump(MarketQuote,
        ScenarioShiftType.Absolute, 1.0 / 10000);
      var bondWithBumpedYield = GetWorkBond(Bond, AsOf, Settle,
        bumpedYield, QuotingConvention);
      if (bondWithBumpedYield.Maturity == bond.Maturity)
      {
        return CalculatePv01(bond);
      }

      var baseValue = FullPrice(bond);
      var origianlQuote = MarketQuote;
      try
      {
        MarketQuote = bumpedYield;
        Reset();
        var bumpedValue = FullPrice(bondWithBumpedYield);
        return baseValue - bumpedValue;
      }
      finally
      {
        MarketQuote = origianlQuote;
        Reset();
      }
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
    /// <math>
    ///   D_{mac} = \frac{ v^{t_{sn}} (p_1 + p_2)}{w B}
    /// </math>
    /// <para>where</para>
    /// <math>
    ///   p_1 = C * ((1 + y_w) / y_w) * (((1 - v^{N-1}) / y_w) - ((N - 1) * v^N))
    /// </math>
    /// <math>
    ///   p_2 = (t_{sn} * C * (1 - v^{N-1}) / y_w) + ((N - 1 + t_sn) * R * v^{N-1}) + (t_{sn} * C_n)
    /// </math>
    /// <para>and</para>
    /// <list type="bullet">
    ///   <item><description><m> D_{mac} </m> is the Macaulay duration</description></item>
    ///   <item><description><m> y_{tm} </m> is the yield to maturity</description></item>
    ///   <item><description><m> w </m> is the frequency of coupon payments</description></item>
    ///   <item><description><m> y_{w} = \frac{y_{tm}}{w} </m> is the periodic yield to maturity</description></item>
    ///   <item><description><m> v = \frac{1}{1 + y_{w}} </m> is the periodic discount factor;</description></item>
    ///   <item><description><m> R </m> is the redemption amount;</description></item>
    ///   <item><description><m> C </m> is the current coupon;</description></item>
    ///   <item><description><m> C_n </m> is the next coupon;</description></item>
    ///   <item><description><m> B </m> is the full price of the bond</description></item>
    ///   <item><description><m> AI </m> is the accrued interest</description></item>
    ///   <item><description><m> t_{sn} </m> is the period fraction (in years)</description></item>
    ///   <item><description><m> N </m> is the number of remaining coupon periods</description></item>
    /// </list>
    /// <para>Or alternatively:</para>
    ///   <math>
    ///     D_{mac} = \displaystyle{\frac{-\frac{dP}{dY}}{P_{full}}(1+\frac{Y}{F})}
    ///   </math>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><m> D_{mac} </m> is the Macaulay duration</description></item>
    ///     <item><description><m> \frac{dP}{dY} </m> is the first derivative of Price with respect to Yield;</description></item>
    ///     <item><description><m> P_{full} </m> is Price, including accrued;</description></item>
    ///     <item><description><m> Y </m> is Yield to maturity;</description></item>
    ///     <item><description><m> F </m> is frequency of coupon payments per year;</description></item>
    ///   </list>
    /// <para>Reference: <i>Handbook of Global Fixed Income Calculations</i> by Dragomir Krgin</para>
    ///   <note>Ignores any call features of the bond.</note>
    /// </remarks>
    /// <returns>Duration of bond</returns>
    public double Duration()
    {
      var bond = WorkBond;
      double effectiveAI = AccruedInterest() * NotionalFactor;
      double effectiveFlatPrice = FlatPrice() * NotionalFactor;

      if (HasDefaulted)
      {
        double T = Dt.Fraction(AsOf, DefaultPaymentDate, DayCount.Actual365Fixed);
        return BondModelDefaulted.Duration(FullPrice(), T, RecoveryRate, YieldToMaturity(bond));
      }

      if (!IsProductActive() || IsOnLastDate())
        return 0.0;

      if (bond.Floating)
      {
        return BondModelFRN.Duration(CashflowPeriodFractions, CashflowPrincipals, CashflowAmounts, StubRate,
          CurrentFloatingRate,
          bond.Coupon, DiscountMargin(), AccruedFraction, CurrentRate, NotionalFactor, FullPrice(), bond.Freq);
      }
      else
      {
        if (bond.IsCustom)
        {
          return PaymentScheduleUtils.Duration(BondCashflowAdapter, this.Settle, ProtectionStart, YieldToMaturity(bond), effectiveAI,
            bond.DayCount, bond.Freq, effectiveFlatPrice);
        }
        else
        {
          return BondModelUtil.Duration(this.Settle, bond, PreviousCycleDate(), NextCouponDate(),
            RemainingCoupons(), FlatPrice(), YieldToMaturity(bond), AccruedInterest(),
            this.Principal, CurrentRate, RecoveryRate, IgnoreExDivDateInCashflow,
            CumDiv());
        }
      }

    }

    /// <summary>
    ///   Calculate modified duration of bond
    /// </summary>
    /// <remarks>
    ///   <para>The modified duration is a measure of the price sensitivity of a bond to interest rate movements.</para>
    ///   <math>
    ///    D_{mod} = \frac{\displaystyle D_{Macaulay}}{\displaystyle {1+\frac{Y}{k}}} 
    ///   </math>
    ///   <list type="bullet">
    ///     <item><description><m> D_{Macaulay} </m> is the Macaulay Duration of the Bond</description></item>
    ///     <item><description><m> Y </m> is Yield to maturity</description></item>
    ///     <item><description><m> k </m> is the number of coupon payments per year</description></item>
    ///   </list>
    ///   <para>The modified duration is inversely proportional to the approximate percentage change in price for
    ///   a given change in yield.</para>    
    ///   <para>Reference: <i>Handbook of Global Fixed Income Calculations</i> by Dragomir Krgin</para>
    ///   <note>Ignores any call features of the bond.</note>
    /// </remarks>
    /// <returns>modified duration of bond</returns>
    public double ModDuration()
    {
      var bond = WorkBond;
      if (HasDefaulted)
      {
        double T = Dt.Fraction(AsOf, DefaultPaymentDate, DayCount.Actual365Fixed);
        return BondModelDefaulted.ModDuration(FullPrice(), T, RecoveryRate, YieldToMaturity(bond));
      }
      if (bond.Floating)
      {
        return BondModelFRN.ModDuration(CashflowPeriodFractions, CashflowPrincipals, CashflowAmounts, StubRate, CurrentFloatingRate,
                                        bond.Coupon, DiscountMargin(), AccruedFraction, CurrentRate, NotionalFactor, FullPrice(), bond.Freq);

      }
      else
      {
        if (bond.IsCustom)
        {
          double effectiveAI = AccruedInterest() * NotionalFactor;
          double effectiveFlatPrice = FlatPrice() * NotionalFactor;
          return PaymentScheduleUtils.ModDuration(BondCashflowAdapter, this.Settle, ProtectionStart,
            YieldToMaturity(bond), effectiveAI, bond.DayCount, bond.Freq, effectiveFlatPrice);

        }
        else
        {
          return BondModelUtil.ModDuration(this.Settle, bond, PreviousCycleDate(), NextCouponDate(),
            RemainingCoupons(), FlatPrice(), YieldToMaturity(bond), AccruedInterest(),
            this.Principal, CurrentRate, RecoveryRate, IgnoreExDivDateInCashflow,
            CumDiv());

        }

      }
    }

    /// <summary>
    /// Calculated the Value on Default (VOD) of a bond
    /// </summary>
    /// <remarks>
    /// <para>The VOD is the expected loss on default of the bond. It is the full value of the bond
    /// minus the expected recovery discounted from settlement to the pricing date.</para>
    /// </remarks>
    /// <returns>Bond VOD</returns>
    public double VOD()
    {
      if (HasDefaulted)
        return 0.0;
      double dfAtSettle = DiscountCurve.DiscountFactor(Settle);
      return EffectiveNotional * (FullPrice() - dfAtSettle * RecoveryRate);
    }

    /// <summary>
    /// Calculate Weighted Average Life (WAL)
    /// </summary>
    /// <remarks>
    ///   <para>The <c>Weighted-Average Life (WAL)</c> is the weighted average time to
    ///   principal repayment, assuming no default.</para>
    ///   <math>
    ///     \mathrm{WAL} = \sum_{i=1}^{n}P_i/P*t_i
    ///   </math>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><m>\mathrm{WAL}</m> weighted average life of the Bond;</description></item>
    ///     <item><description><m>P_i</m> is the principal repayment at coupon i;</description></item>
    ///     <item><description><m>P</m> is the total principal;</description></item>
    ///     <item><description><m>t_i</m> is the time from settlement to coupon i.</description></item>
    ///   </list>
    ///   <para>No default or prepayment risk is considered in this measure.</para>
    /// </remarks>
    /// <returns>WAL of bond</returns>
    public double WAL()
    {
      var bond = WorkBond;
      if (bond.Convertible || bond.Callable)
        throw new ArgumentException("The WAL calculation is not yet supported for callable and convertible bonds");
      if (Settle >= bond.Maturity || HasDefaulted)
        return 0.0;
      var cf = BondCashflowAdapter;
      var notional = NotionalFactor;
      var wal = 0.0;
      for (var i = 0; i < cf.Count; i++)
        if (cf.GetDt(i) > Settle)
          wal += cf.GetAmount(i) / notional * Dt.TimeInYears(Settle, cf.GetDt(i));
      return wal;
    }

    /// <summary>
    ///   Calculate convexity of bond
    /// </summary>
    /// <remarks>
    /// <para>This is the change in Pv01 of a $1000 notional bond given a 1bp reduction in yield.</para>
    /// <para>It is a volatility measure for bonds used in conjunction with modified duration in order to 
    ///    measure how the bond's price will change as interest rates change. It is equal to 
    ///    the negative of the second derivative of the bond's price relative to its yield, 
    ///    divided by its price.</para>
    /// <math>
    ///   Convexity = \displaystyle{\frac{d^2P}{dY^2}/P_{full}}
    /// </math>
    /// <para>where</para>
    /// <list type="bullet">
    ///   <item><description><m> \frac{d^2P}{dY^2} </m> is the 2nd derivative of Price with respect to Yield (twice)</description></item>
    ///   <item><description><m> P_{full} </m> is the Price including accrued</description></item>
    /// </list>
    /// <para>or alternatively</para>
    /// <math>
    ///   Convexity =  ( P_{d} + P_{u} - 2 * P ) / ( B_{y} * B_{y} * P )
    /// </math>
    /// <para>where</para>
    /// <list type="bullet">
    ///   <item><description><m> B_{y} </m> (1bp) is the yield bump</description></item>
    ///   <item><description><m> P </m> is the clean price after 1 bp upbump in yield;</description></item>
    ///   <item><description><m> P_{d} </m> is the clean price after 1 bp downbump in yield;</description></item>
    ///   <item><description><m> P_{u} </m> is the clean price at current yield;</description></item>
    /// </list>
    /// <para>This uses the standard bond equations ignoring any call provisions, amortizations, or coupon schedules.</para>
    /// <para>Reference: <i>Handbook of Global Fixed Income Calculations</i> by Dragomir Krgin</para>
    /// </remarks>
    /// <returns>convexity of bond</returns>
    public double Convexity()
    {
      var bond = WorkBond;
      double effectiveAI = AccruedInterest() * NotionalFactor;
      double effectiveFlatPrice = FlatPrice() * NotionalFactor;

      if (HasDefaulted)
      {
        double T = Dt.Fraction(AsOf, DefaultPaymentDate, DayCount.Actual365Fixed);
        return BondModelDefaulted.Convexity(FullPrice(), T, RecoveryRate, YieldToMaturity(bond));
      }

      if (!IsProductActive() || IsOnLastDate())
        return 0.0;

      if (bond.Floating)
      {
        return BondModelFRN.Convexity(CashflowPeriodFractions, CashflowPrincipals, CashflowAmounts, StubRate,
                                      CurrentFloatingRate, bond.Coupon, DiscountMargin(), AccruedFraction,
                                      CurrentRate, NotionalFactor, FullPrice());
      }
      else
      {
        if (bond.IsCustom)
        {
          //The Convexity calculated below is based on the unit notional and it does not take in to the fact 
          //the bond has amortized
          double effectiveConvexity = PaymentScheduleUtils.Convexity(BondCashflowAdapter, this.Settle, ProtectionStart,
            YieldToMaturity(bond), effectiveAI,
            bond.DayCount, bond.Freq, effectiveFlatPrice);
          return effectiveConvexity;
        }
        else
        {
          return BondModelUtil.Convexity(this.Settle, bond, PreviousCycleDate(), NextCouponDate(),
            RemainingCoupons(), FlatPrice(), YieldToMaturity(bond), AccruedInterest(),
            this.Principal, CurrentRate, RecoveryRate, IgnoreExDivDateInCashflow,
            CumDiv());
        }
      }
    }

    /// <summary>
    /// Calculate the True Yield for the bond.
    /// </summary>
    /// <remarks>
    ///   <para>The True Yield is the yield computed using the actual coupon payment dates
    ///   of the bond.</para>
    /// </remarks>
    /// <returns>True Yield </returns>
    public double TrueYield()
    {
      var bond = WorkBond;
      if (bond.Convertible || bond.Callable)
        throw new ArgumentException("True Yield calculation is not yet supported for callable and convertible bonds");

      if (trueYield_ == null)
      {
        if (HasDefaulted)
        {
          if (Settle >= DefaultPaymentDate)
            return 0.0;
          double T = Dt.Fraction(AsOf, DefaultPaymentDate, DayCount.Actual365Fixed);
          trueYield_ = BondModelDefaulted.YieldFromPrice(FullPrice(), T, RecoveryRate);
        }
        else
        {
          if (Settle >= bond.Maturity)
            return 0.0;

          if (bond.Floating)
          {
            trueYield_ = CurrentFloatingRate + TrueDiscountMargin();
          }
          else
          {
            if (bond.IsCustom)
            {
              double effectiveAI = AccruedInterest() * NotionalFactor;
              double effectiveFlatPrice = FlatPrice() * NotionalFactor;
              trueYield_ = PaymentScheduleUtils.PriceToTrueYield(BondCashflowAdapter, this.Settle, ProtectionStart, bond.DayCount,
                                                                bond.Freq, effectiveFlatPrice, effectiveAI);

            }
            else
            {
              Dt[] payDates;
              int firstIdx;
              var cfAdapter = BondCashflowAdapter;
              for (firstIdx = 0; firstIdx < cfAdapter.Count; firstIdx++)
              {
                if (cfAdapter.GetDt(firstIdx) > Settle)
                  break;
              }
              if ((cfAdapter.Count > 0) && (firstIdx != cfAdapter.Count))
              {
                payDates = new Dt[cfAdapter.Count - firstIdx];
                int count = 0;
                for (int i = firstIdx; i < cfAdapter.Count; i++)
                  payDates[count++] = cfAdapter.GetDt(i);
              }
              else
              {
                payDates = new Dt[] {};
              }

              trueYield_ = BondModel.PriceToTrueYield(AccruedInterest(), this.Settle, payDates, bond.Coupon, Principal, bond.Freq, FlatPrice());
            }
          }
        }
      }

      return (double)trueYield_;
    }


    #endregion Bond Market Methods

    #region Callable Bond Methods

    /// <summary>
    /// Calculates the price of the embedded call option of a callable or convertible bond
    /// </summary>
    /// <remarks>
    /// <para>Calculates the price of the embedded call option of a callable bond
    /// or price of option to convert, to call and to put of a convertible.</para>
    /// <para>This is the value of the call option for a $100 notional bond.</para>
    /// </remarks>
    /// <returns>Option Price</returns>
    public double OptionPrice()
    {
      double opv = 0;
      if (!this.Bond.Callable)
        return opv;

      using (new ZSpreadAdjustment(this))
      {
        if (IsYieldToWorstCallableModel)
        {
          if (QuotingConvention == QuotingConvention.FlatPrice)
            opv = Math.Max(FullPrice(Bond) - FullPrice(), 0.0);
          else
            opv = Math.Max(CalcBondPriceFromYield(Bond, AsOf, Settle, MarketQuote) - FullPrice(), 0.0);
        }
        if (RequiresHullWhiteCallableModel)
        {
          opv = Math.Max(HullWhiteTreeModelPv(false) - HullWhiteTreeModelPv(true), 0.0);
        }
        else if (VolatilityObject != null)
        {
          opv = BondCashflowAdapter.OptionValue(Settle, Settle, OptionType.Put,
                                     ExerciseSchedule, NotificationDays, DiscountCurve, SurvivalCurve, VolatilityObject,
                                     StepSize, StepUnit, BgmTreeOptions, null, CashflowModelFlags);
        }
      }
      return opv;
    }

    /// <summary>
    ///   Value of any embedded call option (scaled by Notional)
    /// </summary>
    /// <returns>Option Pv</returns>
    public double OptionPv()
    {
      double opv = OptionPrice();
      return opv * Notional;
    }

    /// <summary>
    ///  Calculate the probabilities of call by call dates.
    /// </summary>
    /// <returns>IReadOnlyList&lt;KeyValuePair&lt;Dt, System.Double&gt;&gt;.</returns>
    /// <exception cref="System.NotImplementedException">Call probabilities on Hull-White tree not implemented yet</exception>
    public IReadOnlyList<KeyValuePair<Dt,double>> ProbabilitiesOfCall()
    {
      if (!Bond.Callable)
        return new [] { new KeyValuePair<Dt, double>(Bond.Maturity, 1.0)};

      var list = new List<KeyValuePair<Dt, double>>();
      using (new ZSpreadAdjustment(this))
      {
        if (RequiresHullWhiteCallableModel)
        {
          throw new NotImplementedException("Call probabilities on Hull-White tree not implemented yet");
        }
        else if (VolatilityObject != null)
        {
          BondCashflowAdapter.OptionValue(Settle, Settle, OptionType.Put,
            ExerciseSchedule, NotificationDays, DiscountCurve, SurvivalCurve, VolatilityObject,
            StepSize, StepUnit, BgmTreeOptions, list, CashflowModelFlags);
        }
      }
      return list;
    }

    private double HullWhiteTreeModelPv(bool withCall)
    {
      return HullWhiteTreeCashflowModel.Pv(BondCashflowAdapter, AsOf, Settle, DiscountCurve, SurvivalCurve, RecoveryRate,
        DiffusionProcessKind.BlackKarasinski, meanReversion_, sigma_, withCall ? WorkBond : null, UnitAccrued());
    }

    private double HullWhiteTreeModelPv(double sigma, double accrued)
    {
      return HullWhiteTreeCashflowModel.Pv(BondCashflowAdapter, AsOf, Settle, DiscountCurve, SurvivalCurve, RecoveryRate,
        DiffusionProcessKind.BlackKarasinski, meanReversion_, sigma, WorkBond, accrued);
    }

    private double HullWhiteTreeModelVega()
    {
      double accrued = UnitAccrued();
      return HullWhiteTreeModelPv(sigma_ + 0.01, accrued) - HullWhiteTreeModelPv(sigma_, accrued);
    }

    /// <summary>
    /// Calculate the theoretical sensitivity of the bond value (vega) to any option volatility
    /// </summary>
    /// <remarks>
    /// <para>The Vega gives the theoretical sensitivity of the bond price to a 1pc
    /// increase in the volatility.</para>
    /// </remarks>
    /// <returns>(Callable) Bond Vega</returns>
    public double Vega()
    {
      double vega = 0;
      if (RequiresHullWhiteCallableModel)
      {
        using (new ZSpreadAdjustment(this))
        {
          vega = HullWhiteTreeModelVega() * Notional;
        }
      }
      return vega;
    }

    #endregion Callable Bond Methods

    #region Convertible Bond Methods
 
    /// <summary>
    /// Calculate the convertible bond floor, the bond value without conversion
    /// </summary>
    /// <remarks>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Convertible bond floor</returns>
    public double BondFloor()
    {
      if (this.Bond.Convertible == false)
        return 0;
      return ConvertibleBondTreeModel.BondFloor();
    }

    /// <summary>
    /// Calculate the convertible relevant bond floor, the lower bound of convertible bond price
    /// </summary>
    /// <remarks>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Convertible bond floor</returns>
    public double RelevantBondFloor()
    {
      if (this.Bond.Convertible == false)
        return 0;
      return ConvertibleBondTreeModel.RelevantBondFloor();
    }

    /// <summary>
    /// Calculate the convertible bond parity
    /// </summary>
    /// <remarks>
    /// <para>The parity of convertible bond is a benchmark below which the bond is considered
    /// cheap, such that one can buy convertible bond and sell it for an immediate profit.</para>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Convertible bond parity</returns>
    public double Parity()
    {
      if (this.Bond.Convertible == false)
        return 0;
      return ConvertibleBondTreeModel.Parity;
    }

    /// <summary>
    /// Calculate the conversion price, the stock price at which conversion can be exercised
    /// </summary>
    /// <remarks>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Convertible bond conversion price</returns>
    public double ConversionPrice()
    {
      if (this.Bond.Convertible == false)
        return 0;
      return ConvertibleBondTreeModel.ConversionPrice;
    }

    /// <summary>
    /// Calculate the delta of convertible bond
    /// </summary>
    /// <remarks>
    /// <para>Change of price per $1 of conversion value
    /// (or per $1 / Conversion Ratio change of stock price).</para>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Convertible bond delta</returns>
    public double ConvertibleBondDelta()
    {
      if (this.Bond.Convertible == false)
        return 0;
      var pricer = (BondPricer)MemberwiseClone();
      return pricer.ConvertibleBondTreeModel.Delta();
    }

    /// <summary>
    ///  Calculate the gamma of convertible bond.
    /// </summary>
    /// <remarks>
    /// <para>Gamma per share = par*{ [ V(1.005*S) - 2*V(S) + V(0.995*S) ]/(0.005*S)^2 } /(conversion ratio) /100</para>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns></returns>
    public double ConvertibleBondGamma()
    {
      if (this.Bond.Convertible == false)
        return 0;
      var pricer = (BondPricer)MemberwiseClone();
      return pricer.ConvertibleBondTreeModel.Gamma();
    }

    /// <summary>
    ///  Calculate convertible bond Vega.
    /// </summary>
    /// <remarks>
    /// <para>Vega = change in convertible bond price for a 1pc change in volatility</para>
    /// <para>     = [ V(StockVol+0.5pc) - V(StockVol-0.5pc) ] /pc</para>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Convertible bond Vega</returns>
    public double ConvertibleBondVegaEquity()
    {
      if (this.Bond.Convertible == false)
        return 0;
      // Divided by 100 to being vega in unit of price per 1%
      var pricer = (BondPricer)MemberwiseClone();
      return pricer.ConvertibleBondTreeModel.VegaEquity() / 100.0;
    }

    /// <summary>
    ///  Calculate the rate Vega of convertible bond
    /// </summary>
    /// <remarks>
    /// <para>Vega = change in convertible bond price for a 1pc change in rate volatility</para>
    /// <para>     = [ V(RateVol+0.5pc) - V(RateVol-0.5pc) ] /1pc</para>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Convertible bond vega on rate</returns>
    public double ConvertibleBondVegaRate()
    {
      if (this.Bond.Convertible == false)
        return 0;
      var pricer = (BondPricer)MemberwiseClone();
      return pricer.ConvertibleBondTreeModel.VegaRate();
    }

    /// <summary>
    /// Calculate the stock option of convertible bond.
    /// </summary>
    /// <remarks>
    /// <para>It is the difference between convertible bond price and bond floor.</para>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Convertible bond option value</returns>
    public double ConvertibleBondOptionValue()
    {
      if (this.Bond.Convertible == false)
        return 0;
      return ConvertibleBondTreeModel.StockOptionValue();
    }

    /// <summary>
    /// Calculate the premium of convertible bond.
    /// </summary>
    /// <remarks>
    /// <para>It is the difference between convertible bond market price and parity.</para>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Convertible bond premium</returns>
    public double ConvertibleBondPremium()
    {
      if (this.Bond.Convertible == false)
        return 0;

      return FullPrice() * 100 - ConvertibleBondTreeModel.Parity;
    }

    /// <summary>
    /// Calculate the percentage premium of convertible bond.
    /// </summary>
    /// <remarks>
    /// <para>It is the difference between convertible bond market price and parity
    /// divided by the convertible bond parity.</para>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Convertible bond premium</returns>
    public double ConvertibleBondPercentagePremium()
    {
      return ConvertibleBondPremium() / ConvertibleBondTreeModel.Parity;
    }

    /// <summary>
    ///  Calculate the BreakEven of convertible bond
    /// </summary>
    /// <remarks>
    /// <para>Number of years to amortize the premium.</para>
    /// <para>BreakEven = (clean price - parity) / (annual coupon - dividend rate * clean price)</para>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Convertible bond BreakEven</returns>
    public double ConvertibleBondBreakEven()
    {
      if (this.Bond.Convertible == false)
        return 0;
      double cleanPrice = this.MarketQuote;
      return ConvertibleBondTreeModel.BreakEven(cleanPrice);
    }

    /// <summary>
    ///  Calculate the cashflow payback of convertible bond
    /// </summary>
    /// <remarks>
    /// <para>Number of years to amortize the premium</para>
    /// <para>cashflow payback = (clean price - parity) / (annual coupon - dividend rate * parity)</para>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Convertible bond Cashflow Payback</returns>
    public double ConvertibleBondCashflowPayback()
    {
      if (this.Bond.Convertible == false)
        return 0;
      double cleanPrice = this.MarketQuote;
      return ConvertibleBondTreeModel.CashflowPayback(cleanPrice);
    }

    /// <summary>
    /// Calculate the current yield of convertible bond
    /// </summary>
    /// <remarks>
    /// <para>Current Yield = (coupon * par amount) / price * 100</para>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Convertible bond current yield</returns>
    public double ConvertibleBondCurrentYield()
    {
      if (this.Bond.Convertible == false)
        return 0;
      double cYield = Bond.Coupon / this.MarketQuote;
      if (this.QuotingConvention != BaseEntity.Toolkit.Base.QuotingConvention.FlatPrice)
      {
        cYield = Bond.Coupon/this.FlatPrice();
      }
      return cYield;
    }

    /// <summary>
    ///  Calculate the OAS (option adjusted spread) for convertible bond
    /// </summary>
    /// <remarks>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <param name="fixStock">Fix stock or not</param>
    /// <returns>OAS of convertible bond</returns>
    public double ConvertibleBondOAS(bool fixStock)
    {
      // this is same to zspread
      return ImpliedZSpread(fixStock) * 10000;
    }

    /// <summary>
    ///  Calculate the hedge ratio of convertible bond.
    /// </summary>
    /// <remarks>
    /// <para>Hedge Ratio = [ V(1.005*S) - V(0.995*S) ]/(0.01*S)</para>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Convertible bond hedge ratio</returns>
    public double HedgeRatio()
    {
      if (this.Bond.Convertible == false)
        return 0;
      var pricer = (BondPricer)MemberwiseClone();
      return pricer.ConvertibleBondTreeModel.HedgeRatio();
    }

    /// <summary>
    /// Calculate the convertible bond duration
    /// </summary>
    /// <remarks>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Effective duration of convertible bond</returns>
    public double EffectiveDuration(bool fixStock)
    {
      if (this.Bond.Convertible == false)
        return 0;
      var pricer = (BondPricer)MemberwiseClone();
      return pricer.ConvertibleBondTreeModel.EffectiveDuration(FullPrice(), fixStock);
    }

    /// <summary>
    ///  Calculate the convertible bond effective convexity
    /// </summary>
    /// <remarks>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Effective convexity of convertible bond</returns>
    public double EffectiveConvexity(bool fixStock)
    {
      if (this.Bond.Convertible == false)
        return 0;
      var pricer = (BondPricer)MemberwiseClone();
      return pricer.ConvertibleBondTreeModel.EffectiveConvexity(FullPrice(), fixStock);
    }

    /// <summary>
    /// Calculate the interest rate sensitivity
    /// </summary>
    /// <remarks>
    /// <para>Interest sensitivity = [Pv(5 bps ir curve up) - Pv(5 bps ir curve down)] / 10bps</para>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Interest sensitivity</returns>
    public double ConvertibleBondrate01()
    {
      if (this.Bond.Convertible == false)
        return 0;
      var pricer = (BondPricer) MemberwiseClone();
      return pricer.ConvertibleBondTreeModel.InterestSensitivity();
    }

    /// <summary>
    /// Calculate the credit sensitivity
    /// </summary>
    /// <remarks>
    /// <para>Credit Sensitivity = [Pv(5 bps credit curve up) - Pv(5 bps credit curve down)] / 10bps</para>
    /// <para>Note: this method only applies to convertible bonds.</para>
    /// <para>See <see cref="ConvertibleBondIntersectingTreesModel">Convertible Bond intersecting tree model</see>
    /// for calculation details.</para>
    /// </remarks>
    /// <returns>Credit Sensitivity</returns>
    public double ConvertibleCreditSensitivity()
    {
      if (this.Bond.Convertible == false)
        return 0;
      var pricer = (BondPricer)MemberwiseClone();
      return pricer.ConvertibleBondTreeModel.CreditSensitivity();
    }

    private static Bond ValidateConvertibleSupport(Bond bond)
    {
      if (bond.StepUp)
      {
        throw new NotSupportedException("Convertible bond with StepUp feature is not supported");
      }

      if (bond.Amortizes)
      {
        throw new NotSupportedException("Convertible bond with amortization feature is not supported");
      }

      if (bond.CustomPaymentSchedule?.Count != null)
      {
        throw new NotSupportedException("Convertible bond with customized schedule is not supported");
      }

      return bond;
    }

    #endregion Convertible Bond Methods

    #region FRN Methods

    /// <summary>
    /// Calculates the spread duration of a FRN
    /// </summary>
    /// <remarks>
    /// <para>The spread duration measures the price elasticity of the bond, assuming all the future
    /// coupons are fixed at the current rate.</para>
    /// <para>The spread duration is the ratio of a small percentage change in the bond's dirty/full
    /// price divided by a small percentage  change in the bond's yield to maturity, assuming all
    /// the future coupons are fixed at the current rate.</para>
    /// <para>For the fixed rate bond, this is the same as <c>qBondCalcMacaulayDuration</c>. It 
    /// uses the standard bond equations ignoring any call provisions, amortizations, or coupon schedules.</para>
    /// <math>
    ///   D_{s} = \frac{-\frac{dP}{dY}}{P_{full}}*(1+\frac{Y}{F})
    /// </math>
    /// <para>where</para>
    /// <list type="bullet">
    ///   <item><description><m> D_{s} </m> is the spread duration</description></item>
    ///   <item><description><m> \frac{dP}{dY} </m> is the first derivative of Price with respect to Yield;</description></item>
    ///   <item><description><m> P_{full} </m> is Price, including accrued;</description></item>
    ///   <item><description><m> Y </m> is Yield to maturity;</description></item>
    ///   <item><description><m> F </m> is frequency of coupon payments per year;</description></item>
    /// </list>
    /// <para>and <m> \frac{dP}{dY} </m> is approximated as</para>
    /// <math>
    ///   \frac{dP}{dY} \approx \frac{P(+ \Delta Y) - P(- \Delta Y)}{2*\Delta Y}
    /// </math>
    /// <note>This calculation is only valid for FRNs.</note>
    /// </remarks>
    /// <returns>Market spread duration of bond</returns>
    public double MarketSpreadDuration()
    {
      if (HasDefaulted)
        return Double.NaN;
      if (!this.Bond.Floating)
        throw new ArgumentException("Market Spread Duration applies only for floaters");
      return BondModelFRN.MarketSpreadDuration(CashflowPeriodFractions, CashflowPrincipals, CashflowAmounts, StubRate,
        CurrentFloatingRate, this.Bond.Coupon, AccruedFraction, DiscountMargin(), CurrentRate, FullPrice(), this.Bond.Freq);
    }

    /// <summary>
    /// Calculates modified spread duration of a FRN.
    /// </summary>
    /// <remarks>
    /// <para>This calculates the modified spread duration assuming all the future coupons are fixed at the
    /// current rate.</para>
    /// <note>This calculation is only valid for FRNs.</note>
    /// </remarks>
    /// <returns>Spread modified duration of bond</returns>
    public double MarketSpreadModDuration()
    {
      if (HasDefaulted)
        return Double.NaN;
      if (!Bond.Floating)
        throw new ArgumentException("Market Spread Duration applies only for floaters");
      return BondModelFRN.MarketSpreadModDuration(CashflowPeriodFractions, CashflowPrincipals, CashflowAmounts,
        StubRate, CurrentFloatingRate, this.Bond.Coupon, AccruedFraction, DiscountMargin(), CurrentRate,
        FullPrice(), this.Bond.Freq);
    }

    /// <summary>
    /// Calculates the Discount Margin for a floater using the actual coupon payment dates 
    /// </summary>
    /// <returns>Discount margin</returns>
    public double TrueDiscountMargin()
    {
      if (this.Bond.Convertible || this.Bond.Callable)
        throw new ArgumentException("True Discount Margin is not yet supported for callable and convertible bonds ");
      if (HasDefaulted)
        return Double.NaN;

      int firstIdx;
      double[] periodFractions;
      var cfAdapter = BondCashflowAdapter;
      for (firstIdx = 0; firstIdx < cfAdapter.Count; firstIdx++)
      {
        if (cfAdapter.GetDt(firstIdx) > Settle)
          break;
      }
      if ((cfAdapter.Count > 0) && (firstIdx != cfAdapter.Count))
      {
        periodFractions = new double[cfAdapter.Count - firstIdx];
        int count = 0;
        for (int i = firstIdx; i < cfAdapter.Count; i++)
        {
          Dt startDt = cfAdapter.GetStartDt(i);
          Dt endDt = cfAdapter.GetEndDt(i);
          Dt payDt = cfAdapter.GetDt(i);
          periodFractions[count++] = Dt.Fraction(startDt, endDt, startDt, payDt, Bond.DayCount, Bond.Freq);
        }
      }
      else
      {
        periodFractions = new double[] { };
      }

      double accruedFraction = (periodFractions.Length > 0)
                                 ? (periodFractions[0] -
                                    Dt.Fraction(cfAdapter.GetStartDt(firstIdx), cfAdapter.GetEndDt(firstIdx), Settle,
                                      cfAdapter.GetDt(firstIdx), Bond.AccrualDayCount, Bond.Freq))
                                 : 0.0;
      double discountMargin = BondModelFRN.FullPriceToDiscountMargin(periodFractions, CashflowPrincipals,
                                                                     CashflowAmounts, StubRate, CurrentFloatingRate,
                                                                     Bond.Coupon, accruedFraction, FullPrice(),
                                                                     CurrentRate);
      return discountMargin;
    }

    /// <summary>
    /// Calculate the discount margin implied by the full price of a FRN
    /// </summary>
    /// <remarks>
    /// <para>Calculates the fixed add-on to the current Libor rate (the 3 month Libor if the 
    /// FRN frequency is 3 months) that is required to reprice the bond.</para>
    /// <para> Discount margin measures yield relative to the current Libor level and does not take
    /// into account the term structure of interest rates. It is in some sense analogous to the YTM 
    /// of a fixed rate bond.</para>
    /// <para>The expected cashflows of an FRN are ususally based on forward LIBOR rates. For the DM
    /// calculation it is assumed that all future realises Libor rates are equal to the current 
    /// LIBOR rate. Future cashflows/coupons are therefore LIBOR + spread(=quoted margin) except
    /// the cashflow for the next coupon date which is known ( = current coupon)</para>
    /// <para>The discount factors are based on current LIBOR level adjusted by a margin. The DM is the 
    /// value of the margin that reprices the FRN to match the market price of the FRN.</para>
    /// <para>Note: only applies to FRNs.</para>
    /// </remarks>
    /// <returns>The Discount Margin implied by price</returns>
    public double DiscountMargin()
    {
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;

      if (HasDefaulted)
        return Double.NaN;

      if (!Bond.Floating)
        throw new ArgumentException("The Discount Margin calculation only applies to floating rate bonds");

      if (QuotingConvention == QuotingConvention.DiscountMargin)
        return (double)marketQuote_;


      double discountMargin;
      if (RequiresCallableModel)
      {
        DiscountCurve origCurve = this.DiscountCurve;

        DiscountCurve newCurve = new DiscountCurve((DiscountCalibrator)origCurve.Calibrator.Clone(),
                                                   origCurve.Interp, Bond.AccrualDayCount, Bond.Freq);

        // extract L_stub = Libor btw pricing date and next coupon date
        Dt nextCouponDate = ScheduleUtil.NextCouponDate(Settle, Bond.Effective, Bond.FirstCoupon,
                                                        Bond.Freq, true);
        double liborStub = this.StubRate;
        double df1 = RateCalc.PriceFromRate(liborStub, AsOf, nextCouponDate, Bond.AccrualDayCount, Bond.Freq);
        newCurve.Add(nextCouponDate, df1);

        // add flat Libor rate to the discount curve 
        // (at the frequency of the frn note (e.g 3 months Libor rate))
        string endTenor = "35 Year";
        Dt endDate = Dt.Add(AsOf, endTenor);
        double currentLibor = this.CurrentFloatingRate;
        double df2 = df1 * RateCalc.PriceFromRate(currentLibor, nextCouponDate, endDate, Bond.AccrualDayCount, Bond.Freq);
        newCurve.Add(endDate, df2);

        if (VolatilityObject != null)
        {
          discountMargin = BondCashflowAdapter.ImplyDiscountSpread(
            FullPrice(), AsOf, Settle, newCurve, null,
            OptionType.Put, ExerciseSchedule, NotificationDays, VolatilityObject,
            CashflowModelFlags, StepSize, StepUnit, BgmTreeOptions);
        }
        else
        {
          discountMargin = HullWhiteTreeCashflowModel.ImpliedDiscountSpread(
            FullPrice(), BondCashflowAdapter, AsOf, Settle, newCurve, null,
            RecoveryRate, DiffusionProcessKind.BlackKarasinski, meanReversion_,
            sigma_, Bond, UnitAccrued());
        }
      }
      else
      {
        discountMargin = BondModelFRN.FullPriceToDiscountMargin(CashflowPeriodFractions, CashflowPrincipals,
                                                                CashflowAmounts, StubRate, CurrentFloatingRate,
                                                                this.Bond.Coupon, AccruedFraction, FullPrice(),
                                                                CurrentRate);
      }
      return discountMargin;
    }

    /// <summary>
    /// Calculate the discount margin implied by the full price of a FRN.
    /// </summary>
    /// <remarks>
    ///   <para>Calculates the fixed add-on to the current Libor rate (the 3 month Libor if the 
    ///   FRN frequency is 3 months) that is required to reprice the bond.</para>
    ///   <para> Discount margin measures yield relative to the current Libor level and does not take
    ///   into account the term structure of interest rates. It is in some sense analogous to the YTM 
    ///   of a fixed rate bond.</para>
    ///   <para>The expected cashflows of an FRN are ususally based on forward LIBOR rates. For the DM
    ///   calculation it is assumed that all future realises Libor rates are equal to the current 
    ///   LIBOR rate (= "Assumed Rate" in Bloomberg) . Future cashflows/coupons are therefore LIBOR + spread(=quoted margin) except
    ///   the cashflow for the next coupon date which is known ( = current coupon)</para>    ///
    ///   <para>The cashflow for the first cashflow at the next coupon date is doscounted using the
    ///   Libor fix rate (="Index To" in Bloomberg). Both currentLibor and liborFix are passed in as parameters
    ///   to the discount margin function</para>
    ///   <note>This calculation only applies to FRNs.</note>
    /// </remarks>
    /// <returns>The Discount Margin implied by price</returns>
    public double DiscountMargin(double liborStub, double currentLibor)
    {
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;
      if (HasDefaulted)
        return Double.NaN;
      if (!Bond.Floating)
        throw new ArgumentException("The Discount Margin calculation only applies to floating rate bonds");
      double discountMargin;
      if (RequiresCallableModel)
      {
        DiscountCurve origCurve = this.DiscountCurve;
        DiscountCurve newCurve = new DiscountCurve((DiscountCalibrator)origCurve.Calibrator.Clone(),
                                                   origCurve.Interp, Bond.AccrualDayCount, Bond.Freq);

        // calculate Current Libor and Libor_Stub for the term strucure 
        Dt nextCouponDate = ScheduleUtil.NextCouponDate(Settle, Bond.Effective, Bond.FirstCoupon, Bond.Freq, true);
        double df1 = RateCalc.PriceFromRate(liborStub, AsOf, nextCouponDate, Bond.AccrualDayCount, Bond.Freq);
        newCurve.Add(nextCouponDate, df1);

        // add flat Libor rate to the discount curve 
        // (at the frequency of the frn note (e.g 3 months Libor rate))
        string endTenor = "35 Year";
        Dt endDate = Dt.Add(AsOf, endTenor);
        double df2 = df1 * RateCalc.PriceFromRate(currentLibor, nextCouponDate, endDate, Bond.AccrualDayCount, Bond.Freq);
        newCurve.Add(endDate, df2);
        if (VolatilityObject != null)
        {
          discountMargin = BondCashflowAdapter.ImplyDiscountSpread(
            FullPrice(), AsOf, Settle, newCurve, null,
            OptionType.Put, ExerciseSchedule, NotificationDays, VolatilityObject,
            CashflowModelFlags, StepSize, StepUnit, BgmTreeOptions);
        }
        else
        {
          discountMargin = HullWhiteTreeCashflowModel.ImpliedDiscountSpread(
            FullPrice(), BondCashflowAdapter, AsOf, Settle, newCurve, null,
            RecoveryRate, DiffusionProcessKind.BlackKarasinski, meanReversion_,
            sigma_, Bond, UnitAccrued());
        }
      }
      else
      {
        //TODO: Discount margin for BGM
        discountMargin = BondModelFRN.FullPriceToDiscountMargin(CashflowPeriodFractions, CashflowPrincipals,
                                                                CashflowAmounts, liborStub, currentLibor,
                                                                this.Bond.Coupon, AccruedFraction, FullPrice(),
                                                                CurrentRate);
      }
      return discountMargin;
    }

    #endregion FRN Methods

    #region ICashflowPricer Methods

    /// <summary>
    ///   Calculate the present value (full price <m> \times </m> Notional) of the cash flow stream, 
    ///   regarding the next coupon payment as "cum dividend" if the pricer's settlement date happens to be in an ex-div period
    /// </summary>
    /// <remarks>
    ///   <para>Cashflows after the settlement date are present valued back to the pricing
    ///   as-of date.</para>
    /// </remarks>
    /// <returns>Present value to the pricing as-of date of the cashflow stream</returns>
    public double CumDivPv()
    {
      // note: the bond models themselves haven't yet been reviewed/enhanced for ex-dividend behavior.
      // for now the call is routed exactly the same way as for "ProductPv", which would be for an ex-dividend
      // bond trade if applicable
      if (this.Bond.Convertible && this.ConvertibleBondTreeModel != null)
        return (ConvertibleBondTreeModel.Pv() / 1000.0 + ModelBasis) * Notional;
      else if (RequiresHullWhiteCallableModel)
        return (HullWhiteTreeModelPv(true) + ModelBasis) * Notional;

      var pv = BondTradeCashflowAdapter.CalculatePvWithOptions(AsOf, ProtectionStart,
        discountCurve_, survivalCurve_, null, 0.0, OptionType.Put, ExerciseSchedule, NotificationDays,
        VolatilityObject, CashflowModelFlags, StepSize, StepUnit, BgmTreeOptions);
      if (logger.IsDebugEnabled)
        LogPvDiffs(nameof(CumDivPv), pv, logger.Debug, true);
      return (pv + ModelBasis) * Notional;
    }

    /// <summary>
    ///   Calculate the present value (full price <m> \times </m> Notional) of the cash flow stream
    /// </summary>
    /// <remarks>
    ///   <para>Cashflows after the settlement date are present valued back to the pricing
    ///   as-of date.</para>
    /// </remarks>
    /// <returns>Present value to the pricing as-of date of the cashflow stream</returns>
    public override double ProductPv()
    {
      if (!(IsDefaulted(Settle) && BeforeDefaultRecoverySettle(Settle) 
        || Settle < Product.Maturity))
        return 0;
      if (this.Bond.Convertible && this.ConvertibleBondTreeModel != null)
        return (ConvertibleBondTreeModel.Pv() / 1000.0 + ModelBasis) * Notional;
      else if (RequiresHullWhiteCallableModel)
        return (HullWhiteTreeModelPv(true) + ModelBasis) * Notional;

      var pv = BondCashflowAdapter.CalculatePvWithOptions(AsOf, ProtectionStart,
        discountCurve_, survivalCurve_, null, 0.0, OptionType.Put,
        ExerciseSchedule, NotificationDays, VolatilityObject, CashflowModelFlags,
        StepSize, StepUnit, BgmTreeOptions);
      if (logger.IsDebugEnabled)
        LogPvDiffs(nameof(ProductPv), pv, logger.Debug, false);
      return (pv + ModelBasis) * Notional;
    }

    public bool UsePaymentSchedule
    {
      get { return _usePaymentSchedule; }
      set { _usePaymentSchedule = value; }
    }

    /// <summary>
    /// Present value (including accrued) of trade to pricing as-of date given the natural quote for this trade.
    /// </summary>
    /// <returns></returns>
    public double ProductPvFromQuote()
    {
      if (TradeIsForwardSettle || ProductIsForwardIssue)
        return EffectiveNotional * SpotFullPrice();
      double flatPrice = quotingConvention_ == QuotingConvention.FlatPrice
                           ? (double)marketQuote_
                           : SpotFullPrice() - AccruedInterest(ProductSettle, ProductSettle);
      return (EffectiveNotional * flatPrice) + Accrued();
    }

    /// <summary>
    /// Present value of trade payment to pricing as-of date given different discounting method
    /// </summary>
    /// <returns></returns>
    public double PaymentPvFromQuote()
    {
      if (PaymentPricer == null)
        return 0.0;
      var adjustment = 1.0;
      if (TradeIsForwardSettle)
      {
        var paymentLevel = 0.0;
        if (Notional != 0)
          paymentLevel = Math.Abs(Payment.Amount / Notional);
        var paymentLevelCurrent = RepoUtil.ConvertSpotPriceFromForwardPrice(ProductSettle,
          SpotSettleCashflowAdpater, Payment.PayDt, paymentLevel,
          RepoRateAt(Payment.PayDt), RepoCurve.RateQuoteDayCount);
        if (paymentLevel != 0)
          adjustment = paymentLevelCurrent / (paymentLevel * DiscountCurve.DiscountFactor(Payment.PayDt));
      }

      return PaymentPv() * adjustment;
    }

    /// <summary>
    /// Calculate the theoretical model present value of the Bond
    /// </summary>
    /// <remarks>
    /// <para>This calculates the theoretical present value of the bond given the underlying model and
    /// model inputs (eg rates, credit, etc).</para>
    /// <para>Cashflows after the settlement date are present valued back to the pricing
    /// as-of date.</para>
    /// </remarks>
    /// <returns>Bond model present value</returns>
    public override double Pv()
    {
      var bond = WorkBond;
      double pv = IsTerminated
        ? 0.0
        : (bond.CumDiv(Settle, TradeSettle)
          || bond.HasUnSettledLagPayment(Settle, TradeSettle)
            ? CumDivPv()
            : ProductPv());
      pv += PaymentPv();
      return pv;
    }

    /// <summary>
    /// Calculate the payment PV of the bond
    /// </summary>
    /// <returns>Payment pv</returns>
    public override double PaymentPv()
    {
      double pv = 0.0;
      if (PaymentPricer != null)
      {
        if (Payment.PayDt > ProductSettle) // strictly greater than
        {
          return PaymentPricer.Pv();
        }
      }
      return pv;
    }

    /// <summary>
    /// Build payment pricer
    /// </summary>
    /// <param name="payment">Payment</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <returns></returns>
    public override IPricer BuildPaymentPricer(Payment payment, DiscountCurve discountCurve)
    {
      if (payment != null)
      {
        if (payment.PayDt > ProductSettle) // strictly greater than
        {
          OneTimeFee oneTimeFee = new OneTimeFee(payment.Ccy, payment.Amount, payment.PayDt, "");
          SimpleCashflowPricer pricer = new SimpleCashflowPricer(oneTimeFee, AsOf, Settle, discountCurve, null);
          pricer.Add(payment.PayDt, payment.Amount, 0.0, 0.0, 0.0, 0.0, 0.0);
          return pricer;
        }
      }
      return null;
    }

    /// <summary>
    ///   Calculate the pv01 of 1bp increase in coupon
    /// </summary>
    /// <remarks>
    ///   <para>Pv of 1bp coupon/spread. The frequency and daycount of the
    ///   fixed/floating coupon need to be specified. 
    ///   </para>
    /// </remarks>
    /// <param name="dc">Daycount used for the 1bp annualized coupon</param>
    /// <param name="freq">Frequency used for the 1bp annualized coupon</param>
    /// <returns>Pv of 1bp coupon/spread </returns>
    public double Coupon01(DayCount dc, Frequency freq)
    {
      double origCoupon = Bond.Coupon;
      DayCount origDC = Bond.DayCount;
      DayCount origAccrualDC = Bond.AccrualDayCount;
      Frequency origFreq = Bond.Freq;
      double origNotional = Notional;
      double origNotionalFactor = NotionalFactor;
      List<CouponPeriod> origCouponSchedule = CloneUtil.CloneToGenericList(Bond.CouponSchedule);
      // For the "Fully Customized" schedule, do not clone the original object; instead save the original one, and bump
      // the "cloned" copy.
      PaymentSchedule customSchedule = Bond.CustomPaymentSchedule;

      double coupon01;
      try
      {
        Bond.DayCount = dc;
        Bond.AccrualDayCount = dc;
        Bond.Freq = freq;
        Notional = 1.0;
        double newNotionalFactor = EffectiveNotional / Notional;
        NotionalFactor = newNotionalFactor;

        ResetCashflow(); // reset cashflow
        accruedInterest_ = null; // reset interest

        if (customSchedule != null && customSchedule.Count > 0 && (origDC != dc || origAccrualDC != dc))
        {
          PaymentSchedule customScheduleCopy1 = PaymentScheduleUtils.CopyPaymentScheduleForBumpingCoupon(customSchedule, dc);
          Bond.CustomPaymentSchedule = customScheduleCopy1; // Replace with a copy with modified day count - to be restored later.
        }

        double baseProductPV = ProductPv();
        double baseAccrued = AccruedInterest();
        double basePv = baseProductPV / NotionalFactor - baseAccrued;

        if (customSchedule != null && customSchedule.Count > 0)
        {
          // Separate processing for the case of fully customized schedule.
          // See BumpFixedCoupon() in swapPricer.cs
          PaymentSchedule customScheduleCopy2 = PaymentScheduleUtils.CopyPaymentScheduleForBumpingCoupon(customSchedule, dc);

          foreach (Dt dt in customScheduleCopy2.GetPaymentDates())
          {
            foreach (Payment pmt in customScheduleCopy2.GetPaymentsOnDate(dt))
            {
              var iPmt = pmt as FixedInterestPayment;
              if (iPmt != null)
                iPmt.FixedCoupon += Coupon01CouponBump;
            }
          }
          Bond.CustomPaymentSchedule = customScheduleCopy2; // Replace with a bumped copy with modified day count - to be restored later.
        }
        else
        {
          Bond.Coupon += Coupon01CouponBump;
          if (Bond.CouponSchedule.Count > 0)
          {
            Bond.CouponSchedule.Clear();
            foreach (var period in origCouponSchedule)
            {
              Bond.CouponSchedule.Add(new CouponPeriod(period.Date, period.Coupon + Coupon01CouponBump));
            }
          }
        }

        ResetCashflow(); // reset cashflow
        accruedInterest_ = null; // reset interest
        double newProductPv = ProductPv();
        double newAccrued = AccruedInterest();
        double newPv = newProductPv / NotionalFactor - newAccrued;

        coupon01 = (newPv - basePv);
      }
      finally
      {
        // restore orig values
        if (customSchedule != null && customSchedule.Count > 0)
        {
          Bond.CustomPaymentSchedule = customSchedule;
        }
        else
        {
          Bond.Coupon = origCoupon;
          if (origCouponSchedule.Count > 0)
          {
            Bond.CouponSchedule.Clear();
            foreach (var period in origCouponSchedule)
            {
              Bond.CouponSchedule.Add(new CouponPeriod(period.Date, period.Coupon));
            }
          }
        }
        Bond.DayCount = origDC;
        Bond.AccrualDayCount = origAccrualDC;
        Bond.Freq = origFreq;
        Notional = origNotional;
        NotionalFactor = origNotionalFactor;
        ResetCashflow();
        accruedInterest_ = null;
      }

      return coupon01;
    }

    /// <summary>
    ///   Calculate the Asset Swap Spread of fixed rate bond
    /// </summary>
    /// <remarks>
    ///   <para>A par asset swap package consists of a fixed rate asset combined with a fixed-vs-floating IR swap </para>
    ///   <para> The Asset swap spread is the spread(over Libor) that a fixed rate bond holder will receive by exchanging the bond for 
    ///   a floating rate security, via an IR swap. It measures the difference between the market price of the bond and the
    ///   model price of the bond and it cantherefore be looked at as the coupon of an annuity in the swap market tha equals the
    ///   difference btw Market and Model price.
    ///   </para>
    ///  <para>The frequency and daycount of the fixed leg of the IR swap matches the ones of the bond. 
    ///  while the frequency and daycount of the floating leg need to be specified.
    ///  </para>
    /// </remarks>
    /// <param name="dc">Floating leg Daycount</param>
    /// <param name="freq">Floating leg Coupon frequency</param>
    /// <param name="market">market convention</param>
    /// <returns>Asset swap spread</returns>
    public double AssetSwapSpread(DayCount dc, Frequency freq, bool market=false)
    {
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;
      if (HasDefaulted)
        return Double.NaN;

      if (RequiresHullWhiteCallableModel || VolatilityObject != null)
        return double.NaN; // Callable ASW calculation result is not reliable

      if (!Bond.Floating)
      {
        SurvivalCurve origSurvivalCurve = SurvivalCurve;
        try
        {
          double dirtyPrice = FullPrice();
          SurvivalCurve = null;
          double fullModelPrice = FullModelPrice();
          double coupon01 = Coupon01(dc, freq);
          if (market)
            return (fullModelPrice - dirtyPrice) / (dirtyPrice * coupon01 * 10000.0);
          return (fullModelPrice - dirtyPrice) / (coupon01 * 10000.0);
        }
        finally
        {
          SurvivalCurve = origSurvivalCurve;
        }
      }
      else
        throw new ArgumentException("The Asset Swap Spread calculation only applies to fixed rate bonds");
    }

    /// <summary>
    /// Calculate the asset swap spread of fixed bond in dual curve enviroment.
    /// </summary>
    /// <remarks>
    /// <math> s = \frac{c\sum_{i=1}^n\tau_iD(t_e,t_i)-AI_{fix}-\sum_{j=1}^ml_{j-1}D(t_e,t_j)+AI_{float}+1-B}{\sum_{j=1}^m\tau_jD(t_e, t_j)-AF}</math>
    /// <para>
    /// The notionals are:
    /// <list type="bullet">
    /// <item><description><m>s</m>: the asset swap spread</description></item>
    /// <item><description><m>c</m>: the fixed annualized coupon rate.</description></item>
    /// <item><description><m>\tau_i</m>: the day fraction of fixed bond coupon payment period </description></item>
    /// <item><description><m>D(t_e, t_i)</m>:the discount factor from effective day <m>t_e</m> to <m>t_i</m></description></item>
    /// <item><description><m>AI_{fix}</m>:the accrued interest of fixed swap leg</description></item>
    /// <item><description><m>l_{j-1}</m>:the projected floating rate for the period <m>[t_{j-1},t_j]</m></description></item>
    /// <item><description><m>AI_{float}</m>:the accrued interest of floating swap leg</description></item>
    /// <item><description><m>B</m>:the flat price of bond</description></item>
    /// <item><description><m>AF</m>:the day fraction of swap floating leg accrual period</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="pricer">The bond pricer</param>
    /// <param name="referenceCurve">The reference curve</param>
    /// <param name="currentRate">The historical obversation of current rate for floating leg</param>
    /// <param name="frequency">The frequency of floating leg coupon </param>
    /// <param name="dc">Floating leg daycount</param>
    /// <returns>bond asset swap spread</returns>
    public  double CalcDualCurveAsw(BondPricer pricer, DiscountCurve referenceCurve,
      double currentRate, Frequency frequency = Frequency.None, DayCount dc = DayCount.None)
    {
      var b = (Bond)pricer.Product;
      if (b.Floating)
        throw new Exception("The Asset Swap Spread calculation only applies to fixed rate bonds");

      SurvivalCurve origSurvivalCurve = SurvivalCurve;
      try
      {
        var effectiveDate = GetAssetSwapEffectiveDate(pricer.Settle);
        var flatPrice = pricer.FlatPrice();
        
        if (frequency == Frequency.None)
          frequency = referenceCurve.ReferenceIndex?.IndexTenor.ToFrequency() ?? frequency;
        if(dc == DayCount.None)
          dc = referenceCurve.ReferenceIndex?.DayCount ?? dc;
        SurvivalCurve = null;

        //calculate fixed leg
        var fixedLeg = new SwapLeg(effectiveDate, b.Maturity, b.Coupon,
          b.Ccy, b.DayCount, b.Freq, b.BDConvention, b.Calendar, Frequency.None, true);
        var fixedLegPricer = new SwapLegPricer(fixedLeg, pricer.AsOf, pricer.Settle, 1.0,
          pricer.DiscountCurve, null, null, pricer.RateResets, null, null);
        var fixedCleanPv = GetCleanSwaplegPv(fixedLegPricer);

        //calculate floating leg
        var discountCurve = pricer.DiscountCurve;
        var projectionIndex = new InterestRateIndex("FloatingIndex", frequency, b.Ccy, dc, 
          b.Calendar, referenceCurve.ReferenceIndex?.SettlementDays ?? 2);
        var floatingLeg = CreateAssetSwapLeg(b, effectiveDate, projectionIndex);
        var rateResets = new RateResets(RateResetUtil.ResetDate(effectiveDate,
          projectionIndex, Tenor.FromDays(projectionIndex.SettlementDays)), currentRate);
        rateResets.CurrentReset = currentRate;
        var floatingLegPricer = new SwapLegPricer(floatingLeg, pricer.AsOf, pricer.Settle, 1.0,
          discountCurve, projectionIndex, referenceCurve, rateResets, null, null);

        double accrualFraction = 0.0;
        var paymentSchedules = floatingLegPricer.GetPaymentSchedule(null, pricer.AsOf);
        var fip = paymentSchedules.GetPaymentsByType<InterestPayment>()
          .FirstOrDefault(p => p.PayDt > pricer.Settle);
        if (fip != null)
          accrualFraction = Dt.Fraction(fip.AccrualStart, pricer.Settle, dc);
        var floatLevel = 0.0;
        foreach (var ps in paymentSchedules)
        {
          var ip = ps as InterestPayment;
          if (ip != null)
            floatLevel += ip.AccrualFactor*discountCurve.Interpolate(ip.PayDt);
        }
        var floatCleanPv = GetCleanSwaplegPv(floatingLegPricer);

        //calculate asset swap spread
        return (fixedCleanPv - floatCleanPv + 1 - flatPrice)/(floatLevel- accrualFraction);
      }
      finally
      {
        SurvivalCurve = origSurvivalCurve;
      }
    }

    private static double GetCleanSwaplegPv(SwapLegPricer pricer)
    {
      return pricer.ProductPv() - pricer.Accrued();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="b">Bond</param>
    /// <param name="effectiveDate">Effective date</param>
    /// <param name="projectionIndex">Projection index</param>
    /// <returns></returns>
    public SwapLeg CreateAssetSwapLeg(Bond b, Dt effectiveDate, ReferenceIndex projectionIndex)
    {
      var floatingLeg = new SwapLeg(effectiveDate, b.Maturity, 
        projectionIndex.IndexTenor.ToFrequency(), 0.0, projectionIndex)
      {
        AccrueOnCycle = b.AccrueOnCycle,
        Notional = b.Notional,
        ResetLag = new Tenor(projectionIndex.SettlementDays, TimeUnit.Days),
        FinalExchange = true
      };
      floatingLeg.AmortizationSchedule.CopyFrom(b.AmortizationSchedule.ToArray());
      if (b.Amortizes) floatingLeg.IntermediateExchange = true;
      return floatingLeg;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="settle"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public Dt GetAssetSwapEffectiveDate(Dt settle)
    {
      if (settle > Bond.Maturity)
        throw new Exception("The pricing settle date is after the bond maturity");

      var bondEffective = Bond.Effective;
      if (settle <= bondEffective) return bondEffective;

      var date = PreviousCycleDate(settle);
      return (date.IsEmpty() || date < bondEffective) ? bondEffective : date;
    }


    /// <summary>
    /// Calculate the theoretical model price of the Bond
    /// </summary>
    /// <remarks>
    /// <para>This function calculates the theoretical present value of the bond given the 
    /// underlying model and model inputs (eg rates, credit, etc). Cashflows after the settlement
    /// date are present valued back to the pricing as-of date.</para>
    /// <para>
    /// We introduce the following notions:
    /// <list type="bullet">
    /// <item><description><m>c</m>: the annualized coupon rate.</description></item>
    /// <item><description><m>R</m>: the recovery rate of the bond.</description></item>
    /// <item><description><m>t_0</m>: the issue date of the bond. </description></item>
    /// <item><description><m>t_i</m>: the bond coupon payment date, with <m>t_1 \lt 
    /// \cdots \lt t_n.</m></description></item>
    /// <item><description><m>t_n</m>: the maturity of the bond, which is the date when the
    /// unit principal amount of the bond is to be paid in full.</description></item>
    /// <item><description><m>t</m>: the pricing as-of rate. </description></item>
    /// <item><description><m>k</m>: the index <m>k</m> such that <m>t_{k-1} \lt t \lt t_k</m>. 
    /// If <m>t \lt t_0</m>, <m>k = 0</m>. </description></item>
    /// <item><description><m>\Delta_i</m>: the fraction of year between two consecutive payment 
    /// dates <m>t_{i-1}</m> and <m>t_i</m>, i.e. <m>\Delta_i = t_i - t_{i-1}</m>.</description></item>
    /// <item><description><m>D_i</m>: the discount factor <m>D_i = P(t, t_i)</m>. </description></item>
    /// <item><description><m>S_i</m>: the survival probability of the bond at time <m>t_i</m>, 
    /// i.e., <m>S_i = \mathbb{P}(\tau \gt t_i)</m>.</description></item>
    /// </list>
    /// The full model price is given by
    /// <math>
    /// P = \sum_{i = k}^n \left( c \Delta_i S_i D_i + R (S_i - S_{i-1}) \frac{D_i + D_{i-1}}{2}\right) + S_n D_n
    /// </math>
    /// </para>
    /// </remarks>
    /// <returns>Present value to the settlement date of the Bond per unit Notional</returns>
    public double FullModelPrice()
    {
      if (this.Bond.Convertible)
      {
        if (survivalCurve_ != null)
        {
          Defaulted dftd = survivalCurve_.Defaulted;
          if (dftd == Defaulted.HasDefaulted || dftd == Defaulted.WillDefault)
          {
            double recovRate = 0;
            if (survivalCurve_.SurvivalCalibrator != null)
            {
              Dt dftdDate = survivalCurve_.DefaultDate;
              recovRate = survivalCurve_.SurvivalCalibrator.RecoveryCurve.RecoveryRate(dftdDate);
              if (RecoveryRate > 0)
                recovRate = RecoveryRate;
            }
            return recovRate * Notional;
          }
        }

        var pricer = (BondPricer) MemberwiseClone();
        return pricer.ConvertibleBondTreeModel.Pv()/NotionalFactor/1000.0;
      }
      else if (RequiresHullWhiteCallableModel)
        return HullWhiteTreeModelPv(true) / NotionalFactor;
      else
      {
        if (TradeIsForwardSettle && (!IsProductActive(ForwardSettle) || IsOnLastDate(ForwardSettle)))
          return 0.0;

        var volObj = ZSpreadConsistentBgmModel ? null : VolatilityObject;
        var pv = BondCashflowAdapter.CalculatePvWithOptions(AsOf, ProtectionStart,
          discountCurve_, survivalCurve_, null, 0.0, OptionType.Put,
          ExerciseSchedule, NotificationDays, volObj, CashflowModelFlags,
          StepSize, StepUnit, BgmTreeOptions);
        if (ZSpreadConsistentBgmModel) pv -= OptionPrice();
        return pv / NotionalFactor;
      }
    }

    /// <summary>
    ///   Expected Loss at maturity
    /// </summary>
    /// <remarks>
    ///   <note>Ignores any floating coupons, amortizations and call features of the bond.</note>
    /// </remarks>
    /// <returns>Expected loss</returns>
    public double ExpectedLoss()
    {
      return ExpectedLossRate(this.ProtectionStart, Product.Maturity) * this.Notional;
    }

    /// <summary>
    ///   Expected Loss over a date range
    /// </summary>
    /// <remarks>
    ///   <note>Ignores any floating coupons, amortizations and call features of the bond.</note>
    /// </remarks>
    /// <param name="start">Start of date range</param>
    /// <param name="end">End of date range after settle</param>
    /// <returns>Expected loss over date range</returns>
    /// <exclude />
    public double ExpectedLossRate(Dt start, Dt end)
    {
      double defaultProb = CounterpartyRisk.CreditDefaultProbability(start, end, SurvivalCurve, null, 0.0, StepSize, StepUnit);
      return defaultProb * (1 - RecoveryRate);
    }

    /// <summary>
    ///   Calculate the suvival probability of the transaction (including the effects of 
    ///   counterparty defaults or the effects of refinance.
    /// </summary>
    /// <remarks>
    ///   <note>Ignores any floating coupons, amortizations and call features of the bond.</note>
    /// </remarks>
    /// <param name="start">Start date</param>
    /// <param name="end">End date</param>
    /// <returns>Suvival probability</returns>
    /// <exclude />
    public double SurvivalProbability(Dt start, Dt end)
    {
      return CounterpartyRisk.OverallSurvivalProbability(start, end, SurvivalCurve, null, 0.0, StepSize, StepUnit);
    }

    /// <summary>
    /// Calculate the full price on the spot date
    /// </summary>
    /// <remarks>
    ///   <para>Bond market standard price from yield.</para>
    ///   <para>For the standard method</para>
    ///   <math>
    ///    P = \frac{{C v\frac{ v^{N-1} - 1}{v - 1} + R v^{N-1} + C_{n}} }{{1 + t_{sn} y_{w}}} - AI
    ///   </math>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><m> P </m> is the clean bond price</description></item>
    ///     <item><description><m> y_{tm} </m> is the yield to maturity</description></item>
    ///     <item><description><m> w </m> is the frequency of coupon payments</description></item>
    ///     <item><description><m> y_{w} = \frac{y_{tm}}{w} </m> is the periodic yield to maturity</description></item>
    ///     <item><description><m> v = \frac{1}{1 + y_{w}} </m> is the periodic discount factor;</description></item>
    ///     <item><description><m> R </m> is the redemption amount;</description></item>
    ///     <item><description><m> C </m> is the current coupon;</description></item>
    ///     <item><description><m> C_n </m> is the next coupon;</description></item>
    ///     <item><description><m> AI </m> is the accrued interest</description></item>
    ///     <item><description><m> t_{sn} </m> is the period fraction (in years)</description></item>
    ///     <item><description><m> N </m> is the number of remaining coupon periods</description></item>
    ///   </list>
    ///   <para>Reference: <i>Handbook of Global Fixed Income Calculations</i> by Dragomir Krgin</para>
    ///   <note>Ignores any floating coupons, amortizations and call features of the bond.</note>
    /// </remarks>
    /// <seealso cref="FullPrice()"/>
    /// <returns>Full price on spot date</returns>
    public double SpotFullPrice()
    {
      var bond = WorkBond;
      if (TradeIsForwardSettle)
      {
        if (quotingConvention_ == QuotingConvention.ForwardFullPrice ||
            quotingConvention_ == QuotingConvention.ForwardFlatPrice)
        {
          return RoundBondPrice(bond.BondType,
            RepoUtil.ConvertSpotPriceFromForwardPrice(ProductSettle,
              SpotSettleCashflowAdpater, Settle,
              FullPrice(), RepoRateAt(Settle),
              RepoCurve.RateQuoteDayCount));
        }
        else
        {
          var spotBondPricer = new BondPricer(bond, AsOf, ProductSettle, DiscountCurve, SurvivalCurve, StepSize,
                                              StepUnit, RecoveryRate)
                               {
                                 TradeSettle = TradeSettle,
                                 ReferenceCurve = ReferenceCurve,
                                 MarketQuote = MarketQuote,
                                 QuotingConvention = QuotingConvention,
                               };
          if (bond.Floating)
          {
            spotBondPricer.rateResetsList_ = CloneUtil.Clone(rateResetsList_);
          }

          return RoundBondPrice(bond.BondType, spotBondPricer.FullPrice());
        }
      }
      else if (ProductIsForwardIssue)
        return FullPrice() * DiscountCurve.DiscountFactor(Settle);
      else
        return FullPrice();
    }

    /// <summary>
    /// Calculate the flat price on bond on the spot settlement date.
    /// </summary>
    /// <seealso cref="FlatPrice()"/>
    /// <returns>Spot flat price</returns>
    public double SpotFlatPrice()
    {
      return SpotFullPrice() - AccruedInterest(ProductSettle, ProductSettle);
    }

    /// <summary>
    ///   Calculate IRR of cashflows using the current market price
    /// </summary>
    /// <remarks>
    ///   <para>Calculates IRR (yield) of cash flows under no default
    ///   using the current market price.</para>
    ///   <para>The IRR is calculated using the daycount and compounding frequency
    ///   of the bond. For the <see cref="DayCount.ActualActualBond"/> daycount the IRR calculation is inappropriate
    ///   (this <see cref="DayCount"/> is used for AUD bonds) hence we use <see cref="DayCount.Actual365Fixed"/> instead.</para>
    /// <para>See <see cref="CashflowModel"/> for calculation details.</para>
    ///   <note>Ignores any call features of the bond.</note>
    /// </remarks>
    /// <returns>Irr (yield) implied by price</returns>
    public double Irr()
    {
      var bond = WorkBond;
      // Validate
      if ((Settle >= bond.EffectiveMaturity) && !BeforeDefaultRecoverySettle(Settle))
        return 0.0;

      Dt settle = ProtectionStart;
      double settlementCash = SpotFullPrice() * NotionalFactorAt(settle);

      CashflowAdapter cf = BondCashflowAdapter;
      if (HasDefaulted)
      {
        var ds = GetDefaultPayment(cf);
        return ds == null || ds.Amount.AlmostEquals(0.0)
          ? 0.0
          : BondModelDefaulted.Irr(settlementCash, DefaultPaymentDate, ds.RecoveryAmount,
            ds.AccrualAmount, AsOf);
      }

      if (cf.Count == 0)
      {
        // can occur for example when pricing an ex-div bond in its final ex-div period in which case
        // a purchaser would receive nothing back
        return 0.0;
      }

      var dayCount = bond.DayCount == DayCount.ActualActualBond
        ? DayCount.Actual365Fixed : bond.DayCount;
      return PaymentScheduleUtils.Irr(cf, settle, settle, null, null,
        null, 0.0, CashflowModelFlags, StepSize, StepUnit, dayCount,
        bond.Freq, settlementCash);
    }

    /// <summary>
    ///   Calculate IRR of cashflows given full price
    /// </summary>
    /// <remarks>
    ///   <para>Calculates IRR (yield) of cash flows under no default
    ///   given current full price.</para>
    ///   <para>See <see cref="CashflowModel"/> for calculation details.</para>
    ///   <note>Ignores any call features of the bond.</note>
    /// </remarks>
    /// <param name="daycount">Daycount</param>
    /// <param name="freq">Compounding frequency of irr</param>
    /// <returns>Irr (yield) implied by price</returns>
    public double Irr(DayCount daycount, Frequency freq)
    {
      // Validate
      if ((Settle >= WorkBond.EffectiveMaturity) && !BeforeDefaultRecoverySettle(Settle))
        return 0.0;

      Dt settle = ProtectionStart;
      double settlementCash = SpotFullPrice() * NotionalFactorAt(settle);

      if (HasDefaulted)
      {
        return Irr();
      }
      return PaymentScheduleUtils.Irr(BondCashflowAdapter, settle, settle, 
        null, null, null, 0.0, CashflowModelFlags, StepSize, StepUnit, 
        daycount, freq, settlementCash);
    }

    /// <summary>
    ///   Calculate discount rate spread (OAS) implied by the full price of the bond.
    /// </summary>
    /// <remarks>
    ///   <para>Calculates the constant spread (continuously compounded) over
    ///   discount curve for cashflow to match the current price.</para>
    ///   <para>This is also commonly called the Z-Spread for non-callable bonds
    ///   and the OAS (option adjusted spread) for callable bonds.</para>
    ///   <para>In other words the OAS is the Z-spread of the stripped(non-callable) bond 
    ///   when properly adjusting for the value of the embedded call option. Works for both callable and 
    ///   non-callable bonds. Callable bonds will require a HWTree pricer instead of a bond pricer.</para>
    ///   <para>For non-defaultable callable bonds the zspread is the OAS i.e the shift that
    ///   needs to applied to the short rate in order to make model price and market price match.</para>
    /// <para>See <see cref="CashflowModel"/> for calculation details.</para>
    /// </remarks>
    /// <param name="fixStock">Fix stock price for convertible bonds</param>
    /// <returns>spread over discount curve implied by price</returns>
    public double ImpliedZSpread(bool fixStock=false)
    {
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;
      double settlementCash = ProductIsForwardIssue ? (FullPrice() * NotionalFactor) : (SpotFullPrice() * NotionalFactorAt(ProductSettle));

      var origCurve = this.DiscountCurve;
      var newCurve = CloneToBondConventions(origCurve);

      double impliedZSpread;
      if (HasDefaulted)
      {
        var ds = GetDefaultPayment(BondCashflowAdapter);
        impliedZSpread = ds == null || ds.Amount.AlmostEquals(0.0)
          ? 0.0
          : BondModelDefaulted.ImpliedDiscountSpread(settlementCash, 
          DefaultPaymentDate, ds.RecoveryAmount, ds.AccrualAmount, AsOf, newCurve);
      }
      else if (WorkBond.Convertible && ConvertibleBondTreeModel != null)
      {
        // Convertible
        try
        {
          this.DiscountCurve = newCurve;
          impliedZSpread = ConvertibleBondTreeModel.ImpliedDiscountSpread(settlementCash, false, fixStock);
        }
        finally
        {
          this.DiscountCurve = origCurve;
        }
      }
      else if (RequiresHullWhiteCallableModel)
      {
        // Callable
        try
        {
          impliedZSpread = HullWhiteTreeCashflowModel.ImpliedDiscountSpread(
            settlementCash, BondCashflowAdapter, Settle, Settle, newCurve, null,
            RecoveryRate, DiffusionProcessKind.BlackKarasinski,
            meanReversion_, sigma_, WorkBond, UnitAccrued());
        }
        finally
        {
          this.DiscountCurve = origCurve;
        }
      }
      else if (VolatilityObject != null)
      {
        // Other without vol
        try
        {
          impliedZSpread = BondCashflowAdapter.ImplyDiscountSpread(
            settlementCash, AsOf, Settle, newCurve, null,
            OptionType.Put, ExerciseSchedule, NotificationDays, VolatilityObject,
            CashflowModelFlags, StepSize, StepUnit, BgmTreeOptions);
        }
        finally
        {
          this.DiscountCurve = origCurve;
        }
      }
      else
      {
        // Other
        Dt settle = ProtectionStart;
        // impliedZSpread = CashflowModel.ImpDiscountSpread(Cashflow, CashflowPricer.DiscountToSettle ? Settle : AsOf, 
        // settle, newCurve, null, null, 0.0, false, false, StepSize, StepUnit, FullPrice());
        try
        {
          impliedZSpread = PaymentScheduleUtils.ImpDiscountSpread(BondCashflowAdapter,
            settle, settle, newCurve, null, null, 0.0,
            StepSize, StepUnit, settlementCash,
            AdapterUtil.CreateFlags(false, false, discountingAccrued_));
        }
        catch (SolverException ex)
        {
          ex.ToString();
          throw new ToolkitException(
            String.Format("Unable to Imply a ZSpread for market price {0}. Please check your inputs", settlementCash));
        }
        finally
        {
          this.DiscountCurve = origCurve;
        }
      }
      return impliedZSpread;
      }

    /// <summary>
    /// Calculate the hazard rate spread over the survival curve implied by the market price of the bond
    /// </summary>
    /// <remarks>
    /// Calculates the constant hazard rate spread in basis points over the survival curve for
    /// cashflow(bond model price) to match a specified full price. 
    /// </remarks>
    /// <returns>Spread over survival curve implied by price</returns>
    public double ImpliedHazardRateSpread()
    {
      // Validate
      double settlementCash = FullPrice() * NotionalFactor;
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;

      if (HasDefaulted)
        return Double.NaN;

      if (hspread_ == null)
      {
        if (RequiresCallableModel)
        {
          if (VolatilityObject != null)
          {
            hspread_ = BondCashflowAdapter.ImplySurvivalSpread(
              settlementCash, AsOf, Settle, DiscountCurve,
              SurvivalCurve, OptionType.Put, ExerciseSchedule, NotificationDays,
              VolatilityObject, CashflowModelFlags,
              StepSize, StepUnit, BgmTreeOptions);
          }
          else
          {
            hspread_ = HullWhiteTreeCashflowModel.ImpSurvivalSpread(
              settlementCash, BondCashflowAdapter, AsOf, Settle, DiscountCurve,
              SurvivalCurve, RecoveryRate,
              DiffusionProcessKind.BlackKarasinski,
              meanReversion_, sigma_, WorkBond, UnitAccrued());
          }
        }
        else
        {
          Dt settle = ProtectionStart;

          return PaymentScheduleUtils.ImpSurvivalSpread(BondCashflowAdapter, settle,
            settle, discountCurve_, survivalCurve_, null, 0.0, false, false,
            discountingAccrued_, StepSize, StepUnit, settlementCash);
        }
      }
      return (double)hspread_;
    }

    /// <summary>
    /// Calculate the basis (in basis points) over the survival curve implied by the market price of the bond
    /// </summary>
    /// <remarks>
    /// <para>The basis is the CDS spread minus the bond-implied CDS spread at bond
    /// (duration-adjusted) maturity.</para>
    /// <para>Calculates the constant spread shift (in basis points) to the specified survival
    /// curve so that the resulting cashflows match the bond's observed market price. The bond basis is
    /// the difference of the bond's implied CDS spread (qBondCalcCDSLevel) and the CDS spread from the bond
    /// issuer's survival curve at bond maturity.</para>
    /// </remarks>
    /// <returns>Spreads shift to the Survival Curve</returns>
    public double ImpliedCDSSpread()
    {
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;
      if (HasDefaulted)
        return Double.NaN;

      double impliedSpread = 0;
      try
      {
        if (SurvivalCurve == null)
          return Double.NaN;
        double impliedCDSLevel = ImpliedCDSLevel(); // Callable or not is handled in ImpliedCDSLevel
        double curveLevel = CurveUtil.ImpliedSpread(SurvivalCurve, WorkBond.Maturity,
                                                    DayCount.Actual360, Frequency.Quarterly, BDConvention.Following, Calendar.None);
        // Find ImpliedCDSSpread = curveLevel - impliedCDSLevel 
        impliedSpread = curveLevel - impliedCDSLevel;
      }
      catch (Exception e)
      {
        e.ToString();
        if (e is ArgumentException)
          throw;
        else
        {
          throw new ToolkitException(
            "Market level, credit spreads, and recovery assumption appear to be inconsistent.  Check your inputs.  Cannot bracket implied CDS spread."
            );
        }

      }
      return impliedSpread;
    }

    /// <summary>
    /// Calculate the CDS spread shift over the supplied survival curve
    /// </summary>
    /// <remarks>
    ///   <para>Calculates constant CDS spread shift over the supplied survival curve so that the
    ///   model price matches the current market price of the bond. All CDS tenor spreads are shifted
    /// and curve is re-fitted</para>
    /// </remarks>
    /// <returns>Spreads shift to the Survival Curve supplied to pricer</returns>
    public double CDSCurveShift()
    {
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;
      var bond = WorkBond;
      double curveShift = 0;
      double settlementCash = FullPrice() * NotionalFactor;
      try
      {
        if (bond.Convertible && ConvertibleBondTreeModel != null)
        {
          curveShift = ConvertibleBondTreeModel.CDSSpreadShift(settlementCash);
        }
        else if (RequiresCallableModel)
        {
          if (VolatilityObject != null)
          {
            curveShift = BondCashflowAdapter.ImplyCdsSpreadShift(
              settlementCash, RecoveryRate, AsOf, Settle, DiscountCurve,
              SurvivalCurve, OptionType.Put, ExerciseSchedule, NotificationDays, VolatilityObject,
              CashflowModelFlags, StepSize, StepUnit, BgmTreeOptions);
          }
          else
            curveShift = HullWhiteTreeCashflowModel.CdsSpreadShift(
              settlementCash, BondCashflowAdapter, AsOf, Settle, DiscountCurve,
              SurvivalCurve, RecoveryRate, DiffusionProcessKind.BlackKarasinski,
              meanReversion_, sigma_, bond, UnitAccrued());
        }
        else
        {
          curveShift = PaymentScheduleUtils.ImpliedCdsSpread(this.AsOf,
            this.Settle, BondCashflowAdapter, this.DiscountCurve,
            this.SurvivalCurve, null, 0.0, false, discountingAccrued_,
            bond.PaymentLagRule != null, this.StepSize, this.StepUnit, settlementCash);

        }
      }
      catch (Exception e)
      {
        e.ToString();
        throw new ToolkitException(
          "Market level, credit spreads, and recovery assumption appear to be inconsistent.  Check your inputs.  Cannot bracket implied CDS spread."
          );
      }
      return curveShift;
    }

    /// <summary>
    /// Calculate the bond-implied survival curve (implied by the market price of the bond)
    /// </summary>
    /// <remarks>
    /// Calculates the constant spread shift (in basis points) to the credit curve spreads for
    /// cashflow to match a specified full price. Applies the shift to the original credit curve 
    /// and returns the shifted curve. Works for both callable and non-callable bonds. Callable bonds
    /// will require a HWTree pricer instead of a bond pricer.
    /// </remarks>
    /// <returns>Bond-Implied Survival Curve</returns>
    public SurvivalCurve ImpliedCDSCurve()
    {
      var bond = WorkBond;
      // Validate
      double settlementCash = FullPrice() * NotionalFactor;
      if (!IsProductActive() || IsOnLastDate())
        return null;

      if (HasDefaulted)
        return null;

      if (bond.Convertible && ConvertibleBondTreeModel != null)
      {
        return ConvertibleBondTreeModel.ImpliedFlatSpreadCurve(
          settlementCash, RecoveryRate > 0 ? RecoveryRate : 0.4);
      }
      else if (RequiresCallableModel)
      {
        if (VolatilityObject != null)
        {
          return BondCashflowAdapter.ImplyCdsCurve(settlementCash,
            RecoveryRate > 0 ? RecoveryRate : 0.4,
            AsOf, Settle, DiscountCurve, SurvivalCurve,
            OptionType.Put, ExerciseSchedule, NotificationDays, VolatilityObject,
            CashflowModelFlags, StepSize, StepUnit, BgmTreeOptions);
        }
        else
          return HullWhiteTreeCashflowModel.ImpliedCdsCurve(settlementCash,
            BondCashflowAdapter, AsOf, Settle, DiscountCurve, SurvivalCurve,
            RecoveryRate > 0 ? RecoveryRate : 0.4,
            DiffusionProcessKind.BlackKarasinski, meanReversion_, sigma_, bond,
            UnitAccrued());
      }
      else
        return PaymentScheduleUtils.ImpliedCdsCurve(this.AsOf, this.Settle,
          BondCashflowAdapter, this.DiscountCurve,
          this.SurvivalCurve, null, 0.0, false, discountingAccrued_,
          bond.PaymentLagRule != null, this.StepSize,
          this.StepUnit, settlementCash);
    }

    /// <summary>
    ///   Calculate the Implied flat CDS Curve from a full price and a recovery rate
    /// </summary>
    /// <remarks>
    ///   <para>Implies a flat CDS curve from the current market price.</para>
    ///   <note>This function does not require an existing survival curve.</note>
    /// </remarks>
    /// <param name="recoveryRate">Recovery rate for CDS to imply</param>
    /// <returns>Implied Survival Curve fitted from a full price and a recovery rate</returns>
    public SurvivalCurve ImpliedFlatCDSCurve(double recoveryRate)
    {
      var bond = WorkBond;
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return null;
      var settlementCash = SpotFullPrice()*NotionalFactorAt(ProtectionStart);

      if (bond.Convertible && ConvertibleBondTreeModel != null)
        return ConvertibleBondTreeModel.ImpliedFlatSpreadCurve(settlementCash, 
          recoveryRate);
      else if (RequiresCallableModel)
      {
        if (VolatilityObject != null)
        {
          var flags = CashflowModelFlags;
          if (AllowNegativeCDSSpreads)
            flags |= CashflowModelFlags.AllowNegativeSpread;
          return BondCashflowAdapter.ImplyFlatSpreadCurve(settlementCash, recoveryRate,
            AsOf, Settle, DiscountCurve, OptionType.Put, ExerciseSchedule, NotificationDays,
            VolatilityObject, flags, StepSize, StepUnit, BgmTreeOptions);
        }
        else
        {
          return HullWhiteTreeCashflowModel.ImpliedFlatSpreadCurve(
            settlementCash, recoveryRate, BondCashflowAdapter, AsOf, 
            Settle, DiscountCurve, DiffusionProcessKind.BlackKarasinski, 
            meanReversion_, sigma_, bond, UnitAccrued());
        }
      }
      else
      {
        // Generate the correct cashflows
        CashflowAdapter cfa = TradeIsForwardSettle ? 
          SpotSettleCashflowAdpater : BondCashflowAdapter;
        // Bond.GenerateCashflow(null, this.AsOf, this.ReferenceCurve, 
        // this.RateResets, recoveryRate, Dt.Empty);

        try
        {
          // Imply the survival curve
          return PaymentScheduleUtils.ImpliedCdsCurve(
            AsOf, ProtectionStart, cfa, this.DiscountCurve,
            null, 0.0, false, discountingAccrued_,
            bond.PaymentLagRule != null, this.StepSize, this.StepUnit,
            settlementCash, recoveryRate, AllowNegativeCDSSpreads);
        }
        catch (SolverException)
        {
          // For a zero-coupon bond, the general model price is:
          // P = E($1 * I{tau > T} * Z(T)) + E($R * Integral(Z(tau) * dQ(tau), 0 < tau < T)
          // This formula sometimes lead to non-monotonic decreasing function as CDS spread,
          // which is counter-intuitive and sometime fails the solver.        
          // According to DK's suggestion: P(risk-free) - P(market) = CDS level x Risky Annuity
          // Here we need to set up another solver to iteratively get CDS level upon original solver failure.
          // This currently should only apply to zero-coupon bond
          SurvivalCurve bondImpliedSC = null;
          if (bond.Coupon <= 1e-4)
            bondImpliedSC = ImpliedFlatCDSCurveOnFail(
              this.RecoveryRate > 0 ? this.RecoveryRate : 0.4);
          else
          {
            //If the Bond is not a ZeroCoupon bond and we have a settlement Cash is less than recovery rate then throw 
            //an error message indicating the same
            if (settlementCash < recoveryRate)
              throw new ArgumentException(
                "Market full price is less than assumed recovery. Will not be able to imply a flat CDS curve. Try implying a CDS curve with a lower recovery rate");

          }
          if (bondImpliedSC == null)
            throw new SolverException(
              "Market level, credit spreads, and recovery assumption appear to be inconsistent.  Cannot fit implied CDS curve.");
          // If bondImpliedSC is found, return its spread
          logger.Debug("Implied flat CDS curve using fallback method");
          return bondImpliedSC;
        }
      }
    }

    /// <summary>
    ///  Imply a flat CDS curve to compute the CDS level when the ImpliedFlatCDSCurve() solver fails
    /// </summary>
    /// <param name="recoveryRate">Recovery rates</param>
    /// <returns>Implied flat CDS curve</returns>
    private SurvivalCurve ImpliedFlatCDSCurveOnFail(double recoveryRate)
    {
      var bond = WorkBond;
      // get the target full market price
      double fullPrice = FullPrice();
      // get the risk-free model price
      SurvivalCurve origSurvivalCurve = this.SurvivalCurve;
      double fullModelRiskFreePrice = 0.0;
      if (!bond.Floating)
      {
        try
        {
          this.SurvivalCurve = null;
          fullModelRiskFreePrice = FullModelPrice();
        }
        finally
        {
          this.SurvivalCurve = origSurvivalCurve;
        }
      }

      // Now we have the target full market price and will solve for a flat CDS curve        
      // Create initial survival curve with tiny spread
      var calibrator = new SurvivalFitCalibrator(this.AsOf, this.Settle, recoveryRate, this.DiscountCurve);
      var survivalCurve = new SurvivalCurve(calibrator);
      survivalCurve.AddCDS(bond.Maturity, 1e-7, DayCount.Actual360,
                           Frequency.Quarterly, BDConvention.Following, Calendar.None);
      survivalCurve.Fit();

      // If risk-free price is less than market, no solution to CDS spread
      // But a flat CDS curve with almost 0 spread (and survival probability 1)
      // can be returned
      if (fullModelRiskFreePrice < fullPrice)
        return survivalCurve;

      // Set up root finder
      Brent2 rf = new Brent2();
      rf.setToleranceX(10e-6);
      rf.setToleranceF(10e-6);

      CDSLevelOnFailFn fn = new CDSLevelOnFailFn(survivalCurve, bond.Maturity);

      // Set up target value: risk-free price - market price, reflecting the credit quality
      double target = fullModelRiskFreePrice - fullPrice;

      // Solve
      double x = -1.0;
      try
      {
        x = rf.solve(fn, target, 10e-8, 0.1);
      }
      catch (Exception)
      {}
      if (x >= 0)
      {
        survivalCurve.Spread = x;
        return survivalCurve;
      }
      return null;
    }

    /// <summary>
    ///  A helper class inherited from SolverFn to compute CDS level on original CDS level solver fail
    /// </summary>
    private class CDSLevelOnFailFn : SolverFn
    {
      public CDSLevelOnFailFn(SurvivalCurve curve, Dt bondMaturity)
      {
        survivalCurve_ = (SurvivalCurve) curve.Clone();
        x0_ = curve.Spread;
        bondMaturity_ = bondMaturity;
      }

      /// <summary>
      ///  Risky duration * Spread
      /// </summary>
      /// <param name="x">Current trial spraed</param>
      /// <returns>Risky duration * Spread</returns>
      public override double evaluate(double x)
      {
        // modify the current spread state
        survivalCurve_.Spread = x0_ + x;

        // Compute the risk annuity
        double riskyAnnuity = CurveUtil.ImpliedDuration(survivalCurve_, 
          survivalCurve_.Calibrator.Settle,
          bondMaturity_, DayCount.Actual360, Frequency.Quarterly, 
          BDConvention.Following, Calendar.NYB);

        // Compute Risky Annuity * Spread
        double res = riskyAnnuity*survivalCurve_.Spread;
        return res;
      }

      private SurvivalCurve survivalCurve_;
      private readonly double x0_;
      private Dt bondMaturity_;
    }

    /// <summary>
    ///   Calculate the bond-implied CDS level (implied by the current market price of the bond)
    /// </summary>
    /// <remarks>
    ///   <para>Calculates the constant spread (in basis points) at the bond maturity date from 
    ///   the implied flat credit curve for cashflow to match a specified full price.
    ///   </para>
    ///   <para>Works for both the BondCashflow and HWTreeCallableBond pricers.</para>
    /// </remarks>
    /// <returns>Bond-Implied CDS level at the bond's effective duration</returns>
    public double ImpliedCDSLevel()
    {
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;

      if (HasDefaulted)
        return Double.NaN;

      // Get implied survival curve
      SurvivalCurve bondImpliedSC = ImpliedFlatCDSCurve(
        this.RecoveryRate > 0 ? this.RecoveryRate : 0.4);

      Dt durationDate = WorkBond.Maturity;

      // Calculate bond duration and extract spread at the duration generated date.
      return CurveUtil.ImpliedSpread(bondImpliedSC, durationDate, DayCount.Actual360,
        Frequency.Quarterly, BDConvention.Following, Calendar.None);
    }

    /// <summary>
    ///   Calculate the bond-implied CDS level (implied by the current market price of the bond)
    /// </summary>
    /// <remarks>
    ///   <para>Calculates the constant spread shift (in basis points) to the credit curve spreads for
    ///   cashflow to match a specified full price. Applies the shift to the original credit curve 
    /// and returns the implied CDS level of the shifted curve at the bond maturity date.</para>
    ///   <para>Works for both the BondCashflow and HWTreeCallableBond pricers.</para>
    /// </remarks>
    /// <returns>Bond-Implied CDS level at the bond's effective duration</returns>
    public double ShiftedCurveCDSLevel()
    {
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;

      if (HasDefaulted)
        return Double.NaN;

      if (SurvivalCurve == null)
        return Double.NaN;

      // Get the CDSCurveShift 
      double CurveShift = CDSCurveShift();

      // Apply the CDSCurveShift by bumping curve
      CalibratedCurve curve = (CalibratedCurve) SurvivalCurve.Clone();
      curve.Calibrator = (Calibrator) SurvivalCurve.SurvivalCalibrator.Clone();
      CurveUtil.CurveBump(curve, null, CurveShift*10000.0, true, false, true);

      return CurveUtil.ImpliedSpread((SurvivalCurve) curve, WorkBond.Maturity,
        DayCount.Actual360, Frequency.Quarterly,
        BDConvention.Following, Calendar.None);
    }


    /// <summary>
    /// Get Payment Schedule for this product from the specified date
    /// </summary>
    /// <param name="paymentSchedule"></param>
    /// <param name="from">Date to generate Payment Schedule from</param>
    /// <returns>
    /// PaymentSchedule from the specified date or null if not supported
    /// </returns>
    /// <remarks>
    /// Derived pricers may implement this, otherwise a NotImplementedException is thrown.
    /// </remarks>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule paymentSchedule, Dt from)
    {
      double recoverRate;
      Dt defaultDate, dfltSettle;
      SurvivalCurve.GetDefaultDates(RecoveryCurve,
        out defaultDate, out dfltSettle, out recoverRate);
      return WorkBond.GetPaymentSchedule(null, from, Dt.Empty, DiscountCurve,
        ReferenceCurve, RateResets, defaultDate, dfltSettle, recoverRate, IgnoreExDivDateInCashflow);
    }

    /// <summary>
    /// Get Payment Schedule for the bond based on the pricer AsOf and Settle dates, and using a cut-off date which
    /// is consistent with the Pv() method.
    /// </summary>
    /// <returns>
    /// PaymentSchedule based on the pricer AsOf and Settle dates.
    /// </returns>
    public PaymentSchedule GetPaymentSchedule()
    {
      var ps = GetPaymentSchedule(null, TradeSettle);
      return ps;
    }

    /// <summary>
    /// Scale the product payment schedule by trade amount and merge it with other payment and trade payment
    /// </summary>
    /// <param name="ps">Other payment schedule</param>
    /// <param name="productPs">Product payment schedule</param>
    /// <param name="from">Cut-off date</param>
    /// <param name="tradeAmt">Trade amount</param>
    /// <returns>The processed payment schedule</returns>
    public PaymentSchedule ExtractPaymentSchedule(PaymentSchedule ps, PaymentSchedule productPs, Dt from, double tradeAmt)
    {
      foreach (Payment payment in productPs)
        payment.Scale(tradeAmt);
      var psn = ps ?? new PaymentSchedule();
      Payment initPayment = Payment;
      if (initPayment != null && !initPayment.PayDt.IsEmpty() 
        && !initPayment.Amount.ApproximatelyEqualsTo(0.0))
      {
        bool payExistInProduct = false;
        IEnumerable<Payment> lstPayments = productPs.GetPaymentsOnDate(initPayment.PayDt);
        foreach (Payment p in lstPayments)
        {
          // add amount to first upfront fee or principal exange payment on this date
          if (!(p is OneTimePayment))
            continue;
          p.Amount += initPayment.Amount;
          payExistInProduct = true;
          break;
        }
        if (!payExistInProduct)
          psn.AddPayment(initPayment);

        foreach (Payment p in productPs)
        {
          if (p.CutoffDate.IsEmpty() || from < p.CutoffDate)
          {
            psn.AddPayment(p);
          }
        }
      }
      else
      {
        foreach (Payment p in productPs)
        {
          if (p.CutoffDate.IsEmpty() || from < p.CutoffDate)
          {
            psn.AddPayment(p);
          }
        }
      }
      return psn;
    }

    /// <summary>
    /// Get the cash flows start date (from date for the GetPaymentSchedule() method) based on the  pricer AsOf and Settle dates
    /// as well as the properties of the bond, so that it will be consistent with the Pv() method.
    /// </summary>
    public Dt GetCashflowsFromDate()
    {
      // Follow the logic from the Pv() method to determine the start cut-off date of the cash flows to display.
      // There are two branches there: use CumDivPv() or ProductPv().
      Dt settle = TradeSettle; // To generate cashflows, we always start with the trade settle date, not with the pricer settle date.
      var bond = WorkBond;
      bool useCumDivPv = bond.CumDiv(settle, TradeSettle) 
        || bond.HasUnSettledLagPayment(settle, TradeSettle);
      Dt cashflowsFromDate = GetCashflowsFromDate(useCumDivPv, settle);
      return cashflowsFromDate;
    }

    private Dt GetCashflowsFromDate(bool useCumDivPv, Dt settle)
    {
      var bond = WorkBond;
      Dt cashflowsFromDate;
      if (useCumDivPv)
      {
        // In this case we use the logic from the TradeCashflow function.
        cashflowsFromDate = (!bond.CumDiv(settle, TradeSettle) 
          && !IgnoreExDivDateInCashflow && settle < bond.Maturity)
          ? Dt.Add(NextCouponDate(settle), 1)
          : settle;
        if (bond.PaymentLagRule != null)
        {
          cashflowsFromDate = GenerateFromDate(settle, TradeSettle);
        }
      }
      else
      {
        // In this case we use the logic from the Cashflow function.
        cashflowsFromDate = (!bond.CumDiv(settle, settle) 
          && !IgnoreExDivDateInCashflow && settle < bond.Maturity &&
          !IsDefaulted(settle))
          ? Dt.Add(NextCouponDate(settle), 1)
          : settle;
        if (bond.PaymentLagRule != null)
        {
          cashflowsFromDate = GenerateFromDate(settle, settle);
        }
        if (SurvivalCurve != null && !SurvivalCurve.DefaultDate.IsEmpty() &&
          (SurvivalCurve.Defaulted == Defaulted.WillDefault 
          || SurvivalCurve.Defaulted == Defaulted.HasDefaulted))
        {
          if (cashflowsFromDate > settle 
            && cashflowsFromDate > SurvivalCurve.DefaultDate)
            cashflowsFromDate = settle; // restore the asOf to pick up the default recovery payment
        }
      }
      return cashflowsFromDate;
    }

    /// <summary>
    /// If the bond has defaulted, retrieve the default date and default settlement date.
    /// </summary>
    /// <param name="defaultDate">The default date</param>
    /// <param name="dfltSettle">The default settlement date (usually after the default date)</param>
    public void GetDefaultDates(out Dt defaultDate, out Dt dfltSettle)
    {
      defaultDate = Dt.Empty;
      dfltSettle = Dt.Empty;
      SurvivalCurve sc = SurvivalCurve;
      if (sc == null) return;
      double recoverRate;
      sc.GetDefaultDates(RecoveryCurve, out defaultDate, 
        out dfltSettle, out recoverRate);
    }

    #endregion ICashflow Methods

    #region Risk Methods

    /// <summary>
    ///   Calculate the bond dollar IR risk
    /// </summary>
    /// <remarks>
    ///   <para>This is the interest rate sensitivity to a 1bp increase in swap rates
    ///   as a percentage of notional. The applied bump size is 25 bps and the result is scaled
    ///   back per bp
    ///  </para>
    /// </remarks>
    /// <returns>Bond dollar IR Risk</returns>
    public double Rate01()
    {
      return Rate01(0);
    }

    /// <summary>
    ///   Calculate the bond dollar IR risk
    /// </summary>
    /// <remarks>
    ///   <para>This is the interest rate sensitivity to a 1bp increase in swap rates
    ///   as a percentage of notional. The applied bump size is 25 bps and the result is scaled
    /// back per bp</para>
    /// </remarks>
    /// <param name="bump">The bump size.</param>
    /// <returns>The normalized Rate 01</returns>
    public double Rate01(double bump)
    {
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;

      using (new ZSpreadAdjustment(this))
      {
        if (Double.IsNaN(bump) || bump <= 0)
        {
          bump = Bond.Convertible ? 10 : 25; //bps
        }
        var ir01 = Sensitivities.IR01(this, bump, bump, false);
        return ir01 * (-1.0);
      }
    }

    /// <summary>
    ///   Calculate the bond dollar rate convexity
    /// </summary>
    /// <remarks>
    ///   <para>This is the interest rate convexity of the bond. Together, the duration and convexity estimate
    ///   the percentage change in the bond price following a parallel shift of the swap rate curve as:
    ///   <math>
    ///     \frac { \delta{p} } { p } = -D \frac { \delta Y } { 100 } + \frac{1}{2} C (\frac { \delta Y } { 100 })^2
    ///   </math>
    ///   where <m>\delta{Y}</m> is the shift in percentage points to the swap rates, i.e. a 25bps shift is 
    ///   <m>\delta{Y} = 0.25</m>. The convexity is calculated from the bond values when the swap rates are shifted 
    ///   up 25bps and shifted down 25bps using also the bond price (no shift).
    ///   The results are scaled back per bp.
    ///   </para>
    /// </remarks>
    /// <returns>Bond Interest Rate Convexity</returns>
    public double RateConvexity()
    {
      return RateConvexity(0);
    }

    /// <summary>
    ///   Calculate the bond dollar rate convexity
    /// </summary>
    /// <remarks>
    ///   <para>This is the interest rate convexity of the bond. Together, the duration and convexity estimate
    ///   the percentage change in the bond price following a parallel shift of the swap rate curve as:
    ///   <math>
    ///     \frac { \delta{p} } { p } = -D \frac { \delta Y } { 100 } + \frac{1}{2} C (\frac { \delta Y } { 100 })^2
    ///   </math>
    ///   where <m>\delta{Y}</m> is the shift in percentage points to the swap rates, i.e. a 25bps shift is 
    ///   <m>\delta{Y} = 0.25</m>. The convexity is calculated from the bond values when the swap rates are shifted 
    ///   up 25bps and shifted down 25bps using also the bond price (no shift).
    /// The results are scaled back per bp.</para>
    /// <para>The unit of convexity is years^2</para>
    /// </remarks>
    /// <param name="bump">The bump size</param>
    /// <returns>The calculated rate convexity</returns>
    public double RateConvexity(double bump)
    {
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;

      double d2PdY2;
      using (new ZSpreadAdjustment(this))
      {

        if (Double.IsNaN(bump) || bump <= 0)
        {
          bump = 25; //bps
        }
        System.Data.DataTable tbl = Sensitivities.Rate(new[] {this}, null, 0,
          bump, bump, false, true, BumpType.Uniform, null, true, false, null,
          false, null, null, "Keep", null);
        d2PdY2 = (double) tbl.Rows[0]["Gamma"]*10000.0*10000.0;
      }
      return d2PdY2/(this.EffectiveNotional*MarketOrModelPrice());
    }

    /// <summary>
    ///   Calculate the bond effective duration based on the model spread sensitivity
    /// </summary>
    /// <remarks>
    ///   <para>This is the interest rate duration of the bond. The duration is calculated using the bond values
    /// arising when the Libor curve is shifted up 25bp and down 25bp. The result is scaled back per bp.</para>
    /// <math>
    ///   D_{eff} =  (\frac { P(+ \Delta Y) - P(- \Delta Y)} { 2 * \Delta Y})/P
    /// </math>
    /// <para>where Y ( =25bps ) is the shift applied to the swap rates.</para>
    /// <para>The unit of IR duration is years</para> 
    /// </remarks>
    /// <returns>Bond effective duration</returns>
    public double RateDuration()
    {
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;

      return 10000.0 * Rate01() / this.CurrentNotional / MarketOrModelPrice();
    }

    /// <summary>
    ///   Calculate the bond ZSpread dollar sensitivity
    /// </summary>
    /// <remarks>
    ///   <para>This is theoretical sensitivity of the bond calculated by bumping the ZSpread.
    ///   It gives the price increase of the bond per 25bp decrease in the ZSpread scaled 
    ///   back per bps</para>
    /// </remarks>
    /// <returns>Bond ZSpread dollar 01</returns>
    public double ZSpread01()
    {
      return ZorRSpreadSensitivity(false, 25, 25, false, true, false).Item1;
    }


    /// <summary>
    /// Calculate the bond zspread convexity 
    /// </summary>
    /// <remarks>
    /// The convexity is calculated from the bond values when the discount curve 
    /// spread are shifted up and down 25bps and also the bond price (no shift). 
    /// The results are scaled back per bp.
    /// 
    /// <math>
    /// Convexity =\frac{Pv(\Delta)+Pv(-\Delta)-2*Pv(0)}{\Delta^2}
    /// </math>
    /// 
    /// where <m>\Delta</m> is 25pb
    /// </remarks>
    /// 
    /// <returns>Bond ZSpread convexity</returns>
    public double ZSpreadConvexity()
    {
      return ZorRSpreadSensitivity(false, 25, 25, false, true, false, true).Item2;
    }


    /// <summary>
    /// Calculate the bond RSpread dollar sensitivity
    /// </summary>
    /// <remarks>
    /// <para>This is theoretical sensitivity of the bond calculated by bumping the spread on 
    /// the discount curve. It gives the price increase of the bond per 25bp decrease in the ZSpread scaled 
    /// back per bps</para>
    /// <para>Similar to zspread calculation but takes into account the likelihood
    /// of default.</para>
    /// </remarks>
    /// <returns>Bond RSpread dollar 01</returns>
    public double RSpread01()
    {
      return ZorRSpreadSensitivity(true, 25, 25, false, true, false).Item1;
    }

    /// <summary>
    /// Returns the average change in PV for a relative up and down bump in ZSpread.
    /// </summary>
    /// <param name="bump">Factor by which to bump up and down</param>
    /// <remarks>
    /// <para>Pricer's implied zspread is first applied, then change in Pv determined for a relative bump up and down.</para>
    /// <para>If original zspread is Z, then bumped zspreads are <m>(1 + bump)*Z</m> and <m>(1-bump)*Z</m> respectively.</para>
    /// </remarks>
    /// <returns>(Pv(down bump) - Pv(up bump))/2.0</returns>
    public double ZSpreadScenario(double bump)
    {
      return ZorRSpreadSensitivity(false, bump, bump, true, false, true).Item1;
    }

    public Tuple<double, double> ZorRSpreadSensitivity(bool withSurvivalCurve, double dYUp, double dYDown, 
      bool relative, bool scaleDelta, bool forceUseZSpreadAdj, bool calcConvexity = false)
    {
      // Validate state of bond
      if (!IsProductActive() || IsOnLastDate())
        return new Tuple<double, double>(0.0, 0.0);

      // not supported yet for convertible
      if (this.Bond.Convertible && this.ConvertibleBondTreeModel != null)
        throw new ArgumentException((withSurvivalCurve ? "R" : "Z") + "Spread Sensitivities are not yet supported for convertible bond");

      // validate bump requests
      if (dYUp.ApproximatelyEqualsTo(0.0) && dYDown.ApproximatelyEqualsTo(0.0))
        throw new ArgumentException((withSurvivalCurve ? "R" : "Z") + "Spread Sensitivities require non-zero up and/or down bumps");

      if (dYUp < 0.0 || dYDown < 0.0)
        throw new ArgumentException((withSurvivalCurve ? "R" : "Z") + "Spread Sensitivities require positive up and/or down bumps");

      // determine bumps and scale factor
      var scale = DetermineZorRSpreadBumpsandScaleFactor(
        withSurvivalCurve, ref dYUp, ref dYDown, relative, 
        scaleDelta, forceUseZSpreadAdj, out var spread, 
        out var scaledUp, out var scaledDown);

      // We work on locally cloned pricer and curve,
      // so the original pricer is intact, which also
      // means that this method is thread safe.

      double origSpread = DiscountCurve.Spread;
      var pricer = (BondPricer)Clone();
      pricer.AsOf = pricer.ProductSettle;
      var discCurve = CloneToBondConventions(this.DiscountCurve);
      pricer.DiscountCurve = discCurve;

      if (HasDefaulted)
      {
        var ds = GetDefaultPayment(BondCashflowAdapter);
        double zsprd01 = ds == null || ds.Amount.AlmostEquals(0.0)
          ? 0.0
          : BondModelDefaulted.ZSpreadSensitivity(ds.RecoveryAmount, DefaultPaymentDate,
            ds.AccrualAmount, AsOf, discCurve, spread, dYUp, dYDown);

        zsprd01 /= scale;

        double zsprdConvexity = ds == null || ds.Amount.AlmostEquals(0.0)
          ? 0.0
          : BondModelDefaulted.ZSpreadConvexity(ds.RecoveryAmount, DefaultPaymentDate,
            ds.AccrualAmount, AsOf, discCurve, spread, scaledUp, scaledDown, dYUp, dYDown);

        zsprdConvexity /= (scaledUp + scaledDown)/2;

        return new Tuple<double, double>((zsprd01)*(-1.0), zsprdConvexity);
      }

      bool regenerateCashflow = false;
      if (ReferenceCurve == DiscountCurve)
      {
        pricer.ReferenceCurve = discCurve;
        regenerateCashflow = Bond.Floating;
      }

#if DEBUG
      // Initialize a pricer invariance checker.
      var checker = new ObjectStatesChecker(this);
#endif

      // ZSpread calcs are without survival curve
      if (!withSurvivalCurve)
      {
        pricer.SurvivalCurve = null;
      }

      discCurve.Spread = origSpread  + spread;
      var basePv = pricer.ProductPv();

      // downbump in OAS
      if (regenerateCashflow)
      {
        // Regenerate coupon stream with the bumped curve.
        discCurve.Spread = origSpread - dYDown;
        pricer.FixPaymentAmounts();
      }
      discCurve.Spread = origSpread - dYDown + spread;
      double downBump = pricer.ProductPv();

      // upbump in OAS
      if (regenerateCashflow)
      {
        // Regenerate coupon stream with the bumped curve.
        discCurve.Spread = origSpread + dYUp;
        pricer.FixPaymentAmounts();
      }
      discCurve.Spread = origSpread + dYUp + spread;
      double upBump = pricer.ProductPv();

      // calculate ir01.
      double ir01 = (upBump - downBump) / scale;

      // Restore the original spread:
      discCurve.Spread = origSpread;
#if DEBUG
      // Check if the pricer states changed.
      checker.CheckInvariance(this);
#endif

      double spread01 = ir01;
      double convexity = 0.0;
      if (calcConvexity)
      {
        convexity = (upBump - basePv)/scaledUp + (downBump - basePv)/scaledDown;
        convexity /= ((scaledUp + scaledDown)/2);
        convexity =convexity*10000*10000/basePv;
      }

      return new Tuple<double, double>(spread01*(-1.0), convexity);
    }

    private void FixPaymentAmounts()
    {
      if (UsePaymentSchedule)
        PsFixPaymentAmounts();
      else
        CfFixPaymentAmounts();
    }

    private void PsFixPaymentAmounts()
    {
      var ps = GetBondPayments();
      if (ps == null || ps.Count <= 0) return;
      foreach (Dt dt in ps.GetPaymentDates())
      {
        foreach (var payment in ps.GetPaymentsOnDate(dt))
        {
          payment.Amount = payment.Amount;
        }
      }
      _cfAdapter = new CashflowAdapter(ps);// Make sure to reset the cashflow adapter.
      if (!logger.IsDebugEnabled) return;
      // Fall through for logging the differences between the old and new PVs
    }

    private double DetermineZorRSpreadBumpsandScaleFactor(
      bool withSurvivalCurve, ref double dYUp, ref double dYDown, 
      bool relative, bool scaleDelta, bool forceUseZSpreadAdj, 
      out double spread, out double scaleUp, out double scaleDown)
    {
      spread = 0.0;
      double initialZSpread = withSurvivalCurve ? CalcRSpread() : ImpliedZSpread();

      if (relative)
      {
        dYDown = dYDown * initialZSpread;
        dYUp = dYUp * initialZSpread;
        scaleDown = dYDown * 10000.0;
        scaleUp = dYUp * 10000.0;
      }
      else
      {
        scaleDown = dYDown;
        scaleUp = dYUp;
        dYDown /= 10000.0;
        dYUp /= 10000.0;
      }

      // dYDown/Up now in decimals

      double scale;
      if (scaleDelta)
      {
        scale = scaleDown + scaleUp;
      }
      else
      {
        // divide by 2.0 if went both up and down, otherwise leave alone (divide by 1.0)
        double down = scaleDown.ApproximatelyEqualsTo(0.0) ? 0.0 : 1.0;
        double up = scaleUp.ApproximatelyEqualsTo(0.0) ? 0.0 : 1.0;
        scale = down + up;
      }

      if (EnableZSpreadAdjustment || forceUseZSpreadAdj)
      {
        spread = initialZSpread;
      }
      return scale;
    }

    /// <summary>
    /// Calculate the bond ZSpread Duration
    /// </summary>
    /// <remarks>
    ///   <para>This is the interest rate duration of the bond calculated by bumping the ZSpread rather
    ///   than the swap rates. It gives the percentage increase in the bond price per 25bp decrease in
    ///   the ZSpread.The result is scaled back per bp.</para>
    ///   <math>
    ///   D_{z} =  (\frac { P(+ \Delta Y) - P(- \Delta Y)} { 2 * \Delta Y})/P 
    ///   </math>
    ///   <para>where Y ( = 25bps ) is the shift applied to the zspread.</para>
    /// </remarks>
    /// <returns>Bond ZSpread-based Duration</returns>
    public double ZSpreadDuration()
    {
      return ZorRSpreadDuration(false);
    }

    /// <summary>
    /// Calculate the bond RSpread Duration
    /// </summary>
    /// <remarks>
    ///   <para>This is the interest rate duration of the bond calculated by bumping the RSpread rather
    ///   than the swap rates. It gives the percentage increase in the bond price per 25bp decrease in
    ///   the RSpread.The result is scaled back per bp.</para>
    ///   <math>
    ///     D_{rs} =  (\frac { P(+ \Delta Y) - P(- \Delta Y)} { 2 * \Delta Y})/P 
    ///   </math>
    ///   <para>where Y ( = 25bps ) is the shift applied to the rspread.</para>
    ///   <para>Similar to zspread calculation but takes into acount the likelihood of default.</para>
    /// </remarks>
    /// <returns>Bond ZSpread-based Duration</returns>
    public double RSpreadDuration()
    {
      return ZorRSpreadDuration(true);
    }

    private double ZorRSpreadDuration(bool withSurvivalCurve)
    {

      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;

      if (this.Bond.Convertible && this.ConvertibleBondTreeModel != null)
        throw new ArgumentException(withSurvivalCurve ? "R" : "Z" + "SpreadDuration is not yet supported for convertible bond");

      double spread01 = 0.0;
      try
      {
        spread01 = ZorRSpreadSensitivity(withSurvivalCurve, 25, 25, false, true, false).Item1;
      }
      catch (Exception e)
      {
        e.ToString();
        throw new ToolkitException(
          "Market level, credit spreads, and recovery assumption appear to be inconsistent.  Check your inputs.  Cannot match market level."
          );
      }
      return 10000.0 * spread01 / this.CurrentNotional / MarketOrModelPrice();
    }

    /// <summary>
    ///   Calculate the bond dollar spread sensitivity
    /// </summary>
    /// <remarks>
    ///   <para>The spread sensitivity is the bond (dollar)price change (using bond notional) per 1bp decrease
    ///   in the CDS spread. The spread sensitivity is calculated using the bond values arising when the
    ///   CDS curve is parallel shifted up 5bp and down 5bp and the results are scaled back per bps. 
    ///   The Libor Curve is kept unchanged when the BCDS curve is shifted.</para>
    /// </remarks>
    /// <returns>Bond Spread dollar 01</returns>
    public double Spread01()
    {
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;

      if (this.SurvivalCurve == null)
        throw new ToolkitException("Must specify survival curve to calculation implied CDS Spread");
      using (new ZSpreadAdjustment(this))
      {
        double dY = 5; //bps
        var s01 = Sensitivities.Spread01(this, dY, dY);
        return s01;
      }
    }


    /// <summary>
    /// calculate spread01 for bond pricer
    /// </summary>
    /// <param name="bumpFlags">Bumpflag. such as BumpInPlace, RefitCurve and so on</param>
    /// <returns></returns>
    public double Spread01(BumpFlags bumpFlags)
    {
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;

      if (this.SurvivalCurve == null)
        throw new ToolkitException("Must specify survival curve to calculation implied CDS Spread");
      using (new ZSpreadAdjustment(this))
      {
        double dY = 5; //bps
        return Sensitivities2.Spread01(this,null,dY, dY, bumpFlags);
      }
    }

    /// <summary>
    /// Calculate the bond spread duration
    /// </summary>
    /// <remarks>
    ///   <para>The spread (effective) duration gives the percentage increase in the bond price per 1bp decrease 
    ///   in the CDS spread. The spread duration is calculated using the bond values arising when the
    ///   CDS curve is parallel shifted up 5bp and down 5bp. The Libor Curve is kept unchanged
    ///   when the BCDS curve is shifted. The results are scaled back per bp.</para>
    ///   <math>
    ///     D_{s} =  (\frac { P(+ \Delta Y) - P(- \Delta Y)} { 2 * \Delta Y} )/P
    ///   </math>
    ///   <para>where Y ( = 5bps ) is the shift applied to the BCDS curve.</para>
    /// </remarks>
    /// <returns>Bond Spread Duration</returns>
    public double SpreadDuration()
    {
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;
      return -10000.0 * Spread01() / this.CurrentNotional / MarketOrModelPrice();
    }

    /// <summary>
    /// Calculate spread duration.
    /// </summary>
    /// <param name="bumpFlags">BumpFlags</param>
    /// <returns></returns>
    public double SpreadDuration(BumpFlags bumpFlags)
    {
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;
      return -10000.0 * Spread01(bumpFlags) / this.CurrentNotional / MarketOrModelPrice();
    }

    /// <summary>
    /// Calculate the Spread Convexity (gamma) of the bond
    /// </summary>
    /// <remarks>
    ///   <para>Calculates the change in the bond spread delta for a 1bp
    ///   increase in the CDS spread.</para>
    ///   <math>
    ///     C_{s} = (\frac { P(+ \Delta Y) + P(- \Delta Y) - 2*P} { \Delta Y * \Delta Y } )/P
    ///   </math>
    ///   <para>where Y ( = 25bps ) is the shift applied to the BCDS curve.</para>
    /// </remarks>
    /// <returns>Spread Convexity of the Bond</returns>
    public double SpreadConvexity()
    {
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;
      double d2PdY2;
      using (new ZSpreadAdjustment(this))
      {
        double dY = 25; //bps
        double gamma = Sensitivities.SpreadGamma(this, dY, dY);
        d2PdY2 = gamma * 10000.0 * 10000.0;
      }
      return d2PdY2 / (this.EffectiveNotional * MarketOrModelPrice());
    }

    /// <summary>
    /// Spread convexity
    /// </summary>
    /// <param name="bumpFlags">BumpFlags</param>
    /// <returns></returns>
    public double SpreadConvexity(BumpFlags bumpFlags)
    {
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;
      double d2PdY2;
      using (new ZSpreadAdjustment(this))
      {
        double dY = 25; //bps
        double gamma = Sensitivities2.SpreadGamma(this, null, dY, dY, bumpFlags);
        d2PdY2 = gamma * 10000.0 * 10000.0;
      }
      return d2PdY2 / (this.EffectiveNotional * MarketOrModelPrice());
    }

    /// <summary>
    ///   Calculate discount rate spread implied by the full price of the bond.
    /// </summary>
    /// <remarks>
    ///   <para>Calculates the constant spread (continuously compounded) over
    ///   discount curve for cashflow to match the current price.</para>
    ///   <para>Similar to zspread calculation but takes into account the likelihood
    ///   of default.</para>
    /// </remarks>
    /// <param name="fixStock">Fix stock price for convertible bonds</param>
    /// <returns>spread over discount curve implied by price</returns>
    public double CalcRSpread(bool fixStock = false)
    {
      var bond = WorkBond;
      // Validate
      if (!IsProductActive() || IsOnLastDate())
        return 0.0;
      if (rspread_ != null) return (double)rspread_;
      var origCurve = this.DiscountCurve;
      var newCurve = this.CloneToBondConventions(origCurve);
      if (HasDefaulted)
      {
        // Defaulted
        double settlementCash = FullPrice()*NotionalFactor;
        var ds = GetDefaultPayment(BondCashflowAdapter);
        rspread_ = ds == null || ds.Amount.AlmostEquals(0.0)
          ? 0.0
          : BondModelDefaulted.ImpliedDiscountSpread(settlementCash, DefaultPaymentDate,
            ds.RecoveryAmount, ds.AccrualAmount, AsOf, newCurve);
      }
      else if (bond.Convertible && ConvertibleBondTreeModel != null)
      {
        if (MarketQuote <= 0)
          return 0;
        // Convertible
        var calc = (BondPricer) MemberwiseClone();
        calc.DiscountCurve = newCurve;
        rspread_ = calc.ConvertibleBondTreeModel.ImpliedDiscountSpread(FullPrice(), true, fixStock);
      }
      else if (RequiresCallableModel)
      {
        // Callable
        if (VolatilityObject != null)
        {
          rspread_ = BondCashflowAdapter.ImplyDiscountSpread(FullPrice(),
            Settle, Settle, newCurve, SurvivalCurve,
            OptionType.Put, ExerciseSchedule, NotificationDays, VolatilityObject,
            CashflowModelFlags, StepSize, StepUnit, BgmTreeOptions);
        }
        else
        {
          rspread_ = HullWhiteTreeCashflowModel.ImpliedDiscountSpread(
            FullPrice(), BondCashflowAdapter, AsOf, Settle, newCurve, SurvivalCurve,
            RecoveryRate, DiffusionProcessKind.BlackKarasinski,
            meanReversion_, sigma_, bond, UnitAccrued());
        }
      }
      else
      {
        // General case
        Dt settle = ProtectionStart;
        double settlementCash = FullPrice()*NotionalFactor;
        try
        {
          rspread_ = PaymentScheduleUtils.ImpDiscountSpread(BkwdCompatibleAdapter(), settle,
            settle, newCurve, survivalCurve_, null, 0.0,
            StepSize, StepUnit, settlementCash,
            AdapterUtil.CreateFlags(false, false, discountingAccrued_));
        }
        catch (SolverException ex)
        {
          ex.ToString();
          throw new ToolkitException(
            String.Format("Unable to Imply an RSpread for FullPrice {0}. Please check your inputs.", settlementCash));
        }
      }
      return (double)rspread_;
    }

    /// <summary>
    /// This function is particularly for the RSpread backward compatible.
    /// In the cashflow method, the boolean type creditRiskToPaymentDate
    /// is always false, yet it should be decided by the bond.PaymentLagRule.
    /// In this function, the right behavior should directly return BondCashflowAdapter.
    /// Pls note in the payment schedule, the effect of the flag creditRiskToPaymentDate
    /// reveals in the function AddRecoveryPayments. 
    /// </summary>
    /// <returns></returns>
    private CashflowAdapter BkwdCompatibleAdapter()
    {
      var bond = WorkBond;
      if (bond.PaymentLagRule == null)
        return BondCashflowAdapter;

      var recoveryRate = RecoveryRate;
      Dt defaultDate, dfltSettle;
      GetDefaultDates(out defaultDate, out dfltSettle);
      var oPayLagRule = bond.PaymentLagRule; 
      try
      {
        var pps = bond.GetPaymentSchedule(null, Settle, Dt.Empty,
            DiscountCurve, ReferenceCurve, RateResets, defaultDate, dfltSettle, 
            recoveryRate, IgnoreExDivDateInCashflow).FilterPayments(Settle);
        bond.PaymentLagRule = null;
        var payments = pps.AddRecoveryPayments(bond, dt => recoveryRate);

        return new CashflowAdapter(payments);
      }
      finally
      {
        bond.PaymentLagRule = oPayLagRule;
      }
    }


    #endregion Risk Methods

    #region Repo Curve Methods

    /// <summary>
    /// Interpolate the repo rate at forward date
    /// </summary>
    /// <param name="settle">Forward Date</param>
    /// <returns>Repo rate</returns>
    public double RepoRateAt(Dt settle)
    {
      return RepoCurve.Interpolate(settle);
    }

    #endregion

    #region Forward Methods

    /// <summary>
    ///   Calculate forward full price
    /// </summary>
    /// <remarks>
    ///   <para>Calculates forward full price to a forward date
    ///   <paramref name="fwdSettleDate"/> using the current market price.</para>
    ///   <note>Note: Ignores any call features of the bond.</note>
    /// </remarks>
    /// <param name="fwdSettleDate">forward settlement date</param>
    /// <returns>Forward full price</returns>
    public double FwdFullPrice(Dt fwdSettleDate)
    {
      return FwdFullPrice(fwdSettleDate, this.FullPrice());
    }

    /// <summary>
    ///   Calculate forward full price
    /// </summary>
    /// <remarks>
    ///   <para>Calculates forward full price to a forward date
    ///   <paramref name="fwdSettleDate"/> using the current market price.</para>
    ///   <note>Note: Ignores any call features of the bond.</note>
    ///   <para>See <see cref="CashflowModel"/> for more calculation details.</para>
    /// </remarks>
    /// <param name="fwdSettleDate">forward settlement date</param>
    /// <param name="fullPrice">Full price of bond</param>
    /// <returns>Forward full price of Bond</returns>
    public double FwdFullPrice(Dt fwdSettleDate, double fullPrice)
    {
      var cfa = BondCashflowAdapter;
      // Validate
      if (!IsProductActive(fwdSettleDate) || IsOnLastDate(fwdSettleDate))
        return 0.0;
      if (HasDefaulted)
      {
        var ds = GetDefaultPayment(BondCashflowAdapter);
        //  amount.AlmostEquals(0.0) && accrual.AlmostEquals(0.0)
        return ds == null || ds.Amount.AlmostEquals(0.0)
          ? 0.0
          : BondModelDefaulted.FwdPrice(fullPrice, DefaultPaymentDate, ds.RecoveryAmount,
            ds.AccrualAmount, AsOf, fwdSettleDate, DiscountCurve);
      }
      return PaymentScheduleUtils.FwdValue(cfa, ProtectionStart, fullPrice,
        discountCurve_, survivalCurve_, null, 0.0, false, false, discountingAccrued_,
        StepSize, StepUnit, fwdSettleDate);
    }

    /// <summary>
    ///   Calculate forward dollar value
    /// </summary>
    /// <remarks>
    ///   <para>Calculates forward dollar value to a forward date
    ///   <paramref name="fwdSettleDate"/> using the current market price.</para>
    ///   <note>Note: Ignores any call features of the bond.</note>
    /// </remarks>
    /// <param name="fwdSettleDate">forward settlement date</param>
    /// <returns>Forward dollar value</returns>
    public double FwdValue(Dt fwdSettleDate)
    {
      return FwdFullPrice(fwdSettleDate) * Notional;
    }

    /// <summary>
    ///   Calculate forward (convexity adjusted) yield for a bond
    /// </summary>
    /// <remarks>
    ///   <para>Calculates forward convexity adjusted yield to a forward date
    ///   <paramref name="fwdSettleDate"/> using the specified forward full price.</para>
    ///   <para>See <see cref="YieldCAMethod"/> for details re alternate methods
    ///   for calculation of the convexity adjustment.</para>
    ///   <note>Note: Ignores any call features of the bond.</note>
    /// </remarks>
    /// <param name="fwdSettleDate">Forward settlement date</param>
    /// <param name="yieldVolatility">Forward Yield Vol</param>
    /// <param name="method">Convexity Adjustment method</param>
    /// <returns>Calculated forward yield</returns>
    public double FwdYield(Dt fwdSettleDate, double yieldVolatility, YieldCAMethod method)
    {
      var fwdFullPrice = FwdFullPrice(fwdSettleDate);
      return FwdYield(fwdFullPrice, fwdSettleDate, yieldVolatility, method);
    }

    /// <summary>
    ///   Calculate forward (convexity adjusted) yield for a bond
    /// </summary>
    /// <remarks>
    ///   <para>Calculates forward convexity adjusted yield to a forward date
    ///   <paramref name="fwdSettleDate"/> using the specified forward full price.</para>
    ///   <para>See <see cref="YieldCAMethod"/> for details re alternate methods
    ///   for calculation of the convexity adjustment.</para>
    ///   <note>Note: Ignores any call features of the bond.</note>
    /// </remarks>
    /// <param name="fwdPrice">Forward full price</param>
    /// <param name="fwdSettleDate">Forward settlement date</param>
    /// <param name="yieldVolatility">Forward Yield Vol</param>
    /// <param name="method">Convexity Adjustment method</param>
    /// <returns>Calculated forward yield</returns>
    public double FwdYield(double fwdPrice, Dt fwdSettleDate, double yieldVolatility, YieldCAMethod method)
    {
      if (method != YieldCAMethod.None && yieldVolatility <= 0.0)
        throw new ArgumentOutOfRangeException("yieldVolatility", @"Yield volatility must be greater than 0");

      if (!IsProductActive(fwdSettleDate) || IsOnLastDate(fwdSettleDate))
        return 0.0;

      var bond = WorkBond;
      double fwdAI = AccruedInterest(fwdSettleDate, fwdSettleDate);
      double fwdFlatPrice = fwdPrice - fwdAI;

      // Calculate forward yield
      double fwdYield;
      if (IsDefaulted(fwdSettleDate))
      {
        var T = Dt.Fraction(fwdSettleDate, DefaultPaymentDate, DayCount.Actual365Fixed);
        fwdYield = BondModelDefaulted.YieldFromPrice(fwdFlatPrice, T, RecoveryRate);
      }
      else if (bond.IsCustom)
      {
        // For an Amortizing Bond we first normalize the Accrued and Flat price value from their 
        // respective dollar amounts to a unit notional
        var notionalFactor = NotionalFactorAt(fwdSettleDate);
        fwdAI *= notionalFactor;
        fwdFlatPrice *= notionalFactor;
        fwdYield = PaymentScheduleUtils.PriceToYtm(BondCashflowAdapter, fwdSettleDate, ProtectionStart, bond.DayCount, bond.Freq, fwdFlatPrice, fwdAI);
        if (method != YieldCAMethod.None)
        {
          var dpdy = PaymentScheduleUtils.dPdY(BondCashflowAdapter, fwdSettleDate, fwdSettleDate, fwdYield, fwdAI, bond.DayCount, bond.Freq);
          var dp2dy2 = PaymentScheduleUtils.dP2dY2(BondCashflowAdapter, fwdSettleDate, fwdSettleDate, fwdYield, fwdAI, bond.DayCount, bond.Freq, fwdFlatPrice);
          var years = Dt.Diff(fwdSettleDate, bond.Maturity) / 365.25;
          fwdYield = BondModel.FwdCAYield(years, fwdYield, yieldVolatility, dp2dy2, dpdy, method);
        }
      }
      else
      {
        var fwdCoupon = CurrentCoupon(fwdSettleDate);
        var nextCouponDate = NextCouponDate(fwdSettleDate);
        var previousCycleDate = PreviousCycleDate(fwdSettleDate);
        var remainingCoupons = RemainingCoupons(fwdSettleDate);
        var cumDiv = bond.CumDiv(fwdSettleDate, fwdSettleDate);
        fwdYield = BondModelUtil.YieldToMaturity(fwdSettleDate, bond, previousCycleDate, nextCouponDate, bond.Maturity,
          remainingCoupons, fwdFlatPrice, fwdAI, Principal, fwdCoupon, RecoveryRate, IgnoreExDivDateInCashflow, cumDiv);
        if (method != YieldCAMethod.None)
        {
          var dpdy = BondModelUtil.dPdY(fwdSettleDate, bond, previousCycleDate, nextCouponDate, remainingCoupons,
            fwdYield, fwdAI, Principal, fwdCoupon, RecoveryRate, IgnoreExDivDateInCashflow, cumDiv);
          var dp2dy2 = BondModelUtil.dP2dY2(fwdSettleDate, bond, previousCycleDate, nextCouponDate, remainingCoupons,
            fwdFlatPrice, fwdYield, fwdAI, Principal, fwdCoupon, RecoveryRate,
            IgnoreExDivDateInCashflow, cumDiv);
          var years = Dt.Diff(fwdSettleDate, bond.Maturity) / 365.25;
          fwdYield = BondModel.FwdCAYield(years, fwdYield, yieldVolatility, dp2dy2, dpdy, method);
        }
      }
      return fwdYield;
    }

    /// <summary>
    ///   Calculate forward value of a yield 01 of bond
    /// </summary>
    /// <remarks>
    ///   <para>This is the change in forward price given a 1bp reduction in yield.</para>
    ///   <math>
    ///    FV01 = 100\times\frac{pd - pu} {2 \times yieldBump} 
    ///   </math>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><m> FV01 </m> is the forward price to yield delta</description></item>
    ///     <item><description><m> yieldBump = 0.0001</m> is the 1bp yield bump</description></item>
    ///     <item><description><m> pu = F(yield + yieldBump) </m> is the forward clean price  after 1 bp upbump in yield</description></item>
    ///     <item><description><m> pd = F(yield - yieldBump)</m> is the forard clean price after 1 bp downbump in yield</description></item>
    ///   </list>
    ///   <para>Reference: Stigum (p. 219).</para>
    ///   <para>Reference: <i>Handbook of Global Fixed Income Calculations</i> by Dragomir Krgin</para>
    /// </remarks>
    /// <param name="fwdSettleDate">Forward settlement date</param>
    /// <returns>Forward price value of a yield 01</returns>
    public double Fv01(Dt fwdSettleDate)
    {
      // Validate
      if (!IsProductActive(fwdSettleDate) || IsOnLastDate(fwdSettleDate))
        return 0.0;
      var fullPrice = FullPrice();
      var pv01 = PV01();
      var fwdPriceDown = FwdFullPrice(fwdSettleDate, fullPrice - pv01/2.0);
      var fwdPriceUp = FwdFullPrice(fwdSettleDate, fullPrice + pv01/2.0);
      return fwdPriceUp - fwdPriceDown;
    }

    /// <summary>
    ///   Calculate forward value of a forward yield 01 of bond
    /// </summary>
    /// <remarks>
    ///   <para>This is the change in forward price given a 1bp reduction in forward yield.</para>
    ///   <math>
    ///    FV01 = 100\times\frac{pd - pu} {2 \times yieldBump} 
    ///   </math>
    ///   <para>where</para>
    ///   <list type="bullet">
    ///     <item><description><m> FV01 </m> is the forward price to forward yield delta</description></item>
    ///     <item><description><m> yieldBump = 0.0001</m> is the 1bp yield bump</description></item>
    ///     <item><description><m> pu = F(yield_f + yieldBump) </m> is the forward clean price after 1 bp upbump in forward yield</description></item>
    ///     <item><description><m> pd = F(yield_f - yieldBump)</m> is the forard clean price after 1 bp downbump in forward yield</description></item>
    ///   </list>
    ///   <para>Reference: Stigum (p. 219).</para>
    ///   <para>Reference: <i>Handbook of Global Fixed Income Calculations</i> by Dragomir Krgin</para>
    /// </remarks>
    /// <param name="fwdSettleDate">Forward settlement date</param>
    /// <param name="fwdPrice">Forward full price</param>
    /// <param name="fwdYield">Forward yield</param>
    /// <returns>Forward price value of a forward yield 01</returns>
    public double FwdPv01(Dt fwdSettleDate, double fwdPrice, double fwdYield)
    {
      if (!IsProductActive(fwdSettleDate) || IsOnLastDate(fwdSettleDate))
        return 0.0;

      var bond = WorkBond;
      double fwdAI = AccruedInterest(fwdSettleDate, fwdSettleDate);

      double pv01;
      if (IsDefaulted(fwdSettleDate))
      {
        var T = Dt.Fraction(fwdSettleDate, DefaultPaymentDate, DayCount.Actual365Fixed);
        pv01 = BondModelDefaulted.Pv01(fwdPrice, T, RecoveryRate, fwdYield) * -1.0;
      }
      else if (bond.Floating)
      {
        var stubRate = GetStubRate(fwdSettleDate);
        var fwdCoupon = CurrentCoupon(fwdSettleDate);
        var fwdRate = (ReferenceCurve != null) ? ReferenceCurve.F(fwdSettleDate, Dt.Add(AsOf, bond.Freq, bond.EomRule), bond.DayCount, bond.Freq) : 0.0;
        var notionalFactor = NotionalFactorAt(fwdSettleDate);
        // Note: Not fully supported here. Period fractions/etc need to be reviewed or suport of forward date. RTD Aug'14
        pv01 = BondModelFRN.Pv01(CashflowPeriodFractions, CashflowPrincipals, CashflowAmounts, stubRate,
          fwdRate, bond.Coupon, DiscountMargin(), AccruedFraction, fwdCoupon, notionalFactor) * -1.0;
      }
      else if (bond.IsCustom)
      {
        // For an Amortizing Bond we first normalize the Accrued and Flat price value from their 
        // respective dollar amounts to a unit notional
        var notionalFactor = NotionalFactorAt(fwdSettleDate);
        fwdAI *= notionalFactor;
        pv01 = PaymentScheduleUtils.PV01(BondCashflowAdapter, fwdSettleDate, ProtectionStart, fwdYield, fwdAI, bond.DayCount, bond.Freq) * -1.0;
      }
      else
      {
        var fwdCoupon = CurrentCoupon(fwdSettleDate);
        var nextCouponDate = NextCouponDate(fwdSettleDate);
        var previousCycleDate = PreviousCycleDate(fwdSettleDate);
        var remainingCoupons = RemainingCoupons(fwdSettleDate);
        var cumDiv = bond.CumDiv(fwdSettleDate, fwdSettleDate);
        pv01 = BondModelUtil.PV01(fwdSettleDate, bond, fwdYield, previousCycleDate, nextCouponDate,
          remainingCoupons, fwdCoupon, Principal, RecoveryRate, cumDiv, IgnoreExDivDateInCashflow) * -1.0;
      }
      return pv01;
    }

    #endregion Forward Methods

    #region Properties

    /// <summary>
    /// Bond being priced
    /// </summary>
    public Bond Bond
    {
      get { return (Bond)this.Product; }
    }

    /// <summary>
    /// Adjustment to use for model pricing. Used to match model futures price to market futures price
    /// </summary>
    /// <remarks>
    /// <para>This is the basis used in the pricing of the contract to match quote (if available).
    /// This must be set explicitly. There are methods for calculating this implied basis.</para>
    /// </remarks>
    public double ModelBasis { get; set; }

    /// <summary>
    /// Princial redemption
    /// </summary>
    public double Principal
    {
      get { return this.Product.Notional; }
    }

    /// <summary>
    ///   Effective notional on the settlement date
    /// </summary>
    /// <remarks>
    ///   <para>This is the effective notional at the settlement
    ///   date. It includes adjustments based on amortizations
    ///   and any defaults prior to the settlement date</para>
    /// </remarks>
    ///
    public override double EffectiveNotional
    {
      get
      {
        double unitNotional = NotionalFactorAt(Settle);
        return unitNotional * Notional;
      }
    }

    /// <summary>
    ///   Price/yield method.
    /// </summary>
    public PriceYieldMethod PriceYieldMethod
    {
      get { return priceYieldMethod_; }
      set
      {
        priceYieldMethod_ = value;
        Reset();
      }
    }

    /// <summary>
    ///   Market quote for bond
    /// </summary>
    /// <details>
    ///   <para>A variety of quoting types are supported for bonds
    ///   and are set by <see cref="QuotingConvention"/>. The default
    ///   quoting convention is FlatPrice.</para>
    /// </details>
    public double MarketQuote
    {
      get { return marketQuote_ == null ? 0 : (double)marketQuote_; }
      set
      {
        marketQuote_ = value;
        Reset(QuoteChanged);
      }
    }

    /// <summary>
    ///   Quoting convention for bond
    /// </summary>
    public QuotingConvention QuotingConvention
    {
      get { return quotingConvention_; }
      set
      {
        quotingConvention_ = value;
        Reset(QuoteChanged);
      }
    }

    /// <summary>
    /// Regular payment schedule
    /// </summary>
    public PaymentSchedule PaymentSchedule
    {
      get
      {
        if (_paymentSchedule == null)
          _paymentSchedule = GetBondPayments();
        return _paymentSchedule;
      }
    }

    /// <summary>
    /// Trade payment schedule
    /// </summary>
    public PaymentSchedule TradePaymentSchedule
    {
      get
      {
        if (_tradePaymentSchedule == null)
          _tradePaymentSchedule = GetTradePayments();
        return _tradePaymentSchedule;
      }
    }

    /// <summary>
    /// Spot settle payment schedule
    /// </summary>
    public PaymentSchedule SpotSettlePaymentSchedule
    {
      get
      {
        if (_spotSettlePaymentSchedule == null)
          _spotSettlePaymentSchedule = GetSpotSettlePayments();
        return _spotSettlePaymentSchedule;
      }
    }

    /// <summary>
    /// This corresponds to the Property PaymentSchedule/Cashflow. 
    /// </summary>
    public CashflowAdapter BondCashflowAdapter
    {
      get
      {
        if (_cfAdapter == null)
        {
          _cfAdapter = UsePaymentSchedule
            ? new CashflowAdapter(PaymentSchedule)
            : new CashflowAdapter(Cashflow);
        }
        return _cfAdapter;
      }
    }

    /// <summary>
    /// corresponds to the Property TradePaymentSchedule/TradeCashflow.
    /// </summary>
    public CashflowAdapter BondTradeCashflowAdapter
    {
      get
      {
        if (_tradeCfAdapter == null)
        {
          _tradeCfAdapter = UsePaymentSchedule
            ? new CashflowAdapter(TradePaymentSchedule)
            : new CashflowAdapter(TradeCashflow);
        }
        return _tradeCfAdapter;
      }
    }

    /// <summary>
    /// Corresponds to the Property SpotSettlePaymentSchedule/SpotSettleCashflow.
    /// </summary>
    public CashflowAdapter SpotSettleCashflowAdpater
    {
      get
      {
        if (_spotSettleCfAdapter == null)
        {
          _spotSettleCfAdapter = UsePaymentSchedule
            ? new CashflowAdapter(SpotSettlePaymentSchedule)
            : new CashflowAdapter(SpotSettleCashflow);
        }
        return _spotSettleCfAdapter;
      }
    }

    CashflowAdapter ICashflowPricer.Cashflow => BondCashflowAdapter;

    public PaymentSchedule GetTradePayments()
    {
      Dt defaultDate, dfltSettle;
      GetDefaultDates(out defaultDate, out dfltSettle);
      if (!TradeSettle.IsEmpty())
      {
        Dt from = GetCashflowsFromDate(true, Settle);
        return WorkBond.GetBondPayments(TradeSettle, from,
          DiscountCurve, ReferenceCurve, RateResets, RecoveryRate,
          defaultDate, dfltSettle, IgnoreExDivDateInCashflow);
      }
      return GetBondPayments();
    }

    public PaymentSchedule GetBondPayments()
    {
      Dt defaultDate, dfltSettle;
      GetDefaultDates(out defaultDate, out dfltSettle);
      return WorkBond.GetBondPayments(Settle, Settle, this.DiscountCurve,
        this.ReferenceCurve, this.RateResets, this.RecoveryRate,
        defaultDate, dfltSettle, IgnoreExDivDateInCashflow);
    }

    public PaymentSchedule GetSpotSettlePayments()
    {
      var bond = WorkBond;
      Dt nextCoupon = NextCouponDate(AsOf);
      Dt defaultDate, dfltSettle;
      GetDefaultDates(out defaultDate, out dfltSettle);
      Dt asOf = (!defaultDate.IsValid() && 
        !bond.CumDiv(ProductSettle, ProductSettle) && 
        !IgnoreExDivDateInCashflow && 
        nextCoupon.IsValid()) ? Dt.Add(nextCoupon, 1) : AsOf;
      if (bond.PaymentLagRule != null)
      {
        asOf = GenerateFromDate(ProductSettle, ProductSettle);
      }

      return bond.GetBondPayments(asOf, asOf, DiscountCurve,
        ReferenceCurve, RateResets, RecoveryRate,
        defaultDate, dfltSettle, IgnoreExDivDateInCashflow);
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
    ///  Assumed Rate for Floating Rate NOtes 
    /// </summary>
    public double AssumedRate
    {
      get
      {
        if (assumedRate_ == null)
        {
          Dt nextCoupon = NextCouponDate();
          Dt previousCoupon = PreviousCouponDate();
          double quotedMargin = this.Bond.Coupon;
          double Dr = Dt.Fraction(previousCoupon, nextCoupon, previousCoupon, nextCoupon, Bond.DayCount, Bond.Freq) *
                      ((int)Bond.Freq);
          assumedRate_ = CurrentRate / (Dr) - quotedMargin;
        }
        return (double)assumedRate_;
      }
      set { assumedRate_ = value; }
    }

    /// <summary>
    ///   Reference Curve used for pricing of floating-rate cashflows
    /// </summary>
    public DiscountCurve ReferenceCurve
    {
      get { return referenceCurve_; }
      set
      {
        referenceCurve_ = value;
        Reset();
      }
    }

    /// <summary>
    ///   Survival curve used for pricing
    /// </summary>
    public SurvivalCurve SurvivalCurve
    {
      get { return survivalCurve_; }
      set
      {
        survivalCurve_ = value;
        Reset();
      }
    }

    /// <summary>
    ///   Counterparty curve used for pricing
    /// </summary>
    public SurvivalCurve CounterpartyCurve
    {
      get { return null; }
      set { throw new NotImplementedException("Counterparty risk not supported for Bond Pricer"); }
    }

    /// <summary>
    ///   Recovery curve
    /// </summary>
    /// <remarks>
    ///   <para>If a separate recovery curve has not been specified, the recovery from the survival
    ///   curve is used. In this case the survival curve must have a Calibrator which provides a
    ///   recovery curve otherwise an exception will be thrown.</para>
    /// </remarks>
    public RecoveryCurve RecoveryCurve
    {
      get
      {
        if (recoveryCurve_ != null)
          return recoveryCurve_;
        else if (survivalCurve_ != null && survivalCurve_.SurvivalCalibrator != null)
          return survivalCurve_.SurvivalCalibrator.RecoveryCurve;
        else
          return null;
      }
      set
      {
        recoveryCurve_ = value;
        Reset();
      }
    }

    /// <summary>
    ///   Historical floating rate coupons
    /// </summary>
    public RateResets RateResets
    {
      get
      {
        // Make sure it's non-null.
        if (rateResetsList_ == null)
        {
          rateResetsList_ = RateResetsList.Create(Double.NaN, Double.NaN,
                                                  Product.Effective, Bond.BackwardCompatibleCashflow);
        }
        return rateResetsList_;
      }
    }

    /// <summary>
    ///   Historical floating rate coupons
    /// </summary>
    IList<RateReset> ICashflowPricer.RateResets
    {
      get { return rateResetsList_; }
    }

    /// <summary>
    ///   Current floating rate coupon
    /// </summary>
    public double CurrentRate
    {
      get
      {
        if (currentCoupon_ == null)
          currentCoupon_ = CurrentCoupon(Settle);

        return (double)currentCoupon_;
      }
      set
      {
        // Set the RateResets to support returning the current coupon
        if (rateResetsList_ == null)
        {
          var bond = WorkBond;
          rateResetsList_ = RateResetsList.Create(value, Double.NaN,
                                                  bond.Effective, bond.BackwardCompatibleCashflow);
        }
        rateResetsList_.CurrentReset = value;
        Reset();
      }
    }

    /// <summary>
    ///   Return the recovery rate matching the maturity of this product.
    /// </summary>
    public double RecoveryRate
    {
      get { return (RecoveryCurve != null) ? RecoveryCurve.RecoveryRate(Product.Maturity) : 0.0; }
    }

    /// <summary>
    ///   Step size for pricing grid
    /// </summary>
    public int StepSize
    {
      get { return stepSize_; }
      set
      {
        stepSize_ = value;
        Reset();
      }
    }

    /// <summary>
    ///   Step units for pricing grid
    /// </summary>
    public TimeUnit StepUnit
    {
      get { return stepUnit_; }
      set
      {
        stepUnit_ = value;
        Reset();
      }
    }

    /// <summary>
    ///   Call option BK mean reversion
    /// </summary>
    public double MeanReversion
    {
      get { return meanReversion_; }
      set { meanReversion_ = value; }
    }

    /// <summary>
    ///   Call option BK volatility
    /// </summary>
    public double Sigma
    {
      get { return sigma_; }
      set { sigma_ = value; }
    }

    /// <summary>
    ///  Is this product priced by yield-to-worst model for callable features?
    /// </summary>
    private bool IsYieldToWorstCallableModel
    {
      get { return _callableModel == CallableBondPricingMethod.YieldToWorst; }
    }

    /// <summary>
    ///  Should all the callable features be ignored?
    /// </summary>
    private bool IgnoreCall
    {
      get { return _callableModel == CallableBondPricingMethod.IgnoreCall; }
    }

    /// <summary>
    ///   Default correlation between credit and counterparty
    /// </summary>
    public double Correlation
    {
      get { return 0.0; }
      set { throw new NotImplementedException("Counterparty risk not supported for Bond Pricer"); }
    }

    /// <summary>
    ///  Get the initial stock price for convertible bond
    /// </summary>
    public double InitialStockPrice
    {
      get { return s0_; }
    }

    /// <summary>
    ///  Get the stock return valatility for convertible bond
    /// </summary>
    public double StockVolatility
    {
      get { return sigmaS_; }
    }

    /// <summary>
    ///  Get stock dividends schedule
    /// </summary>
    public ConvertibleBondIntersectingTreesModel.StockCorrelatedModel.StockDividends
      StockDividends
    {
      get { return dividends_; }
    }

    /// <summary>
    ///  Get correlation between stock and short rate
    /// </summary>
    public double Rho
    {
      get { return rho_; }
    }

    /// <summary>
    ///  get the speed of mean reversion of short rate for convertible bond
    /// </summary>
    public double Kappa
    {
      get { return kappa_; }
    }

    /// <summary>
    ///  Get the short rate volatility for convertible bond
    /// </summary>
    public double RateVolatility
    {
      get { return sigmaR_; }
    }

    /// <summary>
    ///  Get the number of tree steps 
    /// </summary>
    public int Steps
    {
      get { return n_; }
    }

    /// <summary>
    ///  Get and set the redemption price
    /// </summary>
    public double RedemptionPrice
    {
      get { return redemptionPrice_; }
      set { redemptionPrice_ = value; }
    }

    /// <summary>
    ///  Get/Set WithAccrualOnCall flag 
    /// </summary>
    public bool WithAccrualOnCall
    {
      get { return withAccrualOnCall_; }
      set { withAccrualOnCall_ = value; }

    }

    /// <summary>
    ///  Get/Set WithAccrualOnConversion flag
    /// </summary>
    public bool WithAccrualOnConversion
    {
      get { return withAccrualOnConversion_; }
      set { withAccrualOnConversion_ = value; }
    }

    /// <summary>
    ///   Use the market implied zspread when calculating sensitivities and callable bond option price when enabled
    /// </summary>
    /// <remarks>
    ///   <para>If this is true, the discount curve is adjusted based on the zspread implied
    ///   by the market price before any sensitivities are calculated.</para>
    /// </remarks>
    public bool EnableZSpreadAdjustment
    {
      get { return enableZSpreadAdjustment_; }
      set { enableZSpreadAdjustment_ = value; }
    }

    /// <summary>
    ///   Protection start date
    /// </summary>
    /// <remarks>
    ///   <para>Default is the same as the bond settlement date, except that the bond has not been issued, in such case is effective date</para>
    /// </remarks>
    public Dt ProtectionStart
    {
      get
      {
        if (protectionStart_.IsEmpty())
        {
          protectionStart_ = TradeIsForwardSettle
            ? ProductSettle
            : Settle;
        }

        return protectionStart_;
      }
    }

    /// <summary>
    /// Utility property to get the coupon dates from the bonds coupon schedule object
    /// </summary>
    public Dt[] CouponDates
    {
      get
      {
        if (cpnDates_ == null)
        {
          var bond = WorkBond;
          Dt[] couponDates;
          double[] couponAmounts;
          Dt nextCouponDate = ScheduleUtil.NextCouponDate(Settle, bond.Effective, bond.FirstCoupon, bond.Freq, bond.EomRule);
          CouponPeriodUtil.FromSchedule(bond.CouponSchedule, nextCouponDate, bond.Maturity, bond.Coupon, out couponDates, out couponAmounts);
          cpnDates_ = couponDates;
          cpnAmounts_ = couponAmounts;

        }
        return cpnDates_;
      }
    }

    public bool HasDefaulted
    {
      get
      {
        bool isDef = IsDefaulted(AsOf);
        return isDef;
      }
    }

    public bool IsDefaulted(Dt date)
    {
      if (this.SurvivalCurve != null)
      {
        return (this.SurvivalCurve.DefaultDate.IsValid() && (this.SurvivalCurve.DefaultDate <= date));
      }
      return false;
    }

    public bool BeforeDefaultRecoverySettle(Dt date)
    {
      return (DefaultPaymentDate.IsValid() && date < DefaultPaymentDate || !DefaultPaymentDate.IsValid() && IsDefaulted(date));
    }

    /// <summary>
    /// Utility property to retrieve the coupon amounts from the bonds coupon schedule
    /// </summary>
    public double[] CouponAmounts
    {
      get
      {
        if (cpnAmounts_ == null)
        {
          var bond = WorkBond;
          Dt[] couponDates;
          double[] couponAmounts;
          Dt nextCouponDate = ScheduleUtil.NextCouponDate(Settle, bond.Effective, bond.FirstCoupon, bond.Freq, bond.EomRule);
          CouponPeriodUtil.FromSchedule(bond.CouponSchedule, nextCouponDate, bond.Maturity, bond.Coupon, out couponDates, out couponAmounts);
          cpnDates_ = couponDates;
          cpnAmounts_ = couponAmounts;

        }
        return cpnAmounts_;

      }
    }

    /// <summary>
    /// Utility property to retrieve the reset dates for a floating bond schedule
    /// </summary>
    public Dt[] ResetDates
    {
      get
      {
        if (resetDates_ == null)
        {
          if (RateResets != null && RateResets.Count > 0)
          {
            var bond = WorkBond;
            Schedule sched = new Schedule(bond.Effective, bond.Effective, bond.FirstCoupon, bond.Maturity, bond.Freq,
                                          bond.BDConvention, bond.Calendar);

            int numResets = RateResets.Count;
            resetDates_ = new Dt[numResets];
            resetRates_ = new double[numResets];
            int i = 0; // counter through resetDates/Rates
            int j = 0; // counter through schedule

            foreach (RateReset r in RateResets)
            {
              Dt resetDate = r.Date;

              // if reset was captured for a rolled period start then pass to the cashflow model
              // as the unadjusted period start; FillFloat only looks for most recent resets, <= period start
              for (; j < sched.Count; j++)
              {
                Dt periodStart = sched.GetPeriodStart(j);
                Dt adjPeriodStart = Dt.Roll(periodStart, bond.BDConvention, bond.Calendar);
                if (Dt.Cmp(resetDate, adjPeriodStart) == 0)
                {
                  resetDate = periodStart;
                  ++j; // start at next period for next rate reset
                  break;
                }
                else if (Dt.Cmp(adjPeriodStart, resetDate) > 0)
                {
                  break;
                }
              }

              resetDates_[i] = resetDate;
              resetRates_[i++] = r.Rate;
            }
          }
          else
          {
            resetDates_ = new Dt[0];
            resetRates_ = new double[0];
          }

        }
        return resetDates_;
      }
    }

    /// <summary>
    /// Utility property to retrieve the reset rates for a floating bond schedule
    /// </summary>
    public double[] ResetRates
    {
      get
      {
        if (resetRates_ == null)
        {
          if (RateResets != null && RateResets.Count > 0)
          {
            var bond = WorkBond;
            Schedule sched = new Schedule(bond.Effective, bond.Effective, bond.FirstCoupon, bond.Maturity, bond.Freq,
                                          bond.BDConvention, bond.Calendar);
            int numResets = RateResets.Count;
            resetDates_ = new Dt[numResets];
            resetRates_ = new double[numResets];
            int i = 0; // counter through resetDates/Rates
            int j = 0; // counter through schedule

            foreach (RateReset r in RateResets)
            {
              Dt resetDate = r.Date;

              // if reset was captured for a rolled period start then pass to the cashflow model
              // as the unadjusted period start; FillFloat only looks for most recent resets, <= period start
              for (; j < sched.Count; j++)
              {
                Dt periodStart = sched.GetPeriodStart(j);
                Dt adjPeriodStart = Dt.Roll(periodStart, bond.BDConvention, bond.Calendar);
                if (Dt.Cmp(resetDate, adjPeriodStart) == 0)
                {
                  resetDate = periodStart;
                  ++j; // start at next period for next rate reset
                  break;
                }
                else if (Dt.Cmp(adjPeriodStart, resetDate) > 0)
                {
                  break;
                }
              }

              resetDates_[i] = resetDate;
              resetRates_[i++] = r.Rate;
            }
          }
          else
          {
            resetDates_ = new Dt[0];
            resetRates_ = new double[0];
          }
        }
        return resetRates_;
      }
    }

    /// <summary>
    ///   Include maturity date in protection
    /// </summary>
    /// <exclude />
    public bool IncludeMaturityProtection
    {
      get { return false; }
    }

    /// <summary>
    ///   Include Settle Payments in PV
    /// </summary>
    /// <exclude />
    public bool IncludeSettlePayments
    {
      get { return false; }
    }

    private BusinessDays NotificationDays => Bond.GetNotificationDays();

    private IList<IOptionPeriod> ExerciseSchedule
    {
      get { return (Bond as ICallable).ExerciseSchedule; }
    }

    /// <summary>
    /// Gets the volatility object.
    /// </summary>
    /// <value>The volatility object.</value>
    public IVolatilityObject VolatilityObject
    {
      get { return RequiresCallableModel ? volatilityObject_ : null; }
      set { volatilityObject_ = value; }
    }

    public CashflowModelFlags CashflowModelFlags
    {
      get
      {
        return CashflowModelFlags.IncludeFees | CashflowModelFlags.IncludeProtection |
               (WorkBond.PaymentLagRule != null ? CashflowModelFlags.CreditRiskToPaymentDate : 0) |
               CashflowSettings.IgnoreAccruedInProtection |
               (discountingAccrued_ ? CashflowModelFlags.FullFirstCoupon : 0);
      }
    }

    ///<summary>
    /// Flag to indicate whether HW-callable model is needed for pricing
    ///</summary>
    public bool RequiresCallableModel
    {
      get
      {
        return Bond.Callable && (_callableModel == CallableBondPricingMethod.BlackKarasinski
          || _callableModel == CallableBondPricingMethod.LiborMarket);
      }
    }

    ///<summary>
    /// Flag to indicate whether HW-callable model is needed for pricing
    ///</summary>
    private bool RequiresHullWhiteCallableModel
    {
      get { return RequiresCallableModel && volatilityObject_ == null; }
    }

    /// <summary>
    /// BGM tree option for callable bonds
    /// </summary>
    public BgmTreeOptions BgmTreeOptions { get; set; }

    /// <summary>
    /// Gets the Notional Factor for amortizing bonds 
    /// </summary>
    public double NotionalFactor
    {
      get
      {
        if (notionalFactor_ == 0.0)
        {
          notionalFactor_ = NotionalFactorAt(Settle);
        }
        return notionalFactor_;
      }
      set { notionalFactor_ = value; }
    }

    private double NotionalFactorAt(Dt asOf)
    {
      return NotionalFactorAt(WorkBond, asOf);
    }

    private static double NotionalFactorAt(Bond bond, Dt asOf)
    {
      PaymentSchedule ps = bond.CustomPaymentSchedule;
      double not = 1.0;
      if (ps != null && ps.Count > 0)
      {
        not = PaymentScheduleUtils.GetEffectiveNotionalFromCustomizedSchedule(ps, asOf);
      }
      else if (bond.AmortizationSchedule != null && bond.AmortizationSchedule.Count > 0)
      {
        not = AmortizationUtil.PrincipalAt(bond.AmortizationSchedule, 1.0, asOf);
      }
      return not;
    }

    /// <summary>
    /// Build the binomial intersecting trees model for convertible bond
    /// </summary>
    private ConvertibleBondIntersectingTreesModel ConvertibleBondTreeModel
    {
      get
      {
        if (convertModel_ == null)
        {
          convertModel_ = new ConvertibleBondIntersectingTreesModel(
            ValidateConvertibleSupport(WorkBond), AsOf, Settle, DiscountCurve, SurvivalCurve, Steps,
            RecoveryRate, RedemptionPrice, WithAccrualOnCall, WithAccrualOnConversion, InitialStockPrice, StockVolatility,
            StockDividends, Rho, Kappa, RateVolatility, _convertibleModel);
        }
        return convertModel_;
      }
    }

    /// <summary>
    /// Cashflow amounts
    /// </summary>
    private double[] CashflowAmounts
    {
      get
      {
        if (cashflowAmounts_ == null)
        {
          var cfAdpater = BondCashflowAdapter;
          int firstIdx;
          for (firstIdx = 0; firstIdx < cfAdpater.Count; firstIdx++)
          {
            if (cfAdpater.GetEndDt(firstIdx) > Settle)
              break;
          }
          if ((cfAdpater.Count > 0) && (firstIdx != cfAdpater.Count))
          {
            cashflowAmounts_ = new double[cfAdpater.Count - firstIdx];
            int count = 0;
            for (int i = firstIdx; i < cfAdpater.Count; i++)
              cashflowAmounts_[count++] = cfAdpater.GetAmount(i);
          }
          else
          {
            cashflowAmounts_ = new double[] {};
          }

        }
        return cashflowAmounts_;
      }
    }

    /// <summary>
    /// Cashflow Period Fractions
    /// </summary>
    private double[] CashflowPeriodFractions
    {
      get
      {
        if (cashflowPeriodFractions_ == null)
        {
          var cfAdapter = BondCashflowAdapter;
          int firstIdx;
          for (firstIdx = 0; firstIdx < cfAdapter.Count; firstIdx++)
          {
            if (cfAdapter.GetEndDt(firstIdx) > Settle)
              break;
          }
          if ((cfAdapter.Count > 0) && (firstIdx != cfAdapter.Count))
          {
            cashflowPeriodFractions_ = new double[cfAdapter.Count - firstIdx];
            int count = 0;
            for (int i = firstIdx; i < cfAdapter.Count; i++)
              cashflowPeriodFractions_[count++] = cfAdapter.GetPeriodFraction(i);
          }
          else
          {
            cashflowPeriodFractions_ = new double[] {};
          }
        }
        return cashflowPeriodFractions_;
      }
    }

    /// <summary>
    /// Cashflow Principals
    /// </summary>
    private double[] CashflowPrincipals
    {
      get
      {
        if (cashflowPrincipals_ == null)
        {
          var cfAdapter = BondCashflowAdapter;
          int firstIdx;
          for (firstIdx = 0; firstIdx < cfAdapter.Count; firstIdx++)
          {
            if (cfAdapter.GetEndDt(firstIdx) > Settle)
              break;
          }
          if ((cfAdapter.Count > 0) && (firstIdx != cfAdapter.Count))
          {
            cashflowPrincipals_ = new double[cfAdapter.Count - firstIdx];
            int count = 0;
            for (int i = firstIdx; i < cfAdapter.Count; i++)
              cashflowPrincipals_[count++] = cfAdapter.GetPrincipalAt(i);
          }
          else
          {
            cashflowPrincipals_ = new double[] {};
          }
        }
        return cashflowPrincipals_;
      }
    }

    /// <summary>
    /// The Accrued Fraction of the bond
    /// </summary>
    public double AccruedFraction
    {
      get
      {
        if (accruedFraction_ == null)
        {
          var cfAdapter = BondCashflowAdapter;
          int firstIdx;
          for (firstIdx = 0; firstIdx < cfAdapter.Count; firstIdx++)
          {
            if (cfAdapter.GetEndDt(firstIdx) > Settle)
              break;
          }
          if ((cfAdapter.Count > 0) && (firstIdx != cfAdapter.Count))
          {
            var bond = WorkBond;
            accruedFraction_ = CashflowPeriodFractions[0] -
                               Dt.Fraction(cfAdapter.GetStartDt(firstIdx), cfAdapter.GetEndDt(firstIdx), Settle, cfAdapter.GetEndDt(firstIdx), bond.AccrualDayCount,
                                           bond.Freq);
          }
          else
          {
            accruedFraction_ = 0.0;
          }

        }
        return (double)accruedFraction_;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public bool DiscountingAccrued
    {
      get { return discountingAccrued_; }
      set { discountingAccrued_ = value; }
    }

    /// <summary>
    /// Flag that determines whether we are going to allow negative CDS spreads or not
    /// </summary>
    public bool AllowNegativeCDSSpreads
    {
      get { return allowNegativeCDSSpreads_; }
      set { allowNegativeCDSSpreads_ = value; }
    }

    /// <summary>
    /// boolean flag that determines whether to use the ex-div date in cashflow
    /// </summary>
    public bool IgnoreExDivDateInCashflow
    {
      get { return ignoreExDivDateInCashflow_; }
      set { ignoreExDivDateInCashflow_ = value; }
    }

    /// <summary>
    /// Get floating stub rate
    /// </summary>
    /// <returns>Floating stub rate or 0 if floating rate reference curve not set</returns>
    public double StubRate
    {
      get
      {
        if (stubRate_ == null)
          stubRate_ = GetStubRate(Settle);
        return (double)stubRate_;
      }
      set { stubRate_ = value; }
    }

    /// <summary>
    /// Get floating stub rate
    /// </summary>
    /// <param name="settle">Settlement date</param>
    /// <returns>Floating stub rate or 0 if floating rate reference curve not set</returns>
    public double GetStubRate(Dt settle)
    {
      var cfAdapter = BondCashflowAdapter;
      if (ReferenceCurve == null || cfAdapter.Count <= 0)
        return 0.0;
      for (var idx = 0; idx < cfAdapter.Count; idx++)
      {
        if (cfAdapter.GetEndDt(idx) > settle)
        {
          // first cashflow after settlement
          var T = Dt.Fraction(cfAdapter.GetStartDt(idx), cfAdapter.GetEndDt(idx), settle,
            cfAdapter.GetEndDt(idx), Bond.DayCount, Bond.Freq);
          var df = ReferenceCurve.Interpolate(cfAdapter.GetEndDt(idx)) / ReferenceCurve.Interpolate(settle);
          return RateCalc.RateFromPrice(df, T, Bond.Freq);
        }
      }
      return 0.0;
    }

    /// <summary>
    /// Calculate floating rate matching coupon period for floating rate bonds
    /// </summary>
    /// <returns>Floating rate matching coupon period for floating rate bond or 0 if no float rate reference set</returns>
    public double CurrentFloatingRate
    {
      get
      {
        if (currentFloatingRate_ == null)
          currentFloatingRate_ = (ReferenceCurve != null) ? ReferenceCurve.F(AsOf, Dt.Add(AsOf, Bond.Freq, Bond.EomRule), Bond.DayCount, Bond.Freq) : 0.0;
        return (double)currentFloatingRate_;
      }
      set { currentFloatingRate_ = value; }
    }

    /// <summary>
    /// The Default payment date for a defaulted bond 
    /// </summary>
    public Dt DefaultPaymentDate
    {
      get
      {
        if (defaultPaymentDate_ == null)
        {
          if (!HasDefaulted)
          {
            defaultPaymentDate_ = Dt.Empty;
            return (Dt)defaultPaymentDate_;
          }

          var ds = GetDefaultPayment(BondCashflowAdapter);
          if (ds != null && !ds.Amount.AlmostEquals(0.0))
          {
            defaultPaymentDate_ = (HasDefaulted) ? ds.DefaultDate : Dt.Empty;
          }
          else if (RecoveryCurve != null)
          {
            defaultPaymentDate_ = RecoveryCurve.JumpDate;
          }
          else
          {
            defaultPaymentDate_ = Dt.Empty;
          }
        }
        return (Dt)defaultPaymentDate_;
      }
    }

    ///<summary>
    /// The repo curve used to calculate forward price and discount forward PnL
    ///</summary>
    public RateQuoteCurve RepoCurve { get; set; }

    ///<summary>
    /// The bond settle date
    ///</summary>
    public Dt ProductSettle
    {
      get
      {
        if (productSettle_.IsEmpty())
          productSettle_ = Dt.AddDays(AsOf, settlementDays_.HasValue ? (int)settlementDays_ : 0, Bond.Calendar);

        return productSettle_;
      }
      set { productSettle_ = value; }
    }

    ///<summary>
    /// The forward settle date if pricer settle is different than trade settle and it's forward-settle/issue trade
    ///</summary>
    public Dt ForwardSettle
    {
      get { return fwdSettle_; }
      set
      {
        //Make the ProductSettle remember the regular settle date, 
        //and Settle date to be Trade Settle for forward-settle or forward-issue bond trades
        fwdSettle_ = value;
        ProductSettle = Settle;
        if (!settlementDays_.HasValue && ProductSettle != Dt.Empty && AsOf != Dt.Empty)
          settlementDays_ = ProductSettle.IsValidSettlement(Bond.Calendar) ? Dt.BusinessDays(AsOf, ProductSettle, Bond.Calendar) : Dt.Diff(AsOf, ProductSettle);

        if (fwdSettle_ != Dt.Empty && fwdSettle_ > Settle)
        {
          Settle = fwdSettle_;
        }
      }
    }

    /// <summary>
    /// The Trade Settle date
    /// </summary>
    public Dt TradeSettle
    {
      get { return tradeSettle_; }
      set { tradeSettle_ = value; }
    }


    ///<summary>
    /// Flag to indicate if the trade is on forward-issue bond
    ///</summary>
    public bool ProductIsForwardIssue
    {
      get { return ProductSettle < Bond.Effective; }
    }

    ///<summary>
    /// Flag to indicate if the trade is forward-settle trade while not a forward-issue bond
    ///</summary>
    public bool TradeIsForwardSettle
    {
      get { return !ProductIsForwardIssue && fwdSettle_ > ProductSettle; }
    }

    #endregion Properties

    #region Z-Spread adjustment helper

    /// <summary>
    ///  Returns market or model price depending on EnableZSpreadAdjustment flag.
    /// </summary>
    private double MarketOrModelPrice()
    {
      return EnableZSpreadAdjustment ? FullPrice() : FullModelPrice();
    }

    /// <summary>
    /// A helper to save/restrore discount spread.
    /// </summary>
    public class ZSpreadAdjustment : IDisposable
    {
      private readonly double originalSpread_;
      private readonly DiscountCurve discountCurve_;

      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="pricer">Bond pricer</param>
      public ZSpreadAdjustment(BondPricer pricer)
      {
        if (!pricer.EnableZSpreadAdjustment) return;
        var dc = discountCurve_ = pricer.DiscountCurve;
        originalSpread_ = dc.Spread;

        try
        {
          var rSpread = pricer.CalcRSpread();
          dc.Spread = originalSpread_ + rSpread;
          // Clear survival cache since the discount curve changes.
          var sc = pricer.SurvivalCurve;
          if (sc != null)
            sc.CurveShifts = null;
        }
        catch (Exception e)
        {
          dc.Spread = originalSpread_;
          throw new ToolkitException("Market level, credit spreads, "
                                     + "and recovery assumption appear to be inconsistent.  "
                                     + "Check your inputs.  Cannot match market level."
                                     + e.ToString());
        }
      }

      /// <summary>
      /// Dispose
      /// </summary>
      public void Dispose()
      {
        // restore origSpread and notional
        if (discountCurve_ != null)
          discountCurve_.Spread = originalSpread_;
      }
    }

    // Backward compatible for now. Should switch to TRUE once fully tested.
    public static bool ZSpreadConsistentBgmModel { get; set; } = false;

    #endregion

    #region ResetAction

    /// <summary>Quote (price or yield) changed</summary>
    public static readonly ResetAction QuoteChanged = new ResetAction();

    #endregion ResetAction

    #region Data

    // Note: If you add any properties here make sure the Clone() is updated as necessary.
    //
    private object defaultPaymentDate_ = null;
    private IVolatilityObject volatilityObject_; // If not null, using BGM Tree model.
    private bool ignoreExDivDateInCashflow_ = false;
    private ConvertibleBondIntersectingTreesModel convertModel_; //define the convertible bond binomial trees model

    private PaymentSchedule _paymentSchedule;
    private PaymentSchedule _tradePaymentSchedule;
    private PaymentSchedule _spotSettlePaymentSchedule;

    private CashflowAdapter _cfAdapter;
    private CashflowAdapter _tradeCfAdapter;
    private CashflowAdapter _spotSettleCfAdapter;

    private DiscountCurve discountCurve_;
    private DiscountCurve referenceCurve_;
    private RateResetsList rateResetsList_; // the new rate resets object
    private SurvivalCurve survivalCurve_;
    private RecoveryCurve recoveryCurve_;
    private int stepSize_;
    private TimeUnit stepUnit_;
    private double meanReversion_;
    private double sigma_;
    private CallableBondPricingMethod _callableModel;
    private bool enableZSpreadAdjustment_ = true;

    private double[] cashflowPeriodFractions_ = null;
    private double[] cashflowPrincipals_ = null;
    private double[] cashflowAmounts_ = null;
    private object accruedFraction_ = null;
    private PriceYieldMethod priceYieldMethod_ = PriceYieldMethod.Standard;

    // Results cache
    private object marketQuote_ = null;
    private double redemptionPrice_ = -1;
    private bool withAccrualOnCall_ = false;
    private bool withAccrualOnConversion_ = true;
    private QuotingConvention quotingConvention_;
    private object currentCoupon_ = null;
    private object nextCouponDate_ = null;
    private object prevCouponDate_ = null;
    private object remainingCoupons_ = null;
    private object accrualDays_ = null;
    private object accruedInterest_ = null;
    private object yield_ = null;
    private object trueYield_ = null;
    private object fullPrice_ = null;
    private object rspread_ = null;
    private object hspread_ = null;
    private object stubRate_ = null; // used for discount margin calculation
    private object currentFloatingRate_ = null; // used for discount margin calculation
    private Bond _workBond;

    // Convertible market info
    private double s0_; //initial stock price     
    private double sigmaS_; //equity volatility
    private ConvertibleBondIntersectingTreesModel.StockCorrelatedModel.StockDividends dividends_;
    private double rho_; //correlation between stock and short rate
    private double kappa_; //short rate mean reversion speed
    private double sigmaR_; //short rate volatility
    private int n_; //number of binomail tree steps
    private readonly ShortRateModelType _convertibleModel;

    // automatic updated date
    private object assumedRate_ = null;
    private Dt[] cpnDates_ = null;
    private double[] cpnAmounts_ = null;
    private Dt protectionStart_ = Dt.Empty;
    private Dt[] resetDates_ = null;
    private double[] resetRates_ = null;
    private double notionalFactor_;
    // configuration
    private bool discountingAccrued_ = false;
    private bool allowNegativeCDSSpreads_ = false;
    private Dt tradeSettle_;
    private Dt productSettle_;
    private Dt fwdSettle_;
    private int? settlementDays_;

    private const double Coupon01CouponBump = 0.0001;
    private bool _usePaymentSchedule = true;

    #endregion Data

    #region IRateResetsUpdater Members

    RateResets IRatesLockable.LockedRates
    {
      get { return RateResets; }
      set
      {
        rateResetsList_ = RateResetsList.Convert(
          value, Product.Effective,
          Bond.BackwardCompatibleCashflow);
      }
    }

    IEnumerable<RateReset> IRatesLockable.ProjectedRates
    {
      get
      {
        if (!Bond.Floating) return null;
        return Bond.GetPaymentSchedule(null, AsOf, AsOf, DiscountCurve,
                                     ReferenceCurve, RateResets, Dt.Empty, 
                                     Dt.Empty, RecoveryRate, IgnoreExDivDateInCashflow)
          .EnumerateProjectedRates();
      }
    }

    #endregion

    #region Private Utilities

    public DefaultSettlement GetDefaultPayment(CashflowAdapter cfa)
    {
      if (cfa == null)
        return null;

      if (UsePaymentSchedule)
      {
        DefaultSettlement dp = null;
        InterestPayment ip = null;
        double damt = 0.0, dacc = 0.0;
        Dt ddt = Dt.Empty;
        var ps = cfa.Ps;
        foreach (var p in ps)
        {
          var dsp = p as DefaultSettlement;
          if (dsp != null)
          {
            dp = dsp;
            damt += dsp.RecoveryAmount;
            dacc += dsp.AccrualAmount;
            ddt = dsp.PayDt;
            continue;
          }
          ip = p as InterestPayment;
        }
        if (ip != null && dp == null
          && (ip.ExDivDate.IsEmpty() || Settle < ip.ExDivDate))
        {
          damt = RecoveryRate*ip.Notional;
        }
        return new DefaultSettlement(ddt, ddt, Currency.None, 1.0, damt, dacc, true);
      }

      return GetDfltPytFromCashflow(cfa);
    }

    private DiscountCurve CloneToBondConventions(DiscountCurve discCurve)
    {
      if (discCurve == null) throw new ArgumentNullException("discCurve");

      // ZSpread with ActAct is a bit perverse and ends up with some error cases.
      var newCurve = new DiscountCurve((DiscountCalibrator)discCurve.Calibrator.Clone(),
                                       discCurve.Interp,
                                       (Bond.AccrualDayCount != DayCount.ActualActualBond) ? Bond.AccrualDayCount : DayCount.Actual365Fixed,
                                       Bond.Freq)
                     {
                       Interp = discCurve.Interp
                     };
      for (int i = 0; i < discCurve.Count; ++i)
        newCurve.Add(discCurve.GetDt(i), discCurve.GetVal(i));
      return newCurve;
    }

    private Dt GenerateFromDate(Dt settle, Dt tradeSettle)
    {
      if (Bond.PaymentLagRule == null || tradeSettle.IsEmpty())
        return settle;

      var bond = WorkBond;
      for (int idx = 0; idx < bond.Schedule.Count; idx++)
      {
        if (settle == tradeSettle && settle == bond.Schedule.GetPeriodEnd(idx))
          return settle;
        if (bond.Schedule.GetPeriodEnd(idx) > tradeSettle)
        {
          var payDt = bond.PaymentLagRule.PaymentLagBusinessFlag
                         ? Dt.AddDays(bond.Schedule.GetPeriodEnd(idx), bond.PaymentLagRule.PaymentLagDays, Bond.Calendar)
                         : Dt.Roll(Dt.Add(bond.Schedule.GetPeriodEnd(idx), bond.PaymentLagRule.PaymentLagDays, TimeUnit.Days), bond.Schedule.BdConvention,
                         bond.Schedule.Calendar);
          if (payDt > tradeSettle && payDt > settle)
            return Dt.Add(bond.Schedule.GetPeriodEnd(idx), -1); //Historical accrual or principal entitled to the trade but not paid yet
        }
      }
      return tradeSettle;
    }

    #endregion

    #region IRepoSecurityPricer

    /// <summary>
    ///  Security value method for bond repos
    /// </summary>
    /// <returns></returns>
    public double SecurityMarketValue()
    {
      return ProductPvFromQuote()/EffectiveNotional;
    }

    #endregion
  }

  /// <summary>
  ///  Methods to price callable bond
  /// </summary>
  public enum CallableBondPricingMethod
  {
    /// <summary>
    /// Method not specified
    /// </summary>
    None,

    /// <summary>
    /// Ignore callable features
    /// </summary>
    IgnoreCall,

    /// <summary>
    /// Yield-to-Worst model in which the maturity is determined by yield
    /// </summary>
    YieldToWorst,

    /// <summary>
    ///  Evaluate callable bonds based on LIBOR market model
    /// </summary>
    LiborMarket,

    /// <summary>
    ///  Evaluate callable bond assuming the short rate follows Black-Karasinski process
    /// </summary>
    BlackKarasinski,
  }


}
