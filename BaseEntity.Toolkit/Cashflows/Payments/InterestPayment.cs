//
//  -2011. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BaseEntity.Shared;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows.Payments;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows
{
  /// <summary>
  /// Base Class for payment that involve interest periods
  /// </summary>
  [Serializable]
  public abstract class InterestPayment : Payment
  {
    #region Constructor

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="prevPayDt">Last payment date</param>
    /// <param name="payDt">Payment date</param>
    /// <param name="ccy">Currency of payment</param>
    /// <param name="cycleStart">Cycle start date</param>
    /// <param name="cycleEnd">Cycle end date</param>
    /// <param name="periodStart">Accrual start</param>
    /// <param name="periodEnd">Accrual end</param>
    /// <param name="exDivDt">Ex dividend date</param>
    /// <param name="notional">Notional</param>
    /// <param name="dc">Daycount convention</param>
    /// <param name="compoundingFreq">Compounding frequency</param>
    protected InterestPayment(
      Dt prevPayDt, Dt payDt, Currency ccy, Dt cycleStart, Dt cycleEnd, Dt periodStart,
      Dt periodEnd, Dt exDivDt, double notional, DayCount dc, Frequency compoundingFreq)
      : this(prevPayDt, payDt, ccy, cycleStart, cycleEnd, periodStart,
        periodEnd, exDivDt, notional, dc, compoundingFreq, CycleRule.None)
    {
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="prevPayDt">Last payment date</param>
    /// <param name="payDt">Payment date</param>
    /// <param name="ccy">Currency of payment</param>
    /// <param name="cycleStart">Cycle start date</param>
    /// <param name="cycleEnd">Cycle end date</param>
    /// <param name="periodStart">Accrual start</param>
    /// <param name="periodEnd">Accrual end</param>
    /// <param name="exDivDt">Ex dividend date</param>
    /// <param name="notional">Notional</param>
    /// <param name="dc">Daycount convention</param>
    /// <param name="compoundingFreq">Compounding frequency</param>
    /// <param name="cmpndCycleRule">Compounding cycle rule</param>
    protected InterestPayment(
      Dt prevPayDt, Dt payDt, Currency ccy, Dt cycleStart, Dt cycleEnd, 
      Dt periodStart,Dt periodEnd, Dt exDivDt, double notional, DayCount dc, 
      Frequency compoundingFreq, CycleRule cmpndCycleRule)
      : base(payDt, ccy)
    {
      PreviousPaymentDate = prevPayDt;
      CycleStartDate = cycleStart;
      CycleEndDate = cycleEnd;
      PeriodStartDate = periodStart;
      PeriodEndDate = periodEnd;
      ExDivDate = exDivDt;
      Notional = notional;
      DayCount = dc;
      CompoundingFrequency = compoundingFreq;
      CompoundingCycleRule = cmpndCycleRule;
      AccruedFractionAtDefault = 0.5;
      Frequency = Frequency.None;
    }

    #endregion

    #region Properties

    private double? AccrualFactorOverride { get; set; }

    /// <summary>
    /// For default contingent payments 
    /// </summary>
    public double AccruedFractionAtDefault { get; set; }
    
    /// <summary>
    /// Compounding frequency
    /// </summary>
    public Frequency CompoundingFrequency { get; private set; }

    /// <summary>
    /// Compounding Cycle rule
    /// </summary>
    public CycleRule CompoundingCycleRule { get; private set; }

    /// <summary>
    /// Start of Interest cycle
    /// </summary>
    public Dt CycleStartDate { get; set; }

    /// <summary>
    /// End of Interest cycle
    /// </summary>
    public Dt CycleEndDate { get; set; }

    /// <summary>
    /// Start of accrual within cycle
    /// </summary>
    public Dt PeriodStartDate { get; set; }

    /// <summary>
    /// End of accrual within cycle
    /// </summary>
    public Dt PeriodEndDate { get; set; }

    /// <summary>
    /// Last payment date
    /// </summary>
    public Dt PreviousPaymentDate { get; set; }

    /// <summary>
    /// Ex div date for payment at end of period
    /// </summary>
    public Dt ExDivDate
    {
      get { return CutoffDate; }
      set { CutoffDate = value; }
    }

    /// <summary>
    /// Convention for calcing accrual for period
    /// </summary>
    public DayCount DayCount { get; set; }

    /// <summary>
    /// Payment frequency (needed for some DayCount conventions)
    /// </summary>
    public Frequency Frequency { get; set; }

    /// <summary>
    /// Accrue on cycle
    /// </summary>
    public bool AccrueOnCycle { get; set; }

    /// <summary>
    /// Include last date in day count for accrual calculation
    /// </summary>
    public bool IncludeEndDateInAccrual { get; set; }

    /// <summary>
    /// Payment notional (adjusted by prepayment factor, if applicable)
    /// </summary>
    public double Notional { get; set; }

    /// <summary>
    /// Fixed coupon or spread over floating rate
    /// </summary>
    public double FixedCoupon
    {
      get
      {
        if (RateSchedule != null)
        {
          return CouponPeriodUtil.CalculateAverageCoupon(AccrualStart,
            AccrualEnd, DayCount, RateSchedule);
        }
        return _fixedCoupon;
      }
      set
      {
        if (RateSchedule != null)
        {
          RateSchedule[0] = new CouponPeriod(RateSchedule[0].Date, value);
        }
        _fixedCoupon = value;
      }
    }

    /// <summary>
    /// Gets or sets the rate schedule, including all the coupon schedule.
    /// </summary>
    /// <value>The rate schedule.</value>
    public IList<CouponPeriod> RateSchedule { get; set; }

    private double _fixedCoupon;

    /// <summary>
    /// Interest Rate for period
    /// </summary>
    public abstract double EffectiveRate { get; set; }

    /// <summary>
    /// Determine accrual start
    /// </summary>
    /// <returns></returns>
    public Dt AccrualStart
    {
      get { return AccrueOnCycle ? PeriodStartDate : PreviousPaymentDate; }
    }

    /// <summary>
    /// Determine accrual end
    /// </summary>
    /// <returns></returns>
    public Dt AccrualEnd
    {
      get
      {
        Dt end = AccrueOnCycle ? PeriodEndDate : PayDt;
        if (IncludeEndDateInAccrual)
          end = Dt.Add(end, 1);
        return end;
      }
    }

    /// <summary>
    /// Determine amount of accrual for this period.
    /// </summary>
    /// <returns></returns>
    public virtual double AccrualFactor
    {
      get
      {
        if (AccrualFactorOverride.HasValue)
          return AccrualFactorOverride.Value;
        return Dt.Fraction(CycleStartDate, CycleEndDate, AccrualStart, AccrualEnd, DayCount, Frequency);
      }
      set { AccrualFactorOverride = value; }
    }

    /// <summary>
    ///  Gets the principal used as the base of interest calculation in this payment
    /// </summary>
    /// <remarks>
    ///   For a compounded interest payment, the calculation principal may differ
    ///   from the notional in that it also includes the accumulated interests.
    /// </remarks>
    /// <value>The calculation principal</value>
    public double CalculationPrincipal
    {
      get
      {
        return PrincipalCalculator != null
          ? PrincipalCalculator() : Notional;
      }
    }

    /// <summary>
    /// Gets or sets the principal calculator
    /// </summary>
    /// <value>The principal calculator</value>
    internal Func<double> PrincipalCalculator { get; set; }

    internal bool IncludeEndDateProtection { get; set; }

    #endregion

    #region Methods

    /// <summary>
    /// Convert a payment to a cashflow node used to simulate the 
    /// realized payment amount 
    /// </summary>
    /// <param name="notional">Notional amount</param>
    /// <param name="discountCurve">Discount curve to discount payment</param>
    /// <param name="survivalFunction">Survival curve if coupon is credit contingent</param>
    /// <returns>ICashflowNode</returns>
    /// <remarks>
    /// Rather than the expected payment amount,  
    /// the cashflow node computes the realized payment amount.
    /// </remarks>
    public override ICashflowNode ToCashflowNode(double notional, DiscountCurve discountCurve, Func<Dt,double> survivalFunction)
    {
      return new CouponCashflowNode(this, notional, discountCurve, survivalFunction)
               {
                 FixedAmount = ComputeAmount()
               };
    }

    /// <summary>
    /// Scale payment appropriately
    /// </summary>
    /// <param name="factor">Scaling factor</param>
    public override void Scale(double factor)
    {
      base.Scale(factor);
      Notional *= factor;
    }

    /// <summary>
    /// Payment Amount
    /// </summary>
    protected override double ComputeAmount()
    {
      return EffectiveRate * CalculationPrincipal * AccrualFactor;
    }

    /// <summary>
    /// Risky discount interest payment
    /// </summary>
    /// <param name="discountFunction">discount curve</param>
    /// <param name="survivalFunction">survival function</param>
    /// <returns>risky discount factor</returns>
    public override double RiskyDiscount(
      Func<Dt, double> discountFunction, Func<Dt, double> survivalFunction)
    {
      if (survivalFunction == null)
      {
        return discountFunction(PayDt);
      }

      var calculator = survivalFunction.Target as DefaultRiskCalculator;
      if (calculator != null)
      {
        return calculator.RiskyDiscount(this, discountFunction);
      }

      double df = discountFunction(PayDt);
      double delta = AccruedFractionAtDefault;
      double s1 = survivalFunction(GetCreditRiskEndDate());
      if (delta.AlmostEquals(0.0)) return s1*df;
      double s0 = survivalFunction(PreviousPaymentDate);
      df *= delta*(s0 - s1) + s1;
      return df;
    }


    /// <summary>
    /// Accrued payment up to date
    /// </summary>
    /// <param name="date">Date</param>
    /// <returns>Accrued amount</returns>
    /// <param name="accrual">Coupon - Accrued</param>
    public override double Accrued(Dt date, out double accrual)
    {
      Dt begin = AccrueOnCycle ? PeriodStartDate : PreviousPaymentDate;
      if (begin >= date)
      {
        accrual = DomesticAmount;
        return 0.0;
      }
      accrual = DomesticAmount;
      Dt end = AccrueOnCycle ? PeriodEndDate : PayDt;
      if (date >= end)
      {
        var accrued = accrual;
        accrual = 0;
        return accrued;
      }
      else
      {
        var accrued = accrual*Dt.Diff(begin, date, DayCount)/
          Dt.Diff(begin, end, DayCount);
        accrual -= accrued;
        return accrued;
      }
    }

    /// <summary>
    /// Add Data Columns
    /// </summary>
    /// <param name="collection"></param>
    public override void AddDataColumns(DataColumnCollection collection)
    {
      base.AddDataColumns(collection);
      if (!collection.Contains("Period Start"))
        collection.Add(new DataColumn("Period Start", typeof (string)));
      if (!collection.Contains("Period End"))
        collection.Add(new DataColumn("Period End", typeof (string)));
      if (!collection.Contains("Accrual Start"))
        collection.Add(new DataColumn("Accrual Start", typeof (string)));
      if (!collection.Contains("Accrual End"))
        collection.Add(new DataColumn("Accrual End", typeof (string)));
      //if (!collection.Contains("Ex div date"))  //TODO make this optional column (commenting it out currently because it breaks the replay tests)
      //  collection.Add(new DataColumn("Ex div date", typeof(string)));
      if (!collection.Contains("Accrual Factor"))
        collection.Add(new DataColumn("Accrual Factor", typeof (double)));
      if (!collection.Contains("Notional"))
        collection.Add(new DataColumn("Notional", typeof (double)));
    }

    /// <summary>
    /// Add Data Values
    /// </summary>
    /// <param name="row"></param>
    /// <param name="dtFormat"></param>
    public override void AddDataValues(DataRow row, string dtFormat)
    {
      base.AddDataValues(row, dtFormat);
      row["Period Start"] = PeriodStartDate.ToStr(dtFormat);
      row["Period End"] = PeriodEndDate.ToStr(dtFormat);
      row["Accrual Start"] = AccrualStart.ToStr(dtFormat);
      row["Accrual End"] = AccrualEnd.ToStr(dtFormat);
      //row["Ex div date"] = ExDivDate.ToStr(dtFormat);
      row["Accrual Factor"] = AccrualFactor;
      row["Notional"] = Notional;
    }

    #endregion

    #region CouponCashflowNode

    /// <summary>
    /// Coupon cashflow node
    /// </summary>
    [Serializable]
    protected class CouponCashflowNode : CashflowNode
    {
      #region Constructor
      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="payment">Payment</param>
      /// <param name="notional">Notional amount</param>
      /// <param name="discountCurve">DiscountCurve for discounting coupon</param>
      /// <param name="survivalFunction">Loss function if coupon is credit contingent</param>
      internal CouponCashflowNode(InterestPayment payment, double notional, DiscountCurve discountCurve, Func<Dt,double> survivalFunction)
        : base(payment, notional, discountCurve, survivalFunction)
      {
        PrevPayDt = payment.PreviousPaymentDate;
        AccruedFractionAtDefault = payment.AccruedFractionAtDefault;
      }
      #endregion

      #region Properties
      
      /// <summary>
      /// Compute expected average surviving notional during the accrual period as <m>\Delta*S_0  + (1 - \Delta)*S_1</m>, where <m>S_0,S_1</m> are expected surviving notional at the
      /// beginning and at the end of the period and <m>\Delta \in [0,1]</m> 
      /// </summary>
      protected double AccruedFractionAtDefault { get; private set; }
      
      /// <summary>
      /// Previous payment date
      /// </summary>
      protected Dt PrevPayDt { get; private set; }
      #endregion

      #region ICashflowNode Members
      /// <summary>
      /// Discount to AsOf date  
      /// </summary>
      /// <returns>Risky discount </returns>
      public override double RiskyDiscount()
      {
        double df = DiscountCurve.Interpolate(PayDt);
        if (SurvivalFunction != null)
        {
          double delta = AccruedFractionAtDefault;
          double s0 = SurvivalFunction(PrevPayDt);
          double s1 = SurvivalFunction(PayDt);
          df *= delta * (s0 - s1) + s1;
        }
        return df;
      }

      #endregion
    }

    #endregion CouponCashflowNode
  }
}
