/*
 * SyntheticCDO.cs
 *
 */

using System;
using System.ComponentModel;

using BaseEntity.Toolkit.Base;
using BaseEntity.Shared;
using System.Collections;

namespace BaseEntity.Toolkit.Products
{

  ///
  /// <summary>
  ///   CPDO Product
  /// </summary>
  ///
  /// <remarks>
  ///   
  /// </remarks>
  ///
  /// 
  [Serializable]
  public class CPDO : Product
  {
    #region Constructors

    /// <summary>
    ///   Default Constructor
    /// </summary>
    ///
    ///
    protected
    CPDO()
    {
      premium_ = 0.0; // initial spread
      floatingRateBond_ = null;
      
      // fees
      gapFee_ = 0;
      adminFee_ = 0;
      arrangementFee_ = 0;
      leverageFacilityFee_ = 0;
     
      initialLeverage_ = 0;
      maxLeverage_ = 0;
      
      cashOutTarget_ = 0; // 0.1 (of notional)
      rebalTarget_ = 0; // 0.25 // Difference between Index Notional and Target Notional

    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>No Fees constructor</para>
    /// </remarks>
    ///
    /// <param name="effectiveDate">Effective date (date premium started accruing)</param>
    /// <param name="maturityDate">Maturity or scheduled termination date</param>
    /// <param name="currency">Currency of premium and recovery payments</param>
    /// <param name="premium">Index spread (bps)</param>
    /// <param name="floatingSpread">Spread over Reference Curve (bps)</param>
    /// <param name="dayCount">Day Count of coupons (bps)</param>
    /// <param name="frequency">Day Count of coupons (bps)</param>
    /// <param name="calendar">Day Count of coupons (bps)</param>
    /// <param name="roll">Day Count of coupons (bps)</param>
    /// <param name="initialLeverage">Initial Leverage</param>
    /// <param name="maxLeverage">Maximum Leverage</param>
    /// <param name="cashOutTarget">Cash-out target value</param>
    /// <param name="rebalTarget">Rebalancing Target</param>
    /// <param name="gapFee">Gap Fee</param>
    /// <param name="adminFee">Admin Fee</param>
    ///
    public
    CPDO(Dt effectiveDate,
                  Dt maturityDate,
                  Currency currency,
                  double premium,
                  double floatingSpread,
                  DayCount dayCount,
                  Frequency frequency,
                  Calendar calendar,
                  BDConvention roll, 
                  double initialLeverage,
                  double maxLeverage,
                  double cashOutTarget,
                  double rebalTarget,
                  double gapFee,
                  double adminFee
        
        )
      : base(effectiveDate, maturityDate, currency)
    {
      // Use properties for assignment for data validation
      Premium = premium;
      FloatingSpread = floatingSpread;
      DayCount = dayCount;
      Freq = frequency;
      Calendar = calendar;
      BDConvention = roll;

      // construct floating rate bond
      Bond floatingRateBond = new Bond(effectiveDate, maturityDate,
                         currency, BondType.None, floatingSpread / 10000.0, dayCount, CycleRule.None,
                         frequency, roll, calendar);
      // assign to data member
      floatingRateBond.Index = "LIBOR";
      FloatingRateBond = floatingRateBond;

      // Construct CDX Note;
      CDX underlyingCompositeIndex = new CDX(effectiveDate, maturityDate, currency,
                          premium / 10000.0, dayCount,
                          frequency, roll, calendar);
      // assign to data member
      UnderlyingCompositeIndex = underlyingCompositeIndex;
      
      InitialLeverage = initialLeverage;
      MaxLeverage = maxLeverage;
      RebalTarget = rebalTarget;
      CashOutTarget = cashOutTarget; 
      
      // fees
      GapFee = gapFee/10000.0;
      AdminFee = adminFee/10000.0;
      ArrangementFee = 0;
      LeverageFacilityFee = 0;
      
    }

    /// <summary>
    ///   Constructor
    /// </summary>
    ///
    /// <remarks>
    ///   <para>Constructor including fees</para>
    /// </remarks>
    ///
    /// <param name="effectiveDate">Effective date (date premium started accruing)</param>
    /// <param name="maturityDate">Maturity or scheduled termination date</param>
    /// <param name="currency">Currency of premium and recovery payments</param>
    /// <param name="premium">Deal Premium (bps)</param>
    /// <param name="floatingSpread">Spread over Reference Curve (bps)</param>
    /// <param name="dayCount">Day Count of coupons (bps)</param>
    /// <param name="frequency">Frequency of coupons (bps)</param>
    /// <param name="calendar">Calendar of coupons (bps)</param>
    /// <param name="roll">BDConvention of coupons (bps)</param>
    /// <param name="initialLeverage">Initial Leverage</param>
    /// <param name="maxLeverage">Maximum Leverage</param>
    /// <param name="cashOutTarget">Cash-out target value</param>
    /// <param name="rebalTarget">Rebalancing Target</param>
    /// <param name="gapFee">Gap Fee</param>
    /// <param name="adminFee">Administrative Fee</param>
    /// <param name="arrangementFee">Arrangement Fee</param>
    /// <param name="leverageFacilityFee">Levearge facility Fee</param>
    ///
    public
    CPDO(Dt effectiveDate,
                  Dt maturityDate,
                  Currency currency,
                  double premium,
                  double floatingSpread,
                  DayCount dayCount,
                  Frequency frequency,
                  Calendar calendar,
                  BDConvention roll,     
                  double initialLeverage,
                  double maxLeverage,
                  double cashOutTarget,
                  double rebalTarget,           
                  double gapFee,
                  double adminFee,
                  double arrangementFee,
                  double leverageFacilityFee)
      : base(effectiveDate, maturityDate, currency)
    {
      // Use properties for assignment for data validation
        Premium = premium;
        FloatingSpread = floatingSpread;
        DayCount = dayCount;
        Freq = frequency;
        Calendar = calendar;
        BDConvention = roll;


      // construct floating rate bond
      Bond floatingRateBond = new Bond(effectiveDate, maturityDate,
                         currency, BondType.None, floatingSpread / 10000.0, dayCount, CycleRule.None,
                         frequency, roll, calendar);
      // assign to data member
      FloatingRateBond = floatingRateBond;

      // Construct CDX Note;
      CDX underlyingCompositeIndex = new CDX(effectiveDate, maturityDate, currency,
                          premium / 10000.0, dayCount,
                          frequency, roll, calendar);
      // assign to data member
      UnderlyingCompositeIndex = underlyingCompositeIndex;

      InitialLeverage = initialLeverage;
      MaxLeverage = maxLeverage;
      RebalTarget = rebalTarget;
      
      // fees
      GapFee = GapFee;
      AdminFee = adminFee;
      ArrangementFee = arrangementFee;
      LeverageFacilityFee = leverageFacilityFee;

    }

    #endregion Constructors

    #region Methods

    /// <summary>
    ///   Validate product
    /// </summary>
    ///
    /// <remarks>
    ///   This tests only relationships between fields of the product that
    ///   cannot be validated in the property methods.
    /// </remarks>
    ///
    public override void Validate(ArrayList errors)
    {
      base.Validate(errors);

      // Premium has to be within a [0.0 - 200.0] range
      if (premium_ < 0.0 || premium_ > 200.0)
        InvalidValue.AddError(errors, this, "Premium", String.Format("Invalid premium. Must be between 0 and 200, Not {0}", premium_));

      // Floating Spread has to positive
      if (floatingSpread_ < 0.0)
        InvalidValue.AddError(errors, this, "FloatingSpread", String.Format("Invalid Floating Spread. Must be +Ve, Not {0}", floatingSpread_));

      return;
    }

    /// <summary>
    ///   Calculate the default first premium payment date.
    /// </summary>
    ///
    /// <remarks>
    ///   <para>This is the earlier date of CDSRoll(effective) and maturity.</para>
    /// </remarks>
    ///
    /// <param name="effective">Effective date</param>
    /// <param name="maturity">Maturity date</param>
    ///
    /// <returns>First premium date</returns>
    ///
    public static Dt GetDefaultFirstPremiumDate(Dt effective, Dt maturity)
    {
      Dt dt = Dt.CDSRoll(effective, false);
      if (maturity < dt)
        return maturity;
      return dt;
    }

    #endregion Methods

    #region Properties

    /// <summary>
    ///   premium as a number (100bp = 0.01).
    /// </summary>
    [Category("Base")]
    public double Premium
    {
      get { return premium_; }
      set { premium_ = value; }
    }

    /// <summary>
    ///   Floating Spread in bps 
    /// </summary>
    [Category("Base")]
    public double FloatingSpread
    {
      get { return floatingSpread_; }
      set { floatingSpread_ = value; }
    }

    /// <summary>
    ///   Daycount of premium
    /// </summary>
    [Category("Base")]
    public DayCount DayCount
    {
      get { return dayCount_; }
      set { dayCount_ = value; }
    }

    /// <summary>
    ///   Premium payment frequency (per year)
    /// </summary>
    [Category("Base")]
    public Frequency Freq
    {
      get { return freq_; }
      set { freq_ = value; }
    }
    
    /// <summary>
    ///   Calendar for premium payment schedule
    /// </summary>
    [Category("Base")]
    public Calendar Calendar
    {
        get { return cal_; }
        set { cal_ = value; }
    }

    /// <summary>
    ///   Roll convention for premium payment schedule
    /// </summary>
    [Category("Base")]
    public BDConvention BDConvention
    {
        get { return roll_; }
        set { roll_ = value; }
    }

    /// <summary>
    ///   Floating Rate Bond
    /// </summary>
    [Category("Base")]
    public Bond FloatingRateBond
    {
      get { return floatingRateBond_; }
      set { floatingRateBond_ = value; }
    }
    
    /// <summary>
    ///   Underlying Index    
    /// </summary>
    [Category("Base")]
    public CDX UnderlyingCompositeIndex
    {
      get { return underlyingCompositeIndex_; }
      set { underlyingCompositeIndex_ = value; }
    }

    /// <summary>
    ///   Gap Fee
    /// </summary>
    [Category("Base")]
    public double GapFee
    {
      get { return gapFee_; }
      set { gapFee_ = value; }
    }

    /// <summary>
    ///   Admin Fee
    /// </summary>
    [Category("Base")]
    public double AdminFee
    {
      get { return adminFee_; }
      set { adminFee_ = value; }
    }

    /// <summary>
    ///   Arrangement Fee
    /// </summary>
    [Category("Base")]
    public double ArrangementFee
    {
      get { return arrangementFee_; }
      set { arrangementFee_ = value; }
    }

    /// <summary>
    ///   Arrangement Fee
    /// </summary>
    [Category("Base")]
    public double LeverageFacilityFee
    {
      get { return leverageFacilityFee_; }
      set { leverageFacilityFee_ = value; }
    }

    /// <summary>
    ///  Initial (starting) Leverage
    /// </summary>
    [Category("Base")]
    public double InitialLeverage
    {
      get { return initialLeverage_; }
      set { initialLeverage_ = value; }
    }

    /// <summary>
    ///   Maximum Leverage
    /// </summary>
    [Category("Base")]
     public double MaxLeverage
    {
      get { return maxLeverage_; }
      set { maxLeverage_ = value; }
    }

    /// <summary>
    ///   Cash-Out target value (as a percentage of notional)
    /// </summary>
    [Category("Base")]
    public double CashOutTarget
    {
      get { return cashOutTarget_; }
      set { cashOutTarget_ = value; }
    }

    /// <summary>
    ///   Rebalancing Target (as a percentage of notional)
    /// </summary>
    [Category("Base")]
    public double RebalTarget
    {
      get { return rebalTarget_; }
      set { rebalTarget_ = value; }
    }

    #endregion Properties

    #region Data

    private double gapFee_;
    private double adminFee_;
    private double arrangementFee_;
    private double leverageFacilityFee_;

    private double initialLeverage_;
    private double maxLeverage_;
    private double cashOutTarget_; // 0.1 (of notional)
    private double rebalTarget_; // 0.25 // Difference between Index Notional and Target Notional
    
    private double premium_;// deal premium (average of cdx and itraxx)
    private double floatingSpread_;
    private DayCount dayCount_;
    private Frequency freq_;
    private Calendar cal_;
    private BDConvention roll_;

    private Bond floatingRateBond_;
    private CDX underlyingCompositeIndex_;
    
    #endregion Data

  } // class CPDO
}
