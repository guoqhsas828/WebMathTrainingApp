
using System;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Curves;
using BaseEntity.Toolkit.Products;

namespace BaseEntity.Toolkit.Pricers
{
  /// <summary>
  /// Pricer for MMD Rate lock.
  /// </summary>
  [Serializable]
  public partial class MmdRateLockPricer : PricerBase, IPricer<MmdRateLock>
  {

    #region Constructor

    /// <summary>
    /// Constructor for MMD rate lock pricer.
    /// </summary>
    /// <param name="mmdRateLock">MMD rate lock</param>
    /// <param name="asOf">pricing as-of date</param>
    /// <param name="forwardDeterminationDate">Forward determination date</param>
    /// <param name="notional">Trade notional</param>
    /// <param name="bondYield">Underlying bond yield</param>
    /// <param name="midLevel">Quoted mid level</param>
    public MmdRateLockPricer(MmdRateLock mmdRateLock, Dt asOf, 
      Dt forwardDeterminationDate, double bondYield, 
      double midLevel, double notional)
      : base(mmdRateLock, asOf, asOf)
    {
      BondYield = bondYield;
      MidLevel = midLevel;
      ForwardDeterminationDate = forwardDeterminationDate;
      Notional = notional;
      RateResets = new RateResets();
    }

    #endregion Constructor

    #region Methods
    /// <summary>
    /// Calculate the model present value of the contract
    /// </summary>
    /// <returns>Model present value</returns>
    public override double Pv()
    {
      double pv = IsTerminated ? 0.0 : ProductPv();
      pv += PaymentPv();
      return pv;
    }

    /// <summary>
    /// Calculate the present value of a MMD rate lock.
    /// </summary>
    /// <returns>Pv</returns>
    public override double ProductPv()
    {
      return (MmdRateLock.FixedRate - AdjustedMid)*MmdRateLock.Dv01*Notional;
    }

    /// <summary>
    ///   Get Payment Schedule for this product from the specified date
    /// </summary>
    /// <remarks>
    ///   <para>Derived pricers may implement this, otherwise a NotImplementedException is thrown.</para>
    /// </remarks>
    /// <param name="ps">Payment schedule</param>
    /// <param name="from">Date to generate Payment Schedule from</param>
    /// <returns>PaymentSchedule from the specified date or null if not supported</returns>
    public override PaymentSchedule GetPaymentSchedule(PaymentSchedule ps, Dt from)
    {
      if (from > Product.Maturity)
        return ps ?? new PaymentSchedule();

      return MmdRateLock.GetPaymentSchedule(AsOf, RateResets, new InterestRateIndex("", MmdRateLock.ContractTenor, MmdRateLock.Ccy,
        MmdRateLock.DayCount, Calendar.None, BDConvention.None, MmdRateLock.ResetOffsetDays));
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
            paymentPricer_ = BuildPaymentPricer(Payment, new DiscountCurve(AsOf, 0.0));
        }
        return paymentPricer_;
      }
    }

    #endregion Methods

    #region  Properties

    /// <summary>
    /// Product of MMD rate lock  
    /// </summary>
    public MmdRateLock MmdRateLock
    {
      get{ return (MmdRateLock) Product; }
    }

    /// <summary>
    /// The underlying bond yield of MMD rate lock.
    /// </summary>
    public double BondYield { get; set; }

    /// <summary>
    /// Quoted mid level
    /// </summary>
    public double MidLevel { get; private set; }

    /// <summary>
    /// The forward determination date
    /// </summary>
    public Dt ForwardDeterminationDate { get; private set; }

    /// <summary>
    /// The spread, which equals to midLevel minus bond yield.
    /// </summary>
    public double Spread
    {
      get { return MidLevel - BondYield; }
    }

    /// <summary>
    /// The daily, defined by "Spread" divided by number of days 
    /// implied in the "contract". The numbers of days in the  
    /// following "contracts" are
    /// "3m" is 90 days;
    /// "6m" is 180 days;
    /// "9m" is 270 days
    /// </summary>
    public double DailySpreadAllocation
    {
      get { return Spread/MmdRateLock.ContractDays; }
    }

    /// <summary>
    /// Mid adjustment, which equals to "DailySpreadAllocation" 
    /// multiplies the days between the "DeterminationDate" and 
    /// the "ForwardDeterminationDate"
    /// </summary>
    public double MidAdjustment
    {
      get
      {
        return DailySpreadAllocation*Dt.Diff(MmdRateLock.DeterminationDate,
          ForwardDeterminationDate, MmdRateLock.DayCount);
      }
    }

    /// <summary>
    /// Adjusted Mid level, equaling to quoted mid level minus mid adjustment.
    /// </summary>
    public double AdjustedMid
    {
      get { return MidLevel - MidAdjustment; }
    }

    MmdRateLock IPricer<MmdRateLock>.Product
    {
      get
      {
        return (MmdRateLock) Product;
      }
    }

    /// <summary>
    ///   Historical floating rate coupons
    /// </summary>
    public RateResets RateResets { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public DiscountCurve DiscountCurve {
      get { return null; }
    }

    #endregion Properties
  }
}
