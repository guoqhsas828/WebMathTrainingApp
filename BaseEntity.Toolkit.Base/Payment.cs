using System;
using System.Collections.Generic;
using BaseEntity.Shared;
using System.Data;
using BaseEntity.Toolkit.Base;

namespace MagnoliaIG.ToolKits.Cashflows
{
    [Serializable]
    public abstract class Payment : BaseEntityObject
    {
        protected Payment()
        { }

        protected Payment(Dt payDate, Currency ccy)
        {
            PayDt = payDate;
            Ccy = ccy;
        }

        protected Payment(Dt payDate, double amount, Currency ccy)
        {
            PayDt = payDate;
            Amount = amount;
            Ccy = ccy;
        }
        public Dt PayDt { get; set; }
        public Currency Ccy { get; set; }
        public double Amount
        {
            get
            { return (amountOverride_.HasValue) ? amountOverride_.Value : ComputeAmount(); }
            set { amountOverride_ = value; }
        }

        public double FXRate
        {
            get { return (fxOverride_.HasValue) ? fxOverride_.Value : ComputeFX(); }
            set {fxOverride_ = value;}
        }
        public double DomesticAmount 
        { 
            get 
            { 
                return Amount * FXRate; 
            } 
        }

        public virtual double Accrued(Dt date, out double accrual)
        {
            accrual = DomesticAmount;
            return 0.0;
        }

        protected abstract double ComputeAmount();

        protected double ComputeFX()
        {
           //if (FxCurve == null)
               return 1.0;

           //var toCcy = Ccy == FxCurve.SpotFxRate.FromCcy ? FxCurve.SpotFxRate.ToCcy : FxCurve.SpotFxRate.FromCcy;
           //return FxCurve.FxRate(PayDt, Ccy, toCcy);
        }

        public virtual bool IsProjected
        {
            get { return false; }
        }

        internal Dt GetCreditRiskEndDate()
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

        protected double? AmountOverride
        {
            get { return amountOverride_; }
            set { amountOverride_ = value; }
        }

        public virtual Dt VolatilityStartDt { get; set; }

        public Dt CutoffDate { get; set; }

        internal Dt CreditRiskEndDate { private get; set; }
        //public FxCurve FxCurve {get;set;}
        private double? amountOverride_ = null;
        private double? fxOverride_ = null;

        public void ResetAmountOverride()
        {
            amountOverride_ = null;
        }

        public virtual void Scale(double factor)
        {
            if (amountOverride_.HasValue)
                amountOverride_ = amountOverride_.Value * factor;
        }
        public virtual void AddDataColumns(DataColumnCollection collection)
        {
            //if (!collection.Contains("FX") && FxCurve != null)
            //    collection.Add(new DataColumn("FX", typeof(double)));
            if (!collection.Contains("Payment Date"))
                collection.Add(new DataColumn("Payment Date", typeof(string)));
        }

        public virtual void AddDataValues(DataRow row, string dtFormat)
        {
            //if (FxCurve != null)
            //    row["FX"] = FXRate;
            row["Payment Date"] = PayDt.ToStr(dtFormat);
        }

        //public virtual ICashflowNode ToCashflowNode(double notional, DiscountCurve discount, Func<Dt, double> survivalFunction)
        //{
        //    throw new NotImplementedException();
        //}

    }
}
