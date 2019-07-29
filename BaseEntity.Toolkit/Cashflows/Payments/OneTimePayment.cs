using System;
using System.Collections.Generic;
using System.Data;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Curves;

namespace BaseEntity.Toolkit.Cashflows
{

  #region OneTimePayment

  /// <summary>
  /// Simple Payment Representation
  /// </summary>
  [Serializable]
  public abstract class OneTimePayment : Payment
  {
    #region Constructor

    /// <summary>
    /// Create Simple Payment
    /// </summary>
    /// <param name="payDate">Date of payment</param>
    /// <param name="ccy">Currency of payment</param>
    protected OneTimePayment(Dt payDate, Currency ccy)
      : base(payDate, ccy)
    {}

    /// <summary>
    /// Create Simple Payment
    /// </summary>
    /// <param name="payDate">Date of payment</param>
    /// <param name="amount">Amount of payment</param>
    /// <param name="ccy">Currency of payment</param>
    protected OneTimePayment(Dt payDate, double amount, Currency ccy)
      : base(payDate, amount, ccy)
    {}

    #endregion

    #region Methods

    /// <summary>
    /// Convert a payment to a cashflow node used to simulate the 
    /// realized payment amount 
    /// </summary>
    /// <param name="notional">Payment notional</param>
    /// <param name="discountCurve">Discount curve to discount payment</param>
    /// <param name="survivalFunction">Surviving principal function if coupon is credit contingent</param>
    /// <returns>ICashflowNode</returns>
    /// <remarks>
    /// Rather than the expected payment amount,  
    /// the cashflow node computes the realized payment amount.
    /// </remarks>
    public override ICashflowNode ToCashflowNode(double notional, DiscountCurve discountCurve, Func<Dt, double> survivalFunction)
    {
      return new OneTimeCashflowNode(this, notional, discountCurve, survivalFunction)
             {
               FixedAmount = ComputeAmount()
             };
    }

    /// <exclude></exclude>
    protected override double ComputeAmount()
    {
      return Amount;
    }

    /// <summary>
    /// Add data 
    /// </summary>
    /// <param name="collection">Data collection </param>
    public override void AddDataColumns(DataColumnCollection collection)
    {
      base.AddDataColumns(collection);
      if (!collection.Contains("Amount"))
        collection.Add(new DataColumn("Amount", typeof(double)));
    }

    /// <summary>
    /// Add data values
    /// </summary>
    /// <param name="row">Row</param>
    /// <param name="dtFormat">Format</param>
    public override void AddDataValues(DataRow row, string dtFormat)
    {
      base.AddDataValues(row, dtFormat);
      row["Amount"] = Amount;
    }

    #endregion

    #region OneTimeCashflowNode

    /// <summary>
    /// One time payment
    /// </summary>
    [Serializable]
    protected class OneTimeCashflowNode : CashflowNode
    {
      #region Constructor

      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="payment">Payment</param>
      /// <param name="notional">Notional</param>
      /// <param name="discountCurve">DiscountCurve for discounting coupon</param>
      /// <param name="survivalFunction">Surviving notional </param>
      internal OneTimeCashflowNode(OneTimePayment payment, double notional, DiscountCurve discountCurve, Func<Dt, double> survivalFunction)
        : base(payment, notional, discountCurve, survivalFunction)
      {}

      #endregion
    }

    #endregion
  }

  #endregion

  #region BasicPayment

  /// <summary>
  /// Basic payment
  /// </summary>
  [Serializable]
  public class BasicPayment : OneTimePayment
  {
    /// <summary>
    /// Create a principal exchange payment
    /// </summary>
    /// <param name="payDate">Date of payment</param>
    /// <param name="amount">Amount of payment</param>
    /// <param name="ccy">Currency of payment</param>
    public BasicPayment(Dt payDate, double amount, Currency ccy)
      : base(payDate, amount, ccy)
    {}
  }

  #endregion

  #region PrincipalExchange

  /// <summary>
  /// Type of payment that indicates that principal is actually exchanged with counterparty
  /// </summary>
  [Serializable]
  public class PrincipalExchange : OneTimePayment
  {
    /// <summary>
    /// Create a principal exchange payment
    /// </summary>
    /// <param name="payDate">Date of payment</param>
    /// <param name="principal">Amount of payment</param>
    /// <param name="ccy">Currency of payment</param>
    public PrincipalExchange(Dt payDate, double principal, Currency ccy)
      : base(payDate, ccy)
    {
      Notional = principal;
    }

    /// <summary>
    /// Notional amount
    /// </summary>
    public double Notional { get; private set; }

    /// <summary>
    /// Payment amount
    /// </summary>
    /// <returns>Amount</returns>
    protected override double ComputeAmount()
    {
      return Notional;
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
    /// Add Data Columns
    /// </summary>
    /// <param name="collection">DataColumns</param>
    public override void AddDataColumns(DataColumnCollection collection)
    {
      base.AddDataColumns(collection);
      if (!collection.Contains("Notional"))
        collection.Add(new DataColumn("Notional", typeof(double)));
    }

    /// <summary>
    /// Create Data Values
    /// </summary>
    /// <param name="row">row to add values</param>
    /// <param name="dtFormat">Date format</param>
    public override void AddDataValues(DataRow row, string dtFormat)
    {
      base.AddDataValues(row, dtFormat);
      row["Notional"] = Amount;
    }
  }

  #endregion

  #region UpfrontFee

  /// <summary>
  /// Type of payment that typically represents an upfront lumpSumPayment
  /// </summary>
  [Serializable]
  public class UpfrontFee : OneTimePayment
  {
    /// <summary>
    /// Create an upfront payment
    /// </summary>
    /// <param name="payDate">Date of payment</param>
    /// <param name="amount">Amount of payment</param>
    /// <param name="ccy">Currency of payment</param>
    public UpfrontFee(Dt payDate, double amount, Currency ccy)
      : base(payDate, amount, ccy)
    {}

    /// <summary>
    /// Risky dicount for upfront fee. Shoud directly return the interpolation
    /// on the discount curve (discountFunction).
    /// </summary>
    /// <param name="discountFunction">discount curve</param>
    /// <param name="survivalFunction">survival function</param>
    /// <returns>risky discount factor</returns>
    public override double RiskyDiscount(Func<Dt, double> discountFunction,
      Func<Dt, double> survivalFunction)
    {
      return discountFunction(PayDt);
    }
  }

  #endregion

  #region BulletBonusPayment

  /// <summary>
  /// Type of payment is used as bonus, when default has not happened
  /// </summary>
  [Serializable]
  public class BulletBonusPayment : OneTimePayment
  {
    /// <summary>
    /// Create a principal exchange payment
    /// </summary>
    /// <param name="payDate">Date of payment</param>
    /// <param name="amount">Amount of payment</param>
    /// <param name="ccy">Currency of payment</param>
    public BulletBonusPayment(Dt payDate, double amount, Currency ccy)
      : base(payDate, amount, ccy)
    {}
  }

  #endregion

  #region DefaultSettlement

  /// <summary>
  /// Default payment
  /// </summary>
  [Serializable]
  public class DefaultSettlement : OneTimePayment
  {
    #region Constructors

    /// <summary>
    /// Create a principal exchange payment
    /// </summary>
    /// <param name="defaultDt">Default date</param>
    /// <param name="defaultSettleDt">Date on which default payment is settled</param>
    /// <param name="ccy">Currency of payment</param>
    /// <param name="notional">Notional amount</param>
    /// <param name="recoveryRate">Recovery rate</param>
    /// <param name="accrual">Accrued interests</param>
    /// <param name="isFunded"><c>true</c> for funded; otherwise, <c>false</c></param>
    public DefaultSettlement(Dt defaultDt, Dt defaultSettleDt,
      Currency ccy, double notional, double recoveryRate,
      double accrual = 0, bool isFunded = false)
      : base(defaultSettleDt.IsValid() ? defaultSettleDt : defaultDt, ccy)
    {
      IsFunded = isFunded;
      Notional = notional;
      DefaultDate = defaultDt;
      RecoveryRate = recoveryRate;
      Accrual = accrual;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Is funded effects the sign on the amount, and whether it is accounted as amount or loss in the cashflow object
    /// </summary>
    public bool IsFunded { get; internal set; }

    /// <summary>
    /// Notional 
    /// </summary>
    public double Notional { get; private set; }

    /// <summary>
    /// Default Date
    /// </summary>
    public Dt DefaultDate { get; }

    /// <summary>
    /// Recovery rate
    /// </summary>
    public double RecoveryRate { get; }

    /// <summary>
    /// Accrued interest
    /// </summary>
    public double Accrual { get; set; }

    #endregion

    #region Methods

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
    /// Amount of payment
    /// </summary>
    protected override double ComputeAmount()
    {
      return Notional*(Accrual + (IsFunded ?
        RecoveryRate : (RecoveryRate - 1.0)));
    }

    /// <summary>
    /// Gets the accrued interest amount
    /// </summary>
    /// <value>The accrual amount</value>
    internal double AccrualAmount => Notional*Accrual;

    /// <summary>
    /// Gets the recovery amount.
    /// </summary>
    /// <value>The recovery amount.</value>
    internal double RecoveryAmount =>
      Notional*(IsFunded ? RecoveryRate : (RecoveryRate - 1.0));

    #endregion
  }

  #endregion

  #region FloatingPrincipalExchange

  /// <summary>
  /// Floating principal exchange abstract base class
  /// </summary>
  [Serializable]
  public abstract class FloatingPrincipalExchange : PrincipalExchange
  {
    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="payDate">Date of payment</param>
    /// <param name="ccy">Currency of payment</param>
    /// <param name="notional">Notional amount of payment</param>
    ///<param name="rateProjector">Engine for fixing calculations</param>
    /// <param name="forwardAdjustment">Engine for convexity adjustment calculations</param>
    /// <remarks>The payment amount is then computed as (notional x fixing)</remarks>
    protected FloatingPrincipalExchange(Dt payDate, double notional, Currency ccy, IRateProjector rateProjector, IForwardAdjustment forwardAdjustment)
      : base(payDate, notional, ccy)
    {
      RateProjector = rateProjector;
      ForwardAdjustment = forwardAdjustment;
      FixingSchedule = rateProjector.GetFixingSchedule(payDate, payDate, payDate, payDate);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Override effective principal
    /// </summary>
    protected double? EffectiveExchangeOverride { get; set; }

    /// <summary>
    /// Set/Get AsOf date of ForwardAdjustment 
    /// </summary>
    public override Dt VolatilityStartDt
    {
      get
      {
        if (ForwardAdjustment == null)
          return base.VolatilityStartDt;
        return ((ForwardAdjustment)ForwardAdjustment).AsOf;
      }
      set
      {
        if (ForwardAdjustment == null)
          return;
        ((ForwardAdjustment)ForwardAdjustment).AsOf = value;
      }
    }

    /// <summary>
    /// Processor for projection of floating rate
    /// </summary>
    internal IRateProjector RateProjector { get; private set; }

    /// <summary>
    /// Processor for projection of floating rate
    /// </summary>
    internal IForwardAdjustment ForwardAdjustment { get; private set; }


    /// <summary>
    /// Schedules for projection of floating notional
    /// </summary>
    protected FixingSchedule FixingSchedule { get; set; }



    /// <summary>
    /// Principal ratio
    /// </summary>
    public double EffectiveExchange
    {
      get
      {
        if (EffectiveExchangeOverride.HasValue) return EffectiveExchangeOverride.Value;
        return CalcEffectiveExchange();
      }
      set { EffectiveExchangeOverride = value; }
    }

    /// <summary>
    /// Cap 
    /// </summary>
    public double? Cap { get; set; }

    /// <summary>
    /// Floor
    /// </summary>
    public double? Floor { get; set; }

    /// <summary>
    /// Convexity adjustment 
    /// </summary>
    public virtual double ConvexityAdjustment
    {
      get { return 0.0; }
    }

    /// <summary>
    /// Reference rate fixing for this payment
    /// </summary>
    public double IndexFixing
    {
      get { return RateProjector.Fixing(FixingSchedule).Forward; }
    }

    /// <summary>
    /// Rate reset state
    /// </summary>
    public virtual RateResetState RateResetState
    {
      get
      {
        if (EffectiveExchangeOverride.HasValue)
          return RateResetState.ResetFound;
        return RateProjector.Fixing(FixingSchedule).RateResetState;
      }
    }

    /// <summary>
    /// Computes the reset date for the forward: for rates that are a composition of several projections, 
    /// it returns the last reset date needed to calculate the fixing 
    /// </summary>
    /// <returns>Reset date</returns>
    public Dt ResetDate
    {
      get { return FixingSchedule.ResetDate; }
    }


    /// <summary>
    /// True if payment is projected
    /// </summary>
    public override bool IsProjected
    {
      get
      {
        RateResetState state = RateResetState;
        return (state == RateResetState.IsProjected);
      }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Calculate the effective exchange if projected
    /// </summary>
    /// <returns></returns>
    protected abstract double CalcEffectiveExchange();

    /// <summary>
    /// Overload compute amount function
    /// </summary>
    /// <returns>Amount of the payment</returns>
    protected override double ComputeAmount()
    {
      return Notional * EffectiveExchange;
    }


    /// <summary>
    /// Add Data Columns
    /// </summary>
    /// <param name="collection">DataColumns</param>
    public override void AddDataColumns(DataColumnCollection collection)
    {
      base.AddDataColumns(collection);
      if (!collection.Contains("Notional"))
        collection.Add(new DataColumn("Notional", typeof(double)));
      if (!collection.Contains("Reset Date"))
        collection.Add(new DataColumn("Reset Date", typeof(string)));
      if (!collection.Contains("Index Forward Price"))
        collection.Add(new DataColumn("Index Forward Price", typeof(double)));
      if (!collection.Contains("Forward Price Adj"))
        collection.Add(new DataColumn("Forward Price Adj", typeof(double)));
      if (!collection.Contains("Is Projected"))
        collection.Add(new DataColumn("Is Projected", typeof(bool)));
    }

    /// <summary>
    /// Create Data Values
    /// </summary>
    /// <param name="row">row to add values</param>
    /// <param name="dtFormat">Date format</param>
    public override void AddDataValues(DataRow row, string dtFormat)
    {
      base.AddDataValues(row, dtFormat);
      row["Notional"] = Notional * EffectiveExchange;
      row["Reset Date"] = ResetDate.ToStr(dtFormat);
      row["Index Forward Price"] = IndexFixing;
      row["Forward Price Adj"] = ConvexityAdjustment;
      row["Is Projected"] = IsProjected;
    }

    /// <summary>
    /// Gets reset info for the period
    /// </summary>
    ///<returns>Return a list of reset information (reset date, fixing value, reset state). 
    /// If the state is projected or missing the fixing value is zero by default 
    /// (only found past or overridden fixings are displayed)</returns>
    public List<RateResets.ResetInfo> GetRateResetInfo()
    {
      var resetInfos = new List<RateResets.ResetInfo>();
      if (EffectiveExchangeOverride.HasValue)
      {
        resetInfos.Add(new RateResets.ResetInfo(ResetDate, EffectiveExchangeOverride.Value, RateResetState.ResetFound));
        return resetInfos;
      }
      if (RateProjector == null)
        return null;
      resetInfos.AddRange(RateProjector.GetResetInfo(FixingSchedule));
      return resetInfos;
    }

    #endregion

    #region FloatingPrincipalCashflowNode

    /// <summary>
    /// Floating principal cashflow node
    /// </summary>
    [Serializable]
    protected abstract class FloatingPrincipalCashflowNode : OneTimeCashflowNode
    {
      #region Properties

      /// <summary>
      /// Amount
      /// </summary>
      protected override double Amount
      {
        get { return EffectiveExchange * PrincipalRatio; }
      }

      /// <summary>
      /// Effective exchange
      /// </summary>
      protected abstract double EffectiveExchange { get; }

      /// <summary>
      /// Rate projector
      /// </summary>
      protected IRateProjector RateProjector { get; set; }

      /// <summary>
      /// Cap
      /// </summary>
      protected double? Cap { get; set; }

      /// <summary>
      /// Floor
      /// </summary>
      protected double? Floor { get; set; }

      /// <summary>
      /// Payment principal
      /// </summary>
      private double PrincipalRatio { get; set; }

      /// <summary>
      /// Fixing schedule
      /// </summary>
      protected FixingSchedule FixingSchedule { get; set; }

      #endregion

      #region Constructor

      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="notional">Payment</param>
      /// <param name="payment">Payment</param>
      /// <param name="discountCurve">DiscountCurve</param>
      /// <param name="survivalFunction">Survival function</param>
      protected FloatingPrincipalCashflowNode(FloatingPrincipalExchange payment, double notional, DiscountCurve discountCurve, Func<Dt, double> survivalFunction)
        : base(payment, notional, discountCurve, survivalFunction)
      {
        RateProjector = payment.RateProjector;
        PrincipalRatio = payment.Notional;
        Cap = payment.Cap;
        Floor = payment.Floor;
        FixingSchedule = payment.FixingSchedule;
      }

      #endregion
    }

    #endregion
  }

  #endregion

  #region DividendPayment

  /// <summary>
  /// Type of payment is used as cash dividend distribution
  /// </summary>
  [Serializable]
  public class DividendPayment : OneTimePayment
  {
    /// <summary>
    /// Create a cash dividend payment
    /// </summary>
    /// <param name="payDate">Date of payment</param>
    /// <param name="amount">Amount of payment</param>
    /// <param name="ccy">Currency of payment</param>
    public DividendPayment(Dt payDate, double amount, Currency ccy)
      : base(payDate, amount, ccy)
    { }
  }

  #endregion
}