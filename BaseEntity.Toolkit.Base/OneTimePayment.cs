using System;
using System.Collections.Generic;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using System.Data;
using System.Runtime.Serialization;

namespace MagnoliaIG.ToolKits.Cashflows
{
    [Serializable]
    public abstract class OneTimePayment : Payment
    {
        private double amount_;
        protected double notional_ = 1.0;

        protected OneTimePayment(Dt payDate, double amount, Currency ccy)
            : base(payDate, amount, ccy)
        {
            amount_ = amount;
        }

        protected OneTimePayment(Dt payDt, Currency ccy)
            : base(payDt, ccy)
        { }
        protected OneTimePayment(Dt payDate, double amt)
            : this(payDate, amt, Currency.None)
        { }

        protected override double ComputeAmount()
        {
            return amount_ ;
        }

        public void SetNotional(double notional)
        {
            notional_ = notional;
        }

        public double Notional { get { return notional_; } protected set { notional_ = value; } }

        public void SetAmount(double amt)
        { amount_ = amt; }

        public override void AddDataColumns(System.Data.DataColumnCollection collection)
        {
            base.AddDataColumns(collection);
            if (!collection.Contains("Amount"))
                collection.Add(new DataColumn("Amount", typeof(double)));
            if (!collection.Contains("Notional"))
                collection.Add(new DataColumn("Notional", typeof(double)));
        }

        public override void AddDataValues(DataRow row, string dtFormat)
        {
            base.AddDataValues(row, dtFormat);
            row["Amount"] = Amount;
            row["Notional"] = Notional;
        }
    }

    [Serializable]
    public class UpfrontFee : OneTimePayment
    {
        public UpfrontFee(Dt payDate, double amt, Currency ccy)
            : base(payDate, amt, ccy)
        { }

        public UpfrontFee(Dt payDate, double amt)
            : this(payDate, amt, Currency.None)
        { }
    }

    [Serializable]
    public class PrincipalExchange : OneTimePayment
    {
        public PrincipalExchange(Dt payDt, double principal, Currency ccy)
            : base(payDt, ccy)
        {
            notional_ = principal;
        }
        public virtual double EffectiveExchange
        {
            get { return 1.0; }
            set { return; }
        }

        protected override double ComputeAmount()
        {
            return Notional;
        }

        public override void Scale(double factor)
        {
            base.Scale(factor);
            notional_ *= factor;
        }
    }

    [Serializable]
    public class DefaultSettlement : OneTimePayment
    {
        public DefaultSettlement(Dt defaultDt, Dt defaultSettleDt, Currency ccy, double notional, double recoveryRate)
            : base(defaultSettleDt.IsValid() ? defaultSettleDt : defaultDt, ccy)
        {
            notional_ = notional;
            DefaultDate = defaultDt;
            RecoveryRate = recoveryRate;
        }

        public override void Scale(double factor)
        {
            base.Scale(factor);
            notional_ *= factor;
        }
        protected override double ComputeAmount()
        {
            return Notional * (IsFunded ? RecoveryRate : RecoveryRate - 1.0);
        }

        public bool IsFunded { get; set; }

        public Dt DefaultDate { get; set; }

        private bool PayAccruedOnDefault { get; set; }

        public double RecoveryRate { get; set; }
    }


    #region CashflowNode

    ///// <summary>
    ///// Cashflow node
    ///// </summary>
    //[Serializable]
    //public abstract class CashflowNode : ICashflowNode
    //{
    //    /// <summary>
    //    /// Constructor
    //    /// </summary>
    //    /// <param name="payment">Payment</param>
    //    /// <param name="notional">Notional amount</param>
    //    /// <param name="discountCurve">DiscountCurve for discounting coupon</param>
    //    /// <param name="survivalFunction">SurvivalCurve </param>
    //    internal CashflowNode(Payment payment, double notional, DiscountCurve discountCurve, Func<Dt, double> survivalFunction)
    //    {
    //        Notional = notional;
    //        PayDt = payment.PayDt;
    //        ResetDt = payment.PayDt;
    //        Ccy = payment.Ccy;
    //        DiscountCurve = discountCurve;
    //        SurvivalFunction = survivalFunction;
    //        FxCurve = payment.FxCurve;
    //    }

    //    /// <summary>
    //    /// Payment currency 
    //    /// </summary>
    //    internal Currency Ccy { get; private set; }
    //    /// <summary>
    //    /// Positive for receiver and negative for payer
    //    /// </summary>
    //    internal double Notional { get; private set; }
    //    /// <summary>
    //    /// Fixed payment amount
    //    /// </summary>
    //    internal double FixedAmount { get; set; }
    //    /// <summary>
    //    /// Payment amount
    //    /// </summary>
    //    protected virtual double Amount { get { return FixedAmount; } }
    //    /// <summary>
    //    /// FxRate
    //    /// </summary>
    //    protected FxCurve FxCurve { get; private set; }
    //    /// <summary>
    //    /// DiscountCurve
    //    /// </summary>
    //    protected DiscountCurve DiscountCurve { get; private set; }
    //    /// <summary>
    //    /// SurvivalCurve
    //    /// </summary>
    //    protected Func<Dt, double> SurvivalFunction { get; private set; }

    //    #region ICashflowNode Members
    //    /// <summary>
    //    /// Reset date for coupon fixing
    //    /// </summary>
    //    public Dt ResetDt
    //    {
    //        get;
    //        protected set;
    //    }
    //    /// <summary>
    //    /// Coupon payment date
    //    /// </summary>
    //    public Dt PayDt
    //    {
    //        get;
    //        private set;
    //    }

    //    /// <summary>
    //    /// FX rate
    //    /// </summary>
    //    public double FxRate()
    //    {
    //        return (FxCurve == null) ? 1.0 : FxCurve.FxRate(PayDt, Ccy, DiscountCurve.Ccy);
    //    }

    //    /// <summary>
    //    /// Risky discount to asOf date
    //    /// </summary>
    //    public virtual double RiskyDiscount()
    //    {
    //        return (SurvivalFunction != null) ? DiscountCurve.Interpolate(PayDt) * SurvivalFunction(PayDt) : DiscountCurve.Interpolate(PayDt);
    //    }

    //    /// <summary>
    //    /// Realized amount in ToCcy 
    //    /// </summary>
    //    /// <returns>Realized amount</returns>
    //    public double RealizedAmount()
    //    {
    //        return Notional * Amount;
    //    }

    //    /// <summary>
    //    /// Reset accumulated quantities
    //    /// </summary>
    //    public virtual void Reset()
    //    { }

    //    #endregion

    //    #region Serialization events

    //    [OnSerializing]
    //    void WrapDelegates(StreamingContext context)
    //    {
    //        SurvivalFunction = SurvivalFunction.WrapSerializableDelegate();
    //    }

    //    [OnSerialized, OnDeserialized]
    //    void UnwrapDelegates(StreamingContext context)
    //    {
    //        SurvivalFunction = SurvivalFunction.UnwrapSerializableDelegate();
    //    }

    //    #endregion
    //}
    #endregion


}
