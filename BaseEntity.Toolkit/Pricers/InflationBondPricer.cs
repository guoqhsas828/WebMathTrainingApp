using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Calibrators;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Models;
using BaseEntity.Toolkit.Products;
using BaseEntity.Toolkit.Util;
using CompoundingPeriod =
  System.Tuple<BaseEntity.Toolkit.Base.Dt, BaseEntity.Toolkit.Base.Dt, BaseEntity.Toolkit.Cashflows.FixingSchedule>;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Inflation bond pricers
  /// </summary>
  /// <remarks>
  /// <inheritdoc cref="BaseEntity.Toolkit.Products.InflationBond" />
  /// </remarks>
  /// <seealso cref="BaseEntity.Toolkit.Products.InflationBond" />
  [Serializable]
  public partial class InflationBondPricer : PricerBase, IPricer, IAssetPricer, ICashflowNodesGenerator, IRepoAssetPricer
  {
    #region InflationLinkedInterestPayment

    /// <summary>
    ///  Floating notional inflation payment
    /// </summary> 
    [Serializable]
    private class InflationLinkedInterestPayment : FloatingInterestPayment
    {
      #region Constructors

      /// <summary>
      /// Floating rate payment
      /// </summary>
      /// <param name="prevPayDt">Previous Payment Date or Accrual Start</param>
      /// <param name="payDt">Payment Date</param>
      /// <param name="ccy">Currency of payment</param>
      /// <param name="cycleStart">Start of payment cycle</param>
      /// <param name="cycleEnd">End of payment cycle</param>
      /// <param name="periodStart">Start of accrual period</param>
      /// <param name="periodEnd">End of accrual period</param>
      /// <param name="notional">notional for this payment</param>
      /// <param name="coupon">Fixed coupon on floating notional</param>
      /// <param name="dc">Day Count convention for calcing Accrual</param>
      /// <param name="baseValue">Contractual base fixing value</param>
      /// <param name="rateProjector">Engine for projection of forward fixings</param>
      /// <param name="forwardAdjustment">Engine for calculation of convexity adjustments</param>
      public InflationLinkedInterestPayment(Dt prevPayDt, Dt payDt, Currency ccy, Dt cycleStart, Dt cycleEnd,
                                            Dt periodStart, Dt periodEnd, double notional, double coupon, DayCount dc, double baseValue,
                                            IRateProjector rateProjector, IForwardAdjustment forwardAdjustment)
        : base(prevPayDt, payDt, ccy, cycleStart, cycleEnd, periodStart, periodEnd, Dt.Empty, notional, coupon, dc, Frequency.None,
               CompoundingConvention.None, rateProjector, forwardAdjustment)
      {
        if (baseValue <= 0.0)
          throw new ToolkitException("BaseInflation must be strictly positive");
        BaseInflation = baseValue;
      }

      #endregion

      #region Properties

      /// <summary>
      /// Coupon type
      /// </summary>
      protected override string CouponLabel
      {
        get { return "Coupon"; }
      }

      /// <summary>
      /// Fixing type (rate or price)
      /// </summary>
      protected override string IndexFixingLabel
      {
        get { return "Index Forward Price"; }
      }

      /// <summary>
      /// Forward adjustment type (rate or price)
      /// </summary>
      protected override string ConvexityAdjLabel
      {
        get { return "Forward Price Adj"; }
      }


      /// <summary>
      /// Base value
      /// </summary>
      private double BaseInflation { get; set; }

      /// <summary>
      /// Convexity adjustment 
      /// </summary>
      public override double ConvexityAdjustment
      {
        get
        {
          if (EffectiveRateOverride.HasValue || ForwardAdjustment == null)
            return 0.0;
          RateResetState state;
          double f, ca, ov;
          Process(PayDt, CompoundingPeriods, RateProjector, ForwardAdjustment, BaseInflation, null, null, out f, out ca,
                  out ov, out state);
          return ca;
        }
      }

      /// <summary>
      /// Reference rate fixing for this payment. 
      /// If the index fixing is overridden then it is inclusive of spread 
      /// </summary>
      public override double IndexFixing
      {
        get
        {
          RateResetState state;
          double f, ca, ov;
          Process(PayDt, CompoundingPeriods, RateProjector, null, BaseInflation, null, null, out f, out ca, out ov, out state);
          return f;
        }
      }

      /// <summary>
      /// ResetState
      /// </summary>
      public override RateResetState RateResetState
      {
        get
        {
          if (EffectiveRateOverride.HasValue)
            return RateResetState.ResetFound;
          RateResetState state;
          double f, ca, ov;
          Process(PayDt, CompoundingPeriods, RateProjector, null, BaseInflation, null, null, out f, out ca, out ov, out state);
          return state;
        }
      }

      /// <summary>
      /// Effective Interest Rate for the period
      /// </summary>
      public override double EffectiveRate
      {
        get
        {
          if (EffectiveRateOverride.HasValue)
            return EffectiveRateOverride.Value;
          RateResetState state;
          double f, ca, ov;
          Process(PayDt, CompoundingPeriods, RateProjector, ForwardAdjustment, BaseInflation, Cap, Floor, out f, out ca,
                  out ov, out state);
          return Notional * (f + ca + ov) / BaseInflation * FixedCoupon;
        }
        set { EffectiveRateOverride = value; }
      }

      #endregion

      #region Methods

      /// <summary>
      /// Convert a payment to a cashflow node used to simulate the 
      /// realized payment amount 
      /// </summary>
      /// <param name="notional">Notional</param>
      /// <param name="discountCurve">Discount curve to discount payment</param>
      /// <param name="survivalFunction">Survival function if coupon is credit contingent</param>
      /// <returns>ICashflowNode</returns>
      /// <remarks>
      /// Rather than the expected payment amount,  
      /// the cashflow node computes the realized payment amount.
      /// </remarks>
      public override ICashflowNode ToCashflowNode(double notional, DiscountCurve discountCurve, Func<Dt, double> survivalFunction)
      {
        if (EffectiveRateOverride.HasValue)
          return base.ToCashflowNode(notional, discountCurve, survivalFunction);
        return new InflationLinkedCouponNode(this, notional, discountCurve, survivalFunction);
      }

      /// <summary>
      /// Add Data Values
      /// </summary>
      /// <param name="row"></param>
      /// <param name="dtFormat"></param>
      public override void AddDataValues(DataRow row, string dtFormat)
      {
        base.AddDataValues(row, dtFormat);
        RateResetState state;
        double f, ca, ov;
        Process(PayDt, CompoundingPeriods, RateProjector, ForwardAdjustment, BaseInflation, Cap, Floor, out f, out ca, out ov,
                out state);
        row["Notional"] = (f + ca + ov) / BaseInflation;
      }

      #endregion

      #region Utilities

      private static void Process(Dt payDt, IList<CompoundingPeriod> compoundingPeriods, IRateProjector rateProjector,
                                  IForwardAdjustment forwardAdjustment, double baseVal, double? cap, double? floor,
                                  out double f, out double ca, out double ov, out RateResetState state)
      {
        ca = ov = 0.0;
        Fixing fixing = rateProjector.Fixing(compoundingPeriods[0].Item3);
        f = fixing.Forward;
        state = fixing.RateResetState;
        if (state != RateResetState.IsProjected || forwardAdjustment == null)
          return;
        FixingSchedule sched = compoundingPeriods[0].Item3;
        ca = forwardAdjustment.ConvexityAdjustment(payDt, sched, fixing);
        if (floor.HasValue)
          ov += forwardAdjustment.FloorValue(sched, fixing, floor.Value * baseVal, 0.0, ca);
        if (cap.HasValue)
          ov += forwardAdjustment.CapValue(sched, fixing, cap.Value * baseVal, 0.0, ca);
      }

      #endregion

      #region InflationLinkedCouponNode

      /// <summary>
      /// Floating notional node
      /// </summary>
      [Serializable]
      private class InflationLinkedCouponNode : CouponCashflowNode
      {
        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="payment">Payment</param>
        /// <param name="notional">Notional</param>
        /// <param name="discountCurve">Discount curve</param>
        /// <param name="survivalFunction">Surviving principal</param>
        internal InflationLinkedCouponNode(InflationLinkedInterestPayment payment, double notional, DiscountCurve discountCurve,
                                           Func<Dt, double> survivalFunction)
          : base(payment, notional, discountCurve, survivalFunction)
        {
          BaseInflation = payment.BaseInflation;
          ResetDt = payment.ResetDate;
          FixedCoupon = payment.FixedCoupon;
          AccrualFactor = payment.AccrualFactor * payment.Notional;
          CompoundingPeriods = payment.CompoundingPeriods;
          RateProjector = payment.RateProjector;
          Cap = payment.Cap;
          Floor = payment.Floor;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Amount
        /// </summary>
        protected override double Amount
        {
          get
          {
            double f, cv, ov;
            RateResetState state;
            Process(PayDt, CompoundingPeriods, RateProjector, null, BaseInflation, null, null, out f, out cv, out ov,
                    out state);
            f *= FixedCoupon / BaseInflation;
            if (Cap.HasValue)
              f = Math.Min(f, Cap.Value);
            if (Floor.HasValue)
              f = Math.Max(f, Floor.Value);
            return f * AccrualFactor;
          }
        }

        /// <summary>
        /// Fixed coupon
        /// </summary>
        private double FixedCoupon { get; set; }

        /// <summary>
        /// Base value
        /// </summary>
        private double BaseInflation { get; set; }

        /// <summary>
        /// Compounding periods
        /// </summary>
        private List<CompoundingPeriod> CompoundingPeriods { get; set; }

        /// <summary>
        /// Rate projector
        /// </summary>
        private IRateProjector RateProjector { get; set; }

        /// <summary>
        /// Accrual factor
        /// </summary>
        private double AccrualFactor { get; set; }

        /// <summary>
        /// Cap
        /// </summary>
        private double? Cap { get; set; }

        /// <summary>
        /// Floor
        /// </summary>
        private double? Floor { get; set; }

        #endregion
      }

      #endregion
    }

    #endregion

    #region InflationLinkedPrincipalExchange

    /// <summary>
    /// Floating principal cashflow node
    /// </summary>
    [Serializable]
    private class InflationLinkedPrincipalExchange : FloatingPrincipalExchange
    {
      #region Constructor

      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="payDate">Date of payment</param>
      /// <param name="ccy">Currency of payment</param>
      /// <param name="notional">Notional amount of payment</param>
      ///<param name="rateProjector">Engine for fixing calculations</param>
      /// <param name="forwardAdjustment">Engine for convexity adjustment calculations</param>
      /// <param name="baseInflation">Engine for convexity adjustment calculations</param>
      /// <remarks>The payment amount is then computed as (notional x fixing)</remarks>

      public InflationLinkedPrincipalExchange(Dt payDate, double notional, Currency ccy, IRateProjector rateProjector, IForwardAdjustment forwardAdjustment,
                                              double baseInflation)
        : base(payDate, notional, ccy, rateProjector, forwardAdjustment)
      {
        BaseInflation = baseInflation;
      }

      #endregion

      #region Properties

      /// <summary>
      /// Base inflation
      /// </summary>
      private double BaseInflation { get; set; }

      #endregion

      #region Methods
      public override ICashflowNode ToCashflowNode(double notional, DiscountCurve discountCurve, Func<Dt, double> survivalFunction)
      {
        if (AmountOverride.HasValue)
          return base.ToCashflowNode(notional, discountCurve, survivalFunction);
        return new InflationLinkedPrincipalNode(this, notional, discountCurve, survivalFunction);
      }


      /// <summary>
      /// Calculate the effective exchange if projected
      /// </summary>
      /// <returns></returns>
      protected override double CalcEffectiveExchange()
      {

        Fixing fixing = RateProjector.Fixing(FixingSchedule);
        var f = fixing.Forward;
        var state = fixing.RateResetState;
        if (state == RateResetState.IsProjected && ForwardAdjustment != null)
        {
          if (Floor.HasValue)
            f += ForwardAdjustment.FloorValue(FixingSchedule, fixing, Floor.Value * BaseInflation, 0.0, 0.0);
          if (Cap.HasValue)
            f += ForwardAdjustment.CapValue(FixingSchedule, fixing, Cap.Value * BaseInflation, 0.0, 0.0);
        }
        return f / BaseInflation;
      }

      #endregion

      #region FloatingPrincipalCashflowNode

      /// <summary>
      /// Floating principal cashflow node
      /// </summary>
      [Serializable]
      private class InflationLinkedPrincipalNode : FloatingPrincipalCashflowNode
      {
        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="notional">Payment</param>
        /// <param name="payment">Payment</param>
        /// <param name="discountCurve">DiscountCurve</param>
        /// <param name="survivalFunction">Survival function</param>
        public InflationLinkedPrincipalNode(InflationLinkedPrincipalExchange payment, double notional, DiscountCurve discountCurve,
                                            Func<Dt, double> survivalFunction)
          : base(payment, notional, discountCurve, survivalFunction)
        {
          BaseInflation = payment.BaseInflation;
        }

        #endregion

        #region Properties

        private double BaseInflation { get; set; }

        protected override double EffectiveExchange
        {
          get
          {
            Fixing fixing = RateProjector.Fixing(FixingSchedule);
            var f = fixing.Forward;
            f /= BaseInflation;
            if (Cap.HasValue)
              f = Math.Min(f, Cap.Value);
            if (Floor.HasValue)
              f = Math.Max(f, Floor.Value);
            return f;
          }
        }

        #endregion
      }

      #endregion
    }

    #endregion

    #region InflationLinkedPrincipalAccretion

    /// <summary>
    /// Notional exchanged linked to inflation at current and previous payment date
    /// </summary>
    [Serializable]
    private class InflationLinkedPrincipalAccretion : FloatingPrincipalExchange
    {
      #region Constructor

      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="prevAmt">Previous payment amount</param>
      /// <param name="prevPayDt">Previous pay date</param>
      /// <param name="payDt">Payment date</param>
      /// <param name="ccy">Currency of payment</param>
      /// <param name="notional">Notional amount</param>
      /// <param name="baseInflation">Base value</param>
      /// <param name="projector">Processors to project fixings</param>
      /// <param name="forwardAdjustment">Engine to compute option value</param>
      public InflationLinkedPrincipalAccretion(double? prevAmt, Dt prevPayDt, Dt payDt, Currency ccy, double notional, double baseInflation,
                                               IRateProjector projector,
                                               IForwardAdjustment forwardAdjustment)
        : base(payDt, notional, ccy, projector, forwardAdjustment)
      {
        PrevAmt = prevAmt;
        BaseInflation = baseInflation;
        if (!prevAmt.HasValue)
          PrevFixingSchedule = RateProjector.GetFixingSchedule(Dt.Empty, prevPayDt, prevPayDt, prevPayDt);
      }

      #endregion

      #region Properties

      /// <summary>
      /// Base inflation
      /// </summary>
      private double BaseInflation { get; set; }

      /// <summary>
      /// Previous fixing schedule
      /// </summary>
      private FixingSchedule PrevFixingSchedule { get; set; }

      /// <summary>
      /// Previous compounding period
      /// </summary>
      private double? PrevAmt { get; set; }

      /// <summary>
      /// Convexity adjustment 
      /// </summary>
      public override double ConvexityAdjustment
      {
        get
        {
          if (ForwardAdjustment == null)
            return 0.0;
          var rp = (InflationForwardCalculator)RateProjector;
          var fixing = RateProjector.Fixing(PrevFixingSchedule);
          if (fixing.RateResetState != RateResetState.IsProjected)
            return 0.0;
          return ((ForwardAdjustment)ForwardAdjustment).DelayAdjustment(PayDt, PrevFixingSchedule, fixing, (rp == null) ? Tenor.Empty : rp.ResetLag);
        }
      }

      /// <summary>
      /// Rate reset state
      /// </summary>
      public override RateResetState RateResetState
      {
        get
        {
          if (EffectiveExchangeOverride.HasValue)
            return RateResetState.ResetFound;
          var past = (PrevAmt.HasValue) ? RateResetState.ResetFound : RateProjector.Fixing(PrevFixingSchedule).RateResetState;
          RateResetState current = base.RateResetState;
          if (past == RateResetState.ObservationFound && current == RateResetState.ObservationFound)
            return RateResetState.ObservationFound;
          if (past == RateResetState.Missing || current == RateResetState.Missing)
            return RateResetState.Missing;
          if (past == RateResetState.IsProjected || current == RateResetState.IsProjected)
            return RateResetState.IsProjected;
          return RateResetState.ResetFound;
        }
      }

      #endregion

      #region Methods
      
      public override ICashflowNode ToCashflowNode(double notional, DiscountCurve discountCurve, Func<Dt, double> survivalFunction)
      {
        throw new NotImplementedException();
      }

      protected override double CalcEffectiveExchange()
      {
        var i1 = RateProjector.Fixing(FixingSchedule);
        Dt resetDt = FixingSchedule.ResetDate;
        var i0 = PrevAmt.HasValue
                   ? new Fixing
                     {
                       Forward = (1.0 + PrevAmt.Value) * BaseInflation,
                       RateResetState = RateResetState.ObservationFound
                     }
                   : RateProjector.Fixing(PrevFixingSchedule);
        if (ForwardAdjustment != null)
        {
          Dt prevResetDt = PrevAmt.HasValue ? Dt.Empty : PrevFixingSchedule.ResetDate;
          var rp = (InflationForwardCalculator)RateProjector;
          double ca = (i0.RateResetState != RateResetState.IsProjected)
                        ? 0.0
                        : -((ForwardAdjustment)ForwardAdjustment).DelayAdjustment(PayDt, PrevFixingSchedule, i0,
                                                                                  rp.ResetLag);
          if (Floor.HasValue)
            return FlooredExchange(prevResetDt, resetDt, PayDt, (i0.Forward + ca) / BaseInflation, i1.Forward / BaseInflation,
                                   i0.RateResetState, i1.RateResetState);
          return (i1.Forward - (i0.Forward + ca)) / BaseInflation;
        }
        return (i1.Forward - i0.Forward) / BaseInflation;
      }



      //Margrabe formula for exchange options (normalized by Z(t,T))
      private double FlooredExchange(Dt prevResetDt, Dt resetDt, Dt payDt, double i0, double i1, RateResetState s0,
                                     RateResetState s1)
      {
        if (ForwardAdjustment == null || s0 != RateResetState.IsProjected && s1 != RateResetState.IsProjected)
          return Math.Max(i1 - i0, 0.0);
        var engine = (ForwardAdjustment)ForwardAdjustment;
        if (s0 != RateResetState.IsProjected)
          return engine.RateModelParameters.Option(RateModelParameters.Process.Projection, engine.AsOf, OptionType.Call,
                                                   i1, 0.0, i0, resetDt, resetDt);
        double zT = engine.DiscountCurve.Interpolate(engine.AsOf, payDt);
        return i0 / zT *
               engine.RateModelParameters.OptionOnRatio(RateModelParameters.Process.Projection, engine.AsOf,
                                                        OptionType.Call, i1 / i0, i1, i0, 0.0, 1.0,
                                                        resetDt, resetDt, prevResetDt); //margrabe formula
      }

      #endregion



      /*
      #region InflationLinkedCashflowNode
    
      /// <summary>
      /// Convert a payment to a cashflow node used to simulate the 
      /// realized payment amount 
      /// </summary>
      /// <param name="discountCurve">Discount curve to discount payment</param>
      /// <param name="survivalFunction">Surviving principal if coupon is credit contingent</param>
      /// <returns>ICashflowNode</returns>
      /// <remarks>
      /// Rather than the expected payment amount,  
      /// the cashflow node computes the realized payment amount.
      /// </remarks>
      public override ICashflowNode ToCashflowNode(double notional DiscountCurve discountCurve, Func<Dt,double> survivalFunction)
      {
        if (EffectiveExchangeOverride.HasValue || ResetDate <= ((CouponCalculator)RateProjector).AsOf)
          return base.ToCashflowNode(discountCurve, survivalFunction);
        return new InflationLinkedCashflowNode(this, discountCurve, survivalFunction);
      }

      /// <summary>
      /// Inflation accretion payment
      /// </summary>
      [Serializable]
      private class InflationLinkedCashflowNode : OneTimeCashflowNode
      {
        #region Properties
        /// <summary>
        /// Coupon amount
        /// </summary>
        protected override double Amount
        {
          get
          {
            var fixing = RateProjector.Fixing(FixingSchedule);
            TotalAmountPaid = (fixing.Forward - BaseValue) / BaseValue;
            if (LastTotalAmountPaidOverride.HasValue)
            {
              double currentAmount = (TotalAmountPaid - LastTotalAmountPaidOverride.Value);
              if (Floor.HasValue)
                currentAmount = Math.Max(currentAmount, Floor.Value);
              return PrincipalRatio * currentAmount;
            }
            else
            {
              var prev = PreviousNode;
              double currentAmount = (prev != null) ? TotalAmountPaid - PreviousNode.TotalAmountPaid : TotalAmountPaid;
              if (Floor.HasValue)
                currentAmount = Math.Max(currentAmount, Floor.Value);
              return PrincipalRatio * currentAmount;
            }
          }
        }
        /// <summary>
        /// Floor
        /// </summary>
        private double? Floor { get; set; }
        /// <summary>
        /// Previous node
        /// </summary>
        private InflationLinkedCashflowNode PreviousNode { get { return LinkedCashflowNode as InflationLinkedCashflowNode; } }
        /// <summary>
        /// Rate projector
        /// </summary>
        private IRateProjector RateProjector { get; set; }
        /// <summary>
        /// Fixing schedule
        /// </summary>
        private FixingSchedule FixingSchedule { get; set; }
        /// <summary>
        /// Base value
        /// </summary>
        private double BaseValue { get; set; }
        /// <summary>
        /// Total amount paid
        /// </summary>
        private double TotalAmountPaid { get; set; }
        /// <summary>
        /// Last amount paid 
        /// </summary>
        private double? LastTotalAmountPaidOverride { get; set; }
        /// <summary>
        /// Principal ratio
        /// </summary>
        private double PrincipalRatio { get; set; }


        #endregion

        #region Methods
        /// <summary>
        /// Reset
        /// </summary>
        public override void Reset()
        {
          TotalAmountPaid = 0.0;
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="payment">Payment</param>
        /// <param name="discountCurve">Discount curve</param>
        /// <param name="survivalFunction">Surviving principal</param>
        internal InflationLinkedCashflowNode(InflationLinkedExchange payment, DiscountCurve discountCurve, Func<Dt,double> survivalFunction)
          : base(payment, discountCurve, survivalFunction)
        {
          IsPathDependent = true;
          ResetDt = payment.ResetDate;
          BaseValue = payment.BaseValue;
          Floor = payment.Floor;
          RateProjector = payment.RateProjector;
          FixingSchedule = payment.FixingSchedule;
          PrincipalRatio = payment.Notional;
          if (payment.PrevAmt.HasValue)
            LastTotalAmountPaidOverride = payment.PrevAmt.Value;
        }
        #endregion
      }
      #endregion*/
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="bond">Tips bond object</param>
    /// <param name="asOf">As of date</param>
    /// <param name="settle">Settlement date</param>
    /// <param name="notional">Notional of the deal</param>
    /// <param name="discountCurve">DiscountCurve object</param>
    /// <param name="inflationIndex">Inflation index</param>
    /// <param name="inflationCurve">Inflation curve</param>
    /// <param name="rateResets">Historical CPI resets</param>
    /// <param name="fwdModelParams">rateModelParams for the inflation process</param>
    public InflationBondPricer(InflationBond bond, Dt asOf, Dt settle, double notional, DiscountCurve discountCurve,
                               InflationIndex inflationIndex, InflationCurve inflationCurve, RateResets rateResets,
                               RateModelParameters fwdModelParams)
      : base(bond, asOf, settle)
    {
      if (bond.ReferenceIndex == null && inflationIndex == null)
        throw new ToolkitException("A non null reference index should be provided for inflation bond");
      ReferenceIndex = bond.ReferenceIndex ?? inflationIndex;
      DiscountCurve = discountCurve;
      ReferenceCurve = inflationCurve;
      RateModelParameters = fwdModelParams;
      RateResets = rateResets;
      Notional = notional;
      DiscountingAccrued = settings_.BondPricer.DiscountingAccrued;
      if (bond.ProjectionType == ProjectionType.InflationForward) //this is a fixed coupon/floating principal bond
        CreateInternalBondPricer();
    }

    #endregion Constructors

    #region Methods

    #region Initialization

    private ProjectionParams GetProjectionParams()
    {
      var retVal = new ProjectionParams
                   {
                     ProjectionType = InflationBond.ProjectionType,
                     BaseValue = InflationBond.BaseInflation,
                     IndexationMethod = InflationBond.IndexationMethod,
                     ResetLag = InflationBond.ResetLag,
                     SpreadType = InflationBond.SpreadType
                   };
      return retVal;
    }

    private ProjectionParams GetFloatingNotionalProjectionParams()
    {
      var retVal = new ProjectionParams
                   {
                     ProjectionType = ProjectionType.InflationForward,
                     BaseValue = InflationBond.BaseInflation,
                     IndexationMethod = InflationBond.IndexationMethod,
                     ResetLag = InflationBond.ResetLag,
                     SpreadType = SpreadType.None
                   };
      return retVal;
    }

    private IRateProjector GetRateProjector(ProjectionParams projectionParams)
    {
      return CouponCalculator.Get(AsOf, ReferenceIndex, ReferenceCurve, DiscountCurve, projectionParams);
    }

    private IForwardAdjustment GetForwardAdjustment(ProjectionParams projectionParams)
    {
      return ForwardAdjustment.Get(AsOf, DiscountCurve, RateModelParameters, projectionParams);
    }

    #endregion

    #region AssetPricer Members

    /// <summary>
    /// Payment schedule
    /// </summary>
    /// <param name="paymentSchedule">Payment schedule</param>
    /// <param name="from"></param>
    /// <returns></returns>
    PaymentSchedule IAssetPricer.GetPaymentSchedule(PaymentSchedule paymentSchedule, Dt from)
    {
      return GetPaymentSchedule(null, from, Dt.Empty);
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
      Dt cashflowFromDate = AsOf;
      if (InternalBondPricer != null)
      {
        Dt fromDate = InternalBondPricer.GetCashflowsFromDate();
        if (fromDate > cashflowFromDate)
          cashflowFromDate = fromDate;  // This is relevant for "TIPS" bonds with ex-div rule, like inflation-linked UK gilt
      }
      PaymentSchedule ps = GetPaymentSchedule(null, cashflowFromDate);
      return ps;
    }

    /// <summary>
    ///   Include Settle Payments in PV
    /// </summary>
    /// <exclude />
    public bool IncludeSettlePayments
    {
      get { return false; }
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
    ///   Discount Accrued 
    /// </summary>
    /// <exclude />
    public bool DiscountingAccrued { get; set; }

    /// <summary>
    ///Step size 
    ///</summary>
    public int StepSize { get; set; }

    /// <summary>
    /// Step unit
    /// </summary>
    public TimeUnit StepUnit { get; set; }

    /// <summary>
    /// Maturity
    /// </summary>
    public Dt Maturity
    {
      get { return Product.Maturity; }
      set { Product.Maturity = value; }
    }

    /// <summary>
    /// The Trade Settle date
    /// </summary>
    public Dt TradeSettle
    {
      get { return tradeSettle_; }
      set
      {
        tradeSettle_ = value;
        if (InternalBondPricer != null)
          InternalBondPricer.TradeSettle = tradeSettle_;
      }
    }


    /// <summary>
    /// Create floating leg in AssetSwapPricer
    /// </summary>
    /// <param name="discountCurve">funding curve</param>
    /// <param name="assetSwap">AssetSwap</param>
    /// <param name="projectionCurve">projection curve</param>
    /// <param name="projectionIndex">projection index</param>
    /// <param name="parameters">Model parameters</param>
    /// <returns>Floating leg pricer</returns>
    SwapLegPricer IAssetPricer.GetFloatingLegPricer(AssetSwap assetSwap, DiscountCurve discountCurve, CalibratedCurve projectionCurve,
                                                    InterestRateIndex projectionIndex, RateModelParameters parameters)
    {
      var swapLeg = new SwapLeg(assetSwap.Effective, assetSwap.Maturity, assetSwap.Frequency, assetSwap.Spread,
                                projectionIndex)
                    {
                      AccrueOnCycle = InflationBond.AccrueOnCycle,
                      Notional = InflationBond.Notional
                    };
      swapLeg.AmortizationSchedule.CopyFrom(InflationBond.AmortizationSchedule.ToArray());
      swapLeg.FinalExchange = true;
      if (InflationBond.Amortizes)
        swapLeg.IntermediateExchange = true;
      return new SwapLegPricer(swapLeg, AsOf, Settle, 1.0, discountCurve, projectionIndex, projectionCurve, null,
                               parameters, null);
    }

    #endregion

    #region IPricer Members

    /// <summary>
    /// Get the payment schedule: a detailed representation of payments
    /// <param name="paymentSchedule">Payment schedule</param>
    /// <param name="from">As of date</param>
    /// <returns>Payments associated to the bond</returns>
    /// </summary>
    public override PaymentSchedule GetPaymentSchedule(
      PaymentSchedule paymentSchedule, Dt from)
    {
      return GetPaymentSchedule(null, from, Dt.Empty);
    }

    /// <summary>
    ///   Total dollar accrued for product to as-of date given pricing arguments
    /// </summary>
    ///
    /// <returns>Total dollar accrued interest</returns>
    ///
    public override double Accrued()
    {
      return Accrued(Settle);
    }

    /// <summary>
    /// Deep copy 
    /// </summary>
    /// <returns>A deep copy of the pricer</returns>
    public override object Clone()
    {
      return new InflationBondPricer(InflationBond, AsOf, Settle, Notional, DiscountCurve,
                                     (InflationIndex)ReferenceIndex, ReferenceCurve, RateResets, RateModelParameters)
             {
               SurvivalCurve = SurvivalCurve,
               RecoveryOverwrite = RecoveryOverwrite
             };
    }

    #endregion

    private double Accrued(Dt settle)
    {
      if (InflationBond.ProjectionType == ProjectionType.InflationRate)
        return AccruedFloatingRate(settle);
      return InternalBondPricer.AccruedInterest(settle, TradeSettle) * IndexRatio(settle) * Notional;
    }

    private double AccruedFloatingRate(Dt settle)
    {
      if (settle <= Product.Effective)
        return 0.0;
      var inflBond = (InflationBond)Product;
      var paymentSchedule = GetPaymentSchedule(null, settle, settle);
      var fip = paymentSchedule.GetPaymentsByType<InterestPayment>().FirstOrDefault();
      if (fip == null)
        return 0.0;
      double coupon = fip.EffectiveRate;
      var accrualFactor = inflBond.Schedule.Fraction(fip.AccrualStart, Settle, fip.DayCount);
      return (accrualFactor > 0) ? (fip.Notional / inflBond.Notional) * Notional * coupon * accrualFactor : 0.0;
    }

    private void CreateInternalBondPricer()
    {
      var bond = (InflationBond)Product;
      if (bond.ProjectionType == ProjectionType.InflationRate)
        InternalBondPricer = null;
      Bond internalBond = CreateInternalBond(bond);
      InternalBondPricer = new BondPricer(internalBond, AsOf, Settle, DiscountCurve, null, 0, TimeUnit.None, 0);
    }

    /// <summary>
    /// Create a toolkit bond corresponding to the inflation bond
    /// </summary>
    public static Bond CreateInternalBond(InflationBond bond)
    {
      var internalBond = new Bond(bond.Effective, bond.Maturity, bond.Ccy, bond.BondType, bond.Coupon, bond.DayCount,
                                  bond.CycleRule, bond.Freq, bond.BDConvention, bond.Calendar)
      {
        BondExDivRule = bond.BondExDivRule
      };
      foreach (Amortization amort in bond.AmortizationSchedule)
        internalBond.AmortizationSchedule.Add(amort);
      foreach (CouponPeriod cp in bond.CouponSchedule)
        internalBond.CouponSchedule.Add(cp);
      return internalBond;
    }

    /// <summary>
    ///   Validate pricer inputs
    /// </summary>
    /// <param name="errors">Error list </param>
    /// <remarks>
    ///   This tests only relationships between fields of the pricer that
    ///   cannot be validated in the property methods.
    /// </remarks>
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);
      if (BaseInflation <= 0.0)
        InvalidValue.AddError(errors, this, "BaseInflation",
                              String.Format("Inflation level at bond issuance must be non negative"));
    }


    private PaymentSchedule CustomPaymentSchedule(PaymentSchedule paymentSchedule, Dt to, Dt from)
    {
      if (paymentSchedule == null)
        paymentSchedule = new PaymentSchedule();
      else
        paymentSchedule.Clear();
      foreach (var d in InflationBond.CustomPaymentSchedule.GetPaymentDates())
      {
        if (d >= from)
        {
          var paymentsOnDate = CloneUtil.CloneToGenericList(InflationBond.CustomPaymentSchedule.GetPaymentsOnDate(d).ToList());
          paymentSchedule.AddPayments(paymentsOnDate);
        }
        if (to.IsValid() && d > to)
          break;
      }
      return paymentSchedule;
    }

    private void AddAccretionPayments(PaymentSchedule paymentSchedule, IRateProjector rateProjector, IForwardAdjustment forwardAdjustment, Dt from, Dt to)
    {

      var bond = InflationBond;
      if (paymentSchedule == null || !bond.Accretes)
        return;
      var accretionSched = bond.AccretionSchedule;
      var payDt = bond.Effective;
      double? accretedNext = null;
      foreach (var accretion in accretionSched)
      {
        var prevPayDt = payDt;
        payDt = accretion.Date;
        var accretedPrev = accretedNext;
        accretedNext = accretion.Amount;
        if (payDt < from)
          continue;
        if (DefaultDate.IsValid() && DefaultDate <= payDt)
          break;
        double notional = bond.AmortizationSchedule.PrincipalAt(bond.Notional, accretion.Date);
        var ile = new InflationLinkedPrincipalAccretion(accretedPrev, prevPayDt, payDt, bond.Ccy,
                                                        notional, bond.BaseInflation, rateProjector,
                                                        forwardAdjustment);
        if (accretedNext.HasValue)
          ile.EffectiveExchange = accretedNext.Value;
        if (bond.FlooredNotional)
          ile.Floor = 0.0;
        paymentSchedule.AddPayment(ile);
        if (to.IsValid() && payDt >= to)
          break;
      }
    }


    private void AddNotionalRepayment(PaymentSchedule paymentSchedule, IRateProjector rateProjector, IForwardAdjustment forwardAdjustment, Dt to)
    {
      if (InflationBond.InterestOnly || paymentSchedule == null)
        return;
      Dt maturity = InflationBond.Schedule.GetPaymentDate(InflationBond.Schedule.Count - 1);
      if (DefaultDate.IsValid() && DefaultDate <= to)
      {
        double notionalAtDefault = InflationBond.AmortizationSchedule.PrincipalAt(InflationBond.Notional,
                                                                                  DefaultDate);
        var settlement = new DefaultSettlement(DefaultDate, DefaultSettleDate, Product.Ccy, notionalAtDefault,
                                               RecoveryRate) {IsFunded = true};
        paymentSchedule.AddPayment(settlement);
        return;
      }
      if (to.IsValid() && maturity < to)
        return;
      double notional = InflationBond.AmortizationSchedule.PrincipalAt(InflationBond.Notional, maturity);
      if (InflationBond.ProjectionType == ProjectionType.InflationRate || InflationBond.Accretes)
        paymentSchedule.AddPayment(new PrincipalExchange(maturity, notional, Product.Ccy));
      else
      {
        var finalExchange = new InflationLinkedPrincipalExchange(maturity, notional, InflationBond.Ccy,
                                                                 rateProjector, forwardAdjustment, InflationBond.BaseInflation);
        if (InflationBond.FlooredNotional)
          finalExchange.Floor = notional;
        paymentSchedule.AddPayment(finalExchange);
      }
    }


    private PaymentSchedule GetPaymentSchedule(PaymentSchedule paymentSchedule, Dt from, Dt to)
    {
      if (from > InflationBond.Maturity)
        return null;
      if (InflationBond.CustomPaymentSchedule != null && InflationBond.CustomPaymentSchedule.Count > 0)
        return CustomPaymentSchedule(paymentSchedule, from, to);
      ProjectionParams projectionParams = GetProjectionParams();
      IRateProjector rateProjector = GetRateProjector(projectionParams);
      IForwardAdjustment forwardAdjustment = GetForwardAdjustment(projectionParams);
      if (projectionParams.ProjectionType == ProjectionType.InflationRate)
      {
        paymentSchedule = PaymentScheduleUtils.FloatingRatePaymentSchedule(from, to, Product.Ccy, rateProjector,
                                                                           forwardAdjustment,
                                                                           RateResets, InflationBond.Schedule,
                                                                           InflationBond.CashflowFlag,
                                                                           InflationBond.Coupon,
                                                                           InflationBond.CouponSchedule,
                                                                           InflationBond.Notional,
                                                                           InflationBond.AmortizationSchedule,
                                                                           InflationBond.Amortizes,
                                                                           InflationBond.DayCount, null,
                                                                           projectionParams,
                                                                           InflationBond.Cap, InflationBond.Floor,
                                                                           IncludeSettlePayments, DefaultDate,
                                                                           DefaultSettleDate, null, null);
        ProjectionParams notProjectionParams = GetFloatingNotionalProjectionParams();
        IRateProjector notRateProjector = GetRateProjector(notProjectionParams);
        IForwardAdjustment notForwardAdjustment = GetForwardAdjustment(notProjectionParams);
        if (InflationBond.Accretes)
          AddAccretionPayments(paymentSchedule, notRateProjector, notForwardAdjustment, from, to);
        AddNotionalRepayment(paymentSchedule, notRateProjector, notForwardAdjustment, to);
        return paymentSchedule;
      }

      PaymentScheduleUtils.InterestPaymentGenerator generator = (prevPay, paymentDate, cycleStart, cycleEnd, periodStart, periodEnd, notional, cpn, fraction,
                                                                 includeEndDateInAccrual, accrueOnCycle) =>
                                                                new InflationLinkedInterestPayment(prevPay, paymentDate, InflationBond.Ccy, cycleStart, cycleEnd,
                                                                                                   periodStart, periodEnd, notional, cpn, InflationBond.DayCount,
                                                                                                   InflationBond.BaseInflation, rateProjector, forwardAdjustment)
                                                                                                   {
                                                                                                     IncludeEndDateInAccrual = includeEndDateInAccrual,
                                                                                                     AccrueOnCycle = accrueOnCycle,
                                                                                                     AccrualFactor = fraction
                                                                                                   };
      paymentSchedule = InflationBond.Schedule.InterestPaymentScheduleFactory(from, to,
                                                                              InflationBond.CashflowFlag,
                                                                              InflationBond.Coupon,
                                                                              InflationBond.CouponSchedule,
                                                                              InflationBond.Notional,
                                                                              InflationBond.AmortizationSchedule,
                                                                              InflationBond.Amortizes,
                                                                              InflationBond.DayCount, generator,
                                                                              IncludeSettlePayments, DefaultDate,
                                                                              DefaultSettleDate, null, RateResets);
      if (InflationBond.Accretes)
        AddAccretionPayments(paymentSchedule, rateProjector, forwardAdjustment, from, to);
      AddNotionalRepayment(paymentSchedule, rateProjector, forwardAdjustment, to);
      return paymentSchedule;
    }
	
    /// <summary>
    /// Index level at given settlement date
    /// </summary>
    /// <param name="settle">Settle date</param>
    /// <returns>Index level at settle, projected if necessary</returns>
    public double IndexLevel(Dt settle)
    {
      IRateProjector rateProjector = new InflationForwardCalculator(AsOf, (InflationIndex)ReferenceIndex,
                                                                    ReferenceCurve, InflationBond.IndexationMethod)
      {
        ResetLag = InflationBond.ResetLag
      };
      var fixSched = rateProjector.GetFixingSchedule(settle, settle, settle, settle);
      return rateProjector.Fixing(fixSched).Forward;
    }

    /// <summary>
    /// Expected ratio of inflation index at given settlement date to inflation index at issue date
    /// </summary>
    /// <param name="settle">Settle date</param>
    /// <returns>Expected ratio of inflation index to inflation index at issue date under the pricing measure</returns>
    public double IndexRatio(Dt settle)
    {
      return IndexLevel(settle)/InflationBond.BaseInflation;
    }

    /// <summary>
    /// Expected ratio of inflation index at given settlement date to inflation index at issue date
    /// </summary>
    public double IndexRatio()
    {
      return IndexRatio(Settle);
    }

    /// <summary>
    /// Generate ratio of inflation index to inflation index at the most recent coupon date
    /// </summary>
    /// <returns>The ratio of inflation index to inflation index at the previous coupon date</returns>
    public double IndexRatioPreviousCouponDate(Dt settle)
    {
      Dt prevCouponDate = InflationBond.Schedule.GetPrevCouponDate(settle);
      if (prevCouponDate.IsEmpty())
        prevCouponDate = InflationBond.Effective;
      return IndexRatio(prevCouponDate);
    }

    /// <summary>
    /// Generate portion of settlement amount attributable to principal at last coupon's inflation factor
    /// </summary>
    /// <param name="settle">Settlement date</param>
    /// <returns>The portion of settlement amount attributable to principal at last coupon's inflation factor</returns>
    public double FlatSettlement(Dt settle)
    {
      if (!HasMarketMethods)
        throw new NotImplementedException("Market calcs not implemented for floating coupon Inflation Linked Securities");
      return InternalBondPricer.FlatPrice() * IndexRatioPreviousCouponDate(settle) * Notional;
    }

    /// <summary>
    /// Return the flat price if quoted in flat price, or convert full price to flat price, if quoted in full price
    /// </summary>
    public double FlatPrice()
    {
      if (!HasMarketMethods)  // Another way of saying this is CIPS
      {
        if (quotingConvention_ != QuotingConvention.FullPrice && quotingConvention_ != QuotingConvention.FlatPrice)
          throw new NotImplementedException("Market calcs not implemented for floating coupon Inflation Linked Securities");
        if (!marketQuote_.HasValue)
          throw new ToolkitException("Flat price or full price not specified.");
        if (quotingConvention_ == QuotingConvention.FlatPrice)
          return marketQuote_.Value; // This is what was quoted !
        else // quoted in full price
          return marketQuote_.Value - Accrued() / Notional;
      }
      else
      {
        if (!marketQuote_.HasValue)
          throw new ToolkitException("Flat price is not specified.");
        if (quotingConvention_ == QuotingConvention.FlatPrice)
          return marketQuote_.Value; // This is what was quoted !
        return InternalBondPricer.FlatPrice();
      }
    }


    /// <summary>
    /// Generate portion of settlement amount attributable to principal at settle's inflation factor
    /// </summary>
    /// <returns>Portion of settlement amount attributable to principal at settle's inflation factor</returns>
    public double FlatSettlement()
    {
      return FlatSettlement(Settle);
    }

    /// <summary>
    /// Generate portion of settlement amount attributable to inflation growth of principal 
    /// since last coupon date
    /// </summary>
    /// <param name="settle">Settle date</param>
    /// <returns>Portion of settlement amount attributable to inflation growth of principal 
    /// since last coupon date</returns>
    public double InflationAccrual(Dt settle)
    {
      if (!HasMarketMethods)
        throw new NotImplementedException("Market calcs not implemented for floating coupon Inflation Linked Securities");
      return InternalBondPricer.FlatPrice() * (IndexRatio(settle) - IndexRatioPreviousCouponDate(settle)) * Notional;
    }

    /// <summary>
    /// Generate portion of settlement amount attributable to inflation growth of principal 
    /// since last coupon date
    /// </summary>
    /// <returns>portion of settlement amount attributable to inflation growth of principal 
    /// settle date</returns>
    public double InflationAccrual()
    {
      return InflationAccrual(Settle);
    }

    /// <summary>
    /// Generate full settlement amount given current market quote
    /// </summary>
    /// <param name="settle">Settle date</param>
    /// <returns>The full settlement amount given current market quote</returns>
    public double FullSettlement(Dt settle)
    {
      //return FlatPrice() * IndexRatio(settle) * Notional + Accrued(settle);
      double fp = FlatPrice();
      double ir = (HasMarketMethods ? IndexRatio(settle) : 1);
      double accr = Accrued(settle);
      return fp * ir * Notional + accr;
    }

    /// <summary>
    /// Generate full settlement amount given current market quote
    /// </summary>
    /// <returns>The full settlement amount given current market quote at settle </returns>
    public double MarketPv()
    {
      return FullSettlement(Settle);
    }

    /// <summary>
    ///   Accrued interest as a percentage of face
    /// </summary>
    ///
    /// <returns>accrued interest as a percentage of Notional</returns>
    ///
    public double AccruedInterest()
    {
      if (!HasMarketMethods)
        throw new NotImplementedException("Market calcs not implemented for floating coupon Inflation Linked Securities");
      return InternalBondPricer.AccruedInterest();
    }

    /// <summary>
    ///   Calculate accrued interest on the specified settlement date as a percentage of face
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Calculates accrued interest on the specified settlement date.</para>
    /// </remarks>
    ///
    /// <param name="settle">Settlement date</param>
    ///
    /// <returns>Accrued interest on the settlement date</returns>
    ///
    public double AccruedInterest(Dt settle)
    {
      if (!HasMarketMethods)
        throw new NotImplementedException("Market calcs not implemented for floating coupon Inflation Linked Securities");
      return InternalBondPricer.AccruedInterest(settle, settle);
    }


    /// <summary>
    ///   Full price as a percentage of Notional
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Bond market standard price to yield to maturity.</para>
    ///
    ///   <para>For the standard method</para>
    ///
    ///   <formula>
    ///    P = \frac{\displaystyle  {C \* v\frac{ v^{N-1} - 1}{v - 1} + R \* v^{N-1} + C_{n}} }{\displaystyle  {1 + t_{sn} \* y_{w}}} - AI  
    ///   </formula>
    ///
    ///   <para>where</para>
    ///
    ///   <list type="bullet">
    ///     <item><description><formula inline="true"> P </formula> is the clean bond price</description></item>
    ///     <item><description><formula inline="true"> y_{tm} </formula> is the yield to maturity</description></item>
    ///     <item><description><formula inline="true"> w </formula> is the frequency of coupon payments</description></item>
    ///     <item><description><formula inline="true"> y_{w} = \frac{y_{tm}}{w} </formula> is the periodic yield to maturity</description></item>
    ///     <item><description><formula inline="true"> v = \frac{1}{1 + y_{w}} </formula> is the periodic discount factor;</description></item>
    ///     <item><description><formula inline="true"> R </formula> is the redemption amount;</description></item>
    ///     <item><description><formula inline="true"> C </formula> is the current coupon;</description></item>
    ///     <item><description><formula inline="true"> C_n </formula> is the next coupon;</description></item>
    ///     <item><description><formula inline="true"> AI </formula> is the accrued interest</description></item>
    ///     <item><description><formula inline="true"> t_{sn} </formula> is the period fraction (in years)</description></item>
    ///     <item><description><formula inline="true"> N </formula> is the number of remaining coupon periods</description></item>
    ///   </list>
    ///
    /// </remarks>
    ///
    /// <returns>Full price of bond</returns>
    ///
    public double FullPrice()
    {
      if (!HasMarketMethods) // Another way of saying this is CIPS
      {
        if (quotingConvention_ != QuotingConvention.FullPrice && quotingConvention_ != QuotingConvention.FlatPrice)
          throw new NotImplementedException("Market calcs not implemented for floating coupon Inflation Linked Securities");
        if (!marketQuote_.HasValue)
          throw new ToolkitException("Flat price or full price not specified.");
        if (quotingConvention_ == QuotingConvention.FullPrice)
          return marketQuote_.Value; // This is what was quoted !
        else // quoted in flat price
          return marketQuote_.Value + Accrued() / Notional;
      }
      return InternalBondPricer.FullPrice();
    }

    /// <summary>
    /// Return Pv as if no inflation
    /// </summary>
    /// <returns></returns>
    public double NominalPv()
    {
      if (!HasMarketMethods)
        throw new NotImplementedException("Market calcs not implemented for floating coupon Inflation Linked Securities");
      return InternalBondPricer.ProductPv();
    }

    /// <summary>
    /// Calculate MTM
    /// </summary>
    /// <returns></returns>
    public double CalculateMTM()
    {
      double p1 = MarketPv();
      double p2 = PaymentPv();
      return p1 + p2;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tsyCurve"></param>
    /// <returns></returns>
    public double BreakEvenInflation(DiscountCurve tsyCurve)
    {
      return tsyCurve.F(AsOf, Maturity) - YieldToMaturity();
    }

    /// <summary>
    /// Model implied floor value
    /// </summary>
    /// <returns>Floor price</returns>
    public double ImpliedFloorPrice()
    {
      double flooredPv = 0.0, pv = 0.0;
      bool floored = InflationBond.FlooredNotional;
      if (InflationBond.FlooredNotional)
        flooredPv = ProductPv();
      else
        pv = ProductPv();
      Reset();
      InflationBond.FlooredNotional = !floored;
      if (InflationBond.FlooredNotional)
        flooredPv = ProductPv();
      else
        pv = ProductPv();
      InflationBond.FlooredNotional = floored; //restore 
      Reset();
      return (flooredPv - pv) / Notional;
    }

    /// <summary>
    ///   Present value of a Tips bond at pricing as-of date given pricing arguments
    /// </summary>
    /// <returns>Present value of an inflation bond</returns>
    ///
    public override double ProductPv()
    {
      var ps = GetPaymentSchedule();
      if (SurvivalCurve == null)
        return ps.Pv(AsOf, Settle, DiscountCurve, null, IncludeSettlePayments, DiscountingAccrued) *
               Notional;
      if (DefaultDate.IsEmpty())
      {
        var recovery = RecoveryRate;
        ps = ps.FilterPayments(Settle)
          .AddRecoveryPayments(InternalBond, dt => recovery);
      }
      return ps.SetProtectionStart(Settle).CalculatePv(
        AsOf, Settle, DiscountCurve, SurvivalCurve,
        IncludeSettlePayments, DiscountingAccrued,
        IncludeDefaultOnSettle(SurvivalCurve))*Notional;
    }

    private static bool IncludeDefaultOnSettle(SurvivalCurve survivalCurve)
    {
      return survivalCurve != null && survivalCurve.Defaulted == Defaulted.WillDefault;
    }

    /// <summary>
    /// Calculate the payment PV of the bond
    /// </summary>
    /// <returns></returns>
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
    /// The Payment pricer
    /// </summary>
    public override IPricer PaymentPricer
    {
      get
      {
        if (Payment != null)
        {
          if (paymentPricer_ == null)
            paymentPricer_ = BuildPaymentPricer(Payment, DiscountCurve);
        }
        return paymentPricer_;
      }
    }

    /// <summary>
    /// Build payment pricer
    /// </summary>
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

    ///<summary>
    /// The bond settle date
    ///</summary>
    public Dt ProductSettle
    {
      get { return Settle; }
    }

    /// <summary>
    ///   Calculate the full model price as a percentage of current Notional
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Cashflows after the settlement date are present valued back to the pricing
    ///   as-of date.</para>
    /// </remarks>
    ///
    /// <returns>Present value to the settlement date of the Bond as a percentage of current Notional</returns>
    ///
    public virtual double FullModelPrice()
    {
      return ProductPv() / Notional;
    }

    /// <summary>
    ///   Calculate the full market price as a percentage of current Notional
    /// </summary>
    public virtual double FullMarketPrice()
    {
      return MarketPv() / Notional;
    }


    /// <summary>
    /// Model spread
    /// </summary>
    /// <param name="isMarketQuoted">True if quote type is MARKET</param>
    /// <param name="refCurve">Reference curve</param>
    /// <param name="floorPrice">Value of the floor on principal repayment</param>
    /// <returns>Fair spread</returns>
    public double AssetSwapSpreadModel(bool isMarketQuoted, DiscountCurve refCurve, double floorPrice)
    {
      AssetSwapPricer aswPricer = GetAswPricer(this, true, isMarketQuoted, refCurve, floorPrice);
      return aswPricer.AssetSwapSpread();
    }

    /// <summary>
    /// Model spread
    /// </summary>
    /// <param name="isMarketQuoted">True if quote type is MARKET</param>
    /// <param name="refCurve">Reference curve</param>
    /// <param name="floorPrice">Value of the floor on principal repayment</param>
    /// <returns>Fair spread</returns>
    public double AssetSwapSpreadMarket(bool isMarketQuoted, DiscountCurve refCurve, double floorPrice)
    {
      AssetSwapPricer aswPricer = GetAswPricer(this, false, isMarketQuoted, refCurve, floorPrice);
      return aswPricer.AssetSwapSpread();
    }

    /// <summary>
    /// Get price from asset swap spread
    /// </summary>
    /// <param name="spread">Spread over reference index</param>
    /// <param name="convention">Quoting convention</param>
    /// <param name="refcurve">Reference curve</param>
    /// <param name="floorPrice">Floor price</param>
    /// <returns>Fair price</returns>
    public double PriceFromAssetSwapSpread(double spread, QuotingConvention convention, DiscountCurve refcurve,
                                           double floorPrice)
    {
      if (convention != QuotingConvention.ASW_Mkt && convention != QuotingConvention.ASW_Par)
        throw new ArgumentException("Quoting convention not supported.");
      bool isMarketQuoted = (convention == QuotingConvention.ASW_Mkt);
      if (InflationBond.FlooredNotional && !double.IsNaN(floorPrice))
      {
        var assetPricer = (InflationBondPricer)Clone();
        floorPrice /= 100;
        assetPricer.Product = (InflationBond)assetPricer.InflationBond.Clone();
        assetPricer.InflationBond.FlooredNotional = false;
        var p = GetAswPricer(assetPricer, spread, isMarketQuoted, 1.0, refcurve);
        return p.PriceFromAssetSwapSpread() + floorPrice;
      }
      else
      {
        var p = GetAswPricer(this, spread, isMarketQuoted, 1.0, refcurve);
        return p.PriceFromAssetSwapSpread();
      }
    }

    /// <summary>
    /// Asset swap pricer at par spread
    /// </summary>
    /// <param name="pricer">Pricer</param>
    /// <param name="useModelPrice">True to model implied price is to be used</param>
    /// <param name="isMarketQuoted">True if convention is MARKET</param>
    /// <param name="refcurve">Reference curve</param>
    /// <param name="floorPrice">Value of the floor on principal exchange </param>
    /// <returns></returns>
    private static AssetSwapPricer GetAswPricer(InflationBondPricer pricer, bool useModelPrice, bool isMarketQuoted, DiscountCurve refcurve, double floorPrice)
    {
      if (pricer.InflationBond.FlooredNotional && !double.IsNaN(floorPrice))
      {
        var assetPricer = (InflationBondPricer)pricer.Clone();
        floorPrice /= 100;
        assetPricer.Product = (InflationBond)assetPricer.InflationBond.Clone();
        assetPricer.InflationBond.FlooredNotional = false;
        return GetAswPricer(assetPricer, 0, isMarketQuoted,
                            (useModelPrice ? pricer.FullModelPrice() : pricer.FullMarketPrice()) - floorPrice, refcurve);
      }
      return GetAswPricer(pricer, 0, isMarketQuoted,
                          (useModelPrice ? pricer.FullModelPrice() : pricer.FullMarketPrice()), refcurve);
    }


    private static AssetSwapPricer GetAswPricer(InflationBondPricer pricer, double spread, bool isMarketQuoted, double price, CalibratedCurve refcurve)
    {
      Frequency freq;
      DayCount dc;
      BondPricer.DefaultAssetSwapParams(pricer.InflationBond.Ccy, out freq, out dc);
      var asw = new AssetSwap(pricer.InflationBond, pricer.Settle,
                              isMarketQuoted ? AssetSwapQuoteType.MARKET : AssetSwapQuoteType.PAR,
                              dc, freq, pricer.InflationBond.Calendar, pricer.InflationBond.BDConvention, spread, price);
      var interestRateIndex = new InterestRateIndex("ASW_RateIndex", new Tenor(freq), pricer.InflationBond.Ccy, dc,
                                                    pricer.InflationBond.Calendar, pricer.InflationBond.BDConvention, 2);
      //create AssetSwap Pricer that is by default Market Convention
      return new AssetSwapPricer(pricer, asw, pricer.DiscountCurve, null, null, refcurve, interestRateIndex);
    }

    /// <summary>
    /// YTM based on non-inflation based bond pricer
    /// </summary>
    /// <returns></returns>
    public double YieldToMaturity()
    {
      if (!HasMarketMethods)
        throw new NotImplementedException("Market calcs not implemented for floating coupon Inflation Linked Securities");
      return InternalBondPricer.YieldToMaturity();
    }

    /// <summary>
    /// Modified Duration based on non-inflation based bond pricer
    /// </summary>
    /// <returns></returns>
    public double ModDuration()
    {
      if (!HasMarketMethods)
        throw new NotImplementedException("Market calcs not implemented for floating coupon Inflation Linked Securities");
      return InternalBondPricer.ModDuration();
    }

    /// <summary>
    /// PV01 based on non-inflation based bond pricer
    /// </summary>
    /// <returns></returns>
    public double PV01()
    {
      if (!HasMarketMethods)
        throw new NotImplementedException("Market calcs not implemented for floating coupon Inflation Linked Securities");
      return InternalBondPricer.PV01();
    }

    /// <summary>
    /// Convexity based on non-inflation based bond pricer
    /// </summary>
    /// <returns></returns>
    public double Convexity()
    {
      if (!HasMarketMethods)
        throw new NotImplementedException("Market calcs not implemented for floating coupon Inflation Linked Securities");
      return InternalBondPricer.Convexity();
    }

    /// <summary>
    /// Duration based on non-inflation based bond pricer
    /// </summary>
    /// <returns></returns>
    public double Duration()
    {
      if (!HasMarketMethods)
        throw new NotImplementedException("Market calcs not implemented for floating coupon Inflation Linked Securities");
      return InternalBondPricer.Duration();
    }

    /// <summary>
    /// IRR based on non-inflation based bond pricer
    /// </summary>
    /// <returns>Irr </returns>
    public double Irr()
    {
      if (!HasMarketMethods)
        throw new NotImplementedException("Market calcs not implemented for floating coupon Inflation Linked Securities");
      return InternalBondPricer.Irr();
    }

    /// <summary>
    /// Implied Z spread
    /// </summary>
    /// <param name="useModelPrice">True to use model price</param>
    /// <returns>Z spread implied by the model price</returns>
    public double ImpliedZSpread(bool useModelPrice)
    {
      var bond = (InflationBond)Product;
      // Validate
      if (!IsActive())
        return 0.0;

      DiscountCurve origCurve = DiscountCurve;
      DiscountCurve newCurve;
      if (bond.DayCount == DayCount.ActualActualBond)
        newCurve = new DiscountCurve((DiscountCalibrator)origCurve.Calibrator.Clone(), origCurve.Interp,
                                     DayCount.Actual365Fixed, bond.Freq);
      else
        newCurve = new DiscountCurve((DiscountCalibrator)origCurve.Calibrator.Clone(), origCurve.Interp, bond.DayCount,
                                     bond.Freq);

      newCurve.Interp = origCurve.Interp;
      for (int i = 0; i < origCurve.Count; ++i)
        newCurve.Add(origCurve.GetDt(i), origCurve.GetVal(i));

      Dt settle = Product.Effective <= Settle ? Settle : Product.Effective;

      return PaymentScheduleUtils.ImpDiscountSpread(
        new CashflowAdapter(GetPaymentSchedule(null, AsOf)), settle, settle,
        newCurve, null, null, 0.0, StepSize, StepUnit,
        (useModelPrice ? FullModelPrice() : FullMarketPrice()),
        AdapterUtil.CreateFlags(false, false, DiscountingAccrued));
    }


    /// <summary>
    /// Sets market quote 
    /// </summary>
    /// <param name="quote">Quote</param>
    /// <param name="convention">Quoting convention</param>
    /// <param name="refcurve">Reference curve</param>
    /// <param name="floorPrice">Floor price</param>
    public void SetMarketQuote(double quote, QuotingConvention convention, DiscountCurve refcurve, double floorPrice)
    {
      if (!Double.IsNaN(quote))
      {
        marketQuote_ = quote;
        quotingConvention_ = convention;
        if (HasMarketMethods)
        {
          InternalBondPricer.MarketQuote = quote;
          InternalBondPricer.QuotingConvention = convention;

          if (convention == QuotingConvention.ASW_Par || convention == QuotingConvention.ASW_Mkt)
          {
            if (refcurve == null)
              throw new ArgumentException("Must provide reference curve when quoting with ASW");

            InternalBondPricer.QuotingConvention = QuotingConvention.FlatPrice;
            InternalBondPricer.MarketQuote =
              ConvertFullToFlat(PriceFromAssetSwapSpread(quote, convention, refcurve, floorPrice));
          }
        }
      }
    }

    private double ConvertFullToFlat(double quote)
    {
      if (!HasMarketMethods)
        throw new NotImplementedException("Market calcs not implemented for floating coupon Inflation Linked Securities");
      return (quote / IndexRatio(Settle)) - InternalBondPricer.AccruedInterest();
    }

    /// <summary>
    /// Returns the inflation price at date
    /// </summary>
    /// <param name="inflationIndex">Inflation index object</param>
    /// <param name="iCurve">Inflation curve</param>
    /// <param name="date">Date</param>
    /// <param name="indexLevels"> Past inflation index levels </param>
    /// <param name="indexationLag">Availability lag is the calendar period between the reset date and the relevant fixing date of the reference index</param>
    /// <param name="indexationMethod">Indexation method</param>
    /// <returns>Inflation price at date</returns>
    public static double GetInflationFactorAtDate(
        InflationIndex inflationIndex,
        InflationCurve iCurve,
        Dt date,
        IList<RateReset> indexLevels,
        Tenor indexationLag,
        IndexationMethod indexationMethod)
    {
      // This computation has been factored out from qInflationFactorAtDate() here.
      if (iCurve == null && (indexLevels == null || indexLevels.Count == 0))
        throw new ArgumentException("Must provide inflation curve or historical resets");
      Dt asOf = (iCurve != null) ? iCurve.AsOf : indexLevels[indexLevels.Count - 1].Date;
      var couponCalculator = new InflationForwardCalculator(asOf, inflationIndex, iCurve, indexationMethod) { ResetLag = indexationLag };
      var fixing = couponCalculator.Fixing(couponCalculator.GetFixingSchedule(Dt.Empty, date, date, date));
      if (fixing.RateResetState == RateResetState.Missing)
        throw new ArgumentException("Missing historical data");
      return fixing.Forward;
    }

    /// <summary>
    /// This is a simpliefied version of the above function; it assumes that InflationCurve has InflationIndex, which in turn has index levels.
    /// </summary>
    /// <param name="iCurve">Inflation curve</param>
    /// <param name="date">Date</param>
    /// <param name="indexationLag">Availability lag is the calendar period between the reset date and the relevant fixing date of the reference index</param>
    /// <param name="indexationMethod">Indexation method</param>
    /// <returns>Inflation price at date</returns>
    public static double GetInflationFactorAtDate(
        InflationCurve iCurve,
        Dt date,
        Tenor indexationLag,
        IndexationMethod indexationMethod)
    {
      if (iCurve == null)
        throw new ArgumentException("Null inflation curve passed");
      InflationIndex inflationIndex = iCurve.InflationIndex;
      IList<RateReset> indexLevels = null;
      if (inflationIndex != null && inflationIndex.HistoricalObservations != null)
        indexLevels = inflationIndex.HistoricalObservations.ToList();

      double ret = GetInflationFactorAtDate(
        inflationIndex,
        iCurve,
        date,
        indexLevels,
        indexationLag,
        indexationMethod);
      return ret;
    }

    #region IRepoSecurityPricer methods

    /// <summary>
    ///  Security value method for bond repos
    /// </summary>
    /// <returns></returns>
    public double SecurityMarketValue()
    {
      return FullPrice();
    }

    #endregion

    #endregion Methods

    #region Properties

    /// <summary>
    /// Accessor for model parameters used for convexity adjustments
    /// </summary>
    public RateModelParameters RateModelParameters { get; private set; }

    /// <summary>
    /// Accessor for the inflation level at the bond issue date
    /// </summary>
    public double BaseInflation
    {
      get { return InflationBond.BaseInflation; }
    }

    /// <summary>
    /// Reference index 
    /// </summary>
    public ReferenceIndex ReferenceIndex { get; private set; }


    /// <summary>
    /// Historical Rate Resets
    /// </summary>
    public RateResets RateResets { get; private set; }

    /// <summary>
    /// Internal Bond Pricer
    /// </summary>
    public BondPricer InternalBondPricer { get; private set; }

    /// <summary>
    /// Internal bond
    /// </summary>
    public Bond InternalBond
    {
      get
      {
        if (InternalBondPricer != null)
          return InternalBondPricer.Bond;
        return null;
      }
    }

    /// <summary>
    ///   Quoting convention for bond
    /// </summary>
    public QuotingConvention QuotingConvention
    {
      get
      {
        if (!HasMarketMethods)
          throw new NotImplementedException(
            "Market calcs not implemented for floating coupon Inflation Linked Securities");
        return InternalBondPricer.QuotingConvention;
      }
    }

    /// <summary>
    ///   Market quote for bond
    /// </summary>
    /// 
    /// <details>
    ///   <para>A variety of quoting types are supported for bonds
    ///   and are set by <see cref="QuotingConvention"/>. The default
    ///   quoting convention is FlatPrice.</para>
    /// </details>
    /// 
    public double MarketQuote
    {
      get { return InternalBondPricer.MarketQuote; }
    }

    /// <summary>
    /// Discount curve
    /// </summary>
    public DiscountCurve DiscountCurve { get; private set; }

    /// <summary>
    /// Inflation curve associated to the index
    /// </summary>
    public InflationCurve ReferenceCurve { get; private set; }

    /// <summary>
    /// Survival curve
    /// </summary>
    public SurvivalCurve SurvivalCurve { get; set; }

    /// <summary>
    /// Default date
    /// </summary>
    private Dt DefaultDate
    {
      get { return (SurvivalCurve != null) ? SurvivalCurve.DefaultDate : Dt.Empty; }
    }

    /// <summary>
    /// Default settlement
    /// </summary>
    private Dt? DefaultSettle { get; set; }

    /// <summary>
    /// Default settlement date
    /// </summary>
    public Dt DefaultSettleDate
    {
      get { return DefaultSettle.HasValue ? DefaultSettle.Value : DefaultDate; }
      set { DefaultSettle = value; }
    }

    /// <summary>
    /// Overwrite survival curve recovery
    /// </summary>
    private double? RecoveryOverwrite { get; set; }

    /// <summary>
    /// Recovery rate
    /// </summary>
    public double RecoveryRate
    {
      get
      {
        return RecoveryOverwrite.HasValue
                 ? RecoveryOverwrite.Value
                 : (SurvivalCurve != null && SurvivalCurve.SurvivalCalibrator != null)
                     ? SurvivalCurve.SurvivalCalibrator.RecoveryCurve.Interpolate(InflationBond.Maturity)
                     : 0.0;
      }
      set { RecoveryOverwrite = value; }
    }

    /// <summary>
    /// Inflation bond
    /// </summary>
    public InflationBond InflationBond
    {
      get { return (InflationBond)Product; }
    }

    /// <summary>
    /// True if pricer supports bond market calculations
    /// </summary>
    public bool HasMarketMethods
    {
      get { return InternalBondPricer != null; }
    }

    #endregion Properties

    #region ICashflowNodesGenerator Members

    /// <summary>
    /// Generate array of cashflow nodes for simulation
    /// </summary>
    IList<ICashflowNode> ICashflowNodesGenerator.Cashflow
    {
      get
      {
        var ps = GetPaymentSchedule();
        if (SurvivalCurve != null)
          return ps.ToCashflowNodeList(Math.Abs(InflationBond.Notional), 1.0, DiscountCurve, SurvivalCurve.Interpolate, null);
        return ps.ToCashflowNodeList(Math.Abs(InflationBond.Notional), 1.0, DiscountCurve, null, null);
      }
    }

    #endregion

    #region Data

    private double? marketQuote_;
    private QuotingConvention quotingConvention_;
    private Dt tradeSettle_;

    #endregion
  }
}