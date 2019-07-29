//
// CashflowPricer.cs
//  -2009. All rights reserved.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Cashflows.Utils;

namespace BaseEntity.Toolkit.Pricers
{
	/// <summary>
	/// <para>Abstract parent pricer for credit-contingent cashflow pricing using the
	/// <see cref="BaseEntity.Toolkit.Models.CashflowModel">General Cashflow Model</see>.</para>
  /// <para>Cashflow pricers for specific product types are derived from this parent class
  /// and implement the
  /// <see cref="PricerBase.GenerateCashflow(BaseEntity.Toolkit.Cashflows.Cashflow, Dt)"/> method.</para>
  /// </summary>
	/// <remarks>
  /// <para>Pricing is based on the <see cref="BaseEntity.Toolkit.Models.CashflowModel">Generalised
  /// Contingent Cashflow Model</see>.</para>
  /// <inheritdoc cref="BaseEntity.Toolkit.Models.CashflowModel" />
	/// </remarks>
  // Docs note: remarks are inherited so only include docs suitable for derived classes. RD Mar'14
  [Serializable]
  public abstract partial class CashflowPricer : PricerBase, ICashflowPricer, IPricer
  {
		private static readonly log4net.ILog logger=log4net.LogManager.GetLogger(typeof(CashflowPricer));

		#region Constructors

		/// <summary>
		///   Constructor
		/// </summary>
		///
		/// <param name="product">Product to price</param>
		///
		protected CashflowPricer(IProduct product)
      : this(product, Dt.Empty, Dt.Empty, null, null, null, null, 0.0, 0, TimeUnit.None, null)
		{
		}

		/// <summary>
		///   Constructor for a fixed or floating regular cashflow
		/// </summary>
		///
		/// <remarks>
		///   <para>No survival curve, recovery curve or counterparty curve are used</para>
		/// </remarks>
		///
		/// <param name="product">Product to price</param>
		/// <param name="asOf">Pricing as-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="discountCurve">Discount Curve for pricing</param>
		/// <param name="referenceCurve">Floating rate reference curve (if needed)</param>
		///
		protected CashflowPricer(
      IProduct product, Dt asOf, Dt settle, DiscountCurve discountCurve, DiscountCurve referenceCurve
      )
      : this(product, asOf, settle, discountCurve, referenceCurve, null,
      null, 0.0, 0, TimeUnit.None, null)
		{
		}

    /// <summary>
    ///   Constructor for a floating regular cashflow
    /// </summary>
    ///
    /// <remarks>
    ///   <para>No survival curve, recovery curve or counterparty curve are used</para>
    /// </remarks>
    ///
    /// <param name="product">Product to price</param>
    /// <param name="asOf">Pricing as-of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="discountCurve">Discount Curve for pricing</param>
    /// <param name="referenceCurve">Floating rate reference curve (if needed)</param>
    /// <param name="rateResets">Float rate historical resets</param>
    ///
    protected CashflowPricer(
      IProduct product, Dt asOf, Dt settle, DiscountCurve discountCurve, DiscountCurve referenceCurve,
      IList<RateReset> rateResets
      )
      : this(product, asOf, settle, discountCurve, referenceCurve, null,
      null, 0.0, 0, TimeUnit.None, null)
    {
      rateResets_ = new List<RateReset>(rateResets);
      return;
    }

		/// <summary>
		///   Constructor for a general cashflow without counterparty risk
		/// </summary>
		///
		/// <remarks>
		///   <para>The recovery curve is taken from the survival curve.</para>
		/// </remarks>
		///
		/// <param name="product">Product to price</param>
		/// <param name="asOf">Pricing as-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="discountCurve">Discount Curve for pricing</param>
		/// <param name="referenceCurve">Floating rate reference curve (if needed)</param>
		/// <param name="survivalCurve">Survival Curve for pricing</param>
		/// <param name="stepSize">Step size for pricing grid</param>
		/// <param name="stepUnit">Units for step size</param>
		///
		protected CashflowPricer(
      IProduct product, Dt asOf, Dt settle,
      DiscountCurve discountCurve, DiscountCurve referenceCurve, SurvivalCurve survivalCurve,
			int stepSize, TimeUnit stepUnit
      )
      : this(product, asOf, settle, discountCurve, referenceCurve, survivalCurve,
      null, 0.0, stepSize, stepUnit, null)
		{
		}

		/// <summary>
		///   Constructor for a general cashflow without counterparty risk
		/// </summary>
		///
		/// <remarks>
		///   <para>The recovery curve is specified.</para>
		/// </remarks>
		///
		/// <param name="product">Product to price</param>
		/// <param name="asOf">Pricing as-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="discountCurve">Discount Curve for pricing</param>
		/// <param name="referenceCurve">Floating rate reference curve (if needed)</param>
		/// <param name="survivalCurve">Survival Curve for pricing</param>
		/// <param name="stepSize">Step size for pricing grid</param>
		/// <param name="stepUnit">Units for step size</param>
		/// <param name="recoveryCurve">Recovery curve</param>
		///
		protected
		CashflowPricer(
      IProduct product, Dt asOf, Dt settle,
			DiscountCurve discountCurve, DiscountCurve referenceCurve, SurvivalCurve survivalCurve,
			int stepSize, TimeUnit stepUnit, RecoveryCurve recoveryCurve
      )
      : this(product, asOf, settle, discountCurve, referenceCurve, survivalCurve,
      null, 0.0, stepSize, stepUnit, recoveryCurve)
		{
		}

		/// <summary>
		///   Constructor with general cashflow with counterparty risks
		/// </summary>
		///
		/// <remarks>
		///   <para>The recovery curve is taken from the survival curve.</para>
		/// </remarks>
		///
		/// <param name="product">Product to price</param>
		/// <param name="asOf">Pricing as-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="discountCurve">Discount Curve for pricing</param>
		/// <param name="referenceCurve">Floating rate reference curve (if needed)</param>
		/// <param name="survivalCurve">Survival Curve of underlying credit</param>
		/// <param name="counterpartyCurve">Survival Curve of counterparty</param>
		/// <param name="correlation">Correlation between credit and conterparty defaults</param>
		/// <param name="stepSize">Step size for pricing grid</param>
		/// <param name="stepUnit">Units for step size</param>
		///
		protected CashflowPricer(
      IProduct product, Dt asOf, Dt settle,
      DiscountCurve discountCurve, DiscountCurve referenceCurve,
      SurvivalCurve survivalCurve, SurvivalCurve counterpartyCurve,
			double correlation, int stepSize, TimeUnit stepUnit
      )
      : this(product, asOf, settle, discountCurve, referenceCurve, survivalCurve,
      counterpartyCurve, correlation, stepSize, stepUnit, null)
		{
		}

		/// <summary>
		///   Constructor with general cashflow with counterparty risks
		/// </summary>
		///
		/// <remarks>
		///   <para>The recovery curve is specified.</para>
		/// </remarks>
		///
		/// <param name="product">Product to price</param>
		/// <param name="asOf">Pricing as-of date</param>
		/// <param name="settle">Settlement date</param>
		/// <param name="discountCurve">Discount Curve for pricing</param>
		/// <param name="referenceCurve">Floating rate reference curve (if needed)</param>
		/// <param name="survivalCurve">Survival Curve of underlying credit</param>
		/// <param name="counterpartyCurve">Survival Curve of counterparty</param>
		/// <param name="correlation">Correlation between credit and conterparty defaults</param>
		/// <param name="stepSize">Step size for pricing grid</param>
		/// <param name="stepUnit">Units for step size</param>
		/// <param name="recoveryCurve">Recovery curve</param>
		///
		protected CashflowPricer(
      IProduct product, Dt asOf, Dt settle,
      DiscountCurve discountCurve, DiscountCurve referenceCurve,
      SurvivalCurve survivalCurve, SurvivalCurve counterpartyCurve,
      double correlation, int stepSize, TimeUnit stepUnit, RecoveryCurve recoveryCurve
      )
			: base(product, asOf, settle)
		{
		  if (_usePaymentSchedule)
		  {
		    PsConstruct(discountCurve, referenceCurve, survivalCurve, counterpartyCurve,
		      correlation, stepSize, stepUnit, recoveryCurve);
		  }
		  else
		  {
		    CfConstruct(discountCurve, referenceCurve, survivalCurve, counterpartyCurve,
		      correlation, stepSize, stepUnit, recoveryCurve);
		  }
		}

    private void PsConstruct(DiscountCurve discountCurve, DiscountCurve referenceCurve,
      SurvivalCurve survivalCurve, SurvivalCurve counterpartyCurve,
      double correlation, int stepSize, TimeUnit stepUnit, RecoveryCurve recoveryCurve)
    {
      // Set data, using properties to include validation
      this._ps = null;
      this._feePayments = null;
      this._protectionPayments = null;
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

		/// <summary>
		///   Clone
		/// </summary>
		///
		public override object Clone()
		{
		  return _usePaymentSchedule ? PsClone() : CfClone();
		}
 
    private object PsClone()
    {
      CashflowPricer obj = (CashflowPricer)base.Clone();
      obj._ps = _ps?.CloneObjectGraph();
      obj._feePayments = _feePayments?.CloneObjectGraph();
      obj._protectionPayments = _protectionPayments?.CloneObjectGraph();
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

		#endregion Constructors

    /// <summary>
    /// Payment pricer
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

		#region Methods

    /// <summary>
    ///   Reset the pricer
    /// </summary>
    /// <remarks>
    ///   <para>There are some pricers which need to remember some public state
    ///   in order to skip redundant calculation steps. This method is provided
    ///   to indicate that this internate state should be cleared.</para>
    ///   <para>For pricers which do not need internate state, this does nothing by default.</para>
    /// </remarks>
    public override void Reset()
    {
      if(_usePaymentSchedule)
        PsReset();
      else
        CfReset();
    }

    private void PsReset()
    {
      _ps = null;
      _feePayments = null;
      _protectionPayments = null;
      base.Reset();
    }

    /// <summary>
    ///   Validate, appending errors to specified list
    /// </summary>
    /// <param name="errors">Array of resulting errors</param>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if (this.discountCurve_ == null)
        InvalidValue.AddError(errors, this, "DiscountCurve", String.Format("Invalid discount curve. Cannot be null"));
      if (this.StepSize < 0.0)
        InvalidValue.AddError(errors, this, "StepSize", String.Format("Invalid step size. Must be >= 0, not {0}", this.stepSize_));
      if (this.correlation_ < -1.0 || this.correlation_ > 1.0)
        InvalidValue.AddError(errors, this, "Correlation", String.Format("Invalid correlation value. Must be between -1 and 1, not {0}", this.correlation_));

      // Validate schedules
      RateResetUtil.Validate(rateResets_, errors);

      return;
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
      return _usePaymentSchedule ? PsProductPv() : CfProductPv();
    }

    private double PsProductPv()
    {
      var notional = Payments.GetPaymentsByType<DefaultSettlement>().Any()
        ? Notional : CurrentNotional;
      var pv = Payments.CalculatePv(AsOf, ProtectionStart, discountCurve_,
        survivalCurve_, counterpartyCurve_, correlation_, StepSize, StepUnit,
        AdapterUtil.CreateFlags(includeSettlePayments_, includeMaturityProtection_,
          DiscountingAccrued))*notional;
      return pv;
    }

    /// <summary>
    ///   Calculate the full price (percentage of current Notional) of the cash flow stream
    /// </summary>
    /// <remarks>
    ///   <para>Cashflows after the settlement date are present valued back to the pricing
    ///   as-of date.</para>
    /// </remarks>
    /// <returns>Present value to the settlement date of the cashflow stream as a percentage of current Notional</returns>
    public double FullModelPrice()
		{
		  return this.ProductPv()/this.Notional;
		}

		/// <summary>
		///   Calculate the clean price (Full Price - Accrued) of the cash flow stream
		/// </summary>
		/// <remarks>
		///   <para>Cashflows after the settlement date are present valued back to the pricing
		///   as-of date.</para>
		/// </remarks>
		/// <returns>Present value to the settlement date of the cashflow stream as a percentage of current Notional</returns>
    public double FlatPrice()
		{
		  return (this.ProductPv() - this.Accrued())/this.Notional;
		}

    /// <summary>
		///   Calculate the present value of the fee part of the cashflow stream
		/// </summary>
		/// <remarks>
		///   <para>Fee cashflows after the settlement date are present valued
		///   back to the pricing as-of date.</para>
		/// </remarks>
		/// <returns>Present value of the fee part of the cashflow stream</returns>
    public double FeePv()
		{
      return this.FullFeePv();
		}

    /// <summary>
		///   Calculate the present value of the fee part (including accrued) of the cashflow stream
		/// </summary>
		/// <remarks>
		///   <para>Fee cashflows after the settlement date are present valued
		///   back to the pricing as-of date.</para>
		/// </remarks>
		/// <returns>Present value of the fee part of the cashflow stream</returns>
    public virtual double FullFeePv()
    {
      return _usePaymentSchedule ? PsFullFeePv() : CfFullFeePv();
    }

    private double PsFullFeePv()
    {
      double pv = FeePayments.CalculatePv(AsOf, ProtectionStart, discountCurve_,
        survivalCurve_, counterpartyCurve_, correlation_, StepSize, StepUnit,
        AdapterUtil.CreateFlags(includeSettlePayments_, includeMaturityProtection_,
          DiscountingAccrued)) * CurrentNotional;
      var dp = UpdateDefaultPayment(Payments, counterpartyCurve_);
      if (dp != null)
        pv += dp.Amount * discountCurve_.Interpolate(AsOf, dp.PayDt) * Notional;
      return pv;
    }

    /// <summary>
		///   Calculate the present value of the fee part (excluding accrued) of the cashflow stream
		/// </summary>
		/// <remarks>
		///   <para>Fee cashflows after the settlement date are present valued
		///   back to the pricing as-of date.</para>
		/// </remarks>
		/// <returns>Present value of the fee part of the cashflow stream</returns>
    public double FlatFeePv()
		{
      return (this.FullFeePv() - this.Accrued());
		}

    /// <summary>
		///   Calculate the present value of the protection part of a cashflow stream
		/// </summary>
		/// <remarks>
		///   <para>Protection cashflows after the settlement date are present valued
		///   back to the pricing as-of date.</para>
		/// </remarks>
		/// <returns>Present value of the protection part of the cashflow stream</returns>
    public virtual double ProtectionPv()
    {
      return _usePaymentSchedule ? PsProtectionPv() : CfProtectionPv();
    }

    private double PsProtectionPv()
    {
      double pv = ProtectionPayments.CalculatePv(AsOf, ProtectionStart,
        discountCurve_, survivalCurve_, counterpartyCurve_, correlation_,
        StepSize, StepUnit, AdapterUtil.CreateFlags(includeSettlePayments_,
          includeMaturityProtection_, DiscountingAccrued)) * CurrentNotional;
      return pv;
    }

    /// <summary>
    ///   Total accrued for product to as-of date given pricing arguments
    /// </summary>
    /// <returns>Total accrued interest</returns>
    public override double Accrued()
    {
      // delegate to the ICashflowPricer Cashflow property.
      CashflowAdapter cf = ((ICashflowPricer)this).Cashflow;
      DefaultSettlement defaultPayment = UpdateDefaultPayment(cf.Ps, null);
      // This is the case where the name has already defaulted,
      // but it carries some unsettled accrual.
      if (defaultPayment != null)
        return defaultPayment.Accrual * Notional;

      // The "normal" case, we find first cashflow on or after settlement
      // (depending on includeSettle flag)
      int N = cf.Count;
      Dt settle = this.Settle;
      int firstIdx;
      for (firstIdx = 0; firstIdx < N; firstIdx++)
      {
        if (Dt.Cmp(cf.GetDt(firstIdx), settle) > 0)
          break;
      }
      if (firstIdx >= N || (N > 0 && cf.GetEndDt(N-1) <= settle))
        return 0.0;  // This may happen when the settle is after maturity, for example.

      Dt accrualStart = (firstIdx > 0) ? cf.GetDt(firstIdx - 1) : cf.Effective;
      if (Dt.Cmp(accrualStart, settle) > 0)
        return 0.0; // settle is before the acrrual start

      DayCount dc = cf.GetDayCount(firstIdx);
      Dt nextDate = cf.GetDt(firstIdx);
      int paymentPeriod = Dt.Diff(accrualStart, nextDate, dc);
      if (paymentPeriod == 0)
        return 0.0; // this may happen if maturity on settle, for example

      double accrued = ((double)Dt.Diff(accrualStart, settle, dc))
        / paymentPeriod * cf.GetAccrued(firstIdx);
      return accrued;
    }

    // Create a new flat discount curve from given curve such that the
    // new curve's discount factors are all same to maturity discount factor
    // of the given curve; Or simply a falt curve with 0-rate.
    public DiscountCurve GetFlatDiscountCurve(DiscountCurve discCurve, bool maturityDiscount)
    {
      DiscountCurve curve = null;
      if (!maturityDiscount)
      {
        curve = new DiscountCurve(AsOf, 0);
        return curve;
      }
      double dfMaturity = discCurve.DiscountFactor(Product.Maturity);
      ArrayList tenorDates = new ArrayList();
     
      // Get the tenor dates
      tenorDates.Add(AsOf);
      tenorDates.Add(Settle);
      if (Cashflow.Count > 0)
      {
        for (int i = 0; i < Cashflow.Count; ++i)
          tenorDates.Add(Cashflow.GetDt(i));
      }
      else
      {
        tenorDates.Add(Product.Maturity);
        tenorDates.Add(Dt.Add(Product.Maturity,1));
      }
      Dt[] tenors = (Dt[])tenorDates.ToArray(typeof(Dt));

      // Set discount factors to be same as maturity discount factor
      double[] dfsMaturity = new double[tenors.Length];
      for (int i = 0; i < tenors.Length; ++i)
        dfsMaturity[i] = dfMaturity;

      DayCount dayCount = discCurve.DayCount;
      Frequency freq = discCurve.Frequency;

      DiscountRateCalibrator calibrator = new DiscountRateCalibrator(AsOf, AsOf);

      curve = new DiscountCurve(calibrator);
      curve.Interp = discCurve.Interp;
      curve.Ccy = discCurve.Ccy;
      curve.Category = discCurve.Category;
      curve.DayCount = dayCount;
      curve.Frequency = freq;
      
      curve.Set(tenors, dfsMaturity);

      return curve;
    }

    /// <summary>
    ///   Expected Loss at maturity
    /// </summary>
    /// <returns>Expected loss</returns>
    public double ExpectedLoss()
    {
      // This should be multiplied by the original notional
      // since any previous default is handled by ExpectedLossRate.
      return ExpectedLossRate(this.ProtectionStart, Product.Maturity) * this.Notional;
    }

    /// <summary>
    ///   Expected Loss over a date range
    /// </summary>
    /// <param name="start">Start of date range</param>
    /// <param name="end">End of date range after settle</param>
    /// <returns>Expected loss over date range</returns>
    /// <exclude />
    public double ExpectedLossRate(Dt start, Dt end)
    {
      double defaultProb = CounterpartyRisk.CreditDefaultProbability(
        start, end, SurvivalCurve, CounterpartyCurve, Correlation, StepSize, StepUnit);
      return defaultProb * (1 - RecoveryRate);
    }

    /// <summary>
    ///   Calculate the suvival probability of the transaction (including the effects of 
    ///   counterparty defaults or the effects of refinance.
    /// </summary>
    /// <param name="start">Start date</param>
    /// <param name="end">End date</param>
    /// <returns>Suvival probability</returns>
    /// <exclude />
    public double SurvivalProbability(Dt start, Dt end)
    {
      return CounterpartyRisk.OverallSurvivalProbability(start, end,
        SurvivalCurve, CounterpartyCurve, Correlation, StepSize, StepUnit);
    }

    /// <summary>
		///   Calculate IRR of cashflows given full price
		/// </summary>
		/// <remarks>
		///   <para>Calculates IRR (yield) of cash flows under no default
		///   given full price <paramref name="price"/>.</para>
		/// </remarks>
		/// <param name="price">Full price of cashflows (percentage of notional)</param>
		/// <param name="daycount">Daycount of irr</param>
		/// <param name="freq">Compounding frequency of irr</param>
		/// <returns>Irr (yield) implied by price</returns>
    public double Irr(double price, DayCount daycount, Frequency freq)
    {
      return _usePaymentSchedule
        ? PsIrr(price, daycount, freq)
        : CfIrr(price, daycount, freq);
    }

    private double PsIrr(double price, DayCount daycount, Frequency freq)
    {
      Dt settle = ProtectionStart;
      return PaymentScheduleUtils.Irr(CfAdapter, settle, settle, null,
        survivalCurve_, counterpartyCurve_, correlation_,
        AdapterUtil.CreateFlags(includeSettlePayments_,
          includeMaturityProtection_, DiscountingAccrued), StepSize, StepUnit,
        daycount, freq, price);
    }

    /// <summary>
    ///   Calculate discount rate spread implied by full price.
    /// </summary>
    /// <remarks>
    ///   <para>Calculates the constant spread (continuously compounded) over
    ///   discount curve for cashflow to match a specified full price
    ///   <paramref name="price"/>.</para>
    ///   <para>This is also commonly called the Z-Spread.</para>
    /// </remarks>
    /// <param name="price">Target full price (percentage of notional)</param>
    /// <returns>spread over discount curve implied by price</returns>
    public double ImpliedDiscountSpread(double price)
    {
      return _usePaymentSchedule
        ? PsImpliedDiscountSpread(price)
        : CfImpliedDiscountSpread(price);
    }

    private double PsImpliedDiscountSpread(double price)
    {
      Dt settle = ProtectionStart;
      return PaymentScheduleUtils.ImpDiscountSpread(CfAdapter,
        settle, settle, discountCurve_, survivalCurve_,
        counterpartyCurve_, correlation_, StepSize,
        StepUnit, price, AdapterUtil.CreateFlags(
          includeSettlePayments_, includeMaturityProtection_,
          DiscountingAccrued));
    }

    /// <summary>
		/// Calculate hazard rate spread over survival spread implied by full price.
		/// </summary>
		/// <remarks>
    ///   Calculates constant <formula inline="true">\lambda</formula> (continuous) over survival curve for
		///   cashflow to match a specified full price.
		/// </remarks>
		/// <param name="price">Target full price (percentage of notional)</param>
		/// <returns>spread over survival curve implied by price</returns>
    public double ImpliedHazardRateSpread(double price)
    {
      return _usePaymentSchedule
        ? PsImpliedHazardRateSpread(price)
        : CfImpliedHazardRateSpread(price);
    }

    private double PsImpliedHazardRateSpread(double price)
    {
      Dt settle = ProtectionStart;
      return PaymentScheduleUtils.ImpSurvivalSpread(CfAdapter, settle, settle,
        discountCurve_, survivalCurve_, counterpartyCurve_, correlation_,
        includeSettlePayments_, includeMaturityProtection_, DiscountingAccrued,
        StepSize, StepUnit, price);
    }
 
    private PaymentSchedule GetPaymentSchedule()
    {
      var cds = Product as CDS;
      var pricer = this as CDSCashflowPricer;
      // for swaplegcashflowpricer, notecashflowpricer and recoveryswappricer
      if (cds == null || pricer == null)
        return GetPaymentSchedule(null, AsOf);

      //for the CDSCasflowPricer, if create an override function 
      //GeneratePaymentSchedule(asof, null), many ccr tests 
      //will fail. keep in this way at this moment.
      var retVal = pricer.GeneratePayments(AsOf, cds, pricer.Fee());
      var recoveryRate = pricer.GetRecoveryRate();
      var recoveryPayments = retVal.GetRecoveryPayments(pricer.Settle,
        false, dt => recoveryRate, cds.Funded, cds.CashflowFlag);
      retVal.AddPayments(recoveryPayments);
      return retVal;
    }


    private DefaultSettlement UpdateDefaultPayment(PaymentSchedule ps,
      SurvivalCurve counterparty)
    {
      var dps = ps.GetPaymentsByType<DefaultSettlement>().ToArray();
      if (dps.Length == 0)
        return null;
      double dacc = 0.0, damt = 0.0;
      Dt dt = Dt.Empty;
      foreach (var dp in dps)
      {
        dt = dp.PayDt;
        damt += dp.RecoveryAmount;
        dacc += dp.AccrualAmount;
      }

      if (counterparty != null && !counterparty.DefaultDate.IsEmpty()
        && counterparty.DefaultDate <= dt)
      {
        return null;
      }

      return new DefaultSettlement(dt, dt, Currency.None, 1.0,
        damt, dacc, true);
    }

    #endregion Methods

    #region Properties

    /// <summary>
    /// Current outstanding notional on the settlement date
    /// </summary>
    /// <remarks>
    /// <para>This is the current notional at the settlement
    /// date, excluding all the names defaulted before the settle date.</para>
    /// </remarks>
    /// <value>Current notional.</value>
    public override double CurrentNotional
    {
      get
      {
        if( IsDefaulted )
          return 0;
        else if (this.Product.Effective > this.Settle)
          // Note we should look at some other way of doing this. Currrently here for consistency for forward starting products. HJ/RD Jun'08
          return SurvivalProbability(this.Settle, this.Product.Effective) * Notional;
        else
          return this.Notional;
      }
    }

    /// <summary>
    /// Effective outstanding notional on the settlement date
    /// </summary>
    /// <remarks>
    /// <para>This is the effective notional at the settlement
    /// date. It includes adjustments based on amortizations
    /// and any defaults prior to the settlement date.  Depending
    /// on pricing methods, it may include the name defaulted before
    /// the pricer settle date but the default loss/recovery has
    /// to be included in the prices (for example, when the default
    /// is yet settled on the pricing date).</para>
    /// </remarks>
    /// <value>Effective Notional.</value>
    public override double EffectiveNotional
    {
      get
      {
        if (IsSettled)
          return 0;
        else if (this.Product.Effective > this.Settle)
          // Note we should look at some other way of doing this. Currrently here for consistency for forward starting products. HJ/RD Jun'08
          return SurvivalProbability(this.Settle, this.Product.Effective) * Notional;
        else
          return this.Notional;
      }
    }

    /// <summary>
    /// Discount Curve used for pricing
    /// </summary>
		public DiscountCurve DiscountCurve
		{
			get { return discountCurve_; }
			set { discountCurve_ = value; Reset(); }
		}

    /// <summary>
    /// Reference Curve used for pricing of floating-rate cashflows
    /// </summary>
		public DiscountCurve ReferenceCurve
		{
			get { return referenceCurve_; }
      set { referenceCurve_ = value; Reset(); }
		}

    /// <summary>
    /// Survival curve used for pricing
		/// </summary>
		public SurvivalCurve SurvivalCurve
		{
			get { return survivalCurve_; }
			set {	survivalCurve_ = value; Reset(); }
		}
    
    /// <summary>
    /// Counterparty curve used for pricing
		/// </summary>
		public SurvivalCurve CounterpartyCurve
		{
			get { return counterpartyCurve_; }
			set { counterpartyCurve_ = value; Reset();	}
		}

		/// <summary>
    /// Recovery curve
    /// </summary>
		/// <remarks>
		/// <para>If a separate recovery curve has not been specified, the recovery from the survival
		/// curve is used. In this case the survival curve must have a Calibrator which provides a
		/// recovery curve otherwise an exception will be thrown.</para>
		/// </remarks>
    public RecoveryCurve RecoveryCurve
		{
			get
			{
				if( recoveryCurve_ != null )
					return recoveryCurve_;
				else if( survivalCurve_ != null && survivalCurve_.SurvivalCalibrator != null )
					return survivalCurve_.SurvivalCalibrator.RecoveryCurve;
				else
					return null;
			}
			set {	recoveryCurve_ = value;	Reset(); }
		}

    /// <summary>
    /// Return the recovery rate matching the maturity of this product.
    /// </summary>
    /// <remarks>
    /// <para>Convenience function that looks up the RecoveryCurve.</para>
    /// </remarks>
    public double RecoveryRate
    {
      get { return (RecoveryCurve != null) ? RecoveryCurve.RecoveryRate(Product.Maturity) : 0.0; }
    }

    /// <summary>
		/// Historical coupon fixings (for floating rate securities)
		/// </summary>
		public IList<RateReset> RateResets
		{
			get
			{
        if( rateResets_ == null )
          rateResets_ = new List<RateReset>();
		    return rateResets_;
			}
		}

    /// <summary>
    /// Current floating rate
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
    /// Step size for pricing grid
		/// </summary>
    public int StepSize
		{
			get { return stepSize_; }
			set { stepSize_ = value; Reset(); }
		}

		/// <summary>
    /// Step units for pricing grid
		/// </summary>
    public TimeUnit StepUnit
		{
			get { return stepUnit_; }
			set { stepUnit_ = value; Reset(); }
		}
    
		/// <summary>
    /// Default correlation between credit and counterparty
    /// </summary>
    public double Correlation
		{
			get { return correlation_; }
			set { correlation_ = value; Reset(); }
		}

		/// <summary>
    /// Include settlement date payment in price
    /// </summary>
    public bool IncludeSettlePayments
		{
			get { return includeSettlePayments_; }
			set { includeSettlePayments_ = value; Reset(); }
		}

    /// <summary>
    /// Protection start date
    /// </summary>
    /// <remarks>
    /// <para>Default is the same as the settlement date</para>
    /// </remarks>
    public Dt ProtectionStart
    {
      get
      {
        if (protectionStart_.IsEmpty())
          return (Product == null || (Product.Effective <= Settle)) ? Settle : Product.Effective;
        else
          return protectionStart_;
      }
    }

    /// <summary>
    /// Include maturity date in protection
    /// </summary>
    /// <exclude />
    public bool IncludeMaturityProtection
    {
      get { return includeMaturityProtection_; }
      set { includeMaturityProtection_ = value; Reset(); }
    }

    /// <summary>
    /// Include maturity date in accrual
    /// </summary>
    /// <exclude />
    public bool IncludeMaturityAccrual
    {
      get { return includeMaturityAccrual_; }
      set { includeMaturityAccrual_ = value; Reset(); }
    }

    /// <summary>
    /// Include maturity date in accrual
    /// </summary>
    /// <exclude />
    public bool DiscountingAccrued
    {
      get { return discountingAccrued_; }
      set { discountingAccrued_ = value; Reset(); }
    }

    /// <summary>
    /// Pays full accrual after default and get reimbursed on recovery
    /// </summary>
    /// <exclude />
    public bool SupportAccrualRebateAfterDefault
    {
      get { return _supportAccrualRebateAfterDefault; }
      set { _supportAccrualRebateAfterDefault = value; Reset(); }
    }

    /// <summary>
    /// Has the product being priced defaulted before the
    /// settle date.
    /// </summary>
    public bool IsDefaulted
    {
      get
      {
        // Test based on protection start (for any forward settlement
        return ((this.SurvivalCurve != null && this.SurvivalCurve.Defaulted == Defaulted.HasDefaulted) ||
           (this.CounterpartyCurve != null && this.CounterpartyCurve.Defaulted == Defaulted.HasDefaulted));
      }
    }

    /// <summary>
    /// Has the product being priced defaulted and the default
    /// payment settled before or on the settle date.
    /// </summary>
    public bool IsSettled
    {
      get
      {
        // Test based on protection start (for any forward settlement
        SurvivalCurve sc = this.SurvivalCurve;
        return ((sc != null && sc.Defaulted == Defaulted.HasDefaulted
          && (sc.SurvivalCalibrator == null
           || sc.SurvivalCalibrator.RecoveryCurve == null
           || sc.SurvivalCalibrator.RecoveryCurve.DefaultSettlementDate.IsEmpty()
           || sc.SurvivalCalibrator.RecoveryCurve.Recovered == Recovered.HasRecovered))
          || (counterpartyCurve_ != null &&
           counterpartyCurve_.Defaulted == Defaulted.HasDefaulted));
      }
    }

    public PaymentSchedule Payments
    {
      get
      {
        if (_ps == null)
          _ps = GetPaymentSchedule();
        return _ps;
      }
    }

    public PaymentSchedule FeePayments
    {
      get
      {
        if (_feePayments == null)
        {
          _feePayments = new PaymentSchedule();
          foreach (var p in Payments)
          {
            if ((p as RecoveryPayment) != null
              || (p as DefaultSettlement) != null)
              continue;
            _feePayments.AddPayment(p);
          }
        }
        return _feePayments;
      }
    }

    public PaymentSchedule ProtectionPayments
    {
      get
      {
        if (_protectionPayments == null)
        {
          _protectionPayments = new PaymentSchedule();
          foreach (var p in Payments)
          {
            var rp = p as RecoveryPayment;
            if (rp != null)
              _protectionPayments.AddPayment(rp);
          }
        }
        return _protectionPayments;
      }
    }

    public CashflowAdapter CfAdapter => ((ICashflowPricer)this).Cashflow;

    CashflowAdapter ICashflowPricer.Cashflow
    {
      get
      {
        if(!_usePaymentSchedule)
          return new CashflowAdapter(Cashflow);

        return new CashflowAdapter(Payments);
      }
    }

    #endregion Properties

    #region Data

    // Note: If you add any properties here make sure the Clone() is updated as necessary.
    //

    private DiscountCurve discountCurve_;
		private DiscountCurve referenceCurve_;
		private List<RateReset> rateResets_;
    private SurvivalCurve survivalCurve_;
		private RecoveryCurve recoveryCurve_;
    private int stepSize_;
    private TimeUnit stepUnit_;

    private SurvivalCurve counterpartyCurve_;
		private double correlation_;

		private bool includeSettlePayments_;

    // automatic updated date
    private Dt protectionStart_ = Dt.Empty;
    private bool includeMaturityProtection_ = false;
    private bool includeMaturityAccrual_ = false;
    private bool discountingAccrued_ = false;
	  private bool _supportAccrualRebateAfterDefault = false;
    private PaymentSchedule _ps = null;
    private PaymentSchedule _feePayments = null;
    private PaymentSchedule _protectionPayments = null;
    private bool _usePaymentSchedule = true;


    #endregion Data

  } // CashflowPricer

}