
/*
 * Payment.cs
 * Copyright(c)   2002-2018. All rights reserved.
*/


using System;
using System.Data;
using System.Diagnostics;
using System.Runtime.Serialization;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.Serialization;
using BaseEntity.Toolkit.Cashflows.Expressions;
using BaseEntity.Toolkit.Cashflows.Expressions.Payments;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows
{
  ///Payment class
  [Serializable]
  [DebuggerDisplay("{GetDebugDisplay()}")]
  public abstract class Payment : BaseEntityObject, IDebugDisplay
  {
    #region Constructors

    /// <summary>
    /// Payment Base Class
    /// </summary>
    protected Payment()
    {}

    /// <summary>
    /// Create Payment
    /// </summary>
    /// <param name="payDate">Date of Payment</param>
    /// <param name="ccy">The currency of payment.</param>
    protected Payment(Dt payDate, Currency ccy)
    {
      PayDt = payDate;
      Ccy = ccy;
    }

    /// <summary>
    /// Create Payment
    /// </summary>
    /// <param name="payDate">Date of Payment</param>
    /// <param name="amount">Amount of Payment</param>
    /// <param name="ccy">Currency of Payment</param>
    /// <remarks>
    /// Use this constructor only for fixed payments. Once set this way, 
    /// amount is immutable.
    /// </remarks>
    protected Payment(Dt payDate, double amount, Currency ccy)
    {
      PayDt = payDate;
      Amount = amount;
      Ccy = ccy;
    }

    #endregion

    #region Properties

    /// <exclude></exclude>
    protected double? AmountOverride { get; set; }

    /// <exclude></exclude>
    private double? FxOverride { get; set; }

    /// <summary>
    /// True if the payment is projected
    /// </summary>
    public virtual bool IsProjected
    {
      get { return false; }
    }

    /// <summary>
    /// Amount of payment
    /// </summary>
    public double Amount
    {
      get { return AmountOverride.HasValue ? AmountOverride.Value : ComputeAmount(); }
      set { AmountOverride = value; }
    }

    /// <summary>
    /// Date of payment
    /// </summary>
    public Dt PayDt { get; set; }

    ///<summary>
    /// Currency of payment
    ///</summary>
    public Currency Ccy { get; set; }

    /// <summary>
    /// In some cases, we determine whether we are getting the cash flow not based on the PayDate, but rather based on this extra field.
    /// For example, for bonds with a payment lag, if the bond is amortizing and we have the principal exchange, besides the payment
    /// date we would like to record the corresponding accrual period end date and use that date to determine if the cash flow
    /// belongs to us or not.
    /// If this property is not set, it is ignored.
    /// </summary>
    public Dt CutoffDate { get; set; }

    /// <summary>
    /// Spot FX rate to express payment amount in domestic currency
    /// </summary>
    public double FXRate
    {
      get { return FxOverride.HasValue ? FxOverride.Value : ComputeFX(); }
      set { FxOverride = value; }
    }

    /// <summary>
    /// FXCurve 
    /// </summary>
    public FxCurve FXCurve { get; set; }

    /// <summary>
    /// Total amount of payment 
    /// </summary>
    public double DomesticAmount
    {
      get { return Amount*FXRate; }
    }

    /// <summary>
    /// Start date for convexity adjustment/optionality calculations  
    /// </summary>
    public virtual Dt VolatilityStartDt { get; set; }

    /// <summary>
    /// Resets the amount overridden.
    /// </summary>
    public void ResetAmountOverride()
    {
      AmountOverride = null;
    }

    internal Dt CreditRiskEndDate { private get; set; }

    /// <summary>
    /// Gets the debugger display string of this object
    /// </summary>
    /// <value>The debug display.</value>
    private string GetDebugDisplay()
    {
      return string.Format("{0}({1})", GetType().Name, PayDt.ToInt());
    }

    string IDebugDisplay.DebugDisplay { get { return GetDebugDisplay();  } }

    #endregion

    #region Methods

    public Dt GetCreditRiskEndDate()
    {
      return CreditRiskEndDate.IsEmpty() ? PayDt : CreditRiskEndDate;
    }

    /// <summary>
    /// Risky discount payment amount to date <m>E^T(\beta_T (1 - L_T))</m> where <m>L_T</m> is the cumulative loss process.
    /// </summary>
    /// <param name="discountFunction">Discount curve</param>
    /// <param name="survivalFunction">Expected surviving notional as function of time</param>
    /// <returns>Risky discount</returns>
    public virtual double RiskyDiscount(
      Func<Dt, double> discountFunction, Func<Dt, double> survivalFunction)
    {
      double df = discountFunction(PayDt);
      if (survivalFunction != null)
      {
        df *= survivalFunction(GetCreditRiskEndDate());
      }
      return df;
    }

    /// <summary>
    /// Accrued payment up to date
    /// </summary>
    /// <param name="date">Date</param>
    /// <returns>Accrued amount</returns>
    /// <param name="accrual">Coupon - Accrued</param>
    public virtual double Accrued(Dt date, out double accrual)
    {
      accrual = DomesticAmount;
      return 0.0;
    }

    /// <summary>
    /// Convert a payment to a cashflow node used to simulate the realized payment amount 
    /// </summary>
    /// <param name="notional">Payment notional</param>
    /// <param name="discountCurve">Discount curve</param>
    /// <param name="survivalFunction">Delegate to compute surviving notional at PayDt</param>
    /// <returns>ICashflowNode</returns>
    /// <remarks>
    /// Rather than the expected payment amount, the cashflow node computes the realized payment amount.
    /// </remarks>
    
    
    public virtual ICashflowNode ToCashflowNode(double notional, DiscountCurve discountCurve, Func<Dt,double> survivalFunction)
    {
      throw new NotImplementedException();
    }

    /// <summary>
    /// Scale the payment appropriately
    /// </summary>
    /// <param name="factor">Scaling factor</param>
    public virtual void Scale(double factor)
    {
      if (AmountOverride.HasValue)
        AmountOverride *= factor;
    }


    /// <summary>
    /// When Amount is derived from other fields implement this
    /// </summary>
    /// <returns></returns>
    protected abstract double ComputeAmount();

    /// <summary>
    /// Get an evaluable expression for amount calculation
    /// </summary>
    /// <returns>Evaluable</returns>
    public virtual Evaluable GetEvaluableAmount()
    {
      return AmountOverride.HasValue
        ? Evaluable.Constant(AmountOverride.Value)
        // Simply call the method ComputeAmount, no optimization
        : Evaluable.Call(EvaluateAmount);
    }

    private double EvaluateAmount()
    {
      VolatilityStartDt = PricingDate.Value;
      return ComputeAmount();
    }

    /// <summary>
    /// When FXCurve is present, use this to determine rate
    /// </summary>
    /// <returns></returns>
    protected double ComputeFX()
    {
      if (FXCurve == null)
        return 1.0;
      Currency toCcy = (Ccy == FXCurve.SpotFxRate.FromCcy) ? FXCurve.SpotFxRate.ToCcy : FXCurve.SpotFxRate.FromCcy;
      return FXCurve.FxRate(PayDt, Ccy, toCcy);
    }

    /// <summary>
    /// Add data columns
    /// </summary>
    /// <param name="collection">Data column collection</param>
    public virtual void AddDataColumns(DataColumnCollection collection)
    {
      if (!collection.Contains("FX") && FXCurve != null)
        collection.Add(new DataColumn("FX", typeof (double)));
      if (!collection.Contains("Payment Date"))
        collection.Add(new DataColumn("Payment Date", typeof (string)));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="row"></param>
    /// <param name="dtFormat"></param>
    public virtual void AddDataValues(DataRow row, string dtFormat)
    {
      if (FXCurve != null)
        row["FX"] = FXRate;
      row["Payment Date"] = PayDt.ToStr(dtFormat);
    }

    #endregion
    
    #region CashflowNode

    /// <summary>
    /// Cashflow node
    /// </summary>
    [Serializable]
    protected abstract class CashflowNode : ICashflowNode
    {
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="payment">Payment</param>
      /// <param name="notional">Notional amount</param>
      /// <param name="discountCurve">DiscountCurve for discounting coupon</param>
      /// <param name="survivalFunction">SurvivalCurve </param>
      internal CashflowNode(Payment payment, double notional, DiscountCurve discountCurve, Func<Dt, double> survivalFunction)
      {
        Notional = notional;
        PayDt = payment.PayDt;
        ResetDt = payment.PayDt;
        Ccy = payment.Ccy;
        DiscountCurve = discountCurve;
        SurvivalFunction = survivalFunction;
        FxCurve = payment.FXCurve;
      }

      /// <summary>
      /// Payment currency 
      /// </summary>
      internal Currency Ccy { get; private set; }
      /// <summary>
      /// Positive for receiver and negative for payer
      /// </summary>
      internal double Notional { get; private set; }
      /// <summary>
      /// Fixed payment amount
      /// </summary>
      internal double FixedAmount { get; set; }
      /// <summary>
      /// Payment amount
      /// </summary>
      protected virtual double Amount { get { return FixedAmount; } }
      /// <summary>
      /// FxRate
      /// </summary>
      protected FxCurve FxCurve { get; private set; }
      /// <summary>
      /// DiscountCurve
      /// </summary>
      protected DiscountCurve DiscountCurve { get; private set; }
      /// <summary>
      /// SurvivalCurve
      /// </summary>
      protected Func<Dt,double> SurvivalFunction { get; private set; }

      #region ICashflowNode Members
      /// <summary>
      /// Reset date for coupon fixing
      /// </summary>
      public Dt ResetDt
      {
        get;
        protected set;
      }
      /// <summary>
      /// Coupon payment date
      /// </summary>
      public Dt PayDt
      {
        get;
        private set;
      }

      /// <summary>
      /// FX rate
      /// </summary>
      public double FxRate()
      {
        return (FxCurve == null) ? 1.0 : FxCurve.FxRate(PayDt, Ccy, DiscountCurve.Ccy);
      }

      /// <summary>
      /// Risky discount to asOf date
      /// </summary>
      public virtual double RiskyDiscount()
      {
        return (SurvivalFunction != null) ? DiscountCurve.Interpolate(PayDt) * SurvivalFunction(PayDt) : DiscountCurve.Interpolate(PayDt);
      }

      /// <summary>
      /// Realized amount in ToCcy 
      /// </summary>
      /// <returns>Realized amount</returns>
      public double RealizedAmount()
      {
        return Notional * Amount;
      }

      /// <summary>
      /// Reset accumulated quantities
      /// </summary>
      public virtual void Reset()
      {}

      #endregion

      #region Serialization events

      [OnSerializing]
      void WrapDelegates(StreamingContext context)
      {
        SurvivalFunction = SurvivalFunction.WrapSerializableDelegate();
      }

      [OnSerialized, OnDeserialized]
      void UnwrapDelegates(StreamingContext context)
      {
        SurvivalFunction = SurvivalFunction.UnwrapSerializableDelegate();
      }

      #endregion
    }
    #endregion
  }
}