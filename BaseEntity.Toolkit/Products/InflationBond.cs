using System;
using System.Collections.Generic;
using System.ComponentModel;
using BaseEntity.Toolkit.Base.ReferenceIndices;
using BaseEntity.Toolkit.Base;
using BaseEntity.Toolkit.Cashflows;
using BaseEntity.Toolkit.Models;

namespace BaseEntity.Toolkit.Products
{
  /// <summary>
  /// Government inflation bond
  /// </summary>
  [Serializable]
  public class InflationBond : ProductWithSchedule
  {
    /// <summary>
    /// Component of a schedule of accreting principal repayments on each unit notional
    /// </summary>
    [Serializable]
    public struct Accretion
    {
      /// <summary>
      /// Payment date
      /// </summary>
      public Dt Date;
      /// <summary>
      /// Accretion amount. If null, the amount is projected from the InflationCurve
      /// </summary>
      public double? Amount;
    }

    /// <summary>
    /// Generate a principal accretion re-payment schedule. Non-zero amounts are considered as overrides.
    /// </summary>
    /// <param name="dates">Accreting principal repayment dates</param>
    /// <param name="amounts">Accreting principal amounts per dollar notional</param>
    /// <returns>Accretion schedule</returns>
    public static List<Accretion> GenerateAccretionSchedule(Dt[] dates, double[] amounts)
    {
      var retVal = new List<Accretion>(dates.Length);
      for (int i = 0; i < dates.Length; ++i)
      {
        var accretion = new Accretion
                          {
                            Date = dates[i]
                          };
        if (amounts[i] != 0.0)
          accretion.Amount = amounts[i];
        retVal.Add(accretion);
      }
      return retVal;
    }


    #region Constructor

    /// <summary>
    ///   Constructor for inflation bond
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Sets default first and last coupon payment dates based on bond maturity and coupon
    ///   payment frequency.</para>
    /// </remarks>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    /// <param name="ccy">Currency</param>
    /// <param name="coupon">Coupon of bond</param>
    /// <param name="bondType">Bond type </param>
    /// <param name="dayCount">Daycount of coupon</param>
    /// <param name="cycleRule">End-of-month rule</param>
    /// <param name="freq">Payment frequency of coupon</param>
    /// <param name="roll">Coupon roll method (business day convention)</param>
    /// <param name="calendar">Calendar for coupon rolls</param>
    /// <param name="referenceIndex">Inflation reference index</param>
    /// <param name="resetLag">Lag between publication of the reset and reset date</param>
    /// <param name="baseInflation">Anchor inflation for notional adjustment</param>
    public InflationBond(
      Dt effective, Dt maturity, Currency ccy, BondType bondType, double coupon, DayCount dayCount, CycleRule cycleRule, Frequency freq, BDConvention roll, Calendar calendar,
      InflationIndex referenceIndex, double baseInflation, Tenor resetLag)
      : base(effective, maturity, Dt.Empty, Dt.Empty, ccy, freq, roll, calendar, cycleRule, CashflowFlag.AccrueOnCycle)
    {
      DayCount = dayCount;
      Coupon = coupon;
      BondType = bondType;
      ReferenceIndex = referenceIndex;
      BaseInflation = baseInflation;
      ResetLag = resetLag;
      ProjectionType = ProjectionType.InflationForward;
      FlooredNotional = true;
    }

    #endregion

    #region Properties
    /// <summary>
    /// Indexation method 
    /// </summary>
    [Category("Base")]
    public IndexationMethod IndexationMethod { get; set; }
    
    /// <summary>
    /// True if bond is interest only
    /// </summary>
    [Category("Base")]
    public bool InterestOnly { get; set; }
    
    
    /// <summary>
    /// Inflation bond type
    /// </summary>
    [Category("Base")]
    public SpreadType SpreadType { get; set; }
    
    
    /// <summary>
    /// Fixed nominal coupon
    /// </summary>
    [Category("Base")]
    public double Coupon { get; set; }

    /// <summary>
    /// Daycount convention
    /// </summary>
    [Category("Base")]
    public DayCount DayCount { get; set; }


    /// <summary>
    /// Indexation lag between payment date and fixing of the level of the reference index. It only applies to bonds with floating notional or coupon. 
    /// </summary>
    [Category("Base")]
    public Tenor ResetLag { get; set; }


    /// <summary>
    /// Floored notional
    /// </summary>
    [Category("Base")]
    public bool FlooredNotional{ get; set;}
   
    /// <summary>
    /// Cap over floating coupon. Null if coupon is not capped
    /// </summary>
    [Category("Base")]
    public double? Cap { get; set; }

    /// <summary>
    /// Floor on floating coupon. Null if coupon is not floored
    /// </summary>
    public double? Floor { get; set; }

    /// <summary>
    /// Inflation reference level for notional adjustments
    /// </summary>
    [Category("Base")]
    public double BaseInflation { get; set; }

    /// <summary>
    ///  Reference index.
    ///  </summary>
    [Category("Base")]
    public ReferenceIndex ReferenceIndex { get; private set; }

    /// <summary>
    /// Bond type
    /// </summary>
    [Category("Base")]
    public BondType BondType { get; private set; }

    /// <summary>
    /// Projection type
    /// </summary>
    [Category("Base")]
    public ProjectionType ProjectionType
    {
      get; set;
    }

    ///<summary>
    /// The details of bond ex-div schedule parameters
    ///</summary>
    public ExDivRule BondExDivRule { get; set; }

    /// <summary>
    ///   Amortization schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof (ExpandableObjectConverter))]
    public IList<Amortization> AmortizationSchedule
    {
      get { return amortSched_ ?? (amortSched_ = new List<Amortization>()); }
    }

    /// <summary>
    ///   True if bond amortizes
    /// </summary>
    [Category("Schedule")]
    public bool Amortizes
    {
      get { return !(amortSched_ == null || amortSched_.Count == 0); }
    }

    /// <summary>
    ///    Coupon schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof (ExpandableObjectConverter))]
    public IList<CouponPeriod> CouponSchedule
    {
      get { return couponSched_ ?? (couponSched_ = new List<CouponPeriod>()); }
    }

    /// <summary>
    ///   True if bond has coupon schedule
    /// </summary>
    [Category("Schedule")]
    public bool StepUp
    {
      get { return !(couponSched_ == null || couponSched_.Count == 0); }
    }


    /// <summary>
    ///    Accretion schedule
    /// </summary>
    [Category("Schedule")]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public IList<Accretion> AccretionSchedule
    {
      get { return accretionSched_ ?? (accretionSched_ = new List<Accretion>()); }
    }

    /// <summary>
    ///   True if bond has coupon schedule
    /// </summary>
    [Category("Schedule")]
    public bool Accretes
    {
      get { return !(accretionSched_ == null || accretionSched_.Count == 0); }
    }

    #endregion

    #region Methods

    /// <summary>
    ///  Returns true if bond is cum-dividend
    /// </summary>
    /// <param name="settle"></param>
    /// <param name="tradeSettle"></param>
    /// <returns>True if bond is cum-dividend</returns>
    public bool CumDiv(Dt settle, Dt tradeSettle)
    {
      var nextCpnDate = Schedule.GetNextCouponDate(settle);
      Dt exDivDate = nextCpnDate.IsEmpty() ? Dt.Empty
        : (BondExDivRule == null
            ? BondModelUtil.ExDivDate(BondType, nextCpnDate)
            : BondModelUtil.ExDivDate(BondExDivRule, Calendar, nextCpnDate));
      return (!tradeSettle.IsEmpty()) && (!exDivDate.IsEmpty()) && (tradeSettle < exDivDate);
    }

    /// <summary>
    /// Next coupon date after a date
    /// </summary>
    /// <param name="settle">The date</param>
    /// <returns></returns>
    public Dt NextCouponDate(Dt settle)
    {
      return Schedule.NextCouponDate(ScheduleParams, settle);
    }

    #endregion

    #region Data

    private List<Accretion> accretionSched_;
    private List<Amortization> amortSched_;
    private List<CouponPeriod> couponSched_;
    #endregion
  }

}